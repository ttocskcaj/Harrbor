using Harrbor.Configuration.Connections;
using Harrbor.Services.Configuration;

namespace Harrbor.Services.Clients;

public class RadarrClient : MediaServiceClientBase, IRadarrClient
{
    private readonly IConnectionStore _connectionStore;

    public RadarrClient(
        HttpClient httpClient,
        IConnectionStore connectionStore,
        ILogger<RadarrClient> logger)
        : base(httpClient, logger, "Radarr")
    {
        _connectionStore = connectionStore;
    }

    protected override async Task<MediaServiceSettings?> GetSettingsAsync(CancellationToken cancellationToken)
    {
        var stamped = await _connectionStore.GetAsync<RadarrSettings>(cancellationToken);
        return stamped is null ? null : new MediaServiceSettings(stamped.Settings.BaseUrl, stamped.Settings.ApiKey);
    }

    protected override string GetQueueQueryParameters()
        => "includeMovie=false&includeUnknownMovieItems=true";
}
