using Harrbor.Abstractions.Clients;
using Harrbor.Abstractions.Connections;
using Harrbor.Abstractions.Providers;
using Harrbor.Abstractions.RemoteStorage;
using Harrbor.Services.RemoteStorage;

namespace Harrbor.Configuration.Connections.Providers;

public sealed class RcloneSftpProvider : ConnectionProvider<RemoteStorageSettings, IRemoteStorageService>
{
    public const string ProviderKey = "rclone-sftp";

    public override string Key => ProviderKey;
    public override ConnectionCategory Category => ConnectionCategory.RemoteStorage;
    public override string DisplayName => "Remote Storage (SFTP)";

    // rclone probes shell out to a subprocess — throttle relative to the base tick.
    public override int ProbeEveryNTicks => 4;

    protected override IRemoteStorageService CreateClient(RemoteStorageSettings settings, ClientBuildContext context) =>
        new RcloneRemoteStorageService(settings, context.LoggerFactory.CreateLogger<RcloneRemoteStorageService>());

    protected override async Task<ProbeResult> ProbeAsync(IRemoteStorageService client, CancellationToken cancellationToken)
    {
        var result = await client.TestConnectionAsync(cancellationToken);
        return new ProbeResult(result.Success, result.Error);
    }

    protected override async Task<ConnectionTestResult> TestAsync(IRemoteStorageService client, CancellationToken cancellationToken)
    {
        var result = await client.TestConnectionAsync(cancellationToken);
        return new ConnectionTestResult(result.Success, Error: result.Error);
    }
}
