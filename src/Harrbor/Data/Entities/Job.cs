using Harrbor.Configuration;

namespace Harrbor.Data.Entities;

public class Job
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;

    /// <summary>The media-service connection (Sonarr/Radarr) this job uses.</summary>
    public int MediaServiceConnectionId { get; set; }

    /// <summary>The download-client connection (qBittorrent) this job uses.</summary>
    public int DownloadClientConnectionId { get; set; }

    /// <summary>The remote-storage connection (rclone/SFTP) this job transfers from.</summary>
    public int RemoteStorageConnectionId { get; set; }

    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromMinutes(1);
    public string RemotePath { get; set; } = string.Empty;
    public string StagingPath { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = [];
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
        MediaServiceConnectionId = MediaServiceConnectionId,
        DownloadClientConnectionId = DownloadClientConnectionId,
        RemoteStorageConnectionId = RemoteStorageConnectionId,
        PollingInterval = PollingInterval,
        RemotePath = RemotePath,
        StagingPath = StagingPath,
        Category = Category,
        Tags = [.. Tags],
        TransferParallelism = TransferParallelism,
        MaxTransferRetries = MaxTransferRetries,
        TransferRetryDelay = TransferRetryDelay,
        CompletedCategory = CompletedCategory,
        ImportTimeout = ImportTimeout
    };
}
