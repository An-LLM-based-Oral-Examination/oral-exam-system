using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace OralExamination.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialDB : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "dead_letter_queues",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    task_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    payload_json = table.Column<string>(type: "jsonb", nullable: false),
                    error_message = table.Column<string>(type: "text", nullable: false),
                    retry_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "pending"),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    resolved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dead_letter_queues", x => x.id);
                    table.CheckConstraint("ck_dlq_status", "status IN ('pending', 'resolved', 'abandoned')");
                });

            migrationBuilder.CreateTable(
                name: "semesters",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_semesters", x => x.id);
                    table.CheckConstraint("ck_semesters_dates", "end_date >= start_date");
                });

            migrationBuilder.CreateTable(
                name: "system_configs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    value = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_system_configs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    full_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    student_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    username = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "student"),
                    password_hash = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    must_change_password = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.id);
                    table.CheckConstraint("ck_users_role", "role IN ('admin', 'department_head', 'lecturer', 'proctor', 'student')");
                });

            migrationBuilder.CreateTable(
                name: "courses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    credits = table.Column<int>(type: "integer", nullable: false, defaultValue: 3),
                    semester_id = table.Column<Guid>(type: "uuid", nullable: false),
                    has_follow_up = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    transcript_buffer_seconds = table.Column<int>(type: "integer", nullable: false, defaultValue: 60),
                    max_follow_up_questions = table.Column<int>(type: "integer", nullable: false, defaultValue: 2),
                    max_mock_exams_per_day = table.Column<int>(type: "integer", nullable: false, defaultValue: 3),
                    exam_input_mode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "VoiceWithTranscriptEdit"),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_courses", x => x.id);
                    table.CheckConstraint("ck_courses_credits", "credits > 0");
                    table.CheckConstraint("ck_courses_input_mode", "exam_input_mode IN ('VoiceOnly', 'VoiceWithTranscriptEdit')");
                    table.CheckConstraint("ck_courses_max_follow_up", "max_follow_up_questions >= 1 AND max_follow_up_questions <= 5");
                    table.CheckConstraint("ck_courses_mock_quota", "max_mock_exams_per_day >= 1");
                    table.CheckConstraint("ck_courses_transcript_buffer", "transcript_buffer_seconds >= 10 AND transcript_buffer_seconds <= 300");
                    table.ForeignKey(
                        name: "FK_courses_semesters_semester_id",
                        column: x => x.semester_id,
                        principalTable: "semesters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "audit_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    action = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    entity_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    entity_id = table.Column<Guid>(type: "uuid", nullable: true),
                    old_values = table.Column<string>(type: "jsonb", nullable: true),
                    new_values = table.Column<string>(type: "jsonb", nullable: true),
                    ip_address = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_logs", x => x.id);
                    table.ForeignKey(
                        name: "FK_audit_logs_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "refresh_tokens",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    revoked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    replaced_by_token_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_refresh_tokens", x => x.id);
                    table.ForeignKey(
                        name: "FK_refresh_tokens_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "classes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    course_id = table.Column<Guid>(type: "uuid", nullable: false),
                    semester_id = table.Column<Guid>(type: "uuid", nullable: false),
                    lecturer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    max_students = table.Column<int>(type: "integer", nullable: false, defaultValue: 35),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_classes", x => x.id);
                    table.CheckConstraint("ck_classes_max_students", "max_students > 0");
                    table.ForeignKey(
                        name: "FK_classes_courses_course_id",
                        column: x => x.course_id,
                        principalTable: "courses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_classes_semesters_semester_id",
                        column: x => x.semester_id,
                        principalTable: "semesters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_classes_users_lecturer_id",
                        column: x => x.lecturer_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "exam_structures",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    course_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    total_questions = table.Column<int>(type: "integer", nullable: false, defaultValue: 5),
                    duration_minutes = table.Column<int>(type: "integer", nullable: false, defaultValue: 30),
                    bloom_distribution = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb"),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_exam_structures", x => x.id);
                    table.CheckConstraint("ck_exam_struct_duration", "duration_minutes > 0");
                    table.CheckConstraint("ck_exam_struct_questions", "total_questions > 0");
                    table.ForeignKey(
                        name: "FK_exam_structures_courses_course_id",
                        column: x => x.course_id,
                        principalTable: "courses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_exam_structures_users_created_by",
                        column: x => x.created_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "mock_exam_quotas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    course_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quota_date = table.Column<DateOnly>(type: "date", nullable: false),
                    used_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mock_exam_quotas", x => x.id);
                    table.CheckConstraint("ck_mock_exam_quotas_count", "used_count >= 0");
                    table.ForeignKey(
                        name: "FK_mock_exam_quotas_courses_course_id",
                        column: x => x.course_id,
                        principalTable: "courses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_mock_exam_quotas_users_student_id",
                        column: x => x.student_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "practice_sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    course_id = table.Column<Guid>(type: "uuid", nullable: false),
                    practice_mode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "per_question"),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "in_progress"),
                    last_activity_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    selected_difficulties = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'[]'::jsonb")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_practice_sessions", x => x.id);
                    table.CheckConstraint("ck_practice_sessions_mode", "practice_mode IN ('per_question', 'full_session')");
                    table.CheckConstraint("ck_practice_sessions_status", "status IN ('in_progress', 'completed', 'abandoned', 'timed_out')");
                    table.ForeignKey(
                        name: "FK_practice_sessions_courses_course_id",
                        column: x => x.course_id,
                        principalTable: "courses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_practice_sessions_users_student_id",
                        column: x => x.student_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "rubrics",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    course_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    total_max_score = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: false, defaultValue: 10.00m),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rubrics", x => x.id);
                    table.CheckConstraint("ck_rubrics_total_score", "total_max_score = 10.00");
                    table.ForeignKey(
                        name: "FK_rubrics_courses_course_id",
                        column: x => x.course_id,
                        principalTable: "courses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "class_enrollments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    class_id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    enrolled_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "enrolled")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_class_enrollments", x => x.id);
                    table.CheckConstraint("ck_class_enrollment_status", "status IN ('enrolled', 'dropped', 'completed')");
                    table.ForeignKey(
                        name: "FK_class_enrollments_classes_class_id",
                        column: x => x.class_id,
                        principalTable: "classes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_class_enrollments_users_student_id",
                        column: x => x.student_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "exam_sets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    structure_id = table.Column<Guid>(type: "uuid", nullable: false),
                    course_id = table.Column<Guid>(type: "uuid", nullable: false),
                    set_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    generated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_exam_sets", x => x.id);
                    table.ForeignKey(
                        name: "FK_exam_sets_courses_course_id",
                        column: x => x.course_id,
                        principalTable: "courses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_exam_sets_exam_structures_structure_id",
                        column: x => x.structure_id,
                        principalTable: "exam_structures",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "official_exam_sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    course_id = table.Column<Guid>(type: "uuid", nullable: false),
                    exam_structure_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    exam_date = table.Column<DateOnly>(type: "date", nullable: false),
                    has_follow_up = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    max_follow_up_questions = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    exam_input_mode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "VoiceWithTranscriptEdit"),
                    transcript_buffer_seconds = table.Column<int>(type: "integer", nullable: false, defaultValue: 60),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "scheduled"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_official_exam_sessions", x => x.id);
                    table.CheckConstraint("ck_official_sessions_input_mode", "exam_input_mode IN ('VoiceOnly', 'VoiceWithTranscriptEdit')");
                    table.CheckConstraint("ck_official_sessions_max_follow_up", "max_follow_up_questions >= 1 AND max_follow_up_questions <= 5");
                    table.CheckConstraint("ck_official_sessions_status", "status IN ('scheduled', 'in_progress', 'grading', 'auditing', 'concluded')");
                    table.CheckConstraint("ck_official_sessions_transcript_buffer", "transcript_buffer_seconds >= 10 AND transcript_buffer_seconds <= 300");
                    table.ForeignKey(
                        name: "FK_official_exam_sessions_courses_course_id",
                        column: x => x.course_id,
                        principalTable: "courses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_official_exam_sessions_exam_structures_exam_structure_id",
                        column: x => x.exam_structure_id,
                        principalTable: "exam_structures",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_official_exam_sessions_users_created_by",
                        column: x => x.created_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "exam_questions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    course_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rubric_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    content = table.Column<string>(type: "text", nullable: false),
                    sample_answer = table.Column<string>(type: "text", nullable: true),
                    key_points = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'[]'::jsonb"),
                    difficulty = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "medium"),
                    bloom_level = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Understand"),
                    approved_by = table.Column<Guid>(type: "uuid", nullable: true),
                    source = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "manual"),
                    approval_status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "DRAFT"),
                    submitted_by = table.Column<Guid>(type: "uuid", nullable: true),
                    review_notes = table.Column<string>(type: "text", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_exam_questions", x => x.id);
                    table.CheckConstraint("ck_exam_questions_bloom", "bloom_level IN ('Remember', 'Understand', 'Apply', 'Analyze', 'Evaluate', 'Create')");
                    table.CheckConstraint("ck_exam_questions_diff", "difficulty IN ('easy', 'medium', 'hard')");
                    table.ForeignKey(
                        name: "FK_exam_questions_courses_course_id",
                        column: x => x.course_id,
                        principalTable: "courses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_exam_questions_rubrics_rubric_id",
                        column: x => x.rubric_id,
                        principalTable: "rubrics",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_exam_questions_users_approved_by",
                        column: x => x.approved_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_exam_questions_users_submitted_by",
                        column: x => x.submitted_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "practice_questions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    course_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rubric_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    content = table.Column<string>(type: "text", nullable: false),
                    sample_answer = table.Column<string>(type: "text", nullable: true),
                    key_points = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'[]'::jsonb"),
                    difficulty = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "medium"),
                    bloom_level = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Understand"),
                    has_follow_up = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    follow_up_prompt = table.Column<string>(type: "text", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_practice_questions", x => x.id);
                    table.CheckConstraint("ck_practice_questions_bloom", "bloom_level IN ('Remember', 'Understand', 'Apply', 'Analyze', 'Evaluate', 'Create')");
                    table.CheckConstraint("ck_practice_questions_diff", "difficulty IN ('easy', 'medium', 'hard')");
                    table.ForeignKey(
                        name: "FK_practice_questions_courses_course_id",
                        column: x => x.course_id,
                        principalTable: "courses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_practice_questions_rubrics_rubric_id",
                        column: x => x.rubric_id,
                        principalTable: "rubrics",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "rubric_criteria",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    rubric_id = table.Column<Guid>(type: "uuid", nullable: false),
                    criterion_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    max_score = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: false),
                    weight = table.Column<decimal>(type: "numeric(3,2)", precision: 3, scale: 2, nullable: false),
                    bloom_level = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    order_index = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rubric_criteria", x => x.id);
                    table.CheckConstraint("ck_rubric_criteria_score", "max_score > 0 AND max_score <= 10.00");
                    table.CheckConstraint("ck_rubric_criteria_weight", "weight > 0 AND weight <= 1.00");
                    table.ForeignKey(
                        name: "FK_rubric_criteria_rubrics_rubric_id",
                        column: x => x.rubric_id,
                        principalTable: "rubrics",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "mock_exam_sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    exam_set_id = table.Column<Guid>(type: "uuid", nullable: false),
                    course_id = table.Column<Guid>(type: "uuid", nullable: false),
                    has_follow_up = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    submitted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "in_progress"),
                    total_score = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mock_exam_sessions", x => x.id);
                    table.CheckConstraint("ck_mock_sessions_status", "status IN ('in_progress', 'submitted', 'graded', 'abandoned')");
                    table.ForeignKey(
                        name: "FK_mock_exam_sessions_courses_course_id",
                        column: x => x.course_id,
                        principalTable: "courses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_mock_exam_sessions_exam_sets_exam_set_id",
                        column: x => x.exam_set_id,
                        principalTable: "exam_sets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_mock_exam_sessions_users_student_id",
                        column: x => x.student_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "real_exam_session_shifts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    shift_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    room_lab = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    start_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    end_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    proctor_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "scheduled"),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_real_exam_session_shifts", x => x.id);
                    table.CheckConstraint("ck_shifts_status", "status IN ('scheduled', 'in_progress', 'completed', 'cancelled')");
                    table.CheckConstraint("ck_shifts_time", "end_time > start_time");
                    table.ForeignKey(
                        name: "FK_real_exam_session_shifts_official_exam_sessions_session_id",
                        column: x => x.session_id,
                        principalTable: "official_exam_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_real_exam_session_shifts_users_proctor_user_id",
                        column: x => x.proctor_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "exam_set_questions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    exam_set_id = table.Column<Guid>(type: "uuid", nullable: false),
                    exam_question_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_index = table.Column<int>(type: "integer", nullable: false, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_exam_set_questions", x => x.id);
                    table.ForeignKey(
                        name: "FK_exam_set_questions_exam_questions_exam_question_id",
                        column: x => x.exam_question_id,
                        principalTable: "exam_questions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_exam_set_questions_exam_sets_exam_set_id",
                        column: x => x.exam_set_id,
                        principalTable: "exam_sets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "practice_answers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    question_id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    answer_text = table.Column<string>(type: "text", nullable: false),
                    is_follow_up = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    parent_answer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "pending"),
                    submitted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_practice_answers", x => x.id);
                    table.CheckConstraint("ck_practice_answers_status", "status IN ('pending', 'grading', 'graded', 'failed')");
                    table.ForeignKey(
                        name: "FK_practice_answers_practice_answers_parent_answer_id",
                        column: x => x.parent_answer_id,
                        principalTable: "practice_answers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_practice_answers_practice_questions_question_id",
                        column: x => x.question_id,
                        principalTable: "practice_questions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_practice_answers_practice_sessions_session_id",
                        column: x => x.session_id,
                        principalTable: "practice_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_practice_answers_users_student_id",
                        column: x => x.student_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "mock_exam_answers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    question_id = table.Column<Guid>(type: "uuid", nullable: false),
                    answer_text = table.Column<string>(type: "text", nullable: true),
                    time_taken_seconds = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "answered"),
                    answered_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mock_exam_answers", x => x.id);
                    table.CheckConstraint("ck_mock_answers_status", "status IN ('pending', 'answered', 'graded', 'failed')");
                    table.ForeignKey(
                        name: "FK_mock_exam_answers_mock_exam_sessions_session_id",
                        column: x => x.session_id,
                        principalTable: "mock_exam_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_mock_exam_answers_practice_questions_question_id",
                        column: x => x.question_id,
                        principalTable: "practice_questions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "student_exam_tickets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    shift_id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    exam_set_id = table.Column<Guid>(type: "uuid", nullable: true),
                    seat_number = table.Column<int>(type: "integer", nullable: false),
                    ip_address = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "SCHEDULED"),
                    student_acknowledgement_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "PENDING"),
                    is_locked = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    checked_in_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    submitted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_student_exam_tickets", x => x.id);
                    table.CheckConstraint("ck_tickets_ack_status", "student_acknowledgement_status IN ('PENDING', 'ACKNOWLEDGED', 'APPEALED')");
                    table.CheckConstraint("ck_tickets_seat", "seat_number >= 1 AND seat_number <= 40");
                    table.CheckConstraint("ck_tickets_status", "status IN ('SCHEDULED', 'IN_PROGRESS', 'SUBMITTED', 'AI_GRADED', 'AUDITED', 'PUBLISHED', 'LOCKED')");
                    table.ForeignKey(
                        name: "FK_student_exam_tickets_exam_sets_exam_set_id",
                        column: x => x.exam_set_id,
                        principalTable: "exam_sets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_student_exam_tickets_real_exam_session_shifts_shift_id",
                        column: x => x.shift_id,
                        principalTable: "real_exam_session_shifts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_student_exam_tickets_users_student_id",
                        column: x => x.student_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "exam_question_submissions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    ticket_id = table.Column<Guid>(type: "uuid", nullable: false),
                    exam_question_id = table.Column<Guid>(type: "uuid", nullable: false),
                    audio_r2_url = table.Column<string>(type: "text", nullable: true),
                    audio_hash_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    transcript_whisper = table.Column<string>(type: "text", nullable: true),
                    timestamps_whisper = table.Column<string>(type: "jsonb", nullable: true),
                    time_spent_seconds = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    ai_score = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: true),
                    final_score = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: true),
                    grading_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "pending"),
                    submitted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_exam_question_submissions", x => x.id);
                    table.CheckConstraint("ck_submissions_grading_status", "grading_status IN ('pending', 'transcribing', 'transcribed', 'ai_graded', 'audited', 'failed')");
                    table.ForeignKey(
                        name: "FK_exam_question_submissions_exam_questions_exam_question_id",
                        column: x => x.exam_question_id,
                        principalTable: "exam_questions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_exam_question_submissions_student_exam_tickets_ticket_id",
                        column: x => x.ticket_id,
                        principalTable: "student_exam_tickets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "lecturer_audits",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    ticket_id = table.Column<Guid>(type: "uuid", nullable: false),
                    lecturer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    original_ai_score = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: false),
                    audited_score = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: false),
                    override_reason = table.Column<string>(type: "text", nullable: false),
                    is_locked = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    audited_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lecturer_audits", x => x.id);
                    table.CheckConstraint("ck_lecturer_audits_scores", "original_ai_score >= 0 AND audited_score >= 0");
                    table.ForeignKey(
                        name: "FK_lecturer_audits_student_exam_tickets_ticket_id",
                        column: x => x.ticket_id,
                        principalTable: "student_exam_tickets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_lecturer_audits_users_lecturer_id",
                        column: x => x.lecturer_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ai_evaluations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    answer_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    practice_answer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    exam_submission_id = table.Column<Guid>(type: "uuid", nullable: true),
                    mock_exam_answer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    total_score = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: false),
                    feedback = table.Column<string>(type: "text", nullable: true),
                    confidence_score = table.Column<decimal>(type: "numeric(3,2)", precision: 3, scale: 2, nullable: true),
                    cot_trace = table.Column<string>(type: "text", nullable: true),
                    evaluated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_evaluations", x => x.id);
                    table.CheckConstraint("ck_ai_evaluations_score", "total_score >= 0 AND total_score <= 10.00");
                    table.CheckConstraint("ck_ai_evaluations_type", "answer_type IN ('practice', 'exam')");
                    table.ForeignKey(
                        name: "FK_ai_evaluations_exam_question_submissions_exam_submission_id",
                        column: x => x.exam_submission_id,
                        principalTable: "exam_question_submissions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ai_evaluations_mock_exam_answers_mock_exam_answer_id",
                        column: x => x.mock_exam_answer_id,
                        principalTable: "mock_exam_answers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ai_evaluations_practice_answers_practice_answer_id",
                        column: x => x.practice_answer_id,
                        principalTable: "practice_answers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "appeal_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    ticket_id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    submission_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reason = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "PENDING"),
                    assigned_to = table.Column<Guid>(type: "uuid", nullable: false),
                    reviewed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    decision = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    original_score = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: true),
                    proposed_score = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: true),
                    review_notes = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    resolved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_appeal_requests", x => x.id);
                    table.CheckConstraint("ck_appeal_requests_scores", "(original_score IS NULL OR (original_score >= 0 AND original_score <= 10.00)) AND (proposed_score IS NULL OR (proposed_score >= 0 AND proposed_score <= 10.00))");
                    table.CheckConstraint("ck_appeal_requests_status", "status IN ('PENDING', 'IN_REVIEW', 'APPROVED', 'REJECTED', 'CANCELLED')");
                    table.ForeignKey(
                        name: "FK_appeal_requests_exam_question_submissions_submission_id",
                        column: x => x.submission_id,
                        principalTable: "exam_question_submissions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_appeal_requests_official_exam_sessions_session_id",
                        column: x => x.session_id,
                        principalTable: "official_exam_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_appeal_requests_student_exam_tickets_ticket_id",
                        column: x => x.ticket_id,
                        principalTable: "student_exam_tickets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_appeal_requests_users_assigned_to",
                        column: x => x.assigned_to,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_appeal_requests_users_reviewed_by",
                        column: x => x.reviewed_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_appeal_requests_users_student_id",
                        column: x => x.student_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "lecturer_audit_details",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    audit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    criterion_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ai_score = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: false),
                    audited_score = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: false),
                    lecturer_comment = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lecturer_audit_details", x => x.id);
                    table.CheckConstraint("ck_audit_details_scores", "ai_score >= 0 AND audited_score >= 0");
                    table.ForeignKey(
                        name: "FK_lecturer_audit_details_lecturer_audits_audit_id",
                        column: x => x.audit_id,
                        principalTable: "lecturer_audits",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_lecturer_audit_details_rubric_criteria_criterion_id",
                        column: x => x.criterion_id,
                        principalTable: "rubric_criteria",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ai_evaluation_details",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    evaluation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    criterion_id = table.Column<Guid>(type: "uuid", nullable: false),
                    score = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: false),
                    comment = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_evaluation_details", x => x.id);
                    table.CheckConstraint("ck_ai_eval_detail_score", "score >= 0");
                    table.ForeignKey(
                        name: "FK_ai_evaluation_details_ai_evaluations_evaluation_id",
                        column: x => x.evaluation_id,
                        principalTable: "ai_evaluations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ai_evaluation_details_rubric_criteria_criterion_id",
                        column: x => x.criterion_id,
                        principalTable: "rubric_criteria",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "semesters",
                columns: new[] { "id", "code", "created_at", "end_date", "is_active", "name", "start_date" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000007"), "FA26", new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 12, 31), true, "Fall 2026", new DateOnly(2026, 9, 1) });

            migrationBuilder.InsertData(
                table: "system_configs",
                columns: new[] { "id", "description", "key", "updated_at", "value" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000000027"), "Số lượng câu hỏi luyện tập tối đa trong một phiên do Admin cấu hình", "MaxPracticeQuestionsPerSession", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "10" },
                    { new Guid("00000000-0000-0000-0000-000000000028"), "Số lượng câu hỏi luyện tập tối thiểu cho chế độ Dễ đến Khó do Admin cấu hình", "MinMixedPracticeQuestions", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "3" },
                    { new Guid("00000000-0000-0000-0000-000000000029"), "Số lượng câu hỏi luyện tập tối đa cho chế độ Dễ đến Khó do Admin cấu hình", "MaxMixedPracticeQuestions", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "10" },
                    { new Guid("00000000-0000-0000-0000-000000000030"), "Thời gian đệm hiệu đính transcript mặc định (giây) do Admin cấu hình", "TranscriptBufferSeconds", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "60" },
                    { new Guid("00000000-0000-0000-0000-000000000031"), "Số lượng câu hỏi follow-up luyện tập tối đa do Admin cấu hình (1-5 câu)", "MaxPracticeFollowUpQuestions", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "2" },
                    { new Guid("00000000-0000-0000-0000-000000000032"), "Thời gian timeout không tương tác của phiên luyện tập (phút)", "SessionInactivityTimeoutMinutes", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "5" }
                });

            migrationBuilder.InsertData(
                table: "users",
                columns: new[] { "id", "created_at", "email", "full_name", "is_active", "password_hash", "role", "student_code", "updated_at", "username" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000000001"), new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), "admin@fpt.edu.vn", "Nguyen Quang Thanh", true, "pbkdf2-sha256.210000.5K2wrCgwPiV5n+ABUXfgiA==.008qe4tj/RRL8pPAl4EYA+A5+pfxVetYA6GDQh/R0mY=", "admin", null, new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), "admin" },
                    { new Guid("00000000-0000-0000-0000-000000000002"), new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), "headse@fe.edu.vn", "Tran Van Truong", true, "pbkdf2-sha256.210000.HpTuTN7YNAPOdlxcQWqpHA==.ZwGxFXu923/XOLQOpsxyOeQVE5WyKcnaoz26ljezdsM=", "department_head", null, new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), "headse" },
                    { new Guid("00000000-0000-0000-0000-000000000003"), new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), "totnt@fe.edu.vn", "Nguyen Trong Tot", true, "pbkdf2-sha256.210000.NAemlIZmGVkenMK0C4M/nA==.QMwd1s/sMi+q+NaEjyE4zZmnjyaaoiXq/fPgSa/Bxrk=", "lecturer", null, new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), "totnt" },
                    { new Guid("00000000-0000-0000-0000-000000000004"), new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), "haind@fe.edu.vn", "Nguyen Dang Hai", true, "pbkdf2-sha256.210000.XWSrNfAl9MJ4kGLb8pTU3Q==.3Qk6V00ldedgCY86HSofmJWukSF1OjCzPzAphfouT30=", "proctor", null, new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), "haind" }
                });

            migrationBuilder.InsertData(
                table: "users",
                columns: new[] { "id", "created_at", "email", "full_name", "is_active", "must_change_password", "password_hash", "role", "student_code", "updated_at", "username" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000000005"), new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), "hoanglvse170001@fpt.edu.vn", "Le Vu Hoang", true, true, "pbkdf2-sha256.210000.0GiJlqUca2B7Yifgm2O6Fw==.IC/G1GWasnB5RNxlTCiIoHblv2PZ7+4UcrZCloAEpTQ=", "student", "SE170001", new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), "hoalvse170001" },
                    { new Guid("00000000-0000-0000-0000-000000000006"), new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), "maittse170002@fpt.edu.vn", "Tran Thi Mai", true, true, "pbkdf2-sha256.210000.vgos1dwrA89Q/KbEtKvbdQ==.CCAyM2KcWJkAXVkOAIiUHDJUl0wi5L8FZRhnQBXjbZQ=", "student", "SE170002", new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), "maittse170002" }
                });

            migrationBuilder.InsertData(
                table: "users",
                columns: new[] { "id", "created_at", "email", "full_name", "is_active", "password_hash", "role", "student_code", "updated_at", "username" },
                values: new object[] { new Guid("00000000-0000-0000-0000-00000000000b"), new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), "thanhnqse184236@fpt.edu.vn", "Nguyen Quang Thanh", true, "pbkdf2-sha256.210000./uXLNIRG3H9UiZkYHTLHpg==.uKkapszbetceOex+l2duOGby+Qu8ubMrJ/eqHpM1MeQ=", "student", "SE184236", new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), "thanhnqse184236" });

            migrationBuilder.InsertData(
                table: "courses",
                columns: new[] { "id", "code", "created_at", "credits", "exam_input_mode", "has_follow_up", "is_active", "max_follow_up_questions", "max_mock_exams_per_day", "name", "semester_id", "transcript_buffer_seconds" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000000008"), "PRN231", new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), 3, "VoiceWithTranscriptEdit", true, true, 2, 3, "Building Cross-Platform Back-End Applications with .NET", new Guid("00000000-0000-0000-0000-000000000007"), 60 },
                    { new Guid("00000000-0000-0000-0000-000000000009"), "SWD392", new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), 3, "VoiceWithTranscriptEdit", true, true, 2, 3, "Software Architecture and Design", new Guid("00000000-0000-0000-0000-000000000007"), 60 }
                });

            migrationBuilder.InsertData(
                table: "classes",
                columns: new[] { "id", "code", "course_id", "created_at", "is_active", "lecturer_id", "max_students", "semester_id" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), "SE1801-NET", new Guid("00000000-0000-0000-0000-000000000008"), new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, new Guid("00000000-0000-0000-0000-000000000003"), 35, new Guid("00000000-0000-0000-0000-000000000007") });

            migrationBuilder.InsertData(
                table: "exam_structures",
                columns: new[] { "id", "bloom_distribution", "course_id", "created_at", "created_by", "duration_minutes", "is_active", "name", "total_questions" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000018"), "{\"Understand\": 2, \"Apply\": 2, \"Analyze\": 1}", new Guid("00000000-0000-0000-0000-000000000008"), new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("00000000-0000-0000-0000-000000000003"), 30, true, "Cấu trúc Đề thi Vấn đáp Cuối kỳ PRN231 (FA26)", 5 });

            migrationBuilder.InsertData(
                table: "rubrics",
                columns: new[] { "id", "course_id", "created_at", "description", "is_active", "name", "total_max_score" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000013"), new Guid("00000000-0000-0000-0000-000000000008"), new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Đánh giá Clean Architecture", true, "Barem Đánh Giá Kiến Trúc .NET 8", 10.00m });

            migrationBuilder.InsertData(
                table: "class_enrollments",
                columns: new[] { "id", "class_id", "enrolled_at", "status", "student_id" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000010"), new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), "enrolled", new Guid("00000000-0000-0000-0000-000000000005") },
                    { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000010"), new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), "enrolled", new Guid("00000000-0000-0000-0000-000000000006") }
                });

            migrationBuilder.InsertData(
                table: "exam_questions",
                columns: new[] { "id", "approval_status", "approved_by", "bloom_level", "content", "course_id", "created_at", "difficulty", "is_active", "key_points", "review_notes", "rubric_id", "sample_answer", "source", "submitted_by", "title" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000020"), "APPROVED", new Guid("00000000-0000-0000-0000-000000000002"), "Analyze", "Phân biệt sự khác nhau giữa BackgroundService và IHostedService trong .NET 8 khi xây dựng hàng đợi xử lý chấm điểm ngầm?", new Guid("00000000-0000-0000-0000-000000000008"), new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), "hard", true, "[\"IHostedService là interface cốt lõi\",\"BackgroundService là abstract class kế thừa IHostedService\",\"ExecuteAsync chạy Task bất đồng bộ\"]", null, new Guid("00000000-0000-0000-0000-000000000013"), "IHostedService là interface nền tảng định nghĩa StartAsync và StopAsync. BackgroundService là một abstract class kế thừa IHostedService và triển khai sẵn ExecuteAsync chạy nền vòng lặp an toàn.", "manual", null, "Phân biệt BackgroundService và IHostedService trong .NET 8" });

            migrationBuilder.InsertData(
                table: "exam_sets",
                columns: new[] { "id", "course_id", "generated_at", "is_active", "set_code", "structure_id" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000019"), new Guid("00000000-0000-0000-0000-000000000008"), new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "PRN231-SET01", new Guid("00000000-0000-0000-0000-000000000018") });

            migrationBuilder.InsertData(
                table: "official_exam_sessions",
                columns: new[] { "id", "course_id", "created_at", "created_by", "exam_date", "exam_input_mode", "exam_structure_id", "has_follow_up", "max_follow_up_questions", "status", "title", "transcript_buffer_seconds" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000022"), new Guid("00000000-0000-0000-0000-000000000008"), new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, new DateOnly(2026, 10, 15), "VoiceWithTranscriptEdit", new Guid("00000000-0000-0000-0000-000000000018"), true, 2, "scheduled", "Vấn đáp Cuối kỳ PRN231 — Ca 1 Sáng", 60 });

            migrationBuilder.InsertData(
                table: "practice_questions",
                columns: new[] { "id", "bloom_level", "content", "course_id", "created_at", "difficulty", "follow_up_prompt", "has_follow_up", "is_active", "key_points", "rubric_id", "sample_answer", "title" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000000017"), "Apply", "Hãy giải thích nguyên lý Dependency Inversion trong Clean Architecture .NET 8.", new Guid("00000000-0000-0000-0000-000000000008"), new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), "medium", "Nếu muốn thay đổi cơ sở dữ liệu từ PostgreSQL sang SQL Server, bạn cần thay đổi những tầng nào và tại sao?", true, true, "[\"Module cấp cao không phụ thuộc module cấp thấp\",\"Phụ thuộc vào Abstraction/Interface\"]", new Guid("00000000-0000-0000-0000-000000000013"), "Module cấp cao phụ thuộc abstraction. Application khai báo interface, Infrastructure hiện thực.", "Nguyên lý Dependency Inversion" },
                    { new Guid("00000000-0000-0000-0000-000000000101"), "Understand", "Tầng Domain trong Clean Architecture chứa những thành phần nào và tại sao không được tham chiếu tới các tầng khác?", new Guid("00000000-0000-0000-0000-000000000008"), new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), "easy", "Làm thế nào để tầng Domain giao tiếp với các dịch vụ bên ngoài mà không phá vỡ tính độc lập?", true, true, "[\"Chứa Entities và quy tắc cốt lõi\",\"Không phụ thuộc Infrastructure hay UI\"]", new Guid("00000000-0000-0000-0000-000000000013"), "Tầng Domain chứa Entities, Value Objects, Domain Events và Enums cốt lõi. Không phụ thuộc vào framework hay database để bảo toàn quy tắc nghiệp vụ.", "Vai trò tầng Domain trong Clean Architecture" },
                    { new Guid("00000000-0000-0000-0000-000000000102"), "Understand", "Hãy trình bày sự khác biệt giữa Command và Query khi triển khai mẫu CQRS với thư viện MediatR trong .NET 8.", new Guid("00000000-0000-0000-0000-000000000008"), new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), "easy", "Khi nào nên sử dụng AsNoTracking() trong các Query Handler?", true, true, "[\"Command thay đổi trạng thái\",\"Query chỉ đọc dữ liệu\",\"Tách biệt mô hình đọc và ghi\"]", new Guid("00000000-0000-0000-0000-000000000013"), "Command dùng để thay đổi trạng thái dữ liệu (Create, Update, Delete), trong khi Query chỉ dùng để truy vấn đọc dữ liệu và không làm biến đổi trạng thái.", "Phân biệt Command và Query trong mẫu CQRS" },
                    { new Guid("00000000-0000-0000-0000-000000000103"), "Apply", "Cách thức hoạt động của IPipelineBehavior trong MediatR kết hợp với FluentValidation để kiểm tra dữ liệu đầu vào trước khi vào Handler?", new Guid("00000000-0000-0000-0000-000000000008"), new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), "medium", "Ưu điểm của việc đặt validation ở tầng Application thay vì chỉ đặt ở Controller là gì?", true, true, "[\"Chặn trước khi vào Handler\",\"Chạy tất cả IValidator\",\"Trả lỗi chuẩn RFC 7807\"]", new Guid("00000000-0000-0000-0000-000000000013"), "ValidationBehavior chặn request trước khi tới Handler, chạy toàn bộ IValidator<TRequest> đã đăng ký và ném ValidationException nếu có lỗi.", "Cơ chế Pipeline Behavior với FluentValidation" },
                    { new Guid("00000000-0000-0000-0000-000000000104"), "Analyze", "Phân tích cách sử dụng BoundedChannel kết hợp BackgroundService và Polly Retry để xử lý chấm điểm bất đồng bộ chịu tải cao.", new Guid("00000000-0000-0000-0000-000000000008"), new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), "hard", "Tại sao phải tạo IServiceScope mới bên trong BackgroundService mỗi khi xử lý một item?", true, true, "[\"BoundedChannel chống tràn RAM\",\"BackgroundService xử lý nền\",\"Polly Retry và DLQ bảo toàn dữ liệu\"]", new Guid("00000000-0000-0000-0000-000000000013"), "BoundedChannel giới hạn số lượng slot trong RAM để áp dụng backpressure (FullMode.Wait). BackgroundService đọc tuần tự và dùng Polly retry, nếu vẫn lỗi thì lưu vào bảng dead_letter_queues.", "Thiết kế hàng đợi BoundedChannel và xử lý Dead-Letter Queue" },
                    { new Guid("00000000-0000-0000-0000-000000000105"), "Analyze", "Phân tích nguyên nhân gây ra lỗi N+1 Query trong Entity Framework Core và các giải pháp khắc phục hiệu quả.", new Guid("00000000-0000-0000-0000-000000000008"), new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), "hard", "Khi nào nên dùng AsSplitQuery() khi sử dụng nhiều lệnh Include?", true, true, "[\"Nguyên nhân do truy vấn trong vòng lặp\",\"Dùng Include/ThenInclude\",\"Dùng Select Projection và AsNoTracking\"]", new Guid("00000000-0000-0000-0000-000000000013"), "Lỗi N+1 xảy ra khi truy vấn danh sách thực thể cha rồi lặp qua từng phần tử để truy vấn thực thể con. Khắc phục bằng Eager Loading (Include/ThenInclude) hoặc Projection (Select).", "Tối ưu hóa truy vấn EF Core 8 và phòng chống lỗi N+1 Query" }
                });

            migrationBuilder.InsertData(
                table: "rubric_criteria",
                columns: new[] { "id", "bloom_level", "created_at", "criterion_name", "description", "max_score", "order_index", "rubric_id", "weight" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000000014"), "Understand", new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Khái niệm & Ranh giới 4 tầng Clean Architecture", "Hiểu rõ vai trò Domain, Application, Infrastructure, API. Domain không phụ thuộc tầng nào.", 3.00m, 1, new Guid("00000000-0000-0000-0000-000000000013"), 0.30m },
                    { new Guid("00000000-0000-0000-0000-000000000015"), "Apply", new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Nguyên lý Dependency Inversion & CQRS MediatR", "Áp dụng tách biệt Command và Query, sử dụng Interface và Dependency Injection Container.", 4.00m, 2, new Guid("00000000-0000-0000-0000-000000000013"), 0.40m },
                    { new Guid("00000000-0000-0000-0000-000000000016"), "Analyze", new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Xử lý Ngoại lệ & Kiến trúc chịu lỗi (Resilience)", "Trình bày cơ chế Global Exception RFC 7807, Bounded Channel và xử lý retry Polly.", 3.00m, 3, new Guid("00000000-0000-0000-0000-000000000013"), 0.30m }
                });

            migrationBuilder.InsertData(
                table: "exam_set_questions",
                columns: new[] { "id", "exam_question_id", "exam_set_id", "order_index" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000021"), new Guid("00000000-0000-0000-0000-000000000020"), new Guid("00000000-0000-0000-0000-000000000019"), 1 });

            migrationBuilder.InsertData(
                table: "real_exam_session_shifts",
                columns: new[] { "id", "created_at", "end_time", "proctor_user_id", "room_lab", "session_id", "shift_name", "start_time", "status" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000023"), new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 10, 15, 2, 30, 0, 0, DateTimeKind.Utc), new Guid("00000000-0000-0000-0000-000000000004"), "Lab 301", new Guid("00000000-0000-0000-0000-000000000022"), "Kíp 1 (08h00 - 09h30)", new DateTime(2026, 10, 15, 1, 0, 0, 0, DateTimeKind.Utc), "scheduled" });

            migrationBuilder.InsertData(
                table: "student_exam_tickets",
                columns: new[] { "id", "checked_in_at", "exam_set_id", "ip_address", "seat_number", "shift_id", "status", "student_acknowledgement_status", "student_id", "submitted_at" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000000024"), new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("00000000-0000-0000-0000-000000000019"), "192.168.1.101", 1, new Guid("00000000-0000-0000-0000-000000000023"), "SCHEDULED", "PENDING", new Guid("00000000-0000-0000-0000-000000000005"), null },
                    { new Guid("00000000-0000-0000-0000-000000000025"), new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("00000000-0000-0000-0000-000000000019"), "192.168.1.102", 2, new Guid("00000000-0000-0000-0000-000000000023"), "SCHEDULED", "PENDING", new Guid("00000000-0000-0000-0000-000000000006"), null }
                });

            migrationBuilder.InsertData(
                table: "appeal_requests",
                columns: new[] { "id", "assigned_to", "created_at", "decision", "original_score", "proposed_score", "reason", "resolved_at", "review_notes", "reviewed_by", "session_id", "status", "student_id", "submission_id", "ticket_id", "updated_at" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000026"), new Guid("00000000-0000-0000-0000-000000000003"), new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 7.50m, null, "Em xin phúc khảo phần phát biểu về nguyên lý Dependency Inversion bị ồn mic", null, null, null, new Guid("00000000-0000-0000-0000-000000000022"), "PENDING", new Guid("00000000-0000-0000-0000-000000000005"), null, new Guid("00000000-0000-0000-0000-000000000024"), new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc) });

            migrationBuilder.CreateIndex(
                name: "ix_ai_eval_details_criterion",
                table: "ai_evaluation_details",
                column: "criterion_id");

            migrationBuilder.CreateIndex(
                name: "ix_ai_eval_details_evaluation",
                table: "ai_evaluation_details",
                column: "evaluation_id");

            migrationBuilder.CreateIndex(
                name: "ix_ai_evaluations_exam_submission",
                table: "ai_evaluations",
                column: "exam_submission_id");

            migrationBuilder.CreateIndex(
                name: "ix_ai_evaluations_mock_exam_answer",
                table: "ai_evaluations",
                column: "mock_exam_answer_id");

            migrationBuilder.CreateIndex(
                name: "ix_ai_evaluations_practice_answer",
                table: "ai_evaluations",
                column: "practice_answer_id");

            migrationBuilder.CreateIndex(
                name: "ix_appeal_requests_assigned",
                table: "appeal_requests",
                columns: new[] { "assigned_to", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_appeal_requests_reviewed_by",
                table: "appeal_requests",
                column: "reviewed_by");

            migrationBuilder.CreateIndex(
                name: "ix_appeal_requests_session",
                table: "appeal_requests",
                column: "session_id");

            migrationBuilder.CreateIndex(
                name: "ix_appeal_requests_status",
                table: "appeal_requests",
                columns: new[] { "status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_appeal_requests_student",
                table: "appeal_requests",
                column: "student_id");

            migrationBuilder.CreateIndex(
                name: "IX_appeal_requests_submission_id",
                table: "appeal_requests",
                column: "submission_id");

            migrationBuilder.CreateIndex(
                name: "ix_appeal_requests_ticket",
                table: "appeal_requests",
                column: "ticket_id");

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_created",
                table: "audit_logs",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_entity",
                table: "audit_logs",
                columns: new[] { "entity_name", "entity_id" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_user",
                table: "audit_logs",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_class_enrollments_student",
                table: "class_enrollments",
                column: "student_id");

            migrationBuilder.CreateIndex(
                name: "uq_class_enrollments",
                table: "class_enrollments",
                columns: new[] { "class_id", "student_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_classes_code",
                table: "classes",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_classes_course",
                table: "classes",
                column: "course_id");

            migrationBuilder.CreateIndex(
                name: "ix_classes_lecturer",
                table: "classes",
                column: "lecturer_id");

            migrationBuilder.CreateIndex(
                name: "ix_classes_semester",
                table: "classes",
                column: "semester_id");

            migrationBuilder.CreateIndex(
                name: "ix_courses_code",
                table: "courses",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_courses_semester",
                table: "courses",
                column: "semester_id");

            migrationBuilder.CreateIndex(
                name: "ix_dead_letter_queues_status",
                table: "dead_letter_queues",
                columns: new[] { "status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_exam_question_submissions_question",
                table: "exam_question_submissions",
                column: "exam_question_id");

            migrationBuilder.CreateIndex(
                name: "ix_exam_question_submissions_ticket",
                table: "exam_question_submissions",
                column: "ticket_id");

            migrationBuilder.CreateIndex(
                name: "uq_ticket_question",
                table: "exam_question_submissions",
                columns: new[] { "ticket_id", "exam_question_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_exam_questions_approval_status",
                table: "exam_questions",
                column: "approval_status");

            migrationBuilder.CreateIndex(
                name: "ix_exam_questions_approved",
                table: "exam_questions",
                column: "approved_by");

            migrationBuilder.CreateIndex(
                name: "ix_exam_questions_course",
                table: "exam_questions",
                columns: new[] { "course_id", "difficulty", "is_active" });

            migrationBuilder.CreateIndex(
                name: "ix_exam_questions_rubric",
                table: "exam_questions",
                column: "rubric_id");

            migrationBuilder.CreateIndex(
                name: "ix_exam_questions_submitted",
                table: "exam_questions",
                column: "submitted_by");

            migrationBuilder.CreateIndex(
                name: "ix_exam_set_questions_question",
                table: "exam_set_questions",
                column: "exam_question_id");

            migrationBuilder.CreateIndex(
                name: "ix_exam_set_questions_set",
                table: "exam_set_questions",
                column: "exam_set_id");

            migrationBuilder.CreateIndex(
                name: "uq_exam_set_questions",
                table: "exam_set_questions",
                columns: new[] { "exam_set_id", "exam_question_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_exam_sets_course",
                table: "exam_sets",
                column: "course_id");

            migrationBuilder.CreateIndex(
                name: "ix_exam_sets_structure",
                table: "exam_sets",
                column: "structure_id");

            migrationBuilder.CreateIndex(
                name: "ix_exam_structures_course",
                table: "exam_structures",
                column: "course_id");

            migrationBuilder.CreateIndex(
                name: "ix_exam_structures_created_by",
                table: "exam_structures",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_lecturer_audit_details_audit",
                table: "lecturer_audit_details",
                column: "audit_id");

            migrationBuilder.CreateIndex(
                name: "ix_lecturer_audit_details_criterion",
                table: "lecturer_audit_details",
                column: "criterion_id");

            migrationBuilder.CreateIndex(
                name: "ix_lecturer_audits_lecturer",
                table: "lecturer_audits",
                column: "lecturer_id");

            migrationBuilder.CreateIndex(
                name: "ix_lecturer_audits_ticket",
                table: "lecturer_audits",
                column: "ticket_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_mock_exam_answers_question",
                table: "mock_exam_answers",
                column: "question_id");

            migrationBuilder.CreateIndex(
                name: "ix_mock_exam_answers_session",
                table: "mock_exam_answers",
                column: "session_id");

            migrationBuilder.CreateIndex(
                name: "IX_mock_exam_quotas_course_id",
                table: "mock_exam_quotas",
                column: "course_id");

            migrationBuilder.CreateIndex(
                name: "ix_mock_exam_quotas_lookup",
                table: "mock_exam_quotas",
                columns: new[] { "student_id", "course_id", "quota_date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_mock_exam_sessions_course",
                table: "mock_exam_sessions",
                column: "course_id");

            migrationBuilder.CreateIndex(
                name: "ix_mock_exam_sessions_set",
                table: "mock_exam_sessions",
                column: "exam_set_id");

            migrationBuilder.CreateIndex(
                name: "ix_mock_exam_sessions_student",
                table: "mock_exam_sessions",
                columns: new[] { "student_id", "started_at" });

            migrationBuilder.CreateIndex(
                name: "ix_official_exam_sessions_course",
                table: "official_exam_sessions",
                column: "course_id");

            migrationBuilder.CreateIndex(
                name: "ix_official_exam_sessions_creator",
                table: "official_exam_sessions",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_official_exam_sessions_structure",
                table: "official_exam_sessions",
                column: "exam_structure_id");

            migrationBuilder.CreateIndex(
                name: "ix_practice_answers_parent",
                table: "practice_answers",
                column: "parent_answer_id");

            migrationBuilder.CreateIndex(
                name: "ix_practice_answers_question",
                table: "practice_answers",
                column: "question_id");

            migrationBuilder.CreateIndex(
                name: "ix_practice_answers_session",
                table: "practice_answers",
                column: "session_id");

            migrationBuilder.CreateIndex(
                name: "ix_practice_answers_student",
                table: "practice_answers",
                column: "student_id");

            migrationBuilder.CreateIndex(
                name: "ix_practice_questions_course",
                table: "practice_questions",
                columns: new[] { "course_id", "difficulty", "is_active" });

            migrationBuilder.CreateIndex(
                name: "ix_practice_questions_rubric",
                table: "practice_questions",
                column: "rubric_id");

            migrationBuilder.CreateIndex(
                name: "ix_practice_sessions_course",
                table: "practice_sessions",
                column: "course_id");

            migrationBuilder.CreateIndex(
                name: "ix_practice_sessions_last_activity",
                table: "practice_sessions",
                columns: new[] { "status", "last_activity_at" });

            migrationBuilder.CreateIndex(
                name: "ix_practice_sessions_student",
                table: "practice_sessions",
                columns: new[] { "student_id", "started_at" });

            migrationBuilder.CreateIndex(
                name: "ix_real_exam_shifts_proctor",
                table: "real_exam_session_shifts",
                column: "proctor_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_real_exam_shifts_session",
                table: "real_exam_session_shifts",
                column: "session_id");

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_token_hash",
                table: "refresh_tokens",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_user",
                table: "refresh_tokens",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_rubric_criteria_rubric",
                table: "rubric_criteria",
                column: "rubric_id");

            migrationBuilder.CreateIndex(
                name: "ix_rubrics_course",
                table: "rubrics",
                column: "course_id");

            migrationBuilder.CreateIndex(
                name: "ix_semesters_code",
                table: "semesters",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_student_exam_tickets_set",
                table: "student_exam_tickets",
                column: "exam_set_id");

            migrationBuilder.CreateIndex(
                name: "ix_student_exam_tickets_shift",
                table: "student_exam_tickets",
                column: "shift_id");

            migrationBuilder.CreateIndex(
                name: "ix_student_exam_tickets_student",
                table: "student_exam_tickets",
                column: "student_id");

            migrationBuilder.CreateIndex(
                name: "uq_tickets_seat",
                table: "student_exam_tickets",
                columns: new[] { "shift_id", "seat_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_tickets_student",
                table: "student_exam_tickets",
                columns: new[] { "shift_id", "student_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_system_configs_key",
                table: "system_configs",
                column: "key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_users_email",
                table: "users",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_users_student_code",
                table: "users",
                column: "student_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_users_username",
                table: "users",
                column: "username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ai_evaluation_details");

            migrationBuilder.DropTable(
                name: "appeal_requests");

            migrationBuilder.DropTable(
                name: "audit_logs");

            migrationBuilder.DropTable(
                name: "class_enrollments");

            migrationBuilder.DropTable(
                name: "dead_letter_queues");

            migrationBuilder.DropTable(
                name: "exam_set_questions");

            migrationBuilder.DropTable(
                name: "lecturer_audit_details");

            migrationBuilder.DropTable(
                name: "mock_exam_quotas");

            migrationBuilder.DropTable(
                name: "refresh_tokens");

            migrationBuilder.DropTable(
                name: "system_configs");

            migrationBuilder.DropTable(
                name: "ai_evaluations");

            migrationBuilder.DropTable(
                name: "classes");

            migrationBuilder.DropTable(
                name: "lecturer_audits");

            migrationBuilder.DropTable(
                name: "rubric_criteria");

            migrationBuilder.DropTable(
                name: "exam_question_submissions");

            migrationBuilder.DropTable(
                name: "mock_exam_answers");

            migrationBuilder.DropTable(
                name: "practice_answers");

            migrationBuilder.DropTable(
                name: "exam_questions");

            migrationBuilder.DropTable(
                name: "student_exam_tickets");

            migrationBuilder.DropTable(
                name: "mock_exam_sessions");

            migrationBuilder.DropTable(
                name: "practice_questions");

            migrationBuilder.DropTable(
                name: "practice_sessions");

            migrationBuilder.DropTable(
                name: "real_exam_session_shifts");

            migrationBuilder.DropTable(
                name: "exam_sets");

            migrationBuilder.DropTable(
                name: "rubrics");

            migrationBuilder.DropTable(
                name: "official_exam_sessions");

            migrationBuilder.DropTable(
                name: "exam_structures");

            migrationBuilder.DropTable(
                name: "courses");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "semesters");
        }
    }
}
