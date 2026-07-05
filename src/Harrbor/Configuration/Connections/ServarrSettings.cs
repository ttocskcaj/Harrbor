using Harrbor.Abstractions.Providers;

namespace Harrbor.Configuration.Connections;

/// <summary>
/// Shared shape for *arr media services (Sonarr, Radarr, ...). Concrete types are empty subclasses;
/// the provider (not the settings type) owns the connection key.
/// </summary>
public abstract class ServarrSettings
{
    [Field(Kind = FieldKind.Url, Required = true, Label = "Base URL",
        HelpText = "Root URL where the Sonarr/Radarr web interface is reachable, including scheme and port (e.g. http://localhost:8989).",
        Order = 1)]
    public string BaseUrl { get; set; } = string.Empty;

    [Field(Kind = FieldKind.Password, Secret = true, Required = true, Label = "API Key",
        HelpText = "Found under Settings → General in the web interface. Authenticates every request Harrbor makes.",
        Order = 2)]
    public string ApiKey { get; set; } = string.Empty;
}
