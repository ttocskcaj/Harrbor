using Harrbor.Abstractions.Providers;
using Harrbor.Configuration.Connections.Providers;

namespace Harrbor.Services;

/// <summary>
/// Registers the built-in connection providers and the provider registry. A future plugin loader
/// would add more <see cref="IConnectionProvider"/> singletons here (e.g. from scanned DLLs) before
/// the registry is built — nothing else changes.
/// </summary>
public static class ConnectionProviderRegistration
{
    public static IServiceCollection AddConnectionProviders(this IServiceCollection services)
    {
        services.AddSingleton<IConnectionProvider, QBittorrentProvider>();
        services.AddSingleton<IConnectionProvider, SonarrProvider>();
        services.AddSingleton<IConnectionProvider, RadarrProvider>();
        services.AddSingleton<IConnectionProvider, RcloneSftpProvider>();
        services.AddSingleton<IConnectionProviderRegistry, ConnectionProviderRegistry>();

        // Named, resilient HttpClients for the HTTP-backed (media-service) providers — preserves the
        // pooling + standard resilience the catalog builder used to attach to each typed client.
        services.AddHttpClient(SonarrProvider.ProviderKey).AddStandardResilienceHandler();
        services.AddHttpClient(RadarrProvider.ProviderKey).AddStandardResilienceHandler();

        return services;
    }
}
