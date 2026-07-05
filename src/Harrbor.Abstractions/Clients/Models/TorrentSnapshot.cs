namespace Harrbor.Abstractions.Clients.Models;

/// <summary>
/// Harrbor-owned snapshot of a torrent's state, projected from whatever torrent client
/// backs the download-client connection. Keeps vendor library types (e.g. QBittorrent.Client's
/// <c>TorrentInfo</c>) out of the domain so a different client can be slotted in.
/// </summary>
public sealed record TorrentSnapshot(
    string Hash,
    string Name,
    double Progress,
    string? ContentPath,
    string? SavePath,
    string? Category,
    IReadOnlyList<string> Tags);
