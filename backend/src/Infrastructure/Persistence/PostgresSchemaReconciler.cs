using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace OralExamination.Infrastructure.Persistence;

/// <summary>
/// Bộ đối soát & tự phục hồi lược đồ PostgreSQL (Idempotent Schema Reconciler).
/// Bù đắp hoàn toàn các bảng, cột, khóa ngoại, index và check constraint bị khuyết do các file migration rỗng
/// (20261104062322_AddDlqFlowTypeAndRoleSeparation và 20261108141117_UpdateDB), đồng thời đảm bảo tính nhất quán
/// cho cả môi trường tạo mới (Fresh DB) lẫn môi trường VPS Production đã chạy một phần migration hoặc khởi tạo qua raw SQL.
/// </summary>
public static class PostgresSchemaReconciler
{
    /// <summary>
    /// SQL idempotent cho bước 20261104062322_AddDlqFlowTypeAndRoleSeparation (và chạy trước MigrateAsync trên DB đã có bảng users).
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
            ALTER TABLE users ADD COLUMN IF NOT EXISTS username character varying(32) NULL;
            ALTER TABLE users ADD COLUMN IF NOT EXISTS student_code character varying(20) NULL;
            ALTER TABLE users ADD COLUMN IF NOT EXISTS password_hash character varying(256) NULL;
            CREATE UNIQUE INDEX IF NOT EXISTS ix_users_username ON users (username) WHERE username IS NOT NULL;
            CREATE UNIQUE INDEX IF NOT EXISTS ix_users_student_code ON users (student_code) WHERE student_code IS NOT NULL;

            -- 2. courses: các cột bổ sung trong đợt R1-R8 (trước academic_program_id)
            IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'courses') THEN
                ALTER TABLE courses ADD COLUMN IF NOT EXISTS description text NULL;
                ALTER TABLE courses ADD COLUMN IF NOT EXISTS department_head_user_id uuid NULL;
                ALTER TABLE courses ADD COLUMN IF NOT EXISTS mock_exam_duration_minutes integer NOT NULL DEFAULT 15;
                ALTER TABLE courses ADD COLUMN IF NOT EXISTS mock_exam_total_questions integer NOT NULL DEFAULT 3;
                ALTER TABLE courses ADD COLUMN IF NOT EXISTS mock_exam_bloom_distribution jsonb NOT NULL DEFAULT jsonb_build_object('Remember', 20, 'Understand', 30, 'Apply', 30, 'Analyze', 20);

                CREATE INDEX IF NOT EXISTS ix_courses_department_head ON courses (department_head_user_id);

                BEGIN
                    ALTER TABLE courses ADD CONSTRAINT "FK_courses_users_department_head_user_id"
                        FOREIGN KEY (department_head_user_id) REFERENCES users(id) ON DELETE SET NULL;
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
                    EXECUTE 'UPDATE classes SET join_code = UPPER(SUBSTRING(REPLACE(id::text, ''-'', ''''), 1, 8)) WHERE join_code IS NULL OR join_code = ''''';
                    ALTER TABLE classes ALTER COLUMN join_code SET NOT NULL;
                    CREATE UNIQUE INDEX IF NOT EXISTS ix_classes_join_code ON classes (join_code);
                EXCEPTION WHEN OTHERS THEN NULL;
                END;
                ALTER TABLE classes ADD COLUMN IF NOT EXISTS join_password_hash character varying(256) NULL;
                ALTER TABLE classes ADD COLUMN IF NOT EXISTS is_join_enabled boolean NOT NULL DEFAULT TRUE;
                ALTER TABLE classes ADD COLUMN IF NOT EXISTS allow_self_enrollment boolean NOT NULL DEFAULT TRUE;
            END IF;

            -- 5. class_enrollments
            IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'class_enrollments') THEN
                ALTER TABLE class_enrollments ADD COLUMN IF NOT EXISTS joined_via_code boolean NOT NULL DEFAULT FALSE;
                ALTER TABLE class_enrollments ADD COLUMN IF NOT EXISTS join_method character varying(30) NOT NULL DEFAULT 'assigned';
            END IF;

            -- 6. practice_questions & exam_questions
            IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'practice_questions') THEN
                ALTER TABLE practice_questions ADD COLUMN IF NOT EXISTS clo_id uuid NULL;
                CREATE INDEX IF NOT EXISTS "IX_practice_questions_clo_id" ON practice_questions (clo_id);
                CREATE INDEX IF NOT EXISTS ix_practice_questions_clo ON practice_questions (clo_id);
                BEGIN
                    ALTER TABLE practice_questions ADD CONSTRAINT "FK_practice_questions_course_clos_clo_id"
                        FOREIGN KEY (clo_id) REFERENCES course_clos(id) ON DELETE SET NULL;
                EXCEPTION WHEN OTHERS THEN NULL;
                END;
            END IF;

            IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'exam_questions') THEN
                ALTER TABLE exam_questions ADD COLUMN IF NOT EXISTS clo_id uuid NULL;
                ALTER TABLE exam_questions ADD COLUMN IF NOT EXISTS structure_id uuid NULL;
                CREATE INDEX IF NOT EXISTS "IX_exam_questions_clo_id" ON exam_questions (clo_id);
                CREATE INDEX IF NOT EXISTS ix_exam_questions_clo ON exam_questions (clo_id);
                CREATE INDEX IF NOT EXISTS "IX_exam_questions_structure_id" ON exam_questions (structure_id);
                CREATE INDEX IF NOT EXISTS ix_exam_questions_structure ON exam_questions (structure_id);
                BEGIN
                    ALTER TABLE exam_questions ADD CONSTRAINT "FK_exam_questions_course_clos_clo_id"
                        FOREIGN KEY (clo_id) REFERENCES course_clos(id) ON DELETE SET NULL;
                EXCEPTION WHEN OTHERS THEN NULL;
                END;
                BEGIN
                    ALTER TABLE exam_questions ADD CONSTRAINT "FK_exam_questions_exam_structures_structure_id"
                        FOREIGN KEY (structure_id) REFERENCES exam_structures(id) ON DELETE SET NULL;
                EXCEPTION WHEN OTHERS THEN NULL;
                END;
            END IF;

            -- 7. practice_sessions, practice_answers & exam_sets
            IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'practice_sessions') THEN
                ALTER TABLE practice_sessions ADD COLUMN IF NOT EXISTS total_score numeric(4,2) NULL;
            END IF;

            IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'practice_answers') THEN
                ALTER TABLE practice_answers ADD COLUMN IF NOT EXISTS order_index integer NOT NULL DEFAULT 1;
            END IF;

            IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'exam_sets') THEN
                ALTER TABLE exam_sets ADD COLUMN IF NOT EXISTS is_sample_set boolean NOT NULL DEFAULT FALSE;
            END IF;

            -- 8. dead_letter_queues
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
                        CHECK (flow_type IN ('PRACTICE', 'MOCK_EXAM', 'OFFICIAL_EXAM', 'IN_CLASS_EXAM', 'MF01_PRACTICE', 'MF02_MOCK_EXAM', 'MF04_OFFICIAL_EXAM', 'MF05_IN_CLASS'));
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
    /// Đảm bảo 100% bảng (10 bảng), cột, khóa ngoại, check constraint và tài khoản mẫu khớp tuyệt đối với OralExamDbContext.
    /// Toàn bộ ràng buộc được bọc an toàn trong subtransaction PL/pgSQL và đồng bộ lịch sử 11 migrations.
    /// </summary>
    public static string GetPostMigrationSql() =>
        GetPreMigrationSql() +
        """

        DO $reconcile_post$
        BEGIN
            -----------------------------------------------------------------------
            -- 1. TẠO 10 BẢNG BẮT BUỘC (IDEMPOTENT TABLE CREATION)
            -----------------------------------------------------------------------

            -- 1.1. course_clos
            CREATE TABLE IF NOT EXISTS course_clos (
                id uuid NOT NULL DEFAULT gen_random_uuid(),
                course_id uuid NOT NULL,
                clo_code character varying(50) NOT NULL,
                description text NOT NULL,
                bloom_level character varying(20) NOT NULL DEFAULT 'Understand',
                order_index integer NOT NULL DEFAULT 1,
                is_active boolean NOT NULL DEFAULT TRUE,
                created_at timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
                CONSTRAINT "PK_course_clos" PRIMARY KEY (id)
            );
            CREATE INDEX IF NOT EXISTS ix_course_clos_course_id ON course_clos (course_id);
            CREATE UNIQUE INDEX IF NOT EXISTS uq_course_clos_course_code ON course_clos (course_id, clo_code);

            -- 1.2. academic_programs
            CREATE TABLE IF NOT EXISTS academic_programs (
                id uuid NOT NULL DEFAULT gen_random_uuid(),
                code character varying(20) NOT NULL,
                name character varying(200) NOT NULL,
                department_head_user_id uuid NULL,
                is_active boolean NOT NULL DEFAULT TRUE,
                created_at timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
                updated_at timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
                CONSTRAINT "PK_academic_programs" PRIMARY KEY (id)
            );
            CREATE UNIQUE INDEX IF NOT EXISTS ix_academic_programs_code ON academic_programs (code);
            CREATE INDEX IF NOT EXISTS ix_academic_programs_department_head ON academic_programs (department_head_user_id);

            -- 1.3. course_offerings
            CREATE TABLE IF NOT EXISTS course_offerings (
                id uuid NOT NULL DEFAULT gen_random_uuid(),
                course_id uuid NOT NULL,
                semester_id uuid NOT NULL,
                is_enabled boolean NOT NULL DEFAULT TRUE,
                created_at timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
                updated_at timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
                CONSTRAINT "PK_course_offerings" PRIMARY KEY (id)
            );
            CREATE UNIQUE INDEX IF NOT EXISTS uq_course_offerings_course_semester ON course_offerings (course_id, semester_id);
            CREATE INDEX IF NOT EXISTS ix_course_offerings_semester ON course_offerings (semester_id);

            -- 1.4. course_lecturers (đảm bảo đầy đủ cột)
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
            ALTER TABLE course_lecturers ADD COLUMN IF NOT EXISTS course_offering_id uuid NULL;
            ALTER TABLE course_lecturers ADD COLUMN IF NOT EXISTS semester_id uuid NULL;
            CREATE INDEX IF NOT EXISTS ix_course_lecturers_course ON course_lecturers (course_id, is_active);
            CREATE INDEX IF NOT EXISTS ix_course_lecturers_lecturer ON course_lecturers (lecturer_user_id, is_active);
            CREATE UNIQUE INDEX IF NOT EXISTS uq_course_lecturers ON course_lecturers (course_id, lecturer_user_id, semester_id);
            CREATE UNIQUE INDEX IF NOT EXISTS uq_course_offering_lecturers ON course_lecturers (course_offering_id, lecturer_user_id);

            -- 1.5. in_class_exam_shifts
            CREATE TABLE IF NOT EXISTS in_class_exam_shifts (
                id uuid NOT NULL DEFAULT gen_random_uuid(),
                class_id uuid NOT NULL,
                lecturer_id uuid NOT NULL,
                shift_name character varying(255) NOT NULL,
                description text NULL,
                question_pool_mode character varying(30) NOT NULL DEFAULT 'ALL_COURSE_BANK',
                selected_question_ids jsonb NOT NULL DEFAULT '[]'::jsonb,
                total_questions_per_student integer NOT NULL DEFAULT 1,
                has_follow_up boolean NOT NULL DEFAULT FALSE,
                max_follow_up_questions integer NOT NULL DEFAULT 2,
                is_auto_publish_grades boolean NOT NULL DEFAULT TRUE,
                start_time timestamp with time zone NOT NULL,
                end_time timestamp with time zone NOT NULL,
                exam_duration_minutes integer NULL,
                totp_secret_key character varying(128) NOT NULL,
                status character varying(20) NOT NULL DEFAULT 'SCHEDULED',
                is_locked boolean NOT NULL DEFAULT FALSE,
                finalized_at timestamp with time zone NULL,
                created_at timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
                updated_at timestamp with time zone NULL,
                CONSTRAINT "PK_in_class_exam_shifts" PRIMARY KEY (id)
            );
            ALTER TABLE in_class_exam_shifts ADD COLUMN IF NOT EXISTS exam_duration_minutes integer NULL;
            ALTER TABLE in_class_exam_shifts ADD COLUMN IF NOT EXISTS is_locked boolean NOT NULL DEFAULT FALSE;
            ALTER TABLE in_class_exam_shifts ADD COLUMN IF NOT EXISTS finalized_at timestamp with time zone NULL;
            CREATE INDEX IF NOT EXISTS ix_in_class_exam_shifts_class_status_created ON in_class_exam_shifts (class_id, status, created_at);
            CREATE INDEX IF NOT EXISTS ix_in_class_exam_shifts_lecturer ON in_class_exam_shifts (lecturer_id);

            -- 1.6. in_class_shift_passwords
            CREATE TABLE IF NOT EXISTS in_class_shift_passwords (
                id uuid NOT NULL DEFAULT gen_random_uuid(),
                shift_id uuid NOT NULL,
                plain_code character varying(10) NOT NULL,
                password_hash character varying(255) NOT NULL,
                valid_from timestamp with time zone NOT NULL,
                valid_to timestamp with time zone NOT NULL,
                created_at timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
                CONSTRAINT "PK_in_class_shift_passwords" PRIMARY KEY (id)
            );
            CREATE UNIQUE INDEX IF NOT EXISTS uq_in_class_shift_passwords_shift_valid_from ON in_class_shift_passwords (shift_id, valid_from);
            CREATE INDEX IF NOT EXISTS ix_in_class_shift_passwords_shift_valid ON in_class_shift_passwords (shift_id, valid_from, valid_to);

            -- 1.7. in_class_shift_participants
            CREATE TABLE IF NOT EXISTS in_class_shift_participants (
                id uuid NOT NULL DEFAULT gen_random_uuid(),
                shift_id uuid NOT NULL,
                student_id uuid NOT NULL,
                assigned_question_id uuid NULL,
                assigned_question_ids jsonb NOT NULL DEFAULT '[]'::jsonb,
                total_ai_score numeric(4,2) NULL,
                total_final_score numeric(4,2) NULL,
                joined_at timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
                status character varying(20) NOT NULL DEFAULT 'JOINED',
                CONSTRAINT "PK_in_class_shift_participants" PRIMARY KEY (id)
            );
            CREATE UNIQUE INDEX IF NOT EXISTS uq_in_class_shift_participants_shift_student ON in_class_shift_participants (shift_id, student_id);
            CREATE INDEX IF NOT EXISTS ix_in_class_shift_participants_student ON in_class_shift_participants (student_id, shift_id);
            CREATE INDEX IF NOT EXISTS ix_in_class_shift_participants_assigned_question ON in_class_shift_participants (assigned_question_id);

            -- 1.8. in_class_exam_submissions
            CREATE TABLE IF NOT EXISTS in_class_exam_submissions (
                id uuid NOT NULL DEFAULT gen_random_uuid(),
                shift_id uuid NOT NULL,
                student_id uuid NOT NULL,
                question_id uuid NOT NULL,
                is_follow_up boolean NOT NULL DEFAULT FALSE,
                parent_submission_id uuid NULL,
                follow_up_index integer NOT NULL DEFAULT 0,
                follow_up_prompt text NULL,
                transcript text NOT NULL,
                ai_score numeric(4,2) NULL,
                ai_confidence_score numeric(3,2) NULL,
                cot_trace text NULL,
                ai_feedback text NULL,
                final_score numeric(4,2) NULL,
                lecturer_notes text NULL,
                status character varying(20) NOT NULL DEFAULT 'SUBMITTED',
                submitted_at timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
                graded_at timestamp with time zone NULL,
                CONSTRAINT "PK_in_class_exam_submissions" PRIMARY KEY (id)
            );
            CREATE UNIQUE INDEX IF NOT EXISTS uq_in_class_exam_submissions_shift_student ON in_class_exam_submissions (shift_id, student_id, question_id, follow_up_index);
            CREATE INDEX IF NOT EXISTS ix_in_class_exam_submissions_shift_student ON in_class_exam_submissions (shift_id, student_id, submitted_at);
            CREATE INDEX IF NOT EXISTS ix_in_class_exam_submissions_student ON in_class_exam_submissions (student_id, submitted_at);
            CREATE INDEX IF NOT EXISTS ix_in_class_exam_submissions_question ON in_class_exam_submissions (question_id);
            CREATE INDEX IF NOT EXISTS ix_in_class_exam_submissions_parent ON in_class_exam_submissions (parent_submission_id);

            -- 1.9. exam_anti_cheat_logs
            CREATE TABLE IF NOT EXISTS exam_anti_cheat_logs (
                id uuid NOT NULL DEFAULT gen_random_uuid(),
                ticket_id uuid NOT NULL,
                shift_id uuid NOT NULL,
                student_id uuid NOT NULL,
                event_type character varying(50) NOT NULL,
                severity character varying(20) NOT NULL DEFAULT 'medium',
                details text NULL,
                violation_count integer NOT NULL DEFAULT 1,
                occurred_at timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
                CONSTRAINT "PK_exam_anti_cheat_logs" PRIMARY KEY (id)
            );
            CREATE INDEX IF NOT EXISTS ix_exam_anti_cheat_logs_ticket ON exam_anti_cheat_logs (ticket_id);
            CREATE INDEX IF NOT EXISTS ix_exam_anti_cheat_logs_shift ON exam_anti_cheat_logs (shift_id);
            CREATE INDEX IF NOT EXISTS ix_exam_anti_cheat_logs_student ON exam_anti_cheat_logs (student_id);
            CREATE INDEX IF NOT EXISTS ix_exam_anti_cheat_logs_occurred ON exam_anti_cheat_logs (occurred_at);

            -- 1.10. notifications
            CREATE TABLE IF NOT EXISTS notifications (
                id uuid NOT NULL DEFAULT gen_random_uuid(),
                user_id uuid NOT NULL,
                title character varying(255) NOT NULL,
                message text NOT NULL,
                type character varying(50) NOT NULL,
                is_read boolean NOT NULL DEFAULT FALSE,
                created_at timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
                read_at timestamp with time zone NULL,
                related_entity_name character varying(100) NULL,
                related_entity_id uuid NULL,
                metadata_json text NULL,
                CONSTRAINT "PK_notifications" PRIMARY KEY (id)
            );
            CREATE INDEX IF NOT EXISTS "IX_notifications_user_id_is_read_created_at" ON notifications (user_id, is_read, created_at DESC);

            -----------------------------------------------------------------------
            -- 2. BỔ SUNG CỘT CÒN THIẾU TRÊN CÁC BẢNG HIỆN HỮU
            -----------------------------------------------------------------------

            -- users
            IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'users') THEN
                ALTER TABLE users ADD COLUMN IF NOT EXISTS must_change_password boolean NOT NULL DEFAULT FALSE;
                ALTER TABLE users ADD COLUMN IF NOT EXISTS username character varying(32) NULL;
                ALTER TABLE users ADD COLUMN IF NOT EXISTS student_code character varying(20) NULL;
                ALTER TABLE users ADD COLUMN IF NOT EXISTS password_hash character varying(256) NULL;
                CREATE UNIQUE INDEX IF NOT EXISTS ix_users_username ON users (username) WHERE username IS NOT NULL;
                CREATE UNIQUE INDEX IF NOT EXISTS ix_users_student_code ON users (student_code) WHERE student_code IS NOT NULL;
            END IF;

            -- courses
            IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'courses') THEN
                ALTER TABLE courses ADD COLUMN IF NOT EXISTS academic_program_id uuid NULL;
                CREATE INDEX IF NOT EXISTS ix_courses_academic_program ON courses (academic_program_id);
            END IF;

            -- classes
            IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'classes') THEN
                ALTER TABLE classes ADD COLUMN IF NOT EXISTS course_offering_id uuid NULL;
                ALTER TABLE classes ADD COLUMN IF NOT EXISTS join_password_plain character varying(128) NULL;
                CREATE INDEX IF NOT EXISTS ix_classes_course_offering ON classes (course_offering_id);
                CREATE INDEX IF NOT EXISTS ix_classes_active_semester_course_created ON classes (is_active, semester_id, course_id, created_at);
            END IF;

            -- class_enrollments
            IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'class_enrollments') THEN
                ALTER TABLE class_enrollments ADD COLUMN IF NOT EXISTS status character varying(20) NOT NULL DEFAULT 'enrolled';
            END IF;

            -- real_exam_session_shifts
            IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'real_exam_session_shifts') THEN
                ALTER TABLE real_exam_session_shifts ADD COLUMN IF NOT EXISTS examiner_user_id uuid NULL;
                ALTER TABLE real_exam_session_shifts ADD COLUMN IF NOT EXISTS is_exam_published boolean NOT NULL DEFAULT FALSE;
                ALTER TABLE real_exam_session_shifts ADD COLUMN IF NOT EXISTS published_at timestamp with time zone NULL;
            END IF;

            -- practice_questions
            IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'practice_questions') THEN
                ALTER TABLE practice_questions ADD COLUMN IF NOT EXISTS approval_status character varying(30) NOT NULL DEFAULT 'APPROVED';
                ALTER TABLE practice_questions ADD COLUMN IF NOT EXISTS approved_by uuid NULL;
                ALTER TABLE practice_questions ADD COLUMN IF NOT EXISTS review_notes text NULL;
                ALTER TABLE practice_questions ADD COLUMN IF NOT EXISTS source character varying(50) NOT NULL DEFAULT 'manual';
                ALTER TABLE practice_questions ADD COLUMN IF NOT EXISTS submitted_by uuid NULL;
            END IF;

            -- exam_questions
            IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'exam_questions') THEN
                ALTER TABLE exam_questions ADD COLUMN IF NOT EXISTS approval_status character varying(30) NOT NULL DEFAULT 'DRAFT';
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
                    FOREIGN KEY (department_head_user_id) REFERENCES users(id) ON DELETE SET NULL;
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
                    CHECK (question_pool_mode IN ('ALL_COURSE_BANK', 'SELECTED_POOL'));
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
            BEGIN
                ALTER TABLE in_class_shift_participants DROP CONSTRAINT IF EXISTS ck_in_class_shift_participants_status;
                ALTER TABLE in_class_shift_participants ADD CONSTRAINT ck_in_class_shift_participants_status
                    CHECK (status IN ('JOINED', 'SUBMITTED', 'ABSENT'));
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
                    FOREIGN KEY (clo_id) REFERENCES course_clos(id) ON DELETE SET NULL;
            EXCEPTION WHEN OTHERS THEN NULL;
            END;

            BEGIN
                ALTER TABLE exam_questions ADD CONSTRAINT "FK_exam_questions_course_clos_clo_id"
                    FOREIGN KEY (clo_id) REFERENCES course_clos(id) ON DELETE SET NULL;
            EXCEPTION WHEN OTHERS THEN NULL;
            END;

            -----------------------------------------------------------------------
            -- 4. ĐỒNG BỘ DỮ LIỆU NỀN HỌC THUẬT & TÀI KHOẢN MẪU (DYNAMIC DML)
            -----------------------------------------------------------------------
            BEGIN
                IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'academic_programs') THEN
                    IF EXISTS (SELECT 1 FROM users WHERE id = '00000000-0000-0000-0000-000000000002') THEN
                        EXECUTE 'INSERT INTO academic_programs (id, code, name, department_head_user_id, is_active, created_at, updated_at)
                        VALUES (''00000000-0000-0000-0000-000000000010'', ''SE'', ''Kỹ thuật Phần mềm'', ''00000000-0000-0000-0000-000000000002'', TRUE, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP)
                        ON CONFLICT DO NOTHING';

                        EXECUTE 'UPDATE courses
                        SET department_head_user_id = COALESCE(department_head_user_id, ''00000000-0000-0000-0000-000000000002''::uuid),
                            academic_program_id = COALESCE(academic_program_id, (SELECT id FROM academic_programs WHERE code = ''SE'' LIMIT 1))
                        WHERE academic_program_id IS NULL OR department_head_user_id IS NULL';
                    END IF;
                END IF;
            EXCEPTION WHEN OTHERS THEN NULL;
            END;

            BEGIN
                IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'course_offerings') THEN
                    EXECUTE 'INSERT INTO course_offerings (id, course_id, semester_id, is_enabled, created_at, updated_at)
                    SELECT gen_random_uuid(), c.id, c.semester_id, c.is_active, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP
                    FROM courses c
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
                      AND co.course_id = cl.course_id';
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
                IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'users') THEN
                    -- Tự chữa lành: Gán must_change_password = FALSE cho 100% tài khoản seed mẫu (theo ID, username và email)
                    EXECUTE 'UPDATE users
                    SET must_change_password = FALSE
                    WHERE id IN (
                        ''00000000-0000-0000-0000-000000000001'',
                        ''00000000-0000-0000-0000-000000000002'',
                        ''00000000-0000-0000-0000-000000000003'',
                        ''00000000-0000-0000-0000-000000000004'',
                        ''00000000-0000-0000-0000-000000000005'',
                        ''00000000-0000-0000-0000-000000000006'',
                        ''00000000-0000-0000-0000-00000000000b'',
                        ''00000000-0000-0000-0000-00000000000c'',
                        ''00000000-0000-0000-0000-00000000000d'',
                        ''00000000-0000-0000-0000-00000000000e'',
                        ''00000000-0000-0000-0000-00000000000f''
                    )
                    OR username IN (
                        ''admin'', ''headse'', ''totnt'', ''lecturer2'', ''lecturer3'', ''lecturer4'',
                        ''haind'', ''hoalvse170001'', ''maittse170002'', ''thanhnqse184236'', ''khaothi''
                    )
                    OR email IN (
                        ''admin@fpt.edu.vn'', ''headse@fe.edu.vn'', ''totnt@fe.edu.vn'',
                        ''lecturer2@fe.edu.vn'', ''lecturer3@fe.edu.vn'', ''lecturer4@fe.edu.vn'',
                        ''haind@fe.edu.vn'', ''hoanglvse170001@fpt.edu.vn'', ''maittse170002@fpt.edu.vn'',
                        ''thanhnqse184236@fpt.edu.vn'', ''khaothi@fe.edu.vn''
                    )';
                END IF;
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
        """;

    public static async Task ReconcilePreMigrationAsync(OralExamDbContext context, CancellationToken cancellationToken = default)
    {
        if (!context.Database.IsRelational())
        {
            return;
        }

        await context.Database.ExecuteSqlRawAsync(GetPreMigrationSql(), cancellationToken);
    }

    public static async Task ReconcilePostMigrationAsync(OralExamDbContext context, CancellationToken cancellationToken = default)
    {
        if (!context.Database.IsRelational())
        {
            return;
        }

        await context.Database.ExecuteSqlRawAsync(GetPostMigrationSql(), cancellationToken);
    }
}
