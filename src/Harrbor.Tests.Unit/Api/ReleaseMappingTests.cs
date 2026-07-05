using FluentAssertions;
using Harrbor.Api.Releases;
using Harrbor.Data.Entities;
using Harrbor.Tests.Common;
using Xunit;

namespace Harrbor.Tests.Unit.Api;

/// <summary>
/// Unit tests for the pure release projection: current phase, status, timeline, and
/// retry-timing derivation from a <see cref="TrackedRelease"/>'s status enums.
/// </summary>
public class ReleaseMappingTests
{
    private static readonly DateTime Now = new(2026, 7, 4, 12, 0, 0, DateTimeKind.Utc);

    private static Job JobEntity(int maxRetries = 3, TimeSpan? retryDelay = null, TimeSpan? importTimeout = null) => new()
    {
        Name = "test-job",
        RemotePath = "/r",
        StagingPath = "/s",
        Category = "c",
        MaxTransferRetries = maxRetries,
        TransferRetryDelay = retryDelay ?? TimeSpan.FromMinutes(5),
        ImportTimeout = importTimeout ?? TimeSpan.FromHours(24)
    };

    [Theory]
    [InlineData(DownloadStatus.Pending, TransferStatus.Pending, ImportStatus.Pending, CleanupStatus.Pending, ArchivalStatus.Pending, ReleasePhase.Download)]
    [InlineData(DownloadStatus.Completed, TransferStatus.Pending, ImportStatus.Pending, CleanupStatus.Pending, ArchivalStatus.Pending, ReleasePhase.Transfer)]
    [InlineData(DownloadStatus.Completed, TransferStatus.InProgress, ImportStatus.Pending, CleanupStatus.Pending, ArchivalStatus.Pending, ReleasePhase.Transfer)]
    [InlineData(DownloadStatus.Completed, TransferStatus.Completed, ImportStatus.Pending, CleanupStatus.Pending, ArchivalStatus.Pending, ReleasePhase.Import)]
    [InlineData(DownloadStatus.Completed, TransferStatus.Completed, ImportStatus.Completed, CleanupStatus.Pending, ArchivalStatus.Pending, ReleasePhase.Cleanup)]
    [InlineData(DownloadStatus.Completed, TransferStatus.Completed, ImportStatus.Completed, CleanupStatus.Completed, ArchivalStatus.Pending, ReleasePhase.Archival)]
    [InlineData(DownloadStatus.Completed, TransferStatus.Completed, ImportStatus.Completed, CleanupStatus.Completed, ArchivalStatus.Completed, ReleasePhase.Done)]
    public void DeriveCurrentPhase_WalksPhasesInOrder(
        DownloadStatus dl, TransferStatus tr, ImportStatus im, CleanupStatus cl, ArchivalStatus ar, ReleasePhase expected)
    {
        var release = new TrackedReleaseBuilder()
            .WithDownloadStatus(dl).WithTransferStatus(tr).WithImportStatus(im)
            .WithCleanupStatus(cl).WithArchivalStatus(ar)
            .Build();

        ReleaseMapping.DeriveCurrentPhase(release).Should().Be(expected);
    }

    [Fact]
    public void DeriveStatus_TerminalOutcomes_MapDirectly()
    {
        var completed = new TrackedReleaseBuilder().WithOutcome(PipelineOutcome.Completed).Build();
        var failed = new TrackedReleaseBuilder().WithOutcome(PipelineOutcome.Failed).Build();
        var cancelled = new TrackedReleaseBuilder().WithOutcome(PipelineOutcome.Cancelled).Build();

        ReleaseMapping.DeriveStatus(completed, JobEntity(), Now).Should().Be(ReleaseStatus.Done);
        ReleaseMapping.DeriveStatus(failed, JobEntity(), Now).Should().Be(ReleaseStatus.Failed);
        ReleaseMapping.DeriveStatus(cancelled, JobEntity(), Now).Should().Be(ReleaseStatus.Cancelled);
    }

    [Fact]
    public void DeriveStatus_FailedTransferUnderLimit_IsRetrying()
    {
        var release = new TrackedReleaseBuilder()
            .WithDownloadStatus(DownloadStatus.Completed)
            .WithTransferStatus(TransferStatus.Failed)
            .WithErrorCount(1)
            .Build();

        ReleaseMapping.DeriveStatus(release, JobEntity(maxRetries: 3), Now).Should().Be(ReleaseStatus.Retrying);
    }

    [Fact]
    public void DeriveStatus_FailedTransferOrphanedJob_IsFailed()
    {
        var release = new TrackedReleaseBuilder()
            .WithDownloadStatus(DownloadStatus.Completed)
            .WithTransferStatus(TransferStatus.Failed)
            .WithErrorCount(1)
            .Build();

        // No job (force-deleted): retry limit is unknown, so a failed transfer reads as failed.
        ReleaseMapping.DeriveStatus(release, job: null, Now).Should().Be(ReleaseStatus.Failed);
    }

    [Fact]
    public void DeriveStatus_ImportWaitingPastHalfTimeout_IsStuck()
    {
        var release = new TrackedReleaseBuilder()
            .WithDownloadStatus(DownloadStatus.Completed)
            .WithTransferStatus(TransferStatus.Completed)
            .WithImportStatus(ImportStatus.Pending)
            .WithTransferCompletedAtUtc(Now.AddHours(-13)) // > 50% of a 24h timeout
            .Build();

        ReleaseMapping.DeriveStatus(release, JobEntity(importTimeout: TimeSpan.FromHours(24)), Now)
            .Should().Be(ReleaseStatus.Stuck);
    }

    [Fact]
    public void DeriveStatus_ImportWaitingWithinHalfTimeout_IsFlowing()
    {
        var release = new TrackedReleaseBuilder()
            .WithDownloadStatus(DownloadStatus.Completed)
            .WithTransferStatus(TransferStatus.Completed)
            .WithImportStatus(ImportStatus.Pending)
            .WithTransferCompletedAtUtc(Now.AddHours(-1))
            .Build();

        ReleaseMapping.DeriveStatus(release, JobEntity(importTimeout: TimeSpan.FromHours(24)), Now)
            .Should().Be(ReleaseStatus.Flowing);
    }

    [Fact]
    public void ToListItem_Retrying_ComputesNextRetryFromLastError()
    {
        var lastError = Now.AddMinutes(-2);
        var release = new TrackedReleaseBuilder()
            .WithDownloadStatus(DownloadStatus.Completed)
            .WithTransferStatus(TransferStatus.Failed)
            .WithErrorCount(1)
            .WithLastErrorAtUtc(lastError)
            .Build();

        var item = ReleaseMapping.ToListItem(release, JobEntity(maxRetries: 3, retryDelay: TimeSpan.FromMinutes(5)), Now);

        item.Status.Should().Be(ReleaseStatus.Retrying);
        item.MaxRetries.Should().Be(3);
        item.NextRetryAtUtc.Should().Be(lastError + TimeSpan.FromMinutes(5));
    }

    [Fact]
    public void ToDetail_BuildsTimelineWithDoneCurrentAndPendingStates()
    {
        var release = new TrackedReleaseBuilder()
            .WithDownloadStatus(DownloadStatus.Completed)
            .WithDownloadCompletedAtUtc(Now.AddMinutes(-30))
            .WithTransferStatus(TransferStatus.InProgress)
            .WithTransferStartedAtUtc(Now.AddMinutes(-5))
            .Build();

        var detail = ReleaseMapping.ToDetail(release, JobEntity(), Now);

        detail.Timeline.Should().HaveCount(6);
        Entry(detail, ReleasePhase.Discover).State.Should().Be(PhaseTimelineState.Done);
        Entry(detail, ReleasePhase.Download).State.Should().Be(PhaseTimelineState.Done);
        Entry(detail, ReleasePhase.Transfer).State.Should().Be(PhaseTimelineState.Current);
        Entry(detail, ReleasePhase.Import).State.Should().Be(PhaseTimelineState.Pending);
        Entry(detail, ReleasePhase.Archival).State.Should().Be(PhaseTimelineState.Pending);
    }

    [Fact]
    public void ToDetail_FailedPhase_MarksTimelineEntryFailed()
    {
        var release = new TrackedReleaseBuilder()
            .WithDownloadStatus(DownloadStatus.Completed)
            .WithTransferStatus(TransferStatus.Completed)
            .WithImportStatus(ImportStatus.Failed)
            .WithOutcome(PipelineOutcome.Failed)
            .Build();

        var detail = ReleaseMapping.ToDetail(release, JobEntity(), Now);

        Entry(detail, ReleasePhase.Import).State.Should().Be(PhaseTimelineState.Failed);
        detail.Status.Should().Be(ReleaseStatus.Failed);
    }

    [Fact]
    public void ToListItem_TimeInPhase_MeasuredFromPhaseStart()
    {
        var release = new TrackedReleaseBuilder()
            .WithDownloadStatus(DownloadStatus.Completed)
            .WithTransferStatus(TransferStatus.Completed)
            .WithTransferCompletedAtUtc(Now.AddMinutes(-10)) // import phase started here
            .WithImportStatus(ImportStatus.Pending)
            .Build();

        var item = ReleaseMapping.ToListItem(release, JobEntity(), Now);

        item.CurrentPhase.Should().Be(ReleasePhase.Import);
        item.PhaseEnteredAtUtc.Should().Be(Now.AddMinutes(-10));
        item.TimeInPhaseSeconds.Should().Be(600);
    }

    private static PhaseTimelineEntry Entry(ReleaseDetail detail, ReleasePhase phase) =>
        detail.Timeline.Single(e => e.Phase == phase);
}
