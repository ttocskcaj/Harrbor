using System.Text.Json.Serialization;

namespace Harrbor.Services.Clients.Models;

public class HistoryResponse
{
    [JsonPropertyName("page")]
    public int Page { get; set; }

    [JsonPropertyName("pageSize")]
    public int PageSize { get; set; }

    [JsonPropertyName("totalRecords")]
    public int TotalRecords { get; set; }

    [JsonPropertyName("records")]
    public List<HistoryRecord> Records { get; set; } = [];
}

public class HistoryRecord
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("downloadId")]
    public string DownloadId { get; set; } = string.Empty;

    [JsonPropertyName("eventType")]
    public string EventType { get; set; } = string.Empty;

    [JsonPropertyName("date")]
    public DateTime Date { get; set; }
}
