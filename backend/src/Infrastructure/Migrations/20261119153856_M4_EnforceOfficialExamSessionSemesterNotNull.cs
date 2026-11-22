using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OralExamination.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class M4_EnforceOfficialExamSessionSemesterNotNull : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE official_exam_sessions AS oes
                SET semester_id = c.semester_id
                FROM courses AS c
                WHERE oes.semester_id IS NULL
                  AND oes.course_id = c.id
                  AND c.semester_id IS NOT NULL;
                """);

            migrationBuilder.Sql("""
                DO $$
                DECLARE
                    v_null_count integer;
                    v_dup_count integer;
                BEGIN
                    SELECT COUNT(*) INTO v_null_count
                    FROM official_exam_sessions
                    WHERE semester_id IS NULL;

                    IF v_null_count > 0 THEN
                        RAISE EXCEPTION '[FAIL CLOSED] M4_EnforceOfficialExamSessionSemesterNotNull: Còn % bản ghi official_exam_sessions có semester_id NULL không thể backfill tự động.', v_null_count;
                    END IF;

                    SELECT COUNT(*) INTO v_dup_count
                    FROM (
                        SELECT semester_id, course_id, exam_attempt_type
                        FROM official_exam_sessions
                        GROUP BY semester_id, course_id, exam_attempt_type
                        HAVING COUNT(*) > 1
                    ) AS dups;

                    IF v_dup_count > 0 THEN
                        RAISE EXCEPTION '[FAIL CLOSED] M4_EnforceOfficialExamSessionSemesterNotNull: Phát hiện % nhóm trùng (semester_id, course_id, exam_attempt_type).', v_dup_count;
                    END IF;
                END $$;
                """);

            migrationBuilder.DropIndex(
                name: "ix_official_exam_sessions_semester_course",
                table: "official_exam_sessions");

            migrationBuilder.DropIndex(
                name: "uq_official_exam_sessions_semester_course_attempt",
                table: "official_exam_sessions");

            migrationBuilder.AlterColumn<Guid>(
                name: "semester_id",
                table: "official_exam_sessions",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "uq_official_exam_sessions_semester_course_attempt",
                table: "official_exam_sessions",
                columns: new[] { "semester_id", "course_id", "exam_attempt_type" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "uq_official_exam_sessions_semester_course_attempt",
                table: "official_exam_sessions");

            migrationBuilder.AlterColumn<Guid>(
                name: "semester_id",
                table: "official_exam_sessions",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.CreateIndex(
                name: "ix_official_exam_sessions_semester_course",
                table: "official_exam_sessions",
                columns: new[] { "semester_id", "course_id" });

            migrationBuilder.CreateIndex(
                name: "uq_official_exam_sessions_semester_course_attempt",
                table: "official_exam_sessions",
                columns: new[] { "semester_id", "course_id", "exam_attempt_type" },
                unique: true,
                filter: "semester_id IS NOT NULL");
        }
    }
}
