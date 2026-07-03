using Harrbor.Configuration;

namespace Harrbor.Data.Entities;

public class Job
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public ServiceType ServiceType { get; set; } = ServiceType.Sonarr;
    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromMinutes(1);
    public string RemotePath { get; set; } = string.Empty;
    public string StagingPath { get; set; } = string.Empty;
    public string QBittorrentCategory { get; set; } = string.Empty;
    public List<string> QBittorrentTags { get; set; } = [];
    public int TransferParallelism { get; set; } = 2;
    public int MaxTransferRetries { get; set; } = 3;
    public TimeSpan TransferRetryDelay { get; set; } = TimeSpan.FromMinutes(5);
    public string? CompletedCategory { get; set; }
    public TimeSpan ImportTimeout { get; set; } = TimeSpan.FromHours(24);
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public DateTime? LastRunAtUtc { get; set; }

    public JobDefinition ToDefinition() => new()
    {
        Name = Name,
        Enabled = Enabled,
        ServiceType = ServiceType,
        PollingInterval = PollingInterval,
        RemotePath = RemotePath,
        StagingPath = StagingPath,
        QBittorrentCategory = QBittorrentCategory,
        QBittorrentTags = [.. QBittorrentTags],
        TransferParallelism = TransferParallelism,
        MaxTransferRetries = MaxTransferRetries,
        TransferRetryDelay = TransferRetryDelay,
        CompletedCategory = CompletedCategory,
        ImportTimeout = ImportTimeout
    };
}
