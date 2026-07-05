using Harrbor.Abstractions.Clients;
using Microsoft.EntityFrameworkCore;
using Harrbor.Configuration;
using Harrbor.Data;
using Harrbor.Data.Entities;

namespace Harrbor.Services.Orchestration.Phases;

public interface IArchivalPhaseHandler : IPhaseHandler
{
}

public class ArchivalPhaseHandler : IArchivalPhaseHandler
{
    private readonly IConnectionManager _connectionManager;
    private readonly ILogger<ArchivalPhaseHandler> _logger;

    public ArchivalPhaseHandler(
        IConnectionManager connectionManager,
        ILogger<ArchivalPhaseHandler> logger)
    {
        _connectionManager = connectionManager;
        _logger = logger;
    }

    public async Task ExecuteAsync(JobDefinition job, HarrborDbContext dbContext, CancellationToken cancellationToken)
    {
        var torrentClient = await _connectionManager.GetTorrentClientAsync(job.DownloadClientConnectionId, cancellationToken);
        await ExecuteAsync(job, dbContext, torrentClient, cancellationToken);
    }

    public async Task ExecuteAsync(JobDefinition job, HarrborDbContext dbContext, ITorrentClient torrentClient, CancellationToken cancellationToken)
    {
        var pendingArchival = await dbContext.TrackedReleases
            .Where(r => r.JobName == job.Name
                && r.Outcome == PipelineOutcome.Active
                && r.CleanupStatus == CleanupStatus.Completed
                && r.ArchivalStatus == ArchivalStatus.Pending)
            .ToListAsync(cancellationToken);

        if (pendingArchival.Count == 0)
            return;

        _logger.LogDebug("Job '{JobName}': Processing {Count} pending archivals", job.Name, pendingArchival.Count);

        // Ensure the completed category exists before trying to assign torrents to it
        if (!string.IsNullOrEmpty(job.CompletedCategory))
        {
            await torrentClient.EnsureCategoryExistsAsync(job.CompletedCategory, cancellationToken);
        }

        foreach (var release in pendingArchival)
        {
            try
            {
                if (!string.IsNullOrEmpty(job.CompletedCategory))
                {
                    await torrentClient.SetCategoryAsync(
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
                // Reached the end of the pipeline.
                release.Outcome = PipelineOutcome.Completed;
            }
            catch (Exception ex)
            {
                release.ArchivalStatus = ArchivalStatus.Failed;
                release.Outcome = PipelineOutcome.Failed;
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
