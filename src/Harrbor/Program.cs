using Microsoft.EntityFrameworkCore;
using Serilog;
using Harrbor.Configuration;
using Harrbor.Data;
using Harrbor.Data.Entities;
using Harrbor.HealthChecks;
using Harrbor.Services;
using Harrbor.Services.Clients;
using Harrbor.Services.Conversion;
using Harrbor.Services.Extraction;
using Harrbor.Services.Orchestration;
using Harrbor.Services.RemoteStorage;
using Harrbor.Services.Orchestration.Phases;

// CLI mode: `dotnet Harrbor.dll convert <path> --converter <Name> [--dry-run]` (no DB, no jobs)
if (args.Length > 0 && args[0] == ConversionCli.Verb)
    return await ConversionCli.RunAsync(args[1..]);

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting Harrbor");

    var builder = WebApplication.CreateBuilder(args);

    builder.Configuration
        .AddJsonFile("appsettings.default.json")
        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
        .AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: true)
        .AddEnvironmentVariables();

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    // Configuration
    builder.Services.Configure<HarrborOptions>(
        builder.Configuration.GetSection(HarrborOptions.SectionName));
    builder.Services.Configure<QBittorrentOptions>(
        builder.Configuration.GetSection(QBittorrentOptions.SectionName));
    builder.Services.Configure<SonarrOptions>(
        builder.Configuration.GetSection(SonarrOptions.SectionName));
    builder.Services.Configure<RadarrOptions>(
        builder.Configuration.GetSection(RadarrOptions.SectionName));
    builder.Services.Configure<RemoteStorageOptions>(
        builder.Configuration.GetSection(RemoteStorageOptions.SectionName));
    builder.Services.Configure<JobOptions>(
        builder.Configuration.GetSection(JobOptions.SectionName));

    // Database
    var harrborOptions = builder.Configuration
        .GetSection(HarrborOptions.SectionName)
        .Get<HarrborOptions>() ?? new HarrborOptions();

    var dbPath = Path.Combine(harrborOptions.DataPath, "harrbor.sqlite");
    Directory.CreateDirectory(harrborOptions.DataPath);

    builder.Services.AddDbContext<HarrborDbContext>(options =>
        options.UseSqlite($"Data Source={dbPath}"));

    // Services
    builder.Services.AddSingleton<IQBittorrentClient, QBittorrentClientWrapper>();
    builder.Services.AddScoped<IRemoteStorageService, RcloneRemoteStorageService>();
    builder.Services.AddScoped<IArchiveExtractionService, ArchiveExtractionService>();
    builder.Services.AddScoped<IMediaServiceResolver, MediaServiceResolver>();
    builder.Services.AddMediaConversion(builder.Configuration);

    builder.Services.AddHttpClient<ISonarrClient, SonarrClient>()
        .AddStandardResilienceHandler();

    builder.Services.AddHttpClient<IRadarrClient, RadarrClient>()
        .AddStandardResilienceHandler();

    // Startup validation
    builder.Services.AddTransient<StartupValidator>();

    // Phase Handlers
    builder.Services.AddSingleton<TransferCoordinator>();
    builder.Services.AddSingleton<ConversionCoordinator>();
    builder.Services.AddScoped<IDiscoveryPhaseHandler, DiscoveryPhaseHandler>();
    builder.Services.AddScoped<IDownloadPhaseHandler, DownloadPhaseHandler>();
    builder.Services.AddScoped<ITransferPhaseHandler, TransferPhaseHandler>();
    builder.Services.AddScoped<IExtractionPhaseHandler, ExtractionPhaseHandler>();
    builder.Services.AddScoped<IConversionPhaseHandler, ConversionPhaseHandler>();
    builder.Services.AddScoped<IPublishPhaseHandler, PublishPhaseHandler>();
    builder.Services.AddScoped<IImportPhaseHandler, ImportPhaseHandler>();
    builder.Services.AddScoped<ICleanupPhaseHandler, CleanupPhaseHandler>();
    builder.Services.AddScoped<IArchivalPhaseHandler, ArchivalPhaseHandler>();

    // Background Worker
    builder.Services.AddHostedService<OrchestrationWorker>();

    // Health Checks
    builder.Services.AddHealthChecks()
        .AddDbContextCheck<HarrborDbContext>("database", tags: ["ready"])
        .AddCheck<QBittorrentHealthCheck>("qbittorrent", tags: ["ready"])
        .AddCheck<SonarrHealthCheck>("sonarr", tags: ["ready"])
        .AddCheck<RadarrHealthCheck>("radarr", tags: ["ready"]);

    var app = builder.Build();

    // Apply migrations on startup
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<HarrborDbContext>();
        db.Database.Migrate();

        // Reset any stuck InProgress transfers (from interrupted runs)
        var stuckTransfers = await db.TrackedReleases
            .Where(r => r.TransferStatus == TransferStatus.InProgress)
            .ToListAsync();

        if (stuckTransfers.Count > 0)
        {
            foreach (var release in stuckTransfers)
            {
                release.TransferStatus = TransferStatus.Pending;
                release.TransferStartedAtUtc = null;
            }
            await db.SaveChangesAsync();

            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
            logger.LogInformation(
                "Reset {Count} stuck transfers to Pending state",
                stuckTransfers.Count);
        }

        // Reset any stuck InProgress extractions (safe to redo: extraction overwrites existing output)
        var stuckExtractions = await db.TrackedReleases
            .Where(r => r.ExtractionStatus == ExtractionStatus.InProgress)
            .ToListAsync();

        if (stuckExtractions.Count > 0)
        {
            foreach (var release in stuckExtractions)
            {
                release.ExtractionStatus = ExtractionStatus.Pending;
                release.ExtractionStartedAtUtc = null;
            }
            await db.SaveChangesAsync();

            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
            logger.LogInformation(
                "Reset {Count} stuck extractions to Pending state",
                stuckExtractions.Count);
        }

        // Reset any stuck InProgress conversions. The original file is only replaced by an atomic rename
        // after verification, so redoing is safe; leftover temp/scratch files are removed below.
        var stuckConversions = await db.TrackedReleases
            .Where(r => r.ConversionStatus == ConversionStatus.InProgress)
            .ToListAsync();

        if (stuckConversions.Count > 0)
        {
            foreach (var release in stuckConversions)
            {
                release.ConversionStatus = ConversionStatus.Pending;
                release.ConversionStartedAtUtc = null;
            }
            await db.SaveChangesAsync();

            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
            logger.LogInformation(
                "Reset {Count} stuck conversions to Pending state",
                stuckConversions.Count);
        }

        var jobDefinitions = scope.ServiceProvider
            .GetRequiredService<Microsoft.Extensions.Options.IOptions<JobOptions>>().Value.Definitions;

        // Work out the transfer item of rows from before RemoteItemPath existed (needs the job definitions)
        var backfilled = await RemoteItemPathBackfill.ApplyAsync(db, jobDefinitions);
        if (backfilled > 0)
        {
            scope.ServiceProvider.GetRequiredService<ILogger<Program>>().LogInformation(
                "Set the remote transfer item for {Count} release(s) tracked before 0.0.9", backfilled);
        }

        // Ensure holding dirs exist and remove conversion leftovers from interrupted runs
        foreach (var stagingPath in jobDefinitions
            .Where(j => j.Enabled && !string.IsNullOrEmpty(j.StagingPath))
            .Select(j => j.StagingPath)
            .Distinct())
        {
            var holdingRoot = Path.Combine(stagingPath, TrackedRelease.HoldingDirectoryName);
            try
            {
                Directory.CreateDirectory(holdingRoot);
                var removed = ConversionArtifacts.CleanupLeftovers(holdingRoot);
                if (removed > 0)
                {
                    scope.ServiceProvider.GetRequiredService<ILogger<Program>>().LogInformation(
                        "Removed {Count} leftover conversion temp file(s) under {Path}", removed, holdingRoot);
                }
            }
            catch (Exception ex)
            {
                scope.ServiceProvider.GetRequiredService<ILogger<Program>>().LogWarning(
                    ex, "Could not prepare holding directory {Path}", holdingRoot);
            }
        }

        // Ensure the conversion scratch dir exists and remove scratch dirs left by interrupted runs
        var scratchPath = scope.ServiceProvider
            .GetRequiredService<Microsoft.Extensions.Options.IOptions<ConversionOptions>>().Value.ScratchPath;
        if (!string.IsNullOrWhiteSpace(scratchPath))
        {
            try
            {
                Directory.CreateDirectory(scratchPath);
                var removed = ConversionArtifacts.CleanupLeftovers(scratchPath);
                if (removed > 0)
                {
                    scope.ServiceProvider.GetRequiredService<ILogger<Program>>().LogInformation(
                        "Removed {Count} leftover conversion scratch dir(s) under {Path}", removed, scratchPath);
                }
            }
            catch (Exception ex)
            {
                scope.ServiceProvider.GetRequiredService<ILogger<Program>>().LogWarning(
                    ex, "Could not prepare conversion scratch directory {Path}", scratchPath);
            }
        }
    }

    // Validate external service connections
    using (var scope = app.Services.CreateScope())
    {
        var validator = scope.ServiceProvider.GetRequiredService<StartupValidator>();
        await validator.ValidateAsync();
    }

    app.UseSerilogRequestLogging();

    // Health check endpoints
    app.MapHealthChecks("/health");
    app.MapHealthChecks("/health/ready", new()
    {
        Predicate = check => check.Tags.Contains("ready")
    });
    app.MapHealthChecks("/health/live", new()
    {
        Predicate = _ => false // Liveness just checks the app is running
    });

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
    return 1;
}
finally
{
    Log.CloseAndFlush();
}

return 0;
