using Microsoft.Extensions.Options;
using Harrbor.Configuration;
using Harrbor.Data;
using Harrbor.Services.Clients;
using Harrbor.Services.Transfer;

namespace Harrbor.Services.Orchestration;

public class OrchestrationWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<HarrborOptions> _options;
    private readonly ILogger<OrchestrationWorker> _logger;

    public OrchestrationWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<HarrborOptions> options,
        ILogger<OrchestrationWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OrchestrationWorker starting with polling interval: {Interval}",
            _options.Value.PollingInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ReconcileAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error during reconciliation loop");
            }

            await Task.Delay(_options.Value.PollingInterval, stoppingToken);
        }

        _logger.LogInformation("OrchestrationWorker stopping");
    }

    private async Task ReconcileAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("Starting reconciliation cycle");

        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<HarrborDbContext>();
        var qBittorrentClient = scope.ServiceProvider.GetRequiredService<IQBittorrentClient>();
        var sonarrClient = scope.ServiceProvider.GetRequiredService<ISonarrClient>();
        var radarrClient = scope.ServiceProvider.GetRequiredService<IRadarrClient>();
        var transferService = scope.ServiceProvider.GetRequiredService<ITransferService>();

        // TODO: Implement reconciliation logic
        // 1. Poll qBittorrent for completed torrents
        // 2. Track new completed torrents in database
        // 3. Transfer completed torrents to staging
        // 4. Trigger imports in Sonarr/Radarr
        // 5. Clean up transferred files
        // 6. Archive processed torrents

        _logger.LogDebug("Reconciliation cycle completed");
    }
}
