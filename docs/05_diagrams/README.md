# SƠ ĐỒ HOẠT ĐỘNG & KIẾN TRÚC HỆ THỐNG (DRAW.IO)

> **Đồ Án Tốt Nghiệp Capstone: FA26SE166 — Hệ Thống Luyện Thi & Đánh Giá Vấn Đáp Bằng LLM**  
> **Chuyên ngành:** Kỹ thuật Phần mềm (Software Engineering - SE) — Đại học FPT TP.HCM (FPT SG)  
> **Nhóm kỹ sư thực hiện (4 thành viên):**  
> - 🧑 **Nguyễn Quang Thành:** Team Leader & Lead Backend Architect  
> - 🧑 **Nguyễn Trọng Tốt:** Backend Developer, AI Specialist & QA Lead  
> - 🧑 **Phạm Nguyễn Đăng Hải:** DB Specialist & Frontend Developer  
> - 🧑 **Lê Vũ Hoàng:** Lead Frontend Architect & Fullstack Coordinator  
> **Thư mục này lưu trữ độc quyền các tệp sơ đồ kỹ thuật định dạng `.drawio` chuẩn hóa OMG UML 2.5 Activity Diagrams, dùng để mở và hiệu chỉnh trên Diagrams.net / Draw.io.**

---

## 📊 BẢNG TỔNG HỢP THÔNG SỐ KIỂM ĐỊNH 5 FILE DRAW.IO (ACTIVITY DIAGRAMS)

Toàn bộ 5 tệp `.drawio` đã được sinh và kiểm định tự động qua 5 cổng kiểm định nghiêm ngặt (UTF-8 without BOM, XML Well-Formedness, 0% Bounding-Box Overlaps, 100% Swimlane Containment, 100% Decision Guard Labels và 100% Backward Reachability to Final Nodes):

| Tệp Sơ Đồ (.drawio) | Main Flow Đại Diện | Số Phân Làn (Swimlanes) | Số Đỉnh (Vertices) | Số Cạnh (Edges) | Số Nút Kết Thúc (Finals) | Trạng Thái Kiểm Định Hình Học & Cú Pháp |
|:---|:---|:---:|:---:|:---:|:---:|:---:|
| **`MF01_Interactive_Practice.drawio`** | **MF-01: Luyện Tập Vấn Đáp Tương Tác** | 4 làn | 43 nodes | 48 edges | 5 finals | ✅ **PASS 100%** (0 collisions, 0 dead-ends) |
| **`MF02_Timed_Mock_Exam.drawio`** | **MF-02: Thi Thử Tính Giờ Có Hạn Ngạch** | 4 làn | 45 nodes | 50 edges | 3 finals | ✅ **PASS 100%** (0 collisions, 0 dead-ends) |
| **`MF03_Question_Bank_Rubric.drawio`** | **MF-03: Ngân Hàng Câu Hỏi & Rubric Studio** | 5 làn | 60 nodes | 68 edges | 2 finals | ✅ **PASS 100%** (0 collisions, 0 dead-ends) |
| **`MF04_Lab_Exam_Audit.drawio`** | **MF-04: Thi Thật Phòng Lab & Khóa Điểm** | 5 làn | 84 nodes | 94 edges | 2 finals | ✅ **PASS 100%** (0 collisions, 0 dead-ends) |
| **`CAPSTONE_FA26SE166_4_MAINFLOWS_ACTIVITY_DIAGRAMS.drawio`** | **Master Tích Hợp (Đầy đủ 4 Tabs MF-01..MF-04)** | 18 làn (4 tabs) | 232 nodes | 260 edges | 12 finals | ✅ **PASS 100%** (Tích hợp chuẩn OMG UML 2.5) |

---

## 📂 Danh Sách Chi Tiết Các Tệp Sơ Đồ Draw.io

### 1. Tệp Master Draw.io Tích Hợp 4 Tabs:
- 📁 **[`CAPSTONE_FA26SE166_4_MAINFLOWS_ACTIVITY_DIAGRAMS.drawio`](./CAPSTONE_FA26SE166_4_MAINFLOWS_ACTIVITY_DIAGRAMS.drawio)**  
  *Mở bằng app Diagrams.net / Draw.io sẽ thấy 4 tab chuyển đổi tương ứng 4 Main Flows ở thanh tab góc dưới. Tệp chứa tổng cộng 232 vertices, 260 edges, phân chia trên 18 Swimlanes chuẩn OMG UML 2.5.*

### 2. Tệp Draw.io Độc Lập Cho Từng Luồng Nghiệp Vụ:
- 🎨 **MF-01:** [`MF01_Interactive_Practice.drawio`](./MF01_Interactive_Practice.drawio)  
  *Sinh viên Luyện tập Vấn đáp Tương tác Tự do: 4 Swimlanes (`Student`, `React 19 Client UI & Audio/STT Engine`, `.NET 8 Backend API & PracticeHub`, `Gemini AI Grading & Worker Queue`). Thể hiện trọn vẹn 2 chế độ: Per-Question On-Demand (Anti-3-Consecutive, Follow-up A2 ranh giới $4.0 \le \text{Score} \le 8.0$, 1–5 câu mặc định 2 câu do Admin cấu hình) và Full-Session Progressive (3–10 câu chia đều Dễ $\to$ Trung bình $\to$ Khó, cấm Follow-up, thiếu câu trả HTTP 400 Bad Request), đệm sửa thuật ngữ SE (10–300s, mặc định 60s), Timeout 5 phút không tương tác (HTTP 410 Gone, bảo toàn điểm câu đã làm), DLQ do Admin giám sát & replay, không lưu audio R2 (43 vertices, 48 edges, 5 final nodes).*
- 🎨 **MF-02:** [`MF02_Timed_Mock_Exam.drawio`](./MF02_Timed_Mock_Exam.drawio)  
  *Sinh viên Thi thử Vấn đáp Bấm giờ: 4 Swimlanes (`Student`, `React 19 Client UI & Voice-First Gate`, `.NET 8 Backend API & Quota Handler`, `Gemini AI Grading Engine`). Kiểm soát Daily Quota động theo môn do Admin cấu hình (`Course.MaxMockExamsPerDay` $\to$ HTTP 429 Too Many Requests; Trưởng BM cấu hình cấu trúc đề và thời lượng thi thử), rút đề từ kho `practice_questions` (cô lập kho `exam_questions`), Voice-First Gate khóa cứng ô gõ phím, tùy chọn Có/Không Follow-up ngữ cảnh bài nói (không phụ thuộc điểm số), Auto-Submit 00:00 (câu chưa làm bỏ trống, không chấm), DLQ do Admin giám sát & replay, không lưu audio R2 & Instant Feedback Scorecard theo CLO (45 vertices, 50 edges, 3 final nodes).*
- 🎨 **MF-03:** [`MF03_Question_Bank_Rubric.drawio`](./MF03_Question_Bank_Rubric.drawio)  
  *Quản lý Ngân hàng Đề 2 Luồng & Rubric Studio 10.0: 5 Swimlanes tách biệt rõ ràng ranh giới trách nhiệm (`Lecturer - Tác giả đề`, `Question & Rubric Studio UI`, `.NET 8 Backend API, CLO Adapter & R2 Storage`, `Gemini AI Generator & Simulator`, `Department Head - Người phê duyệt`). Phân tách độc lập 2 luồng tạo câu hỏi: Luồng Luyện tập/Thi thử (`practice_questions` theo Course CLOs 3NF) và Luồng Thi thật (`exam_questions` bám sát Đề thi mẫu / Ma trận Cấu trúc Đề do Trưởng Bộ Môn ban hành); hỗ trợ AI sinh nháp hoặc soạn thủ công (`source == "manual"` đính kèm ảnh/sơ đồ kỹ thuật PNG/JPG/WEBP $\le$ 5MB, kiểm tra Magic Bytes HTTP 413/415, lưu `image_url` + `image_description` chấm Multimodal CoT), Barem Rubric $\sum \equiv 10.0$đ và SampleAnswer $\ge 50$ ký tự (HTTP 422), AI Calibration Simulator, và máy trạng thái phê duyệt của Trưởng Bộ Môn (`DRAFT` $\to$ `SUBMITTED_FOR_REVIEW` $\to$ `APPROVED` / `NEEDS_REVISION` / `REJECTED`) (60 vertices, 68 edges, 2 final nodes).*
- 🎨 **MF-04:** [`MF04_Lab_Exam_Audit.drawio`](./MF04_Lab_Exam_Audit.drawio)  
  *Thi Thật Phòng Lab Kiosk, Công Bố Điểm & Phúc Khảo Nội Bộ 5 Bước (Phân quyền 6 Roles): 5 Swimlanes chuẩn hóa (`Student Examinee`, `Lab Workstation Kiosk Lockdown & R2 Seal`, `.NET 8 Backend API, BoundedChannel & AI Worker`, `Exam Officer, Proctor & Lecturer Auditor`, `Department Head`). Trưởng Bộ Môn nắm 100% thẩm quyền cấu hình Follow-up 1–5 câu (mặc định 2) cho đề/ca thi thật; Cán bộ Khảo thí (`Examination Officer`) tạo kỳ thi/ca thi phòng máy, phân công Giám thị/Giám khảo, phân bổ đề không trùng và mở xuất bản đề thi ca thi (`is_exam_published = true`); Giám thị (`Proctor`) CHỈ điểm danh đối chiếu thẻ SV/CCCD theo số máy SeatNumber 1–40 và báo cáo vi phạm an ninh phòng thi vào `exam_anti_cheat_logs` (lưu IP máy trạm vào cột `ip_address`). Fullscreen Kiosk Lockdown (chặn Alt+Tab, Win, F11, F12, DevTools; blur > 3 lần đình chỉ thi), Mic-Check 30s $\ge 60$dB, stream audio R2 định danh bất biến `STT_MSSV.webm` + mã băm SHA-256 niêm phong tại Kiosk. Nộp bài Persist-First $< 100$ms trạng thái `SUBMITTED` (Kiosk khóa an toàn, 0% hiện điểm liền, 0% khiếu nại tại chỗ), hàng đợi BoundedChannel 1,000 slots RAM + Polly Retry (2s-4s-8s) + DLQ 5m (do Cán bộ Khảo thí giám sát & replay cho luồng thi thật). Chốt chặn AI Doubt Guard phân loại 2 nhóm bài thi, Giảng viên hậu kiểm Evidence Panel (Waveform Player 1.0x–1.5x) sửa điểm kèm `override_reason`. Khi 100% SV có điểm, Giảng viên / Cán bộ Khảo thí Công Bố Điểm 1 lần duy nhất $\to$ kích hoạt One-Way Lock (`is_locked = true` chặn HTTP 403), xuất Excel (.xlsx) + PDF ký số, và quy trình phúc khảo nội bộ 5 bước (`1. Sinh viên nộp AppealRequest lên Khảo thí` $\to$ `2. Cán bộ Khảo thí thẩm tra hợp lệ & chuyển Trưởng BM` $\to$ `3. Trưởng BM phân công GV độc lập` $\to$ `4. GV độc lập chấm lại trên Evidence Panel` $\to$ `5. Trưởng BM phê duyệt quyết định cuối cùng qua ngoại lệ One-Way Lock`) (84 vertices, 94 edges, 2 final nodes).*

### 3. Tệp Kiến Trúc Hệ Thống Toàn Cảnh:
- 🏛️ **[`KIEN_TRUC_HE_THONG.drawio`](./KIEN_TRUC_HE_THONG.drawio)**  
  *Sơ đồ Kiến trúc Toàn diện: Client (React 19 SPA + Router DOM v7), Ingress Reverse Proxy Nginx, Core API .NET 8 Clean Architecture 4 tầng (6 Roles RBAC, chuỗi phân cấp học thuật 4 tầng và ca thi trong lớp dùng mã OTP xoay vòng 5 phút), BoundedChannel 1000 slots RAM, AI Engine (Gemini 1.5 Flash/Pro + Whisper Large-v3), PostgreSQL 16+ (38 bảng chuẩn hóa 3NF), Cloudflare R2.*

---

## 🛠️ Hướng Dẫn Mở, Hiệu Chỉnh & Kiểm Định Tự Động

### 1. Cách Mở & Hiệu Chỉnh Sơ Đồ Draw.io
1. **Cách 1 (Khuyên dùng - Nhanh nhất qua Trình duyệt Web):**
   - Truy cập: [https://app.diagrams.net](https://app.diagrams.net)
   - Chọn **Open Existing Diagram** $\rightarrow$ Trỏ tới tệp `.drawio` tương ứng trong thư mục `05_Source_Code/docs/05_diagrams/`.
2. **Cách 2 (Mở trực tiếp trên VS Code):**
   - Cài đặt extension **Draw.io Integration** (tác giả: Henning Dieterichs) trên Visual Studio Code.
   - Click chuột vào bất kỳ tệp `.drawio` nào để xem và chỉnh sửa đồ họa trực tiếp trong IDE.
3. **Cách 3 (Phần mềm Draw.io Desktop):**
   - Tải và cài đặt Draw.io Desktop từ [https://www.drawio.com](https://www.drawio.com).
   - Mở trực tiếp các tệp `.drawio` trên máy tính.

### 2. Bộ Lệnh Kiểm Định Cú Pháp XML & Hình Học Tự Động (Hard Verification Gate)
Sau khi hiệu chỉnh bất kỳ tệp `.drawio` nào, kiểm thử viên có thể chạy độc lập các script kiểm định tự động bằng PowerShell / Python:

```powershell
# 1. Kiểm tra 5 tiêu chí chấp thuận (UTF-8, XML, 0% Overlaps, Lane Containment, Decision Guards)
python "D:\Đồ Án\.agents\teamwork\worker_m1\verify_all_diagrams.py"

# 2. Kiểm toán ngữ nghĩa OMG UML 2.5 chuyên sâu (Fork-Join balance, Out-Degree, Backward Reachability)
python "D:\Đồ Án\.agents\teamwork\reviewer_m1_1\deep_audit.py"

# 3. Quét va chạm hình học (Bounding-Box Collision & Boundary Containment)
python "D:\Đồ Án\.agents\teamwork\reviewer_m1_1\check_collisions.py"

# 4. Kiểm định 12 Cam Kết Bất Biến Capstone (100% Invariants Compliance)
python "D:\Đồ Án\.agents\teamwork\reviewer_m1_1\verify_invariants.py"
```
