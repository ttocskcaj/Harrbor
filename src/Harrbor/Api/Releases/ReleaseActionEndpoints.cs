using Harrbor.Data;
using Harrbor.Data.Entities;
using Harrbor.Services;
using Microsoft.EntityFrameworkCore;

namespace Harrbor.Api.Releases;

/// <summary>
/// Operator actions on a single release. Each resets or clears the relevant state so the
/// next scheduler tick reprocesses it; the pipeline's <c>Outcome == Active</c> gate plus the
/// in-progress guard keep these safe against the running cycle without row-level locking.
/// </summary>
public static class ReleaseActions
{
    public static async Task<IResult> RetryAsync(
        int id,
        RetryReleaseRequest? request,
        HarrborDbContext db,
        CancellationToken cancellationToken)
    {
        var release = await db.TrackedReleases.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (release is null)
            return Results.NotFound();

        var phase = request?.Phase ?? DeriveFailedPhase(release);
        if (phase is null)
            return Results.Problem(
                detail: "Release has no failed phase to retry; specify a phase to force a re-run.",
                statusCode: StatusCodes.Status400BadRequest);

        switch (phase)
        {
            case ReleasePhase.Transfer:
                if (release.TransferStatus == TransferStatus.InProgress)
                    return Results.Problem(
                        detail: "A transfer is in progress for this release; wait for it to finish before retrying.",
                        statusCode: StatusCodes.Status409Conflict);
                release.TransferStatus = TransferStatus.Pending;
                release.TransferStartedAtUtc = null;
                release.TransferCompletedAtUtc = null;
                release.ErrorCount = 0;
                break;

            case ReleasePhase.Import:
                release.ImportStatus = ImportStatus.Pending;
                release.ImportCompletedAtUtc = null;
                break;

            case ReleasePhase.Cleanup:
                release.CleanupStatus = CleanupStatus.Pending;
                release.CleanupCompletedAtUtc = null;
                break;

            case ReleasePhase.Archival:
                release.ArchivalStatus = ArchivalStatus.Pending;
                release.ArchivedAtUtc = null;
                break;

            default:
                return Results.Problem(
                    detail: $"Phase '{phase}' cannot be retried.",
                    statusCode: StatusCodes.Status400BadRequest);
        }

        release.LastError = null;
        release.LastErrorAtUtc = null;
        release.Outcome = PipelineOutcome.Active;

        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(await BuildResponseAsync(db, release, cancellationToken));
    }

    public static async Task<IResult> CancelAsync(
        int id,
        CancelReleaseRequest? request,
        HarrborDbContext db,
        IConnectionManager connectionManager,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var deleteTorrent = request?.DeleteTorrent ?? true;
        var deleteFiles = request?.DeleteFiles ?? false;

        var release = await db.TrackedReleases.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (release is null)
            return Results.NotFound();

        string? warning = null;
        if (deleteTorrent)
        {
            // Resolve the torrent client the owning job used, by its download-client connection id.
            var owningJob = await db.Jobs.AsNoTracking()
                .FirstOrDefaultAsync(j => j.Name == release.JobName, cancellationToken);

            if (owningJob is null)
            {
                warning = "Release cancelled, but the torrent was left in place because the owning job no longer exists.";
            }
            else
            {
                try
                {
                    var torrentClient = await connectionManager.GetTorrentClientAsync(owningJob.DownloadClientConnectionId, cancellationToken);
                    await torrentClient.DeleteTorrentAsync(release.DownloadId, deleteFiles, cancellationToken);
                }
                catch (Exception ex)
                {
                    // The cancellation still stands; just report that download-client cleanup didn't happen.
                    warning = $"Release cancelled, but removing the torrent from the download client failed: {ex.Message}";
                    loggerFactory.CreateLogger("Harrbor.Api.Releases.ReleaseActions")
                        .LogWarning(ex, "Cancel: failed to delete torrent {DownloadId}", release.DownloadId);
                }
            }
        }

        // Handlers only process Active releases, so once finished an in-flight phase goes inert.
        release.Outcome = PipelineOutcome.Cancelled;
        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(await BuildResponseAsync(db, release, cancellationToken, warning));
    }

    public static async Task<IResult> ReimportAsync(
        int id,
        HarrborDbContext db,
        IConnectionManager connectionManager,
        CancellationToken cancellationToken)
    {
        var release = await db.TrackedReleases.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (release is null)
            return Results.NotFound();

        if (release.TransferStatus != TransferStatus.Completed)
            return Results.Problem(
                detail: "Import can only be re-run after the transfer has completed.",
                statusCode: StatusCodes.Status409Conflict);

        var job = await db.Jobs.FirstOrDefaultAsync(j => j.Name == release.JobName, cancellationToken);
        if (job is null)
            return Results.Problem(
                detail: "The job that owns this release no longer exists.",
                statusCode: StatusCodes.Status409Conflict);

        var mediaService = await connectionManager.GetMediaServiceAsync(job.MediaServiceConnectionId, cancellationToken);
        var importPath = Path.Combine(release.StagingPath, Path.GetFileName(release.RemotePath));
        await mediaService.TriggerManualImportAsync(importPath, cancellationToken);

        release.ImportStatus = ImportStatus.Pending;
        release.ImportCompletedAtUtc = null;
        release.LastError = null;
        release.LastErrorAtUtc = null;
        release.Outcome = PipelineOutcome.Active;

        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(await BuildResponseAsync(db, release, cancellationToken));
    }

    private static ReleasePhase? DeriveFailedPhase(TrackedRelease release)
    {
        if (release.TransferStatus == TransferStatus.Failed) return ReleasePhase.Transfer;
        if (release.ImportStatus == ImportStatus.Failed) return ReleasePhase.Import;
        if (release.CleanupStatus == CleanupStatus.Failed) return ReleasePhase.Cleanup;
        if (release.ArchivalStatus == ArchivalStatus.Failed) return ReleasePhase.Archival;
        return null;
    }

    private static async Task<ReleaseActionResponse> BuildResponseAsync(
        HarrborDbContext db, TrackedRelease release, CancellationToken cancellationToken, string? warning = null)
    {
        var job = await db.Jobs.AsNoTracking()
            .FirstOrDefaultAsync(j => j.Name == release.JobName, cancellationToken);
        return new ReleaseActionResponse
        {
            Release = ReleaseMapping.ToDetail(release, job, DateTime.UtcNow),
            Warning = warning
        };
    }
}
