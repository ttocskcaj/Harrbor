using QBittorrent.Client;

namespace Harrbor.Services.Clients;

public interface IQBittorrentClient
{
    Task<IReadOnlyList<TorrentInfo>> GetTorrentListAsync(CancellationToken cancellationToken = default);
    Task<TorrentInfo?> GetTorrentAsync(string hash, CancellationToken cancellationToken = default);
    Task DeleteTorrentAsync(string hash, bool deleteFiles = false, CancellationToken cancellationToken = default);
    Task<bool> IsConnectedAsync(CancellationToken cancellationToken = default);
}
