# ⚙️ BACKEND MASTER TASK BOARD & EXECUTION SPECIFICATIONS (v8.0)
## ĐỒ ÁN TỐT NGHIỆP SEP490 (FA26SE166) — HỆ THỐNG LUYỆN THI & ĐÁNH GIÁ VẤN ĐÁP BẰNG LLM
### KHO MÃ NGUỒN: `05_Source_Code/backend` | CỔNG DỊCH VỤ API: `5000` | DATABASE: `5432` | FRONTEND: `3000`

---

> [!IMPORTANT]
> **QUY CHUẨN TỐI THƯỢNG & NGUỒN SỰ THẬT DUY NHẤT (SINGLE SOURCE OF TRUTH):**
> - **Mã đề tài:** FA26SE166 | **Học kỳ:** Fall 2026 (FA26) — Đại học FPT TP.HCM (FPT SG)
> - **Tech Stack Chuẩn:** .NET 8 Clean Architecture 4 tầng (Domain, Application, Infrastructure, API) · CQRS MediatR · PostgreSQL 16 (30 bảng 3NF) · SignalR Typed Hubs · BoundedChannel (1,000 slots RAM) · Polly Resilience Pipeline · Google Gemini 1.5 Flash/Pro · Cloudflare Whisper STT · Cloudflare R2 Storage.
> - **Phân định 5 Roles RBAC:** `student`, `lecturer`, `department_head`, `proctor`, `admin`.
> - **File này là tài liệu duy nhất điều phối toàn bộ công việc Backend** cho 2 kỹ sư: **Nguyễn Quang Thành** và **Nguyễn Trọng Tốt**, cùng sự hỗ trợ CSDL độc lập từ **Nguyễn Đăng Hải**.
> - Tuyệt đối tuân thủ 12 cam kết kiến trúc tại `AGENTS.md` và `MASTER_ARCHITECTURE.md`. Không tự ý suy diễn hoặc phá vỡ API contracts.

---

# 📊 PHẦN 1: BẢNG MA TRẬN PHÂN BỔ CÔNG VIỆC (BALANCED 50/50 MATRIX)

| Kỹ sư đảm nhiệm | Chức năng chính (Heavy Core) | Các task vệ tinh ("Râu ria" hoàn thiện) | Thế mạnh chuyên môn |
| :--- | :--- | :--- | :--- |
| 🧑 **Nguyễn Quang Thành**<br>*(Team Leader & Lead Backend Architect)* | **1. Module MF-01: Luyện Tập Vấn Đáp Tương Tác**<br>• Khởi tạo phiên: `[Per-Question]` On-Demand & `[Full-Session]` Progressive 3–10 câu (BE-1.1)<br>• Hàng đợi RAM BoundedChannel 1,000 slots (`FullMode.Wait`) (BE-1.2)<br>• Nộp bài Persist First $< 100$ms, Stream Whisper STT, bỏ `AudioUrl` & bỏ R2 (BE-1.3)<br>• Background Worker `GradingQueueWorker` & Realtime Hub `/hubs/practice` (BE-1.4)<br>• Gemini 1.5 Flash CoT 3 bước & Follow-up A2 ($4.0 \le \text{Score} \le 8.0$) (BE-1.5)<br>• Hoàn tất phiên, Lịch sử luyện tập & Scorecard Rubric chi tiết (BE-1.6)<br>• API NextQuestion On-Demand & Thuật toán Anti-3-Consecutive Randomizer (BE-1.7)<br>• Inactivity Timeout 5 phút, Lazy check `HTTP 410 Gone`, bảo toàn điểm (BE-1.8)<br><br>**2. Module MF-02: Thi Thử Vấn Đáp Bấm Giờ**<br>• Daily Quota Guard do Admin cấu hình, chặn `HTTP 429` (BE-2.1)<br>• Bốc đề ma trận Bloom từ kho `practice_questions` (BE-2.2)<br>• Server Master Timer & Voice-First Gate chỉ lưu transcript (BE-2.3)<br>• Chấm 2 pha / Async Grading, Instant Scorecard theo CLO & Lịch sử thi thử (BE-2.4) | **1. Module Báo Cáo Khảo Thí FPT (BE-V01):**<br>• Xuất bảng điểm Excel `.xlsx` (EPPlus)<br>• Xuất biên bản ca thi PDF (QuestPDF)<br><br>**2. API Public Xác Thực Bảng Điểm (BE-V02):**<br>• Tra cứu chứng chỉ bảng điểm qua QR Code không cần token (`GET /api/v1/verification/grades/{hash}`)<br><br>**3. API Student Dashboard (BE-V03):**<br>• Thống kê buổi tập, điểm TB, độ thành thạo CLO, banner lịch thi phòng Lab (`GET /api/v1/student/dashboard`) | Clean Architecture .NET 8, CQRS MediatR, BoundedChannel RAM Queue, SignalR Typed Hubs, Master Timer, Thuật toán phân bổ đề thi, Export Excel/PDF. |
| 🧑 **Nguyễn Trọng Tốt**<br>*(Backend Developer, AI Specialist & QA Lead)* | **1. Module MF-03: Ngân Hàng Đề & Rubric Studio 10.0**<br>• AI sinh câu hỏi từ FLM Syllabus theo CLO & Barem riêng 10.0đ (BE-3.1)<br>• Soạn Barem Rubric Studio: FluentValidation $\sum \equiv 10.0$đ (`HTTP 422`), Model Answer $\ge 50$ ký tự (BE-3.2)<br>• Tick chọn 2 kho (`practice_questions` / `exam_questions`) & Gửi duyệt Bộ Môn (BE-3.3)<br>• Trưởng Bộ Môn Thẩm định: 3 quyết định `APPROVED`, `NEEDS_REVISION`, `REJECTED` (BE-3.4)<br><br>**2. Module MF-04: Thi Thật Phòng Lab, Công Bố Điểm & Phúc Khảo**<br>• Quản trị Kỳ thi & Ca thi phòng lab, kíp thi, người coi thi, follow-up (BE-4.1)<br>• Check-in Kiosk IP Binding `ip_address` Ghế 1–40 `HTTP 403`, 7 trạng thái HOA (BE-4.2)<br>• Nộp bài Persist First $< 100$ms `SUBMITTED`, Upload Cloudflare R2 `STT_MSSV.webm` + SHA-256 niêm phong (BE-4.3)<br>• BoundedChannel 1000 slots & DLQ 5m chấm ngầm, AI Doubt Guard (`confidence < 0.70`), Evidence Panel API (BE-4.4)<br>• Công Bố Điểm Atomic 100% & Khóa 1 chiều `OneWayLockInterceptor` (`is_locked = true` $\to$ `HTTP 403`) (BE-4.5)<br>• Phân hệ Phúc khảo nội bộ `AppealRequest`: Trưởng BM tiếp nhận và giao GV khác chấm lại, Acknowledge điểm (BE-4.6) | **1. Dual Auth (Google OAuth + Password) (BE-V04):**<br>• Mở mọi email Google, đăng nhập `username`/`password` trường cấp, đổi mật khẩu, cấp JWT 5 roles<br><br>**2. API Quản lý Người dùng (BE-V05):**<br>• User Management FE-09: khóa/mở user, gán role 5 roles<br><br>**3. API Quản lý Học kỳ (BE-V06):**<br>• Semester CRUD FE-08: tạo/sửa học kỳ, không xóa<br><br>**4. Hộp thư Notification In-App (BE-V07):**<br>• Tự động sinh thông báo theo sự kiện hệ thống<br><br>**5. Cấu hình Hệ thống Admin (BE-V08):**<br>• API quản trị bảng `system_configs` MF-01<br><br>**6. Quản trị DLQ & Audit Logs (BE-V09):**<br>• DLQ Replay Worker 5 phút (Trưởng BM) & API Audit Logs (Admin)<br><br>**7. API Giám thị Phòng Lab (BE-V10):**<br>• Điểm danh 40 máy lab, đình chỉ thi Kiosk<br><br>**8. API Dashboard Giảng viên (BE-V11):**<br>• Thống kê ca thi, đề thi, tỷ lệ duyệt đề<br><br>**9. Module QA & Testing (BE-QA):**<br>• Test Suites xUnit & NetArchTest Architecture (100% Pass) | AI Prompting CoT (Gemini 1.5 Flash), Whisper STT Server-side, Dual Auth (Google OAuth + PBKDF2 Password), RBAC 5 Roles, EF Core Interceptors, Cloudflare R2 Storage, QA Automation & Architecture Tests. |
| 🧑 **Nguyễn Đăng Hải**<br>*(Database Specialist & Frontend Developer)* | **Quản Trị Toàn Diện CSDL PostgreSQL 16 (30 Bảng 3NF):**<br>• DDL Schema, Foreign Keys, Unique Index, Check Constraints (`RubricCriteria` sum = 10, Bloom Levels, Ticket Statuses)<br>• ESR Indexing (`Equality`, `Sort`, `Range`) tối ưu hóa hiệu năng truy vấn cao tải phòng lab<br>• Seed Data mẫu chuẩn xác (Học kỳ FA26, Users 5 roles, Môn học, Barem Rubric, Kho câu hỏi, `system_configs`)<br>• Cấu hình Docker Compose `postgres:16-alpine` (Port 5432) & Migration Scripts | **Phối Hợp Frontend Toàn Lực:**<br>• **Hải tuyệt đối KHÔNG code logic C# Backend**<br>• Sau khi hoàn thiện tầng CSDL, Hải chuyển sang dồn 100% năng lượng phụ trách các màn hình Frontend (MF-02, MF-03, MF-04 Quản trị, Admin CRUD) cùng Lê Vũ Hoàng theo `FE_TASK_BOARD.md` | Thiết kế CSDL quan hệ 3NF, Tối ưu hóa Index PostgreSQL, Viết DDL & Constraints chặt chẽ, Docker Database Management. |

---

# 📁 PHẦN 2: CẤU TRÚC KIẾN TRÚC & THƯ MỤC CHUẨN MỰC (`backend/src/`)

Hệ thống được thiết kế theo đúng mô hình **.NET 8 Clean Architecture 4 tầng** chuẩn Enterprise, ánh xạ đầy đủ 30 thực thể CSDL trong 6 Bounded Contexts:

```text
backend/src/
├── Domain/                          # Tầng Core Thuần Khiết (Zero External Dependencies)
│   ├── Common/                      # BaseEntity.cs, IAggregateRoot.cs, ILockableEntity.cs
│   ├── Entities/                    # 30 thực thể chuẩn hóa trong 6 Bounded Contexts + Bảng Hệ thống:
│   │   ├── Identity (1):            User.cs (5 Roles: student, lecturer, department_head, proctor, admin; Dual Auth: Google OAuth + username/password_hash)
│   │   ├── Academic (4):            Semester.cs, Course.cs (has_follow_up, transcript_buffer_seconds, max_follow_up_questions, exam_input_mode), Class.cs, ClassEnrollment.cs
│   │   ├── Assessment (2):          Rubric.cs, RubricCriterion.cs (Ràng buộc sum = 10.0đ)
│   │   ├── Practice [MF-01] (5):    PracticeQuestion.cs, PracticeSession.cs (last_activity_at, selected_difficulties), PracticeAnswer.cs (bỏ audio_url), AiEvaluation.cs, AiEvaluationDetail.cs
│   │   ├── Mock Exam [MF-02] (6):   ExamStructure.cs, ExamSet.cs, ExamSetQuestion.cs, MockExamQuota.cs, MockExamSession.cs, MockExamAnswer.cs (Rút từ kho practice_questions)
│   │   ├── Official Exam [MF-04] (7): ExamQuestion.cs, OfficialExamSession.cs, RealExamSessionShift.cs (phòng lab ghi trực tiếp, người coi thi), StudentExamTicket.cs (ip_address, Seat 1-40), ExamQuestionSubmission.cs, LecturerAudit.cs, LecturerAuditDetail.cs
│   │   ├── Appeals [MF-04] (1):     AppealRequest.cs (Trưởng Bộ Môn tiếp nhận & giao giảng viên chấm lại)
│   │   ├── Resilience & Audit (2):  DeadLetterQueue.cs, AuditLog.cs
│   │   └── System & Notifications (2): SystemConfig.cs (Key-Value), Notification.cs (In-app notifications)
│   ├── Enums/                       # UserRole, ExamTicketStatus (7 trạng thái HOA), ExamInputMode (VoiceOnly, VoiceWithTranscriptEdit), AppealStatus, BloomLevel
│   └── Exceptions/                  # DomainValidationException, OneWayLockException, QuotaExceededException
├── Application/                     # MediatR CQRS, FluentValidation, Interfaces
│   ├── Common/                      # IApplicationDbContext, IGeminiService, IWhisperService, IR2StorageService, IExcelExportService, IPdfExportService
│   ├── Behaviors/                   # ValidationBehavior.cs, LoggingBehavior.cs, PerformanceBehavior.cs
│   └── Features/                    # Tổ chức CQRS theo từng Phân Hệ Nghiệp Vụ
│       ├── Auth/                    # Commands: GoogleLogin, PasswordLogin, ChangePassword, RefreshToken
│       ├── Practice/                # Commands: StartSession, NextQuestion, SubmitAnswer, SubmitBatch, CompleteSession; Queries: GetSession, GetHistory
│       ├── MockExams/               # Commands: StartMockExam, SubmitMockExam; Queries: GetMockExamHistory, GetMockExamDetail
│       ├── Questions/               # Commands: GenerateFromFlm, CreateDraft, BatchSubmitReview, ReviewDecision; Queries: GetQuestionsList
│       ├── OfficialExams/           # Commands: CreateSeason, CreateShift, CheckInKiosk, SubmitOfficialExam, PublishGrades; Queries: ExportReports
│       ├── Appeals/                 # Commands: CreateAppeal, AssignLecturer, SubmitDecision; Queries: GetAppealsList
│       ├── DeadLetterQueue/         # Queries: GetDlqTasks; Commands: ReplayDlqTask (Department Head)
│       ├── Admin/                   # Users, Semesters, SystemConfigs, AuditLogs
│       └── Notifications/           # Queries: GetNotifications; Commands: MarkAsRead
├── Infrastructure/                  # EF Core, External APIs, Background Workers, SignalR
│   ├── Persistence/                 # OralExamDbContext.cs, Configurations/ (Fluent API 30 tables), Migrations/
│   ├── Interceptors/                # OneWayLockInterceptor.cs (Chặn UPDATE/DELETE khi is_locked = true -> HTTP 403)
│   ├── Channels/                    # BoundedGradingQueueChannel.cs (Capacity = 1,000 slots RAM, FullMode.Wait)
│   ├── BackgroundJobs/              # GradingQueueWorker.cs (Polly Retry 2s-4s-8s), DlqReplayWorker.cs (Quét bù sau 5m)
│   ├── Hubs/                        # PracticeHub.cs (/hubs/practice)
│   └── Services/                    # GeminiEvaluationService.cs, CloudflareR2Service.cs, WhisperService.cs, QuotaService.cs, ExcelService.cs, PdfService.cs
├── API/                             # ASP.NET Core Host Gateway (Port 5000)
│   ├── Controllers/v1/              # AuthController, PracticeController, MockExamsController, QuestionsController, OfficialExamsController, AppealsController, DeadLetterQueueController, AdminController, SemestersController, NotificationsController, CoursesController, VerificationController
│   ├── Middlewares/                 # GlobalExceptionMiddleware.cs (RFC 7807), RequestLoggingMiddleware.cs
│   └── Program.cs                   # CORS AllowFrontend (3000) trước StaticFiles, DI Container, MapHub
└── tests/
    ├── OralExamination.UnitTests/   # xUnit: PracticeTests, MockExamTests, QuestionTests, OfficialExamTests, QuotaTests
    └── OralExamination.ArchitectureTests/ # NetArchTest: Khóa ranh giới Clean Architecture 4 tầng
```

---

# 🧑‍💻 PHẦN 3: NHIỆM VỤ CHI TIẾT CỦA NGUYỄN QUANG THÀNH (TEAM LEADER & LEAD BACKEND ARCHITECT)

### 🌟 KHỐI TRỌNG TÂM 1: MODULE MF-01 — PHÒNG LUYỆN TẬP VẤN ĐÁP TƯƠNG TÁC (INTERACTIVE PRACTICE)
> **Trạng thái phân hệ:** ✅ **ĐÃ HOÀN THÀNH 100% (NGUYỄN QUANG THÀNH ĐÃ XÂY DỰNG & TEST PASS)**

#### Module BE-1.1: Khởi Tạo Phiên Luyện Tập Tự Do (On-Demand & Progressive)
* **Kỹ sư phụ trách:** 🧑 **Nguyễn Quang Thành**
* **Endpoint:** `POST /api/v1/practice/sessions`
* **Tệp liên quan:** `Features/Practice/Commands/StartPracticeSession/*`, `PracticeController.cs`, `SystemConfig.cs`, `PracticeSession.cs`
* **Trạng thái:** ✅ **Hoàn thành 100%**
* **Mô tả nghiệp vụ:**
  - Tiếp nhận yêu cầu khởi tạo: `courseId`, `isFullSession` (`false`: Per-Question, `true`: Full-Session), `difficulties` (mảng: `easy`, `medium`, `hard`) hoặc `difficulty` (chuỗi đơn), `questionCount` (int? tùy chọn ở Per-Question, bắt buộc 3–10 ở Full-Session).
  - **Chế độ `[Per-Question On-Demand]`:**
    * Cho phép chọn đơn lẻ hoặc tổ hợp bất kỳ: `["easy", "medium"]`, `["easy", "hard"]`, `["medium", "hard"]`, `["easy", "medium", "hard"]`.
    * Không bắt buộc chốt số câu. Hệ thống cấp ngay Câu 1 trong response khởi tạo, lưu `session.SelectedDifficulties` và `session.LastActivityAt = DateTime.UtcNow`.
  - **Chế độ `[Full-Session Progressive]`:**
    * Ràng buộc số lượng từ 3 đến 10 câu qua `system_configs` (`MinMixedPracticeQuestions = 3`, `MaxMixedPracticeQuestions = 10`).
    * Query kho đề `practice_questions` lọc theo môn, chia đều số câu cho các mức Bloom và **sắp xếp theo tiến trình tăng dần: Dễ $\to$ Trung bình $\to$ Khó**, cấp trọn gói toàn bộ câu hỏi.
    * Nếu kho đề thiếu câu cho bất kỳ mức nào: Trả lỗi `HTTP 400 Bad Request` tiếng Việt rõ ràng, không tạo phiên rác.
  - Cung cấp thời gian đệm `transcript_buffer_seconds` (10–300s, mặc định 60s) từ bảng `courses`.
* **Tiêu chí nghiệm thu (DoD):** Trả về `sessionId`, `transcriptBufferSeconds`, danh sách câu hỏi kèm tóm tắt rubric $\sum \equiv 10.0$đ. Unit test bao phủ ca đơn/tổ hợp mức độ và ca thiếu đề.

#### Module BE-1.2: Hàng Đợi RAM BoundedChannel (1,000 slots RAM)
* **Kỹ sư phụ trách:** 🧑 **Nguyễn Quang Thành**
* **Tệp liên quan:** `Infrastructure/Channels/BoundedGradingQueueChannel.cs`
* **Trạng thái:** ✅ **Hoàn thành 100%**
* **Mô tả nghiệp vụ:**
  - Khởi tạo Singleton `Channel<GradingTask>` dung lượng giới hạn `capacity = 1000`, thiết lập `FullMode = BoundedChannelFullMode.Wait`.
  - Non-blocking `TryWrite()` khi còn slot trống và `WriteAsync()` chờ khi queue đầy 1,000 slots (Backpressure an toàn).
  - Chống tràn bộ nhớ OOM khi lượng lớn sinh viên cùng nộp bài đồng thời.
* **Tiêu chí nghiệm thu (DoD):** Đẩy 1,200 task đồng thời: 1,000 task đầu nhận ngay, 200 task sau chờ theo cơ chế Backpressure mà không làm sập server.

#### Module BE-1.3: Nộp Bài Per-Question & Batch Submit Full-Session (Stream Whisper STT)
* **Kỹ sư phụ trách:** 🧑 **Nguyễn Quang Thành**
* **Endpoint:** `POST /api/v1/practice/sessions/{id}/answers` và `POST /api/v1/practice/sessions/{id}/batch-submit`
* **Tệp liên quan:** `Features/Practice/Commands/SubmitPracticeAnswer/*`, `Features/Practice/Commands/SubmitPracticeBatch/*`, `PracticeController.cs`
* **Trạng thái:** ✅ **Hoàn thành 100%**
* **Mô tả nghiệp vụ:**
  - Nhận file ghi âm `.webm`, stream trực tiếp sang Cloudflare Whisper API để lấy transcript văn bản.
  - **Quy chuẩn lưu trữ:** Loại bỏ upload audio lên Cloudflare R2, xóa cột `AudioUrl` khỏi `practice_answers` (tiết kiệm 100% chi phí lưu trữ cho luyện tập, dành riêng R2 cho MF-04).
  - **Persist First $< 100$ms:** Lưu bản ghi vào bảng `practice_answers` trạng thái `pending`, sau đó đẩy task vào BoundedChannel. Trả mã phản hồi `HTTP 202 Accepted` ngay lập tức.
* **Tiêu chí nghiệm thu (DoD):** Thời gian phản hồi API $< 100$ms. Bài làm được lưu an toàn vào PostgreSQL trước khi tiến trình ngầm bắt đầu chấm.

#### Module BE-1.4: Background Worker `GradingQueueWorker` & SignalR `PracticeHub`
* **Kỹ sư phụ trách:** 🧑 **Nguyễn Quang Thành**
* **Endpoint:** WebSocket `/hubs/practice`
* **Tệp liên quan:** `API/Workers/GradingQueueWorker.cs`, `API/Hubs/PracticeHub.cs`
* **Trạng thái:** ✅ **Hoàn thành 100%**
* **Mô tả nghiệp vụ:**
  - `GradingQueueWorker` chạy dưới dạng `BackgroundService`, liên tục đọc task từ kênh RAM.
  - Gọi Gemini AI chấm ngầm, lưu kết quả vào `ai_evaluations` và `ai_evaluation_details`, cập nhật `practice_answers.status = "graded"`.
  - Đẩy kết quả realtime qua SignalR Hub tới group `session_{sessionId}` qua event `ReceiveGradingResult`.
  - Polly Retry 3 lần (2s, 4s, 8s). Khi lỗi kéo dài: Cách ly vào DLQ `dead_letter_queues` và bắn event `ReceiveGradingError`.
* **Tiêu chí nghiệm thu (DoD):** WebSocket `/hubs/practice` ổn định, Client nhận đúng JSON Scorecard ngay sau khi AI chấm xong mà không cần polling.

#### Module BE-1.5: Gemini 1.5 Flash CoT 3 Bước & Follow-up Engine Đa Nấc
* **Kỹ sư phụ trách:** 🧑 **Nguyễn Quang Thành**
* **Tệp liên quan:** `Infrastructure/Services/GeminiAiGradingService.cs`
* **Trạng thái:** ✅ **Hoàn thành 100%**
* **Mô tả nghiệp vụ:**
  - System Prompt CoT 3 bước ép khuôn JSON: (1) Trích xuất luận điểm kỹ thuật; (2) Đối chiếu Barem Rubric $\sum \equiv 10.0$đ; (3) Cho điểm và nhận xét sư phạm.
  - **Quy tắc kích hoạt câu hỏi phụ (Follow-up):**
    * Chế độ **Per-Question**: Kích hoạt khi điểm ranh giới **$4.0 \le \text{Score} \le 8.0$**. Đếm số câu phụ đã hỏi trong phiên, cho phép hỏi tiếp đến tối đa `MaxPracticeFollowUpQuestions` (1–5 câu, mặc định 2 câu do Admin cấu hình trong `system_configs`). Nếu $\text{Score} < 4.0$ hoặc $> 8.0$, bỏ qua hỏi phụ mở ngay Scorecard.
    * Chế độ **Full-Session**: **TUYỆT ĐỐI KHÔNG có câu hỏi follow-up**, chấm liền mạch toàn bộ đề.
* **Tiêu chí nghiệm thu (DoD):** Gemini trả về đúng cấu trúc JSON 100%. Tỷ lệ kích hoạt câu hỏi phụ chính xác theo khoảng điểm và hạn mức cấu hình.

#### Module BE-1.6: Hoàn Tất Phiên, Lịch Sử Luyện Tập & Chi Tiết Scorecard
* **Kỹ sư phụ trách:** 🧑 **Nguyễn Quang Thành**
* **Endpoint:** `POST /api/v1/practice/sessions/{id}/complete`, `GET /api/v1/practice/student/history`, `GET /api/v1/practice/sessions/{id}`
* **Tệp liên quan:** `Features/Practice/Commands/CompletePracticeSession/*`, `Features/Practice/Queries/*`, `PracticeController.cs`
* **Trạng thái:** ✅ **Hoàn thành 100%**
* **Mô tả nghiệp vụ:**
  - `POST /complete`: Đóng phiên, cập nhật `status = "completed"`, `completed_at = DateTime.UtcNow` (Idempotent).
  - `GET /student/history`: Trả về danh sách phiên luyện tập kèm chế độ (`Per-Question` / `Full-Session`), thời gian, số câu hỏi, điểm trung bình phục vụ Tab Luyện tập ở Student Portal.
  - `GET /{id}`: Trả về toàn bộ Scorecard, câu hỏi, transcript hiệu đính và nhận xét sư phạm từng tiêu chí rubric.
* **Tiêu chí nghiệm thu (DoD):** Phản hồi chuẩn RFC 7807. 6 bài unit tests trong `CompleteAndHistoryPracticeTests.cs` đạt 100% passed.

#### Module BE-1.7: NextQuestion On-Demand & Thuật Toán Anti-3-Consecutive Randomizer
* **Kỹ sư phụ trách:** 🧑 **Nguyễn Quang Thành**
* **Endpoint:** `POST /api/v1/practice/sessions/{sessionId}/next-question`
* **Tệp liên quan:** `Features/Practice/Commands/NextQuestion/*`, `PracticeController.cs`
* **Trạng thái:** ✅ **Hoàn thành 100%**
* **Mô tả nghiệp vụ:**
  - Cấp câu hỏi tiếp theo On-Demand cho chế độ `per_question`, loại trừ các câu đã làm trong phiên (`Id NOT IN (...)`).
  - **Thuật toán Anti-3-Consecutive:** Nếu 2 câu liền trước cùng độ khó $D$ và phiên chọn $> 1$ độ khó $\to$ tạm loại trừ $D$, bốc sang độ khó khác. Đảm bảo *trong bất kỳ chuỗi 3 câu hỏi liên tiếp nào, tối đa chỉ có 2 câu chung mức độ*.
  - **Fallback an toàn:** Nếu các độ khó khác đã hết câu, tự động fallback cho phép bốc tiếp câu còn lại của $D$. Khi cạn sạch toàn bộ các mức: trả `hasMoreQuestions: false`.
  - Cập nhật `session.LastActivityAt = DateTime.UtcNow`.
* **Tiêu chí nghiệm thu (DoD):** 6 unit tests trong `NextQuestionCommandHandlerTests.cs` và 4 stress tests trong `NextQuestionAdversarialStressTests.cs` (assert không bao giờ có 3 câu cùng mức độ) đạt 100% passed.

#### Module BE-1.8: Cơ Chế Timeout 5 Phút Không Tương Tác (Session Inactivity Timeout)
* **Kỹ sư phụ trách:** 🧑 **Nguyễn Quang Thành**
* **Tệp liên quan:** `Domain/Entities/PracticeSession.cs`, `Features/Practice/Commands/*`, `PracticeController.cs`
* **Trạng thái:** ✅ **Hoàn thành 100%**
* **Mô tả nghiệp vụ:**
  - Cấu hình động `SessionInactivityTimeoutMinutes = 5` trong `system_configs`.
  - Lưu `last_activity_at` (timestamp) cập nhật sau mỗi tương tác (tạo phiên, nộp bài, lấy câu tiếp).
  - **Lazy Validation:** Nếu `DateTime.UtcNow - LastActivityAt > 5 phút`: Tự động cập nhật `session.Status = "completed"`, trả lỗi **HTTP 410 Gone** RFC 7807 (`Title = "Session Timed Out"`).
  - **Bảo toàn dữ liệu 100%:** Giữ nguyên toàn bộ câu trả lời, điểm số và nhận xét đã làm trước đó.
* **Tiêu chí nghiệm thu (DoD):** 4 bài unit tests trong `PracticeSessionInactivityTimeoutTests.cs` đạt 100% passed.

---

### 🌟 KHỐI TRỌNG TÂM 2: MODULE MF-02 — THI THỬ VẤN ĐÁP BẤM GIỜ CÓ HẠN NGẠCH (TIMED MOCK EXAM)
> **Trạng thái phân hệ:** ✅ **ĐÃ HOÀN THÀNH 100% (TOÀN BỘ BE-2.1 ĐẾN BE-2.4 ĐÃ ĐƯỢC TRIỂN KHAI VÀ VICTORY AUDITOR PHÊ DUYỆT 624/624 TESTS PASS)**

#### Module BE-2.1: Daily Quota Guard Do Admin Cấu Hình (HTTP 429)
* **Kỹ sư phụ trách:** 🧑 **Nguyễn Quang Thành**
* **Endpoint:** `POST /api/v1/mock-exams/sessions`, `GET /api/v1/mock-exams/quota`
* **Tệp liên quan:** `Features/MockExams/Commands/StartMockExam/*`, `Features/MockExams/Queries/GetMockExamQuota/*`, `MockExamsController.cs`, `Course.cs`
* **Trạng thái:** ✅ **Hoàn thành 100%**
* **Mô tả nghiệp vụ:**
  - Hạn ngạch số lượt thi thử trong ngày do **Admin (`admin`)** cấu hình theo từng môn học (`max_mock_exams_per_day` trong `courses`, mặc định $K = 3$).
  - Khi sinh viên bắt đầu thi: Command Handler kiểm tra số lượt thi đã thực hiện trong ngày (theo múi giờ GMT+7) trong bảng `mock_exam_quotas`.
  - Nếu đã đủ hạn ngạch: Trả mã phản hồi **`HTTP 429 Too Many Requests`** RFC 7807: *"Bạn đã sử dụng hết hạn ngạch thi thử trong ngày (K/K lượt) cho môn học này"*.
  - Nếu còn hạn ngạch: Tăng bộ đếm nguyên tử và cho phép khởi tạo bài thi.
  - Cập nhật `GetMockExamQuotaQueryHandler` đọc động từ `course.MaxMockExamsPerDay`.
* **Tiêu chí nghiệm thu (DoD):** Lượt thứ 1 đến $K$ trả HTTP 200. Lượt $K+1$ trả `HTTP 429`. Unit test kiểm thử thành công 100%.

#### Module BE-2.2: Bốc Đề Thi Thử Theo Ma Trận Bloom Từ Kho `practice_questions`
* **Kỹ sư phụ trách:** 🧑 **Nguyễn Quang Thành**
* **Endpoint:** `POST /api/v1/mock-exams/sessions`
* **Tệp liên quan:** `Features/MockExams/Commands/StartMockExam/*`, `MockExamsController.cs`
* **Trạng thái:** ✅ **Hoàn thành 100%**
* **Mô tả nghiệp vụ:**
  - Rút câu hỏi thi thử theo cấu trúc ma trận Bloom (`exam_structures`) của môn học do Trưởng Bộ Môn thiết lập.
  - **Quy chuẩn nguồn đề:** Bốc đề hoàn toàn từ kho câu hỏi luyện tập (`practice_questions`). **CÔ LẬP TUYỆT ĐỐI** kho câu hỏi thi thật (`exam_questions`) dành riêng cho MF-04.
  - Sinh viên **KHÔNG chọn topic hay độ khó**. Chỉ chọn `courseId` và tùy chọn `hasFollowUp` (`true` = 45 phút, `false` = 30 phút).
  - Thuật toán bốc đề theo phân bổ Bloom, tự động fallback bù đủ tổng số câu nếu một mức Bloom bị thiếu câu.
  - Tạo `ExamSet` và `MockExamSession` (`status = "in_progress"`), khởi tạo câu hỏi với `MockExamAnswer` (`status = "pending"`).
* **Tiêu chí nghiệm thu (DoD):** 100% câu hỏi rút từ `practice_questions`, 0 câu rò rỉ từ `exam_questions`. Unit tests bao phủ đầy đủ ca biên và fallback.

#### Module BE-2.3: Server Master Timer & Voice-First Gate
* **Kỹ sư phụ trách:** 🧑 **Nguyễn Quang Thành**
* **Endpoint:** `POST /api/v1/mock-exams/sessions/{id}/answers`, `POST /api/v1/mock-exams/sessions/{id}/submit`
* **Tệp liên quan:** `Features/MockExams/Commands/SubmitMockExamAnswer/*`, `Features/MockExams/Commands/SubmitMockExamSession/*`, `MockExamsController.cs`
* **Trạng thái:** ✅ **Hoàn thành 100%**
* **Mô tả nghiệp vụ:**
  - Tiếp nhận file âm thanh (`.webm`, `.mp3`, `.wav`), stream nhị phân trực tiếp trên RAM sang Cloudflare Whisper STT lấy transcript. Bảo toàn Invariant Zero-R2 (không lưu audio lên Cloudflare R2, lưu transcript vào `mock_exam_answers`).
  - Quản lý đồng hồ làm bài chuẩn Server Master Timer (30 phút không follow-up, 45 phút có follow-up căn cứ `started_at` và `expires_at`).
  - Thời gian đệm trễ mạng (Network Grace Period) tối đa 10 giây. Nộp muộn quá hạn tự động khóa ca thi, trả `HTTP 410 Gone`. Khi submit tự động chấm các câu đã làm và gán 0.0 điểm cho câu bỏ trống (`status = 'pending'`).
* **Tiêu chí nghiệm thu (DoD):** Kiểm chứng thực tế `dotnet test` đạt 100% Passed.

#### Module BE-2.4: Fast-Path Synchronous Instant AI Grading Scorecard Rubric Theo CLO & Lịch Sử Thi Thử
* **Kỹ sư phụ trách:** 🧑 **Nguyễn Quang Thành**
* **Endpoint:** `POST /api/v1/mock-exams/sessions/{id}/submit`, `GET /api/v1/mock-exams/history`, `GET /api/v1/mock-exams/history/{id}`
* **Tệp liên quan:** `Features/MockExams/Queries/GetMockExamHistory/*`, `Features/MockExams/Queries/GetMockExamSessionDetail/*`, `MockExamsController.cs`
* **Trạng thái:** ✅ **Hoàn thành 100%**
* **Mô tả nghiệp vụ:**
  - Tích hợp Gemini 1.5 Flash CoT chấm điểm song song `Task.WhenAll` (SLA 3–5s) trả về Scorecard Rubric 10.0đ chi tiết theo từng CLO.
  - Xếp loại 5 bậc học thuật (Xuất sắc, Giỏi, Khá, Trung bình, Yếu) và lưu chi tiết vào `ai_evaluations`, `ai_evaluation_details`.
  - Bộ API `GET /history` và `GET /history/{sessionId}` phân trang, thống kê chính xác số câu đã làm cho Student Portal.
* **Tiêu chí nghiệm thu (DoD):** 627/627 unit & adversarial tests passed (100%). Đã khắc phục triệt để 5 lỗi Cấp 1 từ đợt Thẩm định Đối kháng (SEC-R1-01, SEC-R1-02, SEC-R1-03, QA-R3-01, QA-R3-02). Tài liệu `docs/MF02_Frontend_Integration.md` hoàn chỉnh.

---

### 🛠️ CÁC MODULE VỆ TINH ("RÂU RIA") CỦA NGUYỄN QUANG THÀNH

#### Module BE-V01: Module Báo Cáo Khảo Thí FPT (Excel .xlsx + PDF)
* **Kỹ sư phụ trách:** 🧑 **Nguyễn Quang Thành**
* **Endpoint:** `GET /api/v1/official-exams/shifts/{shiftId}/export-excel`, `GET /api/v1/official-exams/shifts/{shiftId}/export-pdf`
* **Tệp liên quan:** `Features/OfficialExams/Queries/ExportReports/*`, `Infrastructure/Services/ExcelService.cs`, `Infrastructure/Services/PdfService.cs`
* **Mô tả nghiệp vụ:**
  - **Xuất bảng điểm Excel `.xlsx` (EPPlus):** Đúng định dạng mẫu biểu Khảo thí Đại học FPT: Header gồm Bộ Môn, Môn thi, Mã ca thi, Phòng Lab, Ngày thi, Tên Giám thị và Giảng viên chấm. Bảng điểm: Cột STT, MSSV, Họ và tên, Lớp, Điểm AI chấm, Điểm Giảng viên chốt, Lý do điều chỉnh (nếu có), Chữ ký điện tử.
  - **Xuất biên bản thi PDF (QuestPDF):** Tạo file PDF chuẩn A4 niêm phong kết quả ca thi, có mã QR tra cứu tính toàn vẹn và chữ ký của Trưởng Bộ Môn / Giảng viên.
* **Tiêu chí nghiệm thu (DoD):** File Excel và PDF tải về mở chuẩn xác, không lỗi font tiếng Việt Unicode, khớp 100% dữ liệu điểm trong CSDL.

#### Module BE-V02: API Public Tra Cứu & Xác Thực Bảng Điểm Qua QR Code
* **Kỹ sư phụ trách:** 🧑 **Nguyễn Quang Thành**
* **Endpoint:** `GET /api/v1/verification/grades/{certificateHash}` (Public API)
* **Tệp liên quan:** `Features/Verification/Queries/VerifyGradeCertificate/*`, `VerificationController.cs`
* **Mô tả nghiệp vụ:**
  - Cung cấp cổng tra cứu công khai (không yêu cầu đăng nhập token) khi quét mã QR trên Báo cáo Khảo thí PDF hoặc Certificate bảng điểm:
    * Nhận tham số `certificateHash` từ URL mã QR.
    * Truy vấn và đối chiếu mã băm niêm phong của ca thi trong CSDL.
    * Trả về thông tin xác thực: Tên trường (Đại học FPT), Tên môn thi, Mã ca thi, Ngày thi, Trạng thái niêm phong hợp lệ (`VALID`), Ngày công bố điểm, và Chữ ký số xác nhận của Trưởng Bộ Môn / Giảng viên.
* **Tiêu chí nghiệm thu (DoD):** Endpoint public phản hồi $< 30$ms; kiểm tra mã băm giả mạo trả `HTTP 404 Not Found` kèm cảnh báo chứng chỉ không hợp lệ.

#### Module BE-V03: API Student Dashboard (FE-10 Student Portal)
* **Kỹ sư phụ trách:** 🧑 **Nguyễn Quang Thành**
* **Endpoint:** `GET /api/v1/student/dashboard`
* **Tệp liên quan:** `Features/Student/Queries/GetStudentDashboard/*`, `StudentController.cs`
* **Mô tả nghiệp vụ:**
  - Cung cấp dữ liệu thống kê tổng hợp phục vụ hiển thị trên `StudentDashboardPage.tsx` của sinh viên:
    1. **Thống kê hoạt động:** Tổng số buổi luyện tập đã tham gia, tổng số bài thi thử đã làm trong học kỳ.
    2. **Chỉ số học tập:** Điểm trung bình gần nhất của các phiên luyện tập và thi thử.
    3. **Phân tích độ thành thạo theo Chuẩn Đầu Ra (CLOs):** Tổng hợp điểm số theo từng CLO của các môn học đang theo học để chỉ ra điểm mạnh và điểm yếu kiến thức của sinh viên.
    4. **Lịch thi sắp tới:** Danh sách các ca thi thật mà sinh viên đã được phân bổ vé thi (`StudentExamTicket`) trong kỳ thi hiện tại (gồm Tên môn, Ngày thi, Phòng lab, Kíp thi, Số ghế).
* **Tiêu chí nghiệm thu (DoD):** Phản hồi API chuẩn RFC 7807 với thời gian $< 50$ms. Dữ liệu tính toán chính xác từ CSDL.

---

# 🧑‍💻 PHẦN 4: NHIỆM VỤ CHI TIẾT CỦA NGUYỄN TRỌNG TỐT (BACKEND DEVELOPER, AI SPECIALIST & QA LEAD)

### 🌟 KHỐI TRỌNG TÂM 1: MODULE MF-03 — NGÂN HÀNG ĐỀ & RUBRIC STUDIO 10.0
> **Trạng thái phân hệ:** ⏳ **KẾ HOẠCH SPRINT CỦA NGUYỄN TRỌNG TỐT TỰ CODE**

#### Module BE-3.1: AI Sinh Câu Hỏi Từ FLM Syllabus Theo CLO & Barem Riêng 10.0đ
* **Kỹ sư phụ trách:** 🧑 **Nguyễn Trọng Tốt (AI Specialist)**
* **Endpoint:** `POST /api/v1/questions/generate-from-flm`
* **Tệp liên quan:** `Features/Questions/Commands/GenerateQuestionsFromFlm/*`, `QuestionsController.cs`
* **Mô tả nghiệp vụ:**
  - **Quyền hạn Giảng viên (`lecturer`):** Giảng viên được toàn quyền sử dụng AI sinh câu hỏi theo barem của mình từ Syllabus FLM hoặc đề cương môn học.
  - Nhận input: `courseId`, `syllabusContent` (hoặc bóc tách tự động qua FLM), `cloList`, `rubricGuidelines`, `targetBloomLevel`.
  - Gọi Gemini AI: Tự động phân tích mục tiêu CLO, sinh câu hỏi vấn đáp chuyên sâu, câu trả lời mẫu chuẩn (Model Answer $\ge 50$ ký tự) và bảng barem rubric chuẩn hóa $\sum \equiv 10.0$đ.
  - Trả về danh sách câu hỏi dự thảo để giảng viên xem trước trên Preview Studio và tùy chỉnh tự do trước khi lưu.
* **Tiêu chí nghiệm thu (DoD):** AI sinh câu hỏi đầy đủ tiêu chí; Model Answer $\ge 50$ ký tự; các tiêu chí con có tổng điểm luôn bằng 10.0 điểm.

#### Module BE-3.2: Soạn Câu Hỏi & Barem Rubric Studio 10.0đ (FluentValidation)
* **Kỹ sư phụ trách:** 🧑 **Nguyễn Trọng Tốt**
* **Endpoint:** `POST /api/v1/questions/draft`
* **Tệp liên quan:** `Features/Questions/Commands/CreateQuestionDraft/*`, `Application/Common/Validators/RubricValidator.cs`
* **Mô tả nghiệp vụ:**
  - Tiếp nhận câu hỏi do giảng viên tự soạn thủ công hoặc hiệu chỉnh từ bản AI sinh. Hỗ trợ đính kèm hình ảnh/sơ đồ kỹ thuật (`imageUrl`, `imageDescription` tải lên qua `POST /api/v1/questions/upload-image` hoặc `POST /api/v1/storage/upload-question-image`, kiểm tra Magic Bytes `PNG/JPG/WEBP <= 5MB`, trả `HTTP 413`/`415` nếu vi phạm; chỉ áp dụng cho câu hỏi tạo thủ công `source == "manual"`). Khi chấm bài ở MF-01, MF-02 và MF-04, `GeminiAiGradingService` tự động nạp ảnh dạng Base64 `inline_data` kèm `imageDescription` để chấm điểm đa phương thức (Multimodal AI Grading).
  - Cài đặt `RubricValidator` sử dụng FluentValidation ép cứng quy tắc:
    * **Tổng điểm barem rubric bắt buộc $\sum \equiv 10.0$ điểm** (dung sai sai số số thực $\le 0.001$). Nếu tổng điểm $< 10.0$ hoặc $> 10.0$ (kể cả 9.9đ hay 10.1đ), ném lỗi validation và trả mã **`HTTP 422 Unprocessable Entity`** kèm thông báo: *"Tổng điểm các tiêu chí rubric phải đúng bằng 10.0 điểm"*.
    * Câu trả lời mẫu (Model Answer) bắt buộc $\ge 50$ ký tự.
    * Tối thiểu 2 tiêu chí rubric con, mỗi tiêu chí điểm $> 0$.
* **Tiêu chí nghiệm thu (DoD):** Barem 10.0đ được lưu thành công vào bảng `rubrics` và `rubric_criteria`. Barem 9.5đ hoặc 10.5đ bị chặn 100% tại tầng Validation Behavior.

#### Module BE-3.3: Tick Chọn 2 Kho Đề & Gửi Duyệt Lên Bộ Môn
* **Kỹ sư phụ trách:** 🧑 **Nguyễn Trọng Tốt**
* **Endpoint:** `POST /api/v1/questions/batch-submit-review`
* **Tệp liên quan:** `Features/Questions/Commands/BatchSubmitQuestionsForReview/*`, `QuestionsController.cs`
* **Mô tả nghiệp vụ:**
  - Hỗ trợ lựa chọn kho đề thông qua 2 cờ boolean:
    * `is_for_practice`: Lưu vào kho luyện tập chung (`practice_questions`).
    * `is_for_exam`: Lưu vào kho câu hỏi thi thật phòng Lab (`exam_questions`).
    * Có thể chọn 1 trong 2 hoặc tick chọn cả 2 kho cùng lúc.
  - **Quy trình gửi duyệt:** Mọi câu hỏi do Giảng viên tạo ra đều ở trạng thái ban đầu là `DRAFT`.
  - Giảng viên bấm **"Gửi lên cho Bộ Môn"** (`batch-submit-review`): Chuyển trạng thái sang `SUBMITTED_FOR_REVIEW`, ghi nhận `submitted_by = lecturerId`, `submitted_at = DateTime.UtcNow`. Tự động phát sinh thông báo in-app cho Trưởng Bộ Môn.
* **Tiêu chí nghiệm thu (DoD):** Câu hỏi chuyển sang `SUBMITTED_FOR_REVIEW`, không thể chỉnh sửa nội dung khi đang chờ duyệt.

#### Module BE-3.4: Trưởng Bộ Môn Thẩm Định & Ra Quyết Định Phê Duyệt
* **Kỹ sư phụ trách:** 🧑 **Nguyễn Trọng Tốt**
* **Endpoint:** `PUT /api/v1/questions/{id}/review-decision`
* **Tệp liên quan:** `Features/Questions/Commands/ReviewQuestionDecision/*`, `QuestionsController.cs`
* **Mô tả nghiệp vụ:**
  - **Trưởng Bộ Môn (`department_head`)** là người duy nhất có thẩm quyền thẩm định và ra quyết định trên các câu hỏi chờ duyệt:
    1. **Phê duyệt (`APPROVED`):** Câu hỏi được lưu chính thức vào ngân hàng câu hỏi môn học và sẵn sàng được rút ra làm đề thi.
    2. **Yêu cầu chỉnh sửa (`NEEDS_REVISION`):** Bắt buộc nhập lý do góp ý $\ge 10$ ký tự vào `review_notes`. Câu hỏi trả về cho giảng viên để chỉnh sửa và gửi duyệt lại.
    3. **Từ chối (`REJECTED`):** Bắt buộc nhập lý do từ chối vào `review_notes`. Câu hỏi bị loại bỏ khỏi danh sách sử dụng.
* **Tiêu chí nghiệm thu (DoD):** Phân quyền chỉ `department_head` mới được duyệt câu hỏi. Đầy đủ 3 trạng thái quyết định; ghi nhận `approved_by` và `review_notes` vào CSDL.

---

### 🌟 KHỐI TRỌNG TÂM 2: MODULE MF-04 — THI THẬT PHÒNG LAB, CÔNG BỐ ĐIỂM & PHÚC KHẢO NỘI BỘ
> **Trạng thái phân hệ:** ⏳ **KẾ HOẠCH SPRINT CỦA NGUYỄN TRỌNG TỐT TỰ CODE**

#### Module BE-4.1: Quản Trị Kỳ Thi & Cấu Hình Môn Thi Của Trưởng Bộ Môn
* **Kỹ sư phụ trách:** 🧑 **Nguyễn Trọng Tốt**
* **Endpoint:** `POST /api/v1/official-exams/seasons`, `POST /api/v1/official-exams/seasons/{id}/shifts`, `PUT /api/v1/courses/{id}/exam-config`
* **Tệp liên quan:** `Features/OfficialExams/Commands/*`, `OfficialExamsController.cs`
* **Mô tả nghiệp vụ:**
  - **Trưởng Bộ Môn (`department_head`)** khởi tạo Kỳ thi (`OfficialExamSession`, ví dụ Kỳ thi Kết thúc môn FA26) và gán danh sách các môn thi thuộc kỳ thi.
  - **Cấu hình Ca thi (`RealExamSessionShift`):**
    * Chọn phòng máy lab trực tiếp trên ca thi (Phòng Lab 301, 302, ...).
    * Phân công người coi thi trực tiếp (chọn Giám thị `proctor` hoặc Giảng viên `lecturer` coi thi).
    * Thiết lập thời gian bắt đầu, thời gian kết thúc, số lượng máy trạm tối đa (40 máy).
  - **Cấu hình Môn thi trong kỳ thi:**
    * Cấu hình Follow-up: Bật/tắt (`has_follow_up`) và số lượng câu hỏi phụ tối đa (`max_follow_up_questions`, từ 1 đến 5 câu, mặc định 2 câu).
    * Cấu hình phương thức làm bài `ExamInputMode`: Gồm 2 giá trị chuẩn hóa `VoiceOnly` (Chỉ dùng Mic) hoặc `VoiceWithTranscriptEdit` (Nói qua Mic kèm sửa transcript trong màn hình đệm). **Tuyệt đối loại bỏ giá trị `VoiceAndTextInput`**.
    * Cấu hình thời gian đệm `TranscriptBufferSeconds`: Từ 10 đến 300 giây (mặc định 60 giây).
* **Tiêu chí nghiệm thu (DoD):** Ca thi được lưu vào `real_exam_session_shifts` có thông tin phòng lab và người coi thi. Môn thi cập nhật đúng các cờ cấu hình.

#### Module BE-4.2: Check-in Kiosk Phòng Lab Ràng Buộc IP `ip_address` (Ghế 1–40)
* **Kỹ sư phụ trách:** 🧑 **Nguyễn Trọng Tốt**
* **Endpoint:** `POST /api/v1/official-exams/tickets/{id}/check-in`
* **Tệp liên quan:** `Features/OfficialExams/Commands/CheckInKiosk/*`, `OfficialExamsController.cs`
* **Mô tả nghiệp vụ:**
  - Định danh máy trạm Kiosk: Ràng buộc số thứ tự ghế ngồi (SeatNumber từ 1 đến 40) với địa chỉ IP của máy trạm.
  - **Sử dụng thống nhất tên cột `ip_address`** (nghiêm cấm dùng `workstation_ip`).
  - Khi thí sinh nhập STT và MSSV tại máy Kiosk: Backend đọc IP của request client (`HttpContext.Connection.RemoteIpAddress`), đối chiếu với IP đã gán cho số ghế đó trong danh sách ca thi phòng Lab.
  - Nếu sai IP: Từ chối check-in, trả về mã lỗi **`HTTP 403 Forbidden`**: *"Máy trạm không hợp lệ cho vị trí thi số {SeatNumber}"*.
  - Nếu khớp IP: Chuyển trạng thái vé thi từ `SCHEDULED` sang `IN_PROGRESS`.
  - **Quản lý đúng 7 trạng thái vé thi viết HOA:** `SCHEDULED` $\to$ `IN_PROGRESS` $\to$ `SUBMITTED` $\to$ `AI_GRADED` $\to$ `AUDITED` $\to$ `PUBLISHED` $\to$ `LOCKED`.
* **Tiêu chí nghiệm thu (DoD):** Check-in sai IP trả HTTP 403; check-in đúng IP cập nhật trạng thái `IN_PROGRESS` và trả về token làm bài của Kiosk.

#### Module BE-4.3: Nộp Bài Persist First $< 100$ms & Upload Audio R2 `STT_MSSV.webm`
* **Kỹ sư phụ trách:** 🧑 **Nguyễn Trọng Tốt**
* **Endpoint:** `POST /api/v1/official-exams/tickets/{id}/submit`
* **Tệp liên quan:** `Features/OfficialExams/Commands/SubmitOfficialExam/*`, `Infrastructure/Services/CloudflareR2Service.cs`
* **Mô tả nghiệp vụ:**
  - Tiếp nhận luồng audio từ Kiosk và upload lên Cloudflare R2 với quy tắc đặt tên bất biến: **`STT_MSSV.webm`** (ví dụ: `01_SE170123.webm`).
  - Tính toán mã băm mật mã SHA-256 niêm phong audio, đối chiếu với mã băm do Kiosk gửi lên để đảm bảo tính toàn vẹn 100% không bị can thiệp.
  - **Persist First $< 100$ms:** Lưu ngay lập tức bản ghi vào bảng `exam_question_submissions` và cập nhật `student_exam_tickets` sang trạng thái **`SUBMITTED` trong $< 100$ms** kèm URL audio R2 và mã băm SHA-256.
  - **Quy tắc phòng thi bất biến:** Kiosk khóa màn hình và hiển thị thông báo an toàn: *"Bài thi đã được lưu trữ an toàn. Kết quả sẽ do Giảng viên thẩm định và công bố trên hệ thống."* **Tuyệt đối KHÔNG có điểm liền và KHÔNG khiếu nại tại chỗ**. Thí sinh ký biên bản nộp bài giấy và ra về.
* **Tiêu chí nghiệm thu (DoD):** Phản hồi API nộp bài $< 100$ms. File âm thanh được lưu an toàn trên Cloudflare R2 với tên `STT_MSSV.webm` kèm SHA-256 niêm phong.

#### Module BE-4.4: BoundedChannel / DLQ Chấm Ngầm, AI Doubt Guard & Evidence Panel
* **Kỹ sư phụ trách:** 🧑 **Nguyễn Trọng Tốt (AI Specialist)**
* **Endpoint:** `GET /api/v1/audit/shifts/{shiftId}/submissions`, `POST /api/v1/audit/submissions/{id}/override-score`
* **Tệp liên quan:** `Features/Audit/*`, `Infrastructure/Services/GeminiEvaluationService.cs`, `Infrastructure/BackgroundJobs/GradingQueueWorker.cs`
* **Mô tả nghiệp vụ:**
  - Tác vụ chấm điểm thi thật được đẩy vào `BoundedGradingQueueChannel` (1,000 slots RAM) để Background Worker gọi Gemini AI chấm ngầm toàn bộ bài thi chỉ dựa trên bản *transcript*.
  - **Polly Resilience & DLQ:** Retry 3 lần với Exponential Backoff (2s, 4s, 8s). Khi Gemini bị lỗi mạng hoặc HTTP 429 quá 3 lần: Cách ly bài thi vào bảng `dead_letter_queues` để tiến trình nền tự động retry sau 5 phút, bảo đảm Zero Data Loss 100%.
  - **Chốt chặn AI Doubt Guard:** Tự động gắn cờ `is_suspicious = true` khi:
    * Chỉ số tin cậy của AI `confidence_score < 0.70` (do ồn, phát âm không rõ, mâu thuẫn chuỗi CoT).
    * Bài thi có điểm số rơi vào khoảng ranh giới hoặc bị fail / điểm liệt.
  - **Cổng Hậu kiểm Evidence Panel cho Giảng viên:**
    * Tự động phân chia 2 nhóm: Nhóm 1 (Đáng nghi ngờ / Fail / Cần can thiệp) và Nhóm 2 (Độ tin cậy cao).
    * Cung cấp dữ liệu đối chiếu toàn diện: AudioURL Cloudflare R2, Transcript Whisper gốc, Chuỗi suy luận AI CoT từng tiêu chí rubric.
    * API Giảng viên điều chỉnh điểm `override-score`: Bắt buộc nhập lý do giải trình bắt buộc (`override_reason`) $\ge 10$ ký tự. Lưu vết vào `lecturer_audits` và `lecturer_audit_details`.
* **Tiêu chí nghiệm thu (DoD):** Bài thi có `confidence < 0.70` tự động gắn `is_suspicious = true`. Sửa điểm không có lý do bị chặn `HTTP 400`.

#### Module BE-4.5: Công Bố Điểm Atomic & Khóa Một Chiều `OneWayLockInterceptor`
* **Kỹ sư phụ trách:** 🧑 **Nguyễn Trọng Tốt**
* **Endpoint:** `POST /api/v1/official-exams/shifts/{shiftId}/publish-grades`
* **Tệp liên quan:** `Features/OfficialExams/Commands/PublishGrades/*`, `Infrastructure/Interceptors/OneWayLockInterceptor.cs`
* **Mô tả nghiệp vụ:**
  - **Quy trình Công Bố Điểm của Giảng viên:**
    * Sinh viên bắt buộc phải đợi Giảng viên chấm xong toàn bộ các bài trong nhóm nghi ngờ/fail.
    * Khi bảo đảm **100% sinh viên trong ca thi đã có điểm hoàn chỉnh**, Giảng viên bấm nút **"Công Bố Điểm"** một lần duy nhất.
    * Endpoint kiểm tra: Nếu còn bất kỳ thí sinh nào chưa có điểm hoặc ca thi chưa hoàn tất hậu kiểm, từ chối công bố và trả `HTTP 400 Bad Request`.
    * Cập nhật trạng thái vé thi của toàn bộ sinh viên trong ca thi từ `AUDITED` sang `PUBLISHED`.
  - **Kích hoạt One-Way Lock:** Đánh dấu `is_locked = true` trên thực thể ca thi.
  - `OneWayLockInterceptor` (EF Core SaveChangesInterceptor): Chặn đứng 100% mọi câu lệnh `UPDATE` hoặc `DELETE` tác động lên bảng điểm và bài thi đã bị khóa, lập tức ném `OneWayLockException` chuyển đổi thành mã phản hồi **`HTTP 403 Forbidden`**.
* **Tiêu chí nghiệm thu (DoD):** Điểm chỉ công bố khi 100% sinh viên đã có điểm. Sau khi khóa, mọi thao tác sửa điểm qua API thông thường đều bị chặn `HTTP 403`.

#### Module BE-4.6: Phân Hệ Phúc Khảo Nội Bộ `AppealRequest` & Acknowledge Điểm
* **Kỹ sư phụ trách:** 🧑 **Nguyễn Trọng Tốt**
* **Endpoint:** `POST /api/v1/appeals`, `GET /api/v1/appeals`, `PUT /api/v1/appeals/{id}/assign-lecturer`, `PUT /api/v1/appeals/{id}/decision`, `POST /api/v1/official-exams/tickets/{id}/acknowledge`
* **Tệp liên quan:** `Features/Appeals/*`, `AppealsController.cs`, `OfficialExamsController.cs`
* **Mô tả nghiệp vụ:**
  - Sau khi Giảng viên công bố điểm, sinh viên ở nhà đăng nhập Student Portal để xem bảng điểm chính thức:
    * Nếu đồng ý với kết quả: Bấm xác nhận nhận điểm (`POST /acknowledge`), cập nhật trạng thái `student_acknowledgement_status = ACKNOWLEDGED`.
    * Nếu không đồng ý kết quả: Sinh viên làm đơn phúc khảo trực tiếp trên hệ thống (`POST /api/v1/appeals`) kèm lý do khiếu nại cụ thể.
  - Hệ thống tạo entity `AppealRequest` ở trạng thái `PENDING` và tự động gán cho **Trưởng Bộ Môn (`department_head`)** tiếp nhận thẩm định độc lập.
  - **Cơ chế Phân công Chấm lại của Trưởng Bộ Môn:**
    * Trưởng Bộ Môn xem xét đơn, có thể tự chấm hoặc gọi `PUT /api/v1/appeals/{id}/assign-lecturer` để giao cho một Giảng viên khác chấm lại bài thi.
    * Giảng viên được giao chấm lại sẽ truy cập Evidence Panel của bài thi, nghe lại file ghi âm và cập nhật điểm phúc khảo.
    * Trưởng Bộ Môn đưa ra quyết định cuối cùng (`APPROVED` điều chỉnh điểm / `REJECTED` giữ nguyên điểm) qua `PUT /api/v1/appeals/{id}/decision`.
* **Tiêu chí nghiệm thu (DoD):** Quy trình phúc khảo nội bộ khép kín 100% trong hệ thống; Trưởng Bộ Môn có toàn quyền tiếp nhận và phân công giảng viên chấm lại; điểm mới được cập nhật có audit log rõ ràng.

---

### 🛠️ CÁC MODULE VỆ TINH, HỆ THỐNG & QA CỦA NGUYỄN TRỌNG TỐT

#### Module BE-V04: Cơ Chế Xác Thực Kép Dual Auth (Google OAuth 2.0 PKCE + Tài Khoản Trường Cấp)
* **Kỹ sư phụ trách:** 🧑 **Nguyễn Trọng Tốt (Lead Auth & Security)**
* **Endpoint:** `POST /api/v1/auth/google-login`, `POST /api/v1/auth/password-login`, `POST /api/v1/auth/change-password`, `POST /api/v1/auth/refresh-token`
* **Tệp liên quan:** `Features/Auth/*`, `API/Controllers/v1/AuthController.cs`, `User.cs`
* **Mô tả nghiệp vụ:**
  - Tiếp nhận Google ID Token từ Frontend (`POST /api/v1/auth/google-login`) hoặc thông tin đăng nhập tài khoản trường cấp (`POST /api/v1/auth/password-login`) cùng đổi mật khẩu (`POST /api/v1/auth/change-password`).
  - **Mở rộng phạm vi người dùng:** Chấp nhận mọi email Google hợp lệ (loại bỏ ràng buộc cứng chỉ cho phép `@fpt.edu.vn` hoặc `@fe.edu.vn` để phục vụ linh hoạt tài khoản demo và hội đồng chấm bảo vệ) và hỗ trợ tài khoản `username`/`password` do Nhà trường cấp phát sẵn (lưu `password_hash` chuẩn PBKDF2 kèm cờ `must_change_password`; không mở form tự đăng ký công khai).
  - Tìm kiếm người dùng theo email/username trong bảng `users`: Với Google OAuth nếu chưa tồn tại thì tự động tạo mới tài khoản với vai trò mặc định `student` (hoặc vai trò do Admin phân quyền trước đó).
  - Cấp cặp JWT Token: Access Token 60 phút (chứa claims `sub`, `email`, `name`, `role`), Refresh Token 7 ngày lưu trong CSDL.
* **Tiêu chí nghiệm thu (DoD):** Đăng nhập qua Google OAuth hoặc tài khoản trường cấp hợp lệ đều trả về HTTP 200 kèm JWT claims chuẩn. Gọi API không có Bearer token trả `HTTP 401 Unauthorized`.

#### Module BE-V05: API Quản Lý Người Dùng User Management (FE-09)
* **Kỹ sư phụ trách:** 🧑 **Nguyễn Trọng Tốt**
* **Endpoint:** `GET /api/v1/admin/users`, `PUT /api/v1/admin/users/{id}/status`, `PUT /api/v1/admin/users/{id}/role`
* **Tệp liên quan:** `Features/Admin/Users/*`, `AdminController.cs`
* **Mô tả nghiệp vụ:**
  - `GET /api/v1/admin/users`: Trả về danh sách người dùng có phân trang, tìm kiếm theo tên, email, lọc theo role (`student`, `lecturer`, `department_head`, `proctor`, `admin`).
  - `PUT /api/v1/admin/users/{id}/status`: Khóa hoặc kích hoạt tài khoản người dùng (`is_active = true/false`). Khi bị khóa, JWT của user lập tức bị từ chối.
  - `PUT /api/v1/admin/users/{id}/role`: Cho phép Admin gán hoặc chuyển đổi vai trò người dùng sang 1 trong 5 vai trò hệ thống. Không mở form tự đăng ký công khai cho người dùng vãng lai.
* **Tiêu chí nghiệm thu (DoD):** Phân quyền chỉ `admin` mới được gọi endpoints này (`[Authorize(Roles = "admin")]`). Cập nhật role thành công và phản ánh ngay trong token tiếp theo.

#### Module BE-V06: API Quản Lý Học Kỳ Semester CRUD (FE-08)
* **Kỹ sư phụ trách:** 🧑 **Nguyễn Trọng Tốt**
* **Endpoint:** `GET /api/v1/semesters`, `POST /api/v1/semesters`, `PUT /api/v1/semesters/{id}`
* **Tệp liên quan:** `Features/Semesters/*`, `SemestersController.cs`, `Semester.cs`
* **Mô tả nghiệp vụ:**
  - `GET /api/v1/semesters`: Lấy danh sách học kỳ (Mã học kỳ: FA26, SP26; Tên: Fall 2026; Ngày bắt đầu, Ngày kết thúc, Trạng thái hoạt động).
  - `POST /api/v1/semesters`: Admin tạo mới học kỳ, validate không trùng mã học kỳ.
  - `PUT /api/v1/semesters/{id}`: Cập nhật thông tin học kỳ hoặc đặt làm học kỳ hiện tại (`is_current = true`).
  - **Quy tắc bất biến:** **Tuyệt đối KHÔNG có endpoint xóa học kỳ** (bảo toàn lịch sử dữ liệu và các ca thi liên kết).
* **Tiêu chí nghiệm thu (DoD):** Phân quyền chỉ `admin` được tạo/sửa học kỳ. API trả về danh sách học kỳ phục vụ chọn kỳ học trên toàn hệ thống.

#### Module BE-V07: Hộp Thư Notification In-App (FE-11)
* **Kỹ sư phụ trách:** 🧑 **Nguyễn Trọng Tốt**
* **Endpoint:** `GET /api/v1/notifications`, `PUT /api/v1/notifications/{id}/read`, `PUT /api/v1/notifications/read-all`
* **Tệp liên quan:** `Features/Notifications/*`, `NotificationsController.cs`, `Notification.cs`
* **Mô tả nghiệp vụ:**
  - `GET /api/v1/notifications`: Lấy danh sách thông báo in-app của người dùng hiện tại (lọc theo `user_id`), sắp xếp thời gian giảm dần, đếm số thông báo chưa đọc.
  - `PUT /api/v1/notifications/{id}/read`: Đánh dấu thông báo cụ thể là đã đọc (`is_read = true`).
  - **Tự động sinh thông báo khi phát sinh các sự kiện hệ thống:**
    1. Lịch thi phòng Lab được công bố $\to$ Gửi thông báo cho toàn bộ sinh viên trong ca thi.
    2. Điểm thi chính thức được công bố $\to$ Gửi thông báo cho sinh viên xem điểm.
    3. Giảng viên gửi câu hỏi duyệt $\to$ Gửi thông báo cho Trưởng Bộ Môn.
    4. Trưởng Bộ Môn phê duyệt / yêu cầu sửa câu hỏi $\to$ Gửi thông báo cho Giảng viên.
    5. Sinh viên nộp đơn phúc khảo $\to$ Gửi thông báo cho Trưởng Bộ Môn.
    6. Trưởng Bộ Môn giao chấm phúc khảo $\to$ Gửi thông báo cho Giảng viên được phân công.
* **Tiêu chí nghiệm thu (DoD):** Bảng `notifications` lưu đầy đủ thông báo; API đọc/đánh dấu đã đọc hoạt động $< 30$ms.

#### Module BE-V08: API Quản Trị Cấu Hình Hệ Thống Admin (`system_configs`)
* **Kỹ sư phụ trách:** 🧑 **Nguyễn Trọng Tốt**
* **Endpoint:** `GET /api/v1/admin/configs`, `PUT /api/v1/admin/configs/{key}`
* **Tệp liên quan:** `Features/Admin/Configs/*`, `AdminController.cs`, `SystemConfig.cs`
* **Mô tả nghiệp vụ:**
  - Cung cấp API phục vụ màn hình `AdminConfigPage.tsx` của Admin:
    * `GET /api/v1/admin/configs`: Lấy danh sách các tham số cấu hình động toàn cục của hệ thống (Key-Value): `MaxPracticeQuestionsPerSession`, `MinMixedPracticeQuestions`, `MaxMixedPracticeQuestions`, `TranscriptBufferSeconds`, `MaxPracticeFollowUpQuestions`, `SessionInactivityTimeoutMinutes`.
    * `PUT /api/v1/admin/configs/{key}`: Admin cập nhật trực tiếp giá trị cấu hình runtime vào bảng `system_configs`.
    * Cài đặt validation kiểm tra chặt chẽ: `TranscriptBufferSeconds` từ 10 đến 300 giây; `MaxPracticeFollowUpQuestions` từ 1 đến 5 câu; `MinMixedPracticeQuestions` từ 3 đến 5 câu và không vượt quá `MaxMixedPracticeQuestions` (6–10 câu); `SessionInactivityTimeoutMinutes` từ 5 đến 60 phút.
    * Ghi vết kiểm toán người cập nhật (`updated_by = adminId`) và thời gian cập nhật vào `audit_logs`.
* **Tiêu chí nghiệm thu (DoD):** Phân quyền chỉ `admin` được gọi. Cập nhật cấu hình có hiệu lực ngay lập tức cho các phiên luyện tập/thi tiếp theo mà không cần khởi động lại Server.

#### Module BE-V09: Quản Trị Dead-Letter Queue (DLQ Replay 5m) & API Audit Logs
* **Kỹ sư phụ trách:** 🧑 **Nguyễn Trọng Tốt**
* **Endpoint:** `GET /api/v1/dlq/tasks`, `POST /api/v1/dlq/tasks/{id}/replay`, `GET /api/v1/admin/audit-logs`
* **Tệp liên quan:** `Features/DeadLetterQueue/*`, `DeadLetterQueueController.cs`, `Infrastructure/BackgroundJobs/DlqReplayWorker.cs`, `AdminController.cs`
* **Mô tả nghiệp vụ:**
  - Bảng `dead_letter_queues` lưu trữ toàn bộ các task chấm thi thất bại sau 3 lần retry của Polly.
  - Background Service `DlqReplayWorker` tự động quét định kỳ sau 5 phút để kích hoạt chấm bù cho các task bị lỗi tạm thời do nghẽn mạng hoặc quá tải API.
  - Trưởng Bộ Môn có dashboard theo dõi danh sách DLQ, log lỗi và bấm "Replay Task" thủ công khi cần; Admin quản trị nhật ký kiểm toán hệ thống.
  - `GET /api/v1/admin/audit-logs`: Truy vấn nhật ký kiểm toán hệ thống từ bảng `audit_logs` (thao tác đổi điểm, phê duyệt đề, khóa điểm, gán phúc khảo).
* **Tiêu chí nghiệm thu (DoD):** Cam kết Zero Data Loss 100%, không bài làm nào của sinh viên bị thất lạc kể cả khi dịch vụ AI bên ngoài gặp sự cố kéo dài.

#### Module BE-V10: API Giám Thị Phòng Lab & Điểm Danh Ca Thi (Proctor Monitor)
* **Kỹ sư phụ trách:** 🧑 **Nguyễn Trọng Tốt**
* **Endpoint:** `GET /api/v1/proctor/shifts/{shiftId}/attendance`, `PUT /api/v1/proctor/tickets/{id}/attendance`, `POST /api/v1/proctor/tickets/{id}/suspend`
* **Tệp liên quan:** `Features/Proctor/*`, `ProctorController.cs`, `StudentExamTicket.cs`
* **Mô tả nghiệp vụ:**
  - Cung cấp API phục vụ màn hình Giám sát phòng lab `ProctorRoomMonitorPage.tsx` (FE-P01):
    * `GET /api/v1/proctor/shifts/{shiftId}/attendance`: Lấy danh sách 40 máy lab trong ca thi gồm: STT (1–40), MSSV, Họ tên, Trạng thái vé thi (`SCHEDULED`, `IN_PROGRESS`, `SUBMITTED`, `SUSPENDED`), địa chỉ IP máy trạm `ip_address`, số lần cảnh báo mất focus Kiosk (`blur_count`).
    * `PUT /api/v1/proctor/tickets/{id}/attendance`: Giám thị đối chiếu thẻ SV/CCCD thực tế tại phòng lab, đánh dấu `is_present = true` (có mặt) hoặc `false` (vắng thi).
    * `POST /api/v1/proctor/tickets/{id}/suspend`: Giám thị lập biên bản đình chỉ thi khi phát hiện gian lận hoặc khi Kiosk gửi cảnh báo mất focus quá 3 lần; vé thi chuyển sang trạng thái `SUSPENDED` và khóa máy Kiosk của thí sinh.
* **Tiêu chí nghiệm thu (DoD):** Phân quyền `proctor` hoặc `lecturer` được phân công coi thi mới được gọi; trạng thái vé thi cập nhật chuẩn xác theo thời gian thực.

#### Module BE-V11: API Dashboard Giảng Viên (FE-10)
* **Kỹ sư phụ trách:** 🧑 **Nguyễn Trọng Tốt**
* **Endpoint:** `GET /api/v1/lecturer/dashboard`
* **Tệp liên quan:** `Features/Lecturer/Queries/*`, `QuestionsController.cs`
* **Mô tả nghiệp vụ:**
  - Thống kê tổng số câu hỏi giảng viên đã tạo trong từng môn học.
  - Phân loại số lượng câu hỏi theo trạng thái: `DRAFT`, `SUBMITTED_FOR_REVIEW`, `APPROVED`, `NEEDS_REVISION`.
  - Tỷ lệ câu hỏi được duyệt thành công. Danh sách các câu hỏi bị yêu cầu chỉnh sửa cần xử lý gấp.
* **Tiêu chí nghiệm thu (DoD):** API trả về thống kê tổng hợp chính xác $< 40$ms, phục vụ hiển thị trực quan trên `LecturerDashboardPage.tsx`.

#### Module BE-QA: Bộ Test Suites xUnit & Architecture Tests NetArchTest
* **Kỹ sư phụ trách:** 🧑 **Nguyễn Trọng Tốt (QA Lead)**
* **Tệp liên quan:** `tests/OralExamination.UnitTests/*`, `tests/OralExamination.ArchitectureTests/*`
* **Mô tả nghiệp vụ:**
  - Viết xUnit bao phủ 100% các quy tắc nghiệp vụ:
    1. Validation Rules: Barem rubric $\sum \equiv 10.0$đ, Model Answer $\ge 50$ ký tự, lý do sửa điểm $\ge 10$ ký tự.
    2. Quota Guard: Chặn lượt thứ 4 trả `HTTP 429 Too Many Requests`.
    3. One-Way Lock: Assert ném `HTTP 403 Forbidden` khi sửa ca thi `is_locked = true`.
    4. BoundedChannel: Assert Enqueue/Dequeue FIFO, Backpressure 1,000 slots RAM.
    5. Clean Architecture Integrity: Dùng NetArchTest khóa ranh giới 4 tầng (Domain không dính EF/Infra, Application không dính API, Controller không gọi DbContext trực tiếp).
* **Tiêu chí nghiệm thu (DoD):** Chạy `dotnet test` đạt **100% tests PASS (536/536 tests pass 100%, Exit Code 0)**, thời gian chạy toàn bộ test suite $< 5$ giây.

---

# 🚀 PHẦN 5: CHECKLIST NGHIỆM THU TIÊU CHUẨN KỸ THUẬT (ACCEPTANCE CRITERIA)

| Mã tiêu chí | Nội dung nghiệm thu kỹ thuật | Kỹ sư chịu trách nhiệm | Tiêu chuẩn đạt |
| :---: | :--- | :---: | :---: |
| **AC-01** | `dotnet build` và `dotnet test` chạy thành công không có lỗi compile | 🧑 Toàn bộ Team BE | Exit Code = 0, 0 C# Error |
| **AC-02** | Dual Auth (Google OAuth PKCE + tài khoản trường cấp), cấp JWT 5 roles, đổi mật khẩu | 🧑 Nguyễn Trọng Tốt | Token trả đúng claims, 401 khi không có Bearer |
| **AC-03** | MF-01 khởi tạo On-Demand & Progressive 3–10 câu, chặn thiếu đề HTTP 400 | 🧑 Nguyễn Quang Thành | Cấp câu 1 ngay, progressive tăng dần Bloom |
| **AC-04** | BoundedChannel 1,000 slots RAM, Backpressure an toàn không crash server | 🧑 Nguyễn Quang Thành | Chịu tải 1,200 task đồng thời, 0 OOM |
| **AC-05** | Whisper Streaming & Persist First $< 100$ms, bỏ AudioUrl & bỏ R2 ở MF-01 | 🧑 Nguyễn Quang Thành | Trả HTTP 202 Accepted $< 100$ms, không lưu file R2 |
| **AC-06** | Gemini 1.5 Flash CoT 3 bước, Follow-up 4.0–8.0đ, Inactivity Timeout 5m | 🧑 Nguyễn Quang Thành | JSON output chuẩn 100%, trả HTTP 410 khi quá 5m |
| **AC-07** | MF-02 Quota Guard chặn quá hạn ngạch trả `HTTP 429 Too Many Requests` | 🧑 Nguyễn Quang Thành | Lượt $K+1$ trả RFC 7807 ProblemDetails 429 |
| **AC-08** | MF-02 Voice-First Gate chỉ nhận transcript, bốc đề cô lập từ `practice_questions` | 🧑 Nguyễn Quang Thành | 0 câu hỏi rò rỉ từ `exam_questions`, timer chuẩn |
| **AC-09** | Barem Rubric Studio FluentValidation ép $\sum \equiv 10.0$đ (`HTTP 422`) & Model Answer $\ge 50$ | 🧑 Nguyễn Trọng Tốt | Lệch 10.0đ chặn 100%, Model Answer ngắn bị từ chối |
| **AC-10** | Trưởng BM duyệt đề 3 quyết định (`APPROVED`, `NEEDS_REVISION`, `REJECTED`) | 🧑 Nguyễn Trọng Tốt | NEEDS_REVISION bắt buộc lý do $\ge 10$ ký tự |
| **AC-11** | Kiosk Check-In IP Binding `ip_address` Ghế 1–40, sai IP trả `HTTP 403` | 🧑 Nguyễn Trọng Tốt | 7 trạng thái vé thi HOA, bảo mật vị trí máy lab |
| **AC-12** | Nộp bài MF-04 Persist First $< 100$ms `SUBMITTED`, Upload R2 `STT_MSSV.webm` + SHA-256 | 🧑 Nguyễn Trọng Tốt | SHA-256 niêm phong audio, 0% điểm liền tại Kiosk |
| **AC-13** | Công Bố Điểm Atomic 100% sinh viên, `OneWayLockInterceptor` khóa HTTP 403 | 🧑 Nguyễn Trọng Tốt | Sửa điểm khi locked trả `HTTP 403 Forbidden` |
| **AC-14** | Phân hệ Phúc khảo nội bộ `AppealRequest`, Trưởng BM giao GV khác chấm lại | 🧑 Nguyễn Trọng Tốt | One-Way Lock mở đường hợp lệ cho GV được giao |
| **AC-15** | Báo cáo Khảo thí FPT xuất file Excel `.xlsx` (EPPlus) và PDF (QuestPDF) | 🧑 Nguyễn Quang Thành | Chuẩn mẫu Đại học FPT, không lỗi font Unicode |
| **AC-16** | Public QR Code tra cứu bảng điểm không cần token, xác thực mã băm | 🧑 Nguyễn Quang Thành | Endpoint public $< 30$ms, giả mạo trả HTTP 404 |

---

# 📊 PHẦN 6: BẢNG MA TRẬN TỔNG HỢP TIẾN ĐỘ SPRINT (LIVE STATUS MATRIX)

| Mã Task | Tên Phân Hệ & Module Chức Năng | Kỹ Sư Phụ Trách | Trạng Thái Thực Tế |
|:---|:---|:---:|:---:|
| **BE-1.1** | Khởi tạo phiên luyện tập `POST /api/v1/practice/sessions` (Per/Full, Progressive 3-10, On-Demand) | **🧑 Thành** | ✅ **Đã hoàn thành 100% (Thành đã làm)** |
| **BE-1.2** | Hàng đợi RAM `BoundedGradingQueueChannel` (1,000 slots RAM, `FullMode.Wait`) | **🧑 Thành** | ✅ **Đã hoàn thành 100% (Thành đã làm)** |
| **BE-1.3** | Nộp bài Per/Full Persist First $< 100$ms, Stream Whisper STT (bỏ `AudioUrl`, bỏ R2) | **🧑 Thành** | ✅ **Đã hoàn thành 100% (Thành đã làm)** |
| **BE-1.4** | Background Worker `GradingQueueWorker` & SignalR Realtime Hub `/hubs/practice` | **🧑 Thành** | ✅ **Đã hoàn thành 100% (Thành đã làm)** |
| **BE-1.5** | Gemini 1.5 Flash CoT 3 bước & Follow-up Engine ($4.0 \le \text{Score} \le 8.0$, 1-5 câu) | **🧑 Thành** | ✅ **Đã hoàn thành 100% (Thành đã làm)** |
| **BE-1.6** | Hoàn tất phiên, Lịch sử luyện tập & Chi tiết Scorecard Rubric | **🧑 Thành** | ✅ **Đã hoàn thành 100% (Thành đã làm)** |
| **BE-1.7** | API NextQuestion On-Demand & Thuật toán Anti-3-Consecutive Randomizer | **🧑 Thành** | ✅ **Đã hoàn thành 100% (Thành đã làm)** |
| **BE-1.8** | Cơ chế Timeout 5 Phút Không Tương Tác (Session Inactivity Timeout 410 Gone) | **🧑 Thành** | ✅ **Đã hoàn thành 100% (Thành đã làm)** |
| **BE-2.1** | Daily Quota Guard MF-02 ($K \le 3$ do Admin cấu hình, chặn HTTP 429) | **🧑 Thành** | ✅ **Đã hoàn thành 100% (Thành đã làm)** |
| **BE-2.2** | Bốc đề thi thử theo Bloom từ kho `practice_questions` (cô lập an toàn `exam_questions`) | **🧑 Thành** | ✅ **Đã hoàn thành 100% (Thành đã làm)** |
| **BE-2.3** | Server Master Timer & Voice-First Gate (chỉ lưu transcript, muộn 10s đánh dấu `is_late`) | **🧑 Thành** | ✅ **Đã hoàn thành 100% (Thành đã làm)** |
| **BE-2.4** | Chấm 2 pha / Async Grading trả Instant Scorecard theo CLO & Lịch sử thi thử | **🧑 Thành** | ✅ **Đã hoàn thành 100% (Thành đã làm)** |
| **BE-V01** | Module Báo cáo Khảo thí FPT xuất bảng điểm Excel `.xlsx` và PDF có chữ ký số SHA-256 | **🧑 Thành** | ✅ **Đã hoàn thành 100% (Thành đã làm)** |
| **BE-V02** | API Public Tra cứu & Xác thực Bảng điểm Chữ ký số qua QR Code | **🧑 Thành** | ✅ **Đã hoàn thành 100% (Thành đã làm)** |
| **BE-V03** | API Student Dashboard (thống kê buổi tập, điểm TB, độ thành thạo CLO, banner lịch thi) | **🧑 Thành** | ✅ **Đã hoàn thành 100% (Thành đã làm)** |
| **BE-3.1** | AI sinh câu hỏi từ FLM Syllabus theo CLO & Barem riêng 10.0đ | **🧑 Tốt** | ✅ **Đã hoàn thành 100%** |
| **BE-3.2** | FluentValidation ép Barem Rubric $\sum \equiv 10.0$đ (`HTTP 422`) & Model Answer $\ge 50$ ký tự | **🧑 Tốt** | ✅ **Đã hoàn thành 100%** |
| **BE-3.3** | Checkbox tick chọn 2 kho `practice_questions`/`exam_questions` (`UsageScope = "both"`) & Gửi duyệt Bộ Môn | **🧑 Tốt** | ✅ **Đã hoàn thành 100%** |
| **BE-3.4** | Trưởng Bộ Môn phê duyệt câu hỏi (APPROVED / NEEDS_REVISION / REJECTED) | **🧑 Tốt** | ✅ **Đã hoàn thành 100%** |
| **BE-4.1** | Trưởng BM tạo kỳ thi, cấu hình ca thi (phòng lab, người coi thi), follow-up (1-5 câu) | **🧑 Tốt** | ✅ **Đã hoàn thành 100%** |
| **BE-4.2** | Check-in Kiosk IP Binding `ip_address` (Ghế 1-40, HTTP 403, 7 trạng thái vé thi HOA) | **🧑 Tốt** | ✅ **Đã hoàn thành 100%** |
| **BE-4.3** | Nộp bài MF-04 Persist First $< 100$ms, Upload R2 `STT_MSSV.webm` + SHA-256 niêm phong | **🧑 Tốt** | ✅ **Đã hoàn thành 100%** |
| **BE-4.4** | BoundedChannel / DLQ chấm ngầm, AI Doubt Guard (`confidence < 0.70`), Evidence Panel API | **🧑 Tốt** | ✅ **Đã hoàn thành 100%** |
| **BE-4.5** | Công Bố Điểm Atomic 100%, `OneWayLockInterceptor` (`is_locked = true` $\to$ HTTP 403) | **🧑 Tốt** | ✅ **Đã hoàn thành 100%** |
| **BE-4.6** | Phân hệ Phúc khảo nội bộ `AppealRequest` Trưởng BM giao giảng viên khác chấm lại & Acknowledge | **🧑 Tốt** | ✅ **Đã hoàn thành 100%** |
| **BE-V04** | Dual Auth (Google OAuth PKCE + tài khoản trường cấp), cấp JWT 5 roles, đổi mật khẩu | **🧑 Tốt** | ✅ **Đã hoàn thành 100%** |
| **BE-V05** | API Quản lý Người dùng User Management FE-09 (status, role; không mở tự đăng ký công khai) | **🧑 Tốt** | ✅ **Đã hoàn thành 100% (`AdminController` + CQRS Handlers)** |
| **BE-V06** | API Quản lý Học kỳ Semester CRUD FE-08 (tạo/sửa, không xóa) & Lớp học phần | **🧑 Tốt** | ✅ **Đã hoàn thành 100% (`SemestersController` + CQRS Handlers)** |
| **BE-V07** | Hộp thư Notification in-app FE-11 (tự động thông báo lịch thi, điểm, duyệt đề, phúc khảo) | **🧑 Tốt** | ✅ **Đã hoàn thành 100% (`NotificationsController` + CQRS Handlers)** |
| **BE-V08** | API Quản trị Cấu hình Hệ thống Admin (`system_configs`) | **🧑 Tốt** | ✅ **Đã hoàn thành 100% (`AdminController` + `UpdateSystemConfigCommand`)** |
| **BE-V09** | Quản trị Dead-Letter Queue (DLQ Replay 5m - Trưởng BM) & API Audit Logs (Admin) | **🧑 Tốt** | ✅ **Đã hoàn thành 100% (`DeadLetterQueueController` & `GetAuditLogsQuery`)** |
| **BE-V10** | API Giám thị Phòng thi 40 ghế & Điểm danh / Đình chỉ Kiosk (`ProctorController`) | **🧑 Tốt** | ✅ **Đã hoàn thành 100% (`ProctorController` + 3 CQRS Handlers)** |
| **BE-V11** | API Dashboard Giảng viên & Trưởng BM FE-10 (`GET /api/v1/lecturer/dashboard`) | **🧑 Tốt** | ✅ **Đã hoàn thành 100% (`LecturerController` + `GetLecturerDashboardQuery`)** |
| **BE-QA** | Bộ Test Suites xUnit & Architecture Tests NetArchTest (958/958 tests pass 100%, Exit Code 0) | **🧑 Tốt & Thành** | ✅ **Đã đạt 958/958 Unit & Arch Tests PASS (100%)** |
