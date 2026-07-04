using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Harrbor.Api.Jobs;
using Harrbor.Configuration;
using Harrbor.Data.Entities;
using Harrbor.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Harrbor.Tests.Api;

public class JobEndpointsTests : IDisposable
{
    private readonly HarrborWebApplicationFactory _factory = new();
    private readonly HttpClient _client;
    private readonly string _stagingRoot;

    public JobEndpointsTests()
    {
        _client = _factory.CreateClient();
        _stagingRoot = Path.Combine(Path.GetTempPath(), "harrbor-tests", Guid.NewGuid().ToString("N"));
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
        if (Directory.Exists(_stagingRoot))
            Directory.Delete(_stagingRoot, recursive: true);
    }

    private JobRequest ValidRequest(string name = "tv-shows") => new()
    {
        Name = name,
        Enabled = true,
        ServiceType = ServiceType.Sonarr,
        PollingIntervalSeconds = 60,
        RemotePath = "/remote/downloads/tv",
        StagingPath = Path.Combine(_stagingRoot, name),
        QBittorrentCategory = "tv",
        TransferParallelism = 2,
        MaxTransferRetries = 3,
        TransferRetryDelaySeconds = 300,
        ImportTimeoutSeconds = 86400
    };

    [Fact]
    public async Task GetJobs_ReturnsEmptyList()
    {
        var jobs = await _client.GetFromJsonAsync<List<JobResponse>>("/api/jobs", TestJson.Options);

        jobs.Should().BeEmpty();
    }

    [Fact]
    public async Task PostJob_Returns201WithLocationAndBody()
    {
        var response = await _client.PostAsJsonAsync("/api/jobs", ValidRequest(), TestJson.Options);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location!.ToString().Should().MatchRegex(@"^/api/jobs/\d+$");

        var body = await response.Content.ReadFromJsonAsync<JobResponse>(TestJson.Options);
        body!.Id.Should().BePositive();
        body.Name.Should().Be("tv-shows");
        body.PollingIntervalSeconds.Should().Be(60);
        body.ActiveReleaseCount.Should().Be(0);
        body.NextRunAtUtc.Should().NotBeNull("an enabled, never-run job is due immediately");
    }

    [Fact]
    public async Task PostJob_DuplicateNameCaseInsensitive_ReturnsNameError()
    {
        await _client.PostAsJsonAsync("/api/jobs", ValidRequest("tv-shows"), TestJson.Options);

        var response = await _client.PostAsJsonAsync("/api/jobs", ValidRequest("TV-Shows"), TestJson.Options);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("errors").TryGetProperty("name", out _).Should().BeTrue();
    }

    [Fact]
    public async Task PostJob_OutOfBoundsInterval_ReturnsFieldError()
    {
        var request = ValidRequest();
        request.PollingIntervalSeconds = 5;

        var response = await _client.PostAsJsonAsync("/api/jobs", request, TestJson.Options);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("errors").TryGetProperty("pollingIntervalSeconds", out _).Should().BeTrue();
    }

    [Fact]
    public async Task PostJob_RelativeRemotePath_ReturnsFieldError()
    {
        var request = ValidRequest();
        request.RemotePath = "downloads/tv";

        var response = await _client.PostAsJsonAsync("/api/jobs", request, TestJson.Options);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("errors").TryGetProperty("remotePath", out _).Should().BeTrue();
    }

    [Fact]
    public async Task PostJob_UnwritableStagingPath_ReturnsFieldError()
    {
        var request = ValidRequest();
        request.StagingPath = "/proc/harrbor-cannot-write-here";

        var response = await _client.PostAsJsonAsync("/api/jobs", request, TestJson.Options);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("errors").TryGetProperty("stagingPath", out _).Should().BeTrue();
    }

    [Fact]
    public async Task GetJob_UnknownId_Returns404()
    {
        var response = await _client.GetAsync("/api/jobs/999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PutJob_UpdatesFields()
    {
        var created = await (await _client.PostAsJsonAsync("/api/jobs", ValidRequest(), TestJson.Options))
            .Content.ReadFromJsonAsync<JobResponse>(TestJson.Options);

        var update = ValidRequest();
        update.Enabled = false;
        update.PollingIntervalSeconds = 120;

        var response = await _client.PutAsJsonAsync($"/api/jobs/{created!.Id}", update, TestJson.Options);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JobResponse>(TestJson.Options);
        body!.Enabled.Should().BeFalse();
        body.PollingIntervalSeconds.Should().Be(120);
        body.NextRunAtUtc.Should().BeNull("disabled jobs have no next run");
    }

    [Fact]
    public async Task PutJob_Rename_CascadesTrackedReleaseJobNames()
    {
        var created = await (await _client.PostAsJsonAsync("/api/jobs", ValidRequest("old-name"), TestJson.Options))
            .Content.ReadFromJsonAsync<JobResponse>(TestJson.Options);

        await using (var db = _factory.CreateDbContext())
        {
            db.TrackedReleases.Add(new TrackedRelease
            {
                DownloadId = "abc123",
                Name = "Some.Release",
                JobName = "old-name",
                RemotePath = "/remote/some.release",
                StagingPath = "/staging/some.release"
            });
            await db.SaveChangesAsync();
        }

        var update = ValidRequest("new-name");
        var response = await _client.PutAsJsonAsync($"/api/jobs/{created!.Id}", update, TestJson.Options);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await using (var db = _factory.CreateDbContext())
        {
            var release = await db.TrackedReleases.SingleAsync();
            release.JobName.Should().Be("new-name");
        }
    }

    [Fact]
    public async Task DeleteJob_WithActiveReleases_Returns409WithCount()
    {
        var created = await (await _client.PostAsJsonAsync("/api/jobs", ValidRequest(), TestJson.Options))
            .Content.ReadFromJsonAsync<JobResponse>(TestJson.Options);

        await using (var db = _factory.CreateDbContext())
        {
            db.TrackedReleases.Add(new TrackedRelease
            {
                DownloadId = "abc123",
                Name = "In.Flight.Release",
                JobName = "tv-shows",
                RemotePath = "/r",
                StagingPath = "/s",
                ArchivalStatus = ArchivalStatus.Pending
            });
            await db.SaveChangesAsync();
        }

        var response = await _client.DeleteAsync($"/api/jobs/{created!.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var body = await response.Content.ReadFromJsonAsync<JobDeleteConflictResponse>();
        body!.ActiveReleaseCount.Should().Be(1);
    }

    [Fact]
    public async Task DeleteJob_Force_DeletesJobAndKeepsReleases()
    {
        var created = await (await _client.PostAsJsonAsync("/api/jobs", ValidRequest(), TestJson.Options))
            .Content.ReadFromJsonAsync<JobResponse>(TestJson.Options);

        await using (var db = _factory.CreateDbContext())
        {
            db.TrackedReleases.Add(new TrackedRelease
            {
                DownloadId = "abc123",
                Name = "In.Flight.Release",
                JobName = "tv-shows",
                RemotePath = "/r",
                StagingPath = "/s",
                ArchivalStatus = ArchivalStatus.Pending
            });
            await db.SaveChangesAsync();
        }

        var response = await _client.DeleteAsync($"/api/jobs/{created!.Id}?force=true");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await using (var db = _factory.CreateDbContext())
        {
            (await db.Jobs.CountAsync()).Should().Be(0);
            (await db.TrackedReleases.CountAsync()).Should().Be(1, "releases stay as orphaned history");
        }
    }

    [Fact]
    public async Task DeleteJob_OnlyArchivedReleases_Returns204()
    {
        var created = await (await _client.PostAsJsonAsync("/api/jobs", ValidRequest(), TestJson.Options))
            .Content.ReadFromJsonAsync<JobResponse>(TestJson.Options);

        await using (var db = _factory.CreateDbContext())
        {
            db.TrackedReleases.Add(new TrackedRelease
            {
                DownloadId = "abc123",
                Name = "Done.Release",
                JobName = "tv-shows",
                RemotePath = "/r",
                StagingPath = "/s",
                ArchivalStatus = ArchivalStatus.Completed,
                Outcome = PipelineOutcome.Completed
            });
            await db.SaveChangesAsync();
        }

        var response = await _client.DeleteAsync($"/api/jobs/{created!.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task GetJobs_ReportsActiveReleaseCounts()
    {
        await _client.PostAsJsonAsync("/api/jobs", ValidRequest("tv-shows"), TestJson.Options);

        await using (var db = _factory.CreateDbContext())
        {
            db.TrackedReleases.AddRange(
                new TrackedRelease
                {
                    DownloadId = "a1", Name = "R1", JobName = "tv-shows",
                    RemotePath = "/r", StagingPath = "/s", ArchivalStatus = ArchivalStatus.Pending
                },
                new TrackedRelease
                {
                    DownloadId = "a2", Name = "R2", JobName = "tv-shows",
                    RemotePath = "/r", StagingPath = "/s",
                    ArchivalStatus = ArchivalStatus.Completed, Outcome = PipelineOutcome.Completed
                });
            await db.SaveChangesAsync();
        }

        var jobs = await _client.GetFromJsonAsync<List<JobResponse>>("/api/jobs", TestJson.Options);

        jobs!.Single().ActiveReleaseCount.Should().Be(1, "archived releases are not active");
    }

    [Fact]
    public async Task ServiceType_SerializesAsCamelCaseString()
    {
        await _client.PostAsJsonAsync("/api/jobs", ValidRequest(), TestJson.Options);

        var raw = await _client.GetStringAsync("/api/jobs");

        raw.Should().Contain("\"serviceType\":\"sonarr\"");
    }

    [Fact]
    public async Task CloneJob_NoName_CreatesDisabledCopyOfSettings()
    {
        var created = await (await _client.PostAsJsonAsync("/api/jobs", ValidRequest("tv-shows"), TestJson.Options))
            .Content.ReadFromJsonAsync<JobResponse>(TestJson.Options);

        var response = await _client.PostAsJsonAsync($"/api/jobs/{created!.Id}/clone", new CloneJobRequest());

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var clone = await response.Content.ReadFromJsonAsync<JobResponse>(TestJson.Options);
        clone!.Name.Should().Be("tv-shows-copy");
        clone.Enabled.Should().BeFalse();
        clone.Id.Should().NotBe(created.Id);
        clone.QBittorrentCategory.Should().Be(created.QBittorrentCategory);
        clone.StagingPath.Should().Be(created.StagingPath);
        clone.LastRunAtUtc.Should().BeNull();
    }

    [Fact]
    public async Task CloneJob_WithName_UsesProvidedName()
    {
        var created = await (await _client.PostAsJsonAsync("/api/jobs", ValidRequest("tv-shows"), TestJson.Options))
            .Content.ReadFromJsonAsync<JobResponse>(TestJson.Options);

        var response = await _client.PostAsJsonAsync($"/api/jobs/{created!.Id}/clone", new CloneJobRequest { Name = "movies" });

        var clone = await response.Content.ReadFromJsonAsync<JobResponse>(TestJson.Options);
        clone!.Name.Should().Be("movies");
    }

    [Fact]
    public async Task CloneJob_DuplicateName_Returns400()
    {
        var created = await (await _client.PostAsJsonAsync("/api/jobs", ValidRequest("tv-shows"), TestJson.Options))
            .Content.ReadFromJsonAsync<JobResponse>(TestJson.Options);

        var response = await _client.PostAsJsonAsync($"/api/jobs/{created!.Id}/clone", new CloneJobRequest { Name = "tv-shows" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CloneJob_Twice_GeneratesUniqueNames()
    {
        var created = await (await _client.PostAsJsonAsync("/api/jobs", ValidRequest("tv-shows"), TestJson.Options))
            .Content.ReadFromJsonAsync<JobResponse>(TestJson.Options);

        var first = await (await _client.PostAsJsonAsync($"/api/jobs/{created!.Id}/clone", new CloneJobRequest()))
            .Content.ReadFromJsonAsync<JobResponse>(TestJson.Options);
        var second = await (await _client.PostAsJsonAsync($"/api/jobs/{created.Id}/clone", new CloneJobRequest()))
            .Content.ReadFromJsonAsync<JobResponse>(TestJson.Options);

        first!.Name.Should().Be("tv-shows-copy");
        second!.Name.Should().Be("tv-shows-copy-2");
    }

    [Fact]
    public async Task CloneJob_MissingSource_Returns404()
    {
        var response = await _client.PostAsJsonAsync("/api/jobs/9999/clone", new CloneJobRequest());
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
