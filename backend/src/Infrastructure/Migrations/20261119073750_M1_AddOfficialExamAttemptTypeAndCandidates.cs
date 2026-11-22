using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OralExamination.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class M1_AddOfficialExamAttemptTypeAndCandidates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "max_follow_up_questions",
                table: "official_exam_sessions",
                type: "integer",
                nullable: false,
                defaultValue: 2,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "exam_attempt_type",
                table: "official_exam_sessions",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "FE");

            migrationBuilder.CreateTable(
                name: "official_exam_candidates",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    imported_by = table.Column<Guid>(type: "uuid", nullable: true),
                    imported_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_official_exam_candidates", x => x.id);
                    table.ForeignKey(
                        name: "FK_official_exam_candidates_official_exam_sessions_session_id",
                        column: x => x.session_id,
                        principalTable: "official_exam_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_official_exam_candidates_users_imported_by",
                        column: x => x.imported_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_official_exam_candidates_users_student_id",
                        column: x => x.student_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_real_exam_shifts_session_status_time",
                table: "real_exam_session_shifts",
                columns: new[] { "session_id", "status", "start_time", "end_time" });

            migrationBuilder.CreateIndex(
                name: "ix_real_exam_shifts_status_time",
                table: "real_exam_session_shifts",
                columns: new[] { "status", "start_time", "end_time" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_shifts_distinct_proctor_examiner",
                table: "real_exam_session_shifts",
                sql: "proctor_user_id IS NULL OR examiner_user_id IS NULL OR proctor_user_id <> examiner_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_official_exam_sessions_semester_attempt_date",
                table: "official_exam_sessions",
                columns: new[] { "semester_id", "exam_attempt_type", "exam_date" },
                descending: new[] { false, false, true });

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
                    v_dup_details text;
                BEGIN
                    SELECT string_agg(
                        format('(semester_id=%s, course_id=%s, attempt=%s, count=%s, ids=%s)',
                            semester_id, course_id, exam_attempt_type, cnt, ids),
                        '; '
                    )
                    INTO v_dup_details
                    FROM (
                        SELECT
                            semester_id,
                            course_id,
                            exam_attempt_type,
                            COUNT(*) AS cnt,
                            string_agg(id::text || ':' || COALESCE(title, ''), ',') AS ids
                        FROM official_exam_sessions
                        WHERE semester_id IS NOT NULL
                        GROUP BY semester_id, course_id, exam_attempt_type
                        HAVING COUNT(*) > 1
                    ) dup;

                    IF v_dup_details IS NOT NULL THEN
                        RAISE EXCEPTION '[FAIL CLOSED] Phát hiện dữ liệu official_exam_sessions trùng (semester_id, course_id, exam_attempt_type) trước khi tạo uq_official_exam_sessions_semester_course_attempt: %. Yêu cầu xử lý thủ công, không tự đổi sang RE.', v_dup_details;
                    END IF;
                END
                $$;
                """);

            migrationBuilder.CreateIndex(
                name: "uq_official_exam_sessions_semester_course_attempt",
                table: "official_exam_sessions",
                columns: new[] { "semester_id", "course_id", "exam_attempt_type" },
                unique: true,
                filter: "semester_id IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_official_sessions_attempt_type",
                table: "official_exam_sessions",
                sql: "exam_attempt_type IN ('FE', 'RE')");

            migrationBuilder.CreateIndex(
                name: "ix_official_exam_candidates_imported_by",
                table: "official_exam_candidates",
                column: "imported_by");

            migrationBuilder.CreateIndex(
                name: "ix_official_exam_candidates_session_imported_at",
                table: "official_exam_candidates",
                columns: new[] { "session_id", "imported_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_official_exam_candidates_student",
                table: "official_exam_candidates",
                column: "student_id");

            migrationBuilder.CreateIndex(
                name: "uq_official_exam_candidates_session_student",
                table: "official_exam_candidates",
                columns: new[] { "session_id", "student_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "official_exam_candidates");

            migrationBuilder.DropIndex(
                name: "ix_real_exam_shifts_session_status_time",
                table: "real_exam_session_shifts");

            migrationBuilder.DropIndex(
                name: "ix_real_exam_shifts_status_time",
                table: "real_exam_session_shifts");

            migrationBuilder.DropCheckConstraint(
                name: "ck_shifts_distinct_proctor_examiner",
                table: "real_exam_session_shifts");

            migrationBuilder.DropIndex(
                name: "ix_official_exam_sessions_semester_attempt_date",
                table: "official_exam_sessions");

            migrationBuilder.DropIndex(
                name: "uq_official_exam_sessions_semester_course_attempt",
                table: "official_exam_sessions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_official_sessions_attempt_type",
                table: "official_exam_sessions");

            migrationBuilder.DropColumn(
                name: "exam_attempt_type",
                table: "official_exam_sessions");

            migrationBuilder.AlterColumn<int>(
                name: "max_follow_up_questions",
                table: "official_exam_sessions",
                type: "integer",
                nullable: false,
                defaultValue: 1,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 2);
        }
    }
}
