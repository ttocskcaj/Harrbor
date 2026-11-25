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
    private readonly ILogger<ConversionPhaseHandler> _logger;

    public ConversionPhaseHandler(
        IConversionService conversionService,
        ConverterRegistry converterRegistry,
        IOptions<ConversionOptions> options,
        ILogger<ConversionPhaseHandler> logger)
    {
        _conversionService = conversionService;
        _converterRegistry = converterRegistry;
        _options = options;
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

        // Skips are instant; real conversions are long-running, so at most one release is converted
        // per cycle. That bounds CPU/disk use and lets the other phases keep moving in between.
        var converted = false;
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

            if (converted)
                continue;

            await ConvertReleaseAsync(job, release, dbContext, cancellationToken);
            converted = true;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task ConvertReleaseAsync(
        JobDefinition job,
        TrackedRelease release,
        HarrborDbContext dbContext,
        CancellationToken cancellationToken)
    {
        release.ConversionStatus = ConversionStatus.InProgress;
        release.ConversionStartedAtUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

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
        catch (OperationCanceledException)
        {
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
