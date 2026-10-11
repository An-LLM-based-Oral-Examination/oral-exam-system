# TỔNG HỢP MÔ TẢ 4 LUỒNG NGHIỆP VỤ CỐT LÕI (4 CORE MAIN FLOWS)
## HỆ THỐNG LUYỆN THI & ĐÁNH GIÁ VẤN ĐÁP BẰNG LLM (ORAL EXAM LLM SYSTEM)
### Đề Tài Tốt Nghiệp Capstone: FA26SE166 — Đại Học FPT TP.HCM (FPT SG)

> **Mã đề tài:** FA26SE166 | **Học kỳ:** Fall 2026 (FA26)  
> **Chuyên ngành:** Kỹ thuật Phần mềm (Software Engineering - SE) — Đại học FPT TP.HCM (FPT SG)  
> **Giảng viên hướng dẫn:** ThS. Nguyễn Thị Cẩm Hương (`huongntc2@fpt.edu.vn`)  
> **Đội ngũ kỹ sư thực hiện (4 thành viên):**  
> - 🧑 **Nguyễn Quang Thành:** Team Leader & Lead Backend Architect (Backend MF-01 + MF-02, Module Báo cáo Khảo thí FPT Excel .xlsx + PDF, Tích hợp hệ thống Clean Architecture)  
> - 🧑 **Nguyễn Trọng Tốt:** Backend Developer, AI Specialist & QA Lead (Auth Google OAuth PKCE, Backend MF-03, TOÀN BỘ Backend MF-04, Backend APIs vệ tinh Semester/Notification/User Mgmt, AI Core & Polly/DLQ, xUnit/NetArchTest)  
> - 🧑 **Nguyễn Đăng Hải:** DB Specialist & Frontend Developer (phụ trách Database 30 bảng PostgreSQL và dồn toàn lực phát triển Frontend; tuyệt đối không code C# Backend; Student Portal Dashboard, Lịch sử luyện tập & thi thử, UI Thi thử Voice-First MF-02, UI Duyệt đề MF-03, UI Ca thi & Thẩm định phúc khảo MF-04, User Mgmt FE-09, Semester CRUD FE-08)  
> - 🧑 **Lê Vũ Hoàng:** Lead Frontend Architect & Fullstack Coordinator (Kiến trúc Frontend Core React 19 + Tailwind v4, UI Luyện tập MF-01, Màn hình Kiosk phòng Lab MF-04, Màn hình Hậu kiểm Evidence Panel cho Giảng viên, Auth Google UI, Notification in-app FE-11, Dashboard FE-10)  
> **Thư mục sơ đồ Draw.io:** [**`05_Source_Code/docs/diagrams/`**](./diagrams/)  
> • Tệp Master 4 Tabs: [`CAPSTONE_FA26SE166_4_MAINFLOWS_ACTIVITY_DIAGRAMS.drawio`](./diagrams/CAPSTONE_FA26SE166_4_MAINFLOWS_ACTIVITY_DIAGRAMS.drawio)

---

## BẢNG TỔNG QUAN 4 LUỒNG NGHIỆP VỤ

| Mã Luồng | Tên Luồng Nghiệp Vụ | Đối Tượng Sử Dụng | Chốt Chặn Kỹ Thuật Trọng Tâm |
|:---:|:---|:---|:---|
| **MF-01** | **Luyện tập vấn đáp tương tác** *(Interactive Practice)* | Sinh viên | Chọn chế độ Upfront (`[Per-Question On-Demand]` vs `[Full-Session Progressive]`); Per-Question cho phép chọn đơn/tổ hợp mức độ (`easy`, `medium`, `hard`), cấp câu 1 khi tạo phiên rồi bốc tiếp On-Demand qua `POST /api/v1/practice/sessions/{id}/next-question` không ép chốt trước số câu; Thuật toán Anti-3-Consecutive ngẫu nhiên có ràng buộc (tối đa 2 câu cùng mức trong 3 câu liên tiếp); Kích hoạt follow-up đào sâu A2 khi điểm ranh giới $4.0 \le \text{Score} \le 8.0$ (1–5 câu do Admin cấu hình, mặc định 2 câu); Full-Session bốc trọn gói 3–10 câu (`MinMixedPracticeQuestions = 3`, `MaxMixedPracticeQuestions = 10` do Admin cấu hình) chia đều tăng dần Dễ $\to$ Trung bình $\to$ Khó, TUYỆT ĐỐI KHÔNG có follow-up; Màn hình đệm sửa lỗi Code-Switching do Admin cấu hình (`transcript_buffer_seconds`, từ 10–300s, mặc định 60s); Cơ chế Timeout 10 phút không tương tác (`SessionInactivityTimeoutMinutes = 10`), ghi nhận `last_activity_at`, quá 10 phút tự động hoàn tất an toàn, trả HTTP 410 Gone, bảo toàn điểm; Luyện tập KHÔNG lưu audio, chỉ lưu bản transcript đã sửa vào `practice_answers`; Hàng đợi phòng thủ 4 tầng Zero Data Loss. |
| **MF-02** | **Thi thử vấn đáp bấm giờ** *(Timed Mock Exam)* | Sinh viên | Nguồn đề rút từ kho câu hỏi luyện tập chung (`practice_questions`); Hạn ngạch thi thử do Trưởng BM cấu hình (`max_mock_exams_per_day`), không khóa cứng K=3; Cấu trúc đề & phân bổ Bloom do Trưởng BM cấu hình; Sinh viên chỉ chọn môn và tùy chọn Có/Không Follow-up trước khi thi (AI hỏi theo nội dung ngữ cảnh bài nói, có follow-up thời lượng dài hơn do Trưởng BM cấu hình); Voice-First Gate khóa cứng ô gõ phím; Đồng hồ kép chống gian lận; Đệm sửa từ (cấu hình động `transcript_buffer_seconds`, 10–300s, mặc định 60s); Hết giờ tự nộp: câu chưa làm tính là bỏ trống, không chấm; KHÔNG lưu audio, chỉ lưu transcript vào `mock_exam_answers`; Chấm AI 2 pha (Fast-Path $<10$s / Async Fallback); Instant Feedback Scorecard chi tiết từng câu theo Rubric tại kết quả và Lịch sử thi. |
| **MF-03** | **Quản lý ngân hàng đề & Barem Rubric** *(Question Bank & Rubric Studio)* | Giảng viên & Trưởng Bộ Môn | **Giảng viên và Trưởng BM sử dụng AI sinh câu hỏi từ FLM Syllabus theo CLO** (hoặc soạn thủ công); Tick chọn lưu vào 2 kho: `practice_questions` và/hoặc `exam_questions`; Barem Rubric chuẩn hóa $\sum \equiv 10.0$đ; Model Answer $\ge 50$ ký tự; AI Calibration Simulator; Gửi lên cho Bộ Môn (`POST /api/v1/questions/batch-submit-review`, `SUBMITTED_FOR_REVIEW`); Trưởng Bộ Môn thẩm định và Phê duyệt (`APPROVED`) / Yêu cầu sửa (`NEEDS_REVISION`) / Từ chối loại hẳn (`REJECTED`) vào ngân hàng câu hỏi môn học; Backend Gate HTTP 422; ACID Transaction. |
| **MF-04** | **Thi thật phòng Lab, Công bố điểm & Phúc khảo nội bộ** *(Lab Exam, Audit & Internal Appeals)* | Sinh viên, Giám thị, Giảng viên & Trưởng Bộ Môn | Trưởng Bộ Môn khởi tạo kỳ thi (`OfficialExamSession`), gán môn thi, cấu hình ca thi (ghi phòng lab ngay trên ca, gán người coi thi là GV hoặc giám thị, danh sách thí sinh lấy từ lớp học đã có), cấu hình Follow-up môn thi trong kỳ thi (1–5 câu, mặc định 2 câu) và `ExamInputMode` (`VoiceOnly`, `VoiceWithTranscriptEdit`); Định danh vé thi bằng `ip_address`; Fullscreen Kiosk Lockdown (chặn F12, onblur $\ge 3$ lần đình chỉ thi); Hardware Mic-Check 30s $\ge 60$dB; Niêm phong âm thanh bằng mã băm SHA-256 stream trực tiếp lên Cloudflare R2 định danh `STT_MSSV.webm`; Nộp bài Persist First < 100ms với trạng thái `SUBMITTED`, Kiosk khóa màn hình TUYỆT ĐỐI 0% HIỆN ĐIỂM và không cho khiếu nại tại chỗ; AI chấm điểm ngầm chỉ dựa trên bản transcript qua BoundedChannel 1000 slots RAM, DLQ Replay 5m; Chốt chặn AI Doubt Guard phân loại 2 nhóm: Nhóm 1 Đáng nghi ngờ (`is_suspicious == true` / `confidence_score < 0.70` hoặc fail) và Nhóm 2 Độ tin cậy cao; Giảng viên đối soát trên Evidence Panel (AudioURL, Transcript Whisper gốc, AI CoT) để nghe Waveform Player và chỉnh điểm kèm giải trình; Giảng viên Công bố điểm (Publish Grades) một lần duy nhất khi 100% sinh viên có điểm, kích hoạt One-Way Lock (HTTP 403); Sinh viên xem bảng điểm trên Student Portal (Xác nhận nhận điểm `ACKNOWLEDGED` HOẶC gửi đơn phúc khảo nội bộ `AppealRequest` trực tiếp trên hệ thống gán cho Trưởng Bộ Môn tiếp nhận và giao một Giảng viên chấm lại, One-Way Lock chừa đường cho GV được giao cập nhật điểm); Xuất báo cáo Khảo thí FPT cả file Excel (.xlsx) và file PDF có chữ ký số. |

---

## PHẦN 1. LUỒNG MF-01: SINH VIÊN LUYỆN TẬP VẤN ĐÁP TƯƠNG TÁC
*(MF-01: STUDENT INTERACTIVE PRACTICE PROCESS)*

* **Tài nguyên trực quan:** [Tệp sơ đồ Draw.io](./diagrams/MF01_Interactive_Practice.drawio)

### 1.1. Mục Tiêu Nghiệp Vụ
Giúp sinh viên chủ động luyện phản xạ thi vấn đáp 1-1 với trợ lý ảo AI (Google Gemini 1.5 Flash). Hệ thống hỗ trợ linh hoạt cả hai phương thức trả lời (nói qua micro hoặc gõ phím), cung cấp màn hình đệm do Admin cấu hình (`transcript_buffer_seconds`, từ 10–300s, mặc định 60s) để sinh viên sửa lỗi nhận diện thuật ngữ CNTT tiếng Anh (*Code-Switching*), và trao quyền chọn chế độ làm bài linh hoạt ngay từ đầu:
- **Luyện từng câu theo yêu cầu [Per-Question On-Demand]:** Sinh viên chọn mức độ đơn lẻ (Dễ, Trung bình, Khó) HOẶC bất kỳ tổ hợp nào (ví dụ: `["easy", "medium"]`, `["easy", "hard"]`, `["medium", "hard"]`, `["easy", "medium", "hard"]`). Phiên luyện tập cấp câu hỏi đầu tiên (Câu 1) khi bắt đầu, sau đó sinh viên trả lời và gọi API lấy câu tiếp theo theo nhu cầu (On-Demand qua `POST /api/v1/practice/sessions/{id}/next-question`), không bắt buộc chốt trước số lượng câu hỏi. Áp dụng **thuật toán ngẫu nhiên có ràng buộc (Anti-3-Consecutive)**: trong bất kỳ chuỗi 3 câu hỏi chính liên tiếp nào, tối đa chỉ có 2 câu chung mức độ (khi 2 câu liền trước cùng độ khó, lần bốc tiếp theo bắt buộc chuyển sang mức khác trong danh sách đã chọn). Không lặp lại câu hỏi sinh viên đã làm trong phiên (`Id NOT IN (...)`). Sau mỗi câu trả lời, kích hoạt câu hỏi chuyên sâu đào sâu (A2) khi điểm số thuộc khoảng ranh giới $4.0 \le \text{Score} \le 8.0$ theo cấu hình hệ thống của Admin (`max_follow_up_questions`, từ 1–5 câu, mặc định 2 câu; nếu điểm $< 4.0$ hoặc $> 8.0$ thì mở Scorecard ngay).
- **Luyện trọn gói theo tiến trình [Full-Session Progressive]:** Sinh viên nhập số lượng câu hỏi muốn làm từ 3 đến 10 câu (ràng buộc bởi `MinMixedPracticeQuestions = 3` và `MaxMixedPracticeQuestions = 10` do Admin cấu hình trong `system_configs`). Hệ thống tự động chia theo tiến trình từ Dễ đến Khó: chia đều số câu cho 3 mức (Dễ, Trung bình, Khó), bốc trọn gói toàn bộ câu hỏi ngay khi khởi tạo phiên và sắp xếp thứ tự phát vấn tăng dần từ Dễ $\to$ Trung bình $\to$ Khó (nếu kho đề không đủ câu cho bất kỳ mức nào, trả `HTTP 400 Bad Request` tiếng Việt rõ ràng, không tạo phiên rác). Chế độ này trả lời liền mạch không bị ngắt quãng, **TUYỆT ĐỐI KHÔNG có câu hỏi follow-up**, hỗ trợ nộp từng câu hoặc nộp trọn gói qua `batch-answers` để AI chấm tổng thể và trả bảng điểm tổng kết.
- **Cơ chế Timeout 10 phút không tương tác (Session Inactivity Timeout):** Cấu hình động qua bảng `system_configs` (`SessionInactivityTimeoutMinutes = 10`, mặc định 10 phút). Hệ thống cập nhật thời điểm `last_activity_at` (TIMESTAMPTZ) sau mỗi tương tác của sinh viên (khởi tạo phiên, lấy câu tiếp theo, nộp câu trả lời). Áp dụng cho cả 2 chế độ: nếu sinh viên không có bất kỳ tương tác nào trong hơn 10 phút, hệ thống tự động kết thúc phiên an toàn (`status = "completed"`), bảo toàn 100% dữ liệu và điểm số các câu đã làm (với Full-Session: các câu chưa làm tính là bỏ trống 0 điểm). Mọi thao tác gọi API tiếp theo sẽ nhận mã lỗi **HTTP 410 Gone** (Session Timed Out).
- **Lưu trữ & Lịch sử:** Luyện tập **KHÔNG lưu audio**, chỉ lưu bản transcript đã sửa vào `practice_answers`. Sinh viên xem lại lịch sử luyện tập trên Student Portal (FE-02) cùng màn hình với lịch sử thi thử.

### 1.2. Bốn Phân Làn Trách Nhiệm (4 Swimlanes)
1. **Lane 1: Student (Sinh viên)**:
   - Chọn môn, chế độ làm bài (`[Per-Question On-Demand]` chọn đơn/tổ hợp độ khó, cấp câu 1 và lấy tiếp On-Demand không ép chốt trước số câu; hoặc `[Full-Session Progressive]` 3–10 câu chia đều Dễ $\to$ Khó) $\to$ Trả lời bằng giọng nói hoặc gõ phím $\to$ Rà soát và chỉnh sửa transcript trên màn hình đệm do Admin cấu hình (`transcript_buffer_seconds`, mặc định 60s) $\to$ Bấm xác nhận nộp $\to$ Xem bảng điểm Scorecard $\to$ Quyết định trả lời câu hỏi chuyên sâu đào sâu (nếu chọn `[Per-Question]`, tối đa theo cấu hình Admin `max_follow_up_questions`, 1–5 câu, mặc định 2 câu) hoặc gọi lấy câu mới On-Demand / kết thúc phiên. Chú ý tương tác trong vòng 10 phút để tránh Inactivity Timeout.
2. **Lane 2: Client UI & Audio Engine (Giao diện Web & Xử lý Âm thanh)**:
   - Đọc to câu hỏi bằng Web Speech TTS $\to$ Thu âm và nhận diện giọng nói STT thời gian thực $\to$ Mở màn hình đệm kèm đồng hồ đếm ngược (do Admin cấu hình chung qua `system_configs` `TranscriptBufferSeconds`, từ 10–300s, mặc định 60s) $\to$ Điều hướng câu hỏi tiếp theo $\to$ Mở modal Scorecard và TTS đọc nhận xét phản hồi; hiển thị thông báo và scorecard bảo toàn điểm khi nhận HTTP 410 Gone.
3. **Lane 3: System Handler (Backend API & Hàng đợi Chịu lỗi)**:
   - Tiếp nhận khởi tạo phiên, quản lý `last_activity_at`, bốc câu 1 cho Per-Question hoặc chia đều 3–10 câu cho Full-Session $\to$ Xử lý `POST /next-question` On-Demand với thuật toán Anti-3-Consecutive $\to$ Kiểm tra Lazy Inactivity Timeout (10 phút quá hạn tự hoàn tất phiên an toàn và trả HTTP 410 Gone) $\to$ Lưu bài nộp tức thì xuống PostgreSQL với `status = 'PENDING'` ($< 100$ms) $\to$ Đẩy task vào hàng đợi in-memory BoundedChannel (1000 slots) $\to$ Điều phối Polly Retry ($2^n$) và Dead-Letter Queue (DLQ) chấm bù $\to$ Cập nhật điểm chính thức và phát realtime qua SignalR PracticeHub.
4. **Lane 4: AI Grading Service (Dịch vụ Chấm điểm AI)**:
   - Gemini 1.5 Flash phân tích câu trả lời theo barem Rubric 10.0 qua chuỗi suy luận CoT $\to$ Ép khuôn đầu ra JSON Schema $\to$ Trả về vector điểm tiêu chí, nhận xét định tính và sinh câu hỏi chuyên sâu đào sâu (trong chế độ `[Per-Question]`).

### 1.3. Diễn Giải Quy Trình Theo Từng Bước (Tên : Mục Đích)

#### 🔹 Giai đoạn 1: Khởi Tạo Phiên Luyện Tập (Initialize Session)
* **Start Practice (`st_start`)**:
  - Khởi đầu quy trình luyện tập vấn đáp từ trang môn học.
* **Select Subject & Practice Configuration (`st_select_topic`)**:
  - Sinh viên chọn Môn học (Course), hình thức luyện tập (Practice), chế độ làm bài (`[Per-Question On-Demand]` hoặc `[Full-Session Progressive]`). Với `[Per-Question On-Demand]`: sinh viên chọn mức độ đơn lẻ (`easy`, `medium`, `hard`) HOẶC bất kỳ tổ hợp nào (`["easy", "medium"]`, `["easy", "hard"]`, `["medium", "hard"]`, `["easy", "medium", "hard"]`), không bắt buộc chốt trước số lượng câu hỏi. Với `[Full-Session Progressive]`: sinh viên nhập số lượng câu hỏi từ 3 đến 10 câu (ràng buộc bởi `MinMixedPracticeQuestions = 3` và `MaxMixedPracticeQuestions = 10` do Admin cấu hình).
* **Select Grading Mode (`st_select_mode`)**:
  - Xác nhận chính sách chấm điểm: `[Per-Question On-Demand]` (chấm từng câu, có follow-up khi điểm 4.0–8.0) hoặc `[Full-Session Progressive]` (làm hết mới chấm, không follow-up).
* **Initialize Session (`ui_init_session`)**:
  - Khởi tạo trạng thái phiên phía client, kết nối SignalR PracticeHub và gửi request `POST /api/v1/practice/sessions` lên Backend với payload `{ courseId, difficulties, isFullSession, questionCount }`.
* **Prepare Question Set (`sys_fetch_q`)**:
  - Backend truy vấn CSDL: Với `[Per-Question On-Demand]`: bốc câu hỏi đầu tiên (Câu 1) theo độ khó đã chọn, lưu `selected_difficulties`, ghi nhận `last_activity_at = NOW()` và tạo bản ghi phiên `IN_PROGRESS`. Với `[Full-Session Progressive]`: bốc trọn gói 3–10 câu chia đều tăng dần Dễ $\to$ Trung bình $\to$ Khó, ghi nhận `last_activity_at = NOW()` và tạo bản ghi phiên `IN_PROGRESS`.
* **Check Media Permission (`ui_check_mic`)**:
  - Kiểm tra quyền micro: nếu có quyền (`[Mic Allowed]`) $\to$ phát âm thanh câu hỏi; nếu bị từ chối (`[Mic Denied]`) $\to$ chuyển sang gõ phím.
* **Play Question Audio (`ui_render_q`)**:
  - Hiển thị thẻ câu hỏi trên giao diện và dùng Web Speech TTS đọc to nội dung đề bài.
* **Choose Input Modality (`st_input_choice`)**:
  - Sinh viên chọn phương thức trả lời: phát biểu qua micro (`[Speak Voice]`) hoặc gõ văn bản (`[Type Text]`).

#### 🔹 Giai đoạn 2: Thu Âm, Nhận Diện STT & Vùng Đệm Hiệu Đính (Cấu Hình Động 10-300s)
* **Speak Voice Answer (`st_speak`)**:
  - Sinh viên phát biểu câu trả lời trực tiếp qua micro.
* **Transcribe Voice Stream (`ui_stt_stream`)**:
  - Web Speech STT bóc băng giọng nói sang văn bản theo thời gian thực ($< 0.5$s độ trễ).
* **Type Text Answer (`st_type_text`)**:
  - Sinh viên gõ trực tiếp câu trả lời bằng bàn phím (khi chọn gõ hoặc khi micro bị lỗi).
* **Review Dynamic Transcript (`st_fix_terms`)**:
  - Màn hình đệm đếm ngược (do Admin cấu hình chung qua `system_configs` `TranscriptBufferSeconds`, từ 10–300s, mặc định 60s) cho phép sinh viên rà soát và sửa lỗi phát âm thuật ngữ tiếng Anh (Code-Switching).
* **Confirm Submission (`st_confirm_submit`)**:
  - Sinh viên bấm nút xác nhận nộp câu trả lời hiện tại.

#### 🔹 Giai đoạn 3: Điều Phối Nộp Bài & Đánh Giá AI
* **Route by Grading Mode (`ui_route_mode`)**:
  - Điều phối nộp bài theo chế độ đã cấu hình:
    - Nếu chọn `[Per-Question]` $\to$ gửi câu hiện tại vào hàng đợi phòng thủ 4 tầng để AI chấm tức thì.
    - Nếu chọn `[Full-Session]` $\to$ lưu tạm câu trả lời vào bộ nhớ client (`ui_cache_answer`).

##### *Xử lý Chế độ Chấm từng câu (`[Per-Question]`):*
* **Grade with Gemini AI (`ai_cot_eval`)**:
  - Gemini 1.5 Flash suy luận CoT 3 bước đối chiếu Barem Rubric 10.0, trả về điểm thành phần và nhận xét định tính.
* **Save Graded & Broadcast (`sys_save_graded`)**:
  - Backend cập nhật trạng thái `GRADED` vào PostgreSQL và phát sự kiện qua SignalR `PracticeHub`.
* **Check Single Score & Follow-up (`ui_check_score_single`)**:
  - Đánh giá điểm số và điều kiện kích hoạt câu hỏi chuyên sâu trong chế độ `[Per-Question]`:
    - Nếu điểm số nằm trong khoảng ranh giới **$4.0 \le \text{Score} \le 8.0$** và số câu hỏi phụ chưa vượt quá giới hạn tối đa do Admin cấu hình (`max_follow_up_questions`, từ 1 đến 5 câu, mặc định 2 câu) $\to$ Kích hoạt câu hỏi chuyên sâu đào sâu lỗ hổng kiến thức (`ui_prompt_followup`).
    - Nếu điểm số **$\text{Score} < 4.0$** (chưa đạt yêu cầu cơ bản) hoặc **$\text{Score} > 8.0$** (đã nắm vững kiến thức), hoặc sinh viên đã hoàn thành tối đa số câu follow-up $\to$ Bỏ qua câu hỏi phụ, mở modal bảng điểm và feedback ngay (`ui_scorecard_modal`).
* **Prompt Deep-Dive (`ui_prompt_followup`)**:
  - Hiển thị câu hỏi chuyên sâu đào sâu vào lỗ hổng kiến thức để sinh viên giải trình thêm và củng cố kiến thức.
* **Decide Follow-up (`st_decide_followup`)**:
  - Sinh viên quyết định: đồng ý trả lời chuyên sâu (`[Accept Deep-Dive]`) $\to$ quay lại nói (`st_speak`); hoặc bỏ qua (`[Skip Follow-up]`) $\to$ xem bảng điểm (`ui_scorecard_modal`).
* **Show Scorecard Modal (`ui_scorecard_modal`)**:
  - Mở modal hiển thị bảng điểm và Web Speech TTS đọc to lời nhận xét của AI.
* **Review Scorecard (`st_review_scorecard`)**:
  - Sinh viên xem chi tiết điểm từng tiêu chí rubric và đọc phản hồi.
* **Next Decision (`st_next_decision`)**:
  - Sinh viên chọn:
    - Bấm làm câu tiếp theo (`[Next Question]`): Giao diện gọi endpoint `POST /api/v1/practice/sessions/{id}/next-question`. Backend thực hiện:
      1. Kiểm tra Inactivity Timeout: nếu thời gian từ `last_activity_at` đến hiện tại $> SessionInactivityTimeoutMinutes$ (10 phút), tự động cập nhật `status = 'completed'`, lưu kết quả các câu đã hoàn thành và trả lỗi **HTTP 410 Gone** (`Title = "Session Timed Out"`). Giao diện hiển thị thông báo phiên hết hạn an toàn và mở bảng điểm các câu đã làm.
      2. Nếu phiên còn hiệu lực: cập nhật `last_activity_at = NOW()`. Áp dụng **thuật toán Anti-3-Consecutive**: kiểm tra 2 câu gần nhất; nếu cả 2 câu có cùng độ khó $D$ và tổ hợp đã chọn có $> 1$ độ khó, loại trừ $D$ khỏi candidate pool để bốc sang độ khó khác; loại trừ các câu đã làm trong phiên (`Id NOT IN (...)`). Nếu độ khó khác hết câu thì fallback về $D$; nếu hết sạch toàn bộ câu thì trả `hasMoreQuestions: false`. Nạp câu hỏi mới sang `ui_render_q`.
    - Bấm kết thúc luyện tập (`[Finish Practice]`): Gọi `POST /api/v1/practice/sessions/{id}/complete`, hệ thống cập nhật `status = 'completed'` $\to$ hoàn tất tại `end_practice_per`.

##### *Xử lý Chế độ Luyện trọn gói tiến trình (`[Full-Session Progressive]`):*
* **Cache Answer Locally (`ui_cache_answer`)**:
  - Lưu câu trả lời vào state phía client, không ngắt quãng người dùng bằng câu hỏi phụ và tăng chỉ số câu hỏi ($i = i + 1$). Cập nhật `last_activity_at` khi gửi câu trả lời lên server.
* **Check Question Progress & Inactivity (`ui_session_check`)**:
  - Kiểm tra tiến độ phiên:
    - Nếu còn câu (`[More Questions]`, $i < N$) $\to$ nạp câu hỏi tiếp theo (`ui_render_q`).
    - Nếu đã trả lời hết toàn bộ câu (`[All Answered]`, $i = N$) $\to$ chuyển sang bước nộp toàn bài.
  - *Kiểm soát Inactivity Timeout:* Nếu sinh viên không tương tác quá 10 phút (`SessionInactivityTimeoutMinutes = 10`), hệ thống tự động hoàn tất phiên, chấm điểm các câu đã trả lời, các câu chưa kịp làm tính 0 điểm, trả về **HTTP 410 Gone** và hiển thị Scorecard tổng kết bảo toàn điểm.
* **Submit All Answers (`st_submit_all`)**:
  - Sinh viên bấm nút xác nhận nộp toàn bộ $N$ câu (`[Submit Batch]` hoặc gọi `POST /api/v1/practice/sessions/{id}/batch-answers`) qua hàng đợi phòng thủ 4 tầng để AI chấm một lượt. Cập nhật `last_activity_at = NOW()`.
* **Save Batch & Broadcast (`sys_save_batch`)**:
  - Backend lưu điểm số và nhận xét chi tiết của toàn bộ $N$ câu vào CSDL và phát thông báo SignalR.
* **Show Batch Summary (`ui_batch_summary`)**:
  - Render giao diện báo cáo tổng kết toàn phiên, hiển thị điểm số chi tiết từng câu, bảng điểm Scorecard Rubric và lời nhận xét sư phạm toàn diện.
* **Review Batch Summary (`st_review_batch`)**:
  - Sinh viên xem phân tích chi tiết kết quả Rubric của toàn bộ phiên luyện tập và hoàn tất tại `end_practice_batch`.

#### 🔹 Giai đoạn 4: Hạ Tầng Hàng Đợi Phòng Thủ 4 Tầng (Zero Data Loss)
* **Save Pending in DB (`sys_tier1_persist`)**:
  - Tầng 1 phòng thủ: lưu DB trạng thái `PENDING` trong $< 100$ms, trả `HTTP 202 Accepted` bảo toàn bài 100%.
* **Enqueue Grading Task (`sys_tier2_enqueue`)**:
  - Tầng 2 phòng thủ: đẩy task vào `BoundedChannel` 1,000 slots làm mịn tải, chống nghẽn server và Rate Limit API.
* **Check Polly Retry (`sys_polly_retry`)**:
  - Tầng 3 phòng thủ: Background Worker rút task, retry lũy thừa 3 lần ($2\text{s} \to 4\text{s} \to 8\text{s}$) nếu nghẽn mạng:
    - Nhánh `[Success]` $\to$ gửi sang AI chấm điểm.
    - Nhánh `[Retry Failed > 3]` $\to$ chuyển sang Dead-Letter Queue (`sys_dlq_save`).
* **Move to Dead-Letter (`sys_dlq_save`)**:
  - Tầng 4 phòng thủ: lưu bài vào bảng `dead_letter_queues` với cờ `PENDING_RETRY` tại `end_dlq` chờ Cron Job chấm bù.

---

## PHẦN 2. LUỒNG MF-02: SINH VIÊN THI THỬ VẤN ĐÁP BẤM GIỜ
*(MF-02: TIMED MOCK EXAM WITH QUOTA & VOICE-FIRST GATE)*

* **Tài nguyên trực quan:** [Tệp sơ đồ Draw.io](./diagrams/MF02_Timed_Mock_Exam.drawio)

### 2.1. Mục Tiêu Nghiệp Vụ
Mô phỏng áp lực phòng thi vấn đáp chính thức có giới hạn thời gian nghiêm ngặt. Áp dụng hai chốt chặn cốt lõi: **Daily Quota Guard** (hạn ngạch thi thử tối đa trong ngày do Trưởng Bộ Môn cấu hình theo môn `max_mock_exams_per_day` để chống lạm dụng token AI, không khóa cứng K=3) và **Voice-First Gate** (bắt buộc phải phát biểu qua micro, khóa cứng ô nhập liệu văn bản nhằm rèn luyện phản xạ phát âm thực tế dưới áp lực thời gian). Đặc biệt, hệ thống cung cấp **Instant Feedback Scorecard chi tiết** (Điểm từng tiêu chí Rubric, ưu điểm, thiếu sót kiến thức, gợi ý cải thiện) ngay tại màn hình kết quả và lưu trữ vĩnh viễn trong mục **Lịch sử thi** (`Exam History`) để phục vụ ôn tập cá nhân hóa.

### 2.2. Bốn Phân Làn Trách Nhiệm (4 Swimlanes)
1. **Lane 1: Student (Sinh viên)**:
   - Chọn môn thi thử $\to$ Chủ động tự chọn chế độ: Có Follow-up hoặc Không Follow-up $\to$ Xác nhận thông báo nếu đã hết lượt $\to$ Phát biểu qua micro $\to$ Chỉnh sửa từ vựng trên ô đệm $\to$ Bấm lưu từng câu $\to$ Bấm nộp bài thi $\to$ Xem Instant Feedback Scorecard chi tiết (tiêu chí Rubric, ưu điểm, lỗ hổng kiến thức, gợi ý cải thiện) hoặc xem lại bất cứ lúc nào trong Lịch sử thi.
2. **Lane 2: Client UI & Countdown Timer (Giao diện Web & Đồng hồ Đếm ngược)**:
   - Kiểm tra hạn ngạch $\to$ Bật đồng hồ đếm ngược $\to$ Hiển thị câu hỏi và khóa ô gõ phím $\to$ Bóc băng giọng nói STT $\to$ Mở khóa ô đệm sau khi nói $\to$ Tự động thu bài khi hết giờ (`00:00`) $\to$ Gửi bài nộp $\to$ Mở modal hiển thị Instant Feedback Scorecard chi tiết kèm biểu đồ năng lực.
3. **Lane 3: System Handler (Backend & Đồng hồ Chủ Server)**:
   - Truy vấn hạn ngạch trong PostgreSQL $\to$ Trừ 1 lượt thi và bốc ngẫu nhiên $N$ câu hỏi $\to$ Kích hoạt Server Master Timer (chống gian lận sửa giờ máy trạm) $\to$ Lưu bài nộp `PENDING` $\to$ Điều phối chấm nhanh Fast-Path hoặc đưa vào hàng đợi nền Async Fallback $\to$ Lưu trữ kết quả và Scorecard vào bảng `mock_exam_sessions` phục vụ tra cứu Lịch sử thi.
4. **Lane 4: AI Grading Service (Dịch vụ Chấm điểm AI)**:
   - Gemini 1.5 Flash chấm toàn bộ $N$ câu trả lời theo Barem Rubric 10.0 qua chuỗi CoT và trả về điểm số tổng hợp kèm vector điểm tiêu chí, phân tích ưu điểm (strengths), thiếu sót kiến thức (weaknesses) và gợi ý cải thiện hành động (actionable suggestions) cho từng câu.

### 2.3. Diễn Giải Quy Trình Theo Từng Bước (Tên : Mục Đích)

#### 🔹 Giai đoạn 1: Kiểm Tra Hạn Ngạch & Khởi Tạo Đề Thi
* **Start Mock Exam (`M01`)**:
  - Khởi đầu quy trình thi thử vấn đáp có bấm giờ.
* **Select Subject & Follow-up Mode (`M02`)**:
  - Sinh viên chọn môn học thi thử (ví dụ: `PRN231`) và chủ động tự chọn chế độ: `Có Follow-up` (AI hỏi chuyên sâu ngữ cảnh đào sâu) hoặc `Không Follow-up` (thi thẳng tính giờ bình thường). Sinh viên không chọn topic và không chọn độ khó (cấu trúc đề và phân bổ Bloom do Trưởng Bộ Môn cấu hình). Lựa chọn này được lưu vào trường `mock_exam_sessions.has_follow_up`. Nếu chọn Có Follow-up, thời lượng ca thi sẽ dài hơn theo cấu hình của Trưởng Bộ Môn.
* **Check Daily Quota (`M14`)**:
  - Client gửi yêu cầu kiểm tra hạn ngạch thi trong ngày lên Backend API.
* **Query Daily Quota (`M25`)**:
  - Backend truy vấn bảng hạn ngạch `mock_exam_quotas` trong PostgreSQL để lấy số lượt thi trong ngày ($K$) đối chiếu với giới hạn do Trưởng Bộ Môn cấu hình (`max_mock_exams_per_day`).
* **Check Quota Status (`M25_Check`)**:
  - Đánh giá số lượt thi:
    - Nếu hết lượt (`[Quota Exceeded]`, $K \ge \text{Limit}$) $\to$ hiển thị thông báo hết lượt (`M15`) $\to$ sinh viên xác nhận (`M03`) $\to$ kết thúc tại `M04`.
    - Nếu còn lượt (`[Quota Available]`, $K < \text{Limit}$) $\to$ chuyển sang trừ lượt và bốc đề (`M26`).
* **Display Quota Alert (`M15`)**:
  - Hiển thị cảnh báo đã dùng hết hạn ngạch thi thử của môn học trong ngày hôm nay theo quy định của Trưởng Bộ Môn.
* **Confirm Daily Limit (`M03`)**:
  - Sinh viên xác nhận thông báo để quay về Student Portal hoặc chế độ luyện tập tự do.
* **Deduct Quota & Fetch Set (`M26`)**:
  - Backend trừ 1 lượt thi ($K = K + 1$), bốc ngẫu nhiên câu hỏi từ kho câu hỏi luyện tập chung (`practice_questions`) theo cấu trúc đề và phân bổ Bloom do Trưởng Bộ Môn cấu hình (bảo toàn kho `exam_questions` bảo mật chỉ dành riêng cho MF-04) và tạo bản ghi ca thi `IN_PROGRESS` lưu kèm `has_follow_up`.
  - Thi thử **KHÔNG lưu audio**, chỉ lưu bản *transcript* vào `mock_exam_answers`.

#### 🔹 Giai đoạn 2: Khởi Chạy Đồng Hồ Kép & Chốt Chặn Voice-First Gate
* **Start Server Timer (`M27`)**:
  - Backend khởi động đồng hồ chủ Server Master Timer ghi nhận thời gian bắt đầu và hết hạn chống gian lận chỉnh giờ máy trạm.
* **Start Countdown Timer (`M16`)**:
  - Client khởi động đồng hồ đếm ngược trực quan hiển thị trên góc màn hình (thời lượng được tính toán dựa trên việc có hay không bật follow-up do Trưởng BM cấu hình).
* **Show Question & Lock Input (`M17`)**:
  - Chốt chặn Voice-First: hiển thị đề thi và khóa cứng ô gõ phím, bắt buộc phải trả lời qua micro.
* **Speak Voice Answer (`M05`)**:
  - Sinh viên nhấn nút micro và phát biểu câu trả lời thi.
* **Transcribe Voice Stream (`M18`)**:
  - Web Speech STT bóc băng giọng nói streaming sang văn bản thời gian thực ($< 0.5$s độ trễ).
* **Unlock Buffer Time (`M19`)**:
  - Dừng nói $\to$ hệ thống mở khóa ô văn bản trong khoảng thời gian đệm (cấu hình động `transcript_buffer_seconds`, từ 10–300s, mặc định 60s) để sinh viên rà soát bài làm.
* **Fix Code-Switching (`M07`)**:
  - Sinh viên gõ chỉnh sửa nhanh các thuật ngữ tiếng Anh bị nhận diện nhầm âm.
* **Save Question Answer (`M08`)**:
  - Sinh viên bấm lưu câu trả lời hiện tại xuống CSDL trước khi chuyển sang câu tiếp theo.

#### 🔹 Giai đoạn 3: Điều Hướng Tiến Độ & Thu Bài Thi
* **Check Follow-up Mode Selected (`M_CheckSubject`)**:
  - Hệ thống kiểm tra chế độ do sinh viên chủ động lựa chọn lúc bắt đầu thi (`mock_exam_sessions.has_follow_up`):
    - Nếu sinh viên chọn Có Follow-up (`has_follow_up = true`): AI tiến hành phân tích ngữ nghĩa và cấu trúc câu trả lời (`M_AIEval`).
    - Nếu sinh viên chọn Không Follow-up (`has_follow_up = false`): Bỏ qua toàn bộ câu hỏi phụ, chuyển thẳng sang kiểm tra tiến độ (`M20`).
* **AI Evaluates Answer (`M_AIEval`)**:
  - Dịch vụ AI phân tích ngữ nghĩa và cấu trúc câu trả lời vừa phát biểu để quyết định hướng đi tiếp.
* **Verify Follow-up Need (`M_NeedFollowup`)**:
  - Đánh giá chất lượng và chiều sâu câu trả lời: Khi sinh viên chọn `has_follow_up = true`, AI phân tích nội dung câu trả lời. Nếu phát hiện câu trả lời chưa rõ ràng, thiếu luận điểm cốt lõi hoặc cần đào sâu phản biện theo ngữ cảnh (`[Needs Follow-up]`), AI kích hoạt câu hỏi chuyên sâu (hoàn toàn theo ngữ cảnh nội dung câu nói, KHÔNG phụ thuộc vào mốc điểm số 4.0 - 8.0, số lượng câu hỏi phụ tối đa cấu hình động do Trưởng Bộ Môn thiết lập `max_follow_up_questions`); ngược lại nếu câu trả lời đã đầy đủ, trọn vẹn thì chuyển tiếp sang câu mới (`[Good Answer - No Follow-up]`).
* **Play Follow-up Question (`M_PlayFollowup`)**:
  - Web Speech TTS phát âm thanh câu hỏi chuyên sâu.
* **Speak Follow-up Answer (`M_SpeakFollowup`)**:
  - Sinh viên trả lời câu hỏi chuyên sâu qua micro.
* **Transcribe Follow-up (`M_TranscribeFollowup`)**:
  - Bóc băng audio của câu hỏi chuyên sâu thành văn bản.
* **Unlock Buffer Time (`M_UnlockFollowup`)**:
  - Mở khóa màn hình đệm (cấu hình động `transcript_buffer_seconds`, 10–300s, mặc định 60s) để sinh viên sửa lỗi nhận diện.
* **Fix Follow-up Text (`M_FixFollowup`)**:
  - Sinh viên dùng bàn phím để fix lỗi Code-Switching.
* **Check Follow-up Count (`M_CheckFollowupCount`)**:
  - Kiểm tra xem đã hỏi đủ số lượng câu hỏi phụ tối đa theo cấu hình do Trưởng Bộ Môn thiết lập chưa. Nếu chưa thì vòng lại, nếu đủ thì chuyển qua [Follow-up Done].
* **Check Exam Progress (`M20`)**:
  - Đánh giá tiến độ bài thi:
    - Nếu còn câu (`[More Questions]`) $\to$ nạp câu tiếp theo (`M17`).
    - Nếu làm xong hết (`[All Answered]`) $\to$ sinh viên bấm nộp bài thi (`M09`).
    - Nếu hết giờ (`[Timer Timeout 00:00]`) $\to$ tự động cưỡng bức nộp bài (`M21`). **Các câu hỏi chưa kịp làm tính là bỏ trống, không chấm**.
* **Submit Exam (`M09`)**:
  - Sinh viên chủ động xác nhận nộp bài thi khi hoàn thành toàn bộ câu hỏi.
* **Auto-Submit on Timeout (`M21`)**:
  - Bộ giám sát `AutoSubmitGuard` tự động thu toàn bộ bài làm và khóa màn hình khi đồng hồ chạm `00:00`. **Các câu chưa làm được ghi nhận bỏ trống và không chấm**.
* **Send Exam Submission (`M22`)**:
  - Client đóng gói toàn bộ bài làm gửi lên Backend API.

#### 🔹 Giai đoạn 4: Chấm Điểm Thông Minh 2 Pha (Dual-Path)
* **Save Pending in DB (`M28`)**:
  - Lưu DB trạng thái `PENDING` trong $< 100$ms, trả `HTTP 202 Accepted` bảo toàn bài 100%.
* **Grade with Gemini AI (`M32`)**:
  - Gemini 1.5 Flash thực hiện suy luận CoT chấm theo Barem Rubric 10.0 cho từng câu hỏi.
* **Route by System Load (`M29_Load`)**:
  - Điều phối chấm theo tải hệ thống:
    - Nhánh tải thấp (`[Fast-Path: Low Load]`): AI phản hồi $< 10$s $\to$ lưu điểm `GRADED` kèm Scorecard chi tiết (`M30`) $\to$ mở modal Instant Feedback Scorecard (`M23`) $\to$ sinh viên xem điểm và nhận xét sư phạm (`M10`) $\to$ kết thúc tại `M11`.
    - Nhánh tải cao (`[Async Fallback: High Load]`): hệ thống quá tải $\to$ đưa vào hàng đợi nền Background Worker chấm ngầm (`M31`) $\to$ hiện thông báo an tâm xem lại kết quả và Instant Feedback Scorecard tại Lịch sử thi (`M24`) $\to$ sinh viên xác nhận (`M12`) $\to$ kết thúc tại `M13`, giải phóng client ngay lập tức.
* **Save Graded & Broadcast (`M30`)**:
  - Backend lưu điểm chính thức, Scorecard chi tiết vào CSDL (`mock_exam_sessions`) và phát SignalR mở modal Scorecard.
* **Show Scorecard Modal (`M23`)**:
  - Giao diện mở bung modal Instant Feedback Scorecard chi tiết từng câu: Điểm thành phần theo Rubric, phân tích Điểm mạnh (Strengths), Thiếu sót kiến thức (Weaknesses/Knowledge Gaps), và Gợi ý cải thiện hành động (Actionable Suggestions).
* **Review Scorecard (`M10`)**:
  - Sinh viên xem bảng điểm Instant Feedback Scorecard chi tiết và có thể truy cập lại bất cứ lúc nào trong mục Lịch sử thi (`Exam History`).
* **Enqueue Background Task (`M31`)**:
  - Đưa bài thi vào hàng đợi nền để xử lý chấm điểm bất đồng bộ khi tải cao, bảo đảm kết quả và Instant Feedback Scorecard đầy đủ được lưu vào CSDL.
* **Show Async Notice (`M24`)**:
  - Hiển thị thông báo bài thi đã lưu an toàn và kết quả kèm Instant Feedback Scorecard chi tiết sẽ hiển thị tại mục Lịch sử thi.
* **Confirm Notice (`M12`)**:
  - Sinh viên bấm xác nhận thông báo để rời phòng thi thử, an tâm tra cứu lại trong Lịch sử thi.

---

## PHẦN 3. LUỒNG MF-03: QUẢN LÝ NGÂN HÀNG CÂU HỎI & BAREM RUBRIC
*(MF-03: QUESTION BANK & RUBRIC STUDIO — NÂNG CẤP FLM AI QUESTION GENERATOR)*

* **Tài nguyên trực quan:** [Tệp sơ đồ Draw.io](./diagrams/MF03_Question_Bank_Rubric.drawio)

### 3.1. Mục Tiêu Nghiệp Vụ
Cung cấp công cụ chuyên biệt cho **Giảng viên (`lecturer`)** và **Trưởng Bộ Môn (`department_head`)** thiết kế, quản lý ngân hàng câu hỏi vấn đáp và barem chấm điểm Rubric theo **chuẩn thang điểm 10.0 bắt buộc ($\sum \equiv 10.0$đ)**.

Đặc biệt, hệ thống nâng cấp tính năng đột phá: **Sinh câu hỏi tự động từ API FLM của FPT (FPT Learning Material)** hoặc Đề cương môn học. **Giảng viên và Trưởng Bộ Môn sử dụng AI sinh câu hỏi từ FLM Syllabus theo CLO** (hoặc biên soạn thủ công), sau đó tinh chỉnh Barem Rubric 10.0đ theo ý mình, chỉnh sửa đề bài, tiêu chí rubric, câu trả lời mẫu, và **tick chọn 2 kho câu hỏi: `practice_questions` và/hoặc `exam_questions`**:
1. **Nội dung đề bài & Cấp độ nhận thức Bloom:** Phân bổ khoa học từ cấp độ 1 (Remember) đến cấp độ 6 (Create).
2. **Barem Rubric chuẩn hóa theo thiết kế riêng:** Danh sách các tiêu chí con phân rã chi tiết với tổng điểm $\sum = 10.0$đ bất biến.
3. **Câu trả lời mẫu (Model Answer $\ge 50$ ký tự):** Đáp án chuẩn mực làm căn cứ đối chiếu AI kèm danh sách các luận điểm then chốt (Key points).

Sau khi sinh tự động, Giảng viên được cung cấp giao diện **Xem trước (Preview)** trực quan, tùy chỉnh nội dung/tiêu chí Barem nếu cần, kiểm chứng độ nhạy bằng **AI Calibration Simulator**. Sau khi tick chọn kho lưu trữ (`practice_questions` và/hoặc `exam_questions`), Giảng viên ấn **Gửi lên cho Bộ Môn (`Submit to Department Head` / `POST /api/v1/questions/batch-submit-review`)**, câu hỏi chuyển sang trạng thái `SUBMITTED_FOR_REVIEW`.

**Trưởng Bộ Môn (`department_head`)** mở danh sách câu hỏi chờ duyệt của các giảng viên $\to$ Thẩm định, xem xét $\to$ Bấm **"Phê duyệt" (`APPROVED`, lưu chính thức vào ngân hàng đề)** hoặc bấm **"Yêu cầu chỉnh sửa" (`NEEDS_REVISION`, kèm review_notes $\ge 10$ ký tự trả về giảng viên sửa)** hoặc **"Từ chối loại hẳn" (`REJECTED`, kèm review_notes $\ge 10$ ký tự loại khỏi ngân hàng đề)**. Trưởng Bộ Môn cũng có thể tự sinh/soạn và trực tiếp phê duyệt lưu vào CSDL với nguồn gốc ghi nhận `source = 'flm_api'` hoặc `source = 'manual'`.

### 3.2. Bốn Phân Làn Trách Nhiệm (4 Swimlanes)
1. **Lane 1: Department Head & Instructor (Trưởng Bộ Môn & Giảng viên)**:
   - Xác thực danh tính qua JWT (`Role = department_head`, `lecturer` hoặc `admin`).
   - Lựa chọn 1 trong 4 nhánh tác vụ:
     - *Nhánh A (Tra cứu / Lọc):* Lọc câu hỏi theo Môn học, Trạng thái phê duyệt (`DRAFT`, `SUBMITTED_FOR_REVIEW`, `APPROVED`, `NEEDS_REVISION`, `REJECTED`), Hình thức thi, Độ khó và Cấp độ Bloom 1–6.
     - *Nhánh B (Chỉnh sửa câu hỏi):* Chọn câu hỏi có sẵn hoặc câu hỏi bị yêu cầu sửa, nạp dữ liệu cũ và cập nhật nội dung/barem.
     - *Nhánh C (Soạn thủ công):* Nhập đề bài, Model Answer $\ge 50$ ký tự, thiết lập tiêu chí Barem 10.0đ, tick chọn 2 kho (`practice_questions` / `exam_questions`).
     - *Nhánh D (Sinh tự động từ FLM API — Mở quyền Giảng viên & Trưởng Bộ Môn):* Chọn Môn học $\to$ Dùng CLOs có sẵn trên Syllabus (hoặc chọn Topic rồi chọn CLO) $\to$ Cấu hình số lượng, độ khó, mức Bloom $\to$ Kích hoạt AI Generator $\to$ Xem trước (Preview) $\to$ Tinh chỉnh theo barem riêng $\to$ Tick chọn 2 kho (`practice_questions` / `exam_questions`) $\to$ Kiểm thử qua AI Calibration Simulator $\to$ Giảng viên ấn "Gửi lên cho Bộ Môn" (`Submit to Department Head`) $\to$ Trưởng Bộ Môn thẩm định và Phê duyệt / Yêu cầu sửa.
   - Thử nghiệm Barem qua AI Calibration Simulator và gửi duyệt hoặc bấm Lưu chính thức.
2. **Lane 2: Question Studio & Approval UI (Giao diện Studio Quản trị & Cổng Thẩm Định)**:
   - Render giao diện danh mục câu hỏi, form biên soạn và cây đề cương CLOs từ FLM Adapter.
   - Tính tổng điểm real-time: Tính $\sum \text{MaxScore}_j$, hiển thị cảnh báo đỏ và khóa cứng nút Lưu/Gửi duyệt nếu lệch 10.0đ.
   - Modal Preview câu hỏi sinh từ FLM: Hiển thị trực quan side-by-side đề bài, cấp độ Bloom, Model Answer, Key points và Barem Rubric 10.0 cho từng câu hỏi.
   - Hiển thị kết quả chấm thử của AI Simulator.
   - Giao diện Thẩm định Phê duyệt dành cho Trưởng Bộ Môn (Approval Studio): Hiển thị danh sách câu hỏi `SUBMITTED_FOR_REVIEW`, nút "Phê duyệt" (`APPROVED`), form "Yêu cầu chỉnh sửa" (`NEEDS_REVISION`) và nút "Từ chối loại hẳn" (`REJECTED`) kèm ô nhập lý do góp ý (review_notes $\ge 10$ ký tự).
3. **Lane 3: Backend API & FLM Adapter Gateway (Cổng Kiểm định Backend & Adapter)**:
   - Xác thực quyền RBAC: Cho phép cả `lecturer` và `department_head` gọi API trích xuất đề cương và sinh câu hỏi từ FLM.
   - Cung cấp endpoint gửi duyệt: `POST /api/v1/questions/batch-submit-review` chuyển trạng thái `SUBMITTED_FOR_REVIEW` và gán `submitted_by`.
   - Cung cấp endpoint thẩm định Trưởng bộ môn: `POST /api/v1/questions/{id}/review-decision` (hoặc batch) xử lý `APPROVED` / `NEEDS_REVISION` / `REJECTED` kèm `review_notes`.
   - Thẩm định độc lập tổng điểm $\sum = 10.0$đ và Model Answer $\ge 50$ ký tự qua FluentValidation (chặn `HTTP 422 Unprocessable Entity` nếu vi phạm).
   - Xử lý ACID Transaction ghi nhận đồng thời Questions, Rubrics, RubricCriteria và AuditLog vào PostgreSQL 16+.
4. **Lane 4: Database & Gemini AI Engine (Cơ sở Dữ liệu & AI Engine)**:
   - **Google Gemini 1.5 Flash:** (1) Phân tích dữ liệu đề cương FLM, tự động sinh bộ câu hỏi vấn đáp kèm Rubric 10.0đ, Model Answer và Key points; (2) Chấm thử nghiệm câu trả lời mẫu trong AI Simulator để hiệu chuẩn Barem.
   - **PostgreSQL 16+:** Lưu trữ bền vững dữ liệu câu hỏi với các trường trạng thái phê duyệt (`approval_status`, `submitted_by`, `approved_by`, `review_notes`), barem rubric và lịch sử kiểm toán `system_audit_logs`.

### 3.3. Diễn Giải Quy Trình Theo Từng Bước (Tên : Mục Đích)

#### 🔹 Giai đoạn 1: Xác Thực Quyền Hạn & Điều Hướng 4 Nhánh Tác Vụ
* **Start Question Bank (`Q01`)**:
  - Trưởng Bộ Môn hoặc Giảng viên truy cập phân hệ quản lý ngân hàng câu hỏi và rubric.
* **Verify RBAC Role (`Q02`)**:
  - Hệ thống xác thực claims qua JWT (`Role` hợp lệ: `department_head`, `lecturer` hoặc `admin`).
* **Open Question Studio (`Q03`)**:
  - Question Studio mở không gian làm việc chuyên dụng theo phân quyền.
* **Choose Authoring Action (`Q04`)**:
  - Người dùng lựa chọn 1 trong 4 nhánh tác vụ chính:
    - **Nhánh 1 `[Browse / Filter]`:** Lọc tra cứu câu hỏi theo trạng thái duyệt (`DRAFT`, `SUBMITTED_FOR_REVIEW`, `APPROVED`, `NEEDS_REVISION`, `REJECTED`) (`Q05`) $\to$ hiển thị danh sách (`Q06`).
    - **Nhánh 2 `[Edit Question]`:** Chọn câu hỏi cần sửa hoặc câu hỏi bị yêu cầu chỉnh sửa (`Q07`) $\to$ truy vấn DB (`Q08`) $\to$ nạp lên form (`Q09`) $\to$ sang bước thiết lập barem (`Q11`).
    - **Nhánh 3 `[Create Manual Question]`:** Soạn câu hỏi thủ công (`Q10`) $\to$ sang bước nhập nội dung và barem (`Q11`).
    - **Nhánh 4 `[Generate from FLM API]`:** Kích hoạt tính năng sinh tự động từ đề cương FLM (`Q_FLM_01`, mở cho cả `lecturer` và `department_head`).

#### 🔹 Giai đoạn 2: Nhánh Sinh Câu Hỏi Từ FLM API, Tùy Chỉnh Barem & Quy Trình Gửi Duyệt Bộ Môn
* **Verify Author Privilege (`Q_FLM_01`)**:
  - Backend kiểm tra JWT Claim `role in ["department_head", "lecturer", "admin"]`. Nếu người dùng mang vai trò khác (`proctor`, `student`) $\to$ từ chối bằng `HTTP 403 Forbidden` (`Q_FLM_01_Err`), UI hiển thị banner cảnh báo và quay về menu Studio (`Q04`).
* **Fetch Syllabus & CLOs from FLM (`Q_FLM_02`)**:
  - Hệ thống gọi `GET /api/v1/flm/courses/{courseId}/syllabus` kết nối cổng FLM Adapter để lấy cây Đề cương, danh sách CLOs và các bài học/chủ đề kiến thức.
  - *Cơ chế phân trang & lọc:* Hỗ trợ query parameters `pageNumber`, `pageSize`, `searchTopic` và `cloCode` để tải mượt mà các môn học quy mô lớn (> 50 bài học/chủ đề như PRN211, SWP391, PRN231).
  - *Cơ chế chịu lỗi Polly & Xử lý ngoại lệ:* Áp dụng chính sách Polly Retry 3 lần theo cấp số nhân (2s, 4s, 8s). Nếu cổng FLM API bị mất kết nối hoặc trả về dữ liệu hỏng $\to$ trả về `HTTP 502 Bad Gateway`; nếu cạn thời gian chờ sau 3 lần retry (Attempt Timeout 10s / Total Request Timeout 60s) $\to$ trả về `HTTP 504 Gateway Timeout`, UI hiển thị thông báo hỗ trợ người dùng.
* **Select Target CLOs & Topics (`Q_FLM_03`)**:
  - AI sinh câu hỏi, rubric và sample answer trực tiếp từ các CLOs có sẵn trên Syllabus của FLM. Giảng viên không bắt buộc phải chọn thủ công từng CLO1, CLO2; bấm sinh là hệ thống tự động bám sát toàn bộ CLOs của môn. Nếu muốn thu hẹp phạm vi, Giảng viên có thể chọn Topic/chủ đề trước, sau đó mới chọn CLOs tương ứng. Hệ thống chỉ đọc syllabus từ FLM, tuyệt đối không ghi ngược lên FLM.
* **Configure Generation Parameters (`Q_FLM_04`)**:
  - Cấu hình số lượng câu hỏi cần sinh, phân bổ độ khó (Dễ / Trung bình / Khó) và mức nhận thức Bloom mục tiêu (Bloom 1 đến 6).
* **Dispatch FLM Generation Request (`Q_FLM_05`)**:
  - Gửi request `POST /api/v1/questions/generate-from-flm` sang Backend API.
* **AI Auto-Generate Questions & Rubrics (`Q_FLM_06`)**:
  - Gemini 1.5 Flash phân tích cấu trúc đề cương và CLOs (kèm cơ chế Polly Retry 3 lần: 2s, 4s, 8s; phân định Attempt Timeout 30s / Total Request Timeout 90s; phân biệt lỗi kết nối/malformed JSON `HTTP 502 Bad Gateway` và lỗi quá thời gian chờ `HTTP 504 Gateway Timeout`):
    1. Sinh câu hỏi vấn đáp có tính phân hóa cao bám sát chuẩn đầu ra.
    2. Thiết lập Barem Rubric chuẩn hóa: Phân rã các tiêu chí con với tổng điểm $\sum = 10.0$đ.
    3. Soạn Câu trả lời mẫu (Model Answer $\ge 50$ ký tự) bao quát các luận điểm mấu chốt (Key points).
* **Render Draft Preview Studio (`Q_FLM_07`)**:
  - Giao diện mở Modal Preview hiển thị danh sách các câu hỏi dự thảo kèm Barem 10.0đ và Model Answer.
* **Review, Customize Rubric & Tick Target Question Pools (`Q_FLM_08`)**:
  - Giảng viên tự thiết kế theo Barem riêng của mình, tùy chỉnh câu hỏi, tiêu chí rubric và câu trả lời mẫu (vẫn tuân thủ bất biến $\sum = 10.0$đ và Model Answer $\ge 50$ ký tự). Có thể chạy AI Calibration Simulator để kiểm thử câu trả lời mẫu đối chiếu với Barem trước khi gửi duyệt.
  - **Tick chọn kho câu hỏi đích:** Giảng viên tick chọn 2 ô kho câu hỏi: `practice_questions` (kho luyện tập & thi thử) và `exam_questions` (kho thi thật phòng Lab). Có thể tick chọn 1 trong 2 ô hoặc cả 2 ô cùng lúc.
* **Submit to Department Head (`Q_FLM_09_Lecturer`)**:
  - Giảng viên sau khi rà soát và hoàn thiện sẽ ấn nút **"Gửi lên cho Bộ Môn" (`POST /api/v1/questions/batch-submit-review`)** $\to$ Hệ thống lưu các câu hỏi ở trạng thái `SUBMITTED_FOR_REVIEW`, ghi nhận `submitted_by = current_user_id` và gửi thông báo chờ duyệt tới Trưởng Bộ Môn.
* **Department Head Review & Approval Decision (`Q_FLM_10_DeptHead`)**:
  - Trưởng Bộ Môn mở danh sách câu hỏi chờ duyệt của các giảng viên $\to$ Thẩm định nội dung đề bài, barem 10.0đ và câu trả lời mẫu:
    - *Nhánh Phê duyệt:* Trưởng Bộ Môn bấm **"Phê duyệt" (`POST /api/v1/questions/{id}/review-decision` với decision='APPROVED')** $\to$ Backend kiểm định FluentValidation ($\sum = 10.0$đ, Model Answer $\ge 50$ ký tự), mở ACID Transaction chuyển trạng thái câu hỏi thành `APPROVED`, ghi nhận `approved_by = current_user_id` và chính thức lưu vào ngân hàng câu hỏi môn học, phản hồi `HTTP 200 OK` / `HTTP 201 Created` (`Q30`).
    - *Nhánh Yêu cầu chỉnh sửa:* Trưởng Bộ Môn bấm **"Yêu cầu chỉnh sửa" (`POST /api/v1/questions/{id}/review-decision` với decision='NEEDS_REVISION' kèm `review_notes`)** $\to$ Hệ thống chuyển trạng thái câu hỏi thành `NEEDS_REVISION`, lưu lý do góp ý vào `review_notes`, gửi thông báo về cho Giảng viên để mở lại form `Q_FLM_08` tinh chỉnh và gửi lại.
    - *Nhánh Từ chối loại hẳn:* Trưởng Bộ Môn bấm **"Từ chối" (`POST /api/v1/questions/{id}/review-decision` với decision='REJECTED' kèm `review_notes`)** $\to$ Hệ thống chuyển trạng thái câu hỏi thành `REJECTED`, lưu lý do vào `review_notes` và loại hẳn câu hỏi khỏi danh sách đề.
  - *(Trường hợp Trưởng Bộ Môn trực tiếp tạo câu hỏi):* Có thể bấm nút "Phê duyệt hàng loạt" (`POST /api/v1/questions/batch-approve`) để trực tiếp thẩm định và lưu thẳng vào CSDL ở trạng thái `APPROVED`.

#### 🔹 Giai đoạn 3: Soạn Thảo Đề Bài Thủ Công & Chốt Chặn Barem 10.0
* **Enter Question & Model Answer (`Q11`)**:
  - Giảng viên nhập nội dung đề bài và câu trả lời mẫu chuẩn mực (Model Answer bắt buộc $\ge 50$ ký tự).
* **Define Rubric Criteria (`Q12`)**:
  - Thiết lập danh mục tiêu chí đánh giá con, phân loại Bloom và gán điểm tối đa cho từng tiêu chí.
* **Calculate Total Points (`Q13`)**:
  - Client Invariant Validator tự động tính tổng điểm thực tế: $\sum = \sum \text{MaxScore}_j$.
* **Check Invariant Total (`Q14`)**:
  - Kiểm tra ràng buộc bất biến $\sum \equiv 10.0$đ:
    - Nếu lệch (`[Sum != 10.0]`) $\to$ hiển thị cảnh báo đỏ và khóa cứng nút Lưu (`Q15`) $\to$ điều chỉnh lại điểm (`Q16`) $\to$ tính lại (`Q13`).
    - Nếu chuẩn (`[Sum == 10.0]`) $\to$ mở khóa cho phép chuyển sang bước giả lập (`Q17`).
* **Show Error & Lock Save (`Q15`)**:
  - Hiển thị banner cảnh báo đỏ lỗi lệch tổng điểm và khóa cứng nút Lưu câu hỏi.
* **Adjust Points to 10.0 (`Q16`)**:
  - Điều chỉnh lại điểm từng tiêu chí cho đến khi tổng bằng đúng 10.0đ.

#### 🔹 Giai đoạn 4: Giả Lập Chấm Thử Bằng AI (AI Simulator)
* **Choose Simulator Action (`Q17`)**:
  - Quyết định chạy thử nghiệm hay lưu ngay:
    - Nhánh `[Run Simulator]` $\to$ nhập câu trả lời mẫu (`Q18`) $\to$ Gemini AI chấm thử (`Q19`) $\to$ hiển thị kết quả AI (`Q20`) $\to$ đánh giá (`Q21`) $\to$ quyết định (`Q22`).
    - Nhánh `[Skip Simulator]` $\to$ chuyển thẳng sang lưu chính thức (`Q23`).
* **Enter Test Answer (`Q18`)**:
  - Nhập câu trả lời giả lập (mức độ tốt, trung bình hoặc yếu) để thử nghiệm.
* **Test Grading with AI (`Q19`)**:
  - Gọi Gemini AI chấm thử nghiệm độc lập theo Barem Rubric vừa thiết lập.
* **Show AI Test Result (`Q20`)**:
  - Giao diện hiển thị chi tiết điểm số từng tiêu chí và nhận xét do AI sinh ra.
* **Review AI Feedback (`Q21`)**:
  - Giảng viên đánh giá chất lượng phản hồi và độ nhạy phân hóa của Barem điểm.
* **Decide Weight Calibration (`Q22`)**:
  - Đánh giá độ phù hợp của Barem:
    - Nếu barem chưa chuẩn (`[Refine Weights]`) $\to$ quay lại khối `Q12` để điều chỉnh lại trọng số tiêu chí.
    - Nếu barem đạt chuẩn (`[Keep Weights]`) $\to$ chuyển sang lưu chính thức (`Q23`).

#### 🔹 Giai đoạn 5: Kiểm Định Backend & Ghi CSDL An Toàn
* **Save Question Officially (`Q23`)**:
  - Bấm nút Lưu chính thức câu hỏi, gửi gói tin payload lên Backend (`source = 'manual'`).
* **Validate Total Points (`Q24`)**:
  - Tầng FluentValidation Backend thẩm định độc lập lần 2 cấu trúc payload, Model Answer $\ge 50$ ký tự và tổng điểm Barem.
* **Check Backend Validation (`Q25`)**:
  - Đánh giá tính hợp lệ của dữ liệu backend:
    - Nếu sai lệch (`[Sum != 10.0]`) $\to$ từ chối bằng `HTTP 422 Unprocessable Entity` (`Q26`) $\to$ hiển thị lỗi (`Q27`) $\to$ yêu cầu sửa lại điểm (`Q16`).
    - Nếu hợp lệ (`[Sum == 10.0]`) $\to$ mở ACID Transaction lưu CSDL (`Q28`).
* **Reject Invalid Points (422) (`Q26`)**:
  - Server từ chối ngay lập tức với mã lỗi `HTTP 422 Unprocessable Entity`.
* **Show Point Error Alert (`Q27`)**:
  - Giao diện hiển thị thông báo lỗi vi phạm Barem và điều hướng sửa điểm.
* **Save to Database (`Q28`)**:
  - Mở ACID Transaction ghi đồng thời Question, RubricCriteria và nhật ký phiên bản `system_audit_logs`.
* **Commit Transaction (`Q29`)**:
  - Hoàn tất transaction ghi nhận dữ liệu câu hỏi và barem rubric mới nhất vào PostgreSQL 16+.
* **Show Success Message (`Q30`)**:
  - Hiển thị thông báo lưu thành công trên giao diện.
* **View Question Catalog (`Q31`)**:
  - Quay lại danh mục ngân hàng câu hỏi đã được cập nhật.
* **Complete Authoring (`Q32`)**:
  - Hoàn tất toàn bộ quy trình quản lý câu hỏi và barem rubric.

---

## PHẦN 4. LUỒNG MF-04: THI THẬT PHÒNG LAB & CỔNG HẬU KIỂM GIẢNG VIÊN
*(MF-04: LAB EXAM & LECTURER AUDIT PORTAL PROCESS)*

* **Tài nguyên trực quan:** [Tệp sơ đồ Draw.io](./diagrams/MF04_Lab_Exam_Audit.drawio)

#### 4.1. Mục Tiêu Nghiệp Vụ
Tổ chức kỳ thi vấn đáp kết thúc môn chính thức tại phòng máy lab có kiểm soát an ninh nghiêm ngặt kiểu IELTS. Quy trình xuất phát từ **Pha 0: Trưởng Bộ Môn (`department_head`) khởi tạo kỳ thi (`OfficialExamSession`), gán môn thi vào kỳ thi, cấu hình ca thi (`RealExamSessionShift`: phòng lab ghi ngay trên ca, gán người coi thi là GV hoặc giám thị, danh sách thí sinh lấy từ lớp đã có), cấu hình Follow-up môn thi trong kỳ thi (`has_follow_up`, `max_follow_up_questions` 1–5 câu, mặc định 2 câu) và `ExamInputMode` (`VoiceOnly`, `VoiceWithTranscriptEdit`)**. Trong phòng thi lab áp dụng các chốt chặn: **Fullscreen Kiosk Lockdown chống gian lận**, **kiểm tra micro phần cứng 30s $\ge 60$dB**, **cấu hình phương thức làm bài theo môn học `ExamInputMode` (`VoiceOnly` khóa cứng 100% bàn phím, `VoiceWithTranscriptEdit` mở đệm sửa transcript)**, **hỏi câu chuyên sâu theo ngữ cảnh khi môn thi bật cờ (1–5 câu do Trưởng BM cấu hình, mặc định 2 câu)**, **niêm phong âm thanh bằng mã băm mật mã SHA-256 stream trực tiếp lên Cloudflare R2 định danh `STT_MSSV.webm`**.

Đặc biệt, quy trình chấm điểm, hậu kiểm và công bố điểm được thiết kế chuẩn mực theo đúng quy chế Khảo thí Đại học FPT:
1. **Thi xong Kiosk KHÔNG CÓ ĐIỂM LIỀN (No Immediate Score):** Sau khi kết thúc thời gian thi hoặc bấm nộp bài, Kiosk TUYỆT ĐỐI KHÔNG hiện điểm và không cho khiếu nại tại phòng thi. Máy trạm Kiosk khóa màn hình và hiển thị thông báo: *"Bài thi đã được ghi nhận và niêm phong an toàn. Kết quả sẽ do Giảng viên thẩm định và công bố trên hệ thống."* Thí sinh ký biên bản nộp bài giấy và ra về (về nhà).
2. **AI chấm điểm ngầm & Sinh viên phải đợi Giảng viên chấm hết bài nghi ngờ hoặc fail:** Đầu vào chấm điểm của AI chỉ là bản *transcript* nhận từ hệ thống (sau bóc băng Whisper STT hoặc sau khi chỉnh sửa qua màn hình đệm nếu môn cho phép). AI (Gemini 1.5 Pro) thực hiện chấm điểm ngầm toàn bộ bài thi theo Barem Rubric 10.0, trích xuất chuỗi suy luận CoT (`ai_chain_of_thought`) và tính toán `confidence_score` kèm cờ `is_suspicious`. Hệ thống tự động phân loại kết quả thành 2 nhóm:
   - *Nhóm 1 (Cần xem lại / Fail / Đáng nghi ngờ):* Các bài có `is_suspicious == true`, `confidence_score < 0.70` (audio ồn, nói ngập ngừng, CoT mâu thuẫn, điểm ranh giới) hoặc bài thi bị fail/điểm liệt. **Sinh viên bắt buộc phải đợi Giảng viên rà soát và chấm lại toàn bộ các bài trong nhóm này**. Giảng viên mở Evidence Panel (AudioURL R2, Transcript Whisper gốc, AI CoT), nghe lại trên Waveform Player (tua theo timestamp citations, 1.0x–1.5x) để chấm và điều chỉnh lại điểm kèm lý do giải trình bắt buộc (`override_reason`).
   - *Nhóm 2 (Độ tin cậy cao):* Các bài AI chấm chuẩn xác (`confidence_score >= 0.70` và `is_suspicious == false`), giảng viên rà soát nhanh đối chiếu Evidence Panel.
3. **Sau khi Giảng viên xử lý có điểm đầy đủ mới gửi điểm về cho sinh viên (Publish Grades):** Chỉ sau khi Giảng viên đã xử lý dứt điểm tất cả bài thi nghi ngờ hoặc fail và đảm bảo **100% sinh viên trong ca thi đã có điểm hoàn chỉnh**, Giảng viên mới bấm nút **"Công Bố Điểm" (`Publish Grades`)** (`POST /api/v1/official-exams/shifts/{shiftId}/publish-grades`) một lần duy nhất. Lúc này hệ thống mới giải phóng điểm gửi về cho sinh viên, cập nhật trạng thái ca thi và vé thi thành `PUBLISHED` và kích hoạt One-Way Lock (`LOCKED`, `is_locked = true`, từ chối mọi lệnh sửa trái phép bằng `HTTP 403 Forbidden`). Đồng thời xuất file Bảng điểm Khảo thí FPT cả định dạng Excel (.xlsx) và PDF có chữ ký số cho Phòng Khảo thí.
4. **Sinh viên ở nhà nhận điểm & Phúc khảo nội bộ:** Sinh viên ở nhà đăng nhập Student Portal trên hệ thống của mình để xem bảng điểm chính thức do Giảng viên công bố (tổng điểm, điểm chi tiết rubric, nhận xét). Nếu đồng ý, sinh viên xác nhận nhận điểm (`ACKNOWLEDGED` qua `POST /api/v1/student/official-exams/{ticketId}/acknowledge-grade`). Nếu có nhu cầu phúc khảo, sinh viên làm đơn trực tiếp ở phần Phúc khảo trong hệ thống của mình (`POST /api/v1/appeals`). Hệ thống tạo entity `AppealRequest` chuyển đến Trưởng Bộ Môn (`department_head`). Trưởng Bộ Môn tiếp nhận và **giao cho một Giảng viên chấm lại** (`PUT /api/v1/appeals/{id}/assign-lecturer`). Cơ chế One-Way Lock mở đường hợp lệ cho Giảng viên được phân công chấm lại cập nhật điểm số mới sau phúc khảo.

### 4.2. Năm Phân Làn Trách Nhiệm (5 Swimlanes)
1. **Lane 0: Department Head (Trưởng Bộ Môn)**:
   - Khởi tạo Kỳ thi (`OfficialExamSession`) $\to$ Gán danh sách môn thi vào kỳ thi $\to$ Cấu hình môn thi: Thiết lập ca thi (`RealExamSessionShift`: ghi phòng lab ngay trên ca, gán người coi thi là GV hoặc giám thị, thí sinh lấy từ lớp đã có), cấu hình Follow-up (`has_follow_up`, `max_follow_up_questions` 1–5 câu, mặc định 2 câu) và `ExamInputMode` (`VoiceOnly`, `VoiceWithTranscriptEdit`) kèm `TranscriptBufferSeconds` $\to$ Tiếp nhận đơn phúc khảo nội bộ `AppealRequest` của sinh viên và giao cho một Giảng viên chấm lại.
2. **Lane 1: Student / Examinee (Sinh viên / Thí sinh)**:
   - Ngồi đúng máy thi theo STT $\to$ Kiểm tra micro 30s $\ge 60$dB $\to$ Bấm bắt đầu thi $\to$ Làm bài theo `ExamInputMode` của môn (nói qua mic, nếu `VoiceOnly` thì bị khóa phím; nếu `VoiceWithTranscriptEdit` thì được sửa transcript qua màn hình đệm) $\to$ Trả lời câu hỏi chuyên sâu do AI gợi mở (nếu môn thi có cấu hình hỏi chuyên sâu, 1–5 câu do Trưởng BM cấu hình, mặc định 2 câu) $\to$ Xác nhận nộp toàn bài $\to$ Kiosk khóa màn hình và nhận thông báo bài đã lưu an toàn, tuyệt đối không hiện điểm và không cho khiếu nại tại chỗ $\to$ Ký biên bản giấy và trật tự rời phòng thi $\to$ Đăng nhập Student Portal xem điểm chính thức do Giảng viên công bố: Xác nhận nhận điểm (`ACKNOWLEDGED`) HOẶC gửi đơn phúc khảo nội bộ `AppealRequest` trực tiếp trên hệ thống.
3. **Lane 2: Lab Workstation Kiosk (Máy trạm Phòng Lab Kiosk)**:
   - Kích hoạt chế độ Fullscreen Kiosk Lockdown (chặn F12, DevTools, Alt+Tab, Ctrl+C/V, bắt `window.onblur`) $\to$ Đo tín hiệu micro phần cứng 30s ($\ge 60$dB) $\to$ Kiểm tra `ExamInputMode` (khóa cứng bàn phím nếu `VoiceOnly`) $\to$ Phát âm thanh đề thi TTS qua tai nghe $\to$ Thu âm WebM/Opus $\to$ Điều phối hỏi câu phụ chuyên sâu nếu đề bật $\to$ Đóng gói âm thanh chuẩn `STT_MSSV.webm` $\to$ Băm cryptographic SHA-256 niêm phong tại máy trạm $\to$ Stream trực tiếp lên Cloudflare R2 qua Presigned URL $\to$ Gửi bài nộp lên Backend $\to$ Khóa màn hình máy trạm và hiển thị thông báo bài nộp an toàn, kết quả sẽ do Giảng viên thẩm định và công bố trên hệ thống (tuyệt đối không push điểm về Kiosk).
4. **Lane 3: Backend & AI Grading Service (Hạ tầng Backend & Dịch Vụ Chấm Điểm AI)**:
   - Khởi tạo ca thi trong CSDL (`SCHEDULED`) $\to$ Bật Server Master Timer kiểm soát thời gian tuyệt đối $\to$ Nhận URL audio và mã băm SHA-256, lưu bài thi (`SUBMITTED`) dưới 100ms $\to$ Điều phối AI Gemini 1.5 Pro chấm điểm ngầm chỉ dựa trên bản transcript theo Rubric 10.0 $\to$ Tính toán `confidence_score` & `is_suspicious`, sinh chuỗi CoT `ai_chain_of_thought`, cập nhật trạng thái `AI_GRADED` $\to$ Cung cấp Evidence Panel và phân loại bài nghi ngờ cho Giảng viên tại Lecturer Audit Portal $\to$ Tiếp nhận lệnh Công bố điểm từ Giảng viên (`Publish Grades`) khi 100% sinh viên có điểm $\to$ Cập nhật trạng thái `PUBLISHED` và kích hoạt `OneWayLockInterceptor` khóa bất biến 1 chiều (`LOCKED`, từ chối mọi lệnh sửa trái phép bằng `HTTP 403 Forbidden`) $\to$ Xuất file Bảng điểm Khảo thí FPT (Excel và PDF có chữ ký số) và đẩy điểm lên Student Portal cho sinh viên tra cứu.
5. **Lane 4: Lecturer / Proctor & Auditor (Giám thị Ca thi & Giảng viên Thẩm định)**:
   - **Giám thị 1 (Trưởng ca thi):** Mở ca thi trên Proctor Console $\to$ Phát tín hiệu đồng bộ bắt đầu ca thi toàn phòng $\to$ Đóng ca thi khi toàn bộ thí sinh đã hoàn tất.
   - **Giám thị 2 (Cán bộ hỗ trợ):** Điểm danh đối chiếu thẻ SV/CCCD $\to$ Phân bổ số máy trạm cố định (Booth 1-40) $\to$ Đổi máy dự phòng nếu máy trạm bị sự cố micro.
   - **Giảng viên Thẩm định:** Mở Cổng Hậu kiểm (Lecturer Audit Portal) $\to$ Hệ thống tự động phân loại 2 nhóm: Ưu tiên xử lý Nhóm 1 (Cần xem lại / Fail / Đáng nghi ngờ), đối chiếu Evidence Panel (AudioURL, Transcript Whisper gốc, AI CoT), nghe lại file `STT_MSSV.webm` trên Waveform Player (tua theo timestamp citations, 1.0x–1.5x) $\to$ Phê duyệt điểm AI hoặc sửa điểm từng tiêu chí kèm **bắt buộc nhập lý do giải trình** (`override_reason`) $\to$ Rà soát nhanh Nhóm 2 (Độ tin cậy cao) $\to$ Bấm nút **"Công Bố Điểm" (`Publish Grades`)** một lần duy nhất khi 100% sinh viên có điểm hoàn chỉnh, kích hoạt One-Way Lock và xuất file bảng điểm nộp Khảo thí FPT. Khi được Trưởng Bộ Môn giao đơn phúc khảo của sinh viên, Giảng viên được phân công tiến hành rà soát Evidence Panel, chấm lại và cập nhật điểm phúc khảo.

### 4.3. Diễn Giải Quy Trình Theo Từng Bước (Tên : Mục Đích)

#### 🔹 Pha 0: Quản Trị Kỳ Thi & Cấu Hình Môn Thi (Trưởng Bộ Môn)
* **Create Exam Season (`act_dept_create_season`)**:
  - Trưởng Bộ Môn khởi tạo Kỳ thi chính thức (`OfficialExamSession` / Exam Season).
* **Assign Courses to Season (`act_dept_assign_courses`)**:
  - Đưa danh sách các môn học thi vấn đáp vào kỳ thi đã tạo.
* **Configure Course & Shifts (`act_dept_config_shifts`)**:
  - Khi ấn vào từng môn học trong kỳ thi, Trưởng Bộ Môn thiết lập:
    1. Danh sách ca thi (`RealExamSessionShift`: ghi phòng lab ngay trên ca, kíp thi, phân công người coi thi là giảng viên hoặc giám thị, danh sách thí sinh lấy từ lớp đã có).
    2. Cấu hình Follow-up: cờ `has_follow_up` (bật/tắt) và số câu hỏi phụ tối đa `max_follow_up_questions` (1–5 câu, mặc định 2 câu, áp dụng đồng bộ cho các ca thi của môn trong kỳ thi).
    3. Cấu hình `ExamInputMode` trong scope gồm 2 phương thức: `VoiceOnly` và `VoiceWithTranscriptEdit` (loại bỏ `VoiceAndTextInput`) kèm `TranscriptBufferSeconds` (10–300s, mặc định 60s).

#### 🔹 Pha 1: Kiểm Soát An Ninh Phòng Lab, Điểm Danh & Kiểm Tra Micro 30s
* **Open Proctor Console (`act_open_proctor_console`)**:
  - Giám thị 1 đăng nhập Cổng Giám thị, chọn ca thi (Shift), phòng máy Lab và nạp cấu trúc đề thi đã duyệt.
* **Initialize Exam Shift (`act_init_session_db`)**:
  - Hệ thống khởi tạo ca thi trong CSDL ở trạng thái `SCHEDULED` và cấp mã kết nối Kiosk cho từng máy thi.
* **Check Attendance & Assign Seats (`act_verify_roster`)**:
  - Giám thị 2 kiểm diện đối chiếu thẻ sinh viên/CCCD và gán cố định số thứ tự bằng số máy trạm Kiosk (SeatNumber 1-40).
* **Verify Student ID & Check-in (`act_verify_id`)**:
  - Thí sinh xuất trình thẻ, xác nhận thông tin điểm danh. Hệ thống tự động trói buộc (binding) vé thi của thí sinh với địa chỉ IP của máy trạm thông qua trường thống nhất `ip_address`.
* **Take Assigned Seat (`act_take_assigned_seat`)**:
  - Thí sinh vào phòng lab cách âm, ngồi đúng máy trạm theo STT (trùng khớp `ip_address` đã gán) và đeo tai nghe chụp đầu có micro.
* **Login Student ID (`act_login_kiosk`)**:
  - Thí sinh nhập mã số sinh viên (MSSV) trên màn hình. Kiosk xác thực MSSV và kiểm tra chéo `ip_address` hiện tại xem có khớp với vé thi không để chống gian lận ngồi sai vị trí.
* **Fullscreen Kiosk Lockdown (`act_activate_kiosk`)**:
  - Máy trạm kích hoạt chế độ Fullscreen Kiosk Lockdown: vô hiệu hóa toàn bộ phím tắt hệ thống (F11, F12, DevTools, Alt+Tab, Windows, Ctrl+C/V, chuột phải). Kích hoạt bắt sự kiện mất tiêu điểm `window.onblur` — **nếu phát hiện mất focus quá 3 lần, hệ thống lập tức khóa bài và lập biên bản đình chỉ thi.**
* **Mic-Check 30s (`act_perform_mic_check`)**:
  - Thí sinh đọc to đoạn văn mẫu trong 30 giây để kiểm tra chất lượng micro và tai nghe.
* **Measure Audio Level (`act_measure_input_level`)**:
  - Máy trạm đo cường độ âm thanh đầu vào xem có đạt ngưỡng tiêu chuẩn $\ge 60$dB hay không.
* **Verify Hardware Status (`dec_verify_hardware`)**:
  - Đánh giá tín hiệu phần cứng:
    - Nếu micro bị lỗi hoặc quá nhỏ (`[Mic < 60dB: Hardware Fault]`) $\to$ Giám thị 2 can thiệp đổi sang máy dự phòng (`act_reassign_station`) $\to$ kiểm tra lại (`[Retest on Backup PC]`).
    - Nếu micro đạt chuẩn (`[Mic >= 60dB: Audio OK]`) $\to$ chuyển sang trạng thái chờ hiệu lệnh thi (`act_wait_exam_start`).
* **Reassign to Backup Station (`act_reassign_station`)**:
  - Giám thị 2 thao tác trên Console chuyển thí sinh sang máy trạm dự phòng đã cấu hình sẵn trong phòng lab.
* **Wait Synchronized Start (`act_wait_exam_start`)**:
  - Màn hình máy trạm khóa sẵn sàng, thí sinh giữ trật tự chờ hiệu lệnh phát đề đồng bộ.

#### 🔹 Pha 2: Bắt Đầu Đồng Bộ, Thi Vấn Đáp Chính Thức, Ghi Âm & Niêm Phong SHA-256
* **Broadcast Start Exam (`act_broadcast_start`)**:
  - Giám thị 1 bấm nút "Bắt Đầu Ca Thi Cả Phòng Lab" trên Console $\to$ SignalR phát lệnh đồng bộ mở đề cho toàn bộ phòng máy.
* **Start Server Master Timer (`act_start_server_timer`)**:
  - Backend bật đồng hồ đếm ngược máy chủ tuyệt đối, ngăn chặn triệt để gian lận bằng ngắt kết nối mạng.
* **Start Kiosk Countdown Timer (`act_start_kiosk_timer`)**:
  - Giao diện máy trạm hiển thị đồng hồ đếm ngược minh bạch từng câu và tổng thời gian thi.
* **Auto-Submit on Timeout (`act_auto_submit_exam`)**:
  - Nếu đồng hồ đếm ngược chạm `00:00`, máy trạm tự động khóa micro và cưỡng chế nộp bài để bảo đảm công bằng.
* **Check Exam Input Mode (`dec_check_input_mode`)**:
  - Kiểm tra cấu hình `ExamInputMode` của môn thi trong kỳ thi (trong scope hỗ trợ 2 phương thức):
    - Nếu `VoiceOnly`: Khóa cứng 100% bàn phím máy trạm, bắt buộc chỉ trả lời bằng micro.
    - Nếu `VoiceWithTranscriptEdit`: Mở màn hình đệm cho phép rà soát và chỉnh sửa transcript (sửa lỗi Code-Switching) sau khi phát biểu theo thời gian cấu hình của môn (`transcript_buffer_seconds`, từ 10–300s, mặc định 60s).
* **TTS Play Question Audio (`act_play_question_tts`)**:
  - Máy trạm hiển thị câu hỏi và tự động đọc to nội dung đề bài qua tai nghe bằng giọng chuẩn.
* **Speak Viva Answer (`act_speak_viva_answer`)**:
  - Thí sinh phát biểu câu trả lời của mình trực tiếp vào micro tai nghe.
* **Record Answer Audio (`act_record_media_stream`)**:
  - Trình duyệt ghi lại toàn bộ luồng âm thanh câu trả lời bằng chuẩn nén WebM/Opus chất lượng cao.
* **Check Follow-up Policy (`dec_check_subject_followup`)**:
  - Hệ thống kiểm tra cấu hình đề thi:
    - Nếu đề thi có cấu hình hỏi phụ (`[AI Follow-up Enabled]`) $\to$ AI phân tích nhanh ngữ cảnh (`act_eval_answer_context`).
    - Nếu đề thi chuẩn không hỏi thêm (`[No AI Follow-up]`) $\to$ chuyển thẳng sang đóng gói âm thanh (`act_package_audio`).
* **AI Quick Context Evaluation (`act_eval_answer_context`)**:
  - AI lắng nghe và đánh giá nhanh mức độ rõ ràng của câu trả lời theo ngữ cảnh đề bài.
* **Verify Follow-up Need (`dec_need_followup`)**:
  - Đánh giá chất lượng:
    - Nếu câu trả lời chưa rõ ràng hoặc cần đào sâu thêm (`[Needs Follow-up]`) $\to$ AI sinh câu hỏi phụ phát qua tai nghe (`act_play_followup_tts`).
    - Nếu câu trả lời đã đầy đủ ý (`[Good Answer - No Follow-up]`) $\to$ không hỏi thêm, chuyển sang đóng gói bài (`act_package_audio`).
* **TTS Play Follow-up Audio (`act_play_followup_tts`)**:
  - Máy trạm phát âm thanh câu hỏi phụ của AI qua tai nghe để thí sinh trả lời tiếp.
* **Speak Follow-up Answer (`act_speak_followup`)**:
  - Thí sinh phát biểu câu trả lời cho câu hỏi phụ vào micro.
* **Check Follow-up Limit (`dec_check_followup_count`)**:
  - Hệ thống chốt giới hạn câu hỏi phụ theo cấu hình của Trưởng Bộ Môn (từ 1–5 câu, mặc định 2 câu) $\to$ chuyển sang đóng gói âm thanh.
* **Package Audio (`act_package_audio`)**:
  - Đóng gói toàn bộ âm thanh bài thi theo định danh bắt buộc: `STT_MSSV.webm`.
* **Compute SHA-256 Seal (`act_hash_sha256`)**:
  - Máy trạm tính toán mã băm cryptographic SHA-256 ngay tại Client tạo seal niêm phong pháp lý bất biến.
* **Stream Audio to Cloudflare R2 (`act_stream_r2`)**:
  - Tải file ghi âm trực tiếp lên Cloudflare R2 qua Presigned URL thời hạn 15 phút (Zero-Egress).
* **Check Exam Progress (`dec_check_questions`)**:
  - Kiểm tra tiến độ làm bài:
    - Nếu còn câu hỏi (`[More Questions]`) $\to$ chuyển tiếp sang câu hỏi sau (`act_play_question_tts`).
    - Nếu đã hoàn thành toàn bộ câu hỏi (`[All Questions Done]`) $\to$ mở màn hình xác nhận nộp bài (`act_confirm_submission`).
* **Confirm Submit Exam (`act_confirm_submission`)**:
  - Thí sinh kiểm tra lần cuối và bấm nút xác nhận nộp bài thi chính thức.
* **Dispatch Submission (`act_send_submission`)**:
  - Máy trạm gửi gói dữ liệu bài thi (URL file audio trên R2 + mã băm SHA-256) lên Backend.
* **Save Submission in DB (`act_save_submitted_db`) — Persist-First Ingestion (< 100ms)**:
  - Áp dụng nguyên tắc **Persist-First**: Backend validate cú pháp, ghi ngay bản ghi nộp bài vào `exam_question_submissions`, cập nhật `student_exam_tickets` với trạng thái **`SUBMITTED` trong $< 100$ms**, đồng thời lưu mã băm SHA-256 niêm phong audio Cloudflare R2 (`STT_MSSV.webm`).

#### 🔹 Pha 3: Kiosk Khóa An Toàn, Hàng Đợi Chịu Tải (BoundedChannel & DLQ) & AI Chấm Ngầm
* **Kiosk Safe Lock Notice (`act_kiosk_safe_notice`)**:
  - Ngay sau khi nộp bài thành công dưới 100ms, màn hình máy trạm Kiosk lập tức khóa toàn bộ và hiển thị thông báo an tâm: *"Bài thi đã được ghi nhận và lưu trữ an toàn. Kết quả chấm sẽ được Giảng viên thẩm định và công bố trên hệ thống."* Tuyệt đối không hiển thị điểm và không tiếp nhận khiếu nại tại phòng thi.
* **Sign Roster & Leave Room (`act_vacate_lab_room`)**:
  - Thí sinh ký tên vào biên bản nộp bài giấy tại bàn giám thị, tháo tai nghe và trật tự rời khỏi phòng thi.
* **Enqueue to Bounded Channel (`act_enqueue_bounded_channel`)**:
  - Hệ thống dispatch non-blocking tác vụ chấm điểm vào hàng đợi RAM **BoundedChannel 1,000 slots (`BoundedChannelFullMode.Wait`)**, tách biệt luồng tiếp nhận bài thi (Ingestion) và luồng xử lý AI (Worker), đảm bảo phòng thi 40 máy nộp đồng thời không bị quá tải bộ nhớ.
* **Background AI Grading with Polly & DLQ (`act_background_ai_grading`)**:
  - Background Worker (`GradingQueueWorker`) rút tác vụ tuần tự từ hàng đợi để gọi Gemini 1.5 Pro chấm điểm ngầm toàn bộ bài thi chỉ dựa trên bản *transcript* nhận từ hệ thống theo Rubric 10.0, trích xuất điểm từng tiêu chí, nhận xét và tính toán hệ số tin cậy (`confidence_score`) kèm cờ nghi ngờ (`is_suspicious`).
  - **Cơ chế chịu lỗi & DLQ (Zero Data Loss):** Kích hoạt chính sách Polly Retry 3 lần (2s $\to$ 4s $\to$ 8s) khi gặp sự cố mạng hoặc AI quá tải (HTTP 429). Nếu thất bại sau 3 lần, bài thi tự động chuyển vào Dead-Letter Queue (`dead_letter_queues`) với trạng thái `PENDING_RETRY`. Tiến trình nền `DlqReplayWorker` định kỳ 5 phút quét và tự động nạp lại vào hàng đợi để chấm bù, cam kết không làm gián đoạn phòng thi và không mất dữ liệu.
* **Update Graded State (`act_update_ai_graded`)**:
  - Backend lưu kết quả chấm vào CSDL và cập nhật trạng thái vé thi thành `AI_GRADED`, sẵn sàng cho Giảng viên thẩm định tại Cổng Hậu kiểm.

#### 🔹 Pha 4: Cổng Hậu Kiểm Giảng Viên, Phân Loại Bài Nghi Ngờ & Waveform Player Thẩm Định
* **Close Exam Shift (`act_close_session_console`)**:
  - Giám thị 1 xác nhận toàn bộ thí sinh đã hoàn tất và ra về hết $\to$ bấm nút "Đóng ca thi" trên Proctor Console.
* **Open Lecturer Audit Portal (`act_open_audit_portal`)**:
  - Giảng viên đăng nhập Cổng Hậu Kiểm, chọn ca thi phòng Lab cần thẩm định (hiển thị danh sách thí sinh theo STT máy 1-40).
* **AI Doubt Guard Classification (`act_filter_audit_queue`)**:
  - Hệ thống tự động phân loại danh sách thí sinh thành 2 nhóm rõ rệt:
    - **Nhóm 1 (Đáng nghi ngờ / Cần can thiệp - Priority Queue):** Các bài có cờ `is_suspicious == true` hoặc `confidence_score < 0.70` (audio ồn, nói ngập ngừng, CoT mâu thuẫn, điểm ranh giới). Giảng viên bắt buộc xử lý nhóm này trước.
    - **Nhóm 2 (Độ tin cậy cao):** Các bài AI chấm chuẩn xác (`confidence_score >= 0.70` và `is_suspicious == false`), giảng viên rà soát nhanh tổng quan.
* **Waveform Audio Player & Evidence Panel (`act_play_waveform`)**:
  - Với các bài thuộc Nhóm 1, Giảng viên đối soát trên **Evidence Panel** gồm: AudioURL Cloudflare R2, Transcript Whisper gốc, và chuỗi suy luận AI Chain-of-Thought (`ai_chain_of_thought`). Giảng viên nghe lại file `STT_MSSV.webm` trên Waveform Player (tùy chỉnh tốc độ 1.0x, 1.25x, 1.5x) và bấm vào từng timestamp citation để nghe đoạn phát ngôn nghi vấn.
* **Audit Decision & Override (`dec_audit_action`)**:
  - Quyết định thẩm định của Giảng viên:
    - Nếu đồng thuận với điểm AI $\to$ giữ nguyên điểm đề xuất.
    - Nếu AI chấm chưa sát do từ lóng, nói lắp, hoặc âm thanh rè $\to$ trực tiếp sửa điểm từng tiêu chí Rubric và **bắt buộc nhập văn bản giải trình lý do điều chỉnh** (`override_reason`).
* **Save Lecturer Audit (`act_save_lecturer_audit`)**:
  - Hệ thống ghi nhận biên bản thẩm định vào bảng `lecturer_audits`, cập nhật trạng thái vé thi thành `AUDITED`.

#### 🔹 Pha 5: Giảng Viên Công Bố Điểm (Publish), Khóa 1 Chiều & Phúc Khảo Nội Bộ
* **Publish Grades by Lecturer (`act_publish_grades`)**:
  - Sau khi hoàn tất kiểm tra và điều chỉnh điểm, Giảng viên bấm nút **"Công Bố Điểm" (`Publish Grades`)** (`POST /api/v1/official-exams/shifts/{shiftId}/publish-grades`).
  - **Ràng buộc Atomic Validation:** Hệ thống kiểm tra 100% sinh viên trong ca thi đã có điểm hoàn chỉnh (`FinalScore != null` và trạng thái thuộc `AI_GRADED` hoặc `AUDITED`). Nếu còn sót bất kỳ bài thi nào chưa có điểm, hệ thống lập tức từ chối và trả về mã lỗi `HTTP 422 Unprocessable Entity` kèm danh sách sinh viên chưa có điểm.
* **Enforce One-Way Lock (`act_enforce_oneway_lock`)**:
  - Backend mở Database Transaction: cập nhật trạng thái ca thi và vé thi thành `PUBLISHED` và `LOCKED`, đồng thời gán `is_locked = true`.
  - `OneWayLockInterceptor` khóa cứng: Mọi hành vi cố tình gửi lệnh UPDATE/DELETE điểm số sau đó đều bị chặn đứng và ném ra mã lỗi `HTTP 403 Forbidden`. Ngoại lệ hợp lệ duy nhất được cho phép: Khi có đơn Phúc khảo nội bộ (`AppealRequest`) được Trưởng Bộ Môn phân công cho Giảng viên chấm lại, Giảng viên được phân công mới có quyền cập nhật điểm phúc khảo kèm lý do giải trình.
* **Export Official Exam Reports (`act_export_fpt_reports`)**:
  - Hệ thống hỗ trợ xuất báo cáo bảng điểm chính thức theo 2 định dạng chuẩn Đại học FPT:
    - File **Excel (.xlsx)**: Phục vụ lưu trữ, tổng hợp và phân tích số liệu phòng Khảo thí.
    - File **PDF**: Định dạng chuẩn in ấn và lưu trữ pháp lý phòng thi.
* **Student Views Grade on Portal (`act_view_official_grade`)**:
  - Sinh viên đăng nhập Student Portal xem bảng điểm chính thức do Giảng viên công bố (tổng điểm, điểm chi tiết từng rubric, nhận xét).
* **Student Decision on Portal & Internal Appeal (`dec_student_grade_action`)**:
  - Sinh viên lựa chọn hành động:
    - **Nếu chấp nhận điểm:** Sinh viên bấm nút "Xác Nhận Nhận Điểm" (`student_acknowledgement_status = 'ACKNOWLEDGED'`), hoàn tất kỳ thi môn học.
    - **Nếu không chấp nhận điểm (Phúc khảo nội bộ):**
      - Sinh viên bấm nút "Nộp Đơn Phúc Khảo" trực tiếp trên Student Portal (`POST /api/v1/appeals`).
      - Hệ thống khởi tạo bản ghi `AppealRequest` và tự động gửi tới **Trưởng Bộ Môn (`department_head`)**.
      - Trưởng Bộ Môn tiếp nhận đơn và phân công một Giảng viên vào chấm lại (`PUT /api/v1/appeals/{id}/assign-lecturer`).
      - Giảng viên được phân công rà soát lại Evidence Panel (Audio R2 `STT_MSSV.webm`, Transcript Whisper gốc, chuỗi suy luận AI CoT và giải trình audit trước đó) để chấm lại trên Waveform Player, điều chỉnh điểm (nếu cần) và nộp kết quả phúc khảo kèm lý do giải trình (`regrade_reason`).
      - Trưởng Bộ Môn xem xét phê duyệt (`APPROVED`) cập nhật điểm chính thức hoặc bác bỏ (`REJECTED`) giữ nguyên điểm.
* **End Viva Lifecycle (`end_viva_lifecycle`)**:
  - Toàn bộ chu trình kỳ thi vấn đáp và quy trình phúc khảo nội bộ kết thúc trọn vẹn, minh bạch và an toàn pháp lý.

---

## BẢNG ĐỐI CHIẾU 4 LUỒNG VỚI CÁC TÀI LIỆU TOÀN HỆ THỐNG

| Luồng Nghiệp Vụ | Tệp Sơ Đồ Draw.io Chuẩn Hóa | Vị Trí Trong Tài Liệu Master Architecture |
|:---:|:---|:---|
| **MF-01** | [`MF01_Interactive_Practice.drawio`](./diagrams/MF01_Interactive_Practice.drawio) | [MASTER_ARCHITECTURE.md Mục 6.4.1 / 6.5.1](./MASTER_ARCHITECTURE.md#phan-651) |
| **MF-02** | [`MF02_Timed_Mock_Exam.drawio`](./diagrams/MF02_Timed_Mock_Exam.drawio) | [MASTER_ARCHITECTURE.md Mục 6.4.2 / 6.5.2](./MASTER_ARCHITECTURE.md#phan-652) |
| **MF-03** | [`MF03_Question_Bank_Rubric.drawio`](./diagrams/MF03_Question_Bank_Rubric.drawio) | [MASTER_ARCHITECTURE.md Mục 6.4.3 / 6.5.3](./MASTER_ARCHITECTURE.md#phan-653) |
| **MF-04** | [`MF04_Lab_Exam_Audit.drawio`](./diagrams/MF04_Lab_Exam_Audit.drawio) | [MASTER_ARCHITECTURE.md Mục 6.4.4 / 6.5.4](./MASTER_ARCHITECTURE.md#phan-654) |

---

## PHẦN 5: XỬ LÝ 4 CẠNH BIÊN NGHIỆP VỤ ĐẶC THÙ (EDGE-CASES)

> **Chi tiết kiến trúc và giải pháp kỹ thuật xem tại:** MASTER_ARCHITECTURE.md (Mục 6.6)

1. **[Session Resumption] Phục hồi phiên thi Kiosk (MF-04):** Lưu trạng thái question_index và bộ đệm (heartbeat) vào Backend để khi máy sập/mất điện, mở lên thi tiếp tục không mất bài.
2. **[R2 Direct Upload] Chống nghẽn mạng (MF-04):** Áp dụng Presigned URL. Kiosk upload STT_MSSV.webm trực tiếp lên Cloudflare R2, sau đó chỉ gửi SHA-256 về Backend thay vì đẩy qua đường ống Backend.
3. **[Booth Reassignment] Đổi máy thi (MF-04):** Giám thị có quyền gỡ niêm phong ip_address máy cũ trên vé thi để gán cho máy mới nếu phần cứng mic (< 60dB) bị hỏng.
4. **[Token Expiration] Quản lý Token (MF-04/MF-02):** Chạy cơ chế **Silent Refresh Token** dưới nền bằng Axios Interceptor để giữ phiên thi không văng ra ngoài đăng nhập sau 1 tiếng.

