namespace Harrbor.Services.Conversion;

public enum ConversionDecisionKind
{
    /// <summary>The converter should rewrite this file.</summary>
    Convert,
    /// <summary>Nothing to do (the file is already in the desired form or not the converter's concern).</summary>
    Skip,
    /// <summary>The file is a candidate but in a variant the converter can't handle safely. Logged as a warning.</summary>
    Unsupported
}

public record ConversionDecision(ConversionDecisionKind Kind, string? Reason = null)
{
    public static ConversionDecision Convert(string? reason = null) => new(ConversionDecisionKind.Convert, reason);
    public static ConversionDecision Skip(string reason) => new(ConversionDecisionKind.Skip, reason);
    public static ConversionDecision Unsupported(string reason) => new(ConversionDecisionKind.Unsupported, reason);
}

public record ConversionVerification(bool Passed, string? FailureReason = null)
{
    public static ConversionVerification Pass() => new(true);
    public static ConversionVerification Fail(string reason) => new(false, reason);
}

/// <summary>
/// A pluggable, lossless media file rewrite. Converters are run by <see cref="IConversionService"/>,
/// which handles file discovery, probing, temp files, verification and the atomic replace.
/// </summary>
public interface IMediaConverter
{
    /// <summary>Config key used in <c>JobDefinition.Converters</c>, e.g. "DoviP7ToP81".</summary>
    string Name { get; }

    /// <summary>File extensions (with leading dot, case-insensitive) this converter applies to.</summary>
    IReadOnlyCollection<string> Extensions { get; }

    /// <summary>Logical tool names (keys of <c>Conversion:Tools</c>) needed by <see cref="ConvertAsync"/>.</summary>
    IReadOnlyCollection<string> RequiredTools { get; }

    /// <summary>Pure decision from probe data; must be cheap and side-effect free.</summary>
    ConversionDecision Evaluate(MediaProbe probe);

    /// <summary>
    /// Writes the converted output to <paramref name="outputPath"/>. Must never modify <paramref name="inputPath"/>.
    /// <paramref name="scratchDir"/> is an empty directory on the same filesystem for intermediates;
    /// the caller deletes it afterwards. Throw on failure.
    /// </summary>
    Task ConvertAsync(string inputPath, string outputPath, MediaProbe probe, string scratchDir, CancellationToken cancellationToken);

    /// <summary>Checks the converted output against the source.</summary>
    ConversionVerification Verify(MediaProbe source, MediaProbe output);
}
