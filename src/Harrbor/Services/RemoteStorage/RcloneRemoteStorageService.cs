using System.Diagnostics;
using System.Text.RegularExpressions;
using Harrbor.Configuration.Connections;
using Harrbor.Services.Configuration;

namespace Harrbor.Services.RemoteStorage;

public partial class RcloneRemoteStorageService : IRemoteStorageService, IDisposable
{
    private readonly IConnectionStore _connectionStore;
    private readonly ILogger<RcloneRemoteStorageService> _logger;

    // The transfer phase fans out TransferParallelism tasks over this one scoped
    // instance, so the cache must be safe for concurrent readers: an immutable
    // record behind a volatile reference, with a semaphore so `rclone obscure`
    // runs at most once per config version.
    private sealed record CachedConnection(string Prefix, RemoteStorageSettings Settings, int Version);
    private volatile CachedConnection? _cached;
    private readonly SemaphoreSlim _cacheLock = new(1, 1);

    public RcloneRemoteStorageService(
        IConnectionStore connectionStore,
        ILogger<RcloneRemoteStorageService> logger)
    {
        _connectionStore = connectionStore;
        _logger = logger;
    }

    private async Task<(string Prefix, RemoteStorageSettings Settings)> GetConnectionAsync(CancellationToken cancellationToken)
    {
        var stamped = await _connectionStore.GetAsync<RemoteStorageSettings>(cancellationToken)
            ?? throw new ServiceNotConfiguredException("Remote storage");

        var cached = _cached;
        if (cached is not null && cached.Version == stamped.Version)
            return (cached.Prefix, cached.Settings);

        await _cacheLock.WaitAsync(cancellationToken);
        try
        {
            cached = _cached;
            if (cached is null || cached.Version != stamped.Version)
            {
                cached = new CachedConnection(BuildRemotePrefix(stamped.Settings), stamped.Settings, stamped.Version);
                _cached = cached;
                _logger.LogDebug("Rclone remote prefix: {RemotePrefix}", RedactSensitiveArgs(cached.Prefix));
            }

            return (cached.Prefix, cached.Settings);
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    public void Dispose() => _cacheLock.Dispose();

    private static string BuildRemotePrefix(RemoteStorageSettings settings)
    {
        var parts = new List<string> { ":sftp" };

        if (!string.IsNullOrEmpty(settings.Host))
            parts.Add($"host={settings.Host}");

        if (settings.Port != 22)
            parts.Add($"port={settings.Port}");

        if (!string.IsNullOrEmpty(settings.User))
            parts.Add($"user={settings.User}");

        if (!string.IsNullOrEmpty(settings.Password))
            parts.Add($"pass={ObscurePassword(settings.Password)}");

        if (!string.IsNullOrEmpty(settings.KeyFile))
            parts.Add($"key_file={settings.KeyFile}");

        if (!string.IsNullOrEmpty(settings.KeyFilePassphrase))
            parts.Add($"key_file_pass={ObscurePassword(settings.KeyFilePassphrase)}");

        if (settings.UseAgent)
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
        IProgress<RcloneProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var (remotePrefix, settings) = await GetConnectionAsync(cancellationToken);
        var remoteSource = $"{remotePrefix}{sourcePath}";

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
            $"--transfers={settings.Transfers}",
            $"--checkers={settings.Checkers}"
        };

        args.AddRange(settings.AdditionalFlags);

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
                    // IProgress.Report is safe to call from this background stderr thread.
                    if (progress is not null && TryParseProgress(e.Data, out var sample))
                        progress.Report(sample);
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
        var stamped = await _connectionStore.GetAsync<RemoteStorageSettings>(cancellationToken);
        if (stamped is null)
            return new RemoteStorageConnectionResult(false, "Remote storage is not configured");

        return await TestConnectionAsync(stamped.Settings, cancellationToken);
    }

    public async Task<RemoteStorageConnectionResult> TestConnectionAsync(
        RemoteStorageSettings settings,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Testing rclone SFTP connection to {Host}", settings.Host);

        try
        {
            var remotePrefix = BuildRemotePrefix(settings);

            // Test by listing the root of the remote
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "rclone",
                    Arguments = $"lsf \"{remotePrefix}/\" --max-depth=1",
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
        var (remotePrefix, _) = await GetConnectionAsync(cancellationToken);
        var remotePath = $"{remotePrefix}{path}";

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

    /// <summary>
    /// Parses a full rclone one-line stats sample, e.g.
    /// "Transferred: 512 MiB / 1 GiB, 50%, 25 MiB/s, ETA 20s".
    /// </summary>
    internal static bool TryParseProgress(string line, out RcloneProgress progress)
    {
        progress = default!;
        var match = ProgressRegex().Match(line);
        if (!match.Success)
            return false;

        var transferred = ToBytes(double.Parse(match.Groups[1].Value), match.Groups[2].Value);
        var total = ToBytes(double.Parse(match.Groups[3].Value), match.Groups[4].Value);
        var percent = double.Parse(match.Groups[5].Value);
        var speed = ToBytes(double.Parse(match.Groups[6].Value), match.Groups[7].Value);
        var eta = match.Groups[8].Success ? ParseEta(match.Groups[8].Value) : null;

        progress = new RcloneProgress(transferred, total, percent, speed, eta);
        return true;
    }

    private static long ToBytes(double value, string unit) => unit.ToUpperInvariant() switch
    {
        "B" or "BYTES" => (long)value,
        "KIB" or "KB" => (long)(value * 1024),
        "MIB" or "MB" => (long)(value * 1024 * 1024),
        "GIB" or "GB" => (long)(value * 1024 * 1024 * 1024),
        "TIB" or "TB" => (long)(value * 1024L * 1024 * 1024 * 1024),
        _ => (long)value
    };

    private static int? ParseEta(string eta)
    {
        if (string.IsNullOrEmpty(eta) || eta == "-")
            return null;

        var match = EtaRegex().Match(eta);
        if (!match.Success || !match.Groups.Cast<Group>().Skip(1).Any(g => g.Success))
            return null;

        int Part(int group) => match.Groups[group].Success ? int.Parse(match.Groups[group].Value) : 0;
        return Part(1) * 86400 + Part(2) * 3600 + Part(3) * 60 + Part(4);
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

    [GeneratedRegex(@"Transferred:\s+([\d.]+)\s*(\w+)\s*/\s*([\d.]+)\s*(\w+),\s*(\d+)%,\s*([\d.]+)\s*(\w+)/s(?:,\s*ETA\s+(\S+))?")]
    private static partial Regex ProgressRegex();

    [GeneratedRegex(@"^(?:(\d+)d)?(?:(\d+)h)?(?:(\d+)m)?(?:(\d+)s)?$")]
    private static partial Regex EtaRegex();
}
