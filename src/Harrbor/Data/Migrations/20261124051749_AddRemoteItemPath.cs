using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Harrbor.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRemoteItemPath : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RemoteItemPath",
                table: "TrackedReleases",
                type: "TEXT",
                maxLength: 1000,
                nullable: true);

            // Already-transferred rows keep their staged layout: their item stays named after RemotePath, as before.
            // Nothing is moved on disk. Other rows that finished downloading are filled in at startup from the job's
            // remote root (RemoteItemPathBackfill), and rows still downloading get it when the download completes.
            migrationBuilder.Sql(
                "UPDATE TrackedReleases SET RemoteItemPath = RemotePath WHERE TransferStatus = 2;"); // Completed
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RemoteItemPath",
                table: "TrackedReleases");
        }
    }
}
