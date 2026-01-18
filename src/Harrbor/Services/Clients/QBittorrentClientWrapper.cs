using Microsoft.Extensions.Options;
using QBittorrent.Client;
using Harrbor.Configuration;

namespace Harrbor.Services.Clients;

public class QBittorrentClientWrapper : IQBittorrentClient, IDisposable
{
    private readonly QBittorrentClient _client;
    private readonly QBittorrentOptions _options;
    private readonly ILogger<QBittorrentClientWrapper> _logger;
    private bool _isLoggedIn;

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

        await _client.LoginAsync(_options.Username, _options.Password, cancellationToken);
        _isLoggedIn = true;
        _logger.LogDebug("Logged in to qBittorrent");
    }

    public async Task<IReadOnlyList<TorrentInfo>> GetTorrentListAsync(CancellationToken cancellationToken = default)
    {
        await EnsureLoggedInAsync(cancellationToken);
        return await _client.GetTorrentListAsync(new TorrentListQuery(), cancellationToken);
    }

    public async Task<TorrentInfo?> GetTorrentAsync(string hash, CancellationToken cancellationToken = default)
    {
        await EnsureLoggedInAsync(cancellationToken);
        var torrents = await _client.GetTorrentListAsync(new TorrentListQuery { Hashes = [hash] }, cancellationToken);
        return torrents.FirstOrDefault();
    }

    public async Task DeleteTorrentAsync(string hash, bool deleteFiles = false, CancellationToken cancellationToken = default)
    {
        await EnsureLoggedInAsync(cancellationToken);
        await _client.DeleteAsync(hash, deleteFiles, cancellationToken);
        _logger.LogInformation("Deleted torrent {Hash} (deleteFiles: {DeleteFiles})", hash, deleteFiles);
    }

    public async Task<bool> IsConnectedAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await EnsureLoggedInAsync(cancellationToken);
            await _client.GetApiVersionAsync(cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "qBittorrent connectivity check failed");
            return false;
        }
    }

    public void Dispose()
    {
        _client.Dispose();
    }
}
