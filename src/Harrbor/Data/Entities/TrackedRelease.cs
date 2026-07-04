namespace Harrbor.Data.Entities;

/// <summary>
/// Tracks a media release through the orchestration pipeline.
/// </summary>
public class TrackedRelease
{
    /// <summary>
    /// Primary key.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// The download ID from qBittorrent (torrent hash).
    /// </summary>
    public string DownloadId { get; set; } = string.Empty;

    /// <summary>
    /// The name/title of the release.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The job that is processing this release.
    /// </summary>
    public string JobName { get; set; } = string.Empty;

    /// <summary>
    /// The path to the release on the remote seedbox.
    /// </summary>
    public string RemotePath { get; set; } = string.Empty;

    /// <summary>
    /// The path to the release in local staging.
    /// </summary>
    public string StagingPath { get; set; } = string.Empty;

    /// <summary>
    /// Current status of the download phase.
    /// </summary>
    public DownloadStatus DownloadStatus { get; set; } = DownloadStatus.Pending;

    /// <summary>
    /// Current status of the transfer phase.
    /// </summary>
    public TransferStatus TransferStatus { get; set; } = TransferStatus.Pending;

    /// <summary>
    /// Current status of the import phase.
    /// </summary>
    public ImportStatus ImportStatus { get; set; } = ImportStatus.Pending;

    /// <summary>
    /// Current status of the cleanup phase.
    /// </summary>
    public CleanupStatus CleanupStatus { get; set; } = CleanupStatus.Pending;

    /// <summary>
    /// Current status of the archival phase.
    /// </summary>
    public ArchivalStatus ArchivalStatus { get; set; } = ArchivalStatus.Pending;

    /// <summary>
    /// Terminal outcome of the release. <see cref="PipelineOutcome.Active"/> while it is
    /// still moving through (or waiting in) the pipeline; the phase handlers only process
    /// releases whose outcome is Active. Set to a terminal value when the release finishes,
    /// permanently fails, or is cancelled by the operator.
    /// </summary>
    public PipelineOutcome Outcome { get; set; } = PipelineOutcome.Active;

    /// <summary>
    /// Number of errors/retries encountered.
    /// </summary>
    public int ErrorCount { get; set; }

    /// <summary>
    /// The last error message.
    /// </summary>
    public string? LastError { get; set; }

    /// <summary>
    /// When the last error occurred.
    /// </summary>
    public DateTime? LastErrorAtUtc { get; set; }

    /// <summary>
    /// When this release was first tracked.
    /// </summary>
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>
    /// When the download completed on the seedbox.
    /// </summary>
    public DateTime? DownloadCompletedAtUtc { get; set; }

    /// <summary>
    /// When the transfer to local staging started.
    /// </summary>
    public DateTime? TransferStartedAtUtc { get; set; }

    /// <summary>
    /// When the transfer to local staging completed.
    /// </summary>
    public DateTime? TransferCompletedAtUtc { get; set; }

    /// <summary>
    /// When Sonarr/Radarr completed the import.
    /// </summary>
    public DateTime? ImportCompletedAtUtc { get; set; }

    /// <summary>
    /// When staging files were cleaned up.
    /// </summary>
    public DateTime? CleanupCompletedAtUtc { get; set; }

    /// <summary>
    /// When the torrent was archived (category changed).
    /// </summary>
    public DateTime? ArchivedAtUtc { get; set; }
}

/// <summary>
/// Status of the download phase.
/// </summary>
public enum DownloadStatus
{
    Pending,
    Completed
}

/// <summary>
/// Status of the transfer phase.
/// </summary>
public enum TransferStatus
{
    Pending,
    InProgress,
    Completed,
    Failed
}

/// <summary>
/// Status of the import phase.
/// </summary>
public enum ImportStatus
{
    Pending,
    Completed,
    Failed
}

/// <summary>
/// Status of the cleanup phase.
/// </summary>
public enum CleanupStatus
{
    Pending,
    Completed,
    Failed
}

/// <summary>
/// Status of the archival phase.
/// </summary>
public enum ArchivalStatus
{
    Pending,
    Completed,
    Failed
}

/// <summary>
/// Terminal outcome of a release, used to partition active pipeline work from history
/// and to make cancellation/permanent failure durable.
/// </summary>
public enum PipelineOutcome
{
    /// <summary>Still moving through, or waiting in, the pipeline.</summary>
    Active = 0,

    /// <summary>Reached the end of the pipeline (archived).</summary>
    Completed = 1,

    /// <summary>Permanently failed (retries exhausted or an unrecoverable phase failure).</summary>
    Failed = 2,

    /// <summary>Cancelled by the operator.</summary>
    Cancelled = 3
}
