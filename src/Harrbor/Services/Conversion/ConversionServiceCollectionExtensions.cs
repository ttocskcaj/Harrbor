using Harrbor.Configuration;
using Harrbor.Services.Conversion.Converters;
using Harrbor.Services.Processes;

namespace Harrbor.Services.Conversion;

public static class ConversionServiceCollectionExtensions
{
    /// <summary>
    /// Registers conversion infrastructure and all converters. Shared by the web host and the <c>convert</c> CLI verb.
    /// To add a converter, implement <see cref="IMediaConverter"/> and register it here.
    /// </summary>
    public static IServiceCollection AddMediaConversion(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ConversionOptions>(configuration.GetSection(ConversionOptions.SectionName));

        services.AddSingleton<IProcessRunner, ProcessRunner>();
        services.AddSingleton<IMediaProbeService, MediaProbeService>();
        services.AddSingleton<IConversionService, ConversionService>();
        services.AddSingleton<ConverterRegistry>();
        services.AddSingleton<ToolChecker>();

        // Converters
        services.AddSingleton<IMediaConverter, DoviP7ToP81Converter>();

        return services;
    }
}
