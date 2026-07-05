using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Harrbor.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddExtractionPhase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ExtractionCompletedAtUtc",
                table: "TrackedReleases",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExtractionStartedAtUtc",
                table: "TrackedReleases",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ExtractionStatus",
                table: "TrackedReleases",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_TrackedReleases_ExtractionStatus",
                table: "TrackedReleases",
                column: "ExtractionStatus");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TrackedReleases_ExtractionStatus",
                table: "TrackedReleases");

            migrationBuilder.DropColumn(
                name: "ExtractionCompletedAtUtc",
                table: "TrackedReleases");

            migrationBuilder.DropColumn(
                name: "ExtractionStartedAtUtc",
                table: "TrackedReleases");

            migrationBuilder.DropColumn(
                name: "ExtractionStatus",
                table: "TrackedReleases");
        }
    }
}
