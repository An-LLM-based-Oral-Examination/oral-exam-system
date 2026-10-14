# 🤖 AGENTS.md — SỔ TAY KỸ SƯ TÁC CHIẾN ĐỒ ÁN SEP490 (FA26SE166)

> **Dự án:** Hệ Thống Luyện Thi & Đánh Giá Vấn Đáp Bằng LLM Cho Ngành Kỹ Thuật Phần Mềm (LLM Oral Exam System)  
> **Mã đề tài:** FA26SE166 | Học kỳ: Fall 2026 (FA26) — Đại học FPT TP.HCM (FPT SG)  
> **Kho mã nguồn:** `05_Source_Code` | **Tech Stack:** .NET 8 Clean Arch · React 19 (Vite) + Router DOM v7 · Tailwind v4 · PostgreSQL 16+ (30 bảng 3NF) · Gemini 1.5 Flash/Pro · Whisper STT · Cloudflare R2  
> **Phạm vi:** Bắt buộc 100% cho AI Agents tại `D:\Đồ Án` (và junction `D:\DoAn`, `05_Source_Code`). Mọi mã nguồn phải tuân thủ 100%.

---

## 👤 1. HỒ SƠ DỰ ÁN, ĐỘI NGŨ & BẢN QUYỀN ĐỒ ÁN
* **Mục tiêu:** Chuẩn hóa tổ chức, thi thử & chấm thi vấn đáp (viva voce) SE bằng AI theo Rubric CLOs, bảo toàn pháp lý phòng thi FPTU.
* **Ngôn ngữ Agent:** **100% Tiếng Việt** trong toàn bộ giải thích, comment và tài liệu.
* **Mô hình:** **Gemini 3.8 Flash (High)** làm Team Lead/Master Orchestrator; **Gemini 3.1 Pro** làm Lead System Architect & Chief Code Auditor.
* **Đội ngũ phát triển (4 Kỹ sư FA26SE166):**
  - **Nguyễn Quang Thành:** Team Leader & Lead Backend Architect
  - **Nguyễn Trọng Tốt:** Backend Developer, AI Specialist & QA Lead
  - **Nguyễn Đăng Hải:** DB Specialist & Frontend Developer
  - **Lê Vũ Hoàng:** Lead Frontend Architect & Fullstack Coordinator

---

## 🔐 2. HỆ THỐNG PHÂN QUYỀN 5 ROLES (RBAC)
1. `student`: Login Google (mọi domain, không password). Luyện tập MF-01 (Per-Question On-Demand / Full-Session Progressive 3–10 câu, timeout 10 phút HTTP 410 Gone, không lưu audio); Thi thử MF-02 (quota Trưởng BM, chọn có/không follow-up, không lưu audio); Thi thật Kiosk MF-04 (stream audio R2); Xem điểm, nộp `AppealRequest` trên Student Portal.
2. `lecturer`: AI gen câu hỏi FLM/CLO hoặc soạn tay, tick 2 kho (`practice_questions`/`exam_questions`), rubric 10.0đ, gửi Trưởng BM duyệt (MF-03); Coi thi; Hậu kiểm bài nghi ngờ/fail qua Waveform Audio Player & Evidence Panel, sửa điểm kèm `override_reason`, công bố điểm ca thi 1 lần duy nhất (MF-04); Chấm lại phúc khảo khi được Trưởng BM giao. CẤM cấu hình follow-up MF-01.
3. `department_head`: Tạo kỳ thi (`OfficialExamSession`), gán môn, ca thi (`RealExamSessionShift`: phòng lab, kíp thi, coi thi), Follow-up (`has_follow_up`, 1–5 câu, mặc định 2), `ExamInputMode` (`VoiceOnly`, `VoiceWithTranscriptEdit`), `TranscriptBufferSeconds` (MF-04); Cấu hình đề, Bloom, quota thi thử (`max_mock_exams_per_day`) theo môn (MF-02); Duyệt đề (`APPROVED`, `NEEDS_REVISION`, `REJECTED`) hoặc tự tạo đề (MF-03); Nhận `AppealRequest`, giao GV chấm lại (MF-04).
4. `proctor`: Điểm danh STT (SeatNumber 1–40), đối chiếu thẻ SV/CCCD, mở ca thi, giám sát Kiosk (MF-04). Dùng màn hình phòng thi.
5. `admin`: Quản trị user (FE-09: khóa/mở, gán role; không tạo user bằng email/password), học kỳ (FE-08: tạo/sửa, không xóa), cấu hình MF-01 qua `system_configs` (follow-up 1–5 mặc định 2; progressive 3–10; buffer 10–300s mặc định 60s; timeout `SessionInactivityTimeoutMinutes = 10` phút), xem Audit Logs, xử lý DLQ. CẤM cấu hình quota thi thử/thi thật.

---

## 🔄 3. ĐẶC TẢ CHI TIẾT 4 MAIN FLOWS NGHIỆP VỤ (NGUỒN SỰ THẬT DUY NHẤT)

### 🔹 MF-01: Luyện Tập Tương Tác Tự Do (Interactive Practice)
* **Cấu hình:** Admin cấu hình `system_configs`: follow-up 1–5 câu (mặc định 2); `MinMixedPracticeQuestions = 3`, `MaxMixedPracticeQuestions = 10`; `TranscriptBufferSeconds` 10–300s (mặc định 60s); timeout `SessionInactivityTimeoutMinutes = 10` phút. GV CẤM cấu hình follow-up MF-01.
* **Per-Question On-Demand:** Chọn mức đơn/tổ hợp (`easy`, `medium`, `hard`). Cấp Câu 1 khi tạo, gọi tiếp on-demand (`POST /api/v1/practice/sessions/{id}/next-question`) đến khi dừng. Anti-3-Consecutive: tối đa 2 câu cùng mức trong 3 câu liên tiếp (2 câu trước cùng mức thì đổi mức; loại trừ `Id NOT IN (...)`; cạn mức bốc mức khác; hết trả `hasMoreQuestions: false`). Follow-up (A2): khi $4.0 \le \text{Score} \le 8.0$ (1–5 câu Admin cấu hình, mặc định 2); ngoài khoảng mở Scorecard.
* **Full-Session Progressive:** Chọn 3–10 câu (`MinMixedPracticeQuestions = 3`, `MaxMixedPracticeQuestions = 10`), chia đều Dễ $\to$ Trung bình $\to$ Khó bốc trọn gói, phát vấn tăng dần (thiếu câu trả `HTTP 400 Bad Request`). CẤM follow-up. Nộp từng câu hoặc trọn gói (`batch-answers`).
* **Timeout 10 phút & Buffer:** Quá 10 phút không tương tác (`last_activity_at`), tự đóng (`status = "completed"`), bảo toàn điểm câu đã làm (Full-Session câu chưa làm tính 0đ); thao tác tiếp trả **HTTP 410 Gone**. Đếm ngược `TranscriptBufferSeconds` (10–300s, mặc định 60s) nghe TTS, xem STT, sửa thuật ngữ SE (Code-Switching). **KHÔNG lưu audio**, chỉ lưu transcript vào `practice_answers`. Lịch sử tại Student Portal (FE-02) cùng thi thử.

### 🔹 MF-02: Thi Thử Tính Giờ Có Hạn Ngạch (Timed Mock Exam)
* **Nguồn đề & Quota:** Bốc từ kho `practice_questions` (cô lập kho `exam_questions` cho MF-04). Quota do Trưởng BM cấu hình theo môn (`max_mock_exams_per_day`), không khóa K=3, không do Admin đặt (vượt quota trả **HTTP 429 Too Many Requests**).
* **Cấu trúc & Follow-up:** Trưởng BM cấu hình cấu trúc đề, Bloom. SV chọn môn và tùy chọn Có/Không Follow-up (không chọn topic/độ khó). Follow-up hỏi theo ngữ cảnh (không theo 4.0–8.0), thời lượng dài hơn.
* **Voice-First Gate & Timer:** Khóa phím, bắt buộc micro. Hết giờ tự nộp; câu chưa làm bỏ trống, không chấm.
* **Lưu trữ & Instant Feedback:** **KHÔNG lưu audio**, chỉ lưu transcript vào `mock_exam_answers`. AI chấm tức thì trả **Scorecard chi tiết kèm nhận xét từng tiêu chí theo CLO** trên màn hình kết quả và lưu lịch sử thi (FE-02).

### 🔹 MF-03: Giảng Viên Sử Dụng AI Sinh Câu Hỏi Theo CLO FLM & Rubric Studio 10.0 (Question Bank & Rubric Studio)
* **AI sinh câu hỏi từ FLM:** AI sinh câu hỏi, rubric, sample answer từ CLOs syllabus FLM (chỉ đọc FLM, không ghi ngược); GV có thể soạn thủ công.
* **Kho & Barem:** GV tick 2 kho `practice_questions` và/hoặc `exam_questions`. Barem rubric bắt buộc $\sum \equiv 10.0$ điểm (sai lệch FluentValidation chặn với **HTTP 422 Unprocessable Entity**). Model Answer $\ge 50$ ký tự.
* **Gửi duyệt & Phê duyệt:** Tạo xong ở `DRAFT`, **BẮT BUỘC gửi Trưởng BM duyệt** (`POST /api/v1/questions/batch-submit-review`) $\to` `SUBMITTED_FOR_REVIEW`. Trưởng BM duyệt: `APPROVED` (vào ngân hàng đề), `NEEDS_REVISION` (kèm góp ý), `REJECTED` (loại bỏ). Trưởng BM có quyền tự tạo đề.

### 🔹 MF-04: Thi Thật Phòng Lab Kiosk, Công Bố Điểm & Phúc Khảo Nội Bộ (Official Lab Viva Exam & Internal Appeals)
* **Cấu hình Trưởng BM:** Tạo kỳ thi (`OfficialExamSession`), gán môn, ca thi (`RealExamSessionShift`: phòng lab, kíp thi, coi thi), Follow-up (`has_follow_up`, 1–5 câu, mặc định 2), `ExamInputMode` (`VoiceOnly`, `VoiceWithTranscriptEdit`), `TranscriptBufferSeconds` (10–300s).
* **An ninh & Niêm phong:** Điểm danh thẻ SV/CCCD; số máy (SeatNumber 1–40) trùng STT ca; cột IP dùng **`ip_address`**. Fullscreen Lockdown, chặn Alt+Tab, Win, F11, F12, DevTools; blur > 3 lần đình chỉ. Test mic 30s đạt $\ge 60$dB. Thi thật CÓ lưu audio stream Cloudflare R2 tên file bất biến **`STT_MSSV.webm`** (ví dụ `01_SE170123.webm`), băm cryptographic SHA-256 niêm phong tại máy trạm Kiosk.
* **Nộp bài & Chấm ngầm:** Persist First lưu `exam_question_submissions` và `student_exam_tickets` trạng thái **`SUBMITTED` trong $< 100$ms** kèm mã SHA-256 audio R2 (`STT_MSSV.webm`). Kiosk khóa màn hình báo bài thi đã lưu an toàn, điểm do GV công bố; **TUYỆT ĐỐI KHÔNG CÓ ĐIỂM LIỀN và không khiếu nại tại chỗ**. Ký biên bản giấy ra về. Chấm ngầm BoundedChannel 1,000 slots RAM (`FullMode.Wait`), Gemini chấm ngầm transcript đã sửa. Quá tải (HTTP 429) hoặc timeout cách ly **DLQ `dead_letter_queues`**, retry sau 5 phút (Zero Data Loss).
* **Hậu kiểm & Công bố điểm:** Cổng Hậu kiểm chia 2 nhóm: Nhóm 1 (Cần xem lại/Fail/Nghi ngờ: `is_suspicious == true`, `confidence_score < 0.70` do ồn, phát âm, CoT, ranh giới hoặc fail/liệt): GV đối chiếu Evidence Panel (AudioURL R2, Transcript Whisper gốc, AI CoT), nghe Waveform Audio Player (1.0x–1.5x), sửa điểm kèm giải trình bắt buộc (`override_reason`). Nhóm 2 (Tin cậy cao): GV rà soát nhanh qua Evidence Panel. Khi 100% SV có điểm, GV bấm **"Công Bố Điểm"** (`POST /api/v1/official-exams/shifts/{shiftId}/publish-grades`) 1 lần duy nhất. Kích hoạt **One-Way Lock (`is_locked = true`)**, `OneWayLockInterceptor` chặn 100% UPDATE/DELETE điểm trái phép (**HTTP 403 Forbidden**).
* **Xem điểm & Phúc khảo:** SV xem điểm Student Portal; đồng ý: xác nhận (`ACKNOWLEDGED` qua `POST /api/v1/student/official-exams/{ticketId}/acknowledge-grade`); phúc khảo: nộp đơn hệ thống (`POST /api/v1/appeals`), tạo `AppealRequest` chuyển Trưởng BM; Trưởng BM giao một GV chấm lại (`PUT /api/v1/appeals/{id}/assign-lecturer`). One-Way Lock mở đường GV chấm lại cập nhật điểm mới. Báo cáo điểm: **file Excel (.xlsx)** và **file PDF có chữ ký số**.

---

## 🧠 4. NGUYÊN TẮC TƯ DUY PHẢN BIỆN & THẨM ĐỊNH ĐỐI THOẠI TRƯỚC KHI SỬA (CRITICAL THINKING & CONSULTATION FIRST)
> *"Không vội vàng sửa code khi chưa suy xét. Lỗi của user cũng có thể nhầm. Sự thật nằm ở mã nguồn và dữ liệu thực tế."*

Agent nhận yêu cầu/báo cáo lỗi **BẮT BUỘC** qua 3 bước:
1. **Kiểm chứng sự thật (Fact-Checking):** CẤM "Yes-Man" sửa ngay. Khảo sát code (`view_file`), CSDL (`OralExamDbContext & Migrations`), test (`dotnet test`), docs xác minh lỗi thật hay do user nhầm (tên biến, luồng MF, role, chưa build).
2. **Đối thoại phản biện (Consultation):** User đúng: xác nhận root-cause, Impact Boundary, giải pháp. User nhầm: giải thích nhã nhặn kèm bằng chứng (file, dòng code, log test). Nhiều phương án: lập bảng so sánh để user chọn.
3. **Cổng đồng thuận (Agreement Gate):** CHỈ SỬA MÃ NGUỒN HOẶC TÀI LIỆU KHI ĐÃ THẢO LUẬN VÀ ĐẠT ĐỒNG THUẬN TỪ NGƯỜI DÙNG. Cấm tự ý sửa âm thầm.

---

## 🛡️ 5. MƯỜI HAI CAM KẾT KỸ THUẬT BẤT BIẾN (CAPSTONE INVARIANTS)
1. **Zero Placeholder:** CẤM `// TODO`, `/* rest of code */`. 100% hoàn chỉnh production.
2. **Anti-Mocking:** CẤM dummy data trong production. Chỉ mock trong `tests/` và Adapter khi offline.
3. **Hard Verification Gate:** Mức S cần targeted build/test pass; Mức M/L chạy test suite, Exit Code = 0.
4. **Ports bất biến:** Frontend `3000`, Backend API `5000`, PostgreSQL `5432` nội bộ (CẤM expose).
5. **Hạn ngạch thi thử (MF-02):** Trưởng BM cấu hình (`max_mock_exams_per_day`), không cố định K=3. Vượt quota trả `HTTP 429 Too Many Requests`. SV chọn Có/Không Follow-up.
6. **Khóa điểm 1 chiều & Phúc khảo (MF-04):** `is_locked = true` khi GV công bố điểm. `OneWayLockInterceptor` chặn UPDATE/DELETE (`HTTP 403 Forbidden`), mở đường GV chấm lại `AppealRequest` sửa điểm.
7. **Barem Rubric chuẩn hóa (MF-03):** $\sum \equiv 10.0$ điểm (FluentValidation). Đề bắt buộc gửi Trưởng BM duyệt (`APPROVED`, `NEEDS_REVISION`, `REJECTED`).
8. **Audio R2 (MF-04):** Thi thật lưu audio tên file bất biến `STT_MSSV.webm` (ví dụ `01_SE170123.webm`), SHA-256 niêm phong tại máy trạm. Luyện tập (MF-01) và thi thử (MF-02) KHÔNG lưu audio.
9. **Worker Queue & Persist First:** Persist First lưu đĩa $< 100$ms `SUBMITTED`, đẩy `BoundedChannel<EvaluationTask>` 1,000 slots RAM (`FullMode.Wait`). Kiosk không điểm liền, không khiếu nại tại chỗ.
10. **LLM Resilience & DLQ:** Polly Retry 3 lần (2s, 4s, 8s) + Circuit Breaker; hết retry vào DLQ `dead_letter_queues`, retry sau 5 phút (Zero Data Loss).
11. **Code-Switching & Cấu hình động:** Buffer đếm ngược `TranscriptBufferSeconds` (10–300s, mặc định 60s); Admin cấu hình MF-01 (follow-up 1–5 mặc định 2, progressive 3–10 câu, timeout `SessionInactivityTimeoutMinutes = 10` phút trả HTTP 410 Gone, bảo toàn điểm); Trưởng BM cấu hình kỳ thi, ca thi, quota MF-02 (`max_mock_exams_per_day`), follow-up thi thật MF-04 (1–5 câu, mặc định 2).
12. **Kiosk Lockdown:** MF-04 Fullscreen Canvas, tắt Alt+Tab, Esc, F11, DevTools; blur > 3 lần đình chỉ thi.

---

## 🚫 6. 12 ĐIỀU CẤM KỴ TUYỆT ĐỐI (12 ANTI-PATTERNS & TABOOS)
1. ❌ **CẤM bảng/cột `chapter`:** Phân loại theo Môn (`course_id`), Bloom, CLO. Cấm thực thể `chapter`.
2. ❌ **CẤM dùng `workstation_ip`:** Bắt buộc dùng **`ip_address`** (đồng bộ C#, SQL Schema, Docs).
3. ❌ **CẤM hardcode buffer và follow-up:** Admin cấu hình buffer 10–300s (mặc định 60s) & follow-up 1–5 (mặc định 2) MF-01. Trưởng BM cấu hình quota thi thử và follow-up thi thật (1–5, mặc định 2).
4. ❌ **CẤM coi AI-gen là quyền riêng Trưởng BM hoặc bỏ duyệt đề:** GV tạo câu hỏi bắt buộc gửi duyệt; Trưởng BM phê duyệt duy nhất (APPROVED / NEEDS_REVISION / REJECTED).
5. ❌ **CẤM vé thi chữ thường:** Trạng thái vé thi Kiosk gồm đúng 7 bước viết HOA: `SCHEDULED`, `IN_PROGRESS`, `SUBMITTED`, `AI_GRADED`, `AUDITED`, `PUBLISHED`, `LOCKED`.
6. ❌ **CẤM cổng phúc khảo Kiosk hoặc cho điểm liền:** Kiosk TUYỆT ĐỐI KHÔNG CÓ ĐIỂM LIỀN và không khiếu nại tại chỗ. Phúc khảo trên Student Portal sau khi GV công bố điểm, tạo `AppealRequest`, Trưởng BM giao GV chấm lại.
7. ❌ **CẤM code giả định hoặc giữ chỗ (`Zero Placeholder`):** CẤM `// TODO`, `/* rest of code */`. 100% code hoàn chỉnh.
8. ❌ **CẤM Mock/Fake Data trong mã nguồn Production:** Cấm dummy data trong production. Chỉ mock trong thư mục `tests/`.
9. ❌ **CẤM sai thứ tự Middleware ASP.NET Core:** `app.UseCors("AllowFrontend")` BẮT BUỘC trước `app.UseHttpsRedirection()` và static files.
10. ❌ **CẤM tự ý commit hoặc push Git:** Giữ thay đổi ở local working tree để dev tự kiểm tra trước khi commit/push.
11. ❌ **CẤM sửa code vội vàng khi chưa suy xét:** Cấm sửa code khi chưa kiểm chứng thực tế và chưa thống nhất với user.
12. ❌ **CẤM commit sai chuẩn Conventional Commits:** Bắt buộc chuẩn **Conventional Commits v1.0.0** bằng **Tiếng Anh**: `<type>(<scope>): <subject in imperative mood>` (`feat`, `fix`, `docs`, `style`, `refactor`, `perf`, `test`, `build`, `ci`, `chore`). Cấm commit không tiền tố hoặc tiếng Việt.

---

## 👥 7. PHÂN BỔ CÔNG VIỆC CHO 4 THÀNH VIÊN NHÓM FA26SE166
* **🧑 Nguyễn Quang Thành (Team Leader & Lead Backend Architect):** Scope: `backend/src/API/`, `Features/{Practice,MockExams}`, `SignalR Hubs`, Báo cáo Khảo thí. Nhiệm vụ: Lead BE MF-01 (Per-Question On-Demand Anti-3-Consecutive, Full-Session Progressive 3–10 câu, timeout `SessionInactivityTimeoutMinutes = 10` trả HTTP 410 Gone, BoundedChannel 1000 slots RAM, SignalR `PracticeHub`) & MF-02 (Quota `max_mock_exams_per_day`, Voice-First Gate, kho `practice_questions`, Instant Scorecard); Báo cáo Khảo thí (.xlsx + PDF có chữ ký số); Clean Architecture; APIs FE-02, FE-10.
* **🧑 Nguyễn Trọng Tốt (Backend Developer, AI Specialist & QA Lead):** Scope: `backend/src/Infrastructure/Services/`, `Features/{Questions,OfficialExams,Appeals,Academic,Notifications}`, `tests/`. Nhiệm vụ: Auth Google OAuth 2.0 PKCE; BE MF-03 (AI FLM CLO, Barem 10.0đ, tick 2 kho, duyệt đề) & MF-04 (Kiosk `ip_address`, Persist First < 100ms, Audio R2 `STT_MSSV.webm` + SHA-256, Background AI Grading BoundedChannel 1000 slots, OneWayLock, `PublishGrades`, `AppealRequest` Trưởng BM giao GV chấm lại); APIs FE-08, FE-09, FE-11; Polly/DLQ; xUnit, NetArchTest.
* **🧑 Nguyễn Đăng Hải (DB Specialist & Frontend Developer):** Scope: `docker-compose.yml`, `frontend/src/*`. Nhiệm vụ: 30 bảng PostgreSQL 16 (6 Bounded Contexts qua EF Core Code-First — Đã xong, CẤM code C# BE); FE: Dashboard (FE-10), Lịch sử (FE-02), Thi thử MF-02 (chặn Quota 429, Scorecard), Trưởng BM duyệt đề MF-03 & Rubric Studio, ca thi & giao phúc khảo MF-04, FE-08, FE-09.
* **🧑 Lê Vũ Hoàng (Lead Frontend Architect & Fullstack Coordinator):** Scope: `frontend/src/*`, `frontend/package.json`, Tailwind CSS v4. Nhiệm vụ: FE Core (React 19 SPA, Tailwind CSS v4, Router DOM v7 Guards 5 roles, Axios RFC 7807, Zustand stores); Luyện tập MF-01 (Màn hình đệm 60s, Web Speech API, SignalR `usePracticeHub.ts`); Kiosk MF-04 (Lockdown, blur >= 3, mic >= 60dB, Upload R2 `STT_MSSV.webm` + SHA-256); Evidence Panel GV (Waveform Audio Player Wavesurfer.js, sửa điểm, Publish Grades); Auth UI, FE-10, FE-11.

---

## ⚡ 8. NGUYÊN TẮC ĐIỀU PHỐI MULTI-AGENT (ORCHESTRATION PROTOCOL)
* **Phân định:** Tác vụ lớn/mới/tái cấu trúc: **BẮT BUỘC** dùng `invoke_subagent` song song. Quick Fix (< 50 dòng): Lead Orchestrator làm trực tiếp (SOLO Mode).
* **Invocation Contract:** Dùng `invoke_subagent` với `TypeName: "self"`, `Role: "<Tên vai trò>"` (hoặc `define_subagent` đầu phiên).
* **5 Subagent Archetypes:**
  1. `backend-dotnet8` (Lead Backend Engineer): .NET 8 Clean Arch, CQRS MediatR, FluentValidation, EF Core, Bounded Channel, Polly Retry.
  2. `frontend-react` (Lead Frontend Architect): React 19, Vite, React Router DOM v7, Tailwind CSS v4, Web Speech API, Buffer Screen, Canvas Kiosk Lockdown.
  3. `ai-pipeline-engineer` (AI & Voice Specialist): Gemini 1.5 Flash/Pro Prompt CoT, Structured JSON, Whisper STT, Cloudflare R2 Upload (`STT_MSSV.webm`), Hash SHA-256.
  4. `qa-verifier` (QA Lead): `dotnet test`, `npm run test`, NetArchTest kiểm thử kiến trúc, trích xuất Exit Code (0).
  5. `docs-diagram-architect` (Technical Writer & Visual Architect): DrawIO, Mermaid (.mmd), SVG, SRS 47 Use Cases, Báo cáo Hội đồng.

---

## 🚀 9. QUY TRÌNH TÁC CHIẾN THEO CẤP ĐỘ S / M / L
* 🟢 **Mức S (Quick Fix / Sửa bug cục bộ / Scripts <= 3 files):** Đọc file -> Sửa trực diện -> Targeted build/test pass. Xong trong 1–2 phút.
* 🟡 **Mức M (Single Feature / Endpoint / Component 4–10 files):** Khảo sát 5 ca biên + Viết test mới + Build & Module Test pass.
* 🔴 **Mức L (Epic lớn / Greenfield / Tái cấu trúc Core > 10 files):** Áp dụng trọn vẹn 6 Pha: `Pha 1: Spec-Locking` -> `Pha 1.5: Scaffolding` -> `Pha 2: Contract-First` -> `Pha 3: Parallel Execution (Subagents)` -> `Pha 4: Zero-Mock Audit` -> `Pha 5: Hard Gate Live`.

---

## 📐 10. KIẾN TRÚC CLEAN ARCHITECTURE 4 TẦNG (.NET 8 BACKEND)
```
05_Source_Code/backend/src/
├── Domain/                   # Core (Zero Dependencies): 30 Entities (UUID), DomainEnums, Interfaces
├── Application/              # Use Cases: CQRS MediatR (Practice, MockExams, Questions, OfficialExams, Appeals)
├── Infrastructure/           # Ngoại vi: OralExamDbContext, OneWayLock, Services (Gemini, Whisper, R2), GradingQueueWorker
└── API/                      # Gateway: Controllers (RFC 7807), SignalR Hubs (/hubs/practice), Program.cs
```

---

## ⚡ 11. BỘ LỆNH MỘT CHẠM & CỔNG KIỂM CHỨNG (HARD VERIFICATION GATE)
Sau khi chỉnh sửa mã nguồn, Agent **BẮT BUỘC** chạy và xác thực Exit Code = 0 bằng bộ lệnh với đường dẫn chuẩn xác:
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
1. **Search & Reuse First:** Kiểm tra tài liệu tương tự hiện có (`05_Source_Code/docs/01_architecture/MASTER_ARCHITECTURE.md`, `02_Bao_cao_Reports/`) trước khi viết mới.
2. **Ask Before Creating New:** Đã có tài liệu liên quan, BẮT BUỘC hỏi user trước khi tạo file mới. Cấm đẻ file rác.
3. **Zero Blind Spot:** Cập nhật kiến thức mới BẮT BUỘC quét toàn hệ thống (`.md`, `.cs`, `.json`) để đồng bộ toàn diện.
4. **Anti-Proliferation:** Nguồn sự thật duy nhất cho kiến trúc là `05_Source_Code/docs/01_architecture/MASTER_ARCHITECTURE.md`. File cũ chuyển vào `99_Archive/Legacy_Docs/`.
