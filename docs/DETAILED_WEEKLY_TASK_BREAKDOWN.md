# BẢNG PHÂN RÃ NHIỆM VỤ CHI TIẾT THEO TUẦN (WBS - WORK BREAKDOWN STRUCTURE)
**Dự án:** An LLM-based Oral Examination  
**Tài liệu này cung cấp danh sách Task chi tiết đến từng Module/API/Màn hình cho 4 thành viên (Bám sát Report 2).**

---

## 🚀 TUẦN 1: NỀN TẢNG HỆ THỐNG & NGÂN HÀNG CÂU HỎI (MF-03)
*Mục tiêu: Dựng móng dự án, hoàn tất việc tạo đề thi và barem điểm 10.0*

**1. Lê Vũ Hoàng (FE Lead)**
- `[FE-T1.1]` Khởi tạo Repo Vite React TS, cài đặt TailwindCSS, Shadcn/ui, cấu hình Prettier/ESLint.
- `[FE-T1.2]` Xây dựng Layout gốc (Sidebar, Header, Auth Guard/Protected Routes).
- `[FE-T1.3]` Dựng màn hình Đăng nhập & Phân quyền UI (Giảng viên vs Sinh viên).
- `[FE-T1.4]` Dựng màn hình Quản lý Câu hỏi (Table list).
- `[FE-T1.5]` Code Form tạo Câu hỏi & Rubric 10.0 (Gắn Zod validation: Khóa nút submit nếu tổng điểm các tiêu chí khác 10.0).

**2. Phạm Nguyễn Đăng Hải (BE & FE Support)**
- `[BE-T1.1]` Thiết kế CSDL PostgreSQL, viết script EF Core Migrations cho 11 bảng theo ERD 3NF.
- `[BE-T1.2]` Setup Data Seeders (Tạo sẵn 5 tài khoản giảng viên, 50 sinh viên mẫu để test).
- `[BE-T1.3]` Viết API CRUD Câu hỏi & Barem điểm (Đảm bảo dùng ACID Transaction để lưu Question và Rubric cùng lúc).
- `[FE-T1.6]` *Hỗ trợ FE:* Hỗ trợ Hoàng móc nối API vào Form tạo câu hỏi để hiện dữ liệu động.

**3. Nguyễn Quang Thành (Lead, BE)**
- `[BE-T1.4]` Review cấu trúc Clean Architecture (.NET 8). Cài đặt Global Exception Handler (RFC 7807) & JWT Authentication.
- `[BE-T1.5]` Thiết lập file OpenAPI/Swagger Contract cho MF-03 để FE và BE làm việc độc lập.
- `[MNG-T1.1]` Duyệt Pull Requests (PR) tuần 1, merge nhánh `feature` vào `develop`.

**4. Nguyễn Trọng Tốt (QA, AI/BE)**
- `[QA-T1.1]` Setup dự án Unit Test (xUnit, Moq, FluentAssertions).
- `[QA-T1.2]` Viết Unit Tests cho các API MF-03 (Test case quan trọng: Bắn payload tổng điểm 9.0 -> Assert API trả về HTTP 422).

---

## 🚀 TUẦN 2: THI TƯƠNG TÁC (MF-01) & LÕI TRÍ TUỆ NHÂN TẠO (AI CORE)
*Mục tiêu: Ghi âm qua web, AI chấm điểm tự động và trả kết quả thời gian thực*

**1. Lê Vũ Hoàng (FE Lead)**
- `[FE-T2.1]` Dựng UI Màn hình Luyện tập (Interactive Practice).
- `[FE-T2.2]` Tích hợp Web Speech API (STT & TTS), thu âm thanh từ Micro và parse ra chữ realtime.
- `[FE-T2.3]` Xây dựng "Màn hình đệm 60s" cho sinh viên sửa lỗi nhận diện sai từ chuyên ngành (Code-Switching).
- `[FE-T2.4]` Code Modal Scorecard nhận tín hiệu Realtime để hiển thị điểm.

**2. Phạm Nguyễn Đăng Hải (BE & FE Support)**
- `[BE-T2.1]` Viết API tạo Phiên luyện tập (Practice Session), update trạng thái PENDING.
- `[BE-T2.2]` Viết API lưu lịch sử bài nộp của sinh viên.
- `[FE-T2.5]` *Hỗ trợ FE:* Giúp Hoàng code bộ đếm ngược 60s ở màn hình đệm bằng React Hooks.

**3. Nguyễn Quang Thành (Lead, BE)**
- `[BE-T2.3]` Xây dựng Hàng đợi 4 tầng (Bounded Channel, Polly Retry 2s-4s-8s) hứng bài nộp chống quá tải Server.
- `[BE-T2.4]` Setup SignalR Hub để khi AI chấm xong, BE đẩy thẳng điểm số về màn hình FE mà không cần F5.

**4. Nguyễn Trọng Tốt (QA, AI/BE)**
- `[AI-T2.1]` Viết Service kết nối Google Gemini 1.5 Flash API.
- `[AI-T2.2]` Tinh chỉnh Chain-of-Thought (CoT) Prompt: Ép Gemini chấm theo đúng chuẩn Rubric và trả về JSON Schema (`{ score: float, feedback: string }`).
- `[QA-T2.3]` Chạy Postman Load Test để ép tải Hàng đợi 4 tầng của Thành.

---

## 🚀 TUẦN 3: THI THỬ (MF-02) & CƠ CHẾ RÀNG BUỘC
*Mục tiêu: Thi có áp lực thời gian, chống spam thi thử và sinh câu hỏi đào sâu*

**1. Lê Vũ Hoàng (FE Lead)**
- `[FE-T3.1]` Dựng UI Màn hình Thi thử.
- `[FE-T3.2]` Code chốt chặn "Voice-First Gate": Khóa hoàn toàn ô nhập Text, ép sinh viên phải thu âm xong mới được mở ô Text để sửa.
- `[FE-T3.3]` Vẽ Biểu đồ Năng lực nhận thức (Bloom Radar Chart) bằng Recharts/Chart.js để hiện ở bảng điểm.

**2. Phạm Nguyễn Đăng Hải (BE & FE Support)**
- `[BE-T3.1]` Viết API "Quota Guard": Chống Spam, giới hạn sinh viên thi thử không quá 3 lần/môn/ngày.
- `[BE-T3.2]` Cấu hình Redis Cache đếm số lượt thi nhanh chóng để giảm tải DB.

**3. Nguyễn Quang Thành (Lead, BE)**
- `[BE-T3.3]` Viết Server-side Timer (Kích hoạt bộ đếm giờ ở Backend để phòng trường hợp sinh viên hack JS đổi đồng hồ trên Web).
- `[BE-T3.4]` Code cơ chế Fallback (Nếu server quá tải, AI bận -> thông báo cho SV xem điểm sau ở Lịch sử thi).

**4. Nguyễn Trọng Tốt (QA, AI/BE)**
- `[AI-T3.1]` Nâng cấp Logic AI: Nếu tổng điểm câu trước đạt 4.0 - 8.0, gọi API Gemini đẻ thêm 1 "Câu hỏi đào sâu A2" (Deep-dive) bám vào lỗi sai của sinh viên.
- `[QA-T3.2]` Viết kịch bản Hồi quy (Regression Test) đảm bảo code MF-02 không làm hỏng MF-01.

---

## 🚀 TUẦN 4: THI THẬT PHÒNG LAB (MF-04), BẢO MẬT & NGHIỆM THU
*Mục tiêu: Khóa an ninh chống gian lận, Giảng viên hậu kiểm và Bàn giao Source Code*

**1. Lê Vũ Hoàng (FE Lead)**
- `[FE-T4.1]` Code tính năng Kiosk Lockdown: Bắt sự kiện Window `onblur` cảnh báo khi SV nhấn F12, đổi Tab, copy/paste.
- `[FE-T4.2]` Dựng giao diện Cổng Hậu Kiểm (Lecturer Audit Portal) có Audio Player hiển thị Timestamp tương ứng với văn bản.

**2. Phạm Nguyễn Đăng Hải (BE & FE Support)**
- `[BE-T4.1]` Viết API lấy danh sách bài thi cần Hậu kiểm.
- `[BE-T4.2]` Viết API lấy danh sách sinh viên thi theo số máy (Lab PC).
- `[FE-T4.3]` *Hỗ trợ FE:* Giúp Hoàng code tính năng tua Audio Player trên giao diện Hậu kiểm.

**3. Nguyễn Quang Thành (Lead, BE)**
- `[BE-T4.3]` Code API Khóa điểm 1 chiều (One-Way Lock): Cập nhật `is_locked = true` vĩnh viễn sau khi giảng viên chốt, không ai được sửa nữa.
- `[BE-T4.4]` Viết Module xuất Excel (Export) danh sách điểm thi nộp cho phòng Khảo thí (FAP).
- `[MNG-T4.1]` Review Code Final, đóng băng nhánh `develop`, tạo tag Release trên Git. Chuẩn bị Slide báo cáo Final Defense.

**4. Nguyễn Trọng Tốt (QA, AI/BE)**
- `[AI-T4.1]` Setup Cloudflare R2 / AWS S3 nhận file `.webm` thi thật của sinh viên đẩy lên (Lưu dạng `STT_MSSV.webm`).
- `[BE-T4.5]` Code thuật toán băm mã hóa SHA-256 niêm phong file Audio thi thật chống cắt ghép/sửa đổi.
- `[QA-T4.2]` Rà soát rủi ro, chạy System/UAT Test. Viết File Test Report (Báo cáo Nghiệm thu) theo chuẩn tài liệu Report 2.

---
*Ghi chú: Toàn team bắt buộc Review tiến độ và ghép code API mỗi 3 ngày 1 lần để tránh Conflict.*
