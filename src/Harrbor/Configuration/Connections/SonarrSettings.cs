namespace Harrbor.Configuration.Connections;

public sealed class SonarrSettings : ServarrSettings, IConnectionSettings
{
    public static string ConnectionType => ConnectionTypes.Sonarr;
}
