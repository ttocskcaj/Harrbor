using Harrbor.Abstractions.Clients;
using Harrbor.Abstractions.Clients.Models;
using Harrbor.Configuration.Connections;
using QBittorrent.Client;

namespace Harrbor.Services.Clients;

/// <summary>
/// qBittorrent-backed <see cref="ITorrentClient"/>. Built from a single connection's settings by
/// the connection manager, which caches one instance per connection id and rebuilds it (a fresh
/// instance) when the connection's version stamp changes. Holds a login session, so it is stateful
/// and disposable; only the login flag mutates after construction.
/// </summary>
public class QBittorrentTorrentClient : ITorrentClient, IDisposable
{
    private readonly QBittorrentSettings _settings;
    private readonly ILogger<QBittorrentTorrentClient> _logger;
    private readonly QBittorrentClient _client;
    private readonly SemaphoreSlim _loginSemaphore = new(1, 1);
    private volatile bool _isLoggedIn;

    public QBittorrentTorrentClient(
        QBittorrentSettings settings,
        ILogger<QBittorrentTorrentClient> logger)
    {
        _settings = settings;
        _logger = logger;
        _client = new QBittorrentClient(new Uri(settings.BaseUrl));
    }

    private static TorrentSnapshot ToSnapshot(TorrentInfo t) => new(
        t.Hash,
        t.Name,
        t.Progress,
        t.ContentPath,
        t.SavePath,
        t.Category,
        t.Tags is null ? [] : [.. t.Tags]);

    private async Task<QBittorrentClient> GetLoggedInClientAsync(CancellationToken cancellationToken)
    {
        if (_isLoggedIn)
            return _client;

        await _loginSemaphore.WaitAsync(cancellationToken);
        try
        {
            if (_isLoggedIn)
                return _client;

            await _client.LoginAsync(_settings.Username, _settings.Password, cancellationToken);
            _isLoggedIn = true;
            _logger.LogDebug("Logged in to qBittorrent");
            return _client;
        }
        catch (QBittorrentClientRequestException ex)
        {
            _logger.LogError(
                "qBittorrent login failed: {StatusCode} - {Message}. BaseUrl: {BaseUrl}, Username: {Username}",
                ex.StatusCode, ex.Message, _settings.BaseUrl, _settings.Username);
            throw;
        }
        finally
        {
            _loginSemaphore.Release();
        }
    }

    private void InvalidateLoginOnAuthError(QBittorrentClientRequestException ex)
    {
        if (ex.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
        {
            _isLoggedIn = false;
            _logger.LogDebug("qBittorrent session invalidated due to {StatusCode}", ex.StatusCode);
        }
    }

    public async Task<IReadOnlyList<TorrentSnapshot>> GetTorrentListAsync(CancellationToken cancellationToken = default)
    {
        var client = await GetLoggedInClientAsync(cancellationToken);
        try
        {
            var torrents = await client.GetTorrentListAsync(new TorrentListQuery(), cancellationToken);
            return [.. torrents.Select(ToSnapshot)];
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

    public async Task<IReadOnlyList<TorrentSnapshot>> GetTorrentListAsync(
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

            return [.. torrents.Select(ToSnapshot)];
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

    public async Task<TorrentSnapshot?> GetTorrentAsync(string hash, CancellationToken cancellationToken = default)
    {
        var client = await GetLoggedInClientAsync(cancellationToken);
        try
        {
            var torrents = await client.GetTorrentListAsync(new TorrentListQuery { Hashes = [hash] }, cancellationToken);
            var torrent = torrents.FirstOrDefault();
            return torrent is null ? null : ToSnapshot(torrent);
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
        var baseUrl = _settings.BaseUrl;
        _logger.LogDebug("Testing connection to qBittorrent at {BaseUrl}", baseUrl);

        try
        {
            using var client = new QBittorrentClient(new Uri(baseUrl));
            await client.LoginAsync(_settings.Username, _settings.Password, cancellationToken);
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
            return new ConnectionTestResult(false, Error: $"API error ({ex.StatusCode}) at {baseUrl}: {ex.Message}");
        }
        catch (HttpRequestException ex)
        {
            var error = ex.InnerException?.Message ?? ex.Message;
            return new ConnectionTestResult(false, Error: $"Cannot reach {baseUrl}: {error}");
        }
        catch (UriFormatException)
        {
            return new ConnectionTestResult(false, Error: $"'{baseUrl}' is not a valid URL");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogDebug(ex, "qBittorrent connectivity check failed");
            return new ConnectionTestResult(false, Error: $"Error connecting to {baseUrl}: {ex.Message}");
        }
    }

    public void Dispose()
    {
        _client.Dispose();
        _loginSemaphore.Dispose();
        GC.SuppressFinalize(this);
    }
}
