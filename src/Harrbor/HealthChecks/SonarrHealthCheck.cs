using Microsoft.Extensions.Diagnostics.HealthChecks;
using Harrbor.Configuration.Connections;
using Harrbor.Services.Clients;
using Harrbor.Services.Configuration;

namespace Harrbor.HealthChecks;

public class SonarrHealthCheck : IHealthCheck
{
    private readonly ISonarrClient _client;
    private readonly IConnectionStore _connectionStore;

    public SonarrHealthCheck(ISonarrClient client, IConnectionStore connectionStore)
    {
        _client = client;
        _connectionStore = connectionStore;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var stamped = await _connectionStore.GetAsync<SonarrSettings>(cancellationToken);

        if (stamped is null)
        {
            return HealthCheckResult.Degraded("Sonarr is not configured");
        }

        if (!stamped.Settings.Enabled)
        {
            return HealthCheckResult.Healthy("Sonarr integration is disabled");
        }

        try
        {
            var isHealthy = await _client.IsHealthyAsync(cancellationToken);

            return isHealthy
                ? HealthCheckResult.Healthy("Sonarr API is reachable")
                : HealthCheckResult.Unhealthy("Unable to connect to Sonarr API");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Sonarr health check failed", ex);
        }
    }
}
