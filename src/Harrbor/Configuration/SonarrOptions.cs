namespace Harrbor.Configuration;

public class SonarrOptions
{
    public const string SectionName = "Sonarr";

    public string BaseUrl { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public string RemotePath { get; set; } = string.Empty;
    public string StagingPath { get; set; } = string.Empty;
    public string ArchivePath { get; set; } = string.Empty;
}
