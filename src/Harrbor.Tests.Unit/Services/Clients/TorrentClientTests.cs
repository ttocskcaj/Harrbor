using FakeItEasy;
using FluentAssertions;
using Harrbor.Abstractions.Clients;
using Harrbor.Abstractions.Clients.Models;
using Xunit;

namespace Harrbor.Tests.Unit.Services.Clients;

/// <summary>
/// Tests for torrent client behavior using the <see cref="ITorrentClient"/> interface.
/// The concrete <c>QBittorrentTorrentClient</c> creates its own internal HTTP client, so these
/// tests use a faked interface to verify expected behavior patterns; integration tests exercise
/// the real client against config/store behavior.
/// </summary>
public class TorrentClientTests
{
    [Fact]
    public async Task GetTorrentListAsync_WithCategory_FiltersCorrectly()
    {
        // Arrange
        var client = A.Fake<ITorrentClient>();
        var expectedTorrents = new List<TorrentSnapshot>
        {
            CreateSnapshot("hash1", "Torrent 1", "sonarr"),
            CreateSnapshot("hash2", "Torrent 2", "sonarr")
        };

        A.CallTo(() => client.GetTorrentListAsync("sonarr", null, A<CancellationToken>._))
            .Returns(expectedTorrents);

        // Act
        var result = await client.GetTorrentListAsync("sonarr", null);

        // Assert
        result.Should().HaveCount(2);
        result.Should().AllSatisfy(t => t.Category.Should().Be("sonarr"));
    }

    [Fact]
    public async Task GetTorrentListAsync_WithMultipleTags_FiltersClientSide()
    {
        // Arrange
        var client = A.Fake<ITorrentClient>();
        var tags = new List<string> { "tag1", "tag2" };
        var expectedTorrents = new List<TorrentSnapshot>
        {
            CreateSnapshotWithTags("hash1", "Torrent 1", ["tag1", "tag2", "tag3"])
        };

        A.CallTo(() => client.GetTorrentListAsync(null, tags, A<CancellationToken>._))
            .Returns(expectedTorrents);

        // Act
        var result = await client.GetTorrentListAsync(null, tags);

        // Assert
        result.Should().HaveCount(1);
    }

    [Fact]
    public async Task EnsureCategoryExistsAsync_CallsWithCorrectCategory()
    {
        // Arrange
        var client = A.Fake<ITorrentClient>();

        // Act
        await client.EnsureCategoryExistsAsync("new-category");

        // Assert
        A.CallTo(() => client.EnsureCategoryExistsAsync("new-category", A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task TestConnectionAsync_Success_ReturnsSuccessResult()
    {
        // Arrange
        var client = A.Fake<ITorrentClient>();
        var expectedResult = new ConnectionTestResult(true, Version: "2.8.19");

        A.CallTo(() => client.TestConnectionAsync(A<CancellationToken>._))
            .Returns(expectedResult);

        // Act
        var result = await client.TestConnectionAsync();

        // Assert
        result.Success.Should().BeTrue();
        result.Version.Should().Be("2.8.19");
        result.Error.Should().BeNull();
    }

    [Fact]
    public async Task TestConnectionAsync_Failure_ReturnsErrorResult()
    {
        // Arrange
        var client = A.Fake<ITorrentClient>();
        var expectedResult = new ConnectionTestResult(false, Error: "Connection refused");

        A.CallTo(() => client.TestConnectionAsync(A<CancellationToken>._))
            .Returns(expectedResult);

        // Act
        var result = await client.TestConnectionAsync();

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Be("Connection refused");
    }

    [Fact]
    public async Task GetTorrentAsync_ReturnsCorrectTorrent()
    {
        // Arrange
        var client = A.Fake<ITorrentClient>();
        var expectedTorrent = CreateSnapshot("abc123", "Test Torrent", "sonarr");

        A.CallTo(() => client.GetTorrentAsync("abc123", A<CancellationToken>._))
            .Returns(expectedTorrent);

        // Act
        var result = await client.GetTorrentAsync("abc123");

        // Assert
        result.Should().NotBeNull();
        result!.Hash.Should().Be("abc123");
    }

    [Fact]
    public async Task GetTorrentAsync_NotFound_ReturnsNull()
    {
        // Arrange
        var client = A.Fake<ITorrentClient>();

        A.CallTo(() => client.GetTorrentAsync("notfound", A<CancellationToken>._))
            .Returns((TorrentSnapshot?)null);

        // Act
        var result = await client.GetTorrentAsync("notfound");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task SetCategoryAsync_CallsWithCorrectParameters()
    {
        // Arrange
        var client = A.Fake<ITorrentClient>();

        // Act
        await client.SetCategoryAsync("abc123", "completed");

        // Assert
        A.CallTo(() => client.SetCategoryAsync("abc123", "completed", A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task IsConnectedAsync_WhenTestSucceeds_ReturnsTrue()
    {
        // Arrange
        var client = A.Fake<ITorrentClient>();
        A.CallTo(() => client.IsConnectedAsync(A<CancellationToken>._)).Returns(true);

        // Act
        var result = await client.IsConnectedAsync();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsConnectedAsync_WhenTestFails_ReturnsFalse()
    {
        // Arrange
        var client = A.Fake<ITorrentClient>();
        A.CallTo(() => client.IsConnectedAsync(A<CancellationToken>._)).Returns(false);

        // Act
        var result = await client.IsConnectedAsync();

        // Assert
        result.Should().BeFalse();
    }

    private static TorrentSnapshot CreateSnapshot(string hash, string name, string category) =>
        new(hash, name, 1.0, ContentPath: null, SavePath: null, Category: category, Tags: []);

    private static TorrentSnapshot CreateSnapshotWithTags(string hash, string name, string[] tags) =>
        new(hash, name, 1.0, ContentPath: null, SavePath: null, Category: null, Tags: tags);
}
