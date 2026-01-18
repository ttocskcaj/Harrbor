namespace Harrbor.Configuration;

public class QBittorrentOptions
{
    public const string SectionName = "QBittorrent";

    public string BaseUrl { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
