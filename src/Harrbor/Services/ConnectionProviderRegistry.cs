using Harrbor.Abstractions.Connections;
using Harrbor.Abstractions.Providers;

namespace Harrbor.Services;

/// <summary>
/// The set of code-defined connection providers (the repurposed catalog — a registry of "kinds",
/// with no DI or instance responsibilities). Built once from all registered <see cref="IConnectionProvider"/>s.
/// </summary>
public interface IConnectionProviderRegistry
{
    IReadOnlyList<IConnectionProvider> All { get; }
    IConnectionProvider? Find(string key);
    IEnumerable<IConnectionProvider> OfCategory(ConnectionCategory category);
}

public sealed class ConnectionProviderRegistry : IConnectionProviderRegistry
{
    private readonly Dictionary<string, IConnectionProvider> _byKey;

    public ConnectionProviderRegistry(IEnumerable<IConnectionProvider> providers)
    {
        All = [.. providers];
        _byKey = All.ToDictionary(p => p.Key, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<IConnectionProvider> All { get; }

    public IConnectionProvider? Find(string key) => _byKey.GetValueOrDefault(key);

    public IEnumerable<IConnectionProvider> OfCategory(ConnectionCategory category)
        => All.Where(p => p.Category == category);
}
