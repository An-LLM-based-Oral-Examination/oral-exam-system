using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OralExamination.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSemesterIdToOfficialExamSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE official_exam_sessions ADD COLUMN IF NOT EXISTS semester_id uuid NULL;");

            migrationBuilder.Sql("""
                UPDATE official_exam_sessions AS oes
                SET semester_id = c.semester_id
                FROM courses AS c
                WHERE oes.semester_id IS NULL AND oes.course_id = c.id;
                """);

            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS ix_official_exam_sessions_semester ON official_exam_sessions (semester_id);");
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS ix_official_exam_sessions_semester_course ON official_exam_sessions (semester_id, course_id);");

            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM pg_constraint
                        WHERE conname = 'FK_official_exam_sessions_semesters_semester_id'
                    ) THEN
                        ALTER TABLE official_exam_sessions
                            ADD CONSTRAINT "FK_official_exam_sessions_semesters_semester_id"
                            FOREIGN KEY (semester_id) REFERENCES semesters(id) ON DELETE RESTRICT;
                    END IF;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_official_exam_sessions_semesters_semester_id",
                table: "official_exam_sessions");

            migrationBuilder.DropIndex(
                name: "ix_official_exam_sessions_semester",
                table: "official_exam_sessions");

            migrationBuilder.DropIndex(
                name: "ix_official_exam_sessions_semester_course",
                table: "official_exam_sessions");

            migrationBuilder.DropColumn(
                name: "semester_id",
                table: "official_exam_sessions");
        }
    }
}
