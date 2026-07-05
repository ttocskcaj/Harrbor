using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using FakeItEasy;
using FluentAssertions;
using Harrbor.Api.Connections;
using Harrbor.Services.Health;
using Harrbor.Tests.Common;
using Harrbor.Tests.Integration.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Harrbor.Tests.Integration.Api;

public class ConnectionHealthEndpointsTests : IDisposable
{
    private readonly HarrborWebApplicationFactory _factory = new();
    private readonly HttpClient _client;

    public ConnectionHealthEndpointsTests()
    {
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    private async Task<ConnectionResponse> CreateQBittorrentAsync()
        => (await (await _client.PostAsJsonAsync("/api/connections", new ConnectionCreateRequest
        {
            ProviderKey = "qbittorrent",
            Name = "qBittorrent",
            Settings = new JsonObject { ["baseUrl"] = "http://qbt:8080", ["username"] = "admin", ["password"] = "s3cret" },
        })).Content.ReadFromJsonAsync<ConnectionResponse>(TestJson.Options))!;

    [Fact]
    public async Task GetHealth_NoConnections_ReturnsEmpty()
    {
        var health = await _client.GetFromJsonAsync<List<ConnectionHealth>>("/api/connections/health", TestJson.Options);

        health.Should().BeEmpty();
    }

    [Fact]
    public async Task GetHealth_AfterMonitorTick_ReflectsReachability()
    {
        A.CallTo(() => _factory.QBittorrentClient.IsConnectedAsync(A<CancellationToken>._)).Returns(true);
        var created = await CreateQBittorrentAsync();

        var monitor = _factory.Services.GetRequiredService<ConnectionHealthMonitor>();
        await monitor.RunChecksAsync(0, CancellationToken.None);

        var health = await _client.GetFromJsonAsync<List<ConnectionHealth>>("/api/connections/health", TestJson.Options);
        var qbit = health!.Single(h => h.Id == created.Id);
        qbit.Status.Should().Be(ConnectionHealthStatus.Up);
        qbit.CheckedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task GetHealth_Unreachable_ReportsDown()
    {
        A.CallTo(() => _factory.QBittorrentClient.IsConnectedAsync(A<CancellationToken>._)).Returns(false);
        var created = await CreateQBittorrentAsync();

        var monitor = _factory.Services.GetRequiredService<ConnectionHealthMonitor>();
        await monitor.RunChecksAsync(0, CancellationToken.None);

        var health = await _client.GetFromJsonAsync<List<ConnectionHealth>>("/api/connections/health", TestJson.Options);
        health!.Single(h => h.Id == created.Id).Status.Should().Be(ConnectionHealthStatus.Down);
    }

    [Fact]
    public async Task GetHealth_DisabledConnection_ReportsDisabled()
    {
        var created = await CreateQBittorrentAsync();
        await _client.PutAsJsonAsync($"/api/connections/{created.Id}", new ConnectionUpdateRequest
        {
            Enabled = false,
            Settings = new JsonObject { ["baseUrl"] = "http://qbt:8080", ["username"] = "admin" },
        });

        var monitor = _factory.Services.GetRequiredService<ConnectionHealthMonitor>();
        await monitor.RunChecksAsync(0, CancellationToken.None);

        var health = await _client.GetFromJsonAsync<List<ConnectionHealth>>("/api/connections/health", TestJson.Options);
        health!.Single(h => h.Id == created.Id).Status.Should().Be(ConnectionHealthStatus.Disabled);
    }

    [Fact]
    public async Task Update_StaleExpectedVersion_Returns412()
    {
        var created = await CreateQBittorrentAsync();

        var stale = await _client.PutAsJsonAsync($"/api/connections/{created.Id}", new ConnectionUpdateRequest
        {
            Settings = new JsonObject { ["baseUrl"] = "http://qbt:9090", ["username"] = "admin" },
            ExpectedVersion = created.Version + 5,
        });

        stale.StatusCode.Should().Be(HttpStatusCode.PreconditionFailed);
    }

    [Fact]
    public async Task Update_CurrentExpectedVersion_SavesAndBumpsVersion()
    {
        var created = await CreateQBittorrentAsync();

        var ok = await _client.PutAsJsonAsync($"/api/connections/{created.Id}", new ConnectionUpdateRequest
        {
            Settings = new JsonObject { ["baseUrl"] = "http://qbt:9090", ["username"] = "admin" },
            ExpectedVersion = created.Version,
        });

        ok.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await ok.Content.ReadFromJsonAsync<ConnectionResponse>(TestJson.Options);
        updated!.Version.Should().Be(created.Version + 1);
        updated.Settings["baseUrl"]!.GetValue<string>().Should().Be("http://qbt:9090");
    }

    [Fact]
    public async Task GetCategories_ReturnsDownloadClientCategories()
    {
        A.CallTo(() => _factory.QBittorrentClient.GetCategoriesAsync(A<CancellationToken>._))
            .Returns(new[] { "tv-shows", "movies" });
        var created = await CreateQBittorrentAsync();

        var categories = await _client.GetFromJsonAsync<List<string>>($"/api/connections/{created.Id}/categories", TestJson.Options);

        categories.Should().BeEquivalentTo("tv-shows", "movies");
    }

    [Fact]
    public async Task GetCategories_OnNonDownloadClient_Returns400()
    {
        var created = await (await _client.PostAsJsonAsync("/api/connections", new ConnectionCreateRequest
        {
            ProviderKey = "sonarr",
            Name = "Sonarr",
            Settings = new JsonObject { ["baseUrl"] = "http://sonarr:8989", ["apiKey"] = "k" },
        })).Content.ReadFromJsonAsync<ConnectionResponse>(TestJson.Options);

        var response = await _client.GetAsync($"/api/connections/{created!.Id}/categories");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
