using FakeItEasy;
using FluentAssertions;
using Harrbor.Configuration;
using Harrbor.Data.Entities;
using Harrbor.Services.Orchestration.Phases;
using Harrbor.Services.RemoteStorage;
using Harrbor.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Harrbor.Tests.Services.Orchestration;

/// <summary>
/// Tests for the Transfer phase of orchestration.
/// The Transfer phase runs rclone transfers from seedbox to local staging.
/// </summary>
public class TransferPhaseTests
{
    private readonly IRemoteStorageService _remoteStorageService;
    private readonly ILogger<TransferPhaseHandler> _logger;

    public TransferPhaseTests()
    {
        _remoteStorageService = A.Fake<IRemoteStorageService>();
        _logger = A.Fake<ILogger<TransferPhaseHandler>>();
    }

    private TransferPhaseHandler CreateHandler()
    {
        return new TransferPhaseHandler(_remoteStorageService, _logger);
    }

    [Fact]
    public async Task ProcessTransfers_ParallelismLimitReached_DoesNotStartNew()
    {
        // Arrange
        using var dbContext = TestDbContextFactory.Create();

        // Add releases that are already in-progress (using up parallelism slots)
        var inProgressRelease1 = new TrackedReleaseBuilder()
            .WithDownloadId("INPROGRESS1")
            .WithJobName("test-job")
            .WithDownloadStatus(DownloadStatus.Completed)
            .WithTransferStatus(TransferStatus.InProgress)
            .Build();
        var inProgressRelease2 = new TrackedReleaseBuilder()
            .WithDownloadId("INPROGRESS2")
            .WithJobName("test-job")
            .WithDownloadStatus(DownloadStatus.Completed)
            .WithTransferStatus(TransferStatus.InProgress)
            .Build();
        var pendingRelease = new TrackedReleaseBuilder()
            .WithDownloadId("PENDING1")
            .WithJobName("test-job")
            .WithDownloadStatus(DownloadStatus.Completed)
            .WithTransferStatus(TransferStatus.Pending)
            .Build();

        dbContext.TrackedReleases.AddRange(inProgressRelease1, inProgressRelease2, pendingRelease);
        await dbContext.SaveChangesAsync();

        var job = new JobDefinitionBuilder()
            .WithName("test-job")
            .WithTransferParallelism(2) // Only 2 parallel transfers allowed
            .Build();

        var handler = CreateHandler();

        // Act
        await handler.ExecuteAsync(job, dbContext, _remoteStorageService, CancellationToken.None);

        // Assert - transfer should not have been called
        A.CallTo(() => _remoteStorageService.TransferAsync(A<string>._, A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task ProcessTransfers_SlotsAvailable_StartsTransfers()
    {
        // Arrange
        using var dbContext = TestDbContextFactory.Create();
        var pendingRelease = new TrackedReleaseBuilder()
            .WithDownloadId("PENDING1")
            .WithJobName("test-job")
            .WithDownloadStatus(DownloadStatus.Completed)
            .WithTransferStatus(TransferStatus.Pending)
            .WithRemotePath("/downloads/release1")
            .WithStagingPath("/staging")
            .Build();
        dbContext.TrackedReleases.Add(pendingRelease);
        await dbContext.SaveChangesAsync();

        A.CallTo(() => _remoteStorageService.TransferAsync(
            A<string>._, A<string>._, A<CancellationToken>._))
            .Returns(new TransferResult(Success: true, BytesTransferred: 1024));

        var job = new JobDefinitionBuilder()
            .WithName("test-job")
            .WithTransferParallelism(2)
            .Build();

        var handler = CreateHandler();

        // Act
        await handler.ExecuteAsync(job, dbContext, _remoteStorageService, CancellationToken.None);

        // Assert
        var updatedRelease = await dbContext.TrackedReleases.FirstAsync();
        updatedRelease.TransferStatus.Should().Be(TransferStatus.Completed);
        A.CallTo(() => _remoteStorageService.TransferAsync(
            A<string>._, A<string>._, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task ProcessTransfers_FailedBelowMaxRetries_RetriesAfterCooldown()
    {
        // Arrange
        var errorTime = DateTime.UtcNow.AddMinutes(-10); // Well past cooldown
        using var dbContext = TestDbContextFactory.Create();
        var failedRelease = new TrackedReleaseBuilder()
            .WithDownloadId("FAILED1")
            .WithJobName("test-job")
            .WithDownloadStatus(DownloadStatus.Completed)
            .WithTransferStatus(TransferStatus.Failed)
            .WithErrorCount(1) // Below max retries (3)
            .WithLastError("Previous failure")
            .WithLastErrorAtUtc(errorTime)
            .WithRemotePath("/downloads/release1")
            .WithStagingPath("/staging")
            .Build();
        dbContext.TrackedReleases.Add(failedRelease);
        await dbContext.SaveChangesAsync();

        A.CallTo(() => _remoteStorageService.ExistsAsync(A<string>._, A<CancellationToken>._))
            .Returns(true);
        A.CallTo(() => _remoteStorageService.TransferAsync(
            A<string>._, A<string>._, A<CancellationToken>._))
            .Returns(new TransferResult(Success: true, BytesTransferred: 1024));

        var job = new JobDefinitionBuilder()
            .WithName("test-job")
            .WithMaxTransferRetries(3)
            .WithTransferRetryDelay(TimeSpan.FromMinutes(5))
            .Build();

        var handler = CreateHandler();

        // Act
        await handler.ExecuteAsync(job, dbContext, _remoteStorageService, CancellationToken.None);

        // Assert
        var updatedRelease = await dbContext.TrackedReleases.FirstAsync();
        updatedRelease.TransferStatus.Should().Be(TransferStatus.Completed);
    }

    [Fact]
    public async Task ProcessTransfers_FailedAtMaxRetries_DoesNotRetry()
    {
        // Arrange
        var errorTime = DateTime.UtcNow.AddMinutes(-10);
        using var dbContext = TestDbContextFactory.Create();
        var failedRelease = new TrackedReleaseBuilder()
            .WithDownloadId("FAILED1")
            .WithJobName("test-job")
            .WithDownloadStatus(DownloadStatus.Completed)
            .WithTransferStatus(TransferStatus.Failed)
            .WithErrorCount(3) // At max retries
            .WithLastError("Max retries reached")
            .WithLastErrorAtUtc(errorTime)
            .Build();
        dbContext.TrackedReleases.Add(failedRelease);
        await dbContext.SaveChangesAsync();

        var job = new JobDefinitionBuilder()
            .WithName("test-job")
            .WithMaxTransferRetries(3)
            .Build();

        var handler = CreateHandler();

        // Act
        await handler.ExecuteAsync(job, dbContext, _remoteStorageService, CancellationToken.None);

        // Assert
        var updatedRelease = await dbContext.TrackedReleases.FirstAsync();
        updatedRelease.TransferStatus.Should().Be(TransferStatus.Failed);
        A.CallTo(() => _remoteStorageService.TransferAsync(
            A<string>._, A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task TransferRelease_Success_ClearsErrorState()
    {
        // Arrange
        using var dbContext = TestDbContextFactory.Create();
        var release = new TrackedReleaseBuilder()
            .WithDownloadId("TEST1")
            .WithJobName("test-job")
            .WithDownloadStatus(DownloadStatus.Completed)
            .WithTransferStatus(TransferStatus.Pending)
            .WithErrorCount(2)
            .WithLastError("Previous error")
            .WithLastErrorAtUtc(DateTime.UtcNow.AddMinutes(-10))
            .WithRemotePath("/downloads/release1")
            .WithStagingPath("/staging")
            .Build();
        dbContext.TrackedReleases.Add(release);
        await dbContext.SaveChangesAsync();

        A.CallTo(() => _remoteStorageService.TransferAsync(
            A<string>._, A<string>._, A<CancellationToken>._))
            .Returns(new TransferResult(Success: true, BytesTransferred: 1024));

        var job = new JobDefinitionBuilder()
            .WithName("test-job")
            .Build();

        var handler = CreateHandler();

        // Act
        await handler.ExecuteAsync(job, dbContext, _remoteStorageService, CancellationToken.None);

        // Assert
        var updatedRelease = await dbContext.TrackedReleases.FirstAsync();
        updatedRelease.TransferStatus.Should().Be(TransferStatus.Completed);
        updatedRelease.LastError.Should().BeNull();
        updatedRelease.LastErrorAtUtc.Should().BeNull();
    }

    [Fact]
    public async Task TransferRelease_Failure_IncrementsErrorCount()
    {
        // Arrange
        using var dbContext = TestDbContextFactory.Create();
        var release = new TrackedReleaseBuilder()
            .WithDownloadId("TEST1")
            .WithJobName("test-job")
            .WithDownloadStatus(DownloadStatus.Completed)
            .WithTransferStatus(TransferStatus.Pending)
            .WithErrorCount(0)
            .WithRemotePath("/downloads/release1")
            .WithStagingPath("/staging")
            .Build();
        dbContext.TrackedReleases.Add(release);
        await dbContext.SaveChangesAsync();

        A.CallTo(() => _remoteStorageService.TransferAsync(
            A<string>._, A<string>._, A<CancellationToken>._))
            .Returns(new TransferResult(Success: false, Error: "Connection failed"));

        var job = new JobDefinitionBuilder()
            .WithName("test-job")
            .Build();

        var handler = CreateHandler();

        // Act
        await handler.ExecuteAsync(job, dbContext, _remoteStorageService, CancellationToken.None);

        // Assert
        var updatedRelease = await dbContext.TrackedReleases.FirstAsync();
        updatedRelease.TransferStatus.Should().Be(TransferStatus.Failed);
        updatedRelease.ErrorCount.Should().Be(1);
        updatedRelease.LastError.Should().Be("Connection failed");
        updatedRelease.LastErrorAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task ProcessTransfers_FailedInCooldown_DoesNotRetry()
    {
        // Arrange - Error occurred recently, still in cooldown
        var recentErrorTime = DateTime.UtcNow.AddMinutes(-2);
        using var dbContext = TestDbContextFactory.Create();
        var failedRelease = new TrackedReleaseBuilder()
            .WithDownloadId("FAILED1")
            .WithJobName("test-job")
            .WithDownloadStatus(DownloadStatus.Completed)
            .WithTransferStatus(TransferStatus.Failed)
            .WithErrorCount(1)
            .WithLastErrorAtUtc(recentErrorTime)
            .Build();
        dbContext.TrackedReleases.Add(failedRelease);
        await dbContext.SaveChangesAsync();

        var job = new JobDefinitionBuilder()
            .WithName("test-job")
            .WithTransferRetryDelay(TimeSpan.FromMinutes(5)) // 5 minute cooldown
            .Build();

        var handler = CreateHandler();

        // Act
        await handler.ExecuteAsync(job, dbContext, _remoteStorageService, CancellationToken.None);

        // Assert
        var updatedRelease = await dbContext.TrackedReleases.FirstAsync();
        updatedRelease.TransferStatus.Should().Be(TransferStatus.Failed); // Unchanged
        A.CallTo(() => _remoteStorageService.TransferAsync(
            A<string>._, A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }
}
