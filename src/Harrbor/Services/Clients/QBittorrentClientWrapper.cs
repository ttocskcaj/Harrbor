using Microsoft.Extensions.Options;
using QBittorrent.Client;
using Harrbor.Configuration;

namespace Harrbor.Services.Clients;

public class QBittorrentClientWrapper : IQBittorrentClient, IDisposable
{
    private readonly QBittorrentClient _client;
    private readonly QBittorrentOptions _options;
    private readonly ILogger<QBittorrentClientWrapper> _logger;
    private readonly SemaphoreSlim _loginSemaphore = new(1, 1);
    private volatile bool _isLoggedIn;

    public QBittorrentClientWrapper(
        IOptions<QBittorrentOptions> options,
        ILogger<QBittorrentClientWrapper> logger)
    {
        _options = options.Value;
        _logger = logger;
        _client = new QBittorrentClient(new Uri(_options.BaseUrl));
    }

    private async Task EnsureLoggedInAsync(CancellationToken cancellationToken)
    {
        if (_isLoggedIn)
            return;

        await _loginSemaphore.WaitAsync(cancellationToken);
        try
        {
            // Double-check after acquiring lock
            if (_isLoggedIn)
                return;

            await _client.LoginAsync(_options.Username, _options.Password, cancellationToken);
            _isLoggedIn = true;
            _logger.LogDebug("Logged in to qBittorrent");
        }
        catch (QBittorrentClientRequestException ex)
        {
            _logger.LogError(
                "qBittorrent login failed: {StatusCode} - {Message}. BaseUrl: {BaseUrl}, Username: {Username}",
                ex.StatusCode, ex.Message, _options.BaseUrl, _options.Username);
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

    public async Task<IReadOnlyList<TorrentInfo>> GetTorrentListAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await EnsureLoggedInAsync(cancellationToken);
            return await _client.GetTorrentListAsync(new TorrentListQuery(), cancellationToken);
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
        try
        {
            await EnsureLoggedInAsync(cancellationToken);

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

            var torrents = await _client.GetTorrentListAsync(query, cancellationToken);

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
        try
        {
            await EnsureLoggedInAsync(cancellationToken);
            var torrents = await _client.GetTorrentListAsync(new TorrentListQuery { Hashes = [hash] }, cancellationToken);
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
        try
        {
            await EnsureLoggedInAsync(cancellationToken);
            await _client.DeleteAsync(hash, deleteFiles, cancellationToken);
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
        try
        {
            await EnsureLoggedInAsync(cancellationToken);
            await _client.SetTorrentCategoryAsync(hash, category, cancellationToken);
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
        try
        {
            await EnsureLoggedInAsync(cancellationToken);
            var categories = await _client.GetCategoriesAsync(cancellationToken);
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
        try
        {
            await EnsureLoggedInAsync(cancellationToken);
            var categories = await _client.GetCategoriesAsync(cancellationToken);

            if (!categories.ContainsKey(category))
            {
                _logger.LogInformation("Creating qBittorrent category: {Category}", category);
                await _client.AddCategoryAsync(category, cancellationToken);
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
        var result = await TestConnectionAsync(cancellationToken);
        return result.Success;
    }

    public async Task<ConnectionTestResult> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Testing connection to qBittorrent at {BaseUrl}", _options.BaseUrl);

        try
        {
            await EnsureLoggedInAsync(cancellationToken);
            var version = await _client.GetApiVersionAsync(cancellationToken);
            return new ConnectionTestResult(true, Version: version.ToString());
        }
        catch (QBittorrentClientRequestException ex)
        {
            InvalidateLoginOnAuthError(ex);
            return new ConnectionTestResult(false, Error: $"API error ({ex.StatusCode}) at {_options.BaseUrl}: {ex.Message}");
        }
        catch (HttpRequestException ex)
        {
            var error = ex.InnerException?.Message ?? ex.Message;
            return new ConnectionTestResult(false, Error: $"Cannot reach {_options.BaseUrl}: {error}");
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "qBittorrent connectivity check failed");
            return new ConnectionTestResult(false, Error: $"Error connecting to {_options.BaseUrl}: {ex.Message}");
        }
    }

    public void Dispose()
    {
        _client.Dispose();
        _loginSemaphore.Dispose();
    }
}
