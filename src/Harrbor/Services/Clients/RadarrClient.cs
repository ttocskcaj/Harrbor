using Microsoft.Extensions.Options;
using Harrbor.Configuration;

namespace Harrbor.Services.Clients;

public class RadarrClient : MediaServiceClientBase, IRadarrClient
{
    public RadarrClient(
        HttpClient httpClient,
        IOptions<RadarrOptions> options,
        ILogger<RadarrClient> logger)
        : base(httpClient, options.Value.BaseUrl, options.Value.ApiKey, logger, "Radarr")
    {
    }

    protected override string GetQueueQueryParameters()
        => "includeMovie=false&includeUnknownMovieItems=true";
}
