# 🤖 AGENTS.md — SỔ TAY KỸ SƯ TÁC CHIẾN ĐỒ ÁN SEP490 (FA26SE166)

> **Dự án:** Hệ Thống Luyện Thi & Đánh Giá Vấn Đáp Bằng LLM Cho Ngành Kỹ Thuật Phần Mềm (LLM Oral Exam System)  
> **Mã đề tài:** FA26SE166 | Học kỳ: Fall 2026 (FA26) — Đại học FPT TP.HCM (FPT SG)  
> **Kho mã nguồn:** `05_Source_Code` (Thư mục làm việc chính thức)  
> **Tech Stack Chuẩn:** .NET 8 Clean Architecture · React 19 (Vite) + React Router DOM v7 · Tailwind CSS v4 · PostgreSQL 16+ (28 bảng 3NF) · Gemini 1.5 Flash/Pro · Whisper STT · Cloudflare R2  
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

1. `student`: Sinh viên luyện tập tự do (MF-01: chọn [Per-Question] luôn có follow-up hoặc [Full-Session] không follow-up), thi thử có hạn ngạch và tự chọn có/không follow-up (MF-02), thi thật Kiosk phòng Lab (MF-04), xem kết quả thi và nộp đơn phúc khảo nội bộ (`AppealRequest`) trên Student Portal.
2. `lecturer`: Giảng viên sử dụng AI sinh câu hỏi theo barem của mình từ FLM hoặc soạn thủ công (bắt buộc gửi Trưởng Bộ Môn phê duyệt) (MF-03), mở phòng thi, hậu kiểm toàn bộ bài thi nghi ngờ/fail qua Waveform Audio Player và Evidence Panel, điều chỉnh điểm và công bố điểm (MF-04). Giảng viên KHÔNG cấu hình follow-up trong MF-01.
3. `department_head`: Trưởng Bộ Môn khởi tạo kỳ thi (`OfficialExamSession`), đưa môn thi vào kỳ thi, cấu hình ca thi (`RealExamSessionShift`), cấu hình Follow-up (`has_follow_up`, `max_follow_up_questions` 1–2 câu) và `ExamInputMode` cho môn thi trong kỳ thi (MF-04); quản lý đề cương môn học, thẩm định và ra quyết định phê duyệt (`APPROVED`), yêu cầu chỉnh sửa (`NEEDS_REVISION`) hoặc từ chối (`REJECTED`) câu hỏi do giảng viên gửi lên (MF-03); tiếp nhận và thẩm định đơn phúc khảo nội bộ (`AppealRequest`) của sinh viên (MF-04).
4. `proctor`: Giám thị phòng thi điểm danh theo số thứ tự (STT 1–40), mở ca thi, giám sát gian lận Kiosk (MF-04).
5. `admin`: Quản trị viên hệ thống quản lý danh mục, người dùng, cấu hình số câu hỏi follow-up cho hệ thống luyện tập (1–5 câu, mặc định 2 câu), cấu hình ca thi, xử lý Dead-Letter Queue (DLQ).

---

## 🔄 3. ĐẶC TẢ CHI TIẾT 4 MAIN FLOWS NGHIỆP VỤ (NGUỒN SỰ THẬT DUY NHẤT)

### 🔹 MF-01: Luyện Tập Tương Tác Tự Do (Interactive Practice)
* **Quyền hạn cấu hình:** **Giảng viên KHÔNG cấu hình follow-up** trong MF-01. **Admin là người duy nhất cấu hình hệ thống** về số lượng câu hỏi follow-up cho luyện tập:
  - Giá trị mặc định là **2 câu**.
  - Admin có thể cấu hình linh hoạt từ **1 tới 5 câu** (`1 <= follow_up_questions <= 5`).
* **Quy tắc kích hoạt theo chế độ làm bài:**
  - Nếu sinh viên chọn **Luyện từng câu (`[Per-Question]`)**: Kích hoạt câu hỏi follow-up chuyên sâu đào sâu (A2) khi điểm số của câu trả lời rơi vào khoảng ranh giới **$4.0 \le \text{Score} \le 8.0$** (số lượng câu hỏi phụ tối đa từ 1 đến 5 câu do Admin cấu hình, mặc định 2 câu). Nếu $\text{Score} < 4.0$ hoặc $\text{Score} > 8.0$, hệ thống bỏ qua câu hỏi phụ và mở ngay bảng điểm Scorecard.
  - Nếu sinh viên chọn **Luyện trọn gói (`[Full-Session]`)**: **KHÔNG có câu hỏi follow-up**, sinh viên tập trung trả lời liền mạch toàn bộ câu hỏi trong bộ đề rồi nhận bảng điểm Scorecard tổng kết.
* **Màn hình đệm hiệu đính (Buffer Screen):** Sau khi nói qua Mic, hệ thống mở màn hình đệm cho phép sinh viên xem lại STT bóc băng, nghe lại TTS câu hỏi và sửa các thuật ngữ kỹ thuật bị phiên âm sai (Code-Switching SE Glossary) trước khi nộp.
  - **Thời gian đệm:** Cấu hình động theo môn (`transcript_buffer_seconds`, từ 10-300s, giá trị **mặc định là 60 giây**).

### 🔹 MF-02: Thi Thử Tính Giờ Có Hạn Ngạch (Timed Mock Exam)
* **Nguồn đề thi thử:** Rút ngẫu nhiên từ kho câu hỏi luyện tập chung (`practice_questions`), cô lập an toàn kho câu hỏi thi thật (`exam_questions`) dành riêng cho MF-04.
* **Hạn ngạch thi thử (Daily Quota Guard):** Sinh viên chỉ được thi tối đa **$K = 3$ lượt/ngày/môn**. Lượt thứ 4 trở đi hệ thống lập tức chặn và trả mã lỗi `HTTP 429 Too Many Requests`.
* **Cổng kiểm soát phát biểu (Voice-First Gate):** Hiển thị đề thi và khóa cứng ô gõ phím, bắt buộc phải trả lời qua micro.
* **Tùy chọn Follow-up chủ động của sinh viên:** Sinh viên truy cập Student Portal, chọn môn học để thi thử. Trước khi bấm bắt đầu làm bài, sinh viên được **chủ động tự chọn chế độ**:
  - `Có Follow-up` (AI hỏi chuyên sâu ngữ cảnh đào sâu `[Needs Follow-up]`).
  - `Không Follow-up` (Làm đề thi thẳng tính giờ bình thường).
  Nếu chọn có follow-up, AI sẽ phát vấn thêm câu hỏi phụ qua TTS và micro trong quá trình làm bài.
* **Instant Feedback & Lịch sử thi:** Sau khi hoàn thành bài thi thử, AI chấm điểm tức thì và trả về **Scorecard chi tiết kèm nhận xét sư phạm từng tiêu chí** cho sinh viên xem ngay trên màn hình kết quả và lưu vào lịch sử thi, tập trung vào Scorecard Rubric chi tiết theo từng CLO.

### 🔹 MF-03: Giảng Viên Sử Dụng AI Sinh Câu Hỏi Theo Barem Của Mình & Rubric Studio 10.0 (Question Bank & Rubric Studio)
* **Quyền hạn Giảng viên (`lecturer`):** **Giảng viên sử dụng AI sinh câu hỏi theo barem của mình** (kể cả từ API FLM của FPT hoặc đề cương môn học, hoặc soạn thủ công), chủ động tự thiết kế theo Barem Rubric riêng, tự do chỉnh sửa nội dung đề bài, tiêu chí barem và câu trả lời mẫu (Model Answer $\ge 50$ ký tự, Barem Rubric $\sum \equiv 10.0$đ).
* **Quy trình Gửi duyệt Bộ Môn:** Mọi câu hỏi do Giảng viên tạo ra — dù là **tạo thủ công** hay **sử dụng Agents / AI sinh từ FLM theo barem riêng 10.0đ** — đều ở trạng thái dự thảo (`DRAFT`) và **BẮT BUỘC PHẢI GỬI QUA CHO TRƯỞNG BỘ MÔN DUYỆT** (`POST /api/v1/questions/batch-submit-review`). Bộ câu hỏi chuyển sang trạng thái `SUBMITTED_FOR_REVIEW`.
* **Trưởng Bộ Môn (`department_head`) phê duyệt:** Trưởng Bộ Môn là chốt chặn thẩm định chính thức duy nhất đưa ra 3 quyết định:
  - Bấm **"Phê duyệt"** (`APPROVED`): Lưu chính thức vào ngân hàng đề môn học.
  - Bấm **"Yêu cầu chỉnh sửa"** (`NEEDS_REVISION` kèm lý do góp ý): Trả về cho giảng viên hiệu chỉnh lại rồi nộp lại.
  - Bấm **"Từ chối"** (`REJECTED` kèm lý do): Loại bỏ câu hỏi.
* **Barem Rubric chuẩn hóa:** Tổng điểm các tiêu chí rubric con của mỗi câu hỏi bắt buộc $\sum \equiv 10.0$ điểm (nếu sai lệch FluentValidation chặn và trả `HTTP 422 Unprocessable Entity`). Câu trả lời mẫu (Model Answer) bắt buộc $\ge 50$ ký tự.

### 🔹 MF-04: Thi Thật Phòng Lab Kiosk, Công Bố Điểm & Phúc Khảo Nội Bộ (Official Lab Viva Exam & Internal Appeals)
* **Chu trình Quản trị Kỳ thi & Cấu hình Môn thi của Trưởng Bộ Môn (`department_head`):**
  - **Trưởng Bộ Môn khởi tạo kỳ thi:** Tạo Kỳ thi (`OfficialExamSession` / Exam Season, ví dụ Kỳ thi Kết thúc môn FA26).
  - **Gán môn thi vào kỳ thi:** Đưa danh sách các môn thi thuộc kỳ thi đó vào hệ thống.
  - **Cấu hình môn thi trong kỳ thi:** Khi ấn vào từng môn thi đã tạo trong kỳ thi, Trưởng Bộ Môn thực hiện:
    1. Cấu hình danh sách **Ca thi** (`RealExamSessionShift`: phòng máy lab, kíp thi, thời gian bắt đầu/kết thúc, phân công giám thị).
    2. **Cấu hình Follow-up:** Bật/tắt hỏi chuyên sâu (`has_follow_up`) và số lượng câu hỏi follow-up áp dụng chung cho Môn thi đó trong kỳ thi (`max_follow_up_questions` từ 1–2 câu, đồng bộ cho tất cả các ca thi của môn).
    3. Cấu hình phương thức làm bài `ExamInputMode` (`VoiceOnly`, `VoiceWithTranscriptEdit`, `VoiceAndTextInput`) và thời gian đệm `TranscriptBufferSeconds` (10–300s).
* **An ninh Kiosk Phòng Lab:**
  - Định danh máy trạm: Số máy Kiosk (SeatNumber 1–40) trùng khớp với Số thứ tự trong danh sách ca thi phòng Lab. Cột IP máy trạm thống nhất dùng **`ip_address`**.
  - Chế độ phong tỏa: Fullscreen Kiosk Lockdown, vô hiệu hóa phím tắt hệ thống (Alt+Tab, Windows, F11, F12, DevTools), phát hiện mất focus (`window.onblur`) quá 3 lần thì lập biên bản đình chỉ thi.
  - Kiểm tra micro phần cứng 30s trước khi vào thi đạt $\ge 60$dB.
* **Niêm phong âm thanh (Cloudflare R2):** Luồng audio được stream trực tiếp lên Cloudflare R2 với tên file bất biến **`STT_MSSV.webm`** (ví dụ `01_SE170123.webm`), băm cryptographic SHA-256 niêm phong ngay tại máy trạm Kiosk.
* **Quy trình nộp bài, Hàng đợi chịu tải & Chấm điểm:**
  - Nộp bài xong, hệ thống thực hiện **Persist First** lưu ngay bài thi vào `exam_question_submissions` và cập nhật `student_exam_tickets` với trạng thái **`SUBMITTED` trong $< 100$ms** kèm mã băm SHA-256 niêm phong audio Cloudflare R2 (`STT_MSSV.webm`). Kiosk khóa màn hình và thông báo: *"Bài thi đã được lưu trữ an toàn. Kết quả sẽ do Giảng viên thẩm định và công bố trên hệ thống."* **Thi xong Kiosk TUYỆT ĐỐI KHÔNG CÓ ĐIỂM LIỀN và không cho khiếu nại tại chỗ**. Thí sinh ký biên bản nộp bài giấy và ra về (về nhà).
  - Tác vụ chấm điểm được đẩy vào hàng đợi **BoundedChannel 1,000 slots RAM (`FullMode.Wait`)** để AI (Gemini) thực hiện chấm điểm ngầm toàn bộ bài thi chỉ dựa trên bản *transcript*. Khi AI quá tải (HTTP 429) hoặc timeout, bài thi được cách ly vào **DLQ `dead_letter_queues`** và tiến trình nền retry sau 5 phút, bảo đảm cam kết Zero Data Loss 100%.
* **Chốt chặn kiểm soát chất lượng & Evidence Panel (Sinh viên phải đợi Giảng viên chấm hết bài nghi ngờ/fail):**
  - Hệ thống tự động phân loại kết quả chấm của AI thành 2 nhóm trên Cổng Hậu kiểm Giảng viên:
    + *Nhóm 1 (Đáng nghi ngờ / Fail / Cần can thiệp):* Các bài thi có `is_suspicious == true`, `confidence_score < 0.70` (do ồn, phát âm không rõ, mâu thuẫn chuỗi CoT, điểm ranh giới) hoặc bài thi bị fail/điểm liệt. **Sinh viên bắt buộc phải đợi Giảng viên rà soát và chấm lại toàn bộ các bài trong nhóm này**. Giảng viên đối chiếu trên Evidence Panel (AudioURL R2, Transcript Whisper gốc, AI Chain-of-Thought phân tích tiêu chí) để nghe lại file ghi âm trên Waveform Player, chấm và điều chỉnh lại điểm kèm lý do giải trình bắt buộc (`override_reason`).
    + *Nhóm 2 (Độ tin cậy cao):* Giảng viên rà soát nhanh đối chiếu Evidence Panel.
* **Giảng viên Công Bố Điểm (Sau khi xử lý đầy đủ mới gửi điểm về cho sinh viên):**
  - Sau khi Giảng viên xử lý xong toàn bộ các bài nghi ngờ và fail, bảo đảm **100% sinh viên trong ca thi đã có điểm hoàn chỉnh**, Giảng viên mới bấm **"Công Bố Điểm"** (`POST /api/v1/official-exams/shifts/{shiftId}/publish-grades`) một lần duy nhất. Lúc này hệ thống mới giải phóng điểm gửi về cho sinh viên.
  - Hệ thống kích hoạt cơ chế **One-Way Lock (`is_locked = true`)**. Bộ đánh chặn `OneWayLockInterceptor` chặn 100% mọi thao tác `UPDATE` hoặc `DELETE` điểm số (`HTTP 403 Forbidden`).
* **Sinh viên ở nhà nhận điểm & Quy trình Phúc khảo Nội bộ trong hệ thống:**
  - Sinh viên ở nhà đăng nhập Student Portal trên hệ thống của mình để xem bảng điểm chính thức do Giảng viên công bố.
  - Nếu sinh viên chấp nhận điểm: Bấm xác nhận hoàn thành kỳ thi (`ACKNOWLEDGED`).
  - Nếu sinh viên **không chấp nhận điểm (muốn phúc khảo)**: Sinh viên làm đơn phúc khảo trực tiếp ở phần Phúc khảo trong hệ thống của mình (`POST /api/v1/appeals`). Hệ thống tạo entity `AppealRequest` và tự động gán cho **Trưởng Bộ Môn (`department_head`)** thẩm định độc lập (`APPROVED` điều chỉnh điểm / `REJECTED` giữ nguyên điểm).

---

## 🛡️ 4. MƯỜI HAI CAM KẾT KỸ THUẬT BẤT BIẾN (CAPSTONE INVARIANTS)

1. **Zero Placeholder (100% Complete Code):** CẤM TUYỆT ĐỐI `// TODO`, `/* rest of code */`, `// tương tự như trên`. Mọi handler, controller, service, component phải hoàn chỉnh 100%.
2. **Anti-Mocking trong Production Code:** CẤM sinh code giả lập, dummy return trong mã nguồn nghiệp vụ chính. Cho phép dùng Mocking trong `tests/` và Fallback Adapter cục bộ khi dev offline.
3. **Hard Verification Gate (Theo Cấp Độ S/M/L):** Mức S chỉ cần targeted build/test pass. Mức M/L chạy test suite tương ứng và đạt Exit Code = 0.
4. **Ports mạng bất biến:** Frontend `3000`, Backend API `5000`, Database PostgreSQL `5432` nội bộ (CẤM expose internet).
5. **Hạn ngạch thi thử (MF-02):** Sinh viên chỉ được thi tối đa $K = 3\text{ lượt/ngày/môn}$ (Lượt 4 trả `HTTP 429 Too Many Requests`). Sinh viên tự chọn Có/Không Follow-up trước khi thi thử.
6. **Khóa điểm vĩnh viễn (MF-04):** `is_locked = true` khi giảng viên ký chốt điểm. `OneWayLockInterceptor` chặn 100% UPDATE/DELETE (`HTTP 403 Forbidden`).
7. **Barem Rubric chuẩn hóa (MF-03):** Luôn chuẩn hóa $\sum \equiv 10.0$ điểm (FluentValidation validate tổng các tiêu chí). Mọi câu hỏi (thủ công hay AI FLM) bắt buộc gửi Trưởng Bộ Môn phê duyệt (`APPROVED`, `NEEDS_REVISION`, `REJECTED`).
8. **Audio Upload Cloudflare R2 (MF-04):** Đặt tên file bất biến `STT_MSSV.webm` (ví dụ: `01_SE170123.webm`), SHA-256 integrity hash niêm phong tại máy trạm.
9. **Worker Queue Background & Persist First:** Persist First lưu đĩa cứng $< 100$ms với trạng thái `SUBMITTED`, sau đó đẩy vào `System.Threading.Channels.BoundedChannel<EvaluationTask>` với capacity = 1000, `FullMode.Wait`. Kiosk không có điểm liền, không khiếu nại tại chỗ.
10. **LLM Resilience & DLQ:** Polly Retry 3 lần với Exponential Backoff (2s, 4s, 8s) + Circuit Breaker. Khi LLM thất bại sau 3 lần, đẩy task vào DLQ `dead_letter_queues` và retry sau 5 phút.
11. **Code-Switching & Dynamic Configuration:** Buffer screen đếm ngược theo cấu hình động môn học (`transcript_buffer_seconds`, 10–300s, mặc định 60s). Admin cấu hình số câu follow-up hệ thống luyện tập (1–5 câu, mặc định 2 câu); Per-Q kích hoạt follow-up khi $4.0 \le \text{Score} \le 8.0$, Full-Session không follow-up. Trưởng Bộ Môn khởi tạo kỳ thi, gán môn thi, cấu hình ca thi và cấu hình follow-up môn thi (1–2 câu) trong MF-04.
12. **Kiosk Exam Mode Lockdown:** Phòng thi thật MF-04 kích hoạt Fullscreen Canvas, vô hiệu hóa phím tắt hệ thống (Alt+Tab, Esc, F11, DevTools), phát hiện mất focus (blur event) quá 3 lần thì lập biên bản đình chỉ thi.

---

## 🚫 5. MƯỜI ĐIỀU CẤM KỴ TUYỆT ĐỐI (ANTI-PATTERNS & TABOOS)

Mọi Agent khi sinh mã nguồn **TUYỆT ĐỐI CẤM** vi phạm các điều sau:

1. ❌ **CẤM tạo bảng hoặc cột `chapter`:** Ngân hàng câu hỏi được phân loại theo Môn học (`course_id`), Cấp độ Bloom và CLO. Tuyệt đối không sinh thêm thực thể `chapter`.
2. ❌ **CẤM dùng tên cột `workstation_ip`:** Bắt buộc dùng thống nhất **`ip_address`** (đồng bộ cả C#, SQL Schema và Docs).
3. ❌ **CẤM hardcode thời gian đệm và số câu hỏi phụ:** Thời gian màn hình đệm sửa transcript cấu hình động theo môn (`transcript_buffer_seconds`, từ 10-300s, mặc định 60s); số câu hỏi phụ luyện tập do Admin cấu hình (1–5 câu, mặc định 2 câu); số câu hỏi phụ thi thật do Trưởng Bộ Môn thiết lập theo môn thi trong kỳ thi (`max_follow_up_questions`, từ 1-2 câu, mặc định 1 câu). Tuyệt đối không hardcode cứng các giá trị này.
4. ❌ **CẤM coi AI-gen là quyền riêng của Trưởng bộ môn hoặc bỏ qua phê duyệt:** Giảng viên sử dụng AI sinh câu hỏi theo barem của mình từ FLM hoặc đề cương môn học rồi **BẮT BUỘC gửi Bộ Môn thẩm định**; Trưởng Bộ Môn là chốt chặn phê duyệt duy nhất (APPROVED / NEEDS_REVISION / REJECTED).
5. ❌ **CẤM dùng vé thi chữ thường:** Trạng thái vé thi Kiosk gồm đúng 7 bước viết HOA: `SCHEDULED`, `IN_PROGRESS`, `SUBMITTED`, `AI_GRADED`, `AUDITED`, `PUBLISHED`, `LOCKED`.
6. ❌ **CẤM tạo cổng phúc khảo trên Kiosk hoặc cho điểm liền:** Máy trạm Kiosk phòng Lab TUYỆT ĐỐI KHÔNG CÓ ĐIỂM LIỀN và không khiếu nại tại chỗ. Phúc khảo của sinh viên được thực hiện trực tiếp trên Student Portal sau khi Giảng viên công bố điểm, tạo đơn AppealRequest gán cho Trưởng Bộ Môn thẩm định.
7. ❌ **CẤM code giả định hoặc giữ chỗ (`Zero Placeholder`):** CẤM sinh `// TODO`, `/* rest of code */`, `// giữ nguyên logic cũ`. Mọi hàm, component phải hoàn chỉnh 100%.
8. ❌ **CẤM Mock/Fake Data trong mã nguồn Production:** Cấm hardcode dummy data trong controllers/services. Chỉ cho phép mock trong thư mục `tests/`.
9. ❌ **CẤM sai thứ tự Middleware ASP.NET Core:** `app.UseCors("AllowFrontend")` BẮT BUỘC phải nằm TRƯỚC `app.UseHttpsRedirection()` và static files.
10. ❌ **CẤM tự ý commit hoặc push Git:** Mọi thay đổi mã nguồn phải giữ nguyên ở local working tree để lập trình viên tự kiểm tra.

---

## 👥 6. PHÂN BỔ CÔNG VIỆC CHO 4 THÀNH VIÊN NHÓM FA26SE166

Khi hỗ trợ từng thành viên, Agent phải nắm rõ phạm vi phụ trách:

* **🧑 Nguyễn Quang Thành (Team Leader & Lead Backend Architect):**
  - Phạm vi: `backend/src/API/`, `backend/src/Application/`, `GradingQueueWorker.cs`, SignalR Hubs.
  - Nhiệm vụ: Kiến trúc Clean Arch, CQRS MediatR, Bounded Channel 1000 RAM queue, SignalR Hub, `OneWayLockInterceptor`, API FLM import.
* **🧑 Nguyễn Trọng Tốt (Backend Developer, AI Specialist & QA Lead):**
  - Phạm vi: `backend/src/Infrastructure/Services/`, `backend/tests/OralExamination.UnitTests/`.
  - Nhiệm vụ: Gemini 1.5 Flash/Pro CoT Grading (Rubric 10.0), Whisper STT `timestamps_json`, Cloudflare R2 Presigned URL + SHA-256, AI Doubt Guard, xUnit test suites.
* **🧑 Nguyễn Đăng Hải (DB Specialist & Frontend Developer):**
  - Phạm vi: `infra/postgres/`, `docker-compose.yml`, `frontend/src/*`.
  - Nhiệm vụ: Quản trị 28 bảng CSDL PostgreSQL 16 trong 6 Bounded Contexts (DDL Schema, Seed data, Docker Compose), tuyệt đối KHÔNG code logic C# Backend; dồn toàn lực tham gia phát triển Frontend (Student Portal, Mock Exam Voice-First, Phê duyệt đề Trưởng BM, Phân hệ Phúc khảo).
* **🧑 Lê Vũ Hoàng (Lead Frontend Architect & Fullstack Coordinator):**
  - Phạm vi: `frontend/src/*`, `frontend/package.json`, Tailwind CSS v4.
  - Nhiệm vụ: React 19 SPA, Tailwind CSS v4, Màn hình Kiosk phòng Lab MF-04 (Fullscreen lockdown, blur $\ge 3$, mic $\ge 60$dB), Transcript Buffer Screen, Waveform Audio Player, SignalR Client.

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

## 🚀 8. QUY TRÌNH TÁC CHIẾN THEO CẤP ĐỘ S / M / L

* 🟢 **Mức S (Quick Fix / Sửa bug cục bộ / Scripts ≤ 3 files):**
  - Đọc file $\rightarrow$ Sửa trực diện $\rightarrow$ Targeted build/test pass. Bỏ qua 6 pha, xong ngay trong 1-2 phút.
* 🟡 **Mức M (Single Feature / Endpoint / Component 4–10 files):**
  - Khảo sát 5 ca biên + Viết test mới + Build & Module Test pass.
* 🔴 **Mức L (Epic lớn / Greenfield / Tái cấu trúc Core > 10 files):**
  - Áp dụng trọn vẹn Quy trình 6 Pha:
    `Pha 1: Spec-Locking` $\rightarrow$ `Pha 1.5: Scaffolding` $\rightarrow$ `Pha 2: Contract-First` $\rightarrow$ `Pha 3: Parallel Execution (Subagents)` $\rightarrow$ `Pha 4: Zero-Mock Audit` $\rightarrow$ `Pha 5: Hard Gate Live`.

---

## 📐 9. KIẾN TRÚC CLEAN ARCHITECTURE 4 TẦNG (.NET 8 BACKEND)

```
05_Source_Code/backend/src/
├── Domain/                   # Tầng Core Thuần Khiết (Zero External Dependencies)
│   ├── Entities/             # 28 thực thể tương ứng 28 bảng CSDL (UUID Primary Keys)
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

## ⚡ 10. BỘ LỆNH MỘT CHẠM & CỔNG KIỂM CHỨNG (HARD VERIFICATION GATE)

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

## 📚 11. BỘ QUY CHUẨN QUẢN TRỊ TÀI LIỆU TỐI GIẢN (DOCUMENTATION GOVERNANCE)

1. **Kiểm tra trước khi tạo mới (Search & Reuse First):** Khi nhận yêu cầu cập nhật hoặc viết tài liệu/sơ đồ mới, BẮT BUỘC kiểm tra toàn bộ workspace xem đã có tài liệu nào tương tự hoặc phục vụ mục đích đó chưa (`03_Workflows_and_Diagrams/MASTER_ARCHITECTURE.md`, `02_Bao_cao_Reports/`, v.v.).
2. **Cổng tham vấn bắt buộc (Ask Before Creating New):** Nếu đã tồn tại tài liệu liên quan, BẮT BUỘC dừng lại hỏi người dùng: *"Đã có tài liệu [tên_tệp] liên quan đến vấn đề này. Bạn muốn sửa trực tiếp trong tài liệu đó hay tạo tài liệu mới?"*. Tuyệt đối không tự ý đẻ thêm file mới làm rác dự án.
3. **Quét toàn hệ thống khi có nội dung mới (Zero Blind Spot):** Khi cập nhật bất kỳ kiến thức, logic nghiệp vụ hay sơ đồ mới nào, BẮT BUỘC quét toàn bộ hệ thống (tất cả file `.md`, `.cs`, `.json`) để cập nhật đồng loạt, không để sót bất kỳ tài liệu nào bị cũ hay mâu thuẫn.
4. **Quy tắc Tối giản & Chống phân mảnh (Anti-Proliferation):** Giữ số lượng file ở mức tối thiểu. Nguồn sự thật duy nhất cho kiến trúc hệ thống là `03_Workflows_and_Diagrams/MASTER_ARCHITECTURE.md`. Các file nháp, file trung gian hoặc file đã được hợp nhất 100% phải được chuyển vào `99_Archive/Legacy_Docs/`.
