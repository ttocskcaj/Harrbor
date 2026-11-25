using System.Collections.Concurrent;

namespace Harrbor.Services.Orchestration;

/// <summary>
/// Runs long phase work (a transfer, a conversion) in the background so the job loop doesn't wait for it.
/// Each piece of work gets its own DI scope, and therefore its own <c>HarrborDbContext</c>: a DbContext must
/// never be shared between concurrent tasks. Work is keyed by release Id, and a release runs at most once at a time.
/// </summary>
public abstract class BackgroundWorkTracker
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<int, Running> _running = new();

    private sealed record Running(string JobName, Task Completion);

    protected BackgroundWorkTracker(IServiceScopeFactory scopeFactory, ILogger logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>What the work is, for log messages (e.g. "transfer").</summary>
    protected abstract string WorkName { get; }

    public bool IsRunning(int releaseId) => _running.ContainsKey(releaseId);

    public int RunningCount => _running.Count;

    public int RunningCountForJob(string jobName) => _running.Values.Count(r => r.JobName == jobName);

    /// <summary>
    /// Starts <paramref name="work"/> in the background with a new DI scope. Returns false, without starting
    /// anything, if work for <paramref name="releaseId"/> is already running.
    /// </summary>
    /// <remarks>
    /// Cancelling <paramref name="cancellationToken"/> (the host's stopping token) stops the work; it is then
    /// expected to leave the release InProgress for the startup reset.
    /// </remarks>
    public bool TryStart(
        int releaseId,
        string jobName,
        Func<IServiceProvider, CancellationToken, Task> work,
        CancellationToken cancellationToken)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_running.TryAdd(releaseId, new Running(jobName, completion.Task)))
            return false;

        _ = Task.Run(async () =>
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                await work(scope.ServiceProvider, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation("Job '{JobName}': {Work} for release {ReleaseId} cancelled", jobName, WorkName, releaseId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Job '{JobName}': Background {Work} for release {ReleaseId} failed", jobName, WorkName, releaseId);
            }
            finally
            {
                _running.TryRemove(releaseId, out _);
                completion.TrySetResult();
            }
        }, CancellationToken.None);

        return true;
    }

    /// <summary>Completes when all work running now has finished. Never throws.</summary>
    public Task WhenIdleAsync() => Task.WhenAll(_running.Values.Select(r => r.Completion));
}

/// <summary>Background rclone transfers, limited per job by <c>TransferParallelism</c>.</summary>
public sealed class TransferCoordinator(IServiceScopeFactory scopeFactory, ILogger<TransferCoordinator> logger)
    : BackgroundWorkTracker(scopeFactory, logger)
{
    protected override string WorkName => "transfer";
}

/// <summary>Background conversions: one at a time across all jobs, since conversion is I/O-heavy.</summary>
public sealed class ConversionCoordinator(IServiceScopeFactory scopeFactory, ILogger<ConversionCoordinator> logger)
    : BackgroundWorkTracker(scopeFactory, logger)
{
    protected override string WorkName => "conversion";
}
