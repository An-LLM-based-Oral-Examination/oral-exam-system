using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OralExamination.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdatePracticeAnswersCheckAndUniqueFilters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "uq_practice_answers_session_parent",
                table: "practice_answers");

            migrationBuilder.DropIndex(
                name: "uq_practice_answers_session_question_main",
                table: "practice_answers");

            migrationBuilder.CreateIndex(
                name: "uq_practice_answers_session_parent",
                table: "practice_answers",
                columns: new[] { "session_id", "parent_answer_id" },
                unique: true,
                filter: "is_follow_up = true AND parent_answer_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "uq_practice_answers_session_question_main",
                table: "practice_answers",
                columns: new[] { "session_id", "question_id" },
                unique: true,
                filter: "is_follow_up = false AND parent_answer_id IS NULL");

            migrationBuilder.Sql("ALTER TABLE practice_answers DROP CONSTRAINT IF EXISTS ck_practice_answers_follow_up_parent;");
            migrationBuilder.AddCheckConstraint(
                name: "ck_practice_answers_follow_up_parent",
                table: "practice_answers",
                sql: "(is_follow_up = false AND parent_answer_id IS NULL) OR (is_follow_up = true AND parent_answer_id IS NOT NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "uq_practice_answers_session_parent",
                table: "practice_answers");

            migrationBuilder.DropIndex(
                name: "uq_practice_answers_session_question_main",
                table: "practice_answers");

            migrationBuilder.DropCheckConstraint(
                name: "ck_practice_answers_follow_up_parent",
                table: "practice_answers");

            migrationBuilder.CreateIndex(
                name: "uq_practice_answers_session_parent",
                table: "practice_answers",
                columns: new[] { "session_id", "parent_answer_id" },
                unique: true,
                filter: "parent_answer_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "uq_practice_answers_session_question_main",
                table: "practice_answers",
                columns: new[] { "session_id", "question_id" },
                unique: true,
                filter: "parent_answer_id IS NULL");
        }
    }
}
