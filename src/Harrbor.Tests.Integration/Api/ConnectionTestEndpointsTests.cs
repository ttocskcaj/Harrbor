using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using FakeItEasy;
using FluentAssertions;
using Harrbor.Abstractions.Clients;
using Harrbor.Api.Connections;
using Harrbor.Tests.Common;
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
    public async Task TestUnsaved_ValidQBittorrentPayload_ReturnsSuccess()
    {
        A.CallTo(() => _factory.QBittorrentClient.TestConnectionAsync(A<CancellationToken>._))
            .Returns(new ConnectionTestResult(true, Version: "2.9.3"));

        var response = await _client.PostAsJsonAsync("/api/connections/test", new ConnectionTestRequest
        {
            ProviderKey = "qbittorrent",
            Settings = new JsonObject { ["baseUrl"] = "http://qbt:8080", ["username"] = "admin", ["password"] = "pw" },
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ConnectionTestResponse>(TestJson.Options);
        body!.Success.Should().BeTrue();
    }

    [Fact]
    public async Task TestUnsaved_ReachabilityFailure_IsSuccessFalseNot4xx()
    {
        A.CallTo(() => _factory.QBittorrentClient.TestConnectionAsync(A<CancellationToken>._))
            .Returns(new ConnectionTestResult(false, Error: "Cannot reach http://qbt:8080"));

        var response = await _client.PostAsJsonAsync("/api/connections/test", new ConnectionTestRequest
        {
            ProviderKey = "qbittorrent",
            Settings = new JsonObject { ["baseUrl"] = "http://qbt:8080" },
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ConnectionTestResponse>(TestJson.Options);
        body!.Success.Should().BeFalse();
        body.Error.Should().Contain("Cannot reach");
    }

    [Fact]
    public async Task TestUnsaved_InvalidPayload_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/connections/test", new ConnectionTestRequest
        {
            ProviderKey = "qbittorrent",
            Settings = new JsonObject { ["baseUrl"] = "not-a-url" },
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("errors").TryGetProperty("baseUrl", out _).Should().BeTrue();
    }

    [Fact]
    public async Task TestUnsaved_Sonarr_MissingApiKey_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/connections/test", new ConnectionTestRequest
        {
            ProviderKey = "sonarr",
            Settings = new JsonObject { ["baseUrl"] = "http://sonarr:8989" },
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("errors").TryGetProperty("apiKey", out _).Should().BeTrue();
    }

    [Fact]
    public async Task TestUnsaved_UnknownProvider_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/connections/test", new ConnectionTestRequest
        {
            ProviderKey = "lidarr",
            Settings = new JsonObject(),
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task TestExisting_UsesStoredSecret_WhenPayloadOmitsIt()
    {
        A.CallTo(() => _factory.SonarrClient.TestConnectionAsync(A<CancellationToken>._))
            .Returns(new ConnectionTestResult(true, Version: "4.0.0"));

        // Save a Sonarr connection with an API key.
        var created = await (await _client.PostAsJsonAsync("/api/connections", new ConnectionCreateRequest
        {
            ProviderKey = "sonarr",
            Name = "Sonarr",
            Settings = new JsonObject { ["baseUrl"] = "http://sonarr:8989", ["apiKey"] = "stored-key" },
        })).Content.ReadFromJsonAsync<ConnectionResponse>(TestJson.Options);

        // Test payload omits the apiKey — validation only passes if the stored secret is merged in.
        var response = await _client.PostAsJsonAsync($"/api/connections/{created!.Id}/test",
            new JsonObject { ["baseUrl"] = "http://sonarr:8989" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ConnectionTestResponse>(TestJson.Options);
        body!.Success.Should().BeTrue();
    }
}
