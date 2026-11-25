using Harrbor.Data;
using Harrbor.Services.Orchestration;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Harrbor.Tests.Helpers;

/// <summary>
/// A DI container for phase handlers that run work in the background: each background task gets its own scope and
/// <see cref="HarrborDbContext"/>, on the same database as the test's own context from <see cref="CreateContext"/>.
/// </summary>
public sealed class BackgroundTestServices : IDisposable
{
    private readonly ServiceProvider _provider;
    private readonly Action<DbContextOptionsBuilder> _configure;
    private readonly string? _sqliteFile;

    private BackgroundTestServices(Action<DbContextOptionsBuilder> configure, string? sqliteFile)
    {
        _configure = configure;
        _sqliteFile = sqliteFile;
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<HarrborDbContext>(configure);
        services.AddSingleton<TransferCoordinator>();
        services.AddSingleton<ConversionCoordinator>();
        _provider = services.BuildServiceProvider();
    }

    /// <summary>EF in-memory database (fast; async calls complete synchronously).</summary>
    public static BackgroundTestServices InMemory()
    {
        var name = Guid.NewGuid().ToString();
        return new BackgroundTestServices(o => o.UseInMemoryDatabase(name), sqliteFile: null);
    }

    /// <summary>
    /// A real SQLite file: async calls really are asynchronous, so sharing one context between tasks trips EF's
    /// concurrency detector, as in production.
    /// </summary>
    public static BackgroundTestServices Sqlite()
    {
        var file = Path.Combine(Path.GetTempPath(), $"harrbor-test-{Guid.NewGuid():N}.sqlite");
        var services = new BackgroundTestServices(o => o.UseSqlite($"Data Source={file};Pooling=False"), file);
        using var db = services.CreateContext();
        db.Database.EnsureCreated();
        return services;
    }

    public TransferCoordinator Transfers => _provider.GetRequiredService<TransferCoordinator>();

    public ConversionCoordinator Conversions => _provider.GetRequiredService<ConversionCoordinator>();

    public HarrborDbContext CreateContext()
    {
        var builder = new DbContextOptionsBuilder<HarrborDbContext>();
        _configure(builder);
        var context = new HarrborDbContext(builder.Options);
        context.Database.EnsureCreated();
        return context;
    }

    public void Dispose()
    {
        Task.WhenAll(Transfers.WhenIdleAsync(), Conversions.WhenIdleAsync()).Wait(TimeSpan.FromSeconds(10));
        _provider.Dispose();
        if (_sqliteFile != null)
        {
            SqliteConnection.ClearAllPools();
            File.Delete(_sqliteFile);
        }
    }
}
