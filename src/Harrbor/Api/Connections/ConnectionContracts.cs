namespace Harrbor.Api.Connections;

public class ConnectionSummaryResponse
{
    public string Type { get; set; } = string.Empty;
    public bool Configured { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
}

/// <summary>
/// Secret fields on requests are write-only: null/absent keeps the stored value,
/// an empty string clears it, and any other value replaces it.
/// </summary>
public class QBittorrentConnectionRequest
{
    public string BaseUrl { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string? Password { get; set; }
}

public class QBittorrentConnectionResponse
{
    public string BaseUrl { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public bool PasswordSet { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

public class ServarrConnectionRequest
{
    public string BaseUrl { get; set; } = string.Empty;
    public string? ApiKey { get; set; }
    public bool Enabled { get; set; } = true;
}

public class ServarrConnectionResponse
{
    public string BaseUrl { get; set; } = string.Empty;
    public bool ApiKeySet { get; set; }
    public bool Enabled { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

public class RemoteStorageConnectionRequest
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 22;
    public string User { get; set; } = string.Empty;
    public string? Password { get; set; }
    public string? KeyFile { get; set; }
    public string? KeyFilePassphrase { get; set; }
    public bool UseAgent { get; set; }
    public int Transfers { get; set; } = 4;
    public int Checkers { get; set; } = 8;
    public List<string> AdditionalFlags { get; set; } = [];
}

public class RemoteStorageConnectionResponse
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; }
    public string User { get; set; } = string.Empty;
    public bool PasswordSet { get; set; }
    public string? KeyFile { get; set; }
    public bool KeyFilePassphraseSet { get; set; }
    public bool UseAgent { get; set; }
    public int Transfers { get; set; }
    public int Checkers { get; set; }
    public List<string> AdditionalFlags { get; set; } = [];
    public DateTime UpdatedAtUtc { get; set; }
}

public class ConnectionTestResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? Error { get; set; }
}
