using Microsoft.EntityFrameworkCore;
using Serilog;
using Harrbor.Configuration;
using Harrbor.Data;
using Harrbor.HealthChecks;
using Harrbor.Services;
using Harrbor.Services.Clients;
using Harrbor.Services.Orchestration;
using Harrbor.Services.RemoteStorage;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting Harrbor");

    var builder = WebApplication.CreateBuilder(args);

    builder.Configuration
        .AddJsonFile("appsettings.json")
        .AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: true);

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
    builder.Services.AddScoped<IMediaServiceResolver, MediaServiceResolver>();

    builder.Services.AddHttpClient<ISonarrClient, SonarrClient>()
        .AddStandardResilienceHandler();

    builder.Services.AddHttpClient<IRadarrClient, RadarrClient>()
        .AddStandardResilienceHandler();

    // Startup validation
    builder.Services.AddTransient<StartupValidator>();

    // Background Worker
    builder.Services.AddHostedService<OrchestrationWorker>();

    // Health Checks
    builder.Services.AddHealthChecks()
        .AddDbContextCheck<HarrborDbContext>("database")
        .AddCheck<QBittorrentHealthCheck>("qbittorrent")
        .AddCheck<SonarrHealthCheck>("sonarr")
        .AddCheck<RadarrHealthCheck>("radarr");

    var app = builder.Build();

    // Apply migrations on startup
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<HarrborDbContext>();
        db.Database.Migrate();
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
        Predicate = check => check.Tags.Contains("ready") || check.Name == "database"
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
}
finally
{
    Log.CloseAndFlush();
}
