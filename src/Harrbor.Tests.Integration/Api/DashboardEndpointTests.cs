using System.Net.Http.Json;
using FluentAssertions;
using Harrbor.Api.Dashboard;
using Harrbor.Api.Releases;
using Harrbor.Data.Entities;
using Harrbor.Tests.Common;
using Harrbor.Tests.Integration.Helpers;
using Xunit;

namespace Harrbor.Tests.Integration.Api;

public class DashboardEndpointTests : IDisposable
{
    private readonly HarrborWebApplicationFactory _factory = new();
    private readonly HttpClient _client;
    private readonly HarrborWebApplicationFactory.SeededConnections _connections;

    public DashboardEndpointTests()
    {
        _client = _factory.CreateClient();
        _connections = _factory.SeedDefaultConnectionsAsync().GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    private async Task SeedAsync(params TrackedRelease[] releases)
    {
        await using var db = _factory.CreateDbContext();
        db.Jobs.Add(new Job
        {
            Name = "tv-shows",
            Enabled = true,
            MediaServiceConnectionId = _connections.Sonarr,
            DownloadClientConnectionId = _connections.QBittorrent,
            RemoteStorageConnectionId = _connections.RemoteStorage,
            RemotePath = "/r",
            StagingPath = "/s",
            Category = "tv",
            MaxTransferRetries = 3,
            TransferRetryDelay = TimeSpan.FromMinutes(5),
            ImportTimeout = TimeSpan.FromHours(24)
        });
        db.TrackedReleases.AddRange(releases);
        await db.SaveChangesAsync();
    }

    private static TrackedRelease Release(string downloadId, Action<TrackedReleaseBuilder> configure)
    {
        var builder = new TrackedReleaseBuilder().WithDownloadId(downloadId).WithJobName("tv-shows");
        configure(builder);
        return builder.Build();
    }

    [Fact]
    public async Task GetDashboard_Empty_ReturnsZeroedButWellFormed()
    {
        var dashboard = await _client.GetFromJsonAsync<DashboardResponse>("/api/dashboard", TestJson.Options);

        dashboard.Should().NotBeNull();
        dashboard!.Attention.Items.Should().BeEmpty();
        dashboard.Attention.TotalCount.Should().Be(0);
        dashboard.Connections.Should().HaveCount(4);
        dashboard.Jobs.Should().BeEmpty();
    }

    [Fact]
    public async Task GetDashboard_BucketsActiveReleasesByPhase()
    {
        await SeedAsync(
            Release("DL", b => b.WithDownloadStatus(DownloadStatus.Pending)),
            Release("TR", b => b
                .WithDownloadStatus(DownloadStatus.Completed).WithTransferStatus(TransferStatus.InProgress)
                .WithTransferStartedAtUtc(DateTime.UtcNow)),
            Release("EX", b => b
                .WithDownloadStatus(DownloadStatus.Completed).WithTransferStatus(TransferStatus.Completed)
                .WithExtractionStatus(ExtractionStatus.Pending).WithTransferCompletedAtUtc(DateTime.UtcNow)),
            Release("IM", b => b
                .WithDownloadStatus(DownloadStatus.Completed).WithTransferStatus(TransferStatus.Completed)
                .WithExtractionStatus(ExtractionStatus.Completed)
                .WithImportStatus(ImportStatus.Pending).WithTransferCompletedAtUtc(DateTime.UtcNow)));

        var dashboard = await _client.GetFromJsonAsync<DashboardResponse>("/api/dashboard", TestJson.Options);

        dashboard!.Pipeline.Download.Should().Be(1);
        dashboard.Pipeline.Transfer.Should().Be(1);
        dashboard.Pipeline.Extraction.Should().Be(1);
        dashboard.Pipeline.Import.Should().Be(1);
        dashboard.Throughput.TransfersInProgress.Should().Be(1);
        dashboard.Jobs.Single().ActiveReleaseCount.Should().Be(4);
    }

    [Fact]
    public async Task GetDashboard_SurfacesFailedAndRetryingInAttention()
    {
        await SeedAsync(
            Release("FAILED", b => b.WithName("Failed.Show")
                .WithDownloadStatus(DownloadStatus.Completed).WithTransferStatus(TransferStatus.Failed)
                .WithErrorCount(3).WithLastError("gave up").WithLastErrorAtUtc(DateTime.UtcNow.AddMinutes(-1))
                .WithOutcome(PipelineOutcome.Failed)),
            Release("RETRY", b => b.WithName("Retry.Show")
                .WithDownloadStatus(DownloadStatus.Completed).WithTransferStatus(TransferStatus.Failed)
                .WithErrorCount(1).WithLastError("temporary").WithLastErrorAtUtc(DateTime.UtcNow.AddMinutes(-2))));

        var dashboard = await _client.GetFromJsonAsync<DashboardResponse>("/api/dashboard", TestJson.Options);

        dashboard!.Attention.TotalCount.Should().Be(2);
        dashboard.Attention.Items.Should().Contain(i => i.Status == ReleaseStatus.Failed && i.Name == "Failed.Show");
        var retry = dashboard.Attention.Items.Single(i => i.Status == ReleaseStatus.Retrying);
        retry.Name.Should().Be("Retry.Show");
        retry.NextRetryAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task GetDashboard_CountsRecentCompletions()
    {
        await SeedAsync(
            Release("DONE", b => b
                .WithDownloadStatus(DownloadStatus.Completed).WithTransferStatus(TransferStatus.Completed)
                .WithImportStatus(ImportStatus.Completed).WithCleanupStatus(CleanupStatus.Completed)
                .WithArchivalStatus(ArchivalStatus.Completed).WithOutcome(PipelineOutcome.Completed)
                .WithArchivedAtUtc(DateTime.UtcNow.AddHours(-2))));

        var dashboard = await _client.GetFromJsonAsync<DashboardResponse>("/api/dashboard", TestJson.Options);

        dashboard!.Throughput.CompletedLast24h.Should().Be(1);
        dashboard.Throughput.CompletedLast7d.Should().Be(1);
        dashboard.Throughput.RecentCompletions.Should().ContainSingle();
    }
}
