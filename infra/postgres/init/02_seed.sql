-- =============================================================================
-- DỮ LIỆU SEED MẪU CHO MÔI TRƯỜNG DEV/TEST (ORAL EXAM SYSTEM)
-- ĐỀ TÀI CAPSTONE FA26SE166 — ĐẠI HỌC FPT TP.HCM (FPT SG)
-- Phủ đầy đủ 6 Bounded Contexts, Barem Rubric 10.0đ và tách riêng Practice - Exam
-- Khớp 100% với 30 C# Domain Entities, OralExamDbContext và 01_schema.sql
-- =============================================================================

-- 1. Seed Người dùng hệ thống (Admin, Giảng viên, Giám thị, Sinh viên)
INSERT INTO users (id, email, full_name, student_code, role, is_active) VALUES
    ('00000000-0000-0000-0000-000000000001', 'admin@fpt.edu.vn', 'Nguyen Quang Thanh', NULL, 'admin', TRUE),
    ('00000000-0000-0000-0000-000000000002', 'headse@fe.edu.vn', 'Tran Van Truong', NULL, 'department_head', TRUE),
    ('00000000-0000-0000-0000-000000000003', 'totnt@fe.edu.vn', 'Nguyen Trong Tot', NULL, 'lecturer', TRUE),
    ('00000000-0000-0000-0000-000000000004', 'haind@fe.edu.vn', 'Nguyen Dang Hai', NULL, 'proctor', TRUE),
    ('00000000-0000-0000-0000-000000000005', 'hoanglvse170001@fpt.edu.vn', 'Le Vu Hoang', 'SE170001', 'student', TRUE),
    ('00000000-0000-0000-0000-000000000006', 'maittse170002@fpt.edu.vn', 'Tran Thi Mai', 'SE170002', 'student', TRUE)
ON CONFLICT (id) DO NOTHING;

-- 2. Seed Học kỳ (Semesters)
INSERT INTO semesters (id, code, name, start_date, end_date, is_active) VALUES
    ('00000000-0000-0000-0000-000000000007', 'FA26', 'Fall 2026', '2026-09-01', '2026-12-31', TRUE)
ON CONFLICT (id) DO NOTHING;

-- 3. Seed Môn học (Courses)
INSERT INTO courses (id, code, name, credits, semester_id, has_follow_up, transcript_buffer_seconds, max_follow_up_questions, max_mock_exams_per_day, is_active) VALUES
    ('00000000-0000-0000-0000-000000000008', 'PRN231', 'Building Cross-Platform Back-End Applications with .NET', 3, '00000000-0000-0000-0000-000000000007', TRUE, 60, 2, 3, TRUE),
    ('00000000-0000-0000-0000-000000000009', 'SWD392', 'Software Architecture and Design', 3, '00000000-0000-0000-0000-000000000007', TRUE, 60, 2, 3, TRUE)
ON CONFLICT (id) DO NOTHING;

-- 4. Seed Lớp học (Classes)
INSERT INTO classes (id, code, course_id, semester_id, lecturer_id, max_students, is_active) VALUES
    ('00000000-0000-0000-0000-000000000010', 'SE1801-NET', '00000000-0000-0000-0000-000000000008', '00000000-0000-0000-0000-000000000007', '00000000-0000-0000-0000-000000000003', 35, TRUE)
ON CONFLICT (id) DO NOTHING;

-- 5. Seed Danh sách sinh viên thuộc lớp (Class Enrollments)
INSERT INTO class_enrollments (id, class_id, student_id, status) VALUES
    ('00000000-0000-0000-0000-000000000011', '00000000-0000-0000-0000-000000000010', '00000000-0000-0000-0000-000000000005', 'enrolled'),
    ('00000000-0000-0000-0000-000000000012', '00000000-0000-0000-0000-000000000010', '00000000-0000-0000-0000-000000000006', 'enrolled')
ON CONFLICT (id) DO NOTHING;

-- 6. Seed Barem Rubric tổng quan (Ràng buộc bắt buộc: Tổng điểm = 10.0đ)
INSERT INTO rubrics (id, course_id, name, description, total_max_score, is_active) VALUES
    ('00000000-0000-0000-0000-000000000013', '00000000-0000-0000-0000-000000000008', 'Barem Đánh Giá Kiến Trúc .NET 8 Clean Architecture', 'Đánh giá hiểu biết về Clean Architecture 4 tầng, CQRS MediatR và Resilience', 10.00, TRUE)
ON CONFLICT (id) DO NOTHING;

-- 7. Seed Tiêu chí Rubric thành phần (Sum = 3.0 + 4.0 + 3.0 = 10.0đ)
INSERT INTO rubric_criteria (id, rubric_id, criterion_name, description, max_score, weight, bloom_level, order_index) VALUES
    ('00000000-0000-0000-0000-000000000014', '00000000-0000-0000-0000-000000000013', 'Khái niệm & Ranh giới 4 tầng Clean Architecture', 'Hiểu rõ vai trò Domain, Application, Infrastructure, API. Domain không phụ thuộc tầng nào.', 3.00, 0.30, 'Understand', 1),
    ('00000000-0000-0000-0000-000000000015', '00000000-0000-0000-0000-000000000013', 'Nguyên lý Dependency Inversion & CQRS MediatR', 'Áp dụng tách biệt Command và Query, sử dụng Interface và Dependency Injection Container.', 4.00, 0.40, 'Apply', 2),
    ('00000000-0000-0000-0000-000000000016', '00000000-0000-0000-0000-000000000013', 'Xử lý Ngoại lệ & Kiến trúc chịu lỗi (Resilience)', 'Trình bày cơ chế Global Exception RFC 7807, Bounded Channel và xử lý retry Polly.', 3.00, 0.30, 'Analyze', 3)
ON CONFLICT (id) DO NOTHING;

-- 8. Seed Câu hỏi Luyện tập tự do (PRACTICE QUESTIONS - MF-01)
INSERT INTO practice_questions (id, course_id, rubric_id, title, content, sample_answer, key_points, difficulty, bloom_level, has_follow_up, follow_up_prompt, is_active) VALUES
    ('00000000-0000-0000-0000-000000000017', '00000000-0000-0000-0000-000000000008', '00000000-0000-0000-0000-000000000013',
     'Nguyên lý Dependency Inversion trong Clean Architecture',
     'Hãy giải thích nguyên lý Dependency Inversion (DIP) trong SOLID và cách hiện thực trong Clean Architecture .NET 8?',
     'Nguyên lý DIP phát biểu rằng các module cấp cao không nên phụ thuộc vào module cấp thấp, cả hai nên phụ thuộc vào abstraction (interface). Trong Clean Architecture, tầng Application định nghĩa Interface và tầng Infrastructure sẽ implement nó.',
     '["Module cấp cao không phụ thuộc module cấp thấp", "Phụ thuộc vào Abstraction/Interface", "Domain/Application nằm ở trung tâm", "Infrastructure triển khai Interface"]'::jsonb,
     'medium', 'Understand', TRUE,
     'Nếu muốn thay đổi cơ sở dữ liệu từ PostgreSQL sang SQL Server, bạn cần thay đổi những tầng nào và tại sao?',
     TRUE)
ON CONFLICT (id) DO NOTHING;

-- 9. Seed Cấu trúc đề thi (EXAM STRUCTURES - MF-02 & MF-04)
INSERT INTO exam_structures (id, course_id, created_by, name, total_questions, duration_minutes, bloom_distribution, is_active) VALUES
    ('00000000-0000-0000-0000-000000000018', '00000000-0000-0000-0000-000000000008', '00000000-0000-0000-0000-000000000003',
     'Cấu trúc Đề thi Vấn đáp Cuối kỳ PRN231 (FA26)', 5, 30,
     '{"Understand": 2, "Apply": 2, "Analyze": 1}'::jsonb,
     TRUE)
ON CONFLICT (id) DO NOTHING;

-- 10. Seed Bộ đề thi (EXAM SETS)
INSERT INTO exam_sets (id, structure_id, course_id, set_code, is_active) VALUES
    ('00000000-0000-0000-0000-000000000019', '00000000-0000-0000-0000-000000000018', '00000000-0000-0000-0000-000000000008', 'PRN231-SET01', TRUE)
ON CONFLICT (id) DO NOTHING;

-- 11. Seed Câu hỏi Thi cử chính thức (EXAM QUESTIONS - MF-02 & MF-04)
INSERT INTO exam_questions (id, course_id, rubric_id, title, content, sample_answer, key_points, difficulty, bloom_level, approved_by, is_active) VALUES
    ('00000000-0000-0000-0000-000000000020', '00000000-0000-0000-0000-000000000008', '00000000-0000-0000-0000-000000000013',
     'Phân biệt BackgroundService và IHostedService trong .NET 8',
     'Phân biệt sự khác nhau giữa BackgroundService và IHostedService trong .NET 8 khi xây dựng hàng đợi xử lý chấm điểm ngầm?',
     'IHostedService là interface nền tảng định nghĩa StartAsync và StopAsync. BackgroundService là một abstract class kế thừa IHostedService và triển khai sẵn ExecuteAsync chạy nền vòng lặp an toàn.',
     '["IHostedService là interface cốt lõi", "BackgroundService là abstract class kế thừa IHostedService", "ExecuteAsync chạy Task bất đồng bộ", "Dùng BoundedChannelReader rút task an toàn"]'::jsonb,
     'hard', 'Analyze', '00000000-0000-0000-0000-000000000002', TRUE)
ON CONFLICT (id) DO NOTHING;

-- 12. Seed Liên kết câu hỏi trong bộ đề (EXAM SET QUESTIONS)
INSERT INTO exam_set_questions (id, exam_set_id, exam_question_id, order_index) VALUES
    ('00000000-0000-0000-0000-000000000021', '00000000-0000-0000-0000-000000000019', '00000000-0000-0000-0000-000000000020', 1)
ON CONFLICT (id) DO NOTHING;

-- 13. Seed Ca thi phòng Lab chính thức (OFFICIAL EXAM SESSIONS - MF-04)
INSERT INTO official_exam_sessions (id, course_id, exam_structure_id, title, exam_date, status) VALUES
    ('00000000-0000-0000-0000-000000000022', '00000000-0000-0000-0000-000000000008', '00000000-0000-0000-0000-000000000018',
     'Vấn đáp Cuối kỳ PRN231 — Ca 1 Sáng', '2026-10-15', 'scheduled')
ON CONFLICT (id) DO NOTHING;

-- 14. Seed Kíp thi phòng Lab (SHIFTS)
INSERT INTO real_exam_session_shifts (id, session_id, shift_name, room_lab, start_time, end_time, proctor_user_id, status) VALUES
    ('00000000-0000-0000-0000-000000000023', '00000000-0000-0000-0000-000000000022', 'Kíp 1 (08h00 - 09h30)', 'Lab 301', '2026-10-15 08:00:00+07', '2026-10-15 09:30:00+07', '00000000-0000-0000-0000-000000000004', 'scheduled')
ON CONFLICT (id) DO NOTHING;

-- 15. Seed Vé thi thí sinh tại máy trạm phòng Lab (Gán STT = Số máy thi 1-40)
INSERT INTO student_exam_tickets (id, shift_id, student_id, exam_set_id, seat_number, ip_address, status, is_locked) VALUES
    ('00000000-0000-0000-0000-000000000024', '00000000-0000-0000-0000-000000000023', '00000000-0000-0000-0000-000000000005', '00000000-0000-0000-0000-000000000019', 1, '192.168.1.101', 'SCHEDULED', FALSE),
    ('00000000-0000-0000-0000-000000000025', '00000000-0000-0000-0000-000000000023', '00000000-0000-0000-0000-000000000006', '00000000-0000-0000-0000-000000000019', 2, '192.168.1.102', 'SCHEDULED', FALSE)
ON CONFLICT (id) DO NOTHING;

-- 16. Seed Đơn phúc khảo nội bộ mẫu (APPEAL REQUESTS - MF-04: Trưởng BM tiếp nhận và phân công Giảng viên chấm lại)
INSERT INTO appeal_requests (id, ticket_id, student_id, session_id, submission_id, reason, status, assigned_to, reviewed_by, decision, original_score, proposed_score, review_notes, created_at) VALUES
    ('00000000-0000-0000-0000-000000000026', '00000000-0000-0000-0000-000000000024', '00000000-0000-0000-0000-000000000005', '00000000-0000-0000-0000-000000000022', NULL, 'Em xin phúc khảo phần phát biểu về nguyên lý Dependency Inversion bị ồn mic', 'PENDING', '00000000-0000-0000-0000-000000000003', NULL, NULL, 7.50, NULL, NULL, CURRENT_TIMESTAMP)
ON CONFLICT (id) DO NOTHING;

-- 17. Seed Cấu hình Hệ thống (SYSTEM CONFIGS)
INSERT INTO system_configs (id, key, value, description, updated_at) VALUES
    ('00000000-0000-0000-0000-000000000027', 'MaxPracticeQuestionsPerSession', '10', 'Số lượng câu hỏi luyện tập tối đa trong một phiên', CURRENT_TIMESTAMP),
    ('00000000-0000-0000-0000-000000000028', 'MinMixedPracticeQuestions', '3', 'Số lượng câu hỏi luyện tập tối thiểu cho chế độ Dễ đến Khó', CURRENT_TIMESTAMP),
    ('00000000-0000-0000-0000-000000000029', 'MaxMixedPracticeQuestions', '10', 'Số lượng câu hỏi luyện tập tối đa cho chế độ Dễ đến Khó', CURRENT_TIMESTAMP),
    ('00000000-0000-0000-0000-000000000030', 'TranscriptBufferSeconds', '60', 'Thời gian đệm hiệu đính transcript mặc định (giây)', CURRENT_TIMESTAMP),
    ('00000000-0000-0000-0000-000000000031', 'MaxPracticeFollowUpQuestions', '2', 'Số lượng câu hỏi follow-up luyện tập tối đa (1-5 câu)', CURRENT_TIMESTAMP),
    ('00000000-0000-0000-0000-000000000032', 'SessionInactivityTimeoutMinutes', '10', 'Thời gian timeout không tương tác của phiên luyện tập (phút)', CURRENT_TIMESTAMP)
ON CONFLICT (key) DO NOTHING;


