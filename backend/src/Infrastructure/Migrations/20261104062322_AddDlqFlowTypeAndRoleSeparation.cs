using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OralExamination.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDlqFlowTypeAndRoleSeparation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Historical migrations must be immutable snapshots. Calling the current
            // reconciler here caused a clean database to receive columns belonging to
            // later migrations, then fail with PostgreSQL 42701 on the next migration.
            migrationBuilder.Sql("""
                ALTER TABLE dead_letter_queues
                    ADD COLUMN IF NOT EXISTS flow_type character varying(30) NOT NULL DEFAULT 'OFFICIAL_EXAM';
                ALTER TABLE dead_letter_queues
                    ADD COLUMN IF NOT EXISTS managed_by_role character varying(30) NOT NULL DEFAULT 'examination_officer';
                ALTER TABLE dead_letter_queues
                    ADD COLUMN IF NOT EXISTS resolved_by_user_id uuid NULL;
                ALTER TABLE dead_letter_queues
                    ADD COLUMN IF NOT EXISTS resolution_notes text NULL;

                UPDATE dead_letter_queues SET flow_type = 'PRACTICE' WHERE flow_type = 'MF01_PRACTICE';
                UPDATE dead_letter_queues SET flow_type = 'MOCK_EXAM' WHERE flow_type = 'MF02_MOCK_EXAM';
                UPDATE dead_letter_queues SET flow_type = 'OFFICIAL_EXAM' WHERE flow_type = 'MF04_OFFICIAL_EXAM';
                UPDATE dead_letter_queues SET flow_type = 'IN_CLASS_EXAM' WHERE flow_type = 'MF05_IN_CLASS';

                CREATE INDEX IF NOT EXISTS ix_dlq_role_flow_status
                    ON dead_letter_queues (managed_by_role, flow_type, status);
                CREATE INDEX IF NOT EXISTS ix_dlq_flow_status
                    ON dead_letter_queues (flow_type, status);
                CREATE INDEX IF NOT EXISTS ix_dlq_managed_role
                    ON dead_letter_queues (managed_by_role, status);

                ALTER TABLE dead_letter_queues DROP CONSTRAINT IF EXISTS ck_dlq_flow_type;
                ALTER TABLE dead_letter_queues ADD CONSTRAINT ck_dlq_flow_type
                    CHECK (flow_type IN ('PRACTICE', 'MOCK_EXAM', 'OFFICIAL_EXAM', 'IN_CLASS_EXAM'));
                ALTER TABLE dead_letter_queues DROP CONSTRAINT IF EXISTS ck_dlq_managed_by_role;
                ALTER TABLE dead_letter_queues DROP CONSTRAINT IF EXISTS ck_dlq_managed_role;
                ALTER TABLE dead_letter_queues ADD CONSTRAINT ck_dlq_managed_role
                    CHECK (managed_by_role IN ('admin', 'examination_officer', 'lecturer'));

                DO $migration$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM pg_constraint
                        WHERE conname = 'FK_dead_letter_queues_users_resolved_by_user_id'
                    ) THEN
                        ALTER TABLE dead_letter_queues
                            ADD CONSTRAINT "FK_dead_letter_queues_users_resolved_by_user_id"
                            FOREIGN KEY (resolved_by_user_id) REFERENCES users(id) ON DELETE SET NULL;
                    END IF;
                END
                $migration$;

                -- Bảng này đã có trong model snapshot tại thời điểm migration nhưng
                -- bị thiếu khỏi InitialDB. Tạo đúng phiên bản trước khi migration
                -- 20261105073054 bổ sung course_offering_id.
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
                    CONSTRAINT "FK_course_lecturers_courses_course_id"
                        FOREIGN KEY (course_id) REFERENCES courses(id) ON DELETE CASCADE,
                    CONSTRAINT "FK_course_lecturers_users_lecturer_user_id"
                        FOREIGN KEY (lecturer_user_id) REFERENCES users(id) ON DELETE CASCADE,
                    CONSTRAINT "FK_course_lecturers_users_assigned_by_user_id"
                        FOREIGN KEY (assigned_by_user_id) REFERENCES users(id) ON DELETE RESTRICT,
                    CONSTRAINT "FK_course_lecturers_semesters_semester_id"
                        FOREIGN KEY (semester_id) REFERENCES semesters(id) ON DELETE RESTRICT
                );
                CREATE INDEX IF NOT EXISTS ix_course_lecturers_course
                    ON course_lecturers (course_id, is_active);
                CREATE INDEX IF NOT EXISTS ix_course_lecturers_lecturer
                    ON course_lecturers (lecturer_user_id, is_active);
                CREATE UNIQUE INDEX IF NOT EXISTS uq_course_lecturers
                    ON course_lecturers (course_id, lecturer_user_id, semester_id);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
