using FakeItEasy;
using FluentAssertions;
using Harrbor.Configuration;
using Harrbor.Services.Conversion;
using Harrbor.Services.Conversion.Converters;
using Harrbor.Services.Processes;
using Microsoft.Extensions.Options;
using Xunit;

namespace Harrbor.Tests.Services.Conversion;

public class DoviP7ToP81ConverterTests : IDisposable
{
    private readonly IProcessRunner _runner = A.Fake<IProcessRunner>();
    private readonly string _scratch = Path.Combine(Path.GetTempPath(), $"harrbor-dovi-{Guid.NewGuid():N}");
    private readonly ConversionOptions _options = new()
    {
        DurationToleranceSeconds = 1,
        Tools = new(StringComparer.OrdinalIgnoreCase) { ["mkvmerge"] = "/opt/mkvtoolnix/mkvmerge" }
    };

    public DoviP7ToP81ConverterTests()
    {
        Directory.CreateDirectory(_scratch);
    }

    public void Dispose()
    {
        if (Directory.Exists(_scratch))
            Directory.Delete(_scratch, recursive: true);
    }

    private DoviP7ToP81Converter CreateConverter() => new(_runner, Options.Create(_options));

    [Theory]
    [InlineData("p7-single-track", ConversionDecisionKind.Convert)]
    [InlineData("p7-dual-track", ConversionDecisionKind.Unsupported)]
    [InlineData("p8-1", ConversionDecisionKind.Skip)]
    [InlineData("p5", ConversionDecisionKind.Skip)]
    [InlineData("hdr10", ConversionDecisionKind.Skip)]
    [InlineData("sdr", ConversionDecisionKind.Skip)]
    public void Evaluate_Fixture_ReturnsExpectedDecision(string fixture, ConversionDecisionKind expected)
    {
        var decision = CreateConverter().Evaluate(ProbeFixtures.Load(fixture));

        decision.Kind.Should().Be(expected);
        decision.Reason.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Evaluate_NoVideo_Skips()
    {
        var probe = MediaProbeParser.Parse("""{"streams":[{"index":0,"codec_type":"audio"}],"format":{}}""");

        CreateConverter().Evaluate(probe).Kind.Should().Be(ConversionDecisionKind.Skip);
    }

    [Fact]
    public void Evaluate_P7WithoutFrameRate_Unsupported()
    {
        var json = ProbeFixtures.Json("p7-single-track")
            .Replace("\"24000/1001\"", "\"0/0\"");

        CreateConverter().Evaluate(MediaProbeParser.Parse(json)).Kind.Should().Be(ConversionDecisionKind.Unsupported);
    }

    [Fact]
    public void Verify_P81OutputMatchingSource_Passes()
    {
        var source = ProbeFixtures.Load("p7-single-track");
        var output = ProbeFixtures.Load("p8-1");

        CreateConverter().Verify(source, output).Passed.Should().BeTrue();
    }

    [Fact]
    public void Verify_OutputStillP7_Fails()
    {
        var source = ProbeFixtures.Load("p7-single-track");

        var result = CreateConverter().Verify(source, source);

        result.Passed.Should().BeFalse();
        result.FailureReason.Should().Contain("profile is 7");
    }

    [Fact]
    public void Verify_OutputWithoutDolbyVision_Fails()
    {
        var result = CreateConverter().Verify(ProbeFixtures.Load("p7-single-track"), ProbeFixtures.Load("hdr10"));

        result.Passed.Should().BeFalse();
        result.FailureReason.Should().Contain("no Dolby Vision");
    }

    [Fact]
    public void Verify_WrongCompatibilityId_Fails()
    {
        var output = MediaProbeParser.Parse(ProbeFixtures.Json("p8-1")
            .Replace("\"dv_bl_signal_compatibility_id\": 1", "\"dv_bl_signal_compatibility_id\": 4"));

        var result = CreateConverter().Verify(ProbeFixtures.Load("p7-single-track"), output);

        result.Passed.Should().BeFalse();
        result.FailureReason.Should().Contain("compatibility id is 4");
    }

    [Theory]
    [InlineData("7260.002000", true)]
    [InlineData("7260.900000", true)]
    [InlineData("7261.500000", false)]
    [InlineData("7000.000000", false)]
    public void Verify_DurationTolerance(string outputDuration, bool passes)
    {
        var output = MediaProbeParser.Parse(ProbeFixtures.Json("p8-1")
            .Replace("\"duration\": \"7260.002000\"", $"\"duration\": \"{outputDuration}\""));

        CreateConverter().Verify(ProbeFixtures.Load("p7-single-track"), output).Passed.Should().Be(passes);
    }

    [Fact]
    public void Verify_MissingSubtitleStream_Fails()
    {
        var source = ProbeFixtures.Load("p7-single-track");
        var full = ProbeFixtures.Load("p8-1");
        var output = full with { Streams = full.Streams.Where(s => s.Index != 4).ToList() };

        var result = CreateConverter().Verify(source, output);

        result.Passed.Should().BeFalse();
        result.FailureReason.Should().Contain("1 subtitle stream(s), source has 2");
    }

    [Fact]
    public async Task ConvertAsync_BuildsExpectedPipelineAndMux()
    {
        var probe = ProbeFixtures.Load("p7-single-track");
        var hevc = Path.Combine(_scratch, "video.hevc");
        IReadOnlyList<ProcessSpec>? pipeline = null;
        ProcessSpec? mux = null;

        A.CallTo(() => _runner.RunPipelineAsync(A<IReadOnlyList<ProcessSpec>>._, A<CancellationToken>._))
            .ReturnsLazily((IReadOnlyList<ProcessSpec> specs, CancellationToken _) =>
            {
                pipeline = specs;
                File.WriteAllText(hevc, "hevc");
                return specs.Select(s => new ProcessResult(s.FileName, 0, "", "")).ToList();
            });
        A.CallTo(() => _runner.RunAsync(A<ProcessSpec>._, A<CancellationToken>._))
            .ReturnsLazily((ProcessSpec spec, CancellationToken _) =>
            {
                mux = spec;
                return new ProcessResult(spec.FileName, 1, "Warning: something minor", ""); // warnings are OK
            });

        await CreateConverter().ConvertAsync("/in/movie.mkv", "/in/.movie.harrbor-tmp.mkv", probe, _scratch, CancellationToken.None);

        pipeline.Should().NotBeNull();
        pipeline!.Should().HaveCount(2);
        pipeline[0].FileName.Should().Be("ffmpeg");
        pipeline[0].Arguments.Should().Equal(
            "-nostdin", "-v", "error", "-i", "/in/movie.mkv", "-map", "0:0", "-c:v", "copy",
            "-bsf:v", "hevc_mp4toannexb", "-f", "hevc", "-");
        pipeline[1].FileName.Should().Be("dovi_tool");
        pipeline[1].Arguments.Should().Equal("-m", "2", "convert", "--discard", "-", "-o", hevc);

        mux.Should().NotBeNull();
        mux!.FileName.Should().Be("/opt/mkvtoolnix/mkvmerge");
        mux.Arguments.Should().Equal(
            "-o", "/in/.movie.harrbor-tmp.mkv",
            "--title", "Example Movie (2023)",
            "--language", "0:eng",
            "--track-name", "0:DV FEL",
            "--default-duration", "0:24000/1001p", hevc,
            "--no-video", "/in/movie.mkv");
    }

    [Fact]
    public async Task ConvertAsync_UnknownLanguageAndNoTitle_UsesUnd()
    {
        var json = ProbeFixtures.Json("p7-single-track")
            .Replace("\"language\": \"eng\",\n        \"DURATION\"", "\"DURATION\"")
            .Replace("\"title\": \"DV FEL\"", "\"x\": \"y\"");
        var probe = MediaProbeParser.Parse(json);
        probe.Streams[0].Language.Should().BeNull("fixture edit should remove the video language");
        ProcessSpec? mux = null;
        A.CallTo(() => _runner.RunPipelineAsync(A<IReadOnlyList<ProcessSpec>>._, A<CancellationToken>._))
            .ReturnsLazily((IReadOnlyList<ProcessSpec> specs, CancellationToken _) =>
            {
                File.WriteAllText(Path.Combine(_scratch, "video.hevc"), "hevc");
                return specs.Select(s => new ProcessResult(s.FileName, 0, "", "")).ToList();
            });
        A.CallTo(() => _runner.RunAsync(A<ProcessSpec>._, A<CancellationToken>._))
            .ReturnsLazily((ProcessSpec spec, CancellationToken _) => { mux = spec; return new ProcessResult(spec.FileName, 0, "", ""); });

        await CreateConverter().ConvertAsync("/in/m.mkv", "/in/out.mkv", probe, _scratch, CancellationToken.None);

        mux!.Arguments.Should().ContainInConsecutiveOrder("--language", "0:und");
        mux.Arguments.Should().NotContain("--track-name");
    }

    [Fact]
    public async Task ConvertAsync_DoviToolFails_ThrowsWithoutMuxing()
    {
        A.CallTo(() => _runner.RunPipelineAsync(A<IReadOnlyList<ProcessSpec>>._, A<CancellationToken>._))
            .ReturnsLazily((IReadOnlyList<ProcessSpec> specs, CancellationToken _) =>
                new List<ProcessResult>
                {
                    new(specs[0].FileName, 0, "", ""),
                    new(specs[1].FileName, 1, "", "Error: invalid RPU")
                });

        var act = () => CreateConverter().ConvertAsync(
            "/in/m.mkv", "/in/out.mkv", ProbeFixtures.Load("p7-single-track"), _scratch, CancellationToken.None);

        await act.Should().ThrowAsync<ProcessFailedException>().WithMessage("*dovi_tool*invalid RPU*");
        A.CallTo(() => _runner.RunAsync(A<ProcessSpec>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task ConvertAsync_MkvmergeError_ThrowsWithStdout()
    {
        A.CallTo(() => _runner.RunPipelineAsync(A<IReadOnlyList<ProcessSpec>>._, A<CancellationToken>._))
            .ReturnsLazily((IReadOnlyList<ProcessSpec> specs, CancellationToken _) =>
            {
                File.WriteAllText(Path.Combine(_scratch, "video.hevc"), "hevc");
                return specs.Select(s => new ProcessResult(s.FileName, 0, "", "")).ToList();
            });
        A.CallTo(() => _runner.RunAsync(A<ProcessSpec>._, A<CancellationToken>._))
            .Returns(new ProcessResult("mkvmerge", 2, "Error: no space left", ""));

        var act = () => CreateConverter().ConvertAsync(
            "/in/m.mkv", "/in/out.mkv", ProbeFixtures.Load("p7-single-track"), _scratch, CancellationToken.None);

        await act.Should().ThrowAsync<ProcessFailedException>().WithMessage("*no space left*");
    }

    [Fact]
    public void RealToolOutput_RemuxedFilePassesVerificationAndIsSkipped()
    {
        // Unedited ffprobe output from running this converter's commands with the real tools (see Fixtures/Probe/README.md)
        var source = ProbeFixtures.Load("real-p8-1-source");
        var output = ProbeFixtures.Load("real-p8-1-remuxed");
        var converter = CreateConverter();

        output.VideoStreams.Single().DolbyVision!.BlSignalCompatibilityId.Should().Be(1);
        converter.Verify(source, output).Passed.Should().BeTrue();
        converter.Evaluate(output).Should().Be(ConversionDecision.Skip("Dolby Vision profile 8"));
    }
}
