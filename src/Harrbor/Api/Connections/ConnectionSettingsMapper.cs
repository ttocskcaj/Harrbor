using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Harrbor.Abstractions.Providers;
using Harrbor.Abstractions.Validation;
using Harrbor.Services.Configuration;

namespace Harrbor.Api.Connections;

/// <summary>
/// Schema-driven validation, write-only secret merging, and response redaction for connection
/// settings — generic across all providers. Replaces the per-type hand-written mapping. Secret rule:
/// null (absent) keeps the stored value, "" clears it, any other value replaces it.
/// </summary>
public static class ConnectionSettingsMapper
{
    /// <summary>Validates a wire settings payload against the provider's schema (plus any bespoke provider rule).</summary>
    public static ValidationErrors Validate(IConnectionProvider provider, JsonObject wire, object? stored)
    {
        var errors = new ValidationErrors();

        object candidate;
        try
        {
            candidate = Deserialize(provider, wire);
        }
        catch (JsonException)
        {
            errors.Add("settings", "Malformed settings payload.");
            return errors;
        }

        foreach (var field in provider.Schema)
        {
            var prop = provider.SettingsType.GetProperty(field.ClrName)!;
            switch (field.Type)
            {
                case FieldType.String:
                    ValidateString(errors, provider, field, prop, wire, candidate, stored);
                    break;
                case FieldType.Int:
                    var value = (int)(prop.GetValue(candidate) ?? 0);
                    if (field.Min is int min && value < min)
                        errors.Add(field.Name, RangeMessage(field));
                    else if (field.Max is int max && value > max)
                        errors.Add(field.Name, RangeMessage(field));
                    break;
                case FieldType.StringList:
                    if (field.Element is { } element && prop.GetValue(candidate) is IEnumerable<string> items)
                        foreach (var item in items)
                            ValidateElement(errors, field, element, item);
                    break;
            }
        }

        provider.Validate(candidate, errors);
        return errors;
    }

    /// <summary>Materialises the typed, merged settings from a wire payload (applies the secret merge and trims scalars).</summary>
    public static object Merge(IConnectionProvider provider, JsonObject wire, object? stored)
    {
        var merged = Deserialize(provider, wire);
        var nullability = new NullabilityInfoContext();

        foreach (var field in provider.Schema)
        {
            var prop = provider.SettingsType.GetProperty(field.ClrName)!;

            if (field.Secret)
            {
                var requested = ReadSecret(wire, field);
                var storedValue = stored is null ? null : prop.GetValue(stored) as string;
                var mergedSecret = MergeSecret(requested, storedValue);
                prop.SetValue(merged, mergedSecret ?? (IsNullable(nullability, prop) ? null : ""));
            }
            else if (field.Type == FieldType.String)
            {
                var raw = prop.GetValue(merged) as string;
                var trimmed = string.IsNullOrWhiteSpace(raw)
                    ? (IsNullable(nullability, prop) ? null : "")
                    : raw.Trim();
                prop.SetValue(merged, trimmed);
            }
        }

        return merged;
    }

    /// <summary>Serialises settings to a wire object with secret values removed and replaced by "&lt;name&gt;Set" booleans.</summary>
    public static JsonObject Redact(IConnectionProvider provider, object settings)
    {
        var node = JsonSerializer.SerializeToNode(settings, provider.SettingsType, ConnectionJson.Options)!.AsObject();

        foreach (var field in provider.Schema.Where(f => f.Secret))
        {
            var prop = provider.SettingsType.GetProperty(field.ClrName)!;
            var isSet = !string.IsNullOrEmpty(prop.GetValue(settings) as string);
            node.Remove(field.Name);
            node[field.Name + "Set"] = isSet;
        }

        return node;
    }

    private static void ValidateString(
        ValidationErrors errors,
        IConnectionProvider provider,
        FieldDescriptor field,
        PropertyInfo prop,
        JsonObject wire,
        object candidate,
        object? stored)
    {
        if (field.Secret)
        {
            if (!field.Required)
                return;

            var requested = ReadSecret(wire, field);
            if (requested is null)
            {
                if (!HasStoredSecret(prop, stored))
                    errors.Add(field.Name, $"{field.Label} is required.");
            }
            else if (requested.Length == 0)
            {
                errors.Add(field.Name, $"{field.Label} cannot be empty.");
            }

            return;
        }

        var value = prop.GetValue(candidate) as string;

        if (field.Kind == FieldKind.Url)
        {
            if (field.Required || !string.IsNullOrWhiteSpace(value))
                errors.RequireAbsoluteHttpUrl(field.Name, value);
        }
        else if (field.Required)
        {
            errors.Require(field.Name, value, $"{field.Label} is required.");
        }

        if (!string.IsNullOrEmpty(value))
        {
            if ((field.ForbidWhitespace && value.Any(char.IsWhiteSpace))
                || (field.ForbiddenChars is not null && value.IndexOfAny(field.ForbiddenChars.ToCharArray()) >= 0))
            {
                errors.Add(field.Name, ForbiddenMessage(field));
            }

            if (field.Pattern is not null && !Regex.IsMatch(value, field.Pattern))
                errors.Add(field.Name, $"{field.Label} has an invalid format.");
        }
    }

    private static void ValidateElement(ValidationErrors errors, FieldDescriptor field, ListElementRules element, string item)
    {
        if (element.RequireNonEmpty && string.IsNullOrEmpty(item))
        {
            errors.Add(field.Name, $"{field.Label} entries must not be empty.");
            return;
        }

        if ((element.ForbidWhitespace && item.Any(char.IsWhiteSpace))
            || (element.ForbiddenChars is not null && item.IndexOfAny(element.ForbiddenChars.ToCharArray()) >= 0))
        {
            errors.Add(field.Name, $"'{item}' must not contain whitespace or quotes.");
        }
    }

    private static object Deserialize(IConnectionProvider provider, JsonObject wire)
        => wire.Deserialize(provider.SettingsType, ConnectionJson.Options)
           ?? Activator.CreateInstance(provider.SettingsType)!;

    private static string? ReadSecret(JsonObject wire, FieldDescriptor field)
        => wire.TryGetPropertyValue(field.Name, out var node) && node is not null
            ? node.GetValue<string>()
            : null;

    private static bool HasStoredSecret(PropertyInfo prop, object? stored)
        => stored is not null && !string.IsNullOrEmpty(prop.GetValue(stored) as string);

    private static bool IsNullable(NullabilityInfoContext context, PropertyInfo prop)
        => context.Create(prop).WriteState == NullabilityState.Nullable;

    private static string? MergeSecret(string? requested, string? stored) => requested switch
    {
        null => stored,
        "" => null,
        _ => requested,
    };

    private static string RangeMessage(FieldDescriptor field)
        => field is { Min: int min, Max: int max }
            ? $"Must be between {min} and {max}."
            : field.Min is int lo ? $"Must be at least {lo}."
            : $"Must be at most {field.Max}.";

    private static string ForbiddenMessage(FieldDescriptor field)
    {
        var parts = new List<string>();
        if (field.ForbidWhitespace)
            parts.Add("whitespace");
        if (field.ForbiddenChars is not null)
        {
            if (field.ForbiddenChars.Contains(','))
                parts.Add("commas");
            if (field.ForbiddenChars.Contains('"') || field.ForbiddenChars.Contains('\''))
                parts.Add("quotes");
        }

        return "Must not contain " + parts.Count switch
        {
            0 => "the forbidden characters",
            1 => parts[0],
            2 => $"{parts[0]} or {parts[1]}",
            _ => string.Join(", ", parts[..^1]) + ", or " + parts[^1],
        } + ".";
    }
}
