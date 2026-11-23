using System.Security.Cryptography;
using FakeItEasy;
using FluentAssertions;
using Harrbor.Configuration;
using Harrbor.Services.Conversion;
using Harrbor.Services.Conversion.Converters;
using Harrbor.Services.Processes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace Harrbor.Tests.Services.Conversion;

/// <summary>
/// Uses the real ffmpeg/ffprobe when they are on PATH; skipped otherwise.
/// </summary>
public class ConversionIntegrationTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"harrbor-int-{Guid.NewGuid():N}");
    private readonly ProcessRunner _runner = new(A.Fake<ILogger<ProcessRunner>>());

    public ConversionIntegrationTests()
    {
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    private async Task<bool> ToolWorksAsync(string tool)
    {
        try
        {
            return (await _runner.RunAsync(new ProcessSpec(tool, ["-version"]), TestContext.Current.CancellationToken)).ExitCode == 0;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }

    [Fact]
    public async Task DoviConverter_SyntheticHevcMkv_IsSkippedAndUnchanged()
    {
        var ct = TestContext.Current.CancellationToken;
        Assert.SkipUnless(await ToolWorksAsync("ffmpeg") && await ToolWorksAsync("ffprobe"), "ffmpeg/ffprobe not on PATH");

        var file = Path.Combine(_dir, "synthetic.mkv");
        var gen = await _runner.RunAsync(new ProcessSpec("ffmpeg",
            [
                "-nostdin", "-v", "error", "-y",
                "-f", "lavfi", "-i", "testsrc2=size=64x64:rate=24:duration=1",
                "-f", "lavfi", "-i", "sine=duration=1",
                "-c:v", "libx265", "-x265-params", "log-level=error",
                "-c:a", "aac",
                file
            ]), ct);
        Assert.SkipWhen(gen.ExitCode != 0, $"ffmpeg could not encode HEVC (libx265 missing?): {gen.StandardError}");

        var hashBefore = SHA256.HashData(await File.ReadAllBytesAsync(file, ct));
        var options = Options.Create(new ConversionOptions());
        var service = new ConversionService(new MediaProbeService(_runner, options), A.Fake<ILogger<ConversionService>>());
        var converter = new DoviP7ToP81Converter(_runner, options);

        var result = await service.ProcessAsync(_dir, [converter], new ConversionRunOptions(false, 0), ct);

        result.Success.Should().BeTrue();
        result.Outcomes.Should().ContainSingle().Which.Should().Match<ConversionOutcome>(
            o => o.Kind == ConversionOutcomeKind.Skipped && o.Detail == "no Dolby Vision");
        SHA256.HashData(await File.ReadAllBytesAsync(file, ct)).Should().Equal(hashBefore);
        Directory.EnumerateFileSystemEntries(_dir).Should().ContainSingle();
    }
}
