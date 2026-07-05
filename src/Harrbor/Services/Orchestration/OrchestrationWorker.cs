using Harrbor.Configuration;
using Harrbor.Data;
using Harrbor.Data.Entities;
using Harrbor.Services.Configuration;
using Microsoft.EntityFrameworkCore;

namespace Harrbor.Services.Orchestration;

/// <summary>
/// Single scheduler loop over the DB-backed job list. Jobs are re-read every tick,
/// so create/edit/enable/disable and interval changes apply without a restart.
/// A job is due when its polling interval has elapsed since its last run; cycles
/// for the same job never overlap.
/// </summary>
public class OrchestrationWorker : BackgroundService
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(5);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IDbContextFactory<HarrborDbContext> _dbContextFactory;
    private readonly IConnectionStore _connectionStore;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<OrchestrationWorker> _logger;

    // Touched only from the scheduler loop (and shutdown after the loop exits)
    private readonly Dictionary<int, Task> _runningCycles = [];
    private readonly Dictionary<int, DateTime> _lastUnconfiguredWarnings = [];
    private int _lastEnabledJobCount = -1;

    public OrchestrationWorker(
        IServiceScopeFactory scopeFactory,
        IDbContextFactory<HarrborDbContext> dbContextFactory,
        IConnectionStore connectionStore,
        TimeProvider timeProvider,
        ILogger<OrchestrationWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _dbContextFactory = dbContextFactory;
        _connectionStore = connectionStore;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "OrchestrationWorker starting; scheduling jobs from the database every {TickInterval}",
            TickInterval);

        using var timer = new PeriodicTimer(TickInterval, _timeProvider);

        try
        {
            do
            {
                try
                {
                    await RunSchedulerTickAsync(stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Scheduler tick failed");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
            // shutdown requested
        }
        finally
        {
            await WaitForRunningCyclesAsync();
            _logger.LogInformation("OrchestrationWorker stopping");
        }
    }

    internal async Task RunSchedulerTickAsync(CancellationToken cancellationToken)
    {
        foreach (var finished in _runningCycles.Where(kv => kv.Value.IsCompleted).Select(kv => kv.Key).ToList())
        {
            _runningCycles.Remove(finished);
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var enabledJobs = await db.Jobs.Where(j => j.Enabled).ToListAsync(cancellationToken);

        LogEnabledJobCountChanges(enabledJobs.Count);

        var stamped = false;
        foreach (var job in enabledJobs)
        {
            if (_runningCycles.ContainsKey(job.Id))
                continue;

            var isDue = job.LastRunAtUtc is null || now - job.LastRunAtUtc >= job.PollingInterval;
            if (!isDue)
                continue;

            if (!await RequiredConnectionsConfiguredAsync(job, cancellationToken))
            {
                // LastRunAtUtc stays untouched: the job never ran, and it should
                // start immediately once its connections are configured. The
                // warning is rate-limited in memory instead.
                if (!_lastUnconfiguredWarnings.TryGetValue(job.Id, out var lastWarned)
                    || now - lastWarned >= job.PollingInterval)
                {
                    _lastUnconfiguredWarnings[job.Id] = now;
                    _logger.LogWarning(
                        "Job '{JobName}' is due but one or more of its connections is missing or disabled " +
                        "(media service #{MediaServiceConnectionId}, download client #{DownloadClientConnectionId}, remote storage #{RemoteStorageConnectionId}); skipping",
                        job.Name, job.MediaServiceConnectionId, job.DownloadClientConnectionId, job.RemoteStorageConnectionId);
                }
                continue;
            }

            _lastUnconfiguredWarnings.Remove(job.Id);
            job.LastRunAtUtc = now;
            stamped = true;

            _runningCycles[job.Id] = RunCycleAsync(job.ToDefinition(), cancellationToken);
        }

        if (stamped)
            await db.SaveChangesAsync(cancellationToken);
    }

    private void LogEnabledJobCountChanges(int enabledCount)
    {
        if (enabledCount == _lastEnabledJobCount)
            return;

        if (enabledCount == 0)
        {
            _logger.LogWarning("No enabled jobs configured. Nothing will be processed until a job is created and enabled.");
        }
        else
        {
            _logger.LogInformation("Scheduling {JobCount} enabled job(s)", enabledCount);
        }

        _lastEnabledJobCount = enabledCount;
    }

    internal Task WaitForRunningCyclesAsync()
        => Task.WhenAll(_runningCycles.Values);

    private async Task RunCycleAsync(JobDefinition job, CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var runner = scope.ServiceProvider.GetRequiredService<IJobCycleRunner>();
            await runner.RunAsync(job, cancellationToken);
        }
        catch (ServiceNotConfiguredException ex)
        {
            _logger.LogWarning("Job '{JobName}': {Message}", job.Name, ex.Message);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // shutdown requested mid-cycle
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during reconciliation for job '{JobName}'", job.Name);
        }
    }

    /// <summary>
    /// A job can run only when each of its three connections (media service, download client, remote
    /// storage) exists and is enabled. Read straight from the store by id, no DI scope needed. The
    /// category of each connection is validated at job-creation time.
    /// </summary>
    private async Task<bool> RequiredConnectionsConfiguredAsync(Job job, CancellationToken cancellationToken)
        => await IsReadyAsync(job.MediaServiceConnectionId, cancellationToken)
           && await IsReadyAsync(job.DownloadClientConnectionId, cancellationToken)
           && await IsReadyAsync(job.RemoteStorageConnectionId, cancellationToken);

    private async Task<bool> IsReadyAsync(int connectionId, CancellationToken cancellationToken)
    {
        var connection = await _connectionStore.GetByIdAsync(connectionId, cancellationToken);
        return connection is { Enabled: true };
    }
}
