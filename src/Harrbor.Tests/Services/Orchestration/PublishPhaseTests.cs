using FakeItEasy;
using FluentAssertions;
using Harrbor.Configuration;
using Harrbor.Data.Entities;
using Harrbor.Services.Orchestration.Phases;
using Harrbor.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Harrbor.Tests.Services.Orchestration;

public class PublishPhaseTests : IDisposable
{
    private readonly string _staging = Path.Combine(Path.GetTempPath(), $"harrbor-publish-{Guid.NewGuid():N}");
    private readonly JobDefinition _job = new JobDefinitionBuilder().WithName("test-job").Build();

    public PublishPhaseTests()
    {
        Directory.CreateDirectory(Path.Combine(_staging, ".harrbor"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_staging))
            Directory.Delete(_staging, recursive: true);
    }

    private static PublishPhaseHandler CreateHandler() => new(A.Fake<ILogger<PublishPhaseHandler>>());

    private TrackedRelease Release(string name, ConversionStatus conversion = ConversionStatus.Completed) =>
        new TrackedReleaseBuilder()
            .WithDownloadId(name)
            .WithJobName("test-job")
            .WithRemotePath($"/downloads/{name}")
            .WithStagingPath(_staging)
            .WithTransferStatus(TransferStatus.Completed)
            .WithExtractionStatus(ExtractionStatus.Completed)
            .WithConversionStatus(conversion)
            .Build();

    [Fact]
    public async Task MovesDirectoryItem()
    {
        using var db = TestDbContextFactory.Create();
        var release = Release("Show.S01");
        Directory.CreateDirectory(Path.Combine(release.GetHoldingItemPath(), "Subs"));
        File.WriteAllText(Path.Combine(release.GetHoldingItemPath(), "ep1.mkv"), "video");
        db.TrackedReleases.Add(release);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        await CreateHandler().ExecuteAsync(_job, db, TestContext.Current.CancellationToken);

        var updated = await db.TrackedReleases.SingleAsync(TestContext.Current.CancellationToken);
        updated.PublishedAtUtc.Should().NotBeNull();
        File.ReadAllText(Path.Combine(_staging, "Show.S01", "ep1.mkv")).Should().Be("video");
        Directory.Exists(Path.Combine(_staging, "Show.S01", "Subs")).Should().BeTrue();
        Directory.Exists(updated.GetHoldingItemPath()).Should().BeFalse();
        updated.GetWorkingItemPath().Should().Be(updated.GetFinalItemPath());
    }

    [Theory]
    [InlineData(ConversionStatus.Completed)]
    [InlineData(ConversionStatus.Failed)]
    [InlineData(ConversionStatus.Skipped)]
    public async Task MovesFileItem_ForAnyFinishedConversionStatus(ConversionStatus status)
    {
        using var db = TestDbContextFactory.Create();
        var release = Release("Movie.2024.mkv", status);
        File.WriteAllText(release.GetHoldingItemPath(), "video");
        db.TrackedReleases.Add(release);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        await CreateHandler().ExecuteAsync(_job, db, TestContext.Current.CancellationToken);

        (await db.TrackedReleases.SingleAsync(TestContext.Current.CancellationToken)).PublishedAtUtc.Should().NotBeNull();
        File.ReadAllText(Path.Combine(_staging, "Movie.2024.mkv")).Should().Be("video");
        File.Exists(release.GetHoldingItemPath()).Should().BeFalse();
    }

    [Fact]
    public async Task SingleFileInRootFolder_PublishesTheRootFolder()
    {
        // Dune: RemotePath is the file, the transfer item is its root folder
        using var db = TestDbContextFactory.Create();
        var release = Release("Dune");
        release.RemotePath = "/downloads/Dune/Dune.mkv";
        release.RemoteItemPath = "/downloads/Dune";
        Directory.CreateDirectory(release.GetHoldingItemPath());
        File.WriteAllText(Path.Combine(release.GetHoldingItemPath(), "Dune.mkv"), "video");
        db.TrackedReleases.Add(release);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        await CreateHandler().ExecuteAsync(_job, db, TestContext.Current.CancellationToken);

        (await db.TrackedReleases.SingleAsync(TestContext.Current.CancellationToken)).PublishedAtUtc.Should().NotBeNull();
        File.ReadAllText(Path.Combine(_staging, "Dune", "Dune.mkv")).Should().Be("video");
    }

    [Fact]
    public async Task RefusesExistingDestination()
    {
        using var db = TestDbContextFactory.Create();
        var release = Release("Movie");
        Directory.CreateDirectory(release.GetHoldingItemPath());
        File.WriteAllText(Path.Combine(release.GetHoldingItemPath(), "new.mkv"), "new");
        Directory.CreateDirectory(release.GetFinalItemPath());
        File.WriteAllText(Path.Combine(release.GetFinalItemPath(), "old.mkv"), "old");
        db.TrackedReleases.Add(release);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        await CreateHandler().ExecuteAsync(_job, db, TestContext.Current.CancellationToken);
        await CreateHandler().ExecuteAsync(_job, db, TestContext.Current.CancellationToken);

        var updated = await db.TrackedReleases.SingleAsync(TestContext.Current.CancellationToken);
        updated.PublishedAtUtc.Should().BeNull();
        updated.LastError.Should().Contain("destination already exists");
        updated.ErrorCount.Should().Be(1, "a repeated identical error is not counted again");
        File.Exists(Path.Combine(release.GetHoldingItemPath(), "new.mkv")).Should().BeTrue();
        File.Exists(Path.Combine(release.GetFinalItemPath(), "new.mkv")).Should().BeFalse();
        File.Exists(Path.Combine(release.GetFinalItemPath(), "old.mkv")).Should().BeTrue();
    }

    [Fact]
    public async Task AlreadyMovedOnPreviousRun_MarksPublished()
    {
        using var db = TestDbContextFactory.Create();
        var release = Release("Movie");
        Directory.CreateDirectory(release.GetFinalItemPath());
        db.TrackedReleases.Add(release);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        await CreateHandler().ExecuteAsync(_job, db, TestContext.Current.CancellationToken);

        (await db.TrackedReleases.SingleAsync(TestContext.Current.CancellationToken)).PublishedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task MissingHoldingItem_RecordsError()
    {
        using var db = TestDbContextFactory.Create();
        db.TrackedReleases.Add(Release("Gone"));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        await CreateHandler().ExecuteAsync(_job, db, TestContext.Current.CancellationToken);

        var updated = await db.TrackedReleases.SingleAsync(TestContext.Current.CancellationToken);
        updated.PublishedAtUtc.Should().BeNull();
        updated.LastError.Should().Contain("Holding item not found");
    }

    public static TheoryData<string> NotReadyCases => ["conversion-pending", "conversion-inprogress", "extraction-pending", "already-published", "other-job"];

    [Theory]
    [MemberData(nameof(NotReadyCases))]
    public async Task IgnoresRowsThatAreNotReady(string scenario)
    {
        using var db = TestDbContextFactory.Create();
        var release = Release("Movie");
        switch (scenario)
        {
            case "conversion-pending": release.ConversionStatus = ConversionStatus.Pending; break;
            case "conversion-inprogress": release.ConversionStatus = ConversionStatus.InProgress; break;
            case "extraction-pending": release.ExtractionStatus = ExtractionStatus.Pending; break;
            case "already-published": release.PublishedAtUtc = DateTime.UtcNow.AddHours(-1); break;
            case "other-job": release.JobName = "other"; break;
        }
        var publishedBefore = release.PublishedAtUtc;
        Directory.CreateDirectory(release.GetHoldingItemPath());
        db.TrackedReleases.Add(release);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        await CreateHandler().ExecuteAsync(_job, db, TestContext.Current.CancellationToken);

        var updated = await db.TrackedReleases.SingleAsync(TestContext.Current.CancellationToken);
        updated.PublishedAtUtc.Should().Be(publishedBefore);
        Directory.Exists(release.GetHoldingItemPath()).Should().BeTrue();
        Directory.Exists(release.GetFinalItemPath()).Should().BeFalse();
    }
}
