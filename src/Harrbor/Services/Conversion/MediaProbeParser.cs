using System.Globalization;
using System.Text.Json;

namespace Harrbor.Services.Conversion;

/// <summary>
/// Parses <c>ffprobe -of json -show_streams -show_format</c> output. Pure, for unit testing.
/// </summary>
public static class MediaProbeParser
{
    public static MediaProbe Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var streams = new List<MediaStream>();
        if (root.TryGetProperty("streams", out var streamsEl) && streamsEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var s in streamsEl.EnumerateArray())
                streams.Add(ParseStream(s));
        }

        double? duration = null;
        string? formatName = null;
        IReadOnlyDictionary<string, string> formatTags = new Dictionary<string, string>();
        if (root.TryGetProperty("format", out var format) && format.ValueKind == JsonValueKind.Object)
        {
            duration = ParseDouble(GetString(format, "duration"));
            formatName = GetString(format, "format_name");
            formatTags = ParseTags(format);
        }

        return new MediaProbe(streams, duration, formatName, formatTags);
    }

    private static MediaStream ParseStream(JsonElement s)
    {
        var attachedPic = s.TryGetProperty("disposition", out var disp)
            && disp.ValueKind == JsonValueKind.Object
            && disp.TryGetProperty("attached_pic", out var ap)
            && ap.ValueKind == JsonValueKind.Number
            && ap.GetInt32() == 1;

        var sideData = new List<MediaSideData>();
        if (s.TryGetProperty("side_data_list", out var sdl) && sdl.ValueKind == JsonValueKind.Array)
        {
            foreach (var sd in sdl.EnumerateArray())
            {
                var type = GetString(sd, "side_data_type") ?? string.Empty;
                var values = new Dictionary<string, string>();
                foreach (var prop in sd.EnumerateObject())
                {
                    if (prop.Value.ValueKind is JsonValueKind.Number or JsonValueKind.String
                        or JsonValueKind.True or JsonValueKind.False)
                    {
                        values[prop.Name] = prop.Value.ValueKind == JsonValueKind.String
                            ? prop.Value.GetString()!
                            : prop.Value.GetRawText();
                    }
                }
                sideData.Add(new MediaSideData(type, values));
            }
        }

        return new MediaStream(
            Index: s.TryGetProperty("index", out var idx) && idx.ValueKind == JsonValueKind.Number ? idx.GetInt32() : -1,
            CodecType: GetString(s, "codec_type") ?? "unknown",
            CodecName: GetString(s, "codec_name"),
            RFrameRate: GetString(s, "r_frame_rate"),
            AvgFrameRate: GetString(s, "avg_frame_rate"),
            IsAttachedPicture: attachedPic,
            Tags: ParseTags(s),
            SideData: sideData);
    }

    private static IReadOnlyDictionary<string, string> ParseTags(JsonElement el)
    {
        // Matroska tag keys vary in case (e.g. "DURATION", "language"); compare case-insensitively.
        var tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (el.TryGetProperty("tags", out var t) && t.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in t.EnumerateObject())
            {
                if (prop.Value.ValueKind == JsonValueKind.String)
                    tags[prop.Name] = prop.Value.GetString()!;
            }
        }
        return tags;
    }

    private static string? GetString(JsonElement el, string name) =>
        el.TryGetProperty(name, out var v)
            ? v.ValueKind switch
            {
                JsonValueKind.String => v.GetString(),
                JsonValueKind.Number => v.GetRawText(),
                _ => null
            }
            : null;

    private static double? ParseDouble(string? s) =>
        double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : null;
}
