using System.Net;
using FakeItEasy;
using FluentAssertions;
using Harrbor.Abstractions.Clients;
using Harrbor.Abstractions.Clients.Models;
using Harrbor.Data.Entities;
using Harrbor.Services;
using Harrbor.Services.Orchestration.Phases;
using Harrbor.Tests.Common;
using Harrbor.Tests.Unit.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Harrbor.Tests.Unit.Services.Orchestration;

/// <summary>
/// Tests for the Download phase of orchestration.
/// The Download phase checks the torrent client for completed downloads.
/// </summary>
public class DownloadPhaseTests
{
    private readonly ITorrentClient _torrentClient;
    private readonly ILogger<DownloadPhaseHandler> _logger;

    public DownloadPhaseTests()
    {
        _torrentClient = A.Fake<ITorrentClient>();
        _logger = A.Fake<ILogger<DownloadPhaseHandler>>();
    }

    // The tests drive the client-injecting overload directly, so the manager is never invoked.
    private DownloadPhaseHandler CreateHandler()
    {
        return new DownloadPhaseHandler(A.Fake<IConnectionManager>(), _logger);
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

        var completedTorrent = new TorrentSnapshot(
            "ABC123", "Test Release", 1.0, "/downloads/Test.Release.S01E01", "/downloads", null, []);
        A.CallTo(() => _torrentClient.GetTorrentAsync("ABC123", A<CancellationToken>._))
            .Returns(completedTorrent);

        var job = new JobDefinitionBuilder().WithName("test-job").Build();
        var handler = CreateHandler();

        // Act
        await handler.ExecuteAsync(job, dbContext, _torrentClient, CancellationToken.None);

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

        var incompleteTorrent = new TorrentSnapshot(
            "ABC123", "Test Release", 0.5, null, null, null, []);
        A.CallTo(() => _torrentClient.GetTorrentAsync("ABC123", A<CancellationToken>._))
            .Returns(incompleteTorrent);

        var job = new JobDefinitionBuilder().WithName("test-job").Build();
        var handler = CreateHandler();

        // Act
        await handler.ExecuteAsync(job, dbContext, _torrentClient, CancellationToken.None);

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

        A.CallTo(() => _torrentClient.GetTorrentAsync("ABC123", A<CancellationToken>._))
            .Returns((TorrentSnapshot?)null);

        var job = new JobDefinitionBuilder().WithName("test-job").Build();
        var handler = CreateHandler();

        // Act
        await handler.ExecuteAsync(job, dbContext, _torrentClient, CancellationToken.None);

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

        var torrent = new TorrentSnapshot(
            "ABC123", "Test.Release", 1.0, "/downloads/actual/content/path", "/downloads", null, []);
        A.CallTo(() => _torrentClient.GetTorrentAsync("ABC123", A<CancellationToken>._))
            .Returns(torrent);

        var job = new JobDefinitionBuilder().WithName("test-job").Build();
        var handler = CreateHandler();

        // Act
        await handler.ExecuteAsync(job, dbContext, _torrentClient, CancellationToken.None);

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

        var torrent = new TorrentSnapshot(
            "ABC123", "Test.Release", 1.0, null, "/downloads", null, []);
        A.CallTo(() => _torrentClient.GetTorrentAsync("ABC123", A<CancellationToken>._))
            .Returns(torrent);

        var job = new JobDefinitionBuilder().WithName("test-job").Build();
        var handler = CreateHandler();

        // Act
        await handler.ExecuteAsync(job, dbContext, _torrentClient, CancellationToken.None);

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

        A.CallTo(() => _torrentClient.GetTorrentAsync("BAD", A<CancellationToken>._))
            .Throws(new HttpRequestException("forbidden"));
        A.CallTo(() => _torrentClient.GetTorrentAsync("GOOD", A<CancellationToken>._))
            .Returns(new TorrentSnapshot(
                "GOOD", "Healthy Release", 1.0, "/downloads/healthy", "/downloads", null, []));

        var job = new JobDefinitionBuilder().WithName("test-job").Build();
        var handler = CreateHandler();

        // Act - a single torrent's failure must not abort the whole phase
        var act = async () => await handler.ExecuteAsync(job, dbContext, _torrentClient, CancellationToken.None);

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

        A.CallTo(() => _torrentClient.GetTorrentAsync("HASH", A<CancellationToken>._))
            .Returns(new TorrentSnapshot(
                "HASH", "Recovered Release", 1.0, "/downloads/recovered", "/downloads", null, []));

        var job = new JobDefinitionBuilder().WithName("test-job").Build();
        var handler = CreateHandler();

        // Act
        await handler.ExecuteAsync(job, dbContext, _torrentClient, CancellationToken.None);

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

        A.CallTo(() => _torrentClient.GetTorrentAsync("HASH", A<CancellationToken>._))
            .Throws(new InvalidOperationException("unexpected"));

        var job = new JobDefinitionBuilder().WithName("test-job").Build();
        var handler = CreateHandler();

        // Act
        var act = async () => await handler.ExecuteAsync(job, dbContext, _torrentClient, CancellationToken.None);

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

        A.CallTo(() => _torrentClient.GetTorrentAsync("PENDING123", A<CancellationToken>._))
            .Returns(new TorrentSnapshot("PENDING123", "", 1.0, null, null, null, []));

        var job = new JobDefinitionBuilder().WithName("test-job").Build();
        var handler = CreateHandler();

        // Act
        await handler.ExecuteAsync(job, dbContext, _torrentClient, CancellationToken.None);

        // Assert
        A.CallTo(() => _torrentClient.GetTorrentAsync("PENDING123", A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => _torrentClient.GetTorrentAsync("COMPLETED456", A<CancellationToken>._))
            .MustNotHaveHappened();
    }
}
