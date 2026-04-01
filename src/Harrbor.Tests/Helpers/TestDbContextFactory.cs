using Harrbor.Data;
using Microsoft.EntityFrameworkCore;

namespace Harrbor.Tests.Helpers;

public static class TestDbContextFactory
{
    public static HarrborDbContext Create(string? databaseName = null)
    {
        var options = new DbContextOptionsBuilder<HarrborDbContext>()
            .UseInMemoryDatabase(databaseName ?? Guid.NewGuid().ToString())
            .Options;

        var context = new HarrborDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    public static HarrborDbContext CreateWithSeed(Action<HarrborDbContext> seedAction, string? databaseName = null)
    {
        var context = Create(databaseName);
        seedAction(context);
        context.SaveChanges();
        return context;
    }
}
