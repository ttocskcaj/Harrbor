using System.Diagnostics;

namespace Harrbor.Services.Transfer;

public class RcloneTransferService : ITransferService
{
    private readonly ILogger<RcloneTransferService> _logger;

    public RcloneTransferService(ILogger<RcloneTransferService> logger)
    {
        _logger = logger;
    }

    public async Task<TransferResult> TransferAsync(
        string sourcePath,
        string destinationPath,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            _logger.LogInformation("Starting transfer from {Source} to {Destination}", sourcePath, destinationPath);

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "rclone",
                    Arguments = $"copy \"{sourcePath}\" \"{destinationPath}\" --progress --stats-one-line",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();

            var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

            await process.WaitForExitAsync(cancellationToken);

            var output = await outputTask;
            var error = await errorTask;

            stopwatch.Stop();

            if (process.ExitCode != 0)
            {
                _logger.LogError("rclone transfer failed with exit code {ExitCode}: {Error}",
                    process.ExitCode, error);

                return new TransferResult(
                    Success: false,
                    BytesTransferred: 0,
                    Duration: stopwatch.Elapsed,
                    Error: error);
            }

            _logger.LogInformation("Transfer completed successfully in {Duration}", stopwatch.Elapsed);

            return new TransferResult(
                Success: true,
                BytesTransferred: 0, // TODO: Parse from rclone output
                Duration: stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Transfer failed with exception");

            return new TransferResult(
                Success: false,
                BytesTransferred: 0,
                Duration: stopwatch.Elapsed,
                Error: ex.Message);
        }
    }
}
