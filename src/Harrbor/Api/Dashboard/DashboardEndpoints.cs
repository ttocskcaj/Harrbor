using Harrbor.Api.Connections;
using Harrbor.Api.Jobs;
using Harrbor.Api.Releases;
using Harrbor.Data;
using Harrbor.Data.Entities;
using Harrbor.Services;
using Harrbor.Services.Configuration;
using Harrbor.Services.Health;
using Harrbor.Services.RemoteStorage;
using Microsoft.EntityFrameworkCore;

namespace Harrbor.Api.Dashboard;

public static class DashboardEndpoints
{
    private const int MaxAttentionItems = 20;
    private const int RecentCompletionsCount = 10;

    public static IEndpointRouteBuilder MapDashboardEndpoints(this IEndpointRouteBuilder app)
    {
        // .Produces<T>() gives the generated OpenAPI doc a typed 200 response body,
        // so the React client codegens fully-typed data (handlers return IResult,
        // which otherwise erases the response type). See the other endpoint groups
        // for the same treatment as they gain UI consumers.
        app.MapGet("/api/dashboard", GetDashboardAsync)
            .Produces<DashboardResponse>();
        return app;
    }

    private static async Task<IResult> GetDashboardAsync(
        HarrborDbContext db,
        IConnectionStore store,
        IConnectionProviderRegistry registry,
        IConnectionHealthMonitor healthMonitor,
        ITransferProgressTracker progressTracker,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        // The active set is bounded (in-flight releases) and is reused for the pipeline
        // counts, throughput's in-progress count, per-job counts, and the attention scan.
        var active = await db.TrackedReleases.AsNoTracking()
            .Where(r => r.Outcome == PipelineOutcome.Active)
            .ToListAsync(cancellationToken);

        var jobs = await db.Jobs.AsNoTracking().OrderBy(j => j.Name).ToListAsync(cancellationToken);
        var jobsByName = jobs.ToDictionary(j => j.Name);

        var response = new DashboardResponse
        {
            Pipeline = BuildPipeline(active),
            Throughput = await BuildThroughputAsync(db, active, progressTracker, now, cancellationToken),
            Jobs = BuildJobSnapshots(jobs, active, now),
            Attention = await BuildAttentionAsync(db, active, jobsByName, now, cancellationToken),
            Connections = await ConnectionHealthReporting.BuildAsync(store, registry, healthMonitor, cancellationToken)
        };

        return Results.Ok(response);
    }

    private static PipelineCounts BuildPipeline(List<TrackedRelease> active)
    {
        var counts = new PipelineCounts();
        foreach (var release in active)
        {
            switch (ReleaseMapping.DeriveCurrentPhase(release))
            {
                case ReleasePhase.Discover: counts.Discover++; break;
                case ReleasePhase.Download: counts.Download++; break;
                case ReleasePhase.Transfer: counts.Transfer++; break;
                case ReleasePhase.Extraction: counts.Extraction++; break;
                case ReleasePhase.Import: counts.Import++; break;
                case ReleasePhase.Cleanup: counts.Cleanup++; break;
                case ReleasePhase.Archival: counts.Archival++; break;
                // Done cannot occur for an active release (its outcome would be Completed).
            }
        }

        return counts;
    }

    private static async Task<ThroughputSummary> BuildThroughputAsync(
        HarrborDbContext db,
        List<TrackedRelease> active,
        ITransferProgressTracker progressTracker,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var since24h = now.AddHours(-24);
        var since7d = now.AddDays(-7);

        var completed24h = await db.TrackedReleases
            .CountAsync(r => r.Outcome == PipelineOutcome.Completed && r.ArchivedAtUtc >= since24h, cancellationToken);
        var completed7d = await db.TrackedReleases
            .CountAsync(r => r.Outcome == PipelineOutcome.Completed && r.ArchivedAtUtc >= since7d, cancellationToken);

        var recent = await db.TrackedReleases.AsNoTracking()
            .Where(r => r.Outcome == PipelineOutcome.Completed && r.ArchivedAtUtc != null)
            .OrderByDescending(r => r.ArchivedAtUtc)
            .Take(RecentCompletionsCount)
            .Select(r => new RecentCompletion
            {
                Id = r.Id,
                Name = r.Name,
                JobName = r.JobName,
                ArchivedAtUtc = r.ArchivedAtUtc
            })
            .ToListAsync(cancellationToken);

        // Sum live speed across the transfers currently in progress.
        long aggregateSpeed = 0;
        foreach (var release in active.Where(r => r.TransferStatus == TransferStatus.InProgress))
        {
            if (progressTracker.Get(release.Id) is { } snapshot)
                aggregateSpeed += snapshot.SpeedBytesPerSec;
        }

        return new ThroughputSummary
        {
            CompletedLast24h = completed24h,
            CompletedLast7d = completed7d,
            TransfersInProgress = active.Count(r => r.TransferStatus == TransferStatus.InProgress),
            AggregateSpeedBytesPerSec = aggregateSpeed,
            RecentCompletions = recent
        };
    }

    private static List<JobSnapshot> BuildJobSnapshots(List<Job> jobs, List<TrackedRelease> active, DateTime now)
    {
        var activeByJob = active
            .GroupBy(r => r.JobName)
            .ToDictionary(g => g.Key, g => g.Count());

        return jobs.Select(job => new JobSnapshot
        {
            Id = job.Id,
            Name = job.Name,
            Enabled = job.Enabled,
            MediaServiceConnectionId = job.MediaServiceConnectionId,
            ActiveReleaseCount = activeByJob.GetValueOrDefault(job.Name),
            LastRunAtUtc = job.LastRunAtUtc,
            NextRunAtUtc = JobMapping.ComputeNextRunAtUtc(job, now)
        }).ToList();
    }

    private static async Task<AttentionSummary> BuildAttentionAsync(
        HarrborDbContext db,
        List<TrackedRelease> active,
        Dictionary<string, Job> jobsByName,
        DateTime now,
        CancellationToken cancellationToken)
    {
        // Active but problematic: retrying or stuck (derived, not stored).
        var problematic = active
            .Select(r => (release: r, status: ReleaseMapping.DeriveStatus(r, jobsByName.GetValueOrDefault(r.JobName), now)))
            .Where(x => x.status is ReleaseStatus.Retrying or ReleaseStatus.Stuck)
            .ToList();

        // Permanently failed releases stay in attention until the operator retries or cancels.
        var failedCount = await db.TrackedReleases
            .CountAsync(r => r.Outcome == PipelineOutcome.Failed, cancellationToken);
        var failedTop = await db.TrackedReleases.AsNoTracking()
            .Where(r => r.Outcome == PipelineOutcome.Failed)
            .OrderByDescending(r => r.LastErrorAtUtc)
            .Take(MaxAttentionItems)
            .ToListAsync(cancellationToken);

        var items = new List<AttentionItem>();
        foreach (var (release, status) in problematic)
            items.Add(ToAttentionItem(release, status, jobsByName.GetValueOrDefault(release.JobName), now));
        foreach (var release in failedTop)
            items.Add(ToAttentionItem(release, ReleaseStatus.Failed, jobsByName.GetValueOrDefault(release.JobName), now));

        var ordered = items
            .OrderByDescending(i => i.LastErrorAtUtc ?? DateTime.MinValue)
            .Take(MaxAttentionItems)
            .ToList();

        return new AttentionSummary
        {
            Items = ordered,
            TotalCount = problematic.Count + failedCount
        };
    }

    private static AttentionItem ToAttentionItem(TrackedRelease release, ReleaseStatus status, Job? job, DateTime now)
    {
        var nextRetry = status == ReleaseStatus.Retrying && job is not null && release.LastErrorAtUtc is { } lastError
            ? lastError + job.TransferRetryDelay
            : (DateTime?)null;

        return new AttentionItem
        {
            Id = release.Id,
            Name = release.Name,
            JobName = release.JobName,
            CurrentPhase = ReleaseMapping.DeriveCurrentPhase(release),
            Status = status,
            LastError = release.LastError,
            LastErrorAtUtc = release.LastErrorAtUtc,
            NextRetryAtUtc = nextRetry
        };
    }
}
