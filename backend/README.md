# ⚙️ Oral Examination System — Backend Engineering Portal (.NET 8 Clean Architecture)

> **Dự án:** Hệ Thống Luyện Thi & Đánh Giá Vấn Đáp Bằng LLM Cho Ngành Kỹ Thuật Phần Mềm (FA26SE166)  
> **Nhóm thực hiện:** Nguyễn Quang Thành (Team Leader & Lead Backend Architect), Nguyễn Trọng Tốt (Backend Developer, AI Specialist & QA Lead), Nguyễn Đăng Hải (DB Specialist & Frontend Developer (phụ trách Database 30 bảng 3NF PostgreSQL và dồn toàn lực phát triển Frontend; tuyệt đối không code C# Backend)), Lê Vũ Hoàng (Lead Frontend Architect & Fullstack Coordinator)  
> **Tech Stack:** .NET 8 · C# 12 · Clean Architecture · MediatR CQRS · EF Core PostgreSQL 16+ · Bounded Channel · SignalR · Google Gemini 1.5 Flash/Pro · Whisper STT · Cloudflare R2

---

## 🚀 1. Khởi Động Nhanh (One-Click Setup)

```bash
# 1. Di chuyển vào thư mục backend
cd "d:\Đồ Án\05_Source_Code\backend"

# 2. Khôi phục packages & biên dịch kiểm tra
dotnet restore
dotnet build

# 3. Khởi chạy Web API Server (Port 5000 / 5265)
cd src/API
dotnet run
```

* Swagger UI: **`http://localhost:5000/swagger`** (hoặc `http://localhost:5265/swagger`)
* Health check: **`http://localhost:5000/health`**

---

## 👥 2. Phân Vai Tác Chiến Kỹ Sư Backend

| Thành viên | Trách nhiệm cốt lõi | Các phân hệ & thư mục phụ trách chính |
| :--- | :--- | :--- |
| **Thành** *(Lead BE & Architect)* | • Kiến trúc Clean Architecture 4 tầng, DI Container, GlobalExceptionMiddleware RFC 7807.<br>• Chủ trì toàn diện Backend Khối 1 (MF-01: Luyện tập tương tác tự do, BoundedChannel 1000 slots RAM, SignalR `PracticeHub`, SystemConfigs, Upload Audio Whisper stream, SubmitAnswer, GradingQueueWorker...).<br>• Chủ trì toàn diện Backend Khối 2 (MF-02: Thi thử tính giờ Voice-First Gate, Dynamic Timer, Quota Guard do Trưởng BM cấu hình, Instant Scorecard CLO).<br>• Module Báo cáo Khảo thí Phòng thi (Excel `.xlsx` + PDF chữ ký số). | • `src/Domain/Common/*`<br>• `src/Application/Common/*`<br>• `src/Application/Features/Practice/*`<br>• `src/Application/Features/MockExams/*`<br>• `src/Infrastructure/Persistence/*`<br>• `src/Infrastructure/Channels/*`<br>• `src/Infrastructure/BackgroundWorkers/*`<br>• `src/Infrastructure/Hubs/PracticeHub.cs`<br>• `src/Infrastructure/Services/ExcelService.cs`<br>• `src/Infrastructure/Services/PdfService.cs`<br>• `src/API/*` |
| **Tốt** *(BE Developer, AI & QA Lead)* | • Xác thực Google OAuth PKCE (mở mọi email Google, cấp JWT 5 roles, loại bỏ `password_hash`).<br>• Chủ trì Backend Khối 3 (MF-03: Ngân hàng câu hỏi & Barem Rubric 10.0đ, AI sinh đề từ FLM Syllabus theo CLO, tick chọn 2 kho `practice_questions`/`exam_questions`, Gửi duyệt & Trưởng BM phê duyệt 3 quyết định `APPROVED`/`NEEDS_REVISION`/`REJECTED`).<br>• TOÀN BỘ Backend Khối 4 (MF-04: Kiosk Check-in IP Binding `ip_address` ghế 1-40, R2 Audio `STT_MSSV.webm` + SHA-256 niêm phong, AI chấm ngầm Gemini CoT, AI Doubt Guard `is_suspicious = true`, API Evidence Panel cho Giảng viên, One-Way Lock `OneWayLockInterceptor`, Phân hệ Phúc khảo nội bộ `AppealRequest` Trưởng BM giao GV chấm lại).<br>• API Vệ tinh: Quản lý người dùng (FE-09 BE), Quản lý học kỳ (FE-08 BE), Thông báo in-app (FE-11 BE), Dashboard GV (FE-10 BE), DLQ & Audit Logs.<br>• QA Lead: Bộ test suites xUnit Tests & NetArchTest. | • `src/Application/Features/Auth/*`<br>• `src/Application/Features/Questions/*`<br>• `src/Application/Features/OfficialExams/*`<br>• `src/Application/Features/Appeals/*`<br>• `src/Application/Features/Users/*`<br>• `src/Application/Features/Semesters/*`<br>• `src/Application/Features/Notifications/*`<br>• `src/Infrastructure/Services/GeminiService.cs`<br>• `src/Infrastructure/Services/WhisperTranscriptionService.cs`<br>• `src/Infrastructure/Services/R2StorageService.cs`<br>• `tests/*` |
| **Hải** *(DB Specialist & Frontend Developer)* | • Thiết kế & quản trị PostgreSQL 16 DDL (30 bảng 3NF & SystemAuditLogs), Docker Compose (tuyệt đối KHÔNG code logic C# Backend); dồn toàn lực tham gia phát triển Frontend (Student Portal, Mock Exam Voice-First MF-02, Phê duyệt đề Trưởng BM MF-03, Ca thi & Phân hệ Phúc khảo MF-04, User Mgmt FE-09, Semester FE-08). | • `infra/postgres/*`<br>• `docker-compose.yml`<br>• `frontend/src/*` |

---

## 🏛️ 3. Cấu Trúc Solution Clean Architecture 4 Tầng

```
backend/
├── OralExamination.sln
├── BE_TASK_BOARD.md              # Bảng phân rã task chi tiết & tiến độ Backend v7.0
├── README.md                     # Hướng dẫn kỹ thuật này
└── src/
    ├── Domain/                   # Thực thể hạt nhân, Enums, Domain Exceptions (Pure C#, Zero Dependencies)
    ├── Application/              # MediatR CQRS Commands/Queries, DTOs, Validators, Interfaces
    ├── Infrastructure/           # EF Core PostgreSQL, Bounded Channels, Background Worker, SignalR, AI Services
    └── API/                      # REST Controllers, Middlewares RFC 7807, Program.cs Composition Root
```

---

## 📚 4. Hai Tài Liệu Pháp Lý Kỹ Thuật Bắt Buộc Đọc

1. 📋 **Bảng phân rã nhiệm vụ & tiến độ kỹ thuật Backend:**  
   👉 Đọc tại file: [`BE_TASK_BOARD.md`](./BE_TASK_BOARD.md)  
   *(Chi tiết 4 Khối chức năng, phân vai từng task cho Thành, Tốt, Hải, Hoàng).*

2. 🔌 **Quy ước Hợp đồng API giữa FE và BE (.NET 8):**  
   👉 Đọc tại file: [`../docs/API_CONTRACT_AND_INTEGRATION_GUIDE.md`](../docs/API_CONTRACT_AND_INTEGRATION_GUIDE.md)  
   *(Chi tiết Base URL `/api/v1/`, cấu trúc JSON camelCase, mã lỗi 422/429/403, và SignalR Hub `/hubs/practice`).*
