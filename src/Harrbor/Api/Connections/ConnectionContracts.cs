using System.Text.Json.Nodes;
using Harrbor.Abstractions.Connections;
using Harrbor.Abstractions.Providers;
using Harrbor.Services.Health;

namespace Harrbor.Api.Connections;

// ----- Provider metadata (drives generic UI form rendering) -----

public class ProviderResponse
{
    public string Key { get; set; } = string.Empty;
    public ConnectionCategory Category { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public int ProbeEveryNTicks { get; set; }
    public List<FieldSchemaDto> Fields { get; set; } = [];
}

public class FieldSchemaDto
{
    public string Name { get; set; } = string.Empty;
    public FieldType Type { get; set; }
    public FieldKind Kind { get; set; }
    public string Label { get; set; } = string.Empty;
    public string? HelpText { get; set; }
    public bool Secret { get; set; }
    public bool Required { get; set; }
    public int? Min { get; set; }
    public int? Max { get; set; }
    public string? ForbiddenChars { get; set; }
    public bool ForbidWhitespace { get; set; }
    public string? Pattern { get; set; }
    public ListElementRulesDto? Element { get; set; }
    public object? Default { get; set; }
    public int Order { get; set; }
}

public class ListElementRulesDto
{
    public bool RequireNonEmpty { get; set; }
    public bool ForbidWhitespace { get; set; }
    public string? ForbiddenChars { get; set; }
}

// ----- Connection CRUD -----

/// <summary>
/// Secret fields inside <see cref="Settings"/> are write-only: null/absent keeps the stored value,
/// an empty string clears it, any other value replaces it.
/// </summary>
public class ConnectionCreateRequest
{
    public string ProviderKey { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public JsonObject Settings { get; set; } = new();
}

/// <summary>
/// <see cref="ExpectedVersion"/> is an optional optimistic-concurrency guard: when set, the save is
/// rejected with 412 if the stored version differs (another edit won the race).
/// </summary>
public class ConnectionUpdateRequest
{
    public string? Name { get; set; }
    public bool? Enabled { get; set; }
    public JsonObject Settings { get; set; } = new();
    public int? ExpectedVersion { get; set; }
}

public class ConnectionTestRequest
{
    public string ProviderKey { get; set; } = string.Empty;
    public JsonObject Settings { get; set; } = new();
}

public class ConnectionResponse
{
    public int Id { get; set; }
    public string ProviderKey { get; set; } = string.Empty;
    public ConnectionCategory Category { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool Enabled { get; set; }

    /// <summary>The settings with secret values removed and replaced by "&lt;name&gt;Set" booleans.</summary>
    public JsonObject Settings { get; set; } = new();

    public int Version { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    /// <summary>Live reachability from the background health monitor (when included).</summary>
    public ConnectionHealthStatus? Status { get; set; }
    public DateTime? CheckedAtUtc { get; set; }
}

public class ConnectionHealth
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ProviderKey { get; set; } = string.Empty;
    public ConnectionCategory Category { get; set; }
    public bool Enabled { get; set; }
    public ConnectionHealthStatus Status { get; set; } = ConnectionHealthStatus.Unknown;
    public DateTime? CheckedAtUtc { get; set; }
    public int? LatencyMs { get; set; }
    public string? Error { get; set; }
}

public class ConnectionDeleteConflictResponse
{
    public string Message { get; set; } = string.Empty;
    public List<int> DependentJobIds { get; set; } = [];
    public List<string> DependentJobNames { get; set; } = [];
}

public class ConnectionTestResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? Error { get; set; }
}
