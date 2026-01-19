using Microsoft.Extensions.Options;
using Harrbor.Configuration;

namespace Harrbor.Services.Clients;

public class SonarrClient : MediaServiceClientBase, ISonarrClient
{
    public SonarrClient(
        HttpClient httpClient,
        IOptions<SonarrOptions> options,
        ILogger<SonarrClient> logger)
        : base(httpClient, options.Value.BaseUrl, options.Value.ApiKey, logger, "Sonarr")
    {
    }

    protected override string GetQueueQueryParameters()
        => "includeEpisode=false&includeUnknownSeriesItems=true";
}
