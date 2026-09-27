# ⚙️ BACKEND IMPLEMENTATION & TASK EXECUTION GUIDE
**Dành cho:** Nguyễn Quang Thành (Lead BE & Architect) & Nguyễn Trọng Tốt (BE, AI & QA)  
**Công nghệ:** .NET 8, Clean Architecture, PostgreSQL, EF Core, Redis, SignalR, Polly, Google Gemini API, xUnit.

---

## 🏛️ 1. CẤU TRÚC THƯ MỤC CẦN TRIỂN KHAI TRONG `backend/src/`

```text
backend/src/
├── Domain/
│   ├── Entities/                 # Question.cs, RubricCriterion.cs, PracticeSession.cs, ExamSubmission.cs, User.cs
│   ├── Enums/                    # BloomLevel.cs, SubmissionStatus.cs, UserRole.cs
│   └── Exceptions/               # DomainValidationException.cs
├── Application/
│   ├── Common/                   # Interfaces (IApplicationDbContext, IGeminiService, IStorageService)
│   ├── Behaviors/                # ValidationBehavior.cs, LoggingBehavior.cs
│   └── Features/                 # CQRS theo tính năng
│       ├── Questions/            # CreateQuestionCommand.cs, GetQuestionsQuery.cs
│       ├── Practice/             # SubmitPracticeAnswerCommand.cs
│       ├── MockExam/             # StartMockExamCommand.cs, SubmitMockExamCommand.cs
│       └── Audit/                # LockExamScoreCommand.cs, ExportFapExcelQuery.cs
├── Infrastructure/
│   ├── Persistence/              # ApplicationDbContext.cs, Migrations/, Configurations/
│   ├── BackgroundServices/       # GradingQueueWorker.cs (Polly retry 2s-4s-8s)
│   ├── Services/                 # GeminiService.cs, CloudflareR2StorageService.cs, RedisQuotaService.cs
│   └── Channels/                 # BoundedGradingQueueChannel.cs (1000 slots)
└── API/
    ├── Controllers/              # AuthController.cs, QuestionsController.cs, PracticeController.cs, AuditController.cs
    ├── Hubs/                     # PracticeHub.cs (SignalR)
    ├── Middlewares/              # GlobalExceptionMiddleware.cs (RFC 7807)
    └── Program.cs                # DI Registration, CORS, Authentication
```

---

## 📦 2. CÁC NUGET PACKAGES CẦN CÀI ĐẶT
```bash
# Vào src/Infrastructure/
dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL
dotnet add package StackExchange.Redis
dotnet add package Polly
dotnet add package EPPlus

# Vào src/Application/
dotnet add package MediatR
dotnet add package FluentValidation.AspNetCore

# Vào src/API/
dotnet add package Microsoft.AspNetCore.Authentication.JwtBearer
dotnet add package Microsoft.EntityFrameworkCore.Design
```

---

## 📋 3. CHECKLIST TRIỂN KHAI CHI TIẾT THEO TUẦN (SPRINTS)

### 🏃 TUẦN 1: MÓNG HỆ THỐNG & MF-03 (CRUD NGÂN HÀNG ĐỀ & RUBRIC 10.0)

#### 🧑 Thành (Lead BE):
- [ ] **Task BE-1.1:** Cài đặt `GlobalExceptionMiddleware.cs` trong `API/Middlewares/` định dạng lỗi theo chuẩn RFC 7807 `ProblemDetails`.
- [ ] **Task BE-1.2:** Cấu hình JWT Bearer Authentication trong `Program.cs` (Ký bằng HMAC SHA256).
- [ ] **Task BE-1.3:** Định nghĩa Entity trong `Domain/Entities/`:
  - `Question.cs` (Id, Title, SubjectCode, BloomLevel, Scope, CreatedAt).
  - `RubricCriterion.cs` (Id, QuestionId, Name, MaxScore, Description).
- [ ] **Task BE-1.4:** Cấu hình `ApplicationDbContext.cs` và chạy lệnh Migration:
  ```bash
  dotnet ef migrations add InitialCreate -p ../Infrastructure/ -s API.csproj
  dotnet ef database update
  ```

#### 🧑 Tốt (BE, AI & QA):
- [ ] **Task BE-1.5:** Viết `CreateQuestionCommand.cs` và `CreateQuestionCommandHandler.cs`.
- [ ] **Task BE-1.6:** Viết `CreateQuestionValidator.cs` dùng FluentValidation:
  - Bắt buộc: `RuleFor(x => x.Criteria.Sum(c => c.MaxScore)).Equal(10.0m).WithMessage("Tổng điểm Rubric bắt buộc = 10.0");`.
- [ ] **Task BE-1.7:** Đảm bảo `CreateQuestionCommandHandler` chạy trong ACID DB Transaction: Ghi cả Question và Rubric cùng 1 đợt, lỗi bất kỳ là Rollback toàn bộ.
- [ ] **Task BE-1.8:** Tạo Project Test `tests/OralExamination.UnitTests/`:
  - Viết xUnit test: Gửi payload tổng điểm 9.0 -> Assert ném `ValidationException` và API trả HTTP 422.

---

### 🏃 TUẦN 2: MF-01 (HÀNG ĐỢI CHỊU TẢI 4 TẦNG & CHẤM GEMINI THỜI GIAN THỰC)

#### 🧑 Thành (Lead BE):
- [ ] **Task BE-2.1:** Viết `BoundedGradingQueueChannel.cs` dùng `System.Threading.Channels`:
  - Dung lượng giới hạn: 1,000 slots. `FullMode = BoundedChannelFullMode.Wait`.
- [ ] **Task BE-2.2:** Viết `GradingQueueWorker.cs` kế thừa `BackgroundService`:
  - Liên tục rút Task từ Channel để gọi AI chấm điểm ngầm.
  - Bọc quanh bởi chính sách thử lại `Polly`: Exponential Backoff `2s -> 4s -> 8s`.
  - Quá 3 lần thử lại thất bại -> Chuyển vào bảng DB `dead_letter_queues` với status `PENDING_RETRY`.
- [ ] **Task BE-2.3:** Dựng `src/API/Hubs/PracticeHub.cs` (SignalR):
  - Khi Worker hoàn tất chấm, gọi `_hubContext.Clients.User(userId).SendAsync("ReceiveScorecard", payload)`.
- [ ] **Task BE-2.4:** Viết API `POST /api/v1/practice/submit`:
  - Ghi tức thì câu trả lời xuống DB trạng thái `PENDING` (< 100ms).
  - Trả về ngay lập tức `HTTP 202 Accepted` cho Frontend.

#### 🧑 Tốt (BE, AI & QA):
- [ ] **Task BE-2.5:** Viết `GeminiService.cs`:
  - Gọi Google Gemini 1.5 Flash API với API Key từ `appsettings.json`.
- [ ] **Task BE-2.6:** Xây dựng Prompt mẫu Chain-of-Thought (CoT):
  - Bước 1: Trích xuất các ý chính trong câu trả lời sinh viên.
  - Bước 2: So khớp với từng tiêu chí Rubric của giảng viên.
  - Bước 3: Cho điểm thành phần và nhận xét cụ thể.
- [ ] **Task BE-2.7:** Cấu hình ép khuôn JSON (Structured Output):
  - Schema bắt buộc trả về: `{ "score": 8.0, "criteriaScores": [...], "feedback": "..." }`.
- [ ] **Task BE-2.8:** Viết Integration Test kiểm tra luồng gọi Gemini giả lập và kiểm tra SignalR nhận message.

---

### 🏃 TUẦN 3: MF-02 (QUOTA GUARD, SERVER TIMER & CÂU HỎI ĐÀO SÂU A2)

#### 🧑 Thành (Lead BE):
- [ ] **Task BE-3.1:** Viết `RedisQuotaService.cs` kết nối Redis:
  - Key: `quota:{userId}:{date}:{subjectCode}`.
  - Mỗi lần gọi `POST /api/v1/mock-exam/start` -> Tăng biến đếm (INCR) và set TTL 24h.
  - Nếu count > 3 -> ném lỗi HTTP 429 Too Many Requests: *"Bạn đã vượt quá giới hạn 3 lượt thi thử/ngày cho môn này"*.
- [ ] **Task BE-3.2:** Xây dựng Server-side Master Timer:
  - Khi bắt đầu thi, lưu `StartTime` và `MaxDurationSeconds` vào DB.
  - Khi gọi `POST /api/v1/mock-exam/submit`: So sánh `DateTime.UtcNow` với hạn chót. Nếu nộp trễ quá 10 giây (bù trễ mạng) -> Tự động từ chối tính điểm câu đó.

#### 🧑 Tốt (BE, AI & QA):
- [ ] **Task BE-3.3:** Xây dựng logic AI Adaptive "Câu hỏi đào sâu A2":
  - Nếu kết quả chấm câu trước nằm trong khoảng `4.0 <= score <= 8.0`:
  - Kích hoạt prompt phụ: *"Dựa vào điểm yếu của sinh viên ở câu trả lời vừa rồi, hãy sinh 1 câu hỏi chuyên sâu đào sâu vào khái niệm đó để thử thách sinh viên"*.
  - Đóng gói câu hỏi A2 trả về cho sinh viên trả lời tiếp.
- [ ] **Task BE-3.4:** Viết kịch bản Regression Test đảm bảo code MF-02 không phá vỡ tính năng MF-01.

---

### 🏃 TUẦN 4: MF-04 (BĂM SHA-256 NIÊM PHONG, ONE-WAY LOCK & EXPORT EXCEL)

#### 🧑 Thành (Lead BE):
- [ ] **Task BE-4.1:** Viết API `POST /api/v1/audit/lock`:
  - Phân quyền: Bắt buộc `[Authorize(Roles = "Lecturer")]`.
  - Cập nhật trường `is_locked = true` trong bảng `exam_submissions`.
  - Viết Interceptor trong EF Core: Bất kỳ câu lệnh UPDATE nào vào record có `is_locked = true` đều bị ném Exception chặn đứng (One-Way Lock).
- [ ] **Task BE-4.2:** Viết API `GET /api/v1/audit/export-excel`:
  - Sử dụng thư viện `EPPlus`.
  - Xuất file `.xlsx` danh sách điểm thi format theo đúng cột điểm của hệ thống trường (MSSV, Họ tên, Mã môn, Điểm số, Giảng viên chấm, Ngày khóa).

#### 🧑 Tốt (BE, AI & QA):
- [ ] **Task BE-4.3:** Viết `CloudflareR2StorageService.cs`:
  - Tạo Presigned URL cho Frontend upload file âm thanh `.webm`.
  - Quy ước đặt tên file: `STT_MSSV.webm`.
- [ ] **Task BE-4.4:** Hàm băm mã hóa mật mã SHA-256:
  - Đọc luồng byte của file audio sau khi sinh viên nộp bài:
  ```csharp
  using var sha256 = SHA256.Create();
  byte[] hashBytes = sha256.ComputeHash(audioStream);
  string cryptographicSeal = Convert.ToHexString(hashBytes);
  ```
  - Lưu chuỗi băm này vào cột `audio_sha256_hash` để làm bằng chứng niêm phong bài thi chống sửa đổi.
- [ ] **Task BE-4.5:** Thực thi toàn bộ kiểm thử UAT, lập tài liệu `FINAL_TEST_REPORT.md` chứng minh 100% test case Critical đều PASS.
