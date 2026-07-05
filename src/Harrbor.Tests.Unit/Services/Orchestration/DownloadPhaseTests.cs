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
        await dbContext.SaveChangesAsync();

        var completedTorrent = new TorrentSnapshot(
            "ABC123", "Test Release", 1.0, "/downloads/Test.Release.S01E01", "/downloads", null, []);
        A.CallTo(() => _torrentClient.GetTorrentAsync("ABC123", A<CancellationToken>._))
            .Returns(completedTorrent);

        var job = new JobDefinitionBuilder().WithName("test-job").Build();
        var handler = CreateHandler();

        // Act
        await handler.ExecuteAsync(job, dbContext, _torrentClient, CancellationToken.None);

        // Assert
        var updatedRelease = await dbContext.TrackedReleases.FirstAsync();
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
        await dbContext.SaveChangesAsync();

        var incompleteTorrent = new TorrentSnapshot(
            "ABC123", "Test Release", 0.5, null, null, null, []);
        A.CallTo(() => _torrentClient.GetTorrentAsync("ABC123", A<CancellationToken>._))
            .Returns(incompleteTorrent);

        var job = new JobDefinitionBuilder().WithName("test-job").Build();
        var handler = CreateHandler();

        // Act
        await handler.ExecuteAsync(job, dbContext, _torrentClient, CancellationToken.None);

        // Assert
        var updatedRelease = await dbContext.TrackedReleases.FirstAsync();
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
        await dbContext.SaveChangesAsync();

        A.CallTo(() => _torrentClient.GetTorrentAsync("ABC123", A<CancellationToken>._))
            .Returns((TorrentSnapshot?)null);

        var job = new JobDefinitionBuilder().WithName("test-job").Build();
        var handler = CreateHandler();

        // Act
        await handler.ExecuteAsync(job, dbContext, _torrentClient, CancellationToken.None);

        // Assert
        var updatedRelease = await dbContext.TrackedReleases.FirstAsync();
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
        await dbContext.SaveChangesAsync();

        var torrent = new TorrentSnapshot(
            "ABC123", "Test.Release", 1.0, "/downloads/actual/content/path", "/downloads", null, []);
        A.CallTo(() => _torrentClient.GetTorrentAsync("ABC123", A<CancellationToken>._))
            .Returns(torrent);

        var job = new JobDefinitionBuilder().WithName("test-job").Build();
        var handler = CreateHandler();

        // Act
        await handler.ExecuteAsync(job, dbContext, _torrentClient, CancellationToken.None);

        // Assert
        var updatedRelease = await dbContext.TrackedReleases.FirstAsync();
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
        await dbContext.SaveChangesAsync();

        var torrent = new TorrentSnapshot(
            "ABC123", "Test.Release", 1.0, null, "/downloads", null, []);
        A.CallTo(() => _torrentClient.GetTorrentAsync("ABC123", A<CancellationToken>._))
            .Returns(torrent);

        var job = new JobDefinitionBuilder().WithName("test-job").Build();
        var handler = CreateHandler();

        // Act
        await handler.ExecuteAsync(job, dbContext, _torrentClient, CancellationToken.None);

        // Assert
        var updatedRelease = await dbContext.TrackedReleases.FirstAsync();
        updatedRelease.RemotePath.Should().Be("/downloads/Test.Release");
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
        await dbContext.SaveChangesAsync();

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
