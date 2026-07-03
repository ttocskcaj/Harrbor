using Harrbor.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Harrbor.Tests.Helpers;

/// <summary>
/// IDbContextFactory over a single in-memory SQLite database. Unlike the EF InMemory
/// provider, SQLite enforces unique indexes and relational constraints. The in-memory
/// database lives as long as the held-open connection.
/// </summary>
public sealed class SqliteDbContextFactory : IDbContextFactory<HarrborDbContext>, IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<HarrborDbContext> _options;

    public SqliteDbContextFactory()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<HarrborDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var context = new HarrborDbContext(_options);
        context.Database.EnsureCreated();
    }

    public HarrborDbContext CreateDbContext() => new(_options);

    public void Dispose() => _connection.Dispose();
}
