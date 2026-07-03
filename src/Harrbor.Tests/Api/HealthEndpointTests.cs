using System.Net;
using FluentAssertions;
using Harrbor.Tests.Helpers;
using Xunit;

namespace Harrbor.Tests.Api;

public class HealthEndpointTests : IDisposable
{
    private readonly HarrborWebApplicationFactory _factory = new();
    private readonly HttpClient _client;

    public HealthEndpointTests()
    {
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task Health_WithNoConnectionsConfigured_Returns200Degraded()
    {
        var response = await _client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK, "a fresh unconfigured install must not look dead to Docker");
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Be("Degraded");
    }

    [Fact]
    public async Task HealthLive_AlwaysHealthy()
    {
        var response = await _client.GetAsync("/health/live");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Be("Healthy");
    }
}
