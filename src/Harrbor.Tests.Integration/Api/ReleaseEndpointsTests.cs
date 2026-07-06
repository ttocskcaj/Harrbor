using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Harrbor.Api.Releases;
using Harrbor.Data.Entities;
using Harrbor.Tests.Common;
using Harrbor.Tests.Integration.Helpers;
using Xunit;

namespace Harrbor.Tests.Integration.Api;

public class ReleaseEndpointsTests : IDisposable
{
    private readonly HarrborWebApplicationFactory _factory = new();
    private readonly HttpClient _client;
    private readonly HarrborWebApplicationFactory.SeededConnections _connections;

    public ReleaseEndpointsTests()
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
    public async Task GetReleases_ReturnsDerivedPhaseAndStatus()
    {
        await SeedAsync(Release("A1", b => b
            .WithName("Transferring.Show")
            .WithDownloadStatus(DownloadStatus.Completed)
            .WithTransferStatus(TransferStatus.InProgress)
            .WithTransferStartedAtUtc(DateTime.UtcNow)));

        var page = await _client.GetFromJsonAsync<PagedResponse<ReleaseListItem>>("/api/releases", TestJson.Options);

        page!.Total.Should().Be(1);
        var item = page.Items.Single();
        item.CurrentPhase.Should().Be(ReleasePhase.Transfer);
        item.Status.Should().Be(ReleaseStatus.Flowing);
        item.MaxRetries.Should().Be(3);
    }

    [Fact]
    public async Task GetReleases_StateHistory_ReturnsOnlyTerminal()
    {
        await SeedAsync(
            Release("ACTIVE", b => b.WithDownloadStatus(DownloadStatus.Completed).WithTransferStatus(TransferStatus.InProgress)),
            Release("DONE", b => b
                .WithDownloadStatus(DownloadStatus.Completed).WithTransferStatus(TransferStatus.Completed)
                .WithImportStatus(ImportStatus.Completed).WithCleanupStatus(CleanupStatus.Completed)
                .WithArchivalStatus(ArchivalStatus.Completed).WithOutcome(PipelineOutcome.Completed)));

        var history = await _client.GetFromJsonAsync<PagedResponse<ReleaseListItem>>("/api/releases?state=history", TestJson.Options);
        var active = await _client.GetFromJsonAsync<PagedResponse<ReleaseListItem>>("/api/releases?state=active", TestJson.Options);

        history!.Items.Should().ContainSingle(i => i.DownloadId == "DONE");
        history.Items.Should().OnlyContain(i => i.Status == ReleaseStatus.Done);
        active!.Items.Should().ContainSingle(i => i.DownloadId == "ACTIVE");
    }

    [Fact]
    public async Task GetReleases_FilterByPhase_UsesEnumPredicates()
    {
        await SeedAsync(
            Release("DL", b => b.WithDownloadStatus(DownloadStatus.Pending)),
            Release("IMP", b => b
                .WithDownloadStatus(DownloadStatus.Completed).WithTransferStatus(TransferStatus.Completed)
                .WithExtractionStatus(ExtractionStatus.Completed)
                .WithImportStatus(ImportStatus.Pending)));

        var imports = await _client.GetFromJsonAsync<PagedResponse<ReleaseListItem>>("/api/releases?phase=import", TestJson.Options);

        imports!.Items.Should().ContainSingle(i => i.DownloadId == "IMP");
    }

    [Fact]
    public async Task GetReleases_Paginates()
    {
        var releases = Enumerable.Range(1, 5)
            .Select(i => Release($"R{i}", b => b.WithDownloadStatus(DownloadStatus.Pending)))
            .ToArray();
        await SeedAsync(releases);

        var page = await _client.GetFromJsonAsync<PagedResponse<ReleaseListItem>>("/api/releases?page=1&pageSize=2", TestJson.Options);

        page!.Total.Should().Be(5);
        page.PageSize.Should().Be(2);
        page.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetRelease_ReturnsDetailWithTimeline()
    {
        await SeedAsync(Release("DETAIL1", b => b
            .WithName("Detail.Show")
            .WithDownloadStatus(DownloadStatus.Completed)
            .WithDownloadCompletedAtUtc(DateTime.UtcNow.AddMinutes(-20))
            .WithTransferStatus(TransferStatus.InProgress)
            .WithTransferStartedAtUtc(DateTime.UtcNow.AddMinutes(-3))));

        int id;
        await using (var db = _factory.CreateDbContext())
            id = db.TrackedReleases.Single(r => r.DownloadId == "DETAIL1").Id;

        var detail = await _client.GetFromJsonAsync<ReleaseDetail>($"/api/releases/{id}", TestJson.Options);

        detail!.CurrentPhase.Should().Be(ReleasePhase.Transfer);
        detail.RemotePath.Should().NotBeNullOrEmpty();
        detail.Timeline.Should().HaveCount(7);
        detail.Timeline.Single(e => e.Phase == ReleasePhase.Transfer).State.Should().Be(PhaseTimelineState.Current);
        detail.Transfer.Should().NotBeNull();
        detail.Transfer!.MaxAttempts.Should().Be(3);
    }

    [Fact]
    public async Task GetRelease_NotFound_Returns404()
    {
        var response = await _client.GetAsync("/api/releases/9999");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
