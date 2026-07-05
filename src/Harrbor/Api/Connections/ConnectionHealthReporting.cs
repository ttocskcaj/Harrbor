using Harrbor.Services;
using Harrbor.Services.Configuration;
using Harrbor.Services.Health;

namespace Harrbor.Api.Connections;

/// <summary>
/// Builds the per-connection health list from the background monitor's cached state (keyed by
/// connection id), shared by the connections health endpoint and the dashboard aggregate.
/// </summary>
public static class ConnectionHealthReporting
{
    public static async Task<List<ConnectionHealth>> BuildAsync(
        IConnectionStore store,
        IConnectionProviderRegistry registry,
        IConnectionHealthMonitor monitor,
        CancellationToken cancellationToken)
    {
        var connections = await store.GetAllAsync(cancellationToken);
        var result = new List<ConnectionHealth>();

        foreach (var connection in connections)
        {
            var provider = registry.Find(connection.ProviderKey);
            var state = monitor.GetState(connection.Id);
            result.Add(new ConnectionHealth
            {
                Id = connection.Id,
                Name = connection.Name,
                ProviderKey = connection.ProviderKey,
                Category = provider?.Category ?? default,
                Enabled = connection.Enabled,
                Status = state.Status,
                CheckedAtUtc = state.CheckedAtUtc,
                LatencyMs = state.LatencyMs,
                Error = state.Error,
            });
        }

        return result;
    }
}
