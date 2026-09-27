# ⚙️ BACKEND TASK & EXECUTION GUIDE (MASTER CONSOLIDATED v3.0)
## ĐỒ ÁN TỐT NGHIỆP SEP490 (FA26SE166) — HỆ THỐNG VẤN ĐÁP AI (ORAL EXAM SYSTEM)
**Dành cho:** 🧑 Trương Minh Thành (Lead BE & Architect) & 🧑 Nguyễn Đức Tốt (BE Developer, AI & QA Lead)  
**Công nghệ:** .NET 8, C# 12, Clean Architecture, MediatR CQRS, EF Core, PostgreSQL 16+, System.Threading.Channels, SignalR, Google Gemini 1.5 Flash, Whisper STT, Cloudflare R2, EPPlus.  
**Quy chuẩn cốt lõi:** Bounded Channel = 1,000 slots | Quota PostgreSQL 3 lượt/ngày/môn | Chấm ngầm < 100ms trả 202 Accepted | Whisper STT Server-side | Khóa điểm 1 chiều (`is_locked=true`).

---

## 📁 1. CẤU TRÚC SOLUTION & MA TRẬN PHÂN BỔ TỆP (`backend/`)

Solution .NET 8 Clean Architecture tuân thủ nghiêm ngặt nguyên lý Dependency Inversion (Domain là trung tâm, độc lập 100% hạ tầng):

```text
backend/
├── OralExamination.sln
├── src/
│   ├── Domain/                      # Không phụ thuộc thư viện ngoài (Pure C#)
│   │   ├── Common/                  # BaseEntity.cs, IAggregateRoot.cs
│   │   ├── Entities/                # User.cs, Subject.cs, Chapter.cs, Question.cs, RubricCriterion.cs,
│   │   │                            # PracticeAnswer.cs, MockExamSession.cs, ExamSession.cs, CandidateSubmission.cs
│   │   ├── Enums/                   # UserRole.cs, BloomLevel.cs, ScopeType.cs, SubmissionStatus.cs
│   │   └── Exceptions/              # DomainValidationException.cs, EntityNotFoundException.cs
│   ├── Application/                 # MediatR CQRS, DTOs, Pipeline Behaviors & Interfaces
│   │   ├── Common/                  # Interfaces: IApplicationDbContext.cs, IGeminiService.cs, IWhisperService.cs, IR2Service.cs
│   │   ├── Behaviors/               # ValidationBehavior.cs, LoggingBehavior.cs
│   │   ├── Features/
│   │   │   ├── Auth/                # LoginCommand, RefreshTokenCommand
│   │   │   ├── Questions/           # CreateQuestionCommand + CreateQuestionValidator (10.0đ), GetQuestionsQuery
│   │   │   ├── Subjects/            # CreateSubjectCommand, GetSubjectsQuery, GetChaptersQuery
│   │   │   ├── Practice/            # SubmitPracticeAnswerCommand, CalibrateQuestionCommand
│   │   │   ├── MockExams/           # StartMockExamCommand (Quota Guard), SubmitMockExamCommand
│   │   │   └── ExamSessions/        # CreateExamSessionCommand, OverrideScoreCommand, LockExamScoreCommand, ExportExcelQuery
│   │   └── DTOs/                    # Các DTOs khớp 1-1 với frontend/src/types/
│   ├── Infrastructure/              # EF Core PostgreSQL, SignalR Hubs, External APIs, Channels
│   │   ├── Persistence/             # ApplicationDbContext.cs, Configurations/, Migrations/, SeedData/
│   │   ├── Interceptors/            # OneWayLockInterceptor.cs (Chặn UPDATE/DELETE row is_locked=true)
│   │   ├── Channels/                # BoundedGradingQueueChannel.cs (1,000 slots)
│   │   ├── BackgroundWorkers/       # GradingQueueWorker.cs (BackgroundService + Polly Retry)
│   │   ├── Hubs/                    # PracticeHub.cs (/hubs/practice)
│   │   └── Services/                # GeminiService.cs (CoT Prompt), WhisperTranscriptionService.cs, R2StorageService.cs, PostgreSqlQuotaService.cs
│   └── API/                         # ASP.NET Core Web API Host
│       ├── Controllers/             # AuthController, SubjectsController, QuestionsController, PracticeController, MockExamController, AuditController
│       ├── Middlewares/             # GlobalExceptionMiddleware.cs (RFC 7807 ProblemDetails)
│       └── Program.cs               # DI Container, CORS, Authentication, Pipeline
└── tests/
    ├── OralExamination.UnitTests/   # xUnit, FluentAssertions, Moq (Unit test handlers & validators)
    └── FA26SE166.ArchitectureTests/ # NetArchTest (Kiểm thử ranh giới Clean Architecture)
```

### Bảng Phân Quyền Sở Hữu File (File Ownership Matrix):
| Phân hệ / Thành phần | Tệp mã nguồn thực tế | Người sở hữu | Trách nhiệm kỹ thuật |
|:---|:---|:---:|:---|
| **Solution & Architecture Base** | `src/Domain/Common/*`, `src/Application/Common/*`, `src/API/Program.cs` | Thành | Dựng khung Clean Architecture 4 tầng, DI Container, CORS chuẩn xác trước StaticFiles. |
| **Global Exception RFC 7807** | `src/API/Middlewares/GlobalExceptionMiddleware.cs` | Thành | Bắt toàn bộ lỗi (400, 401, 403, 422, 429, 500), đóng gói chuẩn RFC 7807 `ProblemDetails`. |
| **Auth & JWT Bearer** | `src/Application/Features/Auth/*`, `src/API/Controllers/AuthController.cs` | Thành | Cấp JWT Token HMAC-SHA256, đóng gói Claims `UserId`, `Role` (Student, Instructor, Admin). |
| **EF Core DB & Migrations** | `src/Infrastructure/Persistence/*`, `src/Domain/Entities/*` | Thành | Quản lý 12 bảng PostgreSQL 16+, Seed Data khởi tạo (Giảng viên, Sinh viên, Môn học). |
| **MF-03: Danh Mục Môn/Chương** | `src/Application/Features/Subjects/*`, `src/API/Controllers/SubjectsController.cs` | Tốt | Endpoints `GET/POST /api/v1/subjects`, `GET/POST /api/v1/subjects/{id}/chapters`. |
| **MF-03: Ngân Hàng Đề & Rubric** | `src/Application/Features/Questions/*`, `src/API/Controllers/QuestionsController.cs` | Tốt | `CreateQuestionCommand` + FluentValidation: **Bắt buộc $\sum \text{Rubric} = 10.0$đ**, ACID Transaction. |
| **MF-01: Hàng Đợi & Worker** | `src/Infrastructure/Channels/*`, `src/Infrastructure/BackgroundWorkers/*` | Thành | Bounded Channel 1,000 slots, Worker rút task + Polly Exponential Retry (2s-4s-8s), DLQ fallback. |
| **MF-01: SignalR & Tiếp Nhận 202** | `src/Infrastructure/Hubs/PracticeHub.cs`, `src/API/Controllers/PracticeController.cs` | Thành | `POST /practice/submit` ghi DB `PENDING` trả 202 trong < 100ms; SignalR bắn `ReceiveScorecard`. |
| **MF-01: Gemini AI Engine** | `src/Infrastructure/Services/GeminiService.cs`, AI Calibration API | Tốt | Prompt CoT 3 bước, ép khuôn JSON Schema `response_mime_type="application/json"`, AI Calibrate. |
| **MF-02: Quota Guard PostgreSQL** | `src/Infrastructure/Services/PostgreSqlQuotaService.cs` | Thành | Đếm trực tiếp từ PostgreSQL: Nếu $\ge 3$ lượt/môn/ngày $\rightarrow$ ném ngay HTTP 429 Too Many Requests. |
| **MF-02: Server Master Timer** | `src/Application/Features/MockExams/*` | Thành | Đo lường thời gian thi phía máy chủ, từ chối nộp bài nếu trễ $> 10$s so với hạn chót. |
| **MF-02: AI Adaptive A2** | Logic kích hoạt Adaptive Question trong `GeminiService.cs` | Tốt | Phân tích nếu điểm $4.0 \le \text{Score} \le 8.0 \rightarrow$ tự động sinh 1 câu hỏi xoáy A2 đào sâu. |
| **MF-04: API Ca Thi Phòng Lab** | `src/Application/Features/ExamSessions/*` | Thành | `POST /api/v1/exam-sessions` gán 40 máy trạm (`pcNumber`), tra cứu phiên thi Kiosk. |
| **MF-04: Whisper STT Server-side** | `src/Infrastructure/Services/WhisperTranscriptionService.cs` | Tốt | Bóc băng file audio thi thật phòng Lab của sinh viên kèm timeline timestamps (Word-level). |
| **MF-04: R2 & Niêm Phong SHA-256** | `src/Infrastructure/Services/R2StorageService.cs` | Tốt | Presigned URL upload `STT_MSSV.webm`, tính toán mã băm SHA-256 niêm phong pháp lý chống tráo. |
| **MF-04: Sửa Điểm & Nhật Ký** | `OverrideScoreCommandHandler.cs` | Tốt | `PUT /api/v1/audit/override` bắt buộc có `overrideReason`, ghi nhật ký `system_audit_logs`. |
| **MF-04: One-Way Lock & Excel** | `OneWayLockInterceptor.cs`, Export Excel FAP bằng EPPlus | Thành | `POST /api/v1/audit/lock` khóa cứng (`is_locked=true`), chặn UPDATE ở tầng DB, xuất file `.xlsx`. |
| **Kiểm Thử & Load Testing** | `tests/OralExamination.UnitTests/`, NBomber / k6 scripts | Tốt | Unit tests xUnit, NetArchTest kiến trúc Clean, Load Test 500 requests đồng thời vào Channel. |

---

## 🔌 2. HỢP ĐỒNG GIAO TIẾP VỚI FRONTEND (API CONTRACT SYNC)

- **Base URL:** `/api/v1/`
- **Chuẩn Dữ Liệu:** JSON `camelCase` 100% (cấu hình `JsonNamingPolicy.CamelCase`).
- **Xác Thực JWT:** `Authorization: Bearer <accessToken>`, Token chứa `sub`, `email`, `role`, `name`.
- **Mã Trạng Thái Nghiệp Vụ Bắt Buộc:**
  - `200 OK`: Truy vấn / cập nhật thành công.
  - `201 Created`: Tạo mới thành công (Môn học, Câu hỏi, Ca thi).
  - `202 Accepted`: Dành riêng cho `POST /api/v1/practice/submit` (< 100ms).
  - `400 Bad Request`: Sai cấu trúc JSON hoặc thiếu trường bắt buộc.
  - `401 Unauthorized`: Chưa đăng nhập hoặc Token hết hạn.
  - `403 Forbidden`: Truy cập trái quyền hoặc bài thi đã bị Khóa một chiều (`is_locked = true`).
  - `422 Unprocessable Entity`: Vi phạm luật nghiệp vụ (Barem Rubric != 10.0, hoặc sửa điểm thiếu lý do giải trình).
  - `429 Too Many Requests`: Vượt hạn ngạch thi thử 3 lượt/môn/ngày.
- **WebSocket SignalR Hub:** `/hubs/practice` phát sự kiện `ReceiveScorecard(ScorecardPayload)`.

---

## 📅 3. LỊCH BÁO CÁO 2 NGÀY/LẦN & CHECKLIST CHI TIẾT 4 TUẦN (BI-DAILY AGILITY)

Mỗi tuần cố định 3 mốc báo cáo kỹ thuật (đồng bộ 100% với lịch của Frontend):
- **Thứ 2:** Họp giao việc đầu tuần (Kick-off Sprint).
- **Thứ 4 (Ngày 2):** Báo cáo Sync 1 — Trình diễn tính năng 1 (API & Database).
- **Thứ 6 (Ngày 4):** Báo cáo Sync 2 — Trình diễn tính năng 2 (Nghiệp vụ sâu & Validation).
- **Chủ Nhật (Ngày 6):** Gate Review tuần — Ghép nối API với Frontend, chạy Unit Tests & kiểm thử tích hợp.

---

### 🏃 TUẦN 1: MÓNG HỆ THỐNG, AUTH API & MF-03 (CRUD MÔN HỌC & NGÂN HÀNG ĐỀ RUBRIC 10.0)

#### 📍 Buổi Báo Cáo 1 — Thứ 4 (Ngày 2 của Tuần 1)
* **🧑 Thành (Lead BE & Architect):**
  - **Task BE-1.1 & BE-1.2:** Cài đặt `GlobalExceptionMiddleware.cs` chuẩn RFC 7807 và cấu hình JWT Bearer trong `Program.cs`. Viết `AuthController.cs` (`POST /api/v1/auth/login`).
  - **Nội dung demo:** Swagger hiển thị `/api/v1/auth/login`. Đăng nhập trả về JWT token HMAC-SHA256 chứa đầy đủ Claims. Test gửi token sai $\rightarrow$ trả về 401 ProblemDetails.
* **🧑 Tốt (BE Developer & QA):**
  - **Task BE-1.5:** Xây dựng CRUD Môn học và Chương (`SubjectsController.cs`).
  - **Nội dung demo:** Gọi `GET /api/v1/subjects` trả về danh sách môn (SWP391, PRN231), gọi `GET /api/v1/subjects/{id}/chapters` trả về danh sách chương tương ứng.
* **Tiêu chí nghiệm thu (DoD):** Swagger UI hoạt động, gọi được Login và lấy danh sách môn học; trả về đúng JSON `camelCase`.

#### 📍 Buổi Báo Cáo 2 — Thứ 6 (Ngày 4 của Tuần 1)
* **🧑 Thành (Lead BE & Architect):**
  - **Task BE-1.3 & BE-1.4:** Hoàn thiện Entities Domain và cấu hình `ApplicationDbContext.cs` với PostgreSQL. Chạy Migration tạo 12 bảng và Seed Data (5 Giảng viên, 50 Sinh viên).
  - **Nội dung demo:** Mở DBeaver / pgAdmin hiển thị cấu trúc database PostgreSQL 16+ với đầy đủ khóa ngoại, chỉ mục (Indexes) và bảng dữ liệu mẫu.
* **🧑 Tốt (BE Developer & QA):**
  - **Task BE-1.6 & BE-1.7:** Hoàn thiện `CreateQuestionCommand` kèm `CreateQuestionValidator` (FluentValidation) trong ACID Transaction.
  - **Nội dung demo:** Gửi payload tạo câu hỏi có tổng rubric 9.5đ $\rightarrow$ API ném ngay **`HTTP 422 Unprocessable Entity`** với thông báo rõ ràng; gửi đúng 10.0đ $\rightarrow$ Ghi đồng thời Question và các Criteria vào DB, trả về `HTTP 201 Created`.
* **Tiêu chí nghiệm thu (DoD):** Quy tắc bất biến $\sum \text{Rubric} = 10.0$đ hoạt động 100% trên Backend; cơ chế Rollback hoạt động trơn tru khi có lỗi.

#### 📍 Buổi Báo Cáo 3 — Chủ Nhật (Ngày 6 của Tuần 1 — Gate 1 Review)
* **Cả 2 thành viên:**
  - **Task BE-1.8:** Chạy bộ Unit Tests xUnit cho Auth, Subject và Question Validation: Đạt 100% PASS.
  - Phối hợp với Frontend (Hoàng & Hải) ghép nối API thực tế: Login, xem danh sách môn và tạo câu hỏi từ giao diện Web.
  - Merge nhánh `feature/be-core-infrastructure` vào `develop`.

---

### 🏃 TUẦN 2: MF-01 (HÀNG ĐỢI CHỊU TẢI 4 TẦNG, SIGNALR & CHẤM GEMINI THỜI GIAN THỰC)

#### 📍 Buổi Báo Cáo 1 — Thứ 4 (Ngày 2 của Tuần 2)
* **🧑 Thành (Lead BE & Architect):**
  - **Task BE-2.1 & BE-2.4:** Xây dựng hàng đợi `BoundedGradingQueueChannel.cs` (1,000 slots) và API tiếp nhận bài nộp `POST /api/v1/practice/submit`.
  - **Nội dung demo:** Gửi bài thi thực hành: Backend ghi bản ghi vào DB trạng thái `PENDING` và phản hồi ngay lập tức **`HTTP 202 Accepted` trong $< 100$ms**, không để client phải chờ AI chấm.
* **🧑 Tốt (BE Developer & QA):**
  - **Task BE-2.5 & BE-2.6:** Tích hợp `GeminiService.cs` kết nối Gemini 1.5 Flash API với Prompt Chain-of-Thought (CoT) 3 bước.
  - **Nội dung demo:** Chạy console test: Đưa transcript câu trả lời sinh viên vào Gemini $\rightarrow$ Log hiển thị quá trình suy luận: (1) Fact-anchoring $\rightarrow$ (2) So khớp Rubric $\rightarrow$ (3) Chấm điểm định lượng.
* **Tiêu chí nghiệm thu (DoD):** Phản hồi tiếp nhận 202 nhanh $< 100$ms; Gemini sinh điểm số và nhận xét bám sát barem tiêu chí.

#### 📍 Buổi Báo Cáo 2 — Thứ 6 (Ngày 4 của Tuần 2)
* **🧑 Thành (Lead BE & Architect):**
  - **Task BE-2.2 & BE-2.3:** Hoàn thiện `GradingQueueWorker.cs` chạy ngầm kế thừa `BackgroundService` (kèm Polly Exponential Retry: 2s-4s-8s) và dựng SignalR Hub `/hubs/practice`.
  - **Nội dung demo:** Worker rút task ra khỏi channel, gọi AI chấm, sau khi có điểm $\rightarrow$ Gọi SignalR Hub bắn sự kiện `ReceiveScorecard` về đúng connection của sinh viên theo thời gian thực.
* **🧑 Tốt (BE Developer & QA):**
  - **Task BE-2.7 & BE-2.8:** Ép khuôn JSON đầu ra của Gemini (`response_mime_type="application/json"`) và viết API chấm thử đề `POST /api/v1/questions/{id}/calibrate`.
  - **Nội dung demo:** Đầu ra của AI là JSON thuần khiết 100% không dính markdown code blocks. API Calibrate trả kết quả chấm thử câu trả lời mẫu cho giảng viên.
* **Tiêu chí nghiệm thu (DoD):** Luồng xử lý ngầm hoạt động ổn định; SignalR phát điểm thành công về client; API Calibrate trả kết quả chính xác.

#### 📍 Buổi Báo Cáo 3 — Chủ Nhật (Ngày 6 của Tuần 2 — Gate 2 Review)
* **Cả 2 thành viên:**
  - Demo thông suốt chu trình MF-01 hoàn chỉnh: FE gửi bài $\rightarrow$ BE phản hồi 202 $\rightarrow$ Worker chấm ngầm $\rightarrow$ Bắn điểm qua SignalR $\rightarrow$ FE bung pop-up Scorecard.
  - Chụp ảnh log hệ thống và kết quả DB chứng minh không xảy ra thất thoát dữ liệu (Zero Data Loss).

---

### 🏃 TUẦN 3: MF-02 (QUOTA GUARD POSTGRESQL, SERVER MASTER TIMER, ADAPTIVE A2 & LOAD TEST)

#### 📍 Buổi Báo Cáo 1 — Thứ 4 (Ngày 2 của Tuần 3)
* **🧑 Thành (Lead BE & Architect):**
  - **Task BE-3.1:** Viết dịch vụ `PostgreSqlQuotaService.cs` kiểm soát hạn ngạch thi thử bằng câu truy vấn PostgreSQL trực tiếp.
  - **Nội dung demo:** Gọi API `POST /api/v1/mock-exam/start` lần 1, 2, 3 thành công. Đến lần thứ 4 trong cùng 1 ngày của môn học $\rightarrow$ Trả ngay **`HTTP 429 Too Many Requests`** kèm chi tiết lỗi RFC 7807.
* **🧑 Tốt (BE Developer & QA):**
  - **Task BE-3.4:** Cài đặt logic AI Adaptive "Câu hỏi đào sâu A2" trong `GeminiService.cs`.
  - **Nội dung demo:** Đưa câu trả lời có điểm nằm trong khoảng trung bình ($4.0 \le \text{Score} \le 8.0$) $\rightarrow$ Gemini tự động sinh thêm 1 câu hỏi đào sâu (`deepDiveQuestion`) để thử thách sinh viên.
* **Tiêu chí nghiệm thu (DoD):** Chặn đúng lần thi thứ 4 trong ngày bằng PostgreSQL; Adaptive A2 sinh câu hỏi phù hợp với lỗ hổng của câu trả lời trước.

#### 📍 Buổi Báo Cáo 2 — Thứ 6 (Ngày 4 của Tuần 3)
* **🧑 Thành (Lead BE & Architect):**
  - **Task BE-3.2 & BE-3.3:** Xây dựng đồng hồ giám định máy chủ (Server-side Master Timer) và API tạo ca thi phòng Lab (`POST /api/v1/exam-sessions`).
  - **Nội dung demo:** Nộp bài thi thử quá hạn chót $> 10$s $\rightarrow$ Server từ chối tính điểm câu đó. Gọi API tạo ca thi phòng Lab: Gán thành công 40 sinh viên vào 40 máy trạm phòng máy (`pcNumber` từ 1 đến 40).
* **🧑 Tốt (BE Developer & QA):**
  - **Task BE-3.5 & BE-3.6:** Thực thi kịch bản **Load Testing** hàng đợi bằng k6 / NBomber (500 requests đồng thời) và viết bộ hồi quy Regression Test Suite.
  - **Nội dung demo:** Trình diễn báo cáo Load Test: 500 requests nộp bài đồng thời đạt 100% thành công, 0% drop request, thời gian phản hồi tiếp nhận 202 duy trì $< 90$ms.
* **Tiêu chí nghiệm thu (DoD):** Server Master Timer bảo đảm tính công bằng; Hàng đợi Bounded Channel chịu tải 500 CCU hoàn hảo; 100% Regression Tests Passed.

#### 📍 Buổi Báo Cáo 3 — Chủ Nhật (Ngày 6 của Tuần 3 — Gate 3 Review)
* **Cả 2 thành viên:**
  - Ghép nối MF-02 với Frontend: Thi thử 5 câu hỏi có bấm giờ, chặn quota 3 lần/ngày, nộp bài nhận phân tích điểm Bloom Radar 6 cấp độ.
  - Kiểm tra đảm bảo các tính năng của MF-02 không ảnh hưởng đến tính ổn định của luồng MF-01.

---

### 🏃 TUẦN 4: MF-04 (WHISPER STT SERVER-SIDE, NIÊM PHONG SHA-256, SỬA ĐIỂM, ONE-WAY LOCK & XUẤT EXCEL)

#### 📍 Buổi Báo Cáo 1 — Thứ 4 (Ngày 2 của Tuần 4)
* **🧑 Thành (Lead BE & Architect):**
  - **Task BE-4.1:** Cài đặt `OneWayLockInterceptor.cs` trong EF Core và viết API Khóa điểm một chiều `POST /api/v1/audit/lock`.
  - **Nội dung demo:** Bấm khóa điểm ca thi $\rightarrow$ Cập nhật `is_locked = true`. Dùng Postman cố tình gửi câu lệnh `UPDATE` hoặc `DELETE` sửa điểm bài thi này $\rightarrow$ Interceptor ném ngay **`HTTP 403 Forbidden`** chặn đứng từ tầng hạ tầng EF Core.
* **🧑 Tốt (BE Developer & QA):**
  - **Task BE-4.3:** Tích hợp dịch vụ Whisper STT Server-side (`WhisperTranscriptionService.cs`).
  - **Nội dung demo:** Đưa 1 file audio thi thật phòng Lab vào server $\rightarrow$ Whisper bóc băng ra văn bản tiếng Việt chính xác kèm mốc thời gian chi tiết từng phân đoạn (Word-level timestamps).
* **Tiêu chí nghiệm thu (DoD):** Cơ chế One-Way Lock bảo vệ dữ liệu tuyệt đối ở mức Database; Whisper bóc băng âm thanh chất lượng cao phục vụ Cổng hậu kiểm.

#### 📍 Buổi Báo Cáo 2 — Thứ 6 (Ngày 4 của Tuần 4)
* **🧑 Thành (Lead BE & Architect):**
  - **Task BE-4.2:** Viết API kết xuất bảng điểm khảo thí `GET /api/v1/audit/export-excel` dùng thư viện `EPPlus`.
  - **Nội dung demo:** Tải về file `.xlsx` bảng điểm: Mở trên Excel hiển thị đúng chuẩn mẫu Khảo thí nhà trường (FAP), đầy đủ STT, MSSV, Họ tên, Phòng Lab, Số máy, Điểm AI, Điểm Giảng viên và Lý do giải trình.
* **🧑 Tốt (BE Developer & QA):**
  - **Task BE-4.4 & BE-4.5:** Tích hợp Cloudflare R2 Presigned Upload (`STT_MSSV.webm`), hàm băm niêm phong SHA-256 và API sửa điểm thẩm định `PUT /api/v1/audit/override`.
  - **Nội dung demo:** Tính toán mã băm SHA-256 trên file audio, sửa thử 1 byte trên R2 $\rightarrow$ Mã băm phát hiện ngay dấu hiệu gian lận. Thử sửa điểm khi để trống lý do giải trình $\rightarrow$ Bị chặn 422; nhập đủ lý do $\rightarrow$ Ghi nhận điểm mới kèm log vào `system_audit_logs`.
* **Tiêu chí nghiệm thu (DoD):** File Excel xuất ra mở được ngay; Con dấu SHA-256 bảo đảm tính toàn vẹn; Sửa điểm bắt buộc giải trình và lưu vết kiểm toán.

#### 📍 Buổi Báo Cáo 3 — Chủ Nhật (Ngày 6 của Tuần 4 — Final Gate Review & Bàn Giao)
* **Cả 2 thành viên:**
  - Chạy toàn bộ kiểm thử UAT và NetArchTest kiểm tra vi phạm kiến trúc Clean Architecture: Đạt 100% PASS (Zero Architecture Violations).
  - Xuất bản tài liệu nghiệm thu kỹ thuật `FINAL_TEST_REPORT.md` (bao gồm kết quả Unit Test, Load Test, và 6 Test Cases cho 4 Core Flows).
  - Phối hợp với Frontend tổng duyệt diễn tập demo toàn hệ thống sẵn sàng bảo vệ đồ án trước Hội đồng tốt nghiệp FPTU!

---

## 📋 4. KHUNG 6 TEST CASES BẮT BUỘC CHO MỖI ENDPOINT BACKEND

Mỗi API Endpoint khi hoàn thành phải có bằng chứng kiểm thử đạt chuẩn cho 6 kịch bản:
1. **TC-01 (Happy Path - 200/201/202):** Dữ liệu hợp lệ, kết quả trả về đúng DTO JSON `camelCase`.
2. **TC-02 (Input Validation - 400 Bad Request):** Payload thiếu trường bắt buộc hoặc sai định dạng cú pháp JSON.
3. **TC-03 (Authentication & Authorization - 401/403):** Thiếu Bearer Token hoặc Sinh viên cố gọi API của Giảng viên.
4. **TC-04 (Business Invariant Rule - 422 Unprocessable):** Tổng Rubric != 10.0, hoặc sửa điểm mà để trống lý do giải trình.
5. **TC-05 (Rate Limit / Quota Guard - 429 Too Many Requests):** Gọi thi thử lần thứ 4 trong ngày của môn học.
6. **TC-06 (Data Integrity & One-Way Lock - 403 Forbidden):** Cố tình gửi request sửa/xóa bài thi đã bị niêm phong khóa một chiều (`is_locked = true`).
