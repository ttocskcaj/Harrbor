using System.Reflection;
using System.Text.Json;

namespace Harrbor.Abstractions.Providers;

/// <summary>
/// Reflects a settings CLR type's <see cref="FieldAttribute"/>-annotated properties into an ordered
/// <see cref="FieldDescriptor"/> list. Called once per provider (the result is cached on the
/// provider). Defaults are snapshotted from a fresh instance so field initializers (e.g. Port = 22,
/// AdditionalFlags = []) become the schema defaults for free.
/// </summary>
public static class FieldSchemaBuilder
{
    public static IReadOnlyList<FieldDescriptor> Build(Type settingsType)
    {
        var instance = Activator.CreateInstance(settingsType)
            ?? throw new InvalidOperationException($"Cannot instantiate settings type {settingsType.FullName}.");

        var fields = new List<FieldDescriptor>();
        foreach (var prop in settingsType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var attr = prop.GetCustomAttribute<FieldAttribute>();
            if (attr is null)
                continue;

            var type = ResolveFieldType(prop.PropertyType, prop.Name);
            var kind = attr.Kind == FieldKind.Auto ? DefaultKind(type) : attr.Kind;

            ListElementRules? element = null;
            if (type == FieldType.StringList &&
                (attr.ElementRequireNonEmpty || attr.ElementForbidWhitespace || attr.ElementForbiddenChars is not null))
            {
                element = new ListElementRules(
                    attr.ElementRequireNonEmpty,
                    attr.ElementForbidWhitespace,
                    attr.ElementForbiddenChars);
            }

            fields.Add(new FieldDescriptor
            {
                Name = JsonNamingPolicy.CamelCase.ConvertName(prop.Name),
                ClrName = prop.Name,
                Type = type,
                Kind = kind,
                Label = string.IsNullOrWhiteSpace(attr.Label) ? prop.Name : attr.Label!,
                HelpText = attr.HelpText,
                Secret = attr.Secret,
                Required = attr.Required,
                ForbidWhitespace = attr.ForbidWhitespace,
                Min = attr.Min == int.MinValue ? null : attr.Min,
                Max = attr.Max == int.MaxValue ? null : attr.Max,
                ForbiddenChars = attr.ForbiddenChars,
                Pattern = attr.Pattern,
                Element = element,
                Default = prop.GetValue(instance),
                Order = attr.Order,
            });
        }

        return fields.OrderBy(f => f.Order).ThenBy(f => f.ClrName, StringComparer.Ordinal).ToArray();
    }

    private static FieldType ResolveFieldType(Type clrType, string propName)
    {
        var t = Nullable.GetUnderlyingType(clrType) ?? clrType;
        if (t == typeof(string)) return FieldType.String;
        if (t == typeof(int)) return FieldType.Int;
        if (t == typeof(bool)) return FieldType.Bool;
        if (t != typeof(string) && typeof(IEnumerable<string>).IsAssignableFrom(t)) return FieldType.StringList;
        throw new NotSupportedException(
            $"Unsupported [Field] property type '{clrType}' on '{propName}'. Supported: string, int, bool, IEnumerable<string>.");
    }

    private static FieldKind DefaultKind(FieldType type) => type switch
    {
        FieldType.String => FieldKind.Text,
        FieldType.Int => FieldKind.Int,
        FieldType.Bool => FieldKind.Bool,
        FieldType.StringList => FieldKind.StringList,
        _ => FieldKind.Text,
    };
}
