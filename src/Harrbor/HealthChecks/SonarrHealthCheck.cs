using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Harrbor.Configuration;
using Harrbor.Services.Clients;

namespace Harrbor.HealthChecks;

public class SonarrHealthCheck : IHealthCheck
{
    private readonly ISonarrClient _client;
    private readonly SonarrOptions _options;

    public SonarrHealthCheck(ISonarrClient client, IOptions<SonarrOptions> options)
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
