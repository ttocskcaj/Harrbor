using FluentAssertions;
using Harrbor.Services.Orchestration;
using Xunit;

namespace Harrbor.Tests.Services.Orchestration;

public class StagedLayoutCheckTests
{
    private const string Dune = "Dune.Part.Two-FraMeSToR";

    [Fact]
    public void SameNamespace_OutputInsideFinalItem_Matches()
    {
        var result = StagedLayoutCheck.Evaluate(
            $"/staging/radarr/{Dune}/{Dune}.mkv", "/staging/radarr", $"/staging/radarr/{Dune}",
            "/downloads/radarr", $"/downloads/radarr/{Dune}/{Dune}.mkv");

        result.Matches.Should().BeTrue();
    }

    [Fact]
    public void SameNamespace_FileItemEqualToOutput_Matches()
    {
        var result = StagedLayoutCheck.Evaluate(
            "/staging/radarr/Kubo.mkv", "/staging/radarr", "/staging/radarr/Kubo.mkv",
            "/downloads/radarr", "/downloads/radarr/Kubo.mkv");

        result.Matches.Should().BeTrue();
    }

    [Fact]
    public void SameNamespace_WrongItemName_DoesNotMatch()
    {
        var result = StagedLayoutCheck.Evaluate(
            $"/staging/radarr/{Dune}/{Dune}.mkv", "/staging/radarr", $"/staging/radarr/{Dune}.mkv",
            "/downloads/radarr", $"/downloads/radarr/{Dune}/{Dune}.mkv");

        result.Matches.Should().BeFalse();
    }

    [Fact]
    public void SameNamespace_SiblingWithCommonPrefix_DoesNotMatch()
    {
        var result = StagedLayoutCheck.Evaluate(
            "/staging/radarr/Movie.2024.extended/x.mkv", "/staging/radarr", "/staging/radarr/Movie.2024",
            "/downloads/radarr", "/downloads/radarr/Movie.2024.extended/x.mkv");

        result.Matches.Should().BeFalse();
    }

    [Fact]
    public void DifferentMounts_CorrectLayout_Matches()
    {
        var result = StagedLayoutCheck.Evaluate(
            $"/media/seedbox-import/radarr/{Dune}/{Dune}.mkv", "/staging/radarr", $"/staging/radarr/{Dune}",
            "/downloads/radarr", $"/downloads/radarr/{Dune}/{Dune}.mkv");

        result.Matches.Should().BeTrue();
        result.ExpectedPath.Should().Be($"/media/seedbox-import/radarr/{Dune}");
    }

    [Fact]
    public void DifferentMounts_DuneBugLayout_DoesNotMatch()
    {
        var result = StagedLayoutCheck.Evaluate(
            $"/media/seedbox-import/radarr/{Dune}/{Dune}.mkv", "/staging/radarr", $"/staging/radarr/{Dune}.mkv",
            "/downloads/radarr", $"/downloads/radarr/{Dune}/{Dune}.mkv");

        result.Matches.Should().BeFalse();
        result.ExpectedPath.Should().Be($"/media/seedbox-import/radarr/{Dune}.mkv");
    }

    [Theory]
    [InlineData("/media/import/Other/thing.mkv")]            // output doesn't end with the content's relative path
    public void DifferentMounts_CannotRelate_ReturnsUnknown(string outputPath)
    {
        var result = StagedLayoutCheck.Evaluate(
            outputPath, "/staging/radarr", $"/staging/radarr/{Dune}",
            "/downloads/radarr", $"/downloads/radarr/{Dune}/{Dune}.mkv");

        result.Matches.Should().BeNull();
    }

    [Fact]
    public void ContentOutsideRemoteRoot_ReturnsUnknown()
    {
        var result = StagedLayoutCheck.Evaluate(
            $"/media/import/{Dune}/{Dune}.mkv", "/staging/radarr", $"/staging/radarr/{Dune}",
            "/downloads/radarr", $"/other/{Dune}/{Dune}.mkv");

        result.Matches.Should().BeNull();
    }
}
