using Harrbor.Data.Entities;
using Xunit;

namespace Harrbor.Tests;

public class TrackedTorrentTests
{
    [Fact]
    public void TrackedTorrent_DefaultValues_AreCorrect()
    {
        var torrent = new TrackedTorrent();

        Assert.Equal(string.Empty, torrent.InfoHash);
        Assert.Equal(string.Empty, torrent.Name);
        Assert.Equal(TorrentCategory.TV, torrent.Category);
        Assert.Equal(TransferStatus.Pending, torrent.TransferStatus);
        Assert.Equal(ImportStatus.Pending, torrent.ImportStatus);
        Assert.Equal(CleanupStatus.Pending, torrent.CleanupStatus);
        Assert.Equal(ArchivalStatus.Pending, torrent.ArchivalStatus);
        Assert.Equal(0, torrent.ErrorCount);
        Assert.Null(torrent.LastError);
    }

    [Fact]
    public void TrackedTorrent_CanSetProperties()
    {
        var now = DateTimeOffset.UtcNow;
        var torrent = new TrackedTorrent
        {
            Id = 1,
            InfoHash = "abc123def456",
            Name = "Test Torrent",
            Category = TorrentCategory.Movies,
            TransferStatus = TransferStatus.Completed,
            CreatedAt = now
        };

        Assert.Equal(1, torrent.Id);
        Assert.Equal("abc123def456", torrent.InfoHash);
        Assert.Equal("Test Torrent", torrent.Name);
        Assert.Equal(TorrentCategory.Movies, torrent.Category);
        Assert.Equal(TransferStatus.Completed, torrent.TransferStatus);
        Assert.Equal(now, torrent.CreatedAt);
    }
}
