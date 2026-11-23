using Harrbor.Configuration;
using Harrbor.Services.Processes;
using Microsoft.Extensions.Options;

namespace Harrbor.Services.Conversion;

public interface IMediaProbeService
{
    Task<MediaProbe> ProbeAsync(string path, CancellationToken cancellationToken = default);
}

public class MediaProbeService : IMediaProbeService
{
    private readonly IProcessRunner _processRunner;
    private readonly ConversionOptions _options;

    public MediaProbeService(IProcessRunner processRunner, IOptions<ConversionOptions> options)
    {
        _processRunner = processRunner;
        _options = options.Value;
    }

    public async Task<MediaProbe> ProbeAsync(string path, CancellationToken cancellationToken = default)
    {
        var result = await _processRunner.RunAsync(
            new ProcessSpec(
                _options.ResolveTool("ffprobe"),
                ["-v", "error", "-show_streams", "-show_format", "-of", "json", path]),
            cancellationToken);

        if (!result.Succeeded())
            throw new ProcessFailedException(result);

        return MediaProbeParser.Parse(result.StandardOutput);
    }
}
