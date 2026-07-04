using Harrbor.Api.Connections;
using Harrbor.Api.Releases;
using Harrbor.Configuration;

namespace Harrbor.Api.Dashboard;

public class DashboardResponse
{
    public PipelineCounts Pipeline { get; set; } = new();
    public AttentionSummary Attention { get; set; } = new();
    public ThroughputSummary Throughput { get; set; } = new();
    public List<JobSnapshot> Jobs { get; set; } = [];
    public List<ConnectionHealth> Connections { get; set; } = [];
}

/// <summary>Count of active releases currently resting in each phase.</summary>
public class PipelineCounts
{
    public int Discover { get; set; }
    public int Download { get; set; }
    public int Transfer { get; set; }
    public int Import { get; set; }
    public int Cleanup { get; set; }
    public int Archival { get; set; }
}

public class AttentionSummary
{
    public List<AttentionItem> Items { get; set; } = [];

    /// <summary>Total number of items needing attention (may exceed the capped <see cref="Items"/>).</summary>
    public int TotalCount { get; set; }
}

public class AttentionItem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string JobName { get; set; } = string.Empty;
    public ReleasePhase CurrentPhase { get; set; }
    public ReleaseStatus Status { get; set; }
    public string? LastError { get; set; }
    public DateTime? LastErrorAtUtc { get; set; }
    public DateTime? NextRetryAtUtc { get; set; }
}

public class ThroughputSummary
{
    public int CompletedLast24h { get; set; }
    public int CompletedLast7d { get; set; }
    public int TransfersInProgress { get; set; }

    /// <summary>Aggregate transfer speed across in-progress transfers (from the progress tracker).</summary>
    public long AggregateSpeedBytesPerSec { get; set; }
    public List<RecentCompletion> RecentCompletions { get; set; } = [];
}

public class RecentCompletion
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string JobName { get; set; } = string.Empty;
    public DateTime? ArchivedAtUtc { get; set; }
}

public class JobSnapshot
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public ServiceType ServiceType { get; set; }
    public int ActiveReleaseCount { get; set; }
    public DateTime? LastRunAtUtc { get; set; }
    public DateTime? NextRunAtUtc { get; set; }
}
