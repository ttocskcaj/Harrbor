using System.IO.Compression;
using FakeItEasy;
using FluentAssertions;
using Harrbor.Services.Extraction;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Harrbor.Tests.Services.Extraction;

/// <summary>
/// Tests extraction against real archives in temp directories. Zip archives are created
/// in-test with System.IO.Compression; rar/7z cannot be created in-test (rar compression
/// is proprietary and SharpCompress only writes zip/tar/gz), so their extraction paths are
/// covered by the volume-detection tests plus SharpCompress's own coverage.
/// </summary>
public class ArchiveExtractionServiceTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly ArchiveExtractionService _service;

    public ArchiveExtractionServiceTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"harrbor-extraction-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempRoot);
        _service = new ArchiveExtractionService(A.Fake<ILogger<ArchiveExtractionService>>());
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, recursive: true);
    }

    private string CreateStagingItem(string name)
    {
        var dir = Path.Combine(_tempRoot, name);
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void CreateZip(string zipPath, params (string Name, string Content)[] entries)
    {
        using var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create);
        foreach (var (name, content) in entries)
        {
            using var writer = new StreamWriter(zip.CreateEntry(name).Open());
            writer.Write(content);
        }
    }

    [Fact]
    public async Task ExtractAsync_ZipInStagingDirectory_ExtractsInPlace()
    {
        // Arrange
        var stagingItem = CreateStagingItem("release");
        CreateZip(Path.Combine(stagingItem, "release.zip"),
            ("episode.mkv", "video-bytes"),
            ("Subs/episode.srt", "subtitle-bytes"));

        // Act
        var result = await _service.ExtractAsync(stagingItem, TestContext.Current.CancellationToken);

        // Assert
        result.Success.Should().BeTrue(result.Error);
        result.ArchivesExtracted.Should().Be(1);
        File.ReadAllText(Path.Combine(stagingItem, "episode.mkv")).Should().Be("video-bytes");
        File.ReadAllText(Path.Combine(stagingItem, "Subs", "episode.srt")).Should().Be("subtitle-bytes");
        // Source archive is left in place; the cleanup phase deletes the whole staging item
        File.Exists(Path.Combine(stagingItem, "release.zip")).Should().BeTrue();
    }

    [Fact]
    public async Task ExtractAsync_SingleFileStagingItem_ExtractsNextToIt()
    {
        // Arrange - the staging item itself is an archive file, not a directory
        var container = CreateStagingItem("container");
        var zipPath = Path.Combine(container, "movie.zip");
        CreateZip(zipPath, ("movie.mkv", "video-bytes"));

        // Act
        var result = await _service.ExtractAsync(zipPath, TestContext.Current.CancellationToken);

        // Assert
        result.Success.Should().BeTrue(result.Error);
        result.ArchivesExtracted.Should().Be(1);
        File.ReadAllText(Path.Combine(container, "movie.mkv")).Should().Be("video-bytes");
    }

    [Fact]
    public async Task ExtractAsync_NoArchives_SucceedsWithZeroCount()
    {
        // Arrange
        var stagingItem = CreateStagingItem("release");
        File.WriteAllText(Path.Combine(stagingItem, "episode.mkv"), "video-bytes");
        File.WriteAllText(Path.Combine(stagingItem, "release.nfo"), "info");

        // Act
        var result = await _service.ExtractAsync(stagingItem, TestContext.Current.CancellationToken);

        // Assert
        result.Success.Should().BeTrue(result.Error);
        result.ArchivesExtracted.Should().Be(0);
    }

    [Fact]
    public async Task ExtractAsync_MissingStagingItem_Fails()
    {
        // Act
        var result = await _service.ExtractAsync(Path.Combine(_tempRoot, "does-not-exist"), TestContext.Current.CancellationToken);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task ExtractAsync_RunTwice_IsIdempotent()
    {
        // Arrange
        var stagingItem = CreateStagingItem("release");
        CreateZip(Path.Combine(stagingItem, "release.zip"), ("episode.mkv", "video-bytes"));

        // Act
        var first = await _service.ExtractAsync(stagingItem, TestContext.Current.CancellationToken);
        var second = await _service.ExtractAsync(stagingItem, TestContext.Current.CancellationToken);

        // Assert
        first.Success.Should().BeTrue(first.Error);
        second.Success.Should().BeTrue(second.Error);
        File.ReadAllText(Path.Combine(stagingItem, "episode.mkv")).Should().Be("video-bytes");
    }

    [Fact]
    public async Task ExtractAsync_OrphanedContinuationVolume_Fails()
    {
        // Arrange - a continuation volume with no first volume present
        var stagingItem = CreateStagingItem("release");
        File.WriteAllBytes(Path.Combine(stagingItem, "release.r00"), [0x52, 0x61, 0x72, 0x21]);

        // Act
        var result = await _service.ExtractAsync(stagingItem, TestContext.Current.CancellationToken);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("without their first volume");
        result.Error.Should().Contain("release.r00");
    }

    [Fact]
    public async Task ExtractAsync_FileNamedLikeArchiveButNotOne_IsSkipped()
    {
        // Arrange - archive extension but no archive signature
        var stagingItem = CreateStagingItem("release");
        File.WriteAllText(Path.Combine(stagingItem, "fake.zip"), "just text");
        File.WriteAllText(Path.Combine(stagingItem, "episode.mkv"), "video-bytes");

        // Act
        var result = await _service.ExtractAsync(stagingItem, TestContext.Current.CancellationToken);

        // Assert
        result.Success.Should().BeTrue(result.Error);
        result.ArchivesExtracted.Should().Be(0);
    }

    [Fact]
    public async Task ExtractAsync_TwoIndependentArchives_ExtractsBoth()
    {
        // Arrange
        var stagingItem = CreateStagingItem("release");
        CreateZip(Path.Combine(stagingItem, "cd1.zip"), ("cd1.mkv", "one"));
        CreateZip(Path.Combine(stagingItem, "cd2.zip"), ("cd2.mkv", "two"));

        // Act
        var result = await _service.ExtractAsync(stagingItem, TestContext.Current.CancellationToken);

        // Assert
        result.Success.Should().BeTrue(result.Error);
        result.ArchivesExtracted.Should().Be(2);
        File.ReadAllText(Path.Combine(stagingItem, "cd1.mkv")).Should().Be("one");
        File.ReadAllText(Path.Combine(stagingItem, "cd2.mkv")).Should().Be("two");
    }

    [Fact]
    public async Task ExtractAsync_ArchiveInSubdirectory_ExtractsIntoThatSubdirectory()
    {
        // Arrange
        var stagingItem = CreateStagingItem("release");
        var subDir = Path.Combine(stagingItem, "CD1");
        Directory.CreateDirectory(subDir);
        CreateZip(Path.Combine(subDir, "cd1.zip"), ("cd1.mkv", "one"));

        // Act
        var result = await _service.ExtractAsync(stagingItem, TestContext.Current.CancellationToken);

        // Assert
        result.Success.Should().BeTrue(result.Error);
        result.ArchivesExtracted.Should().Be(1);
        File.ReadAllText(Path.Combine(subDir, "cd1.mkv")).Should().Be("one");
    }

    [Fact]
    public async Task ExtractAsync_Cancelled_Throws()
    {
        // Arrange
        var stagingItem = CreateStagingItem("release");
        CreateZip(Path.Combine(stagingItem, "release.zip"), ("episode.mkv", "video-bytes"));
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act / Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _service.ExtractAsync(stagingItem, cts.Token));
    }
}
