namespace Harrbor.Services.RemoteStorage;

/// <param name="Interrupted">
/// The transfer was stopped from outside (rclone got SIGINT/SIGTERM, e.g. during a shutdown) rather than failing.
/// </param>
public record TransferResult(
    bool Success,
    long BytesTransferred = 0,
    TimeSpan Duration = default,
    string? Error = null,
    bool Interrupted = false);

public record RemoteStorageConnectionResult(bool Success, string? Error = null);

/// <summary>
/// Thrown when the remote could not be queried, so the result of an operation is unknown
/// (as opposed to the remote positively reporting a result).
/// </summary>
public class RemoteStorageUnavailableException(string message) : Exception(message);

/// <summary>What a remote path refers to.</summary>
public enum RemoteItemKind
{
    Missing,
    File,
    Directory
}

public interface IRemoteStorageService
{
    /// <summary>
    /// Copies a remote directory's contents into <paramref name="destinationPath"/> (<c>rclone copy</c>).
    /// </summary>
    Task<TransferResult> TransferAsync(
        string sourcePath,
        string destinationPath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Copies a single remote file to exactly <paramref name="destinationPath"/> (<c>rclone copyto</c>), so the result
    /// is a file and not a directory named like the file.
    /// </summary>
    Task<TransferResult> TransferFileAsync(
        string sourcePath,
        string destinationPath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reports whether a remote path is a file, a directory, or missing.
    /// </summary>
    /// <returns><see cref="RemoteItemKind.Missing"/> only if the remote positively reported the path as not found.</returns>
    /// <exception cref="RemoteStorageUnavailableException">
    /// The check could not be completed (e.g. connection refused, auth failure), so the result is unknown.
    /// </exception>
    Task<RemoteItemKind> GetItemKindAsync(string path, CancellationToken cancellationToken = default);

    Task<RemoteStorageConnectionResult> TestConnectionAsync(CancellationToken cancellationToken = default);
}
