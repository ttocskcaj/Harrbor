using Microsoft.EntityFrameworkCore;
using Harrbor.Configuration;
using Harrbor.Data;
using Harrbor.Data.Entities;
using Harrbor.Services.Clients;

namespace Harrbor.Services.Orchestration.Phases;

public interface IDownloadPhaseHandler : IPhaseHandler
{
}

public class DownloadPhaseHandler : IDownloadPhaseHandler
{
    private readonly IQBittorrentClient _qBittorrentClient;
    private readonly ILogger<DownloadPhaseHandler> _logger;

    public DownloadPhaseHandler(
        IQBittorrentClient qBittorrentClient,
        ILogger<DownloadPhaseHandler> logger)
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
}
