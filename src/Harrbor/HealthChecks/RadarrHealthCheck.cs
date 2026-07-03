using Microsoft.Extensions.Diagnostics.HealthChecks;
using Harrbor.Configuration.Connections;
using Harrbor.Services.Clients;
using Harrbor.Services.Configuration;

namespace Harrbor.HealthChecks;

public class RadarrHealthCheck : IHealthCheck
{
    private readonly IRadarrClient _client;
    private readonly IConnectionStore _connectionStore;

    public RadarrHealthCheck(IRadarrClient client, IConnectionStore connectionStore)
    {
        _client = client;
        _connectionStore = connectionStore;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var stamped = await _connectionStore.GetAsync<RadarrSettings>(cancellationToken);

        if (stamped is null)
        {
            return HealthCheckResult.Degraded("Radarr is not configured");
        }

        if (!stamped.Settings.Enabled)
        {
            return HealthCheckResult.Healthy("Radarr integration is disabled");
        }

        try
        {
            var isHealthy = await _client.IsHealthyAsync(cancellationToken);

            return isHealthy
                ? HealthCheckResult.Healthy("Radarr API is reachable")
                : HealthCheckResult.Unhealthy("Unable to connect to Radarr API");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Radarr health check failed", ex);
        }
    }
}
