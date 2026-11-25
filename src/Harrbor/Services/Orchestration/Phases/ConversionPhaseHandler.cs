using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Harrbor.Configuration;
using Harrbor.Data;
using Harrbor.Data.Entities;
using Harrbor.Services.Conversion;

namespace Harrbor.Services.Orchestration.Phases;

public interface IConversionPhaseHandler : IPhaseHandler
{
}

public class ConversionPhaseHandler : IConversionPhaseHandler
{
    private readonly IConversionService _conversionService;
    private readonly ConverterRegistry _converterRegistry;
    private readonly IOptions<ConversionOptions> _options;
    private readonly ConversionCoordinator _coordinator;
    private readonly ILogger<ConversionPhaseHandler> _logger;

    public ConversionPhaseHandler(
        IConversionService conversionService,
        ConverterRegistry converterRegistry,
        IOptions<ConversionOptions> options,
        ConversionCoordinator coordinator,
        ILogger<ConversionPhaseHandler> logger)
    {
        _conversionService = conversionService;
        _converterRegistry = converterRegistry;
        _options = options;
        _coordinator = coordinator;
        _logger = logger;
    }

    public async Task ExecuteAsync(JobDefinition job, HarrborDbContext dbContext, CancellationToken cancellationToken)
    {
        var pending = await dbContext.TrackedReleases
            .Where(r => r.JobName == job.Name
                && r.TransferStatus == TransferStatus.Completed
                && r.ExtractionStatus == ExtractionStatus.Completed
                && r.ConversionStatus == ConversionStatus.Pending
                && r.ImportStatus == ImportStatus.Pending)
            .OrderBy(r => r.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        if (pending.Count == 0)
            return;

        // Skips are instant. Real conversions are long-running and I/O-heavy, so they run in the background,
        // one at a time across all jobs; the job loop doesn't wait for them.
        TrackedRelease? toConvert = null;
        foreach (var release in pending)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (job.Converters.Count == 0)
            {
                release.ConversionStatus = ConversionStatus.Skipped;
                release.ConversionCompletedAtUtc = DateTime.UtcNow;
                _logger.LogDebug(
                    "Job '{JobName}': Conversion skipped for '{ReleaseName}' (no converters configured)",
                    job.Name, release.Name);
                continue;
            }

            if (toConvert == null && _coordinator.RunningCount == 0)
                toConvert = release;
        }

        if (toConvert != null)
        {
            // Claim it from this thread, then hand it to the background task (which reloads it in its own context)
            toConvert.ConversionStatus = ConversionStatus.InProgress;
            toConvert.ConversionStartedAtUtc = DateTime.UtcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        if (toConvert != null)
        {
            dbContext.Entry(toConvert).State = EntityState.Detached;
            var releaseId = toConvert.Id;
            _coordinator.TryStart(
                releaseId,
                job.Name,
                (sp, ct) => RunConversionAsync(job, releaseId, sp.GetRequiredService<HarrborDbContext>(), ct),
                cancellationToken);
        }
    }

    /// <summary>One conversion, run in the background with its own scope and DbContext.</summary>
    private async Task RunConversionAsync(JobDefinition job, int releaseId, HarrborDbContext dbContext, CancellationToken cancellationToken)
    {
        var release = await dbContext.TrackedReleases.FirstOrDefaultAsync(r => r.Id == releaseId, cancellationToken);
        if (release == null)
            return;

        await ConvertReleaseAsync(job, release, dbContext, cancellationToken);
    }

    private async Task ConvertReleaseAsync(
        JobDefinition job,
        TrackedRelease release,
        HarrborDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var itemPath = release.GetWorkingItemPath();
        var options = _options.Value;

        try
        {
            var converters = _converterRegistry.Resolve(job.Converters);
            var result = await _conversionService.ProcessAsync(
                itemPath,
                converters,
                new ConversionRunOptions(options.DryRun, options.MinFileSizeBytes),
                cancellationToken);

            release.FilesConverted = result.Converted;
            release.ConversionSummary = Truncate(result.Summary);
            release.ConversionCompletedAtUtc = DateTime.UtcNow;

            if (result.Success)
            {
                release.ConversionStatus = ConversionStatus.Completed;
                _logger.LogInformation(
                    "Job '{JobName}': Conversion completed for '{ReleaseName}' in {Duration}: {Summary}",
                    job.Name, release.Name, result.Duration, result.Summary);
            }
            else
            {
                // Converter failures are deterministic; the release is still published, unconverted.
                MarkFailed(release, result.FirstError ?? "conversion failed");
                _logger.LogError(
                    "Job '{JobName}': Conversion failed for '{ReleaseName}', it will be published unconverted: {Error} ({Summary})",
                    job.Name, release.Name, release.LastError, result.Summary);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Shutting down: leave it InProgress; the startup reset puts it back to Pending.
            throw;
        }
        catch (Exception ex)
        {
            release.ConversionCompletedAtUtc = DateTime.UtcNow;
            release.ConversionSummary = Truncate($"error: {ex.Message}");
            MarkFailed(release, ex.Message);
            _logger.LogError(ex,
                "Job '{JobName}': Conversion failed with exception for '{ReleaseName}', it will be published unconverted",
                job.Name, release.Name);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static void MarkFailed(TrackedRelease release, string error)
    {
        release.ConversionStatus = ConversionStatus.Failed;
        release.ErrorCount++;
        release.LastError = Truncate(error);
        release.LastErrorAtUtc = DateTime.UtcNow;
    }

    private static string Truncate(string s) => s.Length <= 2000 ? s : s[..1997] + "...";
}
