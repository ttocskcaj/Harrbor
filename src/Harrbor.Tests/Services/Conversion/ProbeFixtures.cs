using Harrbor.Services.Conversion;

namespace Harrbor.Tests.Services.Conversion;

internal static class ProbeFixtures
{
    public static string Json(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Probe", $"{name}.json"));

    public static MediaProbe Load(string name) => MediaProbeParser.Parse(Json(name));
}
