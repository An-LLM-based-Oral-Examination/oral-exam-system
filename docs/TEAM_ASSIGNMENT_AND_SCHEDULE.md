# BẢNG PHÂN CÔNG NHIỆM VỤ & LỘ TRÌNH AGILE 4 TUẦN
**Dự án:** An LLM-based Oral Examination  
**Đội hình (4 thành viên):** 3 Backend (BE) - 1 Frontend (FE)  
**Quy tắc vận hành:** Review & Báo cáo tiến độ (Daily/Sync) mỗi 3 ngày 1 lần.  
**Mục tiêu:** Khớp toàn bộ 4 Core Main Flows (MF-01 đến MF-04) từ Zero đến Production-Ready.

---

## 👨‍💻 PHẦN 1: MA TRẬN PHÂN CÔNG NHIỆM VỤ (TASK ASSIGNMENT)

### 1. FRONTEND DEVELOPER (1 Người - Full UI/UX & Client Logic)
Đóng vai trò Frontend Lead, tiêu thụ API từ 3 bạn BE.
* **Nhiệm vụ lõi:** Setup UI Kit (TailwindCSS + Shadcn/ui), State Management (Zustand/Redux). Tích hợp Web Speech API (Speech-to-Text & Text-to-Speech) để thu âm và đọc câu hỏi mô phỏng.
* **Chia Task theo Luồng:**
  - **MF-01 & 02:** UI phòng thi ảo, bộ đếm ngược (Countdown Timer), màn hình đệm 60s (Code-Switching) và vẽ Biểu đồ Bloom Radar.
  - **MF-03:** Form tạo câu hỏi, validation Zod chống nhập sai tổng điểm Rubric 10.0.
  - **MF-04:** Cơ chế Lockdown trình duyệt (chặn F12, Fullscreen) và Giao diện nghe Audio Audit cho Giảng viên.

### 2. BACKEND DEVELOPER 1 (Lead BE / System Architect)
Thiết lập hạ tầng chịu tải và luồng thi thật căng thẳng nhất.
* **Nhiệm vụ lõi:** Cấu hình kiến trúc `.NET 8 Clean Architecture` (DI, Middleware, JWT Auth). Setup hạ tầng thời gian thực SignalR.
* **Chia Task theo Luồng:**
  - **MF-01:** Hàng đợi 4 tầng (In-memory Bounded Channel) và cơ chế thử lại (Polly Retry), Dead-Letter Queue (DLQ).
  - **MF-04:** API tạo phòng thi Lab, Audit Log, API khóa điểm 1 chiều (One-Way Lock) sau khi Giảng viên hậu kiểm.

### 3. BACKEND DEVELOPER 2 (AI Integration & Cloud Storage)
Giao tiếp với Trí tuệ nhân tạo (Gemini) và lưu trữ file ghi âm.
* **Nhiệm vụ lõi:** Viết Service gọi API Google Gemini 1.5 Flash/Pro. Setup Cloudflare R2 / AWS S3 nhận file audio.
* **Chia Task theo Luồng:**
  - **MF-01 & 02:** Ép khuôn AI trả về JSON chuẩn, chấm điểm theo Barem. Tự động đẻ câu hỏi chuyên sâu (A2) nếu điểm 4.0 - 8.0.
  - **MF-04:** Băm mã hóa SHA-256 niêm phong file audio bài thi sinh viên.

### 4. BACKEND DEVELOPER 3 (Database Architect & CRUD Master)
Tương tác PostgreSQL, Entity Framework Core và các API quản trị dữ liệu lớn.
* **Nhiệm vụ lõi:** Thiết kế ERD, Migrations, tối ưu Index. Viết truy vấn hiệu năng cao, tránh N+1.
* **Chia Task theo Luồng:**
  - **MF-03:** API tạo/sửa/xóa Câu hỏi và Barem. Bảo đảm toàn vẹn dữ liệu ACID Transactions.
  - **MF-02:** Logic "Quota Guard" (giới hạn 3 lần thi/môn/ngày). Xuất file Excel bảng điểm.

---

## 📅 PHẦN 2: LỘ TRÌNH THỰC THI 4 TUẦN (AGILE SPRINT PLAN)

### 🏃 TUẦN 1: KHỞI TẠO NỀN TẢNG & NGÂN HÀNG CÂU HỎI (MF-03)
* **Review Lần 1 (Ngày 3): Móng Hệ Thống**
  * **BE 1:** Hoàn tất CI/CD, Swagger, JWT Auth và Base Controller.
  * **BE 2:** Mở cổng API upload file thử nghiệm lên Cloudflare R2.
  * **BE 3:** Chạy Migrations thành công, đẩy ERD 3NF lên PostgreSQL, sinh Seeder.
  * **FE 1:** Setup xong UI Kit (Tailwind + Shadcn), Layout chung, login page.
* **Review Lần 2 (Ngày 6): Chốt chặn MF-03**
  * **BE 3:** Hoàn thiện API tạo/sửa Câu hỏi & Barem Rubric (Bảo đảm Validation tổng điểm 10.0).
  * **BE 1:** Mở ACID Transactions cho hàm lưu của BE 3.
  * **FE 1:** Hoàn thiện màn hình "Question Bank Studio", tích hợp API.
  * **BE 2:** Khởi tạo SDK kết nối Google Gemini API.

### 🏃 TUẦN 2: THI TƯƠNG TÁC ĐA PHƯƠNG THỨC & TÍCH HỢP AI (MF-01)
* **Review Lần 3 (Ngày 9): Khớp nối STT/TTS & AI Core**
  * **FE 1:** Tích hợp Web Speech API, FE tự động thu âm và hiện chữ (Live Transcript). Đếm ngược 60s.
  * **BE 2:** Cấu hình Gemini Prompt (Chain-of-Thought), ép AI trả về Structured Output.
  * **BE 1:** Setup xong SignalR Hub (PracticeHub).
* **Review Lần 4 (Ngày 12): Hoàn thiện MF-01**
  * **BE 1:** Lên móng Hàng đợi 4 tầng (In-memory Channel + Polly) hứng bài nộp.
  * **BE 3:** Viết API lưu Lịch sử luyện tập (Practice History).
  * **FE 1:** Sau khi nộp, nhận tín hiệu SignalR từ BE và hiển thị Scorecard Modal.

### 🏃 TUẦN 3: THI THỬ BẤM GIỜ & ÁP LỰC CHỊU TẢI (MF-02)
* **Review Lần 5 (Ngày 15): Chốt chặn Logic Cao cấp**
  * **BE 3:** Code thành công "Quota Guard" (giới hạn 3 lần thi/môn/ngày).
  * **BE 2:** Tinh chỉnh AI Logic sinh "Câu hỏi đào sâu A2" (điểm 4.0-8.0).
  * **BE 1:** Tích hợp bộ đếm giờ (Server-side Timer) chống gian lận.
* **Review Lần 6 (Ngày 18): Chạy E2E MF-02**
  * **FE 1:** Giao diện Thi thử có Đồng hồ kép. Voice-First Gate (Bắt buộc thu âm xong mới mở ô gõ phím).
  * **Toàn Team:** Đóng vai sinh viên test toàn bộ luồng thi thử, tạt tải hệ thống.

### 🏃 TUẦN 4: AN NINH PHÒNG LAB (MF-04) & ĐÓNG GÓI SẢN PHẨM
* **Review Lần 7 (Ngày 21): Khóa an ninh & Niêm phong Audio**
  * **FE 1:** Kiosk Lockdown (Cảnh báo F12, đổi tab, Copy/Paste).
  * **BE 2:** Băm mã hóa SHA-256 niêm phong audio `STT_MSSV.webm`.
  * **BE 3:** Code xong API Cổng Hậu Kiểm Giảng viên (Lecturer Audit Portal).
* **Review Lần 8 (Ngày 24): Hậu Kiểm & Khóa Điểm 1 Chiều**
  * **BE 1:** Khóa Điểm (One-Way Lock). Xuất Excel bảng điểm.
  * **FE 1:** Làm màn hình Hậu kiểm cho giảng viên: Audio player nhảy timestamp, form sửa điểm và giải trình.
* **Review Lần 9 (Ngày 27 - 28): TỔNG DUYỆT & NGHIỆM THU (Launch)**
  * Toàn team dọn dẹp Code (Refactor). Xử lý bug. Chuẩn bị Slide bảo vệ.
