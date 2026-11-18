using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace OralExamination.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class M1SchemaSeedAndIndexHardening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP INDEX IF EXISTS ix_ai_evaluations_in_class_submission;
                DROP INDEX IF EXISTS ix_ai_evaluations_mock_exam_answer;
                DROP INDEX IF EXISTS ix_ai_evaluations_practice_answer;
                DROP INDEX IF EXISTS uq_ai_evaluations_in_class_submission;
                DROP INDEX IF EXISTS uq_ai_evaluations_mock_exam_answer;
                DROP INDEX IF EXISTS uq_ai_evaluations_practice_answer;
                """);

            migrationBuilder.Sql("""
                DO $$
                DECLARE
                    v_item RECORD;
                    v_uid uuid;
                    v_fk RECORD;
                    v_has_ref boolean;
                    v_sample_users uuid[] := ARRAY[
                        '00000000-0000-0000-0000-000000000001'::uuid,
                        '00000000-0000-0000-0000-000000000002'::uuid,
                        '00000000-0000-0000-0000-000000000003'::uuid,
                        '00000000-0000-0000-0000-000000000004'::uuid,
                        '00000000-0000-0000-0000-000000000005'::uuid,
                        '00000000-0000-0000-0000-000000000006'::uuid,
                        '00000000-0000-0000-0000-00000000000b'::uuid,
                        '00000000-0000-0000-0000-00000000000c'::uuid
                    ];
                BEGIN
                    FOR v_item IN
                        SELECT tbl, id_val FROM (VALUES
                            (1,  'appeal_requests',          '00000000-0000-0000-0000-000000000026'::uuid),
                            (2,  'class_enrollments',        '00000000-0000-0000-0000-000000000011'::uuid),
                            (3,  'class_enrollments',        '00000000-0000-0000-0000-000000000012'::uuid),
                            (4,  'student_exam_tickets',     '00000000-0000-0000-0000-000000000024'::uuid),
                            (5,  'student_exam_tickets',     '00000000-0000-0000-0000-000000000025'::uuid),
                            (6,  'real_exam_session_shifts', '00000000-0000-0000-0000-000000000023'::uuid),
                            (7,  'exam_set_questions',       '00000000-0000-0000-0000-000000000021'::uuid),
                            (8,  'practice_questions',       '00000000-0000-0000-0000-000000000017'::uuid),
                            (9,  'practice_questions',       '00000000-0000-0000-0000-000000000101'::uuid),
                            (10, 'practice_questions',       '00000000-0000-0000-0000-000000000102'::uuid),
                            (11, 'practice_questions',       '00000000-0000-0000-0000-000000000103'::uuid),
                            (12, 'practice_questions',       '00000000-0000-0000-0000-000000000104'::uuid),
                            (13, 'practice_questions',       '00000000-0000-0000-0000-000000000105'::uuid),
                            (14, 'exam_questions',           '00000000-0000-0000-0000-000000000020'::uuid),
                            (15, 'rubric_criteria',          '00000000-0000-0000-0000-000000000014'::uuid),
                            (16, 'rubric_criteria',          '00000000-0000-0000-0000-000000000015'::uuid),
                            (17, 'rubric_criteria',          '00000000-0000-0000-0000-000000000016'::uuid),
                            (18, 'classes',                  '00000000-0000-0000-0000-000000000010'::uuid),
                            (19, 'course_clos',              '00000000-0000-0000-0000-000000000061'::uuid),
                            (20, 'course_clos',              '00000000-0000-0000-0000-000000000062'::uuid),
                            (21, 'course_clos',              '00000000-0000-0000-0000-000000000063'::uuid),
                            (22, 'course_clos',              '00000000-0000-0000-0000-000000000064'::uuid),
                            (23, 'course_offerings',         '00000000-0000-0000-0000-000000000011'::uuid),
                            (24, 'course_offerings',         '00000000-0000-0000-0000-000000000012'::uuid),
                            (25, 'exam_sets',                '00000000-0000-0000-0000-000000000019'::uuid),
                            (26, 'exam_sets',                '00000000-0000-0000-0000-00000000001a'::uuid),
                            (27, 'official_exam_sessions',   '00000000-0000-0000-0000-000000000022'::uuid),
                            (28, 'rubrics',                  '00000000-0000-0000-0000-000000000013'::uuid),
                            (29, 'exam_structures',          '00000000-0000-0000-0000-000000000018'::uuid),
                            (30, 'courses',                  '00000000-0000-0000-0000-000000000008'::uuid),
                            (31, 'courses',                  '00000000-0000-0000-0000-000000000009'::uuid),
                            (32, 'academic_programs',        '00000000-0000-0000-0000-000000000010'::uuid),
                            (33, 'semesters',                '00000000-0000-0000-0000-000000000007'::uuid)
                        ) AS t(ord, tbl, id_val)
                        ORDER BY ord
                    LOOP
                        v_has_ref := FALSE;
                        FOR v_fk IN
                            SELECT c.conrelid::regclass AS child_rel, a.attname AS child_col
                            FROM pg_constraint c
                            JOIN pg_attribute a ON a.attrelid = c.conrelid AND a.attnum = c.conkey[1]
                            WHERE c.contype = 'f'
                              AND c.confrelid = to_regclass('public.' || v_item.tbl)
                              AND array_length(c.conkey, 1) = 1
                        LOOP
                            EXECUTE format('SELECT EXISTS (SELECT 1 FROM %s WHERE %I = $1)', v_fk.child_rel, v_fk.child_col)
                                INTO v_has_ref
                                USING v_item.id_val;
                            EXIT WHEN v_has_ref;
                        END LOOP;

                        IF NOT v_has_ref THEN
                            BEGIN
                                EXECUTE format('DELETE FROM public.%I WHERE id = $1', v_item.tbl) USING v_item.id_val;
                            EXCEPTION
                                WHEN foreign_key_violation THEN
                                    NULL;
                            END;
                        END IF;
                    END LOOP;

                    FOREACH v_uid IN ARRAY v_sample_users
                    LOOP
                        v_has_ref := FALSE;
                        FOR v_fk IN
                            SELECT c.conrelid::regclass AS child_rel, a.attname AS child_col
                            FROM pg_constraint c
                            JOIN pg_attribute a ON a.attrelid = c.conrelid AND a.attnum = c.conkey[1]
                            WHERE c.contype = 'f'
                              AND c.confrelid = to_regclass('public.users')
                              AND array_length(c.conkey, 1) = 1
                        LOOP
                            EXECUTE format('SELECT EXISTS (SELECT 1 FROM %s WHERE %I = $1)', v_fk.child_rel, v_fk.child_col)
                                INTO v_has_ref
                                USING v_uid;
                            EXIT WHEN v_has_ref;
                        END LOOP;

                        IF NOT v_has_ref THEN
                            BEGIN
                                EXECUTE 'DELETE FROM public.users WHERE id = $1' USING v_uid;
                            EXCEPTION
                                WHEN foreign_key_violation THEN
                                    v_has_ref := TRUE;
                            END;
                        END IF;

                        IF v_has_ref THEN
                            UPDATE public.users
                            SET password_hash = NULL,
                                must_change_password = TRUE,
                                is_active = FALSE,
                                auth_version = auth_version + 1,
                                updated_at = CURRENT_TIMESTAMP
                            WHERE id = v_uid;
                        END IF;
                    END LOOP;
                END $$;
                """);

            // [EXPAND_CONTRACT: ALLOW_CONTRACT_DROP]
            migrationBuilder.Sql("ALTER TABLE classes DROP COLUMN IF EXISTS join_password_plain;");

            migrationBuilder.Sql("ALTER TABLE practice_answers ADD COLUMN IF NOT EXISTS processing_started_at timestamp with time zone NULL;");
            migrationBuilder.Sql("ALTER TABLE in_class_exam_submissions ADD COLUMN IF NOT EXISTS processing_started_at timestamp with time zone NULL;");

            migrationBuilder.Sql("""
                WITH duplicate_ai_evaluations AS (
                    SELECT id FROM (
                        SELECT id,
                               ROW_NUMBER() OVER (
                                   PARTITION BY practice_answer_id
                                   ORDER BY evaluated_at DESC, id DESC
                               ) AS rn
                        FROM ai_evaluations
                        WHERE practice_answer_id IS NOT NULL
                    ) ranked_practice
                    WHERE rn > 1
                    UNION
                    SELECT id FROM (
                        SELECT id,
                               ROW_NUMBER() OVER (
                                   PARTITION BY mock_exam_answer_id
                                   ORDER BY evaluated_at DESC, id DESC
                               ) AS rn
                        FROM ai_evaluations
                        WHERE mock_exam_answer_id IS NOT NULL
                    ) ranked_mock
                    WHERE rn > 1
                    UNION
                    SELECT id FROM (
                        SELECT id,
                               ROW_NUMBER() OVER (
                                   PARTITION BY in_class_submission_id
                                   ORDER BY evaluated_at DESC, id DESC
                               ) AS rn
                        FROM ai_evaluations
                        WHERE in_class_submission_id IS NOT NULL
                    ) ranked_in_class
                    WHERE rn > 1
                    UNION
                    SELECT id FROM (
                        SELECT id,
                               ROW_NUMBER() OVER (
                                   PARTITION BY exam_submission_id
                                   ORDER BY evaluated_at DESC, id DESC
                               ) AS rn
                        FROM ai_evaluations
                        WHERE exam_submission_id IS NOT NULL
                    ) ranked_exam
                    WHERE rn > 1
                )
                DELETE FROM ai_evaluations
                WHERE id IN (SELECT id FROM duplicate_ai_evaluations);

                CREATE UNIQUE INDEX IF NOT EXISTS uq_ai_evaluations_exam_submission
                    ON ai_evaluations (exam_submission_id)
                    WHERE exam_submission_id IS NOT NULL;
                """);

            migrationBuilder.CreateIndex(
                name: "uq_ai_evaluations_in_class_submission",
                table: "ai_evaluations",
                column: "in_class_submission_id",
                unique: true,
                filter: "in_class_submission_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "uq_ai_evaluations_mock_exam_answer",
                table: "ai_evaluations",
                column: "mock_exam_answer_id",
                unique: true,
                filter: "mock_exam_answer_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "uq_ai_evaluations_practice_answer",
                table: "ai_evaluations",
                column: "practice_answer_id",
                unique: true,
                filter: "practice_answer_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "uq_ai_evaluations_in_class_submission",
                table: "ai_evaluations");

            migrationBuilder.DropIndex(
                name: "uq_ai_evaluations_mock_exam_answer",
                table: "ai_evaluations");

            migrationBuilder.DropIndex(
                name: "uq_ai_evaluations_practice_answer",
                table: "ai_evaluations");

            migrationBuilder.DropColumn(
                name: "processing_started_at",
                table: "practice_answers");

            migrationBuilder.DropColumn(
                name: "processing_started_at",
                table: "in_class_exam_submissions");

            migrationBuilder.AddColumn<string>(
                name: "join_password_plain",
                table: "classes",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.InsertData(
                table: "semesters",
                columns: new[] { "id", "code", "created_at", "end_date", "is_active", "name", "start_date" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000007"), "FA26", new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 12, 31), true, "Fall 2026", new DateOnly(2026, 9, 1) });

            migrationBuilder.InsertData(
                table: "users",
                columns: new[] { "id", "auth_version", "created_at", "email", "full_name", "is_active", "password_hash", "role", "student_code", "updated_at", "username" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000000001"), 1, new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), "admin@fpt.edu.vn", "Nguyen Quang Thanh", true, "pbkdf2-sha256.210000.5K2wrCgwPiV5n+ABUXfgiA==.008qe4tj/RRL8pPAl4EYA+A5+pfxVetYA6GDQh/R0mY=", "admin", null, new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), "admin" },
                    { new Guid("00000000-0000-0000-0000-000000000002"), 1, new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), "headse@fe.edu.vn", "Tran Van Truong", true, "pbkdf2-sha256.210000.HpTuTN7YNAPOdlxcQWqpHA==.ZwGxFXu923/XOLQOpsxyOeQVE5WyKcnaoz26ljezdsM=", "department_head", null, new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), "headse" },
                    { new Guid("00000000-0000-0000-0000-000000000003"), 1, new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), "totnt@fe.edu.vn", "Nguyen Trong Tot", true, "pbkdf2-sha256.210000.NAemlIZmGVkenMK0C4M/nA==.QMwd1s/sMi+q+NaEjyE4zZmnjyaaoiXq/fPgSa/Bxrk=", "lecturer", null, new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), "totnt" },
                    { new Guid("00000000-0000-0000-0000-000000000004"), 1, new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), "haind@fe.edu.vn", "Nguyen Dang Hai", true, "pbkdf2-sha256.210000.XWSrNfAl9MJ4kGLb8pTU3Q==.3Qk6V00ldedgCY86HSofmJWukSF1OjCzPzAphfouT30=", "proctor", null, new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), "haind" }
                });

            migrationBuilder.InsertData(
                table: "users",
                columns: new[] { "id", "auth_version", "created_at", "email", "full_name", "is_active", "must_change_password", "password_hash", "role", "student_code", "updated_at", "username" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000000005"), 1, new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), "hoanglvse170001@fpt.edu.vn", "Le Vu Hoang", true, true, "pbkdf2-sha256.210000.0GiJlqUca2B7Yifgm2O6Fw==.IC/G1GWasnB5RNxlTCiIoHblv2PZ7+4UcrZCloAEpTQ=", "student", "SE170001", new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), "hoalvse170001" },
                    { new Guid("00000000-0000-0000-0000-000000000006"), 1, new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), "maittse170002@fpt.edu.vn", "Tran Thi Mai", true, true, "pbkdf2-sha256.210000.vgos1dwrA89Q/KbEtKvbdQ==.CCAyM2KcWJkAXVkOAIiUHDJUl0wi5L8FZRhnQBXjbZQ=", "student", "SE170002", new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), "maittse170002" }
                });

            migrationBuilder.InsertData(
                table: "users",
                columns: new[] { "id", "auth_version", "created_at", "email", "full_name", "is_active", "password_hash", "role", "student_code", "updated_at", "username" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-00000000000b"), 1, new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), "thanhnqse184236@fpt.edu.vn", "Nguyen Quang Thanh", true, "pbkdf2-sha256.210000./uXLNIRG3H9UiZkYHTLHpg==.uKkapszbetceOex+l2duOGby+Qu8ubMrJ/eqHpM1MeQ=", "student", "SE184236", new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), "thanhnqse184236" },
                    { new Guid("00000000-0000-0000-0000-00000000000c"), 1, new DateTime(2026, 10, 22, 12, 0, 0, 0, DateTimeKind.Utc), "khaothi@fe.edu.vn", "Phòng Khảo Thí FPTU", true, "pbkdf2-sha256.210000.gXk3elR90D2mDzQO6YESxg==.MFGqqSEgh1msSXnIZj554d/MyQvcksQQD0hIZNBiFNw=", "examination_officer", null, new DateTime(2026, 10, 22, 12, 0, 0, 0, DateTimeKind.Utc), "khaothi" }
                });

            migrationBuilder.InsertData(
                table: "academic_programs",
                columns: new[] { "id", "code", "created_at", "department_head_user_id", "is_active", "name", "updated_at" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), "SE", new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("00000000-0000-0000-0000-000000000002"), true, "Software Engineering", new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc) });

            migrationBuilder.InsertData(
                table: "courses",
                columns: new[] { "id", "academic_program_id", "code", "created_at", "credits", "department_head_user_id", "description", "exam_input_mode", "has_follow_up", "is_active", "max_follow_up_questions", "max_mock_exams_per_day", "mock_exam_bloom_distribution", "mock_exam_duration_minutes", "mock_exam_total_questions", "name", "semester_id", "transcript_buffer_seconds" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000000008"), new Guid("00000000-0000-0000-0000-000000000010"), "PRN231", new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), 3, new Guid("00000000-0000-0000-0000-000000000002"), null, "VoiceWithTranscriptEdit", true, true, 2, 3, "{\"Remember\": 1, \"Understand\": 2, \"Apply\": 2}", 30, 5, "Building Cross-Platform Back-End Applications with .NET", new Guid("00000000-0000-0000-0000-000000000007"), 60 },
                    { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000010"), "SWD392", new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), 3, new Guid("00000000-0000-0000-0000-000000000002"), null, "VoiceWithTranscriptEdit", true, true, 2, 3, "{\"Remember\": 1, \"Understand\": 2, \"Apply\": 2}", 30, 5, "Software Architecture and Design", new Guid("00000000-0000-0000-0000-000000000007"), 60 }
                });

            migrationBuilder.InsertData(
                table: "course_clos",
                columns: new[] { "id", "bloom_level", "clo_code", "course_id", "created_at", "description", "is_active", "order_index" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000000061"), "Understand", "CLO1", new Guid("00000000-0000-0000-0000-000000000008"), new DateTime(2026, 10, 22, 12, 0, 0, 0, DateTimeKind.Utc), "Hiểu và áp dụng kiến trúc ASP.NET Core Web API, Middleware, Dependency Injection", true, 1 },
                    { new Guid("00000000-0000-0000-0000-000000000062"), "Apply", "CLO2", new Guid("00000000-0000-0000-0000-000000000008"), new DateTime(2026, 10, 22, 12, 0, 0, 0, DateTimeKind.Utc), "Thiết kế và triển khai RESTful API, DTOs, Validation và xử lý lỗi chuẩn RFC 7807", true, 2 },
                    { new Guid("00000000-0000-0000-0000-000000000063"), "Analyze", "CLO3", new Guid("00000000-0000-0000-0000-000000000008"), new DateTime(2026, 10, 22, 12, 0, 0, 0, DateTimeKind.Utc), "Tích hợp Entity Framework Core, Migration, tối ưu hóa truy vấn và giao dịch cơ sở dữ liệu", true, 3 },
                    { new Guid("00000000-0000-0000-0000-000000000064"), "Evaluate", "CLO4", new Guid("00000000-0000-0000-0000-000000000008"), new DateTime(2026, 10, 22, 12, 0, 0, 0, DateTimeKind.Utc), "Bảo mật hệ thống bằng JWT, OAuth 2.0, phân quyền RBAC và kiểm thử tự động", true, 4 }
                });

            migrationBuilder.InsertData(
                table: "course_offerings",
                columns: new[] { "id", "course_id", "created_at", "is_enabled", "semester_id", "updated_at" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000008"), new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, new Guid("00000000-0000-0000-0000-000000000007"), new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000009"), new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, new Guid("00000000-0000-0000-0000-000000000007"), new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.InsertData(
                table: "exam_structures",
                columns: new[] { "id", "bloom_distribution", "course_id", "created_at", "created_by", "duration_minutes", "is_active", "name", "required_set_count", "slot_definitions", "total_questions" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000018"), "{\"Understand\": 2, \"Apply\": 2, \"Analyze\": 1}", new Guid("00000000-0000-0000-0000-000000000008"), new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("00000000-0000-0000-0000-000000000003"), 30, true, "Cấu trúc Đề thi Vấn đáp Cuối kỳ PRN231 (FA26)", 1, "[]", 5 });

            migrationBuilder.InsertData(
                table: "rubrics",
                columns: new[] { "id", "course_id", "created_at", "description", "is_active", "name", "total_max_score" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000013"), new Guid("00000000-0000-0000-0000-000000000008"), new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Đánh giá Clean Architecture", true, "Barem Đánh Giá Kiến Trúc .NET 8", 10.00m });

            migrationBuilder.InsertData(
                table: "classes",
                columns: new[] { "id", "allow_self_enrollment", "code", "course_id", "course_offering_id", "created_at", "is_active", "is_join_enabled", "join_code", "join_password_hash", "join_password_plain", "lecturer_id", "max_students", "name", "semester_id" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), true, "SE1801-NET", new Guid("00000000-0000-0000-0000-000000000008"), new Guid("00000000-0000-0000-0000-000000000011"), new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, true, "SE1801NET", null, null, new Guid("00000000-0000-0000-0000-000000000003"), 35, "Lớp SE1801-NET", new Guid("00000000-0000-0000-0000-000000000007") });

            migrationBuilder.InsertData(
                table: "exam_questions",
                columns: new[] { "id", "approval_status", "approved_by", "bloom_level", "clo_id", "content", "course_id", "created_at", "difficulty", "image_description", "image_url", "is_active", "key_points", "review_notes", "rubric_id", "sample_answer", "source", "structure_id", "submitted_by", "title" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000020"), "APPROVED", new Guid("00000000-0000-0000-0000-000000000002"), "Analyze", null, "Phân biệt sự khác nhau giữa BackgroundService và IHostedService trong .NET 8 khi xây dựng hàng đợi xử lý chấm điểm ngầm?", new Guid("00000000-0000-0000-0000-000000000008"), new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), "hard", null, null, true, "[\"IHostedService là interface cốt lõi\",\"BackgroundService là abstract class kế thừa IHostedService\",\"ExecuteAsync chạy Task bất đồng bộ\"]", null, new Guid("00000000-0000-0000-0000-000000000013"), "IHostedService là interface nền tảng định nghĩa StartAsync và StopAsync. BackgroundService là một abstract class kế thừa IHostedService và triển khai sẵn ExecuteAsync chạy nền vòng lặp an toàn.", "manual", null, null, "Phân biệt BackgroundService và IHostedService trong .NET 8" });

            migrationBuilder.InsertData(
                table: "exam_sets",
                columns: new[] { "id", "approved_at", "approved_by", "course_id", "created_by", "generated_at", "handed_over_at", "is_active", "review_notes", "session_id", "set_code", "status", "structure_id", "submitted_at" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000000019"), null, null, new Guid("00000000-0000-0000-0000-000000000008"), null, new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, null, null, "PRN231-SET01", "OFFICIAL", new Guid("00000000-0000-0000-0000-000000000018"), null },
                    { new Guid("00000000-0000-0000-0000-00000000001a"), null, null, new Guid("00000000-0000-0000-0000-000000000008"), null, new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, null, null, "PRN231-SET02", "OFFICIAL", new Guid("00000000-0000-0000-0000-000000000018"), null }
                });

            migrationBuilder.InsertData(
                table: "official_exam_sessions",
                columns: new[] { "id", "course_id", "created_at", "created_by", "exam_date", "exam_input_mode", "exam_structure_id", "has_follow_up", "max_follow_up_questions", "semester_id", "status", "title", "transcript_buffer_seconds" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000022"), new Guid("00000000-0000-0000-0000-000000000008"), new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, new DateOnly(2026, 10, 15), "VoiceWithTranscriptEdit", new Guid("00000000-0000-0000-0000-000000000018"), true, 2, new Guid("00000000-0000-0000-0000-000000000007"), "scheduled", "Vấn đáp Cuối kỳ PRN231 — Ca 1 Sáng", 60 });

            migrationBuilder.InsertData(
                table: "practice_questions",
                columns: new[] { "id", "approval_status", "approved_by", "bloom_level", "clo_id", "content", "course_id", "created_at", "difficulty", "follow_up_prompt", "has_follow_up", "image_description", "image_url", "is_active", "key_points", "review_notes", "rubric_id", "sample_answer", "source", "submitted_by", "title" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000000017"), "APPROVED", null, "Apply", null, "Hãy giải thích nguyên lý Dependency Inversion trong Clean Architecture .NET 8.", new Guid("00000000-0000-0000-0000-000000000008"), new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), "medium", "Nếu muốn thay đổi cơ sở dữ liệu từ PostgreSQL sang SQL Server, bạn cần thay đổi những tầng nào và tại sao?", true, null, null, true, "[\"Module cấp cao không phụ thuộc module cấp thấp\",\"Phụ thuộc vào Abstraction/Interface\"]", null, new Guid("00000000-0000-0000-0000-000000000013"), "Module cấp cao phụ thuộc abstraction. Application khai báo interface, Infrastructure hiện thực.", "manual", null, "Nguyên lý Dependency Inversion" },
                    { new Guid("00000000-0000-0000-0000-000000000101"), "APPROVED", null, "Understand", null, "Tầng Domain trong Clean Architecture chứa những thành phần nào và tại sao không được tham chiếu tới các tầng khác?", new Guid("00000000-0000-0000-0000-000000000008"), new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), "easy", "Làm thế nào để tầng Domain giao tiếp với các dịch vụ bên ngoài mà không phá vỡ tính độc lập?", true, null, null, true, "[\"Chứa Entities và quy tắc cốt lõi\",\"Không phụ thuộc Infrastructure hay UI\"]", null, new Guid("00000000-0000-0000-0000-000000000013"), "Tầng Domain chứa Entities, Value Objects, Domain Events và Enums cốt lõi. Không phụ thuộc vào framework hay database để bảo toàn quy tắc nghiệp vụ.", "manual", null, "Vai trò tầng Domain trong Clean Architecture" },
                    { new Guid("00000000-0000-0000-0000-000000000102"), "APPROVED", null, "Understand", null, "Hãy trình bày sự khác biệt giữa Command và Query khi triển khai mẫu CQRS với thư viện MediatR trong .NET 8.", new Guid("00000000-0000-0000-0000-000000000008"), new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), "easy", "Khi nào nên sử dụng AsNoTracking() trong các Query Handler?", true, null, null, true, "[\"Command thay đổi trạng thái\",\"Query chỉ đọc dữ liệu\",\"Tách biệt mô hình đọc và ghi\"]", null, new Guid("00000000-0000-0000-0000-000000000013"), "Command dùng để thay đổi trạng thái dữ liệu (Create, Update, Delete), trong khi Query chỉ dùng để truy vấn đọc dữ liệu và không làm biến đổi trạng thái.", "manual", null, "Phân biệt Command và Query trong mẫu CQRS" },
                    { new Guid("00000000-0000-0000-0000-000000000103"), "APPROVED", null, "Apply", null, "Cách thức hoạt động của IPipelineBehavior trong MediatR kết hợp với FluentValidation để kiểm tra dữ liệu đầu vào trước khi vào Handler?", new Guid("00000000-0000-0000-0000-000000000008"), new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), "medium", "Ưu điểm của việc đặt validation ở tầng Application thay vì chỉ đặt ở Controller là gì?", true, null, null, true, "[\"Chặn trước khi vào Handler\",\"Chạy tất cả IValidator\",\"Trả lỗi chuẩn RFC 7807\"]", null, new Guid("00000000-0000-0000-0000-000000000013"), "ValidationBehavior chặn request trước khi tới Handler, chạy toàn bộ IValidator<TRequest> đã đăng ký và ném ValidationException nếu có lỗi.", "manual", null, "Cơ chế Pipeline Behavior với FluentValidation" },
                    { new Guid("00000000-0000-0000-0000-000000000104"), "APPROVED", null, "Analyze", null, "Phân tích cách sử dụng BoundedChannel kết hợp BackgroundService và Polly Retry để xử lý chấm điểm bất đồng bộ chịu tải cao.", new Guid("00000000-0000-0000-0000-000000000008"), new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), "hard", "Tại sao phải tạo IServiceScope mới bên trong BackgroundService mỗi khi xử lý một item?", true, null, null, true, "[\"BoundedChannel chống tràn RAM\",\"BackgroundService xử lý nền\",\"Polly Retry và DLQ bảo toàn dữ liệu\"]", null, new Guid("00000000-0000-0000-0000-000000000013"), "BoundedChannel giới hạn số lượng slot trong RAM để áp dụng backpressure (FullMode.Wait). BackgroundService đọc tuần tự và dùng Polly retry, nếu vẫn lỗi thì lưu vào bảng dead_letter_queues.", "manual", null, "Thiết kế hàng đợi BoundedChannel và xử lý Dead-Letter Queue" },
                    { new Guid("00000000-0000-0000-0000-000000000105"), "APPROVED", null, "Analyze", null, "Phân tích nguyên nhân gây ra lỗi N+1 Query trong Entity Framework Core và các giải pháp khắc phục hiệu quả.", new Guid("00000000-0000-0000-0000-000000000008"), new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), "hard", "Khi nào nên dùng AsSplitQuery() khi sử dụng nhiều lệnh Include?", true, null, null, true, "[\"Nguyên nhân do truy vấn trong vòng lặp\",\"Dùng Include/ThenInclude\",\"Dùng Select Projection và AsNoTracking\"]", null, new Guid("00000000-0000-0000-0000-000000000013"), "Lỗi N+1 xảy ra khi truy vấn danh sách thực thể cha rồi lặp qua từng phần tử để truy vấn thực thể con. Khắc phục bằng Eager Loading (Include/ThenInclude) hoặc Projection (Select).", "manual", null, "Tối ưu hóa truy vấn EF Core 8 và phòng chống lỗi N+1 Query" }
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
                table: "class_enrollments",
                columns: new[] { "id", "class_id", "enrolled_at", "join_method", "status", "student_id" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000010"), new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), "assigned", "enrolled", new Guid("00000000-0000-0000-0000-000000000005") },
                    { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000010"), new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), "assigned", "enrolled", new Guid("00000000-0000-0000-0000-000000000006") }
                });

            migrationBuilder.InsertData(
                table: "exam_set_questions",
                columns: new[] { "id", "exam_question_id", "exam_set_id", "order_index" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000021"), new Guid("00000000-0000-0000-0000-000000000020"), new Guid("00000000-0000-0000-0000-000000000019"), 1 });

            migrationBuilder.InsertData(
                table: "real_exam_session_shifts",
                columns: new[] { "id", "created_at", "end_time", "examiner_user_id", "max_seats", "proctor_user_id", "published_at", "room_lab", "session_id", "shift_name", "start_time", "status" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000023"), new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 10, 15, 2, 30, 0, 0, DateTimeKind.Utc), null, 24, new Guid("00000000-0000-0000-0000-000000000004"), null, "Lab 301", new Guid("00000000-0000-0000-0000-000000000022"), "Kíp 1 (08h00 - 09h30)", new DateTime(2026, 10, 15, 1, 0, 0, 0, DateTimeKind.Utc), "scheduled" });

            migrationBuilder.InsertData(
                table: "student_exam_tickets",
                columns: new[] { "id", "audio_hash_sha256", "audio_object_key", "audio_r2_url", "audio_verified_at", "checked_in_at", "exam_set_id", "grades_published_at", "ip_address", "seat_number", "shift_id", "status", "student_acknowledgement_status", "student_id", "submitted_at" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000000024"), null, null, null, null, new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("00000000-0000-0000-0000-000000000019"), null, "192.168.1.101", 1, new Guid("00000000-0000-0000-0000-000000000023"), "SCHEDULED", "PENDING", new Guid("00000000-0000-0000-0000-000000000005"), null },
                    { new Guid("00000000-0000-0000-0000-000000000025"), null, null, null, null, new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("00000000-0000-0000-0000-00000000001a"), null, "192.168.1.102", 2, new Guid("00000000-0000-0000-0000-000000000023"), "SCHEDULED", "PENDING", new Guid("00000000-0000-0000-0000-000000000006"), null }
                });

            migrationBuilder.InsertData(
                table: "appeal_requests",
                columns: new[] { "id", "assigned_to", "created_at", "decision", "original_score", "proposed_score", "reason", "resolved_at", "review_notes", "reviewed_by", "session_id", "status", "student_id", "submission_id", "ticket_id", "updated_at" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000026"), new Guid("00000000-0000-0000-0000-000000000003"), new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 7.50m, null, "Em xin phúc khảo phần phát biểu về nguyên lý Dependency Inversion bị ồn mic", null, null, null, new Guid("00000000-0000-0000-0000-000000000022"), "SUBMITTED_TO_EXAM_OFFICE", new Guid("00000000-0000-0000-0000-000000000005"), null, new Guid("00000000-0000-0000-0000-000000000024"), new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc) });

            migrationBuilder.CreateIndex(
                name: "ix_ai_evaluations_in_class_submission",
                table: "ai_evaluations",
                column: "in_class_submission_id");

            migrationBuilder.CreateIndex(
                name: "ix_ai_evaluations_mock_exam_answer",
                table: "ai_evaluations",
                column: "mock_exam_answer_id");

            migrationBuilder.CreateIndex(
                name: "ix_ai_evaluations_practice_answer",
                table: "ai_evaluations",
                column: "practice_answer_id");
        }
    }
}
