\set ON_ERROR_STOP on

\if :{?commit_purge}
\else
\set commit_purge false
\endif

\if :{?strict_catalog}
\else
\set strict_catalog true
\endif

BEGIN;
LOCK TABLE courses IN ACCESS EXCLUSIVE MODE;

CREATE TEMP TABLE purge_options
(
    strict_catalog boolean NOT NULL
) ON COMMIT DROP;

INSERT INTO purge_options (strict_catalog) VALUES (:'strict_catalog'::boolean);

DO $$
DECLARE
    target_count integer;
    keep_count integer;
    unexpected_codes text;
BEGIN
    SELECT COUNT(*) INTO target_count
    FROM courses
    WHERE code IN ('PRN231', 'SWD392', 'SWR302');

    IF target_count <> 3 THEN
        RAISE EXCEPTION 'Safety check failed: expected exactly 3 purge courses, found %', target_count;
    END IF;

    IF (SELECT strict_catalog FROM purge_options) THEN
        SELECT COUNT(*) INTO keep_count
        FROM courses
        WHERE code IN (
            'PRF192', 'CSD201', 'DBI202',
            'ENT001', 'ENT104', 'ENT203', 'ENT303', 'ENT403', 'ENT503'
        );

        IF keep_count <> 9 THEN
            RAISE EXCEPTION 'Safety check failed: expected exactly 9 retained courses, found %', keep_count;
        END IF;

        SELECT string_agg(code, ', ' ORDER BY code) INTO unexpected_codes
        FROM courses
        WHERE code NOT IN (
            'PRN231', 'SWD392', 'SWR302',
            'PRF192', 'CSD201', 'DBI202',
            'ENT001', 'ENT104', 'ENT203', 'ENT303', 'ENT403', 'ENT503'
        );

        IF unexpected_codes IS NOT NULL THEN
            RAISE EXCEPTION 'Safety check failed: unexpected course codes: %', unexpected_codes;
        END IF;
    END IF;
END $$;

CREATE TEMP TABLE purge_entities
(
    entity_name text NOT NULL,
    id uuid NOT NULL,
    PRIMARY KEY (entity_name, id)
) ON COMMIT DROP;

WITH
target_courses AS (
    SELECT id FROM courses WHERE code IN ('PRN231', 'SWD392', 'SWR302')
),
target_offerings AS (
    SELECT id FROM course_offerings WHERE course_id IN (SELECT id FROM target_courses)
),
target_classes AS (
    SELECT id FROM classes
    WHERE course_id IN (SELECT id FROM target_courses)
       OR course_offering_id IN (SELECT id FROM target_offerings)
),
target_in_class_shifts AS (
    SELECT id FROM in_class_exam_shifts WHERE class_id IN (SELECT id FROM target_classes)
),
target_rubrics AS (
    SELECT id FROM rubrics WHERE course_id IN (SELECT id FROM target_courses)
),
target_criteria AS (
    SELECT id FROM rubric_criteria WHERE rubric_id IN (SELECT id FROM target_rubrics)
),
target_clos AS (
    SELECT id FROM course_clos WHERE course_id IN (SELECT id FROM target_courses)
),
target_structures AS (
    SELECT id FROM exam_structures WHERE course_id IN (SELECT id FROM target_courses)
),
target_official_sessions AS (
    SELECT id FROM official_exam_sessions WHERE course_id IN (SELECT id FROM target_courses)
),
target_exam_questions AS (
    SELECT id FROM exam_questions
    WHERE course_id IN (SELECT id FROM target_courses)
       OR rubric_id IN (SELECT id FROM target_rubrics)
       OR structure_id IN (SELECT id FROM target_structures)
       OR clo_id IN (SELECT id FROM target_clos)
),
target_practice_questions AS (
    SELECT id FROM practice_questions
    WHERE course_id IN (SELECT id FROM target_courses)
       OR rubric_id IN (SELECT id FROM target_rubrics)
       OR clo_id IN (SELECT id FROM target_clos)
),
target_exam_sets AS (
    SELECT id FROM exam_sets
    WHERE course_id IN (SELECT id FROM target_courses)
       OR structure_id IN (SELECT id FROM target_structures)
       OR session_id IN (SELECT id FROM target_official_sessions)
),
target_real_shifts AS (
    SELECT id FROM real_exam_session_shifts
    WHERE session_id IN (SELECT id FROM target_official_sessions)
),
target_tickets AS (
    SELECT id FROM student_exam_tickets
    WHERE shift_id IN (SELECT id FROM target_real_shifts)
       OR exam_set_id IN (SELECT id FROM target_exam_sets)
),
target_exam_submissions AS (
    SELECT id FROM exam_question_submissions
    WHERE ticket_id IN (SELECT id FROM target_tickets)
       OR exam_question_id IN (SELECT id FROM target_exam_questions)
),
target_practice_sessions AS (
    SELECT id FROM practice_sessions WHERE course_id IN (SELECT id FROM target_courses)
),
target_practice_answers AS (
    SELECT id FROM practice_answers
    WHERE session_id IN (SELECT id FROM target_practice_sessions)
       OR question_id IN (SELECT id FROM target_practice_questions)
),
target_mock_sessions AS (
    SELECT id FROM mock_exam_sessions
    WHERE course_id IN (SELECT id FROM target_courses)
       OR exam_set_id IN (SELECT id FROM target_exam_sets)
),
target_mock_answers AS (
    SELECT id FROM mock_exam_answers
    WHERE session_id IN (SELECT id FROM target_mock_sessions)
       OR question_id IN (SELECT id FROM target_practice_questions)
),
target_in_class_submissions AS (
    SELECT id FROM in_class_exam_submissions
    WHERE shift_id IN (SELECT id FROM target_in_class_shifts)
       OR question_id IN (SELECT id FROM target_practice_questions)
),
target_ai_evaluations AS (
    SELECT id FROM ai_evaluations
    WHERE practice_answer_id IN (SELECT id FROM target_practice_answers)
       OR mock_exam_answer_id IN (SELECT id FROM target_mock_answers)
       OR exam_submission_id IN (SELECT id FROM target_exam_submissions)
       OR in_class_submission_id IN (SELECT id FROM target_in_class_submissions)
),
target_grading_jobs AS (
    SELECT id FROM grading_jobs WHERE submission_id IN (SELECT id FROM target_exam_submissions)
),
target_lecturer_audits AS (
    SELECT id FROM lecturer_audits WHERE ticket_id IN (SELECT id FROM target_tickets)
),
target_appeals AS (
    SELECT id FROM appeal_requests
    WHERE session_id IN (SELECT id FROM target_official_sessions)
       OR ticket_id IN (SELECT id FROM target_tickets)
       OR submission_id IN (SELECT id FROM target_exam_submissions)
),
target_anti_cheat AS (
    SELECT id FROM exam_anti_cheat_logs
    WHERE shift_id IN (SELECT id FROM target_real_shifts)
       OR ticket_id IN (SELECT id FROM target_tickets)
)
INSERT INTO purge_entities (entity_name, id)
SELECT 'courses', id FROM target_courses
UNION SELECT 'course_offerings', id FROM target_offerings
UNION SELECT 'classes', id FROM target_classes
UNION SELECT 'class_enrollments', id FROM class_enrollments WHERE class_id IN (SELECT id FROM target_classes)
UNION SELECT 'course_lecturers', id FROM course_lecturers WHERE course_id IN (SELECT id FROM target_courses) OR course_offering_id IN (SELECT id FROM target_offerings)
UNION SELECT 'course_clos', id FROM target_clos
UNION SELECT 'in_class_exam_shifts', id FROM target_in_class_shifts
UNION SELECT 'in_class_shift_passwords', id FROM in_class_shift_passwords WHERE shift_id IN (SELECT id FROM target_in_class_shifts)
UNION SELECT 'in_class_shift_participants', id FROM in_class_shift_participants WHERE shift_id IN (SELECT id FROM target_in_class_shifts) OR assigned_question_id IN (SELECT id FROM target_practice_questions)
UNION SELECT 'in_class_exam_submissions', id FROM target_in_class_submissions
UNION SELECT 'practice_questions', id FROM target_practice_questions
UNION SELECT 'practice_sessions', id FROM target_practice_sessions
UNION SELECT 'practice_answers', id FROM target_practice_answers
UNION SELECT 'mock_exam_quotas', id FROM mock_exam_quotas WHERE course_id IN (SELECT id FROM target_courses)
UNION SELECT 'mock_exam_sessions', id FROM target_mock_sessions
UNION SELECT 'mock_exam_answers', id FROM target_mock_answers
UNION SELECT 'exam_structures', id FROM target_structures
UNION SELECT 'exam_questions', id FROM target_exam_questions
UNION SELECT 'exam_sets', id FROM target_exam_sets
UNION SELECT 'exam_set_questions', id FROM exam_set_questions WHERE exam_set_id IN (SELECT id FROM target_exam_sets) OR exam_question_id IN (SELECT id FROM target_exam_questions)
UNION SELECT 'rubrics', id FROM target_rubrics
UNION SELECT 'rubric_criteria', id FROM target_criteria
UNION SELECT 'official_exam_sessions', id FROM target_official_sessions
UNION SELECT 'real_exam_session_shifts', id FROM target_real_shifts
UNION SELECT 'student_exam_tickets', id FROM target_tickets
UNION SELECT 'exam_question_submissions', id FROM target_exam_submissions
UNION SELECT 'grading_jobs', id FROM target_grading_jobs
UNION SELECT 'lecturer_audits', id FROM target_lecturer_audits
UNION SELECT 'lecturer_audit_details', id FROM lecturer_audit_details WHERE audit_id IN (SELECT id FROM target_lecturer_audits) OR submission_id IN (SELECT id FROM target_exam_submissions) OR criterion_id IN (SELECT id FROM target_criteria)
UNION SELECT 'appeal_requests', id FROM target_appeals
UNION SELECT 'exam_anti_cheat_logs', id FROM target_anti_cheat
UNION SELECT 'ai_evaluations', id FROM target_ai_evaluations
UNION SELECT 'ai_evaluation_details', id FROM ai_evaluation_details WHERE evaluation_id IN (SELECT id FROM target_ai_evaluations) OR criterion_id IN (SELECT id FROM target_criteria);

SELECT entity_name, COUNT(*) AS rows_to_delete
FROM purge_entities
GROUP BY entity_name
ORDER BY entity_name;

DELETE FROM notifications n
WHERE n.related_entity_id IN (SELECT id FROM purge_entities)
   OR COALESCE(n.metadata_json, '') ~ 'PRN231|SWD392|SWR302'
   OR COALESCE(n.title, '') ~ 'PRN231|SWD392|SWR302'
   OR COALESCE(n.message, '') ~ 'PRN231|SWD392|SWR302';

DELETE FROM audit_logs a
WHERE a.entity_id IN (SELECT id FROM purge_entities)
   OR COALESCE(a.old_values::text, '') ~ 'PRN231|SWD392|SWR302'
   OR COALESCE(a.new_values::text, '') ~ 'PRN231|SWD392|SWR302'
   OR EXISTS (
       SELECT 1 FROM purge_entities p
       WHERE COALESCE(a.old_values::text, '') LIKE '%' || p.id::text || '%'
          OR COALESCE(a.new_values::text, '') LIKE '%' || p.id::text || '%'
   );

DELETE FROM dead_letter_queues d
WHERE d.correlation_id IN (SELECT id FROM purge_entities)
   OR COALESCE(d.payload_json::text, '') ~ 'PRN231|SWD392|SWR302'
   OR EXISTS (
       SELECT 1 FROM purge_entities p
       WHERE COALESCE(d.payload_json::text, '') LIKE '%' || p.id::text || '%'
   );

DELETE FROM ai_evaluation_details WHERE id IN (SELECT id FROM purge_entities WHERE entity_name = 'ai_evaluation_details');
DELETE FROM lecturer_audit_details WHERE id IN (SELECT id FROM purge_entities WHERE entity_name = 'lecturer_audit_details');
DELETE FROM ai_evaluations WHERE id IN (SELECT id FROM purge_entities WHERE entity_name = 'ai_evaluations');
DELETE FROM grading_jobs WHERE id IN (SELECT id FROM purge_entities WHERE entity_name = 'grading_jobs');
DELETE FROM appeal_requests WHERE id IN (SELECT id FROM purge_entities WHERE entity_name = 'appeal_requests');
DELETE FROM exam_anti_cheat_logs WHERE id IN (SELECT id FROM purge_entities WHERE entity_name = 'exam_anti_cheat_logs');
DELETE FROM lecturer_audits WHERE id IN (SELECT id FROM purge_entities WHERE entity_name = 'lecturer_audits');
DELETE FROM exam_set_questions WHERE id IN (SELECT id FROM purge_entities WHERE entity_name = 'exam_set_questions');
DELETE FROM exam_question_submissions WHERE id IN (SELECT id FROM purge_entities WHERE entity_name = 'exam_question_submissions');
DELETE FROM student_exam_tickets WHERE id IN (SELECT id FROM purge_entities WHERE entity_name = 'student_exam_tickets');
DELETE FROM real_exam_session_shifts WHERE id IN (SELECT id FROM purge_entities WHERE entity_name = 'real_exam_session_shifts');
DELETE FROM mock_exam_answers WHERE id IN (SELECT id FROM purge_entities WHERE entity_name = 'mock_exam_answers');
DELETE FROM mock_exam_sessions WHERE id IN (SELECT id FROM purge_entities WHERE entity_name = 'mock_exam_sessions');
DELETE FROM exam_sets WHERE id IN (SELECT id FROM purge_entities WHERE entity_name = 'exam_sets');
DELETE FROM official_exam_sessions WHERE id IN (SELECT id FROM purge_entities WHERE entity_name = 'official_exam_sessions');
DELETE FROM practice_answers WHERE id IN (SELECT id FROM purge_entities WHERE entity_name = 'practice_answers');
DELETE FROM practice_sessions WHERE id IN (SELECT id FROM purge_entities WHERE entity_name = 'practice_sessions');
DELETE FROM in_class_exam_submissions WHERE id IN (SELECT id FROM purge_entities WHERE entity_name = 'in_class_exam_submissions');
DELETE FROM in_class_shift_participants WHERE id IN (SELECT id FROM purge_entities WHERE entity_name = 'in_class_shift_participants');
DELETE FROM in_class_shift_passwords WHERE id IN (SELECT id FROM purge_entities WHERE entity_name = 'in_class_shift_passwords');
DELETE FROM in_class_exam_shifts WHERE id IN (SELECT id FROM purge_entities WHERE entity_name = 'in_class_exam_shifts');
DELETE FROM class_enrollments WHERE id IN (SELECT id FROM purge_entities WHERE entity_name = 'class_enrollments');
DELETE FROM classes WHERE id IN (SELECT id FROM purge_entities WHERE entity_name = 'classes');
DELETE FROM exam_questions WHERE id IN (SELECT id FROM purge_entities WHERE entity_name = 'exam_questions');
DELETE FROM practice_questions WHERE id IN (SELECT id FROM purge_entities WHERE entity_name = 'practice_questions');
DELETE FROM rubric_criteria WHERE id IN (SELECT id FROM purge_entities WHERE entity_name = 'rubric_criteria');
DELETE FROM rubrics WHERE id IN (SELECT id FROM purge_entities WHERE entity_name = 'rubrics');
DELETE FROM exam_structures WHERE id IN (SELECT id FROM purge_entities WHERE entity_name = 'exam_structures');
DELETE FROM mock_exam_quotas WHERE id IN (SELECT id FROM purge_entities WHERE entity_name = 'mock_exam_quotas');
DELETE FROM course_lecturers WHERE id IN (SELECT id FROM purge_entities WHERE entity_name = 'course_lecturers');
DELETE FROM course_offerings WHERE id IN (SELECT id FROM purge_entities WHERE entity_name = 'course_offerings');
DELETE FROM course_clos WHERE id IN (SELECT id FROM purge_entities WHERE entity_name = 'course_clos');
DELETE FROM courses WHERE id IN (SELECT id FROM purge_entities WHERE entity_name = 'courses');

DO $$
DECLARE
    remaining_target_count integer;
    retained_count integer;
    unexpected_codes text;
    residual_notifications integer;
    residual_audit_logs integer;
    residual_dlq integer;
BEGIN
    SELECT COUNT(*) INTO remaining_target_count
    FROM courses WHERE code IN ('PRN231', 'SWD392', 'SWR302');

    IF remaining_target_count <> 0 THEN
        RAISE EXCEPTION 'Verification failed: % target courses remain', remaining_target_count;
    END IF;

    IF (SELECT strict_catalog FROM purge_options) THEN
        SELECT COUNT(*) INTO retained_count
        FROM courses
        WHERE code IN (
            'PRF192', 'CSD201', 'DBI202',
            'ENT001', 'ENT104', 'ENT203', 'ENT303', 'ENT403', 'ENT503'
        );

        IF retained_count <> 9 THEN
            RAISE EXCEPTION 'Verification failed: expected 9 retained courses, found %', retained_count;
        END IF;

        SELECT string_agg(code, ', ' ORDER BY code) INTO unexpected_codes
        FROM courses
        WHERE code NOT IN (
            'PRF192', 'CSD201', 'DBI202',
            'ENT001', 'ENT104', 'ENT203', 'ENT303', 'ENT403', 'ENT503'
        );

        IF unexpected_codes IS NOT NULL THEN
            RAISE EXCEPTION 'Verification failed: unexpected courses remain: %', unexpected_codes;
        END IF;
    END IF;

    SELECT COUNT(*) INTO residual_notifications
    FROM notifications n
    WHERE n.related_entity_id IN (SELECT id FROM purge_entities)
       OR COALESCE(n.metadata_json, '') ~ 'PRN231|SWD392|SWR302'
       OR COALESCE(n.title, '') ~ 'PRN231|SWD392|SWR302'
       OR COALESCE(n.message, '') ~ 'PRN231|SWD392|SWR302';

    SELECT COUNT(*) INTO residual_audit_logs
    FROM audit_logs a
    WHERE a.entity_id IN (SELECT id FROM purge_entities)
       OR COALESCE(a.old_values::text, '') ~ 'PRN231|SWD392|SWR302'
       OR COALESCE(a.new_values::text, '') ~ 'PRN231|SWD392|SWR302';

    SELECT COUNT(*) INTO residual_dlq
    FROM dead_letter_queues d
    WHERE d.correlation_id IN (SELECT id FROM purge_entities)
       OR COALESCE(d.payload_json::text, '') ~ 'PRN231|SWD392|SWR302';

    IF residual_notifications + residual_audit_logs + residual_dlq <> 0 THEN
        RAISE EXCEPTION 'Verification failed: residual polymorphic rows notifications=%, audit_logs=%, dlq=%',
            residual_notifications, residual_audit_logs, residual_dlq;
    END IF;
END $$;

SELECT code, name
FROM courses
ORDER BY code;

\if :commit_purge
COMMIT;
\echo 'PURGE COMMITTED'
\else
ROLLBACK;
\echo 'DRY RUN ONLY - ALL CHANGES ROLLED BACK'
\endif
