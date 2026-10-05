# ⚙️ Oral Examination System — Backend Engineering Portal (.NET 8 Clean Architecture)

> **Dự án:** Hệ Thống Luyện Thi & Đánh Giá Vấn Đáp Bằng LLM Cho Ngành Kỹ Thuật Phần Mềm (FA26SE166)  
> **Nhóm thực hiện:** Nguyễn Quang Thành (Team Leader & Lead Backend Architect), Nguyễn Trọng Tốt (Backend Developer, AI Specialist & QA Lead), Nguyễn Đăng Hải (DB Specialist & Frontend Developer (phụ trách Database 28 bảng PostgreSQL và dồn toàn lực phát triển Frontend; tuyệt đối không code C# Backend)), Lê Vũ Hoàng (Lead Frontend Architect & Fullstack Coordinator)  
> **Tech Stack:** .NET 8 · C# 12 · Clean Architecture · MediatR CQRS · EF Core PostgreSQL 16+ · Bounded Channel · SignalR · Google Gemini 1.5 Flash · Whisper STT · Cloudflare R2

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
| **Thành** *(Lead BE & Architect)* | • Kiến trúc Solution Clean Architecture 4 tầng, DI Container.<br>• GlobalExceptionMiddleware chuẩn RFC 7807 `ProblemDetails`.<br>• Authentication JWT Bearer HMAC-SHA256 & Phân quyền RBAC.<br>• EF Core PostgreSQL 28 bảng, Migrations & Seed data.<br>• Hàng đợi bộ nhớ Bounded Channel 1,000 slots & Background Worker.<br>• Real-time WebSocket SignalR Hub (`/hubs/practice`).<br>• PostgreSQL Quota Guard (3 lượt/môn/ngày) & Server Master Timer.<br>• One-Way Lock Interceptor (chặn sửa điểm ở DB) & Xuất bảng điểm ca thi Excel. | • `src/Domain/Common/*`<br>• `src/Application/Common/*`<br>• `src/Infrastructure/Persistence/*`<br>• `src/Infrastructure/Channels/*`<br>• `src/Infrastructure/BackgroundWorkers/*`<br>• `src/Infrastructure/Hubs/PracticeHub.cs`<br>• `src/Infrastructure/Interceptors/*`<br>• `src/API/Middlewares/*`<br>• `src/API/Program.cs` |
| **Tốt** *(BE Developer, AI & QA Lead)* | • CRUD Môn học & Chương (`CoursesController.cs`).<br>• CreateQuestionCommand + FluentValidation (Tổng Rubric = 10.0đ, ACID Transaction).<br>• Gemini 1.5 Flash CoT Prompt 3 bước & Ép khuôn JSON thuần khiết.<br>• Logic Adaptive AI Question A2 ($4.0 \le \text{Score} \le 8.0$).<br>• Whisper STT Server-side cho MF-04 (bóc băng kèm timestamps).<br>• Cloudflare R2 Presigned Upload & Mã băm niêm phong SHA-256.<br>• API Sửa điểm thẩm định giải trình (`PUT /api/v1/audit/override`).<br>• xUnit Tests, NetArchTest và Load Testing 500 CCU k6/NBomber. | • `src/Application/Features/Courses/*`<br>• `src/Application/Features/Questions/*`<br>• `src/Application/Features/Practice/*`<br>• `src/Application/Features/ExamSessions/*`<br>• `src/Infrastructure/Services/GeminiService.cs`<br>• `src/Infrastructure/Services/WhisperTranscriptionService.cs`<br>• `src/Infrastructure/Services/R2StorageService.cs`<br>• `tests/OralExamination.UnitTests/*`<br>• `tests/FA26SE166.ArchitectureTests/*` |
| **Hải** *(DB Specialist & Frontend Developer)* | • Thiết kế & quản trị PostgreSQL 16 DDL (28 bảng 3NF & SystemAuditLogs), Docker Compose (tuyệt đối KHÔNG code logic C# Backend); dồn toàn lực tham gia phát triển Frontend (Student Portal, Mock Exam Voice-First, Phê duyệt đề Trưởng BM, Phân hệ Phúc khảo). | • `infra/postgres/*`<br>• `docker-compose.yml`<br>• `frontend/src/*` |

---

## 🏛️ 3. Cấu Trúc Solution Clean Architecture 4 Tầng

```
backend/
├── OralExamination.sln
├── BE_TASK_EXECUTION_GUIDE.md    # Cẩm nang phân rã task 4 tuần & lịch họp 2 ngày/lần
├── README.md                     # Hướng dẫn kỹ thuật này
└── src/
    ├── Domain/                   # Thực thể hạt nhân, Enums, Domain Exceptions (Pure C#, Zero Dependencies)
    ├── Application/              # MediatR CQRS Commands/Queries, DTOs, Validators, Interfaces
    ├── Infrastructure/           # EF Core PostgreSQL, Bounded Channels, Background Worker, SignalR, AI Services
    └── API/                      # REST Controllers, Middlewares RFC 7807, Program.cs Composition Root
```

---

## 📚 4. Hai Tài Liệu Pháp Lý Kỹ Thuật Bắt Buộc Đọc

1. 📋 **Kế hoạch chi tiết 4 tuần & Lịch họp 2 ngày/lần:**  
   👉 Đọc tại file: [`BE_TASK_EXECUTION_GUIDE.md`](./BE_TASK_EXECUTION_GUIDE.md)  
   *(Chi tiết nhiệm vụ từng ngày từ Tuần 1 đến Tuần 4 cho cả 4 thành viên: Thành, Tốt, Hải, Hoàng).*

2. 🔌 **Quy ước Hợp đồng API giữa FE và BE (.NET 8):**  
   👉 Đọc tại file: [`../docs/API_CONTRACT_AND_INTEGRATION_GUIDE.md`](../docs/API_CONTRACT_AND_INTEGRATION_GUIDE.md)  
   *(Chi tiết Base URL `/api/v1/`, cấu trúc JSON camelCase, mã lỗi 422/429/403, và SignalR Hub `/hubs/practice`).*
