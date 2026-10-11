# 🤖 AGENTS.md — SỔ TAY KỸ SƯ TÁC CHIẾN ĐỒ ÁN SEP490 (FA26SE166)

> **Dự án:** Hệ Thống Luyện Thi & Đánh Giá Vấn Đáp Bằng LLM Cho Ngành Kỹ Thuật Phần Mềm (LLM Oral Exam System)  
> **Mã đề tài:** FA26SE166 | Học kỳ: Fall 2026 (FA26) — Đại học FPT TP.HCM (FPT SG)  
> **Kho mã nguồn:** `05_Source_Code` (Thư mục làm việc chính thức)  
> **Tech Stack Chuẩn:** .NET 8 Clean Architecture · React 19 (Vite) + React Router DOM v7 · Tailwind CSS v4 · PostgreSQL 16+ (30 bảng 3NF) · Gemini 1.5 Flash/Pro · Whisper STT · Cloudflare R2  
> **Phạm vi áp dụng:** Quy chuẩn bắt buộc 100% cho mọi AI Agents làm việc tại không gian `D:\Đồ Án` (và junction `D:\DoAn`, `05_Source_Code`). Mọi hành vi sinh mã nguồn bắt buộc phải tuân thủ 100% các nguyên tắc trong tài liệu này.

---

## 👤 1. HỒ SƠ DỰ ÁN, ĐỘI NGŨ & BẢN QUYỀN ĐỒ ÁN

* **Mục tiêu tối thượng:** Tự động hóa và chuẩn hóa quy trình tổ chức, thi thử và đánh giá thi vấn đáp (viva voce) ngành Kỹ thuật Phần mềm (SE) thông qua tương tác giọng nói thời gian thực với AI, bám sát ma trận Barem Rubric chuẩn đầu ra (CLOs) và bảo toàn tính toàn vẹn pháp lý phòng thi, phục vụ bảo vệ trước Hội đồng chấm tốt nghiệp FPTU.
* **Ngôn ngữ giao tiếp của Agent:** **100% Tiếng Việt** trong toàn bộ giải thích, comment và tài liệu.
* **Mô hình hoạt động:** **Gemini 3.8 Flash (High)** làm **Team Lead / Master Orchestrator**; **Gemini 3.1 Pro** làm **Lead System Architect & Chief Code Auditor**.
* **Đội ngũ phát triển (4 Kỹ sư Nhóm FA26SE166):**
  - **Nguyễn Quang Thành:** Team Leader & Lead Backend Architect
  - **Nguyễn Trọng Tốt:** Backend Developer, AI Specialist & QA Lead
  - **Nguyễn Đăng Hải:** DB Specialist & Frontend Developer
  - **Lê Vũ Hoàng:** Lead Frontend Architect & Fullstack Coordinator

---

## 🔐 2. HỆ THỐNG PHÂN QUYỀN 5 ROLES (RBAC)

1. `student`: Sinh viên đăng nhập bằng tài khoản Google (mọi email domain, không ép `@fpt.edu.vn`), không dùng mật khẩu. Luyện tập tự do (MF-01: chọn [Per-Question] có follow-up khi điểm 4.0–8.0 hoặc [Full-Session] không follow-up; chọn số lượng câu hỏi và tùy chọn bốc đề "progressive" 3–10 câu từ Dễ $\to$ Khó), thi thử tính giờ (MF-02: hạn ngạch và cấu trúc đề do Trưởng BM cấu hình, tự chọn có/không follow-up), thi thật Kiosk phòng Lab (MF-04), xem kết quả thi và nộp đơn phúc khảo nội bộ (`AppealRequest`) trên Student Portal.
2. `lecturer`: Giảng viên sử dụng AI sinh câu hỏi từ FLM Syllabus theo CLO hoặc soạn thủ công, tick chọn 2 kho `practice_questions` và/hoặc `exam_questions`, tự thiết kế theo Barem Rubric riêng 10.0đ (bắt buộc gửi Trưởng Bộ Môn phê duyệt) (MF-03); coi thi; hậu kiểm toàn bộ bài thi nghi ngờ/fail qua Waveform Audio Player và Evidence Panel, điều chỉnh điểm và công bố điểm (MF-04); thực hiện chấm lại đơn phúc khảo khi được Trưởng Bộ Môn giao nhiệm vụ. Giảng viên KHÔNG cấu hình follow-up trong MF-01.
3. `department_head`: Trưởng Bộ Môn khởi tạo kỳ thi (`OfficialExamSession`), đưa môn thi vào kỳ thi, cấu hình ca thi (`RealExamSessionShift`: phòng lab ghi ngay trên ca, kíp thi, phân công người coi thi là GV hoặc giám thị), cấu hình Follow-up (`has_follow_up`, `max_follow_up_questions` 1–5 câu, mặc định 2 câu) và `ExamInputMode` (`VoiceOnly`, `VoiceWithTranscriptEdit`) cho môn thi trong kỳ thi (MF-04); cấu hình cấu trúc đề thi thử, phân bổ Bloom và hạn ngạch thi thử (`max_mock_exams_per_day`) cho môn học (MF-02); quản lý đề cương môn học, thẩm định và ra quyết định phê duyệt (`APPROVED`), yêu cầu chỉnh sửa (`NEEDS_REVISION`) hoặc từ chối (`REJECTED`) câu hỏi do giảng viên gửi lên (MF-03); tiếp nhận đơn phúc khảo nội bộ (`AppealRequest`) của sinh viên và giao cho một Giảng viên chấm lại (MF-04).
4. `proctor`: Giám thị phòng thi (hoặc giảng viên được phân công) điểm danh theo số thứ tự (STT 1–40), đối chiếu thẻ SV/CCCD, mở ca thi, giám sát gian lận Kiosk (MF-04). Không có trang chủ riêng (sử dụng màn hình phòng thi).
5. `admin`: Quản trị viên hệ thống quản lý người dùng (FE-09: khóa/mở tài khoản, gán vai trò; không tạo user bằng email/mật khẩu), quản lý học kỳ (FE-08: tạo và sửa học kỳ, không xóa), cấu hình hệ thống luyện tập MF-01 qua `system_configs` (số câu follow-up 1–5 câu, mặc định 2 câu; min/max số câu progressive 3–10 câu; thời gian đệm sửa transcript 10–300s, mặc định 60s), xem nhật ký kiểm toán Audit Logs, xử lý Dead-Letter Queue (DLQ). Admin KHÔNG cấu hình quota thi thử, KHÔNG cấu hình thi thật.

---

## 🔄 3. ĐẶC TẢ CHI TIẾT 4 MAIN FLOWS NGHIỆP VỤ (NGUỒN SỰ THẬT DUY NHẤT)

### 🔹 MF-01: Luyện Tập Tương Tác Tự Do (Interactive Practice)
* **Quyền hạn cấu hình:** Giảng viên KHÔNG cấu hình follow-up trong MF-01. **Admin là người duy nhất cấu hình hệ thống** qua bảng `system_configs`:
  - Số câu hỏi phụ follow-up: Từ 1 đến 5 câu, giá trị mặc định là **2 câu**.
  - Ràng buộc số câu hỏi tiến trình Dễ $\to$ Khó ("progressive"): `MinMixedPracticeQuestions = 3`, `MaxMixedPracticeQuestions = 10`.
  - Thời gian đệm sửa transcript: `TranscriptBufferSeconds` từ 10–300s, giá trị **mặc định là 60 giây**. Giảng viên và Trưởng BM không can thiệp số này.
* **Quy tắc làm bài và bốc đề:**
  - Sinh viên được **chủ động chọn số lượng câu hỏi luyện tập cho CẢ 2 CHẾ ĐỘ**:
    + Nếu sinh viên chọn **Luyện từng câu (`[Per-Question]`)**: Kích hoạt câu hỏi follow-up chuyên sâu đào sâu (A2) khi điểm số của câu trả lời rơi vào khoảng ranh giới **$4.0 \le \text{Score} \le 8.0$** (số lượng câu hỏi phụ tối đa từ 1 đến 5 câu do Admin cấu hình, mặc định 2 câu). Nếu $\text{Score} < 4.0$ hoặc $\text{Score} > 8.0$, hệ thống bỏ qua câu hỏi phụ và mở ngay bảng điểm Scorecard.
    + Nếu sinh viên chọn **Luyện trọn gói (`[Full-Session]`)**: **KHÔNG có câu hỏi follow-up**. Sinh viên tự nhập số câu cho từng mức Dễ, Trung bình, Khó; hoặc chọn tùy chọn **`progressive` ("Ngẫu nhiên từ dễ đến khó") từ 3–10 câu** (hệ thống chia đều các mức và sắp xếp phát vấn tăng dần từ Dễ $\to$ Trung bình $\to$ Khó; nếu kho đề không đủ câu cho bất kỳ mức nào, trả `HTTP 400 Bad Request` tiếng Việt rõ ràng, không tạo phiên rác).
* **Màn hình đệm hiệu đính (Buffer Screen):** Sau khi nói qua Mic, hệ thống mở màn hình đệm cho phép sinh viên xem lại STT bóc băng, nghe lại TTS câu hỏi và sửa các thuật ngữ kỹ thuật bị phiên âm sai (Code-Switching SE Glossary) trước khi nộp. Thời gian đếm ngược cấu hình động theo môn (`transcript_buffer_seconds`, từ 10–300s, mặc định 60 giây).
* **Lưu trữ dữ liệu:** Luyện tập **KHÔNG lưu audio**, chỉ lưu bản *transcript* sinh viên đã sửa vào `practice_answers`.
* **Lịch sử luyện tập:** Sinh viên xem lại lịch sử luyện tập trên cùng màn hình với lịch sử thi thử tại Student Portal (FE-02).

### 🔹 MF-02: Thi Thử Tính Giờ Có Hạn Ngạch (Timed Mock Exam)
* **Nguồn đề thi thử:** Rút ngẫu nhiên từ kho câu hỏi luyện tập chung (`practice_questions`), cô lập an toàn kho câu hỏi thi thật (`exam_questions`) dành riêng cho MF-04.
* **Hạn ngạch thi thử:** Do **Trưởng Bộ Môn cấu hình** theo từng môn học (`max_mock_exams_per_day`), không khóa cứng $K=3$ và không do Admin đặt. Vượt quá hạn ngạch hệ thống lập tức chặn và trả mã lỗi `HTTP 429 Too Many Requests`.
* **Cấu trúc đề & Phân bổ Bloom:** Do Trưởng Bộ Môn cấu hình, không cố định 6 mức. Sinh viên **chỉ chọn môn học và tùy chọn có/không follow-up**; không chọn topic, không chọn độ khó.
* **Tùy chọn Follow-up chủ động của sinh viên:** Sinh viên chủ động chọn `Có Follow-up` hoặc `Không Follow-up` trước khi bắt đầu. Follow-up MF-02 do Trưởng Bộ Môn cấu hình, AI hỏi theo nội dung ngữ cảnh bài nói (không dựa vào ngưỡng điểm 4.0–8.0). Ca thi có follow-up sẽ có thời lượng làm bài dài hơn do Trưởng Bộ Môn cấu hình.
* **Cổng kiểm soát phát biểu (Voice-First Gate):** Hiển thị đề thi và khóa cứng ô gõ phím, bắt buộc phải trả lời qua micro.
* **Đồng hồ đếm ngược & Tự nộp bài (Dynamic Timer):** Hết giờ ca thi hệ thống tự động khóa và tự nộp bài. **Các câu chưa làm tính là bỏ trống, không chấm**.
* **Lưu trữ dữ liệu:** Thi thử **KHÔNG lưu audio**, chỉ lưu bản *transcript* vào `mock_exam_answers`.
* **Instant Feedback & Lịch sử thi:** Sau khi hoàn thành bài thi thử, AI chấm điểm tức thì và trả về **Scorecard chi tiết kèm nhận xét sư phạm từng tiêu chí theo CLO** cho sinh viên xem ngay trên màn hình kết quả và lưu vào lịch sử thi.

### 🔹 MF-03: Giảng Viên Sử Dụng AI Sinh Câu Hỏi Theo CLO FLM & Rubric Studio 10.0 (Question Bank & Rubric Studio)
* **AI sinh câu hỏi từ FLM:** AI sinh câu hỏi, rubric và câu trả lời mẫu từ các chuẩn đầu ra (CLOs) có sẵn trên syllabus của FLM (giảng viên không bắt buộc chọn từng CLO1, CLO2; bấm sinh là dùng CLO của syllabus; nếu muốn thu hẹp thì chọn topic trước rồi chọn CLO). Hệ thống chỉ đọc syllabus từ FLM, không ghi ngược lên FLM. Giảng viên cũng có toàn quyền tự soạn câu hỏi, rubric và sample answer thủ công.
* **Tick chọn kho câu hỏi:** Sau khi sinh hoặc soạn xong câu hỏi, Giảng viên tick chọn lưu vào 2 kho: `practice_questions` và `exam_questions` (có thể tick một ô hoặc cả hai ô).
* **Barem Rubric chuẩn hóa:** Tổng điểm các tiêu chí rubric con của mỗi câu hỏi bắt buộc $\sum \equiv 10.0$ điểm (nếu sai lệch FluentValidation chặn và trả `HTTP 422 Unprocessable Entity`). Câu trả lời mẫu (Model Answer) bắt buộc $\ge 50$ ký tự.
* **Quy trình Gửi duyệt Bộ Môn:** Mọi câu hỏi do Giảng viên tạo ra — dù là tạo thủ công hay dùng AI sinh từ FLM — đều ở trạng thái dự thảo (`DRAFT`) và **BẮT BUỘC PHẢI GỬI QUA CHO TRƯỞNG BỘ MÔN DUYỆT** (`POST /api/v1/questions/batch-submit-review`). Bộ câu hỏi chuyển sang trạng thái `SUBMITTED_FOR_REVIEW`.
* **Trưởng Bộ Môn (`department_head`) phê duyệt:** Trưởng Bộ Môn là chốt chặn thẩm định chính thức duy nhất đưa ra 3 quyết định:
  - Bấm **"Phê duyệt"** (`APPROVED`): Lưu chính thức vào ngân hàng đề môn học.
  - Bấm **"Yêu cầu chỉnh sửa"** (`NEEDS_REVISION` kèm lý do góp ý): Trả về cho giảng viên hiệu chỉnh lại rồi nộp lại.
  - Bấm **"Từ chối"** (`REJECTED` kèm lý do): Loại bỏ câu hỏi khỏi hệ thống.
  Trưởng Bộ Môn cũng có quyền tự sinh và tạo đề trực tiếp.

### 🔹 MF-04: Thi Thật Phòng Lab Kiosk, Công Bố Điểm & Phúc Khảo Nội Bộ (Official Lab Viva Exam & Internal Appeals)
* **Chu trình Quản trị Kỳ thi & Cấu hình Môn thi của Trưởng Bộ Môn (`department_head`):**
  - **Trưởng Bộ Môn khởi tạo kỳ thi:** Tạo Kỳ thi (`OfficialExamSession` / Exam Season).
  - **Gán môn thi vào kỳ thi:** Đưa danh sách các môn thi thuộc kỳ thi đó vào hệ thống.
  - **Cấu hình môn thi trong kỳ thi:**
    1. Cấu hình danh sách **Ca thi** (`RealExamSessionShift`: ghi phòng lab ngay trên ca, kíp thi, thời gian bắt đầu/kết thúc, phân công người coi thi là giảng viên hoặc giám thị). Danh sách thí sinh của ca thi lấy từ lớp học đã có trong hệ thống (Excel chỉ là giải pháp dự phòng).
    2. **Cấu hình Follow-up:** Bật/tắt hỏi chuyên sâu (`has_follow_up`) và số lượng câu hỏi follow-up áp dụng chung cho Môn thi đó trong kỳ thi (`max_follow_up_questions` từ 1–5 câu, mặc định 2 câu).
    3. Cấu hình phương thức làm bài `ExamInputMode` trong scope gồm 2 phương thức: `VoiceOnly` và `VoiceWithTranscriptEdit` (loại bỏ `VoiceAndTextInput`) và thời gian đệm `TranscriptBufferSeconds` (10–300s).
* **An ninh Kiosk Phòng Lab:**
  - Điểm danh: Giám thị (hoặc GV coi thi) đối chiếu thẻ sinh viên và căn cước tại phòng lab, tick xác nhận trên hệ thống. Số máy Kiosk (SeatNumber 1–40) trùng khớp với Số thứ tự trong danh sách ca thi phòng Lab. Cột IP máy trạm thống nhất dùng **`ip_address`**.
  - Chế độ phong tỏa: Fullscreen Kiosk Lockdown, vô hiệu hóa phím tắt hệ thống (Alt+Tab, Windows, F11, F12, DevTools), phát hiện mất focus (`window.onblur`) quá 3 lần thì lập biên bản đình chỉ thi.
  - Kiểm tra micro phần cứng 30s trước khi vào thi đạt $\ge 60$dB.
* **Niêm phong âm thanh (Cloudflare R2):** Thi thật CÓ lưu audio để phục vụ hậu kiểm. Luồng audio được stream trực tiếp lên Cloudflare R2 với tên file bất biến **`STT_MSSV.webm`** (ví dụ `01_SE170123.webm`), băm cryptographic SHA-256 niêm phong ngay tại máy trạm Kiosk.
* **Quy trình nộp bài, Hàng đợi chịu tải & Chấm điểm:**
  - Nộp bài xong, hệ thống thực hiện **Persist First** lưu ngay bài thi vào `exam_question_submissions` và cập nhật `student_exam_tickets` với trạng thái **`SUBMITTED` trong $< 100$ms** kèm mã băm SHA-256 niêm phong audio Cloudflare R2 (`STT_MSSV.webm`). Kiosk khóa màn hình và thông báo: *"Bài thi đã được lưu trữ an toàn. Kết quả sẽ do Giảng viên thẩm định và công bố trên hệ thống."* **Thi xong Kiosk TUYỆT ĐỐI KHÔNG CÓ ĐIỂM LIỀN và không cho khiếu nại tại chỗ**. Thí sinh ký biên bản nộp bài giấy và ra về (về nhà).
  - Tác vụ chấm điểm được đẩy vào hàng đợi **BoundedChannel 1,000 slots RAM (`FullMode.Wait`)** để AI (Gemini) thực hiện chấm điểm ngầm toàn bộ bài thi chỉ dựa trên bản *transcript* sinh viên đã sửa. Khi AI quá tải (HTTP 429) hoặc timeout, bài thi được cách ly vào **DLQ `dead_letter_queues`** và tiến trình nền retry sau 5 phút, bảo đảm cam kết Zero Data Loss 100%.
* **Chốt chặn kiểm soát chất lượng & Evidence Panel:**
  - Hệ thống tự động tính điểm tin cậy và phân loại kết quả chấm của AI thành 2 nhóm trên Cổng Hậu kiểm Giảng viên:
    + *Nhóm 1 (Cần xem lại / Fail / Đáng nghi ngờ):* Các bài thi có `is_suspicious == true`, `confidence_score < 0.70` (do ồn, phát âm không rõ, mâu thuẫn chuỗi CoT, điểm ranh giới) hoặc bài thi bị fail/điểm liệt. **Sinh viên bắt buộc phải đợi Giảng viên rà soát và chấm lại toàn bộ các bài trong nhóm này**. Giảng viên đối chiếu trên Evidence Panel (AudioURL R2, Transcript Whisper gốc, AI Chain-of-Thought phân tích tiêu chí) để nghe lại file ghi âm trên Waveform Player (1.0x–1.5x), chấm và điều chỉnh lại điểm kèm lý do giải trình bắt buộc (`override_reason`).
    + *Nhóm 2 (Độ tin cậy cao):* Giảng viên rà soát nhanh đối chiếu Evidence Panel.
* **Giảng viên Công Bố Điểm (Sau khi xử lý đầy đủ mới gửi điểm về cho sinh viên):**
  - Sau khi Giảng viên xử lý xong toàn bộ các bài nghi ngờ và fail, bảo đảm **100% sinh viên trong ca thi đã có điểm hoàn chỉnh**, Giảng viên mới bấm **"Công Bố Điểm"** (`POST /api/v1/official-exams/shifts/{shiftId}/publish-grades`) một lần duy nhất. Lúc này hệ thống mới giải phóng điểm gửi về cho sinh viên.
  - Hệ thống kích hoạt cơ chế **One-Way Lock (`is_locked = true`)**. Bộ đánh chặn `OneWayLockInterceptor` chặn 100% mọi thao tác `UPDATE` hoặc `DELETE` điểm số trái phép (`HTTP 403 Forbidden`).
* **Sinh viên ở nhà nhận điểm & Quy trình Phúc khảo Nội bộ trong hệ thống:**
  - Sinh viên ở nhà đăng nhập Student Portal trên hệ thống của mình để xem bảng điểm chính thức do Giảng viên công bố.
  - Nếu sinh viên chấp nhận điểm: Bấm xác nhận hoàn thành kỳ thi (`ACKNOWLEDGED` qua `POST /api/v1/student/official-exams/{ticketId}/acknowledge-grade`).
  - Nếu sinh viên **không chấp nhận điểm (muốn phúc khảo)**: Sinh viên làm đơn phúc khảo trực tiếp ở phần Phúc khảo trong hệ thống của mình (`POST /api/v1/appeals`). Hệ thống tạo entity `AppealRequest` chuyển đến Trưởng Bộ Môn (`department_head`). Trưởng Bộ Môn tiếp nhận và **giao cho một Giảng viên chấm lại** (`PUT /api/v1/appeals/{id}/assign-lecturer`). One-Way Lock mở đường hợp lệ cho Giảng viên được giao nhiệm vụ cập nhật điểm số mới sau phúc khảo.
* **Báo cáo Khảo thí FPT:** Hệ thống hỗ trợ xuất báo cáo tổng kết điểm thi phòng lab ra cả hai định dạng: **file Excel (.xlsx)** và **file PDF có chữ ký số** để nộp Phòng Khảo thí.

---

## 🧠 4. NGUYÊN TẮC TƯ DUY PHẢN BIỆN & THẨM ĐỊNH ĐỐI THOẠI TRƯỚC KHI SỬA (CRITICAL THINKING & CONSULTATION FIRST)

> **Phương châm cốt lõi:** *"Không vội vàng sửa code khi chưa suy xét. Lỗi của người dùng cũng có thể sai. Sự thật kỹ thuật nằm ở mã nguồn và dữ liệu thực tế."*

Mọi AI Agent khi nhận được báo cáo lỗi, vấn đề kỹ thuật, hoặc yêu cầu thay đổi logic từ Người dùng (User / Thành) **BẮT BUỘC** phải tuân thủ quy trình 3 bước sau trước khi thực hiện bất kỳ thao tác sửa đổi nào:

1. **Bước 1: Suy xét độc lập & Kiểm chứng sự thật (Fact-Checking & Root Cause Analysis):**
   - **Tuyệt đối KHÔNG "Yes-Man"** hay máy móc vâng dạ rồi cắm đầu sửa code ngay.
   - Luôn đặt câu hỏi: *"Vấn đề người dùng nêu ra có thực sự đúng không? Có mâu thuẫn với các Invariants khác không? Người dùng có đang nhớ nhầm hay hiểu nhầm không?"*
   - Khảo sát thực tế trong mã nguồn (`view_file`), CSDL DDL (`01_schema.sql`), test suite (`dotnet test`), hoặc tài liệu đặc tả để xác minh tính chính xác của phản ánh.
   - Nhận diện các nguyên nhân chủ quan từ phía người dùng: Nhớ nhầm tên cột/biến, nhầm luồng giữa MF-01/02/04, nhầm quyền hạn các roles, hoặc môi trường local chưa build lại.

2. **Bước 2: Phân tích đa chiều & Thảo luận phản biện (Critical Analysis & Collaborative Consultation):**
   - **Nếu người dùng nhận định ĐÚNG:** Xác nhận nguyên nhân gốc rễ, phân tích ma trận tác động (Impact Boundary) và đề xuất giải pháp tối ưu kèm rủi ro.
   - **Nếu người dùng nhận định CHƯA ĐÚNG hoặc NHẦM LẪN:** Trình bày nhã nhặn, khách quan với **bằng chứng cụ thể** (tên file, số dòng code, kết quả test thực tế, hoặc quy chuẩn trong tài liệu) để giải thích rõ vì sao hệ thống hiện tại đang hoạt động đúng hoặc vì sao sửa theo cách đó sẽ gây ra breaking change / xung đột.
   - **Nếu có nhiều hướng tiếp cận hoặc vấn đề còn mơ hồ:** Đưa ra bảng so sánh các phương án (ưu / nhược điểm) và đặt câu hỏi gợi mở để người dùng định hướng.

3. **Bước 3: Cổng đồng thuận trước khi can thiệp (Agreement Gate Before Edit):**
   - **CHỈ BẮT ĐẦU SỬA MÃ NGUỒN HOẶC TÀI LIỆU KHI ĐÃ THẢO LUẬN, HỎI LẠI VÀ ĐẠT ĐƯỢC SỰ ĐỒNG THUẬN TỪ NGƯỜI DÙNG.**
   - Nghiêm cấm mọi hành vi tự ý sửa đổi hàng loạt file trong âm thầm khi chưa có sự xác nhận của người dùng.

---

## 🛡️ 5. MƯỜI HAI CAM KẾT KỸ THUẬT BẤT BIẾN (CAPSTONE INVARIANTS)

1. **Zero Placeholder (100% Complete Code):** CẤM TUYỆT ĐỐI `// TODO`, `/* rest of code */`, `// tương tự như trên`. Mọi handler, controller, service, component phải hoàn chỉnh 100%.
2. **Anti-Mocking trong Production Code:** CẤM sinh code giả lập, dummy return trong mã nguồn nghiệp vụ chính. Cho phép dùng Mocking trong `tests/` và Fallback Adapter cục bộ khi dev offline.
3. **Hard Verification Gate (Theo Cấp Độ S/M/L):** Mức S chỉ cần targeted build/test pass. Mức M/L chạy test suite tương ứng và đạt Exit Code = 0.
4. **Ports mạng bất biến:** Frontend `3000`, Backend API `5000`, Database PostgreSQL `5432` nội bộ (CẤM expose internet).
5. **Hạn ngạch thi thử (MF-02):** Hạn ngạch thi thử do Trưởng Bộ Môn cấu hình (`max_mock_exams_per_day`), không khóa cứng K=3. Vượt hạn ngạch trả `HTTP 429 Too Many Requests`. Sinh viên tự chọn Có/Không Follow-up trước khi thi thử.
6. **Khóa điểm một chiều & Đường mở Phúc khảo (MF-04):** `is_locked = true` khi giảng viên ký công bố điểm. `OneWayLockInterceptor` chặn 100% UPDATE/DELETE (`HTTP 403 Forbidden`), nhưng chừa đường hợp lệ cho Giảng viên được Trưởng Bộ Môn giao chấm lại đơn phúc khảo nội bộ (`AppealRequest`) cập nhật điểm.
7. **Barem Rubric chuẩn hóa (MF-03):** Luôn chuẩn hóa $\sum \equiv 10.0$ điểm (FluentValidation validate tổng các tiêu chí). Mọi câu hỏi (thủ công hay AI FLM) bắt buộc gửi Trưởng Bộ Môn phê duyệt (`APPROVED`, `NEEDS_REVISION`, `REJECTED`).
8. **Audio Upload Cloudflare R2 (MF-04):** Thi thật lưu audio đặt tên file bất biến `STT_MSSV.webm` (ví dụ: `01_SE170123.webm`), SHA-256 integrity hash niêm phong tại máy trạm. Luyện tập (MF-01) và thi thử (MF-02) KHÔNG lưu audio.
9. **Worker Queue Background & Persist First:** Persist First lưu đĩa cứng $< 100$ms với trạng thái `SUBMITTED`, sau đó đẩy vào `System.Threading.Channels.BoundedChannel<EvaluationTask>` với capacity = 1000, `FullMode.Wait`. Kiosk không có điểm liền, không khiếu nại tại chỗ.
10. **LLM Resilience & DLQ:** Polly Retry 3 lần với Exponential Backoff (2s, 4s, 8s) + Circuit Breaker. Khi LLM thất bại sau 3 lần, đẩy task vào DLQ `dead_letter_queues` và retry sau 5 phút.
11. **Code-Switching & Dynamic Configuration:** Buffer screen đếm ngược theo cấu hình động môn học (`transcript_buffer_seconds`, 10–300s, mặc định 60s do Admin cấu hình); Admin cấu hình số câu follow-up hệ thống luyện tập MF-01 (1–5 câu, mặc định 2 câu) và tùy chọn progressive (3–10 câu); Trưởng Bộ Môn cấu hình kỳ thi, môn thi, ca thi, hạn ngạch thi thử MF-02, và số câu follow-up thi thật MF-04 (1–5 câu, mặc định 2 câu).
12. **Kiosk Exam Mode Lockdown:** Phòng thi thật MF-04 kích hoạt Fullscreen Canvas, vô hiệu hóa phím tắt hệ thống (Alt+Tab, Esc, F11, DevTools), phát hiện mất focus (blur event) quá 3 lần thì lập biên bản đình chỉ thi.

---

## 🚫 6. MƯỜI MỘT ĐIỀU CẤM KỴ TUYỆT ĐỐI (ANTI-PATTERNS & TABOOS)

Mọi Agent khi sinh mã nguồn **TUYỆT ĐỐI CẤM** vi phạm các điều sau:

1. ❌ **CẤM tạo bảng hoặc cột `chapter`:** Ngân hàng câu hỏi được phân loại theo Môn học (`course_id`), Cấp độ Bloom và CLO. Tuyệt đối không sinh thêm thực thể `chapter`.
2. ❌ **CẤM dùng tên cột `workstation_ip`:** Bắt buộc dùng thống nhất **`ip_address`** (đồng bộ cả C#, SQL Schema và Docs).
3. ❌ **CẤM hardcode thời gian đệm và số câu hỏi phụ:** Thời gian màn hình đệm sửa transcript luyện tập do Admin cấu hình (`transcript_buffer_seconds`, từ 10-300s, mặc định 60s); số câu hỏi phụ luyện tập do Admin cấu hình (1–5 câu, mặc định 2 câu); số câu hỏi phụ thi thật do Trưởng Bộ Môn thiết lập theo môn thi trong kỳ thi (`max_follow_up_questions`, từ 1-5 câu, mặc định 2 câu); hạn ngạch thi thử do Trưởng Bộ Môn cấu hình. Tuyệt đối không hardcode cứng các giá trị này.
4. ❌ **CẤM coi AI-gen là quyền riêng của Trưởng bộ môn hoặc bỏ qua phê duyệt:** Giảng viên sử dụng AI sinh câu hỏi từ FLM theo CLO hoặc soạn thủ công rồi **BẮT BUỘC gửi Bộ Môn thẩm định**; Trưởng Bộ Môn là chốt chặn phê duyệt duy nhất (APPROVED / NEEDS_REVISION / REJECTED).
5. ❌ **CẤM dùng vé thi chữ thường:** Trạng thái vé thi Kiosk gồm đúng 7 bước viết HOA: `SCHEDULED`, `IN_PROGRESS`, `SUBMITTED`, `AI_GRADED`, `AUDITED`, `PUBLISHED`, `LOCKED`.
6. ❌ **CẤM tạo cổng phúc khảo trên Kiosk hoặc cho điểm liền:** Máy trạm Kiosk phòng Lab TUYỆT ĐỐI KHÔNG CÓ ĐIỂM LIỀN và không khiếu nại tại chỗ. Phúc khảo của sinh viên được thực hiện trực tiếp trên Student Portal sau khi Giảng viên công bố điểm, tạo đơn `AppealRequest`, Trưởng BM tiếp nhận và giao cho một Giảng viên chấm lại.
7. ❌ **CẤM code giả định hoặc giữ chỗ (`Zero Placeholder`):** CẤM sinh `// TODO`, `/* rest of code */`, `// giữ nguyên logic cũ`. Mọi hàm, component phải hoàn chỉnh 100%.
8. ❌ **CẤM Mock/Fake Data trong mã nguồn Production:** Cấm hardcode dummy data trong controllers/services. Chỉ cho phép mock trong thư mục `tests/`.
9. ❌ **CẤM sai thứ tự Middleware ASP.NET Core:** `app.UseCors("AllowFrontend")` BẮT BUỘC phải nằm TRƯỚC `app.UseHttpsRedirection()` và static files.
10. ❌ **CẤM tự ý commit hoặc push Git:** Mọi thay đổi mã nguồn phải giữ nguyên ở local working tree để lập trình viên tự kiểm tra.
11. ❌ **CẤM sửa code vội vàng khi chưa suy xét và đối thoại:** Tuyệt đối không được cắm đầu sửa code ngay khi nhận vấn đề từ người dùng mà chưa kiểm chứng tính đúng/sai thực tế và chưa thảo luận thống nhất phương án với người dùng.

---

## 👥 7. PHÂN BỔ CÔNG VIỆC CHO 4 THÀNH VIÊN NHÓM FA26SE166

Khi hỗ trợ từng thành viên, Agent phải nắm rõ phạm vi phụ trách:

* **🧑 Nguyễn Quang Thành (Team Leader & Lead Backend Architect):**
  - Phạm vi: `backend/src/API/`, `backend/src/Application/Features/Practice/`, `backend/src/Application/Features/MockExams/`, SignalR Hubs (`PracticeHub`), Module Báo cáo Khảo thí FPT (`ExcelService` / PDF export).
  - Nhiệm vụ: Chủ trì Backend MF-01 (Interactive Practice, Tùy chọn Progressive 3–10 câu từ Dễ $\to$ Khó cho cả Per và Full, BoundedChannel 1000 slots RAM, SignalR `PracticeHub`, SystemConfigs) + Chủ trì Backend MF-02 (Timed Mock Exam, Quota Guard do Trưởng BM cấu hình, Voice-First Gate, Bốc đề `practice_questions`, Instant Scorecard) + Module Báo cáo Khảo thí FPT (Excel .xlsx + PDF có chữ ký số) + Backend APIs cho Dashboard SV (FE-10) và Lịch sử luyện tập (FE-02) + Tích hợp hệ thống Clean Architecture.
* **🧑 Nguyễn Trọng Tốt (Backend Developer, AI Specialist & QA Lead):**
  - Phạm vi: `backend/src/Infrastructure/Services/`, `backend/src/Application/Features/Questions/`, `backend/src/Application/Features/OfficialExams/`, `backend/src/Application/Features/Appeals/`, `backend/src/Application/Features/Academic/`, `backend/src/Application/Features/Notifications/`, `backend/tests/`.
  - Nhiệm vụ: Auth Google OAuth 2.0 PKCE mọi email domain (không mật khẩu) + Chủ trì Backend MF-03 (AI sinh đề FLM theo CLO, Barem Rubric 10.0đ, Tick chọn 2 kho `practice_questions`/`exam_questions`, Gửi duyệt & Trưởng BM phê duyệt) + **TOÀN BỘ Backend MF-04** (Kiosk Check-in IP Binding `ip_address`, Nộp bài Persist First < 100ms, Audio R2 `STT_MSSV.webm` + SHA-256, Background AI Grading qua BoundedChannel 1000 slots, OneWayLock, Công bố điểm `PublishGrades`, Phân hệ Phúc khảo `AppealRequest` Trưởng BM giao GV chấm lại) + Backend APIs vệ tinh (FE-08 Semester CRUD, FE-11 Notification in-app, FE-09 User Management) + AI Doubt Guard + Polly/DLQ Replay Worker + Toàn bộ xUnit và NetArchTest suites.
* **🧑 Nguyễn Đăng Hải (DB Specialist & Frontend Developer):**
  - Phạm vi: `infra/postgres/`, `docker-compose.yml`, `frontend/src/*`.
  - Nhiệm vụ: Quản trị 30 bảng CSDL PostgreSQL 16 trong 6 Bounded Contexts (DDL Schema, Migrations, Seed data, Docker Compose — Đã xong, tuyệt đối KHÔNG code logic C# Backend); dồn toàn lực phát triển Frontend: Student Portal Dashboard (FE-10), Lịch sử luyện tập & thi thử (FE-02), Giao diện Thi thử Voice-First MF-02 (đếm ngược, modal chặn Quota 429, Instant Scorecard Rubric), Giao diện Trưởng BM duyệt đề MF-03 & Rubric Studio, Giao diện Trưởng BM cấu hình ca thi & thẩm định/giao đơn phúc khảo MF-04, Quản lý người dùng User Management FE-09, Quản lý học kỳ Semester CRUD FE-08.
* **🧑 Lê Vũ Hoàng (Lead Frontend Architect & Fullstack Coordinator):**
  - Phạm vi: `frontend/src/*`, `frontend/package.json`, Tailwind CSS v4.
  - Nhiệm vụ: Kiến trúc Frontend Core (React 19 SPA, Tailwind CSS v4, React Router DOM v7 Route Guards 5 roles, Axios Interceptors RFC 7807, Zustand stores) + Giao diện Luyện tập MF-01 (Màn hình đệm 60s, Web Speech API, SignalR hook `usePracticeHub.ts`) + Màn hình Kiosk phòng Lab MF-04 (Fullscreen lockdown, blur $\ge 3$, mic-check $\ge 60$dB, Upload R2 `STT_MSSV.webm` + SHA-256) + Màn hình Hậu kiểm Evidence Panel cho Giảng viên (Waveform Audio Player Wavesurfer.js, sửa điểm kèm giải trình, Publish Grades) + Auth Google UI + Notification in-app FE-11 + Dashboard Giảng viên FE-10.

---

## ⚡ 7. NGUYÊN TẮC ĐIỀU PHỐI MULTI-AGENT (ORCHESTRATION PROTOCOL)

> [!IMPORTANT]
> **PHÂN ĐỊNH TRÁCH NHIỆM CHO MASTER ORCHESTRATOR:**
> - **Tác vụ lớn / Tính năng mới / Tái cấu trúc đa phân hệ:** **BẮT BUỘC** sử dụng công cụ `invoke_subagent` để triệu hồi các Subagents chuyên trách chạy song song.
> - **Tác vụ Quick Fix / Hotfix / Đồng bộ nhỏ (< 50 dòng code) / Kiểm tra lỗi:** Cho phép Lead Orchestrator tự thực hiện trực tiếp (SOLO Mode) để phản hồi nhanh, tối ưu token và tránh độ trễ không cần thiết.

### 👥 Bộ Subagent Mẫu Sẵn Sàng Triệu Hồi (Fast-Track Archetypes):

> [!IMPORTANT]
> **QUY CHUẨN GỌI SUBAGENTS (SUBAGENT INVOCATION CONTRACT):**
> Antigravity mặc định chỉ có sẵn 2 TypeName là `self` và `research`. Các tên archetype bên dưới là **vai trò (Role)**, không phải TypeName tích hợp sẵn.
> - **Cách A (Khuyên dùng, nhanh & chuẩn xác):** Gọi `invoke_subagent` với `TypeName: "self"`, `Role: "<Tên vai trò>"` (ví dụ `Role: "Lead Backend Engineer (.NET 8)"`), prompt mở đầu: *"Đọc D:\DoAn\AGENTS.md và skill liên quan trước khi thực thi..."*.
> - **Cách B:** Gọi `define_subagent` một lần đầu phiên chat (bật `enable_write_tools: true`), sau đó mới gọi `invoke_subagent` theo đúng TypeName vừa định nghĩa (chỉ tồn tại trong phiên hiện tại).

1. **`backend-dotnet8` (Lead Backend Engineer):** Chuyên trách .NET 8 Clean Arch, CQRS MediatR, FluentValidation, EF Core PostgreSQL, Bounded Channel, Polly Retry.
2. **`frontend-react` (Lead Frontend Architect):** Chuyên trách React 19, Vite, React Router DOM v7, Tailwind CSS v4, Web Speech API, Buffer Screen Code-Switching, Canvas Kiosk Lockdown.
3. **`ai-pipeline-engineer` (AI & Voice Specialist):** Chuyên trách Gemini 1.5 Flash/Pro Prompt CoT, Structured JSON Outputs, Whisper STT integration, Cloudflare R2 Upload (`STT_MSSV.webm`), Audio Hash SHA-256.
4. **`qa-verifier` (Quality Assurance Lead):** Chuyên trách `dotnet test`, `npm run test`, NetArchTest kiểm thử ranh giới kiến trúc, trích xuất bằng chứng Exit Code (0).
5. **`docs-diagram-architect` (Technical Writer & Visual Architect):** Chuyên trách DrawIO, Mermaid (.mmd), SVG sequence diagrams, cập nhật Report 3 (SRS), SRS 47 Use Cases, Báo cáo Hội đồng.

---

## 🚀 9. QUY TRÌNH TÁC CHIẾN THEO CẤP ĐỘ S / M / L

* 🟢 **Mức S (Quick Fix / Sửa bug cục bộ / Scripts ≤ 3 files):**
  - Đọc file $\rightarrow$ Sửa trực diện $\rightarrow$ Targeted build/test pass. Bỏ qua 6 pha, xong ngay trong 1-2 phút.
* 🟡 **Mức M (Single Feature / Endpoint / Component 4–10 files):**
  - Khảo sát 5 ca biên + Viết test mới + Build & Module Test pass.
* 🔴 **Mức L (Epic lớn / Greenfield / Tái cấu trúc Core > 10 files):**
  - Áp dụng trọn vẹn Quy trình 6 Pha:
    `Pha 1: Spec-Locking` $\rightarrow$ `Pha 1.5: Scaffolding` $\rightarrow$ `Pha 2: Contract-First` $\rightarrow$ `Pha 3: Parallel Execution (Subagents)` $\rightarrow$ `Pha 4: Zero-Mock Audit` $\rightarrow$ `Pha 5: Hard Gate Live`.

---

## 📐 10. KIẾN TRÚC CLEAN ARCHITECTURE 4 TẦNG (.NET 8 BACKEND)

```
05_Source_Code/backend/src/
├── Domain/                   # Tầng Core Thuần Khiết (Zero External Dependencies)
│   ├── Entities/             # 30 thực thể tương ứng 30 bảng CSDL (UUID Primary Keys)
│   ├── Enums/                # DomainEnums (ExamTicketStatus, ApprovalStatus, ExamInputMode...)
│   └── Interfaces/           # Repository Interfaces & Domain Events
├── Application/              # Tầng Nghiệp Vụ Use Cases
│   ├── Common/               # Behaviors, Exceptions, Mappings
│   ├── Features/             # CQRS Commands & Queries theo từng Feature (MediatR)
│   │   ├── Practice/         # StartPracticeSession, SubmitPracticeAnswer...
│   │   ├── MockExams/        # StartMockExam, SubmitMockExam...
│   │   ├── Questions/        # GenerateQuestionsFromFlm, BatchSubmitReview...
│   │   └── OfficialExams/    # CheckInTicket, PublishGrades, AuditSubmission...
│   └── Interfaces/           # IApplicationDbContext, IAiEvaluationService, IStorageService
├── Infrastructure/           # Tầng Kết Nối Ngoại Vi & Công Nghệ
│   ├── Persistence/          # OralExamDbContext, Interceptors (OneWayLockInterceptor), Migrations
│   ├── Services/             # GeminiService (CoT 3 bước), WhisperService, CloudflareR2Service
│   └── BackgroundJobs/       # GradingQueueWorker (BoundedChannel 1,000 slots, Polly Retry)
└── API/                      # Tầng Trình Diễn Web API Gateway
    ├── Controllers/          # RESTful Endpoints chuẩn RFC 7807 Problem Details
    ├── Hubs/                 # SignalR Typed Hubs (/hubs/practice)
    ├── Middlewares/          # GlobalExceptionMiddleware, RequestLoggingMiddleware
    └── Program.cs            # Dependency Injection, Middleware Pipeline (CORS -> Https -> Auth)
```

---

## ⚡ 11. BỘ LỆNH MỘT CHẠM & CỔNG KIỂM CHỨNG (HARD VERIFICATION GATE)

Sau khi chỉnh sửa mã nguồn, Agent **BẮT BUỘC** chạy và xác thực Exit Code = 0 bằng bộ lệnh với đường dẫn tuyệt đối chuẩn xác:

```powershell
# 1. Build Backend Solution (.NET 8)
dotnet build "D:\Đồ Án\05_Source_Code\backend"

# 2. Chạy toàn bộ Tests Backend (xUnit)
dotnet test "D:\Đồ Án\05_Source_Code\backend" --verbosity normal

# 3. Biên dịch Frontend (React 19 Vite)
cd "D:\Đồ Án\05_Source_Code\frontend"; npm run build; cd ..
```

---

## 📚 12. BỘ QUY CHUẨN QUẢN TRỊ TÀI LIỆU TỐI GIẢN (DOCUMENTATION GOVERNANCE)

1. **Kiểm tra trước khi tạo mới (Search & Reuse First):** Khi nhận yêu cầu cập nhật hoặc viết tài liệu/sơ đồ mới, BẮT BUỘC kiểm tra toàn bộ workspace xem đã có tài liệu nào tương tự hoặc phục vụ mục đích đó chưa (`docs/MASTER_ARCHITECTURE.md`, `../02_Bao_cao_Reports/`, v.v.).
2. **Cổng tham vấn bắt buộc (Ask Before Creating New):** Nếu đã tồn tại tài liệu liên quan, BẮT BUỘC dừng lại hỏi người dùng: *"Đã có tài liệu [tên_tệp] liên quan đến vấn đề này. Bạn muốn sửa trực tiếp trong tài liệu đó hay tạo tài liệu mới?"*. Tuyệt đối không tự ý đẻ thêm file mới làm rác dự án.
3. **Quét toàn hệ thống khi có nội dung mới (Zero Blind Spot):** Khi cập nhật bất kỳ kiến thức, logic nghiệp vụ hay sơ đồ mới nào, BẮT BUỘC quét toàn bộ hệ thống (tất cả file `.md`, `.cs`, `.json`) để cập nhật đồng loạt, không để sót bất kỳ tài liệu nào bị cũ hay mâu thuẫn.
4. **Quy tắc Tối giản & Chống phân mảnh (Anti-Proliferation):** Giữ số lượng file ở mức tối thiểu. Nguồn sự thật duy nhất cho kiến trúc hệ thống là `docs/MASTER_ARCHITECTURE.md`. Các file nháp, file trung gian hoặc file đã được hợp nhất 100% phải được chuyển vào `../99_Archive/Legacy_Docs/`.
