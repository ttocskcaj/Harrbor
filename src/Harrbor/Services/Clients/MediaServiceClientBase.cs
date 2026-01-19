using System.Net.Http.Json;
using System.Text.Json;
using Harrbor.Services.Clients.Models;

namespace Harrbor.Services.Clients;

public abstract class MediaServiceClientBase : IMediaService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger _logger;
    private readonly string _serviceName;
    private readonly Uri _baseUri;

    protected MediaServiceClientBase(
        HttpClient httpClient,
        string baseUrl,
        string apiKey,
        ILogger logger,
        string serviceName)
    {
        _httpClient = httpClient;
        _logger = logger;
        _serviceName = serviceName;
        _baseUri = new Uri(baseUrl.TrimEnd('/') + "/");

        _httpClient.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
    }

    private Uri BuildUri(string relativePath)
    {
        var path = relativePath.TrimStart('/');
        return new Uri(_baseUri, path);
    }

    protected abstract string GetQueueQueryParameters();

    public async Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default)
    {
        const string path = "api/v3/system/status";
        try
        {
            var response = await _httpClient.GetAsync(BuildUri(path), cancellationToken);
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

    public async Task TriggerManualImportAsync(string path, CancellationToken cancellationToken = default)
    {
        var command = new
        {
            name = "ManualImport",
            path,
            importMode = "Auto"
        };

        const string urlPath = "api/v3/command";
        var response = await _httpClient.PostAsJsonAsync(BuildUri(urlPath), command, cancellationToken);

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
            var response = await _httpClient.GetAsync(BuildUri(urlPath), cancellationToken);

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
        var response = await _httpClient.GetAsync(BuildUri(urlPath), cancellationToken);

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
