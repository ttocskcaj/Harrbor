namespace Harrbor.Services.Clients;

public class RadarrClient : MediaServiceClientBase
{
    public RadarrClient(
        HttpClient httpClient,
        MediaServiceSettings settings,
        ILogger<RadarrClient> logger)
        : base(httpClient, settings, logger, "Radarr")
    {
    }

    protected override string GetQueueQueryParameters()
        => "includeMovie=false&includeUnknownMovieItems=true";
}
