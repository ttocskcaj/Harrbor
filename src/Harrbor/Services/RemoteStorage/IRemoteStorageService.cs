namespace Harrbor.Services.RemoteStorage;

public record TransferResult(
    bool Success,
    long BytesTransferred = 0,
    TimeSpan Duration = default,
    string? Error = null);

public record RemoteStorageConnectionResult(bool Success, string? Error = null);

/// <summary>
/// Thrown when the remote could not be queried, so the result of an operation is unknown
/// (as opposed to the remote positively reporting a result).
/// </summary>
public class RemoteStorageUnavailableException(string message) : Exception(message);

public interface IRemoteStorageService
{
    Task<TransferResult> TransferAsync(
        string sourcePath,
        string destinationPath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether a path exists on the remote.
    /// </summary>
    /// <returns>true if the path exists; false only if the remote positively reported it as not found.</returns>
    /// <exception cref="RemoteStorageUnavailableException">
    /// The check could not be completed (e.g. connection refused, auth failure), so existence is unknown.
    /// </exception>
    Task<bool> ExistsAsync(string path, CancellationToken cancellationToken = default);

    Task<RemoteStorageConnectionResult> TestConnectionAsync(CancellationToken cancellationToken = default);
}
