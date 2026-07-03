using Microsoft.EntityFrameworkCore;
using Harrbor.Configuration;
using Harrbor.Data;
using Harrbor.Data.Entities;
using Harrbor.Services.Clients;
using TorrentInfo = QBittorrent.Client.TorrentInfo;

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
            TorrentInfo? torrent;
            try
            {
                torrent = await qBittorrentClient.GetTorrentAsync(release.DownloadId, cancellationToken);
            }
            catch (HttpRequestException ex)
            {
                // Isolate per-release qBittorrent failures: a transient error for one torrent
                // must not abort the phase (or the rest of the reconciliation cycle). The
                // release stays Pending and is retried on the next cycle. Only qBittorrent
                // transport/API failures (HttpRequestException, incl. QBittorrentClientRequestException)
                // are absorbed here; unexpected exceptions propagate so real bugs surface.
                // Record the error for visibility, but do not touch ErrorCount - that budgets
                // transfer-phase retries and must not be consumed by a download-query hiccup.
                release.LastError = ex.Message;
                release.LastErrorAtUtc = DateTime.UtcNow;

                _logger.LogWarning(
                    "Job '{JobName}': Failed to query qBittorrent for '{ReleaseName}' (DownloadId: {DownloadId}): {Error}; skipping until next cycle",
                    job.Name, release.Name, release.DownloadId, ex.Message);
                continue;
            }

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

                // Clear any transient query error recorded while the download was pending so a
                // stale message does not follow the release into the transfer phase.
                release.LastError = null;
                release.LastErrorAtUtc = null;

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
