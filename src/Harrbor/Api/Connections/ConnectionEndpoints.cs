using System.Text.Json;
using System.Text.Json.Nodes;
using Harrbor.Abstractions.Connections;
using Harrbor.Abstractions.Providers;
using Harrbor.Data;
using Harrbor.Data.Entities;
using Harrbor.Services;
using Harrbor.Services.Configuration;
using Harrbor.Services.Health;
using Microsoft.EntityFrameworkCore;

namespace Harrbor.Api.Connections;

/// <summary>
/// Generic, provider-agnostic connection API. There are no per-provider endpoints: settings travel
/// as a JSON object validated against the provider's declared schema, secrets are merged/redacted
/// generically, and the UI renders forms from <c>GET /providers</c>.
/// </summary>
public static class ConnectionEndpoints
{
    // Caps test-connection latency; the resilience handler would otherwise retry an unreachable host.
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(15);

    public static IEndpointRouteBuilder MapConnectionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/connections");

        group.MapGet("/providers", GetProviders);
        group.MapGet("/", ListAsync);
        group.MapGet("/health", GetHealthAsync);
        group.MapPost("/", CreateAsync);
        group.MapPost("/test", TestNewAsync);
        group.MapGet("/{id:int}", GetAsync);
        group.MapPut("/{id:int}", UpdateAsync);
        group.MapDelete("/{id:int}", DeleteAsync);
        group.MapPost("/{id:int}/test", TestExistingAsync);
        group.MapGet("/{id:int}/categories", GetCategoriesAsync);

        return app;
    }

    // ----- Providers (schema) -----

    private static IResult GetProviders(IConnectionProviderRegistry registry)
        => Results.Ok(registry.All.Select(ToProviderResponse).ToList());

    // ----- List / get -----

    private static async Task<IResult> ListAsync(
        string? category,
        IConnectionStore store,
        IConnectionProviderRegistry registry,
        IConnectionHealthMonitor monitor,
        CancellationToken cancellationToken)
    {
        var connections = await store.GetAllAsync(cancellationToken);
        var result = new List<ConnectionResponse>();

        foreach (var connection in connections)
        {
            var provider = registry.Find(connection.ProviderKey);
            if (provider is null)
                continue;
            if (category is not null && !provider.Category.ToString().Equals(category, StringComparison.OrdinalIgnoreCase))
                continue;

            result.Add(ToResponse(connection, provider, monitor.GetState(connection.Id)));
        }

        return Results.Ok(result);
    }

    private static async Task<IResult> GetAsync(
        int id,
        IConnectionStore store,
        IConnectionProviderRegistry registry,
        IConnectionHealthMonitor monitor,
        CancellationToken cancellationToken)
    {
        var connection = await store.GetByIdAsync(id, cancellationToken);
        if (connection is null)
            return Results.NotFound();

        var provider = registry.Find(connection.ProviderKey);
        if (provider is null)
            return Results.NotFound();

        return Results.Ok(ToResponse(connection, provider, monitor.GetState(connection.Id)));
    }

    // ----- Create / update / delete -----

    private static async Task<IResult> CreateAsync(
        ConnectionCreateRequest request,
        IConnectionStore store,
        IConnectionProviderRegistry registry,
        CancellationToken cancellationToken)
    {
        var provider = registry.Find(request.ProviderKey);
        if (provider is null)
            return UnknownProvider(request.ProviderKey);

        var errors = ConnectionSettingsMapper.Validate(provider, request.Settings, stored: null);
        if (string.IsNullOrWhiteSpace(request.Name))
            errors.Add("name", "Name is required.");
        if (errors.HasErrors)
            return Results.ValidationProblem(errors.Build());

        var merged = ConnectionSettingsMapper.Merge(provider, request.Settings, stored: null);
        var json = JsonSerializer.Serialize(merged, provider.SettingsType, ConnectionJson.Options);

        try
        {
            var created = await store.CreateAsync(new Connection
            {
                ProviderKey = provider.Key,
                Name = request.Name.Trim(),
                Enabled = request.Enabled,
                SettingsJson = json,
            }, cancellationToken);

            return Results.Created($"/api/connections/{created.Id}", ToResponse(created, provider, null));
        }
        catch (DbUpdateException)
        {
            return DuplicateName(provider.DisplayName, request.Name);
        }
    }

    private static async Task<IResult> UpdateAsync(
        int id,
        ConnectionUpdateRequest request,
        IConnectionStore store,
        IConnectionProviderRegistry registry,
        IConnectionManager manager,
        CancellationToken cancellationToken)
    {
        var existing = await store.GetByIdAsync(id, cancellationToken);
        if (existing is null)
            return Results.NotFound();

        var provider = registry.Find(existing.ProviderKey);
        if (provider is null)
            return Results.NotFound();

        if (VersionConflict(request.ExpectedVersion, existing.Version) is { } conflict)
            return conflict;

        var storedSettings = Deserialize(provider, existing.SettingsJson);

        var errors = ConnectionSettingsMapper.Validate(provider, request.Settings, storedSettings);
        var name = (request.Name ?? existing.Name).Trim();
        if (string.IsNullOrWhiteSpace(name))
            errors.Add("name", "Name is required.");
        if (errors.HasErrors)
            return Results.ValidationProblem(errors.Build());

        var merged = ConnectionSettingsMapper.Merge(provider, request.Settings, storedSettings);
        var json = JsonSerializer.Serialize(merged, provider.SettingsType, ConnectionJson.Options);

        try
        {
            var updated = await store.UpdateAsync(id, name, request.Enabled ?? existing.Enabled, json, cancellationToken);
            manager.Invalidate(id);
            return Results.Ok(ToResponse(updated!, provider, null));
        }
        catch (DbUpdateException)
        {
            return DuplicateName(provider.DisplayName, name);
        }
    }

    private static async Task<IResult> DeleteAsync(
        int id,
        IConnectionStore store,
        IConnectionManager manager,
        HarrborDbContext db,
        CancellationToken cancellationToken)
    {
        var existing = await store.GetByIdAsync(id, cancellationToken);
        if (existing is null)
            return Results.NotFound();

        var dependents = await db.Jobs
            .Where(j => j.MediaServiceConnectionId == id
                || j.DownloadClientConnectionId == id
                || j.RemoteStorageConnectionId == id)
            .Select(j => new { j.Id, j.Name })
            .ToListAsync(cancellationToken);

        if (dependents.Count > 0)
        {
            return Results.Json(new ConnectionDeleteConflictResponse
            {
                Message = $"Connection '{existing.Name}' is used by {dependents.Count} job(s) and cannot be deleted.",
                DependentJobIds = dependents.Select(d => d.Id).ToList(),
                DependentJobNames = dependents.Select(d => d.Name).ToList(),
            }, statusCode: StatusCodes.Status409Conflict);
        }

        await store.DeleteAsync(id, cancellationToken);
        manager.Invalidate(id);
        return Results.NoContent();
    }

    // ----- Test -----

    private static async Task<IResult> TestNewAsync(
        ConnectionTestRequest request,
        IConnectionProviderRegistry registry,
        IHttpClientFactory httpClientFactory,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var provider = registry.Find(request.ProviderKey);
        if (provider is null)
            return UnknownProvider(request.ProviderKey);

        return await RunProviderTestAsync(provider, request.Settings, stored: null, httpClientFactory, loggerFactory, cancellationToken);
    }

    private static async Task<IResult> TestExistingAsync(
        int id,
        JsonObject settings,
        IConnectionStore store,
        IConnectionProviderRegistry registry,
        IHttpClientFactory httpClientFactory,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var existing = await store.GetByIdAsync(id, cancellationToken);
        if (existing is null)
            return Results.NotFound();

        var provider = registry.Find(existing.ProviderKey);
        if (provider is null)
            return Results.NotFound();

        var storedSettings = Deserialize(provider, existing.SettingsJson);
        return await RunProviderTestAsync(provider, settings, storedSettings, httpClientFactory, loggerFactory, cancellationToken);
    }

    private static async Task<IResult> RunProviderTestAsync(
        IConnectionProvider provider,
        JsonObject wire,
        object? stored,
        IHttpClientFactory httpClientFactory,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var errors = ConnectionSettingsMapper.Validate(provider, wire, stored);
        if (errors.HasErrors)
            return Results.ValidationProblem(errors.Build());

        var merged = ConnectionSettingsMapper.Merge(provider, wire, stored);
        var context = new ClientBuildContext(httpClientFactory, loggerFactory);

        return await RunTestAsync(async token =>
        {
            var client = provider.CreateClient(merged, context);
            try
            {
                var result = await provider.TestAsync(client, token);
                return new ConnectionTestResponse
                {
                    Success = result.Success,
                    Message = result.Success
                        ? result.Version is null ? "Connection successful." : $"Connected — version {result.Version}."
                        : null,
                    Error = result.Error,
                };
            }
            finally
            {
                if (client is IDisposable disposable)
                    disposable.Dispose();
            }
        }, cancellationToken);
    }

    // ----- Download-client capability: categories -----

    private static async Task<IResult> GetCategoriesAsync(
        int id,
        IConnectionStore store,
        IConnectionProviderRegistry registry,
        IConnectionManager manager,
        CancellationToken cancellationToken)
    {
        var existing = await store.GetByIdAsync(id, cancellationToken);
        if (existing is null)
            return Results.NotFound();

        var provider = registry.Find(existing.ProviderKey);
        if (provider is null || provider.Category != ConnectionCategory.DownloadClient)
            return Results.Problem(
                detail: "This connection type does not support categories.",
                statusCode: StatusCodes.Status400BadRequest);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TestTimeout);

        try
        {
            var client = await manager.GetTorrentClientAsync(id, cts.Token);
            var categories = await client.GetCategoriesAsync(cts.Token);
            return Results.Ok(categories);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Results.Problem(detail: "Timed out contacting the download client.", statusCode: StatusCodes.Status504GatewayTimeout);
        }
        catch (Exception ex)
        {
            return Results.Problem(detail: $"Could not load categories: {ex.Message}", statusCode: StatusCodes.Status502BadGateway);
        }
    }

    // ----- Health -----

    private static async Task<IResult> GetHealthAsync(
        IConnectionStore store,
        IConnectionProviderRegistry registry,
        IConnectionHealthMonitor monitor,
        CancellationToken cancellationToken)
        => Results.Ok(await ConnectionHealthReporting.BuildAsync(store, registry, monitor, cancellationToken));

    // ----- Helpers -----

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
                Error = $"Connection test timed out after {TestTimeout.TotalSeconds:0} seconds.",
            });
        }
    }

    private static object Deserialize(IConnectionProvider provider, string settingsJson)
        => JsonSerializer.Deserialize(settingsJson, provider.SettingsType, ConnectionJson.Options)
           ?? Activator.CreateInstance(provider.SettingsType)!;

    private static ConnectionResponse ToResponse(Connection connection, IConnectionProvider provider, ConnectionHealthState? health)
    {
        var settings = Deserialize(provider, connection.SettingsJson);
        return new ConnectionResponse
        {
            Id = connection.Id,
            ProviderKey = connection.ProviderKey,
            Category = provider.Category,
            Name = connection.Name,
            Enabled = connection.Enabled,
            Settings = ConnectionSettingsMapper.Redact(provider, settings),
            Version = connection.Version,
            UpdatedAtUtc = connection.UpdatedAtUtc,
            Status = health?.Status,
            CheckedAtUtc = health?.CheckedAtUtc,
        };
    }

    private static ProviderResponse ToProviderResponse(IConnectionProvider provider) => new()
    {
        Key = provider.Key,
        Category = provider.Category,
        DisplayName = provider.DisplayName,
        ProbeEveryNTicks = provider.ProbeEveryNTicks,
        Fields = provider.Schema.Select(ToFieldDto).ToList(),
    };

    private static FieldSchemaDto ToFieldDto(FieldDescriptor field) => new()
    {
        Name = field.Name,
        Type = field.Type,
        Kind = field.Kind,
        Label = field.Label,
        HelpText = field.HelpText,
        Secret = field.Secret,
        Required = field.Required,
        Min = field.Min,
        Max = field.Max,
        ForbiddenChars = field.ForbiddenChars,
        ForbidWhitespace = field.ForbidWhitespace,
        Pattern = field.Pattern,
        Element = field.Element is null ? null : new ListElementRulesDto
        {
            RequireNonEmpty = field.Element.RequireNonEmpty,
            ForbidWhitespace = field.Element.ForbidWhitespace,
            ForbiddenChars = field.Element.ForbiddenChars,
        },
        Default = field.Default,
        Order = field.Order,
    };

    private static IResult UnknownProvider(string providerKey)
        => Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["providerKey"] = [$"Unknown provider '{providerKey}'."],
        });

    private static IResult DuplicateName(string providerDisplayName, string name)
        => Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["name"] = [$"A {providerDisplayName} connection named '{name}' already exists."],
        });

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
