using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OralExamination.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMf02MockExamSchemaChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "audio_url",
                table: "mock_exam_answers");

            migrationBuilder.AddColumn<int>(
                name: "max_mock_exams_per_day",
                table: "courses",
                type: "integer",
                nullable: false,
                defaultValue: 3);

            migrationBuilder.AddColumn<Guid>(
                name: "mock_exam_answer_id",
                table: "ai_evaluations",
                type: "uuid",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "courses",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000008"),
                column: "max_mock_exams_per_day",
                value: 3);

            migrationBuilder.UpdateData(
                table: "courses",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000009"),
                column: "max_mock_exams_per_day",
                value: 3);

            migrationBuilder.CreateIndex(
                name: "ix_ai_evaluations_mock_exam_answer",
                table: "ai_evaluations",
                column: "mock_exam_answer_id");

            migrationBuilder.AddForeignKey(
                name: "FK_ai_evaluations_mock_exam_answers_mock_exam_answer_id",
                table: "ai_evaluations",
                column: "mock_exam_answer_id",
                principalTable: "mock_exam_answers",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ai_evaluations_mock_exam_answers_mock_exam_answer_id",
                table: "ai_evaluations");

            migrationBuilder.DropIndex(
                name: "ix_ai_evaluations_mock_exam_answer",
                table: "ai_evaluations");

            migrationBuilder.DropColumn(
                name: "max_mock_exams_per_day",
                table: "courses");

            migrationBuilder.DropColumn(
                name: "mock_exam_answer_id",
                table: "ai_evaluations");

            migrationBuilder.AddColumn<string>(
                name: "audio_url",
                table: "mock_exam_answers",
                type: "text",
                nullable: true);
        }
    }
}
