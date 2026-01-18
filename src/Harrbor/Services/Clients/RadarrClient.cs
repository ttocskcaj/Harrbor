using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using Harrbor.Configuration;

namespace Harrbor.Services.Clients;

public class RadarrClient : IRadarrClient
{
    private readonly HttpClient _httpClient;
    private readonly RadarrOptions _options;
    private readonly ILogger<RadarrClient> _logger;

    public RadarrClient(
        HttpClient httpClient,
        IOptions<RadarrOptions> options,
        ILogger<RadarrClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;

        _httpClient.BaseAddress = new Uri(_options.BaseUrl);
        _httpClient.DefaultRequestHeaders.Add("X-Api-Key", _options.ApiKey);
    }

    public async Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/v3/system/status", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Radarr health check failed");
            return false;
        }
    }

    public async Task TriggerManualImportAsync(string path, CancellationToken cancellationToken = default)
    {
        var command = new
        {
            name = "ManualImport",
            path,
            importMode = "Auto"
        };

        var response = await _httpClient.PostAsJsonAsync("/api/v3/command", command, cancellationToken);
        response.EnsureSuccessStatusCode();

        _logger.LogInformation("Triggered manual import for path: {Path}", path);
    }
}
