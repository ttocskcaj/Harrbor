using System.Diagnostics;
using System.Text;

namespace Harrbor.Services.Processes;

public class ProcessRunner : IProcessRunner
{
    // Bound captured text so a chatty tool can't grow memory without limit; keep the tail (errors are usually last).
    private const int MaxCapturedChars = 256 * 1024;

    private readonly ILogger<ProcessRunner> _logger;

    public ProcessRunner(ILogger<ProcessRunner> logger)
    {
        _logger = logger;
    }

    public async Task<ProcessResult> RunAsync(ProcessSpec spec, CancellationToken cancellationToken = default)
    {
        var results = await RunPipelineAsync([spec], cancellationToken);
        return results[0];
    }

    public async Task<IReadOnlyList<ProcessResult>> RunPipelineAsync(
        IReadOnlyList<ProcessSpec> specs,
        CancellationToken cancellationToken = default)
    {
        if (specs.Count == 0)
            throw new ArgumentException("At least one process is required", nameof(specs));

        _logger.LogDebug("Running: {Pipeline}", string.Join(" | ", specs));

        var processes = new List<Process>(specs.Count);
        var stderrTasks = new List<Task<string>>(specs.Count);
        var pumpTasks = new List<Task>(specs.Count);
        Task<string>? stdoutTask = null;

        // Linked token lets one failing stage tear down the others.
        using var pipelineCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        void KillAll()
        {
            foreach (var p in processes)
            {
                try
                {
                    if (!p.HasExited)
                        p.Kill(entireProcessTree: true);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to kill process {Name}", p.StartInfo.FileName);
                }
            }
        }

        try
        {
            for (var i = 0; i < specs.Count; i++)
            {
                var spec = specs[i];
                var psi = new ProcessStartInfo
                {
                    FileName = spec.FileName,
                    RedirectStandardInput = i > 0,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                foreach (var arg in spec.Arguments)
                    psi.ArgumentList.Add(arg);

                var process = new Process { StartInfo = psi };
                process.Start();
                processes.Add(process);
                stderrTasks.Add(ReadCappedAsync(process.StandardError, CancellationToken.None));

                if (i > 0)
                {
                    var upstream = processes[i - 1];
                    pumpTasks.Add(PumpAsync(upstream, process, pipelineCts.Token));
                }
            }

            stdoutTask = ReadCappedAsync(processes[^1].StandardOutput, CancellationToken.None);

            using var registration = pipelineCts.Token.Register(KillAll);

            // If any stage exits non-zero, kill the rest so we don't wait on a stalled pipe.
            var waitTasks = processes
                .Select(async p =>
                {
                    await p.WaitForExitAsync(CancellationToken.None);
                    if (p.ExitCode != 0 && processes.Count > 1)
                        pipelineCts.Cancel();
                })
                .ToList();

            await Task.WhenAll(waitTasks);
            await Task.WhenAll(pumpTasks);

            cancellationToken.ThrowIfCancellationRequested();

            var stdout = await stdoutTask;
            var results = new List<ProcessResult>(processes.Count);
            for (var i = 0; i < processes.Count; i++)
            {
                var stderr = await stderrTasks[i];
                results.Add(new ProcessResult(
                    specs[i].FileName,
                    processes[i].ExitCode,
                    i == processes.Count - 1 ? stdout : string.Empty,
                    stderr));
            }

            return results;
        }
        catch
        {
            KillAll();
            throw;
        }
        finally
        {
            foreach (var p in processes)
                p.Dispose();
        }
    }

    private static async Task PumpAsync(Process upstream, Process downstream, CancellationToken ct)
    {
        try
        {
            await upstream.StandardOutput.BaseStream.CopyToAsync(downstream.StandardInput.BaseStream, 1 << 20, ct);
        }
        catch (Exception ex) when (ex is IOException or OperationCanceledException or ObjectDisposedException)
        {
            // Downstream closed its stdin (exited) or the pipeline was cancelled; exit codes tell the story.
        }
        finally
        {
            try
            {
                downstream.StandardInput.Close();
            }
            catch (Exception ex) when (ex is IOException or ObjectDisposedException or InvalidOperationException)
            {
                // Already gone.
            }
        }
    }

    private static async Task<string> ReadCappedAsync(StreamReader reader, CancellationToken ct)
    {
        var sb = new StringBuilder();
        var buffer = new char[8192];
        int read;
        while ((read = await reader.ReadAsync(buffer, ct)) > 0)
        {
            sb.Append(buffer, 0, read);
            if (sb.Length > MaxCapturedChars)
                sb.Remove(0, sb.Length - MaxCapturedChars);
        }
        return sb.ToString();
    }
}
