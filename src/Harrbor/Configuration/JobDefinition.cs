namespace Harrbor.Configuration;

/// <summary>
/// Immutable snapshot of a job's settings, passed through one pipeline cycle.
/// Mapped from the Job entity at cycle start so mid-cycle edits apply next cycle.
/// </summary>
public class JobDefinition
{
    public string Name { get; set; } = string.Empty;
    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>The media-service connection (e.g. Sonarr/Radarr) this job uses.</summary>
    public int MediaServiceConnectionId { get; set; }

    /// <summary>The download-client connection (e.g. qBittorrent) this job uses.</summary>
    public int DownloadClientConnectionId { get; set; }

    /// <summary>The remote-storage connection (e.g. rclone/SFTP) this job transfers from.</summary>
    public int RemoteStorageConnectionId { get; set; }

    public string RemotePath { get; set; } = string.Empty;
    
    public string StagingPath { get; set; } = string.Empty;

    /// <summary>Download-client category/label applied to this job's torrents.</summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>Download-client tags applied to this job's torrents.</summary>
    public List<string> Tags { get; set; } = [];

    public int TransferParallelism { get; set; } = 2;
    public int MaxTransferRetries { get; set; } = 3;
    public TimeSpan TransferRetryDelay { get; set; } = TimeSpan.FromMinutes(5);
    public string? CompletedCategory { get; set; }

    /// <summary>Unpack zip/rar/7z archives in staging after transfer, before import.</summary>
    public bool ExtractionEnabled { get; set; } = true;

    public TimeSpan ImportTimeout { get; set; } = TimeSpan.FromHours(24);
    public bool Enabled { get; set; } = true;
}
