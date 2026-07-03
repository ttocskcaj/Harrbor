using FluentAssertions;
using Harrbor.Api.Connections;
using Harrbor.Configuration.Connections;
using Harrbor.Services.Configuration;
using Xunit;

namespace Harrbor.Tests.Api;

public class ConnectionMapperTests
{
    // --- Secret merge rules: null keeps stored, "" clears, value replaces ---

    [Fact]
    public void Merge_QBittorrent_NullPassword_KeepsStoredSecret()
    {
        var stored = new QBittorrentSettings { BaseUrl = "http://old", Username = "admin", Password = "stored-pw" };
        var request = new QBittorrentConnectionRequest { BaseUrl = "http://new", Username = "admin", Password = null };

        var merged = ConnectionMapping.Merge(request, stored);

        merged.BaseUrl.Should().Be("http://new");
        merged.Password.Should().Be("stored-pw");
    }

    [Fact]
    public void Merge_QBittorrent_EmptyPassword_ClearsSecret()
    {
        var stored = new QBittorrentSettings { Password = "stored-pw" };
        var request = new QBittorrentConnectionRequest { BaseUrl = "http://qbt", Password = "" };

        var merged = ConnectionMapping.Merge(request, stored);

        merged.Password.Should().BeEmpty();
    }

    [Fact]
    public void Merge_QBittorrent_NewPassword_ReplacesSecret()
    {
        var stored = new QBittorrentSettings { Password = "stored-pw" };
        var request = new QBittorrentConnectionRequest { BaseUrl = "http://qbt", Password = "new-pw" };

        var merged = ConnectionMapping.Merge(request, stored);

        merged.Password.Should().Be("new-pw");
    }

    [Fact]
    public void Merge_QBittorrent_NoStored_NullPassword_YieldsEmpty()
    {
        var request = new QBittorrentConnectionRequest { BaseUrl = "http://qbt", Password = null };

        var merged = ConnectionMapping.Merge(request, stored: null);

        merged.Password.Should().BeEmpty();
    }

    [Fact]
    public void MergeServarr_Sonarr_NullApiKey_KeepsStored()
    {
        var stored = new SonarrSettings { BaseUrl = "http://old", ApiKey = "stored-key", Enabled = true };
        var request = new ServarrConnectionRequest { BaseUrl = "http://new", ApiKey = null, Enabled = false };

        var merged = ConnectionMapping.MergeServarr<SonarrSettings>(request, stored);

        merged.ApiKey.Should().Be("stored-key");
        merged.BaseUrl.Should().Be("http://new");
        merged.Enabled.Should().BeFalse();
    }

    [Fact]
    public void Merge_RemoteStorage_SecretRules()
    {
        var stored = new RemoteStorageSettings { Host = "old", User = "u", Password = "pw", KeyFilePassphrase = "phrase" };
        var request = new RemoteStorageConnectionRequest
        {
            Host = "new-host",
            User = "u",
            Password = null,          // keep
            KeyFilePassphrase = ""    // clear
        };

        var merged = ConnectionMapping.Merge(request, stored);

        merged.Host.Should().Be("new-host");
        merged.Password.Should().Be("pw");
        merged.KeyFilePassphrase.Should().BeNull();
    }

    // --- Validation ---

    [Theory]
    [InlineData("")]
    [InlineData("not-a-url")]
    [InlineData("ftp://host")]
    public void Validate_QBittorrent_RejectsBadBaseUrl(string baseUrl)
    {
        var errors = ConnectionMapping.Validate(new QBittorrentConnectionRequest { BaseUrl = baseUrl });

        errors.HasErrors.Should().BeTrue();
        errors.Build().Should().ContainKey("baseUrl");
    }

    [Fact]
    public void Validate_QBittorrent_AcceptsHttpUrl()
    {
        var errors = ConnectionMapping.Validate(new QBittorrentConnectionRequest { BaseUrl = "http://qbt:8080" });

        errors.HasErrors.Should().BeFalse();
    }

    [Fact]
    public void Validate_Servarr_RequiresApiKeyOnFirstSave()
    {
        var request = new ServarrConnectionRequest { BaseUrl = "http://sonarr:8989", ApiKey = null };

        var errors = ConnectionMapping.Validate(request, hasStoredApiKey: false);

        errors.Build().Should().ContainKey("apiKey");
    }

    [Fact]
    public void Validate_Servarr_AllowsNullApiKey_WhenStoredExists()
    {
        var request = new ServarrConnectionRequest { BaseUrl = "http://sonarr:8989", ApiKey = null };

        var errors = ConnectionMapping.Validate(request, hasStoredApiKey: true);

        errors.HasErrors.Should().BeFalse();
    }

    [Fact]
    public void Validate_Servarr_RejectsEmptyApiKey()
    {
        var request = new ServarrConnectionRequest { BaseUrl = "http://sonarr:8989", ApiKey = "" };

        var errors = ConnectionMapping.Validate(request, hasStoredApiKey: true);

        errors.Build().Should().ContainKey("apiKey");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(99999)]
    public void Validate_RemoteStorage_RejectsOutOfRangePort(int port)
    {
        var request = new RemoteStorageConnectionRequest { Host = "seedbox", User = "u", Port = port };

        var errors = ConnectionMapping.Validate(request);

        errors.Build().Should().ContainKey("port");
    }

    [Fact]
    public void Validate_RemoteStorage_RequiresHostAndUser()
    {
        var errors = ConnectionMapping.Validate(new RemoteStorageConnectionRequest());

        var built = errors.Build();
        built.Should().ContainKey("host");
        built.Should().ContainKey("user");
    }

    [Theory]
    [InlineData("seedbox,key_file=/etc/passwd", "u")]
    [InlineData("seed box", "u")]
    [InlineData("seedbox", "user,pass=x")]
    [InlineData("seedbox", "us er")]
    public void Validate_RemoteStorage_RejectsDelimitersInHostAndUser(string host, string user)
    {
        // host/user are spliced into the rclone inline connection string where
        // commas separate parameters — delimiters must not pass validation
        var errors = ConnectionMapping.Validate(new RemoteStorageConnectionRequest { Host = host, User = user });

        errors.HasErrors.Should().BeTrue();
    }

    [Fact]
    public void Validate_RemoteStorage_RejectsCommaInKeyFile()
    {
        var request = new RemoteStorageConnectionRequest
        {
            Host = "seedbox",
            User = "u",
            KeyFile = "/keys/id,key_file_pass=evil"
        };

        var errors = ConnectionMapping.Validate(request);

        errors.Build().Should().ContainKey("keyFile");
    }

    [Theory]
    [InlineData("--flag with space")]
    [InlineData("--flag\"quoted\"")]
    public void Validate_RemoteStorage_RejectsUnsafeAdditionalFlags(string flag)
    {
        var request = new RemoteStorageConnectionRequest
        {
            Host = "seedbox",
            User = "u",
            AdditionalFlags = [flag]
        };

        var errors = ConnectionMapping.Validate(request);

        errors.Build().Should().ContainKey("additionalFlags");
    }

    // --- Responses never carry secret values ---

    [Fact]
    public void ToResponse_QBittorrent_ReportsPasswordSetWithoutValue()
    {
        var stamped = new StampedSettings<QBittorrentSettings>(
            new QBittorrentSettings { BaseUrl = "http://qbt", Username = "admin", Password = "pw" },
            Version: 3,
            UpdatedAtUtc: DateTime.UtcNow);

        var response = ConnectionMapping.ToResponse(stamped);

        response.PasswordSet.Should().BeTrue();
        response.GetType().GetProperty("Password").Should().BeNull();
    }

    [Fact]
    public void ToResponse_RemoteStorage_ReportsSecretFlags()
    {
        var stamped = new StampedSettings<RemoteStorageSettings>(
            new RemoteStorageSettings { Host = "seedbox", User = "u", Password = null, KeyFilePassphrase = "x" },
            Version: 1,
            UpdatedAtUtc: DateTime.UtcNow);

        var response = ConnectionMapping.ToResponse(stamped);

        response.PasswordSet.Should().BeFalse();
        response.KeyFilePassphraseSet.Should().BeTrue();
    }
}
