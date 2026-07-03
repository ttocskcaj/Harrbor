using FakeItEasy;
using FluentAssertions;
using Harrbor.Configuration;
using Harrbor.Configuration.Connections;
using Harrbor.Data.Entities;
using Harrbor.Services.Configuration;
using Harrbor.Services.Orchestration;
using Harrbor.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Harrbor.Tests.Services.Orchestration;

public class OrchestrationWorkerTests : IDisposable
{
    private readonly SqliteDbContextFactory _dbFactory = new();
    private readonly ConnectionStore _store;
    private readonly IJobCycleRunner _runner = A.Fake<IJobCycleRunner>();
    private readonly FakeTimeProvider _time = new(DateTimeOffset.Parse("2026-07-03T10:00:00Z"));
    private readonly ServiceProvider _serviceProvider;
    private readonly OrchestrationWorker _worker;

    public OrchestrationWorkerTests()
    {
        _store = new ConnectionStore(_dbFactory);

        _serviceProvider = new ServiceCollection()
            .AddSingleton(_runner)
            .BuildServiceProvider();

        _worker = new OrchestrationWorker(
            _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            _dbFactory,
            _store,
            _time,
            A.Fake<ILogger<OrchestrationWorker>>());
    }

    public void Dispose()
    {
        _serviceProvider.Dispose();
        _dbFactory.Dispose();
    }

    private async Task ConfigureAllConnectionsAsync()
    {
        await _store.SaveAsync(new QBittorrentSettings { BaseUrl = "http://qbt:8080" });
        await _store.SaveAsync(new RemoteStorageSettings { Host = "seedbox", User = "u" });
        await _store.SaveAsync(new SonarrSettings { BaseUrl = "http://sonarr:8989", ApiKey = "key", Enabled = true });
        await _store.SaveAsync(new RadarrSettings { BaseUrl = "http://radarr:7878", ApiKey = "key", Enabled = true });
    }

    private async Task<Job> SeedJobAsync(Action<Job>? customize = null)
    {
        var job = new Job
        {
            Name = "tv-shows",
            Enabled = true,
            ServiceType = ServiceType.Sonarr,
            PollingInterval = TimeSpan.FromSeconds(60),
            RemotePath = "/remote/tv",
            StagingPath = "/staging/tv"
        };
        customize?.Invoke(job);

        await using var db = _dbFactory.CreateDbContext();
        db.Jobs.Add(job);
        await db.SaveChangesAsync();
        return job;
    }

    [Fact]
    public async Task Tick_RunsDueJob_AndStampsLastRun()
    {
        await ConfigureAllConnectionsAsync();
        var job = await SeedJobAsync();

        await _worker.RunSchedulerTickAsync(CancellationToken.None);
        await _worker.WaitForRunningCyclesAsync();

        A.CallTo(() => _runner.RunAsync(
                A<JobDefinition>.That.Matches(d => d.Name == "tv-shows"),
                A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();

        await using var db = _dbFactory.CreateDbContext();
        var stored = await db.Jobs.SingleAsync(j => j.Id == job.Id);
        stored.LastRunAtUtc.Should().Be(_time.GetUtcNow().UtcDateTime);
    }

    [Fact]
    public async Task Tick_SkipsJobThatIsNotDueYet()
    {
        await ConfigureAllConnectionsAsync();
        await SeedJobAsync(j => j.LastRunAtUtc = _time.GetUtcNow().UtcDateTime.AddSeconds(-10));

        await _worker.RunSchedulerTickAsync(CancellationToken.None);
        await _worker.WaitForRunningCyclesAsync();

        A.CallTo(() => _runner.RunAsync(A<JobDefinition>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task Tick_RunsJobAgain_AfterIntervalElapses()
    {
        await ConfigureAllConnectionsAsync();
        await SeedJobAsync(j => j.LastRunAtUtc = _time.GetUtcNow().UtcDateTime.AddSeconds(-10));

        await _worker.RunSchedulerTickAsync(CancellationToken.None);
        _time.Advance(TimeSpan.FromSeconds(55));
        await _worker.RunSchedulerTickAsync(CancellationToken.None);
        await _worker.WaitForRunningCyclesAsync();

        A.CallTo(() => _runner.RunAsync(A<JobDefinition>._, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Tick_SkipsDisabledJob()
    {
        await ConfigureAllConnectionsAsync();
        await SeedJobAsync(j => j.Enabled = false);

        await _worker.RunSchedulerTickAsync(CancellationToken.None);
        await _worker.WaitForRunningCyclesAsync();

        A.CallTo(() => _runner.RunAsync(A<JobDefinition>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task Tick_DoesNotOverlapCyclesForTheSameJob()
    {
        await ConfigureAllConnectionsAsync();
        var job = await SeedJobAsync();

        var cycleBlocker = new TaskCompletionSource();
        A.CallTo(() => _runner.RunAsync(A<JobDefinition>._, A<CancellationToken>._))
            .ReturnsLazily(() => cycleBlocker.Task);

        await _worker.RunSchedulerTickAsync(CancellationToken.None);

        // Force the job to look due again while its first cycle is still running
        await using (var db = _dbFactory.CreateDbContext())
        {
            var stored = await db.Jobs.SingleAsync(j => j.Id == job.Id);
            stored.LastRunAtUtc = null;
            await db.SaveChangesAsync();
        }

        await _worker.RunSchedulerTickAsync(CancellationToken.None);

        A.CallTo(() => _runner.RunAsync(A<JobDefinition>._, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();

        cycleBlocker.SetResult();
        await _worker.WaitForRunningCyclesAsync();

        // Completed cycle is pruned; the still-due job runs on the next tick
        await _worker.RunSchedulerTickAsync(CancellationToken.None);
        await _worker.WaitForRunningCyclesAsync();

        A.CallTo(() => _runner.RunAsync(A<JobDefinition>._, A<CancellationToken>._))
            .MustHaveHappenedTwiceExactly();
    }

    [Fact]
    public async Task Tick_SkipsJob_WhenRequiredConnectionsUnconfigured()
    {
        // No connections saved at all
        var job = await SeedJobAsync();

        await _worker.RunSchedulerTickAsync(CancellationToken.None);
        await _worker.WaitForRunningCyclesAsync();

        A.CallTo(() => _runner.RunAsync(A<JobDefinition>._, A<CancellationToken>._))
            .MustNotHaveHappened();

        // A skipped job never ran — LastRunAtUtc must not pretend it did
        await using var db = _dbFactory.CreateDbContext();
        (await db.Jobs.SingleAsync(j => j.Id == job.Id)).LastRunAtUtc.Should().BeNull();
    }

    [Fact]
    public async Task Tick_RunsSkippedJobImmediately_OnceConnectionsConfigured()
    {
        await SeedJobAsync();

        await _worker.RunSchedulerTickAsync(CancellationToken.None);
        A.CallTo(() => _runner.RunAsync(A<JobDefinition>._, A<CancellationToken>._))
            .MustNotHaveHappened();

        await ConfigureAllConnectionsAsync();
        _time.Advance(TimeSpan.FromSeconds(5));

        await _worker.RunSchedulerTickAsync(CancellationToken.None);
        await _worker.WaitForRunningCyclesAsync();

        A.CallTo(() => _runner.RunAsync(A<JobDefinition>._, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Tick_SkipsJob_WhenItsServarrConnectionIsDisabled()
    {
        await ConfigureAllConnectionsAsync();
        await _store.SaveAsync(new SonarrSettings { BaseUrl = "http://sonarr:8989", ApiKey = "key", Enabled = false });
        await SeedJobAsync(j => j.ServiceType = ServiceType.Sonarr);

        await _worker.RunSchedulerTickAsync(CancellationToken.None);
        await _worker.WaitForRunningCyclesAsync();

        A.CallTo(() => _runner.RunAsync(A<JobDefinition>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task Tick_PicksUpNewlyAddedJob()
    {
        await ConfigureAllConnectionsAsync();

        await _worker.RunSchedulerTickAsync(CancellationToken.None);
        A.CallTo(() => _runner.RunAsync(A<JobDefinition>._, A<CancellationToken>._))
            .MustNotHaveHappened();

        await SeedJobAsync(j => j.Name = "movies");

        await _worker.RunSchedulerTickAsync(CancellationToken.None);
        await _worker.WaitForRunningCyclesAsync();

        A.CallTo(() => _runner.RunAsync(
                A<JobDefinition>.That.Matches(d => d.Name == "movies"),
                A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Tick_SurvivesRunnerExceptions()
    {
        await ConfigureAllConnectionsAsync();
        await SeedJobAsync();

        A.CallTo(() => _runner.RunAsync(A<JobDefinition>._, A<CancellationToken>._))
            .ThrowsAsync(new InvalidOperationException("boom"));

        await _worker.RunSchedulerTickAsync(CancellationToken.None);

        var flush = async () => await _worker.WaitForRunningCyclesAsync();
        await flush.Should().NotThrowAsync();
    }
}
