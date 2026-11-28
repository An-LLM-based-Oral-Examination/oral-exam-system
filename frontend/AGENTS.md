# 🎨 AGENTS.md — Ranh Giới Sở Hữu & Chuẩn Kỹ Thuật Frontend (`05_Source_Code/frontend`)

> **Phạm vi nạp tự động:** Chỉ kích hoạt khi mở/sửa tệp trong `05_Source_Code/frontend/`. Kế thừa 100% bất biến nghiệp vụ từ [`D:\Đồ Án\AGENTS.md`](../../AGENTS.md).

## 1. Bản Đồ Sở Hữu Màn Hình & Thư Mục Frontend (Đồng Bộ SSOT `D:\Đồ Án\AGENTS.md`)
- **🧑 Nguyễn Quang Thành (Team Leader — Chủ sở hữu trực tiếp phần FE được giao):**
  - Full-stack **Admin FE** & **FE MF-05 (`InClassShifts` — Ca thi trong lớp TOTP 5p, điều khiển ca, hậu kiểm & chốt điểm `FINALIZED`)**.
- **🧑 Lê Vũ Hoàng (Lead FE Architect & Fullstack Coordinator):**
  - Kiến trúc lõi `src/*`: React 19 SPA + Router DOM v7 Guards 6 roles + Tailwind v4 (*Calm Academic Depth*) + React Context (`AuthContext`) & Hooks/Component State + Axios RFC 7807 Interceptor.
  - Màn hình nghiệp vụ: FE MF-01 (Luyện tập), **Web Kiosk Prototype/Fallback (`OfficialExamPage.tsx`) & Giám thị MF-04** *(lưu ý `OralExamination.DesktopKiosk` là `[Target Design / Proposed — Chưa code]` cho phòng Lab)*, Evidence Panel Wavesurfer.js, Auth UI, Notification in-app FE-11.
- **🧑 Phạm Nguyễn Đăng Hải (DB Specialist & FE Dev):**
  - Khung Layout chung (`PortalChrome`), FE Student Dashboard (FE-10), Lớp học Sinh viên, Lịch sử (FE-02), Thi thử MF-02, Trưởng BM quản lý CLO/phân công GV/duyệt đề MF-03, Khảo thí/Phúc khảo MF-04.
- *(Ranh giới phối hợp)*: Ngoài phạm vi **Admin FE** và **FE MF-05** của Thành, không tự ý sửa đè UI của Hoàng/Hải khi chưa có yêu cầu — chỉ cung cấp API/DTO Contract và công cụ kiểm thử độc lập.

## 2. Kỷ Luật React 19 Vite SPA & TypeScript Strict
- **Thuần Vite SPA (`vite.config.ts`):** **CẤM TUYỆT ĐỐI** đặt `'use client'`; dùng `import.meta.env.VITE_*` (không dùng `process.env`); cấu hình token Tailwind v4 qua `@theme` trong CSS gốc.
- **Chất lượng Type & Lifecycle:** Cấm `any`, cấm `@ts-nocheck`; dọn sạch kết nối SignalR (`/hubs/practice`, `/hubs/in-class-shift`, `/hubs/anti-cheat`), AudioContext, Web Speech và timers trong cleanup của `useEffect`.
- **Kiểm chứng bắt buộc trước khi dừng:** Chạy tại `05_Source_Code/frontend`: `npm run lint`, `npx tsc -b` và `npm run build` đạt `Exit Code = 0`.
