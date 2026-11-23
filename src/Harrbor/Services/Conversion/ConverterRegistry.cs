namespace Harrbor.Services.Conversion;

/// <summary>
/// Resolves converter names from configuration to registered <see cref="IMediaConverter"/> instances.
/// </summary>
public class ConverterRegistry
{
    private readonly Dictionary<string, IMediaConverter> _converters;

    public ConverterRegistry(IEnumerable<IMediaConverter> converters)
    {
        _converters = new Dictionary<string, IMediaConverter>(StringComparer.OrdinalIgnoreCase);
        foreach (var converter in converters)
        {
            if (!_converters.TryAdd(converter.Name, converter))
                throw new InvalidOperationException($"Duplicate media converter name '{converter.Name}'");
        }
    }

    public IReadOnlyCollection<string> Names => _converters.Keys;

    public bool TryGet(string name, out IMediaConverter converter) =>
        _converters.TryGetValue(name, out converter!);

    /// <summary>Resolves names in order. Throws <see cref="InvalidOperationException"/> on an unknown name.</summary>
    public IReadOnlyList<IMediaConverter> Resolve(IEnumerable<string> names)
    {
        var resolved = new List<IMediaConverter>();
        foreach (var name in names)
        {
            if (!_converters.TryGetValue(name, out var converter))
            {
                throw new InvalidOperationException(
                    $"Unknown media converter '{name}'. Available: {(Names.Count == 0 ? "(none)" : string.Join(", ", Names))}");
            }
            resolved.Add(converter);
        }
        return resolved;
    }
}
