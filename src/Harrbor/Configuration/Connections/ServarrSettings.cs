namespace Harrbor.Configuration.Connections;

/// <summary>
/// Shared shape for *arr media services (Sonarr, Radarr, ...). Concrete types add
/// their own <see cref="IConnectionSettings.ConnectionType"/>; the base deliberately
/// does not implement <see cref="IConnectionSettings"/> because a static abstract
/// member cannot be deferred to subclasses.
/// </summary>
public abstract class ServarrSettings
{
    public string BaseUrl { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
}
