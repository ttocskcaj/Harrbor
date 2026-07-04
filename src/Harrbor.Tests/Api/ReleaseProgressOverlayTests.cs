using System.Net.Http.Json;
using FluentAssertions;
using Harrbor.Api.Releases;
using Harrbor.Data.Entities;
using Harrbor.Services.RemoteStorage;
using Harrbor.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Harrbor.Tests.Api;

public class ReleaseProgressOverlayTests : IDisposable
{
    private readonly HarrborWebApplicationFactory _factory = new();
    private readonly HttpClient _client;

    public ReleaseProgressOverlayTests()
    {
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task GetRelease_InProgressTransfer_OverlaysLiveProgress()
    {
        int id;
        await using (var db = _factory.CreateDbContext())
        {
            var release = new TrackedReleaseBuilder()
                .WithDownloadId("PROG1").WithJobName("tv-shows")
                .WithDownloadStatus(DownloadStatus.Completed)
                .WithTransferStatus(TransferStatus.InProgress)
                .WithTransferStartedAtUtc(DateTime.UtcNow)
                .Build();
            db.TrackedReleases.Add(release);
            await db.SaveChangesAsync();
            id = release.Id;
        }

        var tracker = _factory.Services.GetRequiredService<ITransferProgressTracker>();
        tracker.Update(id, new TransferProgressSnapshot(
            Progress: 0.42, SpeedBytesPerSec: 1_000_000, BytesTransferred: 42, TotalBytes: 100,
            EtaSeconds: 30, UpdatedAtUtc: DateTime.UtcNow));

        var detail = await _client.GetFromJsonAsync<ReleaseDetail>($"/api/releases/{id}", TestJson.Options);

        detail!.TransferProgress.Should().Be(0.42);
        detail.TransferSpeedBytesPerSec.Should().Be(1_000_000);
        detail.TransferEtaSeconds.Should().Be(30);
        detail.Transfer!.Progress.Should().Be(0.42);
        detail.Transfer.SpeedBytesPerSec.Should().Be(1_000_000);
    }

    [Fact]
    public async Task GetRelease_NotTransferring_HasNoLiveProgress()
    {
        int id;
        await using (var db = _factory.CreateDbContext())
        {
            var release = new TrackedReleaseBuilder()
                .WithDownloadId("PROG2").WithJobName("tv-shows")
                .WithDownloadStatus(DownloadStatus.Pending)
                .Build();
            db.TrackedReleases.Add(release);
            await db.SaveChangesAsync();
            id = release.Id;
        }

        var detail = await _client.GetFromJsonAsync<ReleaseDetail>($"/api/releases/{id}", TestJson.Options);

        detail!.TransferProgress.Should().BeNull();
    }
}
