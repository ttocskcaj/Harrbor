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

    public async Task<Connection?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Connections
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Connection>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Connections
            .AsNoTracking()
            .OrderBy(c => c.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<Connection> CreateAsync(Connection connection, CancellationToken cancellationToken = default)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        connection.Version = 1;
        connection.UpdatedAtUtc = DateTime.UtcNow;
        context.Connections.Add(connection);
        await context.SaveChangesAsync(cancellationToken);
        return connection;
    }

    public async Task<Connection?> UpdateAsync(int id, string name, bool enabled, string settingsJson, CancellationToken cancellationToken = default)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var row = await context.Connections.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (row is null)
            return null;

        row.Name = name;
        row.Enabled = enabled;
        row.SettingsJson = settingsJson;
        row.Version++;
        row.UpdatedAtUtc = DateTime.UtcNow;

        await context.SaveChangesAsync(cancellationToken);
        return row;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var row = await context.Connections.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (row is null)
            return false;

        context.Connections.Remove(row);
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
