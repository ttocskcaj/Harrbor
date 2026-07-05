using Harrbor.Data.Entities;
using Harrbor.Services.RemoteStorage;

namespace Harrbor.Api.Releases;

/// <summary>
/// Pure projection of a <see cref="TrackedRelease"/> into the API's view model. The
/// release's "current phase" and live status are derived here from the five per-phase
/// status enums, the outcome, and timestamps — nothing is denormalized on the entity.
/// </summary>
public static class ReleaseMapping
{
    /// <summary>A transfer counts as stuck once it has been in progress this long without completing.</summary>
    public static readonly TimeSpan TransferStuckAfter = TimeSpan.FromMinutes(30);

    /// <summary>An import counts as stuck once it has waited this fraction of the job's import timeout.</summary>
    public const double ImportStuckFraction = 0.5;

    private static readonly ReleasePhase[] TimelinePhases =
    [
        ReleasePhase.Discover,
        ReleasePhase.Download,
        ReleasePhase.Transfer,
        ReleasePhase.Import,
        ReleasePhase.Cleanup,
        ReleasePhase.Archival
    ];

    public static ReleasePhase DeriveCurrentPhase(TrackedRelease r)
    {
        if (r.ArchivalStatus == ArchivalStatus.Completed) return ReleasePhase.Done;
        if (r.DownloadStatus != DownloadStatus.Completed) return ReleasePhase.Download;
        if (r.TransferStatus != TransferStatus.Completed) return ReleasePhase.Transfer;
        if (r.ImportStatus != ImportStatus.Completed) return ReleasePhase.Import;
        if (r.CleanupStatus != CleanupStatus.Completed) return ReleasePhase.Cleanup;
        return ReleasePhase.Archival;
    }

    public static ReleaseStatus DeriveStatus(TrackedRelease r, Job? job, DateTime nowUtc)
    {
        switch (r.Outcome)
        {
            case PipelineOutcome.Completed: return ReleaseStatus.Done;
            case PipelineOutcome.Cancelled: return ReleaseStatus.Cancelled;
            case PipelineOutcome.Failed: return ReleaseStatus.Failed;
        }

        // Outcome is Active from here.
        if (r.TransferStatus == TransferStatus.Failed)
        {
            // A retryable failure stays "retrying" only while under the job's limit; an
            // exhausted or orphaned (job-less) failure reads as failed even if not yet marked.
            return job is not null && r.ErrorCount < job.MaxTransferRetries
                ? ReleaseStatus.Retrying
                : ReleaseStatus.Failed;
        }

        return IsStuck(r, job, nowUtc) ? ReleaseStatus.Stuck : ReleaseStatus.Flowing;
    }

    private static bool IsStuck(TrackedRelease r, Job? job, DateTime nowUtc)
    {
        // A transfer that has been running far longer than expected.
        if (r.TransferStatus == TransferStatus.InProgress
            && r.TransferStartedAtUtc is { } startedAt
            && nowUtc - startedAt > TransferStuckAfter)
        {
            return true;
        }

        // An import waiting well past half of its allowed window.
        if (r.TransferStatus == TransferStatus.Completed
            && r.ImportStatus == ImportStatus.Pending
            && job is not null
            && r.TransferCompletedAtUtc is { } transferDone
            && nowUtc - transferDone > job.ImportTimeout * ImportStuckFraction)
        {
            return true;
        }

        return false;
    }

    public static DateTime? PhaseStartedAt(TrackedRelease r, ReleasePhase phase) => phase switch
    {
        ReleasePhase.Discover => r.CreatedAtUtc,
        ReleasePhase.Download => r.CreatedAtUtc,
        ReleasePhase.Transfer => r.TransferStartedAtUtc ?? r.DownloadCompletedAtUtc,
        ReleasePhase.Import => r.TransferCompletedAtUtc,
        ReleasePhase.Cleanup => r.ImportCompletedAtUtc,
        ReleasePhase.Archival => r.CleanupCompletedAtUtc,
        ReleasePhase.Done => r.ArchivedAtUtc,
        _ => null
    };

    private static DateTime? PhaseCompletedAt(TrackedRelease r, ReleasePhase phase) => phase switch
    {
        ReleasePhase.Discover => r.CreatedAtUtc,
        ReleasePhase.Download => r.DownloadCompletedAtUtc,
        ReleasePhase.Transfer => r.TransferCompletedAtUtc,
        ReleasePhase.Import => r.ImportCompletedAtUtc,
        ReleasePhase.Cleanup => r.CleanupCompletedAtUtc,
        ReleasePhase.Archival => r.ArchivedAtUtc,
        _ => null
    };

    private static bool IsPhaseCompleted(TrackedRelease r, ReleasePhase phase) => phase switch
    {
        ReleasePhase.Discover => true,
        ReleasePhase.Download => r.DownloadStatus == DownloadStatus.Completed,
        ReleasePhase.Transfer => r.TransferStatus == TransferStatus.Completed,
        ReleasePhase.Import => r.ImportStatus == ImportStatus.Completed,
        ReleasePhase.Cleanup => r.CleanupStatus == CleanupStatus.Completed,
        ReleasePhase.Archival => r.ArchivalStatus == ArchivalStatus.Completed,
        _ => false
    };

    private static bool IsPhaseFailed(TrackedRelease r, ReleasePhase phase) => phase switch
    {
        ReleasePhase.Transfer => r.TransferStatus == TransferStatus.Failed,
        ReleasePhase.Import => r.ImportStatus == ImportStatus.Failed,
        ReleasePhase.Cleanup => r.CleanupStatus == CleanupStatus.Failed,
        ReleasePhase.Archival => r.ArchivalStatus == ArchivalStatus.Failed,
        _ => false
    };

    public static List<PhaseTimelineEntry> BuildTimeline(TrackedRelease r, ReleasePhase currentPhase)
    {
        var entries = new List<PhaseTimelineEntry>(TimelinePhases.Length);
        foreach (var phase in TimelinePhases)
        {
            PhaseTimelineState state;
            if (IsPhaseCompleted(r, phase)) state = PhaseTimelineState.Done;
            else if (IsPhaseFailed(r, phase)) state = PhaseTimelineState.Failed;
            else if (phase == currentPhase) state = PhaseTimelineState.Current;
            else state = PhaseTimelineState.Pending;

            entries.Add(new PhaseTimelineEntry
            {
                Phase = phase,
                State = state,
                StartedAtUtc = PhaseStartedAt(r, phase),
                CompletedAtUtc = PhaseCompletedAt(r, phase)
            });
        }

        return entries;
    }

    public static ReleaseListItem ToListItem(TrackedRelease r, Job? job, DateTime nowUtc)
    {
        var item = new ReleaseListItem();
        PopulateBase(item, r, job, nowUtc);
        return item;
    }

    /// <summary>
    /// Overlays live transfer metrics from the in-memory progress tracker onto an already-mapped
    /// item (only meaningful while a transfer is in progress). Kept separate so the core mapping
    /// stays a pure projection of the entity.
    /// </summary>
    public static void ApplyTransferProgress(ReleaseListItem item, TransferProgressSnapshot snapshot)
    {
        item.TransferProgress = snapshot.Progress;
        item.TransferSpeedBytesPerSec = snapshot.SpeedBytesPerSec;
        item.TransferBytesTransferred = snapshot.BytesTransferred;
        item.TransferTotalBytes = snapshot.TotalBytes;
        item.TransferEtaSeconds = snapshot.EtaSeconds;

        if (item is ReleaseDetail { Transfer: { } transfer })
        {
            transfer.Progress = snapshot.Progress;
            transfer.SpeedBytesPerSec = snapshot.SpeedBytesPerSec;
            transfer.BytesTransferred = snapshot.BytesTransferred;
            transfer.TotalBytes = snapshot.TotalBytes;
            transfer.EtaSeconds = snapshot.EtaSeconds;
        }
    }

    public static ReleaseDetail ToDetail(TrackedRelease r, Job? job, DateTime nowUtc)
    {
        var detail = new ReleaseDetail
        {
            RemotePath = r.RemotePath,
            StagingPath = r.StagingPath
        };
        var currentPhase = PopulateBase(detail, r, job, nowUtc);
        detail.Timeline = BuildTimeline(r, currentPhase);
        detail.Transfer = BuildTransferDetail(r, job);
        return detail;
    }

    private static ReleasePhase PopulateBase(ReleaseListItem item, TrackedRelease r, Job? job, DateTime nowUtc)
    {
        var currentPhase = DeriveCurrentPhase(r);
        var status = DeriveStatus(r, job, nowUtc);
        var phaseStart = PhaseStartedAt(r, currentPhase);

        item.Id = r.Id;
        item.DownloadId = r.DownloadId;
        item.Name = r.Name;
        item.JobName = r.JobName;
        item.CurrentPhase = currentPhase;
        item.Status = status;
        item.PhaseEnteredAtUtc = phaseStart;
        item.TimeInPhaseSeconds = phaseStart is { } start
            ? (long)Math.Max(0, (nowUtc - start).TotalSeconds)
            : 0;
        item.ErrorCount = r.ErrorCount;
        item.LastError = r.LastError;
        item.LastErrorAtUtc = r.LastErrorAtUtc;
        item.CreatedAtUtc = r.CreatedAtUtc;
        item.MaxRetries = job?.MaxTransferRetries;
        item.NextRetryAtUtc = status == ReleaseStatus.Retrying && job is not null && r.LastErrorAtUtc is { } lastError
            ? lastError + job.TransferRetryDelay
            : null;

        return currentPhase;
    }

    private static TransferDetail BuildTransferDetail(TrackedRelease r, Job? job)
    {
        var isRetrying = r.TransferStatus == TransferStatus.Failed
            && r.Outcome == PipelineOutcome.Active
            && job is not null
            && r.ErrorCount < job.MaxTransferRetries;

        return new TransferDetail
        {
            StartedAtUtc = r.TransferStartedAtUtc,
            CompletedAtUtc = r.TransferCompletedAtUtc,
            Attempt = r.ErrorCount + 1,
            MaxAttempts = job?.MaxTransferRetries,
            NextRetryAtUtc = isRetrying && r.LastErrorAtUtc is { } lastError && job is not null
                ? lastError + job.TransferRetryDelay
                : null,
            LastError = r.LastError
        };
    }
}
