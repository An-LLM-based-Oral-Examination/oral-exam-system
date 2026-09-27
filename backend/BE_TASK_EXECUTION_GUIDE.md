# ⚙️ BACKEND IMPLEMENTATION & TASK EXECUTION GUIDE (v2 — ĐÃ SỬA SAU PHẢN BIỆN)
**Dành cho:** Nguyễn Quang Thành (Lead BE & Architect) & Nguyễn Trọng Tốt (BE, AI & QA)  
**Công nghệ:** .NET 8, Clean Architecture, PostgreSQL, EF Core, SignalR, Polly, Google Gemini API, OpenAI Whisper (Server-side), EPPlus, xUnit.  
**Quy chuẩn bắt buộc:** Quota 3 lượt/môn/ngày kiểm soát bằng PostgreSQL. Whisper STT chạy server-side cho thi thật phòng Lab MF-04. Hàng đợi 4 tầng load test ở Tuần 3. Tốt sở hữu duy nhất việc ghi score + feedback AI xuống DB.

---

## 🏛️ 1. CẤU TRÚC THƯ MỤC CẦN TRIỂN KHAI TRONG `backend/src/`

```text
backend/src/
├── Domain/
│   ├── Entities/                 # User.cs, Role.cs, Subject.cs, Chapter.cs, Question.cs, RubricCriterion.cs,
│   │                             # PracticeSession.cs, MockExamSession.cs, ExamSession.cs, ExamSessionStudent.cs,
│   │                             # ExamSubmission.cs, SystemAuditLog.cs
│   ├── Enums/                    # BloomLevel.cs, SubmissionStatus.cs, UserRole.cs, ScopeType.cs
│   └── Exceptions/               # DomainValidationException.cs, UnauthorizedException.cs
├── Application/
│   ├── Common/                   # Interfaces (IApplicationDbContext, IGeminiService, IWhisperService,
│   │                             # IStorageService, IPostgreSqlQuotaService)
│   ├── Behaviors/                # ValidationBehavior.cs, LoggingBehavior.cs
│   └── Features/                 # CQRS theo tính năng
│       ├── Auth/                 # LoginCommand.cs, LoginCommandHandler.cs, LoginDto.cs
│       ├── Subjects/             # CreateSubjectCommand.cs, GetSubjectsQuery.cs, CreateChapterCommand.cs
│       ├── Questions/            # CreateQuestionCommand.cs, GetQuestionsQuery.cs, CalibrateQuestionCommand.cs
│       ├── Practice/             # SubmitPracticeAnswerCommand.cs
│       ├── MockExam/             # StartMockExamCommand.cs, SubmitMockExamCommand.cs
│       ├── ExamSessions/         # CreateExamSessionCommand.cs, GetExamSessionByIdQuery.cs
│       └── Audit/                # OverrideExamScoreCommand.cs, LockExamScoreCommand.cs, ExportFapExcelQuery.cs
├── Infrastructure/
│   ├── Persistence/              # ApplicationDbContext.cs, Migrations/, Configurations/,
│   │                             # Interceptors/OneWayLockInterceptor.cs
│   ├── BackgroundServices/       # GradingQueueWorker.cs (Polly retry 2s-4s-8s, DLQ)
│   ├── Services/                 # GeminiService.cs, WhisperTranscriptionService.cs, CloudflareR2StorageService.cs,
│   │                             # PostgreSqlQuotaService.cs
│   └── Channels/                 # BoundedGradingQueueChannel.cs (1,000 slots)
└── API/
    ├── Controllers/              # AuthController.cs, SubjectsController.cs, QuestionsController.cs,
    │                             # PracticeController.cs, MockExamController.cs, ExamSessionsController.cs,
    │                             # AuditController.cs
    ├── Hubs/                     # PracticeHub.cs (SignalR endpoint: /hubs/practice)
    ├── Middlewares/              # GlobalExceptionMiddleware.cs (RFC 7807 ProblemDetails)
    └── Program.cs                # DI Registration, CORS, Authentication, Authorization
```

---

## 📦 2. CÁC NUGET PACKAGES CẦN CÀI ĐẶT
*(Danh mục gói phụ thuộc chuẩn cho Clean Architecture .NET 8)*

```bash
# Vào src/Infrastructure/
dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL
dotnet add package Polly
dotnet add package EPPlus

# Vào src/Application/
dotnet add package MediatR
dotnet add package FluentValidation.AspNetCore

# Vào src/API/
dotnet add package Microsoft.AspNetCore.Authentication.JwtBearer
dotnet add package Microsoft.EntityFrameworkCore.Design

# Vào tests/OralExamination.UnitTests/
dotnet add package xunit
dotnet add package FluentAssertions
dotnet add package Moq
```

---

## 📋 3. CHECKLIST TRIỂN KHAI CHI TIẾT THEO TUẦN (SPRINTS)

### 🏃 TUẦN 1: MÓNG HỆ THỐNG & MF-03 (AUTH API, CRUD MÔN HỌC/CHƯƠNG & NGÂN HÀNG ĐỀ RUBRIC 10.0)

#### 🧑 Thành (Lead BE):
- [ ] **Task BE-1.1:** Cài đặt `GlobalExceptionMiddleware.cs` trong `API/Middlewares/` bắt toàn bộ ngoại lệ và trả về theo chuẩn RFC 7807 `ProblemDetails` (chứa `status`, `title`, `detail`, `errors`).
- [ ] **Task BE-1.2:** Cấu hình JWT Bearer Authentication trong `Program.cs`. Viết `AuthController.cs` với endpoint `POST /api/v1/auth/login`:
  - Tiếp nhận credentials (Email, Password), xác thực danh tính.
  - Sinh JWT Token ký bằng thuật toán HMAC-SHA256, đóng gói Claims: `UserId`, `Email`, `FullName`, `Role` thuộc 3 vai: `Student`, `Instructor`, `Admin`.
- [ ] **Task BE-1.3:** Định nghĩa các thực thể cốt lõi trong `Domain/Entities/`:
  - `User.cs` (Id, Email, PasswordHash, FullName, Role, CreatedAt).
  - `Subject.cs` (Id, Code, Name, Description) & `Chapter.cs` (Id, SubjectId, ChapterNumber, Title).
  - `Question.cs` (Id, SubjectId, ChapterId, Title, BloomLevel, Scope, SampleAnswer, CreatedAt).
  - `RubricCriterion.cs` (Id, QuestionId, Name, MaxScore, Description).
- [ ] **Task BE-1.4:** Cấu hình `ApplicationDbContext.cs`, tạo dữ liệu khởi tạo (Seed data: 5 Giảng viên, 50 Sinh viên, 1 Admin hệ thống) và chạy Migration:
  ```bash
  dotnet ef migrations add InitialCreate -p ../Infrastructure/ -s API.csproj
  dotnet ef database update
  ```
  Xuất bản tài liệu hợp đồng tích hợp `docs/API_CONTRACT_AND_INTEGRATION_GUIDE.md` cho toàn đội.
- **Báo cáo (Report):** Swagger UI hiển thị đầy đủ endpoint `POST /api/v1/auth/login`. Database hiển thị đủ 11 bảng cùng dữ liệu seed ban đầu. Đăng nhập 3 vai trò trả về đúng JWT Token chứa claim role tương ứng.

#### 🧑 Tốt (BE, AI & QA):
- [ ] **Task BE-1.5:** Xây dựng tính năng CRUD Môn học (Subject) và Chương (Chapter):
  - Viết Commands & Queries: `CreateSubjectCommand`, `GetSubjectsQuery`, `CreateChapterCommand`, `GetChaptersBySubjectQuery`.
  - Viết `SubjectsController.cs` cung cấp endpoints: `GET /api/v1/subjects`, `POST /api/v1/subjects`, `GET /api/v1/subjects/{id}/chapters`, `POST /api/v1/subjects/{id}/chapters` để cung cấp dữ liệu danh mục cho Frontend.
- [ ] **Task BE-1.6:** Viết các luồng CQRS cho Ngân hàng câu hỏi: `CreateQuestionCommand.cs`, `CreateQuestionCommandHandler.cs`, `GetQuestionsQuery.cs`.
- [ ] **Task BE-1.7:** Viết bộ kiểm thực FluentValidation `CreateQuestionValidator.cs`:
  - Quy tắc bất biến: `RuleFor(x => x.Criteria.Sum(c => c.MaxScore)).Equal(10.0m).WithMessage("Tổng điểm Rubric bắt buộc = 10.0");`.
  - Đảm bảo `CreateQuestionCommandHandler` chạy trong `IDbContextTransaction` (ACID DB Transaction): Ghi đồng thời Question và các RubricCriterion trong cùng một giao dịch; rollback toàn bộ nếu có bất kỳ lỗi nào.
- [ ] **Task BE-1.8:** Khởi tạo dự án kiểm thử `tests/OralExamination.UnitTests/`:
  - Viết xUnit tests: Gửi payload câu hỏi có tổng rubric 9.0m hoặc 10.5m -> Assert ném `ValidationException` và API trả về HTTP 422 Unprocessable Entity kèm chi tiết lỗi.
- **Báo cáo (Report):** Test Runner hiển thị 100% Unit Tests Passed. Thao tác trên Swagger UI tạo thành công Môn học, Chương và Câu hỏi có Rubric chuẩn 10.0; xác nhận cơ chế rollback hoạt động khi payload sai lệch.

---

### 🏃 TUẦN 2: MF-01 (HÀNG ĐỢI CHỊU TẢI 4 TẦNG, SIGNALR & CHẤM GEMINI THỜI GIAN THỰC)

#### 🧑 Thành (Lead BE):
- [ ] **Task BE-2.1:** Viết hàng đợi trong bộ nhớ `BoundedGradingQueueChannel.cs` dùng `System.Threading.Channels`:
  - Thiết lập dung lượng cố định 1,000 slots với chế độ `FullMode = BoundedChannelFullMode.Wait` nhằm làm phẳng lưu lượng (Traffic Smoothing).
- [ ] **Task BE-2.2:** Viết dịch vụ chạy ngầm `GradingQueueWorker.cs` kế thừa `BackgroundService`:
  - Liên tục rút Task từ Channel và điều phối gọi `GeminiService` chấm điểm ngầm.
  - Bọc quanh bởi chính sách thử lại lũy thừa `Polly RetryPolicy`: Exponential Backoff `2s -> 4s -> 8s`.
  - Nếu thất bại quá 3 lần -> Chuyển vào bảng DB `dead_letter_queues` (DLQ) với trạng thái `PENDING_RETRY` (bảo đảm nguyên tắc Zero Data Loss).
- [ ] **Task BE-2.3:** Dựng SignalR Hub `PracticeHub.cs` tại endpoint `/hubs/practice`:
  - Sau khi hoàn tất chấm điểm, gọi `_hubContext.Clients.User(userId).SendAsync("ReceiveScorecard", scorecardPayload)` bắn kết quả về đúng client của sinh viên.
- [ ] **Task BE-2.4:** Viết API tiếp nhận bài nộp `POST /api/v1/practice/submit`:
  - Ghi tức thì câu trả lời xuống database ở trạng thái `PENDING` (Tầng Persist-First < 100ms).
  - Đẩy task vào hàng đợi và phản hồi ngay lập tức `HTTP 202 Accepted` cho Frontend.
- **Báo cáo (Report):** Gửi đồng loạt 100 request giả lập vào `/api/v1/practice/submit`: Toàn bộ phản hồi 202 Accepted trong < 80ms. Worker rút task xử lý ổn định, SignalR phát tin thành công tới client.

#### 🧑 Tốt (BE, AI & QA):
- [ ] **Task BE-2.5:** Viết `GeminiService.cs`:
  - Kết nối Google Gemini 1.5 Flash API với API Key bảo mật từ cấu hình hệ thống.
- [ ] **Task BE-2.6:** Thiết kế Prompt hệ thống theo kỹ thuật Chain-of-Thought (CoT) 3 bước:
  - Bước 1: Trích xuất các ý chính (Fact-anchoring) trong câu trả lời sinh viên.
  - Bước 2: So khớp chi tiết từng ý với tiêu chí Rubric của giảng viên.
  - Bước 3: Phân bổ điểm thành phần và nhận xét sư phạm định tính.
- [ ] **Task BE-2.7:** Cấu hình ép khuôn JSON (Structured Output):
  - Áp dụng `response_mime_type="application/json"` với Schema bắt buộc: `{ "totalScore": number, "criteriaScores": [{ "criterionId": string, "score": number, "comment": string }], "feedback": string }`.
- [ ] **Task BE-2.8:** **Sở hữu duy nhất việc ghi score + feedback AI xuống DB:**
  - Viết phương thức cập nhật điểm số chính thức, mảng tiêu chí và phản hồi của Gemini vào bảng `practice_answers` (cập nhật trạng thái sang `GRADED`) trong cùng luồng trước khi thông báo SignalR.
  - Bổ sung endpoint `POST /api/v1/questions/{id}/calibrate` (AI Calibration) để phục vụ nút "Chấm thử bằng AI" trên Frontend (chấm câu trả lời mẫu của Giảng viên theo Rubric).
  - *(Lưu ý: Tuần 2 tập trung ổn định luồng xử lý AI và hàng đợi, không thực hiện load test).*
- **Báo cáo (Report):** Log hệ thống in ra chuỗi JSON thuần khiết của AI, không bị bọc Markdown (` ```json `). DB cập nhật đầy đủ điểm và nhận xét. Endpoint calibrate trả về kết quả chấm thử rubric chính xác.

---

### 🏃 TUẦN 3: MF-02 (QUOTA GUARD POSTGRESQL, SERVER TIMER, LOAD TEST & API TẠO CA THI LAB)

#### 🧑 Thành (Lead BE):
- [ ] **Task BE-3.1:** Viết dịch vụ kiểm soát hạn mức `PostgreSqlQuotaService.cs` bằng **PostgreSQL**:
  - Truy vấn đếm số lượt thi thử trong ngày trực tiếp từ database:
    ```sql
    SELECT COUNT(*) FROM mock_exam_sessions 
    WHERE user_id = @userId AND subject_id = @subjectId 
      AND DATE(created_at AT TIME ZONE 'UTC') = CURRENT_DATE;
    ```
  - Nếu kết quả count >= 3 lượt/môn/ngày -> Ném ngoại lệ trả về HTTP 429 Too Many Requests kèm RFC 7807 ProblemDetails: *"Bạn đã vượt quá giới hạn 3 lượt thi thử/ngày cho môn học này"*.
- [ ] **Task BE-3.2:** Xây dựng đồng hồ giám định máy chủ (Server-side Master Timer):
  - Khi bắt đầu thi (`POST /api/v1/mock-exam/start`), lưu `StartTime` và `MaxDurationSeconds` vào DB.
  - Khi gọi `POST /api/v1/mock-exam/submit`: So sánh `DateTime.UtcNow` với hạn chót cho phép. Nếu nộp trễ quá 10 giây (biên độ bù trễ mạng) -> Tự động từ chối tính điểm câu đó.
- [ ] **Task BE-3.3:** **Xây dựng API tạo ca thi phòng Lab (`POST /api/v1/exam-sessions`):**
  - Thiết kế thực thể `ExamSession.cs` và `ExamSessionStudent.cs` (liên kết SessionId, StudentId, PcNumber).
  - Viết `CreateExamSessionCommand.cs` và `ExamSessionsController.cs`: Nhận thông tin ca thi (Mã phiên, Mã phòng Lab, Môn học, Thời gian bắt đầu/kết thúc, Danh sách sinh viên và Số máy gán tương ứng).
  - Viết API `GET /api/v1/exam-sessions/{id}` phục vụ trạm thi Kiosk xác thực ở Tuần 4.
- **Báo cáo (Report):** Thử nghiệm gọi thi lần thứ 4 trong ngày bị chặn HTTP 429. Nộp bài trễ 15 giây bị từ chối lưu điểm. Swagger hiện endpoint `POST /api/v1/exam-sessions` và tạo ca thi gán 40 máy thành công.

#### 🧑 Tốt (BE, AI & QA):
- [ ] **Task BE-3.4:** Xây dựng logic AI Adaptive "Câu hỏi đào sâu A2":
  - Nếu kết quả chấm câu hỏi chính nằm trong khoảng trung bình `4.0 <= totalScore <= 8.0`: Kích hoạt prompt chuyên sâu yêu cầu Gemini phân tích kẽ hở lập luận và sinh 1 câu hỏi đào sâu A2 tương ứng để thử thách sinh viên.
- [ ] **Task BE-3.5:** **Thực thi Load Test hàng đợi chịu tải 4 tầng:**
  - Sau khi hệ thống hàng đợi Tuần 2 đã ổn định, thực hiện kịch bản kiểm thử tải trọng (Load Testing) bằng k6 / NBomber giả lập 500 requests nộp bài đồng thời vào hàng đợi.
  - Đánh giá khả năng hấp thụ lưu lượng của Bounded Channel, độ trễ và tài nguyên hệ thống.
- [ ] **Task BE-3.6:** Viết bộ kịch bản kiểm thử hồi quy (Regression Test Suite): Đảm bảo các tính năng của MF-02 chạy độc lập và không làm phát sinh lỗi trên luồng MF-01.
- **Báo cáo (Report):** Demo câu trả lời 6.5 điểm kích hoạt câu hỏi xoáy A2 chính xác. Báo cáo Load Test: 500 requests nộp bài đạt 0% drop, phản hồi 202 trong < 90ms. 100% Regression Tests Passed.

---

### 🏃 TUẦN 4: MF-04 (WHISPER STT SERVER-SIDE, NIÊM PHONG SHA-256, API SỬA ĐIỂM, ONE-WAY LOCK & EXPORT EXCEL)

#### 🧑 Thành (Lead BE):
- [ ] **Task BE-4.1:** Viết API Khóa điểm một chiều `POST /api/v1/audit/lock`:
  - Phân quyền nghiêm ngặt: Bắt buộc `[Authorize(Roles = "Instructor,Admin")]`.
  - Cập nhật trường `is_locked = true` và `locked_at = DateTime.UtcNow` trong bảng `exam_submissions`.
  - Cài đặt `OneWayLockInterceptor` trong EF Core: Mọi câu lệnh `UPDATE` hay `DELETE` tác động lên row có `is_locked == true` đều bị ném `DomainValidationException` (HTTP 403 Forbidden) chặn đứng ở mức hạ tầng.
- [ ] **Task BE-4.2:** Viết API kết xuất bảng điểm khảo thí `GET /api/v1/audit/export-excel`:
  - Sử dụng thư viện `EPPlus`.
  - Xuất file `.xlsx` danh sách điểm thi format theo đúng mẫu biểu của phòng Khảo thí nhà trường (FAP): STT, MSSV, Họ tên, Phòng Lab, Số máy, Điểm AI đề xuất, Điểm chính thức Giảng viên, Lý do điều chỉnh, Giảng viên thẩm định, Thời gian khóa.
- **Báo cáo (Report):** Dùng Postman cố tình gửi lệnh sửa điểm lên bài thi đã khóa -> nhận HTTP 403 Forbidden. File Excel tải về mở trên Microsoft Excel hiển thị định dạng chuẩn xác, đầy đủ cột điểm.

#### 🧑 Tốt (BE, AI & QA):
- [ ] **Task BE-4.3:** Tích hợp dịch vụ Whisper STT Server-side cho MF-04 (`WhisperTranscriptionService.cs`):
  - Xử lý bóc băng âm thanh phía máy chủ (Server-side transcription) cho toàn bộ file audio thi thật phòng Lab của sinh viên.
  - Trích xuất văn bản gỡ băng kèm mốc thời gian chi tiết (word-level timestamps) phục vụ tính năng đồng bộ âm thanh trên Cổng hậu kiểm.
  - *(Lưu ý quan trọng: Web Speech API chỉ dùng phía client cho MF-01/MF-02; MF-04 thi thật bắt buộc dùng Whisper server-side để đảm bảo tính pháp lý và độ chính xác).*
- [ ] **Task BE-4.4:** Tích hợp Cloudflare R2 Storage & Cơ chế niêm phong mật mã SHA-256:
  - Tạo Presigned URL cho Frontend upload file âm thanh WebM theo quy chuẩn tên `STT_MSSV.webm`.
  - Tính toán mã băm mật mã SHA-256 trên luồng byte của file audio sau khi sinh viên nộp bài:
    ```csharp
    using var sha256 = SHA256.Create();
    byte[] hashBytes = sha256.ComputeHash(audioStream);
    string cryptographicSeal = Convert.ToHexString(hashBytes);
    ```
  - Lưu chuỗi mã băm vào trường `audio_sha256_hash` của bài thi để niêm phong bằng chứng chống chối bỏ.
- [ ] **Task BE-4.5:** **Viết API sửa điểm thẩm định của Giảng viên (`PUT /api/v1/audit/override`):**
  - Cho phép Giảng viên điều chỉnh điểm số các tiêu chí Rubric TRƯỚC KHI bài thi bị khóa.
  - Yêu cầu bắt buộc phải có chuỗi giải trình `overrideReason` (validate không được rỗng).
  - Kiểm tra `is_locked == false`; cập nhật điểm số mới và tự động ghi vết nhật ký vào bảng `system_audit_logs` (Lưu ID Giảng viên, Điểm cũ, Điểm mới, Lý do giải trình, Thời gian).
- [ ] **Task BE-4.6:** Thực thi toàn bộ kiểm thử UAT, lập tài liệu `FINAL_TEST_REPORT.md` chứng minh 100% ca kiểm thử Critical đều PASS trước khi nghiệm thu.
- **Báo cáo (Report):** Whisper STT bóc băng chính xác kèm timestamp. Sửa 1 byte file audio trên R2 lập tức bị hàm băm SHA-256 phát hiện. API `PUT /api/v1/audit/override` ghi đè điểm trơn tru kèm nhật ký audit đầy đủ. Báo cáo UAT nghiệm thu hoàn tất.

---
*(Tài liệu hướng dẫn triển khai Backend được cập nhật hoàn chỉnh, đồng bộ 100% với 12 điểm phản biện kỹ thuật và chuẩn hóa .NET 8 Clean Architecture).*
