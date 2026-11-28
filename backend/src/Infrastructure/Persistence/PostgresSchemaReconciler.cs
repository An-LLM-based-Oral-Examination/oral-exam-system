using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace OralExamination.Infrastructure.Persistence;

/// <summary>
/// Bộ đối soát & tự phục hồi lược đồ PostgreSQL (Idempotent Schema Reconciler).
/// Bù đắp hoàn toàn các bảng, cột, khóa ngoại, index và check constraint cho toàn bộ 41 thực thể / 38 bảng CSDL,
/// đồng thời đảm bảo tính nhất quán tuyệt đối cho cả môi trường tạo mới (Fresh DB) lẫn môi trường VPS Production
/// đã chạy một phần migration hoặc khởi tạo qua raw SQL.
/// </summary>
public static class PostgresSchemaReconciler
{
    /// <summary>
    /// SQL idempotent cho bước Pre-Migration (chạy trước MigrateAsync trên DB đã có bảng users).
    /// Bọc các ràng buộc rủi ro bằng subtransaction PL/pgSQL để không bao giờ abort khối lệnh nếu bảng tham chiếu chưa tồn tại.
    /// </summary>
    public static string GetPreMigrationSql() =>
"""
DO $reconcile_pre$
        BEGIN
            IF NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'users') THEN
                RETURN;
            END IF;

            -- 1. users: mở rộng ck_users_role cho đủ 6 roles RBAC và các cột bắt buộc
            BEGIN
                ALTER TABLE users DROP CONSTRAINT IF EXISTS ck_users_role;
                ALTER TABLE users ADD CONSTRAINT ck_users_role
                    CHECK (role IN ('admin', 'department_head', 'examination_officer', 'lecturer', 'proctor', 'student'));
            EXCEPTION WHEN OTHERS THEN NULL;
            END;

            ALTER TABLE users ADD COLUMN IF NOT EXISTS must_change_password boolean NOT NULL DEFAULT FALSE;
            ALTER TABLE users ADD COLUMN IF NOT EXISTS username character varying(50) NULL;
            IF EXISTS (
                SELECT 1 FROM information_schema.columns
                WHERE table_schema = 'public' AND table_name = 'users' AND column_name = 'username'
                  AND character_maximum_length IS NOT NULL AND character_maximum_length < 50
            ) THEN
                ALTER TABLE users ALTER COLUMN username TYPE character varying(50);
            END IF;
            ALTER TABLE users ADD COLUMN IF NOT EXISTS student_code character varying(20) NULL;
            ALTER TABLE users ADD COLUMN IF NOT EXISTS password_hash character varying(256) NULL;
            ALTER TABLE users ADD COLUMN IF NOT EXISTS auth_version integer NOT NULL DEFAULT 1;
            CREATE UNIQUE INDEX IF NOT EXISTS ix_users_username ON users (username) WHERE username IS NOT NULL;
            CREATE UNIQUE INDEX IF NOT EXISTS ix_users_student_code ON users (student_code) WHERE student_code IS NOT NULL;
            CREATE UNIQUE INDEX IF NOT EXISTS ix_users_email_lower ON users (lower(email));
            CREATE UNIQUE INDEX IF NOT EXISTS ix_users_username_lower ON users (lower(username)) WHERE username IS NOT NULL;
            CREATE UNIQUE INDEX IF NOT EXISTS ix_users_student_code_lower ON users (lower(student_code)) WHERE student_code IS NOT NULL;

            -- 2. courses: các cột bổ sung trong đợt R1-R8 (trước academic_program_id)
            IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'courses') THEN
                ALTER TABLE courses ADD COLUMN IF NOT EXISTS description text NULL;
                ALTER TABLE courses ADD COLUMN IF NOT EXISTS department_head_user_id uuid NULL;
                ALTER TABLE courses ADD COLUMN IF NOT EXISTS academic_program_id uuid NULL;
                ALTER TABLE courses ADD COLUMN IF NOT EXISTS mock_exam_duration_minutes integer NOT NULL DEFAULT 15;
                ALTER TABLE courses ADD COLUMN IF NOT EXISTS mock_exam_total_questions integer NOT NULL DEFAULT 3;
                ALTER TABLE courses ADD COLUMN IF NOT EXISTS mock_exam_difficulty_distribution text NULL;
                ALTER TABLE courses ADD COLUMN IF NOT EXISTS mock_exam_bloom_distribution jsonb NOT NULL DEFAULT jsonb_build_object('Remember', 1, 'Understand', 2, 'Apply', 2);

                CREATE INDEX IF NOT EXISTS ix_courses_department_head ON courses (department_head_user_id);
                CREATE INDEX IF NOT EXISTS ix_courses_academic_program ON courses (academic_program_id);

                BEGIN
                    ALTER TABLE courses ADD CONSTRAINT "FK_courses_users_department_head_user_id"
                        FOREIGN KEY (department_head_user_id) REFERENCES users(id) ON DELETE RESTRICT;
                EXCEPTION WHEN OTHERS THEN NULL;
                END;
                BEGIN
                    ALTER TABLE courses DROP CONSTRAINT IF EXISTS ck_courses_mock_duration;
                    ALTER TABLE courses ADD CONSTRAINT ck_courses_mock_duration CHECK (mock_exam_duration_minutes > 0);
                EXCEPTION WHEN OTHERS THEN NULL;
                END;
                BEGIN
                    ALTER TABLE courses DROP CONSTRAINT IF EXISTS ck_courses_mock_questions;
                    ALTER TABLE courses ADD CONSTRAINT ck_courses_mock_questions CHECK (mock_exam_total_questions > 0);
                EXCEPTION WHEN OTHERS THEN NULL;
                END;
            END IF;

            -- 3. course_lecturers (phiên bản trước 20261105073054 chưa có course_offering_id)
            CREATE TABLE IF NOT EXISTS course_lecturers (
                id uuid NOT NULL DEFAULT gen_random_uuid(),
                course_id uuid NOT NULL,
                course_offering_id uuid NULL,
                lecturer_user_id uuid NOT NULL,
                assigned_by_user_id uuid NOT NULL,
                semester_id uuid NULL,
                assigned_at timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
                is_active boolean NOT NULL DEFAULT TRUE,
                created_at timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
                updated_at timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
                CONSTRAINT "PK_course_lecturers" PRIMARY KEY (id)
            );

            CREATE INDEX IF NOT EXISTS ix_course_lecturers_course ON course_lecturers (course_id, is_active);
            CREATE INDEX IF NOT EXISTS ix_course_lecturers_lecturer ON course_lecturers (lecturer_user_id, is_active);
            CREATE UNIQUE INDEX IF NOT EXISTS uq_course_lecturers ON course_lecturers (course_id, lecturer_user_id, semester_id);
            CREATE UNIQUE INDEX IF NOT EXISTS uq_course_offering_lecturers ON course_lecturers (course_offering_id, lecturer_user_id);

            BEGIN
                ALTER TABLE course_lecturers ADD CONSTRAINT "FK_course_lecturers_courses_course_id"
                    FOREIGN KEY (course_id) REFERENCES courses(id) ON DELETE CASCADE;
            EXCEPTION WHEN OTHERS THEN NULL;
            END;
            BEGIN
                ALTER TABLE course_lecturers ADD CONSTRAINT "FK_course_lecturers_users_lecturer_user_id"
                    FOREIGN KEY (lecturer_user_id) REFERENCES users(id) ON DELETE CASCADE;
            EXCEPTION WHEN OTHERS THEN NULL;
            END;
            BEGIN
                ALTER TABLE course_lecturers ADD CONSTRAINT "FK_course_lecturers_users_assigned_by_user_id"
                    FOREIGN KEY (assigned_by_user_id) REFERENCES users(id) ON DELETE RESTRICT;
            EXCEPTION WHEN OTHERS THEN NULL;
            END;
            BEGIN
                ALTER TABLE course_lecturers ADD CONSTRAINT "FK_course_lecturers_semesters_semester_id"
                    FOREIGN KEY (semester_id) REFERENCES semesters(id) ON DELETE RESTRICT;
            EXCEPTION WHEN OTHERS THEN NULL;
            END;

            -- 4. classes: các cột bổ sung trước 20261105073054 và 20261107111940
            IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'classes') THEN
                ALTER TABLE classes ADD COLUMN IF NOT EXISTS name character varying(150) NOT NULL DEFAULT '';
                ALTER TABLE classes ADD COLUMN IF NOT EXISTS join_code character varying(20) NULL;
                BEGIN
                    EXECUTE $repair_join_codes$UPDATE classes SET join_code = UPPER('CLS' || RIGHT(REPLACE(id::text, '-', ''), 16)) WHERE join_code IS NULL OR BTRIM(join_code) = ''$repair_join_codes$;
                    ALTER TABLE classes ALTER COLUMN join_code SET NOT NULL;
                    CREATE UNIQUE INDEX IF NOT EXISTS ix_classes_join_code ON classes (join_code);
                EXCEPTION WHEN OTHERS THEN NULL;
                END;
                ALTER TABLE classes ADD COLUMN IF NOT EXISTS join_password_hash character varying(256) NULL;
                ALTER TABLE classes ADD COLUMN IF NOT EXISTS join_password_protected character varying(1024) NULL;
                ALTER TABLE classes ADD COLUMN IF NOT EXISTS is_join_enabled boolean NOT NULL DEFAULT TRUE;
                ALTER TABLE classes ADD COLUMN IF NOT EXISTS allow_self_enrollment boolean NOT NULL DEFAULT TRUE;
                ALTER TABLE classes ADD COLUMN IF NOT EXISTS course_offering_id uuid NULL;
                CREATE INDEX IF NOT EXISTS ix_classes_course_offering ON classes (course_offering_id);
                CREATE INDEX IF NOT EXISTS ix_classes_active_semester_course_created ON classes (is_active, semester_id, course_id, created_at);
            END IF;

            -- 5. class_enrollments
            IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'class_enrollments') THEN
                ALTER TABLE class_enrollments ADD COLUMN IF NOT EXISTS joined_via_code boolean NOT NULL DEFAULT FALSE;
                ALTER TABLE class_enrollments ADD COLUMN IF NOT EXISTS join_method character varying(30) NOT NULL DEFAULT 'assigned';
                ALTER TABLE class_enrollments ADD COLUMN IF NOT EXISTS status character varying(20) NOT NULL DEFAULT 'enrolled';
            END IF;

            -- 6. practice_questions & exam_questions
            IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'practice_questions') THEN
                ALTER TABLE practice_questions ADD COLUMN IF NOT EXISTS clo_id uuid NULL;
                ALTER TABLE practice_questions ADD COLUMN IF NOT EXISTS approval_status character varying(30) NOT NULL DEFAULT 'APPROVED';
                ALTER TABLE practice_questions ADD COLUMN IF NOT EXISTS approved_by uuid NULL;
                ALTER TABLE practice_questions ADD COLUMN IF NOT EXISTS approved_at timestamp with time zone NULL;
                ALTER TABLE practice_questions ADD COLUMN IF NOT EXISTS is_deleted boolean NOT NULL DEFAULT FALSE;
                ALTER TABLE practice_questions ADD COLUMN IF NOT EXISTS review_notes text NULL;
                ALTER TABLE practice_questions ADD COLUMN IF NOT EXISTS source character varying(50) NOT NULL DEFAULT 'manual';
                ALTER TABLE practice_questions ADD COLUMN IF NOT EXISTS submitted_by uuid NULL;
                CREATE INDEX IF NOT EXISTS "IX_practice_questions_clo_id" ON practice_questions (clo_id);
                CREATE INDEX IF NOT EXISTS ix_practice_questions_clo ON practice_questions (clo_id);
                BEGIN
                    ALTER TABLE practice_questions ADD CONSTRAINT "FK_practice_questions_course_clos_clo_id"
                        FOREIGN KEY (clo_id) REFERENCES course_clos(id) ON DELETE RESTRICT;
                EXCEPTION WHEN OTHERS THEN NULL;
                END;
            END IF;

            IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'exam_questions') THEN
                ALTER TABLE exam_questions ADD COLUMN IF NOT EXISTS clo_id uuid NULL;
                ALTER TABLE exam_questions ADD COLUMN IF NOT EXISTS structure_id uuid NULL;
                ALTER TABLE exam_questions ADD COLUMN IF NOT EXISTS approval_status character varying(30) NOT NULL DEFAULT 'DRAFT';
                CREATE INDEX IF NOT EXISTS "IX_exam_questions_clo_id" ON exam_questions (clo_id);
                CREATE INDEX IF NOT EXISTS ix_exam_questions_clo ON exam_questions (clo_id);
                CREATE INDEX IF NOT EXISTS "IX_exam_questions_structure_id" ON exam_questions (structure_id);
                CREATE INDEX IF NOT EXISTS ix_exam_questions_structure ON exam_questions (structure_id);
                BEGIN
                    ALTER TABLE exam_questions ADD CONSTRAINT "FK_exam_questions_course_clos_clo_id"
                        FOREIGN KEY (clo_id) REFERENCES course_clos(id) ON DELETE RESTRICT;
                EXCEPTION WHEN OTHERS THEN NULL;
                END;
                BEGIN
                    ALTER TABLE exam_questions ADD CONSTRAINT "FK_exam_questions_exam_structures_structure_id"
                        FOREIGN KEY (structure_id) REFERENCES exam_structures(id) ON DELETE SET NULL;
                EXCEPTION WHEN OTHERS THEN NULL;
                END;
            END IF;

            -- 7. practice_sessions & practice_answers
            IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'practice_sessions') THEN
                ALTER TABLE practice_sessions ADD COLUMN IF NOT EXISTS total_score numeric(4,2) NULL;
                ALTER TABLE practice_sessions ADD COLUMN IF NOT EXISTS selected_question_ids jsonb NOT NULL DEFAULT '[]'::jsonb;
            END IF;

            IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'practice_answers') THEN
                ALTER TABLE practice_answers ADD COLUMN IF NOT EXISTS order_index integer NOT NULL DEFAULT 1;
                ALTER TABLE practice_answers ADD COLUMN IF NOT EXISTS prompt_text text NULL;
                ALTER TABLE practice_answers ADD COLUMN IF NOT EXISTS processing_started_at timestamp with time zone NULL;
            END IF;

            IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'in_class_exam_submissions') THEN
                ALTER TABLE in_class_exam_submissions ADD COLUMN IF NOT EXISTS processing_started_at timestamp with time zone NULL;
            END IF;

            -- 8. mock_exam_answers
            IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'mock_exam_answers') THEN
                ALTER TABLE mock_exam_answers ADD COLUMN IF NOT EXISTS initial_voice_transcript text NULL;
                ALTER TABLE mock_exam_answers ADD COLUMN IF NOT EXISTS is_follow_up boolean NOT NULL DEFAULT FALSE;
                ALTER TABLE mock_exam_answers ADD COLUMN IF NOT EXISTS parent_answer_id uuid NULL;
                ALTER TABLE mock_exam_answers ADD COLUMN IF NOT EXISTS prompt_text text NULL;
                ALTER TABLE mock_exam_answers ADD COLUMN IF NOT EXISTS order_index integer NOT NULL DEFAULT 1;

                CREATE INDEX IF NOT EXISTS ix_mock_exam_answers_parent ON mock_exam_answers (parent_answer_id);
                BEGIN
                    ALTER TABLE mock_exam_answers ADD CONSTRAINT "FK_mock_exam_answers_mock_exam_answers_parent_answer_id"
                        FOREIGN KEY (parent_answer_id) REFERENCES mock_exam_answers(id) ON DELETE SET NULL;
                EXCEPTION WHEN OTHERS THEN NULL;
                END;
            END IF;

            -- 9. exam_sets, exam_structures & lecturer_audit_details
            IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'exam_sets') THEN
                ALTER TABLE exam_sets ADD COLUMN IF NOT EXISTS session_id uuid NULL;
                ALTER TABLE exam_sets ADD COLUMN IF NOT EXISTS created_by uuid NULL;
                ALTER TABLE exam_sets ADD COLUMN IF NOT EXISTS handed_over_at timestamp with time zone NULL;
                ALTER TABLE exam_sets ADD COLUMN IF NOT EXISTS approved_at timestamp with time zone NULL;
                ALTER TABLE exam_sets ADD COLUMN IF NOT EXISTS approved_by uuid NULL;
                ALTER TABLE exam_sets ADD COLUMN IF NOT EXISTS submitted_at timestamp with time zone NULL;
                ALTER TABLE exam_sets ADD COLUMN IF NOT EXISTS status character varying(30) NOT NULL DEFAULT 'DRAFT';
                ALTER TABLE exam_sets ADD COLUMN IF NOT EXISTS review_notes text NULL;
                ALTER TABLE exam_sets ADD COLUMN IF NOT EXISTS is_sample_set boolean NOT NULL DEFAULT FALSE;

                CREATE INDEX IF NOT EXISTS ix_exam_sets_session ON exam_sets (session_id);
                CREATE INDEX IF NOT EXISTS ix_exam_sets_created_by ON exam_sets (created_by);
                CREATE INDEX IF NOT EXISTS ix_exam_sets_approved_by ON exam_sets (approved_by);

                BEGIN
                    ALTER TABLE exam_sets ADD CONSTRAINT "FK_exam_sets_official_exam_sessions_session_id"
                        FOREIGN KEY (session_id) REFERENCES official_exam_sessions(id) ON DELETE SET NULL;
                EXCEPTION WHEN OTHERS THEN NULL;
                END;
                BEGIN
                    ALTER TABLE exam_sets ADD CONSTRAINT "FK_exam_sets_users_created_by"
                        FOREIGN KEY (created_by) REFERENCES users(id) ON DELETE SET NULL;
                EXCEPTION WHEN OTHERS THEN NULL;
                END;
                BEGIN
                    ALTER TABLE exam_sets ADD CONSTRAINT "FK_exam_sets_users_approved_by"
                        FOREIGN KEY (approved_by) REFERENCES users(id) ON DELETE SET NULL;
                EXCEPTION WHEN OTHERS THEN NULL;
                END;
            END IF;

            IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'exam_structures') THEN
                ALTER TABLE exam_structures ADD COLUMN IF NOT EXISTS required_set_count integer NOT NULL DEFAULT 1;
                ALTER TABLE exam_structures ADD COLUMN IF NOT EXISTS slot_definitions jsonb NOT NULL DEFAULT '[]'::jsonb;
            END IF;

            IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'lecturer_audit_details') THEN
                ALTER TABLE lecturer_audit_details ADD COLUMN IF NOT EXISTS submission_id uuid NULL;
                CREATE INDEX IF NOT EXISTS ix_lecturer_audit_details_submission ON lecturer_audit_details (submission_id);
                BEGIN
                    ALTER TABLE lecturer_audit_details ADD CONSTRAINT "FK_lecturer_audit_details_exam_question_submissions_submission_id"
                        FOREIGN KEY (submission_id) REFERENCES exam_question_submissions(id) ON DELETE SET NULL;
                EXCEPTION WHEN OTHERS THEN NULL;
                END;
            END IF;

            -- 10. ai_evaluations
            IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'ai_evaluations') THEN
                ALTER TABLE ai_evaluations ADD COLUMN IF NOT EXISTS student_id uuid NULL;
                ALTER TABLE ai_evaluations ADD COLUMN IF NOT EXISTS in_class_submission_id uuid NULL;
                ALTER TABLE ai_evaluations ADD COLUMN IF NOT EXISTS follow_up_prompt text NULL;

                CREATE INDEX IF NOT EXISTS ix_ai_evaluations_student ON ai_evaluations (student_id);
                BEGIN
                    DROP INDEX IF EXISTS ix_ai_evaluations_practice_answer;
                    DROP INDEX IF EXISTS ix_ai_evaluations_mock_exam_answer;
                    DROP INDEX IF EXISTS ix_ai_evaluations_in_class_submission;
                    CREATE UNIQUE INDEX IF NOT EXISTS uq_ai_evaluations_practice_answer ON ai_evaluations (practice_answer_id) WHERE practice_answer_id IS NOT NULL;
                    CREATE UNIQUE INDEX IF NOT EXISTS uq_ai_evaluations_mock_exam_answer ON ai_evaluations (mock_exam_answer_id) WHERE mock_exam_answer_id IS NOT NULL;
                    CREATE UNIQUE INDEX IF NOT EXISTS uq_ai_evaluations_in_class_submission ON ai_evaluations (in_class_submission_id) WHERE in_class_submission_id IS NOT NULL;
                EXCEPTION WHEN OTHERS THEN NULL;
                END;

                BEGIN
                    ALTER TABLE ai_evaluations ADD CONSTRAINT "FK_ai_evaluations_users_student_id"
                        FOREIGN KEY (student_id) REFERENCES users(id) ON DELETE CASCADE;
                EXCEPTION WHEN OTHERS THEN NULL;
                END;
                BEGIN
                    ALTER TABLE ai_evaluations ADD CONSTRAINT "FK_ai_evaluations_in_class_exam_submissions_in_class_submission_id"
                        FOREIGN KEY (in_class_submission_id) REFERENCES in_class_exam_submissions(id) ON DELETE CASCADE;
                EXCEPTION WHEN OTHERS THEN NULL;
                END;
                BEGIN
                    ALTER TABLE ai_evaluations DROP CONSTRAINT IF EXISTS ck_ai_evaluations_type;
                    ALTER TABLE ai_evaluations ADD CONSTRAINT ck_ai_evaluations_type
                        CHECK (answer_type IN ('practice', 'mock_exam', 'exam', 'in_class'));
                EXCEPTION WHEN OTHERS THEN NULL;
                END;
                BEGIN
                    EXECUTE 'UPDATE ai_evaluations SET answer_type = ''mock_exam'' WHERE answer_type = ''practice'' AND mock_exam_answer_id IS NOT NULL AND practice_answer_id IS NULL';
                    EXECUTE 'UPDATE ai_evaluations SET answer_type = ''practice'' WHERE answer_type != ''practice'' AND practice_answer_id IS NOT NULL AND mock_exam_answer_id IS NULL AND exam_submission_id IS NULL AND in_class_submission_id IS NULL';
                    EXECUTE 'UPDATE ai_evaluations SET answer_type = ''exam'' WHERE answer_type != ''exam'' AND exam_submission_id IS NOT NULL AND practice_answer_id IS NULL AND mock_exam_answer_id IS NULL AND in_class_submission_id IS NULL';
                    EXECUTE 'UPDATE ai_evaluations SET answer_type = ''in_class'' WHERE answer_type != ''in_class'' AND in_class_submission_id IS NOT NULL AND practice_answer_id IS NULL AND mock_exam_answer_id IS NULL AND exam_submission_id IS NULL';
                EXCEPTION WHEN OTHERS THEN NULL;
                END;
                BEGIN
                    ALTER TABLE ai_evaluations DROP CONSTRAINT IF EXISTS ck_ai_evaluations_xor;
                    ALTER TABLE ai_evaluations ADD CONSTRAINT ck_ai_evaluations_xor
                        CHECK ((answer_type = 'practice' AND practice_answer_id IS NOT NULL AND mock_exam_answer_id IS NULL AND exam_submission_id IS NULL AND in_class_submission_id IS NULL) OR
                               (answer_type = 'mock_exam' AND mock_exam_answer_id IS NOT NULL AND practice_answer_id IS NULL AND exam_submission_id IS NULL AND in_class_submission_id IS NULL) OR
                               (answer_type = 'exam' AND exam_submission_id IS NOT NULL AND practice_answer_id IS NULL AND mock_exam_answer_id IS NULL AND in_class_submission_id IS NULL) OR
                               (answer_type = 'in_class' AND in_class_submission_id IS NOT NULL AND practice_answer_id IS NULL AND mock_exam_answer_id IS NULL AND exam_submission_id IS NULL));
                EXCEPTION WHEN OTHERS THEN NULL;
                END;
            END IF;

            -- 11. real_exam_session_shifts
            IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'real_exam_session_shifts') THEN
                ALTER TABLE real_exam_session_shifts ADD COLUMN IF NOT EXISTS examiner_user_id uuid NULL;
                ALTER TABLE real_exam_session_shifts ADD COLUMN IF NOT EXISTS is_exam_published boolean NOT NULL DEFAULT FALSE;
                ALTER TABLE real_exam_session_shifts ADD COLUMN IF NOT EXISTS published_at timestamp with time zone NULL;
                ALTER TABLE real_exam_session_shifts ADD COLUMN IF NOT EXISTS max_seats integer NOT NULL DEFAULT 24;
                IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'student_exam_tickets') THEN
                    EXECUTE 'UPDATE real_exam_session_shifts AS shift SET max_seats = GREATEST(24, COALESCE((SELECT MAX(ticket.seat_number) FROM student_exam_tickets AS ticket WHERE ticket.shift_id = shift.id), 0))';
                END IF;
                BEGIN
                    ALTER TABLE real_exam_session_shifts ADD CONSTRAINT ck_shifts_max_seats
                        CHECK (max_seats >= 1 AND max_seats <= 40);
                EXCEPTION WHEN duplicate_object THEN NULL;
                END;
                BEGIN
                    ALTER TABLE real_exam_session_shifts ADD CONSTRAINT "FK_real_exam_session_shifts_users_examiner_user_id"
                        FOREIGN KEY (examiner_user_id) REFERENCES users(id) ON DELETE SET NULL;
                EXCEPTION WHEN OTHERS THEN NULL;
                END;
            END IF;

            -- 12. dead_letter_queues
            IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'dead_letter_queues') THEN
                ALTER TABLE dead_letter_queues ADD COLUMN IF NOT EXISTS flow_type character varying(30) NOT NULL DEFAULT 'OFFICIAL_EXAM';
                ALTER TABLE dead_letter_queues ALTER COLUMN flow_type SET DEFAULT 'OFFICIAL_EXAM';
                ALTER TABLE dead_letter_queues ADD COLUMN IF NOT EXISTS managed_by_role character varying(30) NOT NULL DEFAULT 'examination_officer';
                ALTER TABLE dead_letter_queues ADD COLUMN IF NOT EXISTS resolved_by_user_id uuid NULL;
                ALTER TABLE dead_letter_queues ADD COLUMN IF NOT EXISTS resolution_notes text NULL;

                BEGIN
                    EXECUTE 'UPDATE dead_letter_queues SET flow_type = ''PRACTICE'' WHERE flow_type = ''MF01_PRACTICE''';
                    EXECUTE 'UPDATE dead_letter_queues SET flow_type = ''MOCK_EXAM'' WHERE flow_type = ''MF02_MOCK_EXAM''';
                    EXECUTE 'UPDATE dead_letter_queues SET flow_type = ''OFFICIAL_EXAM'' WHERE flow_type = ''MF04_OFFICIAL_EXAM''';
                    EXECUTE 'UPDATE dead_letter_queues SET flow_type = ''IN_CLASS_EXAM'' WHERE flow_type = ''MF05_IN_CLASS''';
                EXCEPTION WHEN OTHERS THEN NULL;
                END;

                CREATE INDEX IF NOT EXISTS ix_dlq_role_flow_status ON dead_letter_queues (managed_by_role, flow_type, status);
                CREATE INDEX IF NOT EXISTS ix_dlq_flow_status ON dead_letter_queues (flow_type, status);
                CREATE INDEX IF NOT EXISTS ix_dlq_managed_role ON dead_letter_queues (managed_by_role, status);

                BEGIN
                    ALTER TABLE dead_letter_queues DROP CONSTRAINT IF EXISTS ck_dlq_flow_type;
                    ALTER TABLE dead_letter_queues ADD CONSTRAINT ck_dlq_flow_type
                        CHECK (flow_type IN ('PRACTICE', 'MOCK_EXAM', 'OFFICIAL_EXAM', 'IN_CLASS_EXAM'));
                EXCEPTION WHEN OTHERS THEN NULL;
                END;
                BEGIN
                    ALTER TABLE dead_letter_queues DROP CONSTRAINT IF EXISTS ck_dlq_managed_by_role;
                    ALTER TABLE dead_letter_queues DROP CONSTRAINT IF EXISTS ck_dlq_managed_role;
                    ALTER TABLE dead_letter_queues ADD CONSTRAINT ck_dlq_managed_role
                        CHECK (managed_by_role IN ('admin', 'examination_officer', 'lecturer'));
                EXCEPTION WHEN OTHERS THEN NULL;
                END;
                BEGIN
                    ALTER TABLE dead_letter_queues ADD CONSTRAINT "FK_dead_letter_queues_users_resolved_by_user_id"
                        FOREIGN KEY (resolved_by_user_id) REFERENCES users(id) ON DELETE SET NULL;
                EXCEPTION WHEN OTHERS THEN NULL;
                END;
            END IF;
        END
        $reconcile_pre$;
""";

    /// <summary>
    /// SQL idempotent toàn diện chạy ở bước 20261108141117_UpdateDB và sau khi MigrateAsync hoàn tất (Stage 3).
    /// Đảm bảo 100% 41 bảng, cột, khóa ngoại, check constraint và tài khoản mẫu khớp tuyệt đối với OralExamDbContext.
    /// Toàn bộ ràng buộc được bọc an toàn trong subtransaction PL/pgSQL và đồng bộ lịch sử 11 migrations.
    /// </summary>
    public static string GetPostMigrationSql() =>
        GetPreMigrationSql() +
"""

        DO $reconcile_post$
        BEGIN
            -----------------------------------------------------------------------
            -- 1. TẠO TOÀN BỘ 41 BẢNG BẮT BUỘC (IDEMPOTENT TABLE CREATION)
            -----------------------------------------------------------------------

            -- Table: academic_programs
            CREATE TABLE IF NOT EXISTS academic_programs (
                id uuid NOT NULL DEFAULT gen_random_uuid(),
                code character varying(20) NOT NULL,
                created_at timestamp with time zone NULL DEFAULT CURRENT_TIMESTAMP,
                department_head_user_id uuid NULL,
                is_active boolean NULL,
                name character varying(200) NOT NULL,
                updated_at timestamp with time zone NULL DEFAULT CURRENT_TIMESTAMP,
                CONSTRAINT "PK_academic_programs" PRIMARY KEY (id)
            );

            -- Table: ai_evaluation_details
            CREATE TABLE IF NOT EXISTS ai_evaluation_details (
                id uuid NOT NULL DEFAULT gen_random_uuid(),
                comment text NULL,
                criterion_id uuid NULL,
                evaluation_id uuid NULL,
                score numeric(4,2) NULL,
                CONSTRAINT "PK_ai_evaluation_details" PRIMARY KEY (id)
            );

            -- Table: ai_evaluations
            CREATE TABLE IF NOT EXISTS ai_evaluations (
                id uuid NOT NULL DEFAULT gen_random_uuid(),
                answer_type character varying(20) NOT NULL,
                confidence_score numeric(3,2) NULL,
                cot_trace text NULL,
                evaluated_at timestamp with time zone NULL DEFAULT CURRENT_TIMESTAMP,
                exam_submission_id uuid NULL,
                feedback text NULL,
                follow_up_prompt text NULL,
                in_class_submission_id uuid NULL,
                mock_exam_answer_id uuid NULL,
                practice_answer_id uuid NULL,
                student_id uuid NULL,
                total_score numeric(4,2) NULL,
                CONSTRAINT "PK_ai_evaluations" PRIMARY KEY (id)
            );

            -- Table: appeal_requests
            CREATE TABLE IF NOT EXISTS appeal_requests (
                id uuid NOT NULL DEFAULT gen_random_uuid(),
                assigned_to uuid NULL,
                created_at timestamp with time zone NULL DEFAULT CURRENT_TIMESTAMP,
                decision character varying(50) NULL,
                original_score numeric(4,2) NULL,
                proposed_score numeric(4,2) NULL,
                reason text NOT NULL,
                resolved_at timestamp with time zone NULL,
                review_notes text NULL,
                reviewed_by uuid NULL,
                session_id uuid NULL,
                status character varying(40) NOT NULL,
                student_id uuid NULL,
                submission_id uuid NULL,
                ticket_id uuid NULL,
                updated_at timestamp with time zone NULL DEFAULT CURRENT_TIMESTAMP,
                CONSTRAINT "PK_appeal_requests" PRIMARY KEY (id)
            );

            -- Table: audit_logs
            CREATE TABLE IF NOT EXISTS audit_logs (
                id uuid NOT NULL DEFAULT gen_random_uuid(),
                action character varying(50) NOT NULL,
                created_at timestamp with time zone NULL DEFAULT CURRENT_TIMESTAMP,
                entity_id uuid NULL,
                entity_name character varying(100) NOT NULL,
                ip_address character varying(45) NULL,
                new_values jsonb NULL,
                old_values jsonb NULL,
                user_id uuid NULL,
                CONSTRAINT "PK_audit_logs" PRIMARY KEY (id)
            );

            -- Table: class_enrollments
            CREATE TABLE IF NOT EXISTS class_enrollments (
                id uuid NOT NULL DEFAULT gen_random_uuid(),
                class_id uuid NULL,
                enrolled_at timestamp with time zone NULL DEFAULT CURRENT_TIMESTAMP,
                join_method character varying(30) NOT NULL,
                joined_via_code boolean NULL,
                status character varying(20) NOT NULL,
                student_id uuid NULL,
                CONSTRAINT "PK_class_enrollments" PRIMARY KEY (id)
            );

            -- Table: classes
            CREATE TABLE IF NOT EXISTS classes (
                id uuid NOT NULL DEFAULT gen_random_uuid(),
                allow_self_enrollment boolean NULL,
                code character varying(50) NOT NULL,
                course_id uuid NULL,
                course_offering_id uuid NULL,
                created_at timestamp with time zone NULL DEFAULT CURRENT_TIMESTAMP,
                is_active boolean NULL,
                is_join_enabled boolean NULL,
                join_code character varying(20) NOT NULL,
                join_password_hash character varying(256) NULL,
                join_password_protected character varying(1024) NULL,
                lecturer_id uuid NULL,
                max_students integer NULL,
                name character varying(150) NOT NULL,
                semester_id uuid NULL,
                CONSTRAINT "PK_classes" PRIMARY KEY (id)
            );

            -- Table: course_clos
            CREATE TABLE IF NOT EXISTS course_clos (
                id uuid NOT NULL DEFAULT gen_random_uuid(),
                bloom_level character varying(20) NOT NULL,
                clo_code character varying(50) NOT NULL,
                course_id uuid NULL,
                created_at timestamp with time zone NULL DEFAULT CURRENT_TIMESTAMP,
                description text NOT NULL,
                is_active boolean NULL,
                order_index integer NULL,
                CONSTRAINT "PK_course_clos" PRIMARY KEY (id)
            );

            -- Table: course_lecturers
            CREATE TABLE IF NOT EXISTS course_lecturers (
                id uuid NOT NULL DEFAULT gen_random_uuid(),
                assigned_at timestamp with time zone NULL DEFAULT CURRENT_TIMESTAMP,
                assigned_by_user_id uuid NULL,
                course_id uuid NULL,
                course_offering_id uuid NULL,
                created_at timestamp with time zone NULL DEFAULT CURRENT_TIMESTAMP,
                is_active boolean NULL,
                lecturer_user_id uuid NULL,
                semester_id uuid NULL,
                updated_at timestamp with time zone NULL DEFAULT CURRENT_TIMESTAMP,
                CONSTRAINT "PK_course_lecturers" PRIMARY KEY (id)
            );

            -- Table: course_offerings
            CREATE TABLE IF NOT EXISTS course_offerings (
                id uuid NOT NULL DEFAULT gen_random_uuid(),
                course_id uuid NULL,
                created_at timestamp with time zone NULL DEFAULT CURRENT_TIMESTAMP,
                is_enabled boolean NULL,
                semester_id uuid NULL,
                updated_at timestamp with time zone NULL DEFAULT CURRENT_TIMESTAMP,
                CONSTRAINT "PK_course_offerings" PRIMARY KEY (id)
            );

            -- Table: courses
            CREATE TABLE IF NOT EXISTS courses (
                id uuid NOT NULL DEFAULT gen_random_uuid(),
                academic_program_id uuid NULL,
                code character varying(20) NOT NULL,
                created_at timestamp with time zone NULL DEFAULT CURRENT_TIMESTAMP,
                credits integer NULL,
                department_head_user_id uuid NULL,
                description text NULL,
                exam_input_mode character varying(30) NOT NULL,
                has_follow_up boolean NULL,
                is_active boolean NULL,
                max_follow_up_questions integer NULL,
                max_mock_exams_per_day integer NULL,
                mock_exam_bloom_distribution jsonb NOT NULL,
                mock_exam_duration_minutes integer NULL,
                mock_exam_total_questions integer NULL,
                name character varying(200) NOT NULL,
                semester_id uuid NULL,
                transcript_buffer_seconds integer NULL,
                CONSTRAINT "PK_courses" PRIMARY KEY (id)
            );

            -- Table: dead_letter_queues
            CREATE TABLE IF NOT EXISTS dead_letter_queues (
                id uuid NOT NULL DEFAULT gen_random_uuid(),
                correlation_id uuid NULL,
                created_at timestamp with time zone NULL DEFAULT CURRENT_TIMESTAMP,
                error_message text NOT NULL,
                flow_type character varying(30) NOT NULL,
                last_attempt_at timestamp with time zone NULL,
                managed_by_role character varying(30) NOT NULL,
                next_retry_at timestamp with time zone NULL,
                payload_json jsonb NOT NULL,
                resolution_notes text NULL,
                resolved_at timestamp with time zone NULL,
                resolved_by_user_id uuid NULL,
                retry_count integer NULL,
                status character varying(20) NOT NULL,
                task_type character varying(50) NOT NULL,
                CONSTRAINT "PK_dead_letter_queues" PRIMARY KEY (id)
            );

            -- Table: exam_anti_cheat_logs
            CREATE TABLE IF NOT EXISTS exam_anti_cheat_logs (
                id uuid NOT NULL DEFAULT gen_random_uuid(),
                client_event_id character varying(128) NULL,
                details text NULL,
                event_type character varying(50) NOT NULL,
                occurred_at timestamp with time zone NULL DEFAULT CURRENT_TIMESTAMP,
                severity character varying(20) NOT NULL,
                shift_id uuid NULL,
                student_id uuid NULL,
                ticket_id uuid NULL,
                violation_count integer NULL,
                CONSTRAINT "PK_exam_anti_cheat_logs" PRIMARY KEY (id)
            );

            -- Table: exam_question_submissions
            CREATE TABLE IF NOT EXISTS exam_question_submissions (
                id uuid NOT NULL DEFAULT gen_random_uuid(),
                ai_score numeric(4,2) NULL,
                audio_hash_sha256 character varying(64) NULL,
                audio_object_key character varying(512) NULL,
                audio_r2_url text NULL,
                audio_verified_at timestamp with time zone NULL,
                exam_question_id uuid NULL,
                final_score numeric(4,2) NULL,
                follow_up_index integer NOT NULL DEFAULT 0,
                follow_up_prompt text NULL,
                generated_follow_up_prompt text NULL,
                grading_started_at timestamp with time zone NULL,
                grading_status character varying(20) NOT NULL,
                initial_voice_transcript text NULL,
                is_follow_up boolean NOT NULL DEFAULT FALSE,
                is_suspicious boolean NULL,
                parent_submission_id uuid NULL,
                submitted_at timestamp with time zone NULL DEFAULT CURRENT_TIMESTAMP,
                suspicious_reason text NULL,
                ticket_id uuid NULL,
                time_spent_seconds integer NULL,
                timestamps_whisper jsonb NULL,
                transcript_edit_ratio numeric(5,4) NULL,
                transcript_whisper text NULL,
                CONSTRAINT "PK_exam_question_submissions" PRIMARY KEY (id)
            );

            -- Table: exam_questions
            CREATE TABLE IF NOT EXISTS exam_questions (
                id uuid NOT NULL DEFAULT gen_random_uuid(),
                approval_status character varying(30) NOT NULL,
                approved_by uuid NULL,
                bank_curation_status character varying(30) NOT NULL DEFAULT 'BANK_READY',
                bloom_level character varying(20) NOT NULL,
                clo_id uuid NULL,
                content text NOT NULL,
                course_id uuid NULL,
                created_at timestamp with time zone NULL DEFAULT CURRENT_TIMESTAMP,
                curated_at timestamp with time zone NULL,
                curated_by uuid NULL,
                difficulty character varying(20) NOT NULL,
                image_description text NULL,
                image_url character varying(1000) NULL,
                is_active boolean NULL,
                is_deleted boolean NOT NULL DEFAULT FALSE,
                key_points jsonb NOT NULL DEFAULT '[]'::jsonb,
                review_notes text NULL,
                rubric_id uuid NULL,
                sample_answer text NULL,
                source character varying(50) NOT NULL,
                structure_id uuid NULL,
                submitted_by uuid NULL,
                title character varying(300) NOT NULL,
                CONSTRAINT "PK_exam_questions" PRIMARY KEY (id)
            );

            -- Table: exam_set_questions
            CREATE TABLE IF NOT EXISTS exam_set_questions (
                id uuid NOT NULL DEFAULT gen_random_uuid(),
                exam_question_id uuid NULL,
                exam_set_id uuid NULL,
                order_index integer NULL,
                CONSTRAINT "PK_exam_set_questions" PRIMARY KEY (id)
            );

            -- Table: exam_sets
            CREATE TABLE IF NOT EXISTS exam_sets (
                id uuid NOT NULL DEFAULT gen_random_uuid(),
                approved_at timestamp with time zone NULL,
                approved_by uuid NULL,
                course_id uuid NULL,
                created_by uuid NULL,
                generated_at timestamp with time zone NULL DEFAULT CURRENT_TIMESTAMP,
                handed_over_at timestamp with time zone NULL,
                is_active boolean NULL,
                is_sample_set boolean NULL,
                review_notes text NULL,
                session_id uuid NULL,
                set_code character varying(50) NOT NULL,
                status character varying(30) NOT NULL,
                structure_id uuid NULL,
                submitted_at timestamp with time zone NULL,
                CONSTRAINT "PK_exam_sets" PRIMARY KEY (id)
            );

            -- Table: exam_structures
            CREATE TABLE IF NOT EXISTS exam_structures (
                id uuid NOT NULL DEFAULT gen_random_uuid(),
                bloom_distribution jsonb NOT NULL DEFAULT jsonb_build_object(),
                course_id uuid NULL,
                created_at timestamp with time zone NULL DEFAULT CURRENT_TIMESTAMP,
                created_by uuid NULL,
                duration_minutes integer NULL,
                is_active boolean NULL,
                name character varying(200) NOT NULL,
                required_set_count integer NULL,
                slot_definitions jsonb NOT NULL DEFAULT '[]'::jsonb,
                total_questions integer NULL,
                CONSTRAINT "PK_exam_structures" PRIMARY KEY (id)
            );

            -- Table: grading_jobs
            CREATE TABLE IF NOT EXISTS grading_jobs (
                id uuid NOT NULL DEFAULT gen_random_uuid(),
                attempt_count integer NULL,
                completed_at timestamp with time zone NULL,
                created_at timestamp with time zone NULL DEFAULT CURRENT_TIMESTAMP,
                last_error text NULL,
                locked_until timestamp with time zone NULL,
                next_attempt_at timestamp with time zone NULL,
                status character varying(20) NOT NULL,
                submission_id uuid NULL,
                updated_at timestamp with time zone NULL DEFAULT CURRENT_TIMESTAMP,
                CONSTRAINT "PK_grading_jobs" PRIMARY KEY (id)
            );

            -- Table: in_class_exam_shifts
            CREATE TABLE IF NOT EXISTS in_class_exam_shifts (
                id uuid NOT NULL DEFAULT gen_random_uuid(),
                class_id uuid NULL,
                created_at timestamp with time zone NULL DEFAULT CURRENT_TIMESTAMP,
                description text NULL,
                end_time timestamp with time zone NULL,
                exam_duration_minutes integer NULL,
                finalized_at timestamp with time zone NULL,
                has_follow_up boolean NULL,
                is_auto_publish_grades boolean NULL,
                is_locked boolean NULL,
                lecturer_id uuid NULL,
                max_follow_up_questions integer NULL,
                question_pool_mode character varying(30) NOT NULL,
                selected_clo_ids jsonb NOT NULL DEFAULT '[]'::jsonb,
                selected_difficulties jsonb NOT NULL DEFAULT '["easy","medium","hard"]'::jsonb,
                selected_question_ids jsonb NOT NULL DEFAULT '[]'::jsonb,
                shift_name character varying(255) NOT NULL,
                start_time timestamp with time zone NULL,
                status character varying(20) NOT NULL,
                total_questions_per_student integer NULL,
                totp_secret_key character varying(128) NOT NULL,
                updated_at timestamp with time zone NULL,
                CONSTRAINT "PK_in_class_exam_shifts" PRIMARY KEY (id)
            );

            -- Table: in_class_exam_submissions
            CREATE TABLE IF NOT EXISTS in_class_exam_submissions (
                id uuid NOT NULL DEFAULT gen_random_uuid(),
                ai_confidence_score numeric(3,2) NULL,
                ai_feedback text NULL,
                ai_score numeric(4,2) NULL,
                cot_trace text NULL,
                final_score numeric(4,2) NULL,
                follow_up_index integer NULL,
                follow_up_prompt text NULL,
                graded_at timestamp with time zone NULL,
                is_follow_up boolean NULL,
                lecturer_notes text NULL,
                parent_submission_id uuid NULL,
                processing_started_at timestamp with time zone NULL,
                question_id uuid NULL,
                shift_id uuid NULL,
                status character varying(20) NOT NULL,
                student_id uuid NULL,
                submitted_at timestamp with time zone NULL DEFAULT CURRENT_TIMESTAMP,
                transcript text NOT NULL,
                CONSTRAINT "PK_in_class_exam_submissions" PRIMARY KEY (id)
            );

            -- Table: in_class_shift_participants
            CREATE TABLE IF NOT EXISTS in_class_shift_participants (
                id uuid NOT NULL DEFAULT gen_random_uuid(),
                assigned_question_id uuid NULL,
                assigned_question_ids jsonb NOT NULL DEFAULT '[]'::jsonb,
                joined_at timestamp with time zone NULL DEFAULT CURRENT_TIMESTAMP,
                finished_at timestamp with time zone NULL,
                shift_id uuid NULL,
                status character varying(20) NOT NULL,
                student_id uuid NULL,
                total_ai_score numeric(4,2) NULL,
                total_final_score numeric(4,2) NULL,
                CONSTRAINT "PK_in_class_shift_participants" PRIMARY KEY (id)
            );

            -- Table: in_class_shift_passwords
            CREATE TABLE IF NOT EXISTS in_class_shift_passwords (
                id uuid NOT NULL DEFAULT gen_random_uuid(),
                created_at timestamp with time zone NULL DEFAULT CURRENT_TIMESTAMP,
                password_hash character varying(255) NOT NULL,
                plain_code character varying(10) NOT NULL,
                shift_id uuid NULL,
                valid_from timestamp with time zone NULL,
                valid_to timestamp with time zone NULL,
                grace_until timestamp with time zone NULL,
                CONSTRAINT "PK_in_class_shift_passwords" PRIMARY KEY (id)
            );

            -- Table: lecturer_audit_details
            CREATE TABLE IF NOT EXISTS lecturer_audit_details (
                id uuid NOT NULL DEFAULT gen_random_uuid(),
                ai_score numeric(4,2) NULL,
                audit_id uuid NULL,
                audited_score numeric(4,2) NULL,
                criterion_id uuid NULL,
                lecturer_comment text NULL,
                submission_id uuid NULL,
                CONSTRAINT "PK_lecturer_audit_details" PRIMARY KEY (id)
            );

            -- Table: lecturer_audits
            CREATE TABLE IF NOT EXISTS lecturer_audits (
                id uuid NOT NULL DEFAULT gen_random_uuid(),
                audited_at timestamp with time zone NULL DEFAULT CURRENT_TIMESTAMP,
                audited_score numeric(4,2) NULL,
                is_locked boolean NULL,
                lecturer_id uuid NULL,
                original_ai_score numeric(4,2) NULL,
                override_reason text NOT NULL,
                ticket_id uuid NULL,
                CONSTRAINT "PK_lecturer_audits" PRIMARY KEY (id)
            );

            -- Table: mock_exam_answers
            CREATE TABLE IF NOT EXISTS mock_exam_answers (
                id uuid NOT NULL DEFAULT gen_random_uuid(),
                answer_text text NULL,
                answered_at timestamp with time zone NULL DEFAULT CURRENT_TIMESTAMP,
                initial_voice_transcript text NULL,
                is_follow_up boolean NULL,
                order_index integer NULL,
                parent_answer_id uuid NULL,
                prompt_text text NULL,
                question_id uuid NULL,
                session_id uuid NULL,
                status character varying(20) NOT NULL,
                time_taken_seconds integer NULL,
                CONSTRAINT "PK_mock_exam_answers" PRIMARY KEY (id)
            );

            -- Table: mock_exam_quotas
            CREATE TABLE IF NOT EXISTS mock_exam_quotas (
                id uuid NOT NULL DEFAULT gen_random_uuid(),
                course_id uuid NULL,
                quota_date date NULL,
                student_id uuid NULL,
                updated_at timestamp with time zone NULL DEFAULT CURRENT_TIMESTAMP,
                used_count integer NULL,
                CONSTRAINT "PK_mock_exam_quotas" PRIMARY KEY (id)
            );

            -- Table: mock_exam_sessions
            CREATE TABLE IF NOT EXISTS mock_exam_sessions (
                id uuid NOT NULL DEFAULT gen_random_uuid(),
                course_id uuid NULL,
                exam_set_id uuid NULL,
                expires_at timestamp with time zone NULL,
                has_follow_up boolean NULL,
                paused_at timestamp with time zone NULL,
                started_at timestamp with time zone NULL DEFAULT CURRENT_TIMESTAMP,
                status character varying(20) NOT NULL,
                student_id uuid NULL,
                submitted_at timestamp with time zone NULL,
                total_score numeric(4,2) NULL,
                CONSTRAINT "PK_mock_exam_sessions" PRIMARY KEY (id)
            );

            -- Table: notifications
            CREATE TABLE IF NOT EXISTS notifications (
                id uuid NOT NULL DEFAULT gen_random_uuid(),
                created_at timestamp with time zone NULL DEFAULT CURRENT_TIMESTAMP,
                is_read boolean NULL,
                message text NOT NULL,
                metadata_json text NULL,
                read_at timestamp with time zone NULL,
                related_entity_id uuid NULL,
                related_entity_name character varying(100) NULL,
                title character varying(255) NOT NULL,
                type character varying(50) NOT NULL,
                user_id uuid NULL,
                CONSTRAINT "PK_notifications" PRIMARY KEY (id)
            );

            -- Table: official_exam_sessions
            CREATE TABLE IF NOT EXISTS official_exam_sessions (
                id uuid NOT NULL DEFAULT gen_random_uuid(),
                course_id uuid NULL,
                semester_id uuid NULL,
                created_at timestamp with time zone NULL DEFAULT CURRENT_TIMESTAMP,
                created_by uuid NULL,
                exam_attempt_type character varying(10) NOT NULL DEFAULT 'FE',
                exam_date date NULL,
                exam_input_mode character varying(30) NOT NULL,
                exam_structure_id uuid NULL,
                has_follow_up boolean NULL,
                max_follow_up_questions integer NULL DEFAULT 2,
                status character varying(20) NOT NULL,
                title character varying(255) NOT NULL,
                transcript_buffer_seconds integer NULL,
                CONSTRAINT "PK_official_exam_sessions" PRIMARY KEY (id)
            );

            -- Table: official_exam_candidates
            CREATE TABLE IF NOT EXISTS official_exam_candidates (
                id uuid NOT NULL DEFAULT gen_random_uuid(),
                session_id uuid NOT NULL,
                student_id uuid NOT NULL,
                imported_by uuid NULL,
                imported_at timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
                CONSTRAINT "PK_official_exam_candidates" PRIMARY KEY (id)
            );

            -- Table: practice_answers
            CREATE TABLE IF NOT EXISTS practice_answers (
                id uuid NOT NULL DEFAULT gen_random_uuid(),
                answer_text text NOT NULL,
                is_follow_up boolean NULL,
                order_index integer NULL,
                parent_answer_id uuid NULL,
                processing_started_at timestamp with time zone NULL,
                prompt_text text NULL,
                question_id uuid NULL,
                session_id uuid NULL,
                status character varying(20) NOT NULL,
                student_id uuid NULL,
                submitted_at timestamp with time zone NULL DEFAULT CURRENT_TIMESTAMP,
                CONSTRAINT "PK_practice_answers" PRIMARY KEY (id)
            );

            -- Table: practice_questions
            CREATE TABLE IF NOT EXISTS practice_questions (
                id uuid NOT NULL DEFAULT gen_random_uuid(),
                approval_status character varying(30) NOT NULL,
                approved_at timestamp with time zone NULL,
                approved_by uuid NULL,
                bloom_level character varying(20) NOT NULL,
                clo_id uuid NULL,
                content text NOT NULL,
                course_id uuid NULL,
                created_at timestamp with time zone NULL DEFAULT CURRENT_TIMESTAMP,
                difficulty character varying(20) NOT NULL,
                follow_up_prompt text NULL,
                has_follow_up boolean NULL,
                image_description text NULL,
                image_url character varying(1000) NULL,
                is_active boolean NULL,
                is_deleted boolean NOT NULL DEFAULT FALSE,
                key_points jsonb NOT NULL DEFAULT '[]'::jsonb,
                review_notes text NULL,
                rubric_id uuid NULL,
                sample_answer text NULL,
                source character varying(50) NOT NULL,
                submitted_by uuid NULL,
                title character varying(300) NOT NULL,
                CONSTRAINT "PK_practice_questions" PRIMARY KEY (id)
            );

            -- Table: practice_sessions
            CREATE TABLE IF NOT EXISTS practice_sessions (
                id uuid NOT NULL DEFAULT gen_random_uuid(),
                completed_at timestamp with time zone NULL,
                course_id uuid NULL,
                last_activity_at timestamp with time zone NULL DEFAULT CURRENT_TIMESTAMP,
                practice_mode character varying(20) NOT NULL,
                selected_difficulties jsonb NOT NULL DEFAULT '[]'::jsonb,
                selected_question_ids jsonb NOT NULL DEFAULT '[]'::jsonb,
                started_at timestamp with time zone NULL DEFAULT CURRENT_TIMESTAMP,
                status character varying(20) NOT NULL,
                student_id uuid NULL,
                total_score numeric(4,2) NULL,
                CONSTRAINT "PK_practice_sessions" PRIMARY KEY (id)
            );

            -- Table: real_exam_session_shifts
            CREATE TABLE IF NOT EXISTS real_exam_session_shifts (
                id uuid NOT NULL DEFAULT gen_random_uuid(),
                certificate_hash character varying(64) NULL,
                certificate_signed_at timestamp with time zone NULL,
                certificate_thumbprint character varying(64) NULL,
                created_at timestamp with time zone NULL DEFAULT CURRENT_TIMESTAMP,
                end_time timestamp with time zone NULL,
                examiner_user_id uuid NULL,
                is_exam_published boolean NULL,
                proctor_user_id uuid NULL,
                published_at timestamp with time zone NULL,
                room_lab character varying(50) NOT NULL,
                max_seats integer NOT NULL DEFAULT 24,
                session_id uuid NULL,
                shift_name character varying(100) NOT NULL,
                start_time timestamp with time zone NULL,
                status character varying(20) NOT NULL,
                CONSTRAINT "PK_real_exam_session_shifts" PRIMARY KEY (id)
            );

            -- Table: refresh_tokens
            CREATE TABLE IF NOT EXISTS refresh_tokens (
                id uuid NOT NULL DEFAULT gen_random_uuid(),
                created_at timestamp with time zone NULL DEFAULT CURRENT_TIMESTAMP,
                expires_at timestamp with time zone NULL,
                replaced_by_token_hash character varying(128) NULL,
                revoked_at timestamp with time zone NULL,
                token_hash character varying(128) NOT NULL,
                user_id uuid NULL,
                CONSTRAINT "PK_refresh_tokens" PRIMARY KEY (id)
            );

            -- Table: rubric_criteria
            CREATE TABLE IF NOT EXISTS rubric_criteria (
                id uuid NOT NULL DEFAULT gen_random_uuid(),
                bloom_level character varying(20) NULL,
                created_at timestamp with time zone NULL DEFAULT CURRENT_TIMESTAMP,
                criterion_name character varying(200) NOT NULL,
                description text NULL,
                max_score numeric(4,2) NULL,
                order_index integer NULL,
                rubric_id uuid NULL,
                weight numeric(3,2) NULL,
                CONSTRAINT "PK_rubric_criteria" PRIMARY KEY (id)
            );

            -- Table: rubrics
            CREATE TABLE IF NOT EXISTS rubrics (
                id uuid NOT NULL DEFAULT gen_random_uuid(),
                course_id uuid NULL,
                created_at timestamp with time zone NULL DEFAULT CURRENT_TIMESTAMP,
                description text NULL,
                is_active boolean NULL,
                name character varying(200) NOT NULL,
                total_max_score numeric(4,2) NULL,
                CONSTRAINT "PK_rubrics" PRIMARY KEY (id)
            );

            -- Table: semesters
            CREATE TABLE IF NOT EXISTS semesters (
                id uuid NOT NULL DEFAULT gen_random_uuid(),
                code character varying(20) NOT NULL,
                created_at timestamp with time zone NULL DEFAULT CURRENT_TIMESTAMP,
                end_date date NULL,
                is_active boolean NULL,
                name character varying(100) NOT NULL,
                start_date date NULL,
                CONSTRAINT "PK_semesters" PRIMARY KEY (id)
            );

            -- Table: student_exam_tickets
            CREATE TABLE IF NOT EXISTS student_exam_tickets (
                id uuid NOT NULL DEFAULT gen_random_uuid(),
                audio_hash_sha256 character varying(64) NULL,
                audio_object_key character varying(512) NULL,
                audio_r2_url character varying(1024) NULL,
                audio_verified_at timestamp with time zone NULL,
                checked_in_at timestamp with time zone NULL DEFAULT CURRENT_TIMESTAMP,
                device_id character varying(128) NULL,
                device_locked_at timestamp with time zone NULL,
                device_session_token_hash character varying(128) NULL,
                device_unlock_count integer NOT NULL DEFAULT 0,
                exam_set_id uuid NULL,
                grades_published_at timestamp with time zone NULL,
                ip_address character varying(45) NULL,
                is_locked boolean NULL,
                last_device_unlocked_at timestamp with time zone NULL,
                last_device_unlocked_by uuid NULL,
                seat_number integer NULL,
                shift_id uuid NULL,
                status character varying(20) NOT NULL,
                student_acknowledgement_status character varying(20) NOT NULL,
                student_id uuid NULL,
                submitted_at timestamp with time zone NULL,
                CONSTRAINT "PK_student_exam_tickets" PRIMARY KEY (id)
            );

            -- Table: system_configs
            CREATE TABLE IF NOT EXISTS system_configs (
                id uuid NOT NULL DEFAULT gen_random_uuid(),
                description text NULL,
                key character varying(100) NOT NULL,
                updated_at timestamp with time zone NULL DEFAULT CURRENT_TIMESTAMP,
                value character varying(500) NOT NULL,
                CONSTRAINT "PK_system_configs" PRIMARY KEY (id)
            );

            -- Table: users
            CREATE TABLE IF NOT EXISTS users (
                id uuid NOT NULL DEFAULT gen_random_uuid(),
                created_at timestamp with time zone NULL DEFAULT CURRENT_TIMESTAMP,
                email character varying(255) NOT NULL,
                full_name character varying(150) NOT NULL,
                is_active boolean NULL,
                must_change_password boolean NULL,
                password_hash character varying(256) NULL,
                role character varying(20) NOT NULL,
                student_code character varying(20) NULL,
                updated_at timestamp with time zone NULL DEFAULT CURRENT_TIMESTAMP,
                username character varying(50) NULL,
                CONSTRAINT "PK_users" PRIMARY KEY (id)
            );

-----------------------------------------------------------------------
            -- 2. BỔ SUNG CỘT CÒN THIẾU TRÊN CÁC BẢNG HIỆN HỮU (IDEMPOTENT ALTER)
            -----------------------------------------------------------------------

            -- users
            IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'users') THEN
                ALTER TABLE users ADD COLUMN IF NOT EXISTS must_change_password boolean NOT NULL DEFAULT FALSE;
                ALTER TABLE users ADD COLUMN IF NOT EXISTS username character varying(50) NULL;
                IF EXISTS (
                    SELECT 1 FROM information_schema.columns
                    WHERE table_schema = 'public' AND table_name = 'users' AND column_name = 'username'
                      AND character_maximum_length IS NOT NULL AND character_maximum_length < 50
                ) THEN
                    ALTER TABLE users ALTER COLUMN username TYPE character varying(50);
                END IF;
                ALTER TABLE users ADD COLUMN IF NOT EXISTS student_code character varying(20) NULL;
                ALTER TABLE users ADD COLUMN IF NOT EXISTS password_hash character varying(256) NULL;
                ALTER TABLE users ADD COLUMN IF NOT EXISTS auth_version integer NOT NULL DEFAULT 1;
                CREATE UNIQUE INDEX IF NOT EXISTS ix_users_username ON users (username) WHERE username IS NOT NULL;
                CREATE UNIQUE INDEX IF NOT EXISTS ix_users_student_code ON users (student_code) WHERE student_code IS NOT NULL;
                CREATE UNIQUE INDEX IF NOT EXISTS ix_users_email_lower ON users (lower(email));
                CREATE UNIQUE INDEX IF NOT EXISTS ix_users_username_lower ON users (lower(username)) WHERE username IS NOT NULL;
                CREATE UNIQUE INDEX IF NOT EXISTS ix_users_student_code_lower ON users (lower(student_code)) WHERE student_code IS NOT NULL;
            END IF;

            -- courses
            IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'courses') THEN
                ALTER TABLE courses ADD COLUMN IF NOT EXISTS description text NULL;
                ALTER TABLE courses ADD COLUMN IF NOT EXISTS department_head_user_id uuid NULL;
                ALTER TABLE courses ADD COLUMN IF NOT EXISTS academic_program_id uuid NULL;
                ALTER TABLE courses ADD COLUMN IF NOT EXISTS mock_exam_duration_minutes integer NOT NULL DEFAULT 15;
                ALTER TABLE courses ADD COLUMN IF NOT EXISTS mock_exam_total_questions integer NOT NULL DEFAULT 3;
                ALTER TABLE courses ADD COLUMN IF NOT EXISTS mock_exam_difficulty_distribution text NULL;
                ALTER TABLE courses ADD COLUMN IF NOT EXISTS mock_exam_bloom_distribution jsonb NOT NULL DEFAULT jsonb_build_object('Remember', 1, 'Understand', 2, 'Apply', 2);
                CREATE INDEX IF NOT EXISTS ix_courses_department_head ON courses (department_head_user_id);
                CREATE INDEX IF NOT EXISTS ix_courses_academic_program ON courses (academic_program_id);
            END IF;

            -- classes
            IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'classes') THEN
                ALTER TABLE classes ADD COLUMN IF NOT EXISTS name character varying(150) NOT NULL DEFAULT '';
                ALTER TABLE classes ADD COLUMN IF NOT EXISTS join_code character varying(20) NULL;
                BEGIN
                    EXECUTE $repair_join_codes$UPDATE classes SET join_code = UPPER('CLS' || RIGHT(REPLACE(id::text, '-', ''), 16)) WHERE join_code IS NULL OR BTRIM(join_code) = ''$repair_join_codes$;
                    ALTER TABLE classes ALTER COLUMN join_code SET NOT NULL;
                    CREATE UNIQUE INDEX IF NOT EXISTS ix_classes_join_code ON classes (join_code);
                EXCEPTION WHEN OTHERS THEN NULL;
                END;
                ALTER TABLE classes ADD COLUMN IF NOT EXISTS join_password_hash character varying(256) NULL;
                ALTER TABLE classes ADD COLUMN IF NOT EXISTS join_password_protected character varying(1024) NULL;
                ALTER TABLE classes ADD COLUMN IF NOT EXISTS is_join_enabled boolean NOT NULL DEFAULT TRUE;
                ALTER TABLE classes ADD COLUMN IF NOT EXISTS allow_self_enrollment boolean NOT NULL DEFAULT TRUE;
                ALTER TABLE classes ADD COLUMN IF NOT EXISTS course_offering_id uuid NULL;
                CREATE INDEX IF NOT EXISTS ix_classes_course_offering ON classes (course_offering_id);
                CREATE INDEX IF NOT EXISTS ix_classes_active_semester_course_created ON classes (is_active, semester_id, course_id, created_at);
            END IF;

            -- class_enrollments
            IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'class_enrollments') THEN
                ALTER TABLE class_enrollments ADD COLUMN IF NOT EXISTS joined_via_code boolean NOT NULL DEFAULT FALSE;
                ALTER TABLE class_enrollments ADD COLUMN IF NOT EXISTS join_method character varying(30) NOT NULL DEFAULT 'assigned';
                ALTER TABLE class_enrollments ADD COLUMN IF NOT EXISTS status character varying(20) NOT NULL DEFAULT 'enrolled';
            END IF;

            -- course_lecturers
            IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'course_lecturers') THEN
                ALTER TABLE course_lecturers ADD COLUMN IF NOT EXISTS course_offering_id uuid NULL;
                ALTER TABLE course_lecturers ADD COLUMN IF NOT EXISTS semester_id uuid NULL;
                CREATE INDEX IF NOT EXISTS ix_course_lecturers_course ON course_lecturers (course_id, is_active);
                CREATE INDEX IF NOT EXISTS ix_course_lecturers_lecturer ON course_lecturers (lecturer_user_id, is_active);
                CREATE UNIQUE INDEX IF NOT EXISTS uq_course_lecturers ON course_lecturers (course_id, lecturer_user_id, semester_id);
                CREATE UNIQUE INDEX IF NOT EXISTS uq_course_offering_lecturers ON course_lecturers (course_offering_id, lecturer_user_id);
            END IF;

            -- practice_questions
            IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'practice_questions') THEN
                ALTER TABLE practice_questions ADD COLUMN IF NOT EXISTS clo_id uuid NULL;
                ALTER TABLE practice_questions ADD COLUMN IF NOT EXISTS approval_status character varying(30) NOT NULL DEFAULT 'APPROVED';
                ALTER TABLE practice_questions ADD COLUMN IF NOT EXISTS approved_by uuid NULL;
                ALTER TABLE practice_questions ADD COLUMN IF NOT EXISTS approved_at timestamp with time zone NULL;
                ALTER TABLE practice_questions ADD COLUMN IF NOT EXISTS is_deleted boolean NOT NULL DEFAULT FALSE;
                ALTER TABLE practice_questions ADD COLUMN IF NOT EXISTS review_notes text NULL;
                ALTER TABLE practice_questions ADD COLUMN IF NOT EXISTS source character varying(50) NOT NULL DEFAULT 'manual';
                ALTER TABLE practice_questions ADD COLUMN IF NOT EXISTS submitted_by uuid NULL;
                CREATE INDEX IF NOT EXISTS "IX_practice_questions_clo_id" ON practice_questions (clo_id);
                CREATE INDEX IF NOT EXISTS ix_practice_questions_clo ON practice_questions (clo_id);
                CREATE INDEX IF NOT EXISTS ix_practice_questions_course ON practice_questions (course_id, difficulty, is_active);
                CREATE INDEX IF NOT EXISTS ix_practice_questions_rubric ON practice_questions (rubric_id);
                CREATE INDEX IF NOT EXISTS ix_practice_questions_approval_status ON practice_questions (approval_status);
                CREATE INDEX IF NOT EXISTS ix_practice_questions_active_approved ON practice_questions (course_id, is_active, approval_status, difficulty);
                CREATE INDEX IF NOT EXISTS ix_practice_questions_approved ON practice_questions (approved_by);
                CREATE INDEX IF NOT EXISTS ix_practice_questions_submitted ON practice_questions (submitted_by);
                CREATE INDEX IF NOT EXISTS ix_practice_questions_approver_status_time ON practice_questions (approved_by, approval_status, approved_at);
            END IF;

            -- course_clos, course_offerings, exam_structures indexes
            IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'course_clos') THEN
                CREATE INDEX IF NOT EXISTS ix_course_clos_course_id ON course_clos (course_id);
                BEGIN
                    CREATE UNIQUE INDEX IF NOT EXISTS uq_course_clos_course_code ON course_clos (course_id, clo_code);
                EXCEPTION WHEN OTHERS THEN NULL;
                END;
            END IF;

            IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'course_offerings') THEN
                CREATE INDEX IF NOT EXISTS ix_course_offerings_semester ON course_offerings (semester_id);
                BEGIN
                    CREATE UNIQUE INDEX IF NOT EXISTS uq_course_offerings_course_semester ON course_offerings (course_id, semester_id);
                EXCEPTION WHEN OTHERS THEN NULL;
                END;
            END IF;

            IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'exam_structures') THEN
                CREATE INDEX IF NOT EXISTS ix_exam_structures_course ON exam_structures (course_id);
                CREATE INDEX IF NOT EXISTS ix_exam_structures_created_by ON exam_structures (created_by);
            END IF;

            -- exam_questions
            IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'exam_questions') THEN
                BEGIN
                    CREATE EXTENSION IF NOT EXISTS pg_trgm;
                EXCEPTION WHEN OTHERS THEN NULL;
                END;
                ALTER TABLE exam_questions ADD COLUMN IF NOT EXISTS clo_id uuid NULL;
                ALTER TABLE exam_questions ADD COLUMN IF NOT EXISTS structure_id uuid NULL;
                ALTER TABLE exam_questions ADD COLUMN IF NOT EXISTS approval_status character varying(30) NOT NULL DEFAULT 'DRAFT';
                ALTER TABLE exam_questions ADD COLUMN IF NOT EXISTS is_deleted boolean NOT NULL DEFAULT FALSE;
                ALTER TABLE exam_questions ADD COLUMN IF NOT EXISTS bank_curation_status character varying(30) NOT NULL DEFAULT 'BANK_READY';
                ALTER TABLE exam_questions ADD COLUMN IF NOT EXISTS curated_by uuid NULL;
                ALTER TABLE exam_questions ADD COLUMN IF NOT EXISTS curated_at timestamp with time zone NULL;
                CREATE INDEX IF NOT EXISTS "IX_exam_questions_clo_id" ON exam_questions (clo_id);
                CREATE INDEX IF NOT EXISTS ix_exam_questions_clo ON exam_questions (clo_id);
                CREATE INDEX IF NOT EXISTS "IX_exam_questions_structure_id" ON exam_questions (structure_id);
                CREATE INDEX IF NOT EXISTS ix_exam_questions_structure ON exam_questions (structure_id);
                CREATE INDEX IF NOT EXISTS ix_exam_questions_course_curation_pool ON exam_questions (course_id, is_deleted, is_active, approval_status, bank_curation_status);
                CREATE INDEX IF NOT EXISTS ix_exam_questions_curated_by ON exam_questions (curated_by);
                BEGIN
                    CREATE INDEX IF NOT EXISTS ix_exam_questions_content_trgm ON exam_questions USING gin (lower(content) gin_trgm_ops) WHERE is_deleted = false;
                    CREATE INDEX IF NOT EXISTS ix_exam_questions_title_trgm ON exam_questions USING gin (lower(title) gin_trgm_ops) WHERE is_deleted = false;
                EXCEPTION WHEN OTHERS THEN NULL;
                END;
            END IF;

            -- practice_sessions
            IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'practice_sessions') THEN
                ALTER TABLE practice_sessions ADD COLUMN IF NOT EXISTS total_score numeric(4,2) NULL;
                ALTER TABLE practice_sessions ADD COLUMN IF NOT EXISTS selected_question_ids jsonb NOT NULL DEFAULT '[]'::jsonb;
            END IF;

            -- practice_answers
            IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'practice_answers') THEN
                ALTER TABLE practice_answers ADD COLUMN IF NOT EXISTS order_index integer NOT NULL DEFAULT 1;
                ALTER TABLE practice_answers ADD COLUMN IF NOT EXISTS prompt_text text NULL;
                ALTER TABLE practice_answers ADD COLUMN IF NOT EXISTS processing_started_at timestamp with time zone NULL;
                CREATE UNIQUE INDEX IF NOT EXISTS uq_practice_answers_session_question_main ON practice_answers (session_id, question_id) WHERE is_follow_up = false AND parent_answer_id IS NULL;
                CREATE UNIQUE INDEX IF NOT EXISTS uq_practice_answers_session_parent ON practice_answers (session_id, parent_answer_id) WHERE is_follow_up = true AND parent_answer_id IS NOT NULL;
                IF NOT EXISTS (
                    SELECT 1 FROM pg_constraint
                    WHERE conrelid = 'public.practice_answers'::regclass
                      AND conname = 'ck_practice_answers_follow_up_parent'
                ) THEN
                    ALTER TABLE practice_answers ADD CONSTRAINT ck_practice_answers_follow_up_parent
                        CHECK ((is_follow_up = false AND parent_answer_id IS NULL) OR (is_follow_up = true AND parent_answer_id IS NOT NULL));
                END IF;
            END IF;

            -- mock_exam_sessions
            IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'mock_exam_sessions') THEN
                ALTER TABLE mock_exam_sessions ADD COLUMN IF NOT EXISTS paused_at timestamp with time zone NULL;
            END IF;

            -- mock_exam_answers
            IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'mock_exam_answers') THEN
                ALTER TABLE mock_exam_answers ADD COLUMN IF NOT EXISTS initial_voice_transcript text NULL;
                ALTER TABLE mock_exam_answers ADD COLUMN IF NOT EXISTS is_follow_up boolean NOT NULL DEFAULT FALSE;
                ALTER TABLE mock_exam_answers ADD COLUMN IF NOT EXISTS parent_answer_id uuid NULL;
                ALTER TABLE mock_exam_answers ADD COLUMN IF NOT EXISTS prompt_text text NULL;
                ALTER TABLE mock_exam_answers ADD COLUMN IF NOT EXISTS order_index integer NOT NULL DEFAULT 1;
                CREATE INDEX IF NOT EXISTS ix_mock_exam_answers_parent ON mock_exam_answers (parent_answer_id);
            END IF;

            -- exam_sets
            IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'exam_sets') THEN
                ALTER TABLE exam_sets ADD COLUMN IF NOT EXISTS session_id uuid NULL;
                ALTER TABLE exam_sets ADD COLUMN IF NOT EXISTS created_by uuid NULL;
                ALTER TABLE exam_sets ADD COLUMN IF NOT EXISTS handed_over_at timestamp with time zone NULL;
                ALTER TABLE exam_sets ADD COLUMN IF NOT EXISTS approved_at timestamp with time zone NULL;
                ALTER TABLE exam_sets ADD COLUMN IF NOT EXISTS approved_by uuid NULL;
                ALTER TABLE exam_sets ADD COLUMN IF NOT EXISTS submitted_at timestamp with time zone NULL;
                ALTER TABLE exam_sets ADD COLUMN IF NOT EXISTS status character varying(30) NOT NULL DEFAULT 'DRAFT';
                ALTER TABLE exam_sets ADD COLUMN IF NOT EXISTS review_notes text NULL;
                ALTER TABLE exam_sets ADD COLUMN IF NOT EXISTS is_sample_set boolean NOT NULL DEFAULT FALSE;
                CREATE INDEX IF NOT EXISTS ix_exam_sets_session ON exam_sets (session_id);
                CREATE INDEX IF NOT EXISTS ix_exam_sets_created_by ON exam_sets (created_by);
                CREATE INDEX IF NOT EXISTS ix_exam_sets_approved_by ON exam_sets (approved_by);
                CREATE INDEX IF NOT EXISTS ix_exam_sets_structure_status_active ON exam_sets (structure_id, is_active, status, is_sample_set);
                DROP INDEX IF EXISTS uq_exam_sets_course_code;
                IF EXISTS (
                    SELECT 1
                    FROM exam_sets
                    WHERE is_sample_set = true AND is_active = true AND status <> 'REJECTED'
                    GROUP BY structure_id
                    HAVING COUNT(*) > 1
                ) THEN
                    RAISE EXCEPTION '[FAIL CLOSED] Không thể tạo uq_exam_sets_active_sample_structure vì phát hiện nhiều hơn 1 đề mẫu đang hoạt động cho cùng structure_id!';
                END IF;
                IF EXISTS (
                    SELECT 1
                    FROM exam_sets
                    WHERE is_active = true
                    GROUP BY course_id, set_code
                    HAVING COUNT(*) > 1
                ) THEN
                    RAISE EXCEPTION '[FAIL CLOSED] Không thể tạo uq_exam_sets_active_course_set_code vì phát hiện cặp (course_id, set_code) trùng lặp đang hoạt động (is_active = true)!';
                END IF;
                CREATE UNIQUE INDEX IF NOT EXISTS uq_exam_sets_active_sample_structure
                    ON exam_sets (structure_id)
                    WHERE is_sample_set = true AND is_active = true AND status <> 'REJECTED';
                CREATE UNIQUE INDEX IF NOT EXISTS uq_exam_sets_active_course_set_code
                    ON exam_sets (course_id, set_code)
                    WHERE is_active = true;
            END IF;

            -- exam_structures
            IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'exam_structures') THEN
                ALTER TABLE exam_structures ADD COLUMN IF NOT EXISTS required_set_count integer NOT NULL DEFAULT 1;
                ALTER TABLE exam_structures ADD COLUMN IF NOT EXISTS slot_definitions jsonb NOT NULL DEFAULT '[]'::jsonb;
            END IF;

            -- lecturer_audit_details
            IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'lecturer_audit_details') THEN
                ALTER TABLE lecturer_audit_details ADD COLUMN IF NOT EXISTS submission_id uuid NULL;
                CREATE INDEX IF NOT EXISTS ix_lecturer_audit_details_submission ON lecturer_audit_details (submission_id);
            END IF;

            -- ai_evaluations
            IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'ai_evaluations') THEN
                ALTER TABLE ai_evaluations ADD COLUMN IF NOT EXISTS student_id uuid NULL;
                ALTER TABLE ai_evaluations ADD COLUMN IF NOT EXISTS in_class_submission_id uuid NULL;
                ALTER TABLE ai_evaluations ADD COLUMN IF NOT EXISTS follow_up_prompt text NULL;
                CREATE INDEX IF NOT EXISTS ix_ai_evaluations_student ON ai_evaluations (student_id);
                BEGIN
                    DROP INDEX IF EXISTS ix_ai_evaluations_practice_answer;
                    DROP INDEX IF EXISTS ix_ai_evaluations_mock_exam_answer;
                    DROP INDEX IF EXISTS ix_ai_evaluations_in_class_submission;
                    CREATE UNIQUE INDEX IF NOT EXISTS uq_ai_evaluations_practice_answer ON ai_evaluations (practice_answer_id) WHERE practice_answer_id IS NOT NULL;
                    CREATE UNIQUE INDEX IF NOT EXISTS uq_ai_evaluations_mock_exam_answer ON ai_evaluations (mock_exam_answer_id) WHERE mock_exam_answer_id IS NOT NULL;
                    CREATE UNIQUE INDEX IF NOT EXISTS uq_ai_evaluations_in_class_submission ON ai_evaluations (in_class_submission_id) WHERE in_class_submission_id IS NOT NULL;
                EXCEPTION WHEN OTHERS THEN NULL;
                END;
            END IF;

            -- real_exam_session_shifts
            IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'real_exam_session_shifts') THEN
                ALTER TABLE real_exam_session_shifts ADD COLUMN IF NOT EXISTS examiner_user_id uuid NULL;
                ALTER TABLE real_exam_session_shifts ADD COLUMN IF NOT EXISTS is_exam_published boolean NOT NULL DEFAULT FALSE;
                ALTER TABLE real_exam_session_shifts ADD COLUMN IF NOT EXISTS published_at timestamp with time zone NULL;
                ALTER TABLE real_exam_session_shifts ADD COLUMN IF NOT EXISTS max_seats integer NOT NULL DEFAULT 24;
                ALTER TABLE real_exam_session_shifts ADD COLUMN IF NOT EXISTS certificate_hash character varying(64) NULL;
                ALTER TABLE real_exam_session_shifts ADD COLUMN IF NOT EXISTS certificate_thumbprint character varying(64) NULL;
                ALTER TABLE real_exam_session_shifts ADD COLUMN IF NOT EXISTS certificate_signed_at timestamp with time zone NULL;
                CREATE INDEX IF NOT EXISTS ix_real_exam_shifts_session_status_time ON real_exam_session_shifts (session_id, status, start_time, end_time);
                CREATE INDEX IF NOT EXISTS ix_real_exam_shifts_status_time ON real_exam_session_shifts (status, start_time, end_time);
                CREATE INDEX IF NOT EXISTS ix_real_exam_shifts_certificate_hash ON real_exam_session_shifts (certificate_hash);
                IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'student_exam_tickets') THEN
                    EXECUTE 'UPDATE real_exam_session_shifts AS shift SET max_seats = GREATEST(24, COALESCE((SELECT MAX(ticket.seat_number) FROM student_exam_tickets AS ticket WHERE ticket.shift_id = shift.id), 0))';
                END IF;
            END IF;

            -- official_exam_sessions
            IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'official_exam_sessions') THEN
                ALTER TABLE official_exam_sessions ADD COLUMN IF NOT EXISTS semester_id uuid NULL;
                ALTER TABLE official_exam_sessions ADD COLUMN IF NOT EXISTS exam_attempt_type character varying(10) NOT NULL DEFAULT 'FE';
                ALTER TABLE official_exam_sessions ALTER COLUMN max_follow_up_questions SET DEFAULT 2;
                CREATE INDEX IF NOT EXISTS ix_official_exam_sessions_semester ON official_exam_sessions (semester_id);
                CREATE INDEX IF NOT EXISTS ix_official_exam_sessions_semester_attempt_date ON official_exam_sessions (semester_id, exam_attempt_type, exam_date DESC);
                IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'courses') THEN
                    EXECUTE 'UPDATE official_exam_sessions AS oes SET semester_id = c.semester_id FROM courses AS c WHERE oes.semester_id IS NULL AND oes.course_id = c.id';
                END IF;
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM official_exam_sessions WHERE semester_id IS NULL) THEN
                        ALTER TABLE official_exam_sessions ALTER COLUMN semester_id SET NOT NULL;
                    END IF;
                    DROP INDEX IF EXISTS ix_official_exam_sessions_semester_course;
                    DROP INDEX IF EXISTS uq_official_exam_sessions_semester_course_attempt;
                    CREATE UNIQUE INDEX IF NOT EXISTS uq_official_exam_sessions_semester_course_attempt
                        ON official_exam_sessions (semester_id, course_id, exam_attempt_type);
                EXCEPTION WHEN OTHERS THEN NULL;
                END;
            END IF;

            -- official_exam_candidates
            IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'official_exam_candidates') THEN
                CREATE UNIQUE INDEX IF NOT EXISTS uq_official_exam_candidates_session_student ON official_exam_candidates (session_id, student_id);
                CREATE INDEX IF NOT EXISTS ix_official_exam_candidates_student ON official_exam_candidates (student_id);
                CREATE INDEX IF NOT EXISTS ix_official_exam_candidates_imported_by ON official_exam_candidates (imported_by);
                CREATE INDEX IF NOT EXISTS ix_official_exam_candidates_session_imported_at ON official_exam_candidates (session_id, imported_at DESC);
            END IF;

            -- student_exam_tickets (M2 BYOD device binding)
            IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'student_exam_tickets') THEN
                ALTER TABLE student_exam_tickets ADD COLUMN IF NOT EXISTS device_id character varying(128) NULL;
                ALTER TABLE student_exam_tickets ADD COLUMN IF NOT EXISTS device_locked_at timestamp with time zone NULL;
                ALTER TABLE student_exam_tickets ADD COLUMN IF NOT EXISTS device_session_token_hash character varying(128) NULL;
                ALTER TABLE student_exam_tickets ADD COLUMN IF NOT EXISTS device_unlock_count integer NOT NULL DEFAULT 0;
                ALTER TABLE student_exam_tickets ADD COLUMN IF NOT EXISTS last_device_unlocked_by uuid NULL;
                ALTER TABLE student_exam_tickets ADD COLUMN IF NOT EXISTS last_device_unlocked_at timestamp with time zone NULL;
                ALTER TABLE student_exam_tickets ADD COLUMN IF NOT EXISTS grades_published_at timestamp with time zone NULL;
                CREATE UNIQUE INDEX IF NOT EXISTS uq_tickets_shift_device ON student_exam_tickets (shift_id, device_id) WHERE device_id IS NOT NULL;
                CREATE INDEX IF NOT EXISTS ix_student_exam_tickets_device_id ON student_exam_tickets (device_id) WHERE device_id IS NOT NULL;
                CREATE INDEX IF NOT EXISTS ix_student_exam_tickets_last_unlocked_by ON student_exam_tickets (last_device_unlocked_by);
            END IF;

            -- exam_anti_cheat_logs (M2 offline client_event_id idempotency & ESR)
            IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'exam_anti_cheat_logs') THEN
                ALTER TABLE exam_anti_cheat_logs ADD COLUMN IF NOT EXISTS client_event_id character varying(128) NULL;
                CREATE UNIQUE INDEX IF NOT EXISTS uq_exam_anti_cheat_logs_ticket_client_event ON exam_anti_cheat_logs (ticket_id, client_event_id) WHERE client_event_id IS NOT NULL;
                CREATE INDEX IF NOT EXISTS ix_exam_anti_cheat_logs_ticket_occurred ON exam_anti_cheat_logs (ticket_id, occurred_at DESC);
                CREATE INDEX IF NOT EXISTS ix_exam_anti_cheat_logs_shift_occurred ON exam_anti_cheat_logs (shift_id, occurred_at DESC);
            END IF;

            -- exam_question_submissions (M2 MF-04 Follow-up 3NF + R2 audio + edit ratio)
            IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'exam_question_submissions') THEN
                ALTER TABLE exam_question_submissions ADD COLUMN IF NOT EXISTS is_follow_up boolean NOT NULL DEFAULT FALSE;
                ALTER TABLE exam_question_submissions ADD COLUMN IF NOT EXISTS parent_submission_id uuid NULL;
                ALTER TABLE exam_question_submissions ADD COLUMN IF NOT EXISTS follow_up_index integer NOT NULL DEFAULT 0;
                ALTER TABLE exam_question_submissions ADD COLUMN IF NOT EXISTS follow_up_prompt text NULL;
                ALTER TABLE exam_question_submissions ADD COLUMN IF NOT EXISTS generated_follow_up_prompt text NULL;
                ALTER TABLE exam_question_submissions ADD COLUMN IF NOT EXISTS initial_voice_transcript text NULL;
                ALTER TABLE exam_question_submissions ADD COLUMN IF NOT EXISTS transcript_edit_ratio numeric(5,4) NULL;
                ALTER TABLE exam_question_submissions ADD COLUMN IF NOT EXISTS audio_object_key character varying(512) NULL;
                ALTER TABLE exam_question_submissions ADD COLUMN IF NOT EXISTS audio_verified_at timestamp with time zone NULL;
                IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'student_exam_tickets') THEN
                    EXECUTE 'UPDATE exam_question_submissions AS s SET audio_object_key = COALESCE(s.audio_object_key, t.audio_object_key), audio_verified_at = COALESCE(s.audio_verified_at, t.audio_verified_at) FROM student_exam_tickets AS t WHERE s.ticket_id = t.id AND (s.audio_object_key IS NULL OR s.audio_verified_at IS NULL) AND (t.audio_object_key IS NOT NULL OR t.audio_verified_at IS NOT NULL)';
                END IF;
                BEGIN
                    DROP INDEX IF EXISTS uq_ticket_question;
                    CREATE UNIQUE INDEX IF NOT EXISTS uq_exam_submissions_ticket_question_turn ON exam_question_submissions (ticket_id, exam_question_id, follow_up_index);
                    CREATE UNIQUE INDEX IF NOT EXISTS uq_exam_submissions_ticket_parent ON exam_question_submissions (ticket_id, parent_submission_id) WHERE is_follow_up = true AND parent_submission_id IS NOT NULL;
                    CREATE INDEX IF NOT EXISTS ix_exam_question_submissions_parent ON exam_question_submissions (parent_submission_id);
                EXCEPTION WHEN OTHERS THEN NULL;
                END;
            END IF;

            -- dead_letter_queues
            IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'dead_letter_queues') THEN
                ALTER TABLE dead_letter_queues ADD COLUMN IF NOT EXISTS flow_type character varying(30) NOT NULL DEFAULT 'OFFICIAL_EXAM';
                ALTER TABLE dead_letter_queues ALTER COLUMN flow_type SET DEFAULT 'OFFICIAL_EXAM';
                ALTER TABLE dead_letter_queues ADD COLUMN IF NOT EXISTS managed_by_role character varying(30) NOT NULL DEFAULT 'examination_officer';
                ALTER TABLE dead_letter_queues ADD COLUMN IF NOT EXISTS resolved_by_user_id uuid NULL;
                ALTER TABLE dead_letter_queues ADD COLUMN IF NOT EXISTS resolution_notes text NULL;
                CREATE INDEX IF NOT EXISTS ix_dlq_role_flow_status ON dead_letter_queues (managed_by_role, flow_type, status);
                CREATE INDEX IF NOT EXISTS ix_dlq_flow_status ON dead_letter_queues (flow_type, status);
                CREATE INDEX IF NOT EXISTS ix_dlq_managed_role ON dead_letter_queues (managed_by_role, status);
            END IF;

            -- in_class_exam_shifts
            IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'in_class_exam_shifts') THEN
                ALTER TABLE in_class_exam_shifts ADD COLUMN IF NOT EXISTS exam_duration_minutes integer NULL;
                ALTER TABLE in_class_exam_shifts ADD COLUMN IF NOT EXISTS is_locked boolean NOT NULL DEFAULT FALSE;
                ALTER TABLE in_class_exam_shifts ADD COLUMN IF NOT EXISTS finalized_at timestamp with time zone NULL;
                ALTER TABLE in_class_exam_shifts ADD COLUMN IF NOT EXISTS selected_difficulties jsonb NOT NULL DEFAULT '["easy","medium","hard"]'::jsonb;
                ALTER TABLE in_class_exam_shifts ADD COLUMN IF NOT EXISTS selected_clo_ids jsonb NOT NULL DEFAULT '[]'::jsonb;
                CREATE INDEX IF NOT EXISTS ix_in_class_exam_shifts_class_status_created ON in_class_exam_shifts (class_id, status, created_at);
                CREATE INDEX IF NOT EXISTS ix_in_class_exam_shifts_lecturer ON in_class_exam_shifts (lecturer_id);
            END IF;

            -- in_class_exam_submissions
            IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'in_class_exam_submissions') THEN
                ALTER TABLE in_class_exam_submissions ADD COLUMN IF NOT EXISTS processing_started_at timestamp with time zone NULL;
            END IF;

            -- in_class_shift_passwords
            IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'in_class_shift_passwords') THEN
                ALTER TABLE in_class_shift_passwords ADD COLUMN IF NOT EXISTS grace_until timestamp with time zone NULL;
            END IF;

            -- appeal_requests: mở rộng status varchar(40), map legacy status và đồng bộ ck_appeal_requests_status
            IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'appeal_requests') THEN
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_schema = 'public' AND table_name = 'appeal_requests' AND column_name = 'status'
                          AND COALESCE(character_maximum_length, 0) < 40
                    ) THEN
                        ALTER TABLE appeal_requests ALTER COLUMN status TYPE character varying(40);
                    END IF;
                    ALTER TABLE appeal_requests ALTER COLUMN status SET DEFAULT 'SUBMITTED_TO_EXAM_OFFICE';
                    ALTER TABLE appeal_requests DROP CONSTRAINT IF EXISTS ck_appeal_requests_status;
                    EXECUTE 'UPDATE appeal_requests SET status = ''SUBMITTED_TO_EXAM_OFFICE'' WHERE status = ''PENDING''';
                    EXECUTE 'UPDATE appeal_requests SET status = ''ASSIGNED_TO_LECTURER'' WHERE status = ''IN_REVIEW''';
                    ALTER TABLE appeal_requests ADD CONSTRAINT ck_appeal_requests_status
                        CHECK (status IN ('SUBMITTED_TO_EXAM_OFFICE', 'ASSIGNED_TO_LECTURER', 'REGRADED_BY_LECTURER', 'APPROVED', 'REJECTED', 'CANCELLED'));
                EXCEPTION WHEN OTHERS THEN NULL;
                END;
            END IF;

            -----------------------------------------------------------------------
            -- 3. BỌC TOÀN BỘ RÀNG BUỘC RỦI RO BẰNG SUBTRANSACTION PL/PGSQL
            -----------------------------------------------------------------------

            -- course_clos constraints
            BEGIN
                ALTER TABLE course_clos ADD CONSTRAINT "fk_course_clos_course"
                    FOREIGN KEY (course_id) REFERENCES courses(id) ON DELETE CASCADE;
            EXCEPTION WHEN OTHERS THEN NULL;
            END;

            BEGIN
                ALTER TABLE course_clos DROP CONSTRAINT IF EXISTS ck_course_clos_bloom;
                ALTER TABLE course_clos ADD CONSTRAINT ck_course_clos_bloom
                    CHECK (bloom_level IN ('Remember', 'Understand', 'Apply', 'Analyze', 'Evaluate', 'Create'));
            EXCEPTION WHEN OTHERS THEN NULL;
            END;

            -- academic_programs constraints
            BEGIN
                ALTER TABLE academic_programs ADD CONSTRAINT "FK_academic_programs_users_department_head_user_id"
                    FOREIGN KEY (department_head_user_id) REFERENCES users(id) ON DELETE RESTRICT;
            EXCEPTION WHEN OTHERS THEN NULL;
            END;

            -- courses constraints
            BEGIN
                ALTER TABLE courses ADD CONSTRAINT "FK_courses_academic_programs_academic_program_id"
                    FOREIGN KEY (academic_program_id) REFERENCES academic_programs(id) ON DELETE RESTRICT;
            EXCEPTION WHEN OTHERS THEN NULL;
            END;

            -- course_offerings constraints
            BEGIN
                ALTER TABLE course_offerings ADD CONSTRAINT "FK_course_offerings_courses_course_id"
                    FOREIGN KEY (course_id) REFERENCES courses(id) ON DELETE RESTRICT;
            EXCEPTION WHEN OTHERS THEN NULL;
            END;

            BEGIN
                ALTER TABLE course_offerings ADD CONSTRAINT "FK_course_offerings_semesters_semester_id"
                    FOREIGN KEY (semester_id) REFERENCES semesters(id) ON DELETE RESTRICT;
            EXCEPTION WHEN OTHERS THEN NULL;
            END;

            -- course_lecturers constraints
            BEGIN
                ALTER TABLE course_lecturers ADD CONSTRAINT "FK_course_lecturers_course_offerings_course_offering_id"
                    FOREIGN KEY (course_offering_id) REFERENCES course_offerings(id) ON DELETE RESTRICT;
            EXCEPTION WHEN OTHERS THEN NULL;
            END;

            -- classes constraints
            BEGIN
                ALTER TABLE classes ADD CONSTRAINT "FK_classes_course_offerings_course_offering_id"
                    FOREIGN KEY (course_offering_id) REFERENCES course_offerings(id) ON DELETE RESTRICT;
            EXCEPTION WHEN OTHERS THEN NULL;
            END;

            -- in_class_exam_shifts constraints
            BEGIN
                ALTER TABLE in_class_exam_shifts DROP CONSTRAINT IF EXISTS ck_in_class_exam_shifts_status;
                ALTER TABLE in_class_exam_shifts ADD CONSTRAINT ck_in_class_exam_shifts_status
                    CHECK (status IN ('SCHEDULED', 'IN_PROGRESS', 'FINALIZED', 'CLOSED'));
            EXCEPTION WHEN OTHERS THEN NULL;
            END;

            BEGIN
                ALTER TABLE in_class_exam_shifts DROP CONSTRAINT IF EXISTS ck_in_class_exam_shifts_time;
                ALTER TABLE in_class_exam_shifts ADD CONSTRAINT ck_in_class_exam_shifts_time
                    CHECK (end_time >= start_time);
            EXCEPTION WHEN OTHERS THEN NULL;
            END;

            BEGIN
                ALTER TABLE in_class_exam_shifts DROP CONSTRAINT IF EXISTS ck_in_class_exam_shifts_pool_mode;
                ALTER TABLE in_class_exam_shifts ADD CONSTRAINT ck_in_class_exam_shifts_pool_mode
                    CHECK (question_pool_mode IN ('ALL_COURSE_BANK', 'SELECTED_CLOS', 'SELECTED_POOL'));
            EXCEPTION WHEN OTHERS THEN NULL;
            END;

            BEGIN
                ALTER TABLE in_class_exam_shifts DROP CONSTRAINT IF EXISTS ck_in_class_exam_shifts_total_questions;
                ALTER TABLE in_class_exam_shifts ADD CONSTRAINT ck_in_class_exam_shifts_total_questions
                    CHECK (total_questions_per_student >= 1 AND total_questions_per_student <= 10);
            EXCEPTION WHEN OTHERS THEN NULL;
            END;

            BEGIN
                ALTER TABLE in_class_exam_shifts DROP CONSTRAINT IF EXISTS ck_in_class_exam_shifts_exam_duration;
                ALTER TABLE in_class_exam_shifts ADD CONSTRAINT ck_in_class_exam_shifts_exam_duration
                    CHECK (exam_duration_minutes IS NULL OR (exam_duration_minutes >= 1 AND exam_duration_minutes <= 60));
            EXCEPTION WHEN OTHERS THEN NULL;
            END;

            BEGIN
                ALTER TABLE in_class_exam_shifts DROP CONSTRAINT IF EXISTS ck_in_class_exam_shifts_max_followup;
                ALTER TABLE in_class_exam_shifts ADD CONSTRAINT ck_in_class_exam_shifts_max_followup
                    CHECK (max_follow_up_questions >= 0 AND max_follow_up_questions <= 5);
            EXCEPTION WHEN OTHERS THEN NULL;
            END;

            BEGIN
                ALTER TABLE in_class_exam_shifts ADD CONSTRAINT "FK_in_class_exam_shifts_classes_class_id"
                    FOREIGN KEY (class_id) REFERENCES classes(id) ON DELETE CASCADE;
            EXCEPTION WHEN OTHERS THEN NULL;
            END;

            BEGIN
                ALTER TABLE in_class_exam_shifts ADD CONSTRAINT "FK_in_class_exam_shifts_users_lecturer_id"
                    FOREIGN KEY (lecturer_id) REFERENCES users(id) ON DELETE RESTRICT;
            EXCEPTION WHEN OTHERS THEN NULL;
            END;

            -- in_class_shift_passwords constraints
            BEGIN
                ALTER TABLE in_class_shift_passwords ADD CONSTRAINT "FK_in_class_shift_passwords_in_class_exam_shifts_shift_id"
                    FOREIGN KEY (shift_id) REFERENCES in_class_exam_shifts(id) ON DELETE CASCADE;
            EXCEPTION WHEN OTHERS THEN NULL;
            END;

            -- in_class_shift_participants constraints
            ALTER TABLE in_class_shift_participants ADD COLUMN IF NOT EXISTS finished_at timestamp with time zone NULL;
            BEGIN
                ALTER TABLE in_class_shift_participants DROP CONSTRAINT IF EXISTS ck_in_class_shift_participants_status;
                ALTER TABLE in_class_shift_participants ADD CONSTRAINT ck_in_class_shift_participants_status
                    CHECK (status IN ('JOINED', 'IN_PROGRESS', 'SUBMITTED', 'GRADED', 'FINALIZED', 'ABSENT'));
            EXCEPTION WHEN OTHERS THEN NULL;
            END;

            BEGIN
                ALTER TABLE in_class_shift_participants ADD CONSTRAINT "FK_in_class_shift_participants_in_class_exam_shifts_shift_id"
                    FOREIGN KEY (shift_id) REFERENCES in_class_exam_shifts(id) ON DELETE CASCADE;
            EXCEPTION WHEN OTHERS THEN NULL;
            END;

            BEGIN
                ALTER TABLE in_class_shift_participants ADD CONSTRAINT "FK_in_class_shift_participants_users_student_id"
                    FOREIGN KEY (student_id) REFERENCES users(id) ON DELETE CASCADE;
            EXCEPTION WHEN OTHERS THEN NULL;
            END;

            BEGIN
                ALTER TABLE in_class_shift_participants ADD CONSTRAINT "FK_in_class_shift_participants_practice_questions_assigned_question_id"
                    FOREIGN KEY (assigned_question_id) REFERENCES practice_questions(id) ON DELETE SET NULL;
            EXCEPTION WHEN OTHERS THEN NULL;
            END;

            -- in_class_exam_submissions constraints
            BEGIN
                ALTER TABLE in_class_exam_submissions DROP CONSTRAINT IF EXISTS ck_in_class_exam_submissions_status;
                ALTER TABLE in_class_exam_submissions ADD CONSTRAINT ck_in_class_exam_submissions_status
                    CHECK (status IN ('SUBMITTED', 'AI_GRADED', 'AUDITED'));
            EXCEPTION WHEN OTHERS THEN NULL;
            END;

            BEGIN
                ALTER TABLE in_class_exam_submissions DROP CONSTRAINT IF EXISTS ck_in_class_exam_submissions_followup_index;
                ALTER TABLE in_class_exam_submissions ADD CONSTRAINT ck_in_class_exam_submissions_followup_index
                    CHECK (follow_up_index >= 0 AND follow_up_index <= 5);
            EXCEPTION WHEN OTHERS THEN NULL;
            END;

            BEGIN
                ALTER TABLE in_class_exam_submissions ADD CONSTRAINT "FK_in_class_exam_submissions_in_class_exam_shifts_shift_id"
                    FOREIGN KEY (shift_id) REFERENCES in_class_exam_shifts(id) ON DELETE CASCADE;
            EXCEPTION WHEN OTHERS THEN NULL;
            END;

            BEGIN
                ALTER TABLE in_class_exam_submissions ADD CONSTRAINT "FK_in_class_exam_submissions_users_student_id"
                    FOREIGN KEY (student_id) REFERENCES users(id) ON DELETE CASCADE;
            EXCEPTION WHEN OTHERS THEN NULL;
            END;

            BEGIN
                ALTER TABLE in_class_exam_submissions ADD CONSTRAINT "FK_in_class_exam_submissions_practice_questions_question_id"
                    FOREIGN KEY (question_id) REFERENCES practice_questions(id) ON DELETE RESTRICT;
            EXCEPTION WHEN OTHERS THEN NULL;
            END;

            BEGIN
                ALTER TABLE in_class_exam_submissions ADD CONSTRAINT "FK_in_class_exam_submissions_in_class_exam_submissions_parent_submission_id"
                    FOREIGN KEY (parent_submission_id) REFERENCES in_class_exam_submissions(id) ON DELETE CASCADE;
            EXCEPTION WHEN OTHERS THEN NULL;
            END;

            -- exam_anti_cheat_logs constraints
            BEGIN
                ALTER TABLE exam_anti_cheat_logs DROP CONSTRAINT IF EXISTS ck_anti_cheat_severity;
                ALTER TABLE exam_anti_cheat_logs ADD CONSTRAINT ck_anti_cheat_severity
                    CHECK (severity IN ('low', 'medium', 'high', 'critical'));
            EXCEPTION WHEN OTHERS THEN NULL;
            END;

            BEGIN
                ALTER TABLE exam_anti_cheat_logs ADD CONSTRAINT "fk_anti_cheat_logs_ticket"
                    FOREIGN KEY (ticket_id) REFERENCES student_exam_tickets(id) ON DELETE CASCADE;
            EXCEPTION WHEN OTHERS THEN NULL;
            END;

            BEGIN
                ALTER TABLE exam_anti_cheat_logs ADD CONSTRAINT "fk_anti_cheat_logs_shift"
                    FOREIGN KEY (shift_id) REFERENCES real_exam_session_shifts(id) ON DELETE CASCADE;
            EXCEPTION WHEN OTHERS THEN NULL;
            END;

            BEGIN
                ALTER TABLE exam_anti_cheat_logs ADD CONSTRAINT "fk_anti_cheat_logs_student"
                    FOREIGN KEY (student_id) REFERENCES users(id) ON DELETE CASCADE;
            EXCEPTION WHEN OTHERS THEN NULL;
            END;

            -- notifications constraints
            BEGIN
                ALTER TABLE notifications ADD CONSTRAINT "FK_notifications_users_user_id"
                    FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE;
            EXCEPTION WHEN OTHERS THEN NULL;
            END;

            -- practice_questions & exam_questions constraints to course_clos
            BEGIN
                ALTER TABLE practice_questions ADD CONSTRAINT "FK_practice_questions_course_clos_clo_id"
                    FOREIGN KEY (clo_id) REFERENCES course_clos(id) ON DELETE RESTRICT;
            EXCEPTION WHEN OTHERS THEN NULL;
            END;

            BEGIN
                ALTER TABLE exam_questions ADD CONSTRAINT "FK_exam_questions_course_clos_clo_id"
                    FOREIGN KEY (clo_id) REFERENCES course_clos(id) ON DELETE RESTRICT;
            EXCEPTION WHEN OTHERS THEN NULL;
            END;

            -- mock_exam_answers constraints
            BEGIN
                ALTER TABLE mock_exam_answers ADD CONSTRAINT "FK_mock_exam_answers_mock_exam_answers_parent_answer_id"
                    FOREIGN KEY (parent_answer_id) REFERENCES mock_exam_answers(id) ON DELETE SET NULL;
            EXCEPTION WHEN OTHERS THEN NULL;
            END;

            -- exam_sets constraints
            BEGIN
                ALTER TABLE exam_sets ADD CONSTRAINT "FK_exam_sets_official_exam_sessions_session_id"
                    FOREIGN KEY (session_id) REFERENCES official_exam_sessions(id) ON DELETE SET NULL;
            EXCEPTION WHEN OTHERS THEN NULL;
            END;

            BEGIN
                ALTER TABLE exam_sets ADD CONSTRAINT "FK_exam_sets_users_created_by"
                    FOREIGN KEY (created_by) REFERENCES users(id) ON DELETE SET NULL;
            EXCEPTION WHEN OTHERS THEN NULL;
            END;

            BEGIN
                ALTER TABLE exam_sets ADD CONSTRAINT "FK_exam_sets_users_approved_by"
                    FOREIGN KEY (approved_by) REFERENCES users(id) ON DELETE SET NULL;
            EXCEPTION WHEN OTHERS THEN NULL;
            END;

            -- lecturer_audit_details constraints
            BEGIN
                ALTER TABLE lecturer_audit_details ADD CONSTRAINT "FK_lecturer_audit_details_exam_question_submissions_submission_id"
                    FOREIGN KEY (submission_id) REFERENCES exam_question_submissions(id) ON DELETE SET NULL;
            EXCEPTION WHEN OTHERS THEN NULL;
            END;

            -- ai_evaluations constraints
            BEGIN
                ALTER TABLE ai_evaluations ADD CONSTRAINT "FK_ai_evaluations_users_student_id"
                    FOREIGN KEY (student_id) REFERENCES users(id) ON DELETE CASCADE;
            EXCEPTION WHEN OTHERS THEN NULL;
            END;

            BEGIN
                ALTER TABLE ai_evaluations ADD CONSTRAINT "FK_ai_evaluations_in_class_exam_submissions_in_class_submission_id"
                    FOREIGN KEY (in_class_submission_id) REFERENCES in_class_exam_submissions(id) ON DELETE CASCADE;
            EXCEPTION WHEN OTHERS THEN NULL;
            END;

            BEGIN
                ALTER TABLE ai_evaluations DROP CONSTRAINT IF EXISTS ck_ai_evaluations_type;
                ALTER TABLE ai_evaluations ADD CONSTRAINT ck_ai_evaluations_type
                    CHECK (answer_type IN ('practice', 'mock_exam', 'exam', 'in_class'));
            EXCEPTION WHEN OTHERS THEN NULL;
            END;

            BEGIN
                EXECUTE 'UPDATE ai_evaluations SET answer_type = ''mock_exam'' WHERE answer_type = ''practice'' AND mock_exam_answer_id IS NOT NULL AND practice_answer_id IS NULL';
                EXECUTE 'UPDATE ai_evaluations SET answer_type = ''practice'' WHERE answer_type != ''practice'' AND practice_answer_id IS NOT NULL AND mock_exam_answer_id IS NULL AND exam_submission_id IS NULL AND in_class_submission_id IS NULL';
                EXECUTE 'UPDATE ai_evaluations SET answer_type = ''exam'' WHERE answer_type != ''exam'' AND exam_submission_id IS NOT NULL AND practice_answer_id IS NULL AND mock_exam_answer_id IS NULL AND in_class_submission_id IS NULL';
                EXECUTE 'UPDATE ai_evaluations SET answer_type = ''in_class'' WHERE answer_type != ''in_class'' AND in_class_submission_id IS NOT NULL AND practice_answer_id IS NULL AND mock_exam_answer_id IS NULL AND exam_submission_id IS NULL';
            EXCEPTION WHEN OTHERS THEN NULL;
            END;

            BEGIN
                ALTER TABLE ai_evaluations DROP CONSTRAINT IF EXISTS ck_ai_evaluations_xor;
                ALTER TABLE ai_evaluations ADD CONSTRAINT ck_ai_evaluations_xor
                    CHECK ((answer_type = 'practice' AND practice_answer_id IS NOT NULL AND mock_exam_answer_id IS NULL AND exam_submission_id IS NULL AND in_class_submission_id IS NULL) OR
                           (answer_type = 'mock_exam' AND mock_exam_answer_id IS NOT NULL AND practice_answer_id IS NULL AND exam_submission_id IS NULL AND in_class_submission_id IS NULL) OR
                           (answer_type = 'exam' AND exam_submission_id IS NOT NULL AND practice_answer_id IS NULL AND mock_exam_answer_id IS NULL AND in_class_submission_id IS NULL) OR
                           (answer_type = 'in_class' AND in_class_submission_id IS NOT NULL AND practice_answer_id IS NULL AND mock_exam_answer_id IS NULL AND exam_submission_id IS NULL));
            EXCEPTION WHEN OTHERS THEN NULL;
            END;

            -- real_exam_session_shifts constraints
            BEGIN
                ALTER TABLE real_exam_session_shifts ADD CONSTRAINT "FK_real_exam_session_shifts_users_examiner_user_id"
                    FOREIGN KEY (examiner_user_id) REFERENCES users(id) ON DELETE SET NULL;
            EXCEPTION WHEN OTHERS THEN NULL;
            END;

            BEGIN
                ALTER TABLE real_exam_session_shifts DROP CONSTRAINT IF EXISTS ck_shifts_distinct_proctor_examiner;
                ALTER TABLE real_exam_session_shifts ADD CONSTRAINT ck_shifts_distinct_proctor_examiner
                    CHECK (proctor_user_id IS NULL OR examiner_user_id IS NULL OR proctor_user_id <> examiner_user_id);
            EXCEPTION WHEN OTHERS THEN NULL;
            END;

            BEGIN
                ALTER TABLE real_exam_session_shifts DROP CONSTRAINT IF EXISTS ck_shifts_certificate_hash_len;
                ALTER TABLE real_exam_session_shifts ADD CONSTRAINT ck_shifts_certificate_hash_len
                    CHECK (certificate_hash IS NULL OR length(certificate_hash) = 64);
            EXCEPTION WHEN OTHERS THEN NULL;
            END;

            -- official_exam_sessions constraints
            BEGIN
                ALTER TABLE official_exam_sessions ADD CONSTRAINT "FK_official_exam_sessions_semesters_semester_id"
                    FOREIGN KEY (semester_id) REFERENCES semesters(id) ON DELETE RESTRICT;
            EXCEPTION WHEN OTHERS THEN NULL;
            END;

            BEGIN
                ALTER TABLE official_exam_sessions DROP CONSTRAINT IF EXISTS ck_official_sessions_attempt_type;
                ALTER TABLE official_exam_sessions ADD CONSTRAINT ck_official_sessions_attempt_type
                    CHECK (exam_attempt_type IN ('FE', 'RE'));
            EXCEPTION WHEN OTHERS THEN NULL;
            END;

            -- official_exam_candidates constraints
            BEGIN
                ALTER TABLE official_exam_candidates ADD CONSTRAINT "FK_official_exam_candidates_official_exam_sessions_session_id"
                    FOREIGN KEY (session_id) REFERENCES official_exam_sessions(id) ON DELETE CASCADE;
            EXCEPTION WHEN OTHERS THEN NULL;
            END;

            BEGIN
                ALTER TABLE official_exam_candidates ADD CONSTRAINT "FK_official_exam_candidates_users_student_id"
                    FOREIGN KEY (student_id) REFERENCES users(id) ON DELETE RESTRICT;
            EXCEPTION WHEN OTHERS THEN NULL;
            END;

            BEGIN
                ALTER TABLE official_exam_candidates ADD CONSTRAINT "FK_official_exam_candidates_users_imported_by"
                    FOREIGN KEY (imported_by) REFERENCES users(id) ON DELETE SET NULL;
            EXCEPTION WHEN OTHERS THEN NULL;
            END;

            -- student_exam_tickets BYOD constraints
            BEGIN
                ALTER TABLE student_exam_tickets DROP CONSTRAINT IF EXISTS ck_tickets_device_unlock_count;
                ALTER TABLE student_exam_tickets ADD CONSTRAINT ck_tickets_device_unlock_count
                    CHECK (device_unlock_count >= 0);
            EXCEPTION WHEN OTHERS THEN NULL;
            END;

            BEGIN
                ALTER TABLE student_exam_tickets DROP CONSTRAINT IF EXISTS ck_tickets_device_binding_consistency;
                ALTER TABLE student_exam_tickets ADD CONSTRAINT ck_tickets_device_binding_consistency
                    CHECK ((device_id IS NULL AND device_session_token_hash IS NULL) OR (device_id IS NOT NULL));
            EXCEPTION WHEN OTHERS THEN NULL;
            END;

            BEGIN
                ALTER TABLE student_exam_tickets ADD CONSTRAINT "FK_student_exam_tickets_users_last_device_unlocked_by"
                    FOREIGN KEY (last_device_unlocked_by) REFERENCES users(id) ON DELETE SET NULL;
            EXCEPTION WHEN OTHERS THEN NULL;
            END;

            -- exam_question_submissions M2 Follow-up & edit-ratio constraints
            BEGIN
                ALTER TABLE exam_question_submissions DROP CONSTRAINT IF EXISTS ck_exam_question_submissions_follow_up_index;
                ALTER TABLE exam_question_submissions ADD CONSTRAINT ck_exam_question_submissions_follow_up_index
                    CHECK (follow_up_index >= 0 AND follow_up_index <= 5);
            EXCEPTION WHEN OTHERS THEN NULL;
            END;

            BEGIN
                ALTER TABLE exam_question_submissions DROP CONSTRAINT IF EXISTS ck_exam_question_submissions_follow_up_consistency;
                ALTER TABLE exam_question_submissions ADD CONSTRAINT ck_exam_question_submissions_follow_up_consistency
                    CHECK ((is_follow_up = false AND parent_submission_id IS NULL AND follow_up_index = 0 AND follow_up_prompt IS NULL)
                        OR (is_follow_up = true AND parent_submission_id IS NOT NULL AND follow_up_index >= 1 AND follow_up_index <= 5 AND follow_up_prompt IS NOT NULL AND btrim(follow_up_prompt) <> ''));
            EXCEPTION WHEN OTHERS THEN NULL;
            END;

            BEGIN
                ALTER TABLE exam_question_submissions DROP CONSTRAINT IF EXISTS ck_exam_question_submissions_transcript_edit_ratio;
                ALTER TABLE exam_question_submissions ADD CONSTRAINT ck_exam_question_submissions_transcript_edit_ratio
                    CHECK (transcript_edit_ratio IS NULL OR (transcript_edit_ratio >= 0 AND transcript_edit_ratio <= 1));
            EXCEPTION WHEN OTHERS THEN NULL;
            END;

            BEGIN
                ALTER TABLE exam_question_submissions ADD CONSTRAINT "FK_exam_question_submissions_parent_submission_id"
                    FOREIGN KEY (parent_submission_id) REFERENCES exam_question_submissions(id) ON DELETE CASCADE;
            EXCEPTION WHEN OTHERS THEN NULL;
            END;

            -- exam_questions M3 bank curation constraints
            BEGIN
                ALTER TABLE exam_questions DROP CONSTRAINT IF EXISTS ck_exam_questions_bank_curation_status;
                ALTER TABLE exam_questions ADD CONSTRAINT ck_exam_questions_bank_curation_status
                    CHECK (bank_curation_status IN ('BANK_READY', 'PENDING_CURATION', 'CURATED_INTO_BANK', 'EXCLUDED_FROM_BANK'));
            EXCEPTION WHEN OTHERS THEN NULL;
            END;

            BEGIN
                ALTER TABLE exam_questions ADD CONSTRAINT "FK_exam_questions_users_curated_by"
                    FOREIGN KEY (curated_by) REFERENCES users(id) ON DELETE SET NULL;
            EXCEPTION WHEN OTHERS THEN NULL;
            END;

            -----------------------------------------------------------------------
            -- 4. ĐỒNG BỘ DỮ LIỆU NỀN HỌC THUẬT (DYNAMIC DML)
            -----------------------------------------------------------------------
            BEGIN
                IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'course_offerings') THEN
                    EXECUTE 'INSERT INTO course_offerings (id, course_id, semester_id, is_enabled, created_at, updated_at)
                    SELECT gen_random_uuid(), c.id, c.semester_id, c.is_active, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP
                    FROM courses c
                    WHERE c.semester_id IS NOT NULL AND NOT EXISTS (
                        SELECT 1 FROM course_offerings existing
                        WHERE existing.course_id = c.id AND existing.semester_id = c.semester_id)
                    ON CONFLICT DO NOTHING';
                END IF;
            EXCEPTION WHEN OTHERS THEN NULL;
            END;

            BEGIN
                IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'course_offerings')
                   AND EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'course_lecturers') THEN
                    EXECUTE 'UPDATE course_lecturers AS cl
                    SET course_offering_id = co.id,
                        semester_id = COALESCE(cl.semester_id, co.semester_id)
                    FROM course_offerings AS co
                    WHERE cl.course_offering_id IS NULL
                      AND co.course_id = cl.course_id
                      AND (cl.semester_id IS NULL OR co.semester_id = cl.semester_id)';
                END IF;
            EXCEPTION WHEN OTHERS THEN NULL;
            END;

            BEGIN
                IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'course_offerings')
                   AND EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'classes') THEN
                    EXECUTE 'UPDATE classes AS cls
                    SET course_offering_id = co.id
                    FROM course_offerings AS co
                    WHERE cls.course_offering_id IS NULL
                      AND co.course_id = cls.course_id
                      AND co.semester_id = cls.semester_id';
                END IF;
            EXCEPTION WHEN OTHERS THEN NULL;
            END;

            BEGIN
                ALTER TABLE dead_letter_queues DROP CONSTRAINT IF EXISTS ck_dlq_flow_type;
                ALTER TABLE dead_letter_queues ADD CONSTRAINT ck_dlq_flow_type
                    CHECK (flow_type IN ('PRACTICE', 'MOCK_EXAM', 'OFFICIAL_EXAM', 'IN_CLASS_EXAM'));
            EXCEPTION WHEN OTHERS THEN NULL;
            END;

            -----------------------------------------------------------------------
            -- 5. ĐỒNG BỘ BẢNG LỊCH SỬ MIGRATION ("__EFMigrationsHistory")
            -----------------------------------------------------------------------
            BEGIN
                CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
                    "MigrationId" character varying(150) NOT NULL,
                    "ProductVersion" character varying(32) NOT NULL,
                    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
                );

                EXECUTE 'INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
                VALUES
                    (''20261022125559_InitialDB'', ''8.0.11''),
                    (''20261022214810_AddMockExamAnswerFollowUp'', ''8.0.11''),
                    (''20261030041911_OptimizePostgresIndexesAndConstraints'', ''8.0.11''),
                    (''20261102150149_AddCourseClosAndAntiCheatLogsAndExpandRoles'', ''8.0.11''),
                    (''20261104062322_AddDlqFlowTypeAndRoleSeparation'', ''8.0.11''),
                    (''20261105061650_AddInClassExamShiftsAndNotifications3NF'', ''8.0.11''),
                    (''20261105064957_AddAcademicProgramAndCourseOfferings'', ''8.0.11''),
                    (''20261105073054_AddCourseOfferingLecturerAndClassOwnership'', ''8.0.11''),
                    (''20261107111940_AddClassJoinPasswordAndShiftExamWindow'', ''8.0.11''),
                    (''20261108131126_SeedALT'', ''8.0.11''),
                    (''20261108141117_UpdateDB'', ''8.0.11'')
                ON CONFLICT ("MigrationId") DO NOTHING';
            EXCEPTION WHEN OTHERS THEN NULL;
            END;

        END
        $reconcile_post$;
""" + QuestionReviewRequestSchema.CreateSql + """
CREATE INDEX IF NOT EXISTS "ix_question_review_request_items_reviewed_by"
    ON question_review_request_items ("ReviewedBy");
""" + QuestionReviewRequestSchema.BackfillSql + ExamPreparationReuseSchema.PostMigrationSql;

    public static async Task ReconcilePreMigrationAsync(OralExamDbContext context, CancellationToken cancellationToken = default)
    {
        if (!context.Database.IsRelational())
        {
            return;
        }

        // T8A-F008: Preflight kiểm tra dữ liệu trùng lặp trên practice_answers trước khi tạo unique index
        await ValidatePracticeAnswersUniquePreflightAsync(context, cancellationToken);

        // MF-04 M1/M2: Fail-closed preflight kiểm tra trùng lặp / sai nhất quán dữ liệu thi thật
        await ValidateOfficialExamSchemaPreflightAsync(context, cancellationToken);

        await context.Database.ExecuteSqlRawAsync(GetPreMigrationSql(), cancellationToken);
    }

    public static async Task ReconcilePostMigrationAsync(OralExamDbContext context, CancellationToken cancellationToken = default)
    {
        if (!context.Database.IsRelational())
        {
            return;
        }

        await context.Database.ExecuteSqlRawAsync(GetPostMigrationSql(), cancellationToken);

        // T8A-F008: Startup Verification Probe xác nhận sự hiện diện của unique indexes bắt buộc
        await VerifyPracticeAnswersUniqueConstraintsAsync(context, cancellationToken);

        // MF-04 M1/M2: Startup Verification Probe xác nhận bảng, CHECK và unique indexes của M1/M2
        await VerifyOfficialExamSchemaInvariantsAsync(context, cancellationToken);
    }

    public static async Task ValidatePracticeAnswersUniquePreflightAsync(OralExamDbContext context, CancellationToken cancellationToken = default)
    {
        if (!context.Database.IsRelational())
        {
            return;
        }

        var conn = context.Database.GetDbConnection();
        bool wasClosed = conn.State != System.Data.ConnectionState.Open;
        if (wasClosed)
        {
            await conn.OpenAsync(cancellationToken);
        }

        try
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT to_regclass('public.practice_answers') IS NOT NULL;";
            var tableExists = (bool)(await cmd.ExecuteScalarAsync(cancellationToken))!;
            if (!tableExists)
            {
                return;
            }

            // 1. T8A-F013: Kiểm tra dữ liệu vi phạm check constraint (tổ hợp is_follow_up vs parent_answer_id không nhất quán)
            cmd.CommandText = """
                SELECT id, session_id, question_id, is_follow_up, parent_answer_id
                FROM practice_answers
                WHERE NOT ((is_follow_up = false AND parent_answer_id IS NULL)
                        OR (is_follow_up = true AND parent_answer_id IS NOT NULL))
                LIMIT 10;
                """;
            var invalidCheckList = new System.Collections.Generic.List<string>();
            using (var reader0 = await cmd.ExecuteReaderAsync(cancellationToken))
            {
                while (await reader0.ReadAsync(cancellationToken))
                {
                    invalidCheckList.Add($"Id: {reader0[0]}, Session: {reader0[1]}, Question: {reader0[2]}, IsFollowUp: {reader0[3]}, ParentAnswerId: {reader0[4]}");
                }
            }

            if (invalidCheckList.Count > 0)
            {
                throw new System.InvalidOperationException(
                    $"[FAIL CLOSED] Phát hiện {invalidCheckList.Count}+ bản ghi trong practice_answers vi phạm ck_practice_answers_follow_up_parent (is_follow_up vs parent_answer_id)! " +
                    $"Chi tiết: {string.Join("; ", invalidCheckList.Take(5))}. Không tự ý sửa dữ liệu; yêu cầu can thiệp thủ công.");
            }

            // 2. Kiểm tra trùng lặp câu trả lời chính
            cmd.CommandText = """
                SELECT session_id, question_id, COUNT(*) AS dup_count
                FROM practice_answers
                WHERE (is_follow_up = false AND parent_answer_id IS NULL)
                   OR (parent_answer_id IS NULL)
                GROUP BY session_id, question_id
                HAVING COUNT(*) > 1;
                """;
            var duplicateMainList = new System.Collections.Generic.List<string>();
            using (var reader = await cmd.ExecuteReaderAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    duplicateMainList.Add($"Session: {reader[0]}, Question: {reader[1]}, Count: {reader[2]}");
                }
            }

            if (duplicateMainList.Count > 0)
            {
                throw new System.InvalidOperationException(
                    $"[FAIL CLOSED] Phát hiện {duplicateMainList.Count} nhóm câu trả lời chính bị trùng lặp trong practice_answers trước khi tạo unique index! " +
                    $"Chi tiết: {string.Join("; ", duplicateMainList.Take(5))}. Không tự ý sửa dữ liệu; yêu cầu can thiệp thủ công.");
            }

            // 3. Kiểm tra trùng lặp câu trả lời phụ
            cmd.CommandText = """
                SELECT session_id, parent_answer_id, COUNT(*) AS dup_count
                FROM practice_answers
                WHERE (is_follow_up = true AND parent_answer_id IS NOT NULL)
                   OR (parent_answer_id IS NOT NULL)
                GROUP BY session_id, parent_answer_id
                HAVING COUNT(*) > 1;
                """;
            var duplicateFollowUpList = new System.Collections.Generic.List<string>();
            using (var reader2 = await cmd.ExecuteReaderAsync(cancellationToken))
            {
                while (await reader2.ReadAsync(cancellationToken))
                {
                    duplicateFollowUpList.Add($"Session: {reader2[0]}, ParentAnswer: {reader2[1]}, Count: {reader2[2]}");
                }
            }

            if (duplicateFollowUpList.Count > 0)
            {
                throw new System.InvalidOperationException(
                    $"[FAIL CLOSED] Phát hiện {duplicateFollowUpList.Count} nhóm câu trả lời phụ bị trùng lặp trong practice_answers trước khi tạo unique index! " +
                    $"Chi tiết: {string.Join("; ", duplicateFollowUpList.Take(5))}. Không tự ý sửa dữ liệu; yêu cầu can thiệp thủ công.");
            }
        }
        finally
        {
            if (wasClosed && conn.State == System.Data.ConnectionState.Open)
            {
                await conn.CloseAsync();
            }
        }
    }

    public static async Task VerifyPracticeAnswersUniqueConstraintsAsync(OralExamDbContext context, CancellationToken cancellationToken = default)
    {
        if (!context.Database.IsRelational())
        {
            return;
        }

        var conn = context.Database.GetDbConnection();
        bool wasClosed = conn.State != System.Data.ConnectionState.Open;
        if (wasClosed)
        {
            await conn.OpenAsync(cancellationToken);
        }

        try
        {
            using var cmd = conn.CreateCommand();

            // 1. T8A-F013: Bắt buộc bảng practice_answers phải tồn tại
            cmd.CommandText = "SELECT to_regclass('public.practice_answers') IS NOT NULL;";
            var tableExists = (bool)(await cmd.ExecuteScalarAsync(cancellationToken))!;
            if (!tableExists)
            {
                throw new System.InvalidOperationException("[FAIL CLOSED] Startup Verification Probe: Bảng practice_answers không tồn tại trong cơ sở dữ liệu!");
            }

            // 2. T8A-F013 / T8A-F015: Xác nhận check constraint ck_practice_answers_follow_up_parent tồn tại, validated và đúng semantic
            cmd.CommandText = """
                SELECT conname, convalidated, pg_get_constraintdef(oid) AS condef
                FROM pg_constraint
                WHERE conrelid = 'public.practice_answers'::regclass
                  AND conname = 'ck_practice_answers_follow_up_parent';
                """;
            bool constraintFound = false;
            using (var checkReader = await cmd.ExecuteReaderAsync(cancellationToken))
            {
                if (await checkReader.ReadAsync(cancellationToken))
                {
                    constraintFound = true;
                    var isValidated = checkReader.GetBoolean(1);
                    var conDef = checkReader.GetString(2);

                    if (!isValidated)
                    {
                        throw new System.InvalidOperationException(
                            "[FAIL CLOSED] Startup Verification Probe: Check constraint ck_practice_answers_follow_up_parent chưa được xác thực (convalidated = false)!");
                    }

                    // T8A-F015 / T8A-F018: Kiểm tra semantic chính xác qua structural whitelist, từ chối Contains lỏng lẻo & OR TRUE
                    ValidatePracticeAnswersCheckConstraintDefinition(conDef);
                }
            }

            if (!constraintFound)
            {
                throw new System.InvalidOperationException(
                    "[FAIL CLOSED] Startup Verification Probe: Thiếu check constraint ck_practice_answers_follow_up_parent trên bảng practice_answers!");
            }

            // 3. T8A-F013 / T8A-F015 / T8A-F018: Xác nhận 2 unique index qua catalog: indisunique=true, đúng số lượng & thứ tự cột, đúng predicate filter
            cmd.CommandText = """
                SELECT
                    i.relname AS indexname,
                    ix.indisunique AS is_unique,
                    ix.indnatts AS key_count,
                    COALESCE(pg_get_expr(ix.indpred, ix.indrelid), '') AS predicate_expr,
                    COALESCE(pg_get_indexdef(ix.indexrelid, 1, false), '') AS col1,
                    COALESCE(pg_get_indexdef(ix.indexrelid, 2, false), '') AS col2
                FROM pg_class t
                JOIN pg_index ix ON t.oid = ix.indrelid
                JOIN pg_class i ON i.oid = ix.indexrelid
                JOIN pg_namespace n ON n.oid = t.relnamespace
                WHERE n.nspname = 'public'
                  AND t.relname = 'practice_answers'
                  AND i.relname IN ('uq_practice_answers_session_question_main', 'uq_practice_answers_session_parent');
                """;

            var indexDict = new System.Collections.Generic.Dictionary<string, (bool isUnique, short keyCount, string predicateExpr, string col1, string col2)>(System.StringComparer.OrdinalIgnoreCase);
            using (var reader = await cmd.ExecuteReaderAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    var indexName = reader.GetString(0);
                    var isUnique = reader.GetBoolean(1);
                    var keyCount = reader.GetInt16(2);
                    var predicateExpr = reader.GetString(3);
                    var col1 = reader.GetString(4).Trim().Trim('"').ToLowerInvariant();
                    var col2 = reader.GetString(5).Trim().Trim('"').ToLowerInvariant();
                    indexDict[indexName] = (isUnique, keyCount, predicateExpr, col1, col2);
                }
            }

            // Kiểm tra index main
            if (!indexDict.TryGetValue("uq_practice_answers_session_question_main", out var mainIdx))
            {
                throw new System.InvalidOperationException(
                    "[FAIL CLOSED] Startup Verification Probe: Thiếu unique index uq_practice_answers_session_question_main trên practice_answers!");
            }
            if (!mainIdx.isUnique)
            {
                throw new System.InvalidOperationException(
                    "[FAIL CLOSED] Startup Verification Probe: Index uq_practice_answers_session_question_main không phải unique (indisunique = false)!");
            }
            if (mainIdx.keyCount != 2)
            {
                throw new System.InvalidOperationException(
                    $"[FAIL CLOSED] Startup Verification Probe: Index uq_practice_answers_session_question_main phải có đúng 2 key columns! Hiện tại có {mainIdx.keyCount} columns.");
            }
            if (mainIdx.col1 != "session_id" || mainIdx.col2 != "question_id")
            {
                throw new System.InvalidOperationException(
                    $"[FAIL CLOSED] Startup Verification Probe: Index uq_practice_answers_session_question_main sai thứ tự hoặc danh sách cột! Cần 'session_id, question_id', tìm thấy: '{mainIdx.col1}, {mainIdx.col2}'.");
            }

            // T8A-F015 / T8A-F018: Kiểm tra predicate filter main index chính xác, từ chối mọi mệnh đề dư (OR TRUE, AND TRUE, v.v.)
            ValidatePracticeAnswersIndexPredicate("uq_practice_answers_session_question_main", mainIdx.predicateExpr, isMainAnswerIndex: true);

            // Kiểm tra index follow-up
            if (!indexDict.TryGetValue("uq_practice_answers_session_parent", out var fuIdx))
            {
                throw new System.InvalidOperationException(
                    "[FAIL CLOSED] Startup Verification Probe: Thiếu unique index uq_practice_answers_session_parent trên practice_answers!");
            }
            if (!fuIdx.isUnique)
            {
                throw new System.InvalidOperationException(
                    "[FAIL CLOSED] Startup Verification Probe: Index uq_practice_answers_session_parent không phải unique (indisunique = false)!");
            }
            if (fuIdx.keyCount != 2)
            {
                throw new System.InvalidOperationException(
                    $"[FAIL CLOSED] Startup Verification Probe: Index uq_practice_answers_session_parent phải có đúng 2 key columns! Hiện tại có {fuIdx.keyCount} columns.");
            }
            if (fuIdx.col1 != "session_id" || fuIdx.col2 != "parent_answer_id")
            {
                throw new System.InvalidOperationException(
                    $"[FAIL CLOSED] Startup Verification Probe: Index uq_practice_answers_session_parent sai thứ tự hoặc danh sách cột! Cần 'session_id, parent_answer_id', tìm thấy: '{fuIdx.col1}, {fuIdx.col2}'.");
            }

            // T8A-F015 / T8A-F018: Kiểm tra predicate filter follow-up index chính xác, từ chối mọi mệnh đề dư (OR TRUE, AND TRUE, v.v.)
            ValidatePracticeAnswersIndexPredicate("uq_practice_answers_session_parent", fuIdx.predicateExpr, isMainAnswerIndex: false);
        }
        finally
        {
            if (wasClosed && conn.State == System.Data.ConnectionState.Open)
            {
                await conn.CloseAsync();
            }
        }
    }

    private static void ValidatePracticeAnswersCheckConstraintDefinition(string conDef)
    {
        if (string.IsNullOrWhiteSpace(conDef))
        {
            throw new System.InvalidOperationException("[FAIL CLOSED] Startup Verification Probe: Định nghĩa check constraint rỗng!");
        }

        var norm = conDef.Trim().ToLowerInvariant();
        if (norm.StartsWith("check"))
        {
            norm = norm.Substring(5).Trim();
        }
        norm = System.Text.RegularExpressions.Regex.Replace(norm, @"\s+", " ").Trim();

        // Tách các nhánh cấp cao nhất theo toán tử ' or '
        // Bắt buộc phải có đúng 2 nhánh: 1 nhánh cho Main Answer và 1 nhánh cho Follow-up Answer
        var branches = SplitPracticeBooleanExpression(norm, "or");
        if (branches.Length != 2)
        {
            throw new System.InvalidOperationException(
                $"[FAIL CLOSED] Startup Verification Probe: Check constraint ck_practice_answers_follow_up_parent phải có đúng 2 nhánh nối bằng OR! " +
                $"Hiện tại phát hiện {branches.Length} nhánh (từ chối mọi mệnh đề dư thừa như OR TRUE hoặc thiếu nhánh). Định nghĩa: '{conDef}'.");
        }

        bool hasValidMainBranch = false;
        bool hasValidFollowUpBranch = false;

        foreach (var branch in branches)
        {
            var terms = SplitPracticeBooleanExpression(branch, "and");
            if (terms.Length != 2)
            {
                throw new System.InvalidOperationException(
                    $"[FAIL CLOSED] Startup Verification Probe: Mỗi nhánh trong ck_practice_answers_follow_up_parent phải có đúng 2 điều kiện nối bằng AND! " +
                    $"Nhánh '{branch}' có {terms.Length} điều kiện (từ chối điều kiện dư thừa). Định nghĩa: '{conDef}'.");
            }

            var termSet = new System.Collections.Generic.HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            foreach (var term in terms)
            {
                var atom = StripPracticeOuterParentheses(term);
                if (atom.Contains('(') || atom.Contains(')') || atom.Contains(" and ") || atom.Contains(" or "))
                {
                    throw new System.InvalidOperationException($"[FAIL CLOSED] Startup Verification Probe: Nhóm điều kiện không hợp lệ trong ck_practice_answers_follow_up_parent: '{conDef}'.");
                }
                termSet.Add(atom);
            }

            // Kiểm tra nhánh Main: is_follow_up = false AND parent_answer_id IS NULL
            bool isMainIsFollowUp = termSet.Contains("is_follow_up = false") || termSet.Contains("not is_follow_up");
            bool isMainParentNull = termSet.Contains("parent_answer_id is null");
            if (isMainIsFollowUp && isMainParentNull)
            {
                if (hasValidMainBranch)
                {
                    throw new System.InvalidOperationException(
                        $"[FAIL CLOSED] Startup Verification Probe: Check constraint ck_practice_answers_follow_up_parent có 2 nhánh Main trùng nhau! Định nghĩa: '{conDef}'.");
                }
                hasValidMainBranch = true;
                continue;
            }

            // Kiểm tra nhánh Follow-up: is_follow_up = true AND parent_answer_id IS NOT NULL
            bool isFollowUpIsFollowUp = termSet.Contains("is_follow_up = true") ||
                                       (termSet.Contains("is_follow_up") && !termSet.Contains("not is_follow_up") && !termSet.Contains("is_follow_up = false"));
            bool isFollowUpParentNotNull = termSet.Contains("parent_answer_id is not null");
            if (isFollowUpIsFollowUp && isFollowUpParentNotNull)
            {
                if (hasValidFollowUpBranch)
                {
                    throw new System.InvalidOperationException(
                        $"[FAIL CLOSED] Startup Verification Probe: Check constraint ck_practice_answers_follow_up_parent có 2 nhánh Follow-up trùng nhau! Định nghĩa: '{conDef}'.");
                }
                hasValidFollowUpBranch = true;
                continue;
            }

            throw new System.InvalidOperationException(
                $"[FAIL CLOSED] Startup Verification Probe: Nhánh '{branch}' trong ck_practice_answers_follow_up_parent không hợp lệ! " +
                $"Chỉ chấp nhận 'is_follow_up = false AND parent_answer_id IS NULL' hoặc 'is_follow_up = true AND parent_answer_id IS NOT NULL'. Định nghĩa: '{conDef}'.");
        }

        if (!hasValidMainBranch || !hasValidFollowUpBranch)
        {
            throw new System.InvalidOperationException(
                $"[FAIL CLOSED] Startup Verification Probe: ck_practice_answers_follow_up_parent thiếu nhánh Main hoặc Follow-up hợp lệ! Định nghĩa: '{conDef}'.");
        }
    }

    private static string[] SplitPracticeBooleanExpression(string expression, string logicalOperator)
    {
        expression = StripPracticeOuterParentheses(expression);
        var parts = new System.Collections.Generic.List<string>();
        var depth = 0;
        var start = 0;
        var marker = $" {logicalOperator} ";
        for (var i = 0; i < expression.Length; i++)
        {
            if (expression[i] == '(') depth++;
            else if (expression[i] == ')') depth--;
            if (depth < 0)
                throw new System.InvalidOperationException("[FAIL CLOSED] Startup Verification Probe: Biểu thức SQL sai cấu trúc ngoặc.");
            if (depth == 0 && i + marker.Length <= expression.Length &&
                string.Compare(expression, i, marker, 0, marker.Length, System.StringComparison.Ordinal) == 0)
            {
                parts.Add(StripPracticeOuterParentheses(expression.Substring(start, i - start).Trim()));
                i += marker.Length - 1;
                start = i + 1;
            }
        }
        if (depth != 0)
            throw new System.InvalidOperationException("[FAIL CLOSED] Startup Verification Probe: Biểu thức SQL sai cấu trúc ngoặc.");
        parts.Add(StripPracticeOuterParentheses(expression.Substring(start).Trim()));
        return parts.ToArray();
    }

    private static string StripPracticeOuterParentheses(string expression)
    {
        expression = expression.Trim();
        while (expression.Length >= 2 && expression[0] == '(' && expression[^1] == ')')
        {
            var depth = 0;
            var enclosesAll = true;
            for (var i = 0; i < expression.Length; i++)
            {
                if (expression[i] == '(') depth++;
                else if (expression[i] == ')') depth--;
                if (depth < 0)
                    throw new System.InvalidOperationException("[FAIL CLOSED] Startup Verification Probe: Biểu thức SQL sai cấu trúc ngoặc.");
                if (depth == 0 && i < expression.Length - 1)
                {
                    enclosesAll = false;
                    break;
                }
            }
            if (!enclosesAll || depth != 0) break;
            expression = expression.Substring(1, expression.Length - 2).Trim();
        }
        return expression;
    }

    private static void ValidatePracticeAnswersIndexPredicate(string indexName, string predicateExpr, bool isMainAnswerIndex)
    {
        if (string.IsNullOrWhiteSpace(predicateExpr))
        {
            throw new System.InvalidOperationException(
                $"[FAIL CLOSED] Startup Verification Probe: Predicate filter của index {indexName} không được rỗng!");
        }

        var norm = predicateExpr.Trim().ToLowerInvariant();
        if (norm.StartsWith("where"))
        {
            norm = norm.Substring(5).Trim();
        }
        norm = System.Text.RegularExpressions.Regex.Replace(norm, @"[()]+", " ");
        norm = System.Text.RegularExpressions.Regex.Replace(norm, @"\s+", " ").Trim();

        // 1. Tuyệt đối không được chứa toán tử ' or ' (từ chối OR TRUE, OR 1=1, v.v.)
        if (norm.Contains(" or "))
        {
            throw new System.InvalidOperationException(
                $"[FAIL CLOSED] Startup Verification Probe: Predicate filter của index {indexName} không được chứa toán tử OR! " +
                $"Hiện tại phát hiện OR trong: '{predicateExpr}'.");
        }

        // 2. Phải là phép hội (AND) của đúng 2 điều kiện
        var terms = norm.Split(new[] { " and " }, System.StringSplitOptions.TrimEntries | System.StringSplitOptions.RemoveEmptyEntries);
        if (terms.Length != 2)
        {
            throw new System.InvalidOperationException(
                $"[FAIL CLOSED] Startup Verification Probe: Predicate filter của index {indexName} phải có đúng 2 điều kiện nối bằng AND! " +
                $"Hiện tại phát hiện {terms.Length} điều kiện (từ chối điều kiện dư thừa). Hiện tại: '{predicateExpr}'.");
        }

        var termSet = new System.Collections.Generic.HashSet<string>(terms, System.StringComparer.OrdinalIgnoreCase);

        if (isMainAnswerIndex)
        {
            // Main index: WHERE is_follow_up = false AND parent_answer_id IS NULL
            bool hasIsFollowUpFalse = termSet.Contains("is_follow_up = false") || termSet.Contains("not is_follow_up");
            bool hasParentNull = termSet.Contains("parent_answer_id is null");

            if (!hasIsFollowUpFalse || !hasParentNull)
            {
                throw new System.InvalidOperationException(
                    $"[FAIL CLOSED] Startup Verification Probe: Index {indexName} sai predicate filter! " +
                    $"Cần đúng 2 điều kiện 'is_follow_up = false' và 'parent_answer_id IS NULL'. Hiện tại: '{predicateExpr}'.");
            }
        }
        else
        {
            // Follow-up index: WHERE is_follow_up = true AND parent_answer_id IS NOT NULL
            bool hasIsFollowUpTrue = termSet.Contains("is_follow_up = true") ||
                                    (termSet.Contains("is_follow_up") && !termSet.Contains("not is_follow_up") && !termSet.Contains("is_follow_up = false"));
            bool hasParentNotNull = termSet.Contains("parent_answer_id is not null");

            if (!hasIsFollowUpTrue || !hasParentNotNull)
            {
                throw new System.InvalidOperationException(
                    $"[FAIL CLOSED] Startup Verification Probe: Index {indexName} sai predicate filter! " +
                    $"Cần đúng 2 điều kiện 'is_follow_up = true' và 'parent_answer_id IS NOT NULL'. Hiện tại: '{predicateExpr}'.");
            }
        }
    }

    public static async Task ValidateOfficialExamSchemaPreflightAsync(OralExamDbContext context, CancellationToken cancellationToken = default)
    {
        if (!context.Database.IsRelational())
        {
            return;
        }

        var conn = context.Database.GetDbConnection();
        var wasClosed = conn.State != System.Data.ConnectionState.Open;
        if (wasClosed)
        {
            await conn.OpenAsync(cancellationToken);
        }

        try
        {
            using var cmd = conn.CreateCommand();

            // 1. official_exam_sessions duplicate (semester_id, course_id, exam_attempt_type)
            cmd.CommandText = "SELECT to_regclass('public.official_exam_sessions') IS NOT NULL;";
            if ((bool)(await cmd.ExecuteScalarAsync(cancellationToken))!)
            {
                cmd.CommandText = """
                    SELECT EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_schema = 'public' AND table_name = 'official_exam_sessions' AND column_name = 'exam_attempt_type'
                    );
                    """;
                var hasAttemptCol = (bool)(await cmd.ExecuteScalarAsync(cancellationToken))!;

                cmd.CommandText = """
                    SELECT EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_schema = 'public' AND table_name = 'official_exam_sessions' AND column_name = 'semester_id'
                    );
                    """;
                var hasSemesterCol = (bool)(await cmd.ExecuteScalarAsync(cancellationToken))!;

                cmd.CommandText = (hasSemesterCol, hasAttemptCol) switch
                {
                    (true, true) => """
                        SELECT COALESCE(oes.semester_id, c.semester_id) AS sem_id, oes.course_id, COALESCE(oes.exam_attempt_type, 'FE') AS attempt_type, COUNT(*) AS cnt
                        FROM official_exam_sessions oes
                        LEFT JOIN courses c ON c.id = oes.course_id
                        WHERE COALESCE(oes.semester_id, c.semester_id) IS NOT NULL
                        GROUP BY COALESCE(oes.semester_id, c.semester_id), oes.course_id, COALESCE(oes.exam_attempt_type, 'FE')
                        HAVING COUNT(*) > 1
                        LIMIT 10;
                        """,
                    (true, false) => """
                        SELECT COALESCE(oes.semester_id, c.semester_id) AS sem_id, oes.course_id, 'FE' AS attempt_type, COUNT(*) AS cnt
                        FROM official_exam_sessions oes
                        LEFT JOIN courses c ON c.id = oes.course_id
                        WHERE COALESCE(oes.semester_id, c.semester_id) IS NOT NULL
                        GROUP BY COALESCE(oes.semester_id, c.semester_id), oes.course_id
                        HAVING COUNT(*) > 1
                        LIMIT 10;
                        """,
                    (false, true) => """
                        SELECT c.semester_id AS sem_id, oes.course_id, COALESCE(oes.exam_attempt_type, 'FE') AS attempt_type, COUNT(*) AS cnt
                        FROM official_exam_sessions oes
                        LEFT JOIN courses c ON c.id = oes.course_id
                        WHERE c.semester_id IS NOT NULL
                        GROUP BY c.semester_id, oes.course_id, COALESCE(oes.exam_attempt_type, 'FE')
                        HAVING COUNT(*) > 1
                        LIMIT 10;
                        """,
                    (false, false) => """
                        SELECT c.semester_id AS sem_id, oes.course_id, 'FE' AS attempt_type, COUNT(*) AS cnt
                        FROM official_exam_sessions oes
                        LEFT JOIN courses c ON c.id = oes.course_id
                        WHERE c.semester_id IS NOT NULL
                        GROUP BY c.semester_id, oes.course_id
                        HAVING COUNT(*) > 1
                        LIMIT 10;
                        """
                };

                var dupSessions = new System.Collections.Generic.List<string>();
                using (var reader = await cmd.ExecuteReaderAsync(cancellationToken))
                {
                    while (await reader.ReadAsync(cancellationToken))
                    {
                        dupSessions.Add($"Semester={reader[0]}, Course={reader[1]}, Attempt={reader[2]}, Count={reader[3]}");
                    }
                }

                if (dupSessions.Count > 0)
                {
                    throw new System.InvalidOperationException(
                        $"[FAIL CLOSED] Phát hiện {dupSessions.Count} nhóm official_exam_sessions trùng (semester_id, course_id, exam_attempt_type): " +
                        $"{string.Join("; ", dupSessions)}. Không tự ý đổi sang RE; yêu cầu xử lý thủ công.");
                }
            }

            // 2. student_exam_tickets BYOD device binding duplicates & consistency
            cmd.CommandText = """
                SELECT EXISTS (
                    SELECT 1 FROM information_schema.columns
                    WHERE table_schema = 'public' AND table_name = 'student_exam_tickets' AND column_name = 'device_id'
                );
                """;
            if ((bool)(await cmd.ExecuteScalarAsync(cancellationToken))!)
            {
                cmd.CommandText = """
                    SELECT shift_id, device_id, COUNT(*) AS cnt
                    FROM student_exam_tickets
                    WHERE device_id IS NOT NULL
                    GROUP BY shift_id, device_id
                    HAVING COUNT(*) > 1
                    LIMIT 10;
                    """;
                var dupDevices = new System.Collections.Generic.List<string>();
                using (var reader = await cmd.ExecuteReaderAsync(cancellationToken))
                {
                    while (await reader.ReadAsync(cancellationToken))
                    {
                        dupDevices.Add($"Shift={reader[0]}, DeviceId={reader[1]}, Count={reader[2]}");
                    }
                }
                if (dupDevices.Count > 0)
                {
                    throw new System.InvalidOperationException(
                        $"[FAIL CLOSED] Phát hiện {dupDevices.Count} nhóm student_exam_tickets trùng (shift_id, device_id): {string.Join("; ", dupDevices)}.");
                }

                cmd.CommandText = """
                    SELECT EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_schema = 'public' AND table_name = 'student_exam_tickets' AND column_name = 'device_session_token_hash'
                    );
                    """;
                if ((bool)(await cmd.ExecuteScalarAsync(cancellationToken))!)
                {
                    cmd.CommandText = """
                        SELECT id FROM student_exam_tickets
                        WHERE device_id IS NULL AND device_session_token_hash IS NOT NULL
                        LIMIT 10;
                        """;
                    var invalidTokens = new System.Collections.Generic.List<string>();
                    using (var reader = await cmd.ExecuteReaderAsync(cancellationToken))
                    {
                        while (await reader.ReadAsync(cancellationToken))
                        {
                            invalidTokens.Add(reader[0].ToString() ?? string.Empty);
                        }
                    }
                    if (invalidTokens.Count > 0)
                    {
                        throw new System.InvalidOperationException(
                            $"[FAIL CLOSED] Phát hiện {invalidTokens.Count} vé student_exam_tickets có device_session_token_hash nhưng device_id NULL: {string.Join(", ", invalidTokens)}.");
                    }
                }
            }

            // 3. exam_question_submissions follow-up consistency & unique turn/parent
            cmd.CommandText = """
                SELECT EXISTS (
                    SELECT 1 FROM information_schema.columns
                    WHERE table_schema = 'public' AND table_name = 'exam_question_submissions' AND column_name = 'is_follow_up'
                );
                """;
            if ((bool)(await cmd.ExecuteScalarAsync(cancellationToken))!)
            {
                cmd.CommandText = """
                    SELECT id FROM exam_question_submissions
                    WHERE NOT (
                        (is_follow_up = false AND parent_submission_id IS NULL AND follow_up_index = 0 AND follow_up_prompt IS NULL)
                        OR
                        (is_follow_up = true AND parent_submission_id IS NOT NULL AND follow_up_index >= 1 AND follow_up_index <= 5 AND follow_up_prompt IS NOT NULL AND btrim(follow_up_prompt) <> '')
                    )
                    LIMIT 10;
                    """;
                var invalidSubs = new System.Collections.Generic.List<string>();
                using (var reader = await cmd.ExecuteReaderAsync(cancellationToken))
                {
                    while (await reader.ReadAsync(cancellationToken))
                    {
                        invalidSubs.Add(reader[0].ToString() ?? string.Empty);
                    }
                }
                if (invalidSubs.Count > 0)
                {
                    throw new System.InvalidOperationException(
                        $"[FAIL CLOSED] Phát hiện {invalidSubs.Count} bản ghi exam_question_submissions vi phạm ck_exam_question_submissions_follow_up_consistency: {string.Join(", ", invalidSubs)}.");
                }

                cmd.CommandText = """
                    SELECT ticket_id, exam_question_id, follow_up_index, COUNT(*) AS cnt
                    FROM exam_question_submissions
                    GROUP BY ticket_id, exam_question_id, follow_up_index
                    HAVING COUNT(*) > 1
                    LIMIT 10;
                    """;
                var dupTurns = new System.Collections.Generic.List<string>();
                using (var reader = await cmd.ExecuteReaderAsync(cancellationToken))
                {
                    while (await reader.ReadAsync(cancellationToken))
                    {
                        dupTurns.Add($"Ticket={reader[0]}, Question={reader[1]}, Turn={reader[2]}, Count={reader[3]}");
                    }
                }
                if (dupTurns.Count > 0)
                {
                    throw new System.InvalidOperationException(
                        $"[FAIL CLOSED] Phát hiện {dupTurns.Count} nhóm trùng (ticket_id, exam_question_id, follow_up_index) trên exam_question_submissions: {string.Join("; ", dupTurns)}.");
                }

                cmd.CommandText = """
                    SELECT ticket_id, parent_submission_id, COUNT(*) AS cnt
                    FROM exam_question_submissions
                    WHERE is_follow_up = true AND parent_submission_id IS NOT NULL
                    GROUP BY ticket_id, parent_submission_id
                    HAVING COUNT(*) > 1
                    LIMIT 10;
                    """;
                var dupParents = new System.Collections.Generic.List<string>();
                using (var reader = await cmd.ExecuteReaderAsync(cancellationToken))
                {
                    while (await reader.ReadAsync(cancellationToken))
                    {
                        dupParents.Add($"Ticket={reader[0]}, Parent={reader[1]}, Count={reader[2]}");
                    }
                }
                if (dupParents.Count > 0)
                {
                    throw new System.InvalidOperationException(
                        $"[FAIL CLOSED] Phát hiện {dupParents.Count} nhóm trùng (ticket_id, parent_submission_id) trên exam_question_submissions: {string.Join("; ", dupParents)}.");
                }
            }
        }
        finally
        {
            if (wasClosed && conn.State == System.Data.ConnectionState.Open)
            {
                await conn.CloseAsync();
            }
        }
    }

    public static async Task VerifyOfficialExamSchemaInvariantsAsync(OralExamDbContext context, CancellationToken cancellationToken = default)
    {
        if (!context.Database.IsRelational())
        {
            return;
        }

        var conn = context.Database.GetDbConnection();
        var wasClosed = conn.State != System.Data.ConnectionState.Open;
        if (wasClosed)
        {
            await conn.OpenAsync(cancellationToken);
        }

        try
        {
            using var cmd = conn.CreateCommand();

            // 1. Physical presence of official_exam_candidates & M4 NOT NULL on official_exam_sessions.semester_id
            cmd.CommandText = "SELECT to_regclass('public.official_exam_candidates') IS NOT NULL;";
            if (!(bool)(await cmd.ExecuteScalarAsync(cancellationToken))!)
            {
                throw new System.InvalidOperationException(
                    "[FAIL CLOSED] Startup Verification Probe: Bảng official_exam_candidates không tồn tại trong cơ sở dữ liệu!");
            }

            cmd.CommandText = """
                SELECT is_nullable
                FROM information_schema.columns
                WHERE table_schema = 'public'
                  AND table_name = 'official_exam_sessions'
                  AND column_name = 'semester_id';
                """;
            var semesterNullable = (await cmd.ExecuteScalarAsync(cancellationToken))?.ToString();
            if (!string.Equals(semesterNullable, "NO", System.StringComparison.OrdinalIgnoreCase))
            {
                throw new System.InvalidOperationException(
                    "[FAIL CLOSED] Startup Verification Probe: Cột official_exam_sessions.semester_id bắt buộc phải là NOT NULL (M4)!");
            }

            // 2. Verify required validated CHECK constraints
            var requiredChecks = new (string Table, string Constraint)[]
            {
                ("official_exam_sessions", "ck_official_sessions_attempt_type"),
                ("real_exam_session_shifts", "ck_shifts_distinct_proctor_examiner"),
                ("real_exam_session_shifts", "ck_shifts_certificate_hash_len"),
                ("student_exam_tickets", "ck_tickets_device_unlock_count"),
                ("student_exam_tickets", "ck_tickets_device_binding_consistency"),
                ("exam_question_submissions", "ck_exam_question_submissions_follow_up_index"),
                ("exam_question_submissions", "ck_exam_question_submissions_follow_up_consistency"),
                ("exam_question_submissions", "ck_exam_question_submissions_transcript_edit_ratio"),
                ("exam_questions", "ck_exam_questions_bank_curation_status"),
                ("exam_sets", "ck_exam_sets_status")
            };

            cmd.CommandText = """
                SELECT c.relname AS tbl, con.conname, con.convalidated, pg_get_constraintdef(con.oid) AS condef
                FROM pg_constraint con
                JOIN pg_class c ON c.oid = con.conrelid
                JOIN pg_namespace n ON n.oid = c.relnamespace
                WHERE n.nspname = 'public'
                  AND con.conname IN (
                      'ck_official_sessions_attempt_type',
                      'ck_shifts_distinct_proctor_examiner',
                      'ck_shifts_certificate_hash_len',
                      'ck_tickets_device_unlock_count',
                      'ck_tickets_device_binding_consistency',
                      'ck_exam_question_submissions_follow_up_index',
                      'ck_exam_question_submissions_follow_up_consistency',
                      'ck_exam_question_submissions_transcript_edit_ratio',
                      'ck_exam_questions_bank_curation_status',
                      'ck_exam_sets_status'
                  );
                """;
            var foundChecks = new System.Collections.Generic.Dictionary<string, (bool Validated, string Definition)>(System.StringComparer.OrdinalIgnoreCase);
            using (var reader = await cmd.ExecuteReaderAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    foundChecks[$"{reader.GetString(0)}.{reader.GetString(1)}"] = (reader.GetBoolean(2), reader.GetString(3));
                }
            }

            foreach (var (table, constraint) in requiredChecks)
            {
                if (!foundChecks.TryGetValue($"{table}.{constraint}", out var checkInfo))
                {
                    throw new System.InvalidOperationException(
                        $"[FAIL CLOSED] Startup Verification Probe: Thiếu CHECK constraint {constraint} trên bảng {table}!");
                }
                if (!checkInfo.Validated)
                {
                    throw new System.InvalidOperationException(
                        $"[FAIL CLOSED] Startup Verification Probe: CHECK constraint {constraint} trên bảng {table} chưa được xác thực (convalidated = false)!");
                }
                if (string.Equals(constraint, "ck_exam_sets_status", System.StringComparison.OrdinalIgnoreCase) &&
                    !checkInfo.Definition.Contains("NEEDS_REVISION", System.StringComparison.OrdinalIgnoreCase))
                {
                    throw new System.InvalidOperationException(
                        $"[FAIL CLOSED] Startup Verification Probe: CHECK constraint ck_exam_sets_status trên bảng exam_sets thiếu trạng thái 'NEEDS_REVISION'! Hiện tại: {checkInfo.Definition}");
                }
            }

            // 3. Verify pg_trgm extension (M3)
            cmd.CommandText = "SELECT EXISTS (SELECT 1 FROM pg_extension WHERE extname = 'pg_trgm');";
            if (!(bool)(await cmd.ExecuteScalarAsync(cancellationToken))!)
            {
                throw new System.InvalidOperationException(
                    "[FAIL CLOSED] Startup Verification Probe: Thiếu extension pg_trgm bắt buộc cho lọc trùng ngân hàng đề (M3)!");
            }

            // 4. Verify required unique & lookup indexes + absence of legacy uq_ticket_question & uq_exam_sets_course_code
            var requiredIndexes = new (string Table, string Index, bool MustBeUnique)[]
            {
                ("official_exam_candidates", "uq_official_exam_candidates_session_student", true),
                ("official_exam_sessions", "uq_official_exam_sessions_semester_course_attempt", true),
                ("real_exam_session_shifts", "ix_real_exam_shifts_certificate_hash", false),
                ("student_exam_tickets", "uq_tickets_shift_device", true),
                ("exam_anti_cheat_logs", "uq_exam_anti_cheat_logs_ticket_client_event", true),
                ("exam_question_submissions", "uq_exam_submissions_ticket_question_turn", true),
                ("exam_question_submissions", "uq_exam_submissions_ticket_parent", true),
                ("exam_questions", "ix_exam_questions_course_curation_pool", false),
                ("exam_questions", "ix_exam_questions_content_trgm", false),
                ("exam_questions", "ix_exam_questions_title_trgm", false),
                ("exam_sets", "ix_exam_sets_structure_status_active", false),
                ("exam_sets", "uq_exam_sets_active_course_set_code", true),
                ("exam_sets", "uq_exam_sets_active_sample_structure", true)
            };

            cmd.CommandText = """
                SELECT t.relname AS tbl, i.relname AS idx, ix.indisunique
                FROM pg_class t
                JOIN pg_index ix ON t.oid = ix.indrelid
                JOIN pg_class i ON i.oid = ix.indexrelid
                JOIN pg_namespace n ON n.oid = t.relnamespace
                WHERE n.nspname = 'public'
                  AND i.relname IN (
                      'uq_official_exam_candidates_session_student',
                      'uq_official_exam_sessions_semester_course_attempt',
                      'ix_real_exam_shifts_certificate_hash',
                      'uq_tickets_shift_device',
                      'uq_exam_anti_cheat_logs_ticket_client_event',
                      'uq_exam_submissions_ticket_question_turn',
                      'uq_exam_submissions_ticket_parent',
                      'ix_exam_questions_course_curation_pool',
                      'ix_exam_questions_content_trgm',
                      'ix_exam_questions_title_trgm',
                      'ix_exam_sets_structure_status_active',
                      'uq_exam_sets_active_course_set_code',
                      'uq_exam_sets_active_sample_structure',
                      'uq_exam_sets_course_code',
                      'uq_ticket_question'
                  );
                """;
            var foundIndexes = new System.Collections.Generic.Dictionary<string, (string Table, bool IsUnique)>(System.StringComparer.OrdinalIgnoreCase);
            using (var reader = await cmd.ExecuteReaderAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    foundIndexes[reader.GetString(1)] = (reader.GetString(0), reader.GetBoolean(2));
                }
            }

            if (foundIndexes.ContainsKey("uq_ticket_question"))
            {
                throw new System.InvalidOperationException(
                    "[FAIL CLOSED] Startup Verification Probe: Index cũ uq_ticket_question trên exam_question_submissions vẫn còn tồn tại và sẽ chặn follow-up MF-04!");
            }

            if (foundIndexes.ContainsKey("uq_exam_sets_course_code"))
            {
                throw new System.InvalidOperationException(
                    "[FAIL CLOSED] Startup Verification Probe: Index cũ uq_exam_sets_course_code trên exam_sets vẫn còn tồn tại và sẽ chặn tái sử dụng SetCode đã xóa mềm!");
            }

            foreach (var (table, index, mustBeUnique) in requiredIndexes)
            {
                if (!foundIndexes.TryGetValue(index, out var info) || !string.Equals(info.Table, table, System.StringComparison.OrdinalIgnoreCase))
                {
                    throw new System.InvalidOperationException(
                        $"[FAIL CLOSED] Startup Verification Probe: Thiếu index {index} trên bảng {table}!");
                }
                if (mustBeUnique && !info.IsUnique)
                {
                    throw new System.InvalidOperationException(
                        $"[FAIL CLOSED] Startup Verification Probe: Index {index} trên bảng {table} không phải UNIQUE!");
                }
            }
        }
        finally
        {
            if (wasClosed && conn.State == System.Data.ConnectionState.Open)
            {
                await conn.CloseAsync();
            }
        }
    }
}
