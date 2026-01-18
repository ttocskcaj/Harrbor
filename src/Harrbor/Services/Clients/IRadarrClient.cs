namespace Harrbor.Services.Clients;

public interface IRadarrClient
{
    Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default);
    Task TriggerManualImportAsync(string path, CancellationToken cancellationToken = default);
}
