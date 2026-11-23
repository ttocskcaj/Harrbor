using System.Diagnostics;

namespace Harrbor.Services.Conversion;

public class ConversionService : IConversionService
{
    private readonly IMediaProbeService _probeService;
    private readonly ILogger<ConversionService> _logger;

    public ConversionService(IMediaProbeService probeService, ILogger<ConversionService> logger)
    {
        _probeService = probeService;
        _logger = logger;
    }

    public async Task<ConversionResult> ProcessAsync(
        string itemPath,
        IReadOnlyList<IMediaConverter> converters,
        ConversionRunOptions options,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var outcomes = new List<ConversionOutcome>();

        IEnumerable<string> files;
        if (File.Exists(itemPath))
            files = [itemPath];
        else if (Directory.Exists(itemPath))
            files = Directory.EnumerateFiles(itemPath, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal).ToList();
        else
            throw new FileNotFoundException($"Conversion item not found: {itemPath}", itemPath);

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Hidden files include our own temp outputs; never treat them as inputs.
            if (Path.GetFileName(file).StartsWith('.'))
                continue;

            var ext = Path.GetExtension(file);
            var applicable = converters
                .Where(c => c.Extensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
                .ToList();
            if (applicable.Count == 0)
                continue;

            if (new FileInfo(file).Length < options.MinFileSizeBytes)
            {
                _logger.LogDebug("Skipping '{File}': smaller than {MinSize} bytes", file, options.MinFileSizeBytes);
                continue;
            }

            await ProcessFileAsync(file, applicable, options, outcomes, cancellationToken);
        }

        return new ConversionResult(outcomes, stopwatch.Elapsed);
    }

    private async Task ProcessFileAsync(
        string file,
        IReadOnlyList<IMediaConverter> converters,
        ConversionRunOptions options,
        List<ConversionOutcome> outcomes,
        CancellationToken cancellationToken)
    {
        MediaProbe probe;
        try
        {
            probe = await _probeService.ProbeAsync(file, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to probe '{File}'", file);
            outcomes.Add(new ConversionOutcome(file, "probe", ConversionOutcomeKind.Failed, ex.Message));
            return;
        }

        foreach (var converter in converters)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var decision = converter.Evaluate(probe);
            switch (decision.Kind)
            {
                case ConversionDecisionKind.Skip:
                    _logger.LogDebug("{Converter}: skipping '{File}': {Reason}", converter.Name, file, decision.Reason);
                    outcomes.Add(new ConversionOutcome(file, converter.Name, ConversionOutcomeKind.Skipped, decision.Reason));
                    continue;

                case ConversionDecisionKind.Unsupported:
                    _logger.LogWarning("{Converter}: leaving '{File}' untouched, unsupported: {Reason}", converter.Name, file, decision.Reason);
                    outcomes.Add(new ConversionOutcome(file, converter.Name, ConversionOutcomeKind.Unsupported, decision.Reason));
                    continue;
            }

            if (options.DryRun)
            {
                _logger.LogInformation("{Converter}: would convert '{File}' (dry run){Reason}",
                    converter.Name, file, decision.Reason is null ? "" : $": {decision.Reason}");
                outcomes.Add(new ConversionOutcome(file, converter.Name, ConversionOutcomeKind.WouldConvert, decision.Reason));
                continue;
            }

            var converted = await ConvertFileAsync(file, converter, probe, outcomes, cancellationToken);
            if (converted is not null)
                probe = converted;
        }
    }

    /// <summary>Returns the probe of the replaced file on success, or null on failure.</summary>
    private async Task<MediaProbe?> ConvertFileAsync(
        string file,
        IMediaConverter converter,
        MediaProbe probe,
        List<ConversionOutcome> outcomes,
        CancellationToken cancellationToken)
    {
        var dir = Path.GetDirectoryName(file)!;
        var tmpPath = ConversionArtifacts.TempPathFor(file);
        var scratchDir = Path.Combine(dir, ConversionArtifacts.ScratchDirPrefix + Guid.NewGuid().ToString("N"));
        var stopwatch = Stopwatch.StartNew();
        var replaced = false;

        _logger.LogInformation("{Converter}: converting '{File}'", converter.Name, file);

        try
        {
            EnsureFreeSpace(file);

            Directory.CreateDirectory(scratchDir);
            DeleteIfExists(tmpPath);

            await converter.ConvertAsync(file, tmpPath, probe, scratchDir, cancellationToken);

            if (!File.Exists(tmpPath))
                throw new InvalidOperationException("Converter did not produce an output file");

            var outputProbe = await _probeService.ProbeAsync(tmpPath, cancellationToken);
            var verification = converter.Verify(probe, outputProbe);
            if (!verification.Passed)
                throw new InvalidOperationException($"Verification failed: {verification.FailureReason}");

            CopyUnixFileMode(file, tmpPath);
            File.Move(tmpPath, file, overwrite: true);
            replaced = true;

            var newProbe = await _probeService.ProbeAsync(file, cancellationToken);

            _logger.LogInformation("{Converter}: converted '{File}' in {Duration}", converter.Name, file, stopwatch.Elapsed);
            outcomes.Add(new ConversionOutcome(file, converter.Name, ConversionOutcomeKind.Converted));
            return newProbe;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            if (replaced)
            {
                // The file was replaced with verified output but the re-probe failed; the conversion itself stands.
                _logger.LogWarning(ex, "{Converter}: converted '{File}' but re-probe failed", converter.Name, file);
                outcomes.Add(new ConversionOutcome(file, converter.Name, ConversionOutcomeKind.Converted, $"re-probe failed: {ex.Message}"));
                return null;
            }

            _logger.LogError(ex, "{Converter}: conversion failed for '{File}', original left untouched", converter.Name, file);
            outcomes.Add(new ConversionOutcome(file, converter.Name, ConversionOutcomeKind.Failed, ex.Message));
            return null;
        }
        finally
        {
            if (!replaced)
                TryDelete(() => DeleteIfExists(tmpPath), tmpPath);
            TryDelete(() => { if (Directory.Exists(scratchDir)) Directory.Delete(scratchDir, recursive: true); }, scratchDir);
        }
    }

    private static void EnsureFreeSpace(string file)
    {
        // Intermediates (elementary stream + new container) need roughly twice the file size.
        long available;
        try
        {
            available = new DriveInfo(Path.GetDirectoryName(file)!).AvailableFreeSpace;
        }
        catch
        {
            return; // Can't tell; let the tools fail if space really runs out.
        }

        var required = new FileInfo(file).Length * 2;
        if (available < required)
        {
            throw new IOException(
                $"Not enough free disk space: {available / (1L << 20)} MiB available, about {required / (1L << 20)} MiB needed");
        }
    }

    private static void CopyUnixFileMode(string source, string target)
    {
        if (OperatingSystem.IsWindows())
            return;
        try
        {
            File.SetUnixFileMode(target, File.GetUnixFileMode(source));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best effort; permissions of the new file default to the process umask.
        }
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
            File.Delete(path);
    }

    private void TryDelete(Action delete, string path)
    {
        try
        {
            delete();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete conversion leftover '{Path}'", path);
        }
    }
}

/// <summary>
/// Naming of the temp outputs and scratch dirs that conversion creates next to media files.
/// </summary>
public static class ConversionArtifacts
{
    public const string TempMarker = ".harrbor-tmp";
    public const string ScratchDirPrefix = ".harrbor-scratch-";

    /// <summary><c>dir/Movie.mkv</c> → <c>dir/.Movie.harrbor-tmp.mkv</c> (hidden, same directory).</summary>
    public static string TempPathFor(string file) =>
        Path.Combine(
            Path.GetDirectoryName(file)!,
            $".{Path.GetFileNameWithoutExtension(file)}{TempMarker}{Path.GetExtension(file)}");

    /// <summary>
    /// Deletes leftover temp files and scratch dirs under <paramref name="root"/> (from interrupted runs).
    /// Returns the number of items removed.
    /// </summary>
    public static int CleanupLeftovers(string root)
    {
        if (!Directory.Exists(root))
            return 0;

        var removed = 0;
        foreach (var dir in Directory.EnumerateDirectories(root, ScratchDirPrefix + "*", SearchOption.AllDirectories).ToList())
        {
            Directory.Delete(dir, recursive: true);
            removed++;
        }
        foreach (var file in Directory.EnumerateFiles(root, $".*{TempMarker}*", SearchOption.AllDirectories).ToList())
        {
            File.Delete(file);
            removed++;
        }
        return removed;
    }
}
