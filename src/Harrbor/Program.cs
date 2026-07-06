using System.Text.Json;
using System.Text.Json.Serialization;
using Harrbor.Api;
using Harrbor.Configuration;
using Harrbor.Data;
using Harrbor.Data.Entities;
using Harrbor.Services;
using Harrbor.Services.Configuration;
using Harrbor.Services.Extraction;
using Harrbor.Services.Health;
using Harrbor.Services.Orchestration;
using Harrbor.Services.Orchestration.Phases;
using Harrbor.Services.RemoteStorage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Serilog;

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
    builder.Services.AddSingleton<ITransferProgressTracker, TransferProgressTracker>();

    // Archive extraction is not a per-connection client, so it is registered directly
    // rather than through the connection provider registry.
    builder.Services.AddScoped<IArchiveExtractionService, ArchiveExtractionService>();

    // Connection providers (the code-defined "kinds") are registered in a provider registry. The
    // connection manager builds/caches live clients per connection instance (by id) at runtime,
    // replacing keyed DI and the per-category resolvers.
    builder.Services.AddConnectionProviders();
    builder.Services.AddSingleton<IConnectionManager, ConnectionManager>();

    // Phase Handlers
    builder.Services.AddScoped<IDiscoveryPhaseHandler, DiscoveryPhaseHandler>();
    builder.Services.AddScoped<IDownloadPhaseHandler, DownloadPhaseHandler>();
    builder.Services.AddScoped<ITransferPhaseHandler, TransferPhaseHandler>();
    builder.Services.AddScoped<IExtractionPhaseHandler, ExtractionPhaseHandler>();
    builder.Services.AddScoped<IImportPhaseHandler, ImportPhaseHandler>();
    builder.Services.AddScoped<ICleanupPhaseHandler, CleanupPhaseHandler>();
    builder.Services.AddScoped<IArchivalPhaseHandler, ArchivalPhaseHandler>();

    // Background Worker
    builder.Services.AddScoped<IJobCycleRunner, JobCycleRunner>();
    builder.Services.TryAddSingleton(TimeProvider.System);
    builder.Services.AddHostedService<OrchestrationWorker>();

    // Connection health monitor (one instance shared between the hosted service and the API)
    builder.Services.AddSingleton<ConnectionHealthMonitor>();
    builder.Services.AddSingleton<IConnectionHealthMonitor>(sp => sp.GetRequiredService<ConnectionHealthMonitor>());
    builder.Services.AddHostedService(sp => sp.GetRequiredService<ConnectionHealthMonitor>());

    // API
    builder.Services.AddProblemDetails();
    builder.Services.ConfigureHttpJsonOptions(options =>
        options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)));

    // OpenAPI document at /openapi/v1.json for the React client to codegen a typed client
    builder.Services.AddOpenApi();

    // Health Checks
    builder.Services.AddHealthChecks()
        .AddDbContextCheck<HarrborDbContext>("database", tags: ["ready"]);

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

            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Harrbor.Program>>();
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
    }

    app.UseSerilogRequestLogging();

    // Serve the built UI from wwwroot (present in the Docker image; absent locally is fine)
    app.UseDefaultFiles();
    app.UseStaticFiles();

    // Configuration API
    app.MapApiEndpoints();

    // OpenAPI document (served in all environments; single-user trusted-network tool)
    app.MapOpenApi();

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
namespace Harrbor
{
    public partial class Program { }
}
