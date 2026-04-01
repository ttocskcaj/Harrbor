using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Harrbor.Configuration;

namespace Harrbor.Services.RemoteStorage;

public partial class RcloneRemoteStorageService : IRemoteStorageService
{
    private readonly RemoteStorageOptions _options;
    private readonly ILogger<RcloneRemoteStorageService> _logger;
    private readonly string _remotePrefix;

    public RcloneRemoteStorageService(
        IOptions<RemoteStorageOptions> options,
        ILogger<RcloneRemoteStorageService> logger)
    {
        _options = options.Value;
        _logger = logger;
        _remotePrefix = BuildRemotePrefix();
        _logger.LogDebug("Rclone remote prefix: {RemotePrefix}", RedactSensitiveArgs(_remotePrefix));
    }

    private string BuildRemotePrefix()
    {
        var parts = new List<string> { ":sftp" };

        if (!string.IsNullOrEmpty(_options.Host))
            parts.Add($"host={_options.Host}");

        if (_options.Port != 22)
            parts.Add($"port={_options.Port}");

        if (!string.IsNullOrEmpty(_options.User))
            parts.Add($"user={_options.User}");

        if (!string.IsNullOrEmpty(_options.Password))
            parts.Add($"pass={ObscurePassword(_options.Password)}");

        if (!string.IsNullOrEmpty(_options.KeyFile))
            parts.Add($"key_file={_options.KeyFile}");

        if (!string.IsNullOrEmpty(_options.KeyFilePassphrase))
            parts.Add($"key_file_pass={ObscurePassword(_options.KeyFilePassphrase)}");

        if (_options.UseAgent)
            parts.Add("key_use_agent=true");

        return string.Join(",", parts) + ":";
    }

    private static string ObscurePassword(string password)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "rclone",
                Arguments = $"obscure \"{password}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();
        var output = process.StandardOutput.ReadToEnd().Trim();
        var error = process.StandardError.ReadToEnd().Trim();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"rclone obscure failed with exit code {process.ExitCode}: {error}");
        }

        if (string.IsNullOrEmpty(output))
        {
            throw new InvalidOperationException(
                "rclone obscure returned empty output");
        }

        return output;
    }

    public async Task<TransferResult> TransferAsync(
        string sourcePath,
        string destinationPath,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var remoteSource = $"{_remotePrefix}{sourcePath}";

        var args = new List<string>
        {
            "copy",
            $"\"{remoteSource}\"",
            $"\"{destinationPath}\"",
            "--stats=10s",
            "--stats-one-line",
            "--stats-log-level=NOTICE",
            "--log-level=INFO",
            "--low-level-retries=10",
            "--no-update-modtime",
            $"--transfers={_options.Transfers}",
            $"--checkers={_options.Checkers}"
        };

        args.AddRange(_options.AdditionalFlags);

        var argString = string.Join(" ", args);

        _logger.LogDebug("Running rclone: rclone {Arguments}", RedactSensitiveArgs(argString));

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "rclone",
                Arguments = argString,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        var outputLines = new List<string>();
        var errorLines = new List<string>();

        process.OutputDataReceived += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                outputLines.Add(e.Data);
                _logger.LogTrace("rclone stdout: {Line}", e.Data);
            }
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                errorLines.Add(e.Data);
                // rclone sends progress to stderr
                if (e.Data.Contains("Transferred:"))
                {
                    _logger.LogDebug("rclone progress: {Line}", e.Data);
                }
                else
                {
                    _logger.LogTrace("rclone stderr: {Line}", e.Data);
                }
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to kill rclone process after cancellation");
            }
            throw;
        }

        stopwatch.Stop();

        if (process.ExitCode != 0)
        {
            var errorOutput = string.Join(Environment.NewLine, errorLines);
            _logger.LogError("rclone failed with exit code {ExitCode}: {Error}",
                process.ExitCode, errorOutput);

            return new TransferResult(
                Success: false,
                Duration: stopwatch.Elapsed,
                Error: $"rclone exit code {process.ExitCode}: {errorOutput}");
        }

        // Parse bytes transferred from rclone output
        var bytesTransferred = ParseBytesTransferred(errorLines);

        return new TransferResult(
            Success: true,
            BytesTransferred: bytesTransferred,
            Duration: stopwatch.Elapsed);
    }

    public async Task<RemoteStorageConnectionResult> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Testing rclone SFTP connection to {Host}", _options.Host);

        try
        {
            // Test by listing the root of the remote
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "rclone",
                    Arguments = $"lsf \"{_remotePrefix}/\" --max-depth=1",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var error = await process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode != 0)
            {
                return new RemoteStorageConnectionResult(false, $"rclone connection test failed: {error.Trim()}");
            }

            return new RemoteStorageConnectionResult(true);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new RemoteStorageConnectionResult(false, $"rclone connection test failed: {ex.Message}");
        }
    }

    public async Task<bool> ExistsAsync(string path, CancellationToken cancellationToken = default)
    {
        var remotePath = $"{_remotePrefix}{path}";

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "rclone",
                Arguments = $"lsf \"{remotePath}\" --max-depth=1",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();
        var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        // If the path exists (file or directory with contents), lsf will return something
        // If it's a file, lsf on the exact path returns the filename
        // If it doesn't exist, exit code will be non-zero or output will be empty
        return process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output);
    }

    internal static long ParseBytesTransferred(List<string> lines)
    {
        // Look for lines like "Transferred: 1.234 GiB / 1.234 GiB, 100%, 50.000 MiB/s"
        foreach (var line in lines.AsEnumerable().Reverse())
        {
            var match = BytesTransferredRegex().Match(line);
            if (match.Success)
            {
                var value = double.Parse(match.Groups[1].Value);
                var unit = match.Groups[2].Value;

                return unit.ToUpperInvariant() switch
                {
                    "B" => (long)value,
                    "KIB" => (long)(value * 1024),
                    "MIB" => (long)(value * 1024 * 1024),
                    "GIB" => (long)(value * 1024 * 1024 * 1024),
                    "TIB" => (long)(value * 1024 * 1024 * 1024 * 1024),
                    _ => (long)value
                };
            }
        }

        return 0;
    }

    internal static string RedactSensitiveArgs(string args)
    {
        // Redact pass= and key_file_pass= values from rclone connection strings
        return SensitiveArgRegex().Replace(args, "$1[REDACTED]");
    }

    [GeneratedRegex(@"(pass=|key_file_pass=)[^,\s]+")]
    private static partial Regex SensitiveArgRegex();

    [GeneratedRegex(@"Transferred:\s+([\d.]+)\s+(\w+)\s+/")]
    private static partial Regex BytesTransferredRegex();
}
