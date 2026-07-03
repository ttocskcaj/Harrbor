using Harrbor.Configuration.Connections;
using Harrbor.Services.Configuration;
using QBittorrent.Client;

namespace Harrbor.Services.Clients;

public class QBittorrentClientWrapper : IQBittorrentClient, IDisposable
{
    /// <summary>
    /// Immutable-per-version snapshot published atomically via a volatile
    /// reference, so concurrent readers always see a consistent
    /// client/settings/version triple. Only the login flag mutates.
    /// </summary>
    private sealed class ClientState(QBittorrentClient client, QBittorrentSettings settings, int version)
    {
        public QBittorrentClient Client { get; } = client;
        public QBittorrentSettings Settings { get; } = settings;
        public int Version { get; } = version;
        public volatile bool IsLoggedIn;
    }

    private readonly IConnectionStore _connectionStore;
    private readonly ILogger<QBittorrentClientWrapper> _logger;
    private readonly SemaphoreSlim _clientSemaphore = new(1, 1);

    // Rebuilt whenever the stored connection's version stamp changes
    private volatile ClientState? _state;

    public QBittorrentClientWrapper(
        IConnectionStore connectionStore,
        ILogger<QBittorrentClientWrapper> logger)
    {
        _connectionStore = connectionStore;
        _logger = logger;
    }

    private async Task<ClientState> GetStateAsync(CancellationToken cancellationToken)
    {
        var stamped = await _connectionStore.GetAsync<QBittorrentSettings>(cancellationToken)
            ?? throw new ServiceNotConfiguredException("qBittorrent");

        var state = _state;
        if (state is not null && state.Version == stamped.Version)
            return state;

        await _clientSemaphore.WaitAsync(cancellationToken);
        try
        {
            state = _state;
            if (state is null || state.Version != stamped.Version)
            {
                // The previous client is deliberately not disposed: an in-flight
                // operation on another thread may still hold it. Config changes
                // are rare; the GC reclaims the old instance.
                state = new ClientState(
                    new QBittorrentClient(new Uri(stamped.Settings.BaseUrl)),
                    stamped.Settings,
                    stamped.Version);
                _state = state;

                _logger.LogInformation(
                    "qBittorrent client (re)built for {BaseUrl} (config version {Version})",
                    stamped.Settings.BaseUrl, stamped.Version);
            }

            return state;
        }
        finally
        {
            _clientSemaphore.Release();
        }
    }

    private async Task<QBittorrentClient> GetLoggedInClientAsync(CancellationToken cancellationToken)
    {
        var state = await GetStateAsync(cancellationToken);

        if (state.IsLoggedIn)
            return state.Client;

        await _clientSemaphore.WaitAsync(cancellationToken);
        try
        {
            if (state.IsLoggedIn)
                return state.Client;

            await state.Client.LoginAsync(state.Settings.Username, state.Settings.Password, cancellationToken);
            state.IsLoggedIn = true;
            _logger.LogDebug("Logged in to qBittorrent");
            return state.Client;
        }
        catch (QBittorrentClientRequestException ex)
        {
            _logger.LogError(
                "qBittorrent login failed: {StatusCode} - {Message}. BaseUrl: {BaseUrl}, Username: {Username}",
                ex.StatusCode, ex.Message, state.Settings.BaseUrl, state.Settings.Username);
            throw;
        }
        finally
        {
            _clientSemaphore.Release();
        }
    }

    private void InvalidateLoginOnAuthError(QBittorrentClientRequestException ex)
    {
        if (ex.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
        {
            var state = _state;
            if (state is not null)
                state.IsLoggedIn = false;
            _logger.LogDebug("qBittorrent session invalidated due to {StatusCode}", ex.StatusCode);
        }
    }

    public async Task<IReadOnlyList<TorrentInfo>> GetTorrentListAsync(CancellationToken cancellationToken = default)
    {
        var client = await GetLoggedInClientAsync(cancellationToken);
        try
        {
            return await client.GetTorrentListAsync(new TorrentListQuery(), cancellationToken);
        }
        catch (QBittorrentClientRequestException ex)
        {
            InvalidateLoginOnAuthError(ex);
            _logger.LogError(
                "qBittorrent GetTorrentList failed: {StatusCode} - {Message}",
                ex.StatusCode, ex.Message);
            throw;
        }
    }

    public async Task<IReadOnlyList<TorrentInfo>> GetTorrentListAsync(
        string? category,
        IReadOnlyList<string>? tags,
        CancellationToken cancellationToken = default)
    {
        var client = await GetLoggedInClientAsync(cancellationToken);
        try
        {
            var query = new TorrentListQuery();

            // Filter by category if specified
            if (!string.IsNullOrEmpty(category))
            {
                query.Category = category;
            }

            // qBittorrent API supports single tag filter natively
            if (tags is { Count: 1 })
            {
                query.Tag = tags[0];
            }

            var torrents = await client.GetTorrentListAsync(query, cancellationToken);

            // Multi-tag filtering must be done client-side
            if (tags is { Count: > 1 })
            {
                torrents = torrents
                    .Where(t => t.Tags != null && tags.All(tag => t.Tags.Contains(tag)))
                    .ToList();
            }

            return torrents;
        }
        catch (QBittorrentClientRequestException ex)
        {
            InvalidateLoginOnAuthError(ex);
            _logger.LogError(
                "qBittorrent GetTorrentList failed: {StatusCode} - {Message}. Category: {Category}, Tags: {Tags}",
                ex.StatusCode, ex.Message, category, tags != null ? string.Join(", ", tags) : "none");
            throw;
        }
    }

    public async Task<TorrentInfo?> GetTorrentAsync(string hash, CancellationToken cancellationToken = default)
    {
        var client = await GetLoggedInClientAsync(cancellationToken);
        try
        {
            var torrents = await client.GetTorrentListAsync(new TorrentListQuery { Hashes = [hash] }, cancellationToken);
            return torrents.FirstOrDefault();
        }
        catch (QBittorrentClientRequestException ex)
        {
            InvalidateLoginOnAuthError(ex);
            _logger.LogError(
                "qBittorrent GetTorrent failed: {StatusCode} - {Message}. Hash: {Hash}",
                ex.StatusCode, ex.Message, hash);
            throw;
        }
    }

    public async Task DeleteTorrentAsync(string hash, bool deleteFiles = false, CancellationToken cancellationToken = default)
    {
        var client = await GetLoggedInClientAsync(cancellationToken);
        try
        {
            await client.DeleteAsync(hash, deleteFiles, cancellationToken);
            _logger.LogInformation("Deleted torrent {Hash} (deleteFiles: {DeleteFiles})", hash, deleteFiles);
        }
        catch (QBittorrentClientRequestException ex)
        {
            InvalidateLoginOnAuthError(ex);
            _logger.LogError(
                "qBittorrent DeleteTorrent failed: {StatusCode} - {Message}. Hash: {Hash}, DeleteFiles: {DeleteFiles}",
                ex.StatusCode, ex.Message, hash, deleteFiles);
            throw;
        }
    }

    public async Task SetCategoryAsync(string hash, string category, CancellationToken cancellationToken = default)
    {
        var client = await GetLoggedInClientAsync(cancellationToken);
        try
        {
            await client.SetTorrentCategoryAsync(hash, category, cancellationToken);
            _logger.LogInformation("Set category for torrent {Hash} to {Category}", hash, category);
        }
        catch (QBittorrentClientRequestException ex)
        {
            var hint = ex.StatusCode == System.Net.HttpStatusCode.Conflict
                ? " (409 Conflict typically means the category doesn't exist in qBittorrent)"
                : "";

            InvalidateLoginOnAuthError(ex);
            _logger.LogError(
                "qBittorrent SetCategory failed: {StatusCode} - {Message}. Hash: {Hash}, Category: {Category}{Hint}",
                ex.StatusCode, ex.Message, hash, category, hint);
            throw;
        }
    }

    public async Task<IReadOnlyList<string>> GetCategoriesAsync(CancellationToken cancellationToken = default)
    {
        var client = await GetLoggedInClientAsync(cancellationToken);
        try
        {
            var categories = await client.GetCategoriesAsync(cancellationToken);
            return categories.Keys.ToList();
        }
        catch (QBittorrentClientRequestException ex)
        {
            InvalidateLoginOnAuthError(ex);
            _logger.LogError(
                "qBittorrent GetCategories failed: {StatusCode} - {Message}",
                ex.StatusCode, ex.Message);
            throw;
        }
    }

    public async Task EnsureCategoryExistsAsync(string category, CancellationToken cancellationToken = default)
    {
        var client = await GetLoggedInClientAsync(cancellationToken);
        try
        {
            var categories = await client.GetCategoriesAsync(cancellationToken);

            if (!categories.ContainsKey(category))
            {
                _logger.LogInformation("Creating qBittorrent category: {Category}", category);
                await client.AddCategoryAsync(category, cancellationToken);
            }
        }
        catch (QBittorrentClientRequestException ex)
        {
            InvalidateLoginOnAuthError(ex);
            _logger.LogError(
                "qBittorrent EnsureCategoryExists failed: {StatusCode} - {Message}. Category: {Category}",
                ex.StatusCode, ex.Message, category);
            throw;
        }
    }

    public async Task<bool> IsConnectedAsync(CancellationToken cancellationToken = default)
    {
        // Health polls run frequently — reuse the cached session instead of
        // performing a throwaway login per poll (TestConnectionAsync's job)
        try
        {
            var client = await GetLoggedInClientAsync(cancellationToken);
            await client.GetApiVersionAsync(cancellationToken);
            return true;
        }
        catch (ServiceNotConfiguredException)
        {
            return false;
        }
        catch (QBittorrentClientRequestException ex)
        {
            InvalidateLoginOnAuthError(ex);
            return false;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogDebug(ex, "qBittorrent connectivity check failed");
            return false;
        }
    }

    public async Task<ConnectionTestResult> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        var stamped = await _connectionStore.GetAsync<QBittorrentSettings>(cancellationToken);
        if (stamped is null)
            return new ConnectionTestResult(false, Error: "qBittorrent is not configured");

        return await TestConnectionAsync(stamped.Settings, cancellationToken);
    }

    public async Task<ConnectionTestResult> TestConnectionAsync(QBittorrentSettings settings, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Testing connection to qBittorrent at {BaseUrl}", settings.BaseUrl);

        try
        {
            using var client = new QBittorrentClient(new Uri(settings.BaseUrl));
            await client.LoginAsync(settings.Username, settings.Password, cancellationToken);
            var version = await client.GetApiVersionAsync(cancellationToken);
            return new ConnectionTestResult(true, Version: version.ToString());
        }
        catch (QBittorrentClientRequestException ex)
            when (ex.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
        {
            return new ConnectionTestResult(false, Error: "Authentication failed — check the username and password.");
        }
        catch (QBittorrentClientRequestException ex)
        {
            return new ConnectionTestResult(false, Error: $"API error ({ex.StatusCode}) at {settings.BaseUrl}: {ex.Message}");
        }
        catch (HttpRequestException ex)
        {
            var error = ex.InnerException?.Message ?? ex.Message;
            return new ConnectionTestResult(false, Error: $"Cannot reach {settings.BaseUrl}: {error}");
        }
        catch (UriFormatException)
        {
            return new ConnectionTestResult(false, Error: $"'{settings.BaseUrl}' is not a valid URL");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogDebug(ex, "qBittorrent connectivity check failed");
            return new ConnectionTestResult(false, Error: $"Error connecting to {settings.BaseUrl}: {ex.Message}");
        }
    }

    public void Dispose()
    {
        _state?.Client.Dispose();
        _clientSemaphore.Dispose();
    }
}
