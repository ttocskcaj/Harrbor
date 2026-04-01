using FakeItEasy;
using FluentAssertions;
using Harrbor.Configuration;
using Harrbor.Data.Entities;
using Harrbor.Services.Orchestration.Phases;
using Harrbor.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xunit;
using IQBittorrentClient = Harrbor.Services.Clients.IQBittorrentClient;

namespace Harrbor.Tests.Services.Orchestration;

/// <summary>
/// Tests for the Archival phase of orchestration.
/// The Archival phase changes the torrent category in qBittorrent to prevent re-processing.
/// </summary>
public class ArchivalPhaseTests
{
    private readonly IQBittorrentClient _qBittorrentClient;
    private readonly ILogger<ArchivalPhaseHandler> _logger;

    public ArchivalPhaseTests()
    {
        _qBittorrentClient = A.Fake<IQBittorrentClient>();
        _logger = A.Fake<ILogger<ArchivalPhaseHandler>>();
    }

    private ArchivalPhaseHandler CreateHandler()
    {
        return new ArchivalPhaseHandler(_qBittorrentClient, _logger);
    }

    [Fact]
    public async Task ProcessArchival_WithCompletedCategory_SetsCategory()
    {
        // Arrange
        using var dbContext = TestDbContextFactory.Create();
        var release = new TrackedReleaseBuilder()
            .WithDownloadId("ABC123")
            .WithJobName("test-job")
            .WithCleanupStatus(CleanupStatus.Completed)
            .WithArchivalStatus(ArchivalStatus.Pending)
            .Build();
        dbContext.TrackedReleases.Add(release);
        await dbContext.SaveChangesAsync();

        var job = new JobDefinitionBuilder()
            .WithName("test-job")
            .WithCompletedCategory("completed")
            .Build();

        var handler = CreateHandler();

        // Act
        await handler.ExecuteAsync(job, dbContext, _qBittorrentClient, CancellationToken.None);

        // Assert
        var updatedRelease = await dbContext.TrackedReleases.FirstAsync();
        updatedRelease.ArchivalStatus.Should().Be(ArchivalStatus.Completed);
        updatedRelease.ArchivedAtUtc.Should().NotBeNull();

        A.CallTo(() => _qBittorrentClient.EnsureCategoryExistsAsync("completed", A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => _qBittorrentClient.SetCategoryAsync("ABC123", "completed", A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task ProcessArchival_NoCompletedCategory_SkipsSetCategory()
    {
        // Arrange
        using var dbContext = TestDbContextFactory.Create();
        var release = new TrackedReleaseBuilder()
            .WithDownloadId("ABC123")
            .WithJobName("test-job")
            .WithCleanupStatus(CleanupStatus.Completed)
            .WithArchivalStatus(ArchivalStatus.Pending)
            .Build();
        dbContext.TrackedReleases.Add(release);
        await dbContext.SaveChangesAsync();

        var job = new JobDefinitionBuilder()
            .WithName("test-job")
            .WithCompletedCategory(null) // No completed category
            .Build();

        var handler = CreateHandler();

        // Act
        await handler.ExecuteAsync(job, dbContext, _qBittorrentClient, CancellationToken.None);

        // Assert
        var updatedRelease = await dbContext.TrackedReleases.FirstAsync();
        updatedRelease.ArchivalStatus.Should().Be(ArchivalStatus.Completed);

        A.CallTo(() => _qBittorrentClient.SetCategoryAsync(A<string>._, A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task ProcessArchival_SetCategoryFails_MarksFailed()
    {
        // Arrange
        using var dbContext = TestDbContextFactory.Create();
        var release = new TrackedReleaseBuilder()
            .WithDownloadId("ABC123")
            .WithJobName("test-job")
            .WithCleanupStatus(CleanupStatus.Completed)
            .WithArchivalStatus(ArchivalStatus.Pending)
            .Build();
        dbContext.TrackedReleases.Add(release);
        await dbContext.SaveChangesAsync();

        A.CallTo(() => _qBittorrentClient.SetCategoryAsync("ABC123", "completed", A<CancellationToken>._))
            .Throws(new Exception("Torrent not found"));

        var job = new JobDefinitionBuilder()
            .WithName("test-job")
            .WithCompletedCategory("completed")
            .Build();

        var handler = CreateHandler();

        // Act
        await handler.ExecuteAsync(job, dbContext, _qBittorrentClient, CancellationToken.None);

        // Assert
        var updatedRelease = await dbContext.TrackedReleases.FirstAsync();
        updatedRelease.ArchivalStatus.Should().Be(ArchivalStatus.Failed);
        updatedRelease.ErrorCount.Should().Be(1);
        updatedRelease.LastError.Should().Be("Torrent not found");
        updatedRelease.LastErrorAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task ProcessArchival_RequiresCleanupCompleted()
    {
        // Arrange
        using var dbContext = TestDbContextFactory.Create();
        var pendingCleanupRelease = new TrackedReleaseBuilder()
            .WithDownloadId("PENDING_CLEANUP")
            .WithJobName("test-job")
            .WithCleanupStatus(CleanupStatus.Pending)
            .WithArchivalStatus(ArchivalStatus.Pending)
            .Build();
        dbContext.TrackedReleases.Add(pendingCleanupRelease);
        await dbContext.SaveChangesAsync();

        var job = new JobDefinitionBuilder()
            .WithName("test-job")
            .WithCompletedCategory("completed")
            .Build();

        var handler = CreateHandler();

        // Act
        await handler.ExecuteAsync(job, dbContext, _qBittorrentClient, CancellationToken.None);

        // Assert
        var updatedRelease = await dbContext.TrackedReleases.FirstAsync();
        updatedRelease.ArchivalStatus.Should().Be(ArchivalStatus.Pending);
        A.CallTo(() => _qBittorrentClient.SetCategoryAsync(A<string>._, A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task ProcessArchival_EnsuresCategoryExistsBeforeSetting()
    {
        // Arrange
        using var dbContext = TestDbContextFactory.Create();
        var release = new TrackedReleaseBuilder()
            .WithDownloadId("ABC123")
            .WithJobName("test-job")
            .WithCleanupStatus(CleanupStatus.Completed)
            .WithArchivalStatus(ArchivalStatus.Pending)
            .Build();
        dbContext.TrackedReleases.Add(release);
        await dbContext.SaveChangesAsync();

        var job = new JobDefinitionBuilder()
            .WithName("test-job")
            .WithCompletedCategory("new-category")
            .Build();

        var callOrder = new List<string>();
        A.CallTo(() => _qBittorrentClient.EnsureCategoryExistsAsync(A<string>._, A<CancellationToken>._))
            .Invokes(() => callOrder.Add("ensure"));
        A.CallTo(() => _qBittorrentClient.SetCategoryAsync(A<string>._, A<string>._, A<CancellationToken>._))
            .Invokes(() => callOrder.Add("set"));

        var handler = CreateHandler();

        // Act
        await handler.ExecuteAsync(job, dbContext, _qBittorrentClient, CancellationToken.None);

        // Assert
        callOrder.Should().Equal(["ensure", "set"]);
    }

    [Fact]
    public async Task ProcessArchival_OnlyProcessesPendingArchival()
    {
        // Arrange
        using var dbContext = TestDbContextFactory.Create();
        var pendingArchival = new TrackedReleaseBuilder()
            .WithDownloadId("PENDING1")
            .WithJobName("test-job")
            .WithCleanupStatus(CleanupStatus.Completed)
            .WithArchivalStatus(ArchivalStatus.Pending)
            .Build();
        var completedArchival = new TrackedReleaseBuilder()
            .WithDownloadId("COMPLETED1")
            .WithJobName("test-job")
            .WithCleanupStatus(CleanupStatus.Completed)
            .WithArchivalStatus(ArchivalStatus.Completed)
            .Build();
        dbContext.TrackedReleases.AddRange(pendingArchival, completedArchival);
        await dbContext.SaveChangesAsync();

        var job = new JobDefinitionBuilder()
            .WithName("test-job")
            .WithCompletedCategory("completed")
            .Build();

        var handler = CreateHandler();

        // Act
        await handler.ExecuteAsync(job, dbContext, _qBittorrentClient, CancellationToken.None);

        // Assert
        A.CallTo(() => _qBittorrentClient.SetCategoryAsync("PENDING1", "completed", A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => _qBittorrentClient.SetCategoryAsync("COMPLETED1", A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task ProcessArchival_EmptyCompletedCategory_SkipsSetCategory()
    {
        // Arrange
        using var dbContext = TestDbContextFactory.Create();
        var release = new TrackedReleaseBuilder()
            .WithDownloadId("ABC123")
            .WithJobName("test-job")
            .WithCleanupStatus(CleanupStatus.Completed)
            .WithArchivalStatus(ArchivalStatus.Pending)
            .Build();
        dbContext.TrackedReleases.Add(release);
        await dbContext.SaveChangesAsync();

        var job = new JobDefinitionBuilder()
            .WithName("test-job")
            .WithCompletedCategory("") // Empty string
            .Build();

        var handler = CreateHandler();

        // Act
        await handler.ExecuteAsync(job, dbContext, _qBittorrentClient, CancellationToken.None);

        // Assert
        var updatedRelease = await dbContext.TrackedReleases.FirstAsync();
        updatedRelease.ArchivalStatus.Should().Be(ArchivalStatus.Completed);

        A.CallTo(() => _qBittorrentClient.EnsureCategoryExistsAsync(A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();
        A.CallTo(() => _qBittorrentClient.SetCategoryAsync(A<string>._, A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task ProcessArchival_MultipleReleases_ProcessesAll()
    {
        // Arrange
        using var dbContext = TestDbContextFactory.Create();
        var releases = Enumerable.Range(1, 5).Select(i =>
            new TrackedReleaseBuilder()
                .WithDownloadId($"RELEASE{i}")
                .WithJobName("test-job")
                .WithCleanupStatus(CleanupStatus.Completed)
                .WithArchivalStatus(ArchivalStatus.Pending)
                .Build()
        ).ToList();
        dbContext.TrackedReleases.AddRange(releases);
        await dbContext.SaveChangesAsync();

        var job = new JobDefinitionBuilder()
            .WithName("test-job")
            .WithCompletedCategory("completed")
            .Build();

        var handler = CreateHandler();

        // Act
        await handler.ExecuteAsync(job, dbContext, _qBittorrentClient, CancellationToken.None);

        // Assert
        var updatedReleases = await dbContext.TrackedReleases.ToListAsync();
        updatedReleases.Should().AllSatisfy(r => r.ArchivalStatus.Should().Be(ArchivalStatus.Completed));

        A.CallTo(() => _qBittorrentClient.SetCategoryAsync(A<string>._, "completed", A<CancellationToken>._))
            .MustHaveHappened(5, Times.Exactly);
    }
}
