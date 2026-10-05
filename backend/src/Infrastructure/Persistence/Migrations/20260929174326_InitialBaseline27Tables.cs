using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OralExamination.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialBaseline27Tables : Migration
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
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    password_hash = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    full_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    student_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "student"),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.id);
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
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_courses", x => x.id);
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
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "in_progress")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_practice_sessions", x => x.id);
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
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "scheduled"),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_official_exam_sessions", x => x.id);
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
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_exam_questions", x => x.id);
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
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    submitted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "in_progress"),
                    total_score = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mock_exam_sessions", x => x.id);
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
                    audio_url = table.Column<string>(type: "text", nullable: true),
                    is_follow_up = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    parent_answer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "pending"),
                    submitted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_practice_answers", x => x.id);
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
                    exam_question_id = table.Column<Guid>(type: "uuid", nullable: false),
                    answer_text = table.Column<string>(type: "text", nullable: true),
                    audio_url = table.Column<string>(type: "text", nullable: true),
                    time_taken_seconds = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "answered"),
                    answered_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mock_exam_answers", x => x.id);
                    table.ForeignKey(
                        name: "FK_mock_exam_answers_exam_questions_exam_question_id",
                        column: x => x.exam_question_id,
                        principalTable: "exam_questions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_mock_exam_answers_mock_exam_sessions_session_id",
                        column: x => x.session_id,
                        principalTable: "mock_exam_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
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
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "checked_in"),
                    is_locked = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    checked_in_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    submitted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_student_exam_tickets", x => x.id);
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
                    total_score = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: false),
                    feedback = table.Column<string>(type: "text", nullable: true),
                    confidence_score = table.Column<decimal>(type: "numeric(3,2)", precision: 3, scale: 2, nullable: true),
                    evaluated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_evaluations", x => x.id);
                    table.ForeignKey(
                        name: "FK_ai_evaluations_exam_question_submissions_exam_submission_id",
                        column: x => x.exam_submission_id,
                        principalTable: "exam_question_submissions",
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
                name: "ix_ai_evaluations_practice_answer",
                table: "ai_evaluations",
                column: "practice_answer_id");

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
                column: "exam_question_id");

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
                name: "ix_users_email",
                table: "users",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_users_student_code",
                table: "users",
                column: "student_code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ai_evaluation_details");

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
                name: "mock_exam_answers");

            migrationBuilder.DropTable(
                name: "mock_exam_quotas");

            migrationBuilder.DropTable(
                name: "ai_evaluations");

            migrationBuilder.DropTable(
                name: "classes");

            migrationBuilder.DropTable(
                name: "lecturer_audits");

            migrationBuilder.DropTable(
                name: "rubric_criteria");

            migrationBuilder.DropTable(
                name: "mock_exam_sessions");

            migrationBuilder.DropTable(
                name: "exam_question_submissions");

            migrationBuilder.DropTable(
                name: "practice_answers");

            migrationBuilder.DropTable(
                name: "exam_questions");

            migrationBuilder.DropTable(
                name: "student_exam_tickets");

            migrationBuilder.DropTable(
                name: "practice_questions");

            migrationBuilder.DropTable(
                name: "practice_sessions");

            migrationBuilder.DropTable(
                name: "exam_sets");

            migrationBuilder.DropTable(
                name: "real_exam_session_shifts");

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
