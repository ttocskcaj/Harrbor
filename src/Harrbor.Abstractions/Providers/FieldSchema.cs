namespace Harrbor.Abstractions.Providers;

/// <summary>The persisted CLR shape of a settings field.</summary>
public enum FieldType
{
    String,
    Int,
    Bool,
    StringList,
}

/// <summary>
/// The intended UI/validation treatment of a field. <see cref="Auto"/> is resolved from
/// <see cref="FieldType"/> when the schema is built; declare an explicit kind to override
/// (e.g. a string that is a <see cref="Url"/> or a <see cref="Password"/>).
/// </summary>
public enum FieldKind
{
    Auto,
    Text,
    Url,
    Password,
    Port,
    Int,
    Bool,
    StringList,
}

/// <summary>
/// Declares form/validation metadata for a connection settings property. Reflected once per
/// provider by <see cref="FieldSchemaBuilder"/> into a <see cref="FieldDescriptor"/>. The single
/// source of truth for generic validation, secret handling, and UI form rendering.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class FieldAttribute : Attribute
{
    /// <summary>Human label; defaults to the property name when unset.</summary>
    public string? Label { get; set; }

    /// <summary>Optional help/description text for the field.</summary>
    public string? HelpText { get; set; }

    /// <summary>UI/validation treatment; inferred from the CLR type when left <see cref="FieldKind.Auto"/>.</summary>
    public FieldKind Kind { get; set; } = FieldKind.Auto;

    /// <summary>Write-only secret: redacted in responses, merged (null=keep/""=clear/value=replace) on write.</summary>
    public bool Secret { get; set; }

    /// <summary>Required. For a secret this means "required unless a value is already stored".</summary>
    public bool Required { get; set; }

    /// <summary>Reject whitespace anywhere in the (scalar) value.</summary>
    public bool ForbidWhitespace { get; set; }

    /// <summary>Inclusive minimum for an int field. Unset when left at <see cref="int.MinValue"/>.</summary>
    public int Min { get; set; } = int.MinValue;

    /// <summary>Inclusive maximum for an int field. Unset when left at <see cref="int.MaxValue"/>.</summary>
    public int Max { get; set; } = int.MaxValue;

    /// <summary>Reject any of these characters in the (scalar) value.</summary>
    public string? ForbiddenChars { get; set; }

    /// <summary>Optional regular expression the (scalar) value must match.</summary>
    public string? Pattern { get; set; }

    /// <summary>For a <see cref="FieldType.StringList"/>: reject empty elements.</summary>
    public bool ElementRequireNonEmpty { get; set; }

    /// <summary>For a <see cref="FieldType.StringList"/>: reject whitespace within any element.</summary>
    public bool ElementForbidWhitespace { get; set; }

    /// <summary>For a <see cref="FieldType.StringList"/>: reject any of these characters within an element.</summary>
    public string? ElementForbiddenChars { get; set; }

    /// <summary>Display/validation ordering (ascending).</summary>
    public int Order { get; set; }
}

/// <summary>Per-element validation rules for a <see cref="FieldType.StringList"/> field.</summary>
public sealed record ListElementRules(bool RequireNonEmpty, bool ForbidWhitespace, string? ForbiddenChars);

/// <summary>
/// The reflected, immutable schema for one settings field. Drives generic validation, the secret
/// merge/redact engine, and (via the API) UI form rendering.
/// </summary>
public sealed record FieldDescriptor
{
    /// <summary>camelCase wire name, e.g. "baseUrl".</summary>
    public required string Name { get; init; }

    /// <summary>PascalCase CLR property name, e.g. "BaseUrl" — used for reflective get/set.</summary>
    public required string ClrName { get; init; }

    public required FieldType Type { get; init; }
    public required FieldKind Kind { get; init; }
    public required string Label { get; init; }
    public string? HelpText { get; init; }
    public bool Secret { get; init; }
    public bool Required { get; init; }
    public bool ForbidWhitespace { get; init; }
    public int? Min { get; init; }
    public int? Max { get; init; }
    public string? ForbiddenChars { get; init; }
    public string? Pattern { get; init; }
    public ListElementRules? Element { get; init; }

    /// <summary>The field's default value, snapshotted from a fresh settings instance.</summary>
    public object? Default { get; init; }

    public int Order { get; init; }
}
