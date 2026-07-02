using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BookingsAssistant.Api.Migrations
{
    /// <inheritdoc />
    public partial class RemoveEmailFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApplicationLinks");

            migrationBuilder.DropTable(
                name: "EmailMessages");

            migrationBuilder.DropIndex(
                name: "IX_OsmBookings_CustomerEmailHash",
                table: "OsmBookings");

            migrationBuilder.DropColumn(
                name: "CustomerEmailHash",
                table: "OsmBookings");

            migrationBuilder.DropColumn(
                name: "CustomerNameHash",
                table: "OsmBookings");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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

            migrationBuilder.CreateTable(
                name: "EmailMessages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ExtractedBookingRef = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    IsRead = table.Column<bool>(type: "INTEGER", nullable: false),
                    LastFetched = table.Column<DateTime>(type: "TEXT", nullable: true),
                    MessageId = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    ReceivedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SenderEmailHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    SenderName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    Subject = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ApplicationLinks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CreatedByUserId = table.Column<int>(type: "INTEGER", nullable: true),
                    EmailMessageId = table.Column<int>(type: "INTEGER", nullable: false),
                    OsmBookingId = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApplicationLinks_ApplicationUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ApplicationLinks_EmailMessages_EmailMessageId",
                        column: x => x.EmailMessageId,
                        principalTable: "EmailMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ApplicationLinks_OsmBookings_OsmBookingId",
                        column: x => x.OsmBookingId,
                        principalTable: "OsmBookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OsmBookings_CustomerEmailHash",
                table: "OsmBookings",
                column: "CustomerEmailHash");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationLinks_CreatedByUserId",
                table: "ApplicationLinks",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationLinks_EmailMessageId",
                table: "ApplicationLinks",
                column: "EmailMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationLinks_OsmBookingId",
                table: "ApplicationLinks",
                column: "OsmBookingId");

            migrationBuilder.CreateIndex(
                name: "IX_EmailMessages_ExtractedBookingRef",
                table: "EmailMessages",
                column: "ExtractedBookingRef");

            migrationBuilder.CreateIndex(
                name: "IX_EmailMessages_MessageId",
                table: "EmailMessages",
                column: "MessageId",
                unique: true);
        }
    }
}
