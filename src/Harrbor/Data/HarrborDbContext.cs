using Harrbor.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Harrbor.Data;

public class HarrborDbContext : DbContext
{
    public HarrborDbContext(DbContextOptions<HarrborDbContext> options)
        : base(options)
    {
    }

    public DbSet<TrackedRelease> TrackedReleases => Set<TrackedRelease>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<TrackedRelease>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.DownloadId).IsUnique();
            entity.HasIndex(e => e.DownloadStatus);
            entity.HasIndex(e => e.TransferStatus);
            entity.HasIndex(e => e.ImportStatus);
            entity.HasIndex(e => e.JobName);

            entity.Property(e => e.DownloadId).HasMaxLength(40).IsRequired();
            entity.Property(e => e.Name).HasMaxLength(500).IsRequired();
            entity.Property(e => e.JobName).HasMaxLength(100).IsRequired();
            entity.Property(e => e.RemotePath).HasMaxLength(1000).IsRequired();
            entity.Property(e => e.StagingPath).HasMaxLength(1000).IsRequired();
            entity.Property(e => e.LastError).HasMaxLength(2000);
        });
    }
}
