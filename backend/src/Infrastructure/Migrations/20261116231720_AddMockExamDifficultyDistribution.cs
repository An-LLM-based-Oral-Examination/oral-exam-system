using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OralExamination.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMockExamDifficultyDistribution : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "mock_exam_difficulty_distribution",
                table: "courses",
                type: "text",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "courses",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000008"),
                column: "mock_exam_difficulty_distribution",
                value: null);

            migrationBuilder.UpdateData(
                table: "courses",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000009"),
                column: "mock_exam_difficulty_distribution",
                value: null);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "mock_exam_difficulty_distribution",
                table: "courses");
        }
    }
}
