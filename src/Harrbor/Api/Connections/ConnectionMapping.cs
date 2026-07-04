using Harrbor.Configuration.Connections;
using Harrbor.Services.Configuration;

namespace Harrbor.Api.Connections;

/// <summary>
/// Request validation, write-only secret merging, and response mapping for
/// connection settings. Secret rule: null keeps the stored value, "" clears it,
/// any other value replaces it. Test endpoints reuse the same merge so an
/// unsaved payload can be tested against the stored secret.
/// </summary>
public static class ConnectionMapping
{
    public static ValidationErrors Validate(QBittorrentConnectionRequest request)
    {
        var errors = new ValidationErrors();
        errors.RequireAbsoluteHttpUrl("baseUrl", request.BaseUrl);
        return errors;
    }

    public static ValidationErrors Validate(ServarrConnectionRequest request, bool hasStoredApiKey)
    {
        var errors = new ValidationErrors();
        errors.RequireAbsoluteHttpUrl("baseUrl", request.BaseUrl);

        if (request.ApiKey is null)
        {
            if (!hasStoredApiKey)
                errors.Add("apiKey", "An API key is required.");
        }
        else if (request.ApiKey.Length == 0)
        {
            errors.Add("apiKey", "API key cannot be empty.");
        }

        return errors;
    }

    public static ValidationErrors Validate(RemoteStorageConnectionRequest request)
    {
        var errors = new ValidationErrors();
        errors.Require("host", request.Host, "Host is required.");
        errors.RequireRange("port", request.Port, 1, 65535);
        errors.Require("user", request.User, "User is required.");
        errors.RequireRange("transfers", request.Transfers, 1, 32);
        errors.RequireRange("checkers", request.Checkers, 1, 64);

        // These values are spliced into the rclone inline connection string,
        // where commas separate parameters — reject delimiter characters
        RejectRcloneDelimiters(errors, "host", request.Host);
        RejectRcloneDelimiters(errors, "user", request.User);
        if (!string.IsNullOrWhiteSpace(request.KeyFile) && ContainsRcloneDelimiters(request.KeyFile))
            errors.Add("keyFile", "Must not contain commas or quotes.");

        // Flags are joined into the rclone command line — reject anything shell-hostile
        foreach (var flag in request.AdditionalFlags ?? [])
        {
            if (string.IsNullOrEmpty(flag) || flag.Any(char.IsWhiteSpace) || flag.Contains('"') || flag.Contains('\''))
                errors.Add("additionalFlags", $"Flag '{flag}' must be non-empty and contain no whitespace or quotes.");
        }

        return errors;
    }

    public static QBittorrentSettings Merge(QBittorrentConnectionRequest request, QBittorrentSettings? stored) => new()
    {
        BaseUrl = (request.BaseUrl ?? string.Empty).Trim(),
        Username = (request.Username ?? string.Empty).Trim(),
        Password = MergeSecret(request.Password, stored?.Password) ?? string.Empty
    };

    public static T MergeServarr<T>(ServarrConnectionRequest request, T? stored)
        where T : ServarrSettings, new() => new()
    {
        BaseUrl = (request.BaseUrl ?? string.Empty).Trim(),
        ApiKey = MergeSecret(request.ApiKey, stored?.ApiKey) ?? string.Empty,
        Enabled = request.Enabled
    };

    public static RemoteStorageSettings Merge(RemoteStorageConnectionRequest request, RemoteStorageSettings? stored) => new()
    {
        Host = (request.Host ?? string.Empty).Trim(),
        Port = request.Port,
        User = (request.User ?? string.Empty).Trim(),
        Password = MergeSecret(request.Password, stored?.Password),
        KeyFile = string.IsNullOrWhiteSpace(request.KeyFile) ? null : request.KeyFile.Trim(),
        KeyFilePassphrase = MergeSecret(request.KeyFilePassphrase, stored?.KeyFilePassphrase),
        UseAgent = request.UseAgent,
        Transfers = request.Transfers,
        Checkers = request.Checkers,
        AdditionalFlags = [.. request.AdditionalFlags ?? []]
    };

    public static QBittorrentConnectionResponse ToResponse(StampedSettings<QBittorrentSettings> stamped) => new()
    {
        BaseUrl = stamped.Settings.BaseUrl,
        Username = stamped.Settings.Username,
        PasswordSet = !string.IsNullOrEmpty(stamped.Settings.Password),
        Version = stamped.Version,
        UpdatedAtUtc = stamped.UpdatedAtUtc
    };

    public static ServarrConnectionResponse ToResponse<T>(StampedSettings<T> stamped)
        where T : ServarrSettings => new()
    {
        BaseUrl = stamped.Settings.BaseUrl,
        ApiKeySet = !string.IsNullOrEmpty(stamped.Settings.ApiKey),
        Enabled = stamped.Settings.Enabled,
        Version = stamped.Version,
        UpdatedAtUtc = stamped.UpdatedAtUtc
    };

    public static RemoteStorageConnectionResponse ToResponse(StampedSettings<RemoteStorageSettings> stamped) => new()
    {
        Host = stamped.Settings.Host,
        Port = stamped.Settings.Port,
        User = stamped.Settings.User,
        PasswordSet = !string.IsNullOrEmpty(stamped.Settings.Password),
        KeyFile = stamped.Settings.KeyFile,
        KeyFilePassphraseSet = !string.IsNullOrEmpty(stamped.Settings.KeyFilePassphrase),
        UseAgent = stamped.Settings.UseAgent,
        Transfers = stamped.Settings.Transfers,
        Checkers = stamped.Settings.Checkers,
        AdditionalFlags = [.. stamped.Settings.AdditionalFlags],
        Version = stamped.Version,
        UpdatedAtUtc = stamped.UpdatedAtUtc
    };

    private static string? MergeSecret(string? requested, string? stored) => requested switch
    {
        null => stored,
        "" => null,
        _ => requested
    };

    private static void RejectRcloneDelimiters(ValidationErrors errors, string field, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        if (ContainsRcloneDelimiters(value) || value.Any(char.IsWhiteSpace))
            errors.Add(field, "Must not contain whitespace, commas, or quotes.");
    }

    private static bool ContainsRcloneDelimiters(string value)
        => value.Contains(',') || value.Contains('"') || value.Contains('\'');
}
