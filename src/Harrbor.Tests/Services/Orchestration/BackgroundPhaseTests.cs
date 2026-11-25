using FakeItEasy;
using FluentAssertions;
using Harrbor.Configuration;
using Harrbor.Data;
using Harrbor.Data.Entities;
using Harrbor.Services.Conversion;
using Harrbor.Services.Orchestration.Phases;
using Harrbor.Services.RemoteStorage;
using Harrbor.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace Harrbor.Tests.Services.Orchestration;

/// <summary>
/// Transfers and conversions run in the background, each with its own DbContext, so they neither share a context
/// between concurrent tasks nor hold up the job loop. Uses a real SQLite database, where sharing a context throws.
/// </summary>
public class BackgroundPhaseTests : IDisposable
{
    private readonly BackgroundTestServices _services = BackgroundTestServices.Sqlite();
    private readonly IRemoteStorageService _remote = A.Fake<IRemoteStorageService>();
    private readonly IConversionService _conversionService = A.Fake<IConversionService>();
    private readonly IMediaConverter _converter = A.Fake<IMediaConverter>();
    private readonly string _staging = Path.Combine(Path.GetTempPath(), $"harrbor-bg-{Guid.NewGuid():N}");
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    public BackgroundPhaseTests()
    {
        A.CallTo(() => _converter.Name).Returns("Fake");
        A.CallTo(() => _remote.GetItemKindAsync(A<string>._, A<CancellationToken>._))
            .ReturnsLazily(async () =>
            {
                await Task.Yield();
                return RemoteItemKind.Directory;
            });
    }

    public void Dispose()
    {
        _services.Dispose();
        if (Directory.Exists(_staging))
            Directory.Delete(_staging, recursive: true);
    }

    private TransferPhaseHandler TransferHandler() =>
        new(_services.Transfers, A.Fake<ILogger<TransferPhaseHandler>>());

    private ConversionPhaseHandler ConversionHandler() => new(
        _conversionService,
        new ConverterRegistry([_converter]),
        Options.Create(new ConversionOptions()),
        _services.Conversions,
        A.Fake<ILogger<ConversionPhaseHandler>>());

    private JobDefinition Job(params string[] converters) => new JobDefinitionBuilder()
        .WithName("test-job")
        .WithStagingPath(_staging)
        .WithTransferParallelism(2)
        .WithConverters(converters)
        .Build();

    private TrackedRelease PendingTransfer(string downloadId) => new TrackedReleaseBuilder()
        .WithDownloadId(downloadId)
        .WithName(downloadId)
        .WithJobName("test-job")
        .WithDownloadStatus(DownloadStatus.Completed)
        .WithTransferStatus(TransferStatus.Pending)
        .WithRemotePath($"/downloads/{downloadId}")
        .WithStagingPath(_staging)
        .Build();

    private TrackedRelease ReadyToConvert(string downloadId) => new TrackedReleaseBuilder()
        .WithDownloadId(downloadId)
        .WithName(downloadId)
        .WithJobName("test-job")
        .WithDownloadStatus(DownloadStatus.Completed)
        .WithTransferStatus(TransferStatus.Completed)
        .WithExtractionStatus(ExtractionStatus.Completed)
        .WithConversionStatus(ConversionStatus.Pending)
        .WithRemotePath($"/downloads/{downloadId}")
        .WithStagingPath(_staging)
        .Build();

    private async Task SeedAsync(params TrackedRelease[] releases)
    {
        await using var db = _services.CreateContext();
        db.TrackedReleases.AddRange(releases);
        await db.SaveChangesAsync(_ct);
    }

    private async Task<Dictionary<string, TrackedRelease>> LoadAsync()
    {
        await using var db = _services.CreateContext();
        return await db.TrackedReleases.AsNoTracking().ToDictionaryAsync(r => r.DownloadId, _ct);
    }

    /// <summary>A transfer that doesn't finish until <see cref="TaskCompletionSource{T}.SetResult"/> is called.</summary>
    private TaskCompletionSource<TransferResult> BlockTransfers()
    {
        var gate = new TaskCompletionSource<TransferResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        A.CallTo(() => _remote.TransferAsync(A<string>._, A<string>._, A<CancellationToken>._))
            .ReturnsLazily(call => gate.Task.WaitAsync(call.GetArgument<CancellationToken>(2)));
        return gate;
    }

    [Fact]
    public async Task TwoPendingTransfers_BothComplete_WithoutSharingAContext()
    {
        await SeedAsync(PendingTransfer("KUBO"), PendingTransfer("SUPERGIRL"));
        A.CallTo(() => _remote.TransferAsync(A<string>._, A<string>._, A<CancellationToken>._))
            .ReturnsLazily(async () =>
            {
                await Task.Delay(20);
                return new TransferResult(Success: true, BytesTransferred: 1);
            });

        await using (var db = _services.CreateContext())
            await TransferHandler().ExecuteAsync(Job(), db, _remote, _ct);
        await _services.Transfers.WhenIdleAsync();

        var releases = await LoadAsync();
        releases["KUBO"].TransferStatus.Should().Be(TransferStatus.Completed);
        releases["SUPERGIRL"].TransferStatus.Should().Be(TransferStatus.Completed);
        releases.Values.Should().OnlyContain(r => r.LastError == null);
        A.CallTo(() => _remote.TransferAsync(A<string>._, A<string>._, A<CancellationToken>._))
            .MustHaveHappenedTwiceExactly();
    }

    [Fact]
    public async Task TransferPhase_ReturnsWhileTransferRuns_AndLaterPhasesProceedInTheSameCycle()
    {
        await SeedAsync(PendingTransfer("SUPERGIRL"), ReadyToConvert("DUNE"));
        Directory.CreateDirectory(Path.Combine(_staging, ".harrbor", "DUNE"));
        var gate = BlockTransfers();
        A.CallTo(() => _conversionService.ProcessAsync(A<string>._, A<IReadOnlyList<IMediaConverter>>._, A<ConversionRunOptions>._, A<CancellationToken>._))
            .ReturnsLazily(async () =>
            {
                await Task.Yield();
                return new ConversionResult([], TimeSpan.Zero);
            });

        // One cycle, one context, as in OrchestrationWorker
        await using (var db = _services.CreateContext())
        {
            await TransferHandler().ExecuteAsync(Job("Fake"), db, _remote, _ct);
            (await LoadAsync())["SUPERGIRL"].TransferStatus.Should().Be(TransferStatus.InProgress);

            await ConversionHandler().ExecuteAsync(Job("Fake"), db, _ct);
            await _services.Conversions.WhenIdleAsync();
            await new PublishPhaseHandler(A.Fake<ILogger<PublishPhaseHandler>>()).ExecuteAsync(Job("Fake"), db, _ct);
        }

        var releases = await LoadAsync();
        releases["DUNE"].ConversionStatus.Should().Be(ConversionStatus.Completed);
        releases["DUNE"].PublishedAtUtc.Should().NotBeNull();
        Directory.Exists(Path.Combine(_staging, "DUNE")).Should().BeTrue();
        releases["SUPERGIRL"].TransferStatus.Should().Be(TransferStatus.InProgress, "its transfer is still running");

        gate.SetResult(new TransferResult(Success: true, BytesTransferred: 1));
        await _services.Transfers.WhenIdleAsync();
        (await LoadAsync())["SUPERGIRL"].TransferStatus.Should().Be(TransferStatus.Completed);
    }

    [Fact]
    public async Task RunningTransfer_IsNotStartedASecondTime()
    {
        await SeedAsync(PendingTransfer("SUPERGIRL"));
        var gate = BlockTransfers();

        await using (var db = _services.CreateContext())
            await TransferHandler().ExecuteAsync(Job(), db, _remote, _ct);

        // Even if the row looks startable again (e.g. reset by hand), the running task keeps it
        await using (var db = _services.CreateContext())
        {
            var release = await db.TrackedReleases.SingleAsync(_ct);
            release.TransferStatus = TransferStatus.Pending;
            await db.SaveChangesAsync(_ct);
            await TransferHandler().ExecuteAsync(Job(), db, _remote, _ct);
        }

        gate.SetResult(new TransferResult(Success: true, BytesTransferred: 1));
        await _services.Transfers.WhenIdleAsync();

        A.CallTo(() => _remote.TransferAsync(A<string>._, A<string>._, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
        (await LoadAsync())["SUPERGIRL"].TransferStatus.Should().Be(TransferStatus.Completed);
    }

    [Fact]
    public async Task ParallelismCountsRunningTransfers()
    {
        await SeedAsync(PendingTransfer("A"), PendingTransfer("B"), PendingTransfer("C"));
        var gate = BlockTransfers();

        for (var cycle = 0; cycle < 2; cycle++)
        {
            await using var db = _services.CreateContext();
            await TransferHandler().ExecuteAsync(Job(), db, _remote, _ct);
        }

        _services.Transfers.RunningCount.Should().Be(2);
        (await LoadAsync()).Values.Count(r => r.TransferStatus == TransferStatus.Pending).Should().Be(1);

        gate.SetResult(new TransferResult(Success: true, BytesTransferred: 1));
        await _services.Transfers.WhenIdleAsync();
    }

    [Fact]
    public async Task Cancellation_StopsRunningTransfer_AndLeavesItInProgress()
    {
        await SeedAsync(PendingTransfer("SUPERGIRL"));
        BlockTransfers();
        using var stopping = new CancellationTokenSource();

        await using (var db = _services.CreateContext())
            await TransferHandler().ExecuteAsync(Job(), db, _remote, stopping.Token);

        await stopping.CancelAsync();
        await _services.Transfers.WhenIdleAsync().WaitAsync(TimeSpan.FromSeconds(10), _ct);

        var release = (await LoadAsync())["SUPERGIRL"];
        release.TransferStatus.Should().Be(TransferStatus.InProgress, "the startup reset puts it back to Pending");
        release.ErrorCount.Should().Be(0);
    }

    [Fact]
    public async Task Conversion_OneAtATimeAcrossJobs_AndPhaseDoesNotWait()
    {
        await SeedAsync(ReadyToConvert("AAA"), ReadyToConvert("BBB"));
        var gate = new TaskCompletionSource<ConversionResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        A.CallTo(() => _conversionService.ProcessAsync(A<string>._, A<IReadOnlyList<IMediaConverter>>._, A<ConversionRunOptions>._, A<CancellationToken>._))
            .ReturnsLazily(() => gate.Task);

        for (var cycle = 0; cycle < 2; cycle++)
        {
            await using var db = _services.CreateContext();
            await ConversionHandler().ExecuteAsync(Job("Fake"), db, _ct);
        }

        var releases = await LoadAsync();
        releases.Values.Count(r => r.ConversionStatus == ConversionStatus.InProgress).Should().Be(1);
        releases.Values.Count(r => r.ConversionStatus == ConversionStatus.Pending).Should().Be(1);

        gate.SetResult(new ConversionResult([], TimeSpan.Zero));
        await _services.Conversions.WhenIdleAsync();
        A.CallTo(() => _conversionService.ProcessAsync(A<string>._, A<IReadOnlyList<IMediaConverter>>._, A<ConversionRunOptions>._, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Conversion_Cancelled_LeftInProgress()
    {
        await SeedAsync(ReadyToConvert("DUNE"));
        A.CallTo(() => _conversionService.ProcessAsync(A<string>._, A<IReadOnlyList<IMediaConverter>>._, A<ConversionRunOptions>._, A<CancellationToken>._))
            .ReturnsLazily(async call =>
            {
                await Task.Delay(Timeout.Infinite, call.GetArgument<CancellationToken>(3));
                return new ConversionResult([], TimeSpan.Zero);
            });
        using var stopping = new CancellationTokenSource();

        await using (var db = _services.CreateContext())
            await ConversionHandler().ExecuteAsync(Job("Fake"), db, stopping.Token);
        await stopping.CancelAsync();
        await _services.Conversions.WhenIdleAsync().WaitAsync(TimeSpan.FromSeconds(10), _ct);

        (await LoadAsync())["DUNE"].ConversionStatus.Should().Be(ConversionStatus.InProgress);
    }
}
