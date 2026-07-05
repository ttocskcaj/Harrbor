using System.Collections.Concurrent;
using System.Diagnostics;
using Harrbor.Services.Configuration;

namespace Harrbor.Services.Health;

/// <summary>Live reachability of a connection.</summary>
public enum ConnectionHealthStatus
{
    /// <summary>Not yet probed (the monitor hasn't run its first check for it).</summary>
    Unknown,
    Up,
    Degraded,
    Down,

    /// <summary>Configured but intentionally disabled.</summary>
    Disabled
}

public sealed record ConnectionHealthState(
    ConnectionHealthStatus Status,
    DateTime? CheckedAtUtc,
    int? LatencyMs,
    string? Error)
{
    public static readonly ConnectionHealthState NotChecked =
        new(ConnectionHealthStatus.Unknown, null, null, null);
}

public interface IConnectionHealthMonitor
{
    /// <summary>The last known health of a connection; <see cref="ConnectionHealthState.NotChecked"/> if never probed.</summary>
    ConnectionHealthState GetState(int connectionId);
}

/// <summary>
/// Periodically probes each enabled connection instance and caches the result so the dashboard and
/// connections page can read live health without paying a round-trip per request. Health is keyed by
/// connection id; each connection's provider decides how often it is probed (<c>ProbeEveryNTicks</c>).
/// The store, registry, and connection manager are all singletons, so no per-tick DI scope is needed.
/// </summary>
public sealed class ConnectionHealthMonitor : BackgroundService, IConnectionHealthMonitor
{
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(10);
    private const int DegradedLatencyMs = 5000;

    private readonly IConnectionStore _store;
    private readonly IConnectionProviderRegistry _registry;
    private readonly IConnectionManager _manager;
    private readonly ILogger<ConnectionHealthMonitor> _logger;
    private readonly TimeProvider _timeProvider;

    private readonly ConcurrentDictionary<int, ConnectionHealthState> _states = new();

    public ConnectionHealthMonitor(
        IConnectionStore store,
        IConnectionProviderRegistry registry,
        IConnectionManager manager,
        ILogger<ConnectionHealthMonitor> logger,
        TimeProvider timeProvider)
    {
        _store = store;
        _registry = registry;
        _manager = manager;
        _logger = logger;
        _timeProvider = timeProvider;
    }

    public ConnectionHealthState GetState(int connectionId)
        => _states.GetValueOrDefault(connectionId, ConnectionHealthState.NotChecked);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Let the app finish starting before the first probe (also keeps short-lived
        // integration-test hosts from ticking at all).
        try
        {
            await Task.Delay(StartupDelay, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        using var timer = new PeriodicTimer(TickInterval);
        var tick = 0L;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunChecksAsync(tick, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Connection health monitor tick failed");
            }

            tick++;

            try
            {
                if (!await timer.WaitForNextTickAsync(stoppingToken))
                    break;
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    /// <summary>
    /// Runs one round of health probes over all connection instances. Internal so tests can drive a
    /// deterministic single tick rather than waiting on the background loop.
    /// </summary>
    internal async Task RunChecksAsync(long tick, CancellationToken cancellationToken)
    {
        var connections = await _store.GetAllAsync(cancellationToken);
        var seen = new HashSet<int>();

        foreach (var connection in connections)
        {
            seen.Add(connection.Id);

            var provider = _registry.Find(connection.ProviderKey);
            if (provider is null)
            {
                _states[connection.Id] = new ConnectionHealthState(
                    ConnectionHealthStatus.Down, _timeProvider.GetUtcNow().UtcDateTime, null,
                    $"Unknown provider '{connection.ProviderKey}'");
                continue;
            }

            if (!connection.Enabled)
            {
                _states[connection.Id] = new ConnectionHealthState(
                    ConnectionHealthStatus.Disabled, _timeProvider.GetUtcNow().UtcDateTime, null, null);
                continue;
            }

            // Heavy probes (e.g. rclone, which spawns a process) run on a longer sub-interval and
            // keep their previously cached state in between.
            if (provider.ProbeEveryNTicks > 1 && tick % provider.ProbeEveryNTicks != 0 && _states.ContainsKey(connection.Id))
                continue;

            await ProbeAsync(connection.Id, cancellationToken, async probeToken =>
            {
                var client = await _manager.GetClientAsync(connection, probeToken);
                var result = await provider.ProbeAsync(client, probeToken);
                return (result.Ok, result.Error);
            });
        }

        // Drop cached state for connections that no longer exist.
        foreach (var staleId in _states.Keys.Where(id => !seen.Contains(id)).ToList())
            _states.TryRemove(staleId, out _);
    }

    private async Task ProbeAsync(
        int connectionId,
        CancellationToken cancellationToken,
        Func<CancellationToken, Task<(bool ok, string? error)>> probe)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(ProbeTimeout);

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var (ok, error) = await probe(cts.Token);
            stopwatch.Stop();
            var latencyMs = (int)stopwatch.ElapsedMilliseconds;

            _states[connectionId] = ok
                ? new ConnectionHealthState(
                    latencyMs > DegradedLatencyMs ? ConnectionHealthStatus.Degraded : ConnectionHealthStatus.Up,
                    now, latencyMs, null)
                : new ConnectionHealthState(ConnectionHealthStatus.Down, now, latencyMs, error ?? "Unreachable");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw; // application is shutting down
        }
        catch (OperationCanceledException)
        {
            _states[connectionId] = new ConnectionHealthState(
                ConnectionHealthStatus.Down, now, null, $"Timed out after {ProbeTimeout.TotalSeconds:0}s");
        }
        catch (Exception ex)
        {
            _states[connectionId] = new ConnectionHealthState(ConnectionHealthStatus.Down, now, null, ex.Message);
        }
    }
}
