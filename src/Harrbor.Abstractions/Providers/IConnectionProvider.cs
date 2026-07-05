using Harrbor.Abstractions.Clients;
using Harrbor.Abstractions.Connections;
using Harrbor.Abstractions.Validation;
using Microsoft.Extensions.Logging;

namespace Harrbor.Abstractions.Providers;

/// <summary>Outcome of a light-weight health probe against a live connection client.</summary>
public readonly record struct ProbeResult(bool Ok, string? Error);

/// <summary>
/// Ambient services a provider may use to build a client. Passed to
/// <see cref="IConnectionProvider.CreateClient"/> instead of constructor DI so providers stay
/// container-agnostic (and DLL-shippable). Grow this deliberately as providers need more.
/// </summary>
public sealed class ClientBuildContext(IHttpClientFactory httpClientFactory, ILoggerFactory loggerFactory)
{
    public IHttpClientFactory HttpClientFactory { get; } = httpClientFactory;
    public ILoggerFactory LoggerFactory { get; } = loggerFactory;
}

/// <summary>
/// A code-defined connection kind (sonarr, radarr, qbittorrent, rclone-sftp, ...). Declares a
/// settings schema, builds category clients from settings, and probes/tests them. The registry,
/// connection manager, health monitor, and generic API all consume this non-generic surface;
/// authors implement it via <see cref="ConnectionProvider{TSettings, TClient}"/>.
/// </summary>
public interface IConnectionProvider
{
    /// <summary>Stable identity, e.g. "sonarr". Persisted on each connection row.</summary>
    string Key { get; }

    ConnectionCategory Category { get; }

    string DisplayName { get; }

    /// <summary>The provider's strongly-typed settings type.</summary>
    Type SettingsType { get; }

    /// <summary>The reflected field schema driving validation, secret handling, and form rendering.</summary>
    IReadOnlyList<FieldDescriptor> Schema { get; }

    /// <summary>How often (in health-monitor ticks) to probe; &gt;1 throttles expensive probes.</summary>
    int ProbeEveryNTicks { get; }

    /// <summary>Builds a live category client (<see cref="IMediaService"/> / <see cref="ITorrentClient"/> / IRemoteStorageService) from settings.</summary>
    object CreateClient(object settings, ClientBuildContext context);

    /// <summary>Bespoke validation beyond the declarative schema rules (runs after them).</summary>
    void Validate(object settings, ValidationErrors errors);

    /// <summary>Light health probe used by the background monitor.</summary>
    Task<ProbeResult> ProbeAsync(object client, CancellationToken cancellationToken);

    /// <summary>Heavier connect test used by the "test connection" endpoints.</summary>
    Task<ConnectionTestResult> TestAsync(object client, CancellationToken cancellationToken);
}
