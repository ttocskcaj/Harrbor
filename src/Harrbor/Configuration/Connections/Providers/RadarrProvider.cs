using Harrbor.Abstractions.Clients;
using Harrbor.Abstractions.Connections;
using Harrbor.Abstractions.Providers;
using Harrbor.Services.Clients;

namespace Harrbor.Configuration.Connections.Providers;

public sealed class RadarrProvider : ConnectionProvider<RadarrSettings, IMediaService>
{
    public const string ProviderKey = "radarr";

    public override string Key => ProviderKey;
    public override ConnectionCategory Category => ConnectionCategory.MediaService;
    public override string DisplayName => "Radarr";

    protected override IMediaService CreateClient(RadarrSettings settings, ClientBuildContext context) =>
        new RadarrClient(
            context.HttpClientFactory.CreateClient(ProviderKey),
            new MediaServiceSettings(settings.BaseUrl, settings.ApiKey),
            context.LoggerFactory.CreateLogger<RadarrClient>());

    protected override async Task<ProbeResult> ProbeAsync(IMediaService client, CancellationToken cancellationToken) =>
        await client.IsHealthyAsync(cancellationToken)
            ? new ProbeResult(true, null)
            : new ProbeResult(false, "Radarr health check failed");

    protected override Task<ConnectionTestResult> TestAsync(IMediaService client, CancellationToken cancellationToken) =>
        client.TestConnectionAsync(cancellationToken);
}
