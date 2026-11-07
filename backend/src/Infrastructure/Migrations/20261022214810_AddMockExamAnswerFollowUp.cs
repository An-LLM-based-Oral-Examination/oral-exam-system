using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OralExamination.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMockExamAnswerFollowUp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE mock_exam_answers ADD COLUMN IF NOT EXISTS order_index integer NOT NULL DEFAULT 1;");

            migrationBuilder.AddColumn<bool>(
                name: "is_follow_up",
                table: "mock_exam_answers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "parent_answer_id",
                table: "mock_exam_answers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "prompt_text",
                table: "mock_exam_answers",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_mock_exam_answers_parent",
                table: "mock_exam_answers",
                column: "parent_answer_id");

            migrationBuilder.AddForeignKey(
                name: "FK_mock_exam_answers_mock_exam_answers_parent_answer_id",
                table: "mock_exam_answers",
                column: "parent_answer_id",
                principalTable: "mock_exam_answers",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_mock_exam_answers_mock_exam_answers_parent_answer_id",
                table: "mock_exam_answers");

            migrationBuilder.DropIndex(
                name: "ix_mock_exam_answers_parent",
                table: "mock_exam_answers");

            migrationBuilder.DropColumn(
                name: "is_follow_up",
                table: "mock_exam_answers");

            migrationBuilder.DropColumn(
                name: "parent_answer_id",
                table: "mock_exam_answers");

            migrationBuilder.DropColumn(
                name: "prompt_text",
                table: "mock_exam_answers");
        }
    }
}
