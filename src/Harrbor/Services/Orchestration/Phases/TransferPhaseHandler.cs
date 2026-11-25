using Microsoft.EntityFrameworkCore;
using Harrbor.Configuration;
using Harrbor.Data;
using Harrbor.Data.Entities;
using Harrbor.Services.Clients;
using Harrbor.Services.Clients.Models;
using Harrbor.Services.RemoteStorage;

namespace Harrbor.Services.Orchestration.Phases;

public interface ITransferPhaseHandler : IPhaseHandler
{
}

public class TransferPhaseHandler : ITransferPhaseHandler
{
    private readonly TransferCoordinator _coordinator;
    private readonly ILogger<TransferPhaseHandler> _logger;

    public TransferPhaseHandler(TransferCoordinator coordinator, ILogger<TransferPhaseHandler> logger)
    {
        _coordinator = coordinator;
        _logger = logger;
    }

    public Task ExecuteAsync(JobDefinition job, HarrborDbContext dbContext, CancellationToken cancellationToken)
    {
        // Resolve the services inside each transfer's own scope: the phase's scope ends with the cycle.
        return StartTransfersAsync(
            job,
            dbContext,
            sp => sp.GetRequiredService<IRemoteStorageService>(),
            sp => sp.GetRequiredService<IMediaServiceResolver>().GetServiceForJob(job),
            cancellationToken);
    }

    public Task ExecuteAsync(JobDefinition job, HarrborDbContext dbContext, IRemoteStorageService remoteStorageService, CancellationToken cancellationToken)
    {
        return ExecuteAsync(job, dbContext, remoteStorageService, mediaService: null, cancellationToken);
    }

    /// <param name="mediaService">Used to check the staged layout against the *arr's queue; null skips the check.</param>
    public Task ExecuteAsync(
        JobDefinition job,
        HarrborDbContext dbContext,
        IRemoteStorageService remoteStorageService,
        IMediaService? mediaService,
        CancellationToken cancellationToken)
    {
        return StartTransfersAsync(job, dbContext, _ => remoteStorageService, _ => mediaService, cancellationToken);
    }

    /// <summary>
    /// Marks the next releases InProgress (up to the job's parallelism) and starts their transfers in the
    /// background, then returns without waiting for them, so the later phases of the cycle keep running.
    /// </summary>
    private async Task StartTransfersAsync(
        JobDefinition job,
        HarrborDbContext dbContext,
        Func<IServiceProvider, IRemoteStorageService> getRemoteStorage,
        Func<IServiceProvider, IMediaService?> getMediaService,
        CancellationToken cancellationToken)
    {
        // Check currently in-progress transfers to respect parallelism limit
        var inProgressCount = await dbContext.TrackedReleases
            .CountAsync(r => r.JobName == job.Name && r.TransferStatus == TransferStatus.InProgress, cancellationToken);

        var availableSlots = job.TransferParallelism - inProgressCount;
        if (availableSlots <= 0)
        {
            _logger.LogDebug(
                "Job '{JobName}': Transfer parallelism limit reached ({Limit} transfers in progress)",
                job.Name, job.TransferParallelism);
            return;
        }

        var retryEligibleCutoff = DateTime.UtcNow - job.TransferRetryDelay;

        // Get releases that are ready for transfer:
        // 1. Pending transfers (priority)
        // 2. Failed transfers eligible for retry (under retry limit, cooldown elapsed)
        // A release whose transfer task is still winding down is skipped, so it never runs twice.
        var pendingTransfers = (await dbContext.TrackedReleases
            .Where(r => r.JobName == job.Name
                && r.DownloadStatus == DownloadStatus.Completed
                && r.TransferStatus == TransferStatus.Pending)
            .OrderBy(r => r.CreatedAtUtc)
            .ToListAsync(cancellationToken))
            .Where(r => !_coordinator.IsRunning(r.Id))
            .Take(availableSlots)
            .ToList();

        var remainingSlots = availableSlots - pendingTransfers.Count;

        // If we have remaining slots, look for failed transfers eligible for retry
        if (remainingSlots > 0)
        {
            var retryableTransfers = (await dbContext.TrackedReleases
                .Where(r => r.JobName == job.Name
                    && r.DownloadStatus == DownloadStatus.Completed
                    && r.TransferStatus == TransferStatus.Failed
                    && r.ErrorCount < job.MaxTransferRetries
                    && r.LastErrorAtUtc != null
                    && r.LastErrorAtUtc <= retryEligibleCutoff)
                .OrderBy(r => r.LastErrorAtUtc)
                .ToListAsync(cancellationToken))
                .Where(r => !_coordinator.IsRunning(r.Id))
                .Take(remainingSlots);

            pendingTransfers.AddRange(retryableTransfers);
        }

        if (pendingTransfers.Count == 0)
            return;

        var pendingCount = pendingTransfers.Count(r => r.TransferStatus == TransferStatus.Pending);
        var retryCount = pendingTransfers.Count(r => r.TransferStatus == TransferStatus.Failed);

        _logger.LogDebug(
            "Job '{JobName}': Starting {PendingCount} pending transfers and {RetryCount} retries",
            job.Name, pendingCount, retryCount);

        // Claim the slots before starting anything, from this thread only. The InProgress rows are what the
        // parallelism limit counts, and what the startup reset puts back to Pending after a crash.
        var starts = pendingTransfers
            .Select(r => new TransferStart(r.Id, r.TransferStatus, r.TransferStartedAtUtc))
            .ToList();
        foreach (var release in pendingTransfers)
        {
            release.TransferStatus = TransferStatus.InProgress;
            release.TransferStartedAtUtc = DateTime.UtcNow;
        }
        await dbContext.SaveChangesAsync(cancellationToken);

        // From here on each release belongs to its transfer task (and its DbContext). Stop tracking it here so the
        // later phases of this cycle read its current state from the database, not this context's stale copy.
        foreach (var release in pendingTransfers)
            dbContext.Entry(release).State = EntityState.Detached;

        foreach (var start in starts)
        {
            _coordinator.TryStart(
                start.ReleaseId,
                job.Name,
                (sp, ct) => RunTransferAsync(job, start, sp, getRemoteStorage(sp), getMediaService, ct),
                cancellationToken);
        }
    }

    /// <summary>The state a release was in before it was claimed for a transfer.</summary>
    private sealed record TransferStart(int ReleaseId, TransferStatus PreviousStatus, DateTime? PreviousStartedAtUtc);

    /// <summary>One transfer, run in the background with its own scope and DbContext.</summary>
    private async Task RunTransferAsync(
        JobDefinition job,
        TransferStart start,
        IServiceProvider services,
        IRemoteStorageService remoteStorageService,
        Func<IServiceProvider, IMediaService?> getMediaService,
        CancellationToken cancellationToken)
    {
        var dbContext = services.GetRequiredService<HarrborDbContext>();
        var release = await dbContext.TrackedReleases.FirstOrDefaultAsync(r => r.Id == start.ReleaseId, cancellationToken);
        if (release == null)
            return;

        var transferred = await TransferReleaseAsync(job, release, start, dbContext, remoteStorageService, cancellationToken);
        if (!transferred)
            return;

        IMediaService? mediaService;
        try
        {
            mediaService = getMediaService(services);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Job '{JobName}': No media service, staged layout not checked", job.Name);
            return;
        }

        if (mediaService != null)
            await CheckStagedLayoutAsync(job, [release], mediaService, cancellationToken);
    }

    /// <returns>True if the transfer completed.</returns>
    private async Task<bool> TransferReleaseAsync(
        JobDefinition job,
        TrackedRelease release,
        TransferStart start,
        HarrborDbContext dbContext,
        IRemoteStorageService remoteStorageService,
        CancellationToken cancellationToken)
    {
        var isRetry = start.PreviousStatus == TransferStatus.Failed;
        var attemptNumber = release.ErrorCount + 1;
        var remoteItemPath = release.GetRemoteItemPath();

        // Find out whether the torrent's top-level item is a file or a directory (that decides the rclone verb),
        // and whether it still exists.
        RemoteItemKind itemKind;
        try
        {
            itemKind = await remoteStorageService.GetItemKindAsync(remoteItemPath, cancellationToken);
        }
        catch (RemoteStorageUnavailableException ex)
        {
            // Couldn't reach the remote, so we don't know whether the source is there.
            // Don't burn a retry attempt or fail permanently - put it back as it was and check again on a later cycle.
            release.TransferStatus = start.PreviousStatus;
            release.TransferStartedAtUtc = start.PreviousStartedAtUtc;
            release.LastError = ex.Message;
            release.LastErrorAtUtc = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogWarning(
                "Job '{JobName}': Could not check remote source for '{ReleaseName}' at {RemotePath}, will retry: {Error}",
                job.Name, release.Name, remoteItemPath, ex.Message);
            return false;
        }

        if (itemKind == RemoteItemKind.Missing)
        {
            release.TransferStatus = TransferStatus.Failed;
            release.TransferStartedAtUtc = start.PreviousStartedAtUtc;
            release.LastErrorAtUtc = DateTime.UtcNow;

            if (isRetry)
            {
                // Source is gone after a failed attempt - mark as permanently failed
                release.ErrorCount = job.MaxTransferRetries;
                release.LastError = "Remote source no longer exists";
                _logger.LogWarning(
                    "Job '{JobName}': Remote source no longer exists for '{ReleaseName}' at {RemotePath}, marking as permanently failed",
                    job.Name, release.Name, remoteItemPath);
            }
            else
            {
                release.ErrorCount++;
                release.LastError = $"Remote source not found: {remoteItemPath}";
                _logger.LogWarning(
                    "Job '{JobName}': Remote source not found for '{ReleaseName}' at {RemotePath}, will retry",
                    job.Name, release.Name, remoteItemPath);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            return false;
        }

        if (isRetry)
        {
            _logger.LogInformation(
                "Job '{JobName}': Retrying transfer for '{ReleaseName}' (attempt {Attempt}/{MaxAttempts})",
                job.Name, release.Name, attemptNumber, job.MaxTransferRetries);
        }

        // Releases go to the hidden holding directory, named after the torrent's top-level item, and are published
        // to the final staging path (which Sonarr/Radarr watches) only once post-processing is done.
        var destinationPath = release.GetHoldingItemPath();
        var isFile = itemKind == RemoteItemKind.File;

        _logger.LogInformation(
            "Job '{JobName}': Starting transfer for '{ReleaseName}' from {RemotePath} to {DestinationPath} ({ItemKind}){AttemptInfo}",
            job.Name, release.Name, remoteItemPath, destinationPath, isFile ? "file" : "directory",
            isRetry ? $" (attempt {attemptNumber}/{job.MaxTransferRetries})" : "");

        try
        {
            var result = isFile
                ? await remoteStorageService.TransferFileAsync(remoteItemPath, destinationPath, cancellationToken)
                : await remoteStorageService.TransferAsync(remoteItemPath, destinationPath, cancellationToken);

            // Shutting down: leave the release InProgress; the startup reset puts it back to Pending.
            cancellationToken.ThrowIfCancellationRequested();

            if (result.Success)
            {
                release.TransferStatus = TransferStatus.Completed;
                release.TransferCompletedAtUtc = DateTime.UtcNow;
                // Clear error state on success
                release.LastError = null;
                release.LastErrorAtUtc = null;

                _logger.LogInformation(
                    "Job '{JobName}': Transfer completed for '{ReleaseName}' ({BytesTransferred} in {Duration}){RetryInfo}",
                    job.Name, release.Name, FormatSize(result.BytesTransferred), result.Duration,
                    isRetry ? $" after {attemptNumber} attempt(s)" : "");
            }
            else if (result.Interrupted)
            {
                // Stopped from outside, not a failure: put it back as it was, without using up an attempt.
                release.TransferStatus = start.PreviousStatus;
                release.TransferStartedAtUtc = start.PreviousStartedAtUtc;

                _logger.LogWarning(
                    "Job '{JobName}': Transfer for '{ReleaseName}' was interrupted, will start again: {Error}",
                    job.Name, release.Name, result.Error);
            }
            else
            {
                release.TransferStatus = TransferStatus.Failed;
                release.ErrorCount++;
                release.LastError = result.Error;
                release.LastErrorAtUtc = DateTime.UtcNow;

                var isPermanentlyFailed = release.ErrorCount >= job.MaxTransferRetries;

                _logger.LogError(
                    "Job '{JobName}': Transfer failed for '{ReleaseName}': {Error} (attempt {Attempt}/{MaxAttempts}){PermanentStatus}",
                    job.Name, release.Name, result.Error, attemptNumber, job.MaxTransferRetries,
                    isPermanentlyFailed ? " - permanently failed" : " - will retry");
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            release.TransferStatus = TransferStatus.Failed;
            release.ErrorCount++;
            release.LastError = ex.Message;
            release.LastErrorAtUtc = DateTime.UtcNow;

            var isPermanentlyFailed = release.ErrorCount >= job.MaxTransferRetries;

            _logger.LogError(ex,
                "Job '{JobName}': Transfer failed with exception for '{ReleaseName}' (attempt {Attempt}/{MaxAttempts}){PermanentStatus}",
                job.Name, release.Name, attemptNumber, job.MaxTransferRetries,
                isPermanentlyFailed ? " - permanently failed" : " - will retry");
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return release.TransferStatus == TransferStatus.Completed;
    }

    /// <summary>
    /// Compares where each release will be published with where the *arr expects it (the queue record's
    /// <c>outputPath</c>), and warns on a mismatch so a wrong layout shows up now instead of as a stalled import.
    /// </summary>
    private async Task CheckStagedLayoutAsync(
        JobDefinition job,
        IReadOnlyList<TrackedRelease> releases,
        IMediaService mediaService,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<QueueItem> queue;
        try
        {
            queue = await mediaService.GetQueueAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogDebug(ex, "Job '{JobName}': Could not fetch the queue to check the staged layout", job.Name);
            return;
        }

        foreach (var release in releases)
        {
            var outputPath = queue.FirstOrDefault(q =>
                string.Equals(q.DownloadId, release.DownloadId, StringComparison.OrdinalIgnoreCase))?.OutputPath;
            if (string.IsNullOrEmpty(outputPath))
            {
                _logger.LogDebug(
                    "Job '{JobName}': No queue output path for '{ReleaseName}', staged layout not checked",
                    job.Name, release.Name);
                continue;
            }

            var finalItemPath = release.GetFinalItemPath();
            var check = StagedLayoutCheck.Evaluate(outputPath, job.StagingPath, finalItemPath, job.RemotePath, release.RemotePath);
            switch (check.Matches)
            {
                case true:
                    _logger.LogDebug(
                        "Job '{JobName}': Staged layout for '{ReleaseName}' matches the *arr's output path {OutputPath}",
                        job.Name, release.Name, outputPath);
                    break;
                case false:
                    _logger.LogWarning(
                        "Job '{JobName}': Staged layout for '{ReleaseName}' does not match what {ServiceType} expects: " +
                        "it will be published to {FinalItemPath} (seen by {ServiceType} as {ExpectedPath}), but its output path is {OutputPath}. " +
                        "The import will probably fail; check the remote path mapping",
                        job.Name, release.Name, job.ServiceType, finalItemPath, job.ServiceType, check.ExpectedPath, outputPath);
                    break;
                default:
                    _logger.LogDebug(
                        "Job '{JobName}': Can't map {ServiceType} output path {OutputPath} for '{ReleaseName}' to staging, layout not checked",
                        job.Name, job.ServiceType, outputPath, release.Name);
                    break;
            }
        }
    }

    private static string FormatSize(long bytes)
    {
        string[] sizes = ["B", "KB", "MB", "GB", "TB"];
        var order = 0;
        var size = (double)bytes;
        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }
        return $"{size:0.##} {sizes[order]}";
    }
}
