namespace Harrbor.Abstractions.RemoteStorage;

public record TransferResult(
    bool Success,
    long BytesTransferred = 0,
    TimeSpan Duration = default,
    string? Error = null);

public record RemoteStorageConnectionResult(bool Success, string? Error = null);

/// <summary>A live progress sample parsed from rclone's stats output during a transfer.</summary>
public sealed record RcloneProgress(
    long BytesTransferred,
    long TotalBytes,
    double Percent,
    long SpeedBytesPerSec,
    int? EtaSeconds);

/// <summary>
/// Abstraction over remote file storage (rclone/SFTP today). Built from a specific connection's
/// settings, so connectivity tests use the client's own configured settings.
/// </summary>
public interface IRemoteStorageService
{
    Task<TransferResult> TransferAsync(
        string sourcePath,
        string destinationPath,
        IProgress<RcloneProgress>? progress = null,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(string path, CancellationToken cancellationToken = default);

    Task<RemoteStorageConnectionResult> TestConnectionAsync(CancellationToken cancellationToken = default);
}
