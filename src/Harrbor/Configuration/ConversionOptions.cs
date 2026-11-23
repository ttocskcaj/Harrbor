namespace Harrbor.Configuration;

/// <summary>
/// Settings for the media Conversion phase and the <c>convert</c> CLI verb.
/// Per-converter settings, when a converter needs them, live under <c>Conversion:Converters:{Name}</c>
/// and are bound by that converter.
/// </summary>
public class ConversionOptions
{
    public const string SectionName = "Conversion";

    /// <summary>
    /// Evaluate converters and log what would happen, without writing any file.
    /// </summary>
    public bool DryRun { get; set; }

    /// <summary>
    /// Files smaller than this are ignored (skips samples, extras). Default 1 GiB.
    /// </summary>
    public long MinFileSizeBytes { get; set; } = 1L << 30;

    /// <summary>
    /// Allowed difference between source and converted duration.
    /// </summary>
    public double DurationToleranceSeconds { get; set; } = 1;

    /// <summary>
    /// External tool name → executable path. Tools not listed are resolved by name on PATH.
    /// </summary>
    public Dictionary<string, string> Tools { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public string ResolveTool(string name) =>
        Tools.TryGetValue(name, out var path) && !string.IsNullOrWhiteSpace(path) ? path : name;
}
