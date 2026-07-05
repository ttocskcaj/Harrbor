namespace Harrbor.Data.Entities;

/// <summary>
/// A configured connection instance (many per provider). Pure runtime data driven by the database —
/// selected per job by <see cref="Id"/>. <see cref="Version"/> is bumped on every save and drives
/// both optimistic concurrency and the connection manager's client-cache invalidation.
/// </summary>
public class Connection
{
    public int Id { get; set; }

    /// <summary>The provider kind, e.g. "sonarr". Identifies which <c>IConnectionProvider</c> owns this row.</summary>
    public string ProviderKey { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;

    /// <summary>The provider-specific settings, serialised with <c>ConnectionJson.Options</c> (camelCase).</summary>
    public string SettingsJson { get; set; } = string.Empty;

    public int Version { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
