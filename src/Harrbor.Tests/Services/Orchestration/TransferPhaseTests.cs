using FakeItEasy;
using FluentAssertions;
using Harrbor.Configuration;
using Harrbor.Data.Entities;
using Harrbor.Services;
using Harrbor.Services.Clients;
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

        // Remote items are directories unless a test says otherwise
        A.CallTo(() => _remoteStorageService.GetItemKindAsync(A<string>._, A<CancellationToken>._))
            .Returns(RemoteItemKind.Directory);
    }

    private TransferPhaseHandler CreateHandler()
    {
        return new TransferPhaseHandler(_remoteStorageService, A.Fake<IMediaServiceResolver>(), _logger);
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
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

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
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

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
        var updatedRelease = await dbContext.TrackedReleases.FirstAsync(cancellationToken: TestContext.Current.CancellationToken);
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
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

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
        var updatedRelease = await dbContext.TrackedReleases.FirstAsync(cancellationToken: TestContext.Current.CancellationToken);
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
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var job = new JobDefinitionBuilder()
            .WithName("test-job")
            .WithMaxTransferRetries(3)
            .Build();

        var handler = CreateHandler();

        // Act
        await handler.ExecuteAsync(job, dbContext, _remoteStorageService, CancellationToken.None);

        // Assert
        var updatedRelease = await dbContext.TrackedReleases.FirstAsync(cancellationToken: TestContext.Current.CancellationToken);
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
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

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
        var updatedRelease = await dbContext.TrackedReleases.FirstAsync(cancellationToken: TestContext.Current.CancellationToken);
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
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

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
        var updatedRelease = await dbContext.TrackedReleases.FirstAsync(cancellationToken: TestContext.Current.CancellationToken);
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
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var job = new JobDefinitionBuilder()
            .WithName("test-job")
            .WithTransferRetryDelay(TimeSpan.FromMinutes(5)) // 5 minute cooldown
            .Build();

        var handler = CreateHandler();

        // Act
        await handler.ExecuteAsync(job, dbContext, _remoteStorageService, CancellationToken.None);

        // Assert
        var updatedRelease = await dbContext.TrackedReleases.FirstAsync(cancellationToken: TestContext.Current.CancellationToken);
        updatedRelease.TransferStatus.Should().Be(TransferStatus.Failed); // Unchanged
        A.CallTo(() => _remoteStorageService.TransferAsync(
            A<string>._, A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    private static TrackedRelease BuildRetryableRelease(DateTime lastErrorAtUtc) =>
        new TrackedReleaseBuilder()
            .WithDownloadId("FAILED1")
            .WithJobName("test-job")
            .WithDownloadStatus(DownloadStatus.Completed)
            .WithTransferStatus(TransferStatus.Failed)
            .WithErrorCount(1)
            .WithLastError("Previous failure")
            .WithLastErrorAtUtc(lastErrorAtUtc)
            .WithRemotePath("/downloads/release1")
            .WithStagingPath("/staging")
            .Build();

    [Fact]
    public async Task TransferRelease_RetryAndRemoteUnreachable_KeepsRetryingWithoutBurningAttempt()
    {
        // Arrange - existence check can't reach the seedbox (e.g. SSH connection refused)
        var errorTime = DateTime.UtcNow.AddMinutes(-10);
        using var dbContext = TestDbContextFactory.Create();
        dbContext.TrackedReleases.Add(BuildRetryableRelease(errorTime));
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        A.CallTo(() => _remoteStorageService.GetItemKindAsync(A<string>._, A<CancellationToken>._))
            .Throws(new RemoteStorageUnavailableException("connection refused"));

        var job = new JobDefinitionBuilder()
            .WithName("test-job")
            .WithMaxTransferRetries(3)
            .WithTransferRetryDelay(TimeSpan.FromMinutes(5))
            .Build();

        var handler = CreateHandler();

        // Act
        await handler.ExecuteAsync(job, dbContext, _remoteStorageService, CancellationToken.None);

        // Assert - still a normal retryable failure, not permanently failed
        var updatedRelease = await dbContext.TrackedReleases.FirstAsync(cancellationToken: TestContext.Current.CancellationToken);
        updatedRelease.TransferStatus.Should().Be(TransferStatus.Failed);
        updatedRelease.ErrorCount.Should().Be(1);
        updatedRelease.LastError.Should().Be("connection refused");
        updatedRelease.LastErrorAtUtc.Should().BeAfter(errorTime);
        A.CallTo(() => _remoteStorageService.TransferAsync(
            A<string>._, A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task TransferRelease_RetryAndRemoteGone_MarksPermanentlyFailed()
    {
        // Arrange
        using var dbContext = TestDbContextFactory.Create();
        dbContext.TrackedReleases.Add(BuildRetryableRelease(DateTime.UtcNow.AddMinutes(-10)));
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        A.CallTo(() => _remoteStorageService.GetItemKindAsync(A<string>._, A<CancellationToken>._))
            .Returns(RemoteItemKind.Missing);

        var job = new JobDefinitionBuilder()
            .WithName("test-job")
            .WithMaxTransferRetries(3)
            .WithTransferRetryDelay(TimeSpan.FromMinutes(5))
            .Build();

        var handler = CreateHandler();

        // Act
        await handler.ExecuteAsync(job, dbContext, _remoteStorageService, CancellationToken.None);

        // Assert
        var updatedRelease = await dbContext.TrackedReleases.FirstAsync(cancellationToken: TestContext.Current.CancellationToken);
        updatedRelease.TransferStatus.Should().Be(TransferStatus.Failed);
        updatedRelease.ErrorCount.Should().Be(3);
        updatedRelease.LastError.Should().Be("Remote source no longer exists");
        A.CallTo(() => _remoteStorageService.TransferAsync(
            A<string>._, A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    private async Task<TrackedRelease> TransferOneAsync(
        string remotePath, string? remoteItemPath, RemoteItemKind kind, IMediaService? mediaService = null)
    {
        using var dbContext = TestDbContextFactory.Create();
        dbContext.TrackedReleases.Add(new TrackedReleaseBuilder()
            .WithDownloadId("DEST1")
            .WithJobName("test-job")
            .WithDownloadStatus(DownloadStatus.Completed)
            .WithTransferStatus(TransferStatus.Pending)
            .WithRemotePath(remotePath)
            .WithRemoteItemPath(remoteItemPath)
            .WithStagingPath("/staging")
            .Build());
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        A.CallTo(() => _remoteStorageService.GetItemKindAsync(A<string>._, A<CancellationToken>._)).Returns(kind);
        A.CallTo(() => _remoteStorageService.TransferAsync(A<string>._, A<string>._, A<CancellationToken>._))
            .Returns(new TransferResult(Success: true, BytesTransferred: 1));
        A.CallTo(() => _remoteStorageService.TransferFileAsync(A<string>._, A<string>._, A<CancellationToken>._))
            .Returns(new TransferResult(Success: true, BytesTransferred: 1));

        var job = new JobDefinitionBuilder().WithName("test-job").WithRemotePath("/downloads").WithStagingPath("/staging").Build();
        await CreateHandler().ExecuteAsync(job, dbContext, _remoteStorageService, mediaService, TestContext.Current.CancellationToken);
        return await dbContext.TrackedReleases.SingleAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ProcessTransfers_MultiFileRoot_CopiesDirectoryIntoHoldingPath()
    {
        var release = await TransferOneAsync("/downloads/Show.S01", "/downloads/Show.S01", RemoteItemKind.Directory);

        release.TransferStatus.Should().Be(TransferStatus.Completed);
        A.CallTo(() => _remoteStorageService.GetItemKindAsync("/downloads/Show.S01", A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => _remoteStorageService.TransferAsync("/downloads/Show.S01", "/staging/.harrbor/Show.S01", A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => _remoteStorageService.TransferFileAsync(A<string>._, A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task ProcessTransfers_SingleFileInRootFolder_CopiesTheRootFolder()
    {
        // Dune: content_path is the file, but the torrent's top-level item is its root folder
        var release = await TransferOneAsync(
            "/downloads/Dune.Part.Two-FraMeSToR/Dune.Part.Two-FraMeSToR.mkv",
            "/downloads/Dune.Part.Two-FraMeSToR",
            RemoteItemKind.Directory);

        release.TransferStatus.Should().Be(TransferStatus.Completed);
        release.GetFinalItemPath().Should().Be("/staging/Dune.Part.Two-FraMeSToR");
        A.CallTo(() => _remoteStorageService.TransferAsync(
                "/downloads/Dune.Part.Two-FraMeSToR", "/staging/.harrbor/Dune.Part.Two-FraMeSToR", A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => _remoteStorageService.TransferFileAsync(A<string>._, A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task ProcessTransfers_PlainSingleFile_CopiesToFilePath()
    {
        // Kubo: the torrent is just a file, so it's staged as a file, not as a directory named like the file
        var release = await TransferOneAsync("/downloads/Kubo-FGT.mkv", "/downloads/Kubo-FGT.mkv", RemoteItemKind.File);

        release.TransferStatus.Should().Be(TransferStatus.Completed);
        A.CallTo(() => _remoteStorageService.TransferFileAsync(
                "/downloads/Kubo-FGT.mkv", "/staging/.harrbor/Kubo-FGT.mkv", A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => _remoteStorageService.TransferAsync(A<string>._, A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task ProcessTransfers_NoRemoteItemPath_FallsBackToRemotePath()
    {
        await TransferOneAsync("/downloads/release1", remoteItemPath: null, RemoteItemKind.Directory);

        A.CallTo(() => _remoteStorageService.TransferAsync("/downloads/release1", "/staging/.harrbor/release1", A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task ProcessTransfers_FirstAttemptAndRemoteMissing_FailsRetryably()
    {
        var release = await TransferOneAsync("/downloads/release1", "/downloads/release1", RemoteItemKind.Missing);

        release.TransferStatus.Should().Be(TransferStatus.Failed);
        release.ErrorCount.Should().Be(1);
        release.LastError.Should().Contain("not found");
        A.CallTo(() => _remoteStorageService.TransferAsync(A<string>._, A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task ProcessTransfers_FirstAttemptAndRemoteUnreachable_StaysPending()
    {
        using var dbContext = TestDbContextFactory.Create();
        dbContext.TrackedReleases.Add(new TrackedReleaseBuilder()
            .WithJobName("test-job")
            .WithDownloadStatus(DownloadStatus.Completed)
            .WithTransferStatus(TransferStatus.Pending)
            .Build());
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        A.CallTo(() => _remoteStorageService.GetItemKindAsync(A<string>._, A<CancellationToken>._))
            .Throws(new RemoteStorageUnavailableException("connection refused"));

        var job = new JobDefinitionBuilder().WithName("test-job").Build();
        await CreateHandler().ExecuteAsync(job, dbContext, _remoteStorageService, TestContext.Current.CancellationToken);

        var release = await dbContext.TrackedReleases.SingleAsync(TestContext.Current.CancellationToken);
        release.TransferStatus.Should().Be(TransferStatus.Pending);
        release.ErrorCount.Should().Be(0);
        release.LastError.Should().Be("connection refused");
    }

    private IMediaService MediaServiceWithOutputPath(string outputPath)
    {
        var mediaService = A.Fake<IMediaService>();
        A.CallTo(() => mediaService.GetQueueAsync(A<CancellationToken>._))
            .Returns([new QueueItemBuilder().WithDownloadId("DEST1").WithOutputPath(outputPath).Build()]);
        return mediaService;
    }

    private int LoggedWarnings() =>
        Fake.GetCalls(_logger).Count(c => c.Method.Name == "Log" && (LogLevel)c.Arguments[0]! == LogLevel.Warning);

    [Fact]
    public async Task ProcessTransfers_OutputPathMatchesStagedLayout_NoWarning()
    {
        var mediaService = MediaServiceWithOutputPath("/media/seedbox-import/Dune.Part.Two-FraMeSToR/Dune.Part.Two-FraMeSToR.mkv");

        await TransferOneAsync(
            "/downloads/Dune.Part.Two-FraMeSToR/Dune.Part.Two-FraMeSToR.mkv",
            "/downloads/Dune.Part.Two-FraMeSToR",
            RemoteItemKind.Directory,
            mediaService);

        A.CallTo(() => mediaService.GetQueueAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        LoggedWarnings().Should().Be(0);
    }

    [Fact]
    public async Task ProcessTransfers_OutputPathDoesNotMatchStagedLayout_Warns()
    {
        // The pre-0.0.9 bug: the item was named after content_path (the file), not the root folder
        var mediaService = MediaServiceWithOutputPath("/media/seedbox-import/Dune.Part.Two-FraMeSToR/Dune.Part.Two-FraMeSToR.mkv");

        var release = await TransferOneAsync(
            "/downloads/Dune.Part.Two-FraMeSToR/Dune.Part.Two-FraMeSToR.mkv",
            "/downloads/Dune.Part.Two-FraMeSToR/Dune.Part.Two-FraMeSToR.mkv",
            RemoteItemKind.File,
            mediaService);

        release.TransferStatus.Should().Be(TransferStatus.Completed, "the check only warns");
        LoggedWarnings().Should().Be(1);
    }

    [Fact]
    public async Task ProcessTransfers_QueueUnavailable_TransferStillCompletes()
    {
        var mediaService = A.Fake<IMediaService>();
        A.CallTo(() => mediaService.GetQueueAsync(A<CancellationToken>._)).Throws(new HttpRequestException("down"));

        var release = await TransferOneAsync("/downloads/release1", "/downloads/release1", RemoteItemKind.Directory, mediaService);

        release.TransferStatus.Should().Be(TransferStatus.Completed);
    }
}
