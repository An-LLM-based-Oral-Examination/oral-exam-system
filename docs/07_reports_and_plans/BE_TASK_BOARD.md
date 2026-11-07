# ⚙️ BACKEND MASTER TASK BOARD & LIVE EXECUTION STATUS (v9.0)
## ĐỒ ÁN TỐT NGHIỆP SEP490 (FA26SE166) — HỆ THỐNG LUYỆN THI & ĐÁNH GIÁ VẤN ĐÁP BẰNG LLM
### KHO MÃ NGUỒN: `05_Source_Code/backend` | CỔNG DỊCH VỤ API: `5000` | DATABASE: `5432` | FRONTEND: `3000`

---

> [!IMPORTANT]
> **QUY CHUẨN TỐI THƯỢNG & BÁO CÁO HIỆN TRẠNG THỰC TẾ (SINGLE SOURCE OF TRUTH):**
> - **Mã đề tài:** FA26SE166 | **Học kỳ:** Fall 2026 (FA26) — Đại học FPT TP.HCM (FPT SG)
> - **Tech Stack Chuẩn:** .NET 8 Clean Architecture 4 tầng (`Domain`, `Application`, `Infrastructure`, `API`) · CQRS MediatR · PostgreSQL 16 (**Thiết kế 38 bảng 3NF trong 7 Bounded Contexts** — đã ánh xạ đầy đủ **38 bảng thực thể nghiệp vụ chuẩn 3NF** trong `OralExamDbContext.cs` và các bản Migration) · SignalR Typed Hubs (`/hubs/practice`, `/hubs/anti-cheat`) · `BoundedChannel` (1,000 slots RAM) · Polly Resilience Pipeline · Google Gemini 1.5 Flash/Pro · Cloudflare Whisper STT · Cloudflare R2 Storage.
> - **Phân định 6 Roles RBAC:** `admin`, `department_head`, `examination_officer`, `lecturer`, `proctor`, `student`.
> - **Đặc Tả Kỹ Thuật Backlog:** Tài liệu này cung cấp **Đặc tả Kỹ thuật Chi tiết 7 Task Backlog Nâng cấp (R1–R8)** ở trạng thái **`⏳ TODO / CẦN BE TRIỂN KHAI`** theo đúng phương pháp luận Spec-First / Design-First làm căn cứ vững chắc cho 2 kỹ sư Backend (**Nguyễn Quang Thành** & **Nguyễn Trọng Tốt**) triển khai lập trình.

---

# 📊 PHẦN 1: TỔNG QUAN TIẾN ĐỘ BACKEND HIỆN TẠI (EXECUTIVE SUMMARY)

| Phân hệ / Nhóm chức năng | Tổng số Module | Đã hoàn thành (`✅ DONE`) | Chưa làm / Cần chỉnh (`⏳ TODO / PARTIAL`) | Tỷ lệ hoàn thành | Ghi chú hiện trạng thực tế trong Codebase (`backend/src/`) |
|:---|:---:|:---:|:---:|:---:|:---|
| **1. Hạ tầng Core, Deploy, 3NF DB & 6 ESR Indexes** | 4 | 4 | 0 | **100%** | Đã khóa `UseNpgsql` (Retry + `SplitQuery`), CORS đa môi trường, JWT SignalR cho cả 2 Hubs, 34 `DbSet` chuẩn 3NF + 6 Composite Indexes chuẩn ESR. |
| **2. Luồng MF-01: Luyện tập tương tác (`BE-1.1` $\to$ `BE-1.8`)** | 8 | 8 | 0 | **100%** | Đã hoàn tất Per-Question On-Demand (Anti-3-Consecutive, Follow-up A2 $4.0 \le \text{Score} \le 8.0$), Full-Session Progressive (3–10 câu), Timeout 5m (`HTTP 410 Gone`), `BoundedChannel` + SignalR `/hubs/practice`. |
| **3. Luồng MF-02: Thi thử bấm giờ (`BE-2.1` $\to$ `BE-2.4`)** | 4 | 4 | 0 | **100%** | Đã hoàn tất Quota động (`max_mock_exams_per_day`, `HTTP 429`), bốc đề Bloom từ `practice_questions` theo cấu hình môn học, Master Timer + 10s Grace Period, Instant Scorecard & Resume. |
| **4. Luồng MF-03: Ngân hàng đề 2 kho, CLO 3NF & Rubric (`BE-3.1` $\to$ `BE-3.4`, R8)** | 4 | 4 | 0 | **100%** | Đã hoàn tất `course_clos` 3NF CRUD, Rubric $\sum \equiv 10.00$đ (`HTTP 422`), tách 2 luồng tạo đề (`practice_questions` gắn `clo_id` vs `exam_questions` gắn `clo_id` + `structure_id`), duyệt đề & phân trang SQL. |
| **5. Chuỗi Học thuật 4 Cấp (`BE-V06`, `BE-V12` — R5) & Các Module Vệ tinh (`BE-V01..V09`, `BE-V11`)** | 11 | 11 | 0 | **100%** | Đã hoàn tất Semester (`BE-V06`), `course_lecturers` & `ClassesController` mật khẩu lớp (`BE-V12`), DLQ phân tách `flow_type` Admin vs Khảo thí (`BE-V09` — R2), Dashboard SV/GV, Excel/PDF & QR Verification, gỡ auto-enrollment Google Login (`BE-V04`), Admin cấp tài khoản `POST /api/v1/admin/users` (`BE-V05`), và bảng `notifications` 3NF độc lập (`BE-V07`). |
| **6. Luồng MF-04: Thi thật Kiosk, Hậu kiểm, Giám thị & Phúc khảo (`BE-4.1` $\to$ `BE-4.6`, `BE-V10`)** | 7 | 7 | 0 | **100%** | Đã hoàn tất Kiosk Check-in IP (`BE-4.2`), Upload R2 + SHA-256 (`BE-4.3`), Evidence Panel (`BE-4.4`), Publish Grades + OneWayLock (`BE-4.5`), Tách riêng cấu hình follow-up cho TBM & gán cả Examiner + Proctor (`BE-4.1`), Quy trình phúc khảo R2 thuật toán phân công <= 24 bài/GV & Khảo thí duyệt OneWayLock (`BE-4.6`), Khóa quyền Proctor chỉ điểm danh & báo cáo vi phạm (`BE-V10`). |
| **7. Luồng MF-05: Ca thi trong lớp & TOTP 5 phút (`BE-V13` — R6)** | 1 | 1 | 0 | **100%** | Đã hoàn thành 4 thực thể chuẩn 3NF, Migration CSDL, TOTP 5 phút RFC 6238 kèm grace period 60s, `InClassShiftsController` đủ 10 REST endpoints, chấm ngầm BoundedChannel và One-Way Lock khi chốt điểm. |

---

# 📁 PHẦN 2: CẤU TRÚC KIẾN TRÚC & THƯ MỤC HIỆN HÀNH (`backend/src/`)

Hệ thống được thiết kế theo mô hình **.NET 8 Clean Architecture 4 tầng** chuẩn Enterprise:

```text
backend/src/
├── Domain/                          # Tầng Core Thuần Khiết (Zero External Dependencies)
│   ├── Common/                      # BaseEntity.cs, IAggregateRoot.cs, ILockableEntity.cs
│   ├── Entities/                    # 38 Entities Chuẩn 3NF (7 Bounded Contexts + Hạ tầng):
│   │   ├── Identity (2):            User.cs (6 Roles RBAC + Dual Auth), RefreshToken.cs
│   │   ├── Academic [R5] (6):       Semester.cs, Course.cs (department_head_user_id, mock_exam_*), CourseClo.cs, CourseLecturer.cs, Class.cs (join_code, join_password_hash), ClassEnrollment.cs
│   │   ├── Assessment (2):          Rubric.cs, RubricCriterion.cs (CHECK sum = 10.00)
│   │   ├── Practice [MF-01] (5):    PracticeQuestion.cs (clo_id), PracticeSession.cs (total_score, last_activity_at), PracticeAnswer.cs (order_index, follow_up_index, follow_up_prompt), AiEvaluation.cs (follow_up_prompt, ck_ai_evaluations_xor), AiEvaluationDetail.cs
│   │   ├── Mock Exam [MF-02] (6):   ExamStructure.cs, ExamSet.cs, ExamSetQuestion.cs, MockExamQuota.cs, MockExamSession.cs, MockExamAnswer.cs
│   │   ├── Official [MF-04] (9):    ExamQuestion.cs (clo_id, structure_id), OfficialExamSession.cs, RealExamSessionShift.cs (examiner_user_id, proctor_user_id), StudentExamTicket.cs, ExamQuestionSubmission.cs, ExamAntiCheatLog.cs, LecturerAudit.cs, LecturerAuditDetail.cs, AppealRequest.cs
│   │   ├── In-Class [MF-05/R6] (4): InClassExamShift.cs, InClassShiftPassword.cs, InClassShiftParticipant.cs, InClassExamSubmission.cs (Zero R2 Audio)
│   │   └── System, DLQ & Notif (5): DeadLetterQueue.cs (flow_type, managed_by_role), GradingJob.cs, AuditLog.cs, SystemConfig.cs, Notification.cs (3NF)
├── Application/                     # MediatR CQRS, FluentValidation, Interfaces
│   └── Features/                    # 16 Feature Modules hiện có:
│       ├── AcademicHierarchy/       # ✅ AssignDepartmentHead, AssignLecturersToCourse, CreateClass, ResetClassPassword, JoinClass, GetCourseLecturers, GetStudentMyClasses
│       ├── Admin/                   # ✅ Users, SystemConfigs, AuditLogs
│       ├── Appeals/                 # ✅ CreateAppeal, GetAppeals, GetAppealById, GetAppealAllocationProposal (R2 <= 24 bài/GV), BatchAssignAppeals, AssignAppealLecturer, RegradeAppeal, ReviewAppealDecision (Khảo thí phê duyệt One-Way Lock)
│       ├── Auth/                    # ✅ GoogleLogin, PasswordLogin, ChangePassword, RefreshToken, SignOut, GetCurrentUser
│       ├── Courses/                 # ✅ GetCourses, CreateCourse, UpdateCourse, Get/UpdateCourseConfiguration, CourseClo CRUD
│       ├── DeadLetterQueue/         # ✅ GetDlqTasks, ReplayDlqTask, ResolveDlqTask (Đã phân tách flow_type Admin MF-01/02 vs Khảo thí MF-04)
│       ├── Lecturer/                # ✅ GetLecturerDashboard
│       ├── MockExams/               # ✅ GetMockExamQuota, StartMockExam, ResumeMockExamSession, SubmitMockExamAnswer, SubmitMockExamSession, GetHistory, GetDetail
│       ├── Notifications/           # ✅ GetNotifications, MarkNotificationRead, MarkAllNotificationsRead
│       ├── OfficialExams/           # ✅ Sessions, Shifts (phân bổ Examiner + Proctor), ConfigureOfficialExamFollowUp (TBM độc quyền), Tickets, CheckIn, Audio R2, Submit, Evidence, OverrideScore, PublishGrades, ExportExcel/Pdf, AntiCheatLogs
│       ├── Practice/                # ✅ StartPracticeSession, NextQuestion, SubmitPracticeAnswer, SubmitPracticeBatch, CompletePracticeSession, GetPracticeSession, GetStudentPracticeHistory
│       ├── Proctor/                 # ✅ GetShiftAttendance, UpdateTicketAttendance (SeatNumber 1-40, không sửa IP/ca thi), RecordProctorViolation (POST /violations vào exam_anti_cheat_logs & AntiCheatHub)
│       ├── QuestionBank/            # ✅ GenerateQuestions, CreateDraftQuestion (Dual-Track R8), CreateRubric, BatchSubmitForReview, ReviewQuestionDecision, BatchApprove, GetQuestions, GetQuestionById
│       ├── Semesters/               # ✅ GetSemesters, CreateSemester, UpdateSemester, GetAcademicClasses
│       ├── Speech/                  # ✅ SynthesizeSpeech (TTS), TranscribeSpeech (Resilient STT Gemini + Whisper)
│       ├── Student/                 # ✅ GetStudentDashboard, GetStudentOfficialGrade, AcknowledgeOfficialGrade
│       └── InClassShifts/           # ✅ CreateInClassShift, EnterInClassShift, SubmitInClassShiftAnswer, AdjustInClassSubmissionScore, FinalizeInClassShiftGrades, GetInClassShiftCurrentPassword (TOTP 5m), GetInClassShiftSubmissions
├── Infrastructure/                  # EF Core 8 (OralExamDbContext.cs), Interceptors, Channels, Services, Migrations
└── API/                             # 23 Controllers trong Controllers/v1/ + 2 Hubs (PracticeHub, ExamAntiCheatHub) + 2 Workers
```

---

# ✅ PHẦN 3: DANH SÁCH CÁC PHÂN HỆ BACKEND ĐÃ HOÀN THÀNH 100% (`DONE`)

### 3.1. Khối MF-01: Luyện Tập Vấn Đáp Tương Tác (`BE-1.1` $\to$ `BE-1.8`) — 🧑 Nguyễn Quang Thành
- ✅ **`BE-1.1` Khởi tạo phiên (`POST /api/v1/practice/sessions`):** Hỗ trợ `[Per-Question On-Demand]` (chọn đơn/tổ hợp độ khó `easy`, `medium`, `hard`, cấp Câu 1 ngay) và `[Full-Session Progressive]` (3–10 câu chia đều tăng dần Dễ $\to$ Trung bình $\to$ Khó, thiếu đề trả `HTTP 400`).
- ✅ **`BE-1.2` Hàng đợi RAM `BoundedGradingQueueChannel`:** 1,000 slots RAM (`BoundedChannelFullMode.Wait`), chống tràn bộ nhớ.
- ✅ **`BE-1.3` Nộp bài Per-Question & Batch (`POST .../answers`, `POST .../batch-answers`):** Persist-First $< 100$ms trả `HTTP 202 Accepted`, không lưu audio R2, lưu đúng `order_index`, `follow_up_index`, `follow_up_prompt`.
- ✅ **`BE-1.4` Background Worker `GradingQueueWorker` & SignalR `/hubs/practice`:** Chấm ngầm với Polly Retry 3 lần (2s, 4s, 8s); đẩy kết quả qua `ReceiveGradingResult`; nếu lỗi đẩy vào `dead_letter_queues` với `flow_type = 'PRACTICE'`, `managed_by_role = 'admin'`.
- ✅ **`BE-1.5` Gemini CoT 3 bước & Follow-up Engine A2:** Kích hoạt hỏi phụ khi $4.0 \le \text{Score} \le 8.0$ (tối đa `MaxPracticeFollowUpQuestions` 1–5 câu do Admin cấu hình, mặc định 2); lưu câu hỏi phụ chuẩn 3NF vào `ai_evaluations.follow_up_prompt`.
- ✅ **`BE-1.6` Hoàn tất phiên, Lịch sử & Scorecard (`POST .../complete`, `GET /student/history`, `GET /sessions/{id}`):** Tính và lưu `practice_sessions.total_score` chuẩn 3NF, truy vấn tốc độ cao nhờ chỉ mục ESR `ix_practice_sessions_student_started`.
- ✅ **`BE-1.7` Lấy câu hỏi tiếp theo On-Demand (`POST .../next-question`):** Thuật toán **Anti-3-Consecutive Randomizer** (tối đa 2 câu cùng độ khó trong 3 câu liên tiếp, loại trừ câu đã làm `Id NOT IN (...)`, fallback an toàn).
- ✅ **`BE-1.8` Cơ chế Inactivity Timeout 5 phút (`PracticeSessionTimeoutHelper`):** Quá 5 phút không tương tác tự động đóng phiên (`status = "completed"`), bảo toàn điểm câu đã làm, trả **`HTTP 410 Gone`** RFC 7807.

### 3.2. Khối MF-02: Thi Thử Vấn Đáp Bấm Giờ Có Hạn Ngạch (`BE-2.1` $\to$ `BE-2.4`) — 🧑 Nguyễn Quang Thành
- ✅ **`BE-2.1` Daily Quota Guard (`GET /api/v1/mock-exams/quota`, `POST /api/v1/mock-exams/sessions`):** Đọc động `max_mock_exams_per_day` theo từng môn học do Admin cấu hình; vượt hạn ngạch trả **`HTTP 429 Too Many Requests`**.
- ✅ **`BE-2.2` Bốc đề thi thử theo cấu hình môn học & ma trận Bloom:** Rút 100% từ kho `practice_questions` (cô lập tuyệt đối `exam_questions`), đọc thời lượng `mock_exam_duration_minutes`, tổng số câu `mock_exam_total_questions` và phân bổ `mock_exam_bloom_distribution` từ bảng `courses` / `exam_structures`.
- ✅ **`BE-2.3` Server Master Timer, Resume & Voice-First Gate (`GET .../sessions/{id}`, `POST .../answers`):** Kiểm soát thời gian thực tế trên server kèm Grace Period 10 giây; quá hạn trả **`HTTP 410 Gone`**; không lưu audio R2.
- ✅ **`BE-2.4` Chấm điểm tức thì Scorecard theo CLO & Lịch sử (`POST .../submit`, `GET /history`, `GET /history/{id}`):** Trả về Scorecard chi tiết từng câu, `cloCode`, phân loại học lực và lưu vết lỗi vào DLQ (`flow_type = 'MOCK_EXAM'`, `managed_by_role = 'admin'`) nếu AI quá tải.

### 3.3. Khối MF-03: Ngân Hàng Đề 2 Kho Độc Lập, CLO 3NF & Rubric Studio 10.0 (`BE-3.1` $\to$ `BE-3.4`, R8) — 🧑 Nguyễn Trọng Tốt & Nguyễn Quang Thành
- ✅ **`BE-3.1` Quản lý `course_clos` 3NF & AI sinh câu hỏi từ CLO (`POST /api/v1/questions/generate-from-clo`):** CRUD đầy đủ cho `course_clos` trên `CoursesController`; gọi Gemini sinh câu hỏi nháp, đáp án mẫu ($\ge 50$ ký tự) và tiêu chí Barem Rubric ($\sum = 10.00$đ).
- ✅ **`BE-3.2` Barem Rubric Studio 10.0đ & Upload ảnh (`RubricsController`, `POST /api/v1/questions/upload-image`):** Tổng điểm lệch $10.00$đ hoặc Model Answer $< 50$ ký tự bị chặn **`HTTP 422 Unprocessable Entity`**; kiểm tra Magic Bytes ảnh `PNG/JPG/WEBP <= 5MB` (`HTTP 413`/`415`).
- ✅ **`BE-3.3` Phân tách 2 luồng tạo câu hỏi (R8 — `CreateDraftQuestionCommandHandler`):**
  - Luồng Luyện tập/Thi thử (`POST /api/v1/practice/questions`, `usageScope = "practice"`): Lưu độc lập vào `practice_questions` (hỗ trợ gắn `clo_id`).
  - Luồng Thi thật (`POST /api/v1/official-exams/questions`, `usageScope = "exam"`): Bắt buộc truyền `cloId` và `structureId`, kiểm tra khớp ma trận `slot_definitions` của Trưởng Bộ Môn trước khi lưu vào `exam_questions`.
- ✅ **`BE-3.4` Gửi duyệt, Phê duyệt & Tra cứu phân trang SQL (`POST .../batch-submit-review`, `POST .../{id}/review-decision`, `POST .../batch-approve`, `GET /api/v1/questions`, `GET /api/v1/questions/{id}`):** Đầy đủ 3 quyết định (`APPROVED`, `NEEDS_REVISION`, `REJECTED`), phân trang trực tiếp tại SQL Server-side và trả về đầy đủ `cloId`, `cloCode`, `structureId`, `usageScope`, `rubric`.

### 3.4. Khối Chuỗi Học Thuật 4 Cấp (R5), DLQ Phân Tách (R2) & Các Module Vệ Tinh (`BE-V01` $\to$ `BE-V09`, `BE-V11`, `BE-V12`)
- ✅ **`BE-V01` & `BE-V02` (🧑 Thành):** Xuất báo cáo Khảo thí Excel (`.xlsx`) & PDF có mã băm SHA-256 (`ExportShiftExcelQuery`, `ExportShiftPdfQuery`) và cổng tra cứu công khai qua QR Code (`GET /api/v1/verification/grades/{certificateHash}`).
- ✅ **`BE-V03` & `BE-V11` (🧑 Thành & 🧑 Tốt):** API Dashboard Sinh viên (`GET /api/v1/student/dashboard`) và API Dashboard Giảng viên/Trưởng BM (`GET /api/v1/lecturer/dashboard` tổng hợp câu hỏi `NEEDS_REVISION` trên cả 2 kho đề).
- ✅ **`BE-V04`, `BE-V05`, `BE-V06`, `BE-V07`, `BE-V08` (🧑 Tốt):** Dual Auth (`AuthController`), User Management 6 Roles (`AdminController`), Semester CRUD không xóa (`SemestersController`), In-app Notifications (`NotificationsController`), và System Configs (`AdminController`).
- ✅ **`BE-V09` Quản trị DLQ phân tách độc lập theo luồng (R2 — `DeadLetterQueueController`):**
  - Admin (`admin`): Xem, Replay và Resolve các tác vụ DLQ thuộc luồng `PRACTICE` (MF-01), `MOCK_EXAM` (MF-02) và `IN_CLASS_EXAM` (MF-05) qua `/api/v1/admin/dlq/tasks`.
  - Giảng viên (`lecturer`): Xem và Replay các tác vụ DLQ thuộc luồng `IN_CLASS_EXAM` (MF-05) qua `/api/v1/lecturer/dlq/tasks`.
  - Cán bộ Khảo thí (`examination_officer`): Xem, Replay và Resolve các tác vụ DLQ thuộc luồng `OFFICIAL_EXAM` (MF-04) qua `/api/v1/examination-officer/dlq/tasks`.
  - Trưởng Bộ Môn (`department_head`): Đã cắt bỏ hoàn toàn khỏi quyền truy cập và replay DLQ (`HTTP 403 Forbidden`).
- ✅ **`BE-V12` Chuỗi phân cấp học thuật 4 cấp (`AcademicHierarchy` — R5):**
  - Cấp 1 (`admin`): Tạo môn học & gán Trưởng BM (`POST /api/v1/admin/courses/{id}/assign-department-head`).
  - Cấp 2 (`department_head`): Phân công nhiều Giảng viên vào môn học qua bảng 3NF `course_lecturers` (`POST /api/v1/department-head/courses/{id}/lecturers`).
  - Cấp 3 (`lecturer`): Tạo lớp học phần & sinh/reset mật khẩu lớp (`POST /api/v1/lecturer/classes`, `PUT/POST /api/v1/lecturer/classes/{id}/reset-password`).
  - Cấp 4 (`student`): Nhập `joinCode` + `password` tự tham gia lớp học (`POST /api/v1/student/classes/join`, `GET /api/v1/student/classes/my-classes`).

### 3.5. Các Phần Đã Hoàn Thành Trong Luồng MF-04 (`BE-4.2`, `BE-4.3`, `BE-4.4`, `BE-4.5`) — 🧑 Nguyễn Trọng Tốt
- ✅ **`BE-4.2` Check-in Kiosk IP Binding (`OfficialExamCheckInController`):** Ràng buộc ghế 1–40 với `ip_address` máy trạm, sai IP trả `HTTP 403 Forbidden`, quản lý 7 trạng thái vé thi viết HOA (`SCHEDULED` $\to$ `LOCKED`).
- ✅ **`BE-4.3` Upload Audio R2 `STT_MSSV.webm` + SHA-256 & Nộp bài Persist-First (`OfficialExamSubmissionsController`):** Cấp Presigned URL, niêm phong SHA-256, lưu trạng thái `SUBMITTED` $< 100$ms, Kiosk không hiện điểm liền.
- ✅ **`BE-4.4` Chấm ngầm AI Doubt Guard & Evidence Panel (`GetAuditEvidenceQuery`, `OverrideExamScoreCommand`):** Tự động gắn cờ `is_suspicious = true` khi `confidence_score < 0.70` hoặc điểm liệt; sửa điểm trên Evidence Panel bắt buộc nhập `override_reason >= 10` ký tự.
- ✅ **`BE-4.5` Công bố điểm Atomic & `OneWayLockInterceptor` (`PublishGradesCommand`):** Kiểm tra 100% thí sinh có điểm trước khi công bố, bật `is_locked = true` chặn mọi thao tác sửa/xóa trái phép (`HTTP 403 Forbidden`), kèm ghi nhận vi phạm realtime vào bảng `exam_anti_cheat_logs` và `ExamAntiCheatHub` (`/hubs/anti-cheat`).

---

# ⏳ PHẦN 4: DANH SÁCH 7 TASK ĐẶC TẢ CHI TIẾT CHO BACKEND TRIỂN KHAI (R1–R8) [TRẠNG THÁI: ⏳ TODO / CẦN BE TRIỂN KHAI]

Toàn bộ 7 Task dưới đây được xây dựng theo phương pháp luận **Spec-First / Design-First (Khóa cứng đặc tả trước khi code)**, đóng vai trò là hợp đồng kỹ thuật và căn cứ triển khai trực tiếp cho 2 kỹ sư Backend (**Nguyễn Quang Thành** & **Nguyễn Trọng Tốt**) cùng đội ngũ phát triển Capstone FA26SE166. Mỗi task đều có đầy đủ thiết kế CSDL 3NF, ESR Indexes, đặc tả API RESTful RFC 7807, thuật toán lõi, ma trận 5 ca biên và tiêu chí nghiệm thu nghiêm ngặt.

---

### ⏳ TASK 1 (R1 & R2): Thiết Kế Spec-First, Khóa Hợp Đồng API & Phân Định Quản Trị Dead-Letter Queue (DLQ) Theo Luồng Nghiệp Vụ
* **Mã định danh task:** `BE-TASK-01` (Phân hệ: `BE-CORE-DLQ` & `BE-V09` — Thuộc Yêu cầu R1 & R2)
* **Trạng thái thực hiện:** `⏳ TODO / CẦN BE TRIỂN KHAI`
* **Mức độ ưu tiên:** Khẩn cấp (P0 — Nền tảng chịu lỗi và cứu hộ dữ liệu thi cử)
* **Kỹ sư phụ trách:** 🧑 **Nguyễn Trọng Tốt** (AI & QA Lead) & 🧑 **Nguyễn Quang Thành** (Lead Backend Architect)

#### 1. Mục tiêu nghiệp vụ (Business Objectives)
1. **Thực thi nguyên tắc Spec-First & Zero Data Loss:** Nộp bài được persist ngay xuống đĩa trong $< 100$ms (`SUBMITTED`), sau đó đưa vào hàng đợi chấm ngầm `BoundedGradingQueueChannel` (1,000 slots RAM). Nếu quá trình gọi LLM (Gemini 1.5 Flash/Pro) hoặc STT (Whisper) thất bại liên tiếp sau 3 lần retry (Polly Exponential Backoff: 2s, 4s, 8s), tác vụ bắt buộc phải được cách ly vào bảng `dead_letter_queues` để chống mất bài làm của thí sinh.
2. **Phân định ranh giới quản trị DLQ theo luồng (R2):**
   - **Quản trị viên (`admin`):** Chịu trách nhiệm giám sát, xem xét và phát lệnh Replay các tác vụ DLQ thuộc luồng Luyện tập (`flow_type = 'PRACTICE'`, MF-01), Thi thử (`flow_type = 'MOCK_EXAM'`, MF-02) và Ca thi trong lớp (`flow_type = 'IN_CLASS_EXAM'`, MF-05).
   - **Giảng viên (`lecturer`):** Chịu trách nhiệm giám sát, xem xét và phát lệnh Replay các tác vụ DLQ thuộc luồng Ca thi trong lớp (`flow_type = 'IN_CLASS_EXAM'`, MF-05) qua `/api/v1/lecturer/dlq/tasks`.
   - **Cán bộ Khảo thí (`examination_officer`):** Chịu trách nhiệm giám sát, xem xét và phát lệnh Replay các tác vụ DLQ thuộc luồng Thi thật phòng Lab (`flow_type = 'OFFICIAL_EXAM'`, MF-04).
   - **Trưởng Bộ Môn (`department_head`):** CẮT BỎ hoàn toàn quyền xem và can thiệp vào Dead-Letter Queue (trả về `HTTP 403 Forbidden`) nhằm giảm tải đầu việc hành chính và bảo vệ tính bảo mật ca thi thật.

#### 2. Thiết kế CSDL 3NF Chi Tiết (PostgreSQL 16)
* **Bảng:** `dead_letter_queues`
* **Cấu trúc cột & Kiểu dữ liệu:**
  - `id UUID PRIMARY KEY DEFAULT gen_random_uuid()`: Khóa chính định danh tác vụ lỗi.
  - `entity_id UUID NOT NULL`: Khóa ngoại logic liên kết tới bản ghi bài thi cần chấm (`practice_answers.id`, `mock_exam_answers.id`, `exam_question_submissions.id` hoặc `in_class_exam_submissions.id`).
  - `flow_type VARCHAR(30) NOT NULL`: Phân loại luồng nghiệp vụ.
  - `managed_by_role VARCHAR(30) NOT NULL`: Role được cấp quyền xử lý tác vụ này.
  - `payload JSONB NOT NULL`: Dữ liệu đầu vào đầy đủ phục vụ chấm thi (gồm `transcript`, `model_answer`, `rubric_criteria`, `clo_code`).
  - `error_message TEXT NOT NULL`: Chi tiết thông điệp lỗi hoặc stack trace từ API ngoài (ví dụ `HTTP 429 RateLimitExceeded`, `GeminiTimeoutException`).
  - `retry_count INT NOT NULL DEFAULT 0`: Số lần hệ thống đã thử chấm lại.
  - `max_retries INT NOT NULL DEFAULT 3`: Ngưỡng thử lại tối đa trước khi đưa vào diện chờ xử lý thủ công.
  - `status VARCHAR(20) NOT NULL DEFAULT 'FAILED'`: Trạng thái tác vụ.
  - `created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP`: Thời điểm cách ly vào DLQ.
  - `last_retry_at TIMESTAMPTZ NULL`: Thời điểm thử lại gần nhất.
  - `resolved_at TIMESTAMPTZ NULL`: Thời điểm hoàn tất xử lý thành công.
  - `resolved_by_user_id UUID NULL REFERENCES users(id)`: Người phát lệnh replay thành công.
* **Ràng buộc toàn vẹn (Constraints):**
  - `ck_dlq_flow_type`: `CHECK (flow_type IN ('PRACTICE', 'MOCK_EXAM', 'OFFICIAL_EXAM', 'IN_CLASS_EXAM'))`
  - `ck_dlq_managed_role`: `CHECK (managed_by_role IN ('admin', 'examination_officer', 'lecturer'))`
  - `ck_dlq_status`: `CHECK (status IN ('FAILED', 'RETRYING', 'RESOLVED', 'ABANDONED'))`
  - `ck_dlq_retry_count`: `CHECK (retry_count >= 0 AND retry_count <= 10)`
* **Chỉ mục tối ưu ESR (Equality - Sort - Range Indexes):**
  - `ix_dlq_flow_status_created`: `(flow_type, status, created_at DESC)` — Phục vụ lọc theo luồng nghiệp vụ, trạng thái lỗi và sắp xếp thời gian tạo giảm dần.
  - `ix_dlq_role_status_retry`: `(managed_by_role, status, last_retry_at)` — Phục vụ phân quyền truy vấn của Admin/Khảo thí và bộ quét tự động re-poll.
  - `ix_dlq_entity`: `(entity_id)` — Phục vụ kiểm tra bài nộp đã có bản ghi lỗi hay chưa ($O(1)$).

#### 3. Đặc tả API Endpoints (RESTful RFC 7807)
* **Endpoint 1: Admin lấy danh sách tác vụ DLQ luyện tập/thi thử**
  - `GET /api/v1/admin/dlq/tasks`
  - **Phân quyền:** `[Authorize(Roles = "admin")]`
  - **Query Parameters:** `flowType` (tùy chọn: `PRACTICE` | `MOCK_EXAM`), `status` (tùy chọn), `page` (int, default 1), `pageSize` (int, default 20).
  - **Response 200 OK:**
    ```json
    {
      "items": [
        {
          "id": "550e8400-e29b-41d4-a716-446655440001",
          "entityId": "a1b2c3d4-e5f6-7a8b-9c0d-1e2f3a4b5c6d",
          "flowType": "PRACTICE",
          "managedByRole": "admin",
          "status": "FAILED",
          "retryCount": 2,
          "errorMessage": "Gemini API HTTP 429: Resource has been exhausted (quota exceeded).",
          "createdAt": "2026-11-05T10:15:30Z",
          "lastRetryAt": "2026-11-05T10:20:30Z"
        }
      ],
      "totalCount": 1,
      "page": 1,
      "pageSize": 20
    }
    ```
* **Endpoint 2: Admin phát lệnh Replay tác vụ DLQ**
  - `POST /api/v1/admin/dlq/tasks/{id}/replay`
  - **Phân quyền:** `[Authorize(Roles = "admin")]`
  - **Request Body:** `{ "reason": "Admin replay sau khi nạp hạn ngạch API Tier 1" }`
  - **Response 200 OK:** `{ "taskId": "550e8400-e29b-41d4-a716-446655440001", "status": "RETRYING", "message": "Đã đẩy lại tác vụ vào BoundedGradingQueueChannel" }`
  - **Response 404 / 409:** RFC 7807 Problem Details.
* **Endpoint 3: Khảo thí lấy danh sách tác vụ DLQ thi thật**
  - `GET /api/v1/examination-officer/dlq/tasks`
  - **Phân quyền:** `[Authorize(Roles = "examination_officer")]`
  - **Lọc cứng phía Server:** Bắt buộc áp điều kiện `flow_type = 'OFFICIAL_EXAM'`.
* **Endpoint 4: Khảo thí phát lệnh Replay tác vụ thi thật**
  - `POST /api/v1/examination-officer/dlq/tasks/{id}/replay`
  - **Phân quyền:** `[Authorize(Roles = "examination_officer")]`
* **Endpoint 5: Chặn Trưởng Bộ Môn gọi DLQ**
  - Mọi route tới `/api/v1/department-head/dlq/*` đều bị chặn với mã lỗi `HTTP 403 Forbidden`:
    ```json
    {
      "type": "https://oralexam.io.vn/errors/forbidden",
      "title": "Forbidden",
      "status": 403,
      "detail": "Trưởng Bộ Môn không có thẩm quyền truy cập hoặc xử lý Dead-Letter Queue."
    }
    ```

#### 4. Đặc tả Chi Tiết Logic Xử Lý & Thuật Toán
* **Luồng cách ly lỗi (Polly Pipeline $\to$ DLQ):** Khi worker đọc bài nộp từ `BoundedGradingQueueChannel`, thực thi qua Polly Pipeline (3 retries: $2^1, 2^2, 2^3$ giây). Nếu vẫn văng ngoại lệ:
  1. Mở transaction: Khởi tạo bản ghi `dead_letter_queues` với `managed_by_role` tương ứng (`admin` cho MF-01/02, `examination_officer` cho MF-04).
  2. Cập nhật trạng thái bài nộp tương ứng sang `AI_GRADING_FAILED` (không xóa bài thi, bảo toàn audio/transcript).
* **Luồng Background Auto-Recheck (5 phút/lần):**
  - Định kỳ mỗi 300 giây, `DlqBackgroundWorker` truy vấn tối đa 50 bản ghi có `status = 'FAILED'` và `retry_count < max_retries` và `(last_retry_at IS NULL OR last_retry_at <= now() - INTERVAL '5 minutes')`.
  - Đẩy lại payload vào `BoundedGradingQueueChannel` với độ ưu tiên cao.
* **Luồng Replay thủ công:** Kiểm tra quyền Role $\to$ Khóa dòng (`SELECT ... FOR UPDATE`) $\to$ Nếu `status == 'RETRYING'` ném `HTTP 409 Conflict` (chống duplicate replay) $\to$ Đổi `status = 'RETRYING'`, tăng `retry_count`, đẩy lại hàng đợi. Khi worker chấm xong thành công $\to$ cập nhật `status = 'RESOLVED'`, `resolved_at = UtcNow`, `resolved_by_user_id`.

#### 5. Ma Trận 5 Ca Biên Kỹ Thuật (Edge Cases)
| Ca biên | Kịch bản chi tiết | Cơ chế xử lý kỹ thuật | Mã HTTP / Hành vi |
|:---|:---|:---|:---|
| **Null/Empty** | Replay với ID không tồn tại hoặc ID chuỗi rỗng. | FluentValidation chặn GUID rỗng; Query DB kiểm tra `FindOrDefault`. | `HTTP 404 Not Found` RFC 7807 |
| **Boundary** | Task đã thử lại đủ `max_retries = 10` lần mà vẫn lỗi. | Chuyển trạng thái sang `'ABANDONED'`, ngừng auto-retry, thông báo admin. | Log cảnh báo CRITICAL |
| **Empty Collections** | Hàng đợi DLQ hoàn toàn sạch (0 lỗi). | API trả về danh sách rỗng `{ items: [], totalCount: 0 }`, không gây crash hệ thống. | `HTTP 200 OK` |
| **Concurrency** | 2 Cán bộ Khảo thí cùng bấm Replay 1 task thi thật cùng lúc. | Dùng DB Row Locking (`SELECT ... FOR UPDATE`), request đầu đổi sang `RETRYING`, request 2 bị từ chối. | `HTTP 409 Conflict` |
| **Network/Timeout** | Mạng chập chờn khi đẩy task vào channel RAM. | Sử dụng `BoundedChannel.Writer.WaitToWriteAsync` có timeout 5s, nếu timeout giữ nguyên `FAILED`. | `HTTP 503 Service Unavailable` |

#### 6. Tiêu Chí Nghiệm Thu (Acceptance Criteria)
- [ ] Cán bộ Khảo thí xem và replay được các task có `flow_type = 'OFFICIAL_EXAM'`.
- [ ] Quản trị viên xem và replay được các task có `flow_type IN ('PRACTICE', 'MOCK_EXAM')`.
- [ ] Tài khoản role `department_head` bị chặn `HTTP 403 Forbidden` khi truy cập bất kỳ endpoint DLQ nào.
- [ ] Task sau khi replay thành công tự động cập nhật điểm vào bài thi và chuyển trạng thái DLQ sang `RESOLVED`.

---

### ⏳ TASK 2 (R3): Độc Quyền Cấu Hình Follow-up Thi Thật Cho Trưởng Bộ Môn & Cô Lập Khảo Thí
* **Mã định danh task:** `BE-TASK-02` (Phân hệ: `BE-4.1-FOLLOWUP` — Thuộc Yêu cầu R3)
* **Trạng thái thực hiện:** `⏳ TODO / CẦN BE TRIỂN KHAI`
* **Mức độ ưu tiên:** Cao (P1 — Phân định ranh giới học thuật chuẩn FPTU)
* **Kỹ sư phụ trách:** 🧑 **Nguyễn Trọng Tốt** (Backend Developer & QA Lead)

#### 1. Mục tiêu nghiệp vụ (Business Objectives)
1. **Bảo toàn quyền hạn chuyên môn học thuật:** Cấu hình câu hỏi phụ (Follow-up) cho kỳ thi thật phản ánh độ khó và chuẩn đầu ra CLO của môn học, do đó bắt buộc thuộc **100% thẩm quyền độc quyền của Trưởng Bộ Môn (`department_head`)** quản lý môn học đó.
2. **Loại bỏ thẩm quyền cấu hình Follow-up khỏi Cán bộ Khảo thí:** Cán bộ Khảo thí chỉ tổ chức ca thi, phòng máy, kíp thi; tuyệt đối không được tự ý bật/tắt hoặc điều chỉnh số lượng câu hỏi phụ của kỳ thi thật khi tạo ca thi.

#### 2. Thiết kế CSDL 3NF Chi Tiết (PostgreSQL 16)
* **Bảng:** `official_exam_sessions`
* **Cấu trúc cột & Kiểu dữ liệu liên quan:**
  - `has_follow_up BOOLEAN NOT NULL DEFAULT TRUE`: Cho phép hỏi phụ trong kỳ thi hay không.
  - `max_follow_up_questions INT NOT NULL DEFAULT 2`: Số câu hỏi phụ tối đa cho mỗi câu hỏi chính.
  - `configured_by_user_id UUID NULL REFERENCES users(id)`: Định danh Trưởng BM cấu hình.
  - `configured_at TIMESTAMPTZ NULL`: Thời điểm thiết lập cấu hình.
* **Ràng buộc toàn vẹn (Constraints):**
  - `ck_exam_session_follow_up_range`: `CHECK (max_follow_up_questions >= 1 AND max_follow_up_questions <= 5)`
* **Chỉ mục tối ưu ESR:**
  - `ix_exam_sessions_course_active`: `(course_id, is_active, start_time)` — Equality on course_id, is_active; Range on start_time.

#### 3. Đặc tả API Endpoints (RESTful RFC 7807)
* **Endpoint 1: Trưởng Bộ Môn thiết lập câu hỏi phụ kỳ thi thật**
  - `PUT /api/v1/official-exams/sessions/{id}/follow-up-config`
  - **Phân quyền:** `[Authorize(Roles = "department_head,admin")]` (Nếu `examination_officer` hoặc `lecturer` gọi $\to$ `HTTP 403 Forbidden`).
  - **Request DTO (`ConfigureOfficialExamFollowUpRequestDto`):**
    ```json
    {
      "hasFollowUp": true,
      "maxFollowUpQuestions": 2
    }
    ```
    *Validation:* `hasFollowUp` required (bool); `maxFollowUpQuestions` required khi `hasFollowUp == true`, giá trị nguyên $\in [1, 5]$.
  - **Response 200 OK:**
    ```json
    {
      "sessionId": "e2a3b4c5-d6e7-48f9-a0b1-c2d3e4f5a6b7",
      "hasFollowUp": true,
      "maxFollowUpQuestions": 2,
      "configuredByUserId": "11111111-2222-3333-4444-555555555555",
      "configuredAt": "2026-11-05T14:30:00Z",
      "message": "Cập nhật cấu hình câu hỏi phụ kỳ thi thành công."
    }
    ```
  - **Response Lỗi RFC 7807:**
    - `HTTP 400 Bad Request`: `maxFollowUpQuestions` nằm ngoài khoảng $[1, 5]$.
    - `HTTP 403 Forbidden`: Người gọi không phải là Trưởng BM phụ trách môn học của kỳ thi.
    - `HTTP 409 Conflict`: Kỳ thi đã có ca thi xuất bản đề (`is_exam_published = true`) hoặc đang thi, không cho phép sửa đổi.
* **Endpoint 2: Khảo thí tạo kỳ thi (đã tách bỏ cấu hình Follow-up)**
  - `POST /api/v1/examination-officer/official-exams/sessions`
  - Payload của Khảo thí chỉ chứa: `courseId`, `semesterId`, `name`, `startTime`, `endTime`. Không cho phép truyền `hasFollowUp`. Hệ thống tự động gán mặc định `has_follow_up = true, max_follow_up_questions = 2` chờ Trưởng BM tinh chỉnh.

#### 4. Đặc tả Chi Tiết Logic Xử Lý & Thuật Toán
1. Trưởng BM gửi request kèm `sessionId`.
2. Truy vấn kỳ thi kèm thông tin môn học: `_context.OfficialExamSessions.Include(s => s.Course).FirstOrDefaultAsync(s => s.Id == id)`.
3. Kiểm tra quyền sở hữu môn: `session.Course.DepartmentHeadUserId == currentUserId` hoặc role `admin`. Nếu sai $\to$ ném `ForbiddenException` (`HTTP 403`).
4. Kiểm tra khóa kỳ thi: Kiểm tra trong bảng `real_exam_session_shifts` trực thuộc, nếu có bất kỳ ca thi nào có `is_exam_published == true` hoặc `status == 'IN_PROGRESS'` $\to$ ném `ConflictException` (`HTTP 409`).
5. Cập nhật dữ liệu: `session.HasFollowUp = request.HasFollowUp`, `session.MaxFollowUpQuestions = request.MaxFollowUpQuestions`, `session.ConfiguredByUserId = currentUserId`, `session.ConfiguredAt = DateTime.UtcNow`.

#### 5. Ma Trận 5 Ca Biên Kỹ Thuật (Edge Cases)
| Ca biên | Kịch bản chi tiết | Cơ chế xử lý kỹ thuật | Mã HTTP / Hành vi |
|:---|:---|:---|:---|
| **Null/Empty** | `hasFollowUp = true` nhưng `maxFollowUpQuestions = null`. | FluentValidation bắt lỗi thiếu thuộc tính phụ thuộc. | `HTTP 422 Unprocessable Entity` |
| **Boundary** | Nhập `maxFollowUpQuestions = 0` hoặc `6`. | Check Constraint DB và FluentValidation chặn ngoài biên. | `HTTP 400 Bad Request` |
| **Empty Collections** | Kỳ thi mới tạo chưa có ca thi nào (`shifts` rỗng). | Cho phép cập nhật cấu hình bình thường. | `HTTP 200 OK` |
| **Concurrency** | Trưởng BM và Admin cùng bấm lưu cấu hình tại 1 thời điểm. | Áp dụng Optimistic Concurrency Control qua cột `xmin` / `RowVersion`. | Request thứ 2 báo `HTTP 409 Conflict` |
| **Network/Timeout** | Kết nối mạng đứt đoạn khi đang ghi DB. | EF Core DbContext Rollback transaction an toàn. | `HTTP 500 Internal Server Error` |

#### 6. Tiêu Chí Nghiệm Thu (Acceptance Criteria)
- [ ] Cán bộ Khảo thí không thể gọi API `PUT .../follow-up-config` (trả về 403).
- [ ] Trưởng Bộ Môn của môn học cấu hình thành công trong khoảng 1–5 câu hỏi phụ.
- [ ] Giá trị ngoài biên ($< 1$ hoặc $> 5$) bị từ chối ngay tại tầng validation.
- [ ] Không cho phép sửa đổi cấu hình khi ca thi đã mở hoặc đã xuất bản đề thi.

---

### ⏳ TASK 3 (R4): Siết Chặt Quyền Hạn Giám Thị (Proctor) Phòng Lab Kiosk & Chuẩn Hóa Ghi Nhận Vi Phạm
* **Mã định danh task:** `BE-TASK-03` (Phân hệ: `BE-V10` — Thuộc Yêu cầu R4)
* **Trạng thái thực hiện:** `⏳ TODO / CẦN BE TRIỂN KHAI`
* **Mức độ ưu tiên:** Cao (P1 — An ninh phòng thi và kỷ luật phòng máy)
* **Kỹ sư phụ trách:** 🧑 **Nguyễn Trọng Tốt** (Backend Developer & QA Lead)

#### 1. Mục tiêu nghiệp vụ (Business Objectives)
1. **Tuân thủ Luật Cấm số 14 và Cam kết Kỹ thuật Bất biến số 12:** Giám thị (`proctor`) là nhân sự coi thi vật lý trong phòng lab; **CHỈ CÓ THẨM QUYỀN**:
   - Điểm danh sinh viên theo số máy (`SeatNumber` 1–40), đối chiếu thẻ SV/CCCD.
   - Lập biên bản vi phạm an ninh phòng thi gửi vào hệ thống.
2. **CẮT BỎ TOÀN BỘ CÁC QUYỀN HẠN VƯỢT CẤP CỦA PROCTOR:**
   - CẤM mở ca thi (`Shift.Status = "in_progress"`).
   - CẤM đóng ca thi (`Shift.Status = "completed"`).
   - CẤM gỡ niêm phong hoặc đổi IP máy trạm (`ticket.IpAddress`).
   - CẤM đình chỉ thi tự ý gán điểm 0đ vào `LecturerAudit`.
   - CẤM xem điểm thi hoặc sửa điểm thi của thí sinh.

#### 2. Thiết kế CSDL 3NF Chi Tiết (PostgreSQL 16)
* **Bảng:** `exam_anti_cheat_logs`
* **Cấu trúc cột & Kiểu dữ liệu:**
  - `id UUID PRIMARY KEY DEFAULT gen_random_uuid()`: Khóa chính bản ghi vi phạm.
  - `shift_id UUID NOT NULL REFERENCES real_exam_session_shifts(id)`: Ca thi xảy ra vi phạm.
  - `ticket_id UUID NOT NULL REFERENCES student_exam_tickets(id)`: Vé thi của thí sinh vi phạm.
  - `student_id UUID NOT NULL REFERENCES users(id)`: Thí sinh vi phạm.
  - `incident_type VARCHAR(50) NOT NULL`: Phân loại vi phạm.
  - `severity VARCHAR(20) NOT NULL DEFAULT 'MEDIUM'`: Mức độ nghiêm trọng.
  - `description TEXT NOT NULL`: Mô tả hành vi vi phạm do Giám thị ghi nhận.
  - `evidence_data JSONB NULL`: Dữ liệu chứng cứ (ảnh chụp camera, logs client).
  - `reported_by_user_id UUID NOT NULL REFERENCES users(id)`: Giám thị lập biên bản.
  - `created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP`: Thời điểm ghi nhận.
* **Ràng buộc toàn vẹn (Constraints):**
  - `ck_anti_cheat_severity`: `CHECK (severity IN ('LOW', 'MEDIUM', 'HIGH', 'CRITICAL'))`
  - `ck_anti_cheat_incident_type`: `CHECK (incident_type IN ('TAB_SWITCH', 'FULLSCREEN_EXIT', 'DEVTOOLS_OPEN', 'MULTIPLE_PEOPLE', 'PROCTOR_MANUAL_REPORT', 'UNAUTHORIZED_DEVICE', 'OTHER'))`
  - `ck_anti_cheat_desc_min`: `CHECK (length(trim(description)) >= 10)`
* **Chỉ mục tối ưu ESR:**
  - `ix_anti_cheat_shift_created`: `(shift_id, created_at DESC)` — Lấy danh sách vi phạm theo ca thi.
  - `ix_anti_cheat_ticket_severity`: `(ticket_id, severity, created_at DESC)` — Tra cứu vi phạm của từng thí sinh để hỗ trợ Giám khảo chấm thi.

#### 3. Đặc tả API Endpoints (RESTful RFC 7807)
* **Endpoint 1: Giám thị lập biên bản vi phạm phòng thi**
  - `POST /api/v1/proctor/shifts/{shiftId}/violations`
  - **Phân quyền:** `[Authorize(Roles = "proctor,examination_officer,admin")]`
  - **Request DTO (`RecordProctorViolationRequestDto`):**
    ```json
    {
      "ticketId": "a1b2c3d4-e5f6-7a8b-9c0d-1e2f3a4b5c6d",
      "incidentType": "PROCTOR_MANUAL_REPORT",
      "severity": "HIGH",
      "description": "Thí sinh có hành vi sử dụng tài liệu giấy và điện thoại trong hộc bàn",
      "evidenceData": {
        "seatNumber": 15,
        "materialNoted": "Tài liệu in A4 tóm tắt Clean Architecture"
      }
    }
    ```
    *Validation:* `ticketId` required; `description` required $\ge 10$ ký tự; `severity` valid enum.
  - **Response 201 Created:** Trả về chi tiết bản ghi vi phạm và phát sự kiện realtime qua `ExamAntiCheatHub` tới nhóm phòng thi.
* **Endpoint 2: Giám thị điểm danh thí sinh vào máy thi**
  - `PUT /api/v1/proctor/tickets/{ticketId}/attendance`
  - **Phân quyền:** `[Authorize(Roles = "proctor,examination_officer,admin")]`
  - **Request DTO (`UpdateAttendanceRequestDto`):**
    ```json
    {
      "isPresent": true,
      "seatNumber": 15,
      "notes": "Đã kiểm tra CCCD và thẻ sinh viên hợp lệ"
    }
    ```
    *Validation:* `seatNumber` $\in [1, 40]$. **TUYỆT ĐỐI KHÔNG CÓ TRƯỜNG `ipAddress` hay `shiftStatus` TRONG REQUEST DTO**.
  - **Response 200 OK:** `{ "ticketId": "uuid", "isPresent": true, "checkedInAt": "2026-11-05T08:05:00Z" }`
* **Endpoint 3: Chặn Proctor tự ý đình chỉ thi gán điểm 0đ**
  - Endpoint `POST /api/v1/proctor/tickets/{ticketId}/suspend` được đổi phân quyền thành `[Authorize(Roles = "examination_officer,admin")]`. Proctor gọi vào nhận `HTTP 403 Forbidden`. Xóa bỏ hoàn toàn logic tự tạo `LecturerAudit` điểm 0đ.

#### 4. Đặc tả Chi Tiết Logic Xử Lý & Thuật Toán
* **Điểm danh an toàn:** Kiểm tra ca thi mà Proctor được phân công (`shift.ProctorUserId == currentUserId`). Cập nhật cờ `ticket.IsPresent = isPresent`, `ticket.CheckedInAt = UtcNow`, `ticket.CheckedInByUserId = currentUserId`. Giữ nguyên 100% giá trị `ticket.IpAddress` đã niêm phong từ Kiosk. Không thay đổi `shift.Status`.
* **Phát cảnh báo vi phạm Realtime:** Sau khi lưu bản ghi vào `exam_anti_cheat_logs`, gọi `_hubContext.Clients.Group($"shift_{shiftId}").SendAsync("ReceiveViolationAlert", newViolationDto)` để thông báo ngay lập tức trên màn hình Giám khảo (`examiner`) và Khảo thí.

#### 5. Ma Trận 5 Ca Biên Kỹ Thuật (Edge Cases)
| Ca biên | Kịch bản chi tiết | Cơ chế xử lý kỹ thuật | Mã HTTP / Hành vi |
|:---|:---|:---|:---|
| **Null/Empty** | Giám thị gửi `description` rỗng hoặc $< 10$ ký tự. | FluentValidation chặn ngay tại Controller. | `HTTP 422 Unprocessable Entity` |
| **Boundary** | Giám thị nhập số máy `seatNumber = 0` hoặc `41`. | Validation chặn ngoài biên $[1, 40]$. | `HTTP 400 Bad Request` |
| **Empty Collections** | Ca thi không có thí sinh nào bị vi phạm. | API lấy danh sách vi phạm trả về `[]` với HTTP 200. | `HTTP 200 OK` |
| **Concurrency** | Giám thị click điểm danh 2 lần liên tục cho 1 thí sinh. | Xử lý Idempotent: kết quả cuối cùng đều là `isPresent = true`. | `HTTP 200 OK` |
| **Network/Timeout** | Mạng phòng lab lag khi gửi biên bản vi phạm. | Client retry với idempotency key; DB không tạo bản ghi trùng lặp trong 10s. | `HTTP 201 Created` |

#### 6. Tiêu Chí Nghiệm Thu (Acceptance Criteria)
- [ ] Tài khoản role `proctor` gọi endpoint `suspend` bị từ chối `HTTP 403 Forbidden`.
- [ ] Điểm danh thí sinh không làm thay đổi trạng thái ca thi sang `in_progress` và không ghi đè `ip_address`.
- [ ] Báo cáo vi phạm gửi từ Giám thị được lưu vào `exam_anti_cheat_logs` và hiển thị realtime trên Evidence Panel của Giám khảo.

---

### ⏳ TASK 4 (R5): Chuỗi Phân Cấp Quản Lý Học Thuật 4 Cấp & Gỡ Bỏ Logic Auto-Enrollment Khi Đăng Nhập
* **Mã định danh task:** `BE-TASK-04` (Phân hệ: `BE-V12` & `BE-V04` — Thuộc Yêu cầu R5)
* **Trạng thái thực hiện:** `⏳ TODO / CẦN BE TRIỂN KHAI`
* **Mức độ ưu tiên:** Cao (P1 — Phân cấp học thuật chuẩn hóa và an toàn truy cập lớp học)
* **Kỹ sư phụ trách:** 🧑 **Nguyễn Quang Thành** (Lead Backend Architect) & 🧑 **Nguyễn Trọng Tốt** (Backend Developer)

#### 1. Mục tiêu nghiệp vụ (Business Objectives)
1. **Thiết lập chuỗi phân cấp học thuật 4 tầng chuẩn mực:**
   - **Cấp 1 (Admin):** Quản trị Semester (FE-08) $\to$ Tạo môn học (Course) $\to$ Gán Trưởng Bộ Môn vào môn học (`courses.department_head_user_id`).
   - **Cấp 2 (Department Head):** Phân công nhiều Giảng viên vào giảng dạy môn học thông qua bảng trung gian chuẩn 3NF `course_lecturers` ($N-N$).
   - **Cấp 3 (Lecturer):** Giảng viên được phân công giảng dạy môn học có quyền tạo Lớp học phần (`classes`), sinh và reset mật khẩu lớp (`classes.join_password_hash`, `classes.join_code`).
   - **Cấp 4 (Student):** Sinh viên nhập đúng mã lớp và mật khẩu lớp (`POST /api/v1/student/classes/join`) để ghi danh vào lớp.
2. **Gỡ bỏ hoàn toàn logic Auto-Enrollment:** Xóa triệt để hàm `EnsureStudentEnrolledInActiveClassesAsync` trong `GoogleLoginCommandHandler` và `PasswordLoginCommandHandler`. Cấm tự động nhét mọi sinh viên vào tất cả các lớp đang mở của toàn trường khi đăng nhập.

#### 2. Thiết kế CSDL 3NF Chi Tiết (PostgreSQL 16)
* **Bảng:** `courses` (Cập nhật)
  - `department_head_user_id UUID NULL REFERENCES users(id)`: Trưởng BM phụ trách môn.
* **Bảng:** `course_lecturers` (Bảng trung gian $N-N$ chuẩn 3NF)
  - `course_id UUID NOT NULL REFERENCES courses(id) ON DELETE CASCADE`
  - `lecturer_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE`
  - `assigned_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP`
  - `assigned_by_user_id UUID NOT NULL REFERENCES users(id)`
  - `PRIMARY KEY (course_id, lecturer_id)`
  - *Chỉ mục ESR:* `ix_course_lecturers_lecturer`: `(lecturer_id, course_id)`
* **Bảng:** `classes`
  - `id UUID PRIMARY KEY DEFAULT gen_random_uuid()`
  - `course_id UUID NOT NULL REFERENCES courses(id)`
  - `semester_id UUID NOT NULL REFERENCES semesters(id)`
  - `lecturer_id UUID NOT NULL REFERENCES users(id)`
  - `name VARCHAR(100) NOT NULL`: Tên lớp (vd `SE1701-NET`).
  - `join_code VARCHAR(20) NOT NULL UNIQUE`: Mã tham gia lớp (vd `SE1701NET`).
  - `join_password_hash VARCHAR(255) NOT NULL`: Mật khẩu lớp đã băm BCrypt.
  - `is_active BOOLEAN NOT NULL DEFAULT TRUE`
  - `created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP`
  - *Chỉ mục ESR:* `ix_classes_course_semester_active`: `(course_id, semester_id, is_active)`; `ix_classes_join_code`: `(join_code)`.
* **Bảng:** `class_enrollments`
  - `class_id UUID NOT NULL REFERENCES classes(id) ON DELETE CASCADE`
  - `student_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE`
  - `enrolled_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP`
  - `status VARCHAR(20) NOT NULL DEFAULT 'ACTIVE'`
  - `PRIMARY KEY (class_id, student_id)`
  - *Chỉ mục ESR:* `ix_class_enrollments_student`: `(student_id, status)`.

#### 3. Đặc tả API Endpoints (RESTful RFC 7807)
* **Endpoint 1: Admin gán Trưởng BM vào môn học**
  - `POST /api/v1/admin/courses/{id}/assign-department-head` | Roles: `admin` | Body: `{ "departmentHeadUserId": "uuid" }`
* **Endpoint 2: Trưởng BM phân công Giảng viên vào môn học**
  - `POST /api/v1/department-head/courses/{id}/lecturers` | Roles: `department_head,admin` | Body: `{ "lecturerUserIds": ["uuid1", "uuid2"] }`
* **Endpoint 3: Giảng viên tạo lớp học phần**
  - `POST /api/v1/lecturer/classes` | Roles: `lecturer`
  - **Body:** `{ "courseId": "uuid", "semesterId": "uuid", "name": "SE1701-NET", "joinCode": "SE1701NET", "password": "ClassPassword@123" }`
  - **Kiểm tra quyền:** Giảng viên bắt buộc phải có tên trong `course_lecturers` của môn này (nếu không $\to$ `HTTP 403 Forbidden`).
* **Endpoint 4: Giảng viên reset mật khẩu lớp**
  - `POST /api/v1/lecturer/classes/{id}/reset-password` | Roles: `lecturer` | Body: `{ "newPassword": "NewPassword@123" }`
* **Endpoint 5: Sinh viên nhập mật khẩu tham gia lớp**
  - `POST /api/v1/student/classes/join` | Roles: `student`
  - **Body:** `{ "joinCode": "SE1701NET", "password": "ClassPassword@123" }`
  - **Response 200 OK:** `{ "classId": "uuid", "className": "SE1701-NET", "courseCode": "SWP391", "message": "Ghi danh thành công" }`
  - **Response Lỗi:** `HTTP 400 Bad Request` (sai mật khẩu); `HTTP 404 Not Found` (sai mã lớp); `HTTP 409 Conflict` (đã tham gia).
* **Endpoint 6: Sinh viên lấy danh sách lớp của mình**
  - `GET /api/v1/student/classes/my-classes` | Roles: `student` | Trả về danh sách lớp sinh viên đã tham gia.

#### 4. Đặc tả Chi Tiết Logic Xử Lý & Thuật Toán
* **Quy trình gỡ bỏ Auto-Enrollment:**
  1. Trong `GoogleLoginCommandHandler.cs`: Xóa bỏ hoàn toàn lời gọi `EnsureStudentEnrolledInActiveClassesAsync(user.Id, cancellationToken)`.
  2. Trong `PasswordLoginCommandHandler.cs`: Đảm bảo không có lời gọi tương tự.
  3. Kết quả: Sau khi đăng nhập, sinh viên chỉ sở hữu JWT token và quyền `student`. Bảng `class_enrollments` chỉ được thêm bản ghi khi sinh viên chủ động gọi API `POST /api/v1/student/classes/join` với mật khẩu lớp hợp lệ.

#### 5. Ma Trận 5 Ca Biên Kỹ Thuật (Edge Cases)
| Ca biên | Kịch bản chi tiết | Cơ chế xử lý kỹ thuật | Mã HTTP / Hành vi |
|:---|:---|:---|:---|
| **Null/Empty** | Sinh viên gửi chuỗi mật khẩu lớp rỗng hoặc null. | FluentValidation bắt lỗi `NotEmpty`. | `HTTP 400 Bad Request` |
| **Boundary** | Mật khẩu lớp khi tạo ngắn hơn 6 ký tự. | Validation chặn với độ dài tối thiểu $\ge 6$. | `HTTP 422 Unprocessable Entity` |
| **Empty Collections** | Sinh viên mới chưa ghi danh vào bất kỳ lớp nào. | `GET /my-classes` trả về danh sách rỗng `[]`. | `HTTP 200 OK` |
| **Concurrency** | Sinh viên click nút tham gia lớp 2 lần cực nhanh. | Khóa chính `PRIMARY KEY (class_id, student_id)` trong DB chặn vi phạm Unique. | Request 2 báo `HTTP 409 Conflict` |
| **Network/Timeout** | Gián đoạn kết nối khi đang băm BCrypt mật khẩu. | Bắt timeout an toàn tại tầng Application. | `HTTP 500 Internal Server Error` |

#### 6. Tiêu Chí Nghiệm Thu (Acceptance Criteria)
- [ ] Sinh viên mới đăng nhập lần đầu tiên có `my-classes` rỗng 100%.
- [ ] Sinh viên chỉ ghi danh thành công khi gửi đúng `joinCode` và `password`.
- [ ] Giảng viên không thể tạo lớp cho môn học mà mình chưa được Trưởng BM phân công trong `course_lecturers`.
- [ ] **Tham chiếu Mở rộng Tuần 9 (`Week 9 Class Assessment Roadmap`):** Khi mở rộng quản trị Chương trình đào tạo (`AcademicProgram` — bảng `academic_programs`), Đợt mở môn theo học kỳ (`CourseOffering` — bảng `course_offerings`), và nâng cấp cấu hình ca thi trong lớp (`InClassExamShift` — đa chế độ bốc câu hỏi, gia hạn `ExtendTime`, mở lại ca `Reopen`, xuất điểm lớp, khóa sửa câu hỏi khi ca thi đang chạy), đội ngũ Backend đối chiếu trực tiếp tại [`CLASS_ASSESSMENT_EXECUTION_PLAN.md`](./week9/CLASS_ASSESSMENT_EXECUTION_PLAN.md).

---

### ⏳ TASK 5 (R6): Triển Khai Phân Hệ Ca Thi Vấn Đáp Trong Lớp (MF-05) & Cơ Chế Mật Khẩu Xoay Vòng 5 Phút (TOTP)
* **Mã định danh task:** `BE-TASK-05` (Phân hệ: `BE-V13` — Thuộc Yêu cầu R6 & Luồng MF-05)
* **Trạng thái thực hiện:** `⏳ TODO / CẦN BE TRIỂN KHAI`
* **Mức độ ưu tiên:** Cao (P1 — Đánh giá quá trình Quiz/Progress Test trực tiếp trên lớp học)
* **Kỹ sư phụ trách:** 🧑 **Nguyễn Quang Thành & Nguyễn Trọng Tốt**

#### 1. Mục tiêu nghiệp vụ (Business Objectives)
1. **Đánh giá quá trình (Formative Assessment):** Cho phép Giảng viên tổ chức kiểm tra vấn đáp lấy điểm thành phần cho lớp học phần mình phụ trách (`classes`).
2. **Cấu hình Ca thi Linh hoạt (Flexible Shift Configuration):**
   - Đặt tên ca thi `shift_name VARCHAR(150)` và mô tả `description TEXT`.
   - **Chế độ nguồn câu hỏi (`question_pool_mode`):** Hỗ trợ 2 chế độ:
     * `ALL_COURSE_BANK`: Hệ thống tự động bốc ngẫu nhiên $N$ câu từ toàn bộ ngân hàng câu hỏi ôn tập (`practice_questions` đã `APPROVED`) của môn học (cô lập tuyệt đối kho đề R8, không fallback sang `exam_questions`).
     * `SELECTED_POOL`: Giảng viên khoanh vùng danh sách câu hỏi nguồn (`selected_question_ids UUID[]` từ `practice_questions` đã `APPROVED`), hệ thống bốc $N$ câu từ tập khoanh vùng.
   - **Thuật toán Anti-Collision:** Tự động phân bổ đề chống trùng lặp giữa các sinh viên ngồi cạnh nhau dựa trên tần suất câu hỏi xuất hiện trong ca thi (Least-Frequently-Assigned + CSPRNG).
   - **Tổng số câu hỏi mỗi SV (`total_questions_per_student`):** Từ 1–10 câu (mặc định 3 câu).
   - **Cấu hình Câu hỏi phụ Follow-up (`has_follow_up` & `max_follow_up_questions`):** Chọn Có/Không Follow-up ngay lúc tạo bài kiểm tra, số câu phụ từ 1–5 câu (mặc định 2 khi bật).
3. **Cơ chế Mật khẩu Xoay vòng 5 Phút (TOTP 300s):** Tự động sinh mã 6 chữ số ngẫu nhiên chiếu lên bảng lớp học; xoay vòng sau mỗi 5 phút kèm **khoảng gia hạn an toàn (Grace Period) 30–60 giây** cho mã liền kề trước đó để chống trễ mạng và độ lệch đồng hồ client-server.
4. **Hàng đợi Chấm Song Song & Publish Điểm Tức Thì (Zero Bottleneck):**
   - Nộp bài áp dụng **Persist-First (< 100ms)** lưu vào `in_class_exam_submissions` (`status = 'SUBMITTED'`), đẩy vào `BoundedChannel<GradingTask>(1000)`.
   - `GradingQueueWorker` chạy song song đa luồng (`MaxDegreeOfParallelism = 12`) gọi Gemini 1.5 Flash CoT theo Rubric 10.0đ.
   - Tự động **Publish điểm song song theo thời gian thực** qua SignalR (`InClassSubmissionGraded`) cho Sinh viên và Dashboard Giảng viên ngay khi có kết quả, không cần đợi cả lớp nộp xong.
5. **Tiết kiệm tài nguyên hạ tầng:** Vấn đáp trên lớp **TUYỆT ĐỐI KHÔNG LƯU AUDIO LÊN CLOUDFLARE R2**, chỉ lưu transcript bài nói; Giảng viên hậu kiểm bảng điểm, chỉnh sửa điểm và chốt bảng điểm (`FINALIZED`) kích hoạt One-Way Lock.

#### 2. Thiết kế CSDL 3NF Chi Tiết (PostgreSQL 16)
* **Bảng 1:** `in_class_exam_shifts`
  - `id UUID PRIMARY KEY DEFAULT gen_random_uuid()`
  - `class_id UUID NOT NULL REFERENCES classes(id) ON DELETE CASCADE`
  - `lecturer_id UUID NOT NULL REFERENCES users(id) ON DELETE RESTRICT`
  - `shift_name VARCHAR(150) NOT NULL`: Tên ca thi (vd "Progress Test 1 - Clean Architecture").
  - `description TEXT NULL`: Mô tả bài kiểm tra.
  - `question_pool_mode VARCHAR(30) NOT NULL DEFAULT 'ALL_COURSE_BANK'`: CHECK in (`'ALL_COURSE_BANK'`, `'SELECTED_POOL'`).
  - `selected_question_ids UUID[] NULL`: Danh sách ID câu hỏi nguồn nếu chọn `SELECTED_POOL`.
  - `total_questions_per_student INT NOT NULL DEFAULT 3`: CHECK `BETWEEN 1 AND 10`.
  - `start_time TIMESTAMPTZ NOT NULL`, `end_time TIMESTAMPTZ NOT NULL`: CHECK `end_time > start_time`.
  - `duration_minutes INT NOT NULL DEFAULT 15`: CHECK `BETWEEN 5 AND 180`.
  - `rubric_id UUID NULL REFERENCES rubrics(id) ON DELETE SET NULL`.
  - `has_follow_up BOOLEAN NOT NULL DEFAULT false`.
  - `max_follow_up_questions INT NOT NULL DEFAULT 2`: CHECK `BETWEEN 0 AND 5`.
  - `is_auto_publish_grades BOOLEAN NOT NULL DEFAULT true`.
  - `transcript_buffer_seconds INT NOT NULL DEFAULT 60`: CHECK `BETWEEN 10 AND 300`.
  - `allow_transcript_edit BOOLEAN NOT NULL DEFAULT true`.
  - `totp_secret_key VARCHAR(128) NOT NULL`.
  - `status VARCHAR(20) NOT NULL DEFAULT 'SCHEDULED'`: CHECK in (`'SCHEDULED'`, `'IN_PROGRESS'`, `'FINALIZED'`, `'CANCELLED'`).
  - `is_locked BOOLEAN NOT NULL DEFAULT false`.
  - `finalized_at TIMESTAMPTZ NULL`, `created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP`, `updated_at TIMESTAMPTZ NULL`.
  - *Chỉ mục ESR:* `ix_in_class_exam_shifts_class_status_created`: `(class_id, status, created_at DESC)`.
* **Bảng 2:** `in_class_shift_passwords`
  - `id UUID PRIMARY KEY DEFAULT gen_random_uuid()`
  - `shift_id UUID NOT NULL REFERENCES in_class_exam_shifts(id) ON DELETE CASCADE`
  - `plain_code VARCHAR(10) NOT NULL`: Mã OTP 6 số hiển thị.
  - `password_hash VARCHAR(255) NOT NULL`: Chuỗi băm an toàn.
  - `valid_from TIMESTAMPTZ NOT NULL`, `valid_to TIMESTAMPTZ NOT NULL`, `grace_until TIMESTAMPTZ NOT NULL`.
  - `created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP`.
  - *Chỉ mục ESR & Unique:* `uq_in_class_shift_passwords_shift_valid_from`: `UNIQUE (shift_id, valid_from)` (chống race condition khi polling mã OTP đồng thời); `ix_in_class_shift_passwords_shift_valid`: `(shift_id, valid_from, grace_until)`.
* **Bảng 3:** `in_class_shift_participants`
  - `id UUID PRIMARY KEY DEFAULT gen_random_uuid()`
  - `shift_id UUID NOT NULL REFERENCES in_class_exam_shifts(id) ON DELETE CASCADE`
  - `student_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE`
  - `assigned_question_id UUID NULL REFERENCES practice_questions(id) ON DELETE SET NULL`: Khóa ngoại chuẩn 3NF tới câu hỏi đã bốc.
  - `assigned_question_ids UUID[] NOT NULL DEFAULT '{}'`: Danh sách $N$ câu hỏi Anti-Collision đã bốc cho sinh viên.
  - `joined_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP`.
  - `total_ai_score NUMERIC(4,2) NULL`, `total_final_score NUMERIC(4,2) NULL`.
  - `status VARCHAR(20) NOT NULL DEFAULT 'JOINED'`: CHECK in (`'JOINED'`, `'SUBMITTED'`, `'ABSENT'`).
  - *Constraints:* `uq_in_class_shift_participants_shift_student`: `UNIQUE (shift_id, student_id)`.
* **Bảng 4:** `in_class_exam_submissions` (Zero R2 Audio)
  - `id UUID PRIMARY KEY DEFAULT gen_random_uuid()`
  - `shift_id UUID NOT NULL REFERENCES in_class_exam_shifts(id) ON DELETE CASCADE`
  - `student_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE`
  - `question_id UUID NOT NULL REFERENCES practice_questions(id) ON DELETE RESTRICT`
  - `transcript TEXT NOT NULL`: Phiên âm bài nói (thu từ STT client).
  - `is_follow_up BOOLEAN NOT NULL DEFAULT false`.
  - `parent_submission_id UUID NULL REFERENCES in_class_exam_submissions(id) ON DELETE CASCADE`.
  - `follow_up_index INT NOT NULL DEFAULT 0`: CHECK `BETWEEN 0 AND 5`.
  - `follow_up_prompt TEXT NULL`.
  - `ai_score NUMERIC(4,2) NULL`, `ai_feedback TEXT NULL`, `ai_confidence_score NUMERIC(3,2) NULL`, `cot_trace TEXT NULL`.
  - `final_score NUMERIC(4,2) NULL`, `lecturer_notes TEXT NULL`.
  - `status VARCHAR(20) NOT NULL DEFAULT 'SUBMITTED'`: CHECK in (`'SUBMITTED'`, `'AI_GRADED'`, `'AUDITED'`).
  - `submitted_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP`, `graded_at TIMESTAMPTZ NULL`.
  - *Constraints:* `uq_in_class_exam_submissions_shift_student`: `UNIQUE (shift_id, student_id, question_id, follow_up_index)`.

#### 3. Đặc tả API Endpoints (RESTful RFC 7807) & SignalR
* `POST /api/v1/in-class-shifts` | Roles: `lecturer` | Tạo ca thi (nhận `shiftName`, `description`, `questionPoolMode`, `selectedQuestionIds`, `totalQuestionsPerStudent`, `hasFollowUp`, `maxFollowUpQuestions`, `isAutoPublishGrades`).
* `POST /api/v1/in-class-shifts/{id}/open` | Roles: `lecturer` | Mở ca thi và kích hoạt sinh mã TOTP đầu tiên.
* `GET /api/v1/in-class-shifts/{id}/current-password` | Roles: `lecturer` | Lấy mã OTP 6 số hiện tại, thời gian còn lại (giây) để chiếu bảng.
* `POST /api/v1/in-class-shifts/{id}/enter` | Roles: `student` | Body: `{ "otpCode": "A8F3K9" }` $\to$ Thẩm định OTP $\to$ Kích hoạt Anti-Collision bốc $N$ câu hỏi $\to$ Trả về `assignedQuestions: [...]`.
* `POST /api/v1/in-class-shifts/{id}/submit-answer` | Roles: `student` | Nộp transcript bài nói (Persist-First `< 100ms`, Zero-R2) $\to$ Đẩy `BoundedChannel` chấm song song `MaxDegreeOfParallelism = 12`.
* `GET /api/v1/in-class-shifts/{id}/submissions` | Roles: `lecturer` | Xem toàn bộ bài nộp và gợi ý chấm của AI.
* `PUT /api/v1/in-class-shifts/{id}/submissions/{subId}/grade` | Roles: `lecturer` | Điều chỉnh điểm: `{ "finalScore": 8.5, "lecturerNotes": "Tốt" }`.
* `POST /api/v1/in-class-shifts/{id}/finalize-grades` | Roles: `lecturer` | Chốt bảng điểm ca thi $\to$ `status = 'FINALIZED'`, kích hoạt One-Way Lock (`is_locked = true`).
* **SignalR Event `InClassSubmissionGraded`:** Phát sóng tức thì khi Worker AI chấm xong từng bài tới Client Sinh viên và Dashboard Giảng viên.

#### 4. Đặc tả Chi Tiết Thuật Toán Mật Khẩu Xoay Vòng 5 Phút (TOTP) & Grace Period
* **Thông số thuật toán:**
  - Chu kỳ xoay vòng: $T_X = 300\text{ giây}$ (5 phút).
  - Khoảng gia hạn an toàn: $\Delta t_{\text{grace}} = 60\text{ giây}$.
  - Thời điểm mở ca thi: $T_{\text{start}} = \text{Shift.opened\_at}$.
* **Công thức xác định cửa sổ thời gian chu kỳ thứ $k$:**
  $$k = \left\lfloor \frac{t - T_{\text{start}}}{T_X} \right\rfloor$$
  - Mốc bắt đầu hiệu lực: $\text{valid\_from}_k = T_{\text{start}} + k \times T_X$
  - Mốc hết hiệu lực chuẩn: $\text{valid\_to}_k = \text{valid\_from}_k + T_X$
  - Mốc hết hạn gia hạn an toàn: $\text{grace\_until}_k = \text{valid\_to}_k + \Delta t_{\text{grace}}$
* **Sinh mã bảo mật:**
  $$\text{Code}_k = \text{CryptoSecureRandomInt}(100000, 999999)\text{ định dạng 6 chữ số}$$
  Lưu `plain_code = Code_k`, `password_hash = BCrypt.Hash(Code_k)` vào `in_class_shift_passwords`.
* **Thuật toán thẩm định khi sinh viên gửi mã $C$ tại thời điểm $t_{\text{now}}$:**
  1. Kiểm tra sinh viên có trong `class_enrollments` không $\to$ Nếu không $\to$ `HTTP 403 Forbidden`.
  2. Tìm trong `in_class_shift_passwords` các bản ghi thỏa mãn:
     $$\text{valid\_from} \le t_{\text{now}} \le \text{grace\_until}$$
     *(Lưu ý: Trong khoảng 60s gia hạn, luôn có đồng thời 2 bản ghi thỏa mãn: mã chu kỳ hiện tại $k$ và mã chu kỳ liền kề trước $k-1$)*.
  3. So khớp mã $C$:
     - Nếu khớp mã chu kỳ hiện tại $k$ ($\text{valid\_from}_k \le t_{\text{now}} \le \text{valid\_to}_k$): **Hợp lệ chuẩn**.
     - Nếu khớp mã chu kỳ trước $k-1$ ($\text{valid\_to}_{k-1} < t_{\text{now}} \le \text{grace\_until}_{k-1}$): **Chấp nhận qua Grace Period**, trả về kèm cờ cảnh báo `isGracePeriodActive: true` để client nhắc sinh viên cập nhật mã mới.
     - Nếu không khớp hoặc $t_{\text{now}} > \text{grace\_until} \to$ Từ chối **`HTTP 401 Unauthorized`** (*"Mật khẩu ca thi không hợp lệ hoặc đã hết hạn"*).

#### 5. Thuật Toán Anti-Collision, Hàng Đợi Chấm Song Song Đa Luồng & Tối Ưu Hiệu Năng
* **Thuật toán Anti-Collision & Bốc đề 2 Pha Nhẹ RAM (`EnterInClassShiftCommandHandler`):**
  - **Pha 1 (Lọc ID & Tính Tần Suất):** Khi sinh viên nhập đúng mã OTP tham gia ca thi, Backend chỉ truy vấn danh sách `Guid` câu hỏi (`Select(q => q.Id)`) và danh sách ID đã bốc trong ca để tính toán tần suất $O(K)$, hoàn toàn không nạp toàn bộ nội dung text đề bài, barem rubric hay đáp án mẫu vào RAM.
  - **Pha 2 (Nạp Đầy Đủ $N$ Câu Đã Chọn):** Sắp xếp tăng dần theo tần suất, kết hợp xáo trộn ngẫu nhiên CSPRNG để bốc ra đúng $N$ câu hỏi có tần suất thấp nhất, sau đó mới nạp chi tiết nội dung đề bài của đúng $N$ câu hỏi đó để lưu CSDL và trả về cho sinh viên (giảm thiểu 90% tải RAM).
* **Hàng đợi Chấm Song Song Đa Luồng (MaxDegreeOfParallelism = 12):**
  - Persist-First `< 100ms` lưu `in_class_exam_submissions` trạng thái `SUBMITTED`.
  - Đẩy vào `BoundedChannel<GradingTask>(1000, FullMode.Wait)`.
  - Cả `OfficialExamGradingWorker` (thi thật) và `GradingQueueWorker` (luyện tập, thi thử, ca thi lớp) đều cấu hình xử lý song song bất đồng bộ qua `Parallel.ForEachAsync` (`MaxDegreeOfParallelism = 12`) gọi Gemini 1.5 Flash/Pro CoT theo Rubric 10.0đ.
  - Polly Retry 3 lần (2s, 4s, 8s) + DLQ 5 phút nếu lỗi kiệt quệ.
  - SignalR phát sự kiện `InClassSubmissionGraded` publish điểm thời gian thực.
* **Tối Ưu Hóa Truy Vấn Dashboard Sinh Viên & Seeder Chuẩn FLM:**
  - `GetStudentDashboardQueryHandler`: Gom cụm cập nhật `SaveChangesAsync` 1 lần duy nhất trong toàn bộ xử lý, và truy vấn danh sách chi tiết tiêu chí `AiEvaluationDetails` trực tiếp thông qua `d.Evaluation.StudentId` ($O(\log N)$) thay vì dùng các phép OR JOIN nhiều bảng phức tạp.
  - FLM Seeder chuẩn: Tự động nạp dữ liệu chuẩn hóa của 3 môn học FLM (`PRF192`, `CSD201`, `DBI202`) cùng 90 câu hỏi chuẩn bám sát ma trận CLO 3NF và barem rubric 10.0đ.

#### 6. Ma Trận 5 Ca Biên Kỹ Thuật (Edge Cases)
| Ca biên | Kịch bản chi tiết | Cơ chế xử lý kỹ thuật | Mã HTTP / Hành vi |
|:---|:---|:---|:---|
| **Null/Empty** | Sinh viên gửi mã rỗng `""`. | FluentValidation chặn chuỗi rỗng. | `HTTP 400 Bad Request` |
| **Boundary** | Sinh viên nhập mã ở giây thứ 305 (sau khi mã bảng vừa đổi). | Chấp nhận qua cửa sổ Grace Period ($\le 360\text{s}$). | `HTTP 200 OK` (Grace Active) |
| **Empty Collections** | Giảng viên tạo ca thi với `SELECTED_POOL` nhưng mảng `selectedQuestionIds` rỗng hoặc ít hơn $N$. | Chặn kiểm tra hợp lệ trước khi tạo. | `HTTP 422 Unprocessable Entity` |
| **Concurrency** | 40–120 sinh viên cùng nộp bài trong 10 giây. | Persist-First `< 100ms`, BoundedChannel 1,000 slots RAM, xử lý song song 12 luồng, không nghẽn DB. | `HTTP 200 OK` (SUBMITTED) |
| **Network/Timeout** | Mất mạng trong lúc thu âm bài nói. | Client lưu tạm transcript trên LocalStorage, tự động nộp lại khi có mạng. | Nộp lại Idempotent thành công |

#### 7. Tiêu Chí Nghiệm Thu (Acceptance Criteria)
- [ ] Giảng viên cấu hình đầy đủ tên, mô tả, chọn nguồn đề `ALL_COURSE_BANK` / `SELECTED_POOL`, tổng số câu $1–10$, có/không follow-up $1–5$ câu.
- [ ] Sinh viên nhập đúng OTP nhận danh sách $N$ câu hỏi theo thuật toán Anti-Collision.
- [ ] Mật khẩu hết hạn sau $300 + 60\text{s}$ bị từ chối `HTTP 401 Unauthorized`.
- [ ] Mật khẩu trong khoảng $300\text{s} \to 360\text{s}$ được chấp nhận qua Grace Period.
- [ ] Hàng đợi chấm song song `MaxDegreeOfParallelism = 12` hoạt động ổn định khi nhiều sinh viên nộp cùng lúc.
- [ ] Điểm số và feedback được publish tức thì qua SignalR `InClassSubmissionGraded`.
- [ ] Ca thi sau khi `FINALIZED` khóa toàn bộ thao tác sửa điểm One-Way Lock (`HTTP 403 Forbidden`).
- [ ] Tuyệt đối không có file audio nào được upload lên Cloudflare R2 cho luồng này (Zero R2 Audio).

---

### ⏳ TASK 6 (R7): Chuẩn Hóa Quy Trình Phúc Khảo Nội Bộ 5 Bước & Giải Thuật Cân Bằng Tải Phân Bổ Bài Chấm $\le 24$ Bài/GV
* **Mã định danh task:** `BE-TASK-06` (Phân hệ: `BE-4.6` — Thuộc Yêu cầu R7 & Luồng MF-04)
* **Trạng thái thực hiện:** `⏳ TODO / CẦN BE TRIỂN KHAI`
* **Mức độ ưu tiên:** Khẩn cấp (P0 — Pháp lý thi cử và quyền khiếu nại của sinh viên)
* **Kỹ sư phụ trách:** 🧑 **Nguyễn Trọng Tốt** (Backend Developer & QA Lead)

#### 1. Mục tiêu nghiệp vụ (Business Objectives)
1. **Chuẩn hóa Quy trình Phúc khảo 5 bước theo Quy chế Khảo thí FPTU (Single Source of Truth):**
   - **Bước 1 (Sinh viên):** Xem điểm trên Student Portal sau khi công bố điểm, nộp đơn khiếu nại trực tuyến (`POST /api/v1/student/appeals`) $\to$ Trạng thái `SUBMITTED_TO_EXAM_OFFICE`.
   - **Bước 2 (Thông báo song song & Thẩm tra):** Hệ thống phát thông báo in-app đồng thời tới **Cán bộ Khảo thí** (để thụ lý) và **Trưởng Bộ Môn** (để theo dõi chuyên môn học thuật). Khảo thí thẩm tra đơn trong thời hạn 48h.
   - **Bước 3 (Khảo thí phân công Giảng viên chấm lại):** Cán bộ Khảo thí xem trước đề xuất phân bổ tối ưu $\le 24$ bài/GV (`GET .../allocation-proposal`), điều chỉnh nếu cần, và phát lệnh phân công (`POST .../batch-assign`), **LOẠI TRỪ 100% GIÁM KHẢO CHẤM CA THI BAN ĐẦU** $\to$ Trạng thái `ASSIGNED_TO_LECTURER`.
   - **Bước 4 (Giảng viên chấm lại độc lập):** Giảng viên được phân công mở Evidence Panel, nghe lại file ghi âm bất biến `STT_MSSV.webm` trên R2, đối chiếu transcript và đề xuất điểm kèm biên bản giải trình $\ge 10$ ký tự (`PUT .../{id}/regrade`) $\to$ Trạng thái `REGRADED_BY_LECTURER`.
   - **Bước 5 (Khảo thí thẩm định & phê duyệt quyết định cuối cùng):** Cán bộ Khảo thí thẩm định kết quả và ra quyết định phê duyệt (`POST .../{id}/decision`). Nếu chấp thuận (`APPROVED`), mở đường ngoại lệ One-Way Lock duy nhất cập nhật điểm chính thức vào vé thi và khóa lại $\to$ Trạng thái `APPROVED`.
2. **Giải thuật phân bổ bài phúc khảo $\le 24$ bài/GV:** Xây dựng công thức toán học phân bổ bài chấm cân bằng tải cho cả 2 trường hợp: Đủ dung lượng tiếp nhận ($N \le G \times 24$) và Quá tải dung lượng tiếp nhận ($N > G \times 24$), kèm cơ chế chống chia lẻ quá ít (anti-micro-allocation) và cổng điều chỉnh thủ công.

#### 2. Thiết kế CSDL 3NF Chi Tiết (PostgreSQL 16)
* **Bảng:** `appeal_requests`
* **Cấu trúc cột & Kiểu dữ liệu:**
  - `id UUID PRIMARY KEY DEFAULT gen_random_uuid()`
  - `ticket_id UUID NOT NULL REFERENCES student_exam_tickets(id)`: Vé thi được phúc khảo.
  - `student_id UUID NOT NULL REFERENCES users(id)`: Sinh viên khiếu nại.
  - `shift_id UUID NOT NULL REFERENCES real_exam_session_shifts(id)`: Ca thi của bài thi.
  - `reason TEXT NOT NULL`: Lý do sinh viên xin phúc khảo.
  - `status VARCHAR(40) NOT NULL DEFAULT 'SUBMITTED_TO_EXAM_OFFICE'`: Trạng thái đơn.
  - `regrade_lecturer_id UUID NULL REFERENCES users(id)`: Giảng viên chấm lại độc lập.
  - `original_score NUMERIC(4,2) NOT NULL`: Điểm ban đầu của bài thi.
  - `proposed_score NUMERIC(4,2) NULL`: Điểm do Giảng viên chấm lại đề xuất.
  - `regrade_notes TEXT NULL`: Biên bản giải trình chuyên môn của Giảng viên chấm lại.
  - `final_score NUMERIC(4,2) NULL`: Điểm chính thức được Khảo thí phê duyệt.
  - `decision_notes TEXT NULL`: Biên bản quyết định của Cán bộ Khảo thí.
  - `assigned_at TIMESTAMPTZ NULL`, `assigned_by_user_id UUID NULL REFERENCES users(id)`
  - `regraded_at TIMESTAMPTZ NULL`
  - `decided_at TIMESTAMPTZ NULL`, `decided_by_user_id UUID NULL REFERENCES users(id)`
  - `created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP`
* **Ràng buộc toàn vẹn (Constraints):**
  - `ck_appeal_requests_status`: `CHECK (status IN ('SUBMITTED_TO_EXAM_OFFICE', 'ASSIGNED_TO_LECTURER', 'REGRADED_BY_LECTURER', 'APPROVED', 'REJECTED', 'PENDING', 'IN_REVIEW'))`
  - `ck_appeal_regrade_notes_length`: `CHECK (regrade_notes IS NULL OR length(trim(regrade_notes)) >= 10)`
  - `ck_appeal_scores_range`: `CHECK ((proposed_score IS NULL OR (proposed_score >= 0 AND proposed_score <= 10.00)) AND (final_score IS NULL OR (final_score >= 0 AND final_score <= 10.00)))`
* **Chỉ mục tối ưu ESR:**
  - `ix_appeals_status_shift`: `(status, shift_id, created_at DESC)`
  - `ix_appeals_lecturer_status`: `(regrade_lecturer_id, status, assigned_at DESC)`
  - `ix_appeals_ticket`: `(ticket_id)` — Khóa tìm kiếm đơn theo vé thi.

#### 3. Đặc tả API Endpoints (RESTful RFC 7807)
* **Endpoint 1: Sinh viên nộp đơn phúc khảo**
  - `POST /api/v1/student/appeals` | Roles: `student`
  - Body: `{ "ticketId": "uuid", "reason": "Em xin phúc khảo câu 3 về tính chất ACID của database" }`
  - Response 201 Created: Trạng thái `SUBMITTED_TO_EXAM_OFFICE`. Bắn thông báo in-app cho Khảo thí và Trưởng BM.
* **Endpoint 2: Khảo thí xem đề xuất phân bổ $\le 24$ bài/GV**
  - `GET /api/v1/examination-officer/appeals/allocation-proposal` | Roles: `examination_officer,admin`
  - Query: `courseId` (tùy chọn), `shiftId` (tùy chọn).
  - Response 200 OK: Trả về ma trận phân bổ tối ưu theo giải thuật tham lam cân bằng tải.
* **Endpoint 3: Khảo thí phát lệnh phân công chấm lại đồng loạt**
  - `POST /api/v1/examination-officer/appeals/batch-assign` | Roles: `examination_officer,admin`
  - Body: `{ "assignments": [{ "appealId": "uuid1", "lecturerId": "uuidA" }, { "appealId": "uuid2", "lecturerId": "uuidB" }] }`
  - Response 200 OK: Cập nhật `status = 'ASSIGNED_TO_LECTURER'`, gán `regrade_lecturer_id`.
* **Endpoint 4: Giảng viên chấm lại nhập đề xuất điểm**
  - `PUT /api/v1/lecturer/appeals/{id}/regrade` | Roles: `lecturer`
  - Body: `{ "proposedScore": 8.5, "regradeNotes": "Sinh viên phân tích đúng Isolation Level, đồng ý nâng điểm" }`
  - Response 200 OK: Cập nhật `status = 'REGRADED_BY_LECTURER'`.
* **Endpoint 5: Khảo thí phê duyệt quyết định cuối cùng**
  - `POST /api/v1/examination-officer/appeals/{id}/decision` | Roles: `examination_officer,admin`
  - Body: `{ "decision": "APPROVED", "decisionNotes": "Phê duyệt điều chỉnh điểm theo đề xuất của GV chấm lại" }`
  - Response 200 OK: Mở ngoại lệ One-Way Lock, cập nhật điểm chính thức vào vé thi.

#### 4. Đặc Tả Chi Tiết Thuật Toán Phân Bổ Bài Phúc Khảo $\le 24$ Bài/GV
* **Định nghĩa không gian bài toán:**
  - Định mức trần: $M = 24$ bài/GV.
  - Tập hợp đơn phúc khảo cần phân bổ: $A = \{a_1, a_2, \dots, a_N\}$ với tổng số đơn $N = |A|$.
  - Giám khảo ca thi ban đầu của đơn $a_j$: $E(a_j) = a_j.\text{Shift}.\text{ExaminerUserId}$.
  - Tập Giảng viên thuộc môn học: $L_{\text{course}} = \{g_1, g_2, \dots, g_G\}$ lấy từ `course_lecturers`.
  - Tập Giảng viên đủ tư cách tiếp nhận đơn $a_j$:
    $$L_{\text{eligible}}(a_j) = \{g \in L_{\text{course}} \mid g.\text{Id} \neq E(a_j)\}$$
    *(Nguyên tắc độc lập tuyệt đối: 100% loại trừ Giám khảo chấm ca thi ban đầu)*.
  - Tải hiện tại của giảng viên $g_i$: $W(g_i) \in [0, 24]$ là số bài phúc khảo đang thụ lý (`ASSIGNED_TO_LECTURER` hoặc `REGRADED_BY_LECTURER`).
  - Dung lượng khả dụng còn lại của $g_i$: $C(g_i) = \max(0, 24 - W(g_i))$.
  - Tổng dung lượng khả dụng của toàn bộ giảng viên đủ điều kiện:
    $$C_{\text{total}} = \sum_{g_i \in L_{\text{eligible}}} C(g_i)$$

* **Trường hợp 1: Đủ dung lượng tiếp nhận ($N \le C_{\text{total}}$):**
  - Hệ thống áp dụng **Giải thuật Tham lam Cân bằng tải (Greedy Min-Load Balancing)** kèm **Ngưỡng chống chia lẻ quá ít (Anti-Micro-Allocation Threshold)**:
    1. *Ngưỡng chống chia lẻ:* Nếu chia đều $N$ bài cho tất cả $G$ giảng viên khiến mỗi người chỉ nhận 1 hoặc 2 bài ($N/G < 3$), việc đôn đốc chấm bị phân mảnh. Thuật toán quy định kích thước lô tối thiểu $B_{\min} = 3$ bài/GV.
       Số giảng viên tối ưu tham gia đợt chấm:
       $$K = \min\left(|L_{\text{eligible}}|, \max\left(1, \left\lfloor \frac{N}{B_{\min}} \right\rfloor\right)\right)$$
       Chọn ra $K$ giảng viên có tải hiện tại $W(g_i)$ thấp nhất và $C(g_i) \ge \lceil N/K \rceil$.
    2. *Vòng lặp gán bài tham lam:* Lặp qua từng đơn $a_j \in A$:
       Tìm giảng viên $g^*$ trong nhóm $K$ giảng viên thỏa mãn:
       $$g^* = \arg\min_{g \in K, g.\text{Id} \neq E(a_j), C(g) > 0} (W(g))$$
       Gán $a_j$ cho $g^*$; cập nhật $W(g^*) \leftarrow W(g^*) + 1$ và $C(g^*) \leftarrow C(g^*) - 1$.
    3. *Cổng điều chỉnh thủ công (Manual Adjustment Gate):* API trả về danh sách đề xuất. Cán bộ Khảo thí có quyền điều chỉnh thay đổi giảng viên cho từng bài trên giao diện trước khi bấm xác nhận `POST /batch-assign`.

* **Trường hợp 2: Quá tải dung lượng tiếp nhận ($N > C_{\text{total}}$):**
  - Hệ thống phân bổ tối đa cho $G$ giảng viên đến khi toàn bộ đạt trần $W(g_i) = 24$ (tổng số bài phân bổ được là $C_{\text{total}}$).
  - Số bài thặng dư $N_{\text{excess}} = N - C_{\text{total}}$ được giữ nguyên trạng thái `SUBMITTED_TO_EXAM_OFFICE`, đưa vào danh sách chờ `unallocatedAppealIds` kèm cờ cảnh báo `OVER_CAPACITY_PENDING`.
  - Hệ thống gửi cảnh báo in-app tới Khảo thí và Trưởng BM yêu cầu:
    1. Bổ sung thêm Giảng viên vào môn học (`course_lecturers`), hoặc
    2. Chia đợt chấm phúc khảo thành các Batches kế tiếp sau khi đợt 1 hoàn tất.

#### 5. Ma Trận 5 Ca Biên Kỹ Thuật (Edge Cases)
| Ca biên | Kịch bản chi tiết | Cơ chế xử lý kỹ thuật | Mã HTTP / Hành vi |
|:---|:---|:---|:---|
| **Null/Empty** | Giảng viên chấm lại không nhập `regradeNotes` hoặc nhập $< 10$ ký tự. | DB Check Constraint và Validator chặn ghi nhận. | `HTTP 422 Unprocessable Entity` |
| **Boundary** | Phân công bài thi thứ 25 cho cùng một giảng viên ($W(g) > 24$). | Kiểm tra định mức trần chặn ngay tại Handler. | `HTTP 422 Unprocessable Entity` |
| **Empty Collections** | Môn học chỉ có 1 giảng viên duy nhất (chính là Giám khảo ca thi). | Danh sách $L_{\text{eligible}}$ rỗng; hệ thống báo lỗi không có GV đủ điều kiện. | `HTTP 400 Bad Request` |
| **Concurrency** | 2 Cán bộ Khảo thí cùng bấm phê duyệt quyết định phúc khảo 1 bài thi. | Sử dụng optimistic concurrency; `OneWayLockInterceptor` bảo vệ chỉ cập nhật 1 lần. | `HTTP 409 Conflict` |
| **Network/Timeout** | Timeout khi gọi SignalR thông báo quyết định cho SV. | Transaction cập nhật DB thành công trước; SignalR retry ngầm trong background. | `HTTP 200 OK` |

#### 6. Tiêu Chí Nghiệm Thu (Acceptance Criteria)
- [ ] Giám khảo chấm ca thi ban đầu (`examiner_user_id`) không bao giờ được phân công chấm lại bài thi thuộc ca của mình.
- [ ] Không có bất kỳ giảng viên nào bị phân công vượt quá 24 bài phúc khảo đang thụ lý.
- [ ] Điểm số chính thức của vé thi chỉ được cập nhật khi Cán bộ Khảo thí phê duyệt quyết định với trạng thái `APPROVED`.
- [ ] Cập nhật điểm kích hoạt ngoại lệ hợp lệ duy nhất của `OneWayLockInterceptor`, các thao tác sửa điểm ngoài luồng này đều bị chặn 403.

---

### ⏳ TASK 7 (R8): Phân Tách Độc Lập 2 Kho Câu Hỏi & Quy Tắc Bắt Buộc Phân Bổ Đồng Thời Examiner + Proctor Khi Mở Ca Thi
* **Mã định danh task:** `BE-TASK-07` (Phân hệ: `BE-3.3` & `BE-4.1-SHIFT` — Thuộc Yêu cầu R8)
* **Trạng thái thực hiện:** `⏳ TODO / CẦN BE TRIỂN KHAI`
* **Mức độ ưu tiên:** Cao (P1 — Phân định ngân hàng đề thi và quy tắc an toàn ca thi thật)
* **Kỹ sư phụ trách:** 🧑 **Nguyễn Trọng Tốt & Nguyễn Quang Thành**

#### 1. Mục tiêu nghiệp vụ (Business Objectives)
1. **Phân tách độc lập 2 luồng tạo câu hỏi (R8):**
   - **Luồng Luyện tập & Thi thử (MF-01/02):** Sinh tự do từ CLO môn học (`course_clos` 3NF) hoặc soạn thủ công, lưu độc lập vào bảng `practice_questions`. Phục vụ sinh viên tự do ôn luyện, không đòi hỏi bám cấu trúc đề thi phòng Lab.
   - **Luồng Thi thật phòng Lab (MF-04):** BẮT BUỘC bám sát Đề thi mẫu / Ma trận cấu trúc đề (`exam_structures`, `slot_definitions`, tỷ lệ Bloom) do Trưởng Bộ Môn ban hành đưa xuống, lưu vào bảng `exam_questions`.
2. **Quy tắc Bắt buộc Phân bổ Đồng thời Giám khảo (`examiner_user_id`) và Giám thị (`proctor_user_id`) khi mở ca thi thật (`real_exam_session_shifts`):**
   - Khi Cán bộ Khảo thí tạo ca thi phòng Lab, bắt buộc phải chỉ định đồng thời cả hai nhân sự khác nhau:
     * `proctor_user_id`: Cán bộ coi thi phòng máy (điểm danh, kiểm tra thẻ SV, lập biên bản vi phạm phòng thi).
     * `examiner_user_id`: Giám khảo chuyên môn chấm thi (hậu kiểm trên Evidence Panel, nghe lại audio Cloudflare R2, quyết định điểm).
   - **Ràng buộc phân tách vai trò độc lập:** `proctor_user_id != examiner_user_id` (cùng một người không được vừa coi thi vừa chấm thi trong một ca).

#### 2. Thiết kế CSDL 3NF Chi Tiết (PostgreSQL 16)
* **Bảng:** `practice_questions` (Kho câu hỏi luyện tập/thi thử)
  - `id UUID PRIMARY KEY DEFAULT gen_random_uuid()`
  - `clo_id UUID NULL REFERENCES course_clos(id)`
  - `content TEXT NOT NULL`, `sample_answer TEXT NOT NULL`, `rubric_id UUID NOT NULL REFERENCES rubrics(id)`
  - Hoàn toàn độc lập, không có khóa ngoại tới `exam_structures`.
* **Bảng:** `exam_questions` (Kho câu hỏi thi thật phòng Lab)
  - `id UUID PRIMARY KEY DEFAULT gen_random_uuid()`
  - `clo_id UUID NOT NULL REFERENCES course_clos(id)`
  - `structure_id UUID NOT NULL REFERENCES exam_structures(id)`
  - `bloom_level VARCHAR(20) NOT NULL`
  - *Constraints:* `ck_exam_questions_bloom`: `CHECK (bloom_level IN ('Remember', 'Understand', 'Apply', 'Analyze', 'Evaluate', 'Create'))`.
* **Bảng:** `real_exam_session_shifts` (Cập nhật 2 vai trò nhân sự)
  - `id UUID PRIMARY KEY DEFAULT gen_random_uuid()`
  - `session_id UUID NOT NULL REFERENCES official_exam_sessions(id)`
  - `proctor_user_id UUID NOT NULL REFERENCES users(id)`: Giám thị phòng lab.
  - `examiner_user_id UUID NOT NULL REFERENCES users(id)`: Giám khảo chấm thi / hậu kiểm.
  - `room_lab VARCHAR(50) NOT NULL`
  - `is_exam_published BOOLEAN NOT NULL DEFAULT FALSE`
  - `status VARCHAR(20) NOT NULL DEFAULT 'SCHEDULED'`
  - *Constraints:* `ck_shift_examiner_proctor_diff`: `CHECK (examiner_user_id <> proctor_user_id)`; `ck_shift_status`: `CHECK (status IN ('SCHEDULED', 'IN_PROGRESS', 'COMPLETED', 'CANCELLED'))`.
  - *Chỉ mục ESR:* `ix_shifts_session_status`: `(session_id, status, start_time)`; `ix_shifts_examiner`: `(examiner_user_id, status)`; `ix_shifts_proctor`: `(proctor_user_id, status)`.

#### 3. Đặc tả API Endpoints (RESTful RFC 7807)
* **Endpoint 1: Khảo thí mở ca thi phòng Lab phân bổ đồng thời Examiner + Proctor**
  - `POST /api/v1/examination-officer/official-exams/sessions/{sessionId}/shifts`
  - **Phân quyền:** `[Authorize(Roles = "examination_officer,admin")]`
  - **Request DTO (`OpenExamShiftRequest`):**
    ```json
    {
      "name": "Ca 1 - Phong Lab AL-201",
      "roomLab": "AL-201",
      "startTime": "2026-11-20T08:00:00Z",
      "endTime": "2026-11-20T10:00:00Z",
      "proctorUserId": "11111111-1111-1111-1111-111111111111",
      "examinerUserId": "22222222-2222-2222-2222-222222222222",
      "examInputMode": "VoiceWithTranscriptEdit",
      "transcriptBufferSeconds": 60
    }
    ```
    *Validation:*
    - `proctorUserId` required (UUID); bắt buộc có role `proctor` hoặc `lecturer`.
    - `examinerUserId` required (UUID); bắt buộc có role `lecturer`.
    - `proctorUserId != examinerUserId` (nếu trùng $\to$ chặn `HTTP 422 Unprocessable Entity`).
  - **Response 201 Created:** `ExamShiftResponseDto` gồm đầy đủ `proctorUserId` và `examinerUserId`.
* **Endpoint 2: Soạn câu hỏi thi thật (bắt buộc gắn Slot ma trận)**
  - `POST /api/v1/official-exams/questions` | Roles: `lecturer,department_head,admin`
  - Request DTO bắt buộc có: `cloId` (UUID) và `structureId` (UUID). Kiểm tra câu hỏi phải khớp với một slot còn trống trong cấu trúc đề thi mẫu do Trưởng BM ban hành.
* **Endpoint 3: Soạn câu hỏi ôn tập / thi thử (độc lập)**
  - `POST /api/v1/practice/questions` | Roles: `lecturer,department_head,admin`
  - Request DTO: Cho phép gắn `cloId` (tùy chọn), không yêu cầu `structureId`, lưu trực tiếp vào `practice_questions`.

#### 4. Đặc tả Chi Tiết Logic Xử Lý & Thuật Toán
1. Khi Khảo thí tạo ca thi:
   - Validator kiểm tra `proctorUserId != examinerUserId`.
   - Truy vấn DB kiểm tra `proctorUser` có role `proctor` hoặc `lecturer`; kiểm tra `examinerUser` có role `lecturer`.
   - Nếu không thỏa mãn $\to$ ném `ValidationException` (`HTTP 422`).
2. Giá trị `examiner_user_id` của ca thi được lưu chặt chẽ vào dòng bản ghi `real_exam_session_shifts`.
3. Khi sinh viên nộp đơn phúc khảo: Hệ thống tự động truy xuất `examiner_user_id` của ca thi để **loại trừ 100% Giám khảo này** khỏi danh sách phân công chấm lại ở Task 6.

#### 5. Ma Trận 5 Ca Biên Kỹ Thuật (Edge Cases)
| Ca biên | Kịch bản chi tiết | Cơ chế xử lý kỹ thuật | Mã HTTP / Hành vi |
|:---|:---|:---|:---|
| **Null/Empty** | Request tạo ca thi thiếu `examinerUserId` hoặc `proctorUserId`. | FluentValidation chặn thiếu thuộc tính bắt buộc. | `HTTP 422 Unprocessable Entity` |
| **Boundary** | `proctorUserId` và `examinerUserId` cùng là 1 GUID. | Check Constraint DB và Validator chặn trùng lặp. | `HTTP 422 Unprocessable Entity` |
| **Empty Collections** | Tạo câu hỏi thi thật cho môn học chưa có `exam_structures` nào. | Chặn không cho tạo câu hỏi thi thật khi thiếu ma trận đề mẫu. | `HTTP 400 Bad Request` |
| **Concurrency** | 2 ca thi cùng đặt lịch cùng phòng Lab trong cùng khung giờ. | Kiểm tra trùng lặp lịch phòng máy và khung giờ thi. | `HTTP 409 Conflict` |
| **Network/Timeout** | Lỗi timeout khi lưu ca thi vào DB. | Transaction Rollback hoàn toàn. | `HTTP 500 Internal Server Error` |

#### 6. Tiêu Chí Nghiệm Thu (Acceptance Criteria)
- [ ] Ca thi không thể tạo thành công nếu thiếu một trong hai nhân sự `proctorUserId` hoặc `examinerUserId`.
- [ ] Không cho phép gán cùng một người làm cả Giám thị và Giám khảo của cùng một ca thi (bị chặn 422).
- [ ] Câu hỏi thi thật bắt buộc phải có `structureId` và `cloId`; câu hỏi ôn tập được phép lưu độc lập vào `practice_questions`.

---

# 📊 PHẦN 5: BẢNG MA TRẬN TỔNG HỢP TIẾN ĐỘ SPRINT (LIVE STATUS MATRIX)

| Mã Task | Tên Phân Hệ & Module Chức Năng | Kỹ Sư Phụ Trách | Trạng Thái Thực Tế Trong Code (`backend/src/`) |
|:---|:---|:---:|:---|
| **BE-1.1** | Khởi tạo phiên luyện tập `POST /api/v1/practice/sessions` (Per/Full, Progressive 3-10, On-Demand) | **🧑 Thành** | ✅ **Đã hoàn thành 100%** |
| **BE-1.2** | Hàng đợi RAM `BoundedGradingQueueChannel` (1,000 slots RAM, `FullMode.Wait`) | **🧑 Thành** | ✅ **Đã hoàn thành 100%** |
| **BE-1.3** | Nộp bài Per/Full Persist First $< 100$ms, chuẩn 3NF `order_index`, `follow_up_index`, bỏ R2 | **🧑 Thành** | ✅ **Đã hoàn thành 100%** |
| **BE-1.4** | Background Worker `GradingQueueWorker` & SignalR Realtime Hub `/hubs/practice` | **🧑 Thành** | ✅ **Đã hoàn thành 100%** |
| **BE-1.5** | Gemini 1.5 Flash CoT 3 bước & Follow-up Engine ($4.0 \le \text{Score} \le 8.0$, `ai_evaluations.follow_up_prompt`) | **🧑 Thành** | ✅ **Đã hoàn thành 100%** |
| **BE-1.6** | Hoàn tất phiên (`practice_sessions.total_score`), Lịch sử luyện tập & Chi tiết Scorecard Rubric | **🧑 Thành** | ✅ **Đã hoàn thành 100%** |
| **BE-1.7** | API NextQuestion On-Demand & Thuật toán Anti-3-Consecutive Randomizer | **🧑 Thành** | ✅ **Đã hoàn thành 100%** |
| **BE-1.8** | Cơ chế Timeout 5 Phút Không Tương Tác (Session Inactivity Timeout `HTTP 410 Gone`) | **🧑 Thành** | ✅ **Đã hoàn thành 100%** |
| **BE-2.1** | Daily Quota Guard MF-02 (`max_mock_exams_per_day` do Admin cấu hình, chặn `HTTP 429`) | **🧑 Thành** | ✅ **Đã hoàn thành 100%** |
| **BE-2.2** | Bốc đề thi thử theo cấu hình môn (`mock_exam_*`) & Bloom từ kho `practice_questions` | **🧑 Thành** | ✅ **Đã hoàn thành 100%** |
| **BE-2.3** | Server Master Timer, Resume Session & Voice-First Gate (Zero R2, quá hạn trả `HTTP 410 Gone`) | **🧑 Thành** | ✅ **Đã hoàn thành 100%** |
| **BE-2.4** | Chấm 2 pha / Instant Scorecard theo CLO & Lịch sử thi thử | **🧑 Thành** | ✅ **Đã hoàn thành 100%** |
| **BE-V01** | Module Báo cáo Khảo thí FPT xuất bảng điểm Excel `.xlsx` và PDF có chữ ký số SHA-256 | **🧑 Thành** | ✅ **Đã hoàn thành 100%** |
| **BE-V02** | API Public Tra cứu & Xác thực Bảng điểm Chữ ký số qua QR Code | **🧑 Thành** | ✅ **Đã hoàn thành 100%** |
| **BE-V03** | API Student Dashboard (`GET /api/v1/student/dashboard`) | **🧑 Thành** | ✅ **Đã hoàn thành 100%** |
| **BE-3.1** | Quản lý `course_clos` 3NF & AI sinh câu hỏi từ CLO (`POST /api/v1/questions/generate-from-clo`) | **🧑 Tốt & Thành** | ✅ **Đã hoàn thành 100%** |
| **BE-3.2** | FluentValidation ép Barem Rubric $\sum \equiv 10.0$đ (`HTTP 422`), Model Answer $\ge 50$ & Upload ảnh Magic Bytes | **🧑 Tốt & Thành** | ✅ **Đã hoàn thành 100%** |
| **BE-3.3** | Phân tách 2 luồng tạo câu hỏi (R8: `practice_questions` gắn `clo_id` vs `exam_questions` gắn `clo_id` + `structure_id`) | **🧑 Tốt & Thành** | ✅ **Đã hoàn thành 100%** |
| **BE-3.4** | Trưởng Bộ Môn phê duyệt câu hỏi (`APPROVED`/`NEEDS_REVISION`/`REJECTED`), phân trang SQL & bàn giao đề `HANDED_OVER` | **🧑 Tốt & Thành** | ✅ **Đã hoàn thành 100%** |
| **BE-4.1** | Khảo thí tạo kỳ thi, ca thi, xuất bản đề; Trưởng BM cấu hình Follow-up thi thật (R3) | **🧑 Tốt** | ✅ **Đã hoàn thành 100% (Endpoint PUT follow-up-config riêng cho TBM, OpenShift nhận cả Examiner + Proctor)** |
| **BE-4.2** | Check-in Kiosk IP Binding `ip_address` (Ghế 1–40, `HTTP 403`, 7 trạng thái vé thi HOA) | **🧑 Tốt** | ✅ **Đã hoàn thành 100%** |
| **BE-4.3** | Nộp bài MF-04 Persist First $< 100$ms, Upload R2 `STT_MSSV.webm` + SHA-256 niêm phong | **🧑 Tốt** | ✅ **Đã hoàn thành 100%** |
| **BE-4.4** | BoundedChannel / DLQ chấm ngầm, AI Doubt Guard (`confidence < 0.70`), Evidence Panel API | **🧑 Tốt** | ✅ **Đã hoàn thành 100%** |
| **BE-4.5** | Công Bố Điểm Atomic 100%, `OneWayLockInterceptor` (`is_locked = true` $\to$ `HTTP 403`) | **🧑 Tốt** | ✅ **Đã hoàn thành 100%** |
| **BE-4.6** | Phân hệ Phúc khảo nội bộ qua Cán bộ Khảo thí (R2/R7: đề xuất phân công định mức <= 24 bài/GV, Khảo thí duyệt OneWayLock) | **🧑 Tốt** | ✅ **Đã hoàn thành 100% (Thuật toán đề xuất phân bổ tự động, batch-assign, duyệt quyết định mở OneWayLock)** |
| **BE-V04** | Dual Auth (Google OAuth PKCE + tài khoản trường cấp), cấp JWT **6 Roles RBAC**, đổi mật khẩu | **🧑 Tốt** | ✅ **Đã hoàn thành 100% (Đã xóa triệt để hàm auto-enrollment khỏi cả GoogleLogin và PasswordLogin)** |
| **BE-V05** | API Quản lý Người dùng User Management FE-09 (**6 Roles RBAC**) | **🧑 Tốt** | ✅ **Đã hoàn thành 100% (Đã bổ sung `POST /api/v1/admin/users` băm PBKDF2, cờ MustChangePassword=true)** |
| **BE-V06** | API Quản lý Học kỳ Semester CRUD FE-08 (tạo/sửa, không xóa) | **🧑 Tốt** | ✅ **Đã hoàn thành 100%** |
| **BE-V07** | Hộp thư Notification in-app FE-11 (`NotificationsController`) | **🧑 Tốt** | ✅ **Đã hoàn thành 100% (Bảng `notifications` 3NF độc lập, chỉ mục ESR `(user_id, is_read, created_at DESC)`)** |
| **BE-V08** | API Quản trị Cấu hình Hệ thống Admin (`system_configs`) | **🧑 Tốt** | ✅ **Đã hoàn thành 100%** |
| **BE-V09** | Quản trị DLQ phân tách (R2: Admin MF-01/02, Cán bộ Khảo thí MF-04, cắt bỏ Trưởng BM) & Audit Logs | **🧑 Tốt & Thành** | ✅ **Đã hoàn thành 100%** |
| **BE-V10** | API Giám thị Phòng Lab (R4: chỉ điểm danh ghế 1–40 & báo cáo vi phạm vào `exam_anti_cheat_logs`) | **🧑 Tốt** | ✅ **Đã hoàn thành 100% (Đã thêm `POST .../violations`, cắt bỏ đổi IP, mở ca thi và gỡ quyền suspend của Proctor)** |
| **BE-V11** | API Dashboard Giảng viên & Trưởng BM FE-10 (`GET /api/v1/lecturer/dashboard`) | **🧑 Tốt & Thành** | ✅ **Đã hoàn thành 100%** |
| **BE-V12** | Chuỗi phân cấp học thuật 4 cấp (`course_lecturers`, `ClassesController` mật khẩu lớp — R5) | **🧑 Thành & Tốt** | ✅ **Đã hoàn thành 100%** |
| **BE-V13** | Ca thi trong lớp & mật khẩu tự động xoay vòng 5 phút (`in_class_exam_shifts` — R6 / MF-05) | **🧑 Thành & Tốt** | ✅ **Đã hoàn thành 100% (4 thực thể 3NF, TOTP 5m RFC 6238, 10 REST endpoints, chấm ngầm BoundedChannel)** |
| **BE-QA** | Bộ Test Suites xUnit & Architecture Tests NetArchTest + Khóa cấu hình Deploy Production | **🧑 Thành & Tốt** | ✅ **Đã đạt `1,409 / 1,409` Tests PASS (100%, Exit Code = 0)** |
