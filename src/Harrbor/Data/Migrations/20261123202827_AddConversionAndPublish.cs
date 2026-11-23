using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Harrbor.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddConversionAndPublish : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ConversionCompletedAtUtc",
                table: "TrackedReleases",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ConversionStartedAtUtc",
                table: "TrackedReleases",
                type: "TEXT",
                nullable: true);

            // Existing rows default to Skipped (4) so releases tracked before this phase existed are
            // never converted. New rows are inserted with the CLR default (Pending) by EF.
            migrationBuilder.AddColumn<int>(
                name: "ConversionStatus",
                table: "TrackedReleases",
                type: "INTEGER",
                nullable: false,
                defaultValue: 4);

            migrationBuilder.AddColumn<string>(
                name: "ConversionSummary",
                table: "TrackedReleases",
                type: "TEXT",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FilesConverted",
                table: "TrackedReleases",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "PublishedAtUtc",
                table: "TrackedReleases",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "UsesHoldingPath",
                table: "TrackedReleases",
                type: "INTEGER",
                nullable: false,
                defaultValue: false); // legacy rows keep writing straight to the final staging path

            migrationBuilder.CreateIndex(
                name: "IX_TrackedReleases_ConversionStatus",
                table: "TrackedReleases",
                column: "ConversionStatus");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TrackedReleases_ConversionStatus",
                table: "TrackedReleases");

            migrationBuilder.DropColumn(
                name: "ConversionCompletedAtUtc",
                table: "TrackedReleases");

            migrationBuilder.DropColumn(
                name: "ConversionStartedAtUtc",
                table: "TrackedReleases");

            migrationBuilder.DropColumn(
                name: "ConversionStatus",
                table: "TrackedReleases");

            migrationBuilder.DropColumn(
                name: "ConversionSummary",
                table: "TrackedReleases");

            migrationBuilder.DropColumn(
                name: "FilesConverted",
                table: "TrackedReleases");

            migrationBuilder.DropColumn(
                name: "PublishedAtUtc",
                table: "TrackedReleases");

            migrationBuilder.DropColumn(
                name: "UsesHoldingPath",
                table: "TrackedReleases");
        }
    }
}
