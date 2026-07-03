namespace Harrbor.Configuration.Connections;

public interface IConnectionSettings
{
    static abstract string ConnectionType { get; }
}
