using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace OralExamination.Infrastructure.Persistence;

/// <summary>
/// Bộ đối soát & tự phục hồi lược đồ PostgreSQL (Idempotent Schema Reconciler).
/// Bù đắp hoàn toàn các bảng, cột, khóa ngoại, index và check constraint bị khuyết do các file migration rỗng
/// (20261104062322_AddDlqFlowTypeAndRoleSeparation và 20261108141117_UpdateDB), đồng thời đảm bảo tính nhất quán
/// cho cả môi trường tạo mới (Fresh DB) lẫn môi trường VPS Production đã chạy một phần migration.
/// </summary>
public static class PostgresSchemaReconciler
{
    /// <summary>
    /// SQL idempotent cho bước 20261104062322_AddDlqFlowTypeAndRoleSeparation (và chạy trước MigrateAsync trên DB đã có bảng users).
    /// Tuyệt đối không thêm trước các cột sẽ được 20261105064957, 20261105073054, 20261107111940 thêm không có IF NOT EXISTS.
    /// </summary>
    public static string GetPreMigrationSql() =>
        """
        DO $reconcile_pre$
        BEGIN
            IF NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'users') THEN
                RETURN;
            END IF;

            -- 1. users: mở rộng ck_users_role cho đủ 6 roles RBAC
            ALTER TABLE users DROP CONSTRAINT IF EXISTS ck_users_role;
            ALTER TABLE users ADD CONSTRAINT ck_users_role
                CHECK (role IN ('admin', 'department_head', 'examination_officer', 'lecturer', 'proctor', 'student'));

            -- 2. courses: các cột bổ sung trong đợt R1-R8 (trước academic_program_id)
            ALTER TABLE courses ADD COLUMN IF NOT EXISTS description text NULL;
            ALTER TABLE courses ADD COLUMN IF NOT EXISTS department_head_user_id uuid NULL;
            ALTER TABLE courses ADD COLUMN IF NOT EXISTS mock_exam_duration_minutes integer NOT NULL DEFAULT 15;
            ALTER TABLE courses ADD COLUMN IF NOT EXISTS mock_exam_total_questions integer NOT NULL DEFAULT 3;
            ALTER TABLE courses ADD COLUMN IF NOT EXISTS mock_exam_bloom_distribution jsonb NOT NULL DEFAULT jsonb_build_object('Remember', 20, 'Understand', 30, 'Apply', 30, 'Analyze', 20);

            CREATE INDEX IF NOT EXISTS ix_courses_department_head ON courses (department_head_user_id);

            IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_courses_users_department_head_user_id') THEN
                ALTER TABLE courses ADD CONSTRAINT "FK_courses_users_department_head_user_id"
                    FOREIGN KEY (department_head_user_id) REFERENCES users(id) ON DELETE SET NULL;
            END IF;
            IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_courses_mock_duration') THEN
                ALTER TABLE courses ADD CONSTRAINT ck_courses_mock_duration CHECK (mock_exam_duration_minutes > 0);
            END IF;
            IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_courses_mock_questions') THEN
                ALTER TABLE courses ADD CONSTRAINT ck_courses_mock_questions CHECK (mock_exam_total_questions > 0);
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
                CONSTRAINT "PK_course_lecturers" PRIMARY KEY (id),
                CONSTRAINT "FK_course_lecturers_courses_course_id" FOREIGN KEY (course_id) REFERENCES courses(id) ON DELETE CASCADE,
                CONSTRAINT "FK_course_lecturers_users_lecturer_user_id" FOREIGN KEY (lecturer_user_id) REFERENCES users(id) ON DELETE CASCADE,
                CONSTRAINT "FK_course_lecturers_users_assigned_by_user_id" FOREIGN KEY (assigned_by_user_id) REFERENCES users(id) ON DELETE RESTRICT,
                CONSTRAINT "FK_course_lecturers_semesters_semester_id" FOREIGN KEY (semester_id) REFERENCES semesters(id) ON DELETE RESTRICT
            );

            CREATE INDEX IF NOT EXISTS ix_course_lecturers_course ON course_lecturers (course_id, is_active);
            CREATE INDEX IF NOT EXISTS ix_course_lecturers_lecturer ON course_lecturers (lecturer_user_id, is_active);
            CREATE UNIQUE INDEX IF NOT EXISTS uq_course_lecturers ON course_lecturers (course_id, lecturer_user_id, semester_id);

            -- 4. classes: các cột bổ sung trước 20261105073054 và 20261107111940
            ALTER TABLE classes ADD COLUMN IF NOT EXISTS name character varying(150) NOT NULL DEFAULT '';
            ALTER TABLE classes ADD COLUMN IF NOT EXISTS join_code character varying(20) NULL;
            UPDATE classes SET join_code = UPPER(SUBSTRING(REPLACE(id::text, '-', ''), 1, 8)) WHERE join_code IS NULL OR join_code = '';
            ALTER TABLE classes ALTER COLUMN join_code SET NOT NULL;
            ALTER TABLE classes ADD COLUMN IF NOT EXISTS join_password_hash character varying(256) NULL;
            ALTER TABLE classes ADD COLUMN IF NOT EXISTS is_join_enabled boolean NOT NULL DEFAULT TRUE;
            ALTER TABLE classes ADD COLUMN IF NOT EXISTS allow_self_enrollment boolean NOT NULL DEFAULT TRUE;

            CREATE UNIQUE INDEX IF NOT EXISTS ix_classes_join_code ON classes (join_code);

            -- 5. class_enrollments
            ALTER TABLE class_enrollments ADD COLUMN IF NOT EXISTS joined_via_code boolean NOT NULL DEFAULT FALSE;
            ALTER TABLE class_enrollments ADD COLUMN IF NOT EXISTS join_method character varying(30) NOT NULL DEFAULT 'assigned';

            -- 6. practice_questions & exam_questions
            ALTER TABLE practice_questions ADD COLUMN IF NOT EXISTS clo_id uuid NULL;
            CREATE INDEX IF NOT EXISTS "IX_practice_questions_clo_id" ON practice_questions (clo_id);
            CREATE INDEX IF NOT EXISTS ix_practice_questions_clo ON practice_questions (clo_id);
            IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_practice_questions_course_clos_clo_id') THEN
                ALTER TABLE practice_questions ADD CONSTRAINT "FK_practice_questions_course_clos_clo_id"
                    FOREIGN KEY (clo_id) REFERENCES course_clos(id) ON DELETE SET NULL;
            END IF;

            ALTER TABLE exam_questions ADD COLUMN IF NOT EXISTS clo_id uuid NULL;
            ALTER TABLE exam_questions ADD COLUMN IF NOT EXISTS structure_id uuid NULL;
            CREATE INDEX IF NOT EXISTS "IX_exam_questions_clo_id" ON exam_questions (clo_id);
            CREATE INDEX IF NOT EXISTS ix_exam_questions_clo ON exam_questions (clo_id);
            CREATE INDEX IF NOT EXISTS "IX_exam_questions_structure_id" ON exam_questions (structure_id);
            CREATE INDEX IF NOT EXISTS ix_exam_questions_structure ON exam_questions (structure_id);
            IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_exam_questions_course_clos_clo_id') THEN
                ALTER TABLE exam_questions ADD CONSTRAINT "FK_exam_questions_course_clos_clo_id"
                    FOREIGN KEY (clo_id) REFERENCES course_clos(id) ON DELETE SET NULL;
            END IF;
            IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_exam_questions_exam_structures_structure_id') THEN
                ALTER TABLE exam_questions ADD CONSTRAINT "FK_exam_questions_exam_structures_structure_id"
                    FOREIGN KEY (structure_id) REFERENCES exam_structures(id) ON DELETE SET NULL;
            END IF;

            -- 7. practice_sessions, practice_answers & exam_sets
            ALTER TABLE practice_sessions ADD COLUMN IF NOT EXISTS total_score numeric(4,2) NULL;
            ALTER TABLE practice_answers ADD COLUMN IF NOT EXISTS order_index integer NOT NULL DEFAULT 1;
            ALTER TABLE exam_sets ADD COLUMN IF NOT EXISTS is_sample_set boolean NOT NULL DEFAULT FALSE;

            -- 8. dead_letter_queues
            ALTER TABLE dead_letter_queues ADD COLUMN IF NOT EXISTS flow_type character varying(30) NOT NULL DEFAULT 'OFFICIAL_EXAM';
            ALTER TABLE dead_letter_queues ALTER COLUMN flow_type SET DEFAULT 'OFFICIAL_EXAM';
            ALTER TABLE dead_letter_queues ADD COLUMN IF NOT EXISTS managed_by_role character varying(30) NOT NULL DEFAULT 'examination_officer';
            ALTER TABLE dead_letter_queues ADD COLUMN IF NOT EXISTS resolved_by_user_id uuid NULL;
            ALTER TABLE dead_letter_queues ADD COLUMN IF NOT EXISTS resolution_notes text NULL;

            UPDATE dead_letter_queues SET flow_type = 'PRACTICE' WHERE flow_type = 'MF01_PRACTICE';
            UPDATE dead_letter_queues SET flow_type = 'MOCK_EXAM' WHERE flow_type = 'MF02_MOCK_EXAM';
            UPDATE dead_letter_queues SET flow_type = 'OFFICIAL_EXAM' WHERE flow_type = 'MF04_OFFICIAL_EXAM';
            UPDATE dead_letter_queues SET flow_type = 'IN_CLASS_EXAM' WHERE flow_type = 'MF05_IN_CLASS';

            CREATE INDEX IF NOT EXISTS ix_dlq_role_flow_status ON dead_letter_queues (managed_by_role, flow_type, status);
            CREATE INDEX IF NOT EXISTS ix_dlq_flow_status ON dead_letter_queues (flow_type, status);
            CREATE INDEX IF NOT EXISTS ix_dlq_managed_role ON dead_letter_queues (managed_by_role, status);
            ALTER TABLE dead_letter_queues DROP CONSTRAINT IF EXISTS ck_dlq_flow_type;
            ALTER TABLE dead_letter_queues ADD CONSTRAINT ck_dlq_flow_type
                CHECK (flow_type IN ('PRACTICE', 'MOCK_EXAM', 'OFFICIAL_EXAM', 'IN_CLASS_EXAM', 'MF01_PRACTICE', 'MF02_MOCK_EXAM', 'MF04_OFFICIAL_EXAM', 'MF05_IN_CLASS'));
            ALTER TABLE dead_letter_queues DROP CONSTRAINT IF EXISTS ck_dlq_managed_by_role;
            ALTER TABLE dead_letter_queues DROP CONSTRAINT IF EXISTS ck_dlq_managed_role;
            ALTER TABLE dead_letter_queues ADD CONSTRAINT ck_dlq_managed_role
                CHECK (managed_by_role IN ('admin', 'examination_officer', 'lecturer'));
            IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_dead_letter_queues_users_resolved_by_user_id') THEN
                ALTER TABLE dead_letter_queues ADD CONSTRAINT "FK_dead_letter_queues_users_resolved_by_user_id"
                    FOREIGN KEY (resolved_by_user_id) REFERENCES users(id) ON DELETE SET NULL;
            END IF;
        END
        $reconcile_pre$;
        """;

    /// <summary>
    /// SQL idempotent toàn diện chạy ở bước 20261108141117_UpdateDB và sau khi MigrateAsync hoàn tất.
    /// Đảm bảo 100% bảng, cột, khóa ngoại, check constraint và tài khoản mẫu khớp tuyệt đối với OralExamDbContext.
    /// </summary>
    public static string GetPostMigrationSql() =>
        GetPreMigrationSql() +
        """

        DO $reconcile_post$
        BEGIN
            IF NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'users') THEN
                RETURN;
            END IF;

            -- 1. academic_programs & course_offerings
            CREATE TABLE IF NOT EXISTS academic_programs (
                id uuid NOT NULL DEFAULT gen_random_uuid(),
                code character varying(20) NOT NULL,
                name character varying(200) NOT NULL,
                department_head_user_id uuid NULL,
                is_active boolean NOT NULL DEFAULT TRUE,
                created_at timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
                updated_at timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
                CONSTRAINT "PK_academic_programs" PRIMARY KEY (id),
                CONSTRAINT "FK_academic_programs_users_department_head_user_id"
                    FOREIGN KEY (department_head_user_id) REFERENCES users(id) ON DELETE SET NULL
            );
            CREATE UNIQUE INDEX IF NOT EXISTS ix_academic_programs_code ON academic_programs (code);
            CREATE INDEX IF NOT EXISTS ix_academic_programs_department_head ON academic_programs (department_head_user_id);

            ALTER TABLE courses ADD COLUMN IF NOT EXISTS academic_program_id uuid NULL;
            CREATE INDEX IF NOT EXISTS ix_courses_academic_program ON courses (academic_program_id);
            IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_courses_academic_programs_academic_program_id') THEN
                ALTER TABLE courses ADD CONSTRAINT "FK_courses_academic_programs_academic_program_id"
                    FOREIGN KEY (academic_program_id) REFERENCES academic_programs(id) ON DELETE RESTRICT;
            END IF;

            CREATE TABLE IF NOT EXISTS course_offerings (
                id uuid NOT NULL DEFAULT gen_random_uuid(),
                course_id uuid NOT NULL,
                semester_id uuid NOT NULL,
                is_enabled boolean NOT NULL DEFAULT TRUE,
                created_at timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
                updated_at timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
                CONSTRAINT "PK_course_offerings" PRIMARY KEY (id),
                CONSTRAINT "FK_course_offerings_courses_course_id"
                    FOREIGN KEY (course_id) REFERENCES courses(id) ON DELETE RESTRICT,
                CONSTRAINT "FK_course_offerings_semesters_semester_id"
                    FOREIGN KEY (semester_id) REFERENCES semesters(id) ON DELETE RESTRICT
            );
            CREATE UNIQUE INDEX IF NOT EXISTS uq_course_offerings_course_semester ON course_offerings (course_id, semester_id);
            CREATE INDEX IF NOT EXISTS ix_course_offerings_semester ON course_offerings (semester_id);

            -- 2. course_lecturers & classes: course_offering_id & join_password_plain
            ALTER TABLE course_lecturers ADD COLUMN IF NOT EXISTS course_offering_id uuid NULL;
            CREATE UNIQUE INDEX IF NOT EXISTS uq_course_offering_lecturers ON course_lecturers (course_offering_id, lecturer_user_id);
            IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_course_lecturers_course_offerings_course_offering_id') THEN
                ALTER TABLE course_lecturers ADD CONSTRAINT "FK_course_lecturers_course_offerings_course_offering_id"
                    FOREIGN KEY (course_offering_id) REFERENCES course_offerings(id) ON DELETE RESTRICT;
            END IF;

            ALTER TABLE classes ADD COLUMN IF NOT EXISTS course_offering_id uuid NULL;
            ALTER TABLE classes ADD COLUMN IF NOT EXISTS join_password_plain character varying(128) NULL;
            CREATE INDEX IF NOT EXISTS ix_classes_course_offering ON classes (course_offering_id);
            CREATE INDEX IF NOT EXISTS ix_classes_active_semester_course_created ON classes (is_active, semester_id, course_id, created_at);
            IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_classes_course_offerings_course_offering_id') THEN
                ALTER TABLE classes ADD CONSTRAINT "FK_classes_course_offerings_course_offering_id"
                    FOREIGN KEY (course_offering_id) REFERENCES course_offerings(id) ON DELETE RESTRICT;
            END IF;

            -- 3. in_class_exam_shifts & bảng con MF-05
            IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'in_class_exam_shifts') THEN
                ALTER TABLE in_class_exam_shifts ADD COLUMN IF NOT EXISTS exam_duration_minutes integer NULL;
                ALTER TABLE in_class_exam_shifts DROP CONSTRAINT IF EXISTS ck_in_class_exam_shifts_exam_duration;
                ALTER TABLE in_class_exam_shifts ADD CONSTRAINT ck_in_class_exam_shifts_exam_duration
                    CHECK (exam_duration_minutes IS NULL OR (exam_duration_minutes >= 1 AND exam_duration_minutes <= 60));
                ALTER TABLE in_class_exam_shifts DROP CONSTRAINT IF EXISTS ck_in_class_exam_shifts_status;
                ALTER TABLE in_class_exam_shifts ADD CONSTRAINT ck_in_class_exam_shifts_status
                    CHECK (status IN ('SCHEDULED', 'IN_PROGRESS', 'FINALIZED', 'CLOSED'));
            END IF;

            -- 4. Đồng bộ dữ liệu nền cho academic_programs, course_offerings và tài khoản mẫu
            IF EXISTS (SELECT 1 FROM users WHERE id = '00000000-0000-0000-0000-000000000002') THEN
                INSERT INTO academic_programs (id, code, name, department_head_user_id, is_active, created_at, updated_at)
                VALUES ('00000000-0000-0000-0000-000000000010', 'SE', 'Kỹ thuật Phần mềm', '00000000-0000-0000-0000-000000000002', TRUE, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP)
                ON CONFLICT (code) DO NOTHING;

                UPDATE courses
                SET department_head_user_id = COALESCE(department_head_user_id, '00000000-0000-0000-0000-000000000002'),
                    academic_program_id = COALESCE(academic_program_id, (SELECT id FROM academic_programs WHERE code = 'SE' LIMIT 1))
                WHERE academic_program_id IS NULL OR department_head_user_id IS NULL;
            END IF;

            INSERT INTO course_offerings (id, course_id, semester_id, is_enabled, created_at, updated_at)
            SELECT gen_random_uuid(), c.id, c.semester_id, c.is_active, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP
            FROM courses c
            ON CONFLICT (course_id, semester_id) DO NOTHING;

            UPDATE course_lecturers AS cl
            SET course_offering_id = co.id,
                semester_id = COALESCE(cl.semester_id, co.semester_id)
            FROM course_offerings AS co
            WHERE cl.course_offering_id IS NULL
              AND co.course_id = cl.course_id;

            UPDATE classes AS cls
            SET course_offering_id = co.id
            FROM course_offerings AS co
            WHERE cls.course_offering_id IS NULL
              AND co.course_id = cls.course_id
              AND co.semester_id = cls.semester_id;

            -- Đảm bảo các tài khoản demo (Le Vu Hoang, Tran Thi Mai, Nguyen Trong Tot, ...) không bị kẹt cờ must_change_password
            UPDATE users
            SET must_change_password = FALSE
            WHERE id IN (
                '00000000-0000-0000-0000-000000000001',
                '00000000-0000-0000-0000-000000000002',
                '00000000-0000-0000-0000-000000000003',
                '00000000-0000-0000-0000-000000000004',
                '00000000-0000-0000-0000-000000000005',
                '00000000-0000-0000-0000-000000000006',
                '00000000-0000-0000-0000-00000000000b',
                '00000000-0000-0000-0000-00000000000c',
                '00000000-0000-0000-0000-00000000000d',
                '00000000-0000-0000-0000-00000000000e',
                '00000000-0000-0000-0000-00000000000f'
            );
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
