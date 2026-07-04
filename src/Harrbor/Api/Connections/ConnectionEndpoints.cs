using Harrbor.Configuration.Connections;
using Harrbor.Data;
using Harrbor.Services.Clients;
using Harrbor.Services.Configuration;
using Harrbor.Services.Health;
using Harrbor.Services.RemoteStorage;
using Microsoft.EntityFrameworkCore;

namespace Harrbor.Api.Connections;

public static class ConnectionEndpoints
{
    // Caps test-connection latency; the resilience handler would otherwise retry
    // an unreachable host for ~30s
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(15);

    public static IEndpointRouteBuilder MapConnectionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/connections");

        group.MapGet("/", GetOverviewAsync);
        group.MapGet("/health", GetHealthAsync);

        group.MapGet("/qbittorrent", GetQBittorrentAsync);
        group.MapPut("/qbittorrent", PutQBittorrentAsync);
        group.MapPost("/qbittorrent/test", TestQBittorrentAsync);
        group.MapGet("/qbittorrent/categories", GetQBittorrentCategoriesAsync);

        group.MapGet("/sonarr", GetServarrAsync<SonarrSettings>);
        group.MapPut("/sonarr", PutServarrAsync<SonarrSettings>);
        group.MapPost("/sonarr/test", (ServarrConnectionRequest request, IConnectionStore store, ISonarrClient client, CancellationToken ct)
            => TestServarrAsync<SonarrSettings>(request, store, client, "Sonarr", ct));

        group.MapGet("/radarr", GetServarrAsync<RadarrSettings>);
        group.MapPut("/radarr", PutServarrAsync<RadarrSettings>);
        group.MapPost("/radarr/test", (ServarrConnectionRequest request, IConnectionStore store, IRadarrClient client, CancellationToken ct)
            => TestServarrAsync<RadarrSettings>(request, store, client, "Radarr", ct));

        group.MapGet("/remote-storage", GetRemoteStorageAsync);
        group.MapPut("/remote-storage", PutRemoteStorageAsync);
        group.MapPost("/remote-storage/test", TestRemoteStorageAsync);

        return app;
    }

    private static async Task<IResult> RunTestAsync(
        Func<CancellationToken, Task<ConnectionTestResponse>> test,
        CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TestTimeout);

        try
        {
            return Results.Ok(await test(cts.Token));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Results.Ok(new ConnectionTestResponse
            {
                Success = false,
                Error = $"Connection test timed out after {TestTimeout.TotalSeconds:0} seconds."
            });
        }
    }

    // ----- qBittorrent -----

    private static async Task<IResult> GetQBittorrentAsync(IConnectionStore store, CancellationToken cancellationToken)
    {
        var stamped = await store.GetAsync<QBittorrentSettings>(cancellationToken);
        return stamped is null ? Results.NotFound() : Results.Ok(ConnectionMapping.ToResponse(stamped));
    }

    private static async Task<IResult> PutQBittorrentAsync(
        QBittorrentConnectionRequest request,
        IConnectionStore store,
        CancellationToken cancellationToken)
    {
        var errors = ConnectionMapping.Validate(request);
        if (errors.HasErrors)
            return Results.ValidationProblem(errors.Build());

        var stored = await store.GetAsync<QBittorrentSettings>(cancellationToken);
        if (VersionConflict(request.ExpectedVersion, stored?.Version) is { } conflict)
            return conflict;

        var merged = ConnectionMapping.Merge(request, stored?.Settings);
        var saved = await store.SaveAsync(merged, cancellationToken);
        return Results.Ok(ConnectionMapping.ToResponse(saved));
    }

    private static async Task<IResult> TestQBittorrentAsync(
        QBittorrentConnectionRequest request,
        IConnectionStore store,
        IQBittorrentClient client,
        CancellationToken cancellationToken)
    {
        var errors = ConnectionMapping.Validate(request);
        if (errors.HasErrors)
            return Results.ValidationProblem(errors.Build());

        var stored = await store.GetAsync<QBittorrentSettings>(cancellationToken);
        var merged = ConnectionMapping.Merge(request, stored?.Settings);

        return await RunTestAsync(async token =>
        {
            var result = await client.TestConnectionAsync(merged, token);
            return new ConnectionTestResponse
            {
                Success = result.Success,
                Message = result.Version is null ? null : $"Connected — qBittorrent API version {result.Version}",
                Error = result.Error
            };
        }, cancellationToken);
    }

    // ----- Servarr (Sonarr / Radarr) -----

    private static async Task<IResult> GetServarrAsync<T>(IConnectionStore store, CancellationToken cancellationToken)
        where T : ServarrSettings, IConnectionSettings, new()
    {
        var stamped = await store.GetAsync<T>(cancellationToken);
        return stamped is null ? Results.NotFound() : Results.Ok(ConnectionMapping.ToResponse(stamped));
    }

    private static async Task<IResult> PutServarrAsync<T>(
        ServarrConnectionRequest request,
        IConnectionStore store,
        CancellationToken cancellationToken)
        where T : ServarrSettings, IConnectionSettings, new()
    {
        var stored = await store.GetAsync<T>(cancellationToken);

        var errors = ConnectionMapping.Validate(request, hasStoredApiKey: !string.IsNullOrEmpty(stored?.Settings.ApiKey));
        if (errors.HasErrors)
            return Results.ValidationProblem(errors.Build());

        if (VersionConflict(request.ExpectedVersion, stored?.Version) is { } conflict)
            return conflict;

        var merged = ConnectionMapping.MergeServarr<T>(request, stored?.Settings);
        var saved = await store.SaveAsync(merged, cancellationToken);
        return Results.Ok(ConnectionMapping.ToResponse(saved));
    }

    private static async Task<IResult> TestServarrAsync<T>(
        ServarrConnectionRequest request,
        IConnectionStore store,
        IMediaService client,
        string label,
        CancellationToken cancellationToken)
        where T : ServarrSettings, IConnectionSettings, new()
    {
        var stored = await store.GetAsync<T>(cancellationToken);

        var errors = ConnectionMapping.Validate(request, hasStoredApiKey: !string.IsNullOrEmpty(stored?.Settings.ApiKey));
        if (errors.HasErrors)
            return Results.ValidationProblem(errors.Build());

        var merged = ConnectionMapping.MergeServarr<T>(request, stored?.Settings);

        return await RunTestAsync(async token =>
        {
            var result = await client.TestConnectionAsync(merged.BaseUrl, merged.ApiKey, token);
            return new ConnectionTestResponse
            {
                Success = result.Success,
                Message = result.Version is null ? null : $"Connected — {label} version {result.Version}",
                Error = result.Error
            };
        }, cancellationToken);
    }

    // ----- Remote storage -----

    private static async Task<IResult> GetRemoteStorageAsync(IConnectionStore store, CancellationToken cancellationToken)
    {
        var stamped = await store.GetAsync<RemoteStorageSettings>(cancellationToken);
        return stamped is null ? Results.NotFound() : Results.Ok(ConnectionMapping.ToResponse(stamped));
    }

    private static async Task<IResult> PutRemoteStorageAsync(
        RemoteStorageConnectionRequest request,
        IConnectionStore store,
        CancellationToken cancellationToken)
    {
        var errors = ConnectionMapping.Validate(request);
        if (errors.HasErrors)
            return Results.ValidationProblem(errors.Build());

        var stored = await store.GetAsync<RemoteStorageSettings>(cancellationToken);
        if (VersionConflict(request.ExpectedVersion, stored?.Version) is { } conflict)
            return conflict;

        var merged = ConnectionMapping.Merge(request, stored?.Settings);
        var saved = await store.SaveAsync(merged, cancellationToken);
        return Results.Ok(ConnectionMapping.ToResponse(saved));
    }

    private static async Task<IResult> TestRemoteStorageAsync(
        RemoteStorageConnectionRequest request,
        IConnectionStore store,
        IRemoteStorageService remoteStorage,
        CancellationToken cancellationToken)
    {
        var errors = ConnectionMapping.Validate(request);
        if (errors.HasErrors)
            return Results.ValidationProblem(errors.Build());

        var stored = await store.GetAsync<RemoteStorageSettings>(cancellationToken);
        var merged = ConnectionMapping.Merge(request, stored?.Settings);

        return await RunTestAsync(async token =>
        {
            var result = await remoteStorage.TestConnectionAsync(merged, token);
            return new ConnectionTestResponse
            {
                Success = result.Success,
                Message = result.Success ? "Connection successful" : null,
                Error = result.Error
            };
        }, cancellationToken);
    }

    private static async Task<IResult> GetQBittorrentCategoriesAsync(
        IQBittorrentClient client,
        CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TestTimeout);

        try
        {
            var categories = await client.GetCategoriesAsync(cts.Token);
            return Results.Ok(categories);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Results.Problem(
                detail: "Timed out contacting qBittorrent.",
                statusCode: StatusCodes.Status504GatewayTimeout);
        }
        catch (Exception ex)
        {
            return Results.Problem(
                detail: $"Could not load categories from qBittorrent: {ex.Message}",
                statusCode: StatusCodes.Status502BadGateway);
        }
    }

    // ----- Overview & health -----

    private static async Task<IResult> GetOverviewAsync(
        HarrborDbContext db,
        IConnectionHealthMonitor healthMonitor,
        CancellationToken cancellationToken)
    {
        var rows = await db.ServiceConnections.AsNoTracking().ToListAsync(cancellationToken);

        var overview = ConnectionTypes.All
            .Select(type =>
            {
                var row = rows.FirstOrDefault(r => r.Type == type);
                var health = healthMonitor.GetState(type);
                return new ConnectionSummaryResponse
                {
                    Type = type,
                    Configured = row is not null,
                    UpdatedAtUtc = row?.UpdatedAtUtc,
                    Status = health.Status,
                    CheckedAtUtc = health.CheckedAtUtc
                };
            })
            .ToList();

        return Results.Ok(overview);
    }

    private static async Task<IResult> GetHealthAsync(
        IConnectionStore store,
        IConnectionHealthMonitor healthMonitor,
        CancellationToken cancellationToken)
    {
        return Results.Ok(await ConnectionHealthReporting.BuildAsync(store, healthMonitor, cancellationToken));
    }

    /// <summary>
    /// Optimistic-concurrency guard: if the caller supplied an expected version that no longer
    /// matches the stored one, another edit won the race — reject with 412 rather than clobber it.
    /// </summary>
    private static IResult? VersionConflict(int? expectedVersion, int? storedVersion)
    {
        if (expectedVersion is int expected && storedVersion != expected)
        {
            return Results.Problem(
                title: "Connection was modified",
                detail: $"Expected version {expected} but the stored version is " +
                        $"{(storedVersion?.ToString() ?? "none")}. Reload and try again.",
                statusCode: StatusCodes.Status412PreconditionFailed);
        }

        return null;
    }
}
