using QBittorrent.Client;

namespace Harrbor.Services.Clients;

public record ConnectionTestResult(bool Success, string? Version = null, string? Error = null);

public interface IQBittorrentClient
{
    Task<IReadOnlyList<TorrentInfo>> GetTorrentListAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TorrentInfo>> GetTorrentListAsync(
        string? category,
        IReadOnlyList<string>? tags,
        CancellationToken cancellationToken = default);

    Task<TorrentInfo?> GetTorrentAsync(string hash, CancellationToken cancellationToken = default);
    Task DeleteTorrentAsync(string hash, bool deleteFiles = false, CancellationToken cancellationToken = default);
    Task SetCategoryAsync(string hash, string category, CancellationToken cancellationToken = default);
    Task EnsureCategoryExistsAsync(string category, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetCategoriesAsync(CancellationToken cancellationToken = default);
    Task<bool> IsConnectedAsync(CancellationToken cancellationToken = default);
    Task<ConnectionTestResult> TestConnectionAsync(CancellationToken cancellationToken = default);
}
