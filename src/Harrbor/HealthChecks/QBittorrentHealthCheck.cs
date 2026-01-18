using Microsoft.Extensions.Diagnostics.HealthChecks;
using Harrbor.Services.Clients;

namespace Harrbor.HealthChecks;

public class QBittorrentHealthCheck : IHealthCheck
{
    private readonly IQBittorrentClient _client;

    public QBittorrentHealthCheck(IQBittorrentClient client)
    {
        _client = client;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
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
