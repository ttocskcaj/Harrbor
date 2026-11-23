namespace Harrbor.Services.Conversion;

/// <summary>
/// Typed subset of <c>ffprobe -show_streams -show_format</c> output.
/// </summary>
public record MediaProbe(
    IReadOnlyList<MediaStream> Streams,
    double? DurationSeconds,
    string? FormatName,
    IReadOnlyDictionary<string, string> FormatTags)
{
    /// <summary>Real video streams, excluding cover art / attached pictures.</summary>
    public IEnumerable<MediaStream> VideoStreams =>
        Streams.Where(s => s.CodecType == "video" && !s.IsAttachedPicture);

    public int CountOf(string codecType) => Streams.Count(s => s.CodecType == codecType);

    public string? Title => FormatTags.GetValueOrDefault("title");
}

public record MediaStream(
    int Index,
    string CodecType,
    string? CodecName,
    string? RFrameRate,
    string? AvgFrameRate,
    bool IsAttachedPicture,
    IReadOnlyDictionary<string, string> Tags,
    IReadOnlyList<MediaSideData> SideData)
{
    public string? Language => Tags.GetValueOrDefault("language");

    public string? Title => Tags.GetValueOrDefault("title");

    public DoviConfiguration? DolbyVision =>
        SideData.FirstOrDefault(d => d.Type == DoviConfiguration.SideDataType) is { } d
            ? new DoviConfiguration(
                d.GetInt("dv_profile") ?? -1,
                d.GetInt("dv_level"),
                d.GetInt("rpu_present_flag") == 1,
                d.GetInt("el_present_flag") == 1,
                d.GetInt("bl_present_flag") == 1,
                d.GetInt("dv_bl_signal_compatibility_id"))
            : null;
}

/// <summary>
/// One entry of a stream's <c>side_data_list</c>. Scalar values are kept as their JSON text.
/// </summary>
public record MediaSideData(string Type, IReadOnlyDictionary<string, string> Values)
{
    public int? GetInt(string key) =>
        Values.TryGetValue(key, out var v) && int.TryParse(v, out var i) ? i : null;
}

public record DoviConfiguration(
    int Profile,
    int? Level,
    bool RpuPresent,
    bool EnhancementLayerPresent,
    bool BaseLayerPresent,
    int? BlSignalCompatibilityId)
{
    public const string SideDataType = "DOVI configuration record";
}
