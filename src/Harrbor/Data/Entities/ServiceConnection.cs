namespace Harrbor.Data.Entities;

public class ServiceConnection
{
    public int Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string SettingsJson { get; set; } = string.Empty;
    public int Version { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
