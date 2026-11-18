using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OralExamination.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPracticeAnswersUniqueConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Legacy reconciliation may already have created these names before EF recorded this migration.
            // Rebuild them here so EF owns the intermediate predicates and the next migration can upgrade them.
            migrationBuilder.Sql("DROP INDEX IF EXISTS uq_practice_answers_session_parent;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS uq_practice_answers_session_question_main;");

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "uq_practice_answers_session_parent",
                table: "practice_answers");

            migrationBuilder.DropIndex(
                name: "uq_practice_answers_session_question_main",
                table: "practice_answers");
        }
    }
}
