using FakeItEasy;
using FluentAssertions;
using Harrbor.Data.Entities;
using Harrbor.Services.Extraction;
using Harrbor.Services.Orchestration.Phases;
using Harrbor.Tests.Helpers;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Harrbor.Tests.Services.Orchestration;

/// <summary>
/// Tests for the Extraction phase of orchestration.
/// The Extraction phase unpacks zip/rar/7z archives in staging before import.
/// </summary>
public class ExtractionPhaseTests
{
    private readonly IArchiveExtractionService _extractionService;
    private readonly ILogger<ExtractionPhaseHandler> _logger;

    public ExtractionPhaseTests()
    {
        _extractionService = A.Fake<IArchiveExtractionService>();
        _logger = A.Fake<ILogger<ExtractionPhaseHandler>>();
    }

    private ExtractionPhaseHandler CreateHandler()
    {
        return new ExtractionPhaseHandler(_extractionService, _logger);
    }

    private static TrackedReleaseBuilder EligibleRelease()
    {
        return new TrackedReleaseBuilder()
            .WithJobName("test-job")
            .WithRemotePath("/downloads/Test.Release.S01E01")
            .WithStagingPath("/staging/test")
            .WithDownloadStatus(DownloadStatus.Completed)
            .WithTransferStatus(TransferStatus.Completed)
            .WithExtractionStatus(ExtractionStatus.Pending)
            .WithImportStatus(ImportStatus.Pending);
    }

    [Fact]
    public async Task ProcessExtractions_EligibleRelease_ExtractsStagingItemAndMarksCompleted()
    {
        // Arrange
        using var dbContext = TestDbContextFactory.Create();
        var release = EligibleRelease()
            .WithLastError("previous error")
            .WithLastErrorAtUtc(DateTime.UtcNow.AddHours(-1))
            .Build();
        dbContext.TrackedReleases.Add(release);
        await dbContext.SaveChangesAsync();

        A.CallTo(() => _extractionService.ExtractAsync(A<string>._, A<CancellationToken>._))
            .Returns(new ExtractionResult(true, ArchivesExtracted: 1, Duration: TimeSpan.FromSeconds(2)));

        var job = new JobDefinitionBuilder().WithName("test-job").Build();
        var handler = CreateHandler();

        // Act
        await handler.ExecuteAsync(job, dbContext, _extractionService, CancellationToken.None);

        // Assert - service called with the staging item path derived from RemotePath
        A.CallTo(() => _extractionService.ExtractAsync(
                Path.Combine("/staging/test", "Test.Release.S01E01"), A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();

        var updated = dbContext.TrackedReleases.Single();
        updated.ExtractionStatus.Should().Be(ExtractionStatus.Completed);
        updated.ExtractionStartedAtUtc.Should().NotBeNull();
        updated.ExtractionCompletedAtUtc.Should().NotBeNull();
        updated.LastError.Should().BeNull();
        updated.LastErrorAtUtc.Should().BeNull();
    }

    [Fact]
    public async Task ProcessExtractions_NoArchivesFound_MarksCompleted()
    {
        // Arrange
        using var dbContext = TestDbContextFactory.Create();
        dbContext.TrackedReleases.Add(EligibleRelease().Build());
        await dbContext.SaveChangesAsync();

        A.CallTo(() => _extractionService.ExtractAsync(A<string>._, A<CancellationToken>._))
            .Returns(new ExtractionResult(true, ArchivesExtracted: 0));

        var job = new JobDefinitionBuilder().WithName("test-job").Build();
        var handler = CreateHandler();

        // Act
        await handler.ExecuteAsync(job, dbContext, _extractionService, CancellationToken.None);

        // Assert
        var updated = dbContext.TrackedReleases.Single();
        updated.ExtractionStatus.Should().Be(ExtractionStatus.Completed);
        updated.ExtractionCompletedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task ProcessExtractions_ServiceReturnsFailure_MarksFailedWithError()
    {
        // Arrange
        using var dbContext = TestDbContextFactory.Create();
        dbContext.TrackedReleases.Add(EligibleRelease().Build());
        await dbContext.SaveChangesAsync();

        A.CallTo(() => _extractionService.ExtractAsync(A<string>._, A<CancellationToken>._))
            .Returns(new ExtractionResult(false, Error: "Archive volume files found without their first volume: show.r00"));

        var job = new JobDefinitionBuilder().WithName("test-job").Build();
        var handler = CreateHandler();

        // Act
        await handler.ExecuteAsync(job, dbContext, _extractionService, CancellationToken.None);

        // Assert
        var updated = dbContext.TrackedReleases.Single();
        updated.ExtractionStatus.Should().Be(ExtractionStatus.Failed);
        updated.ErrorCount.Should().Be(1);
        updated.LastError.Should().Contain("without their first volume");
        updated.LastErrorAtUtc.Should().NotBeNull();
        updated.ExtractionCompletedAtUtc.Should().BeNull();
    }

    [Fact]
    public async Task ProcessExtractions_ServiceThrows_MarksFailedWithError()
    {
        // Arrange
        using var dbContext = TestDbContextFactory.Create();
        dbContext.TrackedReleases.Add(EligibleRelease().Build());
        await dbContext.SaveChangesAsync();

        A.CallTo(() => _extractionService.ExtractAsync(A<string>._, A<CancellationToken>._))
            .Throws(new IOException("disk full"));

        var job = new JobDefinitionBuilder().WithName("test-job").Build();
        var handler = CreateHandler();

        // Act
        await handler.ExecuteAsync(job, dbContext, _extractionService, CancellationToken.None);

        // Assert
        var updated = dbContext.TrackedReleases.Single();
        updated.ExtractionStatus.Should().Be(ExtractionStatus.Failed);
        updated.ErrorCount.Should().Be(1);
        updated.LastError.Should().Be("disk full");
    }

    [Theory]
    [InlineData(TransferStatus.Pending, ExtractionStatus.Pending, ImportStatus.Pending)]
    [InlineData(TransferStatus.InProgress, ExtractionStatus.Pending, ImportStatus.Pending)]
    [InlineData(TransferStatus.Failed, ExtractionStatus.Pending, ImportStatus.Pending)]
    [InlineData(TransferStatus.Completed, ExtractionStatus.Completed, ImportStatus.Pending)]
    [InlineData(TransferStatus.Completed, ExtractionStatus.Failed, ImportStatus.Pending)]
    [InlineData(TransferStatus.Completed, ExtractionStatus.Pending, ImportStatus.Completed)]
    public async Task ProcessExtractions_IneligibleRelease_DoesNotCallService(
        TransferStatus transferStatus, ExtractionStatus extractionStatus, ImportStatus importStatus)
    {
        // Arrange
        using var dbContext = TestDbContextFactory.Create();
        var release = EligibleRelease()
            .WithTransferStatus(transferStatus)
            .WithExtractionStatus(extractionStatus)
            .WithImportStatus(importStatus)
            .Build();
        dbContext.TrackedReleases.Add(release);
        await dbContext.SaveChangesAsync();

        var job = new JobDefinitionBuilder().WithName("test-job").Build();
        var handler = CreateHandler();

        // Act
        await handler.ExecuteAsync(job, dbContext, _extractionService, CancellationToken.None);

        // Assert
        A.CallTo(() => _extractionService.ExtractAsync(A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();
        dbContext.TrackedReleases.Single().ExtractionStatus.Should().Be(extractionStatus);
    }

    [Fact]
    public async Task ProcessExtractions_DifferentJob_DoesNotCallService()
    {
        // Arrange
        using var dbContext = TestDbContextFactory.Create();
        dbContext.TrackedReleases.Add(EligibleRelease().WithJobName("other-job").Build());
        await dbContext.SaveChangesAsync();

        var job = new JobDefinitionBuilder().WithName("test-job").Build();
        var handler = CreateHandler();

        // Act
        await handler.ExecuteAsync(job, dbContext, _extractionService, CancellationToken.None);

        // Assert
        A.CallTo(() => _extractionService.ExtractAsync(A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task ProcessExtractions_ExtractionDisabled_MarksCompletedWithoutCallingService()
    {
        // Arrange
        using var dbContext = TestDbContextFactory.Create();
        dbContext.TrackedReleases.Add(EligibleRelease().Build());
        await dbContext.SaveChangesAsync();

        var job = new JobDefinitionBuilder()
            .WithName("test-job")
            .WithExtractionEnabled(false)
            .Build();
        var handler = CreateHandler();

        // Act
        await handler.ExecuteAsync(job, dbContext, _extractionService, CancellationToken.None);

        // Assert
        A.CallTo(() => _extractionService.ExtractAsync(A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();

        var updated = dbContext.TrackedReleases.Single();
        updated.ExtractionStatus.Should().Be(ExtractionStatus.Completed);
        updated.ExtractionCompletedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task ProcessExtractions_MultipleEligibleReleases_ProcessesAllInCreationOrder()
    {
        // Arrange
        using var dbContext = TestDbContextFactory.Create();
        var older = EligibleRelease()
            .WithDownloadId("OLDER1")
            .WithRemotePath("/downloads/older")
            .WithCreatedAtUtc(DateTime.UtcNow.AddHours(-2))
            .Build();
        var newer = EligibleRelease()
            .WithDownloadId("NEWER1")
            .WithRemotePath("/downloads/newer")
            .WithCreatedAtUtc(DateTime.UtcNow.AddHours(-1))
            .Build();
        dbContext.TrackedReleases.AddRange(newer, older);
        await dbContext.SaveChangesAsync();

        var extractedPaths = new List<string>();
        A.CallTo(() => _extractionService.ExtractAsync(A<string>._, A<CancellationToken>._))
            .Invokes((string path, CancellationToken _) => extractedPaths.Add(path))
            .Returns(new ExtractionResult(true));

        var job = new JobDefinitionBuilder().WithName("test-job").Build();
        var handler = CreateHandler();

        // Act
        await handler.ExecuteAsync(job, dbContext, _extractionService, CancellationToken.None);

        // Assert
        extractedPaths.Should().ContainInOrder(
            Path.Combine("/staging/test", "older"),
            Path.Combine("/staging/test", "newer"));
        dbContext.TrackedReleases.ToList().Should().AllSatisfy(
            r => r.ExtractionStatus.Should().Be(ExtractionStatus.Completed));
    }
}
