using Microsoft.EntityFrameworkCore;
using Harrbor.Configuration;
using Harrbor.Data;
using Harrbor.Data.Entities;
using Harrbor.Services.Clients;

namespace Harrbor.Services.Orchestration.Phases;

public interface IImportPhaseHandler : IPhaseHandler
{
}

public class ImportPhaseHandler : IImportPhaseHandler
{
    private readonly IMediaServiceResolver _mediaServiceResolver;
    private readonly ILogger<ImportPhaseHandler> _logger;

    public ImportPhaseHandler(
        IMediaServiceResolver mediaServiceResolver,
        ILogger<ImportPhaseHandler> logger)
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
}
