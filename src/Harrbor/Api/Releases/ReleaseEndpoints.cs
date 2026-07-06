using Harrbor.Data;
using Harrbor.Data.Entities;
using Harrbor.Services.RemoteStorage;
using Microsoft.EntityFrameworkCore;

namespace Harrbor.Api.Releases;

public static class ReleaseEndpoints
{
    private const int DefaultPageSize = 50;
    private const int MaxPageSize = 200;

    public static IEndpointRouteBuilder MapReleaseEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/releases");

        // .Produces<T>() gives the generated OpenAPI doc a typed 200 body so the
        // React client codegens fully-typed data (handlers return IResult, which
        // otherwise erases the response type). Same treatment as MapDashboardEndpoints.
        group.MapGet("/", GetReleasesAsync)
            .Produces<PagedResponse<ReleaseListItem>>();
        group.MapGet("/{id:int}", GetReleaseAsync)
            .Produces<ReleaseDetail>();

        group.MapPost("/{id:int}/retry", ReleaseActions.RetryAsync);
        group.MapPost("/{id:int}/cancel", ReleaseActions.CancelAsync);
        group.MapPost("/{id:int}/reimport", ReleaseActions.ReimportAsync);

        return app;
    }

    private static async Task<IResult> GetReleasesAsync(
        HarrborDbContext db,
        ITransferProgressTracker progressTracker,
        CancellationToken cancellationToken,
        string? state = null,
        string? job = null,
        string? phase = null,
        string? status = null,
        string? search = null,
        int page = 1,
        int pageSize = DefaultPageSize)
    {
        var nowUtc = DateTime.UtcNow;
        if (page < 1) page = 1;
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var query = db.TrackedReleases.AsNoTracking();

        if (string.Equals(state, "history", StringComparison.OrdinalIgnoreCase))
            query = query.Where(r => r.Outcome != PipelineOutcome.Active);
        else if (string.Equals(state, "active", StringComparison.OrdinalIgnoreCase))
            query = query.Where(r => r.Outcome == PipelineOutcome.Active);

        if (!string.IsNullOrWhiteSpace(job))
            query = query.Where(r => r.JobName == job);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(r => r.Name.Contains(term));
        }

        if (Enum.TryParse<ReleasePhase>(phase, ignoreCase: true, out var phaseFilter))
            query = ApplyPhaseFilter(query, phaseFilter);

        var hasStatus = Enum.TryParse<ReleaseStatus>(status, ignoreCase: true, out var statusFilter);
        // Flowing/stuck depend on time-in-phase and can't be expressed in SQL.
        var inMemoryStatus = hasStatus && statusFilter is ReleaseStatus.Flowing or ReleaseStatus.Stuck;
        if (hasStatus && !inMemoryStatus)
            query = ApplyStatusFilter(query, statusFilter);

        query = query.OrderByDescending(r => r.CreatedAtUtc).ThenByDescending(r => r.Id);

        // Jobs are needed to derive retry limits, next-retry times, and import-stuck thresholds.
        var jobs = await db.Jobs.AsNoTracking().ToDictionaryAsync(j => j.Name, cancellationToken);

        ReleaseListItem Map(TrackedRelease release)
        {
            var item = ReleaseMapping.ToListItem(release, jobs.GetValueOrDefault(release.JobName), nowUtc);
            if (release.TransferStatus == TransferStatus.InProgress && progressTracker.Get(release.Id) is { } snapshot)
                ReleaseMapping.ApplyTransferProgress(item, snapshot);
            return item;
        }

        List<ReleaseListItem> items;
        int total;

        if (!inMemoryStatus)
        {
            total = await query.CountAsync(cancellationToken);
            var rows = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);
            items = rows.Select(Map).ToList();
        }
        else
        {
            // Narrow to the candidate set (active, not a failed transfer), derive in memory,
            // filter by the derived status, then paginate. Bounded by the active release count.
            var candidates = await query
                .Where(r => r.Outcome == PipelineOutcome.Active && r.TransferStatus != TransferStatus.Failed)
                .ToListAsync(cancellationToken);
            var derived = candidates
                .Select(Map)
                .Where(i => i.Status == statusFilter)
                .ToList();
            total = derived.Count;
            items = derived.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        }

        return Results.Ok(new PagedResponse<ReleaseListItem>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            Total = total
        });
    }

    private static async Task<IResult> GetReleaseAsync(
        int id,
        HarrborDbContext db,
        ITransferProgressTracker progressTracker,
        CancellationToken cancellationToken)
    {
        var release = await db.TrackedReleases.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (release is null)
            return Results.NotFound();

        var job = await db.Jobs.AsNoTracking()
            .FirstOrDefaultAsync(j => j.Name == release.JobName, cancellationToken);

        var detail = ReleaseMapping.ToDetail(release, job, DateTime.UtcNow);
        if (release.TransferStatus == TransferStatus.InProgress && progressTracker.Get(release.Id) is { } snapshot)
            ReleaseMapping.ApplyTransferProgress(detail, snapshot);

        return Results.Ok(detail);
    }

    private static IQueryable<TrackedRelease> ApplyPhaseFilter(IQueryable<TrackedRelease> query, ReleasePhase phase) => phase switch
    {
        // Discovery is transient — a release is tracked already in the download phase.
        ReleasePhase.Discover => query.Where(r => false),
        ReleasePhase.Download => query.Where(r => r.DownloadStatus != DownloadStatus.Completed),
        ReleasePhase.Transfer => query.Where(r =>
            r.DownloadStatus == DownloadStatus.Completed && r.TransferStatus != TransferStatus.Completed),
        ReleasePhase.Extraction => query.Where(r =>
            r.TransferStatus == TransferStatus.Completed && r.ExtractionStatus != ExtractionStatus.Completed),
        ReleasePhase.Import => query.Where(r =>
            r.ExtractionStatus == ExtractionStatus.Completed && r.ImportStatus != ImportStatus.Completed),
        ReleasePhase.Cleanup => query.Where(r =>
            r.ImportStatus == ImportStatus.Completed && r.CleanupStatus != CleanupStatus.Completed),
        ReleasePhase.Archival => query.Where(r =>
            r.CleanupStatus == CleanupStatus.Completed && r.ArchivalStatus != ArchivalStatus.Completed),
        ReleasePhase.Done => query.Where(r => r.ArchivalStatus == ArchivalStatus.Completed),
        _ => query
    };

    private static IQueryable<TrackedRelease> ApplyStatusFilter(IQueryable<TrackedRelease> query, ReleaseStatus status) => status switch
    {
        ReleaseStatus.Done => query.Where(r => r.Outcome == PipelineOutcome.Completed),
        ReleaseStatus.Cancelled => query.Where(r => r.Outcome == PipelineOutcome.Cancelled),
        ReleaseStatus.Failed => query.Where(r => r.Outcome == PipelineOutcome.Failed),
        ReleaseStatus.Retrying => query.Where(r =>
            r.Outcome == PipelineOutcome.Active && r.TransferStatus == TransferStatus.Failed),
        _ => query
    };
}
