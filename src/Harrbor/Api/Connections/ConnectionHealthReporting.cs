using Harrbor.Configuration.Connections;
using Harrbor.Services.Configuration;
using Harrbor.Services.Health;

namespace Harrbor.Api.Connections;

/// <summary>
/// Builds the per-connection health list from the background monitor's cached state,
/// shared by the connections health endpoint and the dashboard aggregate.
/// </summary>
public static class ConnectionHealthReporting
{
    public static async Task<List<ConnectionHealth>> BuildAsync(
        IConnectionStore store,
        IConnectionHealthMonitor monitor,
        CancellationToken cancellationToken)
    {
        var result = new List<ConnectionHealth>(ConnectionTypes.All.Count);
        foreach (var type in ConnectionTypes.All)
        {
            var configured = await store.IsConfiguredAsync(type, cancellationToken);
            var state = monitor.GetState(type);
            result.Add(new ConnectionHealth
            {
                Type = type,
                Configured = configured,
                Status = state.Status,
                CheckedAtUtc = state.CheckedAtUtc,
                LatencyMs = state.LatencyMs,
                Error = state.Error
            });
        }

        return result;
    }
}
