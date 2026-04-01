using FluentAssertions;
using Harrbor.Services.RemoteStorage;
using Xunit;

namespace Harrbor.Tests.Services.RemoteStorage;

public class RcloneRemoteStorageServiceTests
{
    [Theory]
    [InlineData("Transferred: 1.234 GiB / 1.234 GiB, 100%, 50.000 MiB/s", 1324997410L)] // (long)(1.234 * 1024^3)
    [InlineData("Transferred: 2.5 GiB / 2.5 GiB, 100%, 100.000 MiB/s", 2684354560L)] // (long)(2.5 * 1024^3)
    public void ParseBytesTransferred_GiBInput_ReturnsCorrectBytes(string line, long expectedBytes)
    {
        // Arrange
        var lines = new List<string> { line };

        // Act
        var result = RcloneRemoteStorageService.ParseBytesTransferred(lines);

        // Assert
        result.Should().Be(expectedBytes);
    }

    [Theory]
    [InlineData("Transferred: 512 MiB / 512 MiB, 100%, 50.000 MiB/s", 536870912L)] // 512 * 1024^2
    [InlineData("Transferred: 100.5 MiB / 100.5 MiB, 100%, 25.000 MiB/s", 105381888L)] // 100.5 * 1024^2
    public void ParseBytesTransferred_MiBInput_ReturnsCorrectBytes(string line, long expectedBytes)
    {
        // Arrange
        var lines = new List<string> { line };

        // Act
        var result = RcloneRemoteStorageService.ParseBytesTransferred(lines);

        // Assert
        result.Should().Be(expectedBytes);
    }

    [Theory]
    [InlineData("Transferred: 1024 KiB / 1024 KiB, 100%, 1.000 MiB/s", 1048576L)] // 1024 * 1024
    [InlineData("Transferred: 500 KiB / 500 KiB, 100%, 500.000 KiB/s", 512000L)] // 500 * 1024
    public void ParseBytesTransferred_KiBInput_ReturnsCorrectBytes(string line, long expectedBytes)
    {
        // Arrange
        var lines = new List<string> { line };

        // Act
        var result = RcloneRemoteStorageService.ParseBytesTransferred(lines);

        // Assert
        result.Should().Be(expectedBytes);
    }

    [Theory]
    [InlineData("Transferred: 1.5 TiB / 1.5 TiB, 100%, 100.000 MiB/s", 1649267441664L)] // 1.5 * 1024^4
    [InlineData("Transferred: 2 TiB / 2 TiB, 100%, 200.000 MiB/s", 2199023255552L)] // 2 * 1024^4
    public void ParseBytesTransferred_TiBInput_ReturnsCorrectBytes(string line, long expectedBytes)
    {
        // Arrange
        var lines = new List<string> { line };

        // Act
        var result = RcloneRemoteStorageService.ParseBytesTransferred(lines);

        // Assert
        result.Should().Be(expectedBytes);
    }

    [Theory]
    [InlineData("Transferred: 1024 B / 1024 B, 100%, 1.000 KiB/s", 1024L)]
    [InlineData("Transferred: 500 B / 500 B, 100%, 500 B/s", 500L)]
    public void ParseBytesTransferred_BytesInput_ReturnsCorrectBytes(string line, long expectedBytes)
    {
        // Arrange
        var lines = new List<string> { line };

        // Act
        var result = RcloneRemoteStorageService.ParseBytesTransferred(lines);

        // Assert
        result.Should().Be(expectedBytes);
    }

    [Fact]
    public void ParseBytesTransferred_NoMatch_ReturnsZero()
    {
        // Arrange
        var lines = new List<string>
        {
            "Some random log line",
            "Another log line without transfer info"
        };

        // Act
        var result = RcloneRemoteStorageService.ParseBytesTransferred(lines);

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public void ParseBytesTransferred_EmptyLines_ReturnsZero()
    {
        // Arrange
        var lines = new List<string>();

        // Act
        var result = RcloneRemoteStorageService.ParseBytesTransferred(lines);

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public void ParseBytesTransferred_MultipleLines_ReturnsLastMatch()
    {
        // Arrange
        var lines = new List<string>
        {
            "Transferred: 100 MiB / 500 MiB, 20%, 50.000 MiB/s",
            "Transferred: 250 MiB / 500 MiB, 50%, 50.000 MiB/s",
            "Transferred: 500 MiB / 500 MiB, 100%, 50.000 MiB/s"
        };

        // Act
        var result = RcloneRemoteStorageService.ParseBytesTransferred(lines);

        // Assert - should return bytes from last line (500 MiB)
        result.Should().Be(524288000L); // 500 * 1024^2
    }

    [Theory]
    [InlineData(":sftp,host=example.com,pass=secretpassword:", ":sftp,host=example.com,pass=[REDACTED]")] // trailing : consumed
    [InlineData(":sftp,host=example.com,user=admin,pass=mypass123,port=22:", ":sftp,host=example.com,user=admin,pass=[REDACTED],port=22:")] // comma stops match
    public void RedactSensitiveArgs_WithPassword_RedactsPassword(string input, string expected)
    {
        // Act
        var result = RcloneRemoteStorageService.RedactSensitiveArgs(input);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(":sftp,host=example.com,key_file=/path/to/key,key_file_pass=keypassword:", ":sftp,host=example.com,key_file=/path/to/key,key_file_pass=[REDACTED]")] // trailing : consumed
    [InlineData(":sftp,key_file_pass=secret123:", ":sftp,key_file_pass=[REDACTED]")] // trailing : consumed
    public void RedactSensitiveArgs_WithKeyFilePass_RedactsKeyFilePass(string input, string expected)
    {
        // Act
        var result = RcloneRemoteStorageService.RedactSensitiveArgs(input);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(":sftp,host=example.com,pass=secret1,key_file_pass=secret2:", ":sftp,host=example.com,pass=[REDACTED],key_file_pass=[REDACTED]")] // trailing : consumed
    public void RedactSensitiveArgs_WithBothPasswords_RedactsBoth(string input, string expected)
    {
        // Act
        var result = RcloneRemoteStorageService.RedactSensitiveArgs(input);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void RedactSensitiveArgs_NoSensitiveData_ReturnsUnchanged()
    {
        // Arrange
        var input = ":sftp,host=example.com,user=admin,port=22:";

        // Act
        var result = RcloneRemoteStorageService.RedactSensitiveArgs(input);

        // Assert
        result.Should().Be(input);
    }

    [Fact]
    public void RedactSensitiveArgs_EmptyString_ReturnsEmpty()
    {
        // Act
        var result = RcloneRemoteStorageService.RedactSensitiveArgs(string.Empty);

        // Assert
        result.Should().BeEmpty();
    }

    [Theory]
    [InlineData("rclone copy \":sftp,pass=secret:/path\" \"/dest\"", "rclone copy \":sftp,pass=[REDACTED] \"/dest\"")] // regex consumes up to space
    public void RedactSensitiveArgs_InCommandLine_RedactsCorrectly(string input, string expected)
    {
        // Act
        var result = RcloneRemoteStorageService.RedactSensitiveArgs(input);

        // Assert
        result.Should().Be(expected);
    }
}
