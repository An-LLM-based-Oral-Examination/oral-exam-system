# MA TRẬN TRUY VẾT 38 BẢNG CSDL CHUẨN 3NF & BÁO CÁO KIỂM TRA CHÉO DD-01..DD-06

> **Tài liệu:** Report 4 — Database Traceability Matrix (38 Normalized 3NF Tables) & Peer Cross-Review Report  
> **Dự án:** Hệ Thống Luyện Thi & Đánh Giá Vấn Đáp Bằng LLM Cho Ngành Kỹ Thuật Phần Mềm (`FA26SE166`)  
> **Người lập & Kiểm tra chéo (DB Specialist & Cross-Reviewer):** **Phạm Nguyễn Đăng Hải**  
> **Đối tượng được kiểm tra chéo:** Gói `DD-01` .. `DD-06`, Package Diagrams `D03`, `D05` & Kiosk Sequences `D09`, `D10` của **Nguyễn Quang Thành** (Team Leader & Lead Backend Architect)  
> **Mã nguồn & Kiến trúc đối chiếu:** [`MASTER_ARCHITECTURE.md`](file:///d:/Đồ%20Án/05_Source_Code/docs/01_architecture/MASTER_ARCHITECTURE.md), [`OralExamDbContext.cs`](file:///d:/Đồ%20Án/05_Source_Code/backend/src/Infrastructure/Persistence/OralExamDbContext.cs), [`OneWayLockInterceptor.cs`](file:///d:/Đồ%20Án/05_Source_Code/backend/src/Infrastructure/Persistence/Interceptors/OneWayLockInterceptor.cs)

---

## PHẦN 1 — MA TRẬN TRUY VẾT TOÀN DIỆN 38 BẢNG CSDL CHUẨN 3NF TRONG 7 BOUNDED CONTEXTS

Toàn bộ **38 bảng vật lý cốt lõi** trong PostgreSQL 16 (đánh số độc lập từ **01** đến **38**, mỗi dòng tương ứng đúng 1 bảng vật lý theo [`MASTER_ARCHITECTURE.md`](file:///d:/Đồ%20Án/05_Source_Code/docs/01_architecture/MASTER_ARCHITECTURE.md#L312) và [`OralExamDbContext.cs`](file:///d:/Đồ%20Án/05_Source_Code/backend/src/Infrastructure/Persistence/OralExamDbContext.cs#L19-L60)) tuân thủ nghiêm ngặt chuẩn 3NF, không nhồi cột (`Zero Column-Stuffing`) và được truy vết trực tiếp tới các mô-đun thiết kế chi tiết (`DD-01` .. `DD-24`):

### 1.1 Bounded Context 1 — Identity & Role-Based Access Control (2 Bảng)

| STT | Tên Bảng Vật Lý (`snake_case`) | Thực Thể Domain (`PascalCase`) | Khóa Chính & Ràng Buộc / Chỉ Mục Trọng Tâm | Truy Vết Thiết Kế (`DD`) & Luồng Nghiệp Vụ |
| :--- | :--- | :--- | :--- | :--- |
| **01** | `users` | `User` | `id (UUID PK)`, `uq_users_email`, `ck_users_role` (6 roles RBAC), `must_change_password` | `DD-01`, `DD-07`, `DD-08` (Dual Auth Google PKCE / Trường cấp & 6 Roles RBAC) |
| **02** | `refresh_tokens` | `RefreshToken` | `id (UUID PK)`, `fk_refresh_tokens_user_id`, `uq_refresh_tokens_token_hash`, `replaced_by_token_hash` | `DD-01` (Token Rotation & Reuse Detection Revocation) |

### 1.2 Bounded Context 2 — Academic Structure, Cohorts & Classes (6 Bảng — R5)

| STT | Tên Bảng Vật Lý (`snake_case`) | Thực Thể Domain (`PascalCase`) | Khóa Chính & Ràng Buộc / Chỉ Mục Trọng Tâm | Truy Vết Thiết Kế (`DD`) & Luồng Nghiệp Vụ |
| :--- | :--- | :--- | :--- | :--- |
| **03** | `semesters` | `Semester` | `id (UUID PK)`, `uq_semesters_code` (`FA26`, `SP26`), `start_date < end_date` | `DD-07` (Quản lý học kỳ bởi Admin, chỉ tạo/sửa, không xóa vật lý) |
| **04** | `courses` | `Course` | `id (UUID PK)`, `uq_courses_code`, `department_head_user_id`, `max_mock_exams_per_day` | `DD-07`, `DD-09` (Môn học, gán Trưởng BM & Quota thi thử theo ngày) |
| **05** | `course_clos` | `CourseClo` | `id (UUID PK)`, `uq_course_clos_course_code`, `bloom_level`, `weight_percentage` | `DD-12`, `DD-13`, `DD-14` (Danh mục Chuẩn đầu ra môn học CLO chuẩn 3NF) |
| **06** | `course_lecturers` | `CourseLecturer` | `id (UUID PK)`, `uq_course_lecturers_course_user` (`course_id`, `lecturer_user_id`) | `DD-08` (Trưởng BM phân công nhiều Giảng viên vào quản lý môn học) |
| **07** | `classes` | `Class` | `id (UUID PK)`, `uq_classes_code`, `uq_classes_join_code`, `join_password_hash` | `DD-08`, `DD-17` (Lớp học phần do Giảng viên tạo & mật khẩu tham gia lớp) |
| **08** | `class_enrollments` | `ClassEnrollment` | `id (UUID PK)`, `uq_class_enrollments_class_student`, `status` | `DD-08` (Sinh viên tự tham gia lớp học phần bằng mật khẩu lớp) |

### 1.3 Bounded Context 3 — Rubric 10.0 & Criterion Assessment (2 Bảng — MF-03)

| STT | Tên Bảng Vật Lý (`snake_case`) | Thực Thể Domain (`PascalCase`) | Khóa Chính & Ràng Buộc / Chỉ Mục Trọng Tâm | Truy Vết Thiết Kế (`DD`) & Luồng Nghiệp Vụ |
| :--- | :--- | :--- | :--- | :--- |
| **09** | `rubrics` | `Rubric` | `id (UUID PK)`, `total_points = 10.0`, `course_id` | `DD-10`, `DD-12`, `DD-14` (Barem chấm điểm chuẩn $\sum \equiv 10.0$, lệch chặn HTTP 422) |
| **10** | `rubric_criteria` | `RubricCriterion` | `id (UUID PK)`, `rubric_id`, `max_score`, `order_index` | `DD-10`, `DD-12`, `DD-14` (Tiêu chí thành phần của Barem Rubric 10.0) |

### 1.4 Bounded Context 4 — Interactive Practice & AI Evaluation Module (5 Bảng — MF-01)

| STT | Tên Bảng Vật Lý (`snake_case`) | Thực Thể Domain (`PascalCase`) | Khóa Chính & Ràng Buộc / Chỉ Mục Trọng Tâm | Truy Vết Thiết Kế (`DD`) & Luồng Nghiệp Vụ |
| :--- | :--- | :--- | :--- | :--- |
| **11** | `practice_questions` | `PracticeQuestion` | `id (UUID PK)`, `course_id`, `clo_id`, `rubric_id`, `difficulty`, `review_status` | `DD-02`, `DD-03`, `DD-10`, `DD-14`, `DD-17` (Kho câu hỏi Luyện tập, Thi thử & Ca thi lớp) |
| **12** | `practice_sessions` | `PracticeSession` | `id (UUID PK)`, `student_user_id`, `mode`, `last_activity_at`, `status` | `DD-02`, `DD-03`, `DD-04`, `DD-06` (Phiên luyện tập tương tác & Timeout 5p HTTP 410) |
| **13** | `practice_answers` | `PracticeAnswer` | `id (UUID PK)`, `session_id`, `question_id`, `parent_answer_id`, `is_follow_up`, `transcript_text` | `DD-04`, `DD-05`, `DD-06` (Câu trả lời chính & phụ trong luyện tập, Zero-R2) |
| **14** | `ai_evaluations` | `AiEvaluation` | `id (UUID PK)`, `ck_ai_evaluations_xor` (chỉ thuộc 1 trong 3: Practice, Mock hoặc Official Exam) | `DD-05`, `DD-06`, `DD-12`, `DD-22` (Kết quả chấm điểm CoT của Gemini 1.5 Flash/Pro) |
| **15** | `ai_evaluation_details` | `AiEvaluationDetail` | `id (UUID PK)`, `evaluation_id`, `rubric_criterion_id`, `awarded_score`, `feedback` | `DD-05`, `DD-06`, `DD-12`, `DD-22` (Điểm thành phần theo từng tiêu chí Rubric 10.0) |

### 1.5 Bounded Context 5 — Timed Mock Exam & Structure Matrix Module (6 Bảng — MF-02)

| STT | Tên Bảng Vật Lý (`snake_case`) | Thực Thể Domain (`PascalCase`) | Khóa Chính & Ràng Buộc / Chỉ Mục Trọng Tâm | Truy Vết Thiết Kế (`DD`) & Luồng Nghiệp Vụ |
| :--- | :--- | :--- | :--- | :--- |
| **16** | `exam_structures` | `ExamStructure` | `id (UUID PK)`, `course_id`, `slot_definitions (jsonb)`, `duration_minutes` | `DD-09`, `DD-10`, `DD-16` (Ma trận cấu trúc đề thi & tỷ lệ Bloom của Trưởng BM) |
| **17** | `exam_sets` | `ExamSet` | `id (UUID PK)`, `course_id`, `exam_structure_id`, `status` (`OFFICIAL` $\to$ `HANDED_OVER`) | `DD-16`, `DD-19` (Bộ đề thi chính thức được duyệt & bàn giao cho Khảo thí) |
| **18** | `exam_set_questions` | `ExamSetQuestion` | `id (UUID PK)`, `uq_exam_set_questions_set_order` (`exam_set_id`, `order_index`) | `DD-16`, `DD-19` (Ánh xạ thứ tự câu hỏi vào bộ đề thi) |
| **19** | `mock_exam_quotas` | `MockExamQuota` | `id (UUID PK)`, `uq_mock_exam_quotas_student_course_date` (`student_user_id`, `course_id`, `exam_date`) | `DD-09`, `DD-10` (Theo dõi hạn ngạch thi thử theo ngày của sinh viên, HTTP 429) |
| **20** | `mock_exam_sessions` | `MockExamSession` | `id (UUID PK)`, `student_user_id`, `course_id`, `duration_seconds`, `has_follow_up`, `status` | `DD-09`, `DD-10`, `DD-11`, `DD-12` (Phiên thi thử tính giờ & Server Master Timer MF-02) |
| **21** | `mock_exam_answers` | `MockExamAnswer` | `id (UUID PK)`, `session_id`, `question_id`, `parent_answer_id`, `is_follow_up`, `transcript_text` | `DD-11`, `DD-12` (Câu trả lời thi thử kèm Follow-up ngữ cảnh, Zero-R2) |

### 1.6 Bounded Context 6 — Official Lab Exam, Anti-Cheat, Audit & Appeals (9 Bảng — MF-04 / R7 / R8)

| STT | Tên Bảng Vật Lý (`snake_case`) | Thực Thể Domain (`PascalCase`) | Khóa Chính & Ràng Buộc / Chỉ Mục Trọng Tâm | Truy Vết Thiết Kế (`DD`) & Luồng Nghiệp Vụ |
| :--- | :--- | :--- | :--- | :--- |
| **22** | `exam_questions` | `ExamQuestion` | `id (UUID PK)`, `course_id`, `clo_id`, `rubric_id`, `image_url`, `image_description` | `DD-14`, `DD-15`, `DD-19` (Kho câu hỏi Thi thật cô lập 100%, hỗ trợ Multimodal) |
| **23** | `official_exam_sessions` | `OfficialExamSession` | `id (UUID PK)`, `course_id`, `semester_id`, `exam_input_mode`, `transcript_buffer_seconds` | `DD-19` (Kỳ thi vấn đáp chính thức do Cán bộ Khảo thí khởi tạo) |
| **24** | `real_exam_session_shifts` | `RealExamSessionShift` | `id (UUID PK)`, `proctor_user_id`, `examiner_user_id`, `is_exam_published`, `is_locked` (`ILockableEntity`) | `DD-19`, `DD-20`, `DD-22` (Ca thi phòng Lab, xuất bản đề & Công bố điểm One-Way Lock) |
| **25** | `student_exam_tickets` | `StudentExamTicket` | `id (UUID PK)`, `uq_tickets_shift_exam_set`, `seat_number (1..40)`, `ip_address`, `is_locked` | `DD-19`, `DD-20`, `DD-21`, `DD-22` (Vé dự thi, niêm phong IP & STT máy trạm 1–40) |
| **26** | `exam_question_submissions` | `ExamQuestionSubmission` | `id (UUID PK)`, `ticket_id`, `audio_r2_key (STT_MSSV.webm)`, `audio_sha256`, `is_locked` | `DD-21`, `DD-22` (Bài nộp Kiosk $< 100\text{ms}$ kèm niêm phong âm thanh R2 SHA-256) |
| **27** | `exam_anti_cheat_logs` | `ExamAntiCheatLog` | `id (UUID PK)`, `ticket_id`, `reported_by_user_id`, `violation_type`, `severity` | `DD-20` (Biên bản vi phạm an ninh phòng thi từ Kiosk & Giám thị `proctor`) |
| **28** | `lecturer_audits` | `LecturerAudit` | `id (UUID PK)`, `submission_id`, `original_score`, `adjusted_score`, `override_reason`, `is_locked` | `DD-22` (Giám khảo hậu kiểm Evidence Panel & sửa điểm có lý do giải trình) |
| **29** | `lecturer_audit_details` | `LecturerAuditDetail` | `id (UUID PK)`, `lecturer_audit_id`, `rubric_criterion_id`, `adjusted_criterion_score`, `comment` | `DD-22` (Chi tiết điều chỉnh điểm từng tiêu chí Rubric khi hậu kiểm phòng Lab) |
| **30** | `appeal_requests` | `AppealRequest` | `id (UUID PK)`, `ticket_id`, `assigned_lecturer_id`, `status` (5 bước phúc khảo) | `DD-23` (Quy trình phúc khảo 5 bước độc lập định mức $\le 24$ bài/GV, mở ngoại lệ Lock) |

### 1.7 Bounded Context 7 — In-Class Formative Exam Shifts (4 Bảng — MF-05 / R6) & Hạ Tầng Chịu Lỗi / Cấu Hình (4 Bảng)

| STT | Tên Bảng Vật Lý (`snake_case`) | Thực Thể Domain (`PascalCase`) | Khóa Chính & Ràng Buộc / Chỉ Mục Trọng Tâm | Truy Vết Thiết Kế (`DD`) & Luồng Nghiệp Vụ |
| :--- | :--- | :--- | :--- | :--- |
| **31** | `in_class_exam_shifts` | `InClassExamShift` | `id (UUID PK)`, `class_id`, `question_pool_mode`, `has_follow_up`, `is_locked` (`ILockableEntity`) | `DD-17`, `DD-18` (Ca thi trong lớp, Anti-Collision & Chốt sổ One-Way Lock) |
| **32** | `in_class_shift_passwords` | `InClassShiftPassword` | `id (UUID PK)`, `shift_id`, `password_code (6 chars)`, `expires_at`, `grace_until` | `DD-17` (Mật khẩu OTP xoay vòng tự động 5 phút kèm khoảng gia hạn Grace Period) |
| **33** | `in_class_shift_participants` | `InClassShiftParticipant` | `id (UUID PK)`, `uq_in_class_participants_shift_student`, `assigned_question_ids (jsonb)` | `DD-17`, `DD-18` (Sinh viên vào ca thi nhận bộ $N$ câu hỏi tự bốc chống trùng) |
| **34** | `in_class_exam_submissions` | `InClassExamSubmission` | `id (UUID PK)`, `participant_id`, `question_id`, `transcript_text`, `ai_score`, `final_score` | `DD-18` (Nộp bài $< 100\text{ms}$, chấm song song x12 & Publish điểm Real-time) |
| **35** | `dead_letter_queues` | `DeadLetterQueue` | `id (UUID PK)`, `flow_type`, `payload_json`, `retry_count`, `next_retry_at` (+5 phút) | `DD-05`, `DD-12`, `DD-24` (Hàng đợi cách ly lỗi AI: Admin MF-01/02, Khảo thí MF-04) |
| **36** | `grading_jobs` | `GradingJob` | `id (UUID PK)`, `reference_id`, `job_type`, `status`, `attempt_count`, `completed_at` | `DD-05`, `DD-12`, `DD-18`, `DD-22` (Theo dõi trạng thái tác vụ chấm ngầm BoundedChannel) |
| **37** | `audit_logs` | `AuditLog` | `id (UUID PK)`, `actor_user_id`, `action`, `entity_type`, `idx_audit_logs_created_at` | `DD-01`, `DD-07`, `DD-22`, `DD-24` (Nhật ký kiểm toán bảo mật bất biến toàn hệ thống) |
| **38** | `system_configs` | `SystemConfig` | `key (VARCHAR PK)`, `value`, `description`, `updated_by_user_id`, `updated_at` | `DD-02`, `DD-03`, `DD-04`, `DD-06` (Cấu hình động MF-01 do Admin quản lý) |

*(Ghi chú kỹ thuật: Bên cạnh 38 bảng cốt lõi trong ma trận kiến trúc trên, [`OralExamDbContext.cs`](file:///d:/Đồ%20Án/05_Source_Code/backend/src/Infrastructure/Persistence/OralExamDbContext.cs#L22-L59) còn cung cấp các tập thực thể mở rộng phục vụ quản lý khung chương trình và thông báo nội bộ 3NF gồm `AcademicPrograms` (`academic_programs`), `CourseOfferings` (`course_offerings`) và `Notifications` (`notifications`), tất cả đều đồng bộ 100% chỉ mục phức hợp ESR và ràng buộc khóa ngoại).*

---

## PHẦN 2 — BÁO CÁO KIỂM TRA CHÉO (CROSS-REVIEW REPORT) CHO GÓI `DD-01` .. `DD-06` CỦA NGUYỄN QUANG THÀNH

- **Người kiểm tra chéo (Reviewer):** **Phạm Nguyễn Đăng Hải** (DB Specialist & Frontend Developer)
- **Tác giả được kiểm tra (Author):** **Nguyễn Quang Thành** (Team Leader & Lead Backend Architect)
- **Thư mục kiểm tra:** [`05_Source_Code/docs/Task Daily/Report4_NguyenQuangThanh/`](file:///d:/Đồ%20Án/05_Source_Code/docs/Task%20Daily/Report4_NguyenQuangThanh/)

### 2.1 Bảng Kết Quả Kiểm Tra Chéo Từng Hạng Mục (Khớp Chính Xác 100% Tên Tệp Thực Tế)

| Hạng Mục Kiểm Tra | Tệp Sơ Đồ & Đặc Tả Thực Tế Trong `Report4_NguyenQuangThanh/` | Tiêu Chí Đối Chiếu (UML 2.5, Plain English & 3NF DB) | Kết Quả | Nhận Xét Kỹ Thuật Của Reviewer (Phạm Nguyễn Đăng Hải) |
| :--- | :--- | :--- | :--- | :--- |
| **1. Package Diagrams (`1.2.1` & `1.2.3`)** | `D03_Backend_Package_Diagram` (`.drawio`/`.png`), `D05_Desktop_Kiosk_Package_Diagram` (`.drawio`/`.png`), `Section_1.2.1_1.2.3_Package_Diagrams_VI.md` | Đường phụ thuộc nét đứt (`dashed=1`), không cắt qua cạnh hộp khác, tiêu đề chuẩn không chứa hậu tố thừa. | **PASS (100%)** | Đã tách riêng các hành lang đường nét đứt từ `Infrastructure` lên `Application.Interfaces` và từ `LocalJournal` lên `Domain.Interfaces`. |
| **2. `DD-01` (Sign In, Dual Auth, Refresh Token & RBAC)** | `DD01_Auth_Class_Diagram` (`.drawio`/`.png`), `DD01_Auth_Sequence_Diagram` (`.drawio`/`.png`), `DD01_Auth_Specification_VI.md` | Không trùng số thứ tự giữa nhánh Google OAuth (`.1`/`.2`) và Password Login (`.3`/`.4`), khung cuối là `opt [Exception occurs]`. | **PASS (100%)** | Khớp 100% với bảng `users`, `refresh_tokens`, cờ `must_change_password` và logic rotation token trong `AuthCommandHandler`. |
| **3. `DD-02` (Practice Config & Session Initialization)** | `DD02_PracticeInit_Class_Diagram` (`.drawio`/`.png`), `DD02_PracticeInit_Sequence_Diagram` (`.drawio`/`.png`), `DD02_PracticeInit_Specification_VI.md` | Mũi tên `Call` nét liền + tam giác đặc, `Return` nét đứt + mũi tên mở; mọi truy vấn `Database` đều có Activation Bar và `Return`. | **PASS (100%)** | Phản ánh chính xác cấu hình `system_configs` của Admin và khởi tạo phiên trong `practice_sessions`, `practice_questions`. |
| **4. `DD-03` (Practice Question Selection: On-Demand & Progressive)** | `DD03_QuestionSelection_Class_Diagram` (`.drawio`/`.png`), `DD03_QuestionSelection_Sequence_Diagram` (`.drawio`/`.png`), `DD03_QuestionSelection_Specification_VI.md` | Thuật toán chống 3 câu liên tiếp cùng độ khó (`Anti-3-Consecutive`) và bốc câu hỏi theo tiến trình (`Progressive`). | **PASS (100%)** | Khớp hoàn toàn với lược đồ bảng `practice_sessions`, `practice_questions`, `practice_answers` và `system_configs`. |
| **5. `DD-04` (Practice Voice STT & Transcript Buffer)** | `DD04_VoiceSttBuffer_Class_Diagram` (`.drawio`/`.png`), `DD04_VoiceSttBuffer_Sequence_Diagram` (`.drawio`/`.png`), `DD04_VoiceSttBuffer_Specification_VI.md` | Mọi lời gọi hàm từ `Handler` sang `Helper` đều là nét liền + tam giác đặc (`Call`), phản hồi nét đứt (`Return`). | **PASS (100%)** | Bảo toàn nguyên tắc `Zero-R2` cho luồng luyện tập (chỉ chuẩn hóa thuật ngữ SE và lưu văn bản vào `practice_answers.transcript_text`). |
| **6. `DD-05` (Practice Submission, Background AI & SignalR)** | `DD05_PracticeGrading_Class_Diagram` (`.drawio`/`.png`), `DD05_PracticeGrading_Sequence_Diagram` (`.drawio`/`.png`), `DD05_PracticeGrading_Specification_VI.md` | Đánh số thứ tự không trùng lặp giữa các nhánh nộp bài Persist-First, chấm ngầm BoundedChannel, phát SignalR và cách ly DLQ. | **PASS (100%)** | Tuân thủ đúng ràng buộc `ck_ai_evaluations_xor` (`practice_answer_id IS NOT NULL`), bảng `ai_evaluation_details` và `dead_letter_queues`. |
| **7. `DD-06` (Practice Follow-Up, Scorecard & Inactivity Timeout)** | `DD06_FollowUpTimeout_Class_Diagram` (`.drawio`/`.png`), `DD06_FollowUpTimeout_Sequence_Diagram` (`.drawio`/`.png`), `DD06_FollowUpTimeout_Specification_VI.md` | 100% nhãn thông điệp dùng Plain English tự nhiên, xử lý Follow-up trong ngưỡng điểm $4.0 - 8.0$ và hết hạn 5 phút không tương tác (`HTTP 410 Gone`). | **PASS (100%)** | Đồng bộ 100% giữa sơ đồ `.drawio`, ảnh `.png` (300 DPI) và khối Mermaid trong tài liệu đặc tả `.md`. |
| **8. Desktop Kiosk Sequence Diagrams (`D09` & `D10`)** | `D09_Kiosk_Startup_PreCheck_CheckIn_Sequence` (`.drawio`/`.png`), `D10_Kiosk_Exam_Execution_Persist_First_Sequence` (`.drawio`/`.png`), `D09_D10_Desktop_Kiosk_Sequences_VI.md` | Tiêu đề và tên Lifeline sạch hậu tố `[Kiosk]`/`[BE]`, mũi tên Call/Return chuẩn UML 2.5, đánh số tuần tự phẳng không trùng lặp. | **PASS (100%)** | Phản ánh chính xác quy trình kiểm tra phần cứng mic 30s ($\ge 60\text{dB}$), điểm danh STT 1–40 và nộp bài Persist-First niêm phong `STT_MSSV.webm` + SHA-256. |

### 2.2 Kết Luận Nghiệm Thu Chéo
- **Phán quyết của Reviewer (Phạm Nguyễn Đăng Hải):** **ĐẠT CHUẨN 100% (APPROVED FOR FINAL REPORT 4 SUBMISSION)**.
