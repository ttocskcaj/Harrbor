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
        await dbContext.SaveChangesAsync();

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

        A.CallTo(() => _qBittorrentClient.GetTorrentAsync("ABC123", A<CancellationToken>._))
            .Returns((TorrentInfo?)null);

        var job = new JobDefinitionBuilder().WithName("test-job").Build();
        var handler = CreateHandler();

        // Act
        await handler.ExecuteAsync(job, dbContext, _qBittorrentClient, CancellationToken.None);

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
