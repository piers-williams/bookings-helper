using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BookingsAssistant.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddGateCodeSentAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "GateCodeSentAt",
                table: "OsmBookings",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GateCodeSentAt",
                table: "OsmBookings");
        }
    }
}
