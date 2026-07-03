using System.Text.Json;
using Harrbor.Configuration.Connections;
using Harrbor.Data;
using Harrbor.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Harrbor.Services.Configuration;

public class ConnectionStore : IConnectionStore
{
    private readonly IDbContextFactory<HarrborDbContext> _dbContextFactory;

    public ConnectionStore(IDbContextFactory<HarrborDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<StampedSettings<T>?> GetAsync<T>(CancellationToken cancellationToken = default)
        where T : class, IConnectionSettings
    {
        // Static abstract members can't appear inside EF expression trees
        var connectionType = T.ConnectionType;

        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var row = await context.ServiceConnections
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Type == connectionType, cancellationToken);

        if (row is null)
            return null;

        var settings = JsonSerializer.Deserialize<T>(row.SettingsJson);
        if (settings is null)
            return null;

        return new StampedSettings<T>(settings, row.Version, row.UpdatedAtUtc);
    }

    public async Task<StampedSettings<T>> SaveAsync<T>(T settings, CancellationToken cancellationToken = default)
        where T : class, IConnectionSettings
    {
        var connectionType = T.ConnectionType;

        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var row = await context.ServiceConnections
            .FirstOrDefaultAsync(c => c.Type == connectionType, cancellationToken);

        if (row is null)
        {
            row = new ServiceConnection { Type = connectionType };
            context.ServiceConnections.Add(row);
        }

        row.SettingsJson = JsonSerializer.Serialize(settings);
        row.Version++;
        row.UpdatedAtUtc = DateTime.UtcNow;

        await context.SaveChangesAsync(cancellationToken);
        return new StampedSettings<T>(settings, row.Version, row.UpdatedAtUtc);
    }

    public async Task<bool> IsConfiguredAsync(string connectionType, CancellationToken cancellationToken = default)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await context.ServiceConnections
            .AnyAsync(c => c.Type == connectionType, cancellationToken);
    }
}
