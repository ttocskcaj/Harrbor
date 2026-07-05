using System.Net;
using FakeItEasy;
using FluentAssertions;
using Harrbor.Data.Entities;
using Harrbor.Services.Orchestration.Phases;
using Harrbor.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QBittorrent.Client;
using Xunit;
using IQBittorrentClient = Harrbor.Services.Clients.IQBittorrentClient;

namespace Harrbor.Tests.Services.Orchestration;

/// <summary>
/// Tests for the Download phase of orchestration.
/// The Download phase checks qBittorrent for completed downloads.
/// </summary>
public class DownloadPhaseTests
{
    private readonly IQBittorrentClient _qBittorrentClient;
    private readonly ILogger<DownloadPhaseHandler> _logger;

    public DownloadPhaseTests()
    {
        _qBittorrentClient = A.Fake<IQBittorrentClient>();
        _logger = A.Fake<ILogger<DownloadPhaseHandler>>();
    }

    private DownloadPhaseHandler CreateHandler()
    {
        return new DownloadPhaseHandler(_qBittorrentClient, _logger);
    }

    [Fact]
    public async Task ProcessDownloads_TorrentComplete_UpdatesStatus()
    {
        // Arrange
        using var dbContext = TestDbContextFactory.Create();
        var release = new TrackedReleaseBuilder()
            .WithDownloadId("ABC123")
            .WithName("Test Release")
            .WithJobName("test-job")
            .WithDownloadStatus(DownloadStatus.Pending)
            .WithRemotePath("/downloads/test")
            .Build();
        dbContext.TrackedReleases.Add(release);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var completedTorrent = new TorrentInfo
        {
            Hash = "ABC123",
            Name = "Test Release",
            Progress = 1.0, // 100% complete
            ContentPath = "/downloads/Test.Release.S01E01",
            SavePath = "/downloads"
        };
        A.CallTo(() => _qBittorrentClient.GetTorrentAsync("ABC123", A<CancellationToken>._))
            .Returns(completedTorrent);

        var job = new JobDefinitionBuilder().WithName("test-job").Build();
        var handler = CreateHandler();

        // Act
        await handler.ExecuteAsync(job, dbContext, _qBittorrentClient, CancellationToken.None);

        // Assert
        var updatedRelease = await dbContext.TrackedReleases.FirstAsync(cancellationToken: TestContext.Current.CancellationToken);
        updatedRelease.DownloadStatus.Should().Be(DownloadStatus.Completed);
        updatedRelease.DownloadCompletedAtUtc.Should().NotBeNull();
        updatedRelease.RemotePath.Should().Be("/downloads/Test.Release.S01E01");
    }

    [Fact]
    public async Task ProcessDownloads_TorrentIncomplete_RemainsInPending()
    {
        // Arrange
        using var dbContext = TestDbContextFactory.Create();
        var release = new TrackedReleaseBuilder()
            .WithDownloadId("ABC123")
            .WithName("Test Release")
            .WithJobName("test-job")
            .WithDownloadStatus(DownloadStatus.Pending)
            .Build();
        dbContext.TrackedReleases.Add(release);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var incompleteTorrent = new TorrentInfo
        {
            Hash = "ABC123",
            Name = "Test Release",
            Progress = 0.5 // 50% complete
        };
        A.CallTo(() => _qBittorrentClient.GetTorrentAsync("ABC123", A<CancellationToken>._))
            .Returns(incompleteTorrent);

        var job = new JobDefinitionBuilder().WithName("test-job").Build();
        var handler = CreateHandler();

        // Act
        await handler.ExecuteAsync(job, dbContext, _qBittorrentClient, CancellationToken.None);

        // Assert
        var updatedRelease = await dbContext.TrackedReleases.FirstAsync(cancellationToken: TestContext.Current.CancellationToken);
        updatedRelease.DownloadStatus.Should().Be(DownloadStatus.Pending);
        updatedRelease.DownloadCompletedAtUtc.Should().BeNull();
    }

    [Fact]
    public async Task ProcessDownloads_TorrentNotFound_RemainsInPending()
    {
        // Arrange
        using var dbContext = TestDbContextFactory.Create();
        var release = new TrackedReleaseBuilder()
            .WithDownloadId("ABC123")
            .WithName("Test Release")
            .WithJobName("test-job")
            .WithDownloadStatus(DownloadStatus.Pending)
            .Build();
        dbContext.TrackedReleases.Add(release);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        A.CallTo(() => _qBittorrentClient.GetTorrentAsync("ABC123", A<CancellationToken>._))
            .Returns((TorrentInfo?)null);

        var job = new JobDefinitionBuilder().WithName("test-job").Build();
        var handler = CreateHandler();

        // Act
        await handler.ExecuteAsync(job, dbContext, _qBittorrentClient, CancellationToken.None);

        // Assert
        var updatedRelease = await dbContext.TrackedReleases.FirstAsync(cancellationToken: TestContext.Current.CancellationToken);
        updatedRelease.DownloadStatus.Should().Be(DownloadStatus.Pending);
    }

    [Fact]
    public async Task ProcessDownloads_UsesContentPath_WhenAvailable()
    {
        // Arrange
        using var dbContext = TestDbContextFactory.Create();
        var release = new TrackedReleaseBuilder()
            .WithDownloadId("ABC123")
            .WithJobName("test-job")
            .WithDownloadStatus(DownloadStatus.Pending)
            .WithRemotePath("/original/path")
            .Build();
        dbContext.TrackedReleases.Add(release);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var torrent = new TorrentInfo
        {
            Hash = "ABC123",
            Name = "Test.Release",
            Progress = 1.0,
            ContentPath = "/downloads/actual/content/path",
            SavePath = "/downloads"
        };
        A.CallTo(() => _qBittorrentClient.GetTorrentAsync("ABC123", A<CancellationToken>._))
            .Returns(torrent);

        var job = new JobDefinitionBuilder().WithName("test-job").Build();
        var handler = CreateHandler();

        // Act
        await handler.ExecuteAsync(job, dbContext, _qBittorrentClient, CancellationToken.None);

        // Assert
        var updatedRelease = await dbContext.TrackedReleases.FirstAsync(cancellationToken: TestContext.Current.CancellationToken);
        updatedRelease.RemotePath.Should().Be("/downloads/actual/content/path");
    }

    [Fact]
    public async Task ProcessDownloads_UsesSavePathPlusName_WhenContentPathEmpty()
    {
        // Arrange
        using var dbContext = TestDbContextFactory.Create();
        var release = new TrackedReleaseBuilder()
            .WithDownloadId("ABC123")
            .WithJobName("test-job")
            .WithDownloadStatus(DownloadStatus.Pending)
            .WithRemotePath("/original/path")
            .Build();
        dbContext.TrackedReleases.Add(release);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var torrent = new TorrentInfo
        {
            Hash = "ABC123",
            Name = "Test.Release",
            Progress = 1.0,
            ContentPath = null,
            SavePath = "/downloads"
        };
        A.CallTo(() => _qBittorrentClient.GetTorrentAsync("ABC123", A<CancellationToken>._))
            .Returns(torrent);

        var job = new JobDefinitionBuilder().WithName("test-job").Build();
        var handler = CreateHandler();

        // Act
        await handler.ExecuteAsync(job, dbContext, _qBittorrentClient, CancellationToken.None);

        // Assert
        var updatedRelease = await dbContext.TrackedReleases.FirstAsync(cancellationToken: TestContext.Current.CancellationToken);
        updatedRelease.RemotePath.Should().Be("/downloads/Test.Release");
    }

    [Fact]
    public async Task ProcessDownloads_GetTorrentThrows_SkipsReleaseAndProcessesOthers()
    {
        // Arrange - one release whose qBittorrent query fails transiently, one healthy release
        using var dbContext = TestDbContextFactory.Create();
        var failing = new TrackedReleaseBuilder()
            .WithDownloadId("BAD")
            .WithName("Failing Release")
            .WithJobName("test-job")
            .WithDownloadStatus(DownloadStatus.Pending)
            .Build();
        var healthy = new TrackedReleaseBuilder()
            .WithDownloadId("GOOD")
            .WithName("Healthy Release")
            .WithJobName("test-job")
            .WithDownloadStatus(DownloadStatus.Pending)
            .WithRemotePath("/downloads/healthy")
            .Build();
        dbContext.TrackedReleases.AddRange(failing, healthy);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        A.CallTo(() => _qBittorrentClient.GetTorrentAsync("BAD", A<CancellationToken>._))
            .Throws(new QBittorrentClientRequestException("forbidden", HttpStatusCode.Forbidden));
        A.CallTo(() => _qBittorrentClient.GetTorrentAsync("GOOD", A<CancellationToken>._))
            .Returns(new TorrentInfo
            {
                Hash = "GOOD",
                Name = "Healthy Release",
                Progress = 1.0,
                ContentPath = "/downloads/healthy"
            });

        var job = new JobDefinitionBuilder().WithName("test-job").Build();
        var handler = CreateHandler();

        // Act - a single torrent's failure must not abort the whole phase
        var act = async () => await handler.ExecuteAsync(job, dbContext, _qBittorrentClient, CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();

        var failingAfter = await dbContext.TrackedReleases.FirstAsync(r => r.DownloadId == "BAD", cancellationToken: TestContext.Current.CancellationToken);
        failingAfter.DownloadStatus.Should().Be(DownloadStatus.Pending, "the failed release is skipped and retried next cycle");
        failingAfter.LastError.Should().Be("forbidden", "the transient error is recorded for visibility");
        failingAfter.LastErrorAtUtc.Should().NotBeNull();
        failingAfter.ErrorCount.Should().Be(0, "a download-query hiccup must not consume the transfer-phase retry budget");

        var healthyAfter = await dbContext.TrackedReleases.FirstAsync(r => r.DownloadId == "GOOD", cancellationToken: TestContext.Current.CancellationToken);
        healthyAfter.DownloadStatus.Should().Be(DownloadStatus.Completed, "other releases are still processed");
    }

    [Fact]
    public async Task ProcessDownloads_CompletionClearsStaleErrorFromPreviousCycle()
    {
        // Arrange - a release that failed its query last cycle (LastError set) now completes
        using var dbContext = TestDbContextFactory.Create();
        var release = new TrackedReleaseBuilder()
            .WithDownloadId("HASH")
            .WithName("Recovered Release")
            .WithJobName("test-job")
            .WithDownloadStatus(DownloadStatus.Pending)
            .Build();
        release.LastError = "forbidden";
        release.LastErrorAtUtc = DateTime.UtcNow.AddMinutes(-5);
        dbContext.TrackedReleases.Add(release);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        A.CallTo(() => _qBittorrentClient.GetTorrentAsync("HASH", A<CancellationToken>._))
            .Returns(new TorrentInfo
            {
                Hash = "HASH",
                Name = "Recovered Release",
                Progress = 1.0,
                ContentPath = "/downloads/recovered"
            });

        var job = new JobDefinitionBuilder().WithName("test-job").Build();
        var handler = CreateHandler();

        // Act
        await handler.ExecuteAsync(job, dbContext, _qBittorrentClient, CancellationToken.None);

        // Assert - a stale error must not follow a completed download into the transfer phase
        var updated = await dbContext.TrackedReleases.FirstAsync(r => r.DownloadId == "HASH", cancellationToken: TestContext.Current.CancellationToken);
        updated.DownloadStatus.Should().Be(DownloadStatus.Completed);
        updated.LastError.Should().BeNull();
        updated.LastErrorAtUtc.Should().BeNull();
    }

    [Fact]
    public async Task ProcessDownloads_UnexpectedException_PropagatesInsteadOfBeingSwallowed()
    {
        // Arrange - a non-HTTP exception represents a bug, not a transient qBittorrent failure,
        // and must not be silently absorbed by the per-release isolation.
        using var dbContext = TestDbContextFactory.Create();
        var release = new TrackedReleaseBuilder()
            .WithDownloadId("HASH")
            .WithName("Buggy Release")
            .WithJobName("test-job")
            .WithDownloadStatus(DownloadStatus.Pending)
            .Build();
        dbContext.TrackedReleases.Add(release);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        A.CallTo(() => _qBittorrentClient.GetTorrentAsync("HASH", A<CancellationToken>._))
            .Throws(new InvalidOperationException("unexpected"));

        var job = new JobDefinitionBuilder().WithName("test-job").Build();
        var handler = CreateHandler();

        // Act
        var act = async () => await handler.ExecuteAsync(job, dbContext, _qBittorrentClient, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ProcessDownloads_OnlyProcessesPendingDownloads()
    {
        // Arrange
        using var dbContext = TestDbContextFactory.Create();
        var pendingRelease = new TrackedReleaseBuilder()
            .WithDownloadId("PENDING123")
            .WithJobName("test-job")
            .WithDownloadStatus(DownloadStatus.Pending)
            .Build();
        var completedRelease = new TrackedReleaseBuilder()
            .WithDownloadId("COMPLETED456")
            .WithJobName("test-job")
            .WithDownloadStatus(DownloadStatus.Completed)
            .Build();
        dbContext.TrackedReleases.AddRange(pendingRelease, completedRelease);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        A.CallTo(() => _qBittorrentClient.GetTorrentAsync("PENDING123", A<CancellationToken>._))
            .Returns(new TorrentInfo { Hash = "PENDING123", Progress = 1.0 });

        var job = new JobDefinitionBuilder().WithName("test-job").Build();
        var handler = CreateHandler();

        // Act
        await handler.ExecuteAsync(job, dbContext, _qBittorrentClient, CancellationToken.None);

        // Assert
        A.CallTo(() => _qBittorrentClient.GetTorrentAsync("PENDING123", A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => _qBittorrentClient.GetTorrentAsync("COMPLETED456", A<CancellationToken>._))
            .MustNotHaveHappened();
    }
}
