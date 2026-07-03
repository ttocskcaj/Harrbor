using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Serilog;
using Harrbor.Api;
using Harrbor.Configuration;
using Harrbor.Data;
using Harrbor.Data.Entities;
using Harrbor.HealthChecks;
using Harrbor.Services;
using Harrbor.Services.Clients;
using Harrbor.Services.Configuration;
using Harrbor.Services.Orchestration;
using Harrbor.Services.RemoteStorage;
using Harrbor.Services.Orchestration.Phases;

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

    // Bootstrap configuration (connection + job config lives in the database)
    builder.Services.Configure<HarrborOptions>(
        builder.Configuration.GetSection(HarrborOptions.SectionName));

    // Database
    var harrborOptions = builder.Configuration
        .GetSection(HarrborOptions.SectionName)
        .Get<HarrborOptions>() ?? new HarrborOptions();

    var dbPath = Path.Combine(harrborOptions.DataPath, "harrbor.sqlite");
    Directory.CreateDirectory(harrborOptions.DataPath);

    // Factory registration also registers a scoped HarrborDbContext, so scoped
    // consumers keep injecting the context directly while singletons use the factory.
    builder.Services.AddDbContextFactory<HarrborDbContext>(options =>
        options.UseSqlite($"Data Source={dbPath}"));

    // Services
    builder.Services.AddSingleton<IConnectionStore, ConnectionStore>();
    builder.Services.AddSingleton<IQBittorrentClient, QBittorrentClientWrapper>();
    builder.Services.AddScoped<IRemoteStorageService, RcloneRemoteStorageService>();
    builder.Services.AddScoped<IMediaServiceResolver, MediaServiceResolver>();

    builder.Services.AddHttpClient<ISonarrClient, SonarrClient>()
        .AddStandardResilienceHandler();

    builder.Services.AddHttpClient<IRadarrClient, RadarrClient>()
        .AddStandardResilienceHandler();

    // Phase Handlers
    builder.Services.AddScoped<IDiscoveryPhaseHandler, DiscoveryPhaseHandler>();
    builder.Services.AddScoped<IDownloadPhaseHandler, DownloadPhaseHandler>();
    builder.Services.AddScoped<ITransferPhaseHandler, TransferPhaseHandler>();
    builder.Services.AddScoped<IImportPhaseHandler, ImportPhaseHandler>();
    builder.Services.AddScoped<ICleanupPhaseHandler, CleanupPhaseHandler>();
    builder.Services.AddScoped<IArchivalPhaseHandler, ArchivalPhaseHandler>();

    // Background Worker
    builder.Services.AddScoped<IJobCycleRunner, JobCycleRunner>();
    builder.Services.TryAddSingleton(TimeProvider.System);
    builder.Services.AddHostedService<OrchestrationWorker>();

    // API
    builder.Services.AddProblemDetails();
    builder.Services.ConfigureHttpJsonOptions(options =>
        options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)));

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

        // WAL lets the orchestration worker and the config API write concurrently
        db.Database.ExecuteSqlRaw("PRAGMA journal_mode=WAL;");

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
    }

    app.UseSerilogRequestLogging();

    // Serve the built UI from wwwroot (present in the Docker image; absent locally is fine)
    app.UseDefaultFiles();
    app.UseStaticFiles();

    // Configuration API
    app.MapApiEndpoints();

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

    // SPA fallback: unmatched extension-less routes serve the UI
    app.MapFallbackToFile("index.html");

    app.Run();
}
// HostAbortedException must propagate: WebApplicationFactory and dotnet-ef abort
// the entry point with it as part of their host-capture handshake
catch (Exception ex) when (ex is not Microsoft.Extensions.Hosting.HostAbortedException)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

// Exposes the entry point to WebApplicationFactory-based integration tests
public partial class Program { }
