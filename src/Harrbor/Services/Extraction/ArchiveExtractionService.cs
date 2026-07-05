using System.Diagnostics;
using System.Text.RegularExpressions;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Readers;
using SharpCompress.Readers.Rar;

namespace Harrbor.Services.Extraction;

public partial class ArchiveExtractionService : IArchiveExtractionService
{
    private readonly ILogger<ArchiveExtractionService> _logger;

    public ArchiveExtractionService(ILogger<ArchiveExtractionService> logger)
    {
        _logger = logger;
    }

    public Task<ExtractionResult> ExtractAsync(string stagingItemPath, CancellationToken cancellationToken = default)
    {
        return Task.Run(() => ExtractCore(stagingItemPath, cancellationToken), cancellationToken);
    }

    private ExtractionResult ExtractCore(string stagingItemPath, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        IReadOnlyCollection<string> files;
        if (File.Exists(stagingItemPath))
            files = [stagingItemPath];
        else if (Directory.Exists(stagingItemPath))
            files = Directory.EnumerateFiles(stagingItemPath, "*", SearchOption.AllDirectories).ToList();
        else
            return new ExtractionResult(false, Error: $"Staging item not found: {stagingItemPath}");

        var scan = FindFirstVolumes(files);

        if (scan.OrphanedContinuations.Count > 0)
        {
            var orphanNames = string.Join(", ", scan.OrphanedContinuations.Select(Path.GetFileName));
            return new ExtractionResult(
                false,
                Duration: stopwatch.Elapsed,
                Error: $"Archive volume files found without their first volume: {orphanNames}");
        }

        var archivesExtracted = 0;

        foreach (var volumeSet in scan.VolumeSets)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!ArchiveFactory.IsArchive(volumeSet.FirstVolume, out _))
            {
                _logger.LogWarning(
                    "File is named like an archive but has no recognized archive signature, skipping: {Path}",
                    volumeSet.FirstVolume);
                continue;
            }

            try
            {
                ExtractVolumeSet(volumeSet, cancellationToken);
                archivesExtracted++;

                _logger.LogDebug(
                    "Extracted archive '{FirstVolume}' ({VolumeCount} volumes)",
                    volumeSet.FirstVolume, volumeSet.Volumes.Count);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return new ExtractionResult(
                    false,
                    archivesExtracted,
                    stopwatch.Elapsed,
                    $"Failed to extract '{Path.GetFileName(volumeSet.FirstVolume)}': {ex.Message}");
            }
        }

        return new ExtractionResult(true, archivesExtracted, stopwatch.Elapsed);
    }

    private static void ExtractVolumeSet(ArchiveVolumeSet volumeSet, CancellationToken cancellationToken)
    {
        // Extract in place so Sonarr/Radarr find media where they already scan, and so the
        // cleanup phase's recursive delete of the staging item removes the extracted output.
        var destination = Path.GetDirectoryName(volumeSet.FirstVolume)!;

        using var archive = ArchiveFactory.OpenArchive(new FileInfo(volumeSet.FirstVolume));

        if (!archive.IsComplete)
            throw new InvalidOperationException("archive volume set is incomplete");

        if (archive.IsEncrypted || archive.Entries.Any(e => e.IsEncrypted))
            throw new InvalidOperationException("archive is password-protected (not supported)");

        if (archive.Type == ArchiveType.Rar && archive.IsSolid)
        {
            // Solid RARs can only be unpacked through the forward-only reader API.
            ExtractSolidRar(volumeSet, destination, cancellationToken);
            return;
        }

        archive.WriteToDirectory(destination, DefaultExtractionOptions());
    }

    private static void ExtractSolidRar(ArchiveVolumeSet volumeSet, string destination, CancellationToken cancellationToken)
    {
        var streams = volumeSet.Volumes.Select(File.OpenRead).ToList();
        try
        {
            using var reader = RarReader.OpenReader(streams);
            while (reader.MoveToNextEntry())
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (reader.Entry.IsDirectory)
                    continue;

                reader.WriteEntryToDirectory(destination, DefaultExtractionOptions());
            }
        }
        finally
        {
            foreach (var stream in streams)
                stream.Dispose();
        }
    }

    private static ExtractionOptions DefaultExtractionOptions() => new()
    {
        ExtractFullPath = true,
        // Overwriting existing output makes re-extraction after a crash or restart idempotent.
        Overwrite = true
    };

    /// <summary>
    /// Classifies files into archive volume sets keyed by their first volume, using filename
    /// conventions. Detection must be name-based rather than signature-based because old-style
    /// RAR continuation volumes (.r00, .r01, ...) carry RAR signatures too and would otherwise
    /// be extracted once per volume.
    /// </summary>
    internal static ArchiveScanResult FindFirstVolumes(IReadOnlyCollection<string> filePaths)
    {
        var pathSet = new HashSet<string>(filePaths, StringComparer.OrdinalIgnoreCase);
        var volumeSets = new List<ArchiveVolumeSet>();
        var orphans = new List<string>();

        foreach (var path in filePaths.OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
        {
            var fileName = Path.GetFileName(path);

            // New-style RAR volumes: name.part1.rar / name.part01.rar / ...
            var partMatch = NewStyleRarVolumeRegex().Match(fileName);
            if (partMatch.Success)
            {
                if (int.Parse(partMatch.Groups[1].Value) == 1)
                    volumeSets.Add(new ArchiveVolumeSet(path, CollectNewStyleRarVolumes(path, partMatch, filePaths)));
                else if (!HasSibling(pathSet, path, FirstPartCandidates(fileName, partMatch)))
                    orphans.Add(path);
                continue;
            }

            // Old-style RAR sets (name.rar + name.r00 + ...) and standalone rars.
            if (fileName.EndsWith(".rar", StringComparison.OrdinalIgnoreCase))
            {
                volumeSets.Add(new ArchiveVolumeSet(path, CollectOldStyleRarVolumes(path, filePaths)));
                continue;
            }

            // Continuation volumes: .r00-.y99 belong to an old-style RAR set; .z01-.z99 can
            // belong to either an old-style RAR set or a split zip.
            var continuationMatch = ContinuationVolumeRegex().Match(fileName);
            if (continuationMatch.Success)
            {
                var stem = fileName[..continuationMatch.Index];
                var candidates = new List<string> { stem + ".rar" };
                if (char.ToLowerInvariant(continuationMatch.Groups[1].Value[0]) == 'z')
                    candidates.Add(stem + ".zip");

                if (!HasSibling(pathSet, path, candidates))
                    orphans.Add(path);
                continue;
            }

            if (fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                volumeSets.Add(new ArchiveVolumeSet(path, CollectSplitZipVolumes(path, filePaths)));
                continue;
            }

            if (fileName.EndsWith(".7z", StringComparison.OrdinalIgnoreCase))
            {
                volumeSets.Add(new ArchiveVolumeSet(path, [path]));
                continue;
            }

            // Split 7z volumes: name.7z.001 / name.7z.002 / ...
            var sevenZipMatch = SplitSevenZipVolumeRegex().Match(fileName);
            if (sevenZipMatch.Success)
            {
                if (int.Parse(sevenZipMatch.Groups[1].Value) == 1)
                    volumeSets.Add(new ArchiveVolumeSet(path, CollectSplitSevenZipVolumes(path, sevenZipMatch, filePaths)));
                else if (!HasSibling(pathSet, path, [fileName[..sevenZipMatch.Index] + ".7z.001"]))
                    orphans.Add(path);
            }
        }

        return new ArchiveScanResult(volumeSets, orphans);
    }

    private static List<string> CollectNewStyleRarVolumes(
        string firstVolume, Match firstMatch, IReadOnlyCollection<string> filePaths)
    {
        var directory = Path.GetDirectoryName(firstVolume);
        var prefix = Path.GetFileName(firstVolume)[..firstMatch.Index];

        return filePaths
            .Select(p => (Path: p, Match: NewStyleRarVolumeRegex().Match(Path.GetFileName(p))))
            .Where(x => x.Match.Success
                && string.Equals(Path.GetDirectoryName(x.Path), directory, StringComparison.OrdinalIgnoreCase)
                && string.Equals(Path.GetFileName(x.Path)[..x.Match.Index], prefix, StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => int.Parse(x.Match.Groups[1].Value))
            .Select(x => x.Path)
            .ToList();
    }

    private static List<string> CollectOldStyleRarVolumes(string firstVolume, IReadOnlyCollection<string> filePaths)
    {
        var directory = Path.GetDirectoryName(firstVolume);
        var stem = Path.GetFileNameWithoutExtension(firstVolume);
        var volumes = new List<string> { firstVolume };

        volumes.AddRange(filePaths
            .Select(p => (Path: p, Match: ContinuationVolumeRegex().Match(Path.GetFileName(p))))
            .Where(x => x.Match.Success
                && string.Equals(Path.GetDirectoryName(x.Path), directory, StringComparison.OrdinalIgnoreCase)
                && string.Equals(Path.GetFileName(x.Path)[..x.Match.Index], stem, StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => char.ToLowerInvariant(x.Match.Groups[1].Value[0]))
            .ThenBy(x => int.Parse(x.Match.Groups[2].Value))
            .Select(x => x.Path));

        return volumes;
    }

    private static List<string> CollectSplitZipVolumes(string firstVolume, IReadOnlyCollection<string> filePaths)
    {
        var directory = Path.GetDirectoryName(firstVolume);
        var stem = Path.GetFileNameWithoutExtension(firstVolume);
        var volumes = new List<string> { firstVolume };

        volumes.AddRange(filePaths
            .Select(p => (Path: p, Match: SplitZipContinuationRegex().Match(Path.GetFileName(p))))
            .Where(x => x.Match.Success
                && string.Equals(Path.GetDirectoryName(x.Path), directory, StringComparison.OrdinalIgnoreCase)
                && string.Equals(Path.GetFileName(x.Path)[..x.Match.Index], stem, StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => int.Parse(x.Match.Groups[1].Value))
            .Select(x => x.Path));

        return volumes;
    }

    private static List<string> CollectSplitSevenZipVolumes(
        string firstVolume, Match firstMatch, IReadOnlyCollection<string> filePaths)
    {
        var directory = Path.GetDirectoryName(firstVolume);
        var prefix = Path.GetFileName(firstVolume)[..firstMatch.Index];

        return filePaths
            .Select(p => (Path: p, Match: SplitSevenZipVolumeRegex().Match(Path.GetFileName(p))))
            .Where(x => x.Match.Success
                && string.Equals(Path.GetDirectoryName(x.Path), directory, StringComparison.OrdinalIgnoreCase)
                && string.Equals(Path.GetFileName(x.Path)[..x.Match.Index], prefix, StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => int.Parse(x.Match.Groups[1].Value))
            .Select(x => x.Path)
            .ToList();
    }

    private static IEnumerable<string> FirstPartCandidates(string fileName, Match match)
    {
        var prefix = fileName[..match.Index];
        var digits = match.Groups[1].Value;

        // Sets may be zero-padded (part01) or not (part1); accept either as the first volume.
        yield return $"{prefix}.part{"1".PadLeft(digits.Length, '0')}.rar";
        yield return $"{prefix}.part1.rar";
    }

    private static bool HasSibling(
        HashSet<string> pathSet, string continuationPath, IEnumerable<string> candidateFileNames)
    {
        var directory = Path.GetDirectoryName(continuationPath) ?? string.Empty;
        return candidateFileNames.Any(name => pathSet.Contains(Path.Combine(directory, name)));
    }

    [GeneratedRegex(@"\.part(\d+)\.rar$", RegexOptions.IgnoreCase)]
    private static partial Regex NewStyleRarVolumeRegex();

    [GeneratedRegex(@"\.([r-z])(\d{2})$", RegexOptions.IgnoreCase)]
    private static partial Regex ContinuationVolumeRegex();

    [GeneratedRegex(@"\.z(\d{2})$", RegexOptions.IgnoreCase)]
    private static partial Regex SplitZipContinuationRegex();

    [GeneratedRegex(@"\.7z\.(\d{3})$", RegexOptions.IgnoreCase)]
    private static partial Regex SplitSevenZipVolumeRegex();
}

/// <summary>
/// An archive and its volume files in extraction order, keyed by the first volume.
/// </summary>
internal record ArchiveVolumeSet(string FirstVolume, IReadOnlyList<string> Volumes);

/// <summary>
/// Result of scanning a file set for archives: complete volume sets to extract, plus any
/// continuation volumes whose first volume is missing (which must fail the release rather
/// than let import proceed against unextracted archives).
/// </summary>
internal record ArchiveScanResult(
    IReadOnlyList<ArchiveVolumeSet> VolumeSets,
    IReadOnlyList<string> OrphanedContinuations);
