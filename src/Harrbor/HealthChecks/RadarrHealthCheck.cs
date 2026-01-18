using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Harrbor.Configuration;
using Harrbor.Services.Clients;

namespace Harrbor.HealthChecks;

public class RadarrHealthCheck : IHealthCheck
{
    private readonly IRadarrClient _client;
    private readonly RadarrOptions _options;

    public RadarrHealthCheck(IRadarrClient client, IOptions<RadarrOptions> options)
    {
        _client = client;
        _options = options.Value;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
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
