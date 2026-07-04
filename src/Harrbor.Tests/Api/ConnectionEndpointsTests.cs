using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Harrbor.Api.Connections;
using Harrbor.Tests.Helpers;
using Xunit;

namespace Harrbor.Tests.Api;

public class ConnectionEndpointsTests : IDisposable
{
    private readonly HarrborWebApplicationFactory _factory = new();
    private readonly HttpClient _client;

    public ConnectionEndpointsTests()
    {
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task GetOverview_ListsAllFourTypesUnconfigured()
    {
        var response = await _client.GetAsync("/api/connections");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var overview = await response.Content.ReadFromJsonAsync<List<ConnectionSummaryResponse>>(TestJson.Options);
        overview.Should().HaveCount(4);
        overview!.Select(c => c.Type).Should().BeEquivalentTo(
            "qbittorrent", "sonarr", "radarr", "remoteStorage");
        overview.Should().OnlyContain(c => !c.Configured);
    }

    [Fact]
    public async Task GetQBittorrent_Returns404_WhenUnconfigured()
    {
        var response = await _client.GetAsync("/api/connections/qbittorrent");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PutQBittorrent_ThenGet_HidesSecretButReportsItSet()
    {
        var put = await _client.PutAsJsonAsync("/api/connections/qbittorrent", new QBittorrentConnectionRequest
        {
            BaseUrl = "http://qbt:8080",
            Username = "admin",
            Password = "s3cret"
        });
        put.StatusCode.Should().Be(HttpStatusCode.OK);

        var get = await _client.GetAsync("/api/connections/qbittorrent");
        get.StatusCode.Should().Be(HttpStatusCode.OK);

        var raw = await get.Content.ReadAsStringAsync();
        raw.Should().NotContain("s3cret");

        var body = JsonSerializer.Deserialize<JsonElement>(raw);
        body.GetProperty("baseUrl").GetString().Should().Be("http://qbt:8080");
        body.GetProperty("passwordSet").GetBoolean().Should().BeTrue();
        body.TryGetProperty("password", out _).Should().BeFalse();
    }

    [Fact]
    public async Task PutQBittorrent_WithoutPassword_KeepsStoredSecret()
    {
        await _client.PutAsJsonAsync("/api/connections/qbittorrent", new QBittorrentConnectionRequest
        {
            BaseUrl = "http://qbt:8080",
            Username = "admin",
            Password = "s3cret"
        });

        // Password omitted (null) — must keep the stored one
        await _client.PutAsJsonAsync("/api/connections/qbittorrent", new QBittorrentConnectionRequest
        {
            BaseUrl = "http://qbt:9090",
            Username = "admin2",
            Password = null
        });

        var result = await _client.GetFromJsonAsync<QBittorrentConnectionResponse>("/api/connections/qbittorrent");
        result!.BaseUrl.Should().Be("http://qbt:9090");
        result.Username.Should().Be("admin2");
        result.PasswordSet.Should().BeTrue();
    }

    [Fact]
    public async Task PutQBittorrent_WithEmptyPassword_ClearsSecret()
    {
        await _client.PutAsJsonAsync("/api/connections/qbittorrent", new QBittorrentConnectionRequest
        {
            BaseUrl = "http://qbt:8080",
            Password = "s3cret"
        });

        await _client.PutAsJsonAsync("/api/connections/qbittorrent", new QBittorrentConnectionRequest
        {
            BaseUrl = "http://qbt:8080",
            Password = ""
        });

        var result = await _client.GetFromJsonAsync<QBittorrentConnectionResponse>("/api/connections/qbittorrent");
        result!.PasswordSet.Should().BeFalse();
    }

    [Fact]
    public async Task PutQBittorrent_InvalidBaseUrl_ReturnsValidationProblemWithCamelCaseKey()
    {
        var response = await _client.PutAsJsonAsync("/api/connections/qbittorrent", new QBittorrentConnectionRequest
        {
            BaseUrl = "not-a-url"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("errors").TryGetProperty("baseUrl", out _).Should().BeTrue();
    }

    [Fact]
    public async Task PutSonarr_RequiresApiKeyOnFirstSave()
    {
        var response = await _client.PutAsJsonAsync("/api/connections/sonarr", new ServarrConnectionRequest
        {
            BaseUrl = "http://sonarr:8989",
            ApiKey = null
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("errors").TryGetProperty("apiKey", out _).Should().BeTrue();
    }

    [Fact]
    public async Task PutSonarr_ThenGet_RoundTripsWithApiKeySet()
    {
        var put = await _client.PutAsJsonAsync("/api/connections/sonarr", new ServarrConnectionRequest
        {
            BaseUrl = "http://sonarr:8989",
            ApiKey = "abc123",
            Enabled = true
        });
        put.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await _client.GetFromJsonAsync<ServarrConnectionResponse>("/api/connections/sonarr");
        result!.BaseUrl.Should().Be("http://sonarr:8989");
        result.ApiKeySet.Should().BeTrue();
        result.Enabled.Should().BeTrue();
    }

    [Fact]
    public async Task PutRemoteStorage_OutOfRangePort_ReturnsFieldError()
    {
        var response = await _client.PutAsJsonAsync("/api/connections/remote-storage", new RemoteStorageConnectionRequest
        {
            Host = "seedbox.example.com",
            User = "jack",
            Port = 99999
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("errors").TryGetProperty("port", out _).Should().BeTrue();
    }

    [Fact]
    public async Task PutRemoteStorage_ThenGet_RoundTripsNonSecretFields()
    {
        var put = await _client.PutAsJsonAsync("/api/connections/remote-storage", new RemoteStorageConnectionRequest
        {
            Host = "seedbox.example.com",
            Port = 2222,
            User = "jack",
            Password = "pw",
            KeyFile = "/keys/id_ed25519",
            UseAgent = false,
            Transfers = 8,
            Checkers = 16,
            AdditionalFlags = ["--fast-list"]
        });
        put.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await _client.GetFromJsonAsync<RemoteStorageConnectionResponse>("/api/connections/remote-storage");
        result!.Host.Should().Be("seedbox.example.com");
        result.Port.Should().Be(2222);
        result.KeyFile.Should().Be("/keys/id_ed25519");
        result.PasswordSet.Should().BeTrue();
        result.KeyFilePassphraseSet.Should().BeFalse();
        result.AdditionalFlags.Should().Equal("--fast-list");
    }

    [Fact]
    public async Task Overview_AfterSave_ShowsConfigured()
    {
        await _client.PutAsJsonAsync("/api/connections/qbittorrent", new QBittorrentConnectionRequest
        {
            BaseUrl = "http://qbt:8080"
        });

        var overview = await _client.GetFromJsonAsync<List<ConnectionSummaryResponse>>("/api/connections", TestJson.Options);

        overview!.Single(c => c.Type == "qbittorrent").Configured.Should().BeTrue();
        overview.Single(c => c.Type == "sonarr").Configured.Should().BeFalse();
    }
}
