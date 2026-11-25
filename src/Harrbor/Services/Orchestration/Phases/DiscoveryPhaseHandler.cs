using Microsoft.EntityFrameworkCore;
using Harrbor.Configuration;
using Harrbor.Data;
using Harrbor.Data.Entities;
using Harrbor.Services.Clients;

namespace Harrbor.Services.Orchestration.Phases;

public interface IDiscoveryPhaseHandler : IPhaseHandler
{
}

public class DiscoveryPhaseHandler : IDiscoveryPhaseHandler
{
    private readonly IMediaServiceResolver _mediaServiceResolver;
    private readonly ILogger<DiscoveryPhaseHandler> _logger;

    public DiscoveryPhaseHandler(
        IMediaServiceResolver mediaServiceResolver,
        ILogger<DiscoveryPhaseHandler> logger)
    {
        _mediaServiceResolver = mediaServiceResolver;
        _logger = logger;
    }

    public async Task ExecuteAsync(JobDefinition job, HarrborDbContext dbContext, CancellationToken cancellationToken)
    {
        var mediaService = _mediaServiceResolver.GetServiceForJob(job);
        await ExecuteAsync(job, dbContext, mediaService, cancellationToken);
    }

    public async Task ExecuteAsync(JobDefinition job, HarrborDbContext dbContext, IMediaService mediaService, CancellationToken cancellationToken)
    {
        var queue = await mediaService.GetQueueAsync(cancellationToken);

        _logger.LogDebug("Job '{JobName}': Media service queue has {Count} items", job.Name, queue.Count);

        // Deduplicate by DownloadId to handle season packs (multiple episodes share one torrent)
        var uniqueQueue = queue.DistinctBy(q => q.DownloadId).ToList();
        if (uniqueQueue.Count < queue.Count)
        {
            _logger.LogDebug(
                "Job '{JobName}': Deduplicated queue from {Original} to {Unique} items (season pack detected)",
                job.Name, queue.Count, uniqueQueue.Count);
        }

        foreach (var queueItem in uniqueQueue)
        {
            // Check if we're already tracking this release
            var existingRelease = await dbContext.TrackedReleases
                .FirstOrDefaultAsync(r => r.DownloadId == queueItem.DownloadId, cancellationToken);

            if (existingRelease != null)
            {
                _logger.LogDebug(
                    "Job '{JobName}': Release '{ReleaseName}' already tracked by job '{TrackedJob}' (Download: {DownloadStatus}, Transfer: {TransferStatus}, Import: {ImportStatus})",
                    job.Name, existingRelease.Name, existingRelease.JobName,
                    existingRelease.DownloadStatus, existingRelease.TransferStatus, existingRelease.ImportStatus);
                continue;
            }

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
}
