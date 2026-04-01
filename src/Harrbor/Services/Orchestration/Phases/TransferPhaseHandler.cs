using Microsoft.EntityFrameworkCore;
using Harrbor.Configuration;
using Harrbor.Data;
using Harrbor.Data.Entities;
using Harrbor.Services.RemoteStorage;

namespace Harrbor.Services.Orchestration.Phases;

public interface ITransferPhaseHandler : IPhaseHandler
{
}

public class TransferPhaseHandler : ITransferPhaseHandler
{
    private readonly IRemoteStorageService _remoteStorageService;
    private readonly ILogger<TransferPhaseHandler> _logger;

    public TransferPhaseHandler(
        IRemoteStorageService remoteStorageService,
        ILogger<TransferPhaseHandler> logger)
    {
        _remoteStorageService = remoteStorageService;
        _logger = logger;
    }

    public Task ExecuteAsync(JobDefinition job, HarrborDbContext dbContext, CancellationToken cancellationToken)
    {
        return ExecuteAsync(job, dbContext, _remoteStorageService, cancellationToken);
    }

    public async Task ExecuteAsync(JobDefinition job, HarrborDbContext dbContext, IRemoteStorageService remoteStorageService, CancellationToken cancellationToken)
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
        var pendingTransfers = await dbContext.TrackedReleases
            .Where(r => r.JobName == job.Name
                && r.DownloadStatus == DownloadStatus.Completed
                && r.TransferStatus == TransferStatus.Pending)
            .OrderBy(r => r.CreatedAtUtc)
            .Take(availableSlots)
            .ToListAsync(cancellationToken);

        var remainingSlots = availableSlots - pendingTransfers.Count;

        // If we have remaining slots, look for failed transfers eligible for retry
        if (remainingSlots > 0)
        {
            var retryableTransfers = await dbContext.TrackedReleases
                .Where(r => r.JobName == job.Name
                    && r.DownloadStatus == DownloadStatus.Completed
                    && r.TransferStatus == TransferStatus.Failed
                    && r.ErrorCount < job.MaxTransferRetries
                    && r.LastErrorAtUtc != null
                    && r.LastErrorAtUtc <= retryEligibleCutoff)
                .OrderBy(r => r.LastErrorAtUtc)
                .Take(remainingSlots)
                .ToListAsync(cancellationToken);

            pendingTransfers.AddRange(retryableTransfers);
        }

        if (pendingTransfers.Count == 0)
            return;

        var pendingCount = pendingTransfers.Count(r => r.TransferStatus == TransferStatus.Pending);
        var retryCount = pendingTransfers.Count(r => r.TransferStatus == TransferStatus.Failed);

        _logger.LogDebug(
            "Job '{JobName}': Processing {PendingCount} pending transfers and {RetryCount} retries",
            job.Name, pendingCount, retryCount);

        var transferTasks = pendingTransfers
            .Select(release => TransferReleaseAsync(job, release, dbContext, remoteStorageService, cancellationToken))
            .ToList();

        await Task.WhenAll(transferTasks);
    }

    private async Task TransferReleaseAsync(
        JobDefinition job,
        TrackedRelease release,
        HarrborDbContext dbContext,
        IRemoteStorageService remoteStorageService,
        CancellationToken cancellationToken)
    {
        var isRetry = release.TransferStatus == TransferStatus.Failed;
        var attemptNumber = release.ErrorCount + 1;

        // For retries, check if the remote file still exists
        if (isRetry)
        {
            var remoteExists = await remoteStorageService.ExistsAsync(release.RemotePath, cancellationToken);
            if (!remoteExists)
            {
                // Source file is gone - mark as permanently failed
                release.ErrorCount = job.MaxTransferRetries;
                release.LastError = "Remote source no longer exists";
                release.LastErrorAtUtc = DateTime.UtcNow;
                await dbContext.SaveChangesAsync(cancellationToken);

                _logger.LogWarning(
                    "Job '{JobName}': Remote source no longer exists for '{ReleaseName}' at {RemotePath}, marking as permanently failed",
                    job.Name, release.Name, release.RemotePath);
                return;
            }

            _logger.LogInformation(
                "Job '{JobName}': Retrying transfer for '{ReleaseName}' (attempt {Attempt}/{MaxAttempts})",
                job.Name, release.Name, attemptNumber, job.MaxTransferRetries);
        }

        release.TransferStatus = TransferStatus.InProgress;
        release.TransferStartedAtUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        // Destination is a subdirectory matching the torrent name (consistent with cleanup logic)
        var destinationPath = Path.Combine(release.StagingPath, Path.GetFileName(release.RemotePath));

        _logger.LogInformation(
            "Job '{JobName}': Starting transfer for '{ReleaseName}' from {RemotePath} to {DestinationPath}{AttemptInfo}",
            job.Name, release.Name, release.RemotePath, destinationPath,
            isRetry ? $" (attempt {attemptNumber}/{job.MaxTransferRetries})" : "");

        try
        {
            var result = await remoteStorageService.TransferAsync(
                release.RemotePath,
                destinationPath,
                cancellationToken);

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
