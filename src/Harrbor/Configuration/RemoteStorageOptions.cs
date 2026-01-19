namespace Harrbor.Configuration;

public class RemoteStorageOptions
{
    public const string SectionName = "RemoteStorage";

    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 22;
    public string User { get; set; } = string.Empty;
    public string? Password { get; set; }
    public string? KeyFile { get; set; }
    public string? KeyFilePassphrase { get; set; }
    public bool UseAgent { get; set; } = false;
    public int Transfers { get; set; } = 4;
    public int Checkers { get; set; } = 8;
    public List<string> AdditionalFlags { get; set; } = [];
}
