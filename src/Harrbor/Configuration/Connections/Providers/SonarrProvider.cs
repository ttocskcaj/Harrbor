using Harrbor.Abstractions.Clients;
using Harrbor.Abstractions.Connections;
using Harrbor.Abstractions.Providers;
using Harrbor.Services.Clients;

namespace Harrbor.Configuration.Connections.Providers;

public sealed class SonarrProvider : ConnectionProvider<SonarrSettings, IMediaService>
{
    public const string ProviderKey = "sonarr";

    public override string Key => ProviderKey;
    public override ConnectionCategory Category => ConnectionCategory.MediaService;
    public override string DisplayName => "Sonarr";

    protected override IMediaService CreateClient(SonarrSettings settings, ClientBuildContext context) =>
        new SonarrClient(
            context.HttpClientFactory.CreateClient(ProviderKey),
            new MediaServiceSettings(settings.BaseUrl, settings.ApiKey),
            context.LoggerFactory.CreateLogger<SonarrClient>());

    protected override async Task<ProbeResult> ProbeAsync(IMediaService client, CancellationToken cancellationToken) =>
        await client.IsHealthyAsync(cancellationToken)
            ? new ProbeResult(true, null)
            : new ProbeResult(false, "Sonarr health check failed");

    protected override Task<ConnectionTestResult> TestAsync(IMediaService client, CancellationToken cancellationToken) =>
        client.TestConnectionAsync(cancellationToken);
}
