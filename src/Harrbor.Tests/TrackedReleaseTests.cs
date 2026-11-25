using Harrbor.Data.Entities;
using Xunit;

namespace Harrbor.Tests;

public class TrackedReleaseTests
{
    [Fact]
    public void TrackedRelease_DefaultValues_AreCorrect()
    {
        var release = new TrackedRelease();

        Assert.Equal(DownloadStatus.Pending, release.DownloadStatus);
        Assert.Equal(TransferStatus.Pending, release.TransferStatus);
        Assert.Equal(ImportStatus.Pending, release.ImportStatus);
        Assert.Equal(CleanupStatus.Pending, release.CleanupStatus);
        Assert.Equal(ArchivalStatus.Pending, release.ArchivalStatus);
        Assert.Equal(0, release.ErrorCount);
        Assert.Null(release.LastError);
    }

    [Fact]
    public void TrackedRelease_CanSetProperties()
    {
        var now = DateTime.UtcNow;
        var release = new TrackedRelease
        {
            Id = 1,
            DownloadId = "ABC123",
            Name = "Test Release",
            JobName = "tv-shows",
            RemotePath = "/downloads/test",
            StagingPath = "/staging/test",
            DownloadStatus = DownloadStatus.Completed,
            TransferStatus = TransferStatus.InProgress,
            CreatedAtUtc = now
        };

        Assert.Equal(1, release.Id);
        Assert.Equal("ABC123", release.DownloadId);
        Assert.Equal("Test Release", release.Name);
        Assert.Equal("tv-shows", release.JobName);
        Assert.Equal("/downloads/test", release.RemotePath);
        Assert.Equal("/staging/test", release.StagingPath);
        Assert.Equal(DownloadStatus.Completed, release.DownloadStatus);
        Assert.Equal(TransferStatus.InProgress, release.TransferStatus);
        Assert.Equal(now, release.CreatedAtUtc);
    }

    [Fact]
    public void TrackedRelease_ErrorTracking_WorksCorrectly()
    {
        var release = new TrackedRelease();
        var errorTime = DateTime.UtcNow;

        release.ErrorCount = 3;
        release.LastError = "Connection timeout";
        release.LastErrorAtUtc = errorTime;

        Assert.Equal(3, release.ErrorCount);
        Assert.Equal("Connection timeout", release.LastError);
        Assert.Equal(errorTime, release.LastErrorAtUtc);
    }

    [Fact]
    public void DownloadStatus_HasExpectedValues()
    {
        Assert.Equal(0, (int)DownloadStatus.Pending);
        Assert.Equal(1, (int)DownloadStatus.Completed);
    }

    [Fact]
    public void TransferStatus_HasExpectedValues()
    {
        Assert.Equal(0, (int)TransferStatus.Pending);
        Assert.Equal(1, (int)TransferStatus.InProgress);
        Assert.Equal(2, (int)TransferStatus.Completed);
        Assert.Equal(3, (int)TransferStatus.Failed);
    }

    [Fact]
    public void ImportStatus_HasExpectedValues()
    {
        Assert.Equal(0, (int)ImportStatus.Pending);
        Assert.Equal(1, (int)ImportStatus.Completed);
        Assert.Equal(2, (int)ImportStatus.Failed);
    }

    [Fact]
    public void CleanupStatus_HasExpectedValues()
    {
        Assert.Equal(0, (int)CleanupStatus.Pending);
        Assert.Equal(1, (int)CleanupStatus.Completed);
        Assert.Equal(2, (int)CleanupStatus.Failed);
    }

    [Fact]
    public void ArchivalStatus_HasExpectedValues()
    {
        Assert.Equal(0, (int)ArchivalStatus.Pending);
        Assert.Equal(1, (int)ArchivalStatus.Completed);
        Assert.Equal(2, (int)ArchivalStatus.Failed);
    }

    [Fact]
    public void TrackedRelease_NewRow_DefaultsToPendingConversionWithoutRemoteItem()
    {
        var release = new TrackedRelease();

        Assert.Equal(ConversionStatus.Pending, release.ConversionStatus);
        Assert.Null(release.RemoteItemPath);
    }

    [Fact]
    public void PathHelpers_UseHoldingPathUntilPublished()
    {
        var release = new TrackedRelease
        {
            RemotePath = "/downloads/tv/Show.S01E01",
            StagingPath = "/staging/tv"
        };

        Assert.Equal("/staging/tv/Show.S01E01", release.GetFinalItemPath());
        Assert.Equal("/staging/tv/.harrbor/Show.S01E01", release.GetHoldingItemPath());
        Assert.Equal("/staging/tv/.harrbor/Show.S01E01", release.GetWorkingItemPath());

        release.PublishedAtUtc = DateTime.UtcNow;
        Assert.Equal("/staging/tv/Show.S01E01", release.GetWorkingItemPath());
    }

    [Theory]
    // Multi-file torrent: content_path is the root folder
    [InlineData("/downloads/radarr", "/downloads/radarr/Show.S01", "Show.S01", "/downloads/radarr/Show.S01")]
    // Single file inside a root folder (Dune): content_path is the file, the item is the folder
    [InlineData("/downloads/radarr", "/downloads/radarr/Dune-FraMeSToR/Dune-FraMeSToR.mkv", "Dune-FraMeSToR", "/downloads/radarr/Dune-FraMeSToR")]
    // Plain single file (Kubo): the item is the file
    [InlineData("/downloads/radarr", "/downloads/radarr/Kubo-FGT.mkv", "Kubo-FGT.mkv", "/downloads/radarr/Kubo-FGT.mkv")]
    // Trailing slashes from qBittorrent
    [InlineData("/downloads/radarr/", "/downloads/radarr/Show.S01/", "Show.S01", "/downloads/radarr/Show.S01")]
    // No content path: save path + name
    [InlineData("/downloads/radarr", null, "Show.S01", "/downloads/radarr/Show.S01")]
    [InlineData("/downloads/radarr", "", "Show.S01", "/downloads/radarr/Show.S01")]
    // Content path outside the save path: save path + name
    [InlineData("/downloads/radarr", "/elsewhere/Show.S01", "Show.S01", "/downloads/radarr/Show.S01")]
    // Content path equal to the save path (no root folder): save path + name
    [InlineData("/downloads/radarr", "/downloads/radarr", "Show.S01", "/downloads/radarr/Show.S01")]
    // Only the content path known
    [InlineData(null, "/downloads/radarr/Show.S01", null, "/downloads/radarr/Show.S01")]
    [InlineData(null, null, null, null)]
    public void ComputeRemoteItemPath_FindsTopLevelItem(string? savePath, string? contentPath, string? name, string? expected)
    {
        Assert.Equal(expected, TrackedRelease.ComputeRemoteItemPath(savePath, contentPath, name));
    }

    [Theory]
    [InlineData("/downloads/radarr/Dune-FraMeSToR/Dune-FraMeSToR.mkv", "/downloads/radarr/Dune-FraMeSToR", "Dune-FraMeSToR")]
    [InlineData("/downloads/radarr/Kubo-FGT.mkv", "/downloads/radarr/Kubo-FGT.mkv", "Kubo-FGT.mkv")]
    [InlineData("/downloads/radarr/Show.S01", "/downloads/radarr/Show.S01", "Show.S01")]
    [InlineData("/downloads/radarr/Show.S01", null, "Show.S01")] // not set yet: RemotePath
    public void GetItemName_IsNameOfRemoteItem(string remotePath, string? remoteItemPath, string expected)
    {
        var release = new TrackedRelease { RemotePath = remotePath, RemoteItemPath = remoteItemPath, StagingPath = "/staging" };

        Assert.Equal(expected, release.GetItemName());
        Assert.Equal($"/staging/{expected}", release.GetFinalItemPath());
        Assert.Equal($"/staging/.harrbor/{expected}", release.GetHoldingItemPath());
    }
}
