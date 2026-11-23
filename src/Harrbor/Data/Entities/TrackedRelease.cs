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
    /// Current status of the extraction phase.
    /// </summary>
    public ExtractionStatus ExtractionStatus { get; set; } = ExtractionStatus.Pending;

    /// <summary>
    /// Current status of the media conversion phase.
    /// </summary>
    public ConversionStatus ConversionStatus { get; set; } = ConversionStatus.Pending;

    /// <summary>
    /// True when the release is transferred into the hidden holding directory
    /// (<c>{StagingPath}/.harrbor/{name}</c>) and published to the final staging path only after
    /// all post-transfer phases finish. False for rows created before the holding directory existed.
    /// </summary>
    public bool UsesHoldingPath { get; set; }

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
    /// When archive extraction in staging started.
    /// </summary>
    public DateTime? ExtractionStartedAtUtc { get; set; }

    /// <summary>
    /// When archive extraction in staging completed.
    /// </summary>
    public DateTime? ExtractionCompletedAtUtc { get; set; }

    /// <summary>
    /// When media conversion started.
    /// </summary>
    public DateTime? ConversionStartedAtUtc { get; set; }

    /// <summary>
    /// When media conversion completed (successfully or not).
    /// </summary>
    public DateTime? ConversionCompletedAtUtc { get; set; }

    /// <summary>
    /// Number of files rewritten by converters.
    /// </summary>
    public int FilesConverted { get; set; }

    /// <summary>
    /// Short human-readable summary of the conversion outcome, e.g. "DoviP7ToP81: 1 converted".
    /// </summary>
    public string? ConversionSummary { get; set; }

    /// <summary>
    /// When the release was moved from the holding directory to the final staging path.
    /// </summary>
    public DateTime? PublishedAtUtc { get; set; }

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

    /// <summary>
    /// Name of the hidden directory under <see cref="StagingPath"/> that holds releases until they are published.
    /// </summary>
    public const string HoldingDirectoryName = ".harrbor";

    /// <summary>
    /// The file/directory name of the release item in staging (matches the torrent name).
    /// </summary>
    public string GetItemName() => Path.GetFileName(RemotePath);

    /// <summary>
    /// The path Sonarr/Radarr sees: <c>{StagingPath}/{name}</c>.
    /// </summary>
    public string GetFinalItemPath() => Path.Combine(StagingPath, GetItemName());

    /// <summary>
    /// The hidden holding path: <c>{StagingPath}/.harrbor/{name}</c>.
    /// </summary>
    public string GetHoldingItemPath() => Path.Combine(StagingPath, HoldingDirectoryName, GetItemName());

    /// <summary>
    /// Where the release's files currently live: the holding path until published, otherwise the final path.
    /// </summary>
    public string GetWorkingItemPath() =>
        UsesHoldingPath && PublishedAtUtc == null ? GetHoldingItemPath() : GetFinalItemPath();
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
/// Status of the extraction phase.
/// </summary>
public enum ExtractionStatus
{
    Pending,
    InProgress,
    Completed,
    Failed
}

/// <summary>
/// Status of the media conversion phase.
/// </summary>
public enum ConversionStatus
{
    Pending = 0,
    InProgress = 1,
    Completed = 2,
    Failed = 3,
    Skipped = 4
}
