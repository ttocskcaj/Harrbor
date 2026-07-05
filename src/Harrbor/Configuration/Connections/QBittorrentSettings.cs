using Harrbor.Abstractions.Providers;

namespace Harrbor.Configuration.Connections;

public class QBittorrentSettings
{
    [Field(Kind = FieldKind.Url, Required = true, Label = "Base URL",
        HelpText = "Root URL of the qBittorrent Web UI, including scheme and port (e.g. http://localhost:8080).",
        Order = 1)]
    public string BaseUrl { get; set; } = string.Empty;

    [Field(Label = "Username",
        HelpText = "Web UI username. Leave blank if you've whitelisted this host to bypass authentication.",
        Order = 2)]
    public string Username { get; set; } = string.Empty;

    [Field(Kind = FieldKind.Password, Secret = true, Label = "Password",
        HelpText = "Web UI password. Leave blank if authentication is bypassed for this host.",
        Order = 3)]
    public string Password { get; set; } = string.Empty;
}
