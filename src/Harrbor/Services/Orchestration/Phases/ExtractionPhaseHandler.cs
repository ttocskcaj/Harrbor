using Microsoft.EntityFrameworkCore;
using Harrbor.Configuration;
using Harrbor.Data;
using Harrbor.Data.Entities;
using Harrbor.Services.Extraction;

namespace Harrbor.Services.Orchestration.Phases;

public interface IExtractionPhaseHandler : IPhaseHandler
{
}

public class ExtractionPhaseHandler : IExtractionPhaseHandler
{
    private readonly IArchiveExtractionService _archiveExtractionService;
    private readonly ILogger<ExtractionPhaseHandler> _logger;

    public ExtractionPhaseHandler(
        IArchiveExtractionService archiveExtractionService,
        ILogger<ExtractionPhaseHandler> logger)
    {
        _archiveExtractionService = archiveExtractionService;
        _logger = logger;
    }

    public Task ExecuteAsync(JobDefinition job, HarrborDbContext dbContext, CancellationToken cancellationToken)
    {
        return ExecuteAsync(job, dbContext, _archiveExtractionService, cancellationToken);
    }

    public async Task ExecuteAsync(JobDefinition job, HarrborDbContext dbContext, IArchiveExtractionService extractionService, CancellationToken cancellationToken)
    {
        // ImportStatus == Pending keeps releases that were imported before this phase existed
        // (and whose staging files are gone) from ever being scanned
        var pendingExtractions = await dbContext.TrackedReleases
            .Where(r => r.JobName == job.Name
                && r.TransferStatus == TransferStatus.Completed
                && r.ExtractionStatus == ExtractionStatus.Pending
                && r.ImportStatus == ImportStatus.Pending)
            .OrderBy(r => r.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        if (pendingExtractions.Count == 0)
            return;

        _logger.LogDebug("Job '{JobName}': Processing {Count} pending extractions", job.Name, pendingExtractions.Count);

        // Extractions run one at a time per job to bound CPU and disk pressure
        foreach (var release in pendingExtractions)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!job.ExtractionEnabled)
            {
                release.ExtractionStatus = ExtractionStatus.Completed;
                release.ExtractionCompletedAtUtc = DateTime.UtcNow;

                _logger.LogDebug(
                    "Job '{JobName}': Extraction disabled, skipping '{ReleaseName}'",
                    job.Name, release.Name);
                continue;
            }

            await ExtractReleaseAsync(job, release, dbContext, extractionService, cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task ExtractReleaseAsync(
        JobDefinition job,
        TrackedRelease release,
        HarrborDbContext dbContext,
        IArchiveExtractionService extractionService,
        CancellationToken cancellationToken)
    {
        release.ExtractionStatus = ExtractionStatus.InProgress;
        release.ExtractionStartedAtUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        // Staging item is a subdirectory (or file) matching the torrent name (consistent with transfer/cleanup logic)
        var stagingItemPath = Path.Combine(release.StagingPath, Path.GetFileName(release.RemotePath));

        try
        {
            var result = await extractionService.ExtractAsync(stagingItemPath, cancellationToken);

            if (result.Success)
            {
                release.ExtractionStatus = ExtractionStatus.Completed;
                release.ExtractionCompletedAtUtc = DateTime.UtcNow;
                // Clear error state on success
                release.LastError = null;
                release.LastErrorAtUtc = null;

                if (result.ArchivesExtracted > 0)
                {
                    _logger.LogInformation(
                        "Job '{JobName}': Extracted {ArchiveCount} archive(s) for '{ReleaseName}' in {Duration}",
                        job.Name, result.ArchivesExtracted, release.Name, result.Duration);
                }
                else
                {
                    _logger.LogDebug(
                        "Job '{JobName}': No archives found for '{ReleaseName}'",
                        job.Name, release.Name);
                }
            }
            else
            {
                release.ExtractionStatus = ExtractionStatus.Failed;
                release.ErrorCount++;
                release.LastError = result.Error;
                release.LastErrorAtUtc = DateTime.UtcNow;

                _logger.LogError(
                    "Job '{JobName}': Extraction failed for '{ReleaseName}': {Error}",
                    job.Name, release.Name, result.Error);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            release.ExtractionStatus = ExtractionStatus.Failed;
            release.ErrorCount++;
            release.LastError = ex.Message;
            release.LastErrorAtUtc = DateTime.UtcNow;

            _logger.LogError(ex,
                "Job '{JobName}': Extraction failed with exception for '{ReleaseName}'",
                job.Name, release.Name);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
