using System.Collections.Concurrent;
using System.Text.Json;
using Harrbor.Abstractions.Clients;
using Harrbor.Abstractions.Connections;
using Harrbor.Abstractions.Providers;
using Harrbor.Abstractions.RemoteStorage;
using Harrbor.Data.Entities;
using Harrbor.Services.Configuration;

namespace Harrbor.Services;

/// <summary>
/// The runtime registry of live connection clients, keyed by connection id. Replaces keyed DI and
/// the per-category resolvers: given a connection id, it loads the row, finds the provider, and
/// builds (and caches) the category client, rebuilding when the connection's version stamp changes.
/// Singleton — connection instances are runtime data, not DI registrations.
/// </summary>
public interface IConnectionManager
{
    Task<IMediaService> GetMediaServiceAsync(int connectionId, CancellationToken cancellationToken = default);
    Task<ITorrentClient> GetTorrentClientAsync(int connectionId, CancellationToken cancellationToken = default);
    Task<IRemoteStorageService> GetRemoteStorageAsync(int connectionId, CancellationToken cancellationToken = default);

    /// <summary>Builds/returns the cached client for an already-loaded connection row (used by the health monitor).</summary>
    Task<object> GetClientAsync(Connection connection, CancellationToken cancellationToken = default);

    /// <summary>Drops (and disposes) the cached client for a connection — call after delete/disable.</summary>
    void Invalidate(int connectionId);
}

public sealed class ConnectionManager : IConnectionManager, IDisposable
{
    private sealed record Entry(int Version, object Client);

    private readonly IConnectionStore _store;
    private readonly IConnectionProviderRegistry _registry;
    private readonly ClientBuildContext _buildContext;
    private readonly ILogger<ConnectionManager> _logger;
    private readonly ConcurrentDictionary<int, Entry> _cache = new();
    private readonly ConcurrentDictionary<int, SemaphoreSlim> _locks = new();

    public ConnectionManager(
        IConnectionStore store,
        IConnectionProviderRegistry registry,
        IHttpClientFactory httpClientFactory,
        ILoggerFactory loggerFactory)
    {
        _store = store;
        _registry = registry;
        _buildContext = new ClientBuildContext(httpClientFactory, loggerFactory);
        _logger = loggerFactory.CreateLogger<ConnectionManager>();
    }

    public async Task<IMediaService> GetMediaServiceAsync(int connectionId, CancellationToken cancellationToken = default)
        => (IMediaService)await GetForCategoryAsync(connectionId, ConnectionCategory.MediaService, cancellationToken);

    public async Task<ITorrentClient> GetTorrentClientAsync(int connectionId, CancellationToken cancellationToken = default)
        => (ITorrentClient)await GetForCategoryAsync(connectionId, ConnectionCategory.DownloadClient, cancellationToken);

    public async Task<IRemoteStorageService> GetRemoteStorageAsync(int connectionId, CancellationToken cancellationToken = default)
        => (IRemoteStorageService)await GetForCategoryAsync(connectionId, ConnectionCategory.RemoteStorage, cancellationToken);

    private async Task<object> GetForCategoryAsync(int connectionId, ConnectionCategory expectedCategory, CancellationToken cancellationToken)
    {
        var connection = await _store.GetByIdAsync(connectionId, cancellationToken)
            ?? throw new ServiceNotConfiguredException($"connection {connectionId}");

        if (!connection.Enabled)
            throw new ServiceNotConfiguredException($"connection '{connection.Name}' (disabled)");

        var provider = _registry.Find(connection.ProviderKey)
            ?? throw new ServiceNotConfiguredException($"connection '{connection.Name}' (unknown provider '{connection.ProviderKey}')");

        if (provider.Category != expectedCategory)
            throw new InvalidOperationException(
                $"Connection {connectionId} ('{connection.Name}') is a {provider.Category} connection but a {expectedCategory} was expected.");

        return await GetClientAsync(connection, cancellationToken);
    }

    public async Task<object> GetClientAsync(Connection connection, CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(connection.Id, out var cached) && cached.Version == connection.Version)
            return cached.Client;

        var gate = _locks.GetOrAdd(connection.Id, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (_cache.TryGetValue(connection.Id, out cached) && cached.Version == connection.Version)
                return cached.Client;

            var provider = _registry.Find(connection.ProviderKey)
                ?? throw new ServiceNotConfiguredException($"connection '{connection.Name}' (unknown provider '{connection.ProviderKey}')");

            var settings = JsonSerializer.Deserialize(connection.SettingsJson, provider.SettingsType, ConnectionJson.Options)
                ?? throw new InvalidOperationException($"Connection {connection.Id} ('{connection.Name}') has empty settings.");

            var client = provider.CreateClient(settings, _buildContext);

            // The previous client is intentionally not disposed on a version-bump rebuild: an
            // in-flight operation may still hold it, config changes are rare, and the GC reclaims it.
            _cache[connection.Id] = new Entry(connection.Version, client);
            _logger.LogInformation(
                "Built {Provider} client for connection {Id} ('{Name}', version {Version})",
                connection.ProviderKey, connection.Id, connection.Name, connection.Version);
            return client;
        }
        finally
        {
            gate.Release();
        }
    }

    public void Invalidate(int connectionId)
    {
        if (_cache.TryRemove(connectionId, out var entry) && entry.Client is IDisposable disposable)
            disposable.Dispose();
    }

    public void Dispose()
    {
        foreach (var entry in _cache.Values)
            if (entry.Client is IDisposable disposable)
                disposable.Dispose();
        _cache.Clear();

        foreach (var gate in _locks.Values)
            gate.Dispose();
        _locks.Clear();
    }
}
