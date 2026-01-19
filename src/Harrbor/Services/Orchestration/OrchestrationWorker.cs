using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using Harrbor.Configuration;
using Harrbor.Data;
using Harrbor.Data.Entities;
using Harrbor.Services.Clients;
using Harrbor.Services.RemoteStorage;

namespace Harrbor.Services.Orchestration;

public class OrchestrationWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<JobOptions> _jobOptions;
    private readonly ILogger<OrchestrationWorker> _logger;

    public OrchestrationWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<JobOptions> jobOptions,
        ILogger<OrchestrationWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _jobOptions = jobOptions;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var enabledJobs = _jobOptions.Value.Definitions.Where(j => j.Enabled).ToList();

        if (enabledJobs.Count == 0)
        {
            _logger.LogWarning("No enabled jobs configured. OrchestrationWorker will not process any torrents.");
            return;
        }

        _logger.LogInformation(
            "OrchestrationWorker starting with {JobCount} enabled job(s): {JobNames}",
            enabledJobs.Count,
            string.Join(", ", enabledJobs.Select(j => j.Name)));

        var tasks = enabledJobs.Select(job => RunJobLoopAsync(job, stoppingToken));
        await Task.WhenAll(tasks);

        _logger.LogInformation("OrchestrationWorker stopping");
    }

    private async Task RunJobLoopAsync(JobDefinition job, CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Job '{JobName}' starting with polling interval: {Interval}, service: {ServiceType}",
            job.Name,
            job.PollingInterval,
            job.ServiceType);

        // Run immediately on startup
        await ReconcileJobAsync(job, stoppingToken);

        using var timer = new PeriodicTimer(job.PollingInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await ReconcileJobAsync(job, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error during reconciliation for job '{JobName}'", job.Name);
            }
        }

        _logger.LogInformation("Job '{JobName}' stopping", job.Name);
    }

    private async Task ReconcileJobAsync(JobDefinition job, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Job '{JobName}': Starting reconciliation cycle", job.Name);

        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<HarrborDbContext>();
        var qBittorrentClient = scope.ServiceProvider.GetRequiredService<IQBittorrentClient>();
        var mediaServiceResolver = scope.ServiceProvider.GetRequiredService<IMediaServiceResolver>();
        var remoteStorageService = scope.ServiceProvider.GetRequiredService<IRemoteStorageService>();

        var mediaService = mediaServiceResolver.GetServiceForJob(job);

        // 1. DISCOVER - Get queue from Sonarr/Radarr and track new releases
        await DiscoverReleasesAsync(job, dbContext, mediaService, cancellationToken);

        // 2. PROCESS PENDING DOWNLOADS - Check qBittorrent for completed downloads
        await ProcessPendingDownloadsAsync(job, dbContext, qBittorrentClient, cancellationToken);

        // 3. PROCESS PENDING TRANSFERS - Run rclone transfers
        await ProcessPendingTransfersAsync(job, dbContext, remoteStorageService, cancellationToken);

        // 4. PROCESS PENDING IMPORTS - Check if Sonarr/Radarr has imported
        await ProcessPendingImportsAsync(job, dbContext, mediaService, cancellationToken);

        // 5. PROCESS PENDING CLEANUP - Delete files from staging
        await ProcessPendingCleanupAsync(job, dbContext, cancellationToken);

        // 6. PROCESS PENDING ARCHIVAL - Change torrent category
        await ProcessPendingArchivalAsync(job, dbContext, qBittorrentClient, cancellationToken);

        _logger.LogDebug("Job '{JobName}': Reconciliation cycle completed", job.Name);
    }

    private async Task DiscoverReleasesAsync(
        JobDefinition job,
        HarrborDbContext dbContext,
        IMediaService mediaService,
        CancellationToken cancellationToken)
    {
        var queue = await mediaService.GetQueueAsync(cancellationToken);

        _logger.LogDebug("Job '{JobName}': Media service queue has {Count} items", job.Name, queue.Count);

        foreach (var queueItem in queue)
        {
            // Check if we're already tracking this release
            var existingRelease = await dbContext.TrackedReleases
                .FirstOrDefaultAsync(r => r.DownloadId == queueItem.DownloadId, cancellationToken);

            if (existingRelease != null)
                continue;

            // Determine the remote path (from queue item or job default)
            var remotePath = queueItem.OutputPath ?? job.RemotePath;
            if (string.IsNullOrEmpty(remotePath))
            {
                _logger.LogWarning(
                    "Job '{JobName}': Skipping release '{ReleaseName}' - no remote path available (OutputPath and job RemotePath are both empty)",
                    job.Name, queueItem.Title);
                continue;
            }

            // Create new tracked release
            var release = new TrackedRelease
            {
                DownloadId = queueItem.DownloadId,
                Name = queueItem.Title,
                JobName = job.Name,
                RemotePath = remotePath,
                StagingPath = job.StagingPath,
                DownloadStatus = DownloadStatus.Pending,
                TransferStatus = TransferStatus.Pending,
                ImportStatus = ImportStatus.Pending,
                CleanupStatus = CleanupStatus.Pending,
                ArchivalStatus = ArchivalStatus.Pending,
                CreatedAtUtc = DateTime.UtcNow
            };

            dbContext.TrackedReleases.Add(release);
            _logger.LogInformation(
                "Job '{JobName}': Discovered new release '{ReleaseName}' (DownloadId: {DownloadId})",
                job.Name, release.Name, release.DownloadId);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task ProcessPendingDownloadsAsync(
        JobDefinition job,
        HarrborDbContext dbContext,
        IQBittorrentClient qBittorrentClient,
        CancellationToken cancellationToken)
    {
        var pendingDownloads = await dbContext.TrackedReleases
            .Where(r => r.JobName == job.Name && r.DownloadStatus == DownloadStatus.Pending)
            .ToListAsync(cancellationToken);

        if (pendingDownloads.Count == 0)
            return;

        _logger.LogDebug("Job '{JobName}': Processing {Count} pending downloads", job.Name, pendingDownloads.Count);

        foreach (var release in pendingDownloads)
        {
            var torrent = await qBittorrentClient.GetTorrentAsync(release.DownloadId, cancellationToken);

            if (torrent == null)
            {
                _logger.LogWarning(
                    "Job '{JobName}': Torrent not found in qBittorrent for release '{ReleaseName}' (DownloadId: {DownloadId})",
                    job.Name, release.Name, release.DownloadId);
                continue;
            }

            // Check if download is complete (progress = 1.0 or 100%)
            if (torrent.Progress >= 1.0)
            {
                release.DownloadStatus = DownloadStatus.Completed;
                release.DownloadCompletedAtUtc = DateTime.UtcNow;

                // Update the remote path from torrent's actual save path
                if (!string.IsNullOrEmpty(torrent.ContentPath))
                {
                    release.RemotePath = torrent.ContentPath;
                }
                else if (!string.IsNullOrEmpty(torrent.SavePath))
                {
                    release.RemotePath = Path.Combine(torrent.SavePath, torrent.Name);
                }

                _logger.LogInformation(
                    "Job '{JobName}': Download completed for '{ReleaseName}' at {RemotePath}",
                    job.Name, release.Name, release.RemotePath);
            }
            else
            {
                _logger.LogDebug(
                    "Job '{JobName}': Download in progress for '{ReleaseName}' ({Progress:P0})",
                    job.Name, release.Name, torrent.Progress);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task ProcessPendingTransfersAsync(
        JobDefinition job,
        HarrborDbContext dbContext,
        IRemoteStorageService remoteStorageService,
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

    private async Task ProcessPendingImportsAsync(
        JobDefinition job,
        HarrborDbContext dbContext,
        IMediaService mediaService,
        CancellationToken cancellationToken)
    {
        var pendingImports = await dbContext.TrackedReleases
            .Where(r => r.JobName == job.Name
                && r.TransferStatus == TransferStatus.Completed
                && r.ImportStatus == ImportStatus.Pending)
            .ToListAsync(cancellationToken);

        if (pendingImports.Count == 0)
            return;

        _logger.LogDebug("Job '{JobName}': Processing {Count} pending imports", job.Name, pendingImports.Count);

        foreach (var release in pendingImports)
        {
            // Check if still in queue
            var inQueue = await mediaService.IsDownloadInQueueAsync(release.DownloadId, cancellationToken);

            if (!inQueue)
            {
                // Check history for import event
                var hasImported = await mediaService.HasImportedAsync(release.DownloadId, cancellationToken);

                if (hasImported)
                {
                    release.ImportStatus = ImportStatus.Completed;
                    release.ImportCompletedAtUtc = DateTime.UtcNow;

                    _logger.LogInformation(
                        "Job '{JobName}': Import confirmed for '{ReleaseName}'",
                        job.Name, release.Name);
                }
                else
                {
                    // Not in queue and no import history - likely failed or was removed
                    release.ImportStatus = ImportStatus.Failed;
                    release.LastError = "Download removed from queue without import confirmation";
                    release.LastErrorAtUtc = DateTime.UtcNow;

                    _logger.LogWarning(
                        "Job '{JobName}': Import failed for '{ReleaseName}' - removed from queue without import",
                        job.Name, release.Name);
                }
            }
            else
            {
                // Still in queue - check timeout
                if (release.TransferCompletedAtUtc.HasValue)
                {
                    var elapsed = DateTime.UtcNow - release.TransferCompletedAtUtc.Value;
                    if (elapsed > job.ImportTimeout)
                    {
                        release.ImportStatus = ImportStatus.Failed;
                        release.LastError = $"Import timeout exceeded ({job.ImportTimeout})";
                        release.LastErrorAtUtc = DateTime.UtcNow;

                        _logger.LogWarning(
                            "Job '{JobName}': Import timeout for '{ReleaseName}' - exceeded {Timeout}",
                            job.Name, release.Name, job.ImportTimeout);
                    }
                    else
                    {
                        _logger.LogDebug(
                            "Job '{JobName}': Waiting for import of '{ReleaseName}' ({Elapsed} of {Timeout})",
                            job.Name, release.Name, elapsed, job.ImportTimeout);
                    }
                }
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task ProcessPendingCleanupAsync(
        JobDefinition job,
        HarrborDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var pendingCleanup = await dbContext.TrackedReleases
            .Where(r => r.JobName == job.Name
                && r.ImportStatus == ImportStatus.Completed
                && r.CleanupStatus == CleanupStatus.Pending)
            .ToListAsync(cancellationToken);

        if (pendingCleanup.Count == 0)
            return;

        _logger.LogDebug("Job '{JobName}': Processing {Count} pending cleanups", job.Name, pendingCleanup.Count);

        foreach (var release in pendingCleanup)
        {
            try
            {
                // Construct the staging file/folder path
                var stagingItemPath = Path.Combine(release.StagingPath, Path.GetFileName(release.RemotePath));

                if (Directory.Exists(stagingItemPath))
                {
                    Directory.Delete(stagingItemPath, recursive: true);
                    _logger.LogInformation(
                        "Job '{JobName}': Deleted staging directory for '{ReleaseName}': {Path}",
                        job.Name, release.Name, stagingItemPath);
                }
                else if (File.Exists(stagingItemPath))
                {
                    File.Delete(stagingItemPath);
                    _logger.LogInformation(
                        "Job '{JobName}': Deleted staging file for '{ReleaseName}': {Path}",
                        job.Name, release.Name, stagingItemPath);
                }
                else
                {
                    _logger.LogDebug(
                        "Job '{JobName}': Staging path not found for '{ReleaseName}': {Path} (may have been cleaned by import)",
                        job.Name, release.Name, stagingItemPath);
                }

                release.CleanupStatus = CleanupStatus.Completed;
                release.CleanupCompletedAtUtc = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                release.CleanupStatus = CleanupStatus.Failed;
                release.ErrorCount++;
                release.LastError = ex.Message;
                release.LastErrorAtUtc = DateTime.UtcNow;

                _logger.LogError(ex,
                    "Job '{JobName}': Cleanup failed for '{ReleaseName}'",
                    job.Name, release.Name);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task ProcessPendingArchivalAsync(
        JobDefinition job,
        HarrborDbContext dbContext,
        IQBittorrentClient qBittorrentClient,
        CancellationToken cancellationToken)
    {
        var pendingArchival = await dbContext.TrackedReleases
            .Where(r => r.JobName == job.Name
                && r.CleanupStatus == CleanupStatus.Completed
                && r.ArchivalStatus == ArchivalStatus.Pending)
            .ToListAsync(cancellationToken);

        if (pendingArchival.Count == 0)
            return;

        _logger.LogDebug("Job '{JobName}': Processing {Count} pending archivals", job.Name, pendingArchival.Count);

        // Ensure the completed category exists before trying to assign torrents to it
        if (!string.IsNullOrEmpty(job.CompletedCategory))
        {
            await qBittorrentClient.EnsureCategoryExistsAsync(job.CompletedCategory, cancellationToken);
        }

        foreach (var release in pendingArchival)
        {
            try
            {
                if (!string.IsNullOrEmpty(job.CompletedCategory))
                {
                    await qBittorrentClient.SetCategoryAsync(
                        release.DownloadId,
                        job.CompletedCategory,
                        cancellationToken);

                    _logger.LogInformation(
                        "Job '{JobName}': Moved '{ReleaseName}' to category '{Category}'",
                        job.Name, release.Name, job.CompletedCategory);
                }
                else
                {
                    _logger.LogDebug(
                        "Job '{JobName}': No completed category configured, skipping archival for '{ReleaseName}'",
                        job.Name, release.Name);
                }

                release.ArchivalStatus = ArchivalStatus.Completed;
                release.ArchivedAtUtc = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                release.ArchivalStatus = ArchivalStatus.Failed;
                release.ErrorCount++;
                release.LastError = ex.Message;
                release.LastErrorAtUtc = DateTime.UtcNow;

                _logger.LogError(ex,
                    "Job '{JobName}': Archival failed for '{ReleaseName}'",
                    job.Name, release.Name);
            }
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
