namespace Harrbor.Configuration;

public class RadarrOptions
{
    public const string SectionName = "Radarr";

    public string BaseUrl { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
}
