namespace Harrbor.Api.Jobs;

public class JobRequest
{
    public string Name { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public int MediaServiceConnectionId { get; set; }
    public int DownloadClientConnectionId { get; set; }
    public int RemoteStorageConnectionId { get; set; }
    public int PollingIntervalSeconds { get; set; } = 60;
    public string RemotePath { get; set; } = string.Empty;
    public string StagingPath { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = [];
    public int TransferParallelism { get; set; } = 2;
    public int MaxTransferRetries { get; set; } = 3;
    public int TransferRetryDelaySeconds { get; set; } = 300;
    public string? CompletedCategory { get; set; }
    public int ImportTimeoutSeconds { get; set; } = 86400;
}

public class JobResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public int MediaServiceConnectionId { get; set; }
    public int DownloadClientConnectionId { get; set; }
    public int RemoteStorageConnectionId { get; set; }
    public int PollingIntervalSeconds { get; set; }
    public string RemotePath { get; set; } = string.Empty;
    public string StagingPath { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = [];
    public int TransferParallelism { get; set; }
    public int MaxTransferRetries { get; set; }
    public int TransferRetryDelaySeconds { get; set; }
    public string? CompletedCategory { get; set; }
    public int ImportTimeoutSeconds { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public DateTime? LastRunAtUtc { get; set; }
    public DateTime? NextRunAtUtc { get; set; }
    public int ActiveReleaseCount { get; set; }
}

public class JobDeleteConflictResponse
{
    public int ActiveReleaseCount { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class CloneJobRequest
{
    /// <summary>Name for the clone; if omitted, a unique "{source}-copy" name is generated.</summary>
    public string? Name { get; set; }
}
