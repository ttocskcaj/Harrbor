namespace Harrbor.Configuration.Connections;

public class QBittorrentSettings : IConnectionSettings
{
    public static string ConnectionType => ConnectionTypes.QBittorrent;

    public string BaseUrl { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
