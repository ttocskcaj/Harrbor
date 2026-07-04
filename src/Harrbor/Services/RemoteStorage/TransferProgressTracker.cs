using System.Collections.Concurrent;

namespace Harrbor.Services.RemoteStorage;

/// <summary>
/// Live progress of an in-flight transfer, keyed by release id. <see cref="Progress"/> is a
/// 0..1 fraction. Held only in memory (never persisted) — cleared when the transfer ends.
/// </summary>
public sealed record TransferProgressSnapshot(
    double Progress,
    long SpeedBytesPerSec,
    long BytesTransferred,
    long TotalBytes,
    int? EtaSeconds,
    DateTime UpdatedAtUtc);

/// <summary>
/// Thread-safe store of live transfer progress. rclone reports progress from a background
/// stderr thread, so this avoids touching the (non-thread-safe) DbContext on the hot path.
/// </summary>
public interface ITransferProgressTracker
{
    void Update(int releaseId, TransferProgressSnapshot snapshot);
    void Clear(int releaseId);
    TransferProgressSnapshot? Get(int releaseId);
    IReadOnlyDictionary<int, TransferProgressSnapshot> Snapshot();
}

public sealed class TransferProgressTracker : ITransferProgressTracker
{
    private readonly ConcurrentDictionary<int, TransferProgressSnapshot> _snapshots = new();

    public void Update(int releaseId, TransferProgressSnapshot snapshot) => _snapshots[releaseId] = snapshot;

    public void Clear(int releaseId) => _snapshots.TryRemove(releaseId, out _);

    public TransferProgressSnapshot? Get(int releaseId)
        => _snapshots.TryGetValue(releaseId, out var snapshot) ? snapshot : null;

    public IReadOnlyDictionary<int, TransferProgressSnapshot> Snapshot()
        => new Dictionary<int, TransferProgressSnapshot>(_snapshots);
}

/// <summary>
/// An <see cref="IProgress{T}"/> that invokes the handler synchronously on the calling
/// thread, unlike <see cref="Progress{T}"/> which posts asynchronously. This keeps progress
/// reports ordered with the transfer's own lifecycle (so a final sample can't land after the
/// entry is cleared).
/// </summary>
public sealed class SynchronousProgress<T> : IProgress<T>
{
    private readonly Action<T> _handler;

    public SynchronousProgress(Action<T> handler) => _handler = handler;

    public void Report(T value) => _handler(value);
}
