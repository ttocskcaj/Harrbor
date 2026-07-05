namespace Harrbor.Services.Clients;

public class SonarrClient : MediaServiceClientBase
{
    public SonarrClient(
        HttpClient httpClient,
        MediaServiceSettings settings,
        ILogger<SonarrClient> logger)
        : base(httpClient, settings, logger, "Sonarr")
    {
    }

    protected override string GetQueueQueryParameters()
        => "includeEpisode=false&includeUnknownSeriesItems=true";
}
