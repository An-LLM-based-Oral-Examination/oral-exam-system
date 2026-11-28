# 🤖 AGENTS.md — KIẾN TRÚC KHO MÃ NGUỒN, TRẠNG THÁI PHÁT HÀNH & VPS 120 SV (`05_Source_Code`)

> **SSOT & Phân tách Đặc tả / Hiện trạng:** Kế thừa toàn bộ 6 Roles RBAC, 5 Luồng `MF-01..05`, 12 Invariants và 14 Taboos từ [`D:\Đồ Án\AGENTS.md`](../AGENTS.md) cùng [`oral-exam-pipeline`](../.agents/skills/oral-exam-pipeline/SKILL.md). Tuyệt đối không hiểu một tính năng được mô tả trong đặc tả là đã hoàn tất production; kiểm tra trạng thái thực tế tại [`docs/agent-coordination/CURRENT_TASK.md`](./docs/agent-coordination/CURRENT_TASK.md), [`FPT-PRODUCTION-FLOW.md`](./docs/agent-coordination/FPT-PRODUCTION-FLOW.md) và [`WAVE-01-DUAL-LANE-DELIVERY.md`](./docs/agent-coordination/WAVE-01-DUAL-LANE-DELIVERY.md).

---

## 1. Chuẩn UI FE (*Calm Academic Depth*) & 14 Điều Cấm Kỵ Đặc Thù (14 Taboos)
* **Chuẩn UI FE (*Calm Academic Depth*):** Nội dung chính nổi bật, ít màu, tiết chế hiệu ứng (**cấm 3D/Three.js gây nhiễu** trên màn thi/đồng hồ); responsive, hỗ trợ bàn phím & `prefers-reduced-motion`. Xử lý đủ **7 trạng thái**: *đang tải, chưa có dữ liệu, lỗi RFC 7807 dễ hiểu, đang lưu, lưu thành công, hết quyền/hết hạn (`401/403/410/429`), và khôi phục sau refresh*.
* **14 Taboos:** ❌ Cấm bảng/cột `chapter` & `bloom_radar`; ❌ Cấm tên cột `workstation_ip` (dùng **`ip_address`**); ❌ Cấm hardcode buffer/follow-up/quota & cấm Khảo thí chỉnh follow-up thi thật; ❌ Cấm bỏ bước Trưởng BM duyệt đề; ❌ Cấm vé thi chữ thường (7 trạng thái HOA: `SCHEDULED`, `IN_PROGRESS`, `SUBMITTED`, `AI_GRADED`, `AUDITED`, `PUBLISHED`, `LOCKED`); ❌ Cấm phúc khảo/hiện điểm tại Kiosk; ❌ Cấm placeholder; ❌ Cấm mock/dữ liệu giả trong production; ❌ Cấm đặt `UseCors("AllowFrontend")` sau `UseHttpsRedirection`/`UseStaticFiles`; ❌ Cấm tự ý `git commit`/`git push`; ❌ Cấm sửa mù chưa đọc file & AST Graph; ❌ Cấm sai Conventional Commits; ❌ Cấm nhồi cột / `[NotMapped]` RAM tạm; ❌ Cấm `proctor` lạm quyền ngoài điểm danh STT `1..max_seats` & báo cáo vi phạm.

---

## 2. Kiến Trúc 2 Client & Clean Architecture 4 Tầng (`05_Source_Code/`)
* **Client Tier (2 Phân hệ kết nối chung `.NET 8 API` Port `5000`):**
  1. **Web SPA (`frontend/src/*` — `React 19 Vite`, Port `3000` — Đã hiện thực):** Chuẩn UI *Calm Academic Depth*, xử lý đủ 7 trạng thái UI, kết nối 3 Hubs (`/hubs/practice`, `/hubs/in-class-shift`, `/hubs/anti-cheat`) cho 6 roles RBAC. Ranh giới sở hữu chi tiết xem tại [`frontend/AGENTS.md`](./frontend/AGENTS.md).
  2. **Desktop Kiosk (`d:\Đồ Án\oral-exam-desktop-kiosk` — Electron 45 + React 19 + `Oes.WindowsAgent`, nghiệm thu `WAVE-02`):** Desktop App cài trên laptop cá nhân sinh viên (BYOD) mang vào phòng thi có Giám thị cho luồng thí sinh `MF-04`, vé khóa 1 thiết bị + 1 phiên, chống gian lận chỉ trong phạm vi app (`OfficialExamPage.tsx` trên Web là bản giả lập/dự phòng kiểm thử).
* **Backend 4 Tầng (`backend/src/` & `backend/tests/`):** `Domain` (44 Entities) $\leftarrow$ `Application` (CQRS MediatR, FluentValidation) $\leftarrow$ `Infrastructure` (`OralExamDbContext` 48 `DbSet<>`, 51 active EF migrations / 52 files gồm `FrozenSchema`, `PostgresSchemaReconciler` fail-closed, `OneWayLockInterceptor`, Gemini/Whisper/R2) $\leftarrow$ `API` (32 v1 Controllers RFC 7807, 3 Hubs `/hubs/practice`, `/hubs/in-class-shift`, `/hubs/anti-cheat`, 5 Background Workers). Ranh giới sở hữu chi tiết xem tại [`backend/AGENTS.md`](./backend/AGENTS.md); kiến trúc gốc tại [`docs/01_architecture/MASTER_ARCHITECTURE.md`](./docs/01_architecture/MASTER_ARCHITECTURE.md).

---

## 3. Lộ Trình 2 Đợt, Phân Công Lane AI & Cổng Kiểm Chứng Cứng
* **Phân công `WAVE-01` (Cấm ngộ nhận Production-Ready):** Triển khai lớp học, `MF-01/02/05` theo `RC-1 → RC-2 → RC-3` ([`FPT-PRODUCTION-FLOW.md`](./docs/agent-coordination/FPT-PRODUCTION-FLOW.md), [`WAVE-01-DUAL-LANE-DELIVERY.md`](./docs/agent-coordination/WAVE-01-DUAL-LANE-DELIVERY.md)); **Antigravity phụ trách Lane FE (`frontend/**`)**, **Codex phụ trách Lane BE (`backend/**`, migrations & release gates)**; `WAVE-02` nghiệm thu riêng `MF-04`.
* **Kiểm chứng gần nhất:** *Đã đạt:* BE Release build sạch, `2.115/2.115` unit (`Category!=Integration`), `42/42` PostgreSQL local, `30/30` test CLO. *Chưa đạt:* FE build/lint snapshot; nối `cloId` đầy đủ cho `MF-05 SELECTED_CLOS`; E2E thật cùng revision; Testcontainers riêng; load test `120` SV; diễn tập backup/rollback & smoke production cùng revision. Chưa `git commit` / `git push` / deploy khi Thành chưa chỉ đạo rõ.
* **Cổng Kiểm Chứng Cứng (`Exit Code = 0` — hoặc chạy trọn gói [`verify_ci_mirror.ps1`](../04_Scripts_Generators/verify_ci_mirror.ps1)):**
  ```powershell
  dotnet build "D:\Đồ Án\05_Source_Code\backend"
  dotnet test "D:\Đồ Án\05_Source_Code\backend" --filter "Category!=Integration" --verbosity normal
  # Integration PG16: ./backend/scripts/Test-PostgresIntegration.ps1 -Iterations 1 -Configuration Release
  cd "D:\Đồ Án\05_Source_Code\frontend"; npm run build; cd ..
  python -X utf8 "D:\Đồ Án\04_Scripts_Generators\test_anti_split_brain_integrity.py"
  ```

---

## 4. Hạ Tầng VPS Production (`103.90.226.9` $\leftrightarrow$ `https://oralexam.io.vn`)
* **Cấu hình máy chủ:** Vietnix `VPS NVME LINUX 3`, Ubuntu 24.04 LTS, 4 CPU, 6GB RAM + 4GB Swap `/swapfile`, 50GB NVMe, Docker Compose tại `/opt/oralexam`, UFW mở `22/80/443` & khóa `5432` nội bộ, SSL Let's Encrypt.
* **Checklist chịu tải 120 SV đồng thời:** (1) `ClassesController` (R5, advisory lock sĩ số) & `InClassShiftsController` (R6/MF-05 `SELECTED_CLOS`); (2) `GradingQueueWorker` `MaxDegreeOfParallelism = 12`; (3) PostgreSQL `max_connections=300` & `Maximum Pool Size=200`; (4) Gemini Billing **Tier 1 (1.000 RPM)** + Google Cloud OAuth *Authorized JavaScript origins* `https://oralexam.io.vn`.
