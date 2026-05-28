using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiIssueTracker.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class DropAcceptanceCriteriaAiSuggested : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AcceptanceCriteriaAiSuggested",
                table: "issues");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AcceptanceCriteriaAiSuggested",
                table: "issues",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}
