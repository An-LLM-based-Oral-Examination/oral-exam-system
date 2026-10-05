# ⚙️ BACKEND FUNCTIONAL TASK BOARD & TECHNICAL SPECIFICATION (v6.0 — FUNCTIONAL BREAKDOWN)
## ĐỒ ÁN TỐT NGHIỆP SEP490 (FA26SE166) — HỆ THỐNG LUYỆN THI & ĐÁNH GIÁ VẤN ĐÁP BẰNG LLM
### KHO MÃ NGUỒN: `05_Source_Code/backend` | CỔNG DỊCH VỤ API: `5000` | DATABASE: `5432`

---

> [!IMPORTANT]
> **NGUYÊN TẮC VẬN HÀNH & PHÂN BỔ NHÂN SỰ BACKEND:**
> - **Mã đề tài:** FA26SE166 | **Học kỳ:** Fall 2026 (FA26) — Đại học FPT TP.HCM (FPT SG)
> - **Kiến trúc:** .NET 8 Clean Architecture 4 tầng (Domain, Application, Infrastructure, API) · CQRS MediatR · PostgreSQL 16 (28 bảng 3NF) · SignalR Typed Hubs · BoundedChannel (1,000 slots RAM) · Polly Resilience Pipeline.
> - **Cân bằng tải nhân sự:**
>   - 🧑 **Nguyễn Quang Thành (Lead Backend Architect):** Kiến trúc Core Clean Architecture, DI Container, Global Exception RFC 7807, Auth Google PKCE & JWT, Hàng đợi `BoundedGradingQueueChannel` (1,000 slots RAM), `GradingQueueWorker` concurrency, SignalR Typed Hub (`PracticeHub`), Quota Guard MF-02 ($K \le 3$), Check-in Kiosk IP Binding `ip_address` (Ghế 1–40), Nộp bài Persist First $< 100$ms, Khóa một chiều `OneWayLockInterceptor` (`is_locked = true` $\to$ HTTP 403), Phân hệ Phúc khảo Backend (`AppealRequest`), và Xuất Excel Khảo thí FPT (`ExcelService`).
>   - 🧑 **Nguyễn Trọng Tốt (Backend Developer, AI Specialist & QA Lead):** Phân hệ Barem Rubric 10.0đ & Ngân hàng câu hỏi MF-03, Prompt Gemini 1.5 Flash CoT 3 bước ép khuôn JSON, Whisper STT Server-side `timestamps_json`, Cloudflare R2 Presigned URL + SHA-256 (`STT_MSSV.webm`), AI Doubt Guard (`confidence < 0.70` $\to$ `is_suspicious = true`), Phân hệ Thẩm định Evidence Panel Backend, Polly Retry 3 lần (2s, 4s, 8s) + Dead-Letter Queue (`dead_letter_queues`), và toàn bộ Test Suites xUnit / NetArchTest.
>   - 🧑 **Nguyễn Đăng Hải (DB Specialist & Frontend Developer):** **CHỈ LÀM DATABASE**, bao gồm 28 bảng CSDL PostgreSQL 16 (DDL Schema, Check Constraints, Indexes, Seed Data, Docker Compose). **Hải tuyệt đối KHÔNG code logic C# Backend** mà chuyển sang dồn toàn lực phát triển Frontend cùng Hoàng.

---

## 📁 1. CẤU TRÚC 28 THỰC THỂ CSDL & 6 BOUNDED CONTEXTS (.NET 8 CLEAN ARCHITECTURE)

Hệ thống quản lý trọn vẹn 28 thực thể chuẩn hóa 3NF tương ứng 28 bảng CSDL PostgreSQL 16:

```text
backend/src/
├── Domain/                          # Tầng Core Thuần Khiết (Zero External Dependencies)
│   ├── Common/                      # BaseEntity.cs, IAggregateRoot.cs, ILockableEntity.cs
│   ├── Entities/                    # 28 thực thể chuẩn hóa trong 6 Bounded Contexts:
│   │   ├── Identity (1):            User.cs (Roles: student, lecturer, department_head, proctor, admin)
│   │   ├── Academic (4):            Semester.cs, Course.cs (has_follow_up, transcript_buffer_seconds, max_follow_up_questions), Class.cs, ClassEnrollment.cs
│   │   ├── Assessment (2):          Rubric.cs, RubricCriterion.cs (Ràng buộc sum = 10.0đ)
│   │   ├── Practice [MF-01] (5):    PracticeQuestion.cs, PracticeSession.cs, PracticeAnswer.cs, AiEvaluation.cs, AiEvaluationDetail.cs
│   │   ├── Mock Exam [MF-02] (6):   ExamStructure.cs, ExamSet.cs, ExamSetQuestion.cs, MockExamQuota.cs, MockExamSession.cs, MockExamAnswer.cs (Rút từ kho practice_questions)
│   │   ├── Official Exam [MF-04] (7): ExamQuestion.cs, OfficialExamSession.cs, RealExamSessionShift.cs, StudentExamTicket.cs (ip_address, Seat 1-40), ExamQuestionSubmission.cs, LecturerAudit.cs, LecturerAuditDetail.cs
│   │   ├── Appeals [MF-04] (1):     AppealRequest.cs (Gán Trưởng Bộ Môn department_head thẩm định độc lập)
│   │   └── Resilience & Audit (2):  DeadLetterQueue.cs, AuditLog.cs
│   ├── Enums/                       # UserRole, ExamTicketStatus, ExamInputMode, AppealStatus, BloomLevel
│   └── Exceptions/                  # DomainValidationException, OneWayLockException, QuotaExceededException
├── Application/                     # MediatR CQRS, FluentValidation, Interfaces
│   ├── Common/                      # IApplicationDbContext, IGeminiService, IWhisperService, IR2StorageService, IExcelExportService
│   ├── Behaviors/                   # ValidationBehavior.cs, LoggingBehavior.cs, PerformanceBehavior.cs
│   └── Features/                    # Tổ chức theo từng Phân hệ Nghiệp vụ (CQRS)
├── Infrastructure/                  # EF Core, External APIs, Background Workers, SignalR
│   ├── Persistence/                 # OralExamDbContext.cs, Configurations/ (Fluent API 28 tables), Migrations/
│   ├── Interceptors/                # OneWayLockInterceptor.cs (Chặn UPDATE/DELETE khi is_locked = true -> HTTP 403)
│   ├── Channels/                    # BoundedGradingQueueChannel.cs (Capacity = 1,000 slots RAM, FullMode.Wait)
│   ├── BackgroundJobs/              # GradingQueueWorker.cs (Polly Retry 2s-4s-8s), DlqReplayWorker.cs
│   ├── Hubs/                        # PracticeHub.cs (/hubs/practice)
│   └── Services/                    # GeminiEvaluationService.cs, CloudflareR2Service.cs, WhisperService.cs, QuotaService.cs, ExcelService.cs
└── API/                             # ASP.NET Core Host Gateway (Port 5000)
    ├── Controllers/v1/              # AuthController, CoursesController, PracticeController, MockExamsController, QuestionsController, OfficialExamsController, AppealsController, AdminController
    ├── Middlewares/                 # GlobalExceptionMiddleware.cs (RFC 7807), RequestLoggingMiddleware.cs
    └── Program.cs                   # CORS AllowFrontend trước StaticFiles, DI Container, MapHub
```

---

## 🛠️ 2. PHÂN RÃ CHI TIẾT TỪNG CHỨC NĂNG BACKEND (FUNCTIONAL TASK MATRIX)

---

### PHÂN HỆ 1: HẠ TẦNG CORE, AUTH & API GATEWAY

#### Task BE-1.1: Cấu hình Khung Solution, DI Container & Middleware RFC 7807
* **Kỹ sư phụ trách:** 🧑 **Nguyễn Quang Thành**
* **Tệp liên quan:** `API/Program.cs`, `Middlewares/GlobalExceptionMiddleware.cs`, `Application/Common/Models/Result.cs`
* **Mô tả nghiệp vụ:**
  - Cấu hình Dependency Injection cho 4 tầng Clean Architecture.
  - Cài đặt `GlobalExceptionMiddleware.cs`: Bắt toàn bộ ngoại lệ chưa xử lý, chuyển đổi thành định dạng chuẩn RFC 7807 `ProblemDetails` (`type`, `title`, `status`, `detail`, `instance`, `errors`).
  - Cấu hình CORS `AllowFrontend` cho `http://localhost:3000` đặt **TRƯỚC** `app.UseHttpsRedirection()` và static files.
* **Tiêu chí nghiệm thu (DoD):** Swagger UI mở tại `http://localhost:5000/swagger`. Mọi lỗi validation ném `HTTP 400`/`HTTP 422` kèm danh sách trường lỗi rõ ràng.

#### Task BE-1.2: Xác thực Google OAuth PKCE & Quản lý JWT Token
* **Kỹ sư phụ trách:** 🧑 **Nguyễn Quang Thành**
* **Tệp liên quan:** `Features/Auth/*`, `API/Controllers/v1/AuthController.cs`
* **Mô tả nghiệp vụ:**
  - Tiếp nhận Google ID Token từ Frontend (`POST /api/v1/auth/google-login`).
  - Kiểm tra email domain `@fpt.edu.vn` hoặc `@fe.edu.vn`. Tìm hoặc tự động kích hoạt tài khoản trong bảng `users`.
  - Cấp cặp JWT Access Token (hạn 60 phút) và Refresh Token (hạn 7 ngày).
  - Trả về thông tin User kèm Role (1 trong 5 roles: `student`, `lecturer`, `department_head`, `proctor`, `admin`).
* **Tiêu chí nghiệm thu (DoD):** Đăng nhập thành công trả về HTTP 200 kèm JWT claim `sub`, `email`, `role`. Gọi API không kèm Bearer Token trả ngay `HTTP 401 Unauthorized`.

---

### PHÂN HỆ 2: MF-01 — LUYỆN TẬP VẤN ĐÁP TƯƠNG TÁC (INTERACTIVE PRACTICE)

#### Task BE-2.1: Khởi tạo phiên luyện tập tự do (Start Practice Session)
* **Kỹ sư phụ trách:** 🧑 **Nguyễn Quang Thành**
* **Endpoint:** `POST /api/v1/practice/sessions`
* **Tệp liên quan:** `Features/Practice/Commands/StartPracticeSession/*`, `PracticeController.cs`
* **Mô tả nghiệp vụ:**
  - Nhận: `studentId`, `courseId`, `isFullSession` (`false`: Per-Question, `true`: Full-Session), `topic` (tùy chọn), `questionCount` (3–5 câu).
  - Đọc cấu hình môn học từ bảng `courses`: `transcript_buffer_seconds` (10–300s, mặc định 60s).
  - Rút ngẫu nhiên $N$ câu hỏi từ kho `practice_questions` thuộc môn học kèm Barem Rubric $\sum \equiv 10.0$đ.
  - Tạo bản ghi trong `practice_sessions` với trạng thái `in_progress`.
* **Tiêu chí nghiệm thu (DoD):** Trả về `sessionId`, `transcriptBufferSeconds`, và danh sách câu hỏi kèm tóm tắt tiêu chí rubric.

#### Task BE-2.2: Hàng đợi RAM BoundedChannel (1,000 slots RAM)
* **Kỹ sư phụ trách:** 🧑 **Nguyễn Quang Thành**
* **Tệp liên quan:** `Infrastructure/Channels/BoundedGradingQueueChannel.cs`
* **Mô tả nghiệp vụ:**
  - Tạo `Channel<GradingTask>` với `capacity = 1000`, thiết lập `FullMode = BoundedChannelFullMode.Wait`.
  - Payload `GradingTask` chứa: `AnswerId`, `SessionId`, `QuestionId`, `StudentId`, `AnswerText`, `IsFollowUp`, `ParentAnswerId`, `IsFullSession`, `ConnectionId`.
  - Hỗ trợ ghi bất đồng bộ `EnqueueAsync` và đọc luồng `ReadAllAsync`.
* **Tiêu chí nghiệm thu (DoD):** Chịu tải đồng thời nhiều requests mà không gây rò rỉ bộ nhớ, giữ nguyên thứ tự FIFO.

#### Task BE-2.3: Nộp câu trả lời đơn lẻ Persist First (Submit Answer — Per-Question)
* **Kỹ sư phụ trách:** 🧑 **Nguyễn Quang Thành**
* **Endpoint:** `POST /api/v1/practice/sessions/{sessionId}/answers`
* **Tệp liên quan:** `Features/Practice/Commands/SubmitPracticeAnswer/*`, `PracticeController.cs`
* **Mô tả nghiệp vụ:**
  - Kiểm tra quyền sở hữu phiên: `session.StudentId == studentId` và `session.Status == "in_progress"`.
  - **Persist First:** Lưu ngay bản ghi vào bảng `practice_answers` với trạng thái `status = 'pending'`, `submitted_at = DateTime.UtcNow`.
  - Đẩy `GradingTask` vào `BoundedGradingQueueChannel`.
  - **Trả về `HTTP 202 Accepted` ngay lập tức trong $< 100$ms** kèm `answerId`.
* **Tiêu chí nghiệm thu (DoD):** Thời gian phản hồi API bắt buộc $< 100$ms, không đợi AI chấm mới trả response.

#### Task BE-2.4: Nộp bài trọn gói (Submit Batch Answers — Full-Session)
* **Kỹ sư phụ trách:** 🧑 **Nguyễn Quang Thành**
* **Endpoint:** `POST /api/v1/practice/sessions/{sessionId}/batch-submit`
* **Tệp liên quan:** `Features/Practice/Commands/SubmitPracticeBatch/*`
* **Mô tả nghiệp vụ:**
  - Tiếp nhận danh sách $N$ câu trả lời của phiên `[Full-Session]`.
  - Lưu hàng loạt vào bảng `practice_answers` với `status = 'pending'`.
  - Đẩy lần lượt $N$ tasks vào `BoundedGradingQueueChannel`.
  - Cập nhật phiên `practice_sessions.status = 'submitted'`.
  - Trả về `HTTP 202 Accepted`.
* **Tiêu chí nghiệm thu (DoD):** Lưu trọn vẹn $N$ câu trả lời trong một database transaction duy nhất.

#### Task BE-2.5: Dịch vụ chấm AI Gemini 1.5 Flash CoT 3 bước & Follow-up Engine
* **Kỹ sư phụ trách:** 🧑 **Nguyễn Trọng Tốt**
* **Tệp liên quan:** `Infrastructure/Services/GeminiEvaluationService.cs`, `IGeminiEvaluationService.cs`
* **Mô tả nghiệp vụ:**
  - Kết nối Gemini 1.5 Flash API với cấu hình `response_mime_type="application/json"`.
  - Prompt Chain-of-Thought (CoT) 3 bước:
    1. *Bước 1:* Phân tích luận điểm trong câu trả lời sinh viên.
    2. *Bước 2:* So khớp từng tiêu chí trong Barem Rubric $\sum \equiv 10.0$đ và câu trả lời mẫu.
    3. *Bước 3:* Chấm điểm từng tiêu chí con và đưa ra nhận xét sư phạm định tính.
  - **Quy tắc Follow-up MF-01:**
    - Nếu là chế độ `[Per-Question]` VÀ điểm câu trả lời rơi vào khoảng ranh giới **$4.0 \le \text{Score} \le 8.0$** VÀ số câu hỏi phụ chưa vượt quá cấu hình Admin (`1 <= max_follow_up_questions <= 5`, mặc định 2 câu) $\to$ Gemini sinh 1 câu hỏi phụ đào sâu.
    - Nếu $\text{Score} < 4.0$ hoặc $\text{Score} > 8.0$ hoặc đã đạt số câu tối đa $\to$ Chốt bảng điểm Scorecard, không sinh thêm câu hỏi phụ.
    - Chế độ `[Full-Session]`: **TUYỆT ĐỐI KHÔNG sinh câu hỏi phụ**.
* **Tiêu chí nghiệm thu (DoD):** Output JSON chuẩn schema, điểm thành phần khớp đúng rubric, câu hỏi phụ đào sâu vào điểm yếu của sinh viên.

#### Task BE-2.6: Background Worker Chấm Ngầm & SignalR Realtime Hub
* **Kỹ sư phụ trách:** 🧑 **Nguyễn Quang Thành & 🧑 Nguyễn Trọng Tốt**
* **Tệp liên quan:** `Infrastructure/BackgroundJobs/GradingQueueWorker.cs`, `Infrastructure/Hubs/PracticeHub.cs`, `IPracticeClient.cs`
* **Mô tả nghiệp vụ:**
  - `GradingQueueWorker` rút task từ `BoundedGradingQueueChannel`.
  - Gọi `GeminiEvaluationService` chấm điểm.
  - Bọc Polly Resilience Pipeline: Retry 3 lần với Exponential Backoff ($2\text{s} \to 4\text{s} \to 8\text{s}$). Nếu thất bại cả 3 lần, đẩy task vào bảng `dead_letter_queues`.
  - Lưu kết quả vào `practice_answers` (`status = 'graded'`), `ai_evaluations` và `ai_evaluation_details`.
  - Bắn sự kiện realtime qua SignalR `PracticeHub`:
    - `ReceiveScorecard`: Trả bảng điểm chi tiết từng tiêu chí Rubric.
    - `ReceiveFollowUpQuestion`: Trả câu hỏi phụ đào sâu (nếu thuộc diện kích hoạt).
* **Tiêu chí nghiệm thu (DoD):** Sau khi nộp bài 3–5 giây, client SignalR nhận được scorecard/follow-up JSON hoàn chỉnh mà không cần polling HTTP.

#### Task BE-2.7: Cấu hình Hệ Thống Luyện Tập của Admin & Lịch Sử Luyện Tập
* **Kỹ sư phụ trách:** 🧑 **Nguyễn Quang Thành & 🧑 Nguyễn Trọng Tốt**
* **Endpoint:** `PUT /api/v1/admin/practice-config`, `GET /api/v1/practice/history`, `GET /api/v1/practice/sessions/{id}/scorecard`
* **Tệp liên quan:** `Features/Admin/*`, `Features/Practice/Queries/*`
* **Mô tả nghiệp vụ:**
  - **Admin duy nhất** có quyền cấu hình số câu hỏi follow-up luyện tập (`1 <= max_follow_up_questions <= 5`, mặc định 2 câu). Giảng viên KHÔNG cấu hình follow-up trong MF-01.
  - API xem lịch sử luyện tập của sinh viên: phân trang, lọc theo môn học, hiển thị chi tiết Scorecard từng phiên.
* **Tiêu chí nghiệm thu (DoD):** Admin lưu cấu hình thành công; sinh viên xem lại đầy đủ lịch sử bài làm kèm nhận xét chi tiết.

---

### PHÂN HỆ 3: MF-02 — THI THỬ TÍNH GIỜ CÓ HẠN NGẠCH (TIMED MOCK EXAM)

#### Task BE-3.1: Daily Quota Guard (Tối đa 3 lượt/ngày/môn)
* **Kỹ sư phụ trách:** 🧑 **Nguyễn Quang Thành**
* **Endpoint:** `POST /api/v1/mock-exams/sessions`
* **Tệp liên quan:** `Features/MockExams/Commands/StartMockExamSession/*`, `Infrastructure/Services/QuotaService.cs`, `MockExamsController.cs`
* **Mô tả nghiệp vụ:**
  - Kiểm tra số lượt thi thử của `student_id` trên `course_id` trong ngày hiện tại (`DateTime.UtcNow.Date`).
  - Nếu số lượt đã thi $\ge 3 \to$ Lập tức ném `QuotaExceededException` trả mã **`HTTP 429 Too Many Requests`** kèm thông báo: *"Bạn đã sử dụng hết hạn ngạch 3 lượt thi thử trong ngày cho môn học này"*.
  - Nếu số lượt $< 3 \to$ Tăng bộ đếm trong `mock_exam_quotas` và cho phép tạo phiên thi thử.
* **Tiêu chí nghiệm thu (DoD):** Gọi API lần 1, 2, 3 thành công; lần 4 trả ngay HTTP 429.

#### Task BE-3.2: Bốc đề thi thử theo ma trận Bloom từ kho `practice_questions`
* **Kỹ sư phụ trách:** 🧑 **Nguyễn Trọng Tốt**
* **Tệp liên quan:** `Features/MockExams/*`, `ExamStructure.cs`, `ExamSet.cs`
* **Mô tả nghiệp vụ:**
  - Thuật toán bốc đề ngẫu nhiên từ kho câu hỏi luyện tập chung (`practice_questions`) theo tỷ lệ ma trận Bloom (Remember/Understand, Apply, Analyze).
  - **Cô lập an toàn tuyệt đối:** Không được rút câu hỏi từ kho thi thật `exam_questions` (dành riêng cho MF-04).
  - Lưu cấu trúc bộ đề vào `mock_exam_sessions` và danh sách câu hỏi.
* **Tiêu chí nghiệm thu (DoD):** Bộ đề thi thử sinh ra đúng số lượng câu hỏi và tỷ lệ Bloom quy định, không trùng lặp câu hỏi trong cùng một đề.

#### Task BE-3.3: Server-side Master Timer & Tùy chọn Follow-up chủ động
* **Kỹ sư phụ trách:** 🧑 **Nguyễn Quang Thành & 🧑 Nguyễn Trọng Tốt**
* **Endpoint:** `POST /api/v1/mock-exams/sessions/{id}/submit`
* **Tệp liên quan:** `Features/MockExams/Commands/SubmitMockExam/*`
* **Mô tả nghiệp vụ:**
  - Lưu `start_time` và `allocated_minutes` trên server.
  - Khi sinh viên nộp bài: Kiểm tra nếu thời gian nộp vượt quá quy định $> 10$ giây $\to$ Đánh dấu bài nộp muộn (`is_late = true`) hoặc từ chối tiếp nhận.
  - **Tùy chọn Follow-up chủ động của sinh viên:** Sinh viên tự chọn Có/Không Follow-up trước khi thi thử. Nếu chọn Có: AI kích hoạt hỏi chuyên sâu ngữ cảnh (`[Needs Follow-up]`) khi câu trả lời chưa rõ ý.
  - Chấm điểm tức thì và trả về **Scorecard chi tiết kèm nhận xét sư phạm từng tiêu chí theo CLO**.
* **Tiêu chí nghiệm thu (DoD):** Chặn đứng gian lận chỉnh giờ máy client; Scorecard phân tích rõ ràng năng lực theo chuẩn đầu ra môn học.

---

### PHÂN HỆ 4: MF-03 — NGÂN HÀNG CÂU HỎI & RUBRIC STUDIO 10.0 (QUESTION BANK & RUBRIC STUDIO)

#### Task BE-4.1: Giảng viên dùng AI sinh câu hỏi từ FLM theo barem của mình
* **Kỹ sư phụ trách:** 🧑 **Nguyễn Trọng Tốt**
* **Endpoint:** `POST /api/v1/questions/generate-from-flm`
* **Tệp liên quan:** `Features/QuestionBank/Commands/GenerateQuestionsFromFlm/*`, `QuestionsController.cs`
* **Mô tả nghiệp vụ:**
  - Giảng viên (`lecturer`) nhập Syllabus/FLM API hoặc đề cương môn học, chọn CLO và mức Bloom.
  - Gemini 1.5 Pro sinh câu hỏi kèm: Đề bài, Model Answer ($\ge 50$ ký tự), danh sách tiêu chí Rubric ($\sum \equiv 10.0$đ).
  - Giảng viên được toàn quyền xem bản nháp, chỉnh sửa nội dung đề bài, câu trả lời mẫu và điểm từng tiêu chí barem.
  - Lưu câu hỏi ở trạng thái dự thảo `DRAFT`.
* **Tiêu chí nghiệm thu (DoD):** Sinh câu hỏi hợp lệ, Model Answer $\ge 50$ ký tự, tổng điểm các tiêu chí rubric đúng 10.0đ.

#### Task BE-4.2: Tạo Barem Rubric thủ công & Ràng buộc cứng $\sum \equiv 10.0$đ
* **Kỹ sư phụ trách:** 🧑 **Nguyễn Trọng Tốt**
* **Endpoint:** `POST /api/v1/rubrics`, `POST /api/v1/questions`
* **Tệp liên quan:** `Features/QuestionBank/Commands/CreateQuestion/*`, `CreateQuestionValidator.cs`
* **Mô tả nghiệp vụ:**
  - FluentValidation kiểm tra chặt chẽ:
    1. Tổng điểm của danh sách `RubricCriteria` bắt buộc $\sum \equiv 10.0$ điểm (sai lệch ném ngay `HTTP 422 Unprocessable Entity`).
    2. Câu trả lời mẫu `ModelAnswer` bắt buộc $\ge 50$ ký tự.
    3. Mỗi tiêu chí có trọng số $> 0$ và mô tả rõ ràng.
* **Tiêu chí nghiệm thu (DoD):** Gửi rubric có tổng 9.5đ hoặc 10.5đ $\to$ API ném lỗi 422 kèm message: *"Tổng điểm các tiêu chí rubric bắt buộc phải bằng đúng 10.0 điểm"*.

#### Task BE-4.3: Quy trình Gửi duyệt Bộ Môn & Trưởng Bộ Môn Phê Duyệt
* **Kỹ sư phụ trách:** 🧑 **Nguyễn Trọng Tốt**
* **Endpoint:** `POST /api/v1/questions/batch-submit-review`, `PUT /api/v1/questions/{id}/review-decision`
* **Tệp liên quan:** `Features/QuestionBank/Commands/BatchSubmitReview/*`, `Features/QuestionBank/Commands/ReviewQuestionDecision/*`
* **Mô tả nghiệp vụ:**
  - **Giảng viên gửi duyệt:** Mọi câu hỏi do Giảng viên tạo (thủ công hoặc AI gen) **bắt buộc phải gửi qua Trưởng Bộ Môn duyệt** (`batch-submit-review`) $\to$ Chuyển trạng thái sang `SUBMITTED_FOR_REVIEW`.
  - **Trưởng Bộ Môn thẩm định:** Trưởng Bộ Môn (`department_head`) là chốt chặn phê duyệt duy nhất đưa ra 3 quyết định:
    1. Bấm **"Phê duyệt"** (`APPROVED`): Lưu chính thức vào ngân hàng câu hỏi môn học.
    2. Bấm **"Yêu cầu chỉnh sửa"** (`NEEDS_REVISION` kèm lý do góp ý $\ge 10$ ký tự): Trả về cho giảng viên sửa lại.
    3. Bấm **"Từ chối"** (`REJECTED` kèm lý do): Loại bỏ câu hỏi.
* **Tiêu chí nghiệm thu (DoD):** Chặn đứng việc câu hỏi chưa qua phê duyệt được đưa vào đề thi; kiểm soát chặt chẽ trạng thái vòng đời câu hỏi.

---

### PHÂN HỆ 5: MF-04 — THI THẬT PHÒNG LAB KIOSK, CÔNG BỐ ĐIỂM & PHÚC KHẢO NỘI BỘ

#### Task BE-5.1: Quản trị Kỳ Thi & Cấu hình Môn Thi của Trưởng Bộ Môn
* **Kỹ sư phụ trách:** 🧑 **Nguyễn Quang Thành**
* **Endpoint:** `POST /api/v1/official-exams/seasons`, `POST /api/v1/official-exams/shifts`, `PUT /api/v1/official-exams/courses/{courseId}/config`
* **Tệp liên quan:** `Features/OfficialExams/*`, `OfficialExamsController.cs`
* **Mô tả nghiệp vụ:**
  - Trưởng Bộ Môn khởi tạo kỳ thi (`OfficialExamSession`, ví dụ Kỳ thi Kết thúc môn FA26) và gán danh sách môn thi.
  - Khi ấn vào từng môn thi trong kỳ thi, Trưởng Bộ Môn thực hiện:
    1. Cấu hình danh sách **Ca thi** (`RealExamSessionShift`: phòng lab, kíp thi, thời gian bắt đầu/kết thúc, phân công giám thị).
    2. **Cấu hình Follow-up:** Bật/tắt hỏi chuyên sâu (`has_follow_up`) và số lượng câu hỏi follow-up áp dụng chung cho Môn thi trong kỳ thi (`max_follow_up_questions` từ 1–2 câu, đồng bộ cho tất cả các ca thi của môn).
    3. Cấu hình `ExamInputMode` (`VoiceOnly`, `VoiceWithTranscriptEdit`, `VoiceAndTextInput`) và thời gian đệm `TranscriptBufferSeconds` (10–300s).
* **Tiêu chí nghiệm thu (DoD):** Cấu hình được lưu trữ chính xác vào CSDL và áp dụng thống nhất cho toàn bộ ca thi của môn.

#### Task BE-5.2: Check-in Kiosk Phòng Lab Ràng Buộc IP (`ip_address`)
* **Kỹ sư phụ trách:** 🧑 **Nguyễn Quang Thành**
* **Endpoint:** `POST /api/v1/official-exams/tickets/{ticketId}/check-in`
* **Tệp liên quan:** `Features/OfficialExams/Commands/CheckInKioskTicket/*`
* **Mô tả nghiệp vụ:**
  - Trích xuất IP Client từ `HttpContext.Connection.RemoteIpAddress`.
  - Đối chiếu với cột **`ip_address`** trên vé thi ứng với số máy trạm (`seat_number` 1–40).
  - Nếu không khớp IP quy định $\to$ Trả ngay **`HTTP 403 Forbidden`** với thông báo: *"Máy trạm không hợp lệ cho vị trí ghế ngồi này"*.
  - Nếu khớp IP $\to$ Cập nhật trạng thái vé thi theo đúng 7 bước viết HOA: `SCHEDULED` $\to$ `IN_PROGRESS`.
* **Tiêu chí nghiệm thu (DoD):** Chặn đứng việc sinh viên ngồi máy này thi hộ máy khác; kiểm soát đúng 7 trạng thái vé thi viết HOA.

#### Task BE-5.3: Nộp bài Thi Thật Persist First $< 100$ms & Cloudflare R2 Upload
* **Kỹ sư phụ trách:** 🧑 **Nguyễn Quang Thành & 🧑 Nguyễn Trọng Tốt**
* **Endpoint:** `POST /api/v1/official-exams/shifts/{shiftId}/submit`
* **Tệp liên quan:** `Features/OfficialExams/Commands/SubmitOfficialExam/*`, `Infrastructure/Services/CloudflareR2Service.cs`
* **Mô tả nghiệp vụ:**
  - Cấp Presigned URL PUT cho client upload trực tiếp file audio lên Cloudflare R2 với tên file bất biến **`STT_MSSV.webm`** (ví dụ: `01_SE170123.webm`).
  - Nhận bài thi: Lưu mã băm `audio_hash` SHA-256 niêm phong audio.
  - **Persist First:** Lưu DB vào `exam_question_submissions` và cập nhật `student_exam_tickets.status = 'SUBMITTED'` trong $< 100$ms.
  - **Kiosk không có điểm liền và không khiếu nại tại chỗ:** Kiosk khóa màn hình, thông báo bài đã lưu an toàn.
  - Đẩy task chấm ngầm vào `BoundedGradingQueueChannel` (1,000 slots RAM) hoặc DLQ khi AI quá tải (HTTP 429) để chấm bù ngầm chỉ dựa trên bản transcript.
* **Tiêu chí nghiệm thu (DoD):** Thời gian tiếp nhận nộp bài $< 100$ms, file audio được bảo toàn trên R2 với mã băm SHA-256.

#### Task BE-5.4: Chốt chặn AI Doubt Guard & Evidence Panel Hậu Kiểm
* **Kỹ sư phụ trách:** 🧑 **Nguyễn Trọng Tốt**
* **Endpoint:** `GET /api/v1/audit/shifts/{shiftId}/submissions`, `POST /api/v1/audit/submissions/{id}/override-score`
* **Tệp liên quan:** `Features/Audit/*`, `AuditController.cs`
* **Mô tả nghiệp vụ:**
  - AI chấm ngầm: Nếu `confidence_score < 0.70` (do ồn, phát âm không rõ, mâu thuẫn chuỗi CoT) hoặc bài thi bị fail/điểm liệt $\to$ Tự động bật cờ **`is_suspicious = true`**.
  - **Cổng Hậu kiểm Giảng viên phân loại 2 nhóm:**
    - *Nhóm 1 (Đáng nghi ngờ / Fail / Cần can thiệp):* Sinh viên bắt buộc phải đợi Giảng viên rà soát và chấm lại toàn bộ các bài trong nhóm này.
    - *Nhóm 2 (Độ tin cậy cao):* Giảng viên đối soát nhanh.
  - Cung cấp API Evidence Panel gồm đủ 3 thành phần: `audioR2Url`, `transcriptWhisper` gốc, và `aiChainOfThought`.
  - API Giảng viên điều chỉnh điểm `OverrideScoreCommand`: **Bắt buộc nhập lý do giải trình `overrideReason` $\ge 10$ ký tự**.
* **Tiêu chí nghiệm thu (DoD):** Giảng viên nghe lại audio và đối soát transcript trực quan; sửa điểm không có lý do bị chặn `HTTP 422`.

#### Task BE-5.5: Công Bố Điểm, Khóa Một Chiều `OneWayLockInterceptor` & Xuất Excel FPT
* **Kỹ sư phụ trách:** 🧑 **Nguyễn Quang Thành**
* **Endpoint:** `POST /api/v1/official-exams/shifts/{shiftId}/publish-grades`, `GET /api/v1/audit/shifts/{shiftId}/export-excel`
* **Tệp liên quan:** `Features/OfficialExams/Commands/PublishGrades/*`, `Infrastructure/Persistence/Interceptors/OneWayLockInterceptor.cs`, `Infrastructure/Services/ExcelService.cs`
* **Mô tả nghiệp vụ:**
  - **Atomic Check:** Giảng viên chỉ được bấm "Công Bố Điểm" khi **100% sinh viên trong ca thi đã có điểm hoàn chỉnh** (nếu còn dù chỉ 1 sinh viên chưa có điểm $\to$ Chặn `HTTP 422 Unprocessable Entity`).
  - Khi công bố điểm: Cập nhật ca thi `is_locked = true`, vé thi sang `PUBLISHED` rồi `LOCKED`.
  - **`OneWayLockInterceptor.cs`:** Đánh chặn tại EF Core `SavingChangesAsync`. Nếu thực thể thuộc ca thi có `is_locked = true` mà bị `Modified` hoặc `Deleted` $\to$ Ném ngay `OneWayLockException` trả về **`HTTP 403 Forbidden`**.
  - `ExcelService.cs`: Dùng thư viện EPPlus xuất bảng điểm định dạng chuẩn Khảo thí Đại học FPT (MSSV, Họ tên, Điểm thành phần, Điểm tổng kết).
* **Tiêu chí nghiệm thu (DoD):** Ca thi đã khóa không thể bị can thiệp điểm số bởi bất kỳ ai (kể cả admin qua API); xuất file Excel đúng mẫu khảo thí.

#### Task BE-5.6: Phân hệ Phúc Khảo Nội Bộ (`AppealRequest`)
* **Kỹ sư phụ trách:** 🧑 **Nguyễn Quang Thành**
* **Endpoint:** `POST /api/v1/appeals`, `GET /api/v1/appeals`, `PUT /api/v1/appeals/{id}/review`
* **Tệp liên quan:** `Features/Appeals/*`, `AppealsController.cs`
* **Mô tả nghiệp vụ:**
  - Sinh viên ở nhà xem điểm chính thức trên Student Portal. Nếu không đồng ý điểm: Tạo đơn phúc khảo nội bộ `POST /api/v1/appeals` kèm lý do khiếu nại.
  - Hệ thống tự động gán đơn cho **Trưởng Bộ Môn (`department_head`)** thẩm định độc lập.
  - Chặn sinh viên tạo đơn trùng lặp khi đã có đơn đang ở trạng thái `PENDING` hoặc `IN_REVIEW`.
  - Trưởng Bộ Môn thẩm định: Xem Evidence Panel, nhập điểm mới nếu chấp thuận (`APPROVED`) hoặc giữ nguyên điểm nếu bác đơn (`REJECTED`).
* **Tiêu chí nghiệm thu (DoD):** Luồng phúc khảo khép kín trong hệ thống; Trưởng Bộ Môn là người duy nhất có quyền duyệt đơn phúc khảo.

---

### PHÂN HỆ 6: QUẢN TRỊ ADMIN & GIÁM SÁT HÀNG ĐỢI CHẾT (DLQ)

#### Task BE-6.1: Quản trị Dead-Letter Queue & Chấm Bù Tự Động
* **Kỹ sư phụ trách:** 🧑 **Nguyễn Quang Thành & 🧑 Nguyễn Trọng Tốt**
* **Endpoint:** `GET /api/v1/admin/dlq/tasks`, `POST /api/v1/admin/dlq/tasks/{id}/replay`
* **Tệp liên quan:** `Features/Admin/*`, `Infrastructure/BackgroundJobs/DlqReplayWorker.cs`
* **Mô tả nghiệp vụ:**
  - Bảng `dead_letter_queues` lưu trữ toàn bộ các task chấm thi thất bại sau 3 lần retry của Polly.
  - Background Service `DlqReplayWorker` tự động quét định kỳ sau 5 phút để kích hoạt chấm bù cho các task bị lỗi tạm thời do nghẽn mạng hoặc quá tải API.
  - Admin có dashboard xem danh sách DLQ, log lỗi và bấm "Replay Task" thủ công khi cần.
* **Tiêu chí nghiệm thu (DoD):** Cam kết Zero Data Loss 100%, không bài làm nào của sinh viên bị thất lạc kể cả khi dịch vụ AI bên ngoài gặp sự cố kéo dài.

---

### PHÂN HỆ 7: BỘ TEST SUITES KIỂM THỬ TỰ ĐỘNG & QUALITY GATES

#### Task BE-7.1: Bộ Unit Tests xUnit & NetArchTest Ranh Giới Kiến Trúc
* **Kỹ sư phụ trách:** 🧑 **Nguyễn Trọng Tốt (QA Lead)**
* **Tệp liên quan:** `tests/OralExamination.UnitTests/*`, `tests/FA26SE166.ArchitectureTests/*`
* **Mô tả nghiệp vụ:**
  - Viết xUnit bao phủ:
    1. Validation Rules: Barem rubric $\sum \equiv 10.0$đ, Model Answer $\ge 50$ ký tự, lý do sửa điểm $\ge 10$ ký tự.
    2. Quota Guard: Chặn lượt thứ 4 trả HTTP 429.
    3. One-Way Lock: Assert ném HTTP 403 khi sửa ca thi `is_locked = true`.
    4. BoundedChannel: Assert Enqueue/Dequeue FIFO, không rò rỉ bộ nhớ.
    5. Clean Architecture Integrity: Dùng NetArchTest khóa ranh giới 4 tầng (Domain không dính EF/Infra, Application không dính API, Controller không gọi DbContext trực tiếp).
* **Tiêu chí nghiệm thu (DoD):** Chạy `dotnet test` đạt **100% tests PASS (Exit Code 0)**, thời gian chạy toàn bộ test suite $< 3$ giây.

---

## 📊 3. BẢNG TỔNG HỢP TIẾN ĐỘ & PHÂN CÔNG TÁC VỤ BACKEND

| Mã Task | Tên Phân Hệ & Chức Năng | Kỹ Sư Phụ Trách | Trạng Thái Hiện Tại |
|:---|:---|:---:|:---:|
| **BE-1.1** | Khung Solution, DI Container & Global Exception RFC 7807 | **🧑 Thành** | ✅ Đã hoàn thành |
| **BE-1.2** | Auth Google OAuth PKCE & Cấp JWT Token theo 5 Roles | **🧑 Thành** | ✅ Đã hoàn thành |
| **BE-2.1** | Khởi tạo phiên luyện tập `POST /api/v1/practice/sessions` | **🧑 Thành** | 🔄 Cần nâng cấp trả `buffer_seconds` |
| **BE-2.2** | Hàng đợi RAM `BoundedGradingQueueChannel` (1,000 slots) | **🧑 Thành** | ⏳ Cần triển khai |
| **BE-2.3** | Nộp bài Per-Question Persist First $< 100$ms (`202 Accepted`) | **🧑 Thành** | 🔄 Cần nâng cấp tích hợp Channel |
| **BE-2.4** | Nộp bài Full-Session Batch Submit (`batch-submit`) | **🧑 Thành** | ⏳ Cần triển khai |
| **BE-2.5** | Gemini 1.5 Flash CoT 3 bước & Follow-up Engine ($4.0 \le \text{Score} \le 8.0$) | **🧑 Tốt** | ⏳ Cần triển khai |
| **BE-2.6** | Background Worker `GradingQueueWorker` & SignalR `PracticeHub` | **🧑 Thành + Tốt** | ⏳ Cần triển khai |
| **BE-2.7** | Cấu hình Admin Follow-up (1–5 câu) & Lịch sử luyện tập | **🧑 Thành + Tốt** | ⏳ Cần triển khai |
| **BE-3.1** | Daily Quota Guard MF-02 ($K \le 3$, chặn HTTP 429) | **🧑 Thành** | ✅ Đã hoàn thành core logic |
| **BE-3.2** | Bốc đề thi thử theo Bloom từ kho `practice_questions` | **🧑 Tốt** | ✅ Đã hoàn thành core logic |
| **BE-3.3** | Server Master Timer & Instant Feedback Scorecard theo CLO | **🧑 Thành + Tốt** | ⏳ Cần hoàn thiện |
| **BE-4.1** | Giảng viên dùng AI sinh câu hỏi từ FLM theo barem riêng | **🧑 Tốt** | ✅ Đã hoàn thành service |
| **BE-4.2** | FluentValidation ép Barem Rubric $\sum \equiv 10.0$đ (HTTP 422) | **🧑 Tốt** | ✅ Đã hoàn thành validator |
| **BE-4.3** | Giảng viên nộp đề & Trưởng Bộ Môn Phê duyệt (APPROVED / NEEDS_REVISION) | **🧑 Tốt** | ✅ Đã hoàn thành CQRS |
| **BE-5.1** | Trưởng BM tạo kỳ thi, gán môn, cấu hình ca thi & follow-up (1-2 câu) | **🧑 Thành** | ✅ Đã hoàn thành entities & API |
| **BE-5.2** | Check-in Kiosk IP Binding `ip_address` (Ghế 1-40, HTTP 403) | **🧑 Thành** | ✅ Đã hoàn thành |
| **BE-5.3** | Nộp bài MF-04 Persist First $< 100$ms & Upload R2 `STT_MSSV.webm` | **🧑 Thành + Tốt** | ⏳ Cần tích hợp hoàn chỉnh R2 |
| **BE-5.4** | Chốt chặn AI Doubt Guard (`is_suspicious = true`) & Evidence Panel | **🧑 Tốt** | ⏳ Cần hoàn thiện Evidence API |
| **BE-5.5** | Công Bố Điểm 100%, `OneWayLockInterceptor` (HTTP 403) & Xuất Excel | **🧑 Thành** | ✅ Đã có Interceptor, cần hoàn thiện Excel |
| **BE-5.6** | Phân hệ Phúc khảo nội bộ `AppealRequest` gán Trưởng Bộ Môn | **🧑 Thành** | ✅ Đã có Entities, cần hoàn thiện Controller |
| **BE-6.1** | Quản trị Dead-Letter Queue & Worker chấm bù định kỳ 5 phút | **🧑 Thành + Tốt** | ⏳ Cần triển khai Worker |
| **BE-7.1** | Bộ Test Suites xUnit & Architecture Tests (371+ tests pass) | **🧑 Tốt** | ✅ Đã có 371 tests pass |
