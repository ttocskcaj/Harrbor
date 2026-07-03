using Microsoft.Extensions.Diagnostics.HealthChecks;
using Harrbor.Configuration.Connections;
using Harrbor.Services.Clients;
using Harrbor.Services.Configuration;

namespace Harrbor.HealthChecks;

public class QBittorrentHealthCheck : IHealthCheck
{
    private readonly IQBittorrentClient _client;
    private readonly IConnectionStore _connectionStore;

    public QBittorrentHealthCheck(IQBittorrentClient client, IConnectionStore connectionStore)
    {
        _client = client;
        _connectionStore = connectionStore;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (!await _connectionStore.IsConfiguredAsync(ConnectionTypes.QBittorrent, cancellationToken))
        {
            return HealthCheckResult.Degraded("qBittorrent is not configured");
        }

        try
        {
            var isConnected = await _client.IsConnectedAsync(cancellationToken);

            return isConnected
                ? HealthCheckResult.Healthy("qBittorrent is reachable")
                : HealthCheckResult.Unhealthy("Unable to connect to qBittorrent");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("qBittorrent health check failed", ex);
        }
    }
}
