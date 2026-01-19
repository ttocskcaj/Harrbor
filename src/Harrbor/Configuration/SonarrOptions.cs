namespace Harrbor.Configuration;

public class SonarrOptions
{
    public const string SectionName = "Sonarr";

    public string BaseUrl { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
}
