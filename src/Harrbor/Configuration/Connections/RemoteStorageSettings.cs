using Harrbor.Abstractions.Providers;

namespace Harrbor.Configuration.Connections;

public class RemoteStorageSettings
{
    [Field(Required = true, ForbidWhitespace = true, ForbiddenChars = ",\"'", Label = "Host",
        HelpText = "Hostname or IP of the seedbox to reach over SFTP.", Order = 1)]
    public string Host { get; set; } = string.Empty;

    [Field(Kind = FieldKind.Port, Min = 1, Max = 65535, Label = "Port",
        HelpText = "SFTP port on the seedbox. Defaults to 22.", Order = 2)]
    public int Port { get; set; } = 22;

    [Field(Required = true, ForbidWhitespace = true, ForbiddenChars = ",\"'", Label = "User",
        HelpText = "SSH username used to authenticate to the seedbox.", Order = 3)]
    public string User { get; set; } = string.Empty;

    [Field(Kind = FieldKind.Password, Secret = true, Label = "Password",
        HelpText = "SSH password. Leave blank if you authenticate with a key file or the SSH agent.", Order = 4)]
    public string? Password { get; set; }

    [Field(ForbiddenChars = ",\"'", Label = "Key File",
        HelpText = "Absolute path to a private key file on the Harrbor host, used instead of a password.", Order = 5)]
    public string? KeyFile { get; set; }

    [Field(Kind = FieldKind.Password, Secret = true, Label = "Key File Passphrase",
        HelpText = "Passphrase protecting the key file, if it is encrypted.", Order = 6)]
    public string? KeyFilePassphrase { get; set; }

    [Field(Label = "Use SSH Agent",
        HelpText = "Authenticate through a running SSH agent instead of a password or key file.", Order = 7)]
    public bool UseAgent { get; set; } = false;

    [Field(Min = 1, Max = 32, Label = "Transfers",
        HelpText = "Files rclone copies in parallel. Higher saturates the link faster but adds load; 4 is a safe default.", Order = 8)]
    public int Transfers { get; set; } = 4;

    [Field(Min = 1, Max = 64, Label = "Checkers",
        HelpText = "Files rclone compares in parallel while checking what needs copying. 8 is a safe default.", Order = 9)]
    public int Checkers { get; set; } = 8;

    [Field(Kind = FieldKind.StringList, ElementRequireNonEmpty = true, ElementForbidWhitespace = true, ElementForbiddenChars = "\"'", Label = "Additional rclone flags",
        HelpText = "Extra rclone flags passed verbatim, one per entry (e.g. --bwlimit=10M). For tuning beyond the fields above.", Order = 10)]
    public List<string> AdditionalFlags { get; set; } = [];
}
