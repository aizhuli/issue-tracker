using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiIssueTracker.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddIssuesLabelsComments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_labels_ProjectId",
                table: "labels");

            migrationBuilder.DropIndex(
                name: "IX_issues_ProjectId",
                table: "issues");

            migrationBuilder.AddColumn<int>(
                name: "NextIssueNumber",
                table: "projects",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "Color",
                table: "labels",
                type: "character varying(7)",
                maxLength: 7,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAt",
                table: "labels",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "labels",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "AcceptanceCriteria",
                table: "issues",
                type: "character varying(10000)",
                maxLength: 10000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AcceptanceCriteriaAiSuggested",
                table: "issues",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<long>(
                name: "AssigneeId",
                table: "issues",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ClosedAt",
                table: "issues",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAt",
                table: "issues",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "issues",
                type: "character varying(10000)",
                maxLength: 10000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Number",
                table: "issues",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Priority",
                table: "issues",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<long>(
                name: "ReporterId",
                table: "issues",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "issues",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Title",
                table: "issues",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UpdatedAt",
                table: "issues",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.CreateTable(
                name: "comments",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    IssueId = table.Column<long>(type: "bigint", nullable: false),
                    AuthorId = table.Column<long>(type: "bigint", nullable: false),
                    Body = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_comments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_comments_issues_IssueId",
                        column: x => x.IssueId,
                        principalTable: "issues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_comments_users_AuthorId",
                        column: x => x.AuthorId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "issue_labels",
                columns: table => new
                {
                    IssueId = table.Column<long>(type: "bigint", nullable: false),
                    LabelId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_issue_labels", x => new { x.IssueId, x.LabelId });
                    table.ForeignKey(
                        name: "FK_issue_labels_issues_IssueId",
                        column: x => x.IssueId,
                        principalTable: "issues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_issue_labels_labels_LabelId",
                        column: x => x.LabelId,
                        principalTable: "labels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_labels_ProjectId_Name",
                table: "labels",
                columns: new[] { "ProjectId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_issues_AssigneeId",
                table: "issues",
                column: "AssigneeId");

            migrationBuilder.CreateIndex(
                name: "IX_issues_ProjectId_Number",
                table: "issues",
                columns: new[] { "ProjectId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_issues_ProjectId_Status_CreatedAt_Id",
                table: "issues",
                columns: new[] { "ProjectId", "Status", "CreatedAt", "Id" },
                descending: new[] { false, false, true, true });

            migrationBuilder.CreateIndex(
                name: "IX_issues_ReporterId",
                table: "issues",
                column: "ReporterId");

            migrationBuilder.CreateIndex(
                name: "IX_comments_AuthorId",
                table: "comments",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_comments_IssueId_CreatedAt_Id",
                table: "comments",
                columns: new[] { "IssueId", "CreatedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_issue_labels_LabelId",
                table: "issue_labels",
                column: "LabelId");

            migrationBuilder.AddForeignKey(
                name: "FK_issues_users_AssigneeId",
                table: "issues",
                column: "AssigneeId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_issues_users_ReporterId",
                table: "issues",
                column: "ReporterId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_issues_users_AssigneeId",
                table: "issues");

            migrationBuilder.DropForeignKey(
                name: "FK_issues_users_ReporterId",
                table: "issues");

            migrationBuilder.DropTable(
                name: "comments");

            migrationBuilder.DropTable(
                name: "issue_labels");

            migrationBuilder.DropIndex(
                name: "IX_labels_ProjectId_Name",
                table: "labels");

            migrationBuilder.DropIndex(
                name: "IX_issues_AssigneeId",
                table: "issues");

            migrationBuilder.DropIndex(
                name: "IX_issues_ProjectId_Number",
                table: "issues");

            migrationBuilder.DropIndex(
                name: "IX_issues_ProjectId_Status_CreatedAt_Id",
                table: "issues");

            migrationBuilder.DropIndex(
                name: "IX_issues_ReporterId",
                table: "issues");

            migrationBuilder.DropColumn(
                name: "NextIssueNumber",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "Color",
                table: "labels");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "labels");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "labels");

            migrationBuilder.DropColumn(
                name: "AcceptanceCriteria",
                table: "issues");

            migrationBuilder.DropColumn(
                name: "AcceptanceCriteriaAiSuggested",
                table: "issues");

            migrationBuilder.DropColumn(
                name: "AssigneeId",
                table: "issues");

            migrationBuilder.DropColumn(
                name: "ClosedAt",
                table: "issues");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "issues");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "issues");

            migrationBuilder.DropColumn(
                name: "Number",
                table: "issues");

            migrationBuilder.DropColumn(
                name: "Priority",
                table: "issues");

            migrationBuilder.DropColumn(
                name: "ReporterId",
                table: "issues");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "issues");

            migrationBuilder.DropColumn(
                name: "Title",
                table: "issues");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "issues");

            migrationBuilder.CreateIndex(
                name: "IX_labels_ProjectId",
                table: "labels",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_issues_ProjectId",
                table: "issues",
                column: "ProjectId");
        }
    }
}
