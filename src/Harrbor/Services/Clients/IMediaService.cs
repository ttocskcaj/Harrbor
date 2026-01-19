using Harrbor.Services.Clients.Models;

namespace Harrbor.Services.Clients;

/// <summary>
/// Common interface for media management services (Sonarr, Radarr).
/// </summary>
public interface IMediaService
{
    /// <summary>
    /// Gets the current download queue from the media service.
    /// </summary>
    Task<IReadOnlyList<QueueItem>> GetQueueAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a download with the specified ID is still in the queue.
    /// </summary>
    Task<bool> IsDownloadInQueueAsync(string downloadId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a download has been imported by looking at history events.
    /// </summary>
    Task<bool> HasImportedAsync(string downloadId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Triggers a manual import scan for the specified path.
    /// </summary>
    Task TriggerManualImportAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if the service is healthy and reachable.
    /// </summary>
    Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default);
}
