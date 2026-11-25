using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Harrbor.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveUsesHoldingPath : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Legacy rows (created before the holding dir) were transferred straight to the final staging path,
            // so they count as published. Without this, any still in flight would wait for a Publish that can't happen.
            migrationBuilder.Sql(
                "UPDATE TrackedReleases SET PublishedAtUtc = COALESCE(TransferCompletedAtUtc, CreatedAtUtc) " +
                "WHERE UsesHoldingPath = 0 AND PublishedAtUtc IS NULL;");

            migrationBuilder.DropColumn(
                name: "UsesHoldingPath",
                table: "TrackedReleases");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "UsesHoldingPath",
                table: "TrackedReleases",
                type: "INTEGER",
                nullable: false,
                defaultValue: true); // every row tracked since 0.0.8 uses the holding path
        }
    }
}
