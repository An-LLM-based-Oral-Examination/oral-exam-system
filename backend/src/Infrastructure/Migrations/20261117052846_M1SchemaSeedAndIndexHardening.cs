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
            migrationBuilder.Sql("""
                DROP INDEX IF EXISTS uq_ai_evaluations_in_class_submission;
                DROP INDEX IF EXISTS uq_ai_evaluations_mock_exam_answer;
                DROP INDEX IF EXISTS uq_ai_evaluations_practice_answer;
                ALTER TABLE practice_answers DROP COLUMN IF EXISTS processing_started_at;
                ALTER TABLE in_class_exam_submissions DROP COLUMN IF EXISTS processing_started_at;
                ALTER TABLE classes ADD COLUMN IF NOT EXISTS join_password_plain character varying(128) NULL;
                CREATE INDEX IF NOT EXISTS ix_ai_evaluations_in_class_submission ON ai_evaluations (in_class_submission_id);
                CREATE INDEX IF NOT EXISTS ix_ai_evaluations_mock_exam_answer ON ai_evaluations (mock_exam_answer_id);
                CREATE INDEX IF NOT EXISTS ix_ai_evaluations_practice_answer ON ai_evaluations (practice_answer_id);
                """);
        }
    }
}
