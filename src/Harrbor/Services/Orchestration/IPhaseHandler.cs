using Harrbor.Configuration;
using Harrbor.Data;

namespace Harrbor.Services.Orchestration;

/// <summary>
/// Base interface for all orchestration phase handlers.
/// </summary>
public interface IPhaseHandler
{
    Task ExecuteAsync(JobDefinition job, HarrborDbContext dbContext, CancellationToken cancellationToken);
}
