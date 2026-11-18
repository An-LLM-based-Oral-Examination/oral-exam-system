-- ==============================================================================
-- PURGE TEST DATA COMMIT SCRIPT (PRODUCTION VPS SAFE PURGE)
-- Project: Oral Exam System (FA26SE166)
-- Target: Clean up concurrency integration test data and test artifacts
-- Safety Guarantees:
--   1. Transactional isolation (BEGIN ... COMMIT) with \set ON_ERROR_STOP on
--   2. Strict assertions protecting core courses ('PRF192', 'CSD201', 'DBI202') == 3
--   3. Strict assertions protecting English courses ('ENT%') >= 6
--   4. Hard stop if any target test course/user overlaps protected catalogs/admins
--   5. Temporary tables ON COMMIT DROP to isolate candidate IDs
--   6. 10-stage leaf-to-root FK-safe & check-constraint-safe deletion
--      (follow-up answers/submissions deleted BEFORE main rows; zero parent=NULL UPDATEs)
--   7. Post-purge integrity verification asserting retained courses & 0 target rows
-- ==============================================================================

\set ON_ERROR_STOP on

BEGIN;

-- ==============================================================================
-- 1. TẠO BẢNG TẠM THU THẬP ĐỐI TƯỢNG TEST THEO QUAN HỆ BẮC CẦU (TRANSITIVE)
-- ==============================================================================
CREATE TEMP TABLE target_test_users (id uuid PRIMARY KEY) ON COMMIT DROP;
CREATE TEMP TABLE target_test_courses (id uuid PRIMARY KEY) ON COMMIT DROP;
CREATE TEMP TABLE target_test_classes (id uuid PRIMARY KEY) ON COMMIT DROP;
CREATE TEMP TABLE target_test_rubrics (id uuid PRIMARY KEY) ON COMMIT DROP;
CREATE TEMP TABLE target_test_clos (id uuid PRIMARY KEY) ON COMMIT DROP;
CREATE TEMP TABLE target_test_offerings (id uuid PRIMARY KEY) ON COMMIT DROP;
CREATE TEMP TABLE target_test_questions (id uuid PRIMARY KEY) ON COMMIT DROP;
CREATE TEMP TABLE target_test_exam_questions (id uuid PRIMARY KEY) ON COMMIT DROP;
CREATE TEMP TABLE target_test_shifts (id uuid PRIMARY KEY) ON COMMIT DROP;
CREATE TEMP TABLE target_practice_sessions (id uuid PRIMARY KEY) ON COMMIT DROP;
CREATE TEMP TABLE target_practice_answers (id uuid PRIMARY KEY) ON COMMIT DROP;
CREATE TEMP TABLE target_mock_sessions (id uuid PRIMARY KEY) ON COMMIT DROP;
CREATE TEMP TABLE target_mock_answers (id uuid PRIMARY KEY) ON COMMIT DROP;
CREATE TEMP TABLE target_exam_structures (id uuid PRIMARY KEY) ON COMMIT DROP;
CREATE TEMP TABLE target_official_sessions (id uuid PRIMARY KEY) ON COMMIT DROP;
CREATE TEMP TABLE target_exam_sets (id uuid PRIMARY KEY) ON COMMIT DROP;
CREATE TEMP TABLE target_real_shifts (id uuid PRIMARY KEY) ON COMMIT DROP;
CREATE TEMP TABLE target_tickets (id uuid PRIMARY KEY) ON COMMIT DROP;
CREATE TEMP TABLE target_exam_submissions (id uuid PRIMARY KEY) ON COMMIT DROP;
CREATE TEMP TABLE target_lecturer_audits (id uuid PRIMARY KEY) ON COMMIT DROP;
CREATE TEMP TABLE target_in_class_submissions (id uuid PRIMARY KEY) ON COMMIT DROP;
CREATE TEMP TABLE target_ai_evaluations (id uuid PRIMARY KEY) ON COMMIT DROP;

INSERT INTO target_test_users (id)
SELECT id FROM users
WHERE full_name IN ('Test Lecturer Concurrency', 'Test Student Concurrency')
   OR email LIKE '%concurrency%test%'
   OR username LIKE '%concurrency%test%';

INSERT INTO target_test_courses (id)
SELECT id FROM courses
WHERE name = 'PRN Concurrency Test'
   OR code = 'PRN_CONC';

INSERT INTO target_test_classes (id)
SELECT id FROM classes
WHERE name = 'SE Concurrency Class'
   OR course_id IN (SELECT id FROM target_test_courses);

INSERT INTO target_test_rubrics (id)
SELECT id FROM rubrics
WHERE name = 'Concurrency Rubric'
   OR course_id IN (SELECT id FROM target_test_courses);

INSERT INTO target_test_clos (id)
SELECT id FROM course_clos
WHERE course_id IN (SELECT id FROM target_test_courses);

INSERT INTO target_test_offerings (id)
SELECT id FROM course_offerings
WHERE course_id IN (SELECT id FROM target_test_courses);

INSERT INTO target_test_questions (id)
SELECT id FROM practice_questions
WHERE title = 'Concurrency Question'
   OR rubric_id IN (SELECT id FROM target_test_rubrics)
   OR course_id IN (SELECT id FROM target_test_courses)
   OR clo_id IN (SELECT id FROM target_test_clos);

INSERT INTO target_test_exam_questions (id)
SELECT id FROM exam_questions
WHERE rubric_id IN (SELECT id FROM target_test_rubrics)
   OR course_id IN (SELECT id FROM target_test_courses)
   OR clo_id IN (SELECT id FROM target_test_clos);

INSERT INTO target_test_shifts (id)
SELECT id FROM in_class_exam_shifts
WHERE shift_name = 'Concurrency Shift Test'
   OR class_id IN (SELECT id FROM target_test_classes)
   OR lecturer_id IN (SELECT id FROM target_test_users);

INSERT INTO target_practice_sessions (id)
SELECT id FROM practice_sessions
WHERE course_id IN (SELECT id FROM target_test_courses)
   OR student_id IN (SELECT id FROM target_test_users);

INSERT INTO target_practice_answers (id)
SELECT id FROM practice_answers
WHERE session_id IN (SELECT id FROM target_practice_sessions)
   OR question_id IN (SELECT id FROM target_test_questions)
   OR student_id IN (SELECT id FROM target_test_users)
   OR parent_answer_id IN (
       SELECT id FROM practice_answers
       WHERE session_id IN (SELECT id FROM target_practice_sessions)
          OR question_id IN (SELECT id FROM target_test_questions)
          OR student_id IN (SELECT id FROM target_test_users)
   )
ON CONFLICT DO NOTHING;

INSERT INTO target_exam_structures (id)
SELECT id FROM exam_structures
WHERE course_id IN (SELECT id FROM target_test_courses)
   OR created_by IN (SELECT id FROM target_test_users);

INSERT INTO target_official_sessions (id)
SELECT id FROM official_exam_sessions
WHERE course_id IN (SELECT id FROM target_test_courses)
   OR exam_structure_id IN (SELECT id FROM target_exam_structures)
   OR created_by IN (SELECT id FROM target_test_users);

INSERT INTO target_exam_sets (id)
SELECT id FROM exam_sets
WHERE course_id IN (SELECT id FROM target_test_courses)
   OR structure_id IN (SELECT id FROM target_exam_structures)
   OR session_id IN (SELECT id FROM target_official_sessions)
   OR created_by IN (SELECT id FROM target_test_users);

INSERT INTO target_mock_sessions (id)
SELECT id FROM mock_exam_sessions
WHERE course_id IN (SELECT id FROM target_test_courses)
   OR student_id IN (SELECT id FROM target_test_users)
   OR exam_set_id IN (SELECT id FROM target_exam_sets);

INSERT INTO target_mock_answers (id)
SELECT id FROM mock_exam_answers
WHERE session_id IN (SELECT id FROM target_mock_sessions)
   OR question_id IN (SELECT id FROM target_test_questions)
   OR parent_answer_id IN (
       SELECT id FROM mock_exam_answers
       WHERE session_id IN (SELECT id FROM target_mock_sessions)
          OR question_id IN (SELECT id FROM target_test_questions)
   )
ON CONFLICT DO NOTHING;

INSERT INTO target_real_shifts (id)
SELECT id FROM real_exam_session_shifts
WHERE session_id IN (SELECT id FROM target_official_sessions)
   OR proctor_user_id IN (SELECT id FROM target_test_users)
   OR examiner_user_id IN (SELECT id FROM target_test_users);

INSERT INTO target_tickets (id)
SELECT id FROM student_exam_tickets
WHERE shift_id IN (SELECT id FROM target_real_shifts)
   OR student_id IN (SELECT id FROM target_test_users)
   OR exam_set_id IN (SELECT id FROM target_exam_sets);

INSERT INTO target_exam_submissions (id)
SELECT id FROM exam_question_submissions
WHERE ticket_id IN (SELECT id FROM target_tickets)
   OR exam_question_id IN (SELECT id FROM target_test_exam_questions);

INSERT INTO target_lecturer_audits (id)
SELECT id FROM lecturer_audits
WHERE ticket_id IN (SELECT id FROM target_tickets)
   OR lecturer_id IN (SELECT id FROM target_test_users);

INSERT INTO target_in_class_submissions (id)
SELECT id FROM in_class_exam_submissions
WHERE shift_id IN (SELECT id FROM target_test_shifts)
   OR student_id IN (SELECT id FROM target_test_users)
   OR question_id IN (SELECT id FROM target_test_questions)
   OR parent_submission_id IN (
       SELECT id FROM in_class_exam_submissions
       WHERE shift_id IN (SELECT id FROM target_test_shifts)
          OR student_id IN (SELECT id FROM target_test_users)
          OR question_id IN (SELECT id FROM target_test_questions)
   )
ON CONFLICT DO NOTHING;

INSERT INTO target_ai_evaluations (id)
SELECT id FROM ai_evaluations
WHERE student_id IN (SELECT id FROM target_test_users)
   OR practice_answer_id IN (SELECT id FROM target_practice_answers)
   OR mock_exam_answer_id IN (SELECT id FROM target_mock_answers)
   OR exam_submission_id IN (SELECT id FROM target_exam_submissions)
   OR in_class_submission_id IN (SELECT id FROM target_in_class_submissions);

-- ==============================================================================
-- 2. BẢO VỆ TUYỆT ĐỐI DANH MỤC KHÓA HỌC THẬT (PRE-FLIGHT SAFETY VERIFICATION)
-- ==============================================================================
DO $$
DECLARE
    protected_core_count integer;
    protected_ent_count integer;
    leaked_target_count integer;
    admin_leak_count integer;
    program_owner_leak integer;
    retained_course_owner_leak integer;
BEGIN
    SELECT COUNT(*) INTO protected_core_count
    FROM courses
    WHERE code IN ('PRF192', 'CSD201', 'DBI202');

    IF protected_core_count <> 3 THEN
        RAISE EXCEPTION '[SAFETY CHECK FAILED] Danh mục môn học chuyên ngành cốt lõi (PRF192, CSD201, DBI202) thiếu bản ghi! Kỳ vọng: 3, thực tế: %', protected_core_count;
    END IF;

    SELECT COUNT(*) INTO protected_ent_count
    FROM courses
    WHERE code LIKE 'ENT%';

    IF protected_ent_count < 6 THEN
        RAISE EXCEPTION '[SAFETY CHECK FAILED] Danh mục môn học tiếng Anh (ENT%%) bị thiếu! Kỳ vọng tối thiểu: 6, thực tế: %', protected_ent_count;
    END IF;

    SELECT COUNT(*) INTO leaked_target_count
    FROM courses
    WHERE (code IN ('PRF192', 'CSD201', 'DBI202') OR code LIKE 'ENT%')
      AND id IN (SELECT id FROM target_test_courses);

    IF leaked_target_count > 0 THEN
        RAISE EXCEPTION '[FATAL ERROR] Danh sách target purge chứa môn học bảo vệ! Hủy giao dịch ngay lập tức!';
    END IF;

    SELECT COUNT(*) INTO admin_leak_count
    FROM users
    WHERE id IN (SELECT id FROM target_test_users)
      AND role = 'admin';

    IF admin_leak_count > 0 THEN
        RAISE EXCEPTION '[FATAL ERROR] Danh sách target user chứa tài khoản Admin hệ thống! Hủy giao dịch ngay lập tức!';
    END IF;

    SELECT COUNT(*) INTO program_owner_leak
    FROM academic_programs
    WHERE department_head_user_id IN (SELECT id FROM target_test_users);

    IF program_owner_leak > 0 THEN
        RAISE EXCEPTION '[FATAL ERROR] Có academic_programs đang tham chiếu tới target_test_users!';
    END IF;

    SELECT COUNT(*) INTO retained_course_owner_leak
    FROM courses
    WHERE department_head_user_id IN (SELECT id FROM target_test_users)
      AND id NOT IN (SELECT id FROM target_test_courses);

    IF retained_course_owner_leak > 0 THEN
        RAISE EXCEPTION '[FATAL ERROR] Có môn học thật đang tham chiếu tới target_test_users!';
    END IF;

    RAISE NOTICE '[SAFETY CHECK PASS] 3 môn cốt lõi, % môn ENT%%, 0 tài khoản/môn học bảo vệ bị ảnh hưởng.', protected_ent_count;
END $$;

-- ==============================================================================
-- 3. BÁO CÁO KIỂM KÊ TRƯỚC KHI XÓA (PRE-PURGE AUDIT SUMMARY)
-- ==============================================================================
DO $$
DECLARE
    v_users int;
    v_courses int;
    v_classes int;
    v_rubrics int;
    v_questions int;
    v_shifts int;
    v_in_class_subs int;
    v_practice_answers int;
    v_mock_answers int;
    v_ai_evals int;
BEGIN
    SELECT COUNT(*) INTO v_users FROM target_test_users;
    SELECT COUNT(*) INTO v_courses FROM target_test_courses;
    SELECT COUNT(*) INTO v_classes FROM target_test_classes;
    SELECT COUNT(*) INTO v_rubrics FROM target_test_rubrics;
    SELECT COUNT(*) INTO v_questions FROM target_test_questions;
    SELECT COUNT(*) INTO v_shifts FROM target_test_shifts;
    SELECT COUNT(*) INTO v_in_class_subs FROM target_in_class_submissions;
    SELECT COUNT(*) INTO v_practice_answers FROM target_practice_answers;
    SELECT COUNT(*) INTO v_mock_answers FROM target_mock_answers;
    SELECT COUNT(*) INTO v_ai_evals FROM target_ai_evaluations;

    RAISE NOTICE '=======================================================';
    RAISE NOTICE '📊 KIỂM KÊ DỮ LIỆU TEST SẼ PURGE:';
    RAISE NOTICE '  - Target Users               : %', v_users;
    RAISE NOTICE '  - Target Courses             : %', v_courses;
    RAISE NOTICE '  - Target Classes             : %', v_classes;
    RAISE NOTICE '  - Target Rubrics             : %', v_rubrics;
    RAISE NOTICE '  - Target Practice Questions  : %', v_questions;
    RAISE NOTICE '  - Target In-Class Shifts     : %', v_shifts;
    RAISE NOTICE '  - Target In-Class Submissions: %', v_in_class_subs;
    RAISE NOTICE '  - Target Practice Answers    : %', v_practice_answers;
    RAISE NOTICE '  - Target Mock Exam Answers   : %', v_mock_answers;
    RAISE NOTICE '  - Target AI Evaluations      : %', v_ai_evals;
    RAISE NOTICE '=======================================================';
END $$;

-- ==============================================================================
-- 4. THỰC THI XÓA 10 TẦNG TỪ LÁ ĐẾN GỐC (LEAF -> ROOT, FK-SAFE & CHECK-SAFE)
-- ==============================================================================

-- Stage 1: Polymorphic / Unconstrained Audit, Notification & DLQ Tables
DELETE FROM notifications
WHERE user_id IN (SELECT id FROM target_test_users)
   OR related_entity_id IN (
       SELECT id FROM target_test_shifts
       UNION SELECT id FROM target_test_classes
       UNION SELECT id FROM target_test_courses
       UNION SELECT id FROM target_official_sessions
       UNION SELECT id FROM target_real_shifts
       UNION SELECT id FROM target_tickets
   );

DELETE FROM audit_logs
WHERE user_id IN (SELECT id FROM target_test_users)
   OR entity_id IN (
       SELECT id FROM target_test_shifts
       UNION SELECT id FROM target_test_classes
       UNION SELECT id FROM target_test_courses
       UNION SELECT id FROM target_official_sessions
       UNION SELECT id FROM target_real_shifts
       UNION SELECT id FROM target_tickets
   );

UPDATE dead_letter_queues
SET resolved_by_user_id = NULL
WHERE resolved_by_user_id IN (SELECT id FROM target_test_users);

DELETE FROM dead_letter_queues
WHERE correlation_id IN (
    SELECT id FROM target_practice_answers
    UNION SELECT id FROM target_mock_answers
    UNION SELECT id FROM target_exam_submissions
    UNION SELECT id FROM target_in_class_submissions
    UNION SELECT id FROM target_test_shifts
);

-- Stage 2: Evaluation & Audit Leaf Details (ON DELETE RESTRICT on criterion_id)
DELETE FROM ai_evaluation_details
WHERE evaluation_id IN (SELECT id FROM target_ai_evaluations)
   OR criterion_id IN (SELECT id FROM rubric_criteria WHERE rubric_id IN (SELECT id FROM target_test_rubrics));

DELETE FROM lecturer_audit_details
WHERE audit_id IN (SELECT id FROM target_lecturer_audits)
   OR submission_id IN (SELECT id FROM target_exam_submissions)
   OR criterion_id IN (SELECT id FROM rubric_criteria WHERE rubric_id IN (SELECT id FROM target_test_rubrics));

-- Stage 3: Evaluations, Jobs, Appeals, Anti-Cheat, Audits
DELETE FROM ai_evaluations
WHERE id IN (SELECT id FROM target_ai_evaluations);

DELETE FROM grading_jobs
WHERE submission_id IN (SELECT id FROM target_exam_submissions);

DELETE FROM appeal_requests
WHERE session_id IN (SELECT id FROM target_official_sessions)
   OR ticket_id IN (SELECT id FROM target_tickets)
   OR submission_id IN (SELECT id FROM target_exam_submissions)
   OR student_id IN (SELECT id FROM target_test_users)
   OR assigned_to IN (SELECT id FROM target_test_users);

DELETE FROM exam_anti_cheat_logs
WHERE ticket_id IN (SELECT id FROM target_tickets)
   OR shift_id IN (SELECT id FROM target_real_shifts)
   OR student_id IN (SELECT id FROM target_test_users);

DELETE FROM lecturer_audits
WHERE id IN (SELECT id FROM target_lecturer_audits);

-- Stage 4: In-Class Exam (MF-05) Leaves -> Shift (follow-ups BEFORE main rows)
DELETE FROM in_class_exam_submissions
WHERE id IN (SELECT id FROM target_in_class_submissions)
  AND (is_follow_up = true OR parent_submission_id IS NOT NULL);

DELETE FROM in_class_exam_submissions
WHERE id IN (SELECT id FROM target_in_class_submissions);

DELETE FROM in_class_shift_participants
WHERE shift_id IN (SELECT id FROM target_test_shifts)
   OR student_id IN (SELECT id FROM target_test_users)
   OR assigned_question_id IN (SELECT id FROM target_test_questions);

DELETE FROM in_class_shift_passwords
WHERE shift_id IN (SELECT id FROM target_test_shifts);

DELETE FROM in_class_exam_shifts
WHERE id IN (SELECT id FROM target_test_shifts);

-- Stage 5: Practice (MF-01) Leaves -> Session (follow-ups BEFORE main rows; NO parent_answer_id=NULL UPDATE!)
DELETE FROM practice_answers
WHERE id IN (SELECT id FROM target_practice_answers)
  AND (is_follow_up = true OR parent_answer_id IS NOT NULL);

DELETE FROM practice_answers
WHERE id IN (SELECT id FROM target_practice_answers);

DELETE FROM practice_sessions
WHERE id IN (SELECT id FROM target_practice_sessions);

-- Stage 6: Mock Exam (MF-02) Leaves -> Session & Quota (follow-ups BEFORE main rows)
DELETE FROM mock_exam_answers
WHERE id IN (SELECT id FROM target_mock_answers)
  AND (is_follow_up = true OR parent_answer_id IS NOT NULL);

DELETE FROM mock_exam_answers
WHERE id IN (SELECT id FROM target_mock_answers);

DELETE FROM mock_exam_sessions
WHERE id IN (SELECT id FROM target_mock_sessions);

DELETE FROM mock_exam_quotas
WHERE course_id IN (SELECT id FROM target_test_courses)
   OR student_id IN (SELECT id FROM target_test_users);

-- Stage 7: Official Exam (MF-04) Leaves -> Session
DELETE FROM exam_question_submissions
WHERE id IN (SELECT id FROM target_exam_submissions);

DELETE FROM student_exam_tickets
WHERE id IN (SELECT id FROM target_tickets);

DELETE FROM real_exam_session_shifts
WHERE id IN (SELECT id FROM target_real_shifts);

DELETE FROM exam_set_questions
WHERE exam_set_id IN (SELECT id FROM target_exam_sets)
   OR exam_question_id IN (SELECT id FROM target_test_exam_questions);

DELETE FROM exam_sets
WHERE id IN (SELECT id FROM target_exam_sets);

DELETE FROM official_exam_sessions
WHERE id IN (SELECT id FROM target_official_sessions);

-- Stage 8: Question Banks & Rubrics (RESTRICT order)
DELETE FROM practice_questions
WHERE id IN (SELECT id FROM target_test_questions);

DELETE FROM exam_questions
WHERE id IN (SELECT id FROM target_test_exam_questions);

DELETE FROM exam_structures
WHERE id IN (SELECT id FROM target_exam_structures);

DELETE FROM rubric_criteria
WHERE rubric_id IN (SELECT id FROM target_test_rubrics);

DELETE FROM rubrics
WHERE id IN (SELECT id FROM target_test_rubrics);

-- Stage 9: Classes & Course Hierarchy (RESTRICT order)
DELETE FROM class_enrollments
WHERE class_id IN (SELECT id FROM target_test_classes)
   OR student_id IN (SELECT id FROM target_test_users);

DELETE FROM classes
WHERE id IN (SELECT id FROM target_test_classes)
   OR course_offering_id IN (SELECT id FROM target_test_offerings);

DELETE FROM course_lecturers
WHERE course_id IN (SELECT id FROM target_test_courses)
   OR course_offering_id IN (SELECT id FROM target_test_offerings)
   OR lecturer_user_id IN (SELECT id FROM target_test_users)
   OR assigned_by_user_id IN (SELECT id FROM target_test_users);

DELETE FROM course_offerings
WHERE id IN (SELECT id FROM target_test_offerings);

DELETE FROM course_clos
WHERE id IN (SELECT id FROM target_test_clos);

DELETE FROM courses
WHERE id IN (SELECT id FROM target_test_courses);

-- Stage 10: Users & Auth
DELETE FROM refresh_tokens
WHERE user_id IN (SELECT id FROM target_test_users);

DELETE FROM users
WHERE id IN (SELECT id FROM target_test_users);

-- ==============================================================================
-- 5. HẬU KIỂM TÍNH TOÀN VẸN DỮ LIỆU SAU KHI PURGE (POST-PURGE VERIFICATION)
-- ==============================================================================
DO $$
DECLARE
    post_core_count integer;
    post_ent_count integer;
    rem_users integer;
    rem_courses integer;
    rem_classes integer;
    rem_rubrics integer;
    rem_questions integer;
    rem_shifts integer;
BEGIN
    SELECT COUNT(*) INTO post_core_count
    FROM courses
    WHERE code IN ('PRF192', 'CSD201', 'DBI202');

    IF post_core_count <> 3 THEN
        RAISE EXCEPTION '[POST-CHECK FAILED] Môn cốt lõi bị ảnh hưởng sau purge! Số lượng: %, kỳ vọng: 3', post_core_count;
    END IF;

    SELECT COUNT(*) INTO post_ent_count
    FROM courses
    WHERE code LIKE 'ENT%';

    IF post_ent_count < 6 THEN
        RAISE EXCEPTION '[POST-CHECK FAILED] Môn ENT bị ảnh hưởng sau purge! Số lượng: %, kỳ vọng >= 6', post_ent_count;
    END IF;

    SELECT COUNT(*) INTO rem_users FROM users WHERE id IN (SELECT id FROM target_test_users);
    SELECT COUNT(*) INTO rem_courses FROM courses WHERE id IN (SELECT id FROM target_test_courses);
    SELECT COUNT(*) INTO rem_classes FROM classes WHERE id IN (SELECT id FROM target_test_classes);
    SELECT COUNT(*) INTO rem_rubrics FROM rubrics WHERE id IN (SELECT id FROM target_test_rubrics);
    SELECT COUNT(*) INTO rem_questions FROM practice_questions WHERE id IN (SELECT id FROM target_test_questions);
    SELECT COUNT(*) INTO rem_shifts FROM in_class_exam_shifts WHERE id IN (SELECT id FROM target_test_shifts);

    IF rem_users + rem_courses + rem_classes + rem_rubrics + rem_questions + rem_shifts <> 0 THEN
        RAISE EXCEPTION '[POST-CHECK FAILED] Vẫn còn sót dữ liệu test: users=%, courses=%, classes=%, rubrics=%, questions=%, shifts=%',
            rem_users, rem_courses, rem_classes, rem_rubrics, rem_questions, rem_shifts;
    END IF;

    RAISE NOTICE '✅ HẬU KIỂM TOÀN VẸN THÀNH CÔNG: 3/3 môn cốt lõi, % môn ENT%% nguyên vẹn, 0 bản ghi rác tồn đọng.', post_ent_count;
END $$;

COMMIT;

\echo '=============================================================================='
\echo '🚀 [PURGE COMMITTED] Giao dịch Purge dữ liệu test hoàn tất thành công 100%!'
\echo '=============================================================================='
