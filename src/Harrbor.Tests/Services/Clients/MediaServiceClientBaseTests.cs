using System.Net;
using FakeItEasy;
using FluentAssertions;
using Harrbor.Services.Clients;
using Harrbor.Services.Clients.Models;
using Harrbor.Tests.Helpers;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Harrbor.Tests.Services.Clients;

public class MediaServiceClientBaseTests : IDisposable
{
    private readonly MockHttpMessageHandler _handler;
    private readonly TestMediaServiceClient _client;

    public MediaServiceClientBaseTests()
    {
        _handler = new MockHttpMessageHandler();
        var httpClient = _handler.CreateClient();
        var logger = A.Fake<ILogger<TestMediaServiceClient>>();
        _client = new TestMediaServiceClient(httpClient, "http://localhost", "test-api-key", logger);
    }

    public void Dispose()
    {
        _client.Dispose();
    }

    [Fact]
    public async Task GetQueueAsync_SinglePage_ReturnsAllItems()
    {
        // Arrange
        var queueResponse = new QueueResponseBuilder()
            .WithTotalRecords(2)
            .AddRecord(new QueueRecordBuilder().WithDownloadId("ABC123").WithTitle("Release 1").Build())
            .AddRecord(new QueueRecordBuilder().WithDownloadId("DEF456").WithTitle("Release 2").Build())
            .Build();

        _handler.QueueJsonResponse(queueResponse);

        // Act
        var result = await _client.GetQueueAsync();

        // Assert
        result.Should().HaveCount(2);
        result[0].DownloadId.Should().Be("ABC123");
        result[1].DownloadId.Should().Be("DEF456");
    }

    [Fact]
    public async Task GetQueueAsync_MultiplePages_ReturnsAllItems()
    {
        // Arrange
        var page1 = new QueueResponseBuilder()
            .WithPage(1)
            .WithPageSize(100)
            .WithTotalRecords(150)
            .AddRecord(new QueueRecordBuilder().WithDownloadId("ABC123").Build())
            .Build();
        page1.Records.AddRange(Enumerable.Range(0, 99).Select(i =>
            new QueueRecordBuilder().WithDownloadId($"ID{i:D5}").Build()));

        var page2 = new QueueResponseBuilder()
            .WithPage(2)
            .WithPageSize(100)
            .WithTotalRecords(150)
            .Build();
        page2.Records.AddRange(Enumerable.Range(100, 50).Select(i =>
            new QueueRecordBuilder().WithDownloadId($"ID{i:D5}").Build()));

        _handler.QueueJsonResponse(page1);
        _handler.QueueJsonResponse(page2);

        // Act
        var result = await _client.GetQueueAsync();

        // Assert
        result.Should().HaveCount(150);
        _handler.VerifyRequestCount(2);
    }

    [Fact]
    public async Task GetQueueAsync_EmptyQueue_ReturnsEmptyList()
    {
        // Arrange
        var queueResponse = new QueueResponseBuilder()
            .WithTotalRecords(0)
            .Build();

        _handler.QueueJsonResponse(queueResponse);

        // Act
        var result = await _client.GetQueueAsync();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetQueueAsync_NormalizesDownloadIdToUpperCase()
    {
        // Arrange
        var queueResponse = new QueueResponseBuilder()
            .AddRecord(new QueueRecordBuilder().WithDownloadId("abc123def").Build())
            .AddRecord(new QueueRecordBuilder().WithDownloadId("XYZ789").Build())
            .Build();

        _handler.QueueJsonResponse(queueResponse);

        // Act
        var result = await _client.GetQueueAsync();

        // Assert
        result.Should().HaveCount(2);
        result[0].DownloadId.Should().Be("ABC123DEF");
        result[1].DownloadId.Should().Be("XYZ789");
    }

    [Fact]
    public async Task GetQueueAsync_FiltersNullDownloadIds()
    {
        // Arrange - Set TotalRecords to 2 (valid items count) to match pagination logic
        // The code checks if items.Count >= TotalRecords to stop paginating
        var queueResponse = new QueueResponseBuilder()
            .WithTotalRecords(2) // Only count valid items to prevent pagination attempt
            .AddRecord(new QueueRecordBuilder().WithDownloadId("ABC123").Build())
            .AddRecord(new QueueRecordBuilder().WithDownloadId(null).Build())
            .AddRecord(new QueueRecordBuilder().WithDownloadId("").Build())
            .AddRecord(new QueueRecordBuilder().WithDownloadId("DEF456").Build())
            .Build();

        _handler.QueueJsonResponse(queueResponse);

        // Act
        var result = await _client.GetQueueAsync();

        // Assert
        result.Should().HaveCount(2);
        result.Select(r => r.DownloadId).Should().BeEquivalentTo(["ABC123", "DEF456"]);
    }

    [Fact]
    public async Task IsDownloadInQueueAsync_Exists_ReturnsTrue()
    {
        // Arrange
        var queueResponse = new QueueResponseBuilder()
            .AddRecord(new QueueRecordBuilder().WithDownloadId("ABC123").Build())
            .AddRecord(new QueueRecordBuilder().WithDownloadId("DEF456").Build())
            .Build();

        _handler.QueueJsonResponse(queueResponse);

        // Act
        var result = await _client.IsDownloadInQueueAsync("ABC123");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsDownloadInQueueAsync_NotExists_ReturnsFalse()
    {
        // Arrange
        var queueResponse = new QueueResponseBuilder()
            .AddRecord(new QueueRecordBuilder().WithDownloadId("ABC123").Build())
            .Build();

        _handler.QueueJsonResponse(queueResponse);

        // Act
        var result = await _client.IsDownloadInQueueAsync("NOTFOUND");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsDownloadInQueueAsync_CaseInsensitive_ReturnsTrue()
    {
        // Arrange
        var queueResponse = new QueueResponseBuilder()
            .AddRecord(new QueueRecordBuilder().WithDownloadId("abc123def").Build())
            .Build();

        _handler.QueueJsonResponse(queueResponse);

        // Act
        var result = await _client.IsDownloadInQueueAsync("ABC123DEF");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task HasImportedAsync_DownloadFolderImportedEvent_ReturnsTrue()
    {
        // Arrange
        var historyResponse = new HistoryResponseBuilder()
            .AddRecord(new HistoryRecordBuilder()
                .WithDownloadId("ABC123")
                .WithEventType("grabbed")
                .Build())
            .AddRecord(new HistoryRecordBuilder()
                .WithDownloadId("ABC123")
                .WithEventType("downloadFolderImported")
                .Build())
            .Build();

        _handler.QueueJsonResponse(historyResponse);

        // Act
        var result = await _client.HasImportedAsync("ABC123");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task HasImportedAsync_NoImportEvent_ReturnsFalse()
    {
        // Arrange
        var historyResponse = new HistoryResponseBuilder()
            .AddRecord(new HistoryRecordBuilder()
                .WithDownloadId("ABC123")
                .WithEventType("grabbed")
                .Build())
            .Build();

        _handler.QueueJsonResponse(historyResponse);

        // Act
        var result = await _client.HasImportedAsync("ABC123");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task HasImportedAsync_OtherEventTypes_ReturnsFalse()
    {
        // Arrange
        var historyResponse = new HistoryResponseBuilder()
            .AddRecord(new HistoryRecordBuilder()
                .WithDownloadId("ABC123")
                .WithEventType("grabbed")
                .Build())
            .AddRecord(new HistoryRecordBuilder()
                .WithDownloadId("ABC123")
                .WithEventType("downloadCompleted")
                .Build())
            .AddRecord(new HistoryRecordBuilder()
                .WithDownloadId("ABC123")
                .WithEventType("episodeFileDeleted")
                .Build())
            .Build();

        _handler.QueueJsonResponse(historyResponse);

        // Act
        var result = await _client.HasImportedAsync("ABC123");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task HasImportedAsync_CaseInsensitiveEventType_ReturnsTrue()
    {
        // Arrange
        var historyResponse = new HistoryResponseBuilder()
            .AddRecord(new HistoryRecordBuilder()
                .WithDownloadId("ABC123")
                .WithEventType("DOWNLOADFOLDERIMPORTED")
                .Build())
            .Build();

        _handler.QueueJsonResponse(historyResponse);

        // Act
        var result = await _client.HasImportedAsync("ABC123");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task HasImportedAsync_EmptyHistory_ReturnsFalse()
    {
        // Arrange
        var historyResponse = new HistoryResponseBuilder().Build();
        _handler.QueueJsonResponse(historyResponse);

        // Act
        var result = await _client.HasImportedAsync("ABC123");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsHealthyAsync_SuccessfulResponse_ReturnsTrue()
    {
        // Arrange
        _handler.QueueResponse(HttpStatusCode.OK, new { version = "4.0.0" });

        // Act
        var result = await _client.IsHealthyAsync();

        // Assert
        result.Should().BeTrue();
        _handler.VerifyRequest(0, HttpMethod.Get, "api/v3/system/status");
    }

    [Fact]
    public async Task IsHealthyAsync_FailedResponse_ReturnsFalse()
    {
        // Arrange
        _handler.QueueErrorResponse(HttpStatusCode.Unauthorized, "Unauthorized");

        // Act
        var result = await _client.IsHealthyAsync();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsHealthyAsync_ServerError_ReturnsFalse()
    {
        // Arrange
        _handler.QueueErrorResponse(HttpStatusCode.InternalServerError, "Server Error");

        // Act
        var result = await _client.IsHealthyAsync();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task GetQueueAsync_SetsApiKeyHeader()
    {
        // Arrange
        var queueResponse = new QueueResponseBuilder().Build();
        _handler.QueueJsonResponse(queueResponse);

        // Act
        await _client.GetQueueAsync();

        // Assert
        var request = _handler.Requests.First();
        request.Headers.Should().Contain(h => h.Key == "X-Api-Key" && h.Value.Contains("test-api-key"));
    }
}

/// <summary>
/// Concrete implementation for testing the abstract MediaServiceClientBase
/// </summary>
public class TestMediaServiceClient : MediaServiceClientBase, IDisposable
{
    private readonly HttpClient _httpClient;

    public TestMediaServiceClient(
        HttpClient httpClient,
        string baseUrl,
        string apiKey,
        ILogger logger)
        : base(httpClient, baseUrl, apiKey, logger, "TestService")
    {
        _httpClient = httpClient;
    }

    protected override string GetQueueQueryParameters()
    {
        return "includeUnknownSeriesItems=true";
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
