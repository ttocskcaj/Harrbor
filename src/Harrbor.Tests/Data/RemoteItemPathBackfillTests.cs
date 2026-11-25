using FluentAssertions;
using Harrbor.Data;
using Harrbor.Data.Entities;
using Harrbor.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Harrbor.Tests.Data;

public class RemoteItemPathBackfillTests
{
    [Theory]
    [InlineData("/downloads/radarr/Dune/Dune.mkv", "/downloads/radarr", "/downloads/radarr/Dune")]
    [InlineData("/downloads/radarr/Kubo.mkv", "/downloads/radarr", "/downloads/radarr/Kubo.mkv")]
    [InlineData("/downloads/radarr/Show.S01", "/downloads/radarr/", "/downloads/radarr/Show.S01")]
    [InlineData("/downloads/radarr/sub/Dune/Dune.mkv", "/downloads/radarr", "/downloads/radarr/sub/Dune/Dune.mkv")] // too deep to tell
    [InlineData("/elsewhere/Dune/Dune.mkv", "/downloads/radarr", "/elsewhere/Dune/Dune.mkv")] // not under the root
    [InlineData("/downloads/radarr/Dune/Dune.mkv", null, "/downloads/radarr/Dune/Dune.mkv")]   // job not configured
    public void Compute_TakesFirstSegmentAfterRoot(string remotePath, string? remoteRoot, string expected)
    {
        RemoteItemPathBackfill.Compute(remotePath, remoteRoot).Should().Be(expected);
    }

    [Fact]
    public async Task ApplyAsync_FillsOnlyRowsWithoutItemThatFinishedDownloading()
    {
        using var db = TestDbContextFactory.Create();
        db.TrackedReleases.AddRange(
            new TrackedReleaseBuilder().WithDownloadId("PENDING-TRANSFER").WithJobName("movies")
                .WithDownloadStatus(DownloadStatus.Completed).WithTransferStatus(TransferStatus.Pending)
                .WithRemotePath("/downloads/radarr/Dune/Dune.mkv").Build(),
            new TrackedReleaseBuilder().WithDownloadId("TRANSFERRED").WithJobName("movies")
                .WithDownloadStatus(DownloadStatus.Completed).WithTransferStatus(TransferStatus.Completed)
                .WithRemotePath("/downloads/radarr/Old/Old.mkv").WithRemoteItemPath("/downloads/radarr/Old/Old.mkv").Build(),
            new TrackedReleaseBuilder().WithDownloadId("DOWNLOADING").WithJobName("movies")
                .WithDownloadStatus(DownloadStatus.Pending)
                .WithRemotePath("/media/import/Something").Build(),
            new TrackedReleaseBuilder().WithDownloadId("UNKNOWN-JOB").WithJobName("gone")
                .WithDownloadStatus(DownloadStatus.Completed)
                .WithRemotePath("/downloads/radarr/X/X.mkv").Build());
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var jobs = new[] { new JobDefinitionBuilder().WithName("movies").WithRemotePath("/downloads/radarr").Build() };
        var count = await RemoteItemPathBackfill.ApplyAsync(db, jobs, TestContext.Current.CancellationToken);

        count.Should().Be(2);
        var rows = await db.TrackedReleases.ToDictionaryAsync(r => r.DownloadId, r => r.RemoteItemPath, TestContext.Current.CancellationToken);
        rows["PENDING-TRANSFER"].Should().Be("/downloads/radarr/Dune");
        rows["TRANSFERRED"].Should().Be("/downloads/radarr/Old/Old.mkv", "its staged layout stays as it is");
        rows["DOWNLOADING"].Should().BeNull();
        rows["UNKNOWN-JOB"].Should().Be("/downloads/radarr/X/X.mkv");
    }
}
