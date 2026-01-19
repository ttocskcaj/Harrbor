namespace Harrbor.Services.RemoteStorage;

public record TransferResult(
    bool Success,
    long BytesTransferred = 0,
    TimeSpan Duration = default,
    string? Error = null);

public record RemoteStorageConnectionResult(bool Success, string? Error = null);

public interface IRemoteStorageService
{
    Task<TransferResult> TransferAsync(
        string sourcePath,
        string destinationPath,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(string path, CancellationToken cancellationToken = default);

    Task<RemoteStorageConnectionResult> TestConnectionAsync(CancellationToken cancellationToken = default);
}
