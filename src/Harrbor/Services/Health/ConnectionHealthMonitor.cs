using System.Collections.Concurrent;
using System.Diagnostics;
using Harrbor.Configuration.Connections;
using Harrbor.Services.Clients;
using Harrbor.Services.Configuration;
using Harrbor.Services.RemoteStorage;
using Microsoft.Extensions.DependencyInjection;

namespace Harrbor.Services.Health;

/// <summary>Live reachability of a connection.</summary>
public enum ConnectionHealthStatus
{
    /// <summary>Not yet probed (unconfigured, or the monitor hasn't run its first check).</summary>
    Unknown,
    Up,
    Degraded,
    Down,

    /// <summary>Configured but intentionally disabled (Sonarr/Radarr with Enabled = false).</summary>
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
    ConnectionHealthState GetState(string connectionType);
}

/// <summary>
/// Periodically probes each configured connection and caches the result so the dashboard
/// and connections page can read live health without paying a round-trip per request.
/// Mirrors the OrchestrationWorker pattern: a background loop that opens a DI scope per
/// tick to resolve scoped clients.
/// </summary>
public sealed class ConnectionHealthMonitor : BackgroundService, IConnectionHealthMonitor
{
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(10);
    private const int DegradedLatencyMs = 5000;

    // rclone spawns a process per probe, so check remote storage less often than the light HTTP probes.
    private const int RemoteStorageEveryNTicks = 4;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ConnectionHealthMonitor> _logger;
    private readonly TimeProvider _timeProvider;

    private readonly ConcurrentDictionary<string, ConnectionHealthState> _states = new();

    public ConnectionHealthMonitor(
        IServiceScopeFactory scopeFactory,
        ILogger<ConnectionHealthMonitor> logger,
        TimeProvider timeProvider)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _timeProvider = timeProvider;
    }

    public ConnectionHealthState GetState(string connectionType)
        => _states.GetValueOrDefault(connectionType, ConnectionHealthState.NotChecked);

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
    /// Runs one round of health probes. Internal so tests can drive a deterministic
    /// single tick rather than waiting on the background loop.
    /// </summary>
    internal async Task RunChecksAsync(long tick, CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var store = services.GetRequiredService<IConnectionStore>();

        // qBittorrent (singleton client with a cached session — safe to poll frequently)
        if (await store.IsConfiguredAsync(ConnectionTypes.QBittorrent, cancellationToken))
        {
            var client = services.GetRequiredService<IQBittorrentClient>();
            await ProbeAsync(ConnectionTypes.QBittorrent, cancellationToken, async token =>
                await client.IsConnectedAsync(token)
                    ? (true, null)
                    : (false, "Could not authenticate with qBittorrent"));
        }
        else
        {
            _states[ConnectionTypes.QBittorrent] = ConnectionHealthState.NotChecked;
        }

        await CheckServarrAsync<SonarrSettings, ISonarrClient>(ConnectionTypes.Sonarr, store, services, cancellationToken);
        await CheckServarrAsync<RadarrSettings, IRadarrClient>(ConnectionTypes.Radarr, store, services, cancellationToken);

        // Remote storage is heavy (rclone process) — probe on a longer sub-interval and keep the
        // previous state in between ticks.
        if (tick % RemoteStorageEveryNTicks == 0)
        {
            if (await store.IsConfiguredAsync(ConnectionTypes.RemoteStorage, cancellationToken))
            {
                var remote = services.GetRequiredService<IRemoteStorageService>();
                await ProbeAsync(ConnectionTypes.RemoteStorage, cancellationToken, async token =>
                {
                    var result = await remote.TestConnectionAsync(token);
                    return (result.Success, result.Error);
                });
            }
            else
            {
                _states[ConnectionTypes.RemoteStorage] = ConnectionHealthState.NotChecked;
            }
        }
    }

    private async Task CheckServarrAsync<TSettings, TClient>(
        string type, IConnectionStore store, IServiceProvider services, CancellationToken cancellationToken)
        where TSettings : ServarrSettings, IConnectionSettings, new()
        where TClient : class, IMediaService
    {
        var stored = await store.GetAsync<TSettings>(cancellationToken);
        if (stored is null)
        {
            _states[type] = ConnectionHealthState.NotChecked;
            return;
        }

        if (!stored.Settings.Enabled)
        {
            _states[type] = new ConnectionHealthState(
                ConnectionHealthStatus.Disabled, _timeProvider.GetUtcNow().UtcDateTime, null, null);
            return;
        }

        var client = services.GetRequiredService<TClient>();
        await ProbeAsync(type, cancellationToken, async token =>
            await client.IsHealthyAsync(token) ? (true, null) : (false, $"{type} health check failed"));
    }

    private async Task ProbeAsync(
        string type,
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

            _states[type] = ok
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
            _states[type] = new ConnectionHealthState(
                ConnectionHealthStatus.Down, now, null, $"Timed out after {ProbeTimeout.TotalSeconds:0}s");
        }
        catch (Exception ex)
        {
            _states[type] = new ConnectionHealthState(ConnectionHealthStatus.Down, now, null, ex.Message);
        }
    }
}
