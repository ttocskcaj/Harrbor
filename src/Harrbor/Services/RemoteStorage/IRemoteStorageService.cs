using Harrbor.Configuration.Connections;

namespace Harrbor.Services.RemoteStorage;

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

public interface IRemoteStorageService
{
    Task<TransferResult> TransferAsync(
        string sourcePath,
        string destinationPath,
        IProgress<RcloneProgress>? progress = null,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(string path, CancellationToken cancellationToken = default);

    Task<RemoteStorageConnectionResult> TestConnectionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Tests connectivity using an explicit (possibly not-yet-saved) settings payload.
    /// </summary>
    Task<RemoteStorageConnectionResult> TestConnectionAsync(RemoteStorageSettings settings, CancellationToken cancellationToken = default);
}
