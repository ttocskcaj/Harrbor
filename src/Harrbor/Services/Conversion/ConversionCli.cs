using Harrbor.Configuration;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Events;

namespace Harrbor.Services.Conversion;

/// <summary>
/// <c>dotnet Harrbor.dll convert &lt;path&gt; --converter &lt;Name&gt; [--converter &lt;Name&gt;…] [--dry-run] [--min-size &lt;bytes&gt;]</c>
/// Runs converters over a file or directory without a database or jobs, e.g. to backfill an existing library.
/// Tool paths and defaults come from the <c>Conversion</c> config section (appsettings / environment).
/// </summary>
public static class ConversionCli
{
    public const string Verb = "convert";

    private const string Usage =
        "Usage: convert <path> --converter <Name> [--converter <Name>...] [--dry-run] [--min-size <bytes>]";

    public static async Task<int> RunAsync(string[] args)
    {
        string? path = null;
        var converterNames = new List<string>();
        var dryRun = false;
        long? minSize = null;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--converter" or "-c" when i + 1 < args.Length:
                    converterNames.Add(args[++i]);
                    break;
                case "--dry-run":
                    dryRun = true;
                    break;
                case "--min-size" when i + 1 < args.Length && long.TryParse(args[i + 1], out var bytes):
                    minSize = bytes;
                    i++;
                    break;
                case "--help" or "-h":
                    Console.WriteLine(Usage);
                    return 0;
                case var a when !a.StartsWith('-') && path is null:
                    path = a;
                    break;
                default:
                    Console.Error.WriteLine($"Unexpected argument '{args[i]}'");
                    Console.Error.WriteLine(Usage);
                    return 2;
            }
        }

        if (path is null || converterNames.Count == 0)
        {
            Console.Error.WriteLine(Usage);
            return 2;
        }

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}",
                standardErrorFromLevel: LogEventLevel.Verbose)
            .CreateLogger();

        try
        {
            var builder = Host.CreateApplicationBuilder();
            builder.Configuration
                .AddJsonFile("appsettings.default.json", optional: true)
                .AddJsonFile("appsettings.json", optional: true)
                .AddJsonFile("appsettings.local.json", optional: true)
                .AddEnvironmentVariables();
            builder.Logging.ClearProviders();
            builder.Services.AddSerilog();
            builder.Services.AddMediaConversion(builder.Configuration);

            using var host = builder.Build();
            var services = host.Services;
            var options = services.GetRequiredService<IOptions<ConversionOptions>>().Value;

            IReadOnlyList<IMediaConverter> converters;
            try
            {
                converters = services.GetRequiredService<ConverterRegistry>().Resolve(converterNames);
            }
            catch (InvalidOperationException ex)
            {
                Console.Error.WriteLine(ex.Message);
                return 2;
            }

            dryRun |= options.DryRun;
            if (!dryRun)
            {
                var problems = await services.GetRequiredService<ToolChecker>()
                    .FindProblemsAsync(converters.SelectMany(c => c.RequiredTools));
                if (problems.Count > 0)
                {
                    foreach (var problem in problems)
                        Console.Error.WriteLine($"Missing tool: {problem}");
                    return 2;
                }
            }

            using var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
            };

            var result = await services.GetRequiredService<IConversionService>().ProcessAsync(
                Path.GetFullPath(path),
                converters,
                new ConversionRunOptions(dryRun, minSize ?? options.MinFileSizeBytes),
                cts.Token);

            foreach (var outcome in result.Outcomes)
            {
                var detail = outcome.Detail is null ? "" : $"  ({outcome.Detail})";
                Console.WriteLine($"{outcome.Kind,-12} {outcome.Converter,-14} {outcome.FilePath}{detail}");
            }
            Console.WriteLine($"Done in {result.Duration:hh\\:mm\\:ss}: {result.Summary}");

            return result.Success ? 0 : 1;
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("Cancelled");
            return 130;
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Conversion run failed");
            return 1;
        }
        finally
        {
            await Log.CloseAndFlushAsync();
        }
    }
}
