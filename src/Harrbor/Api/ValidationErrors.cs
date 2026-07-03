namespace Harrbor.Api;

/// <summary>
/// Accumulates field-level validation errors keyed by camelCase field name,
/// matching the shape of HttpValidationProblemDetails.errors.
/// </summary>
public sealed class ValidationErrors
{
    private readonly Dictionary<string, List<string>> _errors = [];

    public bool HasErrors => _errors.Count > 0;

    public bool Has(string field) => _errors.ContainsKey(field);

    public void Add(string field, string message)
    {
        if (!_errors.TryGetValue(field, out var messages))
        {
            messages = [];
            _errors[field] = messages;
        }

        messages.Add(message);
    }

    public void Require(string field, string? value, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
            Add(field, message);
    }

    public void RequireAbsoluteHttpUrl(string field, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            Add(field, "A URL is required.");
            return;
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            Add(field, "Must be an absolute http:// or https:// URL.");
        }
    }

    public void RequireRange(string field, int value, int min, int max)
    {
        if (value < min || value > max)
            Add(field, $"Must be between {min} and {max}.");
    }

    public Dictionary<string, string[]> Build()
        => _errors.ToDictionary(kv => kv.Key, kv => kv.Value.ToArray());
}
