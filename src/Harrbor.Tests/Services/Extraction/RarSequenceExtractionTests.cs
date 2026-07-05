using System.Security.Cryptography;
using System.Text;
using FakeItEasy;
using FluentAssertions;
using Harrbor.Services.Extraction;
using Microsoft.Extensions.Logging;
using SharpCompress.Archives;
using Xunit;

namespace Harrbor.Tests.Services.Extraction;

/// <summary>
/// Extraction tests against real RAR/7z volume sets checked in under Fixtures/Archives.
/// The fixtures were created with the official rar tool (RAR 6.24 for the RAR4-format
/// sets, RAR 7.12 for RAR5) and verified with unrar. Contents are deterministic so the
/// tests can regenerate the expected bytes:
///  - content.txt: 100 lines of "Harrbor RAR fixture content line NNNN\n"
///  - solidN.bin: 4096 bytes of a SHA256 chain seeded with "harrbor-solid-N"
/// </summary>
public class RarSequenceExtractionTests : IDisposable
{
    private static readonly string FixtureRoot =
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "Archives");

    private readonly string _tempRoot;
    private readonly ArchiveExtractionService _service;

    public RarSequenceExtractionTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"harrbor-rar-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempRoot);
        _service = new ArchiveExtractionService(A.Fake<ILogger<ArchiveExtractionService>>());
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, recursive: true);
    }

    /// <summary>
    /// Copies a fixture set into a fresh temp staging dir so in-place extraction
    /// never touches the checked-in fixtures.
    /// </summary>
    private string StageFixture(string setName, params string[] excludeFiles)
    {
        var source = Path.Combine(FixtureRoot, setName);
        var stagingItem = Path.Combine(_tempRoot, setName);
        Directory.CreateDirectory(stagingItem);

        foreach (var file in Directory.EnumerateFiles(source))
        {
            var fileName = Path.GetFileName(file);
            if (!excludeFiles.Contains(fileName, StringComparer.OrdinalIgnoreCase))
                File.Copy(file, Path.Combine(stagingItem, fileName));
        }

        return stagingItem;
    }

    private static string ExpectedContentTxt() =>
        string.Concat(Enumerable.Range(0, 100).Select(i => $"Harrbor RAR fixture content line {i:D4}\n"));

    private static byte[] ExpectedSolidBin(int n)
    {
        var output = new List<byte>(4096);
        var block = Encoding.UTF8.GetBytes($"harrbor-solid-{n}");
        while (output.Count < 4096)
        {
            block = SHA256.HashData(block);
            output.AddRange(block);
        }
        return output.Take(4096).ToArray();
    }

    [Theory]
    [InlineData("rar4-oldstyle", 5)] // oldstyle.rar + .r00-.r03 (RAR4, old-style sequencing)
    [InlineData("rar4-parts", 5)]    // parts4.part1-5.rar (RAR4, new-style sequencing)
    [InlineData("rar5-parts", 5)]    // parts5.part1-5.rar (RAR5)
    [InlineData("rar4-single", 1)]   // standalone RAR4
    [InlineData("rar5-single", 1)]   // standalone RAR5
    public async Task ExtractAsync_RarVolumeSet_ExtractsContentByteForByte(string setName, int volumeCount)
    {
        // Arrange
        var stagingItem = StageFixture(setName);
        Directory.EnumerateFiles(stagingItem).Should().HaveCount(volumeCount, "fixture set should be complete");

        // Act
        var result = await _service.ExtractAsync(stagingItem, TestContext.Current.CancellationToken);

        // Assert
        result.Success.Should().BeTrue(result.Error);
        result.ArchivesExtracted.Should().Be(1);
        File.ReadAllText(Path.Combine(stagingItem, "content.txt")).Should().Be(ExpectedContentTxt());
        // Volumes remain in place for the cleanup phase
        Directory.EnumerateFiles(stagingItem).Should().HaveCount(volumeCount + 1);
    }

    [Fact]
    public async Task ExtractAsync_SolidMultiVolumeRar_ExtractsAllFiles()
    {
        // Arrange - guard that the fixture really is solid, so this test keeps
        // exercising the forward-only RarReader fallback
        var stagingItem = StageFixture("rar5-solid");
        using (var archive = ArchiveFactory.OpenArchive(
                   new FileInfo(Path.Combine(stagingItem, "solid.part1.rar"))))
        {
            archive.IsSolid.Should().BeTrue("fixture must be a solid archive");
        }

        // Act
        var result = await _service.ExtractAsync(stagingItem, TestContext.Current.CancellationToken);

        // Assert
        result.Success.Should().BeTrue(result.Error);
        result.ArchivesExtracted.Should().Be(1);
        for (var n = 1; n <= 3; n++)
        {
            File.ReadAllBytes(Path.Combine(stagingItem, $"solid{n}.bin"))
                .Should().Equal(ExpectedSolidBin(n), $"solid{n}.bin should survive the solid-RAR reader path");
        }
    }

    [Fact]
    public async Task ExtractAsync_PasswordProtectedRar_FailsWithClearError()
    {
        // Arrange
        var stagingItem = StageFixture("rar5-encrypted");

        // Act
        var result = await _service.ExtractAsync(stagingItem, TestContext.Current.CancellationToken);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("password-protected");
        File.Exists(Path.Combine(stagingItem, "content.txt")).Should().BeFalse();
    }

    [Fact]
    public async Task ExtractAsync_FirstVolumeMissing_FailsAsOrphanedContinuations()
    {
        // Arrange
        var stagingItem = StageFixture("rar5-parts", excludeFiles: "parts5.part1.rar");

        // Act
        var result = await _service.ExtractAsync(stagingItem, TestContext.Current.CancellationToken);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("without their first volume");
    }

    [Fact]
    public async Task ExtractAsync_MiddleVolumeMissing_Fails()
    {
        // Arrange
        var stagingItem = StageFixture("rar5-parts", excludeFiles: "parts5.part3.rar");

        // Act
        var result = await _service.ExtractAsync(stagingItem, TestContext.Current.CancellationToken);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("parts5");
    }

    [Fact]
    public async Task ExtractAsync_ContinuationVolumesMissing_Fails()
    {
        // Arrange - only the old-style first volume, all .rNN continuations missing
        var stagingItem = StageFixture(
            "rar4-oldstyle",
            "oldstyle.r00", "oldstyle.r01", "oldstyle.r02", "oldstyle.r03");

        // Act
        var result = await _service.ExtractAsync(stagingItem, TestContext.Current.CancellationToken);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("oldstyle");
    }

    [Fact]
    public async Task ExtractAsync_SplitSevenZip_ExtractsContent()
    {
        // Arrange - split.7z.001-004
        var stagingItem = StageFixture("sevenzip-split");

        // Act
        var result = await _service.ExtractAsync(stagingItem, TestContext.Current.CancellationToken);

        // Assert
        result.Success.Should().BeTrue(result.Error);
        result.ArchivesExtracted.Should().Be(1);
        File.ReadAllText(Path.Combine(stagingItem, "content.txt")).Should().Be(ExpectedContentTxt());
    }
}
