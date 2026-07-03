using Harrbor.Configuration;
using Harrbor.Data;
using Harrbor.Services.Orchestration.Phases;
using Microsoft.EntityFrameworkCore;

namespace Harrbor.Services.Orchestration;

public interface IJobCycleRunner
{
    Task RunAsync(JobDefinition job, CancellationToken cancellationToken);
}

/// <summary>
/// Runs one full six-phase reconciliation cycle for a job. Scoped: the scheduler
/// creates a fresh DI scope per cycle so handlers and the DbContext are per-cycle.
/// </summary>
public class JobCycleRunner : IJobCycleRunner
{
    private readonly IDiscoveryPhaseHandler _discoveryHandler;
    private readonly IDownloadPhaseHandler _downloadHandler;
    private readonly ITransferPhaseHandler _transferHandler;
    private readonly IImportPhaseHandler _importHandler;
    private readonly ICleanupPhaseHandler _cleanupHandler;
    private readonly IArchivalPhaseHandler _archivalHandler;
    private readonly HarrborDbContext _dbContext;
    private readonly ILogger<JobCycleRunner> _logger;

    public JobCycleRunner(
        IDiscoveryPhaseHandler discoveryHandler,
        IDownloadPhaseHandler downloadHandler,
        ITransferPhaseHandler transferHandler,
        IImportPhaseHandler importHandler,
        ICleanupPhaseHandler cleanupHandler,
        IArchivalPhaseHandler archivalHandler,
        HarrborDbContext dbContext,
        ILogger<JobCycleRunner> logger)
    {
        _discoveryHandler = discoveryHandler;
        _downloadHandler = downloadHandler;
        _transferHandler = transferHandler;
        _importHandler = importHandler;
        _cleanupHandler = cleanupHandler;
        _archivalHandler = archivalHandler;
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task RunAsync(JobDefinition job, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Job '{JobName}': Starting reconciliation cycle", job.Name);

        // 1. DISCOVER - Get queue from Sonarr/Radarr and track new releases
        await _discoveryHandler.ExecuteAsync(job, _dbContext, cancellationToken);

        // 2. PROCESS PENDING DOWNLOADS - Check qBittorrent for completed downloads
        await _downloadHandler.ExecuteAsync(job, _dbContext, cancellationToken);

        // 3. PROCESS PENDING TRANSFERS - Run rclone transfers
        await _transferHandler.ExecuteAsync(job, _dbContext, cancellationToken);

        // 4. PROCESS PENDING IMPORTS - Check if Sonarr/Radarr has imported
        await _importHandler.ExecuteAsync(job, _dbContext, cancellationToken);

        // 5. PROCESS PENDING CLEANUP - Delete files from staging
        await _cleanupHandler.ExecuteAsync(job, _dbContext, cancellationToken);

        // 6. PROCESS PENDING ARCHIVAL - Change torrent category
        await _archivalHandler.ExecuteAsync(job, _dbContext, cancellationToken);

        // Log summary of tracked releases for this job
        var summary = await _dbContext.TrackedReleases
            .Where(r => r.JobName == job.Name)
            .GroupBy(r => new { r.DownloadStatus, r.TransferStatus, r.ImportStatus })
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        if (summary.Any())
        {
            _logger.LogDebug(
                "Job '{JobName}': Tracked releases summary: {Summary}",
                job.Name,
                string.Join(", ", summary.Select(s =>
                    $"[D:{s.Key.DownloadStatus}/T:{s.Key.TransferStatus}/I:{s.Key.ImportStatus}]={s.Count}")));
        }

        _logger.LogDebug("Job '{JobName}': Reconciliation cycle completed", job.Name);
    }
}
