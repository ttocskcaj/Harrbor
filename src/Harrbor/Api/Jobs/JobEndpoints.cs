using Harrbor.Abstractions.Validation;
using Harrbor.Data;
using Harrbor.Data.Entities;
using Harrbor.Services;
using Harrbor.Services.Configuration;
using Microsoft.EntityFrameworkCore;

namespace Harrbor.Api.Jobs;

public static class JobEndpoints
{
    public static IEndpointRouteBuilder MapJobEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/jobs");

        group.MapGet("/", GetJobsAsync);
        group.MapGet("/{id:int}", GetJobAsync);
        group.MapPost("/", CreateJobAsync);
        group.MapPost("/{id:int}/clone", CloneJobAsync);
        group.MapPut("/{id:int}", UpdateJobAsync);
        group.MapDelete("/{id:int}", DeleteJobAsync);

        return app;
    }

    private static async Task<IResult> GetJobsAsync(HarrborDbContext db, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var jobs = await db.Jobs.AsNoTracking().OrderBy(j => j.Name).ToListAsync(cancellationToken);
        var counts = await GetActiveReleaseCountsAsync(db, cancellationToken);

        var responses = jobs
            .Select(j => JobMapping.ToResponse(j, counts.GetValueOrDefault(j.Name), now))
            .ToList();

        return Results.Ok(responses);
    }

    private static async Task<IResult> GetJobAsync(int id, HarrborDbContext db, CancellationToken cancellationToken)
    {
        var job = await db.Jobs.AsNoTracking().FirstOrDefaultAsync(j => j.Id == id, cancellationToken);
        if (job is null)
            return Results.NotFound();

        var count = await CountActiveReleasesAsync(db, job.Name, cancellationToken);
        return Results.Ok(JobMapping.ToResponse(job, count, DateTime.UtcNow));
    }

    private static async Task<IResult> CreateJobAsync(
        JobRequest request,
        HarrborDbContext db,
        IConnectionStore store,
        IConnectionProviderRegistry registry,
        CancellationToken cancellationToken)
    {
        var errors = JobMapping.Validate(request);
        await JobMapping.ValidateConnectionsAsync(request, store, registry, errors, cancellationToken);

        var name = request.Name.Trim();
        if (!errors.Has("name") && await NameTakenAsync(db, name, excludeId: null, cancellationToken))
            errors.Add("name", $"A job named '{name}' already exists.");

        if (!errors.Has("stagingPath"))
            JobMapping.ProbeStagingPath(request.StagingPath, errors);

        if (errors.HasErrors)
            return Results.ValidationProblem(errors.Build());

        var now = DateTime.UtcNow;
        var job = new Job { CreatedAtUtc = now };
        JobMapping.Apply(request, job, now);

        db.Jobs.Add(job);
        await db.SaveChangesAsync(cancellationToken);

        return Results.Created($"/api/jobs/{job.Id}", JobMapping.ToResponse(job, 0, now));
    }

    private static async Task<IResult> CloneJobAsync(
        int id,
        CloneJobRequest? request,
        HarrborDbContext db,
        CancellationToken cancellationToken)
    {
        var source = await db.Jobs.AsNoTracking().FirstOrDefaultAsync(j => j.Id == id, cancellationToken);
        if (source is null)
            return Results.NotFound();

        var now = DateTime.UtcNow;
        string name;

        var requested = request?.Name?.Trim();
        if (!string.IsNullOrEmpty(requested))
        {
            var errors = new ValidationErrors();
            if (requested.Length > 100)
                errors.Add("name", "Must be at most 100 characters.");
            else if (await NameTakenAsync(db, requested, excludeId: null, cancellationToken))
                errors.Add("name", $"A job named '{requested}' already exists.");

            if (errors.HasErrors)
                return Results.ValidationProblem(errors.Build());

            name = requested;
        }
        else
        {
            name = await GenerateCloneNameAsync(db, source.Name, cancellationToken);
        }

        // Copy every setting but start disabled (so a half-tuned clone doesn't immediately
        // start processing) and with no run history or releases.
        var clone = new Job
        {
            Name = name,
            Enabled = false,
            MediaServiceConnectionId = source.MediaServiceConnectionId,
            DownloadClientConnectionId = source.DownloadClientConnectionId,
            RemoteStorageConnectionId = source.RemoteStorageConnectionId,
            PollingInterval = source.PollingInterval,
            RemotePath = source.RemotePath,
            StagingPath = source.StagingPath,
            Category = source.Category,
            Tags = [.. source.Tags],
            TransferParallelism = source.TransferParallelism,
            MaxTransferRetries = source.MaxTransferRetries,
            TransferRetryDelay = source.TransferRetryDelay,
            CompletedCategory = source.CompletedCategory,
            ImportTimeout = source.ImportTimeout,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            LastRunAtUtc = null
        };

        db.Jobs.Add(clone);
        await db.SaveChangesAsync(cancellationToken);

        return Results.Created($"/api/jobs/{clone.Id}", JobMapping.ToResponse(clone, 0, now));
    }

    private static async Task<IResult> UpdateJobAsync(
        int id,
        JobRequest request,
        HarrborDbContext db,
        IConnectionStore store,
        IConnectionProviderRegistry registry,
        CancellationToken cancellationToken)
    {
        var job = await db.Jobs.FirstOrDefaultAsync(j => j.Id == id, cancellationToken);
        if (job is null)
            return Results.NotFound();

        var errors = JobMapping.Validate(request);
        await JobMapping.ValidateConnectionsAsync(request, store, registry, errors, cancellationToken);

        var name = request.Name.Trim();
        if (!errors.Has("name") && await NameTakenAsync(db, name, excludeId: id, cancellationToken))
            errors.Add("name", $"A job named '{name}' already exists.");

        if (!errors.Has("stagingPath"))
            JobMapping.ProbeStagingPath(request.StagingPath, errors);

        if (errors.HasErrors)
            return Results.ValidationProblem(errors.Build());

        var now = DateTime.UtcNow;
        var oldName = job.Name;
        JobMapping.Apply(request, job, now);

        if (!string.Equals(oldName, job.Name, StringComparison.Ordinal))
        {
            // Rename: tracked releases reference jobs by name, so history follows the job
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            await db.TrackedReleases
                .Where(r => r.JobName == oldName)
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.JobName, job.Name), cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        else
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        var count = await CountActiveReleasesAsync(db, job.Name, cancellationToken);
        return Results.Ok(JobMapping.ToResponse(job, count, now));
    }

    private static async Task<IResult> DeleteJobAsync(
        int id,
        bool? force,
        HarrborDbContext db,
        CancellationToken cancellationToken)
    {
        var job = await db.Jobs.FirstOrDefaultAsync(j => j.Id == id, cancellationToken);
        if (job is null)
            return Results.NotFound();

        var activeCount = await CountActiveReleasesAsync(db, job.Name, cancellationToken);
        if (activeCount > 0 && force != true)
        {
            return Results.Conflict(new JobDeleteConflictResponse
            {
                ActiveReleaseCount = activeCount,
                Message = $"Job '{job.Name}' has {activeCount} release(s) still in the pipeline. " +
                          "Delete with ?force=true to remove the job; its releases are kept as history but stop being processed."
            });
        }

        db.Jobs.Remove(job);
        await db.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    private static async Task<string> GenerateCloneNameAsync(HarrborDbContext db, string baseName, CancellationToken cancellationToken)
    {
        // "{base}-copy", then "-copy-2", "-copy-3", ... truncating the base to stay within 100 chars.
        for (var i = 1; i <= 1000; i++)
        {
            var suffix = i == 1 ? "-copy" : $"-copy-{i}";
            var candidate = Truncate(baseName, 100 - suffix.Length) + suffix;
            if (!await NameTakenAsync(db, candidate, excludeId: null, cancellationToken))
                return candidate;
        }

        return Truncate(baseName, 60) + "-copy-" + Guid.NewGuid().ToString("N")[..8];
    }

    private static string Truncate(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength];

    private static Task<bool> NameTakenAsync(HarrborDbContext db, string name, int? excludeId, CancellationToken cancellationToken)
    {
        var lowered = name.ToLower();
        return db.Jobs.AnyAsync(
            j => (excludeId == null || j.Id != excludeId) && j.Name.ToLower() == lowered,
            cancellationToken);
    }

    private static Task<int> CountActiveReleasesAsync(HarrborDbContext db, string jobName, CancellationToken cancellationToken)
        => db.TrackedReleases.CountAsync(
            r => r.JobName == jobName && r.Outcome == PipelineOutcome.Active,
            cancellationToken);

    private static Task<Dictionary<string, int>> GetActiveReleaseCountsAsync(HarrborDbContext db, CancellationToken cancellationToken)
        => db.TrackedReleases
            .Where(r => r.Outcome == PipelineOutcome.Active)
            .GroupBy(r => r.JobName)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);
}
