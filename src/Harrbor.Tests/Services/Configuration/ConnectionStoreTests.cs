using FluentAssertions;
using Harrbor.Configuration.Connections;
using Harrbor.Data.Entities;
using Harrbor.Services.Configuration;
using Harrbor.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Harrbor.Tests.Services.Configuration;

public class ConnectionStoreTests : IDisposable
{
    private readonly SqliteDbContextFactory _dbFactory = new();
    private readonly ConnectionStore _store;

    public ConnectionStoreTests()
    {
        _store = new ConnectionStore(_dbFactory);
    }

    public void Dispose() => _dbFactory.Dispose();

    [Fact]
    public async Task GetAsync_ReturnsNull_WhenNotConfigured()
    {
        var result = await _store.GetAsync<QBittorrentSettings>();

        result.Should().BeNull();
    }

    [Fact]
    public async Task SaveAsync_ThenGetAsync_RoundTripsQBittorrentSettings()
    {
        var settings = new QBittorrentSettings
        {
            BaseUrl = "http://qbt:8080",
            Username = "admin",
            Password = "s3cret"
        };

        await _store.SaveAsync(settings);
        var result = await _store.GetAsync<QBittorrentSettings>();

        result.Should().NotBeNull();
        result!.Settings.BaseUrl.Should().Be("http://qbt:8080");
        result.Settings.Username.Should().Be("admin");
        result.Settings.Password.Should().Be("s3cret");
    }

    [Fact]
    public async Task SaveAsync_ThenGetAsync_RoundTripsRemoteStorageSettings()
    {
        var settings = new RemoteStorageSettings
        {
            Host = "seedbox.example.com",
            Port = 2222,
            User = "jack",
            Password = null,
            KeyFile = "/keys/id_ed25519",
            KeyFilePassphrase = "phrase",
            UseAgent = true,
            Transfers = 8,
            Checkers = 16,
            AdditionalFlags = ["--fast-list", "--sftp-disable-hashcheck"]
        };

        await _store.SaveAsync(settings);
        var result = await _store.GetAsync<RemoteStorageSettings>();

        result.Should().NotBeNull();
        result!.Settings.Should().BeEquivalentTo(settings);
    }

    [Fact]
    public async Task SaveAsync_IncrementsVersion_OnEachSave()
    {
        var first = await _store.SaveAsync(new SonarrSettings { BaseUrl = "http://sonarr:8989" });
        var second = await _store.SaveAsync(new SonarrSettings { BaseUrl = "http://sonarr:9999" });

        second.Version.Should().BeGreaterThan(first.Version);

        var result = await _store.GetAsync<SonarrSettings>();
        result!.Version.Should().Be(second.Version);
    }

    [Fact]
    public async Task SaveAsync_UpsertsSingleRowPerType()
    {
        await _store.SaveAsync(new SonarrSettings { BaseUrl = "http://old" });
        await _store.SaveAsync(new SonarrSettings { BaseUrl = "http://new" });

        await using var context = _dbFactory.CreateDbContext();
        var rows = await context.ServiceConnections
            .Where(c => c.Type == ConnectionTypes.Sonarr)
            .ToListAsync();

        rows.Should().HaveCount(1);

        var result = await _store.GetAsync<SonarrSettings>();
        result!.Settings.BaseUrl.Should().Be("http://new");
    }

    [Fact]
    public async Task SaveAsync_KeepsConnectionTypesIndependent()
    {
        await _store.SaveAsync(new SonarrSettings { BaseUrl = "http://sonarr:8989", ApiKey = "sonarr-key" });
        await _store.SaveAsync(new RadarrSettings { BaseUrl = "http://radarr:7878", ApiKey = "radarr-key" });

        var sonarr = await _store.GetAsync<SonarrSettings>();
        var radarr = await _store.GetAsync<RadarrSettings>();

        sonarr!.Settings.ApiKey.Should().Be("sonarr-key");
        radarr!.Settings.ApiKey.Should().Be("radarr-key");
    }

    [Fact]
    public async Task GetAsync_IgnoresUnknownJsonFields()
    {
        await using (var context = _dbFactory.CreateDbContext())
        {
            context.ServiceConnections.Add(new ServiceConnection
            {
                Type = ConnectionTypes.QBittorrent,
                SettingsJson = """{"BaseUrl":"http://qbt:8080","Username":"admin","Password":"pw","RemovedField":42}""",
                Version = 1,
                UpdatedAtUtc = DateTime.UtcNow
            });
            await context.SaveChangesAsync();
        }

        var result = await _store.GetAsync<QBittorrentSettings>();

        result.Should().NotBeNull();
        result!.Settings.BaseUrl.Should().Be("http://qbt:8080");
    }

    [Fact]
    public async Task IsConfiguredAsync_ReflectsSavedState()
    {
        (await _store.IsConfiguredAsync(ConnectionTypes.QBittorrent)).Should().BeFalse();

        await _store.SaveAsync(new QBittorrentSettings { BaseUrl = "http://qbt:8080" });

        (await _store.IsConfiguredAsync(ConnectionTypes.QBittorrent)).Should().BeTrue();
        (await _store.IsConfiguredAsync(ConnectionTypes.Sonarr)).Should().BeFalse();
    }

    [Fact]
    public async Task Schema_EnforcesUniqueConnectionType()
    {
        await using var context = _dbFactory.CreateDbContext();
        context.ServiceConnections.Add(new ServiceConnection
        {
            Type = ConnectionTypes.Sonarr,
            SettingsJson = "{}",
            Version = 1,
            UpdatedAtUtc = DateTime.UtcNow
        });
        context.ServiceConnections.Add(new ServiceConnection
        {
            Type = ConnectionTypes.Sonarr,
            SettingsJson = "{}",
            Version = 1,
            UpdatedAtUtc = DateTime.UtcNow
        });

        var act = async () => await context.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }
}
