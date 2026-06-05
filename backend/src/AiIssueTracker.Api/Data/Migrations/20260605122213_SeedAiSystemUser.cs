using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiIssueTracker.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class SeedAiSystemUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "users",
                columns: new[] { "Id", "Avatar", "CreatedAt", "Email", "Name", "PasswordHash" },
                values: new object[] { 1L, null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "ai@system", "AI", "!" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "users",
                keyColumn: "Id",
                keyValue: 1L);
        }
    }
}
