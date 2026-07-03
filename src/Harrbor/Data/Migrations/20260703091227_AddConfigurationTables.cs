using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Harrbor.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddConfigurationTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Jobs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    ServiceType = table.Column<int>(type: "INTEGER", nullable: false),
                    PollingInterval = table.Column<TimeSpan>(type: "TEXT", nullable: false),
                    RemotePath = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    StagingPath = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    QBittorrentCategory = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    QBittorrentTags = table.Column<string>(type: "TEXT", nullable: false),
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
                });

            migrationBuilder.CreateTable(
                name: "ServiceConnections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Type = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    SettingsJson = table.Column<string>(type: "TEXT", nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceConnections", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_Name",
                table: "Jobs",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ServiceConnections_Type",
                table: "ServiceConnections",
                column: "Type",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Jobs");

            migrationBuilder.DropTable(
                name: "ServiceConnections");
        }
    }
}
