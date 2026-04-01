using Microsoft.EntityFrameworkCore;
using Harrbor.Configuration;
using Harrbor.Data;
using Harrbor.Data.Entities;
using Harrbor.Services.Clients;

namespace Harrbor.Services.Orchestration.Phases;

public interface IArchivalPhaseHandler : IPhaseHandler
{
}

public class ArchivalPhaseHandler : IArchivalPhaseHandler
{
    private readonly IQBittorrentClient _qBittorrentClient;
    private readonly ILogger<ArchivalPhaseHandler> _logger;

    public ArchivalPhaseHandler(
        IQBittorrentClient qBittorrentClient,
        ILogger<ArchivalPhaseHandler> logger)
    {
        _qBittorrentClient = qBittorrentClient;
        _logger = logger;
    }

    public Task ExecuteAsync(JobDefinition job, HarrborDbContext dbContext, CancellationToken cancellationToken)
    {
        return ExecuteAsync(job, dbContext, _qBittorrentClient, cancellationToken);
    }

    public async Task ExecuteAsync(JobDefinition job, HarrborDbContext dbContext, IQBittorrentClient qBittorrentClient, CancellationToken cancellationToken)
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
}
