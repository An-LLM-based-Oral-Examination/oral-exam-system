using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace OralExamination.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialBaseline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:pgcrypto", ",,");

            migrationBuilder.CreateTable(
                name: "courses",
                columns: table => new
                {
                    course_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    course_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    course_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("courses_pkey", x => x.course_id);
                });

            migrationBuilder.CreateTable(
                name: "roles",
                columns: table => new
                {
                    role_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    role_name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("roles_pkey", x => x.role_id);
                });

            migrationBuilder.CreateTable(
                name: "semesters",
                columns: table => new
                {
                    semester_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    semester_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("semesters_pkey", x => x.semester_id);
                });

            migrationBuilder.CreateTable(
                name: "exam_structures",
                columns: table => new
                {
                    structure_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    course_id = table.Column<int>(type: "integer", nullable: false),
                    structure_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    exam_mode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValueSql: "'FIXED'::character varying"),
                    easy_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    medium_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    hard_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    duration_minutes = table.Column<int>(type: "integer", nullable: false),
                    max_followups = table.Column<int>(type: "integer", nullable: true),
                    followup_timeout_sec = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("exam_structures_pkey", x => x.structure_id);
                    table.ForeignKey(
                        name: "exam_structures_course_id_fkey",
                        column: x => x.course_id,
                        principalTable: "courses",
                        principalColumn: "course_id");
                });

            migrationBuilder.CreateTable(
                name: "practice_questions",
                columns: table => new
                {
                    question_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    course_id = table.Column<int>(type: "integer", nullable: false),
                    content = table.Column<string>(type: "text", nullable: false),
                    difficulty = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    model_answer = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("practice_questions_pkey", x => x.question_id);
                    table.ForeignKey(
                        name: "practice_questions_course_id_fkey",
                        column: x => x.course_id,
                        principalTable: "courses",
                        principalColumn: "course_id");
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    role_id = table.Column<int>(type: "integer", nullable: false),
                    username = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    password_hash = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    full_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("users_pkey", x => x.user_id);
                    table.ForeignKey(
                        name: "users_role_id_fkey",
                        column: x => x.role_id,
                        principalTable: "roles",
                        principalColumn: "role_id");
                });

            migrationBuilder.CreateTable(
                name: "audit_logs",
                columns: table => new
                {
                    log_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    target_table = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    target_id = table.Column<Guid>(type: "uuid", nullable: true),
                    old_values = table.Column<string>(type: "jsonb", nullable: true),
                    new_values = table.Column<string>(type: "jsonb", nullable: true),
                    ip_address = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("audit_logs_pkey", x => x.log_id);
                    table.ForeignKey(
                        name: "audit_logs_user_id_fkey",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "user_id");
                });

            migrationBuilder.CreateTable(
                name: "classes",
                columns: table => new
                {
                    class_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    course_id = table.Column<int>(type: "integer", nullable: false),
                    semester_id = table.Column<int>(type: "integer", nullable: false),
                    instructor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    class_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("classes_pkey", x => x.class_id);
                    table.ForeignKey(
                        name: "classes_course_id_fkey",
                        column: x => x.course_id,
                        principalTable: "courses",
                        principalColumn: "course_id");
                    table.ForeignKey(
                        name: "classes_instructor_id_fkey",
                        column: x => x.instructor_id,
                        principalTable: "users",
                        principalColumn: "user_id");
                    table.ForeignKey(
                        name: "classes_semester_id_fkey",
                        column: x => x.semester_id,
                        principalTable: "semesters",
                        principalColumn: "semester_id");
                });

            migrationBuilder.CreateTable(
                name: "exam_questions",
                columns: table => new
                {
                    question_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    course_id = table.Column<int>(type: "integer", nullable: false),
                    content = table.Column<string>(type: "text", nullable: false),
                    difficulty = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    model_answer = table.Column<string>(type: "text", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    approved_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("exam_questions_pkey", x => x.question_id);
                    table.ForeignKey(
                        name: "exam_questions_approved_by_fkey",
                        column: x => x.approved_by,
                        principalTable: "users",
                        principalColumn: "user_id");
                    table.ForeignKey(
                        name: "exam_questions_course_id_fkey",
                        column: x => x.course_id,
                        principalTable: "courses",
                        principalColumn: "course_id");
                });

            migrationBuilder.CreateTable(
                name: "mock_exam_quotas",
                columns: table => new
                {
                    quota_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    course_id = table.Column<int>(type: "integer", nullable: false),
                    quota_date = table.Column<DateOnly>(type: "date", nullable: false, defaultValueSql: "CURRENT_DATE"),
                    used_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    max_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 3)
                },
                constraints: table =>
                {
                    table.PrimaryKey("mock_exam_quotas_pkey", x => x.quota_id);
                    table.ForeignKey(
                        name: "mock_exam_quotas_course_id_fkey",
                        column: x => x.course_id,
                        principalTable: "courses",
                        principalColumn: "course_id");
                    table.ForeignKey(
                        name: "mock_exam_quotas_student_id_fkey",
                        column: x => x.student_id,
                        principalTable: "users",
                        principalColumn: "user_id");
                });

            migrationBuilder.CreateTable(
                name: "mock_exam_sessions",
                columns: table => new
                {
                    mock_session_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    structure_id = table.Column<int>(type: "integer", nullable: false),
                    exam_mode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValueSql: "'FIXED'::character varying"),
                    overall_score = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    overall_feedback = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValueSql: "'IN_PROGRESS'::character varying"),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("mock_exam_sessions_pkey", x => x.mock_session_id);
                    table.ForeignKey(
                        name: "mock_exam_sessions_structure_id_fkey",
                        column: x => x.structure_id,
                        principalTable: "exam_structures",
                        principalColumn: "structure_id");
                    table.ForeignKey(
                        name: "mock_exam_sessions_student_id_fkey",
                        column: x => x.student_id,
                        principalTable: "users",
                        principalColumn: "user_id");
                });

            migrationBuilder.CreateTable(
                name: "practice_sessions",
                columns: table => new
                {
                    session_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("practice_sessions_pkey", x => x.session_id);
                    table.ForeignKey(
                        name: "practice_sessions_student_id_fkey",
                        column: x => x.student_id,
                        principalTable: "users",
                        principalColumn: "user_id");
                });

            migrationBuilder.CreateTable(
                name: "class_enrollments",
                columns: table => new
                {
                    enrollment_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    class_id = table.Column<int>(type: "integer", nullable: false),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    enrolled_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValueSql: "'ACTIVE'::character varying")
                },
                constraints: table =>
                {
                    table.PrimaryKey("class_enrollments_pkey", x => x.enrollment_id);
                    table.ForeignKey(
                        name: "class_enrollments_class_id_fkey",
                        column: x => x.class_id,
                        principalTable: "classes",
                        principalColumn: "class_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "class_enrollments_student_id_fkey",
                        column: x => x.student_id,
                        principalTable: "users",
                        principalColumn: "user_id");
                });

            migrationBuilder.CreateTable(
                name: "real_exam_sessions",
                columns: table => new
                {
                    exam_session_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    class_id = table.Column<int>(type: "integer", nullable: false),
                    structure_id = table.Column<int>(type: "integer", nullable: false),
                    exam_mode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValueSql: "'FIXED'::character varying"),
                    title = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    exam_date = table.Column<DateOnly>(type: "date", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("real_exam_sessions_pkey", x => x.exam_session_id);
                    table.ForeignKey(
                        name: "real_exam_sessions_class_id_fkey",
                        column: x => x.class_id,
                        principalTable: "classes",
                        principalColumn: "class_id");
                    table.ForeignKey(
                        name: "real_exam_sessions_structure_id_fkey",
                        column: x => x.structure_id,
                        principalTable: "exam_structures",
                        principalColumn: "structure_id");
                });

            migrationBuilder.CreateTable(
                name: "rubric_items",
                columns: table => new
                {
                    rubric_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    practice_question_id = table.Column<Guid>(type: "uuid", nullable: true),
                    exam_question_id = table.Column<Guid>(type: "uuid", nullable: true),
                    criteria_description = table.Column<string>(type: "text", nullable: false),
                    max_score = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("rubric_items_pkey", x => x.rubric_id);
                    table.ForeignKey(
                        name: "rubric_items_exam_question_id_fkey",
                        column: x => x.exam_question_id,
                        principalTable: "exam_questions",
                        principalColumn: "question_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "rubric_items_practice_question_id_fkey",
                        column: x => x.practice_question_id,
                        principalTable: "practice_questions",
                        principalColumn: "question_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "real_exam_session_shifts",
                columns: table => new
                {
                    shift_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    exam_session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    shift_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    start_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    end_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    lab_room = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("real_exam_session_shifts_pkey", x => x.shift_id);
                    table.ForeignKey(
                        name: "real_exam_session_shifts_exam_session_id_fkey",
                        column: x => x.exam_session_id,
                        principalTable: "real_exam_sessions",
                        principalColumn: "exam_session_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "real_exam_assignments",
                columns: table => new
                {
                    assignment_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    shift_id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    booth_number = table.Column<int>(type: "integer", nullable: true),
                    attendance_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValueSql: "'NOT_STARTED'::character varying")
                },
                constraints: table =>
                {
                    table.PrimaryKey("real_exam_assignments_pkey", x => x.assignment_id);
                    table.ForeignKey(
                        name: "real_exam_assignments_shift_id_fkey",
                        column: x => x.shift_id,
                        principalTable: "real_exam_session_shifts",
                        principalColumn: "shift_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "real_exam_assignments_student_id_fkey",
                        column: x => x.student_id,
                        principalTable: "users",
                        principalColumn: "user_id");
                });

            migrationBuilder.CreateTable(
                name: "real_exam_results",
                columns: table => new
                {
                    result_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    assignment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValueSql: "'NOT_STARTED'::character varying"),
                    mic_check_passed = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    violation_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    is_flagged = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    ai_score = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    final_score = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    reviewed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    reviewed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_overridden = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    override_reason = table.Column<string>(type: "text", nullable: true),
                    is_locked = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    sha256_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    submitted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    locked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("real_exam_results_pkey", x => x.result_id);
                    table.ForeignKey(
                        name: "real_exam_results_assignment_id_fkey",
                        column: x => x.assignment_id,
                        principalTable: "real_exam_assignments",
                        principalColumn: "assignment_id");
                    table.ForeignKey(
                        name: "real_exam_results_reviewed_by_fkey",
                        column: x => x.reviewed_by,
                        principalTable: "users",
                        principalColumn: "user_id");
                });

            migrationBuilder.CreateTable(
                name: "mock_exam_answers",
                columns: table => new
                {
                    mock_answer_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    mock_session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    question_id = table.Column<Guid>(type: "uuid", nullable: false),
                    followup_question_id = table.Column<Guid>(type: "uuid", nullable: true),
                    parent_answer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    round_index = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    user_transcript = table.Column<string>(type: "text", nullable: true),
                    ai_score = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    ai_feedback = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValueSql: "'PENDING'::character varying")
                },
                constraints: table =>
                {
                    table.PrimaryKey("mock_exam_answers_pkey", x => x.mock_answer_id);
                    table.ForeignKey(
                        name: "mock_exam_answers_mock_session_id_fkey",
                        column: x => x.mock_session_id,
                        principalTable: "mock_exam_sessions",
                        principalColumn: "mock_session_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "mock_exam_answers_parent_answer_id_fkey",
                        column: x => x.parent_answer_id,
                        principalTable: "mock_exam_answers",
                        principalColumn: "mock_answer_id");
                    table.ForeignKey(
                        name: "mock_exam_answers_question_id_fkey",
                        column: x => x.question_id,
                        principalTable: "practice_questions",
                        principalColumn: "question_id");
                });

            migrationBuilder.CreateTable(
                name: "mock_exam_followup_questions",
                columns: table => new
                {
                    followup_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    mock_answer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    mock_session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    round_index = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    question_text = table.Column<string>(type: "text", nullable: false),
                    context_transcript = table.Column<string>(type: "text", nullable: true),
                    parent_practice_question_id = table.Column<Guid>(type: "uuid", nullable: true),
                    model_version = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    prompt_version = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValueSql: "'GENERATED'::character varying"),
                    timeout_sec = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("mock_exam_followup_questions_pkey", x => x.followup_id);
                    table.ForeignKey(
                        name: "mock_exam_followup_questions_mock_answer_id_fkey",
                        column: x => x.mock_answer_id,
                        principalTable: "mock_exam_answers",
                        principalColumn: "mock_answer_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "mock_exam_followup_questions_mock_session_id_fkey",
                        column: x => x.mock_session_id,
                        principalTable: "mock_exam_sessions",
                        principalColumn: "mock_session_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "mock_exam_followup_questions_parent_practice_question_id_fkey",
                        column: x => x.parent_practice_question_id,
                        principalTable: "practice_questions",
                        principalColumn: "question_id");
                });

            migrationBuilder.CreateTable(
                name: "practice_answers",
                columns: table => new
                {
                    answer_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    question_id = table.Column<Guid>(type: "uuid", nullable: false),
                    followup_question_id = table.Column<Guid>(type: "uuid", nullable: true),
                    parent_answer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    user_transcript = table.Column<string>(type: "text", nullable: true),
                    ai_score = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    ai_feedback = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValueSql: "'PENDING'::character varying"),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("practice_answers_pkey", x => x.answer_id);
                    table.ForeignKey(
                        name: "practice_answers_parent_answer_id_fkey",
                        column: x => x.parent_answer_id,
                        principalTable: "practice_answers",
                        principalColumn: "answer_id");
                    table.ForeignKey(
                        name: "practice_answers_question_id_fkey",
                        column: x => x.question_id,
                        principalTable: "practice_questions",
                        principalColumn: "question_id");
                    table.ForeignKey(
                        name: "practice_answers_session_id_fkey",
                        column: x => x.session_id,
                        principalTable: "practice_sessions",
                        principalColumn: "session_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "practice_followup_questions",
                columns: table => new
                {
                    followup_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    practice_answer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    practice_session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    round_index = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    question_text = table.Column<string>(type: "text", nullable: false),
                    trigger_score = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    gap_summary = table.Column<string>(type: "jsonb", nullable: true),
                    model_version = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    prompt_version = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValueSql: "'GENERATED'::character varying"),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("practice_followup_questions_pkey", x => x.followup_id);
                    table.ForeignKey(
                        name: "practice_followup_questions_practice_answer_id_fkey",
                        column: x => x.practice_answer_id,
                        principalTable: "practice_answers",
                        principalColumn: "answer_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "practice_followup_questions_practice_session_id_fkey",
                        column: x => x.practice_session_id,
                        principalTable: "practice_sessions",
                        principalColumn: "session_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "real_exam_answers",
                columns: table => new
                {
                    real_answer_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    result_id = table.Column<Guid>(type: "uuid", nullable: false),
                    question_id = table.Column<Guid>(type: "uuid", nullable: false),
                    followup_question_id = table.Column<Guid>(type: "uuid", nullable: true),
                    parent_answer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    round_index = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    audio_url = table.Column<string>(type: "text", nullable: true),
                    transcript = table.Column<string>(type: "text", nullable: true),
                    timestamps_json = table.Column<string>(type: "jsonb", nullable: true),
                    rubric_snapshot = table.Column<string>(type: "jsonb", nullable: true),
                    ai_evidence = table.Column<string>(type: "text", nullable: true),
                    ai_score = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    final_score = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("real_exam_answers_pkey", x => x.real_answer_id);
                    table.ForeignKey(
                        name: "real_exam_answers_parent_answer_id_fkey",
                        column: x => x.parent_answer_id,
                        principalTable: "real_exam_answers",
                        principalColumn: "real_answer_id");
                    table.ForeignKey(
                        name: "real_exam_answers_question_id_fkey",
                        column: x => x.question_id,
                        principalTable: "exam_questions",
                        principalColumn: "question_id");
                    table.ForeignKey(
                        name: "real_exam_answers_result_id_fkey",
                        column: x => x.result_id,
                        principalTable: "real_exam_results",
                        principalColumn: "result_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "real_exam_followup_questions",
                columns: table => new
                {
                    followup_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    real_answer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    result_id = table.Column<Guid>(type: "uuid", nullable: false),
                    round_index = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    question_text = table.Column<string>(type: "text", nullable: false),
                    context_transcript = table.Column<string>(type: "text", nullable: true),
                    prompt_snapshot = table.Column<string>(type: "text", nullable: false),
                    model_version = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    prompt_version = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValueSql: "'GENERATED'::character varying"),
                    timeout_sec = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("real_exam_followup_questions_pkey", x => x.followup_id);
                    table.ForeignKey(
                        name: "real_exam_followup_questions_real_answer_id_fkey",
                        column: x => x.real_answer_id,
                        principalTable: "real_exam_answers",
                        principalColumn: "real_answer_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "real_exam_followup_questions_result_id_fkey",
                        column: x => x.result_id,
                        principalTable: "real_exam_results",
                        principalColumn: "result_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_created",
                table: "audit_logs",
                column: "created_at",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_target",
                table: "audit_logs",
                columns: new[] { "target_table", "target_id" });

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
                name: "IX_classes_course_id",
                table: "classes",
                column: "course_id");

            migrationBuilder.CreateIndex(
                name: "IX_classes_instructor_id",
                table: "classes",
                column: "instructor_id");

            migrationBuilder.CreateIndex(
                name: "uq_classes_semester_name",
                table: "classes",
                columns: new[] { "semester_id", "class_name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "courses_course_code_key",
                table: "courses",
                column: "course_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_exam_questions_approved_by",
                table: "exam_questions",
                column: "approved_by");

            migrationBuilder.CreateIndex(
                name: "IX_exam_questions_course_id",
                table: "exam_questions",
                column: "course_id");

            migrationBuilder.CreateIndex(
                name: "IX_exam_structures_course_id",
                table: "exam_structures",
                column: "course_id");

            migrationBuilder.CreateIndex(
                name: "ix_mock_answers_session",
                table: "mock_exam_answers",
                column: "mock_session_id");

            migrationBuilder.CreateIndex(
                name: "IX_mock_exam_answers_followup_question_id",
                table: "mock_exam_answers",
                column: "followup_question_id");

            migrationBuilder.CreateIndex(
                name: "IX_mock_exam_answers_parent_answer_id",
                table: "mock_exam_answers",
                column: "parent_answer_id");

            migrationBuilder.CreateIndex(
                name: "IX_mock_exam_answers_question_id",
                table: "mock_exam_answers",
                column: "question_id");

            migrationBuilder.CreateIndex(
                name: "IX_mock_exam_followup_questions_mock_answer_id",
                table: "mock_exam_followup_questions",
                column: "mock_answer_id");

            migrationBuilder.CreateIndex(
                name: "IX_mock_exam_followup_questions_mock_session_id",
                table: "mock_exam_followup_questions",
                column: "mock_session_id");

            migrationBuilder.CreateIndex(
                name: "IX_mock_exam_followup_questions_parent_practice_question_id",
                table: "mock_exam_followup_questions",
                column: "parent_practice_question_id");

            migrationBuilder.CreateIndex(
                name: "IX_mock_exam_quotas_course_id",
                table: "mock_exam_quotas",
                column: "course_id");

            migrationBuilder.CreateIndex(
                name: "uq_mock_exam_quotas",
                table: "mock_exam_quotas",
                columns: new[] { "student_id", "course_id", "quota_date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_mock_exam_sessions_structure_id",
                table: "mock_exam_sessions",
                column: "structure_id");

            migrationBuilder.CreateIndex(
                name: "ix_mock_sessions_student",
                table: "mock_exam_sessions",
                column: "student_id");

            migrationBuilder.CreateIndex(
                name: "IX_practice_answers_followup_question_id",
                table: "practice_answers",
                column: "followup_question_id");

            migrationBuilder.CreateIndex(
                name: "IX_practice_answers_parent_answer_id",
                table: "practice_answers",
                column: "parent_answer_id");

            migrationBuilder.CreateIndex(
                name: "IX_practice_answers_question_id",
                table: "practice_answers",
                column: "question_id");

            migrationBuilder.CreateIndex(
                name: "ix_practice_answers_session",
                table: "practice_answers",
                column: "session_id");

            migrationBuilder.CreateIndex(
                name: "IX_practice_followup_questions_practice_answer_id",
                table: "practice_followup_questions",
                column: "practice_answer_id");

            migrationBuilder.CreateIndex(
                name: "IX_practice_followup_questions_practice_session_id",
                table: "practice_followup_questions",
                column: "practice_session_id");

            migrationBuilder.CreateIndex(
                name: "IX_practice_questions_course_id",
                table: "practice_questions",
                column: "course_id");

            migrationBuilder.CreateIndex(
                name: "ix_practice_sessions_student",
                table: "practice_sessions",
                column: "student_id");

            migrationBuilder.CreateIndex(
                name: "ix_real_answers_result",
                table: "real_exam_answers",
                column: "result_id");

            migrationBuilder.CreateIndex(
                name: "IX_real_exam_answers_followup_question_id",
                table: "real_exam_answers",
                column: "followup_question_id");

            migrationBuilder.CreateIndex(
                name: "IX_real_exam_answers_parent_answer_id",
                table: "real_exam_answers",
                column: "parent_answer_id");

            migrationBuilder.CreateIndex(
                name: "IX_real_exam_answers_question_id",
                table: "real_exam_answers",
                column: "question_id");

            migrationBuilder.CreateIndex(
                name: "IX_real_exam_assignments_student_id",
                table: "real_exam_assignments",
                column: "student_id");

            migrationBuilder.CreateIndex(
                name: "uq_real_exam_assignments",
                table: "real_exam_assignments",
                columns: new[] { "shift_id", "student_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_real_exam_followup_questions_real_answer_id",
                table: "real_exam_followup_questions",
                column: "real_answer_id");

            migrationBuilder.CreateIndex(
                name: "IX_real_exam_followup_questions_result_id",
                table: "real_exam_followup_questions",
                column: "result_id");

            migrationBuilder.CreateIndex(
                name: "IX_real_exam_results_reviewed_by",
                table: "real_exam_results",
                column: "reviewed_by");

            migrationBuilder.CreateIndex(
                name: "real_exam_results_assignment_id_key",
                table: "real_exam_results",
                column: "assignment_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_real_exam_session_shifts_exam_session_id",
                table: "real_exam_session_shifts",
                column: "exam_session_id");

            migrationBuilder.CreateIndex(
                name: "IX_real_exam_sessions_class_id",
                table: "real_exam_sessions",
                column: "class_id");

            migrationBuilder.CreateIndex(
                name: "IX_real_exam_sessions_structure_id",
                table: "real_exam_sessions",
                column: "structure_id");

            migrationBuilder.CreateIndex(
                name: "roles_role_name_key",
                table: "roles",
                column: "role_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_rubric_exam",
                table: "rubric_items",
                column: "exam_question_id");

            migrationBuilder.CreateIndex(
                name: "ix_rubric_practice",
                table: "rubric_items",
                column: "practice_question_id");

            migrationBuilder.CreateIndex(
                name: "semesters_semester_code_key",
                table: "semesters",
                column: "semester_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_role_id",
                table: "users",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "users_email_key",
                table: "users",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "users_username_key",
                table: "users",
                column: "username",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_mock_answers_followup",
                table: "mock_exam_answers",
                column: "followup_question_id",
                principalTable: "mock_exam_followup_questions",
                principalColumn: "followup_id");

            migrationBuilder.AddForeignKey(
                name: "fk_practice_answers_followup",
                table: "practice_answers",
                column: "followup_question_id",
                principalTable: "practice_followup_questions",
                principalColumn: "followup_id");

            migrationBuilder.AddForeignKey(
                name: "fk_real_answers_followup",
                table: "real_exam_answers",
                column: "followup_question_id",
                principalTable: "real_exam_followup_questions",
                principalColumn: "followup_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "classes_instructor_id_fkey",
                table: "classes");

            migrationBuilder.DropForeignKey(
                name: "exam_questions_approved_by_fkey",
                table: "exam_questions");

            migrationBuilder.DropForeignKey(
                name: "mock_exam_sessions_student_id_fkey",
                table: "mock_exam_sessions");

            migrationBuilder.DropForeignKey(
                name: "practice_sessions_student_id_fkey",
                table: "practice_sessions");

            migrationBuilder.DropForeignKey(
                name: "real_exam_assignments_student_id_fkey",
                table: "real_exam_assignments");

            migrationBuilder.DropForeignKey(
                name: "real_exam_results_reviewed_by_fkey",
                table: "real_exam_results");

            migrationBuilder.DropForeignKey(
                name: "real_exam_sessions_class_id_fkey",
                table: "real_exam_sessions");

            migrationBuilder.DropForeignKey(
                name: "exam_questions_course_id_fkey",
                table: "exam_questions");

            migrationBuilder.DropForeignKey(
                name: "exam_structures_course_id_fkey",
                table: "exam_structures");

            migrationBuilder.DropForeignKey(
                name: "practice_questions_course_id_fkey",
                table: "practice_questions");

            migrationBuilder.DropForeignKey(
                name: "fk_mock_answers_followup",
                table: "mock_exam_answers");

            migrationBuilder.DropForeignKey(
                name: "practice_answers_question_id_fkey",
                table: "practice_answers");

            migrationBuilder.DropForeignKey(
                name: "real_exam_sessions_structure_id_fkey",
                table: "real_exam_sessions");

            migrationBuilder.DropForeignKey(
                name: "fk_practice_answers_followup",
                table: "practice_answers");

            migrationBuilder.DropForeignKey(
                name: "fk_real_answers_followup",
                table: "real_exam_answers");

            migrationBuilder.DropTable(
                name: "audit_logs");

            migrationBuilder.DropTable(
                name: "class_enrollments");

            migrationBuilder.DropTable(
                name: "mock_exam_quotas");

            migrationBuilder.DropTable(
                name: "rubric_items");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "roles");

            migrationBuilder.DropTable(
                name: "classes");

            migrationBuilder.DropTable(
                name: "semesters");

            migrationBuilder.DropTable(
                name: "courses");

            migrationBuilder.DropTable(
                name: "mock_exam_followup_questions");

            migrationBuilder.DropTable(
                name: "mock_exam_answers");

            migrationBuilder.DropTable(
                name: "mock_exam_sessions");

            migrationBuilder.DropTable(
                name: "practice_questions");

            migrationBuilder.DropTable(
                name: "exam_structures");

            migrationBuilder.DropTable(
                name: "practice_followup_questions");

            migrationBuilder.DropTable(
                name: "practice_answers");

            migrationBuilder.DropTable(
                name: "practice_sessions");

            migrationBuilder.DropTable(
                name: "real_exam_followup_questions");

            migrationBuilder.DropTable(
                name: "real_exam_answers");

            migrationBuilder.DropTable(
                name: "exam_questions");

            migrationBuilder.DropTable(
                name: "real_exam_results");

            migrationBuilder.DropTable(
                name: "real_exam_assignments");

            migrationBuilder.DropTable(
                name: "real_exam_session_shifts");

            migrationBuilder.DropTable(
                name: "real_exam_sessions");
        }
    }
}
