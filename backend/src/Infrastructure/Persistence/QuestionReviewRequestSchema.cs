namespace OralExamination.Infrastructure.Persistence;

public static class QuestionReviewRequestSchema
{
    public const string CreateSql = """
CREATE TABLE IF NOT EXISTS question_review_requests (
    "Id" uuid PRIMARY KEY, "CourseId" uuid NOT NULL REFERENCES courses(id),
    "SubmittedBy" uuid NOT NULL REFERENCES users(id), "SubmittedAt" timestamptz NULL,
    "Notes" varchar(1000) NULL, "Version" bigint NOT NULL
);
CREATE TABLE IF NOT EXISTS question_review_request_items (
    "Id" uuid PRIMARY KEY, "RequestId" uuid NOT NULL REFERENCES question_review_requests("Id") ON DELETE CASCADE,
    "QuestionId" uuid NOT NULL, "UsageScope" varchar(20) NOT NULL, "SnapshotJson" text NULL,
    "Decision" varchar(30) NOT NULL, "ReviewNotes" varchar(2000) NULL,
    "ReviewedBy" uuid NULL, "ReviewedAt" timestamptz NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_question_review_request_items_UsageScope_QuestionId"
    ON question_review_request_items ("UsageScope", "QuestionId") WHERE "Decision" = 'PENDING';
CREATE UNIQUE INDEX IF NOT EXISTS "IX_question_review_request_items_RequestId_QuestionId_UsageSco~"
    ON question_review_request_items ("RequestId", "QuestionId", "UsageScope");
CREATE INDEX IF NOT EXISTS "IX_question_review_requests_CourseId_SubmittedAt"
    ON question_review_requests ("CourseId", "SubmittedAt");
CREATE INDEX IF NOT EXISTS "IX_question_review_requests_SubmittedBy"
    ON question_review_requests ("SubmittedBy");
""";

    public const string BackfillSql = """
WITH pending AS (
    SELECT course_id, submitted_by, left(coalesce(review_notes, ''), 1000) AS notes
    FROM practice_questions q WHERE approval_status = 'SUBMITTED_FOR_REVIEW' AND submitted_by IS NOT NULL AND NOT EXISTS (SELECT 1 FROM question_review_request_items i WHERE i."QuestionId" = q.id AND i."UsageScope" = 'practice' AND i."Decision" = 'PENDING')
    UNION
    SELECT course_id, submitted_by, left(coalesce(review_notes, ''), 1000)
    FROM exam_questions q WHERE approval_status = 'SUBMITTED_FOR_REVIEW' AND submitted_by IS NOT NULL AND NOT EXISTS (SELECT 1 FROM question_review_request_items i WHERE i."QuestionId" = q.id AND i."UsageScope" = 'exam' AND i."Decision" = 'PENDING')
)
INSERT INTO question_review_requests ("Id", "CourseId", "SubmittedBy", "Notes", "Version")
SELECT gen_random_uuid(), p.course_id, p.submitted_by, p.notes, 0 FROM pending p
JOIN users u ON u.id = p.submitted_by JOIN courses c ON c.id = p.course_id
WHERE NOT EXISTS (SELECT 1 FROM question_review_requests r WHERE r."CourseId" = p.course_id AND r."SubmittedBy" = p.submitted_by AND r."SubmittedAt" IS NULL AND r."Version" = 0 AND r."Notes" = p.notes);

INSERT INTO question_review_request_items
    ("Id", "RequestId", "QuestionId", "UsageScope", "Decision", "SnapshotJson")
SELECT gen_random_uuid(), r."Id", q.id, 'practice', 'PENDING',
    jsonb_build_object(
        'Title', q.title, 'Content', q.content, 'SampleAnswer', q.sample_answer,
        'Difficulty', q.difficulty, 'BloomLevel', q.bloom_level,
        'CloId', q.clo_id, 'CloCode', c.clo_code, 'ImageUrl', q.image_url,
        'ImageDescription', q.image_description, 'Source', q.source,
        'Criteria', coalesce((
            SELECT jsonb_agg(jsonb_build_object(
                'Name', rc.criterion_name, 'Description', rc.description,
                'MaxScore', rc.max_score, 'BloomLevel', rc.bloom_level)
                ORDER BY rc.order_index, rc.id)
            FROM rubric_criteria rc WHERE rc.rubric_id = q.rubric_id
        ), '[]'::jsonb)
    )::text
FROM practice_questions q
JOIN question_review_requests r ON r."CourseId" = q.course_id AND r."SubmittedBy" = q.submitted_by
    AND r."SubmittedAt" IS NULL AND r."Version" = 0 AND r."Notes" = left(coalesce(q.review_notes, ''), 1000)
LEFT JOIN course_clos c ON c.id = q.clo_id
WHERE q.approval_status = 'SUBMITTED_FOR_REVIEW'
ON CONFLICT DO NOTHING;
INSERT INTO question_review_request_items
    ("Id", "RequestId", "QuestionId", "UsageScope", "Decision", "SnapshotJson")
SELECT gen_random_uuid(), r."Id", q.id, 'exam', 'PENDING',
    jsonb_build_object(
        'Title', q.title, 'Content', q.content, 'SampleAnswer', q.sample_answer,
        'Difficulty', q.difficulty, 'BloomLevel', q.bloom_level,
        'CloId', q.clo_id, 'CloCode', c.clo_code, 'ImageUrl', q.image_url,
        'ImageDescription', q.image_description, 'Source', q.source,
        'Criteria', coalesce((
            SELECT jsonb_agg(jsonb_build_object(
                'Name', rc.criterion_name, 'Description', rc.description,
                'MaxScore', rc.max_score, 'BloomLevel', rc.bloom_level)
                ORDER BY rc.order_index, rc.id)
            FROM rubric_criteria rc WHERE rc.rubric_id = q.rubric_id
        ), '[]'::jsonb)
    )::text
FROM exam_questions q
JOIN question_review_requests r ON r."CourseId" = q.course_id AND r."SubmittedBy" = q.submitted_by
    AND r."SubmittedAt" IS NULL AND r."Version" = 0 AND r."Notes" = left(coalesce(q.review_notes, ''), 1000)
LEFT JOIN course_clos c ON c.id = q.clo_id
WHERE q.approval_status = 'SUBMITTED_FOR_REVIEW'
ON CONFLICT DO NOTHING;
""";
}
