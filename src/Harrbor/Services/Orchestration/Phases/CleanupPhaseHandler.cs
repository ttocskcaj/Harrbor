using Microsoft.EntityFrameworkCore;
using Harrbor.Configuration;
using Harrbor.Data;
using Harrbor.Data.Entities;

namespace Harrbor.Services.Orchestration.Phases;

public interface ICleanupPhaseHandler : IPhaseHandler
{
}

public class CleanupPhaseHandler : ICleanupPhaseHandler
{
    private readonly ILogger<CleanupPhaseHandler> _logger;

    public CleanupPhaseHandler(ILogger<CleanupPhaseHandler> logger)
    {
        _logger = logger;
    }

    public async Task ExecuteAsync(JobDefinition job, HarrborDbContext dbContext, CancellationToken cancellationToken)
    {
        var pendingCleanup = await dbContext.TrackedReleases
            .Where(r => r.JobName == job.Name
                && r.ImportStatus == ImportStatus.Completed
                && r.CleanupStatus == CleanupStatus.Pending)
            .ToListAsync(cancellationToken);

        if (pendingCleanup.Count == 0)
            return;

        _logger.LogDebug("Job '{JobName}': Processing {Count} pending cleanups", job.Name, pendingCleanup.Count);

        foreach (var release in pendingCleanup)
        {
            try
            {
                // Construct the staging file/folder path
                var stagingItemPath = Path.Combine(release.StagingPath, Path.GetFileName(release.RemotePath));

                if (Directory.Exists(stagingItemPath))
                {
                    Directory.Delete(stagingItemPath, recursive: true);
                    _logger.LogInformation(
                        "Job '{JobName}': Deleted staging directory for '{ReleaseName}': {Path}",
                        job.Name, release.Name, stagingItemPath);
                }
                else if (File.Exists(stagingItemPath))
                {
                    File.Delete(stagingItemPath);
                    _logger.LogInformation(
                        "Job '{JobName}': Deleted staging file for '{ReleaseName}': {Path}",
                        job.Name, release.Name, stagingItemPath);
                }
                else
                {
                    _logger.LogDebug(
                        "Job '{JobName}': Staging path not found for '{ReleaseName}': {Path} (may have been cleaned by import)",
                        job.Name, release.Name, stagingItemPath);
                }

                release.CleanupStatus = CleanupStatus.Completed;
                release.CleanupCompletedAtUtc = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                release.CleanupStatus = CleanupStatus.Failed;
                release.ErrorCount++;
                release.LastError = ex.Message;
                release.LastErrorAtUtc = DateTime.UtcNow;

                _logger.LogError(ex,
                    "Job '{JobName}': Cleanup failed for '{ReleaseName}'",
                    job.Name, release.Name);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
