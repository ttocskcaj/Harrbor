using Harrbor.Abstractions.Clients;
using Harrbor.Abstractions.Connections;
using Harrbor.Abstractions.Providers;
using Harrbor.Services.Clients;

namespace Harrbor.Configuration.Connections.Providers;

public sealed class QBittorrentProvider : ConnectionProvider<QBittorrentSettings, ITorrentClient>
{
    public const string ProviderKey = "qbittorrent";

    public override string Key => ProviderKey;
    public override ConnectionCategory Category => ConnectionCategory.DownloadClient;
    public override string DisplayName => "qBittorrent";

    protected override ITorrentClient CreateClient(QBittorrentSettings settings, ClientBuildContext context) =>
        new QBittorrentTorrentClient(settings, context.LoggerFactory.CreateLogger<QBittorrentTorrentClient>());

    protected override async Task<ProbeResult> ProbeAsync(ITorrentClient client, CancellationToken cancellationToken) =>
        await client.IsConnectedAsync(cancellationToken)
            ? new ProbeResult(true, null)
            : new ProbeResult(false, "Could not connect to qBittorrent");

    protected override Task<ConnectionTestResult> TestAsync(ITorrentClient client, CancellationToken cancellationToken) =>
        client.TestConnectionAsync(cancellationToken);
}
