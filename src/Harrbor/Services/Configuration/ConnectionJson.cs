using System.Text.Json;
using System.Text.Json.Serialization;

namespace Harrbor.Services.Configuration;

/// <summary>
/// The single canonical JSON policy for connection settings — used both for the persisted
/// <c>SettingsJson</c> blob and for mapping generic wire payloads to/from typed settings. camelCase
/// everywhere (matching the HTTP wire), enums as strings, unknown members ignored.
/// </summary>
public static class ConnectionJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };
}
