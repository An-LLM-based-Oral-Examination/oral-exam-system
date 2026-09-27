# BẢNG PHÂN RÃ NHIỆM VỤ & YÊU CẦU BÁO CÁO CHI TIẾT (WBS)
**Dự án:** An LLM-based Oral Examination  
**Cấu trúc Team Mới (2 FE - 2 BE):** Hoàng (FE Lead), Hải (FE), Thành (BE Lead), Tốt (BE/AI/QA).

---

## 🚀 TUẦN 1: NỀN TẢNG HỆ THỐNG & NGÂN HÀNG CÂU HỎI (MF-03)
*Mục tiêu: Dựng móng dự án, thiết kế DB và hoàn tất giao diện, API tạo đề thi và barem 10.0*

**1. Lê Vũ Hoàng (FE Lead)**
- **Công việc (Do):** Khởi tạo Repo Vite React TS, cài đặt TailwindCSS, Shadcn/ui. Xây dựng Layout gốc (Sidebar, Header, Protected Routes). Dựng màn hình Đăng nhập & Phân quyền.
- **Báo cáo (Report):** Mã nguồn FE build thành công 0 lỗi. Màn hình Đăng nhập và Layout hiển thị chuẩn xác trên trình duyệt, chuyển trang không lag.

**2. Phạm Nguyễn Đăng Hải (FE Developer)**
- **Công việc (Do):** Dựng màn hình Danh sách Câu hỏi. Code Form tạo Câu hỏi & Rubric 10.0. Gắn thư viện Zod Validation để khóa nút Submit nếu tổng điểm các tiêu chí khác 10.0.
- **Báo cáo (Report):** Demo trực tiếp form tạo câu hỏi. Chứng minh form chặn thành công người dùng nhập điểm sai (hiện thông báo đỏ).

**3. Nguyễn Quang Thành (Lead, BE)**
- **Công việc (Do):** Setup Clean Architecture, JWT Auth, Global Exception. Thiết kế CSDL PostgreSQL (ERD 3NF), chạy EF Core Migrations và sinh Seed Data (tạo SV mẫu). Chốt OpenAPI.
- **Báo cáo (Report):** Swagger chạy thành công. DB tạo đủ 11 bảng theo ERD gốc. API Auth trả về token hợp lệ.

**4. Nguyễn Trọng Tốt (QA, AI/BE)**
- **Công việc (Do):** Code API CRUD Câu hỏi & Barem điểm (Dùng ACID Transaction). Viết Unit Test (xUnit/Moq) chặn payload tổng điểm != 10.
- **Báo cáo (Report):** Demo gọi API tạo câu hỏi thành công trên Swagger. Chạy Test Runner hiển thị 100% Pass các kịch bản của MF-03.

---

## 🚀 TUẦN 2: THI TƯƠNG TÁC (MF-01) & LÕI TRÍ TUỆ NHÂN TẠO
*Mục tiêu: Ghi âm qua web, AI chấm điểm tự động và gửi kết quả thời gian thực*

**1. Lê Vũ Hoàng (FE Lead)**
- **Công việc (Do):** Dựng UI Màn hình Luyện tập. Tích hợp Web Speech API (STT & TTS) để Web tự đọc câu hỏi và thu âm giọng nói SV parse ra chữ realtime.
- **Báo cáo (Report):** Demo trực tiếp việc ấn nút, Web cất tiếng đọc câu hỏi. Đọc vào micro và Web hiện chữ tức thì (Live Transcript).

**2. Phạm Nguyễn Đăng Hải (FE Developer)**
- **Công việc (Do):** Code Màn hình đệm 60s đếm ngược cho SV sửa từ vựng. Vẽ Scorecard Modal nhận dữ liệu từ SignalR để hiển thị bảng điểm.
- **Báo cáo (Report):** Demo màn hình đệm tự động nhảy số từ 60 về 0. Khi BE bắn tín hiệu, Modal điểm tự động bật lên.

**3. Nguyễn Quang Thành (Lead, BE)**
- **Công việc (Do):** Xây dựng Hàng đợi 4 tầng (Bounded Channel, Polly Retry) hứng bài nộp. Setup SignalR Hub đẩy điểm realtime về FE. API lưu lịch sử bài nộp.
- **Báo cáo (Report):** Giải trình cơ chế Hàng đợi (Code review). Bắn API qua Postman và chứng minh SignalR socket nhận được điểm.

**4. Nguyễn Trọng Tốt (QA, AI/BE)**
- **Công việc (Do):** Kết nối Google Gemini 1.5 Flash. Tinh chỉnh Chain-of-Thought (CoT) Prompt ép Gemini chấm theo Rubric trả về JSON. Chạy Load Test (Tạt tải) Hàng đợi của Thành.
- **Báo cáo (Report):** Cho xem Log AI phân tích từng tiêu chí Rubric và trả JSON chuẩn. Báo cáo báo cáo kết quả tạt tải 1000 req/s.

---

## 🚀 TUẦN 3: THI THỬ (MF-02) & CƠ CHẾ RÀNG BUỘC
*Mục tiêu: Áp lực thời gian, chống spam thi thử và sinh câu hỏi đào sâu*

**1. Lê Vũ Hoàng (FE Lead)**
- **Công việc (Do):** UI Thi thử. Code chốt chặn "Voice-First Gate": Khóa hoàn toàn ô nhập Text, ép sinh viên phải thu âm xong mới được mở ô Text để sửa bài.
- **Báo cáo (Report):** Demo hành vi khóa bàn phím thành công khi SV chưa thu âm.

**2. Phạm Nguyễn Đăng Hải (FE Developer)**
- **Công việc (Do):** Làm đồng hồ đếm ngược thời gian làm bài. Vẽ Biểu đồ Năng lực nhận thức (Bloom Radar Chart) bằng Recharts/Chart.js tại Bảng điểm.
- **Báo cáo (Report):** Demo đồng hồ đếm lùi và Biểu đồ nhện (Radar Chart) hiện điểm đẹp mắt, đúng tỷ lệ.

**3. Nguyễn Quang Thành (Lead, BE)**
- **Công việc (Do):** API "Quota Guard" chặn thi quá 3 lần/ngày qua Redis Cache. Viết Server-side Timer (Bộ đếm giờ ngầm) chống SV hack đồng hồ FE.
- **Báo cáo (Report):** Demo gọi API lần thứ 4 bị từ chối cấp quyền thi (HTTP 429/403). Giải trình cơ chế chống gian lận thời gian.

**4. Nguyễn Trọng Tốt (QA, AI/BE)**
- **Công việc (Do):** Nâng cấp AI: Nếu điểm câu trước đạt 4.0 - 8.0, đẻ thêm "Câu hỏi đào sâu A2". Viết kịch bản Test Hồi quy (Regression).
- **Báo cáo (Report):** Demo AI tự động sinh câu hỏi móc nối bám vào lỗi sai của SV. Cung cấp Test Report cho luồng MF-01 & MF-02.

---

## 🚀 TUẦN 4: THI THẬT (MF-04), BẢO MẬT & BÀN GIAO SẢN PHẨM
*Mục tiêu: Khóa an ninh chống gian lận, Giảng viên hậu kiểm và Nghiệm thu toàn hệ thống*

**1. Lê Vũ Hoàng (FE Lead)**
- **Công việc (Do):** Kiosk Lockdown: Bắt sự kiện Window `onblur`, cảnh báo đỏ 3 cấp độ khi SV nhấn F12, đổi Tab, Copy/Paste bài thi.
- **Báo cáo (Report):** Mở trình duyệt ẩn danh, ấn F12 hoặc đổi Tab và chứng minh màn hình cảnh báo hiện lên, hệ thống tự niêm phong bài thi (đình chỉ).

**2. Phạm Nguyễn Đăng Hải (FE Developer)**
- **Công việc (Do):** Dựng UI Cổng Hậu Kiểm (Lecturer Audit Portal). Gắn Audio Player nhảy Timestamp đồng bộ với văn bản Transcript. 
- **Báo cáo (Report):** Bấm Play Audio và tua đến giây 15, chứng minh dòng Transcript tương ứng được bôi đậm (Highlight).

**3. Nguyễn Quang Thành (Lead, BE)**
- **Công việc (Do):** API Lấy danh sách bài hậu kiểm. API Khóa điểm 1 chiều (One-Way Lock) - cập nhật `is_locked = true`. Xuất file Excel bảng điểm đẩy về FAP.
- **Báo cáo (Report):** Thử gọi API sửa điểm khi `is_locked = true` và chứng minh hệ thống từ chối (HTTP 403). File Excel tải về mở ra không bị lỗi Font.

**4. Nguyễn Trọng Tốt (QA, AI/BE)**
- **Công việc (Do):** Nhận file audio thi `.webm`, lưu lên Cloudflare R2. Code băm mã hóa SHA-256 niêm phong audio. Rà soát Bug Blocker, xuất Test Report.
- **Báo cáo (Report):** Thử sửa lén file Audio trên server và chứng minh mã băm SHA-256 thay đổi, báo lỗi bảo mật (Audit Failed). Nộp file báo cáo Test UAT.
