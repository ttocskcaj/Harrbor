using Microsoft.EntityFrameworkCore;
using Serilog;
using Harrbor.Configuration;
using Harrbor.Data;
using Harrbor.HealthChecks;
using Harrbor.Services.Clients;
using Harrbor.Services.Orchestration;
using Harrbor.Services.Transfer;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting Harrbor");

    var builder = WebApplication.CreateBuilder(args);

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

    // Database
    var harrborOptions = builder.Configuration
        .GetSection(HarrborOptions.SectionName)
        .Get<HarrborOptions>() ?? new HarrborOptions();

    var dbPath = Path.Combine(harrborOptions.DataPath, "harrbor.db");
    Directory.CreateDirectory(harrborOptions.DataPath);

    builder.Services.AddDbContext<HarrborDbContext>(options =>
        options.UseSqlite($"Data Source={dbPath}"));

    // Services
    builder.Services.AddSingleton<IQBittorrentClient, QBittorrentClientWrapper>();
    builder.Services.AddScoped<ITransferService, RcloneTransferService>();

    builder.Services.AddHttpClient<ISonarrClient, SonarrClient>()
        .AddStandardResilienceHandler();

    builder.Services.AddHttpClient<IRadarrClient, RadarrClient>()
        .AddStandardResilienceHandler();

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
        db.Database.EnsureCreated();
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
