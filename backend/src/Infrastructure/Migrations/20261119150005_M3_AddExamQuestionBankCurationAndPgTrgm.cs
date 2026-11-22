using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OralExamination.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class M3_AddExamQuestionBankCurationAndPgTrgm : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pg_trgm;");

            migrationBuilder.AddColumn<string>(
                name: "bank_curation_status",
                table: "exam_questions",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "BANK_READY");

            migrationBuilder.AddColumn<DateTime>(
                name: "curated_at",
                table: "exam_questions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "curated_by",
                table: "exam_questions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_exam_sets_structure_status_active",
                table: "exam_sets",
                columns: new[] { "structure_id", "is_active", "status", "is_sample_set" });

            migrationBuilder.CreateIndex(
                name: "ix_exam_questions_course_curation_pool",
                table: "exam_questions",
                columns: new[] { "course_id", "is_deleted", "is_active", "approval_status", "bank_curation_status" });

            migrationBuilder.CreateIndex(
                name: "ix_exam_questions_curated_by",
                table: "exam_questions",
                column: "curated_by");

            migrationBuilder.AddCheckConstraint(
                name: "ck_exam_questions_bank_curation_status",
                table: "exam_questions",
                sql: "bank_curation_status IN ('BANK_READY', 'PENDING_CURATION', 'CURATED_INTO_BANK', 'EXCLUDED_FROM_BANK')");

            migrationBuilder.AddForeignKey(
                name: "FK_exam_questions_users_curated_by",
                table: "exam_questions",
                column: "curated_by",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.Sql(
                "CREATE INDEX IF NOT EXISTS ix_exam_questions_content_trgm ON exam_questions USING gin (lower(content) gin_trgm_ops) WHERE is_deleted = false;");

            migrationBuilder.Sql(
                "CREATE INDEX IF NOT EXISTS ix_exam_questions_title_trgm ON exam_questions USING gin (lower(title) gin_trgm_ops) WHERE is_deleted = false;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_exam_questions_title_trgm;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_exam_questions_content_trgm;");

            migrationBuilder.DropForeignKey(
                name: "FK_exam_questions_users_curated_by",
                table: "exam_questions");

            migrationBuilder.DropIndex(
                name: "ix_exam_sets_structure_status_active",
                table: "exam_sets");

            migrationBuilder.DropIndex(
                name: "ix_exam_questions_course_curation_pool",
                table: "exam_questions");

            migrationBuilder.DropIndex(
                name: "ix_exam_questions_curated_by",
                table: "exam_questions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_exam_questions_bank_curation_status",
                table: "exam_questions");

            migrationBuilder.DropColumn(
                name: "bank_curation_status",
                table: "exam_questions");

            migrationBuilder.DropColumn(
                name: "curated_at",
                table: "exam_questions");

            migrationBuilder.DropColumn(
                name: "curated_by",
                table: "exam_questions");
        }
    }
}
