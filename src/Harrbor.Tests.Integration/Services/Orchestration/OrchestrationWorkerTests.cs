using FakeItEasy;
using FluentAssertions;
using Harrbor.Configuration;
using Harrbor.Data.Entities;
using Harrbor.Services.Configuration;
using Harrbor.Services.Orchestration;
using Harrbor.Tests.Integration.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Harrbor.Tests.Integration.Services.Orchestration;

public class OrchestrationWorkerTests : IDisposable
{
    private readonly SqliteDbContextFactory _dbFactory = new();
    private readonly ConnectionStore _store;
    private readonly IJobCycleRunner _runner = A.Fake<IJobCycleRunner>();
    private readonly FakeTimeProvider _time = new(DateTimeOffset.Parse("2026-07-03T10:00:00Z"));
    private readonly ServiceProvider _serviceProvider;
    private readonly OrchestrationWorker _worker;

    private int _sonarrId;
    private int _radarrId;
    private int _qbitId;
    private int _remoteId;

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

    private async Task<int> AddConnectionAsync(string providerKey, string name, bool enabled = true)
        => (await _store.CreateAsync(new Connection
        {
            ProviderKey = providerKey,
            Name = name,
            Enabled = enabled,
            SettingsJson = "{}",
        })).Id;

    private async Task ConfigureAllConnectionsAsync()
    {
        _qbitId = await AddConnectionAsync("qbittorrent", "qbit");
        _remoteId = await AddConnectionAsync("rclone-sftp", "remote");
        _sonarrId = await AddConnectionAsync("sonarr", "sonarr");
        _radarrId = await AddConnectionAsync("radarr", "radarr");
    }

    private async Task<Job> SeedJobAsync(Action<Job>? customize = null)
    {
        var job = new Job
        {
            Name = "tv-shows",
            Enabled = true,
            MediaServiceConnectionId = _sonarrId,
            DownloadClientConnectionId = _qbitId,
            RemoteStorageConnectionId = _remoteId,
            PollingInterval = TimeSpan.FromSeconds(60),
            RemotePath = "/remote/tv",
            StagingPath = "/staging/tv",
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
    public async Task Tick_SkipsJob_WhenAConnectionIsDisabled()
    {
        _qbitId = await AddConnectionAsync("qbittorrent", "qbit");
        _remoteId = await AddConnectionAsync("rclone-sftp", "remote");
        _sonarrId = await AddConnectionAsync("sonarr", "sonarr", enabled: false); // disabled
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
    public async Task Tick_RunsSkippedJobImmediately_OnceConnectionEnabled()
    {
        _qbitId = await AddConnectionAsync("qbittorrent", "qbit");
        _remoteId = await AddConnectionAsync("rclone-sftp", "remote");
        _sonarrId = await AddConnectionAsync("sonarr", "sonarr", enabled: false);
        await SeedJobAsync();

        await _worker.RunSchedulerTickAsync(CancellationToken.None);
        A.CallTo(() => _runner.RunAsync(A<JobDefinition>._, A<CancellationToken>._))
            .MustNotHaveHappened();

        await _store.UpdateAsync(_sonarrId, "sonarr", enabled: true, "{}");
        _time.Advance(TimeSpan.FromSeconds(5));

        await _worker.RunSchedulerTickAsync(CancellationToken.None);
        await _worker.WaitForRunningCyclesAsync();

        A.CallTo(() => _runner.RunAsync(A<JobDefinition>._, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Tick_RunsOnlyTheJobWhoseConnectionsAreEnabled()
    {
        // Sonarr enabled, Radarr disabled — the Sonarr job runs and the Radarr job is skipped cleanly,
        // proving jobs no longer require every *arr app to be enabled.
        _qbitId = await AddConnectionAsync("qbittorrent", "qbit");
        _remoteId = await AddConnectionAsync("rclone-sftp", "remote");
        _sonarrId = await AddConnectionAsync("sonarr", "sonarr");
        _radarrId = await AddConnectionAsync("radarr", "radarr", enabled: false);

        await SeedJobAsync(j => { j.Name = "tv"; j.MediaServiceConnectionId = _sonarrId; });
        await SeedJobAsync(j => { j.Name = "movies"; j.MediaServiceConnectionId = _radarrId; });

        await _worker.RunSchedulerTickAsync(CancellationToken.None);
        await _worker.WaitForRunningCyclesAsync();

        A.CallTo(() => _runner.RunAsync(A<JobDefinition>.That.Matches(d => d.Name == "tv"), A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => _runner.RunAsync(A<JobDefinition>.That.Matches(d => d.Name == "movies"), A<CancellationToken>._))
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
