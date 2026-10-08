-- =============================================================================
-- HỆ THỐNG LUYỆN THI & ĐÁNH GIÁ VẤN ĐÁP BẰNG LLM (ORAL EXAM SYSTEM)
-- ĐỀ TÀI CAPSTONE FA26SE166 — ĐẠI HỌC FPT TP.HCM (FPT SG)
-- Bản CSDL PostgreSQL 16+ Hợp Nhất Chuẩn Bậc 3 (3NF) & Phân Tách Practice - Exam
-- Khớp 100% với 27 C# Domain Entities, OralExamDbContext và ERD_DATABASE_DESIGN.md
-- =============================================================================

CREATE EXTENSION IF NOT EXISTS "pgcrypto";

-- =============================================================================
-- PHÂN HỆ 1: QUẢN TRỊ ĐÀO TẠO, NGƯỜI DÙNG & LỚP HỌC (IDENTITY & ACADEMIC)
-- =============================================================================

-- 1.1. Bảng Người dùng hệ thống (Admin, Giảng viên, Giám thị, Sinh viên)
CREATE TABLE users (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    email           VARCHAR(255) NOT NULL UNIQUE,
    full_name       VARCHAR(150) NOT NULL,
    student_code    VARCHAR(20) UNIQUE,
    role            VARCHAR(20) NOT NULL DEFAULT 'student',
    is_active       BOOLEAN NOT NULL DEFAULT TRUE,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT ck_users_role CHECK (role IN ('admin', 'department_head', 'lecturer', 'proctor', 'student'))
);

CREATE INDEX ix_users_email ON users (email);
CREATE INDEX ix_users_student_code ON users (student_code) WHERE student_code IS NOT NULL;

-- 1.2. Bảng Học kỳ (FA26, SP27...)
CREATE TABLE semesters (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    code            VARCHAR(20) NOT NULL UNIQUE,
    name            VARCHAR(100) NOT NULL,
    start_date      DATE NOT NULL,
    end_date        DATE NOT NULL,
    is_active       BOOLEAN NOT NULL DEFAULT TRUE,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT ck_semesters_dates CHECK (end_date >= start_date)
);

CREATE INDEX ix_semesters_code ON semesters (code);

-- 1.3. Bảng Môn học (PRN231, SWD392...)
CREATE TABLE courses (
    id                          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    code                        VARCHAR(20) NOT NULL UNIQUE,
    name                        VARCHAR(200) NOT NULL,
    credits                     INT NOT NULL DEFAULT 3,
    semester_id                 UUID NOT NULL REFERENCES semesters (id) ON DELETE RESTRICT,
    has_follow_up               BOOLEAN NOT NULL DEFAULT FALSE,
    transcript_buffer_seconds   INT NOT NULL DEFAULT 60,
    max_follow_up_questions     INT NOT NULL DEFAULT 2,
    max_mock_exams_per_day      INT NOT NULL DEFAULT 3,
    exam_input_mode             VARCHAR(30) NOT NULL DEFAULT 'VoiceWithTranscriptEdit',
    is_active                   BOOLEAN NOT NULL DEFAULT TRUE,
    created_at                  TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT ck_courses_credits CHECK (credits > 0),
    CONSTRAINT ck_courses_transcript_buffer CHECK (transcript_buffer_seconds >= 10 AND transcript_buffer_seconds <= 300),
    CONSTRAINT ck_courses_max_follow_up CHECK (max_follow_up_questions >= 1 AND max_follow_up_questions <= 5),
    CONSTRAINT ck_courses_mock_quota CHECK (max_mock_exams_per_day >= 1),
    CONSTRAINT ck_courses_input_mode CHECK (exam_input_mode IN ('VoiceOnly', 'VoiceWithTranscriptEdit'))
);

CREATE INDEX ix_courses_code ON courses (code);
CREATE INDEX ix_courses_semester ON courses (semester_id);

-- 1.4. Bảng Lớp học (SE1801, SE1802...)
CREATE TABLE classes (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    code            VARCHAR(50) NOT NULL UNIQUE,
    course_id       UUID NOT NULL REFERENCES courses (id) ON DELETE RESTRICT,
    semester_id     UUID NOT NULL REFERENCES semesters (id) ON DELETE RESTRICT,
    lecturer_id     UUID REFERENCES users (id) ON DELETE SET NULL,
    max_students    INT NOT NULL DEFAULT 35,
    is_active       BOOLEAN NOT NULL DEFAULT TRUE,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT ck_classes_max_students CHECK (max_students > 0)
);

CREATE INDEX ix_classes_code ON classes (code);
CREATE INDEX ix_classes_course ON classes (course_id);
CREATE INDEX ix_classes_semester ON classes (semester_id);
CREATE INDEX ix_classes_lecturer ON classes (lecturer_id);

-- 1.5. Bảng Danh sách sinh viên thuộc lớp (Enrollments)
CREATE TABLE class_enrollments (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    class_id        UUID NOT NULL REFERENCES classes (id) ON DELETE CASCADE,
    student_id      UUID NOT NULL REFERENCES users (id) ON DELETE CASCADE,
    enrolled_at     TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    status          VARCHAR(20) NOT NULL DEFAULT 'enrolled',
    CONSTRAINT uq_class_enrollments UNIQUE (class_id, student_id),
    CONSTRAINT ck_class_enrollment_status CHECK (status IN ('enrolled', 'dropped', 'completed'))
);

CREATE INDEX ix_class_enrollments_student ON class_enrollments (student_id);

-- =============================================================================
-- PHÂN HỆ 2: BAREM ĐÁNH GIÁ RUBRIC 10.0 (DÙNG CHUNG CHO CẢ LUYỆN TẬP VÀ THI CỬ)
-- =============================================================================

-- 2.1. Bảng Barem Rubric tổng quan (Ràng buộc bất biến Tổng điểm = 10.0đ)
CREATE TABLE rubrics (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    course_id           UUID NOT NULL REFERENCES courses (id) ON DELETE CASCADE,
    name                VARCHAR(200) NOT NULL,
    description         TEXT,
    total_max_score     NUMERIC(4, 2) NOT NULL DEFAULT 10.00,
    is_active           BOOLEAN NOT NULL DEFAULT TRUE,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT ck_rubrics_total_score CHECK (total_max_score = 10.00)
);

CREATE INDEX ix_rubrics_course ON rubrics (course_id);

-- 2.2. Bảng Tiêu chí thành phần của Barem (Criteria C1..Ck)
CREATE TABLE rubric_criteria (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    rubric_id           UUID NOT NULL REFERENCES rubrics (id) ON DELETE CASCADE,
    criterion_name      VARCHAR(200) NOT NULL,
    description         TEXT,
    max_score           NUMERIC(4, 2) NOT NULL,
    weight              NUMERIC(3, 2) NOT NULL,
    bloom_level         VARCHAR(20),
    order_index         INT NOT NULL DEFAULT 1,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT ck_rubric_criteria_score CHECK (max_score > 0 AND max_score <= 10.00),
    CONSTRAINT ck_rubric_criteria_weight CHECK (weight > 0 AND weight <= 1.00)
);

CREATE INDEX ix_rubric_criteria_rubric ON rubric_criteria (rubric_id);

-- =============================================================================
-- PHÂN HỆ 3: NGÂN HÀNG CÂU HỎI — PHÂN TÁCH RIÊNG PRACTICE VÀ EXAM
-- =============================================================================

-- 3.1. Bảng Câu hỏi luyện tập tự do (PRACTICE QUESTIONS - MF-01)
CREATE TABLE practice_questions (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    course_id           UUID NOT NULL REFERENCES courses (id) ON DELETE CASCADE,
    rubric_id           UUID NOT NULL REFERENCES rubrics (id) ON DELETE RESTRICT,
    title               VARCHAR(300) NOT NULL,
    content             TEXT NOT NULL,
    sample_answer       TEXT,
    key_points          JSONB NOT NULL DEFAULT '[]'::jsonb,
    difficulty          VARCHAR(20) NOT NULL DEFAULT 'medium',
    bloom_level         VARCHAR(20) NOT NULL DEFAULT 'Understand',
    has_follow_up       BOOLEAN NOT NULL DEFAULT FALSE,
    follow_up_prompt    TEXT,
    is_active           BOOLEAN NOT NULL DEFAULT TRUE,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT ck_practice_questions_diff CHECK (difficulty IN ('easy', 'medium', 'hard')),
    CONSTRAINT ck_practice_questions_bloom CHECK (bloom_level IN ('Remember', 'Understand', 'Apply', 'Analyze', 'Evaluate', 'Create'))
);

CREATE INDEX ix_practice_questions_course ON practice_questions (course_id, difficulty, is_active);
CREATE INDEX ix_practice_questions_rubric ON practice_questions (rubric_id);

-- 3.2. Bảng Câu hỏi thi cử chính thức & thi thử (EXAM QUESTIONS - MF-02 & MF-04)
CREATE TABLE exam_questions (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    course_id           UUID NOT NULL REFERENCES courses (id) ON DELETE CASCADE,
    rubric_id           UUID NOT NULL REFERENCES rubrics (id) ON DELETE RESTRICT,
    title               VARCHAR(300) NOT NULL,
    content             TEXT NOT NULL,
    sample_answer       TEXT,
    key_points          JSONB NOT NULL DEFAULT '[]'::jsonb,
    difficulty          VARCHAR(20) NOT NULL DEFAULT 'medium',
    bloom_level         VARCHAR(20) NOT NULL DEFAULT 'Understand',
    approved_by         UUID REFERENCES users (id) ON DELETE SET NULL,
    source              VARCHAR(50) NOT NULL DEFAULT 'manual',
    approval_status     VARCHAR(30) NOT NULL DEFAULT 'DRAFT',
    submitted_by        UUID REFERENCES users (id) ON DELETE SET NULL,
    review_notes        TEXT,
    is_active           BOOLEAN NOT NULL DEFAULT TRUE,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT ck_exam_questions_diff CHECK (difficulty IN ('easy', 'medium', 'hard')),
    CONSTRAINT ck_exam_questions_bloom CHECK (bloom_level IN ('Remember', 'Understand', 'Apply', 'Analyze', 'Evaluate', 'Create'))
);

CREATE INDEX ix_exam_questions_course ON exam_questions (course_id, difficulty, is_active);
CREATE INDEX ix_exam_questions_rubric ON exam_questions (rubric_id);
CREATE INDEX ix_exam_questions_approved ON exam_questions (approved_by);
CREATE INDEX ix_exam_questions_approval_status ON exam_questions (approval_status);
CREATE INDEX ix_exam_questions_submitted ON exam_questions (submitted_by);

-- =============================================================================
-- PHÂN HỆ 4: PHÂN HỆ LUYỆN TẬP TỰ DO (PRACTICE - MF-01)
-- =============================================================================

-- 4.1. Bảng Phiên luyện tập của sinh viên
CREATE TABLE practice_sessions (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    student_id          UUID NOT NULL REFERENCES users (id) ON DELETE CASCADE,
    course_id           UUID NOT NULL REFERENCES courses (id) ON DELETE CASCADE,
    practice_mode       VARCHAR(20) NOT NULL DEFAULT 'per_question',
    started_at          TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    completed_at        TIMESTAMPTZ,
    status              VARCHAR(20) NOT NULL DEFAULT 'in_progress',
    CONSTRAINT ck_practice_sessions_mode CHECK (practice_mode IN ('per_question', 'full_session')),
    CONSTRAINT ck_practice_sessions_status CHECK (status IN ('in_progress', 'completed', 'abandoned'))
);

CREATE INDEX ix_practice_sessions_student ON practice_sessions (student_id, started_at DESC);
CREATE INDEX ix_practice_sessions_course ON practice_sessions (course_id);

-- 4.2. Bảng Câu trả lời luyện tập (Self-Referencing cho câu hỏi phụ Follow-up)
CREATE TABLE practice_answers (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    session_id          UUID NOT NULL REFERENCES practice_sessions (id) ON DELETE CASCADE,
    question_id         UUID NOT NULL REFERENCES practice_questions (id) ON DELETE RESTRICT,
    student_id          UUID NOT NULL REFERENCES users (id) ON DELETE CASCADE,
    answer_text         TEXT NOT NULL,
    is_follow_up        BOOLEAN NOT NULL DEFAULT FALSE,
    parent_answer_id    UUID REFERENCES practice_answers (id) ON DELETE SET NULL,
    status              VARCHAR(20) NOT NULL DEFAULT 'pending',
    submitted_at        TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT ck_practice_answers_status CHECK (status IN ('pending', 'grading', 'graded', 'failed'))
);

CREATE INDEX ix_practice_answers_session ON practice_answers (session_id);
CREATE INDEX ix_practice_answers_question ON practice_answers (question_id);
CREATE INDEX ix_practice_answers_student ON practice_answers (student_id);
CREATE INDEX ix_practice_answers_parent ON practice_answers (parent_answer_id);

-- 4.3. Bảng Đánh giá AI chi tiết cho câu trả lời (Dùng chung cho cả practice & exam submission)
CREATE TABLE ai_evaluations (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    answer_type         VARCHAR(20) NOT NULL,
    practice_answer_id  UUID REFERENCES practice_answers (id) ON DELETE CASCADE,
    exam_submission_id  UUID, -- Sẽ gắn FOREIGN KEY tới exam_question_submissions ở Phân hệ 6
    total_score         NUMERIC(4, 2) NOT NULL,
    feedback            TEXT,
    confidence_score    NUMERIC(3, 2),
    cot_trace           TEXT,
    evaluated_at        TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT ck_ai_evaluations_type CHECK (answer_type IN ('practice', 'exam')),
    CONSTRAINT ck_ai_evaluations_score CHECK (total_score >= 0 AND total_score <= 10.00)
);

CREATE INDEX ix_ai_evaluations_practice_answer ON ai_evaluations (practice_answer_id);
CREATE INDEX ix_ai_evaluations_exam_submission ON ai_evaluations (exam_submission_id);

-- 4.4. Bảng Điểm chi tiết từng tiêu chí Rubric của AI chấm
CREATE TABLE ai_evaluation_details (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    evaluation_id       UUID NOT NULL REFERENCES ai_evaluations (id) ON DELETE CASCADE,
    criterion_id        UUID NOT NULL REFERENCES rubric_criteria (id) ON DELETE RESTRICT,
    score               NUMERIC(4, 2) NOT NULL,
    comment             TEXT,
    CONSTRAINT ck_ai_eval_detail_score CHECK (score >= 0)
);

CREATE INDEX ix_ai_eval_details_evaluation ON ai_evaluation_details (evaluation_id);
CREATE INDEX ix_ai_eval_details_criterion ON ai_evaluation_details (criterion_id);

-- =============================================================================
-- PHÂN HỆ 5: CẤU TRÚC ĐỀ THI & THI THỬ BẤM GIỜ (MOCK EXAM - MF-02)
-- =============================================================================

-- 5.1. Bảng Cấu trúc ma trận đề thi do Giảng viên thiết lập
CREATE TABLE exam_structures (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    course_id           UUID NOT NULL REFERENCES courses (id) ON DELETE CASCADE,
    created_by          UUID NOT NULL REFERENCES users (id) ON DELETE RESTRICT,
    name                VARCHAR(200) NOT NULL,
    total_questions     INT NOT NULL DEFAULT 5,
    duration_minutes    INT NOT NULL DEFAULT 30,
    bloom_distribution  JSONB NOT NULL DEFAULT '{}'::jsonb,
    is_active           BOOLEAN NOT NULL DEFAULT TRUE,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT ck_exam_struct_duration CHECK (duration_minutes > 0),
    CONSTRAINT ck_exam_struct_questions CHECK (total_questions > 0)
);

CREATE INDEX ix_exam_structures_course ON exam_structures (course_id);
CREATE INDEX ix_exam_structures_created_by ON exam_structures (created_by);

-- 5.2. Bảng Bộ đề thi sinh ngẫu nhiên từ cấu trúc
CREATE TABLE exam_sets (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    structure_id        UUID NOT NULL REFERENCES exam_structures (id) ON DELETE CASCADE,
    course_id           UUID NOT NULL REFERENCES courses (id) ON DELETE CASCADE,
    set_code            VARCHAR(50) NOT NULL,
    generated_at        TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    is_active           BOOLEAN NOT NULL DEFAULT TRUE
);

CREATE INDEX ix_exam_sets_structure ON exam_sets (structure_id);
CREATE INDEX ix_exam_sets_course ON exam_sets (course_id);

-- 5.3. Bảng Liên kết câu hỏi trong bộ đề
CREATE TABLE exam_set_questions (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    exam_set_id         UUID NOT NULL REFERENCES exam_sets (id) ON DELETE CASCADE,
    exam_question_id    UUID NOT NULL REFERENCES exam_questions (id) ON DELETE RESTRICT,
    order_index         INT NOT NULL DEFAULT 1,
    CONSTRAINT uq_exam_set_questions UNIQUE (exam_set_id, exam_question_id)
);

CREATE INDEX ix_exam_set_questions_set ON exam_set_questions (exam_set_id);
CREATE INDEX ix_exam_set_questions_question ON exam_set_questions (exam_question_id);

-- 5.4. Bảng Hạn ngạch thi thử bấm giờ (Daily Quota Guard theo môn max_mock_exams_per_day)
CREATE TABLE mock_exam_quotas (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    student_id          UUID NOT NULL REFERENCES users (id) ON DELETE CASCADE,
    course_id           UUID NOT NULL REFERENCES courses (id) ON DELETE CASCADE,
    quota_date          DATE NOT NULL,
    used_count          INT NOT NULL DEFAULT 0,
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT uq_mock_exam_quotas UNIQUE (student_id, course_id, quota_date),
    CONSTRAINT ck_mock_exam_quotas_count CHECK (used_count >= 0)
);

CREATE INDEX ix_mock_exam_quotas_lookup ON mock_exam_quotas (student_id, course_id, quota_date);

-- 5.5. Bảng Phiên thi thử của sinh viên
CREATE TABLE mock_exam_sessions (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    student_id          UUID NOT NULL REFERENCES users (id) ON DELETE CASCADE,
    exam_set_id         UUID NOT NULL REFERENCES exam_sets (id) ON DELETE RESTRICT,
    course_id           UUID NOT NULL REFERENCES courses (id) ON DELETE CASCADE,
    has_follow_up       BOOLEAN NOT NULL DEFAULT FALSE,
    started_at          TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    expires_at          TIMESTAMPTZ NOT NULL,
    submitted_at        TIMESTAMPTZ,
    status              VARCHAR(20) NOT NULL DEFAULT 'in_progress',
    total_score         NUMERIC(4, 2),
    CONSTRAINT ck_mock_sessions_status CHECK (status IN ('in_progress', 'submitted', 'graded', 'abandoned'))
);

CREATE INDEX ix_mock_exam_sessions_student ON mock_exam_sessions (student_id, started_at DESC);
CREATE INDEX ix_mock_exam_sessions_set ON mock_exam_sessions (exam_set_id);
CREATE INDEX ix_mock_exam_sessions_course ON mock_exam_sessions (course_id);

-- 5.6. Bảng Câu trả lời thi thử (Rút từ kho practice_questions chung với MF-01)
CREATE TABLE mock_exam_answers (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    session_id          UUID NOT NULL REFERENCES mock_exam_sessions (id) ON DELETE CASCADE,
    question_id         UUID NOT NULL REFERENCES practice_questions (id) ON DELETE RESTRICT,
    answer_text         TEXT,
    time_taken_seconds  INT NOT NULL DEFAULT 0,
    status              VARCHAR(20) NOT NULL DEFAULT 'answered',
    answered_at         TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT ck_mock_answers_status CHECK (status IN ('pending', 'answered', 'graded', 'failed'))
);

CREATE INDEX ix_mock_exam_answers_session ON mock_exam_answers (session_id);
CREATE INDEX ix_mock_exam_answers_question ON mock_exam_answers (question_id);

-- =============================================================================
-- PHÂN HỆ 6: THI THẬT PHÒNG LAB, THU ÂM R2 & THẨM ĐỊNH KHÓA 1 CHIỀU (MF-04)
-- =============================================================================

-- 6.1. Bảng Ca thi vấn đáp chính thức tại phòng Lab
CREATE TABLE official_exam_sessions (
    id                          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    course_id                   UUID NOT NULL REFERENCES courses (id) ON DELETE CASCADE,
    exam_structure_id           UUID NOT NULL REFERENCES exam_structures (id) ON DELETE RESTRICT,
    title                       VARCHAR(255) NOT NULL,
    exam_date                   DATE NOT NULL,
    has_follow_up               BOOLEAN NOT NULL DEFAULT FALSE,
    max_follow_up_questions     INT NOT NULL DEFAULT 2,
    exam_input_mode             VARCHAR(30) NOT NULL DEFAULT 'VoiceWithTranscriptEdit',
    transcript_buffer_seconds   INT NOT NULL DEFAULT 60,
    status                      VARCHAR(20) NOT NULL DEFAULT 'scheduled',
    created_by                  UUID REFERENCES users (id) ON DELETE SET NULL,
    created_at                  TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT ck_official_sessions_status CHECK (status IN ('scheduled', 'in_progress', 'grading', 'auditing', 'concluded')),
    CONSTRAINT ck_official_sessions_max_follow_up CHECK (max_follow_up_questions >= 1 AND max_follow_up_questions <= 5),
    CONSTRAINT ck_official_sessions_input_mode CHECK (exam_input_mode IN ('VoiceOnly', 'VoiceWithTranscriptEdit')),
    CONSTRAINT ck_official_sessions_transcript_buffer CHECK (transcript_buffer_seconds >= 10 AND transcript_buffer_seconds <= 300)
);

CREATE INDEX ix_official_exam_sessions_course ON official_exam_sessions (course_id);
CREATE INDEX ix_official_exam_sessions_structure ON official_exam_sessions (exam_structure_id);
CREATE INDEX ix_official_exam_sessions_creator ON official_exam_sessions (created_by);

-- 6.2. Bảng Ca thi chi tiết theo Kíp / Phòng Lab (Shifts)
CREATE TABLE real_exam_session_shifts (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    session_id          UUID NOT NULL REFERENCES official_exam_sessions (id) ON DELETE CASCADE,
    shift_name          VARCHAR(100) NOT NULL,
    room_lab            VARCHAR(50) NOT NULL,
    start_time          TIMESTAMPTZ NOT NULL,
    end_time            TIMESTAMPTZ NOT NULL,
    proctor_user_id     UUID REFERENCES users (id) ON DELETE SET NULL,
    status              VARCHAR(20) NOT NULL DEFAULT 'scheduled',
    created_at          TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT ck_shifts_time CHECK (end_time > start_time),
    CONSTRAINT ck_shifts_status CHECK (status IN ('scheduled', 'in_progress', 'completed', 'cancelled'))
);

CREATE INDEX ix_real_exam_shifts_session ON real_exam_session_shifts (session_id);
CREATE INDEX ix_real_exam_shifts_proctor ON real_exam_session_shifts (proctor_user_id);

-- 6.3. Bảng Vé thi thí sinh tại phòng máy Lab (Gán STT = Số máy thi 1-40)
CREATE TABLE student_exam_tickets (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    shift_id            UUID NOT NULL REFERENCES real_exam_session_shifts (id) ON DELETE CASCADE,
    student_id          UUID NOT NULL REFERENCES users (id) ON DELETE CASCADE,
    exam_set_id         UUID REFERENCES exam_sets (id) ON DELETE SET NULL,
    seat_number         INT NOT NULL,
    ip_address          VARCHAR(45),
    status              VARCHAR(20) NOT NULL DEFAULT 'SCHEDULED',
    is_locked           BOOLEAN NOT NULL DEFAULT FALSE,
    checked_in_at       TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    submitted_at        TIMESTAMPTZ,
    ai_confidence_score NUMERIC(5,2),
    is_suspicious       BOOLEAN NOT NULL DEFAULT FALSE,
    student_acknowledgement_status VARCHAR(20) NOT NULL DEFAULT 'PENDING',
    published_at        TIMESTAMPTZ,
    published_by        UUID REFERENCES users (id) ON DELETE SET NULL,
    CONSTRAINT uq_shift_seat UNIQUE (shift_id, seat_number),
    CONSTRAINT uq_shift_student UNIQUE (shift_id, student_id),
    CONSTRAINT ck_tickets_seat CHECK (seat_number >= 1 AND seat_number <= 40),
    CONSTRAINT ck_tickets_status CHECK (status IN ('SCHEDULED', 'IN_PROGRESS', 'SUBMITTED', 'AI_GRADED', 'AUDITED', 'PUBLISHED', 'LOCKED')),
    CONSTRAINT ck_tickets_ack_status CHECK (student_acknowledgement_status IN ('PENDING', 'ACKNOWLEDGED', 'APPEALED'))
);

CREATE INDEX ix_student_exam_tickets_shift ON student_exam_tickets (shift_id);
CREATE INDEX ix_student_exam_tickets_student ON student_exam_tickets (student_id);
CREATE INDEX ix_student_exam_tickets_set ON student_exam_tickets (exam_set_id);
CREATE INDEX ix_student_exam_tickets_published_by ON student_exam_tickets (published_by);

-- 6.4. Bảng Bản nộp âm thanh câu hỏi thi thật (Cloudflare R2: STT_MSSV.webm + SHA-256 seal)
CREATE TABLE exam_question_submissions (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    ticket_id           UUID NOT NULL REFERENCES student_exam_tickets (id) ON DELETE CASCADE,
    exam_question_id    UUID NOT NULL REFERENCES exam_questions (id) ON DELETE RESTRICT,
    audio_r2_url        TEXT,
    audio_hash_sha256   VARCHAR(64),
    transcript_whisper  TEXT,
    timestamps_whisper  JSONB,
    time_spent_seconds  INT NOT NULL DEFAULT 0,
    ai_score            NUMERIC(4, 2),
    final_score         NUMERIC(4, 2),
    grading_status      VARCHAR(20) NOT NULL DEFAULT 'pending',
    submitted_at        TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT uq_ticket_question UNIQUE (ticket_id, exam_question_id),
    CONSTRAINT ck_submissions_grading_status CHECK (grading_status IN ('pending', 'transcribing', 'transcribed', 'ai_graded', 'audited', 'failed'))
);

CREATE INDEX ix_exam_question_submissions_ticket ON exam_question_submissions (ticket_id);
CREATE INDEX ix_exam_question_submissions_question ON exam_question_submissions (exam_question_id);

-- Ràng buộc khóa ngoại từ ai_evaluations về exam_question_submissions
ALTER TABLE ai_evaluations
    ADD CONSTRAINT fk_ai_evaluations_exam_submission
    FOREIGN KEY (exam_submission_id)
    REFERENCES exam_question_submissions (id) ON DELETE CASCADE;

-- 6.5. Bảng Hậu kiểm & Điều chỉnh điểm của Giảng viên (Lecturer Audit)
CREATE TABLE lecturer_audits (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    ticket_id           UUID NOT NULL UNIQUE REFERENCES student_exam_tickets (id) ON DELETE CASCADE,
    lecturer_id         UUID NOT NULL REFERENCES users (id) ON DELETE RESTRICT,
    original_ai_score   NUMERIC(4, 2) NOT NULL,
    audited_score       NUMERIC(4, 2) NOT NULL,
    override_reason     TEXT NOT NULL,
    is_locked           BOOLEAN NOT NULL DEFAULT FALSE,
    audited_at          TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT ck_lecturer_audits_scores CHECK (original_ai_score >= 0 AND audited_score >= 0)
);

CREATE INDEX ix_lecturer_audits_ticket ON lecturer_audits (ticket_id);
CREATE INDEX ix_lecturer_audits_lecturer ON lecturer_audits (lecturer_id);

-- 6.6. Bảng Chi tiết điều chỉnh theo từng tiêu chí Barem
CREATE TABLE lecturer_audit_details (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    audit_id            UUID NOT NULL REFERENCES lecturer_audits (id) ON DELETE CASCADE,
    criterion_id        UUID NOT NULL REFERENCES rubric_criteria (id) ON DELETE RESTRICT,
    ai_score            NUMERIC(4, 2) NOT NULL,
    audited_score       NUMERIC(4, 2) NOT NULL,
    lecturer_comment    TEXT,
    CONSTRAINT ck_audit_details_scores CHECK (ai_score >= 0 AND audited_score >= 0)
);

CREATE INDEX ix_lecturer_audit_details_audit ON lecturer_audit_details (audit_id);
CREATE INDEX ix_lecturer_audit_details_criterion ON lecturer_audit_details (criterion_id);

-- 6.7. Bảng Quản lý Đơn Phúc khảo Nội bộ (Internal Exam Appeals - MF-04)
CREATE TABLE appeal_requests (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    ticket_id           UUID NOT NULL REFERENCES student_exam_tickets (id) ON DELETE CASCADE,
    student_id          UUID NOT NULL REFERENCES users (id) ON DELETE CASCADE,
    session_id          UUID NOT NULL REFERENCES official_exam_sessions (id) ON DELETE CASCADE,
    submission_id       UUID REFERENCES exam_question_submissions (id) ON DELETE SET NULL,
    reason              TEXT NOT NULL,
    status              VARCHAR(20) NOT NULL DEFAULT 'PENDING',
    assigned_to         UUID NOT NULL REFERENCES users (id) ON DELETE RESTRICT,
    reviewed_by         UUID REFERENCES users (id) ON DELETE SET NULL,
    decision            VARCHAR(50),
    original_score      NUMERIC(4, 2),
    proposed_score      NUMERIC(4, 2),
    review_notes        TEXT,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    resolved_at         TIMESTAMPTZ,
    CONSTRAINT ck_appeal_requests_status 
        CHECK (status IN ('PENDING', 'IN_REVIEW', 'APPROVED', 'REJECTED', 'CANCELLED')),
    CONSTRAINT ck_appeal_requests_scores 
        CHECK ((original_score IS NULL OR (original_score >= 0 AND original_score <= 10.00)) AND
               (proposed_score IS NULL OR (proposed_score >= 0 AND proposed_score <= 10.00)))
);

CREATE INDEX ix_appeal_requests_ticket ON appeal_requests (ticket_id);
CREATE INDEX ix_appeal_requests_student ON appeal_requests (student_id);
CREATE INDEX ix_appeal_requests_session ON appeal_requests (session_id);
CREATE INDEX ix_appeal_requests_assigned ON appeal_requests (assigned_to, status);
CREATE INDEX ix_appeal_requests_status ON appeal_requests (status, created_at DESC);

-- =============================================================================
-- PHÂN HỆ 7: KIẾN TRÚC PHÒNG VỆ ZERO DATA LOSS & NHẬT KÝ HỆ THỐNG
-- =============================================================================

-- 7.1. Bảng Dead-Letter Queue (Khu cách ly cứu hộ tác vụ chấm thi lỗi khi AI nghẽn mạng)
CREATE TABLE dead_letter_queues (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    task_type           VARCHAR(50) NOT NULL,
    payload_json        JSONB NOT NULL,
    error_message       TEXT NOT NULL,
    retry_count         INT NOT NULL DEFAULT 0,
    status              VARCHAR(20) NOT NULL DEFAULT 'pending',
    created_at          TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    resolved_at         TIMESTAMPTZ,
    CONSTRAINT ck_dlq_status CHECK (status IN ('pending', 'resolved', 'abandoned'))
);

CREATE INDEX ix_dead_letter_queues_status ON dead_letter_queues (status, created_at);

-- 7.2. Bảng Nhật ký kiểm toán hệ thống (Audit Logs)
CREATE TABLE audit_logs (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id             UUID REFERENCES users (id) ON DELETE SET NULL,
    action              VARCHAR(50) NOT NULL,
    entity_name         VARCHAR(100) NOT NULL,
    entity_id           UUID,
    old_values          JSONB,
    new_values          JSONB,
    ip_address          VARCHAR(45),
    created_at          TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX ix_audit_logs_user ON audit_logs (user_id);
CREATE INDEX ix_audit_logs_entity ON audit_logs (entity_name, entity_id);
CREATE INDEX ix_audit_logs_created ON audit_logs (created_at DESC);

-- =============================================================================
-- PHÂN HỆ 8: CẤU HÌNH HỆ THỐNG ENTERPRISE (SYSTEM CONFIGS)
-- =============================================================================

CREATE TABLE system_configs (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    key                 VARCHAR(100) NOT NULL,
    value               VARCHAR(500) NOT NULL,
    description         TEXT,
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT uq_system_configs_key UNIQUE (key)
);

CREATE INDEX ix_system_configs_key ON system_configs (key);

-- =============================================================================
-- PHÂN HỆ 9: THÔNG BÁO TRONG ỨNG DỤNG (IN-APP NOTIFICATIONS - FE-11)
-- =============================================================================

CREATE TABLE notifications (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id             UUID NOT NULL REFERENCES users (id) ON DELETE CASCADE,
    title               VARCHAR(200) NOT NULL,
    message             TEXT NOT NULL,
    type                VARCHAR(50) NOT NULL,
    is_read             BOOLEAN NOT NULL DEFAULT FALSE,
    metadata            JSONB,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    read_at             TIMESTAMPTZ
);

CREATE INDEX idx_notifications_user_read ON notifications (user_id, is_read, created_at DESC);
