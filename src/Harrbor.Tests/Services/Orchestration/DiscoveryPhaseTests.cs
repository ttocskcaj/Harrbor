using FakeItEasy;
using FluentAssertions;
using Harrbor.Configuration;
using Harrbor.Data.Entities;
using Harrbor.Services;
using Harrbor.Services.Clients;
using Harrbor.Services.Clients.Models;
using Harrbor.Services.Orchestration.Phases;
using Harrbor.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Harrbor.Tests.Services.Orchestration;

/// <summary>
/// Tests for the Discovery phase of orchestration.
/// The Discovery phase queries Sonarr/Radarr queue and creates TrackedRelease records.
/// </summary>
public class DiscoveryPhaseTests
{
    private readonly IMediaServiceResolver _mediaServiceResolver;
    private readonly IMediaService _mediaService;
    private readonly ILogger<DiscoveryPhaseHandler> _logger;

    public DiscoveryPhaseTests()
    {
        _mediaService = A.Fake<IMediaService>();
        _mediaServiceResolver = A.Fake<IMediaServiceResolver>();
        _logger = A.Fake<ILogger<DiscoveryPhaseHandler>>();

        A.CallTo(() => _mediaServiceResolver.GetServiceForJob(A<JobDefinition>._))
            .Returns(_mediaService);
    }

    private DiscoveryPhaseHandler CreateHandler()
    {
        return new DiscoveryPhaseHandler(_mediaServiceResolver, _logger);
    }

    [Fact]
    public async Task DiscoverReleases_NewQueueItems_CreatesTrackedReleases()
    {
        // Arrange
        using var dbContext = TestDbContextFactory.Create();
        var job = new JobDefinitionBuilder()
            .WithName("test-job")
            .WithRemotePath("/downloads")
            .WithStagingPath("/staging")
            .Build();

        var queueItems = new List<QueueItem>
        {
            new QueueItemBuilder().WithDownloadId("ABC123").WithTitle("Release 1").WithOutputPath("/downloads/release1").Build(),
            new QueueItemBuilder().WithDownloadId("DEF456").WithTitle("Release 2").WithOutputPath("/downloads/release2").Build()
        };

        A.CallTo(() => _mediaService.GetQueueAsync(A<CancellationToken>._))
            .Returns(queueItems);

        var handler = CreateHandler();

        // Act
        await handler.ExecuteAsync(job, dbContext, _mediaService, CancellationToken.None);

        // Assert
        var trackedReleases = await dbContext.TrackedReleases.ToListAsync();
        trackedReleases.Should().HaveCount(2);
        trackedReleases.Should().Contain(r => r.DownloadId == "ABC123" && r.Name == "Release 1");
        trackedReleases.Should().Contain(r => r.DownloadId == "DEF456" && r.Name == "Release 2");
        trackedReleases.Should().AllSatisfy(r =>
        {
            r.JobName.Should().Be("test-job");
            r.DownloadStatus.Should().Be(DownloadStatus.Pending);
            r.TransferStatus.Should().Be(TransferStatus.Pending);
            r.ImportStatus.Should().Be(ImportStatus.Pending);
        });
    }

    [Fact]
    public async Task DiscoverReleases_ExistingRelease_DoesNotDuplicate()
    {
        // Arrange
        using var dbContext = TestDbContextFactory.Create();
        var existingRelease = new TrackedReleaseBuilder()
            .WithDownloadId("ABC123")
            .WithName("Existing Release")
            .WithJobName("test-job")
            .Build();
        dbContext.TrackedReleases.Add(existingRelease);
        await dbContext.SaveChangesAsync();

        var job = new JobDefinitionBuilder()
            .WithName("test-job")
            .WithRemotePath("/downloads")
            .WithStagingPath("/staging")
            .Build();

        var queueItems = new List<QueueItem>
        {
            new QueueItemBuilder().WithDownloadId("ABC123").WithTitle("Release 1").Build(),
            new QueueItemBuilder().WithDownloadId("DEF456").WithTitle("Release 2").Build()
        };

        A.CallTo(() => _mediaService.GetQueueAsync(A<CancellationToken>._))
            .Returns(queueItems);

        var handler = CreateHandler();

        // Act
        await handler.ExecuteAsync(job, dbContext, _mediaService, CancellationToken.None);

        // Assert
        var trackedReleases = await dbContext.TrackedReleases.ToListAsync();
        trackedReleases.Should().HaveCount(2); // Original + 1 new
        trackedReleases.Count(r => r.DownloadId == "ABC123").Should().Be(1);
    }

    [Fact]
    public async Task DiscoverReleases_SeasonPack_DeduplicatesByDownloadId()
    {
        // Arrange
        using var dbContext = TestDbContextFactory.Create();
        var job = new JobDefinitionBuilder()
            .WithName("test-job")
            .WithRemotePath("/downloads")
            .WithStagingPath("/staging")
            .Build();

        // Season pack appears multiple times in queue (one per episode)
        var queueItems = new List<QueueItem>
        {
            new QueueItemBuilder().WithDownloadId("SEASON123").WithTitle("Show S01E01").Build(),
            new QueueItemBuilder().WithDownloadId("SEASON123").WithTitle("Show S01E02").Build(),
            new QueueItemBuilder().WithDownloadId("SEASON123").WithTitle("Show S01E03").Build()
        };

        A.CallTo(() => _mediaService.GetQueueAsync(A<CancellationToken>._))
            .Returns(queueItems);

        var handler = CreateHandler();

        // Act
        await handler.ExecuteAsync(job, dbContext, _mediaService, CancellationToken.None);

        // Assert
        var trackedReleases = await dbContext.TrackedReleases.ToListAsync();
        trackedReleases.Should().HaveCount(1);
        trackedReleases[0].DownloadId.Should().Be("SEASON123");
    }

    [Fact]
    public async Task DiscoverReleases_EmptyQueue_NoReleasesCreated()
    {
        // Arrange
        using var dbContext = TestDbContextFactory.Create();
        var job = new JobDefinitionBuilder()
            .WithName("test-job")
            .Build();

        A.CallTo(() => _mediaService.GetQueueAsync(A<CancellationToken>._))
            .Returns(new List<QueueItem>());

        var handler = CreateHandler();

        // Act
        await handler.ExecuteAsync(job, dbContext, _mediaService, CancellationToken.None);

        // Assert
        var trackedReleases = await dbContext.TrackedReleases.ToListAsync();
        trackedReleases.Should().BeEmpty();
    }

    [Fact]
    public async Task DiscoverReleases_UsesQueueItemOutputPath_WhenAvailable()
    {
        // Arrange
        using var dbContext = TestDbContextFactory.Create();
        var job = new JobDefinitionBuilder()
            .WithName("test-job")
            .WithRemotePath("/default/path")
            .WithStagingPath("/staging")
            .Build();

        var queueItems = new List<QueueItem>
        {
            new QueueItemBuilder()
                .WithDownloadId("ABC123")
                .WithTitle("Release 1")
                .WithOutputPath("/custom/output/path")
                .Build()
        };

        A.CallTo(() => _mediaService.GetQueueAsync(A<CancellationToken>._))
            .Returns(queueItems);

        var handler = CreateHandler();

        // Act
        await handler.ExecuteAsync(job, dbContext, _mediaService, CancellationToken.None);

        // Assert
        var release = await dbContext.TrackedReleases.FirstAsync();
        release.RemotePath.Should().Be("/custom/output/path");
    }

    [Fact]
    public async Task DiscoverReleases_FallsBackToJobRemotePath_WhenOutputPathIsNull()
    {
        // Arrange
        using var dbContext = TestDbContextFactory.Create();
        var job = new JobDefinitionBuilder()
            .WithName("test-job")
            .WithRemotePath("/default/path")
            .WithStagingPath("/staging")
            .Build();

        var queueItems = new List<QueueItem>
        {
            new QueueItemBuilder()
                .WithDownloadId("ABC123")
                .WithTitle("Release 1")
                .WithOutputPath(null)
                .Build()
        };

        A.CallTo(() => _mediaService.GetQueueAsync(A<CancellationToken>._))
            .Returns(queueItems);

        var handler = CreateHandler();

        // Act
        await handler.ExecuteAsync(job, dbContext, _mediaService, CancellationToken.None);

        // Assert
        var release = await dbContext.TrackedReleases.FirstAsync();
        release.RemotePath.Should().Be("/default/path");
    }
}
