using FakeItEasy;
using FluentAssertions;
using Harrbor.Configuration;
using Harrbor.Services;
using Harrbor.Services.Clients;
using Harrbor.Services.RemoteStorage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace Harrbor.Tests.Services;

public class StartupValidatorTests : IDisposable
{
    private readonly IQBittorrentClient _qBittorrentClient;
    private readonly ISonarrClient _sonarrClient;
    private readonly IRadarrClient _radarrClient;
    private readonly IRemoteStorageService _remoteStorageService;
    private readonly IOptions<SonarrOptions> _sonarrOptions;
    private readonly IOptions<RadarrOptions> _radarrOptions;
    private readonly IOptions<JobOptions> _jobOptions;
    private readonly ILogger<StartupValidator> _logger;
    private readonly string _tempDir;

    public StartupValidatorTests()
    {
        _qBittorrentClient = A.Fake<IQBittorrentClient>();
        _sonarrClient = A.Fake<ISonarrClient>();
        _radarrClient = A.Fake<IRadarrClient>();
        _remoteStorageService = A.Fake<IRemoteStorageService>();
        _sonarrOptions = Options.Create(new SonarrOptions { Enabled = true });
        _radarrOptions = Options.Create(new RadarrOptions { Enabled = true });
        _jobOptions = Options.Create(new JobOptions { Definitions = [] });
        _logger = A.Fake<ILogger<StartupValidator>>();
        _tempDir = Path.Combine(Path.GetTempPath(), $"harrbor-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    private StartupValidator CreateValidator(
        IOptions<SonarrOptions>? sonarrOptions = null,
        IOptions<RadarrOptions>? radarrOptions = null,
        IOptions<JobOptions>? jobOptions = null)
    {
        return new StartupValidator(
            _qBittorrentClient,
            _sonarrClient,
            _radarrClient,
            _remoteStorageService,
            sonarrOptions ?? _sonarrOptions,
            radarrOptions ?? _radarrOptions,
            jobOptions ?? _jobOptions,
            _logger);
    }

    [Fact]
    public async Task ValidateAsync_RemoteStorageFails_ThrowsException()
    {
        // Arrange
        A.CallTo(() => _remoteStorageService.TestConnectionAsync(A<CancellationToken>._))
            .Returns(new RemoteStorageConnectionResult(false, "Connection refused"));

        var validator = CreateValidator();

        // Act
        var act = () => validator.ValidateAsync();

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Remote storage*Connection refused*");
    }

    [Fact]
    public async Task ValidateAsync_RemoteStorageSucceeds_DoesNotThrow()
    {
        // Arrange
        A.CallTo(() => _remoteStorageService.TestConnectionAsync(A<CancellationToken>._))
            .Returns(new RemoteStorageConnectionResult(true));
        A.CallTo(() => _qBittorrentClient.TestConnectionAsync(A<CancellationToken>._))
            .Returns(new ConnectionTestResult(true, Version: "2.8.19"));
        A.CallTo(() => _sonarrClient.IsHealthyAsync(A<CancellationToken>._))
            .Returns(true);
        A.CallTo(() => _radarrClient.IsHealthyAsync(A<CancellationToken>._))
            .Returns(true);

        var validator = CreateValidator();

        // Act
        var act = () => validator.ValidateAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ValidateAsync_QBittorrentFails_DoesNotThrow()
    {
        // Arrange
        A.CallTo(() => _remoteStorageService.TestConnectionAsync(A<CancellationToken>._))
            .Returns(new RemoteStorageConnectionResult(true));
        A.CallTo(() => _qBittorrentClient.TestConnectionAsync(A<CancellationToken>._))
            .Returns(new ConnectionTestResult(false, Error: "Connection refused"));
        A.CallTo(() => _sonarrClient.IsHealthyAsync(A<CancellationToken>._))
            .Returns(true);
        A.CallTo(() => _radarrClient.IsHealthyAsync(A<CancellationToken>._))
            .Returns(true);

        var validator = CreateValidator();

        // Act
        var act = () => validator.ValidateAsync();

        // Assert - Should not throw, but logs warning
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ValidateAsync_SonarrFails_DoesNotThrow()
    {
        // Arrange
        A.CallTo(() => _remoteStorageService.TestConnectionAsync(A<CancellationToken>._))
            .Returns(new RemoteStorageConnectionResult(true));
        A.CallTo(() => _qBittorrentClient.TestConnectionAsync(A<CancellationToken>._))
            .Returns(new ConnectionTestResult(true, Version: "2.8.19"));
        A.CallTo(() => _sonarrClient.IsHealthyAsync(A<CancellationToken>._))
            .Returns(false);
        A.CallTo(() => _radarrClient.IsHealthyAsync(A<CancellationToken>._))
            .Returns(true);

        var validator = CreateValidator();

        // Act
        var act = () => validator.ValidateAsync();

        // Assert - Should not throw, but logs warning
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ValidateAsync_RadarrFails_DoesNotThrow()
    {
        // Arrange
        A.CallTo(() => _remoteStorageService.TestConnectionAsync(A<CancellationToken>._))
            .Returns(new RemoteStorageConnectionResult(true));
        A.CallTo(() => _qBittorrentClient.TestConnectionAsync(A<CancellationToken>._))
            .Returns(new ConnectionTestResult(true, Version: "2.8.19"));
        A.CallTo(() => _sonarrClient.IsHealthyAsync(A<CancellationToken>._))
            .Returns(true);
        A.CallTo(() => _radarrClient.IsHealthyAsync(A<CancellationToken>._))
            .Returns(false);

        var validator = CreateValidator();

        // Act
        var act = () => validator.ValidateAsync();

        // Assert - Should not throw, but logs warning
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ValidateAsync_SonarrDisabled_SkipsValidation()
    {
        // Arrange
        A.CallTo(() => _remoteStorageService.TestConnectionAsync(A<CancellationToken>._))
            .Returns(new RemoteStorageConnectionResult(true));
        A.CallTo(() => _qBittorrentClient.TestConnectionAsync(A<CancellationToken>._))
            .Returns(new ConnectionTestResult(true, Version: "2.8.19"));
        A.CallTo(() => _radarrClient.IsHealthyAsync(A<CancellationToken>._))
            .Returns(true);

        var sonarrOptions = Options.Create(new SonarrOptions { Enabled = false });
        var validator = CreateValidator(sonarrOptions: sonarrOptions);

        // Act
        await validator.ValidateAsync();

        // Assert
        A.CallTo(() => _sonarrClient.IsHealthyAsync(A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task ValidateAsync_RadarrDisabled_SkipsValidation()
    {
        // Arrange
        A.CallTo(() => _remoteStorageService.TestConnectionAsync(A<CancellationToken>._))
            .Returns(new RemoteStorageConnectionResult(true));
        A.CallTo(() => _qBittorrentClient.TestConnectionAsync(A<CancellationToken>._))
            .Returns(new ConnectionTestResult(true, Version: "2.8.19"));
        A.CallTo(() => _sonarrClient.IsHealthyAsync(A<CancellationToken>._))
            .Returns(true);

        var radarrOptions = Options.Create(new RadarrOptions { Enabled = false });
        var validator = CreateValidator(radarrOptions: radarrOptions);

        // Act
        await validator.ValidateAsync();

        // Assert
        A.CallTo(() => _radarrClient.IsHealthyAsync(A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task ValidateAsync_JobWithEmptyName_DoesNotThrow()
    {
        // Arrange
        A.CallTo(() => _remoteStorageService.TestConnectionAsync(A<CancellationToken>._))
            .Returns(new RemoteStorageConnectionResult(true));
        A.CallTo(() => _qBittorrentClient.TestConnectionAsync(A<CancellationToken>._))
            .Returns(new ConnectionTestResult(true, Version: "2.8.19"));
        A.CallTo(() => _sonarrClient.IsHealthyAsync(A<CancellationToken>._))
            .Returns(true);
        A.CallTo(() => _radarrClient.IsHealthyAsync(A<CancellationToken>._))
            .Returns(true);

        var jobOptions = Options.Create(new JobOptions
        {
            Definitions =
            [
                new JobDefinition { Name = "", Enabled = true, StagingPath = _tempDir, RemotePath = "/downloads" }
            ]
        });
        var validator = CreateValidator(jobOptions: jobOptions);

        // Act - Should not throw, adds to warnings
        var act = () => validator.ValidateAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ValidateAsync_JobWithEmptyRemotePath_DoesNotThrow()
    {
        // Arrange
        A.CallTo(() => _remoteStorageService.TestConnectionAsync(A<CancellationToken>._))
            .Returns(new RemoteStorageConnectionResult(true));
        A.CallTo(() => _qBittorrentClient.TestConnectionAsync(A<CancellationToken>._))
            .Returns(new ConnectionTestResult(true, Version: "2.8.19"));
        A.CallTo(() => _sonarrClient.IsHealthyAsync(A<CancellationToken>._))
            .Returns(true);
        A.CallTo(() => _radarrClient.IsHealthyAsync(A<CancellationToken>._))
            .Returns(true);

        var jobOptions = Options.Create(new JobOptions
        {
            Definitions =
            [
                new JobDefinition { Name = "test-job", Enabled = true, RemotePath = "", StagingPath = _tempDir }
            ]
        });
        var validator = CreateValidator(jobOptions: jobOptions);

        // Act
        var act = () => validator.ValidateAsync();

        // Assert - Should not throw, adds to warnings
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ValidateAsync_StagingPathWritable_Succeeds()
    {
        // Arrange
        A.CallTo(() => _remoteStorageService.TestConnectionAsync(A<CancellationToken>._))
            .Returns(new RemoteStorageConnectionResult(true));
        A.CallTo(() => _qBittorrentClient.TestConnectionAsync(A<CancellationToken>._))
            .Returns(new ConnectionTestResult(true, Version: "2.8.19"));
        A.CallTo(() => _sonarrClient.IsHealthyAsync(A<CancellationToken>._))
            .Returns(true);
        A.CallTo(() => _radarrClient.IsHealthyAsync(A<CancellationToken>._))
            .Returns(true);

        var jobOptions = Options.Create(new JobOptions
        {
            Definitions =
            [
                new JobDefinition
                {
                    Name = "test-job",
                    Enabled = true,
                    RemotePath = "/downloads",
                    StagingPath = _tempDir
                }
            ]
        });
        var validator = CreateValidator(jobOptions: jobOptions);

        // Act
        var act = () => validator.ValidateAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ValidateAsync_StagingPathNotWritable_DoesNotThrow()
    {
        // Arrange
        A.CallTo(() => _remoteStorageService.TestConnectionAsync(A<CancellationToken>._))
            .Returns(new RemoteStorageConnectionResult(true));
        A.CallTo(() => _qBittorrentClient.TestConnectionAsync(A<CancellationToken>._))
            .Returns(new ConnectionTestResult(true, Version: "2.8.19"));
        A.CallTo(() => _sonarrClient.IsHealthyAsync(A<CancellationToken>._))
            .Returns(true);
        A.CallTo(() => _radarrClient.IsHealthyAsync(A<CancellationToken>._))
            .Returns(true);

        var jobOptions = Options.Create(new JobOptions
        {
            Definitions =
            [
                new JobDefinition
                {
                    Name = "test-job",
                    Enabled = true,
                    RemotePath = "/downloads",
                    StagingPath = "/nonexistent/path/that/cant/be/created/due/to/permissions"
                }
            ]
        });
        var validator = CreateValidator(jobOptions: jobOptions);

        // Act - Should not throw, adds to warnings
        var act = () => validator.ValidateAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ValidateAsync_DisabledJob_SkipsValidation()
    {
        // Arrange
        A.CallTo(() => _remoteStorageService.TestConnectionAsync(A<CancellationToken>._))
            .Returns(new RemoteStorageConnectionResult(true));
        A.CallTo(() => _qBittorrentClient.TestConnectionAsync(A<CancellationToken>._))
            .Returns(new ConnectionTestResult(true, Version: "2.8.19"));
        A.CallTo(() => _sonarrClient.IsHealthyAsync(A<CancellationToken>._))
            .Returns(true);
        A.CallTo(() => _radarrClient.IsHealthyAsync(A<CancellationToken>._))
            .Returns(true);

        var jobOptions = Options.Create(new JobOptions
        {
            Definitions =
            [
                new JobDefinition
                {
                    Name = "", // Invalid name but disabled
                    Enabled = false,
                    RemotePath = "",
                    StagingPath = ""
                }
            ]
        });
        var validator = CreateValidator(jobOptions: jobOptions);

        // Act
        var act = () => validator.ValidateAsync();

        // Assert - Should not report errors for disabled job
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ValidateAsync_MultipleJobs_ValidatesAll()
    {
        // Arrange
        A.CallTo(() => _remoteStorageService.TestConnectionAsync(A<CancellationToken>._))
            .Returns(new RemoteStorageConnectionResult(true));
        A.CallTo(() => _qBittorrentClient.TestConnectionAsync(A<CancellationToken>._))
            .Returns(new ConnectionTestResult(true, Version: "2.8.19"));
        A.CallTo(() => _sonarrClient.IsHealthyAsync(A<CancellationToken>._))
            .Returns(true);
        A.CallTo(() => _radarrClient.IsHealthyAsync(A<CancellationToken>._))
            .Returns(true);

        var stagingPath2 = Path.Combine(_tempDir, "job2");
        Directory.CreateDirectory(stagingPath2);

        var jobOptions = Options.Create(new JobOptions
        {
            Definitions =
            [
                new JobDefinition
                {
                    Name = "job1",
                    Enabled = true,
                    RemotePath = "/downloads/tv",
                    StagingPath = _tempDir
                },
                new JobDefinition
                {
                    Name = "job2",
                    Enabled = true,
                    RemotePath = "/downloads/movies",
                    StagingPath = stagingPath2
                }
            ]
        });
        var validator = CreateValidator(jobOptions: jobOptions);

        // Act
        var act = () => validator.ValidateAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }
}
