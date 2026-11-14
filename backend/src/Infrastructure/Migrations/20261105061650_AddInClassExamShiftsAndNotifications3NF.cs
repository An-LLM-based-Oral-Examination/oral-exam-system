using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OralExamination.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInClassExamShiftsAndNotifications3NF : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "ck_exam_questions_approval_status",
                table: "exam_questions",
                sql: "approval_status IN ('DRAFT', 'SUBMITTED_FOR_REVIEW', 'APPROVED', 'NEEDS_REVISION', 'REJECTED')");

            migrationBuilder.CreateTable(
                name: "in_class_exam_shifts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    class_id = table.Column<Guid>(type: "uuid", nullable: false),
                    lecturer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    shift_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    question_pool_mode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "ALL_COURSE_BANK"),
                    selected_question_ids = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'[]'::jsonb"),
                    total_questions_per_student = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    has_follow_up = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    max_follow_up_questions = table.Column<int>(type: "integer", nullable: false, defaultValue: 2),
                    is_auto_publish_grades = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    start_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    end_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    totp_secret_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "SCHEDULED"),
                    is_locked = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    finalized_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_in_class_exam_shifts", x => x.id);
                    table.CheckConstraint("ck_in_class_exam_shifts_status", "status IN ('SCHEDULED', 'IN_PROGRESS', 'FINALIZED', 'CANCELLED')");
                    table.CheckConstraint("ck_in_class_exam_shifts_time", "end_time >= start_time");
                    table.CheckConstraint("ck_in_class_exam_shifts_pool_mode", "question_pool_mode IN ('ALL_COURSE_BANK', 'SELECTED_POOL')");
                    table.CheckConstraint("ck_in_class_exam_shifts_total_questions", "total_questions_per_student >= 1 AND total_questions_per_student <= 10");
                    table.CheckConstraint("ck_in_class_exam_shifts_max_followup", "max_follow_up_questions >= 0 AND max_follow_up_questions <= 5");
                    table.ForeignKey(
                        name: "FK_in_class_exam_shifts_classes_class_id",
                        column: x => x.class_id,
                        principalTable: "classes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_in_class_exam_shifts_users_lecturer_id",
                        column: x => x.lecturer_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "notifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    message = table.Column<string>(type: "text", nullable: false),
                    type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    is_read = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    read_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    related_entity_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    related_entity_id = table.Column<Guid>(type: "uuid", nullable: true),
                    metadata_json = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notifications", x => x.id);
                    table.ForeignKey(
                        name: "FK_notifications_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "in_class_exam_submissions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    shift_id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    question_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_follow_up = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    parent_submission_id = table.Column<Guid>(type: "uuid", nullable: true),
                    follow_up_index = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    follow_up_prompt = table.Column<string>(type: "text", nullable: true),
                    transcript = table.Column<string>(type: "text", nullable: false),
                    ai_score = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: true),
                    ai_confidence_score = table.Column<decimal>(type: "numeric(3,2)", precision: 3, scale: 2, nullable: true),
                    cot_trace = table.Column<string>(type: "text", nullable: true),
                    ai_feedback = table.Column<string>(type: "text", nullable: true),
                    final_score = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: true),
                    lecturer_notes = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "SUBMITTED"),
                    submitted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    graded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_in_class_exam_submissions", x => x.id);
                    table.CheckConstraint("ck_in_class_exam_submissions_status", "status IN ('SUBMITTED', 'AI_GRADED', 'AUDITED')");
                    table.CheckConstraint("ck_in_class_exam_submissions_followup_index", "follow_up_index >= 0 AND follow_up_index <= 5");
                    table.ForeignKey(
                        name: "FK_in_class_exam_submissions_in_class_exam_shifts_shift_id",
                        column: x => x.shift_id,
                        principalTable: "in_class_exam_shifts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_in_class_exam_submissions_users_student_id",
                        column: x => x.student_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_in_class_exam_submissions_practice_questions_question_id",
                        column: x => x.question_id,
                        principalTable: "practice_questions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_in_class_exam_submissions_in_class_exam_submissions_parent~",
                        column: x => x.parent_submission_id,
                        principalTable: "in_class_exam_submissions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "in_class_shift_participants",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    shift_id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assigned_question_id = table.Column<Guid>(type: "uuid", nullable: true),
                    assigned_question_ids = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'[]'::jsonb"),
                    total_ai_score = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: true),
                    total_final_score = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: true),
                    joined_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "JOINED")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_in_class_shift_participants", x => x.id);
                    table.CheckConstraint("ck_in_class_shift_participants_status", "status IN ('JOINED', 'SUBMITTED', 'ABSENT')");
                    table.ForeignKey(
                        name: "FK_in_class_shift_participants_in_class_exam_shifts_shift_id",
                        column: x => x.shift_id,
                        principalTable: "in_class_exam_shifts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_in_class_shift_participants_users_student_id",
                        column: x => x.student_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_in_class_shift_participants_practice_questions_assigned_qu~",
                        column: x => x.assigned_question_id,
                        principalTable: "practice_questions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "in_class_shift_passwords",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    shift_id = table.Column<Guid>(type: "uuid", nullable: false),
                    plain_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    password_hash = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    valid_from = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    valid_to = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_in_class_shift_passwords", x => x.id);
                    table.ForeignKey(
                        name: "FK_in_class_shift_passwords_in_class_exam_shifts_shift_id",
                        column: x => x.shift_id,
                        principalTable: "in_class_exam_shifts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Migration 20261104062322 chạy PreMigration reconciler trước và có thể đã
            // bổ sung các cột/index/FK này. Dùng DDL idempotent để cả DB sạch lẫn DB
            // đã được self-heal đều nâng cấp được, tránh lỗi PostgreSQL 42701/42P07.
            migrationBuilder.Sql("""
                ALTER TABLE ai_evaluations ADD COLUMN IF NOT EXISTS student_id uuid NULL;
                ALTER TABLE ai_evaluations ADD COLUMN IF NOT EXISTS in_class_submission_id uuid NULL;
                """);

            migrationBuilder.Sql("""
                ALTER TABLE ai_evaluations DROP CONSTRAINT IF EXISTS ck_ai_evaluations_type;
                ALTER TABLE ai_evaluations DROP CONSTRAINT IF EXISTS ck_ai_evaluations_xor;
                """);

            migrationBuilder.AddCheckConstraint(
                name: "ck_ai_evaluations_type",
                table: "ai_evaluations",
                sql: "answer_type IN ('practice', 'mock_exam', 'exam', 'in_class')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_ai_evaluations_xor",
                table: "ai_evaluations",
                sql: "(answer_type = 'practice' AND practice_answer_id IS NOT NULL AND mock_exam_answer_id IS NULL AND exam_submission_id IS NULL AND in_class_submission_id IS NULL) OR (answer_type = 'mock_exam' AND mock_exam_answer_id IS NOT NULL AND practice_answer_id IS NULL AND exam_submission_id IS NULL AND in_class_submission_id IS NULL) OR (answer_type = 'exam' AND exam_submission_id IS NOT NULL AND practice_answer_id IS NULL AND mock_exam_answer_id IS NULL AND in_class_submission_id IS NULL) OR (answer_type = 'in_class' AND in_class_submission_id IS NOT NULL AND practice_answer_id IS NULL AND mock_exam_answer_id IS NULL AND exam_submission_id IS NULL)");

            migrationBuilder.Sql("""
                CREATE INDEX IF NOT EXISTS ix_ai_evaluations_student
                    ON ai_evaluations (student_id);
                CREATE INDEX IF NOT EXISTS ix_ai_evaluations_in_class_submission
                    ON ai_evaluations (in_class_submission_id);
                CREATE INDEX IF NOT EXISTS ix_classes_active_semester_course_created
                    ON classes (is_active, semester_id, course_id, created_at);

                DO $migration$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM pg_constraint
                        WHERE conname = 'FK_ai_evaluations_users_student_id'
                    ) THEN
                        ALTER TABLE ai_evaluations
                            ADD CONSTRAINT "FK_ai_evaluations_users_student_id"
                            FOREIGN KEY (student_id) REFERENCES users(id) ON DELETE CASCADE;
                    END IF;

                    IF NOT EXISTS (
                        SELECT 1 FROM pg_constraint
                        WHERE conname = 'FK_ai_evaluations_in_class_exam_submissions_in_class_submissio~'
                    ) THEN
                        ALTER TABLE ai_evaluations
                            ADD CONSTRAINT "FK_ai_evaluations_in_class_exam_submissions_in_class_submissio~"
                            FOREIGN KEY (in_class_submission_id)
                            REFERENCES in_class_exam_submissions(id) ON DELETE CASCADE;
                    END IF;
                END
                $migration$;
                """);

            migrationBuilder.CreateIndex(
                name: "ix_in_class_exam_shifts_class_status_created",
                table: "in_class_exam_shifts",
                columns: new[] { "class_id", "status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_in_class_exam_shifts_lecturer",
                table: "in_class_exam_shifts",
                column: "lecturer_id");

            migrationBuilder.CreateIndex(
                name: "ix_in_class_exam_submissions_question",
                table: "in_class_exam_submissions",
                column: "question_id");

            migrationBuilder.CreateIndex(
                name: "ix_in_class_exam_submissions_parent",
                table: "in_class_exam_submissions",
                column: "parent_submission_id");

            migrationBuilder.CreateIndex(
                name: "ix_in_class_exam_submissions_shift_student",
                table: "in_class_exam_submissions",
                columns: new[] { "shift_id", "student_id", "submitted_at" });

            migrationBuilder.CreateIndex(
                name: "uq_in_class_exam_submissions_shift_student",
                table: "in_class_exam_submissions",
                columns: new[] { "shift_id", "student_id", "question_id", "follow_up_index" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_in_class_exam_submissions_student",
                table: "in_class_exam_submissions",
                columns: new[] { "student_id", "submitted_at" });

            migrationBuilder.CreateIndex(
                name: "ix_in_class_shift_participants_student",
                table: "in_class_shift_participants",
                columns: new[] { "student_id", "shift_id" });

            migrationBuilder.CreateIndex(
                name: "ix_in_class_shift_participants_assigned_question",
                table: "in_class_shift_participants",
                column: "assigned_question_id");

            migrationBuilder.CreateIndex(
                name: "uq_in_class_shift_participants_shift_student",
                table: "in_class_shift_participants",
                columns: new[] { "shift_id", "student_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_in_class_shift_passwords_shift_valid_from",
                table: "in_class_shift_passwords",
                columns: new[] { "shift_id", "valid_from" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_in_class_shift_passwords_shift_valid",
                table: "in_class_shift_passwords",
                columns: new[] { "shift_id", "valid_from", "valid_to" });

            migrationBuilder.CreateIndex(
                name: "IX_notifications_user_id_is_read_created_at",
                table: "notifications",
                columns: new[] { "user_id", "is_read", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_exam_questions_approval_status",
                table: "exam_questions");

            migrationBuilder.DropForeignKey(
                name: "FK_ai_evaluations_in_class_exam_submissions_in_class_submissio~",
                table: "ai_evaluations");

            migrationBuilder.DropForeignKey(
                name: "FK_ai_evaluations_users_student_id",
                table: "ai_evaluations");

            migrationBuilder.DropIndex(
                name: "ix_ai_evaluations_in_class_submission",
                table: "ai_evaluations");

            migrationBuilder.DropIndex(
                name: "ix_ai_evaluations_student",
                table: "ai_evaluations");

            migrationBuilder.DropIndex(
                name: "ix_classes_active_semester_course_created",
                table: "classes");

            migrationBuilder.DropCheckConstraint(
                name: "ck_ai_evaluations_type",
                table: "ai_evaluations");

            migrationBuilder.DropCheckConstraint(
                name: "ck_ai_evaluations_xor",
                table: "ai_evaluations");

            migrationBuilder.DropColumn(
                name: "in_class_submission_id",
                table: "ai_evaluations");

            migrationBuilder.DropColumn(
                name: "student_id",
                table: "ai_evaluations");

            migrationBuilder.AddCheckConstraint(
                name: "ck_ai_evaluations_type",
                table: "ai_evaluations",
                sql: "answer_type IN ('practice', 'mock_exam', 'exam')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_ai_evaluations_xor",
                table: "ai_evaluations",
                sql: "(answer_type = 'practice' AND practice_answer_id IS NOT NULL AND mock_exam_answer_id IS NULL AND exam_submission_id IS NULL) OR (answer_type = 'mock_exam' AND mock_exam_answer_id IS NOT NULL AND practice_answer_id IS NULL AND exam_submission_id IS NULL) OR (answer_type = 'exam' AND exam_submission_id IS NOT NULL AND practice_answer_id IS NULL AND mock_exam_answer_id IS NULL)");

            migrationBuilder.DropTable(
                name: "in_class_exam_submissions");

            migrationBuilder.DropTable(
                name: "in_class_shift_participants");

            migrationBuilder.DropTable(
                name: "in_class_shift_passwords");

            migrationBuilder.DropTable(
                name: "notifications");

            migrationBuilder.DropTable(
                name: "in_class_exam_shifts");
        }
    }
}
