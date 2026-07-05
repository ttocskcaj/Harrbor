using System.Net;
using System.Net.Http.Json;
using FakeItEasy;
using FluentAssertions;
using Harrbor.Api.Releases;
using Harrbor.Data.Entities;
using Harrbor.Tests.Common;
using Harrbor.Tests.Integration.Helpers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Harrbor.Tests.Integration.Api;

public class ReleaseActionEndpointsTests : IDisposable
{
    private readonly HarrborWebApplicationFactory _factory = new();
    private readonly HttpClient _client;
    private readonly HarrborWebApplicationFactory.SeededConnections _connections;

    public ReleaseActionEndpointsTests()
    {
        _client = _factory.CreateClient();
        _connections = _factory.SeedDefaultConnectionsAsync().GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    private async Task<int> SeedAsync(TrackedRelease release)
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
        db.TrackedReleases.Add(release);
        await db.SaveChangesAsync();
        return release.Id;
    }

    private async Task<TrackedRelease> ReloadAsync(int id)
    {
        await using var db = _factory.CreateDbContext();
        return await db.TrackedReleases.AsNoTracking().SingleAsync(r => r.Id == id);
    }

    [Fact]
    public async Task Retry_FailedTransfer_ResetsToPendingAndActive()
    {
        var id = await SeedAsync(new TrackedReleaseBuilder()
            .WithDownloadId("H1").WithJobName("tv-shows")
            .WithDownloadStatus(DownloadStatus.Completed)
            .WithTransferStatus(TransferStatus.Failed)
            .WithErrorCount(3).WithLastError("gave up").WithLastErrorAtUtc(DateTime.UtcNow)
            .WithOutcome(PipelineOutcome.Failed)
            .Build());

        var response = await _client.PostAsJsonAsync($"/api/releases/{id}/retry", new RetryReleaseRequest());
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var release = await ReloadAsync(id);
        release.TransferStatus.Should().Be(TransferStatus.Pending);
        release.ErrorCount.Should().Be(0);
        release.LastError.Should().BeNull();
        release.Outcome.Should().Be(PipelineOutcome.Active);
    }

    [Fact]
    public async Task Retry_TransferInProgress_Returns409()
    {
        var id = await SeedAsync(new TrackedReleaseBuilder()
            .WithDownloadId("H1").WithJobName("tv-shows")
            .WithDownloadStatus(DownloadStatus.Completed)
            .WithTransferStatus(TransferStatus.InProgress)
            .Build());

        var response = await _client.PostAsJsonAsync($"/api/releases/{id}/retry",
            new RetryReleaseRequest { Phase = ReleasePhase.Transfer });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Retry_NoFailedPhase_Returns400()
    {
        var id = await SeedAsync(new TrackedReleaseBuilder()
            .WithDownloadId("H1").WithJobName("tv-shows")
            .WithDownloadStatus(DownloadStatus.Completed)
            .WithTransferStatus(TransferStatus.Completed)
            .WithImportStatus(ImportStatus.Pending)
            .Build());

        var response = await _client.PostAsJsonAsync($"/api/releases/{id}/retry", new RetryReleaseRequest());

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Cancel_DeletesTorrentAndMarksCancelled()
    {
        var id = await SeedAsync(new TrackedReleaseBuilder()
            .WithDownloadId("HASHX").WithJobName("tv-shows")
            .WithDownloadStatus(DownloadStatus.Completed)
            .WithTransferStatus(TransferStatus.InProgress)
            .Build());

        var response = await _client.PostAsJsonAsync($"/api/releases/{id}/cancel",
            new CancelReleaseRequest { DeleteTorrent = true, DeleteFiles = true });
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        (await ReloadAsync(id)).Outcome.Should().Be(PipelineOutcome.Cancelled);
        A.CallTo(() => _factory.QBittorrentClient.DeleteTorrentAsync("HASHX", true, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Cancel_DeleteTorrentFalse_SkipsQBittorrent()
    {
        var id = await SeedAsync(new TrackedReleaseBuilder()
            .WithDownloadId("HASHX").WithJobName("tv-shows").Build());

        await _client.PostAsJsonAsync($"/api/releases/{id}/cancel",
            new CancelReleaseRequest { DeleteTorrent = false });

        (await ReloadAsync(id)).Outcome.Should().Be(PipelineOutcome.Cancelled);
        A.CallTo(() => _factory.QBittorrentClient.DeleteTorrentAsync(A<string>._, A<bool>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task Cancel_TorrentDeleteFails_StillCancelsWithWarning()
    {
        A.CallTo(() => _factory.QBittorrentClient.DeleteTorrentAsync(A<string>._, A<bool>._, A<CancellationToken>._))
            .Throws(new Exception("qBittorrent unreachable"));

        var id = await SeedAsync(new TrackedReleaseBuilder()
            .WithDownloadId("HASHX").WithJobName("tv-shows").Build());

        var response = await _client.PostAsJsonAsync($"/api/releases/{id}/cancel", new CancelReleaseRequest());
        var body = await response.Content.ReadFromJsonAsync<ReleaseActionResponse>(TestJson.Options);

        (await ReloadAsync(id)).Outcome.Should().Be(PipelineOutcome.Cancelled);
        body!.Warning.Should().Contain("qBittorrent");
    }

    [Fact]
    public async Task Reimport_TransferComplete_TriggersImportAndResetsStatus()
    {
        var id = await SeedAsync(new TrackedReleaseBuilder()
            .WithDownloadId("H1").WithJobName("tv-shows")
            .WithDownloadStatus(DownloadStatus.Completed)
            .WithTransferStatus(TransferStatus.Completed)
            .WithImportStatus(ImportStatus.Failed)
            .WithOutcome(PipelineOutcome.Failed)
            .WithRemotePath("/downloads/Show.S01E01").WithStagingPath("/staging")
            .Build());

        var response = await _client.PostAsync($"/api/releases/{id}/reimport", null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var release = await ReloadAsync(id);
        release.ImportStatus.Should().Be(ImportStatus.Pending);
        release.Outcome.Should().Be(PipelineOutcome.Active);
        A.CallTo(() => _factory.SonarrClient.TriggerManualImportAsync(
            "/staging/Show.S01E01", A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Reimport_TransferNotComplete_Returns409()
    {
        var id = await SeedAsync(new TrackedReleaseBuilder()
            .WithDownloadId("H1").WithJobName("tv-shows")
            .WithDownloadStatus(DownloadStatus.Completed)
            .WithTransferStatus(TransferStatus.Pending)
            .Build());

        var response = await _client.PostAsync($"/api/releases/{id}/reimport", null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Retry_MissingRelease_Returns404()
    {
        var response = await _client.PostAsJsonAsync("/api/releases/9999/retry", new RetryReleaseRequest());
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
