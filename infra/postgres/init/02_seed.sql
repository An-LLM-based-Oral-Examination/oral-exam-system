-- Minimal seed for local/dev (roles + sample academic skeleton)

INSERT INTO roles (role_id, role_name) VALUES
    (1, 'Student'),
    (2, 'Instructor'),
    (3, 'Admin');

SELECT setval(pg_get_serial_sequence('roles', 'role_id'), (SELECT MAX(role_id) FROM roles));

-- Dev users (password_hash placeholder — replace with OAuth / bcrypt in app)
INSERT INTO users (user_id, role_id, username, email, password_hash, full_name) VALUES
    ('11111111-1111-1111-1111-111111111111', 3, 'admin', 'admin@fpt.edu.vn', NULL, 'System Admin'),
    ('22222222-2222-2222-2222-222222222222', 2, 'instructor1', 'instructor1@fpt.edu.vn', NULL, 'Nguyen Van Giang Vien'),
    ('33333333-3333-3333-3333-333333333333', 1, 'student1', 'student1@fpt.edu.vn', NULL, 'Tran Van Sinh Vien');

INSERT INTO semesters (semester_code, start_date, end_date) VALUES
    ('FA26', '2026-09-01', '2026-12-31');

INSERT INTO courses (course_code, course_name) VALUES
    ('PRN231', 'Advanced Cross-Platform Application Programming with .NET'),
    ('SWP391', 'Software development project');

INSERT INTO classes (course_id, semester_id, instructor_id, class_name)
SELECT c.course_id, s.semester_id, '22222222-2222-2222-2222-222222222222'::uuid, 'SE1801'
FROM courses c
CROSS JOIN semesters s
WHERE c.course_code = 'PRN231' AND s.semester_code = 'FA26';

INSERT INTO class_enrollments (class_id, student_id, student_code, status)
SELECT cl.class_id, '33333333-3333-3333-3333-333333333333'::uuid, 'SE180001', 'ACTIVE'
FROM classes cl
WHERE cl.class_name = 'SE1801';
