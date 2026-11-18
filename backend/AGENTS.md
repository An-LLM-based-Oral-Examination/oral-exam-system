# ⚙️ AGENTS.md — Ranh Giới Sở Hữu & Chuẩn Kỹ Thuật Backend (`05_Source_Code/backend`)

> **Phạm vi nạp tự động:** Chỉ kích hoạt khi mở/sửa tệp trong `05_Source_Code/backend/`. Kế thừa 100% bất biến nghiệp vụ từ [`D:\Đồ Án\AGENTS.md`](../../AGENTS.md).

## 1. Bản Đồ Sở Hữu Thư Mục Backend (Đồng Bộ SSOT `D:\Đồ Án\AGENTS.md`)
- **🧑 Nguyễn Quang Thành (Team Leader & Lead BE Architect — Chủ sở hữu trực tiếp):**
  - `src/API/` (27 Controllers RFC 7807, cấu hình pipeline `Program.cs` đặt `UseCors("AllowFrontend")` **trước** `UseHttpsRedirection`/`UseStaticFiles`, 3 `SignalR Hubs`: `/hubs/practice`, `/hubs/in-class-shift`, `/hubs/anti-cheat`)
  - `src/Application/Features/{Practice,MockExams,InClassShifts,OfficialExams,Appeals,Admin}/` (MF-01, MF-02, BE MF-05 `InClassShifts`, Gói BE Khảo thí & Phúc khảo 5 bước MF-04, Admin BE)
  - Báo cáo Khảo thí (`.xlsx` + PDF ký số PKI), vận hành VPS Production & điều phối kiến trúc tổng thể.
- **🧑 Nguyễn Trọng Tốt (BE Dev, AI Specialist & QA Lead — Chỉ đọc Contract khi chưa điều phối):**
  - `src/Infrastructure/{Services,Authentication}/` & `OneWayLockInterceptor`
  - `src/Application/Features/{Auth,QuestionBank,AcademicCatalog,AcademicHierarchy,Courses,Semesters,Notifications,DeadLetterQueue,Speech}/` (Dual Auth `auth_version` + HttpOnly refresh session + Rate Limiting, MF-03 hai kho câu hỏi R8 & Multimodal AI, đường truyền AI/STT Polly 2s-4s-8s/DLQ)
  - `tests/` (Chiến lược kiểm thử xUnit / PostgreSQL Integration / `NetArchTest`).
- **🧑 Phạm Nguyễn Đăng Hải (DB Specialist):** 38 bảng PostgreSQL 16 3NF + 3 bảng vật lý bổ trợ = 41 `DbSet` (**CẤM code logic C# BE ngoài phạm vi Migration/DB khi chưa điều phối**).

## 2. Kỷ Luật Clean Architecture 4 Tầng & 3NF
- **Chiều phụ thuộc cứng:** `Domain` (POCO Entities 3NF, 0 tham chiếu EF/ASP.NET) $\leftarrow$ `Application` (CQRS MediatR, FluentValidation, `Result<T>`) $\leftarrow$ `Infrastructure` (`OralExamDbContext`, `PostgresSchemaReconciler` fail-closed, Migrations, 5 Workers) $\leftarrow$ `API` (Controllers mỏng trả lỗi chuẩn **RFC 7807 `ProblemDetails`**).
- **Truy vấn & Đồng bộ CSDL:** Bắt buộc `.AsNoTracking()` cho truy vấn đọc, truyền `CancellationToken` xuyên suốt, đồng bộ `Entity` $\leftrightarrow$ `OralExamDbContext` $\leftrightarrow$ `Migration` (cấm nhồi cột, cấm thêm `[NotMapped]` RAM tạm).
- **Kiểm chứng bắt buộc trước khi dừng:** `dotnet build "D:\Đồ Án\05_Source_Code\backend\OralExamination.sln" --configuration Release -warnaserror -nowarn:NU1900` và `dotnet test` đạt `Exit Code = 0`.
