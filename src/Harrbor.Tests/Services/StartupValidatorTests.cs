using FakeItEasy;
using FluentAssertions;
using Harrbor.Configuration;
using Harrbor.Services;
using Harrbor.Services.Clients;
using Harrbor.Services.Conversion;
using Harrbor.Services.Processes;
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
    private readonly IProcessRunner _processRunner;
    private readonly IMediaConverter _converter;
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
        _processRunner = A.Fake<IProcessRunner>();
        A.CallTo(() => _processRunner.RunAsync(A<ProcessSpec>._, A<CancellationToken>._))
            .ReturnsLazily((ProcessSpec spec, CancellationToken _) => new ProcessResult(spec.FileName, 0, "v1", ""));
        _converter = A.Fake<IMediaConverter>();
        A.CallTo(() => _converter.Name).Returns("FakeConverter");
        A.CallTo(() => _converter.RequiredTools).Returns(["ffmpeg", "dovi_tool"]);
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
        IOptions<JobOptions>? jobOptions = null,
        ConversionOptions? conversionOptions = null)
    {
        return new StartupValidator(
            _qBittorrentClient,
            _sonarrClient,
            _radarrClient,
            _remoteStorageService,
            sonarrOptions ?? _sonarrOptions,
            radarrOptions ?? _radarrOptions,
            jobOptions ?? _jobOptions,
            Options.Create(conversionOptions ?? new ConversionOptions()),
            new ConverterRegistry([_converter]),
            new ToolChecker(_processRunner, Options.Create(new ConversionOptions())),
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
        await validator.ValidateAsync(TestContext.Current.CancellationToken);

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
        await validator.ValidateAsync(TestContext.Current.CancellationToken);

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

    private IOptions<JobOptions> JobWithConverters(params string[] converters) =>
        Options.Create(new JobOptions
        {
            Definitions =
            [
                new JobDefinition
                {
                    Name = "movies",
                    RemotePath = "/downloads",
                    StagingPath = _tempDir,
                    Converters = [.. converters]
                }
            ]
        });

    [Fact]
    public async Task ValidateAsync_UnknownConverter_Throws()
    {
        var validator = CreateValidator(jobOptions: JobWithConverters("NoSuchConverter"));

        var act = () => validator.ValidateAsync(TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*unknown converter 'NoSuchConverter'*FakeConverter*");
    }

    [Fact]
    public async Task ValidateAsync_MissingTool_Throws()
    {
        A.CallTo(() => _processRunner.RunAsync(
                A<ProcessSpec>.That.Matches(s => s.FileName == "dovi_tool"), A<CancellationToken>._))
            .Throws(new System.ComponentModel.Win32Exception("No such file or directory"));
        var validator = CreateValidator(jobOptions: JobWithConverters("FakeConverter"));

        var act = () => validator.ValidateAsync(TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*dovi_tool*could not be run*");
    }

    [Fact]
    public async Task ValidateAsync_ToolExitsNonZero_Throws()
    {
        A.CallTo(() => _processRunner.RunAsync(
                A<ProcessSpec>.That.Matches(s => s.FileName == "ffmpeg"), A<CancellationToken>._))
            .Returns(new ProcessResult("ffmpeg", 127, "", "not found"));
        var validator = CreateValidator(jobOptions: JobWithConverters("FakeConverter"));

        var act = () => validator.ValidateAsync(TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*ffmpeg*exited with code 127*");
    }

    [Fact]
    public async Task ValidateAsync_KnownConverterWithTools_ChecksEachToolOnce()
    {
        A.CallTo(() => _remoteStorageService.TestConnectionAsync(A<CancellationToken>._))
            .Returns(new RemoteStorageConnectionResult(true));
        var validator = CreateValidator(jobOptions: JobWithConverters("FakeConverter", "fakeconverter"));

        await validator.ValidateAsync(TestContext.Current.CancellationToken);

        A.CallTo(() => _processRunner.RunAsync(
                A<ProcessSpec>.That.Matches(s => s.FileName == "ffmpeg" && s.Arguments.SequenceEqual(new[] { "-version" })),
                A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => _processRunner.RunAsync(
                A<ProcessSpec>.That.Matches(s => s.FileName == "dovi_tool" && s.Arguments.SequenceEqual(new[] { "--version" })),
                A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task ValidateAsync_NoConvertersConfigured_DoesNotCheckTools()
    {
        A.CallTo(() => _remoteStorageService.TestConnectionAsync(A<CancellationToken>._))
            .Returns(new RemoteStorageConnectionResult(true));
        var validator = CreateValidator(jobOptions: JobWithConverters());

        await validator.ValidateAsync(TestContext.Current.CancellationToken);

        A.CallTo(() => _processRunner.RunAsync(A<ProcessSpec>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    private int LoggedErrors() =>
        Fake.GetCalls(_logger).Count(c => c.Method.Name == "Log" && (LogLevel)c.Arguments[0]! == LogLevel.Error);

    [Fact]
    public async Task ValidateAsync_ScratchPathMissing_IsCreated()
    {
        A.CallTo(() => _remoteStorageService.TestConnectionAsync(A<CancellationToken>._))
            .Returns(new RemoteStorageConnectionResult(true));
        var root = Path.Combine(Path.GetTempPath(), $"harrbor-scratch-{Guid.NewGuid():N}");
        var scratch = Path.Combine(root, "nested");
        try
        {
            var validator = CreateValidator(jobOptions: JobWithConverters(), conversionOptions: new ConversionOptions { ScratchPath = scratch });

            await validator.ValidateAsync(TestContext.Current.CancellationToken);

            Directory.Exists(scratch).Should().BeTrue();
            Directory.EnumerateFileSystemEntries(scratch).Should().BeEmpty("the write test file is removed");
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ValidateAsync_ScratchPathNotCreatable_LogsErrorButContinues()
    {
        A.CallTo(() => _remoteStorageService.TestConnectionAsync(A<CancellationToken>._))
            .Returns(new RemoteStorageConnectionResult(true));
        var file = Path.GetTempFileName(); // a file can't be a directory's parent
        try
        {
            var validator = CreateValidator(jobOptions: JobWithConverters(),
                conversionOptions: new ConversionOptions { ScratchPath = Path.Combine(file, "scratch") });

            var act = () => validator.ValidateAsync(TestContext.Current.CancellationToken);

            await act.Should().NotThrowAsync();
            LoggedErrors().Should().BeGreaterThan(0);
        }
        finally
        {
            File.Delete(file);
        }
    }
}
