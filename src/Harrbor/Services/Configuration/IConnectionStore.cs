using Harrbor.Data.Entities;

namespace Harrbor.Services.Configuration;

/// <summary>
/// Persistence for connection instances, keyed by id. Holds no JSON policy — settings are stored as
/// an opaque <c>SettingsJson</c> string (serialised by the mapper/manager via <c>ConnectionJson</c>).
/// </summary>
public interface IConnectionStore
{
    Task<Connection?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Connection>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<Connection> CreateAsync(Connection connection, CancellationToken cancellationToken = default);

    /// <summary>Updates name/enabled/settings and bumps the version; returns null when no row has that id.</summary>
    Task<Connection?> UpdateAsync(int id, string name, bool enabled, string settingsJson, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
}
