using Harrbor.Data;
using Harrbor.Data.Entities;
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
        CancellationToken cancellationToken)
    {
        var errors = JobMapping.Validate(request);

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

    private static async Task<IResult> UpdateJobAsync(
        int id,
        JobRequest request,
        HarrborDbContext db,
        CancellationToken cancellationToken)
    {
        var job = await db.Jobs.FirstOrDefaultAsync(j => j.Id == id, cancellationToken);
        if (job is null)
            return Results.NotFound();

        var errors = JobMapping.Validate(request);

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

    private static Task<bool> NameTakenAsync(HarrborDbContext db, string name, int? excludeId, CancellationToken cancellationToken)
    {
        var lowered = name.ToLower();
        return db.Jobs.AnyAsync(
            j => (excludeId == null || j.Id != excludeId) && j.Name.ToLower() == lowered,
            cancellationToken);
    }

    private static Task<int> CountActiveReleasesAsync(HarrborDbContext db, string jobName, CancellationToken cancellationToken)
        => db.TrackedReleases.CountAsync(
            r => r.JobName == jobName && r.ArchivalStatus != ArchivalStatus.Completed,
            cancellationToken);

    private static Task<Dictionary<string, int>> GetActiveReleaseCountsAsync(HarrborDbContext db, CancellationToken cancellationToken)
        => db.TrackedReleases
            .Where(r => r.ArchivalStatus != ArchivalStatus.Completed)
            .GroupBy(r => r.JobName)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);
}
