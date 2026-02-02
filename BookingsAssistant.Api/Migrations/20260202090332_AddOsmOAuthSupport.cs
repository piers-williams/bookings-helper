using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BookingsAssistant.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddOsmOAuthSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "OsmApiToken",
                table: "ApplicationUsers",
                newName: "OsmRefreshToken");

            migrationBuilder.AddColumn<string>(
                name: "OsmAccessToken",
                table: "ApplicationUsers",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OsmAccessToken",
                table: "ApplicationUsers");

            migrationBuilder.RenameColumn(
                name: "OsmRefreshToken",
                table: "ApplicationUsers",
                newName: "OsmApiToken");
        }
    }
}
