using FluentAssertions;
using Harrbor.Configuration;
using Harrbor.Data.Entities;
using Harrbor.Services.Orchestration.Phases;
using Harrbor.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using FakeItEasy;
using Xunit;

namespace Harrbor.Tests.Services.Orchestration;

/// <summary>
/// Tests for the Cleanup phase of orchestration.
/// The Cleanup phase deletes files from staging after confirmed import.
/// </summary>
public class CleanupPhaseTests : IDisposable
{
    private readonly string _tempDir;
    private readonly ILogger<CleanupPhaseHandler> _logger;

    public CleanupPhaseTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"harrbor-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);
        _logger = A.Fake<ILogger<CleanupPhaseHandler>>();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    private CleanupPhaseHandler CreateHandler()
    {
        return new CleanupPhaseHandler(_logger);
    }

    [Fact]
    public async Task ProcessCleanup_DirectoryExists_Deletes()
    {
        // Arrange
        var stagingPath = _tempDir;
        var releaseDir = Path.Combine(stagingPath, "Test.Release.S01E01");
        Directory.CreateDirectory(releaseDir);
        File.WriteAllText(Path.Combine(releaseDir, "video.mkv"), "test content");

        using var dbContext = TestDbContextFactory.Create();
        var release = new TrackedReleaseBuilder()
            .WithDownloadId("ABC123")
            .WithJobName("test-job")
            .WithImportStatus(ImportStatus.Completed)
            .WithCleanupStatus(CleanupStatus.Pending)
            .WithRemotePath("/downloads/Test.Release.S01E01")
            .WithStagingPath(stagingPath)
            .Build();
        dbContext.TrackedReleases.Add(release);
        await dbContext.SaveChangesAsync();

        var job = new JobDefinitionBuilder().WithName("test-job").Build();
        var handler = CreateHandler();

        // Act
        await handler.ExecuteAsync(job, dbContext, CancellationToken.None);

        // Assert
        var updatedRelease = await dbContext.TrackedReleases.FirstAsync();
        updatedRelease.CleanupStatus.Should().Be(CleanupStatus.Completed);
        updatedRelease.CleanupCompletedAtUtc.Should().NotBeNull();
        Directory.Exists(releaseDir).Should().BeFalse();
    }

    [Fact]
    public async Task ProcessCleanup_FileExists_Deletes()
    {
        // Arrange
        var stagingPath = _tempDir;
        var releaseFile = Path.Combine(stagingPath, "Test.Movie.2024.mkv");
        File.WriteAllText(releaseFile, "test content");

        using var dbContext = TestDbContextFactory.Create();
        var release = new TrackedReleaseBuilder()
            .WithDownloadId("ABC123")
            .WithJobName("test-job")
            .WithImportStatus(ImportStatus.Completed)
            .WithCleanupStatus(CleanupStatus.Pending)
            .WithRemotePath("/downloads/Test.Movie.2024.mkv")
            .WithStagingPath(stagingPath)
            .Build();
        dbContext.TrackedReleases.Add(release);
        await dbContext.SaveChangesAsync();

        var job = new JobDefinitionBuilder().WithName("test-job").Build();
        var handler = CreateHandler();

        // Act
        await handler.ExecuteAsync(job, dbContext, CancellationToken.None);

        // Assert
        var updatedRelease = await dbContext.TrackedReleases.FirstAsync();
        updatedRelease.CleanupStatus.Should().Be(CleanupStatus.Completed);
        File.Exists(releaseFile).Should().BeFalse();
    }

    [Fact]
    public async Task ProcessCleanup_NeitherExists_MarksCompleted()
    {
        // Arrange
        var stagingPath = _tempDir;

        using var dbContext = TestDbContextFactory.Create();
        var release = new TrackedReleaseBuilder()
            .WithDownloadId("ABC123")
            .WithJobName("test-job")
            .WithImportStatus(ImportStatus.Completed)
            .WithCleanupStatus(CleanupStatus.Pending)
            .WithRemotePath("/downloads/Nonexistent.Release")
            .WithStagingPath(stagingPath)
            .Build();
        dbContext.TrackedReleases.Add(release);
        await dbContext.SaveChangesAsync();

        var job = new JobDefinitionBuilder().WithName("test-job").Build();
        var handler = CreateHandler();

        // Act
        await handler.ExecuteAsync(job, dbContext, CancellationToken.None);

        // Assert
        var updatedRelease = await dbContext.TrackedReleases.FirstAsync();
        updatedRelease.CleanupStatus.Should().Be(CleanupStatus.Completed);
        updatedRelease.CleanupCompletedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task ProcessCleanup_RequiresImportCompleted()
    {
        // Arrange
        using var dbContext = TestDbContextFactory.Create();
        var pendingImportRelease = new TrackedReleaseBuilder()
            .WithDownloadId("PENDING_IMPORT")
            .WithJobName("test-job")
            .WithImportStatus(ImportStatus.Pending)
            .WithCleanupStatus(CleanupStatus.Pending)
            .Build();
        dbContext.TrackedReleases.Add(pendingImportRelease);
        await dbContext.SaveChangesAsync();

        var job = new JobDefinitionBuilder().WithName("test-job").Build();
        var handler = CreateHandler();

        // Act
        await handler.ExecuteAsync(job, dbContext, CancellationToken.None);

        // Assert
        var updatedRelease = await dbContext.TrackedReleases.FirstAsync();
        updatedRelease.CleanupStatus.Should().Be(CleanupStatus.Pending);
    }

    [Fact]
    public async Task ProcessCleanup_RecursiveDirectoryDeletion()
    {
        // Arrange
        var stagingPath = _tempDir;
        var releaseDir = Path.Combine(stagingPath, "Test.Release");
        var subDir = Path.Combine(releaseDir, "Subs");
        Directory.CreateDirectory(subDir);
        File.WriteAllText(Path.Combine(releaseDir, "video.mkv"), "content");
        File.WriteAllText(Path.Combine(subDir, "english.srt"), "subs");

        using var dbContext = TestDbContextFactory.Create();
        var release = new TrackedReleaseBuilder()
            .WithDownloadId("ABC123")
            .WithJobName("test-job")
            .WithImportStatus(ImportStatus.Completed)
            .WithCleanupStatus(CleanupStatus.Pending)
            .WithRemotePath("/downloads/Test.Release")
            .WithStagingPath(stagingPath)
            .Build();
        dbContext.TrackedReleases.Add(release);
        await dbContext.SaveChangesAsync();

        var job = new JobDefinitionBuilder().WithName("test-job").Build();
        var handler = CreateHandler();

        // Act
        await handler.ExecuteAsync(job, dbContext, CancellationToken.None);

        // Assert
        Directory.Exists(releaseDir).Should().BeFalse();
    }

    [Fact]
    public async Task ProcessCleanup_OnlyProcessesPendingCleanup()
    {
        // Arrange
        var stagingPath = _tempDir;

        using var dbContext = TestDbContextFactory.Create();
        var pendingCleanup = new TrackedReleaseBuilder()
            .WithDownloadId("PENDING1")
            .WithJobName("test-job")
            .WithImportStatus(ImportStatus.Completed)
            .WithCleanupStatus(CleanupStatus.Pending)
            .WithStagingPath(stagingPath)
            .WithRemotePath("/downloads/release1")
            .Build();
        var completedCleanup = new TrackedReleaseBuilder()
            .WithDownloadId("COMPLETED1")
            .WithJobName("test-job")
            .WithImportStatus(ImportStatus.Completed)
            .WithCleanupStatus(CleanupStatus.Completed)
            .Build();
        dbContext.TrackedReleases.AddRange(pendingCleanup, completedCleanup);
        await dbContext.SaveChangesAsync();

        var job = new JobDefinitionBuilder().WithName("test-job").Build();
        var handler = CreateHandler();

        // Act
        await handler.ExecuteAsync(job, dbContext, CancellationToken.None);

        // Assert
        var releases = await dbContext.TrackedReleases.ToListAsync();
        releases.First(r => r.DownloadId == "PENDING1").CleanupStatus.Should().Be(CleanupStatus.Completed);
        releases.First(r => r.DownloadId == "COMPLETED1").CleanupCompletedAtUtc.Should().BeNull();
    }
}
