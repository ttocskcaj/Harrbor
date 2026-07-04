using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Harrbor.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddReleaseOutcome : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Outcome",
                table: "TrackedReleases",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            // Backfill Outcome for existing rows. Enum values (see TrackedRelease.cs):
            //   PipelineOutcome: Active=0, Completed=1, Failed=2, Cancelled=3
            //   ArchivalStatus/ImportStatus/CleanupStatus: Pending=0, Completed=1, Failed=2
            //   TransferStatus: Pending=0, InProgress=1, Completed=2, Failed=3
            // Everything not matched below keeps the default Active (0).

            // Reached the end of the pipeline -> Completed.
            migrationBuilder.Sql(
                "UPDATE TrackedReleases SET Outcome = 1 WHERE ArchivalStatus = 1;");

            // A terminal phase failure (import/cleanup/archival) -> Failed.
            migrationBuilder.Sql(
                "UPDATE TrackedReleases SET Outcome = 2 " +
                "WHERE Outcome = 0 AND (ImportStatus = 2 OR CleanupStatus = 2 OR ArchivalStatus = 2);");

            // Transfers whose retries are exhausted are permanently failed. The retry limit
            // lives on the owning job (matched by name); orphaned releases stay Active.
            migrationBuilder.Sql(
                "UPDATE TrackedReleases SET Outcome = 2 " +
                "WHERE Outcome = 0 AND TransferStatus = 3 " +
                "AND ErrorCount >= (SELECT j.MaxTransferRetries FROM Jobs j WHERE j.Name = TrackedReleases.JobName);");

            migrationBuilder.CreateIndex(
                name: "IX_TrackedReleases_Outcome",
                table: "TrackedReleases",
                column: "Outcome");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TrackedReleases_Outcome",
                table: "TrackedReleases");

            migrationBuilder.DropColumn(
                name: "Outcome",
                table: "TrackedReleases");
        }
    }
}
