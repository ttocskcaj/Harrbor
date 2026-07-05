using System.Text.Json;
using FakeItEasy;
using Harrbor.Abstractions.Clients;
using Harrbor.Abstractions.Connections;
using Harrbor.Abstractions.Providers;
using Harrbor.Abstractions.RemoteStorage;
using Harrbor.Abstractions.Validation;
using Harrbor.Configuration.Connections;
using Harrbor.Configuration.Connections.Providers;
using Harrbor.Data;
using Harrbor.Data.Entities;
using Harrbor.Services;
using Harrbor.Services.Configuration;
using Harrbor.Services.Orchestration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Harrbor.Tests.Integration.Helpers;

/// <summary>
/// Boots the real app (including startup migrations) against an isolated in-memory SQLite database,
/// with the background worker removed and every external client replaced by a FakeItEasy fake. The
/// provider registry is swapped for one whose <c>CreateClient</c> returns the fakes, so both the
/// connection manager and the provider-driven test endpoints exercise the fakes rather than real I/O.
/// </summary>
public sealed class HarrborWebApplicationFactory : WebApplicationFactory<Program>
{
    // HostFactoryResolver captures the entry point's HostBuilder via a global DiagnosticListener
    // subscription with no per-factory filtering, so two factories booting concurrently cross-capture
    // each other's builders. Serialize the boot handshake; test execution itself stays parallel.
    private static readonly Lock BootLock = new();

    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    public ITorrentClient QBittorrentClient { get; } = A.Fake<ITorrentClient>();
    public IMediaService SonarrClient { get; } = A.Fake<IMediaService>();
    public IMediaService RadarrClient { get; } = A.Fake<IMediaService>();
    public IRemoteStorageService RemoteStorageService { get; } = A.Fake<IRemoteStorageService>();

    public HarrborWebApplicationFactory()
    {
        _connection.Open();

        A.CallTo(() => RemoteStorageService.TestConnectionAsync(A<CancellationToken>._))
            .Returns(new RemoteStorageConnectionResult(true));
        A.CallTo(() => QBittorrentClient.TestConnectionAsync(A<CancellationToken>._))
            .Returns(new ConnectionTestResult(true, Version: "test"));
        A.CallTo(() => SonarrClient.TestConnectionAsync(A<CancellationToken>._))
            .Returns(new ConnectionTestResult(true, Version: "test"));
        A.CallTo(() => RadarrClient.TestConnectionAsync(A<CancellationToken>._))
            .Returns(new ConnectionTestResult(true, Version: "test"));
        A.CallTo(() => SonarrClient.IsHealthyAsync(A<CancellationToken>._)).Returns(true);
        A.CallTo(() => RadarrClient.IsHealthyAsync(A<CancellationToken>._)).Returns(true);
        A.CallTo(() => QBittorrentClient.IsConnectedAsync(A<CancellationToken>._)).Returns(true);
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

            // Swap the provider registry for stubs whose CreateClient returns the fakes, so the
            // connection manager and the /test endpoints both use the fakes rather than real clients.
            services.RemoveAll<IConnectionProviderRegistry>();
            services.AddSingleton<IConnectionProviderRegistry>(new ConnectionProviderRegistry(new IConnectionProvider[]
            {
                new StubConnectionProvider(new QBittorrentProvider(), QBittorrentClient),
                new StubConnectionProvider(new SonarrProvider(), SonarrClient),
                new StubConnectionProvider(new RadarrProvider(), RadarrClient),
                new StubConnectionProvider(new RcloneSftpProvider(), RemoteStorageService),
            }));
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

    /// <summary>Ids of the default seeded connections, one per built-in provider.</summary>
    public sealed record SeededConnections(int Sonarr, int Radarr, int QBittorrent, int RemoteStorage);

    /// <summary>Seeds one enabled connection per built-in provider and returns their ids.</summary>
    public async Task<SeededConnections> SeedDefaultConnectionsAsync()
    {
        await using var db = CreateDbContext();

        var sonarr = SeedConnection(db, "sonarr", "Sonarr", new SonarrSettings { BaseUrl = "http://sonarr:8989", ApiKey = "key" });
        var radarr = SeedConnection(db, "radarr", "Radarr", new RadarrSettings { BaseUrl = "http://radarr:7878", ApiKey = "key" });
        var qbit = SeedConnection(db, "qbittorrent", "qBittorrent", new QBittorrentSettings { BaseUrl = "http://qbt:8080", Username = "admin", Password = "pw" });
        var remote = SeedConnection(db, "rclone-sftp", "Seedbox", new RemoteStorageSettings { Host = "seedbox", User = "u" });

        await db.SaveChangesAsync();
        return new SeededConnections(sonarr.Id, radarr.Id, qbit.Id, remote.Id);
    }

    private static Connection SeedConnection(HarrborDbContext db, string providerKey, string name, object settings)
    {
        var connection = new Connection
        {
            ProviderKey = providerKey,
            Name = name,
            Enabled = true,
            SettingsJson = JsonSerializer.Serialize(settings, settings.GetType(), ConnectionJson.Options),
            Version = 1,
            UpdatedAtUtc = DateTime.UtcNow,
        };
        db.Connections.Add(connection);
        return connection;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            _connection.Dispose();
    }
}

/// <summary>
/// A provider that borrows a real provider's identity/schema/validation but returns a pre-built
/// (fake) client, so tests never touch real network/process I/O. Probe/test delegate to the real
/// provider, which invokes the fake client's own methods.
/// </summary>
internal sealed class StubConnectionProvider(IConnectionProvider inner, object client) : IConnectionProvider
{
    public string Key => inner.Key;
    public ConnectionCategory Category => inner.Category;
    public string DisplayName => inner.DisplayName;
    public Type SettingsType => inner.SettingsType;
    public IReadOnlyList<FieldDescriptor> Schema => inner.Schema;
    public int ProbeEveryNTicks => inner.ProbeEveryNTicks;

    public object CreateClient(object settings, ClientBuildContext context) => client;
    public void Validate(object settings, ValidationErrors errors) => inner.Validate(settings, errors);
    public Task<ProbeResult> ProbeAsync(object c, CancellationToken cancellationToken) => inner.ProbeAsync(c, cancellationToken);
    public Task<ConnectionTestResult> TestAsync(object c, CancellationToken cancellationToken) => inner.TestAsync(c, cancellationToken);
}
