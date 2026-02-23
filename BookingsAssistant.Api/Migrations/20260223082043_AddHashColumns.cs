using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BookingsAssistant.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddHashColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OsmBookings_CustomerEmail",
                table: "OsmBookings");

            migrationBuilder.DropColumn(
                name: "CustomerEmail",
                table: "OsmBookings");

            migrationBuilder.AddColumn<string>(
                name: "CustomerEmailHash",
                table: "OsmBookings",
                type: "TEXT",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomerNameHash",
                table: "OsmBookings",
                type: "TEXT",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "SenderEmail",
                table: "EmailMessages",
                type: "TEXT",
                maxLength: 255,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 255);

            migrationBuilder.AddColumn<string>(
                name: "SenderEmailHash",
                table: "EmailMessages",
                type: "TEXT",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_OsmBookings_CustomerEmailHash",
                table: "OsmBookings",
                column: "CustomerEmailHash");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OsmBookings_CustomerEmailHash",
                table: "OsmBookings");

            migrationBuilder.DropColumn(
                name: "CustomerEmailHash",
                table: "OsmBookings");

            migrationBuilder.DropColumn(
                name: "CustomerNameHash",
                table: "OsmBookings");

            migrationBuilder.DropColumn(
                name: "SenderEmailHash",
                table: "EmailMessages");

            migrationBuilder.AddColumn<string>(
                name: "CustomerEmail",
                table: "OsmBookings",
                type: "TEXT",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "SenderEmail",
                table: "EmailMessages",
                type: "TEXT",
                maxLength: 255,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 255,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_OsmBookings_CustomerEmail",
                table: "OsmBookings",
                column: "CustomerEmail");
        }
    }
}
