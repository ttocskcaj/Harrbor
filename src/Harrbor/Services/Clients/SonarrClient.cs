using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Harrbor.Configuration;
using Harrbor.Services.Clients.Models;

namespace Harrbor.Services.Clients;

public class SonarrClient : ISonarrClient
{
    private readonly HttpClient _httpClient;
    private readonly SonarrOptions _options;
    private readonly ILogger<SonarrClient> _logger;

    public SonarrClient(
        HttpClient httpClient,
        IOptions<SonarrOptions> options,
        ILogger<SonarrClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;

        _httpClient.BaseAddress = new Uri(_options.BaseUrl);
        _httpClient.DefaultRequestHeaders.Add("X-Api-Key", _options.ApiKey);
    }

    public async Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/v3/system/status", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                await LogFailedResponseAsync("GET", "/api/v3/system/status", null, response, cancellationToken);
            }
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Sonarr health check failed");
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

        const string url = "/api/v3/command";
        var response = await _httpClient.PostAsJsonAsync(url, command, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            await LogFailedResponseAsync("POST", url, command, response, cancellationToken);
            response.EnsureSuccessStatusCode();
        }

        _logger.LogInformation("Triggered manual import for path: {Path}", path);
    }

    public async Task<IReadOnlyList<QueueItem>> GetQueueAsync(CancellationToken cancellationToken = default)
    {
        var items = new List<QueueItem>();
        var page = 1;
        const int pageSize = 100;

        while (true)
        {
            var url = $"/api/v3/queue?page={page}&pageSize={pageSize}&includeEpisode=false&includeUnknownSeriesItems=true";
            var response = await _httpClient.GetAsync(url, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                await LogFailedResponseAsync("GET", url, null, response, cancellationToken);
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

        _logger.LogDebug("Sonarr queue returned {Count} items", items.Count);
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
        var url = $"/api/v3/history?pageSize=50&downloadId={normalizedId}";
        var response = await _httpClient.GetAsync(url, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            await LogFailedResponseAsync("GET", url, null, response, cancellationToken);
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
                "Sonarr API request failed: {Method} {Url} returned {StatusCode}. Request body: {RequestBody}. Response body: {ResponseBody}",
                method, url, (int)response.StatusCode, requestBodyJson, responseBody);
        }
        else
        {
            _logger.LogError(
                "Sonarr API request failed: {Method} {Url} returned {StatusCode}. Response body: {ResponseBody}",
                method, url, (int)response.StatusCode, responseBody);
        }
    }
}
