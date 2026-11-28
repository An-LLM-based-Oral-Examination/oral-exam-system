namespace OralExamination.Infrastructure.Persistence;

internal static class ExamPreparationReuseSchema
{
    internal const string PostMigrationSql = """
        DO $exam_reuse$
        BEGIN
            IF to_regclass('public.exam_preparations') IS NOT NULL THEN
                ALTER TABLE exam_preparations ADD COLUMN IF NOT EXISTS reuse_percent integer NOT NULL DEFAULT 30;
                IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_exam_preparation_reuse' AND conrelid = 'exam_preparations'::regclass) THEN
                    ALTER TABLE exam_preparations ADD CONSTRAINT ck_exam_preparation_reuse CHECK (reuse_percent BETWEEN 10 AND 50);
                END IF;
            END IF;
            IF to_regclass('public.exam_sets') IS NOT NULL THEN
                DROP INDEX IF EXISTS uq_exam_sets_course_code;
                IF EXISTS (
                    SELECT 1
                    FROM exam_sets
                    WHERE is_active = true
                    GROUP BY course_id, set_code
                    HAVING COUNT(*) > 1
                ) THEN
                    RAISE EXCEPTION '[FAIL CLOSED] Không thể tạo uq_exam_sets_active_course_set_code vì phát hiện cặp (course_id, set_code) trùng lặp đang hoạt động (is_active = true) trong exam_sets!';
                END IF;
                CREATE UNIQUE INDEX IF NOT EXISTS uq_exam_sets_active_course_set_code
                    ON exam_sets (course_id, set_code) WHERE is_active = true;
                IF EXISTS (
                    SELECT 1
                    FROM exam_sets
                    WHERE status IS NULL
                       OR status NOT IN ('DRAFT', 'SUBMITTED_FOR_REVIEW', 'OFFICIAL', 'HANDED_OVER', 'REJECTED', 'NEEDS_REVISION')
                ) THEN
                    RAISE EXCEPTION '[FAIL CLOSED] Không thể áp dụng ck_exam_sets_status vì phát hiện bản ghi exam_sets có status không hợp lệ!';
                END IF;
                ALTER TABLE exam_sets DROP CONSTRAINT IF EXISTS ck_exam_sets_status;
                ALTER TABLE exam_sets ADD CONSTRAINT ck_exam_sets_status
                    CHECK (status IN ('DRAFT', 'SUBMITTED_FOR_REVIEW', 'OFFICIAL', 'HANDED_OVER', 'REJECTED', 'NEEDS_REVISION'));
            END IF;
            IF to_regclass('public.exam_questions') IS NOT NULL THEN
                ALTER TABLE exam_questions ADD COLUMN IF NOT EXISTS origin_question_id uuid NULL;
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_exam_questions_origin_question_id" ON exam_questions(origin_question_id);
                IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_exam_questions_exam_questions_origin_question_id' AND conrelid = 'exam_questions'::regclass) THEN
                    BEGIN
                    ALTER TABLE exam_questions ADD CONSTRAINT "FK_exam_questions_exam_questions_origin_question_id"
                        FOREIGN KEY (origin_question_id) REFERENCES exam_questions(id) ON DELETE RESTRICT;
                    EXCEPTION WHEN OTHERS THEN NULL;
                    END;
                END IF;
                IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_exam_questions_exam_questions_origin_question_id' AND conrelid = 'exam_questions'::regclass) THEN
                    RAISE EXCEPTION 'Cannot reconcile exam question origin constraint; repair orphan origins before startup';
                END IF;
            END IF;
        END
        $exam_reuse$;
        """;
}
