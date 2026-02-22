using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BookingsAssistant.Api.Migrations
{
    /// <inheritdoc />
    public partial class RemoveOffice365Fields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Office365AccessToken",
                table: "ApplicationUsers");

            migrationBuilder.DropColumn(
                name: "Office365Email",
                table: "ApplicationUsers");

            migrationBuilder.DropColumn(
                name: "Office365RefreshToken",
                table: "ApplicationUsers");

            migrationBuilder.DropColumn(
                name: "Office365TokenExpiry",
                table: "ApplicationUsers");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Office365AccessToken",
                table: "ApplicationUsers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Office365Email",
                table: "ApplicationUsers",
                type: "TEXT",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Office365RefreshToken",
                table: "ApplicationUsers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "Office365TokenExpiry",
                table: "ApplicationUsers",
                type: "TEXT",
                nullable: true);
        }
    }
}
