namespace Harrbor.Configuration.Connections;

public static class ConnectionTypes
{
    public const string QBittorrent = "qbittorrent";
    public const string Sonarr = "sonarr";
    public const string Radarr = "radarr";
    public const string RemoteStorage = "remoteStorage";

    public static readonly IReadOnlyList<string> All = [QBittorrent, Sonarr, Radarr, RemoteStorage];
}
