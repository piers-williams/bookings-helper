using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BookingsAssistant.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddProposedPlanExecutionResultJson : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExecutionResultJson",
                table: "ProposedPlans",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExecutionResultJson",
                table: "ProposedPlans");
        }
    }
}
