using Harrbor.Configuration.Connections;

namespace Harrbor.Services.Configuration;

public interface IConnectionStore
{
    Task<StampedSettings<T>?> GetAsync<T>(CancellationToken cancellationToken = default)
        where T : class, IConnectionSettings;

    Task<StampedSettings<T>> SaveAsync<T>(T settings, CancellationToken cancellationToken = default)
        where T : class, IConnectionSettings;

    Task<bool> IsConfiguredAsync(string connectionType, CancellationToken cancellationToken = default);
}

public sealed record StampedSettings<T>(T Settings, int Version, DateTime UpdatedAtUtc);
