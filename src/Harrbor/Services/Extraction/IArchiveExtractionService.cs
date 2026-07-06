namespace Harrbor.Services.Extraction;

public record ExtractionResult(
    bool Success,
    int ArchivesExtracted = 0,
    TimeSpan Duration = default,
    string? Error = null);

public interface IArchiveExtractionService
{
    /// <summary>
    /// Scans a staging item (file or directory) for zip/rar/7z archives and extracts each
    /// volume set in place, next to its first volume. Only the original file set is scanned;
    /// archives produced by extraction are not extracted again.
    /// </summary>
    Task<ExtractionResult> ExtractAsync(string stagingItemPath, CancellationToken cancellationToken = default);
}
