using System.Text.Json.Serialization;

namespace Harrbor.Services.Clients.Models;

/// <summary>
/// Simplified queue item for internal use.
/// </summary>
public record QueueItem(
    string DownloadId,
    string Title,
    string Status,
    string? TrackedDownloadState,
    string? OutputPath);

/// <summary>
/// Response from /api/v3/queue endpoint (Sonarr/Radarr).
/// </summary>
public class QueueResponse
{
    [JsonPropertyName("page")]
    public int Page { get; set; }

    [JsonPropertyName("pageSize")]
    public int PageSize { get; set; }

    [JsonPropertyName("totalRecords")]
    public int TotalRecords { get; set; }

    [JsonPropertyName("records")]
    public List<QueueRecord> Records { get; set; } = [];
}

/// <summary>
/// A single queue record from Sonarr/Radarr.
/// </summary>
public class QueueRecord
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("downloadId")]
    public string? DownloadId { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("trackedDownloadState")]
    public string? TrackedDownloadState { get; set; }

    [JsonPropertyName("trackedDownloadStatus")]
    public string? TrackedDownloadStatus { get; set; }

    [JsonPropertyName("downloadClient")]
    public string? DownloadClient { get; set; }

    [JsonPropertyName("outputPath")]
    public string? OutputPath { get; set; }

    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; set; }
}
