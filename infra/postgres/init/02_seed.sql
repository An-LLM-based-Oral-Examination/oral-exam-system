-- =============================================================================
-- DỮ LIỆU SEED MẪU CHO MÔI TRƯỜNG DEV/TEST (ORAL EXAM SYSTEM)
-- ĐỀ TÀI CAPSTONE FA26SE166 — ĐẠI HỌC FPT TP.HCM (FPT SG)
-- Phủ đầy đủ 6 Bounded Contexts, Barem Rubric 10.0đ và tách riêng Practice - Exam
-- Khớp 100% với 27 C# Domain Entities, OralExamDbContext và 01_schema.sql
-- =============================================================================

-- 1. Seed Người dùng hệ thống (Admin, Giảng viên, Giám thị, Sinh viên)
INSERT INTO users (id, email, password_hash, full_name, student_code, role, is_active) VALUES
    ('11111111-1111-1111-1111-111111111111', 'admin@fpt.edu.vn', NULL, 'Nguyen Quang Thanh (Admin)', NULL, 'admin', TRUE),
    ('66666666-6666-6666-6666-666666666666', 'headse@fe.edu.vn', NULL, 'Tran Van Truong (Truong Bo Mon SE)', NULL, 'department_head', TRUE),
    ('22222222-2222-2222-2222-222222222222', 'totnt@fe.edu.vn', NULL, 'Nguyen Trong Tot (Giang Vien)', NULL, 'lecturer', TRUE),
    ('33333333-3333-3333-3333-333333333333', 'haind@fe.edu.vn', NULL, 'Nguyen Dang Hai (Giam Thi)', NULL, 'proctor', TRUE),
    ('44444444-4444-4444-4444-444444444444', 'hoanglvse170001@fpt.edu.vn', NULL, 'Le Vu Hoang (Sinh Vien 1)', 'SE170001', 'student', TRUE),
    ('55555555-5555-5555-5555-555555555555', 'maittse170002@fpt.edu.vn', NULL, 'Tran Thi Mai (Sinh Vien 2)', 'SE170002', 'student', TRUE)
ON CONFLICT (id) DO NOTHING;

-- 2. Seed Học kỳ (Semesters)
INSERT INTO semesters (id, code, name, start_date, end_date, is_active) VALUES
    ('a1111111-aaaa-aaaa-aaaa-aaaaaaaaaaaa', 'FA26', 'Fall 2026', '2026-09-01', '2026-12-31', TRUE)
ON CONFLICT (id) DO NOTHING;

-- 3. Seed Môn học (Courses)
INSERT INTO courses (id, code, name, credits, semester_id, has_follow_up, transcript_buffer_seconds, max_follow_up_questions, is_active) VALUES
    ('c1111111-cccc-cccc-cccc-cccccccccccc', 'PRN231', 'Building Cross-Platform Back-End Applications with .NET', 3, 'a1111111-aaaa-aaaa-aaaa-aaaaaaaaaaaa', TRUE, 60, 1, TRUE),
    ('c2222222-cccc-cccc-cccc-cccccccccccc', 'SWD392', 'Software Architecture and Design', 3, 'a1111111-aaaa-aaaa-aaaa-aaaaaaaaaaaa', TRUE, 60, 1, TRUE)
ON CONFLICT (id) DO NOTHING;

-- 4. Seed Lớp học (Classes)
INSERT INTO classes (id, code, course_id, semester_id, lecturer_id, max_students, is_active) VALUES
    ('b1111111-bbbb-bbbb-bbbb-bbbbbbbbbbbb', 'SE1801-NET', 'c1111111-cccc-cccc-cccc-cccccccccccc', 'a1111111-aaaa-aaaa-aaaa-aaaaaaaaaaaa', '22222222-2222-2222-2222-222222222222', 35, TRUE)
ON CONFLICT (id) DO NOTHING;

-- 5. Seed Danh sách sinh viên thuộc lớp (Class Enrollments)
INSERT INTO class_enrollments (id, class_id, student_id, status) VALUES
    ('e1111111-eeee-eeee-eeee-eeeeeeeeeeee', 'b1111111-bbbb-bbbb-bbbb-bbbbbbbbbbbb', '44444444-4444-4444-4444-444444444444', 'enrolled'),
    ('e2222222-eeee-eeee-eeee-eeeeeeeeeeee', 'b1111111-bbbb-bbbb-bbbb-bbbbbbbbbbbb', '55555555-5555-5555-5555-555555555555', 'enrolled')
ON CONFLICT (id) DO NOTHING;

-- 6. Seed Barem Rubric tổng quan (Ràng buộc bắt buộc: Tổng điểm = 10.0đ)
INSERT INTO rubrics (id, course_id, name, description, total_max_score, is_active) VALUES
    ('d1111111-1111-1111-1111-111111111111', 'c1111111-cccc-cccc-cccc-cccccccccccc', 'Barem Đánh Giá Kiến Trúc .NET 8 Clean Architecture', 'Đánh giá hiểu biết về Clean Architecture 4 tầng, CQRS MediatR và Resilience', 10.00, TRUE)
ON CONFLICT (id) DO NOTHING;

-- 7. Seed Tiêu chí Rubric thành phần (Sum = 3.0 + 4.0 + 3.0 = 10.0đ)
INSERT INTO rubric_criteria (id, rubric_id, criterion_name, description, max_score, weight, bloom_level, order_index) VALUES
    ('dc111111-1111-1111-1111-111111111111', 'd1111111-1111-1111-1111-111111111111', 'Khái niệm & Ranh giới 4 tầng Clean Architecture', 'Hiểu rõ vai trò Domain, Application, Infrastructure, API. Domain không phụ thuộc tầng nào.', 3.00, 0.30, 'Understand', 1),
    ('dc222222-2222-2222-2222-222222222222', 'd1111111-1111-1111-1111-111111111111', 'Nguyên lý Dependency Inversion & CQRS MediatR', 'Áp dụng tách biệt Command và Query, sử dụng Interface và Dependency Injection Container.', 4.00, 0.40, 'Apply', 2),
    ('dc333333-3333-3333-3333-333333333333', 'd1111111-1111-1111-1111-111111111111', 'Xử lý Ngoại lệ & Kiến trúc chịu lỗi (Resilience)', 'Trình bày cơ chế Global Exception RFC 7807, Bounded Channel và xử lý retry Polly.', 3.00, 0.30, 'Analyze', 3)
ON CONFLICT (id) DO NOTHING;

-- 8. Seed Câu hỏi Luyện tập tự do (PRACTICE QUESTIONS - MF-01)
INSERT INTO practice_questions (id, course_id, rubric_id, title, content, sample_answer, key_points, difficulty, bloom_level, has_follow_up, follow_up_prompt, is_active) VALUES
    ('da111111-1111-1111-1111-111111111111', 'c1111111-cccc-cccc-cccc-cccccccccccc', 'd1111111-1111-1111-1111-111111111111',
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
    ('ea111111-1111-1111-1111-111111111111', 'c1111111-cccc-cccc-cccc-cccccccccccc', '22222222-2222-2222-2222-222222222222',
     'Cấu trúc Đề thi Vấn đáp Cuối kỳ PRN231 (FA26)', 5, 30,
     '{"Understand": 2, "Apply": 2, "Analyze": 1}'::jsonb,
     TRUE)
ON CONFLICT (id) DO NOTHING;

-- 10. Seed Bộ đề thi (EXAM SETS)
INSERT INTO exam_sets (id, structure_id, course_id, set_code, is_active) VALUES
    ('eb111111-1111-1111-1111-111111111111', 'ea111111-1111-1111-1111-111111111111', 'c1111111-cccc-cccc-cccc-cccccccccccc', 'PRN231-SET01', TRUE)
ON CONFLICT (id) DO NOTHING;

-- 11. Seed Câu hỏi Thi cử chính thức (EXAM QUESTIONS - MF-02 & MF-04)
INSERT INTO exam_questions (id, course_id, rubric_id, title, content, sample_answer, key_points, difficulty, bloom_level, approved_by, is_active) VALUES
    ('ec111111-1111-1111-1111-111111111111', 'c1111111-cccc-cccc-cccc-cccccccccccc', 'd1111111-1111-1111-1111-111111111111',
     'Phân biệt BackgroundService và IHostedService trong .NET 8',
     'Phân biệt sự khác nhau giữa BackgroundService và IHostedService trong .NET 8 khi xây dựng hàng đợi xử lý chấm điểm ngầm?',
     'IHostedService là interface nền tảng định nghĩa StartAsync và StopAsync. BackgroundService là một abstract class kế thừa IHostedService và triển khai sẵn ExecuteAsync chạy nền vòng lặp an toàn.',
     '["IHostedService là interface cốt lõi", "BackgroundService là abstract class kế thừa IHostedService", "ExecuteAsync chạy Task bất đồng bộ", "Dùng BoundedChannelReader rút task an toàn"]'::jsonb,
     'hard', 'Analyze', '66666666-6666-6666-6666-666666666666', TRUE)
ON CONFLICT (id) DO NOTHING;

-- 12. Seed Liên kết câu hỏi trong bộ đề (EXAM SET QUESTIONS)
INSERT INTO exam_set_questions (id, exam_set_id, exam_question_id, order_index) VALUES
    ('ed111111-1111-1111-1111-111111111111', 'eb111111-1111-1111-1111-111111111111', 'ec111111-1111-1111-1111-111111111111', 1)
ON CONFLICT (id) DO NOTHING;

-- 13. Seed Ca thi phòng Lab chính thức (OFFICIAL EXAM SESSIONS - MF-04)
INSERT INTO official_exam_sessions (id, course_id, exam_structure_id, title, exam_date, status) VALUES
    ('ee111111-1111-1111-1111-111111111111', 'c1111111-cccc-cccc-cccc-cccccccccccc', 'ea111111-1111-1111-1111-111111111111',
     'Vấn đáp Cuối kỳ PRN231 — Ca 1 Sáng', '2026-10-15', 'scheduled')
ON CONFLICT (id) DO NOTHING;

-- 14. Seed Kíp thi phòng Lab (SHIFTS)
INSERT INTO real_exam_session_shifts (id, session_id, shift_name, room_lab, start_time, end_time, proctor_user_id, status) VALUES
    ('ef111111-1111-1111-1111-111111111111', 'ee111111-1111-1111-1111-111111111111', 'Kíp 1 (08h00 - 09h30)', 'Lab 301', '2026-10-15 08:00:00+07', '2026-10-15 09:30:00+07', '33333333-3333-3333-3333-333333333333', 'scheduled')
ON CONFLICT (id) DO NOTHING;

-- 15. Seed Vé thi thí sinh tại máy trạm phòng Lab (Gán STT = Số máy thi 1-40)
INSERT INTO student_exam_tickets (id, shift_id, student_id, exam_set_id, seat_number, ip_address, status, is_locked) VALUES
    ('fa000001-1111-1111-1111-111111111111', 'ef111111-1111-1111-1111-111111111111', '44444444-4444-4444-4444-444444444444', 'eb111111-1111-1111-1111-111111111111', 1, '192.168.1.101', 'SCHEDULED', FALSE),
    ('fa000002-2222-2222-2222-222222222222', 'ef111111-1111-1111-1111-111111111111', '55555555-5555-5555-5555-555555555555', 'eb111111-1111-1111-1111-111111111111', 2, '192.168.1.102', 'SCHEDULED', FALSE)
ON CONFLICT (id) DO NOTHING;

-- 16. Seed Đơn phúc khảo nội bộ mẫu (APPEAL REQUESTS - MF-04)
INSERT INTO appeal_requests (id, ticket_id, student_id, session_id, submission_id, reason, status, assigned_to, reviewed_by, decision, original_score, proposed_score, review_notes, created_at) VALUES
    ('fb000001-1111-1111-1111-111111111111', 'fa000001-1111-1111-1111-111111111111', '44444444-4444-4444-4444-444444444444', 'ee111111-1111-1111-1111-111111111111', NULL, 'Em xin phúc khảo phần phát biểu về nguyên lý Dependency Inversion bị ồn mic', 'PENDING', '66666666-6666-6666-6666-666666666666', NULL, NULL, 7.50, NULL, NULL, CURRENT_TIMESTAMP)
ON CONFLICT (id) DO NOTHING;


