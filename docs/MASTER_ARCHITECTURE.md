# 🏛️ TÀI LIỆU KIẾN TRÚC TỔNG THỂ HỆ THỐNG (MASTER SYSTEM ARCHITECTURE)
## HỆ THỐNG LUYỆN THI & ĐÁNH GIÁ VẤN ĐÁP BẰNG LLM CHO NGÀNH KỸ THUẬT PHẦN MỀM
### Đồ Án Tốt Nghiệp Capstone: FA26SE166 — Khoa Kỹ Thuật Phần Mềm — Đại Học FPT Hà Nội

> **Mã đề tài:** FA26SE166 | **Học kỳ:** Fall 2026 (FA26)  
> **Chuyên ngành:** Kỹ thuật Phần mềm (Software Engineering - SE) — Đại học FPT Hà Nội  
> **Giảng viên hướng dẫn:** ThS. Nguyễn Thị Cẩm Hương (`huongntc2@fpt.edu.vn`)  
> **Kiến trúc công nghệ chuẩn:** .NET 8 Clean Architecture 4 tầng · React 19 (Vite) / Next.js 15 · PostgreSQL 16 · Google Gemini 1.5 Flash/Pro · Whisper STT · Cloudflare R2 · SignalR Core  
> **Tiêu chuẩn tài liệu:** Báo cáo Hội đồng chấm tốt nghiệp FPTU & Tài liệu Đặc tả Kỹ thuật Hệ thống (C4 Model, Clean Architecture, Deployment Topology, 4 Core Main Flows).

---

## MỤC LỤC TÀI LIỆU

1. [Sơ Đồ 1: Sơ Đồ Thành Phần Hệ Thống (C4 System Context & Component Diagram)](#so-do-1)
2. [Sơ Đồ 2: Sơ Đồ Kiến Trúc Phân Tầng Clean Architecture (.NET 8 + Next.js 15 / React 19)](#so-do-2)
3. [Sơ Đồ 3: Sơ Đồ Tuần Tự MF-04 (Sequence Diagram: Lab Exam & Audit Flow)](#so-do-3)
4. [Sơ Đồ 4: Sơ Đồ Kiến Trúc Hạ Tầng Vật Lý & Mạng (Physical / Network Deployment Architecture)](#so-do-4)
5. [Sơ Đồ 5: Tổng Quan Kiến Trúc Luồng Dữ Liệu 4 Main Flows (Data Architecture)](#so-do-5)
6. [Ma Trận Bền Vững & Ranh Giới Bảo Mật Hệ Thống (NFRs & Security Guarantees)](#nfrs-security)

---

## <a id="so-do-1"></a>1. SƠ ĐỒ THÀNH PHẦN HỆ THỐNG (C4 SYSTEM CONTEXT & COMPONENT DIAGRAM)

### 1.1. Sơ Đồ Mermaid

```mermaid
flowchart TD
    subgraph Human_Actors ["Tác Nhân Người Dùng (Authenticated Stakeholders @fpt.edu.vn)"]
        Student["Sinh viên SE (ACT-01)<br>• Luyện tập tự do (MF-01)<br>• Thi thử bấm giờ (MF-02)<br>• Thi Kiosk phòng Lab (MF-04)"]
        Instructor["Giảng viên / Giám khảo (ACT-02)<br>• Quản trị ngân hàng đề và Rubric (MF-03)<br>• Thử nghiệm Barem qua AI Simulator<br>• Cổng Thẩm định Audit Portal (MF-04)"]
        Admin["Quản trị viên Hệ thống (ACT-03)<br>• Quản trị danh mục môn/lớp<br>• Cấu hình Quota K và mô hình AI<br>• Quản lý và Replay bài lỗi DLQ"]
    end

    subgraph Presentation_Tier ["Tầng Trình Diễn (Presentation / Client Tier)"]
        WebPortal["Web Application Portal (React 19 Vite / Next.js 15)<br>• Student Practice và Mock Exam UI<br>• Lecturer Question Studio và Audit Portal<br>• 60s Terminology Code-Switching Buffer<br>• Waveform Player 1.25x/1.5x"]
        LabKiosk["Máy Trạm Kiosk Phòng Lab (Lab PC Client)<br>• Fullscreen Kiosk Lockdown (chặn F12, onblur 3 cấp)<br>• 30s Hardware Mic-Check Validation<br>• Ghi âm MediaRecorder WebM theo STT_MSSV"]
        ClientSpeech["Web Speech Engine (Client STT / TTS - ACT-05a)<br>• STT tiếng Việt độ trễ cực thấp dưới 0.5s<br>• TTS đọc to câu hỏi và nhận xét"]
    end

    subgraph Core_Backend ["Tầng Dịch Vụ Cốt Lõi (.NET 8 Clean Architecture API)"]
        APIGateway["ASP.NET Core 8 Web API Gateway<br>• REST Controllers (Auth, Subjects, Questions, Practice, Lab, Audit)<br>• RFC 7807 Global Exception Handler<br>• JWT Bearer và RBAC Authorization Filter"]
        SignalRHub["SignalR Core Hub (/hubs/practice)<br>• WebSocket 2 chiều đẩy Scorecard dưới 100ms<br>• Đồng bộ trạng thái phòng thi và chấm bù"]
        ValidationPipe["FluentValidation và Business Guards<br>• Client/Server Guard: Barem Rubric tổng = 10.0đ (HTTP 422)<br>• PostgreSQL Quota Guard: Hạn ngạch K=3 lượt/ngày (HTTP 429)"]
        BoundedQueue["Bounded Channel In-Memory Queue<br>• Hàng đợi 1,000 slots phân tách Ingestion và Worker<br>• Chế độ Backpressure BoundedChannelFullMode.Wait"]
        BgWorker["Background Hosted Workers (ACT-07)<br>• GradingQueueWorker: Rút task, gọi AI + Polly Retry<br>• DlqReplayWorker: Quét bài chấm bù định kỳ 5 phút<br>• Server Master Timer: Đo thời gian thi chống hack client"]
    end

    subgraph Persistence_Tier ["Tầng Lưu Trữ và Bền Vững (Data Persistence Tier)"]
        PostgresDB[("PostgreSQL 16 Relational DB<br>• 12 bảng chuẩn hóa 3NF<br>• JSONB lưu trữ Barem Rubric 10.0<br>• OneWayLockInterceptor: Chặn sửa điểm DB khi locked")]
        DLQ_Store[("Bảng Dead-Letter Queue (DLQ)<br>• Cách ly an toàn bài thi lỗi sau 3 lần retry<br>• Bảo đảm Zero Data Loss 100%")]
    end

    subgraph External_Cloud ["Dịch Vụ Ngoại Vi và Điện Toán Đám Mây"]
        OAuth["Google Workspace (FPT OAuth 2.0 PKCE)<br>• Xác thực định danh duy nhất @fpt.edu.vn"]
        GeminiAI["Google API (Dịch vụ AI Đám Mây - ACT-04)<br>• Gemini 1.5 Flash: Chấm luyện tập CoT 3 bước và Adaptive A2<br>• Gemini 1.5 Pro: Chấm Batch ca thi phòng Lab"]
        WhisperSTT["Whisper Large-v3 STT Engine (ACT-05b)<br>• Bóc băng Server-side kèm timestamps phát ngôn"]
        Cloudflare_R2["Cloudflare R2 Object Storage (ACT-06)<br>• Lưu trữ Audio STT_MSSV.webm (Zero-Egress S3)<br>• Niêm phong toàn vẹn bằng mã băm SHA-256"]
        FAP_System["FPT Academic Portal (FAP Interchange)<br>• Import danh sách thi phòng Lab Excel<br>• Export bảng điểm chính thức .xlsx chữ ký số"]
    end

    %% Human Interactions
    Student -->|Truy cập tự luyện và thi thử| WebPortal
    Student -->|Ngồi đúng STT máy thi vấn đáp| LabKiosk
    Instructor -->|Quản lý đề, chạy Simulator và Hậu kiểm| WebPortal
    Admin -->|Quản trị hệ thống và Giám sát DLQ| WebPortal

    %% Client Interactions
    WebPortal <-->|Nhận diện giọng nói và Đọc đề| ClientSpeech
    LabKiosk <-->|Phần cứng tai nghe mic và TTS| ClientSpeech
    WebPortal -->|HTTPS REST JSON và WSS| APIGateway
    WebPortal <-->|Real-time Scorecard Push| SignalRHub
    LabKiosk -->|Gửi Audio Hash SHA-256 và Kết quả| APIGateway
    LabKiosk -->|Stream trực tiếp file audio STT_MSSV.webm| Cloudflare_R2

    %% Backend Internals
    APIGateway -->|Xác thực Token PKCE| OAuth
    APIGateway --> ValidationPipe
    ValidationPipe -->|Ghi đĩa tức thời Persist-First dưới 100ms| PostgresDB
    ValidationPipe -->|Đẩy task chấm điểm non-blocking| BoundedQueue
    BoundedQueue -->|Rút task tuần tự| BgWorker
    BgWorker -->|Gọi chấm Rubric CoT JSON Schema| GeminiAI
    BgWorker -->|Kéo audio ca thi bóc băng timestamps| WhisperSTT
    BgWorker -->|Đối chiếu file audio và kiểm tra SHA-256| Cloudflare_R2
    BgWorker -->|Cập nhật trạng thái GRADED / Lưu điểm| PostgresDB
    BgWorker -->|Cách ly bài lỗi sau 3 lần thất bại| DLQ_Store
    BgWorker -->|Bắn sự kiện hoàn tất chấm| SignalRHub
    APIGateway -->|Import sinh viên và Export điểm FAP| FAP_System
```

### 1.2. Diễn Giải Thành Phần & Ranh Giới Kỹ Thuật
- **Nguyên tắc Zero-Trust Identity:** Hệ thống loại bỏ tác nhân người dùng nặc danh (Zero Guest Access). 100% người dùng bắt buộc xác thực qua Google Workspace `@fpt.edu.vn` bằng chuẩn OAuth 2.0 Authorization Code Flow kèm PKCE.
- **Phân tách Presentation & Ingestion:** Client giao tiếp với Backend thông qua HTTPS REST API (`/api/v1/`) và kết nối hai chiều WebSocket SignalR Core (`/hubs/practice`). Trình duyệt xử lý audio cục bộ qua Web Speech API (độ trễ dưới 0.5s) và màn hình đệm 60s để người học sửa lỗi phát âm thuật ngữ tiếng Anh (*Code-Switching*).
- **Hàng đợi chịu tải & Phòng thủ 4 tầng (Zero Data Loss):**
  1. *Tầng 1 (Persist-First):* Ghi bài nộp xuống PostgreSQL dưới trạng thái `PENDING` trong dưới 100ms và trả ngay `HTTP 202 Accepted`.
  2. *Tầng 2 (Bounded Channel):* Điều tiết tải bằng `System.Threading.Channels` dung lượng 1,000 slots, kích hoạt cơ chế `Wait` khi quá tải.
  3. *Tầng 3 (Polly Exponential Backoff):* Tự động thử lại cuộc gọi Gemini AI (2s → 4s → 8s) khi gặp lỗi mạng/HTTP 429/503.
  4. *Tầng 4 (Dead-Letter Queue):* Cách ly các bài nộp lỗi vào bảng `dead_letter_queues`, tiến trình nền định kỳ 5 phút quét chấm bù tự động.

---

## <a id="so-do-2"></a>2. SƠ ĐỒ KIẾN TRÚC PHÂN TẦNG CLEAN ARCHITECTURE (.NET 8 + NEXT.JS 15 / REACT 19)

### 2.1. Sơ Đồ Mermaid

```mermaid
flowchart TD
    subgraph Layer_Client ["Presentation Layer — Web Client (React 19 Vite / Next.js 15)"]
        direction TB
        UI_Components["React Components và Pages<br>• PracticeView và TimedMockExamView<br>• QuestionRubricStudio và AISimulatorModal<br>• LecturerAuditPortal (Waveform Player 1.25x/1.5x)<br>• AdminGovernanceConsole (DLQ Replay)"]
        UI_Audio["Client Audio và Security Engines<br>• Web Speech STT / TTS Client Controller<br>• 60s Terminology Code-Switching Buffer<br>• MediaRecorder Audio Sealer (STT_MSSV.webm)<br>• Fullscreen Kiosk Lockdown và onblur Guard"]
        UI_Network["Client Network Adapters<br>• Axios / Fetch API Client (/api/v1/*)<br>• SignalR Connection Hub Client (/hubs/practice)<br>• Client-Side Rubric Guard (Sum == 10.0)"]
    end

    subgraph Layer_API ["API Layer — Host và Entry Point (ASP.NET Core 8 Web API)"]
        direction TB
        API_Program["Program.cs (Composition Root)<br>• Dependency Injection Wire-up<br>• Middleware Pipeline Configuration<br>• CORS Contract (UseCors trước UseStaticFiles)"]
        API_Controllers["API Controllers (/api/v1/)<br>• AuthController (Google OAuth PKCE)<br>• SubjectsController và QuestionsController<br>• PracticeController và MockExamController<br>• ExamSessionsController và AuditController<br>• AdminController (DLQ Management)"]
        API_Middleware["Middlewares và Filters<br>• GlobalExceptionMiddleware (RFC 7807 ProblemDetails)<br>• JwtBearerAuthMiddleware và RBAC Authorization<br>• Client RateLimiting Middleware"]
        API_SignalR["SignalR Real-Time Hubs<br>• PracticeHub (/hubs/practice)<br>• ExamProctorHub (Đồng bộ phòng thi)"]
    end

    subgraph Layer_App ["Application Layer — Core Business Logic (MediatR CQRS)"]
        direction TB
        App_Commands["CQRS Commands và Handlers<br>• SubmitPracticeAnswerCommand (202 Accepted)<br>• StartMockExamCommand (PostgreSQL Quota Guard)<br>• CreateQuestionCommand (Barem Rubric 10.0)<br>• CalibrateQuestionCommand (AI Simulator)<br>• OverrideScoreCommand và LockExamScoreCommand<br>• ReplayDlqTaskCommand (Cứu hộ DLQ)"]
        App_Queries["CQRS Queries và Handlers<br>• GetPracticeHistoryQuery và GetScorecardQuery<br>• GetMockExamAnalyticsQuery (Biểu đồ Radar Bloom)<br>• GetExamAuditSessionQuery (Đối chiếu Audio và Barem)<br>• ExportFapGradesExcelQuery (EPPlus)"]
        App_Pipeline["MediatR Pipeline Behaviors<br>• ValidationBehavior (FluentValidation: Sum == 10.0đ)<br>• LoggingBehavior và PerformanceTrackingBehavior"]
        App_Interfaces["Application Interfaces (Contracts / Abstractions)<br>• IApplicationDbContext và IUnitOfWork<br>• IGeminiService và IWhisperService<br>• IR2StorageService, IQuotaService và IExcelService"]
    end

    subgraph Layer_Infra ["Infrastructure Layer — Adapters và External Concerns"]
        direction TB
        Infra_Persistence["Data Access và EF Core 8<br>• ApplicationDbContext (PostgreSQL Npgsql Provider)<br>• Entity Configurations (Fluent API, 3NF, Indexes)<br>• EF Migrations và SeedData (5 GV, 50 SV, 2 Môn)"]
        Infra_Interceptors["Database Interceptors<br>• OneWayLockInterceptor (Chặn UPDATE row is_locked=true)<br>• AuditTrailInterceptor (Ghi system_audit_logs)"]
        Infra_Channels["In-Memory Bounded Channels<br>• BoundedGradingQueueChannel (1,000 slots Capacity)<br>• ChannelReader / ChannelWriter Thread-Safe Queue"]
        Infra_Workers["Background Hosted Services<br>• GradingQueueWorker (IHostedService + Polly Retry)<br>• DlqReplayWorker (Định kỳ 5 phút quét chấm bù)"]
        Infra_ExternalServices["External Service Adapters<br>• GeminiService (Prompt CoT 3 bước và JSON Schema)<br>• WhisperTranscriptionService (Bóc băng kèm Timestamps)<br>• R2StorageService (Presigned URL và SHA-256 Hash)<br>• PostgreSqlQuotaService (Kiểm tra Quota 3 lượt/ngày)<br>• ExcelService (EPPlus Import Roster / Export FAP)"]
    end

    subgraph Layer_Domain ["Domain Layer — Enterprise Kernel (Pure C# POCO)"]
        direction TB
        Domain_Entities["Core Domain Entities<br>• User, Course, Class, ClassEnrollment<br>• Question, RubricCriterion, ModelAnswer<br>• ExamStructure, ExamSet, ExamSetQuestion<br>• PracticeSession, StudentAnswer, AIEvaluation<br>• OfficialExamSession, StudentExamTicket<br>• DeadLetterQueue, SystemAuditLog"]
        Domain_Enums["Domain Enums và Value Objects<br>• UserRole (Student, Instructor, Admin)<br>• BloomLevel (Remember đến Create)<br>• ScopeType (EXAM_ONLY, PRACTICE_ONLY, SHARED)<br>• SubmissionStatus (PENDING đến LOCKED)<br>• ExamSessionStatus"]
        Domain_Exceptions["Domain Business Exceptions<br>• DomainValidationException<br>• EntityNotFoundException<br>• ExamQuotaExceededException<br>• ScoreSheetLockedException"]
    end

    %% Component Dependency Arrows enforcing Clean Architecture
    UI_Network -->|Giao tiếp REST và WSS| API_Controllers
    UI_Network -.->|Lắng nghe sự kiện| API_SignalR
    API_Controllers -->|Dispatch Commands / Queries| App_Commands
    API_Controllers -->|Dispatch Queries| App_Queries
    App_Commands -->|Pipeline qua ValidationBehavior| App_Pipeline
    Infra_Persistence -->|Implement| App_Interfaces
    Infra_ExternalServices -->|Implement| App_Interfaces
    App_Commands -->|Thao tác nghiệp vụ thực thể| Domain_Entities
    App_Queries -->|Truy vấn dữ liệu| Domain_Entities
    Infra_Persistence -->|Ánh xạ ORM CSDL| Domain_Entities
```

### 2.2. Diễn Giải Ranh Giới 4 Tầng & Nguyên Lý Dependency Inversion
- **Domain Layer (Hạt nhân bất biến):** Chứa 100% C# POCO thuần túy, tuyệt đối không tham chiếu bất kỳ thư viện ngoài, framework hay ORM nào. Nắm giữ quy tắc nghiệp vụ cốt lõi: các trạng thái bài thi (`SubmissionStatus`), phân loại Bloom, và ngoại lệ nghiệp vụ (`ScoreSheetLockedException`).
- **Application Layer (Điều phối nghiệp vụ):** Độc lập với cơ sở dữ liệu và công nghệ bên ngoài. Triển khai mẫu thiết kế CQRS qua MediatR; mọi thao tác ghi dữ liệu đều đóng gói trong Command, thao tác đọc trong Query. Toàn bộ nghiệp vụ kiểm tra tính hợp lệ của Barem Rubric 10.0đ được kiểm soát tập trung qua `FluentValidation` và `ValidationBehavior`.
- **Infrastructure Layer (Triển khai kỹ thuật):** Chịu trách nhiệm thực thi các Interfaces định nghĩa tại Application: kết nối PostgreSQL 16 qua EF Core 8, điều phối hàng đợi bộ nhớ `System.Threading.Channels`, giao tiếp Google Gemini API, bóc băng Whisper STT, lưu trữ Cloudflare R2, và chốt chặn khóa điểm bất biến bằng `OneWayLockInterceptor`.
- **API Layer (Cổng tiếp nhận & Host):** Chịu trách nhiệm khởi tạo Composition Root (`Program.cs`), tiếp nhận HTTP Request, đóng gói mã lỗi thống nhất theo chuẩn RFC 7807 (`ProblemDetails`), và duy trì kết nối thời gian thực SignalR Hub.

---

## <a id="so-do-3"></a>3. SƠ ĐỒ TUẦN TỰ MF-04 (SEQUENCE DIAGRAM: LAB EXAM & AUDIT FLOW)

### 3.1. Sơ Đồ Mermaid

```mermaid
sequenceDiagram
    autonumber
    actor GV as Giảng viên (Giám thị / Hậu kiểm)
    actor SV as Thí sinh (Tại Máy Lab theo STT)
    participant LabPC as Máy trạm Kiosk (Lab PC UI)
    participant Cloud as Cloudflare R2 Audio Storage
    participant API as Backend Exam API (.NET 8)
    participant DB as CSDL PostgreSQL và AuditLog
    participant Worker as Background Worker (Whisper + Gemini Pro)
    participant AuditUI as Lecturer Audit Portal

    Note over GV,LabPC: 🖥️ PHA 1: THIẾT LẬP CA THI VÀ KIỂM TRA PHẦN CỨNG KIOSK (SETUP + MIC-CHECK)
    GV->>API: Mở ca thi trên Proctor Console: Chọn lớp, nạp danh sách SV gán STT = Số máy thi, MSSV
    API->>DB: Khởi tạo ca thi phòng lab, gán thí sinh vào vị trí máy trạm theo STT
    
    SV->>LabPC: Vào phòng lab cách âm IELTS, ngồi vào máy theo đúng STT và Đăng nhập MSSV
    LabPC->>LabPC: Kích hoạt Fullscreen Kiosk Mode (chặn F12, Ctrl+C/V, Alt+Tab, chuột phải, window.onblur)
    LabPC->>LabPC: Bắt buộc Mic-Check 30s kiểm tra phần cứng âm thanh tai nghe và micro
    opt Phần cứng lỗi [Hardware error]
        GV->>LabPC: Đổi thí sinh sang máy trạm dự phòng đã cấu hình sẵn
    end
    
    Note over GV,LabPC: 🎙️ PHA 2: THI VẤN ĐÁP PHÒNG LAB VÀ NIÊM PHONG AUDIO (STT_MSSV.webm)
    GV->>API: Bấm nút Bắt Đầu Ca Thi Cả Phòng Lab trên Proctor Console
    API->>LabPC: Phát tín hiệu SignalR đồng bộ bắt đầu ca thi: Cấp đề theo cấu trúc đã duyệt
    
    loop Cho từng câu hỏi thứ i (1 ≤ i ≤ N)
        LabPC->>LabPC: Web Speech TTS đọc đề bài qua tai nghe và bật đồng hồ đếm ngược
        SV->>LabPC: Phát biểu câu trả lời vào micro tai nghe (MediaRecorder thu âm webm/opus)
        alt Thí sinh nói xong sớm
            SV->>LabPC: Bấm nút Hoàn thành câu hỏi
        else Hết thời gian câu i
            LabPC->>LabPC: Tự động khóa micro, kết thúc thu âm câu thứ i
        end
        
        Note over LabPC,Cloud: Đóng gói file âm thanh theo định danh bắt buộc: STT_MSSV.webm
        LabPC->>Cloud: Stream file audio STT_MSSV.webm lên Cloudflare R2
        Cloud-->>LabPC: Trả về audio_url
        LabPC->>LabPC: Tính mã băm cryptographic SHA-256 niêm phong pháp lý
        LabPC->>API: POST /api/v1/exam-lab/submit-question (STT_MSSV, audio_url, sha256_hash)
        API->>DB: Lưu bản ghi nộp bài (status='SUBMITTED', audio_url, sha256_hash)
    end

    LabPC->>API: Gửi lệnh kết thúc bài thi của thí sinh
    API->>DB: UPDATE student_exam_tickets SET status='SUBMITTED'
    LabPC-->>SV: MÀN HÌNH CHỈ HIỆN DUY NHẤT: Bài thi đã nộp thành công và niêm phong an toàn.<br>Vui lòng tháo tai nghe và trật tự rời phòng thi.
    Note over SV,LabPC: TUYỆT ĐỐI KHÔNG HIỆN ĐIỂM LIỀN TRONG PHÒNG THI! Sinh viên rời phòng (Hết Pha 2)

    Note over GV,DB: 🤖 PHA 3: BACKGROUND AI BATCH WORKER (AI CHẤM HẬU KỲ 1-2 GIỜ SAU CA THI)
    GV->>API: Bấm nút Kết Thúc Ca Thi trên Proctor Console khi cả phòng đã nộp bài
    API->>DB: Đóng ca thi phòng lab, kích hoạt Background Worker quét các bài SUBMITTED
    
    loop Background Worker chấm batch toàn bộ ca thi
        Worker->>Cloud: Kéo file audio STT_MSSV.webm từ Cloudflare R2
        Worker->>Worker: Chạy Whisper STT bóc tách văn bản kèm timestamp phát ngôn
        Worker->>DB: UPDATE student_exam_tickets SET status='TRANSCRIBED'
        Worker->>Worker: Gọi Gemini 1.5 Pro nạp Barem Rubric 10.0 chấm tuần tự bóc tách tiêu chí
        Worker->>DB: INSERT official_ai_evaluations (Điểm tiêu chí, Điểm mạnh/yếu, Timestamp Citations)
        Worker->>DB: UPDATE student_exam_tickets SET status='AI_GRADED'
    end
    Worker->>GV: AI xuất file bảng điểm sơ bộ theo từng SV và gửi email thông báo cho GV hoàn tất 100%

    Note over GV,AuditUI: 👨‍🏫 PHA 4: GIẢNG VIÊN THẨM ĐỊNH TẠI LECTURER AUDIT PORTAL VÀ KHÓA 1 CHIỀU
    GV->>AuditUI: Đăng nhập Cổng Hậu Kiểm, chọn ca thi phòng lab cần thẩm định
    AuditUI->>API: GET /api/v1/exam-audit/sessions/{sessionId}
    API->>DB: Nạp danh sách SV theo STT, file audio STT_MSSV.webm và Điểm AI đề xuất
    DB-->>AuditUI: Dữ liệu thẩm định song song
    AuditUI-->>GV: Hiển thị bảng điểm: Cột Điểm GV MẶC ĐỊNH = Điểm AI đề xuất kèm bằng chứng timestamp
    
    loop Thẩm định từng sinh viên theo STT
        GV->>AuditUI: Chọn sinh viên STT k
        AuditUI->>Cloud: Phát file audio STT_MSSV.webm trên Waveform Player (1.0x, 1.25x, 1.5x)
        GV->>AuditUI: Nghe lại và nhấp vào mốc timestamp để nghe đoạn phát ngôn nghi vấn
        alt GV đồng ý với điểm AI
            GV->>AuditUI: Bấm Duyệt (chấp thuận điểm AI)
        else AI chấm chưa sát (do từ lóng, nói lắp, hoặc ngữ cảnh đặc thù)
            GV->>AuditUI: Trực tiếp sửa điểm tiêu chí và nhập lý do giải trình bắt buộc
        end
        AuditUI->>API: POST /api/v1/exam-audit/save-audit (Cập nhật DB status='AUDITED')
    end

    Note over GV,AuditUI: KHÓA ĐIỂM 1 CHIỀU VÀ ĐẨY ĐIỂM VỀ SINH VIÊN / ĐỒNG BỘ FAP
    GV->>AuditUI: Bấm nút tối cao: XÁC NHẬN VÀ KHÓA ĐIỂM CHÍNH THỨC (One-Way Lock)
    AuditUI->>API: POST /api/v1/exam-audit/lock-grades (sessionId)
    API->>DB: BEGIN TRANSACTION
    API->>DB: UPDATE student_exam_tickets SET is_locked=true, status='LOCKED' WHERE session_id=sessionId
    API->>DB: INSERT INTO audit_logs (gv_id, ai_score, final_score, override_reason, timestamp)
    API->>DB: COMMIT TRANSACTION
    
    par [Đẩy điểm về Sinh viên]
        API-->>SV: Đẩy bảng điểm chính thức và nhận xét hiển thị trên Student Portal
    and [Đồng bộ cổng FAP]
        API->>AuditUI: Xuất tệp bảng điểm Excel chuẩn FAP và PDF có chữ ký số
        AuditUI-->>GV: Tải file nộp về Phòng Khảo thí Đại học FPT
    end
```

### 3.2. Diễn Giải Quy Trình Khảo Thí 4 Pha (MF-04)
1. **Chuẩn hóa định danh `STT = Số Máy Thi`:** Sinh viên bước vào phòng lab cách âm ngồi vào máy theo đúng số thứ tự STT danh sách thi. Mọi dữ liệu thu âm được định danh tự động theo cú pháp bắt buộc **`STT_MSSV.webm`** (ví dụ: `01_SE170123.webm`), triệt tiêu hoàn toàn rủi ro nhầm lẫn bài nộp.
2. **Nguyên tắc Bảo mật Phòng Thi — Zero Immediate Score:** Máy trạm phòng thi sau khi kết thúc giờ làm bài chỉ hiển thị thông báo niêm phong an toàn và yêu cầu thí sinh trật tự rời phòng thi. Tuyệt đối không hiển thị điểm số hay nhận xét của AI tại phòng lab, ngăn chặn triệt để tình trạng hoang mang hoặc tranh cãi tại chỗ.
3. **Bóc băng Whisper kèm Timestamps & Chấm Hậu kỳ:** AI chấm ngầm độc lập ở Pha 3 sau khi ca thi đóng. Mô hình Whisper Large-v3 bóc băng âm thanh có gắn mốc thời gian chi tiết từng câu nói, giúp Gemini 1.5 Pro trích dẫn bằng chứng cụ thể vào Scorecard.
4. **Cổng Hậu kiểm Giảng viên & Khóa Một Chiều (One-Way Lock):** Giảng viên nghe lại bản ghi âm trên Waveform Player (hỗ trợ tua 1.25x/1.5x và nhảy nhanh đến timestamp trích dẫn). Giảng viên có toàn quyền điều chỉnh điểm kèm theo **lý do giải trình bắt buộc**. Khi bấm nút khóa điểm, hệ thống kích hoạt transaction đặt cờ `is_locked = true`, kích hoạt `OneWayLockInterceptor` tại DB để ngăn chặn mọi hành vi can thiệp trái phép sau đó.
5. **Vòng đời 5 trạng thái bài thi thật:**
   $$\mathbf{SUBMITTED} \longrightarrow \mathbf{TRANSCRIBED} \longrightarrow \mathbf{AI\_GRADED} \longrightarrow \mathbf{AUDITED} \longrightarrow \mathbf{LOCKED}$$
   *(Hiển thị chuẩn hóa: **SUBMITTED** → **TRANSCRIBED** → **AI_GRADED** → **AUDITED** → **LOCKED**)*

---

## <a id="so-do-4"></a>4. SƠ ĐỒ KIẾN TRÚC HẠ TẦNG VẬT LÝ & MẠNG (PHYSICAL / NETWORK DEPLOYMENT ARCHITECTURE)

### 4.1. Sơ Đồ Mermaid

```mermaid
flowchart TD
    subgraph Client_Locations ["Phạm Vi Truy Cập Người Dùng (Client Networks)"]
        WAN_User["Người Dùng Ngoại Mạng (Sinh viên / Giảng viên tại nhà)<br>• Web Browser (Chrome, Edge, Safari)<br>• Kết nối Internet công cộng qua HTTPS/TLS 1.3"]
        Lab_Network["Phòng Máy Khảo Thí Đại Học FPT (Campus Lab Network)<br>• 40 Máy trạm Kiosk Lab PC (Mạng LAN nội bộ / Isolated VLAN)<br>• Firewall trường chặn truy cập Internet tự do<br>• Whitelist duy nhất Domain hệ thống thi"]
    end

    subgraph Edge_Security ["Vành Đai An Ninh và Phân Phối (Edge and Ingress Gateway)"]
        Cloudflare_Edge["Cloudflare Edge Network (Global Anycast)<br>• DDoS Protection Layer 3/4/7<br>• WAF Rules chống SQL Injection, XSS, Path Traversal<br>• Quản lý DNS và Chứng chỉ SSL/TLS 1.3"]
        Reverse_Proxy["Nginx Ingress / Reverse Proxy (Linux Host Gateway)<br>• Port 80: HTTP 301 Redirect sang HTTPS Port 443<br>• Port 443: SSL Offloading và HSTS Security Headers<br>• Upstream Router: Định tuyến / sang Port 3000, /api/* và /hubs/* sang Port 5000"]
    end

    subgraph Production_Host ["Máy Chủ Sản Xuất (Production Linux Server / Docker Host)"]
        subgraph Docker_Engine ["Docker Containerization Topology"]
            FE_Container["Container: oralexam-frontend<br>• Base Image: Node.js 20 Alpine<br>• Framework: React 19 (Vite) / Next.js 15 SSR<br>• Internal Port: 3000"]
            BE_Container["Container: oralexam-backend<br>• Base Image: .NET 8 ASP.NET Core Runtime<br>• Web Server: Kestrel Web Server<br>• Internal Port: 5000<br>• In-Process Bounded Channel (1,000 slots)<br>• In-Process Hosted Services: GradingQueueWorker và DlqReplayWorker"]
            DB_Container["Container: oralexam-postgres<br>• Base Image: PostgreSQL 16 Alpine<br>• Internal Port: 5432 (Không mở ra Internet)<br>• Volume Mount: /var/lib/postgresql/data (Persistent Storage)<br>• Daily Automated Database Snapshot Backup"]
        end
    end

    subgraph Cloud_Infrastructure ["Hạ Tầng Đám Mây và Dịch Vụ Ngoài (External Cloud and AI)"]
        Google_AI["Google Cloud Vertex AI và Google API<br>• Google Gemini 1.5 Flash (Practice và AI Simulator)<br>• Google Gemini 1.5 Pro (Batch Grading Lab Exam)<br>• HTTPS REST API Endpoint"]
        Whisper_Service["Whisper STT Server Service<br>• OpenAI Whisper Large-v3 (GPU Worker Node)<br>• Server-side Audio Transcription"]
        Cloudflare_R2["Cloudflare R2 Object Storage<br>• S3-Compatible Storage Endpoint (Zero-Egress Fees)<br>• Bucket: oralexam-lab-audio<br>• Lưu trữ {STT}_{MSSV}.webm kèm SHA-256 seal"]
        Google_OAuth["Google Workspace Identity Provider<br>• FPT OAuth 2.0 PKCE Endpoint accounts.google.com"]
        FAP_Interchange["FPT Academic Portal (FAP)<br>• Cổng Thông Tin Khảo Thí Nhà Trường (Excel Interchange)"]
    end

    %% Network Routes
    WAN_User -->|HTTPS 443 / WSS| Cloudflare_Edge
    Lab_Network -->|HTTPS 443 Kiosk Mode| Cloudflare_Edge
    Cloudflare_Edge -->|Proxy Encrypted Traffic| Reverse_Proxy

    Reverse_Proxy -->|Proxy Pass / và static assets| FE_Container
    Reverse_Proxy -->|Proxy Pass /api/* và WebSocket /hubs/*| BE_Container

    BE_Container -->|Internal TCP 5432 / Npgsql Connection Pool| DB_Container

    %% Outbound Cloud Integrations
    BE_Container -->|REST HTTPS Call Outbound| Google_AI
    BE_Container -->|Dispatch Audio for Batch STT| Whisper_Service
    BE_Container -->|Generate Presigned URL và Verify Digest| Cloudflare_R2
    Lab_Network -->|Direct Presigned Stream STT_MSSV.webm| Cloudflare_R2
    BE_Container -->|PKCE Token Exchange| Google_OAuth
    BE_Container <-->|Import Roster và Export Grade Sheet .xlsx| FAP_Interchange
```

### 4.2. Bảng Phân Bổ Cổng Mạng, Vùng Cách Ly & Bảo Mật

| Vùng mạng | Thành phần | Cổng tiếp nhận | Giao thức | Chính sách truy cập & Bảo vệ an ninh |
|:---|:---|:---:|:---:|:---|
| **Public Edge** | Cloudflare Edge Network | 443 (HTTPS), 80 (HTTP) | TLS 1.3 / WSS | WAF kiểm soát botnet, chống DDoS Layer 7, chặn truy cập ngoài dải IP FPT nếu ca thi bật giới hạn. |
| **Ingress Gateway** | Nginx Reverse Proxy | 443, 80 | HTTPS / WSS | Tiếp nhận traffic từ Cloudflare, chuyển hướng 80 → 443, cấu hình Rate Limiting 100 req/s/IP. |
| **Frontend App** | `oralexam-frontend` | 3000 (Internal) | HTTP | Chỉ lắng nghe gói tin từ Nginx trên mạng ảo Docker bridge (`oralexam-net`). |
| **Backend API** | `oralexam-backend` | 5000 (Internal) | HTTP / WSS | Chạy Kestrel Server, tiếp nhận REST request và kết nối WebSocket SignalR `/hubs/practice`. |
| **Database Tier** | `oralexam-postgres` | 5432 (Internal) | TCP / PostgreSQL | **CẤM TUYỆT ĐỐI mở port 5432 ra Internet**. Chỉ cho phép Backend kết nối nội bộ qua mật khẩu mạnh mã hóa. |
| **Object Storage** | Cloudflare R2 | 443 (HTTPS) | S3 API | Lưu trữ âm thanh bài thi phòng Lab với chính sách **Zero-Egress** không tính phí tải về, phân quyền qua Pre-signed URL có hạn 15 phút. |

---

## <a id="so-do-5"></a>5. TỔNG QUAN KIẾN TRÚC LUỒNG DỮ LIỆU 4 MAIN FLOWS (DATA ARCHITECTURE)

### 5.1. Sơ Đồ Mermaid

```mermaid
flowchart TD
    subgraph MF01_Box ["MF-01: Luyện Tập Tự Do (Interactive Practice)"]
        direction TB
        M1_Start["SV chọn Môn và Chọn Chế Độ Upfront<br>• [Per-Question]: Chấm tức thì từng câu<br>• [Full-Session]: Làm hết N câu rồi chấm"]
        M1_Speech["TTS đọc đề ↔ Micro thu âm / Gõ phím"]
        M1_Buffer["Màn hình đệm 60s hiệu đính Code-Switching"]
        M1_Queue["Hàng đợi 4 tầng Zero Data Loss<br>(Persist PENDING → Bounded Channel → Polly Retry → DLQ)"]
        M1_AI["Gemini 1.5 Flash CoT chấm Barem Rubric 10.0"]
        M1_A2["Kiểm tra điểm: Nếu 4.0 ≤ Score ≤ 8.0<br>→ Kích hoạt câu hỏi chuyên sâu đào sâu A2"]
        M1_Start --> M1_Speech --> M1_Buffer --> M1_Queue --> M1_AI --> M1_A2
    end

    subgraph MF02_Box ["MF-02: Thi Thử Bấm Giờ (Timed Mock Exam)"]
        direction TB
        M2_Quota["PostgreSQL Quota Guard<br>Kiểm tra hạn ngạch K=3 lượt/ngày/môn<br>(Quá lượt: ném HTTP 429 và gợi ý MF-01)"]
        M2_Timer["Đồng hồ kép: Server Master Timer và Client Countdown"]
        M2_Gate["Voice-First Gate: Khóa ô gõ phím,<br>bắt buộc phát biểu bằng giọng nói trước"]
        M2_DualPath["Chấm điểm 2 pha: Fast-Path dưới 10s hoặc Async Fallback"]
        M2_Radar["Cập nhật Scorecard và Biểu đồ Radar Bloom 6 cấp độ"]
        M2_Quota --> M2_Timer --> M2_Gate --> M2_DualPath --> M2_Radar
    end

    subgraph MF03_Box ["MF-03: Ngân Hàng Đề và Barem Rubric (Question and Rubric Studio)"]
        direction TB
        M3_Author["Giảng viên soạn câu hỏi và Phân loại Bloom 6 cấp"]
        M3_Scope["Phân định phạm vi: [Thi thật], [Luyện tập], [Dùng chung]"]
        M3_Rubric["Thiết kế Barem Rubric: Tiêu chí C1..Ck và Điểm số"]
        M3_ClientGuard["Real-time Client Guard: Tổng điểm bắt buộc = 10.0đ<br>(Nếu != 10.0đ: Khóa nút Lưu và Cảnh báo đỏ)"]
        M3_Sim["AI Simulator: Giảng viên chấm thử nghiệm Barem bằng AI"]
        M3_Commit["Backend FluentValidation Gate HTTP 422<br>→ ACID Database Transaction Commit"]
        M3_Author --> M3_Scope --> M3_Rubric --> M3_ClientGuard --> M3_Sim --> M3_Commit
    end

    subgraph MF04_Box ["MF-04: Thi Thật Phòng Lab và Cổng Hậu Kiểm (Lab Exam and Audit Portal)"]
        direction TB
        M4_P1["Pha 1: Thiết lập ca thi (STT = Số máy thi)<br>Kiosk Fullscreen Lockdown và Mic-Check 30s"]
        M4_P2["Pha 2: Làm bài thi viva, stream STT_MSSV.webm lên R2<br>Băm SHA-256 niêm phong pháp lý, KHÔNG hiện điểm tại phòng"]
        M4_P3["Pha 3: AI chấm ngầm hậu kỳ hàng loạt (Background Worker)<br>Whisper STT bóc băng timestamps + Gemini 1.5 Pro CoT"]
        M4_P4["Pha 4: Giảng viên thẩm định tại Audit Portal<br>Waveform Player, override điểm kèm lý do bắt buộc"]
        M4_Lock["One-Way Lock (is_locked=true) khóa cứng DB<br>→ Đẩy điểm về Student Portal và Xuất Excel FAP"]
        M4_P1 --> M4_P2 --> M4_P3 --> M4_P4 --> M4_Lock
    end

    subgraph Core_Shared_Data ["Tài Nguyên Dữ Liệu Dùng Chung (Shared Data and Engine Kernel)"]
        Shared_QB[("Ngân Hàng Câu Hỏi và Barem 10.0<br>(PostgreSQL questions, rubric_criteria)")]
        Shared_Queue["Bounded Channel In-Memory Queue (1,000 slots)"]
        Shared_AI["Google Gemini 1.5 API (Flash / Pro CoT Engine)"]
        Shared_Audio["Cloudflare R2 Object Storage (STT_MSSV.webm)"]
        Shared_DB[("PostgreSQL 12 Bảng 3NF và SystemAuditLogs")]
    end

    %% Cross-Flow Integrations
    M3_Commit -->|Nạp dữ liệu đề thi| Shared_QB
    Shared_QB -.->|Cung cấp câu hỏi tự luyện| M1_Start
    Shared_QB -.->|Bốc đề thi ngẫu nhiên| M2_Quota
    Shared_QB -.->|Cấp đề ca thi phòng Lab| M4_P1

    M1_Queue --> Shared_Queue
    M2_DualPath --> Shared_Queue
    Shared_Queue --> Shared_AI

    M4_P2 -->|Lưu trữ file âm thanh| Shared_Audio
    Shared_Audio -->|Kéo file bóc băng| M4_P3
    M4_P3 --> Shared_AI

    M1_AI --> Shared_DB
    M2_Radar --> Shared_DB
    M4_Lock --> Shared_DB
```

### 5.2. Ma Trận So Sánh Điểm Nhấn 4 Core Main Flows

| Đặc tính kiến trúc | MF-01: Luyện tập tự do | MF-02: Thi thử bấm giờ | MF-03: Quản lý ngân hàng đề | MF-04: Thi thật phòng Lab |
|:---|:---|:---|:---|:---|
| **Tác nhân chính** | Sinh viên SE (`ACT-01`) | Sinh viên SE (`ACT-01`) | Giảng viên (`ACT-02`) | Sinh viên (`ACT-01`) & Giảng viên (`ACT-02`) |
| **Mục tiêu sư phạm** | Formative Assessment (Rèn luyện phản xạ) | Mock Pressure (Tập dượt áp lực thời gian) | Governance (Chuẩn hóa chuẩn đầu ra) | Summative Assessment (Đánh giá học phần lấy điểm) |
| **Cơ chế kiểm soát** | Tự do chọn chế độ Upfront (`[Per-Q]` vs `[Full]`) | Daily Quota Guard (K=3 lượt/ngày) | Ràng buộc bất biến ∑ Barem ≡ 10.0đ | Kiosk Lockdown, STT=Số máy, One-Way Lock |
| **Xử lý âm thanh** | Web Speech STT/TTS (dưới 0.5s) | Voice-First Gate (Bắt buộc nói) | Text-based Barem & Audio Preview | Whisper Large-v3 STT bóc băng có timestamps |
| **Lưu trữ âm thanh** | Không lưu trữ (tối ưu chi phí) | Không lưu trữ (tối ưu chi phí) | Không áp dụng | Cloudflare R2 (`STT_MSSV.webm`) kèm SHA-256 |
| **Mô hình AI** | Gemini 1.5 Flash (CoT 3 bước) | Gemini 1.5 Flash (Dual-Path) | Gemini 1.5 Flash (AI Simulator) | Gemini 1.5 Pro (Batch Grading hậu kỳ) |
| **Tính năng độc bản** | Adaptive Follow-up A2 (4.0 ≤ Score ≤ 8.0) | Biểu đồ Radar Bloom 6 mức độ | AI Simulator thẩm định độ nhạy Barem | Cổng Thẩm định Giảng viên & Khóa điểm FAP |

---

## <a id="nfrs-security"></a>6. MA TRẬN BỀN VỮNG & RANH GIỚI BẢO MẬT HỆ THỐNG (NFRS & SECURITY GUARANTEES)

1. **Hiệu năng & Tốc độ đáp ứng (NFR-P1 & NFR-P2):**
   - Phản hồi chấm điểm tức thời qua Gemini 1.5 Flash hoàn tất trong vòng ≤ 10 giây (thực tế đo lường đạt 1.8s - 2.5s).
   - Nhận diện giọng nói phía client qua Web Speech API đạt độ trễ cực thấp dưới 0.5 giây.
   - Thao tác nộp bài ghi nhận đĩa cứng PostgreSQL trong dưới 100ms và trả mã `202 Accepted` trước khi phân luồng vào hàng đợi.
2. **Bảo đảm Không Mất Dữ Liệu (Zero Data Loss - NFR-R1):**
   - Hàng đợi `BoundedChannel` 1,000 slots kết hợp cơ chế `Wait` ngăn chặn tràn bộ nhớ khi lượng truy cập tăng vọt đột biến.
   - Cơ chế thử lại tự động Polly Exponential Backoff (2s → 4s → 8s) bảo vệ hệ thống trước sự cố chập chờn mạng hoặc nghẽn API Google Cloud.
   - Các tác vụ chấm điểm thất bại sau 3 lần retry được lưu trữ vĩnh viễn trong bảng `dead_letter_queues`, sẵn sàng cho tiến trình tự động chấm bù hoặc thao tác Replay thủ công từ Quản trị viên.
3. **Toàn vẹn Pháp lý & Chống Can Thiệp Điểm Thi (NFR-S1 & NFR-S2):**
   - Toàn bộ file âm thanh thi thật tại phòng Lab được niêm phong bằng mã băm SHA-256 ngay tại thời điểm nộp bài trên máy trạm Kiosk và kiểm chứng lại tại Server.
   - Quy trình khóa điểm một chiều (`is_locked = true`) kích hoạt `OneWayLockInterceptor` tại tầng Entity Framework Core, triệt tiêu hoàn toàn nguy cơ sửa điểm sau khi Giảng viên đã ký số và chốt kết quả.
   - Mọi thao tác sửa điểm của Giảng viên bắt buộc phải có trường `overrideReason` và tự động lưu vết vào `system_audit_logs`.

---
*Tài liệu kiến trúc hệ thống chính thức phục vụ đồ án tốt nghiệp Capstone FA26SE166 — Đại học FPT Hà Nội.*
