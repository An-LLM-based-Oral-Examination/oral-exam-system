using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OralExamination.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInClassShiftClosAndDifficulties : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE in_class_exam_shifts DROP CONSTRAINT IF EXISTS ck_in_class_exam_shifts_pool_mode;");
            migrationBuilder.Sql("ALTER TABLE in_class_exam_shifts ADD COLUMN IF NOT EXISTS selected_clo_ids jsonb NOT NULL DEFAULT '[]'::jsonb;");
            migrationBuilder.Sql("ALTER TABLE in_class_exam_shifts ADD COLUMN IF NOT EXISTS selected_difficulties jsonb NOT NULL DEFAULT '[\"easy\",\"medium\",\"hard\"]'::jsonb;");

            migrationBuilder.AddCheckConstraint(
                name: "ck_in_class_exam_shifts_pool_mode",
                table: "in_class_exam_shifts",
                sql: "question_pool_mode IN ('ALL_COURSE_BANK', 'SELECTED_CLOS', 'SELECTED_POOL')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_in_class_exam_shifts_pool_mode",
                table: "in_class_exam_shifts");

            migrationBuilder.DropColumn(
                name: "selected_clo_ids",
                table: "in_class_exam_shifts");

            migrationBuilder.DropColumn(
                name: "selected_difficulties",
                table: "in_class_exam_shifts");

            migrationBuilder.AddCheckConstraint(
                name: "ck_in_class_exam_shifts_pool_mode",
                table: "in_class_exam_shifts",
                sql: "question_pool_mode IN ('ALL_COURSE_BANK', 'SELECTED_POOL')");
        }
    }
}
