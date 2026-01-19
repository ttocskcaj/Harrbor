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
}
