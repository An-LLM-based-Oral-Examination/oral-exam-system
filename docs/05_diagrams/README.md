# SƠ ĐỒ HOẠT ĐỘNG & KIẾN TRÚC HỆ THỐNG (DRAW.IO)

> **Đồ Án Tốt Nghiệp FA26SE166 — Hệ Thống Luyện Thi & Đánh Giá Vấn Đáp Bằng LLM**  
> **Nhóm thực hiện (4 thành viên):** Nguyễn Quang Thành (Team Leader & Lead Backend Architect), Nguyễn Trọng Tốt (Backend Developer, AI Specialist & QA Lead), Nguyễn Đăng Hải (DB Specialist & Frontend Developer), Lê Vũ Hoàng (Lead Frontend Architect & Fullstack Coordinator)  
> **Thư mục này lưu trữ độc quyền các tệp sơ đồ kỹ thuật định dạng `.drawio` chuẩn hóa, dùng để mở và hiệu chỉnh trên Diagrams.net / Draw.io.**

---

## 📂 Danh Sách Các Tệp Sơ Đồ Draw.io

### 1. Tệp Master Draw.io (Gộp chung cả 4 luồng trong 4 Tabs):
- 📁 **[CAPSTONE_FA26SE166_4_MAINFLOWS_ACTIVITY_DIAGRAMS.drawio](./CAPSTONE_FA26SE166_4_MAINFLOWS_ACTIVITY_DIAGRAMS.drawio)**  
  *Mở bằng app Diagrams.net / Draw.io sẽ thấy 4 tab chuyển đổi tương ứng 4 Main Flows ở thanh tab góc dưới.*

### 2. Tệp Draw.io Độc Lập Cho Từng Luồng Nghiệp Vụ:
- 🎨 **MF-01:** [MF01_Interactive_Practice.drawio](./MF01_Interactive_Practice.drawio) — *Sinh viên Luyện tập Vấn đáp Tương tác Tự do (35 nút, 4 phân làn chuẩn OMG UML 2.5)*
- 🎨 **MF-02:** [MF02_Timed_Mock_Exam.drawio](./MF02_Timed_Mock_Exam.drawio) — *Sinh viên Thi thử Vấn đáp Bấm giờ Voice-First có Hạn ngạch (32 nút, 4 phân làn chuẩn OMG UML 2.5)*
- 🎨 **MF-03:** [MF03_Question_Bank_Rubric.drawio](./MF03_Question_Bank_Rubric.drawio) — *Giảng viên Dùng AI Sinh Đề từ FLM Syllabus theo CLO, Barem 10.0đ, Tick chọn 2 kho & Trưởng Bộ Môn Phê duyệt (35 nút, 4 phân làn chuẩn OMG UML 2.5)*
- 🎨 **MF-04:** [MF04_Lab_Exam_Audit.drawio](./MF04_Lab_Exam_Audit.drawio) — *Thi Thật Phòng Lab Kiosk Lockdown, Stream Audio R2, AI Chấm Ngầm, Cổng Hậu Kiểm Phân Nhóm, Giảng Viên Công Bố Điểm One-Way Lock & Phúc Khảo Nội Bộ (42 nút, 4 phân làn chuẩn OMG UML 2.5)*

### 3. Tệp Kiến Trúc Hệ Thống Tổng Thể:
- 🏛️ **[KIEN_TRUC_HE_THONG.drawio](./KIEN_TRUC_HE_THONG.drawio)** — *Sơ đồ Kiến trúc Toàn diện: Client (React 19), Ingress Reverse Proxy, Core API .NET 8 Clean Architecture, Bounded Channel 1000 slots RAM, AI Engine (Gemini 1.5 + Whisper), PostgreSQL 16+ (30 bảng 3NF), Cloudflare R2.*

---

## 🛠️ Hướng Dẫn Mở & Hiệu Chỉnh Tệp `.drawio`

1. **Cách 1 (Khuyên dùng - Nhanh nhất):**
   - Truy cập trang web: [https://app.diagrams.net](https://app.diagrams.net)
   - Chọn **Open Existing Diagram** $\rightarrow$ Trỏ tới tệp `.drawio` tương ứng trong thư mục này.
2. **Cách 2 (Mở trực tiếp trên VS Code):**
   - Cài đặt tiện ích mở rộng **Draw.io Integration** (tác giả: Henning Dieterichs) trên VS Code.
   - Click chuột vào tệp `.drawio` để xem và chỉnh sửa trực quan ngay trong IDE.
3. **Cách 3 (Phần mềm Draw.io Desktop):**
   - Cài đặt phần mềm Draw.io Desktop từ [https://www.drawio.com](https://www.drawio.com).
   - Double-click vào tệp `.drawio` để mở.
