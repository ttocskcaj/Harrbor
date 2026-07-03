using FakeItEasy;
using Harrbor.Data;
using Harrbor.Services.Clients;
using Harrbor.Services.Orchestration;
using Harrbor.Services.RemoteStorage;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Harrbor.Tests.Helpers;

/// <summary>
/// Boots the real app (including startup migrations) against an isolated
/// in-memory SQLite database, with the background worker removed and all
/// external service clients replaced by FakeItEasy fakes.
/// </summary>
public sealed class HarrborWebApplicationFactory : WebApplicationFactory<Program>
{
    // HostFactoryResolver captures the entry point's HostBuilder via a global
    // DiagnosticListener subscription with no per-factory filtering, so two
    // factories booting concurrently cross-capture each other's builders.
    // Serialize the boot handshake; test execution itself stays parallel.
    private static readonly Lock BootLock = new();

    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    public IQBittorrentClient QBittorrentClient { get; } = A.Fake<IQBittorrentClient>();
    public ISonarrClient SonarrClient { get; } = A.Fake<ISonarrClient>();
    public IRadarrClient RadarrClient { get; } = A.Fake<IRadarrClient>();
    public IRemoteStorageService RemoteStorageService { get; } = A.Fake<IRemoteStorageService>();

    public HarrborWebApplicationFactory()
    {
        _connection.Open();

        // Startup validation treats a remote-storage failure as fatal
        A.CallTo(() => RemoteStorageService.TestConnectionAsync(A<CancellationToken>._))
            .Returns(new RemoteStorageConnectionResult(true));
        A.CallTo(() => QBittorrentClient.TestConnectionAsync(A<CancellationToken>._))
            .Returns(new ConnectionTestResult(true, Version: "test"));
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Isolated in-memory SQLite instead of the file under Harrbor:DataPath
            services.RemoveAll<DbContextOptions<HarrborDbContext>>();
            services.RemoveAll<DbContextOptions>();
            services.RemoveAll<IDbContextFactory<HarrborDbContext>>();
            services.RemoveAll<HarrborDbContext>();
            services.AddDbContextFactory<HarrborDbContext>(options => options.UseSqlite(_connection));

            // No background orchestration during API tests
            foreach (var descriptor in services
                         .Where(d => d.ImplementationType == typeof(OrchestrationWorker))
                         .ToList())
            {
                services.Remove(descriptor);
            }

            services.RemoveAll<IQBittorrentClient>();
            services.AddSingleton(QBittorrentClient);
            services.RemoveAll<ISonarrClient>();
            services.AddSingleton(SonarrClient);
            services.RemoveAll<IRadarrClient>();
            services.AddSingleton(RadarrClient);
            services.RemoveAll<IRemoteStorageService>();
            services.AddSingleton(RemoteStorageService);
        });
    }

    protected override Microsoft.Extensions.Hosting.IHost CreateHost(Microsoft.Extensions.Hosting.IHostBuilder builder)
    {
        lock (BootLock)
        {
            return base.CreateHost(builder);
        }
    }

    public HarrborDbContext CreateDbContext()
    {
        var factory = Services.GetRequiredService<IDbContextFactory<HarrborDbContext>>();
        return factory.CreateDbContext();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            _connection.Dispose();
    }
}
