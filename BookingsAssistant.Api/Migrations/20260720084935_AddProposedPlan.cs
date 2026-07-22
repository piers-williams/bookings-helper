using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BookingsAssistant.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddProposedPlan : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProposedPlans",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    SourceEmailText = table.Column<string>(type: "TEXT", nullable: true),
                    OsmBookingId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    ActionsJson = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProposedPlans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProposedPlans_OsmBookings_OsmBookingId",
                        column: x => x.OsmBookingId,
                        principalTable: "OsmBookings",
                        principalColumn: "OsmBookingId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProposedPlans_OsmBookingId",
                table: "ProposedPlans",
                column: "OsmBookingId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProposedPlans");
        }
    }
}
