using FakeItEasy;
using FluentAssertions;
using Harrbor.Configuration;
using Harrbor.Data.Entities;
using Harrbor.Services;
using Harrbor.Services.Clients;
using Harrbor.Services.Orchestration.Phases;
using Harrbor.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Harrbor.Tests.Services.Orchestration;

/// <summary>
/// Tests for the Import phase of orchestration.
/// The Import phase checks if Sonarr/Radarr has imported the downloaded files.
/// </summary>
public class ImportPhaseTests
{
    private readonly IMediaServiceResolver _mediaServiceResolver;
    private readonly IMediaService _mediaService;
    private readonly ILogger<ImportPhaseHandler> _logger;

    public ImportPhaseTests()
    {
        _mediaService = A.Fake<IMediaService>();
        _mediaServiceResolver = A.Fake<IMediaServiceResolver>();
        _logger = A.Fake<ILogger<ImportPhaseHandler>>();

        A.CallTo(() => _mediaServiceResolver.GetServiceForJob(A<JobDefinition>._))
            .Returns(_mediaService);
    }

    private ImportPhaseHandler CreateHandler()
    {
        return new ImportPhaseHandler(_mediaServiceResolver, _logger);
    }

    [Fact]
    public async Task ProcessImports_Imported_MarksCompleted()
    {
        // Arrange
        using var dbContext = TestDbContextFactory.Create();
        var release = new TrackedReleaseBuilder()
            .WithDownloadId("ABC123")
            .WithJobName("test-job")
            .WithTransferStatus(TransferStatus.Completed)
            .WithImportStatus(ImportStatus.Pending)
            .WithTransferCompletedAtUtc(DateTime.UtcNow.AddMinutes(-5))
            .Build();
        dbContext.TrackedReleases.Add(release);
        await dbContext.SaveChangesAsync();

        A.CallTo(() => _mediaService.IsDownloadInQueueAsync("ABC123", A<CancellationToken>._))
            .Returns(false);
        A.CallTo(() => _mediaService.HasImportedAsync("ABC123", A<CancellationToken>._))
            .Returns(true);

        var job = new JobDefinitionBuilder()
            .WithName("test-job")
            .WithImportTimeout(TimeSpan.FromHours(24))
            .Build();

        var handler = CreateHandler();

        // Act
        await handler.ExecuteAsync(job, dbContext, _mediaService, CancellationToken.None);

        // Assert
        var updatedRelease = await dbContext.TrackedReleases.FirstAsync();
        updatedRelease.ImportStatus.Should().Be(ImportStatus.Completed);
        updatedRelease.ImportCompletedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task ProcessImports_NotImported_MarksFailed()
    {
        // Arrange
        using var dbContext = TestDbContextFactory.Create();
        var release = new TrackedReleaseBuilder()
            .WithDownloadId("ABC123")
            .WithJobName("test-job")
            .WithTransferStatus(TransferStatus.Completed)
            .WithImportStatus(ImportStatus.Pending)
            .WithTransferCompletedAtUtc(DateTime.UtcNow.AddMinutes(-5))
            .Build();
        dbContext.TrackedReleases.Add(release);
        await dbContext.SaveChangesAsync();

        A.CallTo(() => _mediaService.IsDownloadInQueueAsync("ABC123", A<CancellationToken>._))
            .Returns(false); // Not in queue anymore
        A.CallTo(() => _mediaService.HasImportedAsync("ABC123", A<CancellationToken>._))
            .Returns(false); // But no import event found

        var job = new JobDefinitionBuilder()
            .WithName("test-job")
            .Build();

        var handler = CreateHandler();

        // Act
        await handler.ExecuteAsync(job, dbContext, _mediaService, CancellationToken.None);

        // Assert
        var updatedRelease = await dbContext.TrackedReleases.FirstAsync();
        updatedRelease.ImportStatus.Should().Be(ImportStatus.Failed);
        updatedRelease.LastError.Should().Contain("removed from queue without import");
    }

    [Fact]
    public async Task ProcessImports_StillInQueue_RemainsInPending()
    {
        // Arrange
        using var dbContext = TestDbContextFactory.Create();
        var release = new TrackedReleaseBuilder()
            .WithDownloadId("ABC123")
            .WithJobName("test-job")
            .WithTransferStatus(TransferStatus.Completed)
            .WithImportStatus(ImportStatus.Pending)
            .WithTransferCompletedAtUtc(DateTime.UtcNow.AddMinutes(-5))
            .Build();
        dbContext.TrackedReleases.Add(release);
        await dbContext.SaveChangesAsync();

        A.CallTo(() => _mediaService.IsDownloadInQueueAsync("ABC123", A<CancellationToken>._))
            .Returns(true); // Still in queue, being processed

        var job = new JobDefinitionBuilder()
            .WithName("test-job")
            .WithImportTimeout(TimeSpan.FromHours(24))
            .Build();

        var handler = CreateHandler();

        // Act
        await handler.ExecuteAsync(job, dbContext, _mediaService, CancellationToken.None);

        // Assert
        var updatedRelease = await dbContext.TrackedReleases.FirstAsync();
        updatedRelease.ImportStatus.Should().Be(ImportStatus.Pending);
        // HasImportedAsync should not be called when item is still in queue
        A.CallTo(() => _mediaService.HasImportedAsync(A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task ProcessImports_ExceedsTimeout_MarksFailed()
    {
        // Arrange
        using var dbContext = TestDbContextFactory.Create();
        var release = new TrackedReleaseBuilder()
            .WithDownloadId("ABC123")
            .WithJobName("test-job")
            .WithTransferStatus(TransferStatus.Completed)
            .WithImportStatus(ImportStatus.Pending)
            .WithTransferCompletedAtUtc(DateTime.UtcNow.AddHours(-25)) // Completed 25 hours ago
            .Build();
        dbContext.TrackedReleases.Add(release);
        await dbContext.SaveChangesAsync();

        A.CallTo(() => _mediaService.IsDownloadInQueueAsync("ABC123", A<CancellationToken>._))
            .Returns(true); // Still in queue

        var job = new JobDefinitionBuilder()
            .WithName("test-job")
            .WithImportTimeout(TimeSpan.FromHours(24)) // 24 hour timeout
            .Build();

        var handler = CreateHandler();

        // Act
        await handler.ExecuteAsync(job, dbContext, _mediaService, CancellationToken.None);

        // Assert
        var updatedRelease = await dbContext.TrackedReleases.FirstAsync();
        updatedRelease.ImportStatus.Should().Be(ImportStatus.Failed);
        updatedRelease.LastError.Should().Contain("timeout");
    }

    [Fact]
    public async Task ProcessImports_OnlyProcessesPendingImports()
    {
        // Arrange
        using var dbContext = TestDbContextFactory.Create();
        var pendingRelease = new TrackedReleaseBuilder()
            .WithDownloadId("PENDING123")
            .WithJobName("test-job")
            .WithTransferStatus(TransferStatus.Completed)
            .WithImportStatus(ImportStatus.Pending)
            .WithTransferCompletedAtUtc(DateTime.UtcNow)
            .Build();
        var completedRelease = new TrackedReleaseBuilder()
            .WithDownloadId("COMPLETED456")
            .WithJobName("test-job")
            .WithTransferStatus(TransferStatus.Completed)
            .WithImportStatus(ImportStatus.Completed)
            .Build();
        dbContext.TrackedReleases.AddRange(pendingRelease, completedRelease);
        await dbContext.SaveChangesAsync();

        A.CallTo(() => _mediaService.IsDownloadInQueueAsync(A<string>._, A<CancellationToken>._))
            .Returns(false);
        A.CallTo(() => _mediaService.HasImportedAsync(A<string>._, A<CancellationToken>._))
            .Returns(true);

        var job = new JobDefinitionBuilder().WithName("test-job").Build();
        var handler = CreateHandler();

        // Act
        await handler.ExecuteAsync(job, dbContext, _mediaService, CancellationToken.None);

        // Assert
        A.CallTo(() => _mediaService.IsDownloadInQueueAsync("PENDING123", A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => _mediaService.IsDownloadInQueueAsync("COMPLETED456", A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task ProcessImports_RequiresTransferCompleted()
    {
        // Arrange
        using var dbContext = TestDbContextFactory.Create();
        var pendingTransferRelease = new TrackedReleaseBuilder()
            .WithDownloadId("PENDING_TRANSFER")
            .WithJobName("test-job")
            .WithTransferStatus(TransferStatus.Pending)
            .WithImportStatus(ImportStatus.Pending)
            .Build();
        dbContext.TrackedReleases.Add(pendingTransferRelease);
        await dbContext.SaveChangesAsync();

        var job = new JobDefinitionBuilder().WithName("test-job").Build();
        var handler = CreateHandler();

        // Act
        await handler.ExecuteAsync(job, dbContext, _mediaService, CancellationToken.None);

        // Assert - Should not process releases where transfer is not completed
        A.CallTo(() => _mediaService.IsDownloadInQueueAsync(A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task ProcessImports_WithinTimeout_RemainsInPending()
    {
        // Arrange
        using var dbContext = TestDbContextFactory.Create();
        var release = new TrackedReleaseBuilder()
            .WithDownloadId("ABC123")
            .WithJobName("test-job")
            .WithTransferStatus(TransferStatus.Completed)
            .WithImportStatus(ImportStatus.Pending)
            .WithTransferCompletedAtUtc(DateTime.UtcNow.AddHours(-1)) // Completed 1 hour ago
            .Build();
        dbContext.TrackedReleases.Add(release);
        await dbContext.SaveChangesAsync();

        A.CallTo(() => _mediaService.IsDownloadInQueueAsync("ABC123", A<CancellationToken>._))
            .Returns(true); // Still in queue

        var job = new JobDefinitionBuilder()
            .WithName("test-job")
            .WithImportTimeout(TimeSpan.FromHours(24)) // 24 hour timeout - still within
            .Build();

        var handler = CreateHandler();

        // Act
        await handler.ExecuteAsync(job, dbContext, _mediaService, CancellationToken.None);

        // Assert
        var updatedRelease = await dbContext.TrackedReleases.FirstAsync();
        updatedRelease.ImportStatus.Should().Be(ImportStatus.Pending);
        updatedRelease.LastError.Should().BeNull();
    }
}
