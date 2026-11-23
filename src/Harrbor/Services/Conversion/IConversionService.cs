namespace Harrbor.Services.Conversion;

public record ConversionRunOptions(bool DryRun, long MinFileSizeBytes);

public enum ConversionOutcomeKind
{
    Converted,
    WouldConvert,
    Skipped,
    Unsupported,
    Failed
}

/// <summary>Outcome of one converter on one file. <c>Converter</c> is "probe" for probe failures.</summary>
public record ConversionOutcome(string FilePath, string Converter, ConversionOutcomeKind Kind, string? Detail = null);

public record ConversionResult(IReadOnlyList<ConversionOutcome> Outcomes, TimeSpan Duration)
{
    public int Converted => Outcomes.Count(o => o.Kind == ConversionOutcomeKind.Converted);
    public int Failed => Outcomes.Count(o => o.Kind == ConversionOutcomeKind.Failed);
    public bool Success => Failed == 0;

    /// <summary>
    /// Totals per converter, e.g. "DoviP7ToP81: 1 converted, 2 skipped". Empty outcomes give "no eligible files".
    /// </summary>
    public string Summary
    {
        get
        {
            if (Outcomes.Count == 0)
                return "no eligible files";

            return string.Join("; ", Outcomes
                .GroupBy(o => o.Converter)
                .Select(g => $"{g.Key}: " + string.Join(", ", g
                    .GroupBy(o => o.Kind)
                    .OrderBy(k => k.Key)
                    .Select(k => $"{k.Count()} {Describe(k.Key)}"))));
        }
    }

    public string? FirstError =>
        Outcomes.FirstOrDefault(o => o.Kind == ConversionOutcomeKind.Failed) is { } f
            ? $"{f.Converter} failed for '{Path.GetFileName(f.FilePath)}': {f.Detail}"
            : null;

    private static string Describe(ConversionOutcomeKind kind) => kind switch
    {
        ConversionOutcomeKind.Converted => "converted",
        ConversionOutcomeKind.WouldConvert => "would convert (dry run)",
        ConversionOutcomeKind.Skipped => "skipped",
        ConversionOutcomeKind.Unsupported => "unsupported",
        ConversionOutcomeKind.Failed => "failed",
        _ => kind.ToString()
    };
}

public interface IConversionService
{
    /// <summary>
    /// Runs <paramref name="converters"/> in order over every media file under <paramref name="itemPath"/>
    /// (a file or a directory, recursive). Converter failures are recorded in the result, not thrown;
    /// the original file is left untouched on failure.
    /// </summary>
    Task<ConversionResult> ProcessAsync(
        string itemPath,
        IReadOnlyList<IMediaConverter> converters,
        ConversionRunOptions options,
        CancellationToken cancellationToken = default);
}
