using System.Net;
using System.Net.Http.Json;
using FakeItEasy;
using FluentAssertions;
using Harrbor.Api.Connections;
using Harrbor.Services.Clients;
using Harrbor.Services.Health;
using Harrbor.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Harrbor.Tests.Api;

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

    [Fact]
    public async Task GetHealth_Unconfigured_ReportsAllTypesUnknown()
    {
        var health = await _client.GetFromJsonAsync<List<ConnectionHealth>>("/api/connections/health", TestJson.Options);

        health!.Select(h => h.Type).Should().BeEquivalentTo("qbittorrent", "sonarr", "radarr", "remoteStorage");
        health.Should().OnlyContain(h => !h.Configured && h.Status == ConnectionHealthStatus.Unknown);
    }

    [Fact]
    public async Task GetHealth_AfterMonitorTick_ReflectsReachability()
    {
        A.CallTo(() => _factory.QBittorrentClient.IsConnectedAsync(A<CancellationToken>._)).Returns(true);

        await _client.PutAsJsonAsync("/api/connections/qbittorrent", new QBittorrentConnectionRequest
        {
            BaseUrl = "http://qbt:8080",
            Username = "admin",
            Password = "s3cret"
        });

        // Drive one deterministic probe round instead of waiting on the background loop.
        var monitor = _factory.Services.GetRequiredService<ConnectionHealthMonitor>();
        await monitor.RunChecksAsync(0, CancellationToken.None);

        var health = await _client.GetFromJsonAsync<List<ConnectionHealth>>("/api/connections/health", TestJson.Options);

        var qbit = health!.Single(h => h.Type == "qbittorrent");
        qbit.Configured.Should().BeTrue();
        qbit.Status.Should().Be(ConnectionHealthStatus.Up);
        qbit.CheckedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task GetHealth_UnreachableQBittorrent_ReportsDown()
    {
        A.CallTo(() => _factory.QBittorrentClient.IsConnectedAsync(A<CancellationToken>._)).Returns(false);

        await _client.PutAsJsonAsync("/api/connections/qbittorrent", new QBittorrentConnectionRequest
        {
            BaseUrl = "http://qbt:8080",
            Username = "admin",
            Password = "s3cret"
        });

        var monitor = _factory.Services.GetRequiredService<ConnectionHealthMonitor>();
        await monitor.RunChecksAsync(0, CancellationToken.None);

        var health = await _client.GetFromJsonAsync<List<ConnectionHealth>>("/api/connections/health", TestJson.Options);
        health!.Single(h => h.Type == "qbittorrent").Status.Should().Be(ConnectionHealthStatus.Down);
    }

    [Fact]
    public async Task PutQBittorrent_StaleExpectedVersion_Returns412()
    {
        var put = await _client.PutAsJsonAsync("/api/connections/qbittorrent", new QBittorrentConnectionRequest
        {
            BaseUrl = "http://qbt:8080",
            Username = "admin",
            Password = "s3cret"
        });
        var saved = await put.Content.ReadFromJsonAsync<QBittorrentConnectionResponse>(TestJson.Options);

        var stale = await _client.PutAsJsonAsync("/api/connections/qbittorrent", new QBittorrentConnectionRequest
        {
            BaseUrl = "http://qbt:9090",
            Username = "admin",
            ExpectedVersion = saved!.Version + 5
        });

        stale.StatusCode.Should().Be(HttpStatusCode.PreconditionFailed);
    }

    [Fact]
    public async Task PutQBittorrent_CurrentExpectedVersion_SavesAndBumpsVersion()
    {
        var put = await _client.PutAsJsonAsync("/api/connections/qbittorrent", new QBittorrentConnectionRequest
        {
            BaseUrl = "http://qbt:8080",
            Username = "admin",
            Password = "s3cret"
        });
        var saved = await put.Content.ReadFromJsonAsync<QBittorrentConnectionResponse>(TestJson.Options);

        var ok = await _client.PutAsJsonAsync("/api/connections/qbittorrent", new QBittorrentConnectionRequest
        {
            BaseUrl = "http://qbt:9090",
            Username = "admin",
            ExpectedVersion = saved!.Version
        });

        ok.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await ok.Content.ReadFromJsonAsync<QBittorrentConnectionResponse>(TestJson.Options);
        updated!.Version.Should().Be(saved.Version + 1);
        updated.BaseUrl.Should().Be("http://qbt:9090");
    }

    [Fact]
    public async Task GetQBittorrentCategories_ReturnsClientCategories()
    {
        A.CallTo(() => _factory.QBittorrentClient.GetCategoriesAsync(A<CancellationToken>._))
            .Returns(new[] { "tv-shows", "movies" });

        var categories = await _client.GetFromJsonAsync<List<string>>("/api/connections/qbittorrent/categories", TestJson.Options);

        categories.Should().BeEquivalentTo("tv-shows", "movies");
    }
}
