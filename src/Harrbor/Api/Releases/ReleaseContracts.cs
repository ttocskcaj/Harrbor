namespace Harrbor.Api.Releases;

/// <summary>
/// The phase a release currently sits in, derived from its per-phase status enums.
/// <see cref="Discover"/> is transient (a release is tracked already downloading) and
/// only appears in the timeline; <see cref="Done"/> means the pipeline is complete.
/// </summary>
public enum ReleasePhase
{
    Discover,
    Download,
    Transfer,
    Extraction,
    Import,
    Cleanup,
    Archival,
    Done
}

/// <summary>
/// The at-a-glance health of a release, derived from its outcome, phase statuses,
/// retry state, and time-in-phase.
/// </summary>
public enum ReleaseStatus
{
    /// <summary>Moving through the pipeline normally.</summary>
    Flowing,

    /// <summary>A transfer failed but is under the retry limit and will be retried.</summary>
    Retrying,

    /// <summary>Still active but has sat in its phase past a soft threshold.</summary>
    Stuck,

    /// <summary>Permanently failed.</summary>
    Failed,

    /// <summary>Cancelled by the operator.</summary>
    Cancelled,

    /// <summary>Completed the pipeline (archived).</summary>
    Done
}

/// <summary>State of a single phase within a release's timeline.</summary>
public enum PhaseTimelineState
{
    Pending,
    Current,
    Done,
    Failed
}

public class PagedResponse<T>
{
    public List<T> Items { get; set; } = [];
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int Total { get; set; }
}

public class ReleaseListItem
{
    public int Id { get; set; }
    public string DownloadId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string JobName { get; set; } = string.Empty;
    public ReleasePhase CurrentPhase { get; set; }
    public ReleaseStatus Status { get; set; }

    /// <summary>When the release entered its current phase (for a "time in phase" readout).</summary>
    public DateTime? PhaseEnteredAtUtc { get; set; }
    public long TimeInPhaseSeconds { get; set; }

    public int ErrorCount { get; set; }
    public string? LastError { get; set; }
    public DateTime? LastErrorAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>The owning job's transfer retry limit; null if the job no longer exists.</summary>
    public int? MaxRetries { get; set; }

    /// <summary>When a retrying transfer becomes eligible for its next attempt.</summary>
    public DateTime? NextRetryAtUtc { get; set; }

    // Live transfer metrics, populated from the in-memory progress tracker while a
    // transfer is in progress. Null when not transferring (or before the tracker exists).
    public double? TransferProgress { get; set; }
    public long? TransferSpeedBytesPerSec { get; set; }
    public long? TransferBytesTransferred { get; set; }
    public long? TransferTotalBytes { get; set; }
    public int? TransferEtaSeconds { get; set; }
}

public class ReleaseDetail : ReleaseListItem
{
    public string RemotePath { get; set; } = string.Empty;
    public string StagingPath { get; set; } = string.Empty;
    public List<PhaseTimelineEntry> Timeline { get; set; } = [];
    public TransferDetail? Transfer { get; set; }
}

public class PhaseTimelineEntry
{
    public ReleasePhase Phase { get; set; }
    public PhaseTimelineState State { get; set; }
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
}

public class RetryReleaseRequest
{
    /// <summary>Which phase to reset and re-run; defaults to the current failed phase.</summary>
    public ReleasePhase? Phase { get; set; }
}

public class CancelReleaseRequest
{
    /// <summary>Also remove the torrent from qBittorrent (default true).</summary>
    public bool DeleteTorrent { get; set; } = true;

    /// <summary>When removing the torrent, also delete its downloaded files on the seedbox.</summary>
    public bool DeleteFiles { get; set; }
}

/// <summary>Result of a release action: the updated release plus an optional non-fatal warning.</summary>
public class ReleaseActionResponse
{
    public ReleaseDetail Release { get; set; } = new();
    public string? Warning { get; set; }
}

public class TransferDetail
{
    public double? Progress { get; set; }
    public long? SpeedBytesPerSec { get; set; }
    public long? BytesTransferred { get; set; }
    public long? TotalBytes { get; set; }
    public int? EtaSeconds { get; set; }
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }

    /// <summary>The current/next attempt number (1-based).</summary>
    public int Attempt { get; set; }

    /// <summary>The job's transfer retry limit; null if the job no longer exists.</summary>
    public int? MaxAttempts { get; set; }
    public DateTime? NextRetryAtUtc { get; set; }
    public string? LastError { get; set; }
}
