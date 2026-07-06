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
    public DbSet<Connection> Connections => Set<Connection>();
    public DbSet<Job> Jobs => Set<Job>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<TrackedRelease>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.DownloadId).IsUnique();
            entity.HasIndex(e => e.DownloadStatus);
            entity.HasIndex(e => e.TransferStatus);
            entity.HasIndex(e => e.ExtractionStatus);
            entity.HasIndex(e => e.ImportStatus);
            entity.HasIndex(e => e.JobName);
            entity.HasIndex(e => e.Outcome);

            entity.Property(e => e.DownloadId).HasMaxLength(40).IsRequired();
            entity.Property(e => e.Name).HasMaxLength(500).IsRequired();
            entity.Property(e => e.JobName).HasMaxLength(100).IsRequired();
            entity.Property(e => e.RemotePath).HasMaxLength(1000).IsRequired();
            entity.Property(e => e.StagingPath).HasMaxLength(1000).IsRequired();
            entity.Property(e => e.LastError).HasMaxLength(2000);
        });

        modelBuilder.Entity<Connection>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ProviderKey);
            entity.HasIndex(e => new { e.ProviderKey, e.Name }).IsUnique();

            entity.Property(e => e.ProviderKey).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
            entity.Property(e => e.SettingsJson).IsRequired();
        });

        modelBuilder.Entity<Job>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Name).IsUnique();

            entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
            entity.Property(e => e.RemotePath).HasMaxLength(1000).IsRequired();
            entity.Property(e => e.StagingPath).HasMaxLength(1000).IsRequired();
            entity.Property(e => e.Category).HasMaxLength(100).IsRequired();
            entity.Property(e => e.CompletedCategory).HasMaxLength(100);

            // A job references one connection per category. Deletes are blocked while referenced
            // (surfaced as a 409 by the API; RESTRICT is the DB-level backstop).
            entity.HasOne<Connection>()
                .WithMany()
                .HasForeignKey(e => e.MediaServiceConnectionId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Connection>()
                .WithMany()
                .HasForeignKey(e => e.DownloadClientConnectionId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Connection>()
                .WithMany()
                .HasForeignKey(e => e.RemoteStorageConnectionId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
