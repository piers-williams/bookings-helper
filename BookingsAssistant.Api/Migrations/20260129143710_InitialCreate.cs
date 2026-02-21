using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BookingsAssistant.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApplicationUsers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Office365Email = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Office365AccessToken = table.Column<string>(type: "TEXT", nullable: true),
                    Office365RefreshToken = table.Column<string>(type: "TEXT", nullable: true),
                    Office365TokenExpiry = table.Column<DateTime>(type: "TEXT", nullable: true),
                    OsmUsername = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    OsmApiToken = table.Column<string>(type: "TEXT", nullable: true),
                    OsmTokenExpiry = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastSync = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EmailMessages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MessageId = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    SenderEmail = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    SenderName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    Subject = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    ReceivedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsRead = table.Column<bool>(type: "INTEGER", nullable: false),
                    ExtractedBookingRef = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    LastFetched = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OsmBookings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    OsmBookingId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    CustomerName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    CustomerEmail = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    StartDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    LastFetched = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OsmBookings", x => x.Id);
                    table.UniqueConstraint("AK_OsmBookings_OsmBookingId", x => x.OsmBookingId);
                });

            migrationBuilder.CreateTable(
                name: "ApplicationLinks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EmailMessageId = table.Column<int>(type: "INTEGER", nullable: false),
                    OsmBookingId = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "INTEGER", nullable: true),
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

            migrationBuilder.CreateTable(
                name: "OsmComments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    OsmBookingId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    OsmCommentId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    AuthorName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    TextPreview = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsNew = table.Column<bool>(type: "INTEGER", nullable: false),
                    LastFetched = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OsmComments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OsmComments_OsmBookings_OsmBookingId",
                        column: x => x.OsmBookingId,
                        principalTable: "OsmBookings",
                        principalColumn: "OsmBookingId",
                        onDelete: ReferentialAction.Cascade);
                });

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

            migrationBuilder.CreateIndex(
                name: "IX_OsmBookings_CustomerEmail",
                table: "OsmBookings",
                column: "CustomerEmail");

            migrationBuilder.CreateIndex(
                name: "IX_OsmBookings_OsmBookingId",
                table: "OsmBookings",
                column: "OsmBookingId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OsmComments_OsmBookingId",
                table: "OsmComments",
                column: "OsmBookingId");

            migrationBuilder.CreateIndex(
                name: "IX_OsmComments_OsmCommentId",
                table: "OsmComments",
                column: "OsmCommentId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApplicationLinks");

            migrationBuilder.DropTable(
                name: "OsmComments");

            migrationBuilder.DropTable(
                name: "ApplicationUsers");

            migrationBuilder.DropTable(
                name: "EmailMessages");

            migrationBuilder.DropTable(
                name: "OsmBookings");
        }
    }
}
