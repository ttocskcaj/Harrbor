using Harrbor.Abstractions.Clients.Models;

namespace Harrbor.Abstractions.Clients;

/// <summary>
/// Abstraction over a torrent download client (qBittorrent today). Returns Harrbor-owned
/// <see cref="TorrentSnapshot"/> values rather than vendor types so alternative clients can
/// implement it. "Categories" are a torrent-client-level concept surfaced here even though only
/// qBittorrent implements them today.
/// </summary>
public interface ITorrentClient
{
    Task<IReadOnlyList<TorrentSnapshot>> GetTorrentListAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TorrentSnapshot>> GetTorrentListAsync(
        string? category,
        IReadOnlyList<string>? tags,
        CancellationToken cancellationToken = default);

    Task<TorrentSnapshot?> GetTorrentAsync(string hash, CancellationToken cancellationToken = default);
    Task DeleteTorrentAsync(string hash, bool deleteFiles = false, CancellationToken cancellationToken = default);
    Task SetCategoryAsync(string hash, string category, CancellationToken cancellationToken = default);
    Task EnsureCategoryExistsAsync(string category, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetCategoriesAsync(CancellationToken cancellationToken = default);
    Task<bool> IsConnectedAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Tests connectivity using the client's own configured settings.
    /// </summary>
    Task<ConnectionTestResult> TestConnectionAsync(CancellationToken cancellationToken = default);
}
