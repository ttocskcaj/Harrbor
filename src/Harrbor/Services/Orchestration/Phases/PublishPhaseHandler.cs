using Microsoft.EntityFrameworkCore;
using Harrbor.Configuration;
using Harrbor.Data;
using Harrbor.Data.Entities;

namespace Harrbor.Services.Orchestration.Phases;

public interface IPublishPhaseHandler : IPhaseHandler
{
}

/// <summary>
/// Moves finished releases from the hidden holding directory (<c>{StagingPath}/.harrbor/{name}</c>)
/// to the path Sonarr/Radarr watches (<c>{StagingPath}/{name}</c>) with a single same-filesystem rename,
/// so the *arr only ever sees complete, final files.
/// </summary>
public class PublishPhaseHandler : IPublishPhaseHandler
{
    private readonly ILogger<PublishPhaseHandler> _logger;

    public PublishPhaseHandler(ILogger<PublishPhaseHandler> logger)
    {
        _logger = logger;
    }

    public async Task ExecuteAsync(JobDefinition job, HarrborDbContext dbContext, CancellationToken cancellationToken)
    {
        var ready = await dbContext.TrackedReleases
            .Where(r => r.JobName == job.Name
                && r.PublishedAtUtc == null
                && r.TransferStatus == TransferStatus.Completed
                && r.ExtractionStatus == ExtractionStatus.Completed
                && (r.ConversionStatus == ConversionStatus.Completed
                    || r.ConversionStatus == ConversionStatus.Failed
                    || r.ConversionStatus == ConversionStatus.Skipped))
            .OrderBy(r => r.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        if (ready.Count == 0)
            return;

        foreach (var release in ready)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Publish(job, release);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private void Publish(JobDefinition job, TrackedRelease release)
    {
        var source = release.GetHoldingItemPath();
        var destination = release.GetFinalItemPath();

        var sourceIsDir = Directory.Exists(source);
        var sourceExists = sourceIsDir || File.Exists(source);
        var destinationExists = Directory.Exists(destination) || File.Exists(destination);

        if (!sourceExists && destinationExists)
        {
            // Moved on a previous run, but the DB update was lost (e.g. crash after the move).
            MarkPublished(job, release, destination, recovered: true);
            return;
        }

        if (!sourceExists)
        {
            RecordError(job, release, $"Holding item not found: {source}");
            return;
        }

        if (destinationExists)
        {
            RecordError(job, release, $"Cannot publish: destination already exists: {destination}");
            return;
        }

        try
        {
            if (sourceIsDir)
                Directory.Move(source, destination);
            else
                File.Move(source, destination, overwrite: false);

            MarkPublished(job, release, destination, recovered: false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            RecordError(job, release, $"Publish failed: {ex.Message}");
        }
    }

    private void MarkPublished(JobDefinition job, TrackedRelease release, string destination, bool recovered)
    {
        release.PublishedAtUtc = DateTime.UtcNow;
        _logger.LogInformation(
            "Job '{JobName}': Published '{ReleaseName}' to {Destination}{Recovered}",
            job.Name, release.Name, destination, recovered ? " (already moved on a previous run)" : "");
    }

    private void RecordError(JobDefinition job, TrackedRelease release, string error)
    {
        // Retried every cycle so a manual fix (e.g. removing a stale destination) is picked up;
        // only log loudly when the error changes to avoid flooding the log.
        if (release.LastError != error)
        {
            release.ErrorCount++;
            release.LastError = error;
            release.LastErrorAtUtc = DateTime.UtcNow;
            _logger.LogError("Job '{JobName}': {Error} ('{ReleaseName}')", job.Name, error, release.Name);
        }
        else
        {
            _logger.LogDebug("Job '{JobName}': {Error} ('{ReleaseName}')", job.Name, error, release.Name);
        }
    }
}
