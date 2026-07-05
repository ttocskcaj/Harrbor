using System.Text.Json.Nodes;
using FluentAssertions;
using Harrbor.Abstractions.Providers;
using Harrbor.Api.Connections;
using Harrbor.Configuration.Connections;
using Harrbor.Configuration.Connections.Providers;
using Xunit;

namespace Harrbor.Tests.Unit.Api;

/// <summary>
/// Generic, schema-driven validation / secret-merge / redaction, replacing the per-type mapper.
/// Exercised through the real built-in providers.
/// </summary>
public class ConnectionSettingsMapperTests
{
    private static readonly IConnectionProvider QBittorrent = new QBittorrentProvider();
    private static readonly IConnectionProvider Sonarr = new SonarrProvider();
    private static readonly IConnectionProvider Rclone = new RcloneSftpProvider();

    private static JsonObject Wire(params (string Key, JsonNode? Value)[] entries)
    {
        var obj = new JsonObject();
        foreach (var (key, value) in entries)
            obj[key] = value;
        return obj;
    }

    // --- Secret merge: null keeps stored, "" clears, value replaces ---

    [Fact]
    public void Merge_NullSecret_KeepsStored()
    {
        var stored = new QBittorrentSettings { BaseUrl = "http://old", Username = "admin", Password = "stored-pw" };
        var wire = Wire(("baseUrl", "http://new"), ("username", "admin")); // password omitted

        var merged = (QBittorrentSettings)ConnectionSettingsMapper.Merge(QBittorrent, wire, stored);

        merged.BaseUrl.Should().Be("http://new");
        merged.Password.Should().Be("stored-pw");
    }

    [Fact]
    public void Merge_EmptySecret_Clears()
    {
        var stored = new QBittorrentSettings { Password = "stored-pw" };
        var wire = Wire(("baseUrl", "http://qbt"), ("password", ""));

        var merged = (QBittorrentSettings)ConnectionSettingsMapper.Merge(QBittorrent, wire, stored);

        merged.Password.Should().BeEmpty();
    }

    [Fact]
    public void Merge_ValueSecret_Replaces()
    {
        var stored = new QBittorrentSettings { Password = "stored-pw" };
        var wire = Wire(("baseUrl", "http://qbt"), ("password", "new-pw"));

        var merged = (QBittorrentSettings)ConnectionSettingsMapper.Merge(QBittorrent, wire, stored);

        merged.Password.Should().Be("new-pw");
    }

    [Fact]
    public void Merge_NoStored_NullSecret_YieldsEmptyForNonNullable()
    {
        var wire = Wire(("baseUrl", "http://qbt")); // password omitted

        var merged = (QBittorrentSettings)ConnectionSettingsMapper.Merge(QBittorrent, wire, stored: null);

        merged.Password.Should().BeEmpty();
    }

    [Fact]
    public void Merge_Servarr_NullApiKey_KeepsStored_AndAppliesOtherFields()
    {
        var stored = new SonarrSettings { BaseUrl = "http://old", ApiKey = "stored-key" };
        var wire = Wire(("baseUrl", "http://new"));

        var merged = (SonarrSettings)ConnectionSettingsMapper.Merge(Sonarr, wire, stored);

        merged.ApiKey.Should().Be("stored-key");
        merged.BaseUrl.Should().Be("http://new");
    }

    [Fact]
    public void Merge_RemoteStorage_MixedSecretRules()
    {
        var stored = new RemoteStorageSettings { Host = "old", User = "u", Password = "pw", KeyFilePassphrase = "phrase" };
        var wire = Wire(("host", "new-host"), ("user", "u"), ("keyFilePassphrase", "")); // password omitted, passphrase cleared

        var merged = (RemoteStorageSettings)ConnectionSettingsMapper.Merge(Rclone, wire, stored);

        merged.Host.Should().Be("new-host");
        merged.Password.Should().Be("pw");
        merged.KeyFilePassphrase.Should().BeNull();
    }

    // --- Validation ---

    [Theory]
    [InlineData("")]
    [InlineData("not-a-url")]
    [InlineData("ftp://host")]
    public void Validate_RejectsBadUrl(string baseUrl)
    {
        var errors = ConnectionSettingsMapper.Validate(QBittorrent, Wire(("baseUrl", baseUrl)), stored: null);

        errors.Build().Should().ContainKey("baseUrl");
    }

    [Fact]
    public void Validate_AcceptsHttpUrl()
    {
        var errors = ConnectionSettingsMapper.Validate(QBittorrent, Wire(("baseUrl", "http://qbt:8080")), stored: null);

        errors.HasErrors.Should().BeFalse();
    }

    [Fact]
    public void Validate_RequiredSecret_RequiredOnFirstSave()
    {
        var errors = ConnectionSettingsMapper.Validate(Sonarr, Wire(("baseUrl", "http://sonarr:8989")), stored: null);

        errors.Build().Should().ContainKey("apiKey");
    }

    [Fact]
    public void Validate_RequiredSecret_AllowedWhenStored()
    {
        var stored = new SonarrSettings { BaseUrl = "http://s", ApiKey = "stored-key" };

        var errors = ConnectionSettingsMapper.Validate(Sonarr, Wire(("baseUrl", "http://sonarr:8989")), stored);

        errors.HasErrors.Should().BeFalse();
    }

    [Fact]
    public void Validate_RequiredSecret_RejectsExplicitEmpty()
    {
        var stored = new SonarrSettings { BaseUrl = "http://s", ApiKey = "stored-key" };

        var errors = ConnectionSettingsMapper.Validate(Sonarr, Wire(("baseUrl", "http://sonarr:8989"), ("apiKey", "")), stored);

        errors.Build().Should().ContainKey("apiKey");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(99999)]
    public void Validate_RejectsOutOfRangeInt(int port)
    {
        var wire = Wire(("host", "seedbox"), ("user", "u"), ("port", port));

        var errors = ConnectionSettingsMapper.Validate(Rclone, wire, stored: null);

        errors.Build().Should().ContainKey("port");
    }

    [Fact]
    public void Validate_RequiresNonBlankFields()
    {
        var errors = ConnectionSettingsMapper.Validate(Rclone, new JsonObject(), stored: null);

        var built = errors.Build();
        built.Should().ContainKey("host");
        built.Should().ContainKey("user");
    }

    [Theory]
    [InlineData("seedbox,key_file=/etc/passwd", "u")]
    [InlineData("seed box", "u")]
    [InlineData("seedbox", "user,pass=x")]
    [InlineData("seedbox", "us er")]
    public void Validate_RejectsForbiddenCharsInScalars(string host, string user)
    {
        var errors = ConnectionSettingsMapper.Validate(Rclone, Wire(("host", host), ("user", user)), stored: null);

        errors.HasErrors.Should().BeTrue();
    }

    [Fact]
    public void Validate_KeyFile_RejectsCommaButAllowsWhitespaceRule()
    {
        var wire = Wire(("host", "seedbox"), ("user", "u"), ("keyFile", "/keys/id,key_file_pass=evil"));

        var errors = ConnectionSettingsMapper.Validate(Rclone, wire, stored: null);

        errors.Build().Should().ContainKey("keyFile");
    }

    [Theory]
    [InlineData("--flag with space")]
    [InlineData("--flag\"quoted\"")]
    public void Validate_RejectsUnsafeListElements(string flag)
    {
        var wire = Wire(("host", "seedbox"), ("user", "u"), ("additionalFlags", new JsonArray(flag)));

        var errors = ConnectionSettingsMapper.Validate(Rclone, wire, stored: null);

        errors.Build().Should().ContainKey("additionalFlags");
    }

    // --- Redaction: secrets never echoed, replaced by <name>Set booleans ---

    [Fact]
    public void Redact_ReportsSecretSet_WithoutValue()
    {
        var settings = new QBittorrentSettings { BaseUrl = "http://qbt", Username = "admin", Password = "pw" };

        var redacted = ConnectionSettingsMapper.Redact(QBittorrent, settings);

        redacted.ContainsKey("password").Should().BeFalse();
        redacted["passwordSet"]!.GetValue<bool>().Should().BeTrue();
        redacted["baseUrl"]!.GetValue<string>().Should().Be("http://qbt");
    }

    [Fact]
    public void Redact_RemoteStorage_ReportsEachSecretFlag()
    {
        var settings = new RemoteStorageSettings { Host = "seedbox", User = "u", Password = null, KeyFilePassphrase = "x" };

        var redacted = ConnectionSettingsMapper.Redact(Rclone, settings);

        redacted["passwordSet"]!.GetValue<bool>().Should().BeFalse();
        redacted["keyFilePassphraseSet"]!.GetValue<bool>().Should().BeTrue();
    }
}
