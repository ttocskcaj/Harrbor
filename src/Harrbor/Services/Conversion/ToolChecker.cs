using Harrbor.Configuration;
using Harrbor.Services.Processes;
using Microsoft.Extensions.Options;

namespace Harrbor.Services.Conversion;

/// <summary>
/// Checks that external tools used by converters can be executed.
/// </summary>
public class ToolChecker
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    // FFmpeg tools only accept a single dash (some builds reject "--version" with a non-zero exit).
    private static readonly Dictionary<string, string> VersionArguments = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ffmpeg"] = "-version",
        ["ffprobe"] = "-version"
    };

    private readonly IProcessRunner _processRunner;
    private readonly ConversionOptions _options;

    public ToolChecker(IProcessRunner processRunner, IOptions<ConversionOptions> options)
    {
        _processRunner = processRunner;
        _options = options.Value;
    }

    /// <summary>Runs each tool's version command (<c>--version</c>, or <c>-version</c> for FFmpeg tools). Returns a description of each failure.</summary>
    public async Task<IReadOnlyList<string>> FindProblemsAsync(IEnumerable<string> tools, CancellationToken cancellationToken = default)
    {
        var problems = new List<string>();
        foreach (var tool in tools.Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.Ordinal))
        {
            var path = _options.ResolveTool(tool);
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(Timeout);
            try
            {
                var versionArg = VersionArguments.GetValueOrDefault(tool, "--version");
                var result = await _processRunner.RunAsync(new ProcessSpec(path, [versionArg]), cts.Token);
                if (!result.Succeeded())
                    problems.Add($"{tool} ('{path}') exited with code {result.ExitCode} for {versionArg}");
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                problems.Add($"{tool} ('{path}') did not respond to its version command within {Timeout.TotalSeconds:0}s");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                problems.Add($"{tool} ('{path}') could not be run: {ex.Message}");
            }
        }
        return problems;
    }
}
