# 🤖 AGENTS.md — BẢN THAM CHIẾU NGUỒN MÃ NGUỒN (05_Source_Code)

> **Single Source of Truth:** Toàn bộ quy chuẩn chi tiết của Đồ Án SEP490 (`FA26SE166`) được nạp tự động từ tệp gốc [`D:\Đồ Án\AGENTS.md`](../AGENTS.md) và skill [`oral-exam-pipeline`](../.agents/skills/oral-exam-pipeline/SKILL.md). Tệp này chỉ giữ chỉ mục điều hướng nhanh cho thư mục `05_Source_Code` nhằm bảo toàn Rules Token Budget.

## ⚡ Chỉ Mục Nhanh `05_Source_Code`
1. **Kiến trúc Backend (.NET 8 Clean Architecture 4 Tầng):** `backend/src/{Domain,Application,Infrastructure,API}` và `backend/tests/` (38 Entities 3NF, 6 Roles RBAC, xUnit test suites).
2. **Kiến trúc Frontend (React 19 Vite SPA):** `frontend/src/{components,pages,services,stores,hooks}` (Tailwind v4 CSS-first, Zustand, Axios RFC 7807, SignalR `/hubs/practice` & `/hubs/anti-cheat`).
3. **Tài liệu Kiến trúc Duy nhất (SSOT):** [`docs/01_architecture/MASTER_ARCHITECTURE.md`](./docs/01_architecture/MASTER_ARCHITECTURE.md) & [`docs/01_architecture/TECHNICAL_DESIGN_PROPOSAL_R1_R8.md`](./docs/01_architecture/TECHNICAL_DESIGN_PROPOSAL_R1_R8.md).
4. **Cổng Kiểm Chứng Cứng (Hard Verification Gate — Exit Code = 0):**
   ```powershell
   dotnet build "D:\Đồ Án\05_Source_Code\backend"
   dotnet test "D:\Đồ Án\05_Source_Code\backend" --verbosity normal
   cd "D:\Đồ Án\05_Source_Code\frontend"; npm run build; cd ..
   python "D:\Đồ Án\04_Scripts_Generators\test_anti_split_brain_integrity.py"
   ```
