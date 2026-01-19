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
                name: "IX_TrackedReleases_TransferStatus",
                table: "TrackedReleases",
                column: "TransferStatus");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TrackedReleases");
        }
    }
}
