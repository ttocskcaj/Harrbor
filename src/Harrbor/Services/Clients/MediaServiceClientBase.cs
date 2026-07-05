using System.Net;
using System.Text.Json;
using Harrbor.Abstractions.Clients;
using Harrbor.Abstractions.Clients.Models;
using Harrbor.Services.Clients.Models;

namespace Harrbor.Services.Clients;

/// <summary>
/// Connection settings snapshot used for a single request.
/// </summary>
public sealed record MediaServiceSettings(string BaseUrl, string ApiKey);

public abstract class MediaServiceClientBase : IMediaService
{
    private readonly HttpClient _httpClient;
    private readonly MediaServiceSettings _settings;
    private readonly ILogger _logger;
    private readonly string _serviceName;

    protected MediaServiceClientBase(
        HttpClient httpClient,
        MediaServiceSettings settings,
        ILogger logger,
        string serviceName)
    {
        _httpClient = httpClient;
        _settings = settings;
        _logger = logger;
        _serviceName = serviceName;
    }

    protected abstract string GetQueueQueryParameters();

    private static Uri BuildUri(string baseUrl, string relativePath)
        => new(new Uri(baseUrl.TrimEnd('/') + "/"), relativePath.TrimStart('/'));

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string relativePath,
        HttpContent? content,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, BuildUri(_settings.BaseUrl, relativePath));
        request.Headers.Add("X-Api-Key", _settings.ApiKey);
        if (content is not null)
            request.Content = content;

        return await _httpClient.SendAsync(request, cancellationToken);
    }

    public async Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default)
    {
        const string path = "api/v3/system/status";
        try
        {
            var response = await SendAsync(HttpMethod.Get, path, null, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                await LogFailedResponseAsync("GET", path, null, response, cancellationToken);
            }
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "{ServiceName} health check failed", _serviceName);
            return false;
        }
    }

    public async Task<ConnectionTestResult> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        var baseUrl = _settings.BaseUrl;
        const string path = "api/v3/system/status";
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, BuildUri(baseUrl, path));
            request.Headers.Add("X-Api-Key", _settings.ApiKey);
            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                return new ConnectionTestResult(false, Error: "Authentication failed — check the API key.");

            if (response.StatusCode == HttpStatusCode.NotFound)
                return new ConnectionTestResult(false, Error: $"Host reached but the {_serviceName} API was not found — check the base URL and port.");

            if (!response.IsSuccessStatusCode)
                return new ConnectionTestResult(false, Error: $"{_serviceName} returned HTTP {(int)response.StatusCode}.");

            var status = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
            var version = status.TryGetProperty("version", out var v) ? v.GetString() : null;
            return new ConnectionTestResult(true, Version: version);
        }
        catch (HttpRequestException ex)
        {
            var error = ex.InnerException?.Message ?? ex.Message;
            return new ConnectionTestResult(false, Error: $"Cannot reach {baseUrl}: {error}");
        }
        catch (UriFormatException)
        {
            return new ConnectionTestResult(false, Error: $"'{baseUrl}' is not a valid URL");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new ConnectionTestResult(false, Error: $"Error connecting to {baseUrl}: {ex.Message}");
        }
    }

    public async Task TriggerManualImportAsync(string path, CancellationToken cancellationToken = default)
    {
        var command = new
        {
            name = "ManualImport",
            path,
            importMode = "Auto"
        };

        const string urlPath = "api/v3/command";
        var response = await SendAsync(HttpMethod.Post, urlPath, JsonContent.Create(command), cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            await LogFailedResponseAsync("POST", urlPath, command, response, cancellationToken);
            response.EnsureSuccessStatusCode();
        }

        _logger.LogInformation("Triggered manual import for path: {Path}", path);
    }

    public async Task<IReadOnlyList<QueueItem>> GetQueueAsync(CancellationToken cancellationToken = default)
    {
        var items = new List<QueueItem>();
        var page = 1;
        const int pageSize = 100;
        const int maxPages = 100; // Safeguard against infinite loops

        while (page <= maxPages)
        {
            var urlPath = $"api/v3/queue?page={page}&pageSize={pageSize}&{GetQueueQueryParameters()}";
            var response = await SendAsync(HttpMethod.Get, urlPath, null, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                await LogFailedResponseAsync("GET", urlPath, null, response, cancellationToken);
                response.EnsureSuccessStatusCode();
            }

            var queueResponse = await response.Content.ReadFromJsonAsync<QueueResponse>(cancellationToken);

            if (queueResponse?.Records == null || queueResponse.Records.Count == 0)
                break;

            foreach (var record in queueResponse.Records)
            {
                if (string.IsNullOrEmpty(record.DownloadId))
                    continue;

                items.Add(new QueueItem(
                    DownloadId: record.DownloadId.ToUpperInvariant(),
                    Title: record.Title,
                    Status: record.Status,
                    TrackedDownloadState: record.TrackedDownloadState,
                    OutputPath: record.OutputPath));
            }

            if (items.Count >= queueResponse.TotalRecords)
                break;

            page++;
        }

        if (page > maxPages)
        {
            _logger.LogWarning(
                "{ServiceName} queue pagination reached max page limit ({MaxPages}). Results may be incomplete.",
                _serviceName, maxPages);
        }

        _logger.LogDebug("{ServiceName} queue returned {Count} items", _serviceName, items.Count);
        return items;
    }

    public async Task<bool> IsDownloadInQueueAsync(string downloadId, CancellationToken cancellationToken = default)
    {
        var normalizedId = downloadId.ToUpperInvariant();
        var queue = await GetQueueAsync(cancellationToken);
        return queue.Any(q => q.DownloadId.Equals(normalizedId, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<bool> HasImportedAsync(string downloadId, CancellationToken cancellationToken = default)
    {
        var normalizedId = downloadId.ToUpperInvariant();

        // Query history directly by downloadId
        var urlPath = $"api/v3/history?pageSize=50&downloadId={normalizedId}";
        var response = await SendAsync(HttpMethod.Get, urlPath, null, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            await LogFailedResponseAsync("GET", urlPath, null, response, cancellationToken);
            response.EnsureSuccessStatusCode();
        }

        var historyResponse = await response.Content.ReadFromJsonAsync<HistoryResponse>(cancellationToken);

        if (historyResponse?.Records == null)
            return false;

        // Look for downloadFolderImported event type
        var hasImported = historyResponse.Records.Any(r =>
            string.Equals(r.EventType, "downloadFolderImported", StringComparison.OrdinalIgnoreCase));

        if (hasImported)
        {
            _logger.LogDebug("Found import history for download {DownloadId}", downloadId);
        }

        return hasImported;
    }

    private async Task LogFailedResponseAsync(
        string method,
        string url,
        object? requestBody,
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (requestBody != null)
        {
            var requestBodyJson = JsonSerializer.Serialize(requestBody);
            _logger.LogError(
                "{ServiceName} API request failed: {Method} {Url} returned {StatusCode}. Request body: {RequestBody}. Response body: {ResponseBody}",
                _serviceName, method, url, (int)response.StatusCode, requestBodyJson, responseBody);
        }
        else
        {
            _logger.LogError(
                "{ServiceName} API request failed: {Method} {Url} returned {StatusCode}. Response body: {ResponseBody}",
                _serviceName, method, url, (int)response.StatusCode, responseBody);
        }
    }
}
