using Harrbor.Configuration.Connections;
using Harrbor.Services.Configuration;

namespace Harrbor.Services.Clients;

public class SonarrClient : MediaServiceClientBase, ISonarrClient
{
    private readonly IConnectionStore _connectionStore;

    public SonarrClient(
        HttpClient httpClient,
        IConnectionStore connectionStore,
        ILogger<SonarrClient> logger)
        : base(httpClient, logger, "Sonarr")
    {
        _connectionStore = connectionStore;
    }

    protected override async Task<MediaServiceSettings?> GetSettingsAsync(CancellationToken cancellationToken)
    {
        var stamped = await _connectionStore.GetAsync<SonarrSettings>(cancellationToken);
        return stamped is null ? null : new MediaServiceSettings(stamped.Settings.BaseUrl, stamped.Settings.ApiKey);
    }

    protected override string GetQueueQueryParameters()
        => "includeEpisode=false&includeUnknownSeriesItems=true";
}
