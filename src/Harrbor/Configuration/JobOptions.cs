namespace Harrbor.Configuration;

public class JobOptions
{
    public const string SectionName = "Jobs";

    public List<JobDefinition> Definitions { get; set; } = [];
}

public class JobDefinition
{
    public string Name { get; set; } = string.Empty;
    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromMinutes(1);
    public ServiceType ServiceType { get; set; } = ServiceType.Sonarr;
    public string RemotePath { get; set; } = string.Empty;
    public string StagingPath { get; set; } = string.Empty;
    public string QBittorrentCategory { get; set; } = string.Empty;
    public List<string> QBittorrentTags { get; set; } = [];
    public int TransferParallelism { get; set; } = 2;
    public int MaxTransferRetries { get; set; } = 3;
    public TimeSpan TransferRetryDelay { get; set; } = TimeSpan.FromMinutes(5);
    public string? CompletedCategory { get; set; }
    public TimeSpan ImportTimeout { get; set; } = TimeSpan.FromHours(24);
    public bool Enabled { get; set; } = true;
}

public enum ServiceType
{
    Sonarr,
    Radarr
}
