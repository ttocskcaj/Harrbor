using Harrbor.Configuration;
using Harrbor.Services.Clients;

namespace Harrbor.Services;

public interface IMediaServiceResolver
{
    IMediaService GetServiceForJob(JobDefinition job);
}

public class MediaServiceResolver : IMediaServiceResolver
{
    private readonly ISonarrClient _sonarrClient;
    private readonly IRadarrClient _radarrClient;

    public MediaServiceResolver(
        ISonarrClient sonarrClient,
        IRadarrClient radarrClient)
    {
        _sonarrClient = sonarrClient;
        _radarrClient = radarrClient;
    }

    public IMediaService GetServiceForJob(JobDefinition job)
    {
        return job.ServiceType switch
        {
            ServiceType.Sonarr => _sonarrClient,
            ServiceType.Radarr => _radarrClient,
            _ => throw new ArgumentException($"Unknown service type: {job.ServiceType}")
        };
    }
}
