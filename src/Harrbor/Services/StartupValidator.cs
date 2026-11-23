using Microsoft.Extensions.Options;
using Harrbor.Configuration;
using Harrbor.Services.Clients;
using Harrbor.Services.Conversion;
using Harrbor.Services.RemoteStorage;

namespace Harrbor.Services;

public class StartupValidator
{
    private readonly IQBittorrentClient _qBittorrentClient;
    private readonly ISonarrClient _sonarrClient;
    private readonly IRadarrClient _radarrClient;
    private readonly IRemoteStorageService _remoteStorageService;
    private readonly IOptions<SonarrOptions> _sonarrOptions;
    private readonly IOptions<RadarrOptions> _radarrOptions;
    private readonly IOptions<JobOptions> _jobOptions;
    private readonly ConverterRegistry _converterRegistry;
    private readonly ToolChecker _toolChecker;
    private readonly ILogger<StartupValidator> _logger;

    public StartupValidator(
        IQBittorrentClient qBittorrentClient,
        ISonarrClient sonarrClient,
        IRadarrClient radarrClient,
        IRemoteStorageService remoteStorageService,
        IOptions<SonarrOptions> sonarrOptions,
        IOptions<RadarrOptions> radarrOptions,
        IOptions<JobOptions> jobOptions,
        ConverterRegistry converterRegistry,
        ToolChecker toolChecker,
        ILogger<StartupValidator> logger)
    {
        _qBittorrentClient = qBittorrentClient;
        _sonarrClient = sonarrClient;
        _radarrClient = radarrClient;
        _remoteStorageService = remoteStorageService;
        _sonarrOptions = sonarrOptions;
        _radarrOptions = radarrOptions;
        _jobOptions = jobOptions;
        _converterRegistry = converterRegistry;
        _toolChecker = toolChecker;
        _logger = logger;
    }

    public async Task ValidateAsync(CancellationToken cancellationToken = default)
    {
        // Converter config is checked first and is fatal: a typo would otherwise silently skip conversion.
        await ValidateConvertersAsync(cancellationToken);

        _logger.LogInformation("Validating external service connections...");

        var errors = new List<string>();

        // Validate rclone/SFTP connectivity (blocking - required for operation)
        var rcloneResult = await _remoteStorageService.TestConnectionAsync(cancellationToken);
        if (rcloneResult.Success)
        {
            _logger.LogInformation("Remote storage (rclone/SFTP) connection successful");
        }
        else
        {
            _logger.LogError("Remote storage connection failed: {Error}", rcloneResult.Error);
            throw new InvalidOperationException($"Remote storage (rclone/SFTP) connection failed: {rcloneResult.Error}");
        }

        // Validate qBittorrent
        var qbtResult = await _qBittorrentClient.TestConnectionAsync(cancellationToken);
        if (qbtResult.Success)
        {
            _logger.LogInformation("qBittorrent connection successful (version: {Version})", qbtResult.Version);
        }
        else
        {
            errors.Add($"qBittorrent: {qbtResult.Error}");
            _logger.LogError("qBittorrent connection failed: {Error}", qbtResult.Error);
        }

        // Validate Sonarr if enabled
        if (_sonarrOptions.Value.Enabled)
        {
            var sonarrHealthy = await _sonarrClient.IsHealthyAsync(cancellationToken);
            if (sonarrHealthy)
            {
                _logger.LogInformation("Sonarr connection successful");
            }
            else
            {
                errors.Add("Sonarr: Connection failed");
                _logger.LogError("Sonarr connection failed");
            }
        }
        else
        {
            _logger.LogInformation("Sonarr is disabled, skipping validation");
        }

        // Validate Radarr if enabled
        if (_radarrOptions.Value.Enabled)
        {
            var radarrHealthy = await _radarrClient.IsHealthyAsync(cancellationToken);
            if (radarrHealthy)
            {
                _logger.LogInformation("Radarr connection successful");
            }
            else
            {
                errors.Add("Radarr: Connection failed");
                _logger.LogError("Radarr connection failed");
            }
        }
        else
        {
            _logger.LogInformation("Radarr is disabled, skipping validation");
        }

        // Validate job configuration
        var enabledJobs = _jobOptions.Value.Definitions.Where(j => j.Enabled).ToList();
        foreach (var job in enabledJobs)
        {
            if (string.IsNullOrEmpty(job.Name))
            {
                errors.Add("Job configuration: Job name is required");
            }

            if (string.IsNullOrEmpty(job.RemotePath))
            {
                errors.Add($"Job '{job.Name}': RemotePath is required");
            }

            if (string.IsNullOrEmpty(job.StagingPath))
            {
                errors.Add($"Job '{job.Name}': StagingPath is required");
            }

            // Verify staging path is writable
            if (!string.IsNullOrEmpty(job.StagingPath))
            {
                try
                {
                    Directory.CreateDirectory(job.StagingPath);
                    var testFile = Path.Combine(job.StagingPath, ".harrbor-write-test");
                    await File.WriteAllTextAsync(testFile, "test", cancellationToken);
                    File.Delete(testFile);
                    _logger.LogDebug("Job '{JobName}': Staging path is writable: {Path}", job.Name, job.StagingPath);
                }
                catch (Exception ex)
                {
                    errors.Add($"Job '{job.Name}': Staging path is not writable: {ex.Message}");
                    _logger.LogError(ex, "Job '{JobName}': Staging path is not writable: {Path}", job.Name, job.StagingPath);
                }
            }
        }

        if (errors.Count > 0)
        {
            _logger.LogWarning(
                "Startup validation completed with {ErrorCount} warning(s). Service will continue but may encounter issues.",
                errors.Count);
        }
        else
        {
            _logger.LogInformation("Startup validation completed successfully");
        }
    }

    private async Task ValidateConvertersAsync(CancellationToken cancellationToken)
    {
        var jobs = _jobOptions.Value.Definitions.Where(j => j.Enabled && j.Converters.Count > 0).ToList();
        if (jobs.Count == 0)
            return;

        var errors = new List<string>();
        var converters = new List<IMediaConverter>();
        foreach (var job in jobs)
        {
            foreach (var name in job.Converters)
            {
                if (_converterRegistry.TryGet(name, out var converter))
                    converters.Add(converter);
                else
                    errors.Add($"Job '{job.Name}': unknown converter '{name}'");
            }
        }

        if (errors.Count > 0)
        {
            var available = _converterRegistry.Names.Count == 0 ? "(none)" : string.Join(", ", _converterRegistry.Names);
            throw new InvalidOperationException(
                $"Invalid converter configuration: {string.Join("; ", errors)}. Available converters: {available}");
        }

        var tools = converters.SelectMany(c => c.RequiredTools).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var problems = await _toolChecker.FindProblemsAsync(tools, cancellationToken);
        if (problems.Count > 0)
        {
            throw new InvalidOperationException(
                $"Media conversion tools are missing or not runnable: {string.Join("; ", problems)}. " +
                "Install them or set their paths under Conversion:Tools.");
        }

        _logger.LogInformation(
            "Media converters validated ({Converters}); tools available: {Tools}",
            string.Join(", ", converters.Select(c => c.Name).Distinct()),
            string.Join(", ", tools));
    }
}
