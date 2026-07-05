using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Harrbor.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Connections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProviderKey = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    SettingsJson = table.Column<string>(type: "TEXT", nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Connections", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TrackedReleases",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DownloadId = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    JobName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    RemotePath = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    StagingPath = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    DownloadStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    TransferStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    ImportStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    CleanupStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    ArchivalStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    Outcome = table.Column<int>(type: "INTEGER", nullable: false),
                    ErrorCount = table.Column<int>(type: "INTEGER", nullable: false),
                    LastError = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    LastErrorAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DownloadCompletedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    TransferStartedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    TransferCompletedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ImportCompletedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CleanupCompletedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ArchivedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrackedReleases", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Jobs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    MediaServiceConnectionId = table.Column<int>(type: "INTEGER", nullable: false),
                    DownloadClientConnectionId = table.Column<int>(type: "INTEGER", nullable: false),
                    RemoteStorageConnectionId = table.Column<int>(type: "INTEGER", nullable: false),
                    PollingInterval = table.Column<TimeSpan>(type: "TEXT", nullable: false),
                    RemotePath = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    StagingPath = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    Category = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Tags = table.Column<string>(type: "TEXT", nullable: false),
                    TransferParallelism = table.Column<int>(type: "INTEGER", nullable: false),
                    MaxTransferRetries = table.Column<int>(type: "INTEGER", nullable: false),
                    TransferRetryDelay = table.Column<TimeSpan>(type: "TEXT", nullable: false),
                    CompletedCategory = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    ImportTimeout = table.Column<TimeSpan>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastRunAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Jobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Jobs_Connections_DownloadClientConnectionId",
                        column: x => x.DownloadClientConnectionId,
                        principalTable: "Connections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Jobs_Connections_MediaServiceConnectionId",
                        column: x => x.MediaServiceConnectionId,
                        principalTable: "Connections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Jobs_Connections_RemoteStorageConnectionId",
                        column: x => x.RemoteStorageConnectionId,
                        principalTable: "Connections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Connections_ProviderKey",
                table: "Connections",
                column: "ProviderKey");

            migrationBuilder.CreateIndex(
                name: "IX_Connections_ProviderKey_Name",
                table: "Connections",
                columns: new[] { "ProviderKey", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_DownloadClientConnectionId",
                table: "Jobs",
                column: "DownloadClientConnectionId");

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_MediaServiceConnectionId",
                table: "Jobs",
                column: "MediaServiceConnectionId");

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_Name",
                table: "Jobs",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_RemoteStorageConnectionId",
                table: "Jobs",
                column: "RemoteStorageConnectionId");

            migrationBuilder.CreateIndex(
                name: "IX_TrackedReleases_DownloadId",
                table: "TrackedReleases",
                column: "DownloadId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TrackedReleases_DownloadStatus",
                table: "TrackedReleases",
                column: "DownloadStatus");

            migrationBuilder.CreateIndex(
                name: "IX_TrackedReleases_ImportStatus",
                table: "TrackedReleases",
                column: "ImportStatus");

            migrationBuilder.CreateIndex(
                name: "IX_TrackedReleases_JobName",
                table: "TrackedReleases",
                column: "JobName");

            migrationBuilder.CreateIndex(
                name: "IX_TrackedReleases_Outcome",
                table: "TrackedReleases",
                column: "Outcome");

            migrationBuilder.CreateIndex(
                name: "IX_TrackedReleases_TransferStatus",
                table: "TrackedReleases",
                column: "TransferStatus");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Jobs");

            migrationBuilder.DropTable(
                name: "TrackedReleases");

            migrationBuilder.DropTable(
                name: "Connections");
        }
    }
}
