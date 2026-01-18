using Harrbor.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Harrbor.Data;

public class HarrborDbContext : DbContext
{
    public HarrborDbContext(DbContextOptions<HarrborDbContext> options)
        : base(options)
    {
    }

    public DbSet<TrackedTorrent> TrackedTorrents => Set<TrackedTorrent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<TrackedTorrent>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.InfoHash).IsUnique();
            entity.HasIndex(e => e.TransferStatus);
            entity.HasIndex(e => e.Category);

            entity.Property(e => e.InfoHash).HasMaxLength(40).IsRequired();
            entity.Property(e => e.Name).HasMaxLength(500).IsRequired();
            entity.Property(e => e.LastError).HasMaxLength(2000);
        });
    }
}
