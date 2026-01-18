namespace Harrbor.Services.Transfer;

public interface ITransferService
{
    Task<TransferResult> TransferAsync(
        string sourcePath,
        string destinationPath,
        CancellationToken cancellationToken = default);
}

public record TransferResult(
    bool Success,
    long BytesTransferred,
    TimeSpan Duration,
    string? Error = null);
