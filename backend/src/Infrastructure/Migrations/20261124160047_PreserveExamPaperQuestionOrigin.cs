using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OralExamination.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PreserveExamPaperQuestionOrigin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_bank_question",
                table: "exam_set_questions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql("""
                UPDATE exam_set_questions AS membership
                SET is_bank_question = TRUE
                FROM exam_questions AS source
                WHERE source.id = membership.source_question_id
                  AND source.approval_status = 'APPROVED';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "is_bank_question",
                table: "exam_set_questions");
        }
    }
}
