using FluentAssertions;
using Harrbor.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace Harrbor.Tests.Data;

/// <summary>
/// Runs the real migrations on SQLite: seeds rows at the 0.0.8 schema, then migrates to the latest.
/// </summary>
public class MigrationTests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly HarrborDbContext _db;

    public MigrationTests()
    {
        _connection.Open();
        _db = new HarrborDbContext(new DbContextOptionsBuilder<HarrborDbContext>().UseSqlite(_connection).Options);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private void MigrateTo(string migration) => _db.GetService<IMigrator>().Migrate(migration);

    private void Insert(string downloadId, int transferStatus, bool usesHoldingPath, string? transferCompletedAt, string? publishedAt,
        string remotePath = "/downloads/radarr/Dune/Dune.mkv")
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO TrackedReleases
                (DownloadId, Name, JobName, RemotePath, StagingPath, DownloadStatus, TransferStatus, ExtractionStatus,
                 ConversionStatus, UsesHoldingPath, ImportStatus, CleanupStatus, ArchivalStatus, ErrorCount, CreatedAtUtc,
                 FilesConverted, TransferCompletedAtUtc, PublishedAtUtc)
            VALUES ($id, $id, 'movies', $remote, '/staging', 1, $transfer, 2, 4, $holding, 0, 0, 0, 0, '2026-01-01 00:00:00',
                    0, $transferAt, $publishedAt)
            """;
        cmd.Parameters.AddWithValue("$id", downloadId);
        cmd.Parameters.AddWithValue("$remote", remotePath);
        cmd.Parameters.AddWithValue("$transfer", transferStatus);
        cmd.Parameters.AddWithValue("$holding", usesHoldingPath ? 1 : 0);
        cmd.Parameters.AddWithValue("$transferAt", (object?)transferCompletedAt ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$publishedAt", (object?)publishedAt ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    [Fact]
    public async Task Upgrade_LegacyRowsArePublished_TransferredRowsKeepTheirItem()
    {
        MigrateTo("AddConversionAndPublish");
        Insert("LEGACY-TRANSFERRED", transferStatus: 2, usesHoldingPath: false, "2026-02-01 00:00:00", null);
        Insert("LEGACY-PENDING", transferStatus: 0, usesHoldingPath: false, null, null);
        Insert("HOLDING-UNPUBLISHED", transferStatus: 0, usesHoldingPath: true, null, null);
        Insert("HOLDING-PUBLISHED", transferStatus: 2, usesHoldingPath: true, "2026-02-01 00:00:00", "2026-03-01 00:00:00");

        await _db.Database.MigrateAsync(TestContext.Current.CancellationToken);

        var rows = await _db.TrackedReleases.AsNoTracking()
            .ToDictionaryAsync(r => r.DownloadId, TestContext.Current.CancellationToken);

        rows["LEGACY-TRANSFERRED"].PublishedAtUtc.Should().Be(new DateTime(2026, 2, 1));
        rows["LEGACY-PENDING"].PublishedAtUtc.Should().Be(new DateTime(2026, 1, 1), "falls back to CreatedAtUtc");
        rows["HOLDING-UNPUBLISHED"].PublishedAtUtc.Should().BeNull();
        rows["HOLDING-PUBLISHED"].PublishedAtUtc.Should().Be(new DateTime(2026, 3, 1));

        rows["LEGACY-TRANSFERRED"].RemoteItemPath.Should().Be("/downloads/radarr/Dune/Dune.mkv", "already-staged layout is kept");
        rows["HOLDING-PUBLISHED"].RemoteItemPath.Should().Be("/downloads/radarr/Dune/Dune.mkv");
        rows["HOLDING-UNPUBLISHED"].RemoteItemPath.Should().BeNull("filled in at startup from the job's remote root");
    }

    [Fact]
    public async Task Upgrade_ThenBackfill_CorrectsItemOfRowsNotYetTransferred()
    {
        MigrateTo("AddConversionAndPublish");
        Insert("PENDING", transferStatus: 0, usesHoldingPath: true, null, null);
        await _db.Database.MigrateAsync(TestContext.Current.CancellationToken);

        await RemoteItemPathBackfill.ApplyAsync(
            _db, [new Harrbor.Configuration.JobDefinition { Name = "movies", RemotePath = "/downloads/radarr" }],
            TestContext.Current.CancellationToken);

        (await _db.TrackedReleases.SingleAsync(TestContext.Current.CancellationToken))
            .RemoteItemPath.Should().Be("/downloads/radarr/Dune");
    }

    [Fact]
    public void Model_HasNoPendingChanges()
    {
        _db.Database.HasPendingModelChanges().Should().BeFalse();
    }
}
