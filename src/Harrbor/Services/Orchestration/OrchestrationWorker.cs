using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using Harrbor.Configuration;
using Harrbor.Data;
using Harrbor.Services.Orchestration.Phases;

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

        // Get phase handlers from DI
        var discoveryHandler = scope.ServiceProvider.GetRequiredService<IDiscoveryPhaseHandler>();
        var downloadHandler = scope.ServiceProvider.GetRequiredService<IDownloadPhaseHandler>();
        var transferHandler = scope.ServiceProvider.GetRequiredService<ITransferPhaseHandler>();
        var importHandler = scope.ServiceProvider.GetRequiredService<IImportPhaseHandler>();
        var cleanupHandler = scope.ServiceProvider.GetRequiredService<ICleanupPhaseHandler>();
        var archivalHandler = scope.ServiceProvider.GetRequiredService<IArchivalPhaseHandler>();

        // 1. DISCOVER - Get queue from Sonarr/Radarr and track new releases
        await discoveryHandler.ExecuteAsync(job, dbContext, cancellationToken);

        // 2. PROCESS PENDING DOWNLOADS - Check qBittorrent for completed downloads
        await downloadHandler.ExecuteAsync(job, dbContext, cancellationToken);

        // 3. PROCESS PENDING TRANSFERS - Run rclone transfers
        await transferHandler.ExecuteAsync(job, dbContext, cancellationToken);

        // 4. PROCESS PENDING IMPORTS - Check if Sonarr/Radarr has imported
        await importHandler.ExecuteAsync(job, dbContext, cancellationToken);

        // 5. PROCESS PENDING CLEANUP - Delete files from staging
        await cleanupHandler.ExecuteAsync(job, dbContext, cancellationToken);

        // 6. PROCESS PENDING ARCHIVAL - Change torrent category
        await archivalHandler.ExecuteAsync(job, dbContext, cancellationToken);

        // Log summary of tracked releases for this job
        var summary = await dbContext.TrackedReleases
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
