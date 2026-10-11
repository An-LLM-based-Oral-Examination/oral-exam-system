# ⚙️ BACKEND FUNCTIONAL TASK BOARD & TECHNICAL SPECIFICATION (v7.0 — FUNCTIONAL BREAKDOWN THEO 4 KHỐI CHỨC NĂNG ĐỘC LẬP)
## ĐỒ ÁN TỐT NGHIỆP SEP490 (FA26SE166) — HỆ THỐNG LUYỆN THI & ĐÁNH GIÁ VẤN ĐÁP BẰNG LLM
### KHO MÃ NGUỒN: `05_Source_Code/backend` | CỔNG DỊCH VỤ API: `5000` | DATABASE: `5432`

---

> [!IMPORTANT]
> **NGUYÊN TẮC VẬN HÀNH & PHÂN BỔ NHÂN SỰ BACKEND CHUẨN XÁC 100%:**
> - **Mã đề tài:** FA26SE166 | **Học kỳ:** Fall 2026 (FA26) — Đại học FPT TP.HCM (FPT SG)
> - **Kiến trúc:** .NET 8 Clean Architecture 4 tầng (Domain, Application, Infrastructure, API) · CQRS MediatR · PostgreSQL 16 (30 bảng 3NF) · SignalR Typed Hubs · BoundedChannel (1,000 slots RAM) · Polly Resilience Pipeline.
> - **Cân bằng tải nhân sự theo chỉ đạo chính thức:**
>   - 🧑 **Nguyễn Quang Thành (Team Leader & Lead Backend Architect):**
>     * Chủ trì Backend Khối 1 (MF-01: Luyện tập vấn đáp tương tác): Core Luyện tập, Tùy chọn Dễ $\to$ Khó `progressive` 3–10 câu cho cả Per/Full, Bounded Channel 1,000 slots RAM, SignalR `PracticeHub`, Stream Whisper trực tiếp (bỏ `AudioUrl`), `system_configs`.
>     * Chủ trì Backend Khối 2 (MF-02: Thi thử vấn đáp bấm giờ): Quota Guard ($K \le 3$ do Trưởng Bộ Môn cấu hình), Voice-First Gate, Bốc đề Bloom từ kho `practice_questions`, Option Follow-up, Server Master Timer, Fast-Path / Async Grading, Instant Scorecard Rubric.
>     * Module Báo cáo Khảo thí FPT: Xuất bảng điểm chuẩn khảo thí Đại học FPT ra cả 2 định dạng Excel `.xlsx` (EPPlus) VÀ PDF (QuestPDF).
>     * API Vệ tinh: API Dashboard Sinh viên FE-10, API Lịch sử luyện tập FE-02.
>   - 🧑 **Nguyễn Trọng Tốt (Backend Developer, AI Specialist & QA Lead):**
>     * Auth Google OAuth PKCE: Chấp nhận mọi email Google hợp lệ, tự động kích hoạt tài khoản trong `users`, cấp JWT Access/Refresh Token theo 5 roles RBAC, loại bỏ `password_hash`.
>     * Chủ trì Backend Khối 3 (MF-03: Ngân hàng câu hỏi & Rubric Studio 10.0đ): AI sinh câu hỏi từ FLM Syllabus theo CLO, Barem Rubric Studio $\sum \equiv 10.0$đ, Tick chọn 2 kho `practice_questions` / `exam_questions`, Gửi duyệt & Trưởng Bộ Môn phê duyệt 3 quyết định (`APPROVED`, `NEEDS_REVISION`, `REJECTED`).
>     * TOÀN BỘ Backend Khối 4 (MF-04: Thi thật phòng Lab, Công bố điểm & Phúc khảo nội bộ): Cấu hình kỳ thi & ca thi gán phòng lab trực tiếp và người coi thi, Check-in Kiosk IP Binding `ip_address` (Ghế 1–40, HTTP 403), Nộp bài Persist First $< 100$ms lưu DB `SUBMITTED`, Upload audio Cloudflare R2 `STT_MSSV.webm` + SHA-256 niêm phong, BoundedChannel 1000 slots / DLQ chấm ngầm AI CoT, AI Doubt Guard (`is_suspicious = true`), API Evidence Panel cho Giảng viên, Công bố điểm Atomic 100%, Khóa một chiều `OneWayLockInterceptor` (`is_locked = true` $\to$ HTTP 403), Phân hệ Phúc khảo nội bộ `AppealRequest` Trưởng Bộ Môn tiếp nhận và giao Giảng viên chấm lại.
>     * API Vệ tinh: API Quản lý Người dùng User Management FE-09, API Quản lý Học kỳ Semester CRUD FE-08, API Hộp thư Notification in-app FE-11, API Dashboard Giảng viên FE-10, Quản trị DLQ & Audit Logs.
>     * AI Core & QA Lead: Gemini 1.5 Flash CoT Prompting ép khuôn JSON, Whisper STT Server-side `timestamps_json`, Polly Retry 3 lần (2s, 4s, 8s), Dead-Letter Queue worker (`dead_letter_queues`), xUnit / NetArchTest suites (100% PASS).
>   - 🧑 **Nguyễn Đăng Hải (DB Specialist & Frontend Developer):**
>     * **CHỈ LÀM DATABASE**, quản trị trọn vẹn 30 bảng CSDL PostgreSQL 16 (DDL Schema, Check Constraints, Indexes, Seed Data, Docker Compose).
>     * **Hải tuyệt đối KHÔNG code logic C# Backend**, chuyển sang dồn toàn lực phát triển Frontend cùng Hoàng.

---

## 📁 1. CẤU TRÚC 30 THỰC THỂ CSDL & BẢNG HỆ THỐNG TRONG 6 BOUNDED CONTEXTS (.NET 8 CLEAN ARCHITECTURE)

Hệ thống quản lý trọn vẹn 30 thực thể chuẩn hóa 3NF tương ứng 30 bảng CSDL PostgreSQL 16 trong 6 Bounded Contexts:

```text
backend/src/
├── Domain/                          # Tầng Core Thuần Khiết (Zero External Dependencies)
│   ├── Common/                      # BaseEntity.cs, IAggregateRoot.cs, ILockableEntity.cs
│   ├── Entities/                    # 30 thực thể chuẩn hóa trong 6 Bounded Contexts + Bảng Cấu hình & Thông báo:
│   │   ├── Identity (1):            User.cs (5 Roles: student, lecturer, department_head, proctor, admin; bỏ password_hash)
│   │   ├── Academic (4):            Semester.cs, Course.cs (has_follow_up, transcript_buffer_seconds, max_follow_up_questions, exam_input_mode), Class.cs, ClassEnrollment.cs
│   │   ├── Assessment (2):          Rubric.cs, RubricCriterion.cs (Ràng buộc sum = 10.0đ)
│   │   ├── Practice [MF-01] (5):    PracticeQuestion.cs, PracticeSession.cs, PracticeAnswer.cs (đã bỏ audio_url), AiEvaluation.cs, AiEvaluationDetail.cs
│   │   ├── Mock Exam [MF-02] (6):   ExamStructure.cs, ExamSet.cs, ExamSetQuestion.cs, MockExamQuota.cs, MockExamSession.cs, MockExamAnswer.cs (Rút từ kho practice_questions)
│   │   ├── Official Exam [MF-04] (7): ExamQuestion.cs, OfficialExamSession.cs, RealExamSessionShift.cs (phòng lab, người coi thi), StudentExamTicket.cs (ip_address, Seat 1-40), ExamQuestionSubmission.cs, LecturerAudit.cs, LecturerAuditDetail.cs
│   │   ├── Appeals [MF-04] (1):     AppealRequest.cs (Gán Trưởng Bộ Môn tiếp nhận & giao giảng viên chấm lại)
│   │   ├── Resilience & Audit (2):  DeadLetterQueue.cs, AuditLog.cs
│   │   └── System & Notifications (2): SystemConfig.cs (Key-Value), Notification.cs (In-app notifications)
│   ├── Enums/                       # UserRole, ExamTicketStatus (7 trạng thái HOA), ExamInputMode (VoiceOnly, VoiceWithTranscriptEdit), AppealStatus, BloomLevel
│   └── Exceptions/                  # DomainValidationException, OneWayLockException, QuotaExceededException
├── Application/                     # MediatR CQRS, FluentValidation, Interfaces
│   ├── Common/                      # IApplicationDbContext, IGeminiService, IWhisperService, IR2StorageService, IExcelExportService, IPdfExportService
│   ├── Behaviors/                   # ValidationBehavior.cs, LoggingBehavior.cs, PerformanceBehavior.cs
│   └── Features/                    # Tổ chức theo 4 Khối Chức Năng Độc Lập
├── Infrastructure/                  # EF Core, External APIs, Background Workers, SignalR
│   ├── Persistence/                 # OralExamDbContext.cs, Configurations/ (Fluent API 30 tables), Migrations/
│   ├── Interceptors/                # OneWayLockInterceptor.cs (Chặn UPDATE/DELETE khi is_locked = true -> HTTP 403)
│   ├── Channels/                    # BoundedGradingQueueChannel.cs (Capacity = 1,000 slots RAM, FullMode.Wait)
│   ├── BackgroundJobs/              # GradingQueueWorker.cs (Polly Retry 2s-4s-8s), DlqReplayWorker.cs
│   ├── Hubs/                        # PracticeHub.cs (/hubs/practice)
│   └── Services/                    # GeminiEvaluationService.cs, CloudflareR2Service.cs, WhisperService.cs, QuotaService.cs, ExcelService.cs, PdfService.cs
└── API/                             # ASP.NET Core Host Gateway (Port 5000)
    ├── Controllers/v1/              # AuthController, PracticeController, MockExamsController, QuestionsController, OfficialExamsController, AppealsController, AdminController, SemestersController, NotificationsController, CoursesController
    ├── Middlewares/                 # GlobalExceptionMiddleware.cs (RFC 7807), RequestLoggingMiddleware.cs
    └── Program.cs                   # CORS AllowFrontend trước StaticFiles, DI Container, MapHub
```

---

## 🛠️ 2. PHÂN RÃ CHI TIẾT 4 KHỐI CHỨC NĂNG ĐỘC LẬP & TÍNH NĂNG VỆ TINH

---

### KHỐI 1 (MF-01): LUYỆN TẬP VẤN ĐÁP TƯƠNG TÁC (INTERACTIVE PRACTICE) & VỆ TINH — [x] ĐÃ HOÀN THÀNH 100% (NGUYỄN QUANG THÀNH ĐÃ LÀM)

#### Task BE-1.1: Khởi tạo phiên luyện tập tự do (Per-Question On-Demand & Full-Session Progressive) — [x] ĐÃ HOÀN TẤT
* **Kỹ sư phụ trách:** 🧑 **Nguyễn Quang Thành**
* **Endpoint:** `POST /api/v1/practice/sessions`
* **Tệp liên quan:** `Features/Practice/Commands/StartPracticeSession/*`, `PracticeController.cs`, `SystemConfig.cs`, `PracticeSession.cs`
* **Trạng thái:** ✅ **Hoàn thành 100%** (Đã hỗ trợ chọn đơn/tổ hợp mức độ `difficulties: ["easy", "medium"]`, Per-Question On-Demand không ép số câu, Full-Session progressive 3–10 câu chia đều Bloom, đọc cấu hình động `system_configs`, chặn thiếu đề HTTP 400 rõ ràng tiếng Việt, lưu `SelectedDifficulties` và `LastActivityAt`).
* **Mô tả nghiệp vụ:**
  - Nhận tham số từ sinh viên: `studentId`, `courseId`, `isFullSession` (`false`: Per-Question, `true`: Full-Session), `difficulties` (mảng mức độ con: `easy`, `medium`, `hard`) hoặc `difficulty` (chuỗi đơn, backward-compatible), `questionCount` (int? tùy chọn ở Per-Question, bắt buộc 3–10 ở Full-Session).
  - **Chế độ `[Per-Question On-Demand]`:**
    * Sinh viên chọn đơn mức độ (`easy`, `medium`, `hard`) hoặc tổ hợp (`["easy", "medium"]`, `["easy", "hard"]`, `["medium", "hard"]`, `["easy", "medium", "hard"]`).
    * Không bắt buộc chốt trước số lượng câu hỏi (`questionCount` tùy chọn). Khi khởi tạo, hệ thống cấp ngay Câu 1 trong response, lưu `session.SelectedDifficulties` và `session.LastActivityAt = DateTime.UtcNow`.
    * Sinh viên làm từng câu và lấy câu tiếp theo on-demand qua `POST /next-question` đến khi muốn dừng.
  - **Chế độ `[Full-Session Progressive]`:**
    * Sinh viên nhập số câu hỏi trong khoảng 3 đến 10 câu (ràng buộc bởi `MinMixedPracticeQuestions = 3`, `MaxMixedPracticeQuestions = 10` trong `system_configs`).
    * Query kho câu hỏi luyện tập (`practice_questions`) lọc theo `course_id`, chia đều các mức độ (Dễ, Trung bình, Khó) và **sắp xếp thứ tự phát vấn tăng dần từ Dễ $\to$ Trung bình $\to$ Khó**, cấp trọn gói toàn bộ câu hỏi.
    * Nếu kho đề không đủ câu hỏi cho bất kỳ mức nào, ném lỗi nghiệp vụ `HTTP 400 Bad Request` tiếng Việt chuẩn (VD: *"Kho đề hiện tại không đủ câu hỏi để tạo phiên luyện tập Dễ đến Khó: cần 1 Dễ (có 10), 2 Trung bình (có 1), 1 Khó (có 10). Vui lòng liên hệ giảng viên bổ sung câu hỏi."*), không tạo phiên rác.
  - Đọc cấu hình thời gian đệm hiệu đính transcript từ `courses`: `transcript_buffer_seconds` (10–300s, mặc định 60s).
  - Tạo bản ghi trong `practice_sessions` với trạng thái `in_progress`, `last_activity_at = DateTime.UtcNow`.
* **Tiêu chí nghiệm thu (DoD):** Trả về `sessionId`, `transcriptBufferSeconds`, danh sách câu hỏi kèm tóm tắt barem rubric $\sum \equiv 10.0$đ. Unit test bao phủ các ca biên: single difficulty, multiple difficulties, bounds progressive, kho đề thiếu câu hỏi.

#### Task BE-1.2: Hàng đợi RAM BoundedChannel (1,000 slots RAM) — [x] ĐÃ HOÀN TẤT
* **Kỹ sư phụ trách:** 🧑 **Nguyễn Quang Thành**
* **Tệp liên quan:** `Infrastructure/Channels/BoundedGradingQueueChannel.cs`
* **Trạng thái:** ✅ **Hoàn thành 100%** (Đã cấu hình Singleton BoundedChannel 1000 capacity, FullMode.Wait, Backpressure an toàn).
* **Mô tả nghiệp vụ:**
  - Tạo singleton `Channel<GradingTask>` với dung lượng giới hạn `capacity = 1000`, thiết lập `FullMode = BoundedChannelFullMode.Wait`.
  - Triển khai phương thức non-blocking `TryWrite()` khi còn slot trống và `WriteAsync()` chờ khi queue đầy 1,000 slots.
  - Ngăn ngừa triệt để nguy cơ tràn bộ nhớ (Out-of-Memory Exception) khi hàng trăm sinh viên cùng nộp bài cùng lúc.
* **Tiêu chí nghiệm thu (DoD):** Unit test đẩy 1,200 task đồng thời: 1,000 task đầu nhận ngay lập tức, 200 task sau chờ theo cơ chế Backpressure an toàn mà không làm sập tiến trình.

#### Task BE-1.3: Nộp bài Per-Question & Batch Submit Full-Session (Stream Whisper STT) — [x] ĐÃ HOÀN TẤT
* **Kỹ sư phụ trách:** 🧑 **Nguyễn Quang Thành**
* **Endpoint:** `POST /api/v1/storage/upload-audio`, `POST /api/v1/practice/sessions/{id}/answers` và `POST /api/v1/practice/sessions/{id}/batch-submit`
* **Tệp liên quan:** `Features/Practice/Commands/UploadAudioPractice/*`, `Features/Practice/Commands/SubmitPracticeAnswer/*`, `Features/Practice/Commands/SubmitPracticeBatch/*`, `PracticeController.cs`, `StorageController.cs`
* **Trạng thái:** ✅ **Hoàn thành 100%** (Stream trực tiếp qua Whisper API, bỏ R2, bỏ `AudioUrl`, Persist First < 100ms trả HTTP 202 Accepted).
* **Mô tả nghiệp vụ:**
  - **Tối ưu hóa luồng Audio:** Tiếp nhận file ghi âm `IFormFile` (.webm) từ client và chuyển thẳng trực tiếp dạng stream sang Cloudflare Whisper API để lấy transcript văn bản.
  - **Quy chuẩn lưu trữ:** Loại bỏ hoàn toàn việc upload audio lên Cloudflare R2 và xóa bỏ cột `AudioUrl` khỏi bảng `practice_answers` (giảm tải 100% chi phí lưu trữ âm thanh cho hệ thống luyện tập; bảo lưu R2 dành riêng cho MF-04).
  - **Persist First $< 100$ms:** Lưu ngay bản ghi vào bảng `practice_answers` với trạng thái `pending`, sau đó đẩy task vào `BoundedGradingQueueChannel`.
  - Trả về mã phản hồi `HTTP 202 Accepted` ngay lập tức để giải phóng giao diện cho sinh viên.
* **Tiêu chí nghiệm thu (DoD):** Thời gian phản hồi API nộp bài $< 100$ms. Bản ghi được lưu bền vững vào PostgreSQL trước khi AI bắt đầu chấm ngầm.

#### Task BE-1.4: Background Worker `GradingQueueWorker` & SignalR `PracticeHub` — [x] ĐÃ HOÀN TẤT
* **Kỹ sư phụ trách:** 🧑 **Nguyễn Quang Thành**
* **Endpoint:** Hub WebSocket `/hubs/practice`
* **Tệp liên quan:** `API/Workers/GradingQueueWorker.cs`, `API/Hubs/PracticeHub.cs`
* **Trạng thái:** ✅ **Hoàn thành 100%** (Chạy BackgroundService, Polly Retry 3 lần 2s-4s-8s, Circuit Breaker, DLQ isolate khi lỗi, SignalR bắn riêng group `session_{id}`).
* **Mô tả nghiệp vụ:**
  - `GradingQueueWorker` chạy dưới dạng `BackgroundService`, liên tục đọc task từ `BoundedGradingQueueChannel.Reader.ReadAllAsync()`.
  - Gọi `IAiGradingService` (Gemini 1.5 Flash CoT) để chấm điểm.
  - Khi có kết quả chấm: Lưu kết quả vào `ai_evaluations`, `ai_evaluation_details`, cập nhật `practice_answers.status = "graded"` và đẩy kết quả realtime qua SignalR Hub tới group `session_{sessionId}` của sinh viên qua event `ReceiveGradingResult`.
  - Nếu gặp lỗi: Cách ly vào Dead-Letter Queue `dead_letter_queues` và bắn event `ReceiveGradingError` kèm thông báo thân thiện.
* **Tiêu chí nghiệm thu (DoD):** Kết nối WebSocket ổn định qua `/hubs/practice`. Nhận đúng payload JSON Scorecard ngay sau khi AI chấm xong mà client không cần gửi request polling.

#### Task BE-1.5: Gemini 1.5 Flash CoT 3 Bước & Follow-up Engine Đa Nấc — [x] ĐÃ HOÀN TẤT
* **Kỹ sư phụ trách:** 🧑 **Nguyễn Quang Thành**
* **Tệp liên quan:** `Infrastructure/Services/GeminiAiGradingService.cs`, `API/Workers/GradingQueueWorker.cs`
* **Trạng thái:** ✅ **Hoàn thành 100%** (Hỗ trợ cấu hình `MaxPracticeFollowUpQuestions` 1–5 câu, mặc định 2 câu; đếm số câu phụ đã có; hỏi phụ khi $4.0 \le \text{Score} \le 8.0$; Full-Session không follow-up).
* **Mô tả nghiệp vụ:**
  - System Prompt Chain-of-Thought (CoT) 3 bước nghiêm ngặt ép khuôn JSON:
    1. Bước 1: Trích xuất các ý kỹ thuật chính từ bản transcript của sinh viên.
    2. Bước 2: Đối chiếu từng tiêu chí Barem Rubric $\sum \equiv 10.0$đ với Model Answer.
    3. Bước 3: Tính điểm từng tiêu chí và tổng hợp nhận xét sư phạm theo định dạng JSON schema chuẩn.
  - **Quy tắc kích hoạt câu hỏi phụ (Follow-up Engine):**
    * Nếu sinh viên chọn **Luyện từng câu (`[Per-Question]`)**: Kích hoạt hỏi phụ khi điểm số rơi vào khoảng ranh giới **$4.0 \le \text{Score} \le 8.0$**. Đếm số lượng câu hỏi phụ đã trả lời trong phiên, cho phép hỏi tiếp đến tối đa `MaxPracticeFollowUpQuestions` (1–5 câu, mặc định 2 câu do Admin cấu hình trong `system_configs`). Nếu $\text{Score} < 4.0$ hoặc $\text{Score} > 8.0$ hoặc đã đủ số câu phụ, bỏ qua hỏi phụ và trả ngay Scorecard.
    * Nếu sinh viên chọn **Luyện trọn gói (`[Full-Session]`)**: **KHÔNG CÓ câu hỏi follow-up**, chấm liền mạch toàn bộ đề và trả tổng kết Scorecard.
* **Tiêu chí nghiệm thu (DoD):** Gemini trả về đúng cấu trúc JSON 100%, không bị vỡ cú pháp markdown. Tỷ lệ kích hoạt câu hỏi phụ chính xác 100% theo khoảng điểm quy định và hạn mức cấu hình.

#### Task BE-1.6: Hoàn Tất Phiên, Lịch Sử Luyện Tập & Chi Tiết Scorecard — [x] ĐÃ HOÀN TẤT
* **Kỹ sư phụ trách:** 🧑 **Nguyễn Quang Thành**
* **Endpoint:** 
  - `POST /api/v1/practice/sessions/{id}/complete` (Hoàn tất phiên)
  - `GET /api/v1/practice/student/history` (Lịch sử luyện tập sinh viên)
  - `GET /api/v1/practice/sessions/{id}` (Chi tiết phiên & Scorecard)
* **Tệp liên quan:** `Features/Practice/Commands/CompletePracticeSession/*`, `Features/Practice/Queries/GetStudentPracticeHistory/*`, `Features/Practice/Queries/GetPracticeSession/*`, `PracticeController.cs`
* **Trạng thái:** ✅ **Hoàn thành 100%** (Cập nhật `Status = "completed"` và `CompletedAt = DateTime.UtcNow`, trả về danh sách lịch sử phiên kèm tính điểm trung bình chính xác, hỗ trợ Tab Luyện Tập trên Student Portal).
* **Mô tả nghiệp vụ:**
  - `POST /api/v1/practice/sessions/{id}/complete`: Đóng phiên luyện tập khi sinh viên hoàn thành, cập nhật trạng thái `completed`, ghi nhận `completed_at` (Idempotent).
  - `GET /api/v1/practice/student/history`: Trả về danh sách các phiên luyện tập của sinh viên kèm chế độ (`Per-Question` / `Full-Session`), thời gian bắt đầu/kết thúc, số câu hỏi, điểm trung bình để phục vụ hiển thị lên Tab Luyện tập ở Student Portal.
  - `GET /api/v1/practice/sessions/{id}`: Trả về chi tiết toàn bộ Scorecard, câu hỏi, câu trả lời bóc băng và nhận xét của từng tiêu chí rubric.
* **Tiêu chí nghiệm thu (DoD):** Phản hồi API chuẩn RFC 7807. Đã có 6 bài unit tests trong `CompleteAndHistoryPracticeTests.cs` đạt 100% passed.

#### Task BE-1.7 (Vệ tinh): API Dashboard Sinh Viên (FE-10 Student Portal)
* **Kỹ sư phụ trách:** 🧑 **Nguyễn Quang Thành**
* **Endpoint:** `GET /api/v1/student/dashboard`
* **Tệp liên quan:** `Features/Student/Queries/GetStudentDashboard/*`, `StudentController.cs`
* **Mô tả nghiệp vụ:**
  - Cung cấp dữ liệu thống kê tổng hợp phục vụ hiển thị trên `StudentDashboardPage.tsx` của sinh viên:
    1. **Thống kê hoạt động:** Tổng số buổi luyện tập đã tham gia, tổng số bài thi thử đã làm trong học kỳ.
    2. **Chỉ số học tập:** Điểm trung bình gần nhất của các phiên luyện tập và thi thử.
    3. **Phân tích độ thành thạo theo Chuẩn Đầu Ra (CLOs):** Tổng hợp điểm số theo từng CLO của các môn học đang theo học (ví dụ: CLO1 đạt 8.5/10, CLO2 đạt 6.0/10) để chỉ ra điểm mạnh và điểm yếu kiến thức của sinh viên.
    4. **Lịch thi sắp tới:** Danh sách các ca thi thật mà sinh viên đã được phân bổ vé thi (`StudentExamTicket`) trong kỳ thi hiện tại (gồm Tên môn, Ngày thi, Phòng lab, Kíp thi, Số ghế).
* **Tiêu chí nghiệm thu (DoD):** Phản hồi API chuẩn RFC 7807 với thời gian $< 50$ms. Dữ liệu tính toán chính xác từ `practice_sessions`, `mock_exam_sessions`, `ai_evaluation_details` và `student_exam_tickets`.

#### Task BE-1.8: API NextQuestion On-Demand & Thuật toán Anti-3-Consecutive Randomizer — [x] ĐÃ HOÀN TẤT
* **Kỹ sư phụ trách:** 🧑 **Nguyễn Quang Thành**
* **Endpoint:** `POST /api/v1/practice/sessions/{sessionId}/next-question`
* **Tệp liên quan:** `Features/Practice/Commands/NextQuestion/*`, `PracticeController.cs`
* **Trạng thái:** ✅ **Hoàn thành 100%** (Cấp câu hỏi tiếp theo On-Demand, thuật toán chống lặp 3 câu liên tiếp cùng mức, loại trừ câu đã làm, fallback an toàn khi cạn bucket, trả `hasMoreQuestions: false` khi cạn toàn bộ, cập nhật `last_activity_at`).
* **Mô tả nghiệp vụ:**
  - Nhận `sessionId` từ route và `studentId` từ JWT/query.
  - Kiểm tra trạng thái phiên `in_progress` và chế độ `per_question`.
  - Loại trừ toàn bộ câu hỏi chính đã trả lời trong phiên (`Id NOT IN (answeredIds)`).
  - **Thuật toán Anti-3-Consecutive Randomizer:**
    * Phân tích 2 câu hỏi chính gần nhất đã trả lời trong phiên (`!IsFollowUp`).
    * Nếu 2 câu liền trước cùng độ khó $D$ và phiên chọn $> 1$ độ khó: Tạm thời loại trừ $D$ khỏi candidate pool. Câu tiếp theo bắt buộc bốc sang độ khó khác.
    * Đảm bảo: *trong bất kỳ chuỗi 3 câu hỏi liên tiếp nào, tối đa chỉ có 2 câu chung mức độ*.
    * **Fallback thông minh:** Nếu các độ khó khác trong tổ hợp đã hết câu chưa làm, hệ thống tự động fallback cho phép bốc tiếp câu còn lại của $D$.
    * **Cạn sạch đề:** Nếu toàn bộ câu hỏi trong tất cả các mức đã chọn đều đã làm hết, trả về `hasMoreQuestions: false`, `question: null` kèm thông báo đã hoàn thành.
  - Cập nhật `session.LastActivityAt = DateTime.UtcNow`.
* **Tiêu chí nghiệm thu (DoD):** Đã kiểm chứng qua 6 bài unit test trong `NextQuestionCommandHandlerTests.cs` và 4 bài stress test đối kháng trong `NextQuestionAdversarialStressTests.cs` (mô phỏng chuỗi 15 câu với window check trượt, assert không bao giờ có 3 câu cùng mức độ).

#### Task BE-1.9: Cơ chế Timeout 10 Phút Không Tương Tác (Session Inactivity Timeout) — [x] ĐÃ HOÀN TẤT
* **Kỹ sư phụ trách:** 🧑 **Nguyễn Quang Thành**
* **Tệp liên quan:** `Domain/Entities/PracticeSession.cs`, `Infrastructure/Persistence/OralExamDbContext.cs`, `Features/Practice/Commands/NextQuestion/*`, `Features/Practice/Commands/SubmitPracticeAnswer/*`, `Features/Practice/Commands/SubmitPracticeBatch/*`, `PracticeController.cs`
* **Trạng thái:** ✅ **Hoàn thành 100%** (Cấu hình `SessionInactivityTimeoutMinutes = 10` trong `system_configs`, Lazy validation tự động kết thúc phiên `status = "completed"`, trả lỗi HTTP 410 Gone RFC 7807, bảo toàn 100% dữ liệu đã làm).
* **Mô tả nghiệp vụ:**
  - Thêm trường `last_activity_at` vào thực thể `PracticeSession` và CSDL (tự động cập nhật mỗi khi tạo phiên, lấy câu tiếp theo, nộp bài).
  - Seed khóa cấu hình `SessionInactivityTimeoutMinutes = 10` vào `system_configs`.
  - **Lazy Inactivity Timeout Validation:**
    * Khi nhận request gọi lên (`NextQuestion`, `SubmitAnswer`, `SubmitBatch`), nếu `DateTime.UtcNow - session.LastActivityAt > 10 phút`:
    * Tự động cập nhật `session.Status = "completed"`, `session.CompletedAt = session.LastActivityAt + 10m` và lưu vào DB.
    * Controller trả về mã phản hồi **HTTP 410 Gone** RFC 7807 (`Title = "Session Timed Out"`).
    * **Bảo toàn dữ liệu:** Giữ nguyên 100% các câu trả lời, điểm số và nhận xét đã thực hiện trước đó trong CSDL.
* **Tiêu chí nghiệm thu (DoD):** 4 bài unit tests trong `PracticeSessionInactivityTimeoutTests.cs` đạt 100% passed (Lazy check NextQuestion, Lazy check SubmitAnswer, bảo toàn điểm số/nhận xét, cập nhật `LastActivityAt` sau tương tác hợp lệ).

---

### KHỐI 2 (MF-02): THI THỬ VẤN ĐÁP BẤM GIỜ (TIMED MOCK EXAM) & VỆ TINH — [ ] KẾ HOẠCH TIẾP THEO CỦA NGUYỄN QUANG THÀNH

#### Task BE-2.1: Daily Quota Guard do Trưởng Bộ Môn Cấu Hình ($K \le 3$)
* **Kỹ sư phụ trách:** 🧑 **Nguyễn Quang Thành**
* **Endpoint:** `POST /api/v1/mock-exams/sessions`
* **Tệp liên quan:** `Features/MockExams/Commands/StartMockExam/*`, `Infrastructure/Services/QuotaService.cs`, `MockExamsController.cs`
* **Mô tả nghiệp vụ:**
  - Hạn ngạch số lượt thi thử trong ngày do **Trưởng Bộ Môn (`department_head`)** cấu hình theo môn học (mặc định $K = 3$ lượt/ngày/môn).
  - Khi sinh viên bấm bắt đầu thi thử: `QuotaService` kiểm tra số lượt thi đã thực hiện trong ngày (theo múi giờ GMT+7) trong bảng `mock_exam_quotas`.
  - Nếu đã đủ $K$ lượt: Chặn ngay lập tức từ tầng Application, ném `QuotaExceededException` chuyển đổi thành mã phản hồi **`HTTP 429 Too Many Requests`** kèm RFC 7807 `ProblemDetails`: *"Bạn đã sử dụng hết hạn ngạch thi thử trong ngày cho môn học này (tối đa K lượt/ngày)"*.
  - Nếu còn hạn ngạch: Tăng bộ đếm lượt thi và cho phép khởi tạo phiên thi.
* **Tiêu chí nghiệm thu (DoD):** Lượt thứ 1, 2, 3 thành công trả HTTP 200. Lượt thứ 4 chặn đứng tại Gateway trả `HTTP 429`.

#### Task BE-2.2: Bốc Đề Thi Thử Theo Ma Trận Bloom Từ Kho `practice_questions`
* **Kỹ sư phụ trách:** 🧑 **Nguyễn Quang Thành**
* **Tệp liên quan:** `Features/MockExams/Commands/StartMockExam/*`
* **Mô tả nghiệp vụ:**
  - Rút câu hỏi thi thử theo cấu trúc ma trận Bloom (`exam_structures`) do Trưởng Bộ Môn thiết lập.
  - **Quy chuẩn nguồn đề:** Bốc đề hoàn toàn từ kho câu hỏi luyện tập (`practice_questions`). **CÔ LẬP TUYỆT ĐỐI** kho câu hỏi thi thật (`exam_questions`) dành riêng cho MF-04.
  - Sinh viên **KHÔNG được tự chọn topic hay độ khó** (đề thi thử bốc ngẫu nhiên theo chuẩn đề cương môn học).
  - Sinh viên chỉ được chủ động lựa chọn: **Có Follow-up** hoặc **Không Follow-up** trước khi bắt đầu bài thi.
* **Tiêu chí nghiệm thu (DoD):** 100% câu hỏi thi thử được rút từ `practice_questions`, không có bất kỳ câu hỏi nào rò rỉ từ `exam_questions`.

#### Task BE-2.3: Server Master Timer & Voice-First Gate
* **Kỹ sư phụ trách:** 🧑 **Nguyễn Quang Thành**
* **Endpoint:** `POST /api/v1/mock-exams/sessions/{id}/submit`
* **Tệp liên quan:** `Features/MockExams/Commands/SubmitMockExam/*`, `MockExamsController.cs`
* **Mô tả nghiệp vụ:**
  - Quản lý đồng hồ thời gian làm bài chuẩn từ Server (`started_at`, `duration_minutes`).
  - **Cổng Voice-First Gate:** Yêu cầu sinh viên trả lời hoàn toàn qua micro, backend chỉ nhận stream âm thanh qua Whisper STT lấy transcript. Không lưu file audio lên Cloudflare R2, chỉ lưu transcript vào `mock_exam_answers`.
  - Kiểm tra thời gian nộp bài: Cho phép độ trễ mạng tối đa 10 giây. Nếu nộp muộn quá 10 giây so với giờ kết thúc của Server: Đánh dấu `is_late = true` và tự động thu bài.
* **Tiêu chí nghiệm thu (DoD):** Đồng bộ thời gian chính xác giữa Client và Server; nộp đúng hạn trả HTTP 200, nộp quá hạn tự động khóa và tính điểm đến thời điểm hết giờ.

#### Task BE-2.4: Chấm 2 Pha, Instant Scorecard Rubric & Lịch Sử Thi Thử
* **Kỹ sư phụ trách:** 🧑 **Nguyễn Quang Thành**
* **Endpoint:** `GET /api/v1/mock-exams/history`, `GET /api/v1/mock-exams/history/{id}`
* **Tệp liên quan:** `Features/MockExams/Queries/*`, `MockExamsController.cs`
* **Mô tả nghiệp vụ:**
  - Chấm điểm bài thi thử theo 2 pha hoặc async nhanh: AI đánh giá toàn bộ câu trả lời đối chiếu barem rubric theo từng Chuẩn Đầu Ra (CLO) của môn học.
  - **Instant Feedback:** Ngay khi hoàn thành bài thi thử, hệ thống trả về Scorecard chi tiết kèm điểm tổng kết, điểm thành phần từng CLO và nhận xét sư phạm để sinh viên xem ngay trên màn hình.
  - `GET /api/v1/mock-exams/history`: Lưu trữ toàn bộ lịch sử thi thử của sinh viên để theo dõi sự tiến bộ qua các lần thi.
* **Tiêu chí nghiệm thu (DoD):** Scorecard chi tiết theo CLO hiển thị tức thì sau khi nộp bài; dữ liệu được lưu chuẩn xác vào bảng `mock_exam_sessions`.

---

### KHỐI 3 (MF-03): NGÂN HÀNG ĐỀ & RUBRIC STUDIO 10.0 & VỆ TINH — [ ] CHƯA BẮT ĐẦU (KẾ HOẠCH SPRINT CỦA NGUYỄN TRỌNG TỐT TỰ CODE)

#### Task BE-3.1 (Vệ tinh): Xác Thực Google OAuth PKCE (Mở Cho Mọi Email)
* **Kỹ sư phụ trách:** 🧑 **Nguyễn Trọng Tốt (Lead Auth & Security)**
* **Endpoint:** `POST /api/v1/auth/google-login`
* **Tệp liên quan:** `Features/Auth/*`, `API/Controllers/v1/AuthController.cs`, `User.cs`
* **Mô tả nghiệp vụ:**
  - Tiếp nhận Google ID Token từ Frontend (`POST /api/v1/auth/google-login`).
  - **Mở rộng phạm vi người dùng:** Chấp nhận mọi email Google hợp lệ (loại bỏ ràng buộc cứng chỉ cho phép `@fpt.edu.vn` hoặc `@fe.edu.vn` để phục vụ linh hoạt tài khoản demo và hội đồng chấm bảo vệ).
  - Tìm kiếm người dùng theo email trong bảng `users`:
    * Nếu chưa tồn tại: Tự động tạo mới tài khoản với vai trò mặc định `student` (hoặc vai trò do Admin phân quyền trước đó).
    * Loại bỏ hoàn toàn cột `password_hash` (xác thực thuần OAuth2/OIDC).
  - Cấp cặp JWT Token:
    * Access Token: Thời hạn 60 phút, chứa claims `sub` (User ID), `email`, `name`, `role` (1 trong 5 roles: `student`, `lecturer`, `department_head`, `proctor`, `admin`).
    * Refresh Token: Thời hạn 7 ngày lưu trong DB.
* **Tiêu chí nghiệm thu (DoD):** Đăng nhập với bất kỳ tài khoản Google hợp lệ nào đều trả về HTTP 200 kèm JWT claims chuẩn. Gọi API không có Bearer token trả `HTTP 401 Unauthorized`.

#### Task BE-3.2 (Vệ tinh): API Quản Lý Người Dùng User Management (FE-09)
* **Kỹ sư phụ trách:** 🧑 **Nguyễn Trọng Tốt**
* **Endpoint:** `GET /api/v1/admin/users`, `PUT /api/v1/admin/users/{id}/status`, `PUT /api/v1/admin/users/{id}/role`
* **Tệp liên quan:** `Features/Admin/Users/*`, `AdminController.cs`
* **Mô tả nghiệp vụ:**
  - `GET /api/v1/admin/users`: Trả về danh sách người dùng có phân trang, tìm kiếm theo tên, email, lọc theo role (`student`, `lecturer`, `department_head`, `proctor`, `admin`).
  - `PUT /api/v1/admin/users/{id}/status`: Khóa hoặc kích hoạt tài khoản người dùng (`is_active = true/false`). Khi bị khóa, JWT của user lập tức bị từ chối.
  - `PUT /api/v1/admin/users/{id}/role`: Cho phép Admin gán hoặc chuyển đổi vai trò người dùng sang 1 trong 5 vai trò hệ thống.
* **Tiêu chí nghiệm thu (DoD):** Phân quyền chỉ `admin` mới được gọi endpoints này (`[Authorize(Roles = "admin")]`). Cập nhật role thành công và phản ánh ngay trong token tiếp theo.

#### Task BE-3.3: AI Sinh Câu Hỏi Từ FLM Syllabus Theo CLO & Barem Riêng 10.0đ
* **Kỹ sư phụ trách:** 🧑 **Nguyễn Trọng Tốt**
* **Endpoint:** `POST /api/v1/questions/generate-from-flm`
* **Tệp liên quan:** `Features/Questions/Commands/GenerateQuestionsFromFlm/*`, `QuestionsController.cs`
* **Mô tả nghiệp vụ:**
  - **Quyền hạn Giảng viên (`lecturer`):** Giảng viên được toàn quyền sử dụng AI sinh câu hỏi theo barem của mình từ Syllabus FLM hoặc đề cương môn học.
  - Nhận input: `courseId`, `syllabusContent` (hoặc bóc tách tự động qua FLM), `cloList`, `rubricGuidelines`, `targetBloomLevel`.
  - Gọi Gemini AI: Tự động phân tích mục tiêu CLO, sinh câu hỏi vấn đáp chuyên sâu, câu trả lời mẫu chuẩn (Model Answer $\ge 50$ ký tự) và bảng barem rubric chuẩn hóa $\sum \equiv 10.0$đ.
  - Trả về danh sách câu hỏi dự thảo để giảng viên xem trước trên Preview Studio và tùy chỉnh tự do trước khi lưu.
* **Tiêu chí nghiệm thu (DoD):** AI sinh câu hỏi đầy đủ tiêu chí; Model Answer $\ge 50$ ký tự; các tiêu chí con có tổng điểm luôn bằng 10.0 điểm.

#### Task BE-3.4: Soạn Câu Hỏi & Barem Rubric Studio 10.0đ (FluentValidation)
* **Kỹ sư phụ trách:** 🧑 **Nguyễn Trọng Tốt**
* **Endpoint:** `POST /api/v1/questions/draft`
* **Tệp liên quan:** `Features/Questions/Commands/CreateQuestionDraft/*`, `Application/Common/Validators/RubricValidator.cs`
* **Mô tả nghiệp vụ:**
  - Tiếp nhận câu hỏi do giảng viên tự soạn thủ công hoặc hiệu chỉnh từ bản AI sinh.
  - Cài đặt `RubricValidator` sử dụng FluentValidation ép cứng quy tắc:
    * **Tổng điểm barem rubric bắt buộc $\sum \equiv 10.0$ điểm** (dung sai sai số số thực $\le 0.001$). Nếu tổng điểm $< 10.0$ hoặc $> 10.0$ (kể cả 9.9đ hay 10.1đ), ném lỗi validation và trả mã **`HTTP 422 Unprocessable Entity`** kèm thông báo: *"Tổng điểm các tiêu chí rubric phải đúng bằng 10.0 điểm"*.
    * Câu trả lời mẫu (Model Answer) bắt buộc $\ge 50$ ký tự.
    * Tối thiểu 2 tiêu chí rubric con, mỗi tiêu chí điểm $> 0$.
* **Tiêu chí nghiệm thu (DoD):** Barem 10.0đ được lưu thành công vào bảng `rubrics` và `rubric_criteria`. Barem 9.5đ hoặc 10.5đ bị chặn 100% tại tầng Validation Behavior.

#### Task BE-3.5: Tick Chọn 2 Kho Đề & Gửi Duyệt Lên Bộ Môn
* **Kỹ sư phụ trách:** 🧑 **Nguyễn Trọng Tốt**
* **Endpoint:** `POST /api/v1/questions/batch-submit-review`
* **Tệp liên quan:** `Features/Questions/Commands/BatchSubmitQuestionsForReview/*`, `QuestionsController.cs`
* **Mô tả nghiệp vụ:**
  - Hỗ trợ lựa chọn kho đề thông qua 2 checkbox:
    * `is_for_practice`: Lưu vào kho luyện tập chung (`practice_questions`).
    * `is_for_exam`: Lưu vào kho câu hỏi thi thật phòng Lab (`exam_questions`).
    * Có thể chọn 1 trong 2 hoặc tick chọn cả 2 kho cùng lúc.
  - **Quy trình gửi duyệt:** Mọi câu hỏi do Giảng viên tạo ra đều ở trạng thái ban đầu là `DRAFT`.
  - Giảng viên bấm **"Gửi lên cho Bộ Môn"** (`batch-submit-review`): Chuyển trạng thái sang `SUBMITTED_FOR_REVIEW`, ghi nhận `submitted_by = lecturerId`, `submitted_at = DateTime.UtcNow`.
* **Tiêu chí nghiệm thu (DoD):** Câu hỏi chuyển sang `SUBMITTED_FOR_REVIEW`, không thể chỉnh sửa nội dung khi đang chờ duyệt. Tự động gửi thông báo in-app cho Trưởng Bộ Môn.

#### Task BE-3.6: Trưởng Bộ Môn Thẩm Định & Ra Quyết Định Phê Duyệt
* **Kỹ sư phụ trách:** 🧑 **Nguyễn Trọng Tốt**
* **Endpoint:** `PUT /api/v1/questions/{id}/review-decision`
* **Tệp liên quan:** `Features/Questions/Commands/ReviewQuestionDecision/*`, `QuestionsController.cs`
* **Mô tả nghiệp vụ:**
  - **Trưởng Bộ Môn (`department_head`)** là người duy nhất có thẩm quyền thẩm định và ra quyết định trên các câu hỏi chờ duyệt:
    1. **Phê duyệt (`APPROVED`):** Câu hỏi được lưu chính thức vào ngân hàng câu hỏi môn học và sẵn sàng được rút ra làm đề thi.
    2. **Yêu cầu chỉnh sửa (`NEEDS_REVISION`):** Bắt buộc nhập lý do góp ý $\ge 10$ ký tự vào `review_notes`. Câu hỏi trả về cho giảng viên để chỉnh sửa và gửi duyệt lại.
    3. **Từ chối (`REJECTED`):** Bắt buộc nhập lý do từ chối vào `review_notes`. Câu hỏi bị loại bỏ khỏi danh sách sử dụng.
* **Tiêu chí nghiệm thu (DoD):** Phân quyền chỉ `department_head` mới được duyệt câu hỏi. Đầy đủ 3 trạng thái quyết định; ghi nhận `approved_by` và `review_notes` vào CSDL.

#### Task BE-3.7 (Vệ tinh): API Dashboard Giảng Viên (FE-10)
* **Kỹ sư phụ trách:** 🧑 **Nguyễn Trọng Tốt**
* **Endpoint:** `GET /api/v1/lecturer/dashboard`
* **Tệp liên quan:** `Features/Lecturer/Queries/*`, `QuestionsController.cs`
* **Mô tả nghiệp vụ:**
  - Thống kê tổng số câu hỏi giảng viên đã tạo trong từng môn học.
  - Phân loại số lượng câu hỏi theo trạng thái: `DRAFT`, `SUBMITTED_FOR_REVIEW`, `APPROVED`, `NEEDS_REVISION`.
  - Tỷ lệ câu hỏi được duyệt thành công. Danh sách các câu hỏi bị yêu cầu chỉnh sửa cần xử lý gấp.
* **Tiêu chí nghiệm thu (DoD):** API trả về thống kê tổng hợp chính xác $< 40$ms, phục vụ hiển thị trực quan trên `LecturerDashboardPage.tsx`.

---

### KHỐI 4 (MF-04): THI THẬT PHÒNG LAB, CÔNG BỐ ĐIỂM & PHÚC KHẢO NỘI BỘ & VỆ TINH — [ ] CHƯA BẮT ĐẦU (KẾ HOẠCH SPRINT CỦA NGUYỄN TRỌNG TỐT TỰ CODE)

#### Task BE-4.1: Quản Trị Kỳ Thi & Cấu Hình Môn Thi Của Trưởng Bộ Môn
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

#### Task BE-4.2: Check-in Kiosk Phòng Lab Ràng Buộc IP `ip_address` (Ghế 1–40)
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

#### Task BE-4.3: Nộp Bài Persist First $< 100$ms & Upload Audio R2 `STT_MSSV.webm`
* **Kỹ sư phụ trách:** 🧑 **Nguyễn Trọng Tốt**
* **Endpoint:** `POST /api/v1/official-exams/tickets/{id}/submit`
* **Tệp liên quan:** `Features/OfficialExams/Commands/SubmitOfficialExam/*`, `Infrastructure/Services/CloudflareR2Service.cs`
* **Mô tả nghiệp vụ:**
  - Tiếp nhận luồng audio từ Kiosk và upload lên Cloudflare R2 với quy tắc đặt tên bất biến: **`STT_MSSV.webm`** (ví dụ: `01_SE170123.webm`).
  - Tính toán mã băm mật mã SHA-256 niêm phong audio, đối chiếu với mã băm do Kiosk gửi lên để đảm bảo tính toàn vẹn 100% không bị can thiệp.
  - **Persist First $< 100$ms:** Lưu ngay lập tức bản ghi vào bảng `exam_question_submissions` và cập nhật `student_exam_tickets` sang trạng thái **`SUBMITTED` trong $< 100$ms** kèm URL audio R2 và mã băm SHA-256.
  - **Quy tắc phòng thi bất biến:** Kiosk khóa màn hình và hiển thị thông báo an toàn: *"Bài thi đã được lưu trữ an toàn. Kết quả sẽ do Giảng viên thẩm định và công bố trên hệ thống."* **Tuyệt đối KHÔNG có điểm liền và KHÔNG khiếu nại tại chỗ**. Thí sinh ký biên bản nộp bài giấy và ra về.
* **Tiêu chí nghiệm thu (DoD):** Phản hồi API nộp bài $< 100$ms. File âm thanh được lưu an toàn trên Cloudflare R2 với tên `STT_MSSV.webm` kèm SHA-256 niêm phong.

#### Task BE-4.4: BoundedChannel / DLQ Chấm Ngầm, AI Doubt Guard & Evidence Panel
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

#### Task BE-4.5: Công Bố Điểm Atomic & Khóa Một Chiều `OneWayLockInterceptor`
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

#### Task BE-4.6: Phân Hệ Phúc Khảo Nội Bộ `AppealRequest` & Acknowledge Điểm
* **Kỹ sư phụ trách:** 🧑 **Nguyễn Trọng Tốt**
* **Endpoint:** `POST /api/v1/appeals`, `GET /api/v1/appeals`, `PUT /api/v1/appeals/{id}/assign-lecturer`, `PUT /api/v1/appeals/{id}/decision`, `POST /api/v1/official-exams/tickets/{id}/acknowledge`
* **Tệp liên quan:** `Features/Appeals/*`, `AppealsController.cs`, `OfficialExamsController.cs`
* **Mô tả nghiệp vụ:**
  - Sau khi Giảng viên công bố điểm, sinh viên ở nhà đăng nhập Student Portal để xem bảng điểm chính thức:
    * Nếu đồng ý với kết quả: Bấm xác nhận nhận điểm (`POST /api/v1/official-exams/tickets/{id}/acknowledge`), cập nhật trạng thái `student_acknowledgement_status = ACKNOWLEDGED`.
    * Nếu không đồng ý kết quả: Sinh viên làm đơn phúc khảo trực tiếp trên hệ thống (`POST /api/v1/appeals`) kèm lý do khiếu nại cụ thể.
  - Hệ thống tạo entity `AppealRequest` ở trạng thái `PENDING` và tự động gán cho **Trưởng Bộ Môn (`department_head`)** tiếp nhận thẩm định độc lập.
  - **Cơ chế Phân công Chấm lại của Trưởng Bộ Môn:**
    * Trưởng Bộ Môn xem xét đơn, có thể tự chấm hoặc gọi `PUT /api/v1/appeals/{id}/assign-lecturer` để giao cho một Giảng viên khác chấm lại bài thi.
    * Giảng viên được giao chấm lại sẽ truy cập Evidence Panel của bài thi, nghe lại file ghi âm và cập nhật điểm phúc khảo.
    * Trưởng Bộ Môn đưa ra quyết định cuối cùng (`APPROVED` điều chỉnh điểm / `REJECTED` giữ nguyên điểm) qua `PUT /api/v1/appeals/{id}/decision`.
* **Tiêu chí nghiệm thu (DoD):** Quy trình phúc khảo nội bộ khép kín 100% trong hệ thống; Trưởng Bộ Môn có toàn quyền tiếp nhận và phân công giảng viên chấm lại; điểm mới được cập nhật có audit log rõ ràng.

#### Task BE-4.7 (Vệ tinh): API Quản Lý Học Kỳ Semester CRUD (FE-08)
* **Kỹ sư phụ trách:** 🧑 **Nguyễn Trọng Tốt**
* **Endpoint:** `GET /api/v1/semesters`, `POST /api/v1/semesters`, `PUT /api/v1/semesters/{id}`, `DELETE /api/v1/semesters/{id}`
* **Tệp liên quan:** `Features/Semesters/*`, `SemestersController.cs`, `Semester.cs`
* **Mô tả nghiệp vụ:**
  - `GET /api/v1/semesters`: Lấy danh sách học kỳ (Mã học kỳ: FA26, SP26; Tên: Fall 2026; Ngày bắt đầu, Ngày kết thúc, Trạng thái hoạt động).
  - `POST /api/v1/semesters`: Admin tạo mới học kỳ, validate không trùng mã học kỳ.
  - `PUT /api/v1/semesters/{id}`: Cập nhật thông tin học kỳ hoặc đặt làm học kỳ hiện tại (`is_current = true`).
* **Tiêu chí nghiệm thu (DoD):** Phân quyền chỉ `admin` được tạo/sửa học kỳ. API trả về danh sách học kỳ phục vụ chọn kỳ học trên toàn hệ thống.

#### Task BE-4.8 (Vệ tinh): Hộp Thư Notification In-App (FE-11)
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

#### Task BE-4.9 (Vệ tinh): Module Báo Cáo Khảo Thí FPT (Excel .xlsx + PDF)
* **Kỹ sư phụ trách:** 🧑 **Nguyễn Quang Thành**
* **Endpoint:** `GET /api/v1/official-exams/shifts/{shiftId}/export-excel`, `GET /api/v1/official-exams/shifts/{shiftId}/export-pdf`
* **Tệp liên quan:** `Features/OfficialExams/Queries/ExportReports/*`, `Infrastructure/Services/ExcelService.cs`, `Infrastructure/Services/PdfService.cs`
* **Mô tả nghiệp vụ:**
  - **Xuất bảng điểm Excel `.xlsx` (EPPlus):**
    * Đúng định dạng mẫu biểu Khảo thí Đại học FPT: Header gồm Bộ Môn, Môn thi, Mã ca thi, Phòng Lab, Ngày thi, Tên Giám thị và Giảng viên chấm.
    * Bảng điểm: Cột STT, MSSV, Họ và tên, Lớp, Điểm AI chấm, Điểm Giảng viên chốt, Lý do điều chỉnh (nếu có), Chữ ký điện tử.
  - **Xuất biên bản thi PDF (QuestPDF):**
    * Tạo file PDF chuẩn A4 niêm phong kết quả ca thi, có mã QR tra cứu tính toàn vẹn và chữ ký của Trưởng Bộ Môn / Giảng viên.
* **Tiêu chí nghiệm thu (DoD):** File Excel và PDF tải về mở chuẩn xác, không lỗi font tiếng Việt Unicode, khớp 100% dữ liệu điểm trong CSDL.

#### Task BE-4.10: Quản Trị Dead-Letter Queue (DLQ) & Audit Logs
* **Kỹ sư phụ trách:** 🧑 **Nguyễn Trọng Tốt**
* **Endpoint:** `GET /api/v1/admin/dlq/tasks`, `POST /api/v1/admin/dlq/tasks/{id}/replay`, `GET /api/v1/admin/audit-logs`
* **Tệp liên quan:** `Features/Admin/Dlq/*`, `Infrastructure/BackgroundJobs/DlqReplayWorker.cs`, `AdminController.cs`
* **Mô tả nghiệp vụ:**
  - Bảng `dead_letter_queues` lưu trữ toàn bộ các task chấm thi thất bại sau 3 lần retry của Polly.
  - Background Service `DlqReplayWorker` tự động quét định kỳ sau 5 phút để kích hoạt chấm bù cho các task bị lỗi tạm thời do nghẽn mạng hoặc quá tải API.
  - Admin có dashboard xem danh sách DLQ, log lỗi và bấm "Replay Task" thủ công khi cần.
  - `GET /api/v1/admin/audit-logs`: Truy vấn nhật ký kiểm toán hệ thống từ bảng `audit_logs` (thao tác đổi điểm, phê duyệt đề, khóa điểm, gán phúc khảo).
* **Tiêu chí nghiệm thu (DoD):** Cam kết Zero Data Loss 100%, không bài làm nào của sinh viên bị thất lạc kể cả khi dịch vụ AI bên ngoài gặp sự cố kéo dài.

#### Task BE-4.11 (Vệ tinh): API Quản Trị Cấu Hình Hệ Thống Admin (`system_configs`)
* **Kỹ sư phụ trách:** 🧑 **Nguyễn Trọng Tốt**
* **Endpoint:** `GET /api/v1/admin/configs`, `PUT /api/v1/admin/configs/{key}`
* **Tệp liên quan:** `Features/Admin/Configs/*`, `AdminController.cs`, `SystemConfig.cs`
* **Mô tả nghiệp vụ:**
  - Cung cấp API phục vụ màn hình `AdminConfigPage.tsx` của Admin:
    * `GET /api/v1/admin/configs`: Lấy danh sách các tham số cấu hình động toàn cục của hệ thống (Key-Value): `MaxPracticeQuestionsPerSession`, `MinMixedPracticeQuestions`, `MaxMixedPracticeQuestions`, `TranscriptBufferSeconds`, `MaxPracticeFollowUpQuestions`.
    * `PUT /api/v1/admin/configs/{key}`: Admin cập nhật trực tiếp giá trị cấu hình runtime vào bảng `system_configs`.
    * Cài đặt validation kiểm tra chặt chẽ: `TranscriptBufferSeconds` từ 10 đến 300 giây; `MaxPracticeFollowUpQuestions` từ 1 đến 5 câu; `MinMixedPracticeQuestions` từ 3 đến 5 câu và không vượt quá `MaxMixedPracticeQuestions` (6–10 câu).
    * Ghi vết kiểm toán người cập nhật (`updated_by = adminId`) và thời gian cập nhật vào `audit_logs`.
* **Tiêu chí nghiệm thu (DoD):** Phân quyền chỉ `admin` được gọi. Cập nhật cấu hình có hiệu lực ngay lập tức cho các phiên luyện tập/thi tiếp theo mà không cần khởi động lại Server.

#### Task BE-4.12: API Giám Thị Phòng Lab & Điểm Danh Ca Thi (Proctor Lab Attendance & Monitor)
* **Kỹ sư phụ trách:** 🧑 **Nguyễn Trọng Tốt**
* **Endpoint:** `GET /api/v1/proctor/shifts/{shiftId}/attendance`, `PUT /api/v1/proctor/tickets/{id}/attendance`, `POST /api/v1/proctor/tickets/{id}/suspend`
* **Tệp liên quan:** `Features/Proctor/*`, `ProctorController.cs`, `StudentExamTicket.cs`
* **Mô tả nghiệp vụ:**
  - Cung cấp API phục vụ màn hình Giám sát phòng lab `ProctorRoomMonitorPage.tsx` (FE-P01):
    * `GET /api/v1/proctor/shifts/{shiftId}/attendance`: Lấy danh sách 40 máy lab trong ca thi gồm: STT (1–40), MSSV, Họ tên, Trạng thái vé thi (`SCHEDULED`, `IN_PROGRESS`, `SUBMITTED`, `SUSPENDED`), địa chỉ IP máy trạm `ip_address`, số lần cảnh báo mất focus Kiosk (`blur_count`).
    * `PUT /api/v1/proctor/tickets/{id}/attendance`: Giám thị đối chiếu thẻ SV/CCCD thực tế tại phòng lab, đánh dấu `is_present = true` (có mặt) hoặc `false` (vắng thi).
    * `POST /api/v1/proctor/tickets/{id}/suspend`: Giám thị lập biên bản đình chỉ thi khi phát hiện gian lận hoặc khi Kiosk gửi cảnh báo mất focus quá 3 lần; vé thi chuyển sang trạng thái `SUSPENDED` và khóa máy Kiosk của thí sinh.
* **Tiêu chí nghiệm thu (DoD):** Phân quyền `proctor` hoặc `lecturer` được phân công coi thi mới được gọi; trạng thái vé thi cập nhật chuẩn xác theo thời gian thực.

#### Task BE-4.13 (Vệ tinh): API Tra Cứu & Xác Thực Tính Toàn Vẹn Bảng Điểm Qua Mã QR (Public Verification)
* **Kỹ sư phụ trách:** 🧑 **Nguyễn Quang Thành**
* **Endpoint:** `GET /api/v1/verification/grades/{certificateHash}` (Public API)
* **Tệp liên quan:** `Features/Verification/Queries/VerifyGradeCertificate/*`, `VerificationController.cs`
* **Mô tả nghiệp vụ:**
  - Cung cấp cổng tra cứu công khai (không yêu cầu đăng nhập token) khi quét mã QR trên Báo cáo Khảo thí PDF hoặc Certificate bảng điểm:
    * Nhận tham số `certificateHash` từ URL mã QR.
    * Truy vấn và đối chiếu mã băm niêm phong của ca thi trong CSDL.
    * Trả về thông tin xác thực: Tên trường (Đại học FPT), Tên môn thi, Mã ca thi, Ngày thi, Trạng thái niêm phong hợp lệ (`VALID`), Ngày công bố điểm, và Chữ ký số xác nhận của Trưởng Bộ Môn / Giảng viên.
* **Tiêu chí nghiệm thu (DoD):** Endpoint public phản hồi $< 30$ms; kiểm tra mã băm giả mạo trả `HTTP 404 Not Found` kèm cảnh báo chứng chỉ không hợp lệ.

#### Task BE-QA: Bộ Test Suites xUnit & Architecture Tests NetArchTest
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

## 📊 3. BẢNG TỔNG HỢP TIẾN ĐỘ & PHÂN CÔNG TÁC VỤ BACKEND

| Mã Task | Tên Phân Hệ & Chức Năng | Kỹ Sư Phụ Trách | Trạng Thái Thực Tế |
|:---|:---|:---:|:---:|
| **BE-1.1** | Khởi tạo phiên luyện tập `POST /api/v1/practice/sessions` (Per/Full, Progressive 3-10 câu, On-Demand) | **🧑 Thành** | ✅ **Đã hoàn thành 100% (Thành đã làm)** |
| **BE-1.2** | Hàng đợi RAM `BoundedGradingQueueChannel` (1,000 slots RAM, `FullMode.Wait`) | **🧑 Thành** | ✅ **Đã hoàn thành 100% (Thành đã làm)** |
| **BE-1.3** | Nộp bài Per/Full Persist First $< 100$ms, Stream Whisper STT (bỏ `AudioUrl`, bỏ R2) | **🧑 Thành** | ✅ **Đã hoàn thành 100% (Thành đã làm)** |
| **BE-1.4** | Background Worker `GradingQueueWorker` & SignalR Realtime Hub `/hubs/practice` | **🧑 Thành** | ✅ **Đã hoàn thành 100% (Thành đã làm)** |
| **BE-1.5** | Gemini 1.5 Flash CoT 3 bước & Follow-up Engine ($4.0 \le \text{Score} \le 8.0$, 1-5 câu) | **🧑 Thành** | ✅ **Đã hoàn thành 100% (Thành đã làm)** |
| **BE-1.6** | Hoàn tất phiên, Lịch sử luyện tập & Chi tiết Scorecard | **🧑 Thành** | ✅ **Đã hoàn thành 100% (Thành đã làm)** |
| **BE-1.7** | API Dashboard Sinh viên FE-10 (thống kê buổi tập, điểm TB, độ thành thạo CLO, lịch thi) | **🧑 Thành** | ⏳ Kế hoạch của Thành (Chưa bắt đầu) |
| **BE-1.8** | API NextQuestion On-Demand & Thuật toán Anti-3-Consecutive Randomizer | **🧑 Thành** | ✅ **Đã hoàn thành 100% (Thành đã làm)** |
| **BE-1.9** | Cơ chế Timeout 10 Phút Không Tương Tác (Session Inactivity Timeout 410 Gone) | **🧑 Thành** | ✅ **Đã hoàn thành 100% (Thành đã làm)** |
| **BE-2.1** | Daily Quota Guard MF-02 ($K \le 3$ do Trưởng BM cấu hình, chặn HTTP 429) | **🧑 Thành** | ⏳ Kế hoạch của Thành (Chưa bắt đầu) |
| **BE-2.2** | Bốc đề thi thử theo Bloom từ kho `practice_questions` (cô lập an toàn `exam_questions`) | **🧑 Thành** | ⏳ Kế hoạch của Thành (Chưa bắt đầu) |
| **BE-2.3** | Server Master Timer & Voice-First Gate (chỉ lưu transcript, muộn 10s đánh dấu `is_late`) | **🧑 Thành** | ⏳ Kế hoạch của Thành (Chưa bắt đầu) |
| **BE-2.4** | Chấm 2 pha / Async Grading trả Instant Scorecard theo CLO & Lịch sử thi thử | **🧑 Thành** | ⏳ Kế hoạch của Thành (Chưa bắt đầu) |
| **BE-3.1** | Auth Google OAuth PKCE mở mọi email, cấp JWT 5 roles, bỏ `password_hash` | **🧑 Tốt** | ⏳ Chưa bắt đầu (Tốt tự code) |
| **BE-3.2** | API Quản lý Người dùng User Management FE-09 (status, role) | **🧑 Tốt** | ⏳ Chưa bắt đầu (Tốt tự code) |
| **BE-3.3** | AI sinh câu hỏi từ FLM Syllabus theo CLO & Barem riêng 10.0đ | **🧑 Tốt** | ⏳ Chưa bắt đầu (Tốt tự code) |
| **BE-3.4** | FluentValidation ép Barem Rubric $\sum \equiv 10.0$đ (HTTP 422) & Model Answer $\ge 50$ ký tự | **🧑 Tốt** | ⏳ Chưa bắt đầu (Tốt tự code) |
| **BE-3.5** | Checkbox tick chọn 2 kho `practice_questions`/`exam_questions` & Gửi duyệt Bộ Môn | **🧑 Tốt** | ⏳ Chưa bắt đầu (Tốt tự code) |
| **BE-3.6** | Trưởng Bộ Môn phê duyệt câu hỏi (APPROVED / NEEDS_REVISION / REJECTED) | **🧑 Tốt** | ⏳ Chưa bắt đầu (Tốt tự code) |
| **BE-3.7** | API Dashboard Giảng viên FE-10 (thống kê đề thi, tỷ lệ duyệt) | **🧑 Tốt** | ⏳ Chưa bắt đầu (Tốt tự code) |
| **BE-4.1** | Trưởng BM tạo kỳ thi, cấu hình ca thi (phòng lab, người coi thi), follow-up (1-5 câu) | **🧑 Tốt** | ⏳ Chưa bắt đầu (Tốt tự code) |
| **BE-4.2** | Check-in Kiosk IP Binding `ip_address` (Ghế 1-40, HTTP 403, 7 trạng thái vé thi HOA) | **🧑 Tốt** | ⏳ Chưa bắt đầu (Tốt tự code) |
| **BE-4.3** | Nộp bài MF-04 Persist First $< 100$ms, Upload R2 `STT_MSSV.webm` + SHA-256 | **🧑 Tốt** | ⏳ Chưa bắt đầu (Tốt tự code) |
| **BE-4.4** | BoundedChannel / DLQ chấm ngầm, AI Doubt Guard (`is_suspicious = true`), Evidence Panel API | **🧑 Tốt** | ⏳ Chưa bắt đầu (Tốt tự code) |
| **BE-4.5** | Công Bố Điểm Atomic 100%, `OneWayLockInterceptor` (`is_locked = true` $\to$ HTTP 403) | **🧑 Tốt** | ⏳ Chưa bắt đầu (Tốt tự code) |
| **BE-4.6** | Phân hệ Phúc khảo nội bộ `AppealRequest` Trưởng BM giao giảng viên chấm lại & Acknowledge | **🧑 Tốt** | ⏳ Chưa bắt đầu (Tốt tự code) |
| **BE-4.7** | API Quản lý Học kỳ Semester CRUD FE-08 | **🧑 Tốt** | ⏳ Chưa bắt đầu (Tốt tự code) |
| **BE-4.8** | Hộp thư Notification in-app FE-11 (tự động thông báo lịch thi, điểm, duyệt đề, phúc khảo) | **🧑 Tốt** | ⏳ Chưa bắt đầu (Tốt tự code) |
| **BE-4.9** | Module Báo cáo Khảo thí FPT xuất bảng điểm Excel `.xlsx` và PDF | **🧑 Thành** | ⏳ Kế hoạch của Thành (Chưa bắt đầu) |
| **BE-4.10** | Quản trị Dead-Letter Queue (DLQ Replay 5m) & API Audit Logs | **🧑 Tốt** | ⏳ Chưa bắt đầu (Tốt tự code) |
| **BE-4.11** | API Quản trị Cấu hình Hệ thống Admin (`system_configs`) | **🧑 Tốt** | ⏳ Chưa bắt đầu (Tốt tự code) |
| **BE-4.12** | API Giám thị Phòng thi & Điểm danh Ca thi Lab (`ProctorRoomMonitorPage.tsx`) | **🧑 Tốt** | ⏳ Chưa bắt đầu (Tốt tự code) |
| **BE-4.13** | API Tra cứu & Xác thực Bảng điểm Chữ ký số qua QR Code | **🧑 Thành** | ⏳ Kế hoạch của Thành (Chưa bắt đầu) |
| **BE-QA** | Bộ Test Suites xUnit & Architecture Tests NetArchTest (536/536 tests pass 100%, Exit Code 0) | **🧑 Tốt** | ⏳ Đang triển khai (Hiện có 536 tests của Khối 1/Architecture; MF-03/04 do Tốt viết tiếp) |
