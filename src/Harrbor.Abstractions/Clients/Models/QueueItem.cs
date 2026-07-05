namespace Harrbor.Abstractions.Clients.Models;

/// <summary>
/// Simplified queue item for internal use — the vendor-neutral projection surfaced on
/// <see cref="IMediaService"/>.
/// </summary>
public record QueueItem(
    string DownloadId,
    string Title,
    string Status,
    string? TrackedDownloadState,
    string? OutputPath);
