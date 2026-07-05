namespace Harrbor.Abstractions.Connections;

/// <summary>
/// The role a connection plays in the orchestration pipeline. Each category has one client
/// abstraction and (usually) a shared settings shape; concrete <em>types</em> (sonarr, radarr,
/// qbittorrent, ...) live under a category.
/// </summary>
public enum ConnectionCategory
{
    /// <summary>Sonarr/Radarr-style media managers. Used by the Discover and Import phases.</summary>
    MediaService,

    /// <summary>Torrent download clients (qBittorrent today). Used by the Download and Archival phases.</summary>
    DownloadClient,

    /// <summary>Remote file storage (rclone/SFTP today). Used by the Transfer phase.</summary>
    RemoteStorage
}
