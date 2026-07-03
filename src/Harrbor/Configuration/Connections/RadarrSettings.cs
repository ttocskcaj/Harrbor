namespace Harrbor.Configuration.Connections;

public sealed class RadarrSettings : ServarrSettings, IConnectionSettings
{
    public static string ConnectionType => ConnectionTypes.Radarr;
}
