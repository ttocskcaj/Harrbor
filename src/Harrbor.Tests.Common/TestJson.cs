using System.Text.Json;
using System.Text.Json.Serialization;

namespace Harrbor.Tests.Common;

/// <summary>
/// Mirrors the server's HTTP JSON options (camelCase + string enums).
/// </summary>
public static class TestJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };
}
