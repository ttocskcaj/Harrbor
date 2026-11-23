using FluentAssertions;
using Harrbor.Services.Conversion;
using Xunit;

namespace Harrbor.Tests.Services.Conversion;

public class MediaProbeParserTests
{
    [Fact]
    public void Parse_P7SingleTrack_ReadsStreamsFormatAndDoviRecord()
    {
        var probe = ProbeFixtures.Load("p7-single-track");

        probe.Streams.Should().HaveCount(6);
        probe.DurationSeconds.Should().BeApproximately(7260.002, 0.0001);
        probe.FormatName.Should().Be("matroska,webm");
        probe.Title.Should().Be("Example Movie (2023)");
        probe.CountOf("audio").Should().Be(2);
        probe.CountOf("subtitle").Should().Be(2);

        var video = probe.Streams[0];
        video.CodecType.Should().Be("video");
        video.CodecName.Should().Be("hevc");
        video.RFrameRate.Should().Be("24000/1001");
        video.Language.Should().Be("eng");
        video.Title.Should().Be("DV FEL");
        video.SideData.Should().HaveCount(3);

        var dovi = video.DolbyVision;
        dovi.Should().NotBeNull();
        dovi!.Profile.Should().Be(7);
        dovi.Level.Should().Be(6);
        dovi.RpuPresent.Should().BeTrue();
        dovi.EnhancementLayerPresent.Should().BeTrue();
        dovi.BaseLayerPresent.Should().BeTrue();
        dovi.BlSignalCompatibilityId.Should().Be(6);
    }

    [Fact]
    public void Parse_AttachedPicture_ExcludedFromVideoStreams()
    {
        var probe = ProbeFixtures.Load("p7-single-track");

        probe.Streams.Last().IsAttachedPicture.Should().BeTrue();
        probe.VideoStreams.Should().ContainSingle().Which.Index.Should().Be(0);
    }

    [Theory]
    [InlineData("p7-dual-track", 7, 2)]
    [InlineData("p8-1", 8, 1)]
    [InlineData("p5", 5, 1)]
    public void Parse_DolbyVisionFixtures_ReadsProfile(string fixture, int profile, int videoCount)
    {
        var probe = ProbeFixtures.Load(fixture);

        probe.VideoStreams.Should().HaveCount(videoCount);
        probe.VideoStreams.First().DolbyVision!.Profile.Should().Be(profile);
    }

    [Theory]
    [InlineData("hdr10")]
    [InlineData("sdr")]
    public void Parse_NonDolbyVision_HasNoDoviRecord(string fixture)
    {
        var probe = ProbeFixtures.Load(fixture);

        probe.VideoStreams.Should().ContainSingle().Which.DolbyVision.Should().BeNull();
    }

    [Fact]
    public void Parse_HdrSideData_KeepsScalarValues()
    {
        var probe = ProbeFixtures.Load("hdr10");

        var cll = probe.Streams[0].SideData.Single(d => d.Type == "Content light level metadata");
        cll.GetInt("max_content").Should().Be(1000);
        cll.Values["max_average"].Should().Be("400");
    }

    [Fact]
    public void Parse_MinimalJson_ToleratesMissingFields()
    {
        var probe = MediaProbeParser.Parse("""{"streams":[{"index":0,"codec_type":"video"}],"format":{}}""");

        probe.Streams.Should().ContainSingle();
        probe.Streams[0].CodecName.Should().BeNull();
        probe.Streams[0].Tags.Should().BeEmpty();
        probe.DurationSeconds.Should().BeNull();
    }

    [Fact]
    public void Parse_TagsAreCaseInsensitive()
    {
        var probe = MediaProbeParser.Parse(
            """{"streams":[{"index":0,"codec_type":"video","tags":{"LANGUAGE":"ger","Title":"Main"}}]}""");

        probe.Streams[0].Language.Should().Be("ger");
        probe.Streams[0].Title.Should().Be("Main");
    }
}
