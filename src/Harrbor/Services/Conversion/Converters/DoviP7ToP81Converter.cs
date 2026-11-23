using Harrbor.Configuration;
using Harrbor.Services.Processes;
using Microsoft.Extensions.Options;

namespace Harrbor.Services.Conversion.Converters;

/// <summary>
/// Rewrites single-track Dolby Vision profile 7 (UHD Blu-ray dual-layer) MKVs to profile 8.1 by
/// discarding the enhancement layer and converting the RPU. Lossless for the base layer; all audio,
/// subtitle, chapter, attachment and tag data is kept.
/// </summary>
public class DoviP7ToP81Converter : IMediaConverter
{
    public const string ConverterName = "DoviP7ToP81";
    private const string ScratchVideoFileName = "video.hevc";

    private readonly IProcessRunner _processRunner;
    private readonly ConversionOptions _options;

    public DoviP7ToP81Converter(IProcessRunner processRunner, IOptions<ConversionOptions> options)
    {
        _processRunner = processRunner;
        _options = options.Value;
    }

    public string Name => ConverterName;

    public IReadOnlyCollection<string> Extensions { get; } = [".mkv"];

    public IReadOnlyCollection<string> RequiredTools { get; } = ["ffprobe", "ffmpeg", "mkvmerge", "dovi_tool"];

    public ConversionDecision Evaluate(MediaProbe probe)
    {
        var videos = probe.VideoStreams.ToList();
        if (videos.Count == 0)
            return ConversionDecision.Skip("no video stream");

        var video = videos[0];
        var dovi = video.DolbyVision;
        var doviTracks = videos.Count(v => v.DolbyVision is not null);

        if (dovi is null)
        {
            return videos.Any(v => v.DolbyVision?.Profile == 7)
                ? ConversionDecision.Unsupported("Dolby Vision profile 7 in a non-primary video track")
                : ConversionDecision.Skip("no Dolby Vision");
        }

        if (dovi.Profile != 7)
            return ConversionDecision.Skip($"Dolby Vision profile {dovi.Profile}");

        if (!string.Equals(video.CodecName, "hevc", StringComparison.OrdinalIgnoreCase))
            return ConversionDecision.Unsupported($"Dolby Vision profile 7 with codec '{video.CodecName}'");

        if (doviTracks > 1)
            return ConversionDecision.Unsupported("dual-track Dolby Vision profile 7 (enhancement layer in a separate track)");

        if (videos.Count > 1)
            return ConversionDecision.Unsupported($"{videos.Count} video streams; only single-video files are converted");

        if (GetFrameRate(video) is null)
            return ConversionDecision.Unsupported("video stream has no usable frame rate");

        return ConversionDecision.Convert("Dolby Vision profile 7");
    }

    public async Task ConvertAsync(
        string inputPath,
        string outputPath,
        MediaProbe probe,
        string scratchDir,
        CancellationToken cancellationToken)
    {
        var video = probe.VideoStreams.First();
        var frameRate = GetFrameRate(video)
            ?? throw new InvalidOperationException("video stream has no usable frame rate");
        var hevcPath = Path.Combine(scratchDir, ScratchVideoFileName);

        // 1. Extract the video as Annex-B HEVC and convert the RPU to 8.1, discarding the EL.
        var pipeline = await _processRunner.RunPipelineAsync(
            [
                new ProcessSpec(_options.ResolveTool("ffmpeg"),
                [
                    "-nostdin", "-v", "error",
                    "-i", inputPath,
                    // Map by absolute index so cover-art "video" streams can never be picked.
                    "-map", $"0:{video.Index}",
                    "-c:v", "copy",
                    "-bsf:v", "hevc_mp4toannexb",
                    "-f", "hevc", "-"
                ]),
                new ProcessSpec(_options.ResolveTool("dovi_tool"),
                [
                    "-m", "2", "convert", "--discard", "-", "-o", hevcPath
                ])
            ],
            cancellationToken);

        foreach (var stage in pipeline)
        {
            if (!stage.Succeeded())
                throw new ProcessFailedException(stage);
        }

        if (!File.Exists(hevcPath) || new FileInfo(hevcPath).Length == 0)
            throw new InvalidOperationException("dovi_tool produced no output");

        // 2. Remux: new video first, then everything except video from the source.
        var mkvmergeArgs = new List<string> { "-o", outputPath };
        if (!string.IsNullOrEmpty(probe.Title))
            mkvmergeArgs.AddRange(["--title", probe.Title]);
        mkvmergeArgs.AddRange(["--language", $"0:{(string.IsNullOrEmpty(video.Language) ? "und" : video.Language)}"]);
        if (!string.IsNullOrEmpty(video.Title))
            mkvmergeArgs.AddRange(["--track-name", $"0:{video.Title}"]);
        mkvmergeArgs.AddRange(["--default-duration", $"0:{frameRate}p", hevcPath]);
        mkvmergeArgs.AddRange(["--no-video", inputPath]);

        var mux = await _processRunner.RunAsync(
            new ProcessSpec(_options.ResolveTool("mkvmerge"), mkvmergeArgs),
            cancellationToken);

        // mkvmerge: 0 = ok, 1 = warnings (output is fine), 2 = error. It reports errors on stdout.
        if (!mux.Succeeded(1))
            throw new ProcessFailedException(mux with { StandardError = mux.StandardError + mux.StandardOutput });
    }

    public ConversionVerification Verify(MediaProbe source, MediaProbe output)
    {
        var video = output.VideoStreams.FirstOrDefault();
        var dovi = video?.DolbyVision;
        if (dovi is null)
            return ConversionVerification.Fail("output has no Dolby Vision video stream");
        if (dovi.Profile != 8)
            return ConversionVerification.Fail($"output Dolby Vision profile is {dovi.Profile}, expected 8");
        if (dovi.BlSignalCompatibilityId != 1)
            return ConversionVerification.Fail($"output BL compatibility id is {dovi.BlSignalCompatibilityId?.ToString() ?? "missing"}, expected 1");

        if (source.DurationSeconds is { } srcDuration)
        {
            if (output.DurationSeconds is not { } outDuration)
                return ConversionVerification.Fail("output has no duration");
            if (Math.Abs(outDuration - srcDuration) > _options.DurationToleranceSeconds)
                return ConversionVerification.Fail($"duration {outDuration:0.###}s differs from source {srcDuration:0.###}s");
        }

        foreach (var type in new[] { "audio", "subtitle" })
        {
            var expected = source.CountOf(type);
            var actual = output.CountOf(type);
            if (expected != actual)
                return ConversionVerification.Fail($"output has {actual} {type} stream(s), source has {expected}");
        }

        return ConversionVerification.Pass();
    }

    private static string? GetFrameRate(MediaStream video)
    {
        foreach (var rate in new[] { video.RFrameRate, video.AvgFrameRate })
        {
            if (string.IsNullOrEmpty(rate))
                continue;
            var parts = rate.Split('/');
            if (parts.Length == 2
                && long.TryParse(parts[0], out var num) && num > 0
                && long.TryParse(parts[1], out var den) && den > 0)
            {
                return rate;
            }
        }
        return null;
    }
}
