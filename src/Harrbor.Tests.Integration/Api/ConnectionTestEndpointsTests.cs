using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FakeItEasy;
using FluentAssertions;
using Harrbor.Api.Connections;
using Harrbor.Configuration.Connections;
using Harrbor.Services.Clients;
using Harrbor.Services.RemoteStorage;
using Harrbor.Tests.Integration.Helpers;
using Xunit;

namespace Harrbor.Tests.Integration.Api;

public class ConnectionTestEndpointsTests : IDisposable
{
    private readonly HarrborWebApplicationFactory _factory = new();
    private readonly HttpClient _client;

    public ConnectionTestEndpointsTests()
    {
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task TestQBittorrent_MergesStoredSecretIntoPayload()
    {
        await _client.PutAsJsonAsync("/api/connections/qbittorrent", new QBittorrentConnectionRequest
        {
            BaseUrl = "http://qbt:8080",
            Username = "admin",
            Password = "stored-pw"
        });

        QBittorrentSettings? received = null;
        A.CallTo(() => _factory.QBittorrentClient.TestConnectionAsync(A<QBittorrentSettings>._, A<CancellationToken>._))
            .Invokes((QBittorrentSettings s, CancellationToken _) => received = s)
            .Returns(new ConnectionTestResult(true, Version: "2.9.3"));

        // Payload omits the password — the stored secret must be used for the test
        var response = await _client.PostAsJsonAsync("/api/connections/qbittorrent/test", new QBittorrentConnectionRequest
        {
            BaseUrl = "http://qbt:8080",
            Username = "admin"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ConnectionTestResponse>();
        body!.Success.Should().BeTrue();

        received.Should().NotBeNull();
        received!.Password.Should().Be("stored-pw");
    }

    [Fact]
    public async Task TestQBittorrent_ReachabilityFailure_IsSuccessFalseNot4xx()
    {
        A.CallTo(() => _factory.QBittorrentClient.TestConnectionAsync(A<QBittorrentSettings>._, A<CancellationToken>._))
            .Returns(new ConnectionTestResult(false, Error: "Cannot reach http://qbt:8080"));

        var response = await _client.PostAsJsonAsync("/api/connections/qbittorrent/test", new QBittorrentConnectionRequest
        {
            BaseUrl = "http://qbt:8080"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ConnectionTestResponse>();
        body!.Success.Should().BeFalse();
        body.Error.Should().Contain("Cannot reach");
    }

    [Fact]
    public async Task TestQBittorrent_InvalidPayload_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/connections/qbittorrent/test", new QBittorrentConnectionRequest
        {
            BaseUrl = "not-a-url"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("errors").TryGetProperty("baseUrl", out _).Should().BeTrue();
    }

    [Fact]
    public async Task TestSonarr_UsesStoredApiKey_WhenPayloadOmitsIt()
    {
        await _client.PutAsJsonAsync("/api/connections/sonarr", new ServarrConnectionRequest
        {
            BaseUrl = "http://sonarr:8989",
            ApiKey = "stored-key"
        });

        A.CallTo(() => _factory.SonarrClient.TestConnectionAsync("http://sonarr:8989", "stored-key", A<CancellationToken>._))
            .Returns(new ConnectionTestResult(true, Version: "4.0.0"));

        var response = await _client.PostAsJsonAsync("/api/connections/sonarr/test", new ServarrConnectionRequest
        {
            BaseUrl = "http://sonarr:8989",
            ApiKey = null
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ConnectionTestResponse>();
        body!.Success.Should().BeTrue();

        A.CallTo(() => _factory.SonarrClient.TestConnectionAsync("http://sonarr:8989", "stored-key", A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task TestSonarr_NoStoredOrPayloadKey_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/connections/sonarr/test", new ServarrConnectionRequest
        {
            BaseUrl = "http://sonarr:8989",
            ApiKey = null
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("errors").TryGetProperty("apiKey", out _).Should().BeTrue();
    }

    [Fact]
    public async Task TestRemoteStorage_ReturnsClientError()
    {
        A.CallTo(() => _factory.RemoteStorageService.TestConnectionAsync(A<RemoteStorageSettings>._, A<CancellationToken>._))
            .Returns(new RemoteStorageConnectionResult(false, "ssh: handshake failed"));

        var response = await _client.PostAsJsonAsync("/api/connections/remote-storage/test", new RemoteStorageConnectionRequest
        {
            Host = "seedbox.example.com",
            User = "jack",
            Password = "wrong"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ConnectionTestResponse>();
        body!.Success.Should().BeFalse();
        body.Error.Should().Contain("handshake failed");
    }
}
