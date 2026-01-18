namespace Harrbor.Data.Entities;

public class TrackedTorrent
{
    public int Id { get; set; }
    public string InfoHash { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public TorrentCategory Category { get; set; }

    public TransferStatus TransferStatus { get; set; }
    public ImportStatus ImportStatus { get; set; }
    public CleanupStatus CleanupStatus { get; set; }
    public ArchivalStatus ArchivalStatus { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? TransferStartedAt { get; set; }
    public DateTimeOffset? TransferCompletedAt { get; set; }
    public DateTimeOffset? ImportCompletedAt { get; set; }
    public DateTimeOffset? CleanupCompletedAt { get; set; }
    public DateTimeOffset? ArchivedAt { get; set; }

    public int ErrorCount { get; set; }
    public string? LastError { get; set; }
    public DateTimeOffset? LastErrorAt { get; set; }
}

public enum TorrentCategory
{
    TV,
    Movies
}

public enum TransferStatus
{
    Pending,
    InProgress,
    Completed,
    Failed
}

public enum ImportStatus
{
    Pending,
    InProgress,
    Completed,
    Failed,
    Skipped
}

public enum CleanupStatus
{
    Pending,
    Completed,
    Failed,
    Skipped
}

public enum ArchivalStatus
{
    Pending,
    Completed,
    Failed,
    Skipped
}
