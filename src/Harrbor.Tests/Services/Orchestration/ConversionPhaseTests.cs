using FakeItEasy;
using FluentAssertions;
using Harrbor.Configuration;
using Harrbor.Data.Entities;
using Harrbor.Services.Conversion;
using Harrbor.Services.Orchestration.Phases;
using Harrbor.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace Harrbor.Tests.Services.Orchestration;

public class ConversionPhaseTests
{
    private readonly IConversionService _conversionService = A.Fake<IConversionService>();
    private readonly IMediaConverter _converter = A.Fake<IMediaConverter>();
    private readonly ConversionOptions _options = new() { MinFileSizeBytes = 123 };

    public ConversionPhaseTests()
    {
        A.CallTo(() => _converter.Name).Returns("Fake");
    }

    private ConversionPhaseHandler CreateHandler() => new(
        _conversionService,
        new ConverterRegistry([_converter]),
        Options.Create(_options),
        A.Fake<ILogger<ConversionPhaseHandler>>());

    private static TrackedRelease ReadyRelease(string downloadId = "ABC", bool usesHoldingPath = true) =>
        new TrackedReleaseBuilder()
            .WithDownloadId(downloadId)
            .WithJobName("test-job")
            .WithRemotePath($"/downloads/{downloadId}.Release")
            .WithStagingPath("/staging")
            .WithTransferStatus(TransferStatus.Completed)
            .WithExtractionStatus(ExtractionStatus.Completed)
            .WithConversionStatus(ConversionStatus.Pending)
            .WithUsesHoldingPath(usesHoldingPath)
            .Build();

    private static JobDefinition Job(params string[] converters) =>
        new JobDefinitionBuilder().WithName("test-job").WithConverters(converters).Build();

    private static ConversionResult Result(params ConversionOutcome[] outcomes) => new(outcomes, TimeSpan.FromSeconds(1));

    [Fact]
    public async Task NoConvertersConfigured_Skipped()
    {
        using var db = TestDbContextFactory.Create();
        db.TrackedReleases.Add(ReadyRelease());
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        await CreateHandler().ExecuteAsync(Job(), db, TestContext.Current.CancellationToken);

        var release = await db.TrackedReleases.SingleAsync(TestContext.Current.CancellationToken);
        release.ConversionStatus.Should().Be(ConversionStatus.Skipped);
        A.CallTo(_conversionService).MustNotHaveHappened();
    }

    [Fact]
    public async Task LegacyRelease_Skipped()
    {
        using var db = TestDbContextFactory.Create();
        db.TrackedReleases.Add(ReadyRelease(usesHoldingPath: false));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        await CreateHandler().ExecuteAsync(Job("Fake"), db, TestContext.Current.CancellationToken);

        (await db.TrackedReleases.SingleAsync(TestContext.Current.CancellationToken))
            .ConversionStatus.Should().Be(ConversionStatus.Skipped);
        A.CallTo(_conversionService).MustNotHaveHappened();
    }

    [Fact]
    public async Task Success_CompletedWithSummary_UsesHoldingPath()
    {
        using var db = TestDbContextFactory.Create();
        db.TrackedReleases.Add(ReadyRelease());
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        A.CallTo(() => _conversionService.ProcessAsync(A<string>._, A<IReadOnlyList<IMediaConverter>>._, A<ConversionRunOptions>._, A<CancellationToken>._))
            .Returns(Result(new ConversionOutcome("/x.mkv", "Fake", ConversionOutcomeKind.Converted)));

        await CreateHandler().ExecuteAsync(Job("Fake"), db, TestContext.Current.CancellationToken);

        var release = await db.TrackedReleases.SingleAsync(TestContext.Current.CancellationToken);
        release.ConversionStatus.Should().Be(ConversionStatus.Completed);
        release.FilesConverted.Should().Be(1);
        release.ConversionSummary.Should().Be("Fake: 1 converted");
        release.ConversionStartedAtUtc.Should().NotBeNull();
        release.ConversionCompletedAtUtc.Should().NotBeNull();
        A.CallTo(() => _conversionService.ProcessAsync(
                Path.Combine("/staging", ".harrbor", "ABC.Release"),
                A<IReadOnlyList<IMediaConverter>>.That.Matches(c => c.Single() == _converter),
                new ConversionRunOptions(false, 123),
                A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task ConverterFailure_FailedAndStillReachesPublish()
    {
        using var db = TestDbContextFactory.Create();
        db.TrackedReleases.Add(ReadyRelease());
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        A.CallTo(() => _conversionService.ProcessAsync(A<string>._, A<IReadOnlyList<IMediaConverter>>._, A<ConversionRunOptions>._, A<CancellationToken>._))
            .Returns(Result(new ConversionOutcome("/x.mkv", "Fake", ConversionOutcomeKind.Failed, "bad stream")));

        await CreateHandler().ExecuteAsync(Job("Fake"), db, TestContext.Current.CancellationToken);

        var release = await db.TrackedReleases.SingleAsync(TestContext.Current.CancellationToken);
        release.ConversionStatus.Should().Be(ConversionStatus.Failed);
        release.ErrorCount.Should().Be(1);
        release.LastError.Should().Contain("bad stream");

        // Publish picks up failed conversions (the unconverted file is still importable)
        var staging = Path.Combine(Path.GetTempPath(), $"harrbor-pub-{Guid.NewGuid():N}");
        try
        {
            release.StagingPath = staging;
            Directory.CreateDirectory(release.GetHoldingItemPath());
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);

            await new PublishPhaseHandler(A.Fake<ILogger<PublishPhaseHandler>>())
                .ExecuteAsync(Job("Fake"), db, TestContext.Current.CancellationToken);

            release.PublishedAtUtc.Should().NotBeNull();
            Directory.Exists(release.GetFinalItemPath()).Should().BeTrue();
        }
        finally
        {
            Directory.Delete(staging, recursive: true);
        }
    }

    [Fact]
    public async Task ServiceThrows_Failed()
    {
        using var db = TestDbContextFactory.Create();
        db.TrackedReleases.Add(ReadyRelease());
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        A.CallTo(() => _conversionService.ProcessAsync(A<string>._, A<IReadOnlyList<IMediaConverter>>._, A<ConversionRunOptions>._, A<CancellationToken>._))
            .Throws(new FileNotFoundException("item missing"));

        await CreateHandler().ExecuteAsync(Job("Fake"), db, TestContext.Current.CancellationToken);

        var release = await db.TrackedReleases.SingleAsync(TestContext.Current.CancellationToken);
        release.ConversionStatus.Should().Be(ConversionStatus.Failed);
        release.LastError.Should().Be("item missing");
    }

    [Fact]
    public async Task ConvertsAtMostOneReleasePerCycle()
    {
        using var db = TestDbContextFactory.Create();
        var first = ReadyRelease("AAA");
        first.CreatedAtUtc = DateTime.UtcNow.AddMinutes(-10);
        db.TrackedReleases.AddRange(first, ReadyRelease("BBB"));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        A.CallTo(() => _conversionService.ProcessAsync(A<string>._, A<IReadOnlyList<IMediaConverter>>._, A<ConversionRunOptions>._, A<CancellationToken>._))
            .Returns(Result());

        await CreateHandler().ExecuteAsync(Job("Fake"), db, TestContext.Current.CancellationToken);

        var releases = await db.TrackedReleases.ToDictionaryAsync(r => r.DownloadId, TestContext.Current.CancellationToken);
        releases["AAA"].ConversionStatus.Should().Be(ConversionStatus.Completed);
        releases["BBB"].ConversionStatus.Should().Be(ConversionStatus.Pending);
    }

    [Theory]
    [InlineData(TransferStatus.Completed, ExtractionStatus.Pending, ConversionStatus.Pending, ImportStatus.Pending)]
    [InlineData(TransferStatus.Completed, ExtractionStatus.Failed, ConversionStatus.Pending, ImportStatus.Pending)]
    [InlineData(TransferStatus.InProgress, ExtractionStatus.Pending, ConversionStatus.Pending, ImportStatus.Pending)]
    [InlineData(TransferStatus.Completed, ExtractionStatus.Completed, ConversionStatus.Skipped, ImportStatus.Pending)]
    [InlineData(TransferStatus.Completed, ExtractionStatus.Completed, ConversionStatus.Pending, ImportStatus.Completed)]
    public async Task NotReady_Ignored(TransferStatus transfer, ExtractionStatus extraction, ConversionStatus conversion, ImportStatus import)
    {
        using var db = TestDbContextFactory.Create();
        var release = ReadyRelease();
        release.TransferStatus = transfer;
        release.ExtractionStatus = extraction;
        release.ConversionStatus = conversion;
        release.ImportStatus = import;
        db.TrackedReleases.Add(release);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        await CreateHandler().ExecuteAsync(Job(), db, TestContext.Current.CancellationToken);

        (await db.TrackedReleases.SingleAsync(TestContext.Current.CancellationToken)).ConversionStatus.Should().Be(conversion);
    }
}
