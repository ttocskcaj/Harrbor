using FluentAssertions;
using Harrbor.Services.Extraction;
using Xunit;

namespace Harrbor.Tests.Services.Extraction;

public class ArchiveVolumeDetectionTests
{
    private const string Dir = "/staging/release";

    private static List<string> Paths(params string[] fileNames) =>
        fileNames.Select(f => Path.Combine(Dir, f)).ToList();

    [Fact]
    public void OldStyleRarSet_SelectsOnlyRarFileAsFirstVolume()
    {
        var files = Paths("show.rar", "show.r00", "show.r01", "show.r02");

        var result = ArchiveExtractionService.FindFirstVolumes(files);

        result.VolumeSets.Should().ContainSingle()
            .Which.FirstVolume.Should().Be(Path.Combine(Dir, "show.rar"));
        result.OrphanedContinuations.Should().BeEmpty();
    }

    [Fact]
    public void OldStyleRarSet_OrdersVolumesBySequence()
    {
        var files = Paths("show.r01", "show.rar", "show.s00", "show.r00", "show.r99");

        var result = ArchiveExtractionService.FindFirstVolumes(files);

        result.VolumeSets.Should().ContainSingle()
            .Which.Volumes.Select(Path.GetFileName).Should().ContainInOrder(
                "show.rar", "show.r00", "show.r01", "show.r99", "show.s00");
    }

    [Theory]
    [InlineData("SHOW.RAR", "SHOW.R00")]
    [InlineData("show.Rar", "show.R00")]
    public void OldStyleRarSet_CaseVariants_SelectsFirstVolume(string first, string continuation)
    {
        var files = Paths(first, continuation);

        var result = ArchiveExtractionService.FindFirstVolumes(files);

        result.VolumeSets.Should().ContainSingle()
            .Which.FirstVolume.Should().Be(Path.Combine(Dir, first));
        result.OrphanedContinuations.Should().BeEmpty();
    }

    [Theory]
    [InlineData("show.part1.rar", "show.part2.rar", "show.part3.rar")]
    [InlineData("show.part01.rar", "show.part02.rar", "show.part03.rar")]
    [InlineData("show.part001.rar", "show.part002.rar", "show.part003.rar")]
    public void NewStyleRarSet_SelectsOnlyPartOneAsFirstVolume(string first, params string[] rest)
    {
        var files = Paths([first, .. rest]);

        var result = ArchiveExtractionService.FindFirstVolumes(files);

        result.VolumeSets.Should().ContainSingle()
            .Which.FirstVolume.Should().Be(Path.Combine(Dir, first));
        result.OrphanedContinuations.Should().BeEmpty();
    }

    [Fact]
    public void NewStyleRarSet_OrdersVolumesNumerically()
    {
        var files = Paths("show.part10.rar", "show.part2.rar", "show.part1.rar");

        var result = ArchiveExtractionService.FindFirstVolumes(files);

        result.VolumeSets.Should().ContainSingle()
            .Which.Volumes.Select(Path.GetFileName).Should().ContainInOrder(
                "show.part1.rar", "show.part2.rar", "show.part10.rar");
    }

    [Fact]
    public void NewStyleRarSet_MixedPaddingWidths_DoesNotReportOrphans()
    {
        // part1..part10 sets mix digit widths; part10's expected first volume is part1.rar
        var files = Paths("show.part1.rar", "show.part10.rar");

        var result = ArchiveExtractionService.FindFirstVolumes(files);

        result.VolumeSets.Should().ContainSingle();
        result.OrphanedContinuations.Should().BeEmpty();
    }

    [Fact]
    public void NewStyleContinuationWithoutFirstVolume_ReportsOrphan()
    {
        var files = Paths("show.part2.rar", "show.part3.rar");

        var result = ArchiveExtractionService.FindFirstVolumes(files);

        result.VolumeSets.Should().BeEmpty();
        result.OrphanedContinuations.Should().HaveCount(2);
    }

    [Fact]
    public void OldStyleContinuationWithoutFirstVolume_ReportsOrphan()
    {
        var files = Paths("show.r00", "show.r01");

        var result = ArchiveExtractionService.FindFirstVolumes(files);

        result.VolumeSets.Should().BeEmpty();
        result.OrphanedContinuations.Should().HaveCount(2);
    }

    [Fact]
    public void StandaloneZip_IsFirstVolume()
    {
        var files = Paths("movie.zip");

        var result = ArchiveExtractionService.FindFirstVolumes(files);

        result.VolumeSets.Should().ContainSingle()
            .Which.FirstVolume.Should().Be(Path.Combine(Dir, "movie.zip"));
    }

    [Fact]
    public void SplitZip_SelectsOnlyZipFileAsFirstVolume()
    {
        var files = Paths("movie.zip", "movie.z01", "movie.z02");

        var result = ArchiveExtractionService.FindFirstVolumes(files);

        result.VolumeSets.Should().ContainSingle()
            .Which.FirstVolume.Should().Be(Path.Combine(Dir, "movie.zip"));
        result.OrphanedContinuations.Should().BeEmpty();
    }

    [Fact]
    public void SplitZipContinuationWithoutFirstVolume_ReportsOrphan()
    {
        var files = Paths("movie.z01");

        var result = ArchiveExtractionService.FindFirstVolumes(files);

        result.VolumeSets.Should().BeEmpty();
        result.OrphanedContinuations.Should().ContainSingle();
    }

    [Fact]
    public void StandaloneSevenZip_IsFirstVolume()
    {
        var files = Paths("movie.7z");

        var result = ArchiveExtractionService.FindFirstVolumes(files);

        result.VolumeSets.Should().ContainSingle()
            .Which.FirstVolume.Should().Be(Path.Combine(Dir, "movie.7z"));
    }

    [Fact]
    public void SplitSevenZip_SelectsOnly001AsFirstVolume()
    {
        var files = Paths("movie.7z.001", "movie.7z.002", "movie.7z.003");

        var result = ArchiveExtractionService.FindFirstVolumes(files);

        result.VolumeSets.Should().ContainSingle()
            .Which.FirstVolume.Should().Be(Path.Combine(Dir, "movie.7z.001"));
        result.OrphanedContinuations.Should().BeEmpty();
    }

    [Fact]
    public void SplitSevenZipContinuationWithoutFirstVolume_ReportsOrphan()
    {
        var files = Paths("movie.7z.002");

        var result = ArchiveExtractionService.FindFirstVolumes(files);

        result.VolumeSets.Should().BeEmpty();
        result.OrphanedContinuations.Should().ContainSingle();
    }

    [Fact]
    public void MixedReleaseDirectory_SelectsExactlyTheRarFirstVolume()
    {
        var files = Paths(
            "show.s01e01.mkv", "show.nfo", "show.srt", "show.sub", "show.sfv",
            "show.rar", "show.r00", "show.r01");
        files.Add(Path.Combine(Dir, "Sample", "sample.mkv"));

        var result = ArchiveExtractionService.FindFirstVolumes(files);

        result.VolumeSets.Should().ContainSingle()
            .Which.FirstVolume.Should().Be(Path.Combine(Dir, "show.rar"));
        result.OrphanedContinuations.Should().BeEmpty();
    }

    [Fact]
    public void MediaFiles_AreNeverSelected()
    {
        var files = Paths("show.mkv", "show.mp4", "show.nfo", "show.srt", "show.sub", "show.idx");

        var result = ArchiveExtractionService.FindFirstVolumes(files);

        result.VolumeSets.Should().BeEmpty();
        result.OrphanedContinuations.Should().BeEmpty();
    }

    [Fact]
    public void TwoIndependentSets_ProduceTwoVolumeSets()
    {
        var files = Paths("cd1.rar", "cd1.r00", "cd2.rar", "cd2.r00");

        var result = ArchiveExtractionService.FindFirstVolumes(files);

        result.VolumeSets.Should().HaveCount(2);
        result.VolumeSets.Select(s => Path.GetFileName(s.FirstVolume))
            .Should().BeEquivalentTo("cd1.rar", "cd2.rar");
        result.VolumeSets.Should().AllSatisfy(s => s.Volumes.Should().HaveCount(2));
    }

    [Fact]
    public void ContinuationsInDifferentDirectory_DoNotAttachToFirstVolume()
    {
        var files = new List<string>
        {
            Path.Combine(Dir, "show.rar"),
            Path.Combine(Dir, "Subs", "show.r00")
        };

        var result = ArchiveExtractionService.FindFirstVolumes(files);

        result.VolumeSets.Should().ContainSingle()
            .Which.Volumes.Should().HaveCount(1);
        result.OrphanedContinuations.Should().ContainSingle();
    }
}
