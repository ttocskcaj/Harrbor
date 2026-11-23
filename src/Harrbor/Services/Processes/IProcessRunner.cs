namespace Harrbor.Services.Processes;

/// <summary>
/// One external process invocation. Arguments are passed verbatim (no shell, no quoting needed).
/// </summary>
public record ProcessSpec(string FileName, IReadOnlyList<string> Arguments)
{
    public override string ToString() => $"{FileName} {string.Join(' ', Arguments)}";
}

public record ProcessResult(string FileName, int ExitCode, string StandardOutput, string StandardError)
{
    public bool Succeeded(params int[] allowedExitCodes) =>
        ExitCode == 0 || allowedExitCodes.Contains(ExitCode);
}

public class ProcessFailedException : Exception
{
    public ProcessResult Result { get; }

    public ProcessFailedException(ProcessResult result)
        : base($"{Path.GetFileName(result.FileName)} exited with code {result.ExitCode}: {Trim(result.StandardError)}")
    {
        Result = result;
    }

    private static string Trim(string s)
    {
        s = s.Trim();
        return s.Length <= 1000 ? s : "…" + s[^1000..];
    }
}

public interface IProcessRunner
{
    /// <summary>
    /// Runs a single process, capturing stdout and stderr. Does not throw on non-zero exit codes.
    /// On cancellation the process tree is killed and <see cref="OperationCanceledException"/> is thrown.
    /// </summary>
    Task<ProcessResult> RunAsync(ProcessSpec spec, CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs processes connected stdout→stdin in order. The last process's stdout is captured.
    /// Returns one result per process, in order. If any process fails, the rest are killed.
    /// </summary>
    Task<IReadOnlyList<ProcessResult>> RunPipelineAsync(IReadOnlyList<ProcessSpec> specs, CancellationToken cancellationToken = default);
}
