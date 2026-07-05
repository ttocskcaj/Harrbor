using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Harrbor.Api.Connections;
using Harrbor.Api.Jobs;
using Harrbor.Tests.Common;
using Harrbor.Tests.Integration.Helpers;
using Xunit;

namespace Harrbor.Tests.Integration.Api;

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

    private static ConnectionCreateRequest QbitRequest(string name, string? password = "s3cret") => new()
    {
        ProviderKey = "qbittorrent",
        Name = name,
        Settings = new JsonObject { ["baseUrl"] = "http://qbt:8080", ["username"] = "admin", ["password"] = password },
    };

    private async Task<ConnectionResponse> CreateAsync(ConnectionCreateRequest request)
    {
        var response = await _client.PostAsJsonAsync("/api/connections", request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<ConnectionResponse>(TestJson.Options))!;
    }

    [Fact]
    public async Task GetProviders_ReturnsBuiltInProvidersWithSchema()
    {
        var providers = await _client.GetFromJsonAsync<List<ProviderResponse>>("/api/connections/providers", TestJson.Options);

        providers!.Select(p => p.Key).Should().BeEquivalentTo("qbittorrent", "sonarr", "radarr", "rclone-sftp");
        var sonarr = providers.Single(p => p.Key == "sonarr");
        sonarr.Fields.Should().Contain(f => f.Name == "apiKey" && f.Secret);
        sonarr.Fields.Should().Contain(f => f.Name == "baseUrl" && f.Required);
    }

    [Fact]
    public async Task List_IsEmpty_OnFreshInstall()
    {
        var list = await _client.GetFromJsonAsync<List<ConnectionResponse>>("/api/connections", TestJson.Options);

        list.Should().BeEmpty();
    }

    [Fact]
    public async Task Create_AllowsMultipleConnectionsPerProvider()
    {
        var first = await CreateAsync(QbitRequest("Main"));
        var second = await CreateAsync(QbitRequest("Backup"));

        first.Id.Should().NotBe(second.Id);
        var list = await _client.GetFromJsonAsync<List<ConnectionResponse>>("/api/connections", TestJson.Options);
        list!.Should().HaveCount(2);
    }

    [Fact]
    public async Task Create_ThenGet_HidesSecretButReportsSet()
    {
        var created = await CreateAsync(QbitRequest("Main"));

        var raw = await (await _client.GetAsync($"/api/connections/{created.Id}")).Content.ReadAsStringAsync();
        raw.Should().NotContain("s3cret");

        var body = JsonSerializer.Deserialize<JsonElement>(raw);
        var settings = body.GetProperty("settings");
        settings.GetProperty("baseUrl").GetString().Should().Be("http://qbt:8080");
        settings.GetProperty("passwordSet").GetBoolean().Should().BeTrue();
        settings.TryGetProperty("password", out _).Should().BeFalse();
    }

    [Fact]
    public async Task Update_WithoutSecret_KeepsStored()
    {
        var created = await CreateAsync(QbitRequest("Main"));

        var update = new ConnectionUpdateRequest
        {
            Settings = new JsonObject { ["baseUrl"] = "http://qbt:9090", ["username"] = "admin2" }, // password omitted
        };
        var response = await _client.PutAsJsonAsync($"/api/connections/{created.Id}", update);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<ConnectionResponse>(TestJson.Options);
        body!.Settings["baseUrl"]!.GetValue<string>().Should().Be("http://qbt:9090");
        body.Settings["passwordSet"]!.GetValue<bool>().Should().BeTrue();
    }

    [Fact]
    public async Task Update_WithEmptySecret_Clears()
    {
        var created = await CreateAsync(QbitRequest("Main"));

        var update = new ConnectionUpdateRequest
        {
            Settings = new JsonObject { ["baseUrl"] = "http://qbt:8080", ["password"] = "" },
        };
        var body = await (await _client.PutAsJsonAsync($"/api/connections/{created.Id}", update))
            .Content.ReadFromJsonAsync<ConnectionResponse>(TestJson.Options);

        body!.Settings["passwordSet"]!.GetValue<bool>().Should().BeFalse();
    }

    [Fact]
    public async Task Create_InvalidBaseUrl_ReturnsValidationProblemWithCamelCaseKey()
    {
        var response = await _client.PostAsJsonAsync("/api/connections", new ConnectionCreateRequest
        {
            ProviderKey = "qbittorrent",
            Name = "Bad",
            Settings = new JsonObject { ["baseUrl"] = "not-a-url" },
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("errors").TryGetProperty("baseUrl", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Create_Sonarr_RequiresApiKeyOnFirstSave()
    {
        var response = await _client.PostAsJsonAsync("/api/connections", new ConnectionCreateRequest
        {
            ProviderKey = "sonarr",
            Name = "Sonarr",
            Settings = new JsonObject { ["baseUrl"] = "http://sonarr:8989" },
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("errors").TryGetProperty("apiKey", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Create_UnknownProvider_ReturnsValidationProblem()
    {
        var response = await _client.PostAsJsonAsync("/api/connections", new ConnectionCreateRequest
        {
            ProviderKey = "lidarr",
            Name = "Nope",
            Settings = new JsonObject(),
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("errors").TryGetProperty("providerKey", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Create_RemoteStorage_OutOfRangePort_ReturnsFieldError()
    {
        var response = await _client.PostAsJsonAsync("/api/connections", new ConnectionCreateRequest
        {
            ProviderKey = "rclone-sftp",
            Name = "Seedbox",
            Settings = new JsonObject { ["host"] = "seedbox.example.com", ["user"] = "jack", ["port"] = 99999 },
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("errors").TryGetProperty("port", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Create_DuplicateNamePerProvider_ReturnsValidationProblem()
    {
        await CreateAsync(QbitRequest("Main"));

        var response = await _client.PostAsJsonAsync("/api/connections", QbitRequest("Main"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("errors").TryGetProperty("name", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Delete_UnusedConnection_ReturnsNoContent()
    {
        var created = await CreateAsync(QbitRequest("Main"));

        var response = await _client.DeleteAsync($"/api/connections/{created.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await _client.GetAsync($"/api/connections/{created.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_ConnectionUsedByJob_ReturnsConflictWithDependents()
    {
        var seeded = await _factory.SeedDefaultConnectionsAsync();
        var jobResponse = await _client.PostAsJsonAsync("/api/jobs", new JobRequest
        {
            Name = "tv",
            MediaServiceConnectionId = seeded.Sonarr,
            DownloadClientConnectionId = seeded.QBittorrent,
            RemoteStorageConnectionId = seeded.RemoteStorage,
            RemotePath = "/downloads",
            StagingPath = Path.Combine(Path.GetTempPath(), "harrbor-test-staging"),
        });
        jobResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var response = await _client.DeleteAsync($"/api/connections/{seeded.Sonarr}");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var conflict = await response.Content.ReadFromJsonAsync<ConnectionDeleteConflictResponse>(TestJson.Options);
        conflict!.DependentJobNames.Should().Contain("tv");
    }
}
