# 🏛️ TÀI LIỆU KIẾN TRÚC TỔNG THỂ HỆ THỐNG (MASTER SYSTEM ARCHITECTURE)
## HỆ THỐNG LUYỆN THI & ĐÁNH GIÁ VẤN ĐÁP BẰNG LLM CHO NGÀNH KỸ THUẬT PHẦN MỀM
### Đồ Án Tốt Nghiệp Capstone: FA26SE166 — Khoa Kỹ Thuật Phần Mềm — Đại Học FPT TP.HCM (FPT SG)

> [!NOTE]
> **THÔNG TIN DỰ ÁN & ĐỊNH DANH HỘI ĐỒNG:**
> - **Mã đề tài:** FA26SE166 | **Học kỳ:** Fall 2026 (FA26)
> - **Chuyên ngành:** Kỹ thuật Phần mềm (Software Engineering - SE) — Trường Đại học FPT TP.HCM (FPT SG)
> - **Giảng viên hướng dẫn:** ThS. Nguyễn Thị Cẩm Hương (`huongntc2@fpt.edu.vn`)
> - **Nhóm kỹ sư thực hiện (Core Engineering Team - 4 thành viên):**
>   - 🧑 **Nguyễn Quang Thành** — Team Leader, Lead Backend Engineer & System Architect
>   - 🧑 **Nguyễn Trọng Tốt** — Backend Developer, AI Engineer & QA Lead
>   - 🧑 **Phạm Nguyễn Đăng Hải** — DB Specialist & Frontend Developer
>   - 🧑 **Lê Vũ Hoàng** — Lead Frontend Architect & Fullstack Coordinator
> - **Kiến trúc công nghệ chuẩn:** .NET 8 Clean Architecture 4 tầng · React 19 (Vite) + Tailwind CSS v4 + shadcn/ui · PostgreSQL 16 (38 bảng chuẩn hóa 3NF trong 7 Bounded Contexts, phân tách Luyện tập, Thi cử & Ca thi tại lớp) · Google Gemini 1.5 Flash / Gemini 1.5 Pro · Whisper Large-v3 STT · Cloudflare R2 (S3 API Zero-Egress) · SignalR Core
> - **Vị thế tài liệu:** **Bản Kiến Trúc Tối Thượng — Nguồn Sự Thật Duy Nhất (Single Source of Truth)** hợp nhất toàn bộ đặc tả hệ thống, phục vụ Hội đồng chấm bảo vệ tốt nghiệp Capstone FPTU và kim chỉ nam kỹ thuật cho toàn bộ nhóm tác chiến.

---

## 📑 MỤC LỤC TÀI LIỆU TOÀN DIỆN (8 PHẦN CỐT LÕI)

- [Phần 1: Tổng Quan Đề Tài & Bảng Khóa Thông Số Kỹ Thuật Bất Biến](#phan-1)
  - [1.1. Bối cảnh & Tuyên ngôn Kiến trúc Enterprise](#phan-11)
  - [1.2. Bảng Khóa Thông Số Nghiệp Vụ & Kỹ Thuật Bất Biến (System Invariants)](#phan-12)
  - [1.3. Ma Trận Ngăn Xếp Công Nghệ Đa Ngôn Ngữ (Polyglot Tech Stack)](#phan-13)
- [Phần 2: Sơ Đồ C4 Component & System Context](#phan-2)
  - [2.1. Sơ Đồ C4 System Context & Component (Mermaid)](#phan-21)
  - [2.2. Phân Tích Ranh Giới Kỹ Thuật & Nguyên Tắc Zero-Trust](#phan-22)
- [Phần 3: Sơ Đồ Kiến Trúc Hệ Thống Tổng Thể Theo Chiều Dọc (Top-to-Bottom Flowchart TD)](#phan-3)
  - [3.1. Sơ Đồ Kiến Trúc Hệ Thống 5 Tầng Chiều Dọc (Mermaid)](#phan-31)
  - [3.2. Phân Tích Kỹ Thuật Chuyên Sâu 5 Tầng Hệ Thống](#phan-32)
- [Phần 4: Kiến Trúc Phân Tầng Clean Architecture 4 Tầng & React 19 Frontend](#phan-4)
  - [4.1. Sơ Đồ Phân Tầng Clean Architecture (.NET 8 + React 19)](#phan-41)
  - [4.2. Ranh Giới 4 Tầng & Nguyên Lý Đảo Ngược Phụ Thuộc (DIP)](#phan-42)
- [Phần 5: Sơ Đồ Hạ Tầng Mạng & Triển Khai Docker/Nginx Topology](#phan-5)
  - [5.1. Sơ Đồ Mạng & Containerization Topology (Mermaid)](#phan-51)
  - [5.2. Bảng Phân Bổ Cổng Mạng, Vùng Cách Ly & Chính Sách Tường Lửa](#phan-52)
  - [5.3. Cấu Hình Reverse Proxy & Docker Compose Chuẩn Mực](#phan-53)
- [Phần 6: Luồng Dữ Liệu Tương Tác Của 4 Core Main Flows & Bộ Quy Tắc Follow-up Kép](#phan-6)
  - [6.1. Sơ Đồ Tổng Quan Luồng Dữ Liệu 4 Main Flows (Mermaid)](#phan-61)
  - [6.2. Ma Trận So Sánh Toàn Diện 4 Core Main Flows](#phan-62)
  - [6.3. Đặc Tả Chuyên Sâu Cơ Chế followup_id & Bộ Quy Tắc Kích Hoạt Follow-up Kép](#phan-63)
  - [6.4. Sơ Đồ Chi Tiết Từng Luồng Nghiệp Vụ (Flowchart TD Độc Lập)](#phan-64)
  - [6.5. Sơ Đồ Tuần Tự Tương Tác Chi Tiết 4 Core Main Flows (Sequence Diagrams)](#phan-65)
    - [6.5.1. MF-01: Luyện Tập Vấn Đáp Tự Do (Interactive Practice)](#phan-651)
    - [6.5.2. MF-02: Thi Thử Vấn Đáp Bấm Giờ (Timed Mock Exam)](#phan-652)
    - [6.5.3. MF-03: Quản Lý Ngân Hàng Câu Hỏi & Barem Rubric 10.0 (Question Studio)](#phan-653)
    - [6.5.4. MF-04: Thi Thật Phòng Lab & Thẩm Định Điểm (Official Lab Exam & Audit)](#phan-654)
  - [6.6. Xử Lý 4 Cạnh Biên Nghiệp Vụ Đặc Thù (Edge-Cases Resolution)](#phan-66)
  - [6.7. Luồng Nghiệp Vụ Bổ Trợ: Phân Cấp Học Thuật 4 Tầng (Academic Hierarchy - R5)](#phan-67)
  - [6.8. Luồng Nghiệp Vụ Bổ Trợ: Ca Thi Vấn Đáp Tại Lớp & Mật Khẩu Xoay Vòng Động (In-Class Exam Shifts & TOTP - R6)](#phan-68)
- [Phần 7: Kiến Trúc Phòng Thủ 4 Tầng Chịu Tải & Chống Mất Dữ Liệu (Zero Data Loss)](#phan-7)
  - [7.1. Sơ Đồ Cơ Chế Phòng Thủ 4 Tầng Liên Hoàn (Mermaid)](#phan-71)
  - [7.2. Phân Tích Kỹ Thuật Từng Tầng Chống Chịu Sự Cố](#phan-72)
- [Phần 8: Ma Trận Cổng Mạng, Ranh Giới Bảo Mật & Danh Mục Mã Lỗi RFC 7807](#phan-8)
  - [8.1. Ma Trận Cổng Mạng & Ranh Giới Phân Vùng Bảo Mật](#phan-81)
  - [8.1.1. Ma Trận Phân Quyền Vai Trò Người Dùng (RBAC Authorization Matrix)](#phan-811)
  - [8.2. Danh Mục Mã Lỗi Chuẩn Hóa RFC 7807 (ProblemDetails Matrix)](#phan-82)
  - [8.3. Cam Kết Mức Độ Dịch Vụ & Mục Tiêu Hiệu Năng (SLA & Latency Targets)](#phan-83)

---

## <a id="phan-1"></a>📌 PHẦN 1: TỔNG QUAN ĐỀ TÀI & BẢNG KHÓA THÔNG SỐ KỸ THUẬT BẤT BIẾN

### <a id="phan-11"></a>1.1. Bối Cảnh & Tuyên Ngôn Kiến Trúc Enterprise
Thi vấn đáp (Oral Examination / Viva Voce) là hình thức đánh giá học thuật có độ tin cậy và giá trị sư phạm cao nhất trong ngành Kỹ thuật Phần mềm (Software Engineering), giúp kiểm tra trực tiếp phản xạ tư duy, khả năng lập luận kiến trúc và chiều sâu chuyên môn của sinh viên. Tuy nhiên, rào cản lớn nhất của hình thức này là sự tốn kém tài nguyên giảng viên, tính chủ quan trong quá trình đánh giá và áp lực tâm lý cho người học.

Đề tài **FA26SE166** giải quyết triệt để bài toán trên bằng cách xây dựng hệ thống luyện thi và khảo thí vấn đáp thông minh theo chuẩn công nghiệp:
1. **Chuẩn hóa khung đánh giá:** Ứng dụng mô hình tư duy Chain-of-Thought (CoT) của Google Gemini 1.5 Flash/Pro kết hợp Barem Rubric 10.0đ phân tách tiêu chí theo 6 mức độ nhận thức của thang đo Bloom.
2. **Kỹ thuật âm thanh kép:** Kết hợp nhận dạng giọng nói tức thời Client-side (< 0.5s) bằng Web Speech API cho giai đoạn tự luyện và bóc băng Server-side chính xác cao bằng OpenAI Whisper Large-v3 kèm mốc thời gian (timestamps) cho kỳ thi thật phòng Lab.
3. **Phòng thủ dữ liệu tuyệt đối (Zero Data Loss):** Kiến trúc phân tách tiếp nhận và xử lý (Decoupled Ingestion & Worker), ghi đĩa tức thời Persist-First (< 100ms), điều tiết bộ nhớ RAM qua Bounded Channel 1,000 slots, tự phục hồi qua Polly Retry và cách ly lỗi an toàn qua Dead-Letter Queue (DLQ).
4. **Bảo chứng pháp lý khảo thí:** Khóa điểm một chiều (One-Way Lock) bằng EF Core Database Interceptors ở cấp độ CSDL, niêm phong băng ghi âm `STT_MSSV.webm` bằng mã băm cryptographic SHA-256 trên Cloudflare R2 Zero-Egress.

### <a id="phan-12"></a>1.2. Bảng Khóa Thông Số Nghiệp Vụ & Kỹ Thuật Bất Biến (System Invariants)

Bảng dưới đây là **Cam kết Kỹ thuật Bất biến (Non-Negotiable System Invariants)** được nhóm kỹ sư trưởng khóa cứng trên toàn bộ hệ thống:

| STT | Tham số / Cơ chế kỹ thuật | Giá trị khóa cứng | Ý nghĩa nghiệp vụ & Hành vi kỹ thuật | Mã trạng thái / Mã lỗi phản hồi |
|:---:|:---|:---:|:---|:---:|
| 1 | **Frontend Port** | `3000` (Internal) | Nginx Reverse Proxy trỏ toàn bộ traffic trang chủ, static assets và SPA Router sang container React 19 Vite. | `HTTP 200 OK` |
| 2 | **Backend API Port** | `5000` (Internal) | Nginx chuyển hướng toàn bộ `/api/v1/*` và nâng cấp giao thức WebSocket `/hubs/*` sang ASP.NET Core 8 Kestrel Server. | `HTTP 200` / `202` / `WSS` |
| 3 | **Database Port** | `5432` (Internal) | Cổng nội bộ PostgreSQL 16 trong Docker network `oralexam-net`. **CẤM TUYỆT ĐỐI mở ra Internet**. Chỉ Backend truy cập qua Connection Pool. | Npgsql TCP Pool |
| 4 | **Hạn ngạch thi thử (MF-02)** | `Cấu hình theo môn do Admin thiết lập (max_mock_exams_per_day)` | Admin cấu hình số lượt thi thử tối đa trong ngày cho từng môn học (`max_mock_exams_per_day`, không khóa cứng K=3). Trưởng Bộ Môn cấu hình cấu trúc đề thi thử, ma trận Bloom và thời lượng. Vượt hạn ngạch bị chặn bởi `PostgreSqlQuotaService` để chống lãng phí quota AI và ép ôn tập sâu. | `HTTP 429 Too Many Requests` |
| 5 | **Khóa điểm 1 chiều (MF-04)** | `is_locked = true` | Sau khi Giám khảo hoàn tất hậu kiểm bài thi, Cán bộ Khảo thí bấm Publish Điểm (`POST /api/v1/official-exams/shifts/{shiftId}/publish-grades`). `OneWayLockInterceptor` chặn 100% lệnh `UPDATE`/`DELETE` ở mức CSDL. Ngoại lệ hợp lệ duy nhất: Khi có đơn Phúc khảo nội bộ (`AppealRequest`) được Trưởng Bộ Môn giao cho Giảng viên chấm lại, Giảng viên được phân công mới có quyền cập nhật điểm phúc khảo kèm lý do. | `HTTP 403 Forbidden` |
| 6 | **Ràng buộc Barem Rubric & Phê duyệt (MF-03)** | $\sum \text{Tiêu chí} \equiv 10.0\text{đ}$ & Duyệt Bộ Môn | Client Guard khóa nút Lưu/Gửi duyệt; AI sinh câu hỏi, rubric và sample answer từ chuẩn đầu ra CLO quản lý trong `course_clos` (3NF); Giảng viên tùy chỉnh đề bài, tick kho (`practice_questions` và/hoặc `exam_questions`) và barem 10.0đ rồi gửi Bộ Môn (`SUBMITTED_FOR_REVIEW`); Trưởng Bộ Môn thẩm định đối chiếu trùng câu hỏi và Phê duyệt (`OFFICIAL`) / Yêu cầu sửa (`NEEDS_REVISION`) / Từ chối (`REJECTED`). Khi đủ số lượng bộ đề yêu cầu, Trưởng BM bấm chuyển giao `HANDED_OVER` cho Khảo thí. FluentValidation chặn lưu nếu lệch 10.0đ. | `HTTP 422 Unprocessable` |
| 7 | **Dung lượng Bounded Channel** | `1,000 slots` | Hàng đợi bộ nhớ RAM trung chuyển giữa tiếp nhận bài và Worker chấm điểm (`BoundedChannelFullMode.Wait`). Chống tràn RAM (OOM). | Backpressure Control |
| 8 | **Chính sách Polly Retry** | `2s → 4s → 8s` | Tự động thử lại cuộc gọi Gemini AI khi gặp sự cố chập chờn mạng hoặc quá tải API (HTTP 429/503), tối đa 3 lần với hệ số mũ. | Exponential Backoff |
| 9 | **Tần suất quét Dead-Letter Queue** | `5 phút / lần` | Tiến trình nền `DlqReplayWorker` định kỳ 5 phút quét các bài nộp lỗi cách ly trong bảng `dead_letter_queues` để tự động chấm bù. (Phân quyền quản trị & replay: Admin quản trị và kích hoạt replay DLQ cho MF-01/MF-02; Cán bộ Khảo thí quản trị và kích hoạt replay DLQ cho MF-04; Trưởng Bộ Môn tuyệt đối KHÔNG can thiệp DLQ). | Zero Data Loss 100% |
| 10 | **Định danh file ghi âm phòng Lab** | `STT_MSSV.webm` | Quy tắc đặt tên bất biến: `Số_thứ_tự_máy` + `Mã_sinh_viên`. Ví dụ: `01_SE170123.webm` lưu trữ tại Cloudflare R2, niêm phong bằng SHA-256 qua 2 API. | SHA-256 Seal |
| 11 | **Quy tắc Follow-up MF-01 (Luyện tập)** | `Admin cấu hình (1-5 câu, mặc định 2); Per-Q khi 4.0-8.0đ, Full-Session không` | Giảng viên KHÔNG cấu hình follow-up trong MF-01. Admin cấu hình hệ thống (1–5 câu, mặc định 2 câu). Sinh viên chọn [Per-Question]: On-Demand chọn đơn/tổ hợp mức độ (easy/medium/hard), cấp câu 1 rồi bốc tiếp qua POST /next-question (thuật toán Anti-3-Consecutive); kích hoạt câu hỏi follow-up đào sâu A2 khi điểm ranh giới $4.0 \le \text{Score} \le 8.0$; bỏ qua khi $<4.0$ hoặc $>8.0$. Chọn [Full-Session]: Progressive 3-10 câu Dễ -> Khó, KHÔNG có câu hỏi follow-up. Luyện tập không lưu audio (chỉ lưu transcript). | Admin System Config & Score 4.0-8.0 Gate |
| 12 | **Quy tắc Follow-up MF-02 & MF-04** | `MF-02: SV tự chọn; MF-04: 100% Trưởng BM cấu hình môn thi (1-5 câu, mặc định 2)` | MF-02: Sinh viên chủ động tự chọn Có/Không Follow-up trước khi thi thử; nếu có, AI hỏi chuyên sâu ngữ cảnh ([Needs Follow-up]) theo nội dung, KHÔNG theo điểm; thời lượng ca thi có follow-up dài hơn do Trưởng BM cấu hình; không lưu audio. MF-04: 100% quyền Trưởng Bộ Môn cấu hình Follow-up môn thi trong kỳ thi (`has_follow_up`, 1–5 câu, mặc định 2 câu); Cán bộ Khảo thí tuyệt đối KHÔNG có quyền cấu hình follow-up; Cán bộ Khảo thí chỉ quản lý tạo kỳ thi, ca thi (phòng lab ghi trên ca thi) và ExamInputMode. | Student Choice (MF-02) / Dept Head Config (MF-04) |
| 13 | **Instant Feedback Thi Thử (MF-02)** | `Scorecard chi tiết & History` | Sau khi nộp bài thi thử, AI trả về Scorecard chi tiết từng câu (Điểm Rubric, Strengths, Weaknesses, Suggestions) tại màn hình kết quả và lưu vĩnh viễn vào `mock_exam_sessions` phục vụ tra cứu Lịch sử thi. Nguồn đề rút từ kho `practice_questions`. Khi hết giờ tự động nộp bài (câu chưa làm tính là bỏ trống, không chấm). | `HTTP 200 OK` |
| 14 | **Phương thức làm bài Kiosk (MF-04)** | `ExamInputMode` | Cấu hình cấp môn trong kỳ thi gồm 2 phương thức: `VoiceOnly` (khóa cứng 100% phím Kiosk, chỉ dùng mic) và `VoiceWithTranscriptEdit` (mở đệm sửa từ theo `transcript_buffer_seconds` 10-300s, mặc định 60s do Admin cấu hình). Loại bỏ hoàn toàn `VoiceAndTextInput` khỏi scope hệ thống. | Kiosk Hardware Guard |
| 15 | **AI Chấm Ngầm, Publish Điểm & Phúc Khảo Nội Bộ (MF-04)** | Kiosk khóa an toàn, Publish, Internal Appeal | Nộp bài Kiosk khóa màn hình lưu an toàn < 100ms; AI chấm ngầm (đầu vào chỉ là transcript) chuyển Giám khảo hậu kiểm; Khảo thí Publish điểm khi 100% sinh viên có điểm, kích hoạt One-Way Lock; Xuất báo cáo điểm thi FPT cả 2 định dạng Excel (.xlsx) và PDF có chữ ký số; Sinh viên xem điểm Student Portal & nộp đơn phúc khảo nội bộ `AppealRequest` trực tiếp trên hệ thống; Quy trình phúc khảo 5 bước chuẩn mực: (1) SV nộp đơn -> (2) Cán bộ Khảo thí tiếp nhận & thẩm tra tính hợp lệ -> (3) Khảo thí chuyển tiếp Trưởng Bộ Môn -> (4) Trưởng Bộ Môn phân công Giảng viên vào chấm lại (`PUT /api/v1/appeals/{id}/assign-lecturer`) và Giảng viên nộp điểm tái thẩm định -> (5) Trưởng Bộ Môn phê duyệt quyết định điểm cuối cùng mở đường One-Way Lock cập nhật điểm. | `HTTP 200 OK` |
| 16 | **Chốt chặn AI Doubt Guard & Evidence Panel (MF-04)** | `conf < 0.70 OR is_suspicious` | Cổng Hậu kiểm Giám khảo cung cấp Evidence Panel (AudioURL, Transcript Whisper gốc, AI CoT) và tự động chia 2 nhóm: Nhóm 1 Đáng nghi ngờ (`conf < 0.70` hoặc `is_suspicious == true`), Giám khảo bắt buộc đối soát Evidence Panel, nghe Waveform Player và chỉnh điểm; Nhóm 2 Độ tin cậy cao, Giám khảo rà soát nhanh. | Doubt Guard Priority Queue |
| 17 | **Timeout Không Tương Tác MF-01 (Inactivity Timeout)** | `5 phút (SessionInactivityTimeoutMinutes = 5)` | Quản lý thời gian timeout không tương tác của phiên luyện tập MF-01 qua system_configs. Cập nhật last_activity_at sau mỗi tương tác (tạo phiên, nộp câu trả lời, lấy câu tiếp theo). Quá 5 phút không tương tác, hệ thống tự động kết thúc phiên an toàn (status = 'completed'), bảo toàn 100% điểm các câu đã làm (Full-Session tính các câu chưa làm là 0 điểm). Mọi request tiếp theo trả về HTTP 410 Gone (Session Timed Out). | `HTTP 410 Gone` |
| 18 | **Hình ảnh đính kèm câu hỏi thủ công & Chấm điểm Đa phương thức (Multimodal Image Support)** | `PNG/JPG/WEBP <= 5MB, Magic Bytes, Manual-Only, R2 questions/{yyyyMM}/` | Chỉ câu hỏi do Giảng viên/Trưởng BM tạo thủ công (`source == 'manual'`) mới được phép đính kèm hình ảnh/sơ đồ kỹ thuật (UML, ERD, Clean Architecture, UI Mockup) qua `POST /api/v1/questions/upload-image` hoặc `POST /api/v1/storage/upload-question-image`. Hệ thống kiểm tra nhị phân Magic Bytes (`PNG`, `JPG/JPEG`, `WEBP`), giới hạn $\le 5\text{MB}$, lưu vào `wwwroot/uploads/questions/{yyyyMM}/` và Cloudflare R2 (`questions/{yyyyMM}/`), ghi nhận `image_url` & `image_description` trên cả `exam_questions` và `practice_questions`. Khi chấm điểm (MF-01, MF-02, MF-04), `GeminiAiGradingService` tự động giải mã ảnh thành Base64 (`inline_data: { mime_type, data }`) kèm mô tả ngữ cảnh hình ảnh vào payload cùng transcript sinh viên để thực hiện suy luận đối chiếu thị giác (Multimodal CoT Grading). | `HTTP 200` / `413` / `415` |

### <a id="phan-13"></a>1.3. Ma Trận Ngăn Xếp Công Nghệ Đa Ngôn Ngữ (Polyglot Tech Stack)

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                      ENTERPRISE POLYGLOT TECH STACK                         │
├───────────────────┬─────────────────────────────────────────────────────────┤
│ Frontend Web App  │ React 19 (Vite) · TypeScript 5.5 Strict · Tailwind v4   │
│ Client State & UI │ Zustand · shadcn/ui · Lucide Icons · Canvas Wavesurfer  │
│ Backend Service   │ .NET 8 Web API · C# 12 · Clean Architecture 4 Tầng      │
│ Orchestration CQRS│ MediatR 12 · FluentValidation 11 · Polly 8 Resilience   │
│ Database & ORM    │ PostgreSQL 16 · Entity Framework Core 8 (Npgsql)        │
│ In-Memory Channel │ System.Threading.Channels (1,000 capacity BoundedQueue) │
│ Real-Time Engine  │ ASP.NET Core SignalR Core Hubs (/hubs/practice)         │
│ AI & LLM Cluster  │ Google Gemini 1.5 Flash (CoT 3 bước) & Gemini 1.5 Pro   │
│ Speech & Voice    │ Web Speech API (Client) · OpenAI Whisper Large-v3 (GPU) │
│ Object Storage    │ Cloudflare R2 (S3 API Compatible Zero-Egress Storage)   │
│ Security & Proxy  │ Nginx 1.25 Alpine · Cloudflare WAF/DDoS · OAuth PKCE    │
└───────────────────┴─────────────────────────────────────────────────────────┘
```

---

## <a id="phan-2"></a>📐 PHẦN 2: SƠ ĐỒ C4 COMPONENT & SYSTEM CONTEXT

### <a id="phan-21"></a>2.1. Sơ Đồ C4 System Context & Component (Mermaid)

Sơ đồ mô hình hóa toàn diện các tác nhân con người, ranh giới hệ thống, các khối thành phần cốt lõi và hệ thống dịch vụ ngoại vi:

```mermaid
flowchart TD
    subgraph Human_Actors ["Tác Nhân Người Dùng (Google OAuth 2.0 & Tài khoản Nhà trường cấp)"]
        Student["Sinh viên SE (ACT-01 / student)<br>• Luyện tập tự do (MF-01) & Thi thử bấm giờ (MF-02)<br>• Tham gia Lớp học bằng mật khẩu lớp & Thi trong lớp bằng OTP xoay vòng 5m (R6)<br>• Thi Kiosk phòng Lab (MF-04) & Nộp đơn phúc khảo cho Khảo thí (R7)"]
        Instructor["Giảng viên / Giám khảo (ACT-02 / lecturer)<br>• Nhận phân công dạy môn từ Trưởng BM (course_lecturers)<br>• Tạo Lớp học & phát sinh mật khẩu lớp (join_password_hash)<br>• Tạo Ca thi trong lớp (OTP xoay vòng 5m + grace 30-60s) & Hậu kiểm điểm thành phần<br>• Soạn đề: luồng luyện tập độc lập; luồng thi thật bám sát đề thi mẫu Trưởng BM ban hành<br>• Hậu kiểm bài thi Evidence Panel & Chấm lại phúc khảo khi Trưởng BM giao (MF-04)"]
        DeptHead["Trưởng Bộ Môn (ACT-04 / department_head)<br>• Quản lý môn học, CLOs (course_clos 3NF) & Phân công nhiều GV dạy môn (course_lecturers)<br>• Thẩm định duyệt đề OFFICIAL -> HANDED_OVER & Ban hành ma trận cấu trúc đề thi mẫu<br>• 100% thẩm quyền cấu hình Follow-up thi thật (has_follow_up, 1-5 câu)<br>• Tiếp nhận đơn phúc khảo từ Khảo thí chuyển sang, phân công GV chấm lại & Duyệt quyết định"]
        ExamOfficer["Cán bộ Khảo thí (ACT-05 / examination_officer)<br>• Tạo kỳ thi, ca/phòng thi & import DSSV<br>• Phát đề không trùng (uq_tickets_shift_exam_set) & Mở xuất bản đề ca thi<br>• Công bố điểm One-Way Lock & xuất báo cáo Excel/PDF ký số<br>• Tiếp nhận & thẩm tra đơn phúc khảo từ SV rồi chuyển giao Trưởng BM (R7)<br>• Giám sát & Kích hoạt replay thủ công Dead-Letter Queue (DLQ) cho thi thật MF-04 (R2)"]
        Proctor["Giám thị coi thi (ACT-06 / proctor)<br>• Điểm danh đối chiếu thẻ SV/CCCD (SeatNumber 1-40) & kiểm tra ip_address máy trạm<br>• Báo cáo vi phạm an ninh phòng thi vào exam_anti_cheat_logs<br>• (Cắt bỏ toàn bộ quyền mở ca, unseal IP, reset máy hay import danh sách)"]
        Admin["Quản trị viên Hệ thống (ACT-03 / admin)<br>• Quản trị người dùng, khóa/mở & gán role RBAC (Google OAuth & Tài khoản trường cấp)<br>• Quản lý chuỗi đào tạo: Tạo học kỳ (Semester) -> Tạo môn học (Course) -> Gán Trưởng BM (department_head_user_id)<br>• Cấu hình hệ thống MF-01 & Quota thi thử theo môn (max_mock_exams_per_day)<br>• Giám sát & Kích hoạt replay Dead-Letter Queue (DLQ) cho luồng MF-01 & MF-02"]
    end

    subgraph Presentation_Tier ["Tầng Trình Diễn (Presentation / Client Tier)"]
        WebPortal["Web Application Portal (React 19 Vite)<br>• Student Practice & Mock Exam UI<br>• Course CLO & Question Studio (MF-03)<br>• Examination Officer Console & Audit Portal<br>• Terminology Code-Switching Buffer (cấu hình động theo môn)<br>• Waveform Player 1.0x / 1.25x / 1.5x"]
        LabKiosk["Máy Trạm Kiosk Phòng Lab (Lab PC Client)<br>• Fullscreen Kiosk Lockdown (chặn F12, onblur 3 cấp)<br>• 30s Hardware Mic-Check Validation<br>• Ghi âm MediaRecorder WebM theo STT_MSSV.webm<br>• Băm SHA-256 & Upload R2 qua 2 API niêm phong"]
        ClientSpeech["Web Speech Engine (Client STT / TTS)<br>• STT tiếng Việt độ trễ cực thấp dưới 0.5s<br>• TTS đọc to câu hỏi và nhận xét"]
    end

    subgraph Core_Backend ["Tầng Dịch Vụ Cốt Lõi (.NET 8 Clean Architecture API)"]
        APIGateway["ASP.NET Core 8 Web API Gateway (Port 5000)<br>• REST Controllers (Auth, Courses, CourseClos, Questions, OfficialExams, Appeals, AntiCheat)<br>• RFC 7807 Global Exception Handler<br>• JWT Bearer & RBAC Authorization Filter (6 Roles)"]
        SignalRHub["SignalR Core Hubs (/hubs/practice, /hubs/anti-cheat)<br>• WebSocket 2 chiều đẩy Scorecard dưới 100ms<br>• Đồng bộ trạng thái phòng thi và anti-cheat realtime"]
        ValidationPipe["FluentValidation & Business Guards<br>• Client/Server Guard: Barem Rubric tổng = 10.0đ (HTTP 422)<br>• PostgreSQL Quota Guard: Hạn ngạch theo môn do Admin cấu hình (HTTP 429)<br>• Non-Duplicate Guard: uq_tickets_shift_exam_set"]
        BoundedQueue["Bounded Channel In-Memory Queue<br>• Hàng đợi 1,000 slots phân tách Ingestion & Worker<br>• Chế độ Backpressure: BoundedChannelFullMode.Wait"]
        BgWorker["Background Hosted Workers<br>• GradingQueueWorker: Rút task, gọi AI + Polly Retry<br>• DlqReplayWorker: Quét bài chấm bù định kỳ 5 phút<br>• Server Master Timer: Đo thời gian thi chống gian lận client"]
    end

    subgraph Persistence_Tier ["Tầng Lưu Trữ & Bền Vững (Data Persistence Tier)"]
        PostgresDB[("PostgreSQL 16 Relational DB (Port 5432)<br>• Lược đồ 38 bảng chuẩn hóa 3NF trong 7 Bounded Contexts<br>• Phân tách độc lập Luyện tập (Practice), Thi cử (Exam) & Ca thi tại lớp (In-Class)<br>• questions (source: 'manual' | 'ai_generated')<br>• course_clos (Chuẩn đầu ra CLO 3NF nội bộ) & course_lecturers (Phân công N-N)<br>• exam_anti_cheat_logs (Nhật ký vi phạm realtime)<br>• JSONB lưu trữ Barem Rubric 10.0 & Key Points<br>• OneWayLockInterceptor: Chặn sửa điểm DB khi is_locked=true (ngoại lệ phúc khảo nội bộ)")]
        DLQ_Store[("Bảng Dead-Letter Queue (DLQ)<br>• Cách ly an toàn bài thi lỗi sau 3 lần retry<br>• Phân định quản lý: Admin (MF-01/02) vs Khảo thí (MF-04)<br>• Bảo đảm Zero Data Loss 100%")]
    end

    subgraph External_Cloud ["Dịch Vụ Ngoại Vi & Điện Toán Đám Mây"]
        OAuth["Google Authentication (OAuth 2.0) & School Provisioned Auth<br>• Xác thực tài khoản Google hoặc Username/Password do Nhà trường cấp"]
        GeminiAI["Google API (Dịch vụ AI Đám Mây)<br>• Gemini 1.5 Flash: Chấm CoT & Tự động sinh câu hỏi theo CLOs môn học<br>• Gemini 1.5 Pro: Chấm Batch ca thi phòng Lab hậu kỳ"]
        WhisperSTT["Whisper Large-v3 STT Engine<br>• Bóc băng Server-side kèm timestamps phát ngôn"]
        Cloudflare_R2["Cloudflare R2 Object Storage<br>• Lưu trữ Audio STT_MSSV.webm (Zero-Egress S3)<br>• Niêm phong toàn vẹn bằng mã băm SHA-256 qua 2 API"]
        FPT_Exam_System["Hệ Thống Khảo Thí FPT (Excel & PDF Interchange)<br>• Danh sách thí sinh từ lớp học hệ thống (hoặc dự phòng Excel)<br>• Export bảng điểm khảo thí cả định dạng Excel (.xlsx) và PDF có chữ ký số"]
    end

    Student -->|Truy cập tự luyện & thi thử| WebPortal
    Student -->|Ngồi đúng STT máy thi vấn đáp| LabKiosk
    Instructor -->|Soạn đề, cắm cờ trùng & Hậu kiểm| WebPortal
    DeptHead -->|Thẩm định duyệt đề OFFICIAL, Cấu hình Follow-up thi thật & Phân công GV| WebPortal
    ExamOfficer -->|Tạo ca thi, phát đề, Tiếp nhận phúc khảo, Giám sát DLQ thi thật & Công bố điểm| WebPortal
    Proctor -->|Điểm danh STT 1-40 đối chiếu thẻ/CCCD & Báo cáo vi phạm an ninh| WebPortal
    Admin -->|Quản trị hệ thống, Chuỗi học thuật Semester->Course->DeptHead & Giám sát DLQ MF-01/02| WebPortal

    WebPortal <-->|Nhận diện giọng nói & Đọc đề| ClientSpeech
    LabKiosk <-->|Phần cứng tai nghe mic & TTS| ClientSpeech
    WebPortal -->|HTTPS REST JSON & WSS| APIGateway
    WebPortal <-->|Real-time Scorecard Push| SignalRHub
    LabKiosk -->|Gửi Audio Hash SHA-256 & Nộp bài < 100ms| APIGateway
    LabKiosk -->|Stream trực tiếp file audio STT_MSSV.webm| Cloudflare_R2

    APIGateway -->|Xác thực Token Google / Trường cấp| OAuth
    APIGateway --> ValidationPipe
    ValidationPipe -->|Ghi đĩa tức thời Persist-First dưới 100ms| PostgresDB
    ValidationPipe -->|Đẩy task chấm điểm non-blocking| BoundedQueue
    BoundedQueue -->|Rút task tuần tự| BgWorker
    BgWorker -->|Gọi chấm Rubric CoT JSON Schema| GeminiAI
    BgWorker -->|Kéo audio ca thi bóc băng timestamps| WhisperSTT
    BgWorker -->|Đối chiếu file audio & kiểm tra SHA-256| Cloudflare_R2
    BgWorker -->|Cập nhật trạng thái GRADED / Lưu điểm| PostgresDB
    BgWorker -->|Cách ly bài lỗi sau 3 lần thất bại| DLQ_Store
    BgWorker -->|Bắn sự kiện hoàn tất chấm| SignalRHub
    APIGateway --> ValidationPipe
    ValidationPipe -->|Ghi đĩa tức thời Persist-First dưới 100ms| PostgresDB
    ValidationPipe -->|Đẩy task chấm điểm non-blocking| BoundedQueue
    BoundedQueue -->|Rút task tuần tự| BgWorker
    BgWorker -->|Gọi chấm Rubric CoT JSON Schema| GeminiAI
    BgWorker -->|Kéo audio ca thi bóc băng timestamps| WhisperSTT
    BgWorker -->|Đối chiếu file audio & kiểm tra SHA-256| Cloudflare_R2
    BgWorker -->|Cập nhật trạng thái GRADED / Lưu điểm| PostgresDB
    BgWorker -->|Cách ly bài lỗi sau 3 lần thất bại| DLQ_Store
    BgWorker -->|Bắn sự kiện hoàn tất chấm| SignalRHub
    APIGateway -->|Import sinh viên & Export bảng điểm khảo thí| FPT_Exam_System
```

### <a id="phan-22"></a>2.2. Phân Tích Ranh Giới Kỹ Thuật & Nguyên Tắc Zero-Trust
1. **Nguyên tắc Xác thực Kép Zero-Trust & Zero Guest Access:** Hệ thống không chấp nhận người dùng nặc danh. Người dùng xác thực qua **Google OAuth 2.0** (`POST /api/v1/auth/google-login`) hoặc bằng **Tài khoản `username` & `password` do Nhà trường cấp sẵn** (`POST /api/v1/auth/password-login`, hỗ trợ đổi mật khẩu qua `POST /api/v1/auth/change-password`). Không mở cổng tự đăng ký bằng username/password công khai; vai trò (`role`) luôn được đọc trực tiếp từ cơ sở dữ liệu.
2. **Phân tách Rạch Ròi Ingestion & Processing:** Tầng Web API chỉ đóng vai trò tiếp nhận, kiểm tra dữ liệu đầu vào (Validation), ghi nhận đĩa tức thì (Persist-First < 100ms) và trả về `HTTP 202 Accepted`. Mọi tác vụ nặng (gọi LLM, bóc băng giọng nói) đều được đẩy sang tiến trình nền thông qua hàng đợi `BoundedChannel`, giải phóng hoàn toàn các luồng xử lý của Web Server.
3. **Phân Vùng Bền Vững Đa Cấp:** Dữ liệu có cấu trúc lưu trữ tại PostgreSQL 16 với ràng buộc toàn vẹn khóa ngoại 3NF; dữ liệu file âm thanh dung lượng lớn được phân luồng trực tiếp từ Client lên Cloudflare R2 qua Presigned URL, hoàn toàn không đi qua máy chủ Backend để tiết kiệm băng thông CPU/RAM.

---

## <a id="phan-3"></a>🧱 PHẦN 3: SƠ ĐỒ KIẾN TRÚC HỆ THỐNG TỔNG THỂ THEO CHIỀU DỌC (TOP-TO-BOTTOM FLOWCHART TD)

### <a id="phan-31"></a>3.1. Sơ Đồ Kiến Trúc Hệ Thống 5 Tầng Chiều Dọc (Mermaid)

Sơ đồ được thiết kế nghiêm ngặt theo chiều dọc (`flowchart TD`), thể hiện mạch lạc hành trình dữ liệu từ ngoài vào trong, loại bỏ hoàn toàn đan chéo rối dây:

```mermaid
flowchart TD
    %% =========================================================================
    %% TẦNG 1: TẦNG CLIENT & PHÒNG LAB (Presentation Tier - React 19 Vite - Port 3000)
    %% =========================================================================
    subgraph T1_Client ["1. TẦNG CLIENT & PHÒNG LAB (Presentation Tier - React 19 Vite - Port 3000)"]
        direction TB
        subgraph C_Actors ["Tác Nhân Người Dùng (Google OAuth 2.0 & Tài khoản Trường cấp)"]
            direction LR
            Actor_SV["Sinh viên SE (ACT-01 / student)<br>• MF-01: Luyện tập tự do (On-Demand & Progressive)<br>• MF-02: Thi thử tính giờ (Quota theo môn)<br>• Tham gia Lớp học bằng mật khẩu lớp & Thi trong lớp bằng OTP xoay vòng 5m (R6)<br>• MF-04: Thi vấn đáp phòng Lab Kiosk & Nộp đơn phúc khảo cho Khảo thí (R7)"]
            Actor_GV["Giảng viên (ACT-02 / lecturer)<br>• Nhận phân công dạy môn từ Trưởng BM (course_lecturers)<br>• Tạo Lớp học & phát sinh mật khẩu lớp (join_password_hash)<br>• Tạo Ca thi trong lớp (OTP xoay vòng 5m + grace 30-60s) & Hậu kiểm điểm thành phần<br>• MF-03: Soạn đề: luồng luyện tập độc lập; luồng thi thật bám sát đề thi mẫu Trưởng BM<br>• MF-04: Hậu kiểm Evidence Panel & Chấm lại phúc khảo khi được phân công"]
            Actor_DeptHead["Trưởng Bộ Môn (ACT-04 / department_head)<br>• Quản lý môn học, CLOs & Phân công nhiều GV dạy môn (course_lecturers)<br>• 100% thẩm quyền cấu hình Follow-up thi thật (has_follow_up, 1-5 câu)<br>• MF-03: Thẩm định duyệt đề OFFICIAL -> HANDED_OVER & Ban hành ma trận cấu trúc đề mẫu<br>• MF-04: Tiếp nhận đơn phúc khảo từ Khảo thí chuyển sang, phân công GV chấm lại & Duyệt quyết định"]
            Actor_ExamOfficer["Cán bộ Khảo thí (ACT-05 / examination_officer)<br>• MF-04: Tiếp nhận đề HANDED_OVER, lập kỳ thi, ca thi & import DSSV<br>• MF-04: Phân bổ đề không trùng (uq_tickets_shift_exam_set) & Mở xuất bản đề ca thi<br>• MF-04: Công bố điểm (PublishGrades) One-Way Lock & xuất báo cáo Excel/PDF<br>• MF-04: Tiếp nhận & thẩm tra đơn phúc khảo từ SV rồi chuyển giao Trưởng BM (R7)<br>• MF-04: Giám sát & Kích hoạt replay thủ công Dead-Letter Queue (DLQ) cho thi thật (R2)"]
            Actor_Proctor["Cán bộ Coi thi / Giám thị (ACT-06 / proctor)<br>• MF-04: Điểm danh thí sinh theo STT ca thi (SeatNumber 1-40) & đối chiếu CCCD/Thẻ SV<br>• MF-04: Báo cáo vi phạm an ninh phòng thi vào exam_anti_cheat_logs<br>• (Cắt bỏ toàn bộ quyền mở ca, unseal IP, reset máy hay import danh sách)"]
            Actor_Admin["Quản trị viên (ACT-03 / admin)<br>• Quản trị người dùng (Google & Tài khoản trường cấp), phân quyền 6 roles<br>• Quản lý chuỗi đào tạo: Tạo học kỳ (Semester) -> Tạo môn học (Course) -> Gán Trưởng BM (department_head_user_id)<br>• Cấu hình hệ thống MF-01 & Quota thi thử theo môn MF-02<br>• Giám sát & Kích hoạt replay Dead-Letter Queue (DLQ) cho luồng MF-01 & MF-02"]
        end

        subgraph C_Components ["Các Phân Hệ Giao Diện & Thiết Bị Client"]
            direction LR
            FE_Student["Web App Sinh Viên & Giảng Viên<br>• Web Speech STT (< 0.5s) & TTS đọc đề<br>• Màn hình đệm hiệu đính Code-Switching (cấu hình động transcript_buffer_seconds 10-300s)<br>• Instant Feedback Scorecard theo Rubric (MF-02)<br>• Question & Rubric Studio (CHECK Sum=10.0đ)<br>• Evidence Panel & Waveform Player thẩm định 1.0x, 1.25x, 1.5x"]
            FE_KioskLockdown["Máy Trạm Kiosk Phòng Lab (Lab PC Client)<br>• Định danh: STT máy = Số danh sách thi<br>• Fullscreen Lockdown: Chặn Alt+Tab, F12, onblur 3 cấp<br>• Hardware Mic-Check 30s kiểm tra tai nghe & mic<br>• Cấu hình ExamInputMode theo môn (VoiceOnly khóa phím)<br>• Thu âm WebM nén định danh: STT_MSSV.webm & Băm SHA-256<br>• Kiosk Khóa An Toàn & AI Chấm Ngầm (Background Worker)"]
        end
    end

    %% =========================================================================
    %% TẦNG 2: CỔNG KẾT NỐI, ĐỊNH TUYẾN & BẢO MẬT (Ingress, Gateway & Auth Tier)
    %% =========================================================================
    subgraph T2_Gateway ["2. CỔNG KẾT NỐI, ĐỊNH TUYẾN & BẢO MẬT (Ingress, Gateway & Auth Tier)"]
        direction TB
        Edge_Cloudflare["Cloudflare Edge Network (Global Anycast)<br>• DDoS Protection Layer 3 / 4 / 7 | WAF OWASP Top 10 | Quản lý DNS & SSL/TLS 1.3"]

        subgraph GW_Sub ["Định Tuyến, Xác Thực & Kết Nối Thời Gian Thực"]
            direction LR
            GW_Nginx["Nginx Reverse Proxy & Ingress<br>• Port 80: HTTP 301 Redirect sang HTTPS 443<br>• SSL Offloading & HSTS Security Headers<br>• Rate Limiting: 100 req/s/IP chống DoS & Spam<br>• Phân luồng: / -> :3000, /api/v1/* -> :5000"]
            GW_Auth["Cơ Chế Xác Thực Định Danh Kép (Dual Auth)<br>• Google OAuth 2.0 & Tài khoản Username/Password do Nhà trường cấp<br>• Zero Guest Access (Chặn truy cập nặc danh)<br>• JWT Bearer Token kèm Claims phân quyền RBAC"]
            GW_SignalR["SignalR Core Real-Time Hubs (WSS)<br>• /hubs/practice: Đẩy Scorecard tức thời (< 100ms)<br>• /hubs/exam-proctor: Đồng bộ lệnh ca thi Lab<br>• WebSocket Upgrade tự động từ Port 5000"]
        end
    end

    %% =========================================================================
    %% TẦNG 3: LÕI BACKEND (.NET 8 CLEAN ARCHITECTURE) - Port 5000
    %% =========================================================================
    subgraph T3_Backend ["3. LÕI BACKEND (.NET 8 CLEAN ARCHITECTURE) - Port 5000"]
        direction TB
        subgraph BE_API ["API Tier: Controllers & Middleware"]
            direction LR
            API_Controllers["RESTful API Controllers (/api/v1/*)<br>• AuthController: Đổi mã Auth Code PKCE lấy JWT<br>• PracticeController: Nộp bài, trả ngay HTTP 202 Accepted<br>• MockExamController: PostgreSQL Quota Guard (HTTP 429)<br>• ExamLabController: Nộp STT_MSSV.webm & mã băm SHA-256<br>• ExamAuditController: Sửa điểm giải trình & One-Way Lock<br>• AdminController: Quản trị User, Semester, Course & Cấu hình<br>• AcademicController: Quản lý phân công GV & Lớp học (R5)<br>• InClassShiftController: Ca thi trong lớp & Mật khẩu xoay vòng 5m (R6)<br>• AppealsController: Quy trình phúc khảo qua Khảo thí (R7)<br>• DlqController: Giám sát DLQ & Replay (Admin cho MF-01/02, Khảo thí cho MF-04)"]
            API_Middleware["Middleware Pipeline & Global Handlers<br>• RFC 7807 Global Exception Handler: ProblemDetails<br>• Server Master Timer: Đồng hồ ca thi, Grace Period 10s<br>• JwtBearerAuthMiddleware & RBAC Authorization Filter"]
        end

        subgraph BE_AppCQRS ["Application Tier: MediatR CQRS & Business Services"]
            direction LR
            APP_MediatR["MediatR CQRS Pipeline Behaviors<br>• ValidationBehavior: FluentValidation ∑ Barem == 10.0đ (HTTP 422)<br>• LoggingBehavior & PerformanceTrackingBehavior (> 500ms)<br>• Commands: SubmitAnswerCommand, StartMockExam, LockGrade<br>• Queries: GetScorecardQuery, GetBloomAnalytics, GetAuditSession"]
            APP_Services["Core Application Business Services<br>• PostgreSqlQuotaService: Đếm nguyên tử hạn ngạch ngày theo môn<br>• ExamGeneratorService: Bốc đề theo cấu trúc Bloom do Trưởng BM cấu hình<br>• AudioVerificationService: Kiểm tra WebM & SHA-256"]
        end

        subgraph BE_Resilience ["Cơ Chế Chịu Tải RAM & Background Workers (Zero Data Loss)"]
            direction LR
            RAM_Queue["System.Threading.Channels Bounded Channel<br>• Dung lượng hàng đợi: 1,000 slots trong RAM<br>• Cơ chế Backpressure: BoundedChannelFullMode.Wait<br>• Phân tách Ingestion & Worker chống tràn bộ nhớ"]
            Worker_Grading["GradingQueueWorker (IHostedService Background Worker)<br>• Rút task tuần tự, điều phối Whisper STT & Gemini AI<br>• Polly Retry Policy: Exponential Backoff 2s -> 4s -> 8s (Max 3)<br>• Cập nhật điểm & thông báo hoàn tất qua SignalR"]
            Worker_DLQ["DlqReplayWorker (Background Recovery Worker)<br>• Định kỳ 5 phút quét bảng dead_letter_queues<br>• Tự động kích hoạt chấm bù, cách ly bài hỏng nặng"]
        end
    end

    %% =========================================================================
    %% TẦNG 4: TẦNG DỮ LIỆU & LƯU TRỮ (Data Persistence & Cloud Storage Tier)
    %% =========================================================================
    subgraph T4_Persistence ["4. TẦNG DỮ LIỆU & LƯU TRỮ (Persistence & Storage Tier)"]
        direction TB
        subgraph DB_PostgreSQL ["PostgreSQL 16 Relational DB (Port 5432 Internal) - 38 Bảng Chuẩn 3NF (7 Bounded Contexts)"]
            direction TB
            DB_30Core["38 Bảng Thực Thể Chuẩn Hóa (7 Bounded Contexts + Hạ tầng Chịu lỗi):<br>• [Ctx 1] Identity & RBAC (2): users, refresh_tokens<br>• [Ctx 2] Academic & Cohorts (6): semesters, courses, course_clos, course_lecturers, classes, class_enrollments<br>• [Ctx 3] Rubric & Assessment (2): rubrics, rubric_criteria (CHECK = 10.00)<br>• [Ctx 4] Practice Module (MF-01 - 5): practice_questions, practice_sessions, practice_answers, ai_evaluations, ai_evaluation_details<br>• [Ctx 5] Mock Exam Module (MF-02 - 6): exam_structures, exam_sets, exam_set_questions, mock_exam_quotas, mock_exam_sessions, mock_exam_answers<br>• [Ctx 6] Official Lab Exam, Audit & Appeals (MF-04 - 9): exam_questions, official_exam_sessions, real_exam_session_shifts, student_exam_tickets, exam_question_submissions, exam_anti_cheat_logs, lecturer_audits, lecturer_audit_details, appeal_requests<br>• [Ctx 7] In-Class Exam Shifts & TOTP (MF-05 - 4): in_class_exam_shifts, in_class_shift_passwords, in_class_shift_participants, in_class_exam_submissions<br>• [Resilience, Jobs & Config (4)]: dead_letter_queues, grading_jobs, audit_logs, system_configs"]
            DB_Interceptors["EF Core Enterprise Database Interceptors:<br>• OneWayLockInterceptor: Chặn UPDATE/DELETE khi is_locked=true (Ném HTTP 403 Forbidden bất biến; mở ngoại lệ cho Giảng viên chấm lại đơn phúc khảo)<br>• AuditTrailInterceptor: Bắt buộc override_reason khi sửa điểm, ghi vết toàn bộ vào audit_logs"]
        end

        subgraph Storage_R2 ["Cloudflare R2 Object Storage (S3-Compatible Zero-Egress)"]
            R2_AudioStore["Bucket: oralexam-lab-audio<br>• Lưu trữ audio thi thật: STT_MSSV.webm (ví dụ: 01_SE170123.webm)<br>• Niêm phong toàn vẹn pháp lý bằng mã băm cryptographic SHA-256<br>• Cấp quyền thẩm định Waveform Player qua Presigned URL thời hạn 15 phút<br>• Chính sách Zero-Egress Fees: Tiết kiệm 100% chi phí băng thông nghe lại audio"]
        end
    end

    %% =========================================================================
    %% TẦNG 5: CỤM AI & VOICE SERVICES (External AI & Machine Learning Cluster)
    %% =========================================================================
    subgraph T5_AI ["5. CỤM AI & VOICE SERVICES (External AI & Machine Learning Cluster)"]
        direction TB
        subgraph AI_Engines ["Các Mô Hình Trí Tuệ Nhân Tạo & Bóc Băng Chuyên Trách"]
            direction LR
            AI_Whisper["Cloudflare Whisper Large-v3 STT Engine<br>• GPU Node bóc băng bài thi phòng Lab MF-04<br>• Nhận diện chuẩn xác tiếng Việt & thuật ngữ tiếng Anh<br>• Xuất timestamps_json chi tiết từng câu nói"]
            AI_GeminiFlash["Google Gemini 1.5 Flash API (1.8s - 2.5s)<br>• Phục vụ Luyện tập MF-01, Thi thử MF-02 & Simulator MF-03<br>• Chain-of-Thought (CoT) 3 bước xuất Structured JSON<br>• Chấm chi tiết tiêu chí Barem C1..Ck & tổng điểm 10.0đ"]
            AI_GeminiPro["Google Gemini 1.5 Pro API (Suy Luận Sâu)<br>• Chấm Batch ca thi phòng Lab MF-04 (hậu kỳ 1-2h sau ca thi)<br>• Phân tích sâu Barem 10.0đ & gắn Timestamp Citations"]
        end

        AI_FollowUpRules["Bộ Quy Tắc Kích Hoạt Follow-Up Phân Tầng<br>• MF-01 Luyện tập: Admin cấu hình (1-5 câu, mặc định 2); Chỉ ở [Per-Question] khi 4.0 <= Score <= 8.0; [Full-Session] không follow-up<br>• MF-02 Thi thử: SV tự chọn Có/Không; Trưởng BM cấu hình (1-5 câu, mặc định 2); AI hỏi theo nội dung ([Needs Follow-up])<br>• MF-04 Thi thật: Trưởng BM cấu hình môn trong kỳ thi (1-5 câu, mặc định 2); AI hỏi theo nội dung"]
    end

    %% =========================================================================
    %% LUỒNG KẾT NỐI XUYÊN TẦNG THEO CHIỀU DỌC THỐNG NHẤT (TOP-TO-BOTTOM)
    %% =========================================================================
    Actor_SV -->|Truy cập tự luyện & thi thử| FE_Student
    Actor_SV -->|Ngồi đúng STT máy thi Lab| FE_KioskLockdown
    Actor_GV -->|Soạn đề, chạy Simulator & Hậu kiểm| FE_Student
    Actor_DeptHead -->|Duyệt đề, cấu hình ca thi & Tiếp nhận phúc khảo| FE_Student
    Actor_Admin -->|Giám sát hệ thống & Audit Logs| FE_Student

    FE_Student -->|HTTPS REST & WSS Traffic| Edge_Cloudflare
    FE_KioskLockdown -->|HTTPS Kiosk Mode Traffic| Edge_Cloudflare
    FE_KioskLockdown -.->|Upload trực tiếp STT_MSSV.webm qua Presigned URL| Storage_R2

    Edge_Cloudflare -->|Proxy mã hóa TLS 1.3| GW_Nginx
    GW_Nginx -->|Xác thực OAuth 2.0 PKCE| GW_Auth
    GW_Nginx -->|Nâng cấp kết nối WSS| GW_SignalR
    GW_Nginx -->|Định tuyến REST API /api/v1/*| API_Controllers

    API_Controllers --> API_Middleware
    API_Middleware --> APP_MediatR
    APP_MediatR --> APP_Services

    %% Ghi đĩa Persist-First & Hàng đợi RAM
    APP_MediatR -->|1. Persist-First: Ghi bản ghi PENDING < 100ms| DB_PostgreSQL
    APP_MediatR -->|2. Đẩy task chấm điểm non-blocking| RAM_Queue
    RAM_Queue -->|3. Rút task tuần tự an toàn| Worker_Grading
    Worker_DLQ -.->|Quét bản ghi lỗi định kỳ 5 phút| DB_PostgreSQL
    Worker_DLQ -.->|Tự động đẩy lại task cần chấm bù| RAM_Queue

    %% Kết nối từ Worker tới Dữ liệu & AI
    Worker_Grading -->|Lấy file audio & kiểm tra SHA-256 seal| Storage_R2
    Worker_Grading -->|Gửi audio bóc băng kèm timestamps| AI_Whisper
    Worker_Grading -->|Chấm Barem 10.0đ CoT cho MF-01, MF-02, MF-03| AI_GeminiFlash
    Worker_Grading -->|Chấm Batch hậu kỳ phòng Lab MF-04| AI_GeminiPro
    AI_GeminiFlash --- AI_FollowUpRules
    AI_GeminiPro --- AI_FollowUpRules

    %% Lưu kết quả & Xử lý DLQ
    Worker_Grading -->|Cập nhật trạng thái GRADED & Điểm số| DB_PostgreSQL
    Worker_Grading -->|Cách ly bài lỗi quá 3 lần vào dead_letter_queues| DB_PostgreSQL
```

### <a id="phan-32"></a>3.2. Phân Tích Kỹ Thuật Chuyên Sâu 5 Tầng Hệ Thống

#### 3.2.1. Tầng 1: Tầng Client & Phòng Lab (Presentation & Lab Kiosk Tier)
Tầng trình diễn được xây dựng trên nền tảng **React 19 (Vite)** kết hợp **Tailwind CSS v4** và **shadcn/ui**, hoạt động tại cổng nội bộ `3000`. Tầng này đảm nhận 3 vai trò tác chiến riêng biệt:
1. **Phân hệ Sinh viên (Student Experience):**
   - **Xử lý âm thanh độ trễ thấp (< 0.5s):** Sử dụng Web Speech API native trên trình duyệt để nhận diện giọng nói tiếng Việt và đọc câu hỏi (TTS), giảm thiểu tối đa chi phí hạ tầng máy chủ khi luyện tập. Luyện tập không lưu audio (chỉ lưu transcript đã sửa).
   - **Màn hình đệm hiệu đính Code-Switching (Cấu hình động bởi Admin: `transcript_buffer_seconds`, 10-300s, mặc định 60s):** Sinh viên ngành SE thường xuyên phát âm đan xen thuật ngữ kỹ thuật tiếng Anh (ví dụ: *Interface, Polymorphism, Dependency Injection, Asynchronous*). Sau khi phát biểu, văn bản nháp hiển thị trong khoảng thời gian đệm đếm ngược (mặc định 60 giây do Admin cấu hình) để sinh viên chủ động sửa lỗi chính tả trước khi gửi chấm chính thức.
   - **Instant Feedback Scorecard chi tiết theo Rubric (MF-02):** Sau khi hoàn thành bài thi thử, sinh viên được nhận Scorecard chi tiết từng câu: phân tích Điểm mạnh (Strengths), Thiếu sót kiến thức (Weaknesses), Gợi ý cải thiện hành động (Suggestions) và lưu vào Lịch sử thi. Nguồn đề rút ngẫu nhiên từ kho câu hỏi luyện tập chung (`practice_questions`). Thi thử không lưu audio (chỉ lưu transcript).
2. **Phân hệ Trưởng Bộ Môn & Giảng viên (Course CLO & Question Studio, ExamSets, Audit & Appeals):**
   - **Course CLOs & Question Studio (MF-03):** Không gian tác chiến dành cho Giảng viên (`lecturer`) và Trưởng Bộ Môn (`department_head`). Chuẩn hóa cây chuẩn đầu ra môn học trực tiếp trong CSDL qua bảng `course_clos` (3NF), loại bỏ hoàn toàn phụ thuộc vào hệ thống bên ngoài FLM. Giảng viên cấu hình mục tiêu Bloom 1-6 & độ khó, xem trước (Draft Preview Modal side-by-side) câu hỏi vấn đáp kèm Model Answer $\ge 50$ ký tự & Barem Rubric $\sum \equiv 10.0$đ. AI Gemini sinh câu hỏi, rubric và sample answer từ CLO môn học (`POST /api/v1/questions/generate-from-clo`) hoặc Giảng viên soạn thủ công (`source == 'manual'`, hỗ trợ đính kèm ảnh/sơ đồ kỹ thuật PNG/JPG/WEBP $\le 5$MB qua `POST /api/v1/questions/upload-image`, kiểm tra Magic Bytes HTTP 413/415, phục vụ chấm Multimodal CoT).
   - **Kiểm soát Trùng lặp & Phê duyệt Câu hỏi:** Hệ thống tự động so khớp độ tương đồng ngữ nghĩa; nếu phát hiện câu hỏi tương đồng vượt ngưỡng, hệ thống tự động gắn cờ cảnh báo `isFlagged = true` để Trưởng Bộ Môn rà soát kỹ lưỡng. Giảng viên tick chọn kho (`practice_questions` và/hoặc `exam_questions`) rồi gửi Bộ Môn thẩm định (`POST /api/v1/questions/batch-submit-review`) $\to$ `SUBMITTED_FOR_REVIEW`. Trưởng Bộ Môn là chốt chặn phê duyệt duy nhất (`APPROVED`, `NEEDS_REVISION`, `REJECTED`), hoặc trực tiếp tạo và duyệt câu hỏi.
   - **ExamStructure Blueprint & Đóng gói Đề thi (ExamSets):** Trưởng Bộ Môn định nghĩa khung cấu trúc đề thi `exam_structures` với ma trận chi tiết `slot_definitions` (JSONB: quy định số lượng câu theo từng CLO, mức Bloom, độ khó) và `required_set_count` (số lượng đề cần sinh). Hệ thống hỗ trợ biên soạn và đóng gói các bộ đề thi `exam_sets` theo vòng đời chuẩn: `DRAFT` $\to$ `SUBMITTED_FOR_REVIEW` $\to$ `OFFICIAL` $\to$ `HANDED_OVER`. Khi Trưởng Bộ Môn duyệt đề thành `OFFICIAL`, toàn bộ câu hỏi thành phần được tự động khóa sang `APPROVED`. Khi hoàn tất, Trưởng Bộ Môn bàn giao gói đề chính thức cho Cán bộ Khảo thí (`HANDED_OVER`).
   - **Question & Rubric Studio (MF-03):** Thiết kế cấu trúc câu hỏi và phân rã Barem Rubric thành danh sách tiêu chí $C_1, C_2, \dots, C_k$ với ràng buộc tổng trọng số $\sum \text{Tiêu chí} \equiv 10.0\text{đ}$ (sai lệch chặn HTTP 422).
   - **AI Simulator Modal:** Cho phép cán bộ chuyên môn nhập câu trả lời giả định để kiểm tra độ nhạy và tính nghiêm ngặt của barem trước khi phát hành đề.
   - **Lecturer Audit Portal & Evidence Panel (MF-04):** Màn hình hậu kiểm song song dành cho Giảng viên chấm thi (`examiner`), cung cấp **Evidence Panel** (AudioURL Cloudflare R2, Transcript Whisper gốc, và chuỗi suy luận AI Chain-of-Thought), hiển thị file ghi âm trên **Waveform Player** hỗ trợ tua tốc độ (1.0x, 1.25x, 1.5x) và nhấp chuột trực tiếp vào mốc thời gian (timestamp) để nghe đúng đoạn phát ngôn nghi vấn.
   - **Phân hệ Thẩm định Phúc khảo Nội bộ (Department Head Appeals - MF-04):** Trưởng Bộ Môn tiếp nhận danh sách `AppealRequest` từ sinh viên trên Student Portal và phân công một Giảng viên vào chấm lại (`PUT /api/v1/appeals/{id}/assign-lecturer`). Giảng viên được phân công rà soát Evidence Panel và file ghi âm trên Waveform Player để chấm lại và nộp kết quả kèm giải trình; Trưởng Bộ Môn xem xét phê duyệt (`APPROVED`) cập nhật điểm chính thức hoặc bác bỏ (`REJECTED`) giữ nguyên điểm.

3. **Phân hệ Cán bộ Khảo thí & Cán bộ Coi thi (Examination Officer & Proctor Workflows - MF-04):**
   - **Quản lý Kỳ thi & Ca thi (Cán bộ Khảo thí):** Cán bộ Khảo thí (`examination_officer`) tiếp nhận các gói đề thi ở trạng thái `HANDED_OVER`, tạo kỳ thi chính thức (`OfficialExamSession`), khởi tạo các ca thi (`RealExamSessionShift`: phòng lab, kíp thi, phân công giám thị `proctor_id`, giảng viên thẩm định `examiner_user_id`).
   - **Phân bổ Đề thi Không Trùng Lặp (Ticket Distribution):** Khảo thí thực hiện thuật toán phân bổ đề thi cho thí sinh trong ca thi sao cho các máy liền kề hoặc cùng ca không nhận trùng đề thi, bảo đảm toàn vẹn qua chỉ mục duy nhất `uq_tickets_shift_exam_set` (`shift_id`, `exam_set_id`).
   - **Xuất bản Đề ca thi & Công bố Điểm (Publish Exam & Publish Grades):** Trước giờ thi, Khảo thí phát lệnh xuất bản đề ca thi (`is_exam_published = true` qua `POST /api/v1/official-exams/shifts/{id}/publish-exam`). Sau khi toàn bộ bài thi đã được AI chấm và Giảng viên thẩm định đầy đủ 100%, Khảo thí / Giảng viên thực hiện **"Công Bố Điểm"** (`POST /api/v1/official-exams/shifts/{shiftId}/publish-grades`), kích hoạt **One-Way Lock (`is_locked = true`)**, chặn 100% thao tác sửa/xóa trái phép (`HTTP 403 Forbidden`). Xuất báo cáo điểm thi FPT cả 2 định dạng Excel (.xlsx) và PDF có chữ ký số.
   - **Điểm danh & Giám sát Phòng thi (Cán bộ Coi thi / Giám thị):** Cán bộ Coi thi (`proctor`) CHỈ có quyền điểm danh thí sinh theo STT ca thi (SeatNumber 1–40), đối chiếu Thẻ sinh viên / CCCD và ghi nhận báo cáo các vi phạm an ninh phòng thi trực tiếp vào `exam_anti_cheat_logs` (MF-04). Proctor tuyệt đối CẤM mở ca thi, CẤM mở khóa/xuất bản đề thi, CẤM đóng ca thi, CẤM reset máy trạm hay can thiệp điểm số. Thao tác mở xuất bản đề và mở ca thi thuộc 100% thẩm quyền của Cán bộ Khảo thí.

4. **Máy trạm Kiosk Phòng Lab (Lab PC Lockdown - MF-04):**
   - **Định danh bất biến:** `STT Máy Trạm = Số Thứ Tự Danh Sách Thi (SeatNumber 1-40)`. Thí sinh ngồi vào máy số nào sẽ tự động nạp hồ sơ thi của STT đó qua cột `ip_address` (cấm `workstation_ip`), triệt tiêu rủi ro nhầm lẫn phòng thi.
   - **Fullscreen Lockdown 3 cấp độ:** Kích hoạt chế độ toàn màn hình bắt buộc, chặn đứng các phím tắt hệ thống: `Alt + Tab`, `F12` (DevTools), `Ctrl + C / Ctrl + V`, chuột phải (`contextmenu`). Bắt sự kiện `window.onblur` để cảnh báo và khóa bài thi nếu cố tình chuyển cửa sổ quá 3 lần.
   - **Nhật ký Chống Gian lận Thời gian thực (Real-time Anti-Cheat Logging):** Toàn bộ sự kiện bất thường (Window blur, phím tắt bị chặn, cố mở DevTools, màn hình phụ, âm thanh lạ) được gửi tức thì về máy chủ qua `POST /api/v1/official-exams/anti-cheat/log` và lưu vào bảng `exam_anti_cheat_logs` với 4 mức độ nghiêm trọng (`LOW`, `MEDIUM`, `HIGH`, `CRITICAL`) phục vụ giám sát và hậu kiểm.
   - **Hardware Mic-Check 30s:** Trước khi bắt đầu thi, phần mềm bắt buộc sinh viên thử giọng trong 30 giây để đo âm lượng (Decibel Level $\ge 60$dB), bảo đảm micro tai nghe hoạt động hoàn hảo.
   - **Niêm phong Audio 2 Bước lên Cloudflare R2:** Toàn bộ phát biểu được thu âm qua `MediaRecorder` chuẩn định dạng nén `WebM/Opus`, định danh độc bản `STT_MSSV.webm` (vd `01_SE170123.webm`). Quy trình tải lên 2 bước bảo mật:
     1. Kiosk gọi `POST /api/v1/official-exams/tickets/{ticketId}/questions/{questionId}/audio-upload-authorizations` để nhận Presigned PUT URL, R2 Object URL và Authorization Token.
     2. Kiosk tải file trực tiếp lên Cloudflare R2 qua Presigned URL, sau đó gọi `PUT /api/v1/official-exams/tickets/{ticketId}/questions/{questionId}/audio` gửi kèm mã băm SHA-256 và `audio_duration_seconds` để niêm phong bài thi.
   - **Kiosk Khóa An Toàn & AI Chấm Ngầm (Background AI Grading):** Ngay sau khi kết thúc ca thi hoặc thí sinh bấm nộp bài, hệ thống Persist-First lưu vé thi `SUBMITTED` trong $< 100$ms, máy trạm Kiosk khóa toàn màn hình bảo vệ và thông báo chính thức: *"Bài thi đã được ghi nhận và lưu trữ an toàn. Kết quả chấm sẽ được Giảng viên thẩm định và công bố trên hệ thống."* Kiosk tuyệt đối không hiển thị điểm, không có điểm liền và không khiếu nại tại chỗ. Thí sinh ký biên bản giấy và rời phòng thi. BoundedChannel (1,000 slots) đẩy bài thi sang Background Worker để Gemini 1.5 Pro chấm ngầm theo Rubric 10.0 và cập nhật trạng thái `AI_GRADED`.
   - **Xử lý Biên & Phòng Vệ Kiosk (Lockdown & Reconnect):**
     - *Duy trì Màn hình Khóa:* Máy Kiosk giữ nguyên màn hình khóa an toàn (`isKioskLocked = true`) cho đến khi Cán bộ Khảo thí phát lệnh đóng ca thi hoặc chuyển sang ca thi kế tiếp. Cán bộ coi thi (`proctor`) tuyệt đối CẤM đóng ca thi hoặc reset máy trạm.
     - *Khôi phục Kết nối Mạng:* Trường hợp mất mạng cục bộ, vé thi đã được lưu trạng thái `SUBMITTED` tại CSDL kèm mã băm SHA-256; khi có mạng lại, Kiosk gọi `GET /api/v1/official-exams/tickets/{id}` để khôi phục trạng thái khóa an toàn tức thì.
     - *Hậu kiểm Phân loại 2 Nhóm & Công bố Điểm:* Bài thi được phân loại: Nhóm 1 (Cần xem lại/Fail/Nghi ngờ: `is_suspicious == true`, `confidence_score < 0.70`, hoặc fail/điểm liệt) được Giảng viên thẩm định kỹ lưỡng trên Evidence Panel, nghe Waveform Player và sửa điểm có lý do (`override_reason`). Nhóm 2 (Tin cậy cao) được rà soát nhanh. Sau khi 100% sinh viên có điểm, Cán bộ Khảo thí / Giảng viên bấm **"Công Bố Điểm"** (`POST /api/v1/official-exams/shifts/{shiftId}/publish-grades`), kích hoạt One-Way Lock (`is_locked = true`, HTTP 403). Sinh viên xem điểm trên Student Portal; nếu có nguyện vọng phúc khảo, sinh viên gửi đơn trực tuyến tạo `AppealRequest` để Trưởng Bộ Môn giao Giảng viên chấm lại.

#### 3.2.2. Tầng 2: Cổng Kết Nối, Định Tuyến & Bảo Mật (Ingress Gateway & Auth Tier)
1. **Cloudflare Edge Network:** Hoạt động tại lớp ngoài cùng qua mạng lưới Anycast toàn cầu, cung cấp khả năng chống tấn công từ chối dịch vụ (DDoS Protection) ở cả Layer 3/4 và Layer 7. Tích hợp bộ quy tắc Web Application Firewall (WAF) ngăn chặn các lỗ hổng OWASP Top 10 và quản lý SSL/TLS 1.3 tự động.
2. **Nginx Reverse Proxy & Ingress Router:**
   - **Port 80:** Tự động chuyển hướng toàn bộ sang HTTPS qua mã trạng thái `HTTP 301 Permanent Redirect`.
   - **Port 443:** Thực hiện giải mã SSL (SSL Offloading) và gán các tiêu đề an ninh nghiêm ngặt (`HSTS`, `X-Frame-Options: SAMEORIGIN`, `X-Content-Type-Options: nosniff`).
   - **Rate Limiting:** Cấu hình trần 100 requests/giây/IP để chống brute-force và spam API.
   - **Bảng phân tuyến Upstream:**
     - `/` và static assets $\rightarrow$ Chuyển tiếp tới Container Frontend (Port `3000`).
     - `/api/v1/*` $\rightarrow$ Chuyển tiếp tới Container Backend .NET 8 (Port `5000`).
     - `/hubs/*` $\rightarrow$ Nâng cấp giao thức (Protocol Upgrade) sang WebSocket WSS phục vụ SignalR (Port `5000`).
3. **Cơ Chế Xác Thực & Phân Quyền Định Danh Duy Nhất (Identity & RBAC):**
   - **Zero Guest Access:** Hệ thống không cho phép người dùng nặc danh.
   - **Google Authentication (OAuth 2.0 PKCE):** Xác thực tài khoản Google (hỗ trợ mọi email domain, sinh viên tự đăng nhập hoặc tự đăng ký qua Google, không sử dụng mật khẩu local).
   - **JWT Bearer Token:** Máy chủ cấp JWT Token có thời hạn kèm Claims phân quyền nghiêm ngặt theo 6 vai trò chuẩn: `student`, `lecturer`, `department_head`, `examination_officer`, `proctor`, `admin`.
4. **SignalR Core Hubs (Real-Time Communication):**
   - Hub `/hubs/practice`: Đẩy bảng điểm Scorecard về trình duyệt ngay khi Worker hoàn tất chấm điểm với độ trễ dưới 100ms.
   - Hub `/hubs/exam-proctor`: Phục vụ giám thị điều khiển ca thi phòng lab, đồng bộ lệnh bắt đầu/kết thúc làm bài và theo dõi trạng thái trực tuyến của 40 máy trạm.

#### 3.2.3. Tầng 3: Lõi Backend .NET 8 Clean Architecture (Application & Core Processing)
Vận hành tại cổng nội bộ `5000`, triển khai theo kiến trúc 4 tầng chuẩn mực:
1. **Phân định trách nhiệm CQRS qua MediatR:**
   - Mọi thao tác thay đổi trạng thái (Nộp bài, Khóa điểm, Tạo đề) đều đi qua **Commands** có kiểm tra giao dịch ACID.
   - Mọi thao tác truy vấn (Xem lịch sử, Tra cứu Scorecard, Thẩm định) đi qua **Queries** tối ưu `.AsNoTracking()`.
2. **MediatR Pipeline Behaviors:**
   - `ValidationBehavior`: Tích hợp `FluentValidation`, tự động chặn đứng các command vi phạm quy tắc nghiệp vụ (ví dụ: Barem Rubric khác 10.0đ trả về mã lỗi `HTTP 422 Unprocessable Entity`).
   - `Logging & PerformanceBehavior`: Ghi log có cấu trúc và phát cảnh báo với mọi truy vấn xử lý vượt quá ngưỡng 500ms.
3. **Cơ chế Hàng đợi Chịu tải RAM (In-Memory Bounded Channel):**
   - Khởi tạo qua thư viện chuẩn `System.Threading.Channels` với dung lượng **1,000 slots**.
   - Cấu hình chế độ đẩy `BoundedChannelFullMode.Wait` tạo cơ chế áp lực ngược (Backpressure), đảm bảo máy chủ không bị tràn bộ nhớ khi toàn bộ phòng lab cùng nộp bài một lúc.
4. **Hệ thống Tiến trình Chạy nền (Background Hosted Services):**
   - `GradingQueueWorker`: Đọc tuần tự các task chấm điểm từ Channel, gọi bóc băng Whisper và chấm điểm Gemini kèm chiến lược thử lại tự động Polly.
   - `DlqReplayWorker`: Chạy nền định kỳ mỗi 5 phút, quét các bản ghi gặp sự cố quá tải mạng để thực hiện chấm bù tự động.

#### 3.2.4. Tầng 4: Tầng Dữ Liệu & Lưu Trữ Bền Vững (Data Persistence & Cloud Storage)
1. **Hệ Quản Trị Cơ Sở Dữ Liệu PostgreSQL 16 (Port 5432 Internal):**
   - Chuẩn hóa Bậc 3 (**3NF**), cấu trúc thành **38 bảng thực thể chuẩn hóa cốt lõi** phân bổ trong 7 Bounded Contexts và cụm hạ tầng chịu lỗi (trong đó 34 `DbSet` thuộc Phân hệ 1–6 & Hạ tầng đã hiện thực hóa trực tiếp trong `OralExamDbContext` của EF Core 8, cùng 4 bảng Phân hệ 7 `in_class_*` cho luồng mở rộng R6):
      - **Phân hệ 1 (Identity & RBAC - 2 bảng):** `users` (Định danh UUID duy nhất, xác thực kép Dual Auth qua Google OAuth 2.0 hoặc tài khoản `username`/`password` do Nhà trường cấp với BCrypt hash, 6 vai trò: `admin`, `department_head`, `examination_officer`, `lecturer`, `proctor`, `student`), `refresh_tokens`.
     - **Phân hệ 2 (Academic & Cohorts - R5 - 6 bảng):** `semesters`, `courses`, `course_clos`, `course_lecturers`, `classes`, `class_enrollments` (Quản trị học kỳ FA26, môn học PRN231 có `department_head_user_id` & cấu hình thi thử/follow-up, chuẩn đầu ra `course_clos`, bảng phân công nhiều giảng viên `course_lecturers`, lớp học có mật khẩu tham gia và sinh viên ghi danh).
     - **Phân hệ 3 (Rubric & Assessment - MF-03 - 2 bảng):** `rubrics`, `rubric_criteria` (Barem đánh giá bắt buộc `CHECK (total_max_score = 10.00)` và các tiêu chí gắn với 6 bậc Bloom).
     - **Phân hệ 4 (Practice Module - MF-01 - 5 bảng):** `practice_questions`, `practice_sessions`, `practice_answers`, `ai_evaluations`, `ai_evaluation_details` (Kho câu hỏi tự luyện mở, phiên làm bài, câu trả lời, liên kết câu hỏi phụ `parent_answer_id`, và bảng điểm chi tiết AI).
      - **Phân hệ 5 (Mock Exam Module - MF-02 - 6 bảng):** `exam_structures`, `exam_sets`, `exam_set_questions`, `mock_exam_quotas`, `mock_exam_sessions`, `mock_exam_answers` (Ma trận cấu trúc Bloom do Trưởng BM cấu hình, bộ đề bốc ngẫu nhiên, hạn ngạch ngày theo môn `max_mock_exams_per_day` do Admin cấu hình, và đo lường thời gian thi Master Timer).
      - **Phân hệ 6 (Official Lab Exam, Audit & Appeals - MF-04 - 9 bảng):** `exam_questions`, `official_exam_sessions`, `real_exam_session_shifts`, `student_exam_tickets`, `exam_question_submissions`, `exam_anti_cheat_logs`, `lecturer_audits`, `lecturer_audit_details`, `appeal_requests` (Kho câu hỏi thi bảo mật cần `approved_by`, ca thi phòng Lab ghi trực tiếp phòng thi trên ca, 40 vé máy trạm Kiosk, nộp bài âm thanh R2 + SHA-256, nhật ký vi phạm an ninh phòng thi `exam_anti_cheat_logs`, và cổng thẩm định sửa điểm bắt buộc giải trình `override_reason`, kèm quy trình phúc khảo 5 bước qua bảng `appeal_requests`: Khảo thí tiếp nhận & thẩm tra hợp lệ $\to$ chuyển giao Trưởng BM $\to$ Trưởng BM phân công Giảng viên chấm lại $\to$ Giảng viên chấm lại nộp giải trình $\to$ Trưởng BM thẩm định & phê duyệt quyết định cuối cùng).
      - **Phân hệ Chịu Lỗi, Cấu Hình & Thông Báo (4 bảng):** `dead_letter_queues` (Cứu hộ Zero Data Loss sau 3 lần retry; phân định rõ ràng trách nhiệm quản lý: Quản trị viên `admin` giám sát và kích hoạt replay cho luồng Luyện tập MF-01 và Thi thử MF-02, Cán bộ Khảo thí `examination_officer` giám sát và kích hoạt replay cho bài thi thật phòng Lab MF-04; Trưởng BM không can thiệp DLQ), `audit_logs` (Nhật ký không thể chối bỏ lưu vết mọi thao tác can thiệp dữ liệu), `system_configs` (Cấu hình động hệ thống: Min/Max Practice Questions, TranscriptBufferSeconds, SessionInactivityTimeoutMinutes = 5 phút), `notifications` (Hộp thư thông báo trong hệ thống).
      - **Phân hệ 7 (In-Class Exam Shifts & Rotating TOTP - MF-05 / R6 - 4 bảng):** `in_class_exam_shifts`, `in_class_shift_passwords`, `in_class_shift_participants`, `in_class_exam_submissions` (Ca thi vấn đáp tại lớp do Giảng viên mở với mật khẩu 6 ký tự xoay vòng 5 phút kèm grace period 30–60s).
      - **Tổng cộng 38 bảng chuẩn hóa 3NF:** $2 + 6 + 2 + 5 + 6 + 9 + 4 + 4 = \mathbf{38\text{ bảng thực thể nghiệp vụ}}$ (34 `DbSet` hiện hành trong `OralExamDbContext` + 4 bảng ca thi tại lớp MF-05).
     - **OneWayLockInterceptor (Bảo vệ pháp lý điểm thi):** Can thiệp vào pipeline `SaveChanges` của EF Core. Khi bản ghi có cờ `is_locked = true`, interceptor sẽ hủy bỏ ngay lập tức và ném ngoại lệ `ScoreSheetLockedException`, tương ứng với mã lỗi **`HTTP 403 Forbidden`**. Không một ai (kể cả Giảng viên hay Admin) có thể cập nhật hoặc xóa dữ liệu điểm số sau khi đã khóa sổ. Ngoại lệ hợp lệ duy nhất: Khi có đơn Phúc khảo nội bộ (`AppealRequest`) được Trưởng Bộ Môn giao cho Giảng viên chấm lại, Giảng viên được phân công mới có quyền cập nhật điểm phúc khảo kèm lý do giải trình.
    - **AuditTrailInterceptor:** Tự động ghi lại toàn bộ lịch sử can thiệp điểm thi của Giảng viên (Điểm AI cũ, Điểm mới thay đổi, Lý do giải trình bắt buộc `override_reason`, Thời điểm thực hiện) vào bảng `audit_logs`.
2. **Lưu Trữ Đối Tượng Đám Mây Cloudflare R2 (S3-Compatible Zero-Egress):**
   - Bucket: `oralexam-lab-audio`.
   - Chính sách **Zero-Egress Fees**: Không tính phí tải dữ liệu ra ngoài, giúp nhà trường tiết kiệm 100% chi phí băng thông khi Giảng viên nghe lại audio bài thi để chấm hậu kiểm.
   - **Quy tắc đặt tên file bất biến:** `STT_MSSV.webm` (Ví dụ: `01_SE170123.webm`).
   - **Niêm phong toàn vẹn bằng mã băm SHA-256:** Client tạo mã băm ngay khi thu âm xong; Backend kiểm tra đối chiếu mã băm này trước khi ghi nhận tính hợp lệ của bài nộp.
   - **Presigned URL:** Client đẩy trực tiếp file lên Cloudflare R2 qua Presigned URL thời hạn 15 phút, giải phóng hoàn toàn băng thông cho máy chủ Backend.

#### 3.2.5. Tầng 5: Cụm AI & Voice Services (External AI & Voice Cluster)
1. **Cloudflare Whisper STT Engine (Whisper Large-v3 GPU Node):** Chuyên trách bóc băng các bài thi nói phòng Lab (MF-04). Mô hình xử lý xuất sắc khả năng nhận diện tiếng Việt và thuật ngữ chuyên ngành phần mềm tiếng Anh. Trả về cấu trúc `timestamps_json` chi tiết đến từng mili-giây cho từng đoạn phát ngôn, làm bằng chứng đối chiếu cho Giảng viên trên Waveform Player.
2. **Google Gemini 1.5 Flash API (Tốc độ cao 1.8s - 2.5s):** Sử dụng cho: Luyện tập tự do (MF-01), Thi thử bấm giờ (MF-02), AI Simulator hiệu chuẩn Barem (MF-03). Áp dụng kỹ thuật **Chain-of-Thought (CoT) Prompting 3 bước**:
   - *Bước 1:* Phân tích ngữ nghĩa câu trả lời của sinh viên, trích xuất các luận điểm chính.
   - *Bước 2:* So sánh từng luận điểm với các tiêu chí trong Barem Rubric 10.0đ.
   - *Bước 3:* Xuất bảng điểm định dạng **Structured JSON** gồm: Điểm từng tiêu chí, Điểm tổng, Phân tích điểm mạnh, Điểm cần khắc phục và Gợi ý đáp án chuẩn.
3. **Google Gemini 1.5 Pro API (Suy luận phân tích sâu):** Sử dụng cho: Chấm điểm hàng loạt (Batch Grading) các ca thi phòng Lab (MF-04) chạy ngầm hậu kỳ 1 - 2 giờ sau ca thi. Kết hợp văn bản transcript của Whisper kèm mốc thời gian để đưa ra các trích dẫn bằng chứng (Timestamp Citations) trong nhận xét đánh giá.
4. **Bộ Quy Tắc Kích Hoạt Follow-Up Phân Tầng (Hierarchical Follow-up Rules):**
   - **Đối với Luồng 1 (Luyện tập tự do - MF-01):**
     $$\text{Kích hoạt} \iff (\text{Mode} == \mathbf{[Per\text{-}Question]}) \;\mathbf{AND}\; (4.0 \le \text{AI\_Score} \le 8.0)$$
     *Ý nghĩa:* Giảng viên KHÔNG cấu hình follow-up trong MF-01. Admin cấu hình số câu follow-up (1–5 câu, mặc định 2 câu). Nếu sinh viên chọn [Per-Question]: kích hoạt câu hỏi follow-up đào sâu khi điểm ranh giới $4.0 \le \text{Score} \le 8.0$; bỏ qua khi $<4.0$ (cung cấp đáp án mẫu) hoặc $>8.0$ (đã xuất sắc). Nếu chọn [Full-Session]: KHÔNG có câu hỏi follow-up. Luyện tập không lưu audio (chỉ lưu transcript đã sửa).
   - **Đối với Luồng 2 (Thi thử - MF-02):**
     $$\text{Kích hoạt} \iff (\text{Student\_Choice} == \mathbf{true}) \;\mathbf{AND}\; (\text{Phát hiện luận điểm cần phản biện trong Text})$$
     *Ý nghĩa:* Sinh viên chủ động tự chọn Có/Không Follow-up trước khi thi thử. Nếu chọn có, AI hỏi chuyên sâu ngữ cảnh (`[Needs Follow-up]`) theo nội dung thực tế, hoàn toàn **KHÔNG phụ thuộc vào mốc điểm số**. Trưởng Bộ Môn cấu hình số câu follow-up (1–5 câu, mặc định 2 câu) và thời lượng ca thi có follow-up. Thi thử không lưu audio (chỉ lưu transcript).
   - **Đối với Luồng 4 (Thi thật phòng Lab - MF-04):**
     $$\text{Kích hoạt} \iff (\text{ExamCourse.has\_follow\_up} == \mathbf{true}) \;\mathbf{AND}\; (\text{Phát hiện luận điểm cần phản biện trong Text})$$
     *Ý nghĩa:* Trưởng Bộ Môn cấu hình tính năng hỏi chuyên sâu (`has_follow_up`) và số câu follow-up (1–5 câu, mặc định 2 câu) theo môn thi trong kỳ thi. AI hỏi chuyên sâu theo nội dung thực tế (`[Needs Follow-up]`), KHÔNG phụ thuộc vào điểm số. Thi thật bắt buộc lưu audio Cloudflare R2 (`STT_MSSV.webm` + SHA-256) phục vụ hậu kiểm.

---

## <a id="phan-4"></a>🏛️ PHẦN 4: KIẾN TRÚC PHÂN TẦNG CLEAN ARCHITECTURE 4 TẦNG & REACT 19 FRONTEND

### <a id="phan-41"></a>4.1. Sơ Đồ Phân Tầng Clean Architecture (.NET 8 + React 19)

```mermaid
flowchart TD
    subgraph Layer_Client ["Presentation Layer — Web Client (React 19 Vite)"]
        direction TB
        UI_Components["React Components & Pages<br>• PracticeView & TimedMockExamView<br>• QuestionRubricStudio & AISimulatorModal<br>• LecturerAuditPortal (Waveform Player 1.25x/1.5x)<br>• DepartmentHeadPortal (DLQ Replay)"]
        UI_Audio["Client Audio & Security Engines<br>• Web Speech STT / TTS Client Controller<br>• Terminology Code-Switching Buffer (cấu hình theo môn)<br>• MediaRecorder Audio Sealer (STT_MSSV.webm)<br>• Fullscreen Kiosk Lockdown & onblur Guard"]
        UI_Network["Client Network Adapters<br>• Axios / Fetch API Client (/api/v1/*)<br>• SignalR Connection Hub Client (/hubs/practice)<br>• Client-Side Rubric Guard (Sum == 10.0)"]
    end

    subgraph Layer_API ["API Layer — Host & Entry Point (ASP.NET Core 8 Web API)"]
        direction TB
        API_Program["Program.cs (Composition Root)<br>• Dependency Injection Wire-up<br>• Middleware Pipeline Configuration<br>• CORS Contract (UseCors trước UseStaticFiles)"]
        API_Controllers["API Controllers (/api/v1/)<br>• AuthController (Dual Auth: Google OAuth + Password)<br>• CourseClosController & CoursesController<br>• QuestionsController & ExamSetsController<br>• PracticeController & MockExamController<br>• OfficialExamsController & AuditController<br>• AdminController (User, Semester, Course, Config & Mock Quota)<br>• AcademicController (Gán Trưởng BM, Phân công GV, Tạo lớp & Join lớp)<br>• InClassShiftController (Ca thi trong lớp & Mật khẩu xoay vòng 5m)<br>• AppealsController (Phúc khảo qua Khảo thí thẩm tra)<br>• DeadLetterQueueController (Admin cho MF-01/02, Khảo thí cho MF-04)"]
        API_Middleware["Middlewares & Filters<br>• GlobalExceptionMiddleware (RFC 7807 ProblemDetails)<br>• JwtBearerAuthMiddleware & RBAC Authorization (6 Roles)<br>• Client RateLimiting Middleware"]
        API_SignalR["SignalR Real-Time Hubs<br>• PracticeHub (/hubs/practice)<br>• ExamProctorHub (Đồng bộ ca thi phòng Lab)"]
    end

    subgraph Layer_App ["Application Layer — Core Business Logic (MediatR CQRS)"]
        direction TB
        App_Commands["CQRS Commands & Handlers<br>• SubmitPracticeAnswerCommand (202 Accepted)<br>• StartMockExamCommand (PostgreSQL Quota Guard)<br>• CreateQuestionCommand (Barem Rubric 10.0)<br>• GenerateQuestionFromCloCommand (Gemini 1.5 Flash)<br>• CalibrateQuestionCommand (AI Simulator)<br>• AssignDepartmentHeadCommand & AssignLecturersToCourseCommand (R5)<br>• CreateClassCommand, ResetClassPasswordCommand & JoinClassCommand (R5)<br>• CreateInClassShiftCommand, EnterInClassShiftCommand & AdjustInClassGradeCommand (R6)<br>• SubmitAppealCommand, ForwardAppealCommand, AssignLecturerAppealCommand & RegradeAppealCommand (R7)<br>• OverrideScoreCommand & PublishGradesCommand<br>• ReplayDlqTaskCommand (Admin / Khảo thí)"]
        App_Queries["CQRS Queries & Handlers<br>• GetPracticeHistoryQuery & GetScorecardQuery<br>• GetMockExamScorecardQuery<br>• GetExamAuditSessionQuery (Evidence Panel & Barem)<br>• GetInClassShiftSubmissionsQuery & GetActiveRotatingPasswordQuery<br>• GetAppealRequestsQuery<br>• ExportExamGradesExcelQuery (EPPlus)"]
        App_Pipeline["MediatR Pipeline Behaviors<br>• ValidationBehavior (FluentValidation: Sum == 10.0đ)<br>• LoggingBehavior & PerformanceTrackingBehavior"]
        App_Interfaces["Application Interfaces (Contracts / Abstractions)<br>• IApplicationDbContext & IUnitOfWork<br>• IGeminiService & IWhisperService<br>• IR2StorageService, IQuotaService & IExcelService"]
    end

    subgraph Layer_Infra ["Infrastructure Layer — Adapters & External Concerns"]
        direction TB
        Infra_Persistence["Data Access & EF Core 8<br>• OralExamDbContext (PostgreSQL Npgsql Provider, Lược đồ 3NF)<br>• Entity Configurations (Fluent API, 3NF, ESR Indexes)<br>• EF Migrations & SeedData"]
        Infra_Interceptors["Database Interceptors<br>• OneWayLockInterceptor (Chặn UPDATE row is_locked=true)<br>• AuditTrailInterceptor (Ghi system_audit_logs)"]
        Infra_Channels["In-Memory Bounded Channels<br>• BoundedGradingQueueChannel (1,000 slots Capacity)<br>• ChannelReader / ChannelWriter Thread-Safe Queue"]
        Infra_Workers["Background Hosted Services<br>• GradingQueueWorker (IHostedService + Polly Retry)<br>• DlqReplayWorker (Định kỳ 5 phút quét chấm bù)"]
        Infra_ExternalServices["External Service Adapters<br>• GeminiService (Prompt CoT 3 bước, Multimodal & JSON Schema)<br>• WhisperTranscriptionService (Bóc băng kèm Timestamps)<br>• R2StorageService (Presigned URL & SHA-256 Hash)<br>• PostgreSqlQuotaService (Kiểm tra Quota theo môn do Admin cấu hình)<br>• ExcelService (EPPlus Import Roster / Export Bảng Điểm Khảo Thí FPT)"]
    end

    subgraph Layer_Domain ["Domain Layer — Enterprise Kernel (Pure C# POCO)"]
        direction TB
        Domain_Entities["Core Domain Entities (Lược đồ 3NF)<br>• User, Course, CourseClo, CourseLecturer, Class, ClassEnrollment<br>• InClassExamShift, InClassShiftPassword, InClassShiftParticipant, InClassExamSubmission<br>• Question, RubricCriterion, ModelAnswer<br>• ExamStructure, ExamSet, ExamSetQuestion<br>• PracticeSession, StudentAnswer, AIEvaluation<br>• OfficialExamSession, RealExamSessionShift, StudentExamTicket<br>• ExamAntiCheatLog, DeadLetterQueue, SystemAuditLog, AppealRequest"]
        Domain_Enums["Domain Enums & Value Objects<br>• UserRole (Student, Lecturer, DepartmentHead, ExaminationOfficer, Proctor, Admin)<br>• BloomLevel (Remember đến Create)<br>• UsageScope (practice, exam)<br>• QuestionSource (Manual, AiGenerated)<br>• ExamSetStatus (Draft, SubmittedForReview, Official, HandedOver, Rejected)<br>• SubmissionStatus (PENDING đến LOCKED)<br>• AntiCheatEventType & AntiCheatSeverity"]
        Domain_Exceptions["Domain Business Exceptions<br>• DomainValidationException<br>• EntityNotFoundException<br>• ExamQuotaExceededException<br>• ScoreSheetLockedException"]
    end

    %% Dependency Arrows
    UI_Network -->|Giao tiếp REST & WSS| API_Controllers
    UI_Network -.->|Lắng nghe sự kiện| API_SignalR
    API_Controllers -->|Dispatch Commands / Queries| App_Commands
    API_Controllers -->|Dispatch Queries| App_Queries
    App_Commands -->|Pipeline qua ValidationBehavior| App_Pipeline
    Infra_Persistence -->|Implement| App_Interfaces
    Infra_ExternalServices -->|Implement| App_Interfaces
    App_Commands -->|Thao tác nghiệp vụ thực thể| Domain_Entities
    App_Queries -->|Truy vấn dữ liệu| Domain_Entities
    Infra_Persistence -->|Ánh xạ ORM CSDL| Domain_Entities
```

### <a id="phan-42"></a>4.2. Ranh Giới 4 Tầng & Nguyên Lý Đảo Ngược Phụ Thuộc (DIP)
- **Domain Layer (Hạt nhân bất biến):** Chứa 100% C# POCO thuần túy, tuyệt đối không phụ thuộc bất kỳ framework hay ORM nào. Nắm giữ quy tắc nghiệp vụ cốt lõi: các trạng thái bài thi (`SubmissionStatus`), phân loại Bloom, và ngoại lệ nghiệp vụ (`ScoreSheetLockedException`).
- **Application Layer (Điều phối nghiệp vụ):** Độc lập với cơ sở dữ liệu và công nghệ bên ngoài. Triển khai mẫu thiết kế CQRS qua MediatR; mọi thao tác ghi dữ liệu đều đóng gói trong Command, thao tác đọc trong Query. Toàn bộ nghiệp vụ kiểm tra tính hợp lệ của Barem Rubric 10.0đ được kiểm soát tập trung qua `FluentValidation` và `ValidationBehavior`.
- **Infrastructure Layer (Triển khai kỹ thuật):** Chịu trách nhiệm thực thi các Interfaces định nghĩa tại Application: kết nối PostgreSQL 16 qua EF Core 8, điều phối hàng đợi bộ nhớ `System.Threading.Channels`, giao tiếp Google Gemini API, bóc băng Whisper STT, lưu trữ Cloudflare R2, và chốt chặn khóa điểm bất biến bằng `OneWayLockInterceptor`.
- **API Layer (Cổng tiếp nhận & Host):** Chịu trách nhiệm khởi tạo Composition Root (`Program.cs`), tiếp nhận HTTP Request, đóng gói mã lỗi thống nhất theo chuẩn RFC 7807 (`ProblemDetails`), và duy trì kết nối thời gian thực SignalR Hub.

---

## <a id="phan-5"></a>🌐 PHẦN 5: SƠ ĐỒ HẠ TẦNG MẠNG & TRIỂN KHAI DOCKER/NGINX TOPOLOGY

### <a id="phan-51"></a>5.1. Sơ Đồ Mạng & Containerization Topology (Mermaid)

```mermaid
flowchart TD
    subgraph Client_Locations ["Phạm Vi Truy Cập Người Dùng (Client Networks)"]
        WAN_User["Người Dùng Ngoại Mạng (Sinh viên / Giảng viên tại nhà)<br>• Web Browser (Chrome, Edge, Safari)<br>• Kết nối Internet công cộng qua HTTPS/TLS 1.3"]
        Lab_Network["Phòng Máy Khảo Thí Đại Học FPT (Campus Lab Network)<br>• 40 Máy trạm Kiosk Lab PC (Mạng LAN nội bộ / Isolated VLAN)<br>• Firewall trường chặn truy cập Internet tự do<br>• Whitelist duy nhất Domain hệ thống thi"]
    end

    subgraph Edge_Security ["Vành Đai An Ninh & Phân Phối (Edge & Ingress Gateway)"]
        Cloudflare_Edge["Cloudflare Edge Network (Global Anycast)<br>• DDoS Protection Layer 3/4/7<br>• WAF Rules chống SQL Injection, XSS, Path Traversal<br>• Quản lý DNS và Chứng chỉ SSL/TLS 1.3"]
        Reverse_Proxy["Nginx Ingress / Reverse Proxy (Linux Host Gateway)<br>• Port 80: HTTP 301 Redirect sang HTTPS Port 443<br>• Port 443: SSL Offloading và HSTS Security Headers<br>• Upstream Router: / -> Port 3000, /api/* và /hubs/* -> Port 5000"]
    end

    subgraph Production_Host ["Máy Chủ Sản Xuất (Production Linux Server / Docker Host)"]
        subgraph Docker_Engine ["Docker Containerization Topology"]
            FE_Container["Container: oralexam-frontend<br>• Base Image: Node.js 20 Alpine<br>• Framework: React 19 (Vite) Production Build<br>• Internal Port: 3000"]
            BE_Container["Container: oralexam-backend<br>• Base Image: .NET 8 ASP.NET Core Runtime<br>• Web Server: Kestrel Web Server<br>• Internal Port: 5000<br>• In-Process Bounded Channel (1,000 slots)<br>• In-Process Hosted Services: GradingQueueWorker & DlqReplayWorker"]
            DB_Container["Container: oralexam-postgres<br>• Base Image: PostgreSQL 16 Alpine<br>• Internal Port: 5432 (Không mở ra Internet)<br>• Volume Mount: /var/lib/postgresql/data (Persistent Storage)<br>• Daily Automated Database Snapshot Backup"]
        end
    end

    subgraph Cloud_Infrastructure ["Hạ Tầng Đám Mây & Dịch Vụ Ngoài (External Cloud & AI)"]
        Google_AI["Google Cloud Vertex AI & Google API<br>• Google Gemini 1.5 Flash (Practice & AI Simulator)<br>• Google Gemini 1.5 Pro (Batch Grading Lab Exam)<br>• HTTPS REST API Endpoint"]
        Whisper_Service["Whisper STT Server Service<br>• OpenAI Whisper Large-v3 (GPU Worker Node)<br>• Server-side Audio Transcription kèm Timestamps"]
        Cloudflare_R2["Cloudflare R2 Object Storage<br>• S3-Compatible Storage Endpoint (Zero-Egress Fees)<br>• Bucket: oralexam-lab-audio<br>• Lưu trữ STT_MSSV.webm kèm SHA-256 seal"]
        Google_OAuth["Google Workspace Identity Provider<br>• FPT OAuth 2.0 PKCE Endpoint accounts.google.com"]
        FPT_Exam_Portal["Hệ Thống Khảo Thí FPT<br>• Cổng Thông Tin Khảo Thí Nhà Trường (Excel Interchange)"]
    end

    %% Network Routes
    WAN_User -->|HTTPS 443 / WSS| Cloudflare_Edge
    Lab_Network -->|HTTPS 443 Kiosk Mode| Cloudflare_Edge
    Cloudflare_Edge -->|Proxy Encrypted Traffic| Reverse_Proxy

    Reverse_Proxy -->|Proxy Pass / và static assets| FE_Container
    Reverse_Proxy -->|Proxy Pass /api/* và WebSocket /hubs/*| BE_Container

    BE_Container -->|Internal TCP 5432 / Npgsql Connection Pool| DB_Container

    %% Outbound Cloud Integrations
    BE_Container -->|REST HTTPS Call Outbound| Google_AI
    BE_Container -->|Dispatch Audio for Batch STT| Whisper_Service
    BE_Container -->|Generate Presigned URL & Verify Digest| Cloudflare_R2
    Lab_Network -->|Direct Presigned Stream STT_MSSV.webm| Cloudflare_R2
    BE_Container -->|PKCE Token Exchange| Google_OAuth
    BE_Container <-->|Import Roster & Export Grade Sheet .xlsx| FPT_Exam_Portal
```

### <a id="phan-52"></a>5.2. Bảng Phân Bổ Cổng Mạng, Vùng Cách Ly & Chính Sách Tường Lửa

| Vùng mạng (Zone) | Thành phần hệ thống | Cổng tiếp nhận (Port) | Giao thức | Chính sách truy cập & Ranh giới bảo mật |
|:---|:---|:---:|:---:|:---|
| **Public Edge** | Cloudflare Edge Network | `80`, `443` | HTTP, HTTPS / TLS 1.3 | Tiếp nhận toàn bộ lưu lượng công cộng, lọc DDoS Layer 3/4/7, tự động cấp chứng chỉ SSL. |
| **Ingress Gateway** | Nginx Reverse Proxy | `80`, `443` | HTTPS / WSS | Chuyển hướng 80 $\rightarrow$ 443, Rate Limiting 100 req/s, SSL Offloading, chặn IP nghi vấn. |
| **Presentation Tier** | `oralexam-frontend` (React 19) | `3000` (Nội bộ) | HTTP | Chỉ lắng nghe các gói tin từ Nginx trong mạng Docker bridge `oralexam-net`. |
| **Application Tier** | `oralexam-backend` (.NET 8) | `5000` (Nội bộ) | HTTP / WSS | Nhận API call từ Nginx và kết nối thời gian thực WebSocket SignalR `/hubs/*`. |
| **Database Tier** | `oralexam-postgres` (PostgreSQL 16) | `5432` (Nội bộ) | TCP / PostgreSQL | **CẤM TUYỆT ĐỐI mở ra Internet**. Chỉ Backend được kết nối qua thông tin xác thực mã hóa. |
| **Object Storage** | Cloudflare R2 | `443` | S3 API HTTPS | Zero-Egress, phân quyền upload/download qua Presigned URL thời hạn 15 phút. |
| **External AI** | Gemini 1.5 Flash/Pro & Whisper | `443` | HTTPS REST | Gọi ra ngoài (Outbound Call) từ Backend Hosted Workers qua API Key bảo mật. |

### <a id="phan-53"></a>5.3. Cấu Hình Reverse Proxy & Docker Compose Chuẩn Mực

#### Cấu hình Nginx Upstream & SignalR WebSocket Upgrade (`nginx.conf`):
```nginx
upstream frontend_upstream {
    server oralexam-frontend:3000;
}

upstream backend_upstream {
    server oralexam-backend:5000;
}

server {
    listen 80;
    server_name oralexam.fpt.edu.vn;
    return 301 https://$host$request_uri;
}

server {
    listen 443 ssl http2;
    server_name oralexam.fpt.edu.vn;

    ssl_certificate /etc/nginx/certs/fullchain.pem;
    ssl_certificate_key /etc/nginx/certs/privkey.pem;
    ssl_protocols TLSv1.2 TLSv1.3;

    # Security Headers
    add_header X-Frame-Options "SAMEORIGIN" always;
    add_header X-Content-Type-Options "nosniff" always;
    add_header Strict-Transport-Security "max-age=31536000; includeSubDomains" always;

    # Rate Limiting
    limit_req zone=api_limit burst=20 nodelay;

    # Frontend Route
    location / {
        proxy_pass http://frontend_upstream;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
    }

    # Backend API Route
    location /api/v1/ {
        proxy_pass http://backend_upstream;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }

    # SignalR WebSocket Route
    location /hubs/ {
        proxy_pass http://backend_upstream;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
        proxy_set_header Host $host;
        proxy_cache_bypass $http_upgrade;
        proxy_read_timeout 300s;
    }
}
```

---

## <a id="phan-6"></a>🔄 PHẦN 6: LUỒNG DỮ LIỆU TƯƠNG TÁC CỦA 4 CORE MAIN FLOWS & BỘ QUY TẮC FOLLOW-UP KÉP

### <a id="phan-61"></a>6.1. Sơ Đồ Tổng Quan Luồng Dữ Liệu 4 Main Flows (Mermaid)

```mermaid
flowchart TD
    subgraph MF01_Box ["MF-01: Luyện Tập Tự Do (Interactive Practice)"]
        direction TB
        M1_Start["SV chọn Môn & Chọn Chế Độ Upfront<br>• [Per-Question]: Chấm tức thì từng câu<br>• [Full-Session]: Làm hết N câu rồi chấm"]
        M1_Speech["TTS đọc đề ↔ Micro thu âm / Gõ phím"]
        M1_Buffer["Màn hình đệm hiệu đính Code-Switching (mặc định 60s)"]
        M1_Queue["Hàng đợi 4 tầng Zero Data Loss<br>(Persist PENDING → Bounded Channel → Polly Retry → DLQ)"]
        M1_AI["Gemini 1.5 Flash CoT chấm Barem Rubric 10.0"]
        M1_A2["Kiểm tra điểm: Nếu 4.0 ≤ Score ≤ 8.0 & Môn bật<br>→ Kích hoạt câu hỏi chuyên sâu đào sâu A2"]
        M1_Start --> M1_Speech --> M1_Buffer --> M1_Queue --> M1_AI --> M1_A2
    end

    subgraph MF02_Box ["MF-02: Thi Thử Bấm Giờ (Timed Mock Exam)"]
        direction TB
        M2_Quota["PostgreSQL Quota Guard<br>Kiểm tra hạn ngạch theo môn max_mock_exams_per_day<br>(Quá lượt: ném HTTP 429 và gợi ý MF-01)"]
        M2_Timer["Đồng hồ kép: Server Master Timer & Client Countdown"]
        M2_Gate["Voice-First Gate: Khóa ô gõ phím,<br>bắt buộc phát biểu bằng giọng nói trước"]
        M2_FollowUp["Follow-up: Môn bật & Ngữ cảnh bài nói<br>(AI hỏi xoáy theo nội dung, KHÔNG theo điểm)"]
        M2_Scorecard["Instant Feedback Scorecard chi tiết theo Rubric<br>(Strengths, Weaknesses, Suggestions & Lịch sử thi)"]
        M2_Quota --> M2_Timer --> M2_Gate --> M2_FollowUp --> M2_Scorecard
    end

    subgraph MF03_Box ["MF-03: Ngân Hàng Đề, Chuẩn CLO & Đóng Gói Bộ Đề (Question Studio, ExamSets & Approval Flow)"]
        direction TB
        M3_CLO["Kho Chuẩn Đầu Ra Môn Học (course_clos 3NF)<br>Quản lý mã CLO, mô tả & Bloom mục tiêu"]
        M3_AIGen["Gemini 1.5 Flash tự động sinh câu hỏi từ CLO:<br>• Bloom 1-6 & Barem Rubric ∑=10.0đ bất biến<br>• Model Answer >= 50 ký tự & Tiêu chí chi tiết"]
        M3_Manual["Giảng viên biên soạn thủ công (source='manual')<br>Đính kèm sơ đồ/ảnh kỹ thuật <= 5MB (Magic Bytes)"]
        M3_DupCheck["Kiểm tra độ tương đồng ngữ nghĩa tự động<br>Gắn cờ cảnh báo isFlagged = true nếu trùng lặp"]
        M3_Review["Giảng viên kiểm tra AI Simulator & Gửi duyệt Bộ Môn<br>(POST /batch-submit-review -> SUBMITTED_FOR_REVIEW)"]
        M3_DeptApprove["Trưởng Bộ Môn rà soát cờ trùng lặp & Phê duyệt<br>(APPROVED, NEEDS_REVISION, REJECTED)"]
        M3_Blueprint["ExamStructure: slot_definitions (JSONB) & required_set_count<br>Ma trận số lượng câu theo CLO, Bloom, độ khó"]
        M3_ExamSets["Đóng gói bộ đề ExamSets: DRAFT -> SUBMITTED_FOR_REVIEW<br>-> OFFICIAL (Khóa câu hỏi APPROVED) -> HANDED_OVER"]
        M3_CLO --> M3_AIGen --> M3_DupCheck --> M3_Review
        M3_Manual --> M3_DupCheck
        M3_Review --> M3_DeptApprove --> M3_Blueprint --> M3_ExamSets
    end

    subgraph MF04_Box ["MF-04: Thi Thật Phòng Lab, Công Bố Điểm & Phúc Khảo Nội Bộ (Lab Exam, Audit & Internal Appeals)"]
        direction TB
        M4_P0["Khảo thí tiếp nhận đề HANDED_OVER<br>Lập kỳ thi OfficialExamSession & Ca thi RealExamSessionShift<br>Phân bổ đề không trùng (uq_tickets_shift_exam_set) & Xuất bản đề"]
        M4_P1["Giám thị điểm danh STT 1-40, đối chiếu CCCD/Thẻ SV<br>Kiosk Lockdown & Hardware Mic-Check 30s (>= 60dB)"]
        M4_P2["Làm bài thi viva theo ExamInputMode, ghi nhật ký exam_anti_cheat_logs<br>Upload 2 bước WebM STT_MSSV.webm lên R2 kèm SHA-256"]
        M4_P3["Nộp bài: Persist SUBMITTED < 100ms, Kiosk khóa an toàn (không điểm liền)<br>Gemini 1.5 Pro chấm ngầm Rubric 10.0 qua BoundedChannel 1,000 slots"]
        M4_P4["Giảng viên hậu kiểm Evidence Panel & Waveform Player<br>Phân loại 2 nhóm: Nghi ngờ (conf < 0.70 / suspicious) vs Tin cậy cao"]
        M4_Lock["Khảo thí / Giảng viên Publish Điểm (100% SV có điểm) -> One-Way Lock (is_locked=true)<br>SV xem điểm Student Portal, nộp AppealRequest -> Trưởng BM giao GV chấm lại"]
        M4_P0 --> M4_P1 --> M4_P2 --> M4_P3 --> M4_P4 --> M4_Lock
    end

    subgraph Core_Shared_Data ["Tài Nguyên Dữ Liệu Dùng Chung (Shared Kernel)"]
        Shared_QB[("Ngân Hàng Câu Hỏi, CLOs & Bộ Đề ExamSets<br>(course_clos, practice_questions, exam_questions, exam_sets)")]
        Shared_Queue["Bounded Channel In-Memory Queue (1,000 slots)"]
        Shared_AI["Google Gemini 1.5 API (Flash / Pro CoT Engine)"]
        Shared_Audio["Cloudflare R2 Object Storage (STT_MSSV.webm)"]
        Shared_DB[("PostgreSQL 38 Bảng 3NF & SystemAuditLogs")]
    end

    %% Cross-Flow Integrations
    M3_ExamSets -->|Bàn giao bộ đề HANDED_OVER| M4_P0
    Shared_QB -.->|Cung cấp câu hỏi tự luyện practice_questions| M1_Start
    Shared_QB -.->|Bốc đề thi thử từ practice_questions| M2_Quota
    Shared_QB -.->|Cấp đề ca thi phòng Lab exam_questions| M4_P0

    M1_Queue --> Shared_Queue
    M4_P3 --> Shared_Queue
    Shared_Queue --> Shared_AI

    M4_P2 -->|Lưu trữ file âm thanh| Shared_Audio
    Shared_Audio -->|Kéo file bóc băng| M4_P3

    M1_AI --> Shared_DB
    M2_Scorecard --> Shared_DB
    M4_Lock --> Shared_DB
```

### <a id="phan-62"></a>6.2. Ma Trận So Sánh Toàn Diện 4 Core Main Flows

| Tiêu chí so sánh | MF-01: Luyện tập tự do | MF-02: Thi thử bấm giờ | MF-03: Ngân hàng đề, CLO & Đóng gói Đề | MF-04: Thi thật phòng Lab, Công bố điểm & Phúc khảo |
|:---|:---|:---|:---|:---|
| **Tác nhân chính** | Sinh viên SE (`ACT-01 / student`) | Sinh viên SE (`ACT-01 / student`) | Giảng viên (`ACT-02 / lecturer`) & Trưởng Bộ Môn (`ACT-04 / department_head`) | Khảo thí (`ACT-05 / examination_officer`), Giám thị (`ACT-06 / proctor`), Giảng viên (`ACT-02`), Sinh viên (`ACT-01`), Trưởng Bộ Môn (`ACT-04`) |
| **Mục tiêu sư phạm** | Formative Assessment (Rèn luyện phản xạ) | Mock Pressure (Tập dượt áp lực thời gian) | Governance & Accreditation (Chuẩn hóa CLOs & Bộ đề thi) | Summative Assessment (Đánh giá học phần lấy điểm chính thức) |
| **Cơ chế kiểm soát** | Per-Question: On-Demand chọn đơn/tổ hợp mức độ, không ép số câu, thuật toán Anti-3-Consecutive (tối đa 2 câu cùng mức trong 3 câu liên tiếp). Full-Session: Progressive 3-10 câu do Admin cấu hình (MinMixedPracticeQuestions=3, MaxMixedPracticeQuestions=10), chia đều Dễ -> TB -> Khó, KHÔNG có follow-up. Inactivity Timeout 5 phút (SessionInactivityTimeoutMinutes=5), HTTP 410 Gone, bảo toàn điểm. DLQ do Admin quản lý. | Daily Quota Guard (hạn ngạch theo môn `max_mock_exams_per_day` do Admin cấu hình); Voice-First Gate; Instant Feedback Scorecard & Lịch sử thi (kho `practice_questions`). DLQ do Admin quản lý. | Quản lý CLOs trực tiếp (`course_clos` 3NF); Phân tách 2 luồng: Luyện tập/thi thử độc lập vs Thi thật bám sát ma trận đề mẫu Trưởng BM ban hành; Giảng viên AI Gen từ CLO (`POST /generate-from-clo`) hoặc soạn thủ công kèm ảnh $\le 5$MB; Barem $\sum \equiv 10.0$đ; Model Answer $\ge 50$ ký tự; Cảnh báo trùng lặp `isFlagged = true`; Trưởng Bộ Môn duyệt (`APPROVED`, `NEEDS_REVISION`, `REJECTED`); Blueprint `slot_definitions` & đóng gói bộ đề `exam_sets` (`DRAFT` $\to$ `SUBMITTED_FOR_REVIEW` $\to$ `OFFICIAL` $\to$ `HANDED_OVER`) | Kiosk Lockdown, Hardware Mic-Check 30s ($\ge 60$dB), `ip_address`, Nhật ký chống gian lận `exam_anti_cheat_logs`, Upload 2 bước audio R2, Kiosk khóa an toàn, AI Chấm ngầm BoundedChannel 1,000 slots, Hậu kiểm 2 nhóm Evidence Panel (AudioURL, Transcript Whisper gốc, AI CoT), Khảo thí Publish Điểm khi 100% SV có điểm, One-Way Lock (`is_locked = true`), Phúc khảo qua Khảo thí tiếp nhận -> Trưởng BM giao GV chấm lại -> Trưởng BM duyệt; DLQ do Khảo thí quản lý |
| **Xử lý âm thanh** | Web Speech STT/TTS (dưới 0.5s) | Voice-First Gate (Bắt buộc nói) | Text-based Barem & Audio Preview | Whisper Large-v3 STT bóc băng có timestamps JSON |
| **Lưu trữ âm thanh** | Không lưu trữ (tối ưu chi phí) | Không lưu trữ (tối ưu chi phí) | Không áp dụng | Cloudflare R2 (`STT_MSSV.webm`) kèm SHA-256 niêm phong |
| **Mô hình AI** | Gemini 1.5 Flash (CoT 3 bước) | Gemini 1.5 Flash (Dual-Path) | Gemini 1.5 Flash (Sinh từ CLO & AI Simulator) | Gemini 1.5 Pro (Chấm ngầm toàn bộ bài thi & Doubt Guard phân loại bài nghi ngờ) |
| **Quy tắc Follow-up** | **Admin cấu hình (1–5 câu, mặc định 2 câu); [Per-Question] kích hoạt khi $4.0 \le \text{Score} \le 8.0$, [Full-Session] KHÔNG có** | **Sinh viên chủ động tự chọn Có/Không Follow-up; AI hỏi theo ngữ cảnh [Needs Follow-up]** | Không áp dụng | **100% Trưởng Bộ Môn cấu hình cho môn/ca thi thật (1–5 câu, mặc định 2 câu); AI hỏi theo ngữ cảnh [Needs Follow-up]** |
| **Tính năng độc bản** | Màn hình đệm sửa Code-Switching (cấu hình động `transcript_buffer_seconds`, 10-300s, mặc định 60s); Admin cấu hình số câu follow-up hệ thống (1–5 câu, mặc định 2 câu); DLQ do Admin quản lý | Nguồn đề kho `practice_questions`, Sinh viên tự chọn Có/Không Follow-up trước khi thi, Instant Feedback Scorecard chi tiết từng câu theo Rubric và lưu Lịch sử thi; DLQ do Admin quản lý | Quản lý CLOs môn học 3NF; Gemini sinh đề theo CLO; Barem 10.0; Model Answer; Cảnh báo trùng lặp `isFlagged = true`; AI Calibration Simulator; Blueprint `slot_definitions` (JSONB); Đóng gói đề `exam_sets` chuẩn vòng đời `DRAFT` $\to$ `HANDED_OVER` cho Khảo thí | Khảo thí lập kỳ thi, ca thi, phân bổ đề không trùng (`uq_tickets_shift_exam_set`), xuất bản đề; Giám thị điểm danh STT 1-40 & báo cáo vi phạm an ninh; Kiosk Safe Lock Notice, AI Chấm ngầm, Evidence Panel Waveform Player, Khảo thí Publish Điểm một lần duy nhất khi 100% sinh viên có điểm, One-Way Lock, Phúc khảo 5 bước qua Khảo thí thẩm tra & DLQ do Khảo thí quản lý |

### <a id="phan-63"></a>6.3. Đặc Tả Chuyên Sâu Cơ Chế Follow-up & Bộ Quy Tắc Kích Hoạt Follow-up Chuẩn Hóa

#### 1. Định nghĩa Cơ Chế Follow-up trong Cơ Sở Dữ Liệu Lược Đồ 3NF
* **Khái niệm:** Câu hỏi phụ / câu hỏi phản biện do AI Gemini tự động sinh ra sau khi phân tích câu trả lời của sinh viên.
* **Liên kết dữ liệu chuẩn hóa trong CSDL lược đồ 3NF:**
  - Trong **MF-01 (Luyện tập)**: Thực hiện qua cơ chế **Self-Referencing** trong bảng `practice_answers`. Câu trả lời phụ lưu bản ghi riêng trong `practice_answers` với `is_follow_up = true`, `parent_answer_id` liên kết về `id` của câu trả lời gốc (`practice_answers.id`). Câu hỏi luyện tập trong `practice_questions` chứa gợi ý `follow_up_prompt`. Kết quả chấm AI lưu vào `ai_evaluations` trỏ `practice_answer_id`.
  - Trong **MF-02 (Thi thử)**: Lưu câu trả lời trong bảng `mock_exam_answers`. Lưu cờ tùy chọn của sinh viên tại `mock_exam_sessions.has_follow_up`. Khi sinh viên chọn Có Follow-up, AI phân tích trực tiếp ngữ cảnh câu nói (`[Needs Follow-up]`) để phát vấn câu hỏi phụ.
  - Trong **MF-04 (Thi thật)**: Kiểm tra cấu hình Follow-up do Trưởng Bộ Môn thiết lập cho Môn thi / Ca thi thật: `has_follow_up = true` và `max_follow_up_questions` (1–5 câu, mặc định 2 câu). Thẩm quyền cấu hình Follow-up thi thật thuộc 100% về Trưởng Bộ Môn (Khảo thí không được phép can thiệp). Bản nộp âm thanh lưu tại `exam_question_submissions` ghi nhận trọn vẹn âm thanh của cả câu gốc và câu phụ vào file `STT_MSSV.webm` trên Cloudflare R2 kèm mã băm SHA-256. Bóc băng Whisper lưu transcript và timestamps tại `transcript_whisper` và `timestamps_whisper`.

#### 2. Bộ Quy Tắc Kích Hoạt Follow-up Chuẩn Hóa (R1, R2, R4):
- 🎯 **Đối với Luồng 1 — Luyện tập tự do (MF-01):**
  - **Giảng viên KHÔNG cấu hình follow-up** trong MF-01.
  - **Admin cấu hình hệ thống:** Admin là người duy nhất cấu hình số lượng câu hỏi follow-up cho hệ thống luyện tập (`max_follow_up_questions` từ 1–5 câu, mặc định **2 câu**); số câu tiến trình `progressive` (`MinMixedPracticeQuestions = 3`, `MaxMixedPracticeQuestions = 10`); và thời gian timeout không tương tác (`SessionInactivityTimeoutMinutes = 5`, mặc định 5 phút).
  - **Quy tắc chi tiết theo 2 chế độ làm bài:**
    1. **Luyện từng câu theo yêu cầu (`[Per-Question On-Demand]`):**
       - Sinh viên chọn mức độ đơn lẻ (`easy`, `medium`, `hard`) HOẶC bất kỳ tổ hợp nào (`["easy", "medium"]`, `["easy", "hard"]`, `["medium", "hard"]`, `["easy", "medium", "hard"]`).
       - **Không ép chốt trước số câu**: Hệ thống cấp câu 1 khi tạo phiên, sau đó sinh viên trả lời và gọi lấy câu tiếp theo theo nhu cầu (On-Demand qua `POST /api/v1/practice/sessions/{id}/next-question`) đến khi chủ động kết thúc phiên.
       - **Thuật toán Anti-3-Consecutive**: Trong bất kỳ 3 câu hỏi chính liên tiếp nào, tối đa chỉ có 2 câu cùng mức độ (nếu 2 câu liền trước cùng mức $D$, lần bốc tiếp theo loại trừ $D$, chuyển sang mức khác trong danh sách đã chọn). Không lặp lại câu đã làm trong phiên (`Id NOT IN (...)`).
       - Kích hoạt câu hỏi chuyên sâu đào sâu (A2) khi điểm câu trả lời rơi vào khoảng ranh giới **$4.0 \le \text{Score} \le 8.0$** (1–5 câu do Admin cấu hình, mặc định 2 câu). Bỏ qua khi $\text{Score} < 4.0$ hoặc $\text{Score} > 8.0$.
    2. **Luyện trọn gói theo tiến trình (`[Full-Session Progressive]`):**
       - Sinh viên nhập số lượng từ **3 đến 10 câu** (ràng buộc bởi `MinMixedPracticeQuestions = 3` và `MaxMixedPracticeQuestions = 10`).
       - Hệ thống tự động chia đều số câu cho 3 mức (Dễ, Trung bình, Khó), bốc trọn gói toàn bộ ngay khi khởi tạo phiên và sắp xếp thứ tự phát vấn tăng dần từ Dễ $\to$ Trung bình $\to$ Khó.
       - **TUYỆT ĐỐI KHÔNG có câu hỏi follow-up**. Hỗ trợ nộp từng câu hoặc nộp trọn gói qua `batch-answers` để AI chấm tổng thể và trả bảng điểm tổng kết.
    3. **Cơ chế Timeout 5 phút không tương tác (Session Inactivity Timeout):**
       - Quản lý qua `system_configs` với `SessionInactivityTimeoutMinutes = 5` (mặc định 5 phút).
       - Cập nhật `last_activity_at` (TIMESTAMPTZ) sau mỗi tương tác (tạo phiên, nộp câu trả lời, lấy câu tiếp theo).
       - Quá 5 phút không tương tác: Hệ thống tự động hoàn tất phiên an toàn (`status = 'completed'`), bảo toàn điểm các câu đã làm (với Full-Session: các câu chưa làm tính là 0 điểm). Mọi request tiếp theo trả về **HTTP 410 Gone** (Session Timed Out).
- ⏱️ **Đối với Luồng 2 — Thi thử tính giờ (MF-02):**
  - Sinh viên truy cập Student Portal, chọn môn học để thi thử.
  - Trước khi bấm bắt đầu làm bài, sinh viên được **chủ động tự chọn chế độ**:
    1. `Có Follow-up` (AI hỏi chuyên sâu ngữ cảnh đào sâu).
    2. `Không Follow-up` (Làm đề thi thẳng tính giờ bình thường).
  - Nếu chọn có follow-up, AI sẽ phát vấn thêm câu hỏi phụ qua TTS và micro trong quá trình làm bài khi phát hiện ngữ cảnh cần đào sâu (`[Needs Follow-up]`). Hạn ngạch thi thử theo môn `max_mock_exams_per_day` do Admin cấu hình (vượt hạn ngạch trả `HTTP 429 Too Many Requests`). Nguồn đề rút từ kho `practice_questions`.
- 🏛️ **Đối với Luồng 4 — Thi thật phòng Lab Kiosk (MF-04):**
  - **100% Thẩm Quyền Cấu Hình Follow-up Thuộc Về Trưởng Bộ Môn (`department_head`):**
    - Trưởng Bộ Môn có thẩm quyền duy nhất quyết định môn thi đó có hỏi Follow-up hay không (`has_follow_up`) và cấu hình số lượng câu hỏi follow-up (`max_follow_up_questions` từ 1–5 câu, mặc định 2 câu) áp dụng cho ca thi/đề thi thật.
    - Cán bộ Khảo thí (`examination_officer`) chịu trách nhiệm tổ chức hành chính ca thi (phòng lab, kíp thi, phân công giám thị `proctor`, mở xuất bản đề thi), **tuyệt đối không được phép can thiệp vào cấu hình follow-up học thuật**.
  - **Quy tắc hỏi phụ:** Kích hoạt khi đề thi/môn thi trong kỳ thi được Trưởng Bộ Môn bật `has_follow_up = true` VÀ AI phát hiện câu trả lời của thí sinh có luận điểm cần đào sâu phản biện trong văn bản transcript (`[Needs Follow-up]`), tối đa 1–5 câu (mặc định 2 câu) do Trưởng Bộ Môn thiết lập. Không phụ thuộc điểm số.

---

### <a id="phan-64"></a>6.4. Sơ Đồ Chi Tiết Từng Luồng Nghiệp Vụ (Detailed Activity & Flowchart Diagrams)

> Toàn bộ sơ đồ hoạt động liên làn (OMG UML 2.5 Swimlane Activity Diagrams) và lưu đồ thuật toán chuyên sâu (Backend Algorithmic Flowcharts) dưới đây được chuẩn hóa và đồng bộ 100% với bộ 5 tệp sơ đồ `.drawio` tại [`docs/05_diagrams/`](../05_diagrams/) (Master file: `CAPSTONE_FA26SE166_4_MAINFLOWS_ACTIVITY_DIAGRAMS.drawio`) và tài liệu đặc tả chi tiết [`MO_TA_4_LUONG_MAINFLOW_NGHIEP_VU.md`](MO_TA_4_LUONG_MAINFLOW_NGHIEP_VU.md). Mỗi luồng nghiệp vụ được phân tách rõ ràng thành 3 phần:
> - **Phần A — Sơ Đồ Hoạt Động Liên Làn (OMG UML 2.5 Swimlane Activity Diagram):** Phân chia làn trách nhiệm chặt chẽ giữa Thí sinh, Giao diện Client, Backend API, Worker hàng đợi, Giám thị / Giảng viên và Trưởng Bộ Môn theo đúng chuẩn OMG UML 2.5.
> - **Phần B — Lưu Đồ Thuật Toán Chuyên Sâu (Algorithmic Flowchart):** Bóc tách chi tiết từng bước thuật toán, rẽ nhánh điều kiện bảo vệ (guards), mã HTTP phản hồi và xử lý ngoại lệ Zero Data Loss.
> - **Phần C — Vòng Đời Trạng Thái (State Machine Diagram):** Định nghĩa tường minh các trạng thái thực thể trong CSDL từ khi khởi tạo đến khi niêm phong kết thúc.

#### <a id="phan-641"></a>6.4.1. MF-01: Luyện Tập Tương Tác Tự Do (Interactive Practice)

##### 1. Tóm Tắt Nghiệp Vụ & Chốt Chặn Kỹ Thuật (MF-01)
* **Chế độ Upfront:** Sinh viên chọn 1 trong 2 chế độ ngay từ đầu:
  - `[Per-Question On-Demand]`: Chọn đơn hoặc bất kỳ tổ hợp mức độ (`easy`, `medium`, `hard`). Cấp câu 1 khi tạo phiên, sau đó gọi `POST /api/v1/practice/sessions/{id}/next-question` On-Demand để bốc câu tiếp theo (không ép chốt trước tổng số câu). Áp dụng **thuật toán Anti-3-Consecutive**: tối đa 2 câu cùng mức độ trong chuỗi 3 câu liên tiếp; loại trừ câu đã làm (`Id NOT IN (...)`). Kích hoạt câu hỏi chuyên sâu đào sâu (A2) khi $4.0 \le \text{Score} \le 8.0$ (tối đa 1–5 câu do Admin cấu hình qua `system_configs`, mặc định 2 câu).
  - `[Full-Session Progressive]`: Nhập số lượng 3–10 câu (`MinMixedPracticeQuestions = 3`, `MaxMixedPracticeQuestions = 10`), hệ thống tự động chia đều số câu cho 3 mức Dễ $\to$ Trung bình $\to$ Khó bốc trọn gói và phát vấn tăng dần (nếu thiếu câu trả `HTTP 400 Bad Request`). **TUYỆT ĐỐI KHÔNG có follow-up**. Nộp từng câu hoặc nộp trọn gói qua `POST /batch-answers`.
* **Inactivity Timeout 5 phút:** Quản lý qua `SessionInactivityTimeoutMinutes = 5`, cập nhật `last_activity_at = NOW()`. Quá 5 phút không tương tác, tự động hoàn tất an toàn (`status = "completed"`), bảo toàn 100% điểm các câu đã làm (Full-Session tính các câu chưa làm 0đ). Thao tác tiếp theo trả **HTTP 410 Gone**.
* **Đệm hiệu đính Code-Switching:** Màn hình đệm 10–300s (mặc định 60s do Admin cấu hình qua `TranscriptBufferSeconds`) để sửa thuật ngữ SE tiếng Anh. **KHÔNG lưu audio**, chỉ lưu transcript vào `practice_answers`.
* **Phòng thủ 4 tầng Zero Data Loss:** Tầng 1 Persist-First ghi CSDL `PENDING` ($< 100$ms) $\to$ Tầng 2 BoundedChannel 1,000 slots RAM $\to$ Tầng 3 Gemini 1.5 Flash chấm Rubric 10.0đ (Polly Retry 2-4-8s) $\to$ Tầng 4 DLQ `dead_letter_queues` chấm bù sau 5 phút.

##### 2. Phần A — Sơ Đồ Hoạt Động Liên Làn (OMG UML 2.5 Swimlane Activity Diagram - 4 Phân Làn, 43 Đỉnh)

```mermaid
flowchart TD
    subgraph Lane1 ["Lane 1: Student"]
        INIT((●))
        A01(["Select Subject & Config Upfront\n(Per-Question vs Progressive 3-10)"])
        D02{"Input Modality?"}
        A06(["Speak Voice Answer\ninto Microphone"])
        A09(["Review Transcript Buffer (10-300s)\n(Edit SE Code-Switching Terms)"])
        M01{"◇"}
        A10(["Confirm Submission of Answer"])
        A26(["Submit Batch All Answers\n(POST /batch-answers, Update Activity)"])
        D07{"Accept A2?"}
        A20(["Review Rubric Feedback\n& Gap Analysis"])
        D08{"Next Step?"}
        A25(["Complete Practice Session\n(Update Status = COMPLETED)"])
        F04(((◉)))
        A29(["Review Full Progressive Session\n& Access Student Portal History"])
        F05(((◉)))
    end

    subgraph Lane2 ["Lane 2: React 19 Client UI & Audio/STT"]
        A02(["Initialize Practice Session\n(Connect SignalR /hubs/practice)"])
        A05(["Display Question Card\n& Web Speech TTS Audio"])
        A08(["Transcribe Stream Realtime\n(Web Speech STT < 0.5s, No Audio)"])
        D03{"Session Mode?"}
        A11(["Cache Answer Locally\n(Advance question index i = i + 1)"])
        D04{"Questions Done?"}
        D06{"Score in 4.0-8.0?"}
        A18(["Prompt Deep-Dive Follow-up A2\n(Target Lowest Rubric Criterion)"])
        A19(["Open Modal Scorecard\n& Web Speech TTS AI Feedback"])
        A28(["Render Batch Summary\n& Full Rubric Analytics"])
    end

    subgraph Lane3 ["Lane 3: .NET 8 Backend API & PracticeHub"]
        A03(["Query Question Pool\n(Set last_activity_at = NOW())"])
        D01{"Pool Sufficient?"}
        A04(["Return HTTP 400 Bad Request\n(Pool Insufficient for Progressive)"])
        F01((⊗))
        A12(["Persist-First PENDING (< 100ms)\n(HTTP 202 Accepted, Zero Audio)"])
        OBJ_PENDING["[practice_answers: PENDING]"]
        M02{"◇"}
        A13(["Enqueue EvaluationTask\n(BoundedChannel 1,000 Slots RAM)"])
        D05{"Polly Status?"}
        A15(["Isolate to Dead-Letter Queue\n(dead_letter_queues, 5m Auto-Retry)"])
        F02((⊗))
        A17(["Save GRADED Status\n& Broadcast via /hubs/practice"])
        D09{"Inactive > 5m?"}
        A23(["Auto-Complete Session\n& Return HTTP 410 Gone"])
        F03((⊗))
        A24(["Fetch Next Anti-3-Consecutive\n(Max 2 same diff in 3, Id NOT IN)"])
        A27(["Save Batch Results\n& Broadcast Complete SignalR"])
    end

    subgraph Lane4 ["Lane 4: Gemini AI Grading & Worker Queue"]
        A16(["Gemini AI Grade CoT\n(Check Rubric 10.0, Extract CoT)"])
    end

    INIT --> A01
    A01 --> A02
    A02 --> A03
    A03 --> D01
    D01 -->|"[Insufficient Questions]"| A04
    D01 -->|"[Questions Ready]"| A05
    A04 --> F01
    A05 --> D02
    D02 -->|"[Speak Voice]"| A06
    D02 -->|"[Type Text]"| M01
    A06 --> A08
    A08 --> A09
    A09 --> M01
    M01 --> A10
    A10 --> D03
    D03 -->|"[Per-Question On-Demand]"| A12
    D03 -->|"[Full-Session Progressive]"| A11
    A12 --> OBJ_PENDING
    OBJ_PENDING --> M02
    A11 --> D04
    D04 -->|"[More Questions (i < N)]"| A05
    D04 -->|"[All Answered (i == N)]"| A26
    A26 --> M02
    M02 --> A13
    A13 --> D05
    D05 -->|"[Retry Failed > 3]"| A15
    D05 -->|"[Polly Success]"| A16
    A15 --> F02
    A16 --> A17
    A17 --> D06
    D06 -->|"[4.0 <= Score <= 8.0 & Count < Max]"| A18
    D06 -->|"[Score < 4.0 or > 8.0 or Max Reached]"| A19
    A18 --> D07
    D07 -->|"[Accept Deep-Dive]"| A05
    D07 -->|"[Skip Follow-up]"| A19
    A19 --> A20
    A20 --> D08
    D08 -->|"[Next Question]"| D09
    D08 -->|"[Finish Practice]"| A25
    D09 -->|"[Timeout > 5m]"| A23
    D09 -->|"[Active <= 5m]"| A24
    A23 --> F03
    A24 --> A05
    A25 --> F04
    A17 --> A27
    A27 --> A28
    A28 --> A29
    A29 --> F05
```

##### 3. Phần B — Lưu Đồ Thuật Toán Chuyên Sâu (Algorithmic Flowchart)

```mermaid
flowchart TD
    M1_Req(["Nhận HTTP Request tại PracticeController"]) --> M1_CheckSession{"Phiên đã tồn tại trong DB?"}

    M1_CheckSession -- "Tạo phiên mới (POST /sessions)" --> M1_CheckMode{"isFullSession == true?"}
    M1_CheckMode -- "Đúng (Full-Session Progressive)" --> M1_ValidCount{"3 <= questionCount <= 10 (Min/MaxMixedPracticeQuestions)?"}
    M1_ValidCount -- "Sai" --> M1_Err400(["Trả HTTP 400 Bad Request: Số câu phải từ 3 đến 10"])
    M1_ValidCount -- "Đúng" --> M1_DrawProg["Chia đều số câu theo 3 bậc: Easy -> Medium -> Hard từ kho practice_questions (status = APPROVED)"]
    M1_DrawProg --> M1_CheckEnough{"Kho đủ số lượng câu hỏi cho cả 3 bậc?"}
    M1_CheckEnough -- "Thiếu câu hỏi" --> M1_Err400
    M1_CheckEnough -- "Đủ câu hỏi" --> M1_CreateFull["INSERT practice_sessions (is_full_session = true, has_follow_up = false, last_activity_at = NOW())"]
    M1_CreateFull --> M1_ReturnInit(["Trả HTTP 200 OK kèm danh sách 3-10 câu hỏi tăng dần"])

    M1_CheckMode -- "Sai (Per-Question On-Demand)" --> M1_DrawFirst["Bốc ngẫu nhiên Câu 1 từ practice_questions theo tập độ khó SV chọn (easy / medium / hard)"]
    M1_DrawFirst --> M1_CreatePerQ["INSERT practice_sessions (is_full_session = false, last_activity_at = NOW())"]
    M1_CreatePerQ --> M1_ReturnInit

    M1_CheckSession -- "Thao tác trên phiên đang mở" --> M1_CheckInactivity{"NOW() - session.last_activity_at > 5 phút (SessionInactivityTimeoutMinutes)?"}
    M1_CheckInactivity -- "Đúng (Quá hạn 5 phút)" --> M1_CloseSession["UPDATE practice_sessions SET status = completed (Bảo toàn điểm câu đã làm; câu chưa làm Full-Session tính 0đ)"]
    M1_CloseSession --> M1_Err410(["Trả HTTP 410 Gone (Session Timed Out)"])

    M1_CheckInactivity -- "Sai (Trong hạn 5 phút)" --> M1_Touch["UPDATE practice_sessions SET last_activity_at = NOW()"]
    M1_Touch --> M1_ActionType{"Phân loại Endpoint gọi vào?"}

    M1_ActionType -- "POST /sessions/{id}/next-question" --> M1_GetLast2["Truy vấn 2 câu hỏi chính liền trước trong phiên (is_follow_up == false)"]
    M1_GetLast2 --> M1_IsSameDiff{"Cả 2 câu liền trước cùng độ khó D và SV chọn > 1 độ khó?"}
    M1_IsSameDiff -- "Đúng (Vi phạm ngưỡng 3 câu liên tiếp)" --> M1_ExcludeDiff["Loại trừ độ khó D khỏi candidate pool (Thuật toán Anti-3-Consecutive)"]
    M1_IsSameDiff -- "Sai" --> M1_KeepDiff["Giữ nguyên tập độ khó đang chọn"]
    M1_ExcludeDiff --> M1_QueryPool["SELECT từ practice_questions WHERE CourseId = @cid AND Id NOT IN (AnsweredIds) AND Difficulty IN (AllowedDiffs)"]
    M1_KeepDiff --> M1_QueryPool
    M1_QueryPool --> M1_PoolEmpty{"Tìm thấy câu hỏi ở mức độ ưu tiên?"}
    M1_PoolEmpty -- "Cạn mức ưu tiên" --> M1_FallbackPool["Fallback: Tìm trong các mức độ khó còn lại chưa làm (WHERE Id NOT IN AnsweredIds)"]
    M1_PoolEmpty -- "Tìm thấy" --> M1_RetNext(["Trả HTTP 200 OK (question, hasMoreQuestions = true)"])
    M1_FallbackPool --> M1_StillEmpty{"Vẫn hết sạch câu hỏi trong kho?"}
    M1_StillEmpty -- "Còn câu" --> M1_RetNext
    M1_StillEmpty -- "Hết sạch kho" --> M1_RetNoMore(["Trả HTTP 200 OK (question = null, hasMoreQuestions = false)"])

    M1_ActionType -- "POST /submit hoặc /batch-answers" --> M1_Tier1["Tầng 1 (Persist-First < 100ms): INSERT practice_answers (status = pending, chỉ lưu transcript, KHÔNG lưu audio)"]
    M1_Tier1 --> M1_Tier2["Tầng 2 (RAM Queue): Enqueue vào BoundedChannel 1,000 slots (FullMode.Wait) & Trả HTTP 202 Accepted"]
    M1_Tier2 --> M1_Tier3{"Tầng 3 (Polly Retry): Gọi Gemini 1.5 Flash chấm Rubric 10.0 (Retry 2s -> 4s -> 8s)"}
    M1_Tier3 -- "Lỗi sau 3 lần thử" --> M1_Tier4["Tầng 4 (DLQ): INSERT dead_letter_queues (status = PENDING_RETRY, DlqReplayWorker quét lại sau 5 phút)"]
    M1_Tier3 -- "Chấm thành công" --> M1_EvalFU{"!session.IsFullSession VÀ 4.0 <= TotalScore <= 8.0 VÀ FollowUpCount < MaxFollowUp?"}
    M1_EvalFU -- "Thỏa mãn" --> M1_Weakest["Tìm RubricCriterion có tỷ lệ (EarnedScore / MaxScore) thấp nhất -> Sinh câu hỏi phụ A2 xoáy sâu"]
    M1_Weakest --> M1_BroadcastFU(["Lưu ai_evaluations & Bắn SignalR Scorecard kèm Follow-up A2"])
    M1_EvalFU -- "Không thỏa mãn" --> M1_BroadcastDone(["Lưu ai_evaluations & Bắn SignalR Scorecard hoàn chỉnh"])
```

##### 4. Phần C — Vòng Đời Trạng Thái (State Machine Diagram)

```mermaid
stateDiagram-v2
    [*] --> ACTIVE: Tạo phiên (Per-Question hoặc Full-Session 3..10 câu)
    ACTIVE --> BUFFER_EDITING: SV dừng nói -> Đếm ngược TranscriptBufferSeconds (10-300s, mặc định 60s)
    BUFFER_EDITING --> ANSWER_PENDING: Nộp transcript (Persist-First < 100ms vào practice_answers)
    ANSWER_PENDING --> AI_GRADING: GradingQueueWorker rút task từ BoundedChannel 1,000 slots
    AI_GRADING --> PENDING_RETRY_DLQ: Lỗi API sau 3 lần Polly Retry (2s, 4s, 8s)
    PENDING_RETRY_DLQ --> AI_GRADING: DlqReplayWorker chấm bù sau 5 phút
    AI_GRADING --> FOLLOW_UP_REQUIRED: Per-Question & 4.0 <= Score <= 8.0 (Sinh câu hỏi A2 theo weakestCriterion)
    FOLLOW_UP_REQUIRED --> BUFFER_EDITING: SV trả lời câu hỏi phụ A2
    AI_GRADING --> QUESTION_COMPLETED: Ngoài khoảng [4.0..8.0] hoặc đã hết lượt Follow-up hoặc chế độ Full-Session
    QUESTION_COMPLETED --> ACTIVE: Bấm Câu tiếp theo (POST /next-question, kiểm tra Anti-3-Consecutive)
    ACTIVE --> TIMED_OUT_410: Quá 5 phút không tương tác (last_activity_at) -> Trả HTTP 410 Gone
    QUESTION_COMPLETED --> TIMED_OUT_410: Quá 5 phút mới bấm Câu tiếp theo -> Trả HTTP 410 Gone
    TIMED_OUT_410 --> COMPLETED: Tự động đóng phiên & Bảo toàn điểm các câu đã làm
    QUESTION_COMPLETED --> COMPLETED: SV bấm Kết thúc hoặc hết câu hỏi (hasMoreQuestions = false)
    COMPLETED --> [*]
```

---

#### <a id="phan-642"></a>6.4.2. MF-02: Thi Thử Vấn Đáp Bấm Giờ (Timed Mock Exam)

##### 1. Tóm Tắt Nghiệp Vụ & Chốt Chặn Kỹ Thuật (MF-02)
* **Hạn ngạch thi thử (Daily Quota Guard):** Admin cấu hình số lượt thi tối đa theo môn trong ngày (`max_mock_exams_per_day`), không khóa cứng K=3. Khi `used_attempts >= max_mock_exams_per_day`, trả ngay **HTTP 429 Too Many Requests**, khóa nút thi thử và điều hướng sang MF-01.
* **Cấu trúc đề & Phân bổ Bloom:** Trưởng Bộ Môn cấu hình cấu trúc đề thi thử (ExamStructure: thời lượng phút, tổng số câu, tỷ lệ Bloom) theo từng môn học. Nguồn đề rút từ kho `practice_questions` (cô lập 100% với kho `exam_questions` của MF-04).
* **Chế độ Follow-up tự chọn:** Sinh viên chỉ chọn môn học và chủ động chọn `Có Follow-up` hoặc `Không Follow-up` (không chọn topic hay độ khó). Nếu chọn có follow-up, AI phân tích ngữ cảnh bài nói để đào sâu (`[Needs Follow-up]`), không phụ thuộc điểm số; thời lượng ca thi có follow-up dài hơn do Trưởng BM cấu hình.
* **Voice-First Gate & Đồng hồ kép:** Bắt buộc micro, khóa cứng 100% ô gõ phím. Server Master Timer (chống sửa giờ máy trạm) chạy song song với Client Countdown Timer. Hết giờ (`00:00`) tự động nộp bài: **câu chưa làm tính là bỏ trống, không chấm**. **KHÔNG lưu audio**, chỉ lưu transcript vào `mock_exam_answers`.
* **Dual-Path Grading & Instant Scorecard:** Fast-Path ($< 10$s) chấm tức thì qua Gemini 1.5 Flash hoặc Async Fallback qua BoundedChannel 1,000 slots. Trả Instant Feedback Scorecard chi tiết từng tiêu chí Rubric theo CLO tại màn hình kết quả và lưu vĩnh viễn vào `mock_exam_sessions` phục vụ tra cứu Lịch sử thi (FE-02).

##### 2. Phần A — Sơ Đồ Hoạt Động Liên Làn (OMG UML 2.5 Swimlane Activity Diagram - 4 Phân Làn, 45 Đỉnh)

```mermaid
flowchart TD
    subgraph Lane1 ["Lane 1: Student"]
        M_INIT((●))
        M01(["Select Subject & Follow-up Mode\n(No topic selection; Time longer if Follow-up)"])
        M06(["Confirm Limit & Redirect\n(Go to Student Portal or MF-01)"])
        MF01((⊗))
        M11(["Speak Viva Voice Answer\ninto Headset Microphone"])
        M14(["Fix Code-Switching SE Terms\n(Software Engineering Vocabulary)"])
        M15(["Save Answer Transcript Record\n(into mock_exam_answers, Zero Audio)"])
        M18(["Speak Follow-up Answer\ninto Microphone"])
        M21(["Fix Follow-up Terms & Confirm"])
        M22(["Active Submit Exam\n(Student Clicks Submit All)"])
        M32(["Acknowledge Notice & Exit Exam"])
        MF02_ASYNC((⊗))
        M29(["Review Scorecard & Insights\n(Saved to Exam History FE-02)"])
        MF02_FINAL(((◉)))
    end

    subgraph Lane2 ["Lane 2: React 19 Client UI & Voice-First Gate"]
        M02(["Request Quota Check\n(POST /api/v1/mock-exams/check-quota)"])
        M04(["Return HTTP 429 Too Many Requests\n(Daily Quota Limit Reached Modal)"])
        FORK_TIMERS["═════ FORK BAR (Khởi chạy đồng hồ kép song song) ═════"]
        M09(["Start Client Countdown Timer"])
        JOIN_TIMERS["═════ JOIN BAR (Đồng bộ đồng hồ đếm ngược) ═════"]
        M10(["Voice-First Gate: Lock Keyboard 100%\n(Enforce Microphone Answer Only)"])
        M12(["Transcribe Stream Realtime\n(Web Speech STT < 0.5s, No Audio R2)"])
        M13(["Unlock Buffer Review (10-300s, def 60s)\n(Countdown & Enable Keyword Edit)"])
        M17(["Web Speech TTS Plays\nContextual Follow-up Question"])
        M19(["Transcribe Follow-up Stream\n(No Audio Recording)"])
        M20(["Unlock Buffer for Follow-up\n(TranscriptBufferSeconds)"])
        M_MERGE_DONE{"◇"}
        MD05{"Exam Progress?"}
        M23(["AutoSubmitGuard on 00:00\n(Unanswered questions left blank, unrated)"])
        M_MERGE_SUB{"◇"}
        M24(["Send Submission Package\n(POST /mock-exams/sessions/{id}/submit)"])
        M31(["Display Submission Sealed Notice\n(Scorecard available in Exam History FE-02)"])
        M28(["Open Modal Instant Scorecard\n(Rubric CLO Breakdown & Guidance)"])
    end

    subgraph Lane3 ["Lane 3: .NET 8 Backend API & Quota Handler"]
        M03(["Query mock_exam_quotas\n(Check Course.MaxMockExamsPerDay)"])
        MD01{"Quota Available?"}
        M07(["Deduct Quota (K = K + 1)\n& Fetch Bloom from practice_questions"])
        M08(["Start Server Master Timer\n(Anti-Tampering Record)"])
        MD02{"Has Follow-up?"}
        MD03{"AI Needs Follow-up?"}
        MD04{"Follow-up < Max?"}
        M25(["Persist-First PENDING < 100ms\n(HTTP 202 Accepted, Zero Audio)"])
        MD06{"System Load?"}
        M30(["Enqueue Async Background Task\n(GradingQueueWorker Channel)"])
        M27(["Save GRADED Status & CLO Scorecard\n& Broadcast via SignalR"])
    end

    subgraph Lane4 ["Lane 4: Gemini AI Grading Engine"]
        M16(["Gemini AI Contextual Probe\n(Evaluate Context, Not bound to 4.0-8.0)"])
        M26(["Gemini AI Instant Grading CoT\n(Grade all answered Qs on Rubric 10.0)"])
    end

    M_INIT --> M01
    M01 --> M02
    M02 --> M03
    M03 --> MD01
    MD01 -->|"[Quota Exceeded (HTTP 429)]"| M04
    MD01 -->|"[Quota Available (K < Limit)]"| M07
    M04 --> M06
    M06 --> MF01
    M07 --> FORK_TIMERS
    FORK_TIMERS --> M09
    FORK_TIMERS --> M08
    M09 --> JOIN_TIMERS
    M08 --> JOIN_TIMERS
    JOIN_TIMERS --> M10
    M10 --> M11
    M11 --> M12
    M12 --> M13
    M13 --> M14
    M14 --> M15
    M15 --> MD02
    MD02 -->|"[HasFollowUp == true]"| M16
    MD02 -->|"[HasFollowUp == false]"| M_MERGE_DONE
    M16 --> MD03
    MD03 -->|"[Needs Follow-up (Contextual Probe)]"| M17
    MD03 -->|"[Good Answer - No Follow-up]"| M_MERGE_DONE
    M17 --> M18
    M18 --> M19
    M19 --> M20
    M20 --> M21
    M21 --> MD04
    MD04 -->|"[Count < Max]"| M_MERGE_DONE
    MD04 -->|"[Count >= Max]"| M_MERGE_DONE
    M_MERGE_DONE --> MD05
    MD05 -->|"[More Questions]"| M10
    MD05 -->|"[All Answered]"| M22
    MD05 -->|"[Timer 00:00]"| M23
    M22 --> M_MERGE_SUB
    M23 --> M_MERGE_SUB
    M_MERGE_SUB --> M24
    M24 --> M25
    M25 --> MD06
    MD06 -->|"[Fast-Path: Low Load (< 10s)]"| M26
    MD06 -->|"[Async Fallback: High Load]"| M30
    M26 --> M27
    M27 --> M28
    M28 --> M29
    M29 --> MF02_FINAL
    M30 --> M31
    M31 --> M32
    M32 --> MF02_ASYNC
```

##### 3. Phần B — Lưu Đồ Thuật Toán Chuyên Sâu (Algorithmic Flowchart)

```mermaid
flowchart TD
    M2_Start(["Sinh viên gửi POST /api/v1/mock-exams/sessions { courseId, hasFollowUp }"]) --> M2_ReadCfg["Truy vấn Course.MaxMockExamsPerDay (do Admin cấu hình theo môn) & bảng mock_exam_quotas (quota_date = TODAY)"]
    M2_ReadCfg --> M2_QuotaCheck{"used_attempts >= Course.MaxMockExamsPerDay?"}

    M2_QuotaCheck -- "Đúng (Vượt hạn ngạch ngày)" --> M2_Block429(["Trả HTTP 429 Too Many Requests -> Giao diện khóa nút thi thử & Điều hướng sang MF-01"])
    M2_QuotaCheck -- "Sai (Còn lượt hợp lệ)" --> M2_LoadStruct["Đọc cấu hình đề thi thử của môn (ExamStructure: Thời lượng phút, tổng số câu, tỷ lệ Bloom do Trưởng BM thiết lập)"]
    M2_LoadStruct --> M2_SamplePool["Rút ngẫu nhiên N câu từ kho practice_questions (status = APPROVED, cô lập 100% với kho exam_questions)"]
    M2_SamplePool --> M2_InitSession["Tăng used_attempts + 1; INSERT mock_exam_sessions (status = in_progress, has_follow_up, started_at = NOW, expected_end_time = NOW + Duration)"]

    M2_InitSession --> M2_QuestionLoop["Duyệt qua từng câu hỏi i (1 <= i <= N) trên giao diện phòng thi"]
    M2_QuestionLoop --> M2_LockInput["VOICE-FIRST GATE: Khóa cứng ô nhập liệu Textarea (disabled = true)"]
    M2_LockInput --> M2_HasAudio{"Trình duyệt phát hiện luồng thu âm Micro hợp lệ & STT trả transcript?"}
    M2_HasAudio -- "Chưa thu âm" --> M2_CheckTimer1{"Đồng hồ chạm mốc expected_end_time?"}
    M2_CheckTimer1 -- "Chưa hết giờ" --> M2_LockInput
    M2_CheckTimer1 -- "Hết giờ làm bài" --> M2_AutoSubmit["Kích hoạt Auto-Submit: Thu bài tự động ngay lập tức"]

    M2_HasAudio -- "Đã thu âm xong" --> M2_UnlockEdit["Mở khóa Textarea trong khoảng TranscriptBufferSeconds (10-300s, mặc định 60s do Admin cấu hình)"]
    M2_UnlockEdit --> M2_CheckFUFlag{"session.has_follow_up == true?"}
    M2_CheckFUFlag -- "Đúng (SV chọn Có Follow-up)" --> M2_ContextCheck{"AI đánh giá nhanh ngữ cảnh: Luận điểm cần đào sâu phản biện [Needs Follow-up]?"}
    M2_ContextCheck -- "Cần phản biện (Tối đa 1-5 câu, mặc định 2)" --> M2_AskContextFU["Phát câu hỏi phụ qua TTS -> SV trả lời qua Micro (Voice-First) -> Lưu transcript phụ"]
    M2_ContextCheck -- "Trả lời đã rõ ý" --> M2_SaveTemp["Ghi nhận câu trả lời i vào bộ nhớ phiên (KHÔNG lưu file audio R2)"]
    M2_CheckFUFlag -- "Sai (SV chọn Không Follow-up)" --> M2_SaveTemp
    M2_AskContextFU --> M2_SaveTemp

    M2_SaveTemp --> M2_MoreQ{"Còn câu hỏi tiếp theo VÀ chưa hết giờ?"}
    M2_MoreQ -- "Còn câu & Còn giờ" --> M2_QuestionLoop
    M2_MoreQ -- "Đã làm xong hoặc Hết giờ" --> M2_AutoSubmit

    M2_AutoSubmit --> M2_FilterAns["Persist-First (< 100ms): Lưu mock_exam_answers (Câu chưa trả lời đánh dấu bỏ trống, tuyệt đối KHÔNG đưa vào chấm AI)"]
    M2_FilterAns --> M2_Dispatch{"Dual-Path Grading Dispatcher: Kiểm tra độ trễ AI (< 10s)?"}
    M2_Dispatch -- "Fast-Path (Phản hồi trong 10s)" --> M2_SyncGrade["Gemini 1.5 Flash chấm toàn bộ câu đã làm theo Rubric 10.0 -> Tổng hợp Scorecard theo CLO"]
    M2_SyncGrade --> M2_PushLive(["UPDATE mock_exam_sessions (status = completed) & Đẩy Instant Scorecard qua SignalR"])
    M2_Dispatch -- "Async Fallback (Quá 10s / Hàng đợi bận)" --> M2_QueueGrade["Đẩy vào BoundedChannel 1,000 slots + Polly Retry (2-4-8s) + DLQ -> Chấm ngầm hoàn tất"]
    M2_QueueGrade --> M2_SaveHistory(["UPDATE mock_exam_sessions (status = completed) & Lưu vĩnh viễn vào Lịch sử thi (GET /api/v1/mock-exams/history)"])
```

##### 4. Phần C — Vòng Đời Trạng Thái (State Machine Diagram)

```mermaid
stateDiagram-v2
    [*] --> QUOTA_CHECKING: SV bấm Bắt đầu thi thử (POST /sessions)
    QUOTA_CHECKING --> QUOTA_BLOCKED_429: used_attempts >= Course.MaxMockExamsPerDay (Chặn HTTP 429)
    QUOTA_BLOCKED_429 --> [*]: Điều hướng sang MF-01 hoặc Student Portal
    QUOTA_CHECKING --> IN_PROGRESS: used_attempts < MaxMockExamsPerDay (Trừ quota, bật Dual Timer)
    IN_PROGRESS --> VOICE_LOCKED: Hiển thị câu hỏi i, khóa phím 100%
    VOICE_LOCKED --> BUFFER_EDITING: Phát biểu xong -> Mở đệm sửa Code-Switching (10-300s)
    BUFFER_EDITING --> CONTEXT_FOLLOW_UP: has_follow_up == true & AI yêu cầu đào sâu
    CONTEXT_FOLLOW_UP --> BUFFER_EDITING: Trả lời câu hỏi phụ
    BUFFER_EDITING --> QUESTION_SAVED: Lưu transcript vào mock_exam_answers (Không lưu audio)
    QUESTION_SAVED --> VOICE_LOCKED: Chuyển câu tiếp theo
    QUESTION_SAVED --> SUBMITTED: Sinh viên bấm nộp bài hoặc Hết giờ 00:00 (Auto-Submit)
    VOICE_LOCKED --> SUBMITTED: Hết giờ 00:00 (Câu chưa làm tính bỏ trống, không chấm)
    SUBMITTED --> FAST_PATH_GRADING: Chấm nhanh AI (< 10s)
    SUBMITTED --> ASYNC_FALLBACK_QUEUE: Hệ thống tải cao -> Đẩy BoundedChannel 1000 slots
    FAST_PATH_GRADING --> COMPLETED: Đẩy Instant Scorecard qua SignalR
    ASYNC_FALLBACK_QUEUE --> COMPLETED: Chấm ngầm xong -> Lưu vào Lịch sử thi FE-02
    COMPLETED --> [*]
```

---

#### <a id="phan-643"></a>6.4.3. MF-03: Quản Lý Ngân Hàng Câu Hỏi & Barem Rubric 10.0 (Question Studio)

##### 1. Tóm Tắt Nghiệp Vụ & Chốt Chặn Kỹ Thuật (MF-03)
* **Hai phương thức khởi tạo:**
  1. *Sinh câu hỏi từ CLO môn học (`course_clos` 3NF):* Giảng viên và Trưởng BM lấy danh sách chuẩn đầu ra từ kho `course_clos`, cấu hình Bloom 1-6 & độ khó $\to$ Gemini 1.5 Flash tự động sinh Đề bài, Barem Rubric $\sum \equiv 10.0$đ và Model Answer $\ge 50$ ký tự (`POST /api/v1/questions/generate-from-clo`) $\to$ Giảng viên xem trước (Preview) và tùy chỉnh theo barem riêng.
  2. *Soạn câu hỏi thủ công (`source == "manual"`):* Hỗ trợ đính kèm ảnh/sơ đồ kỹ thuật (PNG/JPG/WEBP $\le 5$MB). Kiểm tra nhị phân Magic Bytes (`HTTP 413 Payload Too Large` nếu $> 5$MB, `HTTP 415 Unsupported Media Type` nếu sai định dạng), stream lên Cloudflare R2 qua `POST /api/v1/questions/upload-image`, lưu `image_url` và bắt buộc nhập `image_description` để phục vụ chấm Gemini Multimodal CoT.
* **Cảnh báo trùng lặp tự động & Tick chọn 2 kho:** Hệ thống tự động so khớp độ tương đồng ngữ nghĩa; nếu phát hiện trùng lặp vượt ngưỡng cho phép, hệ thống tự động gắn cờ cảnh báo `isFlagged = true` để phục vụ hậu kiểm. Giảng viên tick chọn lưu vào `practice_questions` (cho MF-01, MF-02) và/hoặc `exam_questions` (cô lập tuyệt đối cho MF-04).
* **Barem Rubric chuẩn hóa:** $\sum \text{MaxScore}_j \equiv 10.0$đ và Model Answer $\ge 50$ ký tự. Client Guard cảnh báo đỏ và khóa nút; Backend FluentValidation chặn `HTTP 422 Unprocessable Entity` nếu vi phạm. Hỗ trợ chạy AI Calibration Simulator để hiệu chuẩn trọng số tiêu chí.
* **Quy trình Duyệt đề Bộ Môn & Đóng gói ExamSets:** Giảng viên gửi duyệt (`POST /api/v1/questions/batch-submit-review`) $\to$ `SUBMITTED_FOR_REVIEW`. **Trưởng Bộ Môn (`department_head`) là người duy nhất có thẩm quyền phê duyệt**:
  - Rà soát các câu hỏi bị gắn cờ trùng lặp `isFlagged = true`.
  - `APPROVED`: Phê duyệt chính thức, ACID commit vào kho câu hỏi mục tiêu.
  - `NEEDS_REVISION`: Yêu cầu sửa kèm `review_notes` $\ge 10$ ký tự, trả về Giảng viên chỉnh sửa.
  - `REJECTED`: Từ chối kèm lý do, loại bỏ hẳn khỏi ngân hàng đề.
  - Sau khi duyệt câu hỏi, Trưởng Bộ Môn định nghĩa khung `exam_structures` với `slot_definitions` (JSONB) và `required_set_count`, đóng gói các bộ đề `exam_sets` (`DRAFT` $\to$ `SUBMITTED_FOR_REVIEW` $\to$ `OFFICIAL` $\to$ `HANDED_OVER`) và bàn giao cho Cán bộ Khảo thí.

##### 2. Phần A — Sơ Đồ Hoạt Động Liên Làn (OMG UML 2.5 Swimlane Activity Diagram - 5 Phân Làn Tách Biệt, 60 Đỉnh)

```mermaid
flowchart TD
    subgraph Lane1 ["Lane 1: Lecturer (Question Author)"]
        Q01((●))
        Q02(["Access Question Bank & Rubric Studio"])
        Q_Browse_Filter(["Apply Filters (Course, Bloom 1-6, Difficulty, Approval Status)"])
        Q_Edit_Select(["Select Question to Edit (DRAFT or NEEDS_REVISION)"])
        Q_Man_Start(["Enter Question Content & Select Target Pools (practice/exam)"])
        D_Man_Image{"Attach Technical Diagram?"}
        M_Man_Img{"◇"}
        Q_CLO_Req(["Select Course & Target CLOs from course_clos (3NF)"])
        Q_CLO_Config(["Select Target CLOs, Bloom 1-6, Difficulty & Question Count"])
        Q_CLO_Customize(["Customize Rubric, Refine Model Ans & Tick Target Pools"])
        M_Author_Done{"◇"}
        Q_Rubric_Adjust(["Adjust Criteria Points to exactly 10.0"])
        D_Simulator{"Run AI Calibration Simulator?"}
        Q_Sim_Input(["Input Test Student Answer (High / Medium / Low)"])
        D_Sim_Check{"Criteria Weights Satisfactory?"}
        M_Sim_Done{"◇"}
        Q_Submit_Review(["Lecturer Submits Batch (POST /batch-submit-review)"])
        Q_Lec_Revise(["Lecturer Re-opens Question Form to Revise"])
        Q_Catalog_View(["View Updated Question Catalog (Ready for MF-01/02/04)"])
        Q_End_Final(((◉)))
    end

    subgraph Lane2 ["Lane 2: Question & Rubric Studio UI"]
        Q04(["Render Question Studio Workspace"])
        M_Studio{"◇"}
        D_AuthorMode{"Select Authoring Mode?"}
        Q_Browse_Render(["Display Paginated Question Grid & Details Modal"])
        Q_Edit_Form(["Populate Question Form & Revision Feedback"])
        Q_Man_Attach(["Attach Image URL & Description for Gemini Multimodal CoT"])
        Q_CLO_RenderTree(["Render Course CLO Tree & Target CLO Selector"])
        Q_CLO_Preview(["Render Draft Preview Studio & Side-by-Side Comparison"])
        Q_Rubric_Calc(["Calculate Real-time Total Points (∑ MaxScore_j)"])
        D_Rubric_Sum{"∑ == 10.0?"}
        Q_Rubric_Warn(["Show Red Invariant Warning & Disable Submit Actions"])
        Q_Sim_Show(["Display Calibration Scorecard & Feedback Analysis"])
        Q_Val_422(["Display 422 Validation Error Alert"])
        Q_UI_Revise(["Notify Lecturer with Revision Feedback (review_notes)"])
        Q_Sync_UI(["Display Approval Success Banner & Update Bank Catalog"])
    end

    subgraph Lane3 ["Lane 3: .NET 8 Backend API & R2"]
        Q03(["Verify RBAC Claims (Role in [lecturer, department_head])"])
        Q_Browse_Query(["Query Question Catalog & Rubrics (GET /api/v1/questions)"])
        Q_Edit_Fetch(["Fetch Question, Rubric & Revision Notes from DB"])
        Q_Man_Upload(["POST /upload-image: Check Magic Bytes (413/415) & Stream R2"])
        Obj_R2_Image["[Diagram Sealed on Cloudflare R2: image_url + image_desc]"]
        Q_CLO_Fetch(["GET /api/v1/courses/{courseId}/clos (Fetch CLOs from 3NF DB)"])
        Q_CLO_Dispatch(["POST /api/v1/questions/generate-from-clo (Dispatch Request)"])
        Q_Sim_Dispatch(["POST /api/v1/questions/simulate-grading"])
        Q_Dup_Check(["Semantic Duplicate Check: Flag isFlagged = true if duplicate"])
        Q_Val_Backend(["Backend FluentValidation: Validate ∑==10.0 & Ans>=50"])
        D_Val_Backend{"Valid ∑==10.0 & Ans>=50?"}
        Q_Persist_Review(["ACID Persist: Save DRAFT & Set Status = SUBMITTED_FOR_REVIEW"])
        Obj_State_Review["[Question & Rubric: SUBMITTED_FOR_REVIEW (isFlagged evaluated)]"]
        Q_DB_Revise(["Update Status = NEEDS_REVISION & Record review_notes in DB"])
        Q_DB_Reject(["Update Status = REJECTED, Save AuditLog & Discard Question"])
        End_Reject((⊗))
        Q_DB_Approve(["ACID: Status = APPROVED, Commit Target Pools & Invalidate Cache"])
        Obj_State_Approved["[Question & Rubric: APPROVED in Target Banks]"]
    end

    subgraph Lane4 ["Lane 4: Gemini AI Generator & Simulator"]
        Q_CLO_AIGen(["Gemini 1.5 Flash: Auto-Gen Questions, Model Ans & Rubric (∑=10.0) from CLO"])
        Q_Sim_AI(["Gemini 1.5 Flash: Grade Test Answer against Rubric & Trace CoT"])
    end

    subgraph Lane5 ["Lane 5: Department Head (Approver)"]
        Q_Dept_Open(["Open Approval Studio: View Pending (SUBMITTED_FOR_REVIEW)"])
        Q_Dept_Inspect(["Inspect Question Content, Duplicate Flags (isFlagged), Diagram, Model Ans & Rubric 10.0"])
        D_Dept_Decision{"Dept Head Decision?"}
        Q_Dept_Revise(["Enter Revision Notes (review_notes >= 10 chars) & Request"])
        Q_Dept_Reject(["Enter Rejection Reason (>= 10 chars) & Reject Question"])
        Q_Dept_Approve(["Approve Question Batch (POST /review-decision)"])
    end

    Q01 --> Q02
    Q02 --> Q03
    Q03 --> Q04
    Q04 --> M_Studio
    M_Studio --> D_AuthorMode
    D_AuthorMode -->|"[Browse / Filter Catalog]"| Q_Browse_Filter
    D_AuthorMode -->|"[Edit Existing Question]"| Q_Edit_Select
    D_AuthorMode -->|"[Manual Authoring]"| Q_Man_Start
    D_AuthorMode -->|"[Generate from Course CLO]"| Q_CLO_Req
    Q_Browse_Filter --> Q_Browse_Query
    Q_Browse_Query --> Q_Browse_Render
    Q_Browse_Render --> M_Studio
    Q_Edit_Select --> Q_Edit_Fetch
    Q_Edit_Fetch --> Q_Edit_Form
    Q_Edit_Form --> M_Author_Done
    Q_Man_Start --> D_Man_Image
    D_Man_Image -->|"[Attach Diagram]"| Q_Man_Upload
    D_Man_Image -->|"[No Diagram]"| M_Man_Img
    Q_Man_Upload --> Obj_R2_Image
    Obj_R2_Image --> Q_Man_Attach
    Q_Man_Attach --> M_Man_Img
    M_Man_Img --> M_Author_Done
    Q_CLO_Req --> Q_CLO_Fetch
    Q_CLO_Fetch --> Q_CLO_RenderTree
    Q_CLO_RenderTree --> Q_CLO_Config
    Q_CLO_Config --> Q_CLO_Dispatch
    Q_CLO_Dispatch --> Q_CLO_AIGen
    Q_CLO_AIGen --> Q_CLO_Preview
    Q_CLO_Preview --> Q_CLO_Customize
    Q_CLO_Customize --> M_Author_Done
    M_Author_Done --> Q_Rubric_Calc
    Q_Rubric_Calc --> D_Rubric_Sum
    D_Rubric_Sum -->|"[Sum != 10.0 (Invalid)]"| Q_Rubric_Warn
    D_Rubric_Sum -->|"[Sum == 10.0 (Valid)]"| D_Simulator
    Q_Rubric_Warn --> Q_Rubric_Adjust
    Q_Rubric_Adjust --> Q_Rubric_Calc
    D_Simulator -->|"[Run AI Simulator]"| Q_Sim_Input
    D_Simulator -->|"[Skip Simulator]"| M_Sim_Done
    Q_Sim_Input --> Q_Sim_Dispatch
    Q_Sim_Dispatch --> Q_Sim_AI
    Q_Sim_AI --> Q_Sim_Show
    Q_Sim_Show --> D_Sim_Check
    D_Sim_Check -->|"[Need Weight Adjustment]"| M_Author_Done
    D_Sim_Check -->|"[Weights Satisfactory]"| M_Sim_Done
    M_Sim_Done --> Q_Submit_Review
    Q_Submit_Review --> Q_Dup_Check
    Q_Dup_Check --> Q_Val_Backend
    Q_Val_Backend --> D_Val_Backend
    D_Val_Backend -->|"[Validation Failed (HTTP 422)]"| Q_Val_422
    D_Val_Backend -->|"[Validation Passed]"| Q_Persist_Review
    Q_Val_422 --> M_Author_Done
    Q_Persist_Review --> Obj_State_Review
    Obj_State_Review --> Q_Dept_Open
    Q_Dept_Open --> Q_Dept_Inspect
    Q_Dept_Inspect --> D_Dept_Decision
    D_Dept_Decision -->|"[NEEDS_REVISION]"| Q_Dept_Revise
    D_Dept_Decision -->|"[REJECTED]"| Q_Dept_Reject
    D_Dept_Decision -->|"[APPROVED]"| Q_Dept_Approve
    Q_Dept_Revise --> Q_DB_Revise
    Q_DB_Revise --> Q_UI_Revise
    Q_UI_Revise --> Q_Lec_Revise
    Q_Lec_Revise --> M_Studio
    Q_Dept_Reject --> Q_DB_Reject
    Q_DB_Reject --> End_Reject
    Q_Dept_Approve --> Q_DB_Approve
    Q_DB_Approve --> Obj_State_Approved
    Obj_State_Approved --> Q_Sync_UI
    Q_Sync_UI --> Q_Catalog_View
    Q_Catalog_View --> Q_End_Final
```

##### 3. Phần B — Lưu Đồ Thuật Toán Chuyên Sâu (Algorithmic Flowchart)

```mermaid
flowchart TD
    M3_Init(["Khởi tạo tác vụ trong Question Studio (Role: lecturer hoặc department_head)"]) --> M3_Source{"Phương thức khởi tạo (source)?"}

    M3_Source -- "1. source == 'ai_generated'" --> M3_ReadCLO["Lấy danh sách CLOs của Course từ course_clos (3NF CSDL)"]
    M3_ReadCLO --> M3_CallGemini["Gọi Gemini 1.5 Flash sinh Draft Question + Rubric (Sum = 10.0) + SampleAnswer (>= 50 ký tự) qua POST /generate-from-clo"]
    M3_CallGemini --> M3_DupCheck["Tự động so khớp tương đồng ngữ nghĩa: nếu trùng lặp -> gắn cờ isFlagged = true"]
    M3_DupCheck --> M3_SelectBanks

    M3_Source -- "2. source == 'manual'" --> M3_InputManual["Giảng viên nhập nội dung câu hỏi, Bloom Level, CLO & SampleAnswer"]
    M3_InputManual --> M3_HasImg{"Có đính kèm file ảnh/sơ đồ kỹ thuật?"}
    M3_HasImg -- "Không đính kèm" --> M3_SelectBanks
    M3_HasImg -- "Có đính kèm (POST /upload-image)" --> M3_CheckSize{"Kích thước file <= 5MB?"}
    M3_CheckSize -- "Vượt quá 5MB" --> M3_Ret413(["Chặn & Trả HTTP 413 Payload Too Large"])
    M3_CheckSize -- "Hợp lệ (<= 5MB)" --> M3_CheckMagic{"Kiểm tra Header Magic Bytes có đúng PNG / JPG / WEBP?"}
    M3_CheckMagic -- "Sai chữ ký nhị phân (Giả mạo đuôi file)" --> M3_Ret415(["Chặn & Trả HTTP 415 Unsupported Media Type"])
    M3_CheckMagic -- "Đúng Magic Bytes" --> M3_UploadR2["Lưu ảnh lên Cloudflare R2 -> Trả về image_url & Yêu cầu nhập image_description (phục vụ Gemini Multimodal CoT)"]
    M3_UploadR2 --> M3_SelectBanks

    M3_SelectBanks["Chọn kho lưu trữ đích riêng biệt (R8): practice_questions (practice) hoặc exam_questions (exam)"] --> M3_CheckRubric{"Kiểm tra Barem & Đáp án mẫu: Sum(Criteria.MaxScore) == 10.00 VÀ SampleAnswer.Length >= 50?"}
    M3_CheckRubric -- "Sai lệch (Sum != 10.0 hoặc Sample < 50 ký tự)" --> M3_Ret422(["Chặn lưu/gửi duyệt & Trả HTTP 422 Unprocessable Entity"])
    M3_CheckRubric -- "Hợp lệ (Sum == 10.00)" --> M3_OptSim{"Chạy AI Calibration Simulator?"}
    M3_OptSim -- "Có" --> M3_SimRun["POST /api/v1/questions/simulate-grading: Chấm thử câu trả lời mẫu & Tinh chỉnh lại trọng số tiêu chí"]
    M3_SimRun --> M3_CheckRubric
    M3_OptSim -- "Không (Sẵn sàng nộp)" --> M3_SaveDraft["Lưu bản nháp (approval_status = DRAFT)"]

    M3_SaveDraft --> M3_BatchSubmit["Giảng viên bấm Gửi duyệt Bộ Môn: POST /api/v1/questions/batch-submit-review"]
    M3_BatchSubmit --> M3_SetPending["Chuyển trạng thái sang SUBMITTED_FOR_REVIEW"]
    M3_SetPending --> M3_HeadReview{"Trưởng Bộ Môn thẩm định (POST /api/v1/questions/{id}/review-decision)"}

    M3_HeadReview -- "decision == 'NEEDS_REVISION'" --> M3_ToRev["Cập nhật status = NEEDS_REVISION kèm review_notes -> Trả về Giảng viên sửa"]
    M3_ToRev --> M3_SelectBanks
    M3_HeadReview -- "decision == 'REJECTED'" --> M3_ToRej(["Cập nhật status = REJECTED kèm review_notes -> Loại khỏi ngân hàng đề"])
    M3_HeadReview -- "decision == 'APPROVED'" --> M3_ToApp(["Cập nhật status = APPROVED -> Kích hoạt chính thức trong kho practice_questions / exam_questions"])
```

##### 4. Phần C — Vòng Đời Trạng Thái Câu Hỏi Ngân Hàng Đề (Question State Machine)

```mermaid
stateDiagram-v2
    [*] --> DRAFT: Giảng viên hoặc Trưởng BM khởi tạo (AI CLO hoặc Manual + Ảnh <= 5MB)
    DRAFT --> DRAFT: Hiệu chỉnh Rubric (Sum == 10.0đ), SampleAnswer (>= 50 ký tự), chạy AI Simulator
    DRAFT --> SUBMITTED_FOR_REVIEW: Gửi Trưởng Bộ Môn duyệt (POST /api/v1/questions/batch-submit-review)
    SUBMITTED_FOR_REVIEW --> APPROVED: Trưởng BM phê duyệt (decision = APPROVED) -> Vào kho practice_questions / exam_questions
    SUBMITTED_FOR_REVIEW --> NEEDS_REVISION: Trưởng BM yêu cầu sửa (decision = NEEDS_REVISION kèm review_notes)
    SUBMITTED_FOR_REVIEW --> REJECTED: Trưởng BM từ chối loại bỏ (decision = REJECTED kèm review_notes)
    NEEDS_REVISION --> DRAFT: Giảng viên chỉnh sửa lại nội dung câu hỏi / Barem 10.0 / Ảnh kỹ thuật
    NEEDS_REVISION --> SUBMITTED_FOR_REVIEW: Giảng viên gửi duyệt lại sau khi hoàn tất chỉnh sửa
    APPROVED --> [*]
    REJECTED --> [*]
```

---

#### <a id="phan-644"></a>6.4.4. MF-04: Thi Thật Phòng Lab Kiosk, Công Bố Điểm & Phúc Khảo Nội Bộ (Lab Exam & Audit)

##### 1. Tóm Tắt Nghiệp Vụ & Chốt Chặn Kỹ Thuật (MF-04)
* **Pha 0 - Quản trị kỳ thi & ca thi (Cán bộ Khảo thí `examination_officer`):**
  - Tiếp nhận các bộ đề thi đã được Trưởng Bộ Môn phê duyệt và bàn giao (`HANDED_OVER`).
  - Khởi tạo Kỳ thi chính thức (`OfficialExamSession`), gán môn thi, liên kết cấu trúc đề `exam_structures`.
* **Pha 0 - Quản trị kỳ thi & ca thi (Cán bộ Khảo thí `examination_officer` & Trưởng BM `department_head`):**
  - Trưởng Bộ Môn nắm 100% thẩm quyền cấu hình câu hỏi tiếp nối Follow-up (`has_follow_up`, `max_follow_up_questions` 1–5 câu, mặc định 2 câu) áp dụng cho môn/ca thi thật (R3).
  - Cán bộ Khảo thí tiếp nhận các bộ đề thi đã được Trưởng Bộ Môn phê duyệt và bàn giao (`HANDED_OVER`).
  - Cán bộ Khảo thí khởi tạo Kỳ thi chính thức (`OfficialExamSession`), gán môn thi, liên kết cấu trúc đề `exam_structures`.
  - Cán bộ Khảo thí khởi tạo các Ca thi (`RealExamSessionShift`: phòng lab, kíp thi, phân công Cán bộ coi thi `proctor_id`, Giảng viên hậu kiểm `examiner_user_id`), cấu hình `ExamInputMode` (`VoiceOnly`, `VoiceWithTranscriptEdit`) kèm `TranscriptBufferSeconds` (10–300s).
  - Cán bộ Khảo thí phân bổ đề thi không trùng lặp cho thí sinh trong ca qua chỉ mục duy nhất `uq_tickets_shift_exam_set` (`shift_id`, `exam_set_id`).
  - Cán bộ Khảo thí mở xuất bản đề ca thi (`is_exam_published = true` qua `POST /api/v1/official-exams/shifts/{id}/publish-exam`).
* **An ninh phòng thi & Điểm danh (Cán bộ Coi thi `proctor` - Giới hạn phạm vi R4):**
  - Điểm danh đối chiếu CCCD / Thẻ sinh viên, gán vị trí máy SeatNumber 1–40 và kiểm tra chéo qua cột `ip_address` (cấm `workstation_ip`).
  - Giám thị CHỈ có quyền điểm danh thí sinh và báo cáo vi phạm phòng thi vào nhật ký chống gian lận `exam_anti_cheat_logs` với 4 mức độ (`LOW`, `MEDIUM`, `HIGH`, `CRITICAL`).
  - CẮT BỎ TOÀN BỘ QUYỀN HẠN DƯ THỪA CỦA GIÁM THỊ: Cấm mở ca thi, cấm đóng ca thi, cấm mở khóa xuất bản đề, cấm gỡ niêm phong IP máy trạm, cấm reset máy trạm, cấm import danh sách sinh viên, cấm xem điểm hay sửa điểm.
  - Hardware Mic-Check 30s đạt $\ge 60$dB (nếu mic hỏng, Giám thị lập biên bản kỹ thuật báo Khảo thí điều chuyển máy trạm dự phòng).
* **Niêm phong Audio R2 2 Bước & Persist-First < 100ms:**
  - Kiosk ghi âm WebM/Opus tên bất biến `STT_MSSV.webm` (vd: `01_SE170123.webm`).
  - Bước 1: Gọi `POST /api/v1/official-exams/tickets/{ticketId}/questions/{questionId}/audio-upload-authorizations` nhận Presigned PUT URL và Auth Token.
  - Bước 2: Tải file trực tiếp lên Cloudflare R2, sau đó gọi `PUT /api/v1/official-exams/tickets/{ticketId}/questions/{questionId}/audio` gửi kèm mã băm SHA-256 và `audio_duration_seconds` để niêm phong.
  - Persist-First ghi nhận bài nộp vào CSDL trạng thái `SUBMITTED` trong $< 100$ms.
* **Kiosk khóa an toàn — Tuyệt đối 0% điểm liền, 0% khiếu nại tại chỗ:** Sau khi nộp bài, Kiosk khóa màn hình báo đã lưu an toàn, thí sinh ký biên bản giấy và ra về. Đẩy task vào RAM BoundedChannel 1,000 slots (`FullMode.Wait`) để Gemini 1.5 Pro chấm ngầm Rubric 10.0 (chỉ nhận transcript input). Polly Retry 3 lần (2-4-8s) và DLQ 5m do Cán bộ Khảo thí giám sát & chủ động replay (Trưởng BM không can thiệp DLQ - R2).
* **Chốt chặn AI Doubt Guard & Hậu kiểm Giảng viên (`examiner`):** Tự động chia 2 nhóm:
  - *Nhóm 1 (Đáng nghi ngờ / Fail):* `is_suspicious == true` HOẶC `confidence_score < 0.70` HOẶC điểm liệt. Giảng viên **BẮT BUỘC** đối soát Evidence Panel (AudioURL R2, Transcript Whisper gốc, AI CoT), nghe lại Waveform Player (1.0x–1.5x) và sửa điểm kèm lý do giải trình bắt buộc `override_reason` $\ge 10$ ký tự.
  - *Nhóm 2 (Độ tin cậy cao):* Giảng viên rà soát nhanh.
* **Công Bố Điểm (Publish Grades) & One-Way Lock:** Chỉ khi **100% sinh viên trong ca thi đã có điểm hoàn chỉnh**, Cán bộ Khảo thí / Giảng viên mới bấm "Công Bố Điểm" 1 lần duy nhất (`POST /api/v1/official-exams/shifts/{shiftId}/publish-grades`). Hệ thống cập nhật `PUBLISHED` $\to$ `LOCKED` (`is_locked = true`), `OneWayLockInterceptor` chặn 100% lệnh `UPDATE`/`DELETE` trái phép (**HTTP 403 Forbidden**). Đồng thời xuất file Bảng điểm Khảo thí FPT cả định dạng Excel (.xlsx) và PDF có chữ ký số.
* **Xem điểm tại nhà & Quy trình Phúc khảo Nội bộ 5 Bước (R7):** Sinh viên xem điểm trên Student Portal; đồng ý thì bấm Xác nhận nhận điểm (`ACKNOWLEDGED` qua `POST /api/v1/student/official-exams/{ticketId}/acknowledge-grade`). Nếu không đồng ý:
  1. *Sinh viên nộp đơn phúc khảo:* `POST /api/v1/student/appeals` (status = `SUBMITTED_TO_EXAM_OFFICE`).
  2. *Khảo thí tiếp nhận & thẩm tra tính hợp lệ:* Kiểm tra hạn nộp, trạng thái điểm, ghi chú và chuyển giao Trưởng BM (`POST /api/v1/examination-officer/appeals/{id}/forward-to-head`, status = `FORWARDED_TO_DEPT_HEAD`).
  3. *Trưởng BM phân công Giảng viên chấm lại:* `POST /api/v1/department-head/appeals/{id}/assign-lecturer` (status = `ASSIGNED_TO_LECTURER`).
  4. *Giảng viên chấm lại độc lập trên Evidence Panel:* `PUT /api/v1/lecturer/appeals/{id}/regrade` (status = `REGRADED_BY_LECTURER`).
  5. *Trưởng BM thẩm định & phê duyệt quyết định:* `POST /api/v1/department-head/appeals/{id}/decision` (`APPROVED`/`REJECTED`). One-Way Lock mở đường ngoại lệ cập nhật điểm chính thức.

##### 2. Phần A — Sơ Đồ Hoạt Động Liên Làn (OMG UML 2.5 Swimlane Activity Diagram - 5 Phân Làn Tách Biệt, 84 Đỉnh)

```mermaid
flowchart TD
    subgraph Lane1 ["Lane 1: Student (Examinee)"]
        E07(["Student Takes Assigned Seat (1-40) & Signs in with Student ID"])
        E10(["Run 30s Microphone Hardware Test (Read Calibration Passage)"])
        E_Student_Move(["Student Moves to Backup Station"])
        E16(["Student Speaks Viva Answer into Headset Mic"])
        E_Speak_Followup(["Student Speaks Follow-up Answer"])
        E_Student_Confirm(["Student Confirms Final Submission"])
        E24_Student_Leave(["Sign Paper Attendance Sheet & Leave Exam Room Safely"])
        FlowEnd_Lab((⊗))
        E35_Student_Portal(["Student at Home Views Official Grade & CLO Scorecard on Portal"])
        D_Student_Appeal{"Student Grade Decision?"}
        E_Ack_Grade(["Acknowledge Grade (POST /acknowledge-grade -> ACKNOWLEDGED)"])
        E_Submit_Appeal(["Submit AppealRequest on Student Portal (POST /api/v1/appeals)"])
        E_Student_ReAck(["Student Views Updated Grade on Portal & Acknowledges"])
        E_End_Final(((◉)))
    end

    subgraph Lane2 ["Lane 2: Lab Workstation Kiosk"]
        E08(["Kiosk Validates SeatNumber & Checks ip_address Binding"])
        E09(["Enforce Fullscreen Lockdown (Block Alt+Tab/Win/DevTools; Blur>3 Suspend)"])
        E11(["Measure Audio Input Level (Target Peak >= 60dB)"])
        D_Mic_Check{"Hardware Mic >= 60dB?"}
        E_Wait_Start(["Wait for Synchronized Shift Exam Start Signal"])
        Fork_Timers["═════ FORK BAR (Khởi chạy đồng hồ chủ và Kiosk) ═════"]
        E14_KioskTimer(["Start Kiosk Synchronized Countdown Timer"])
        Join_Timers["═════ JOIN BAR (Hội tụ đồng bộ đồng hồ thi) ═════"]
        D_InputMode{"ExamInputMode?"}
        E_Mode_Voice(["Lock Keyboard 100% (Microphone Only)"])
        E_Mode_Buffer(["Enable TranscriptBufferSeconds (10-300s, default 60s)"])
        M_Mode{"◇"}
        E15(["TTS Plays Question Audio (from isolated exam_questions pool)"])
        E17(["Record Audio Stream (WebM/Opus) & Transcribe Stream"])
        D_Shift_Followup{"Shift has_follow_up Enabled?"}
        E_Play_Followup(["TTS Plays Follow-up Question Audio"])
        E_Rec_Followup(["Record & Transcribe Follow-up"])
        D_Followup_Limit{"Follow-up Count < Limit?"}
        M_Followup_Done{"◇"}
        E18(["Package Complete Audio: STT_MSSV.webm (e.g., 01_SE170123.webm)"])
        E19(["Compute Cryptographic SHA-256 Hash Seal at Kiosk"])
        E20(["Direct Upload STT_MSSV.webm to Cloudflare R2 via Presigned URL"])
        D_Exam_Progress{"More Questions or Timeout?"}
        E_Auto_Submit(["Auto-Submit Guard: Force Lock Micro & Auto-Submit"])
        M_Submit{"◇"}
        E21(["Dispatch Submission (R2 Audio URL + SHA-256 Seal + Transcripts)"])
        Fork_Post_Submit["═════ FORK BAR (Tách nhánh: Khóa Kiosk vs Worker chấm ngầm) ═════"]
        E23_Kiosk_Lock(["Lock Kiosk Safely: Submission Sealed (0% Instant Score, 0% Appeal)"])
    end

    subgraph Lane3 ["Lane 3: .NET 8 Backend API & Worker"]
        E04(["Initialize Shift in DB (Tickets: SCHEDULED, Pre-bind IP & Roster)"])
        Obj_Shift_Scheduled["[Shift & Tickets: SCHEDULED | IP Pre-bound]"]
        E13(["SignalR Broadcasts Shift Start & Updates Tickets = IN_PROGRESS"])
        E14_ServerTimer(["Start Server Master Timer (Anti-Tampering Absolute Clock)"])
        E_AI_QuickEval(["AI Quick Context Evaluation on Answer Quality"])
        D_Need_Followup{"Needs Follow-up?"}
        E_Gen_Followup(["Generate Contextual Follow-up Question"])
        E22(["Persist-First (< 100ms): Save Submissions & Ticket = SUBMITTED"])
        Obj_Ticket_Submitted["[Ticket Status = SUBMITTED | SHA-256 Sealed]"]
        E25_Enqueue(["Enqueue EvaluationTask into BoundedChannel (1,000 slots RAM)"])
        E26_Polly_Worker(["Background Worker Rinses Task & Runs Polly Retry (3x: 2s, 4s, 8s)"])
        D_Polly_Status{"AI Service Status?"}
        E_DLQ_Isolate(["Isolate into Dead-Letter Queue with PENDING_RETRY"])
        E_Gemini_Grading(["Gemini 1.5 Pro Grades Transcript via Rubric 10.0 & Computes CoT"])
        E27_Save_AI_Graded(["Update DB: Save Criteria Scores, CoT & Ticket = AI_GRADED"])
        Obj_Ticket_AiGraded["[Ticket Status = AI_GRADED]"]
        E30_AI_Doubt(["AI Doubt Guard Classifies Shift Submissions into 2 Groups"])
        E_Audit_Save(["Update DB Ticket Status = AUDITED & Save lecturer_audits"])
        Obj_Shift_Audited["[All Tickets in Shift: AUDITED]"]
        E32_Validate_100(["Backend Atomic Gate: Verify 100% Shift Tickets have FinalScore"])
        E33_OneWayLock(["Enforce One-Way Lock: Status = PUBLISHED -> LOCKED (HTTP 403)"])
        Obj_Shift_Locked["[Shift & Tickets: PUBLISHED & LOCKED | OneWayLock Active]"]
        E34_Export_Reports(["Export Official Shift Reports (FPT Excel .xlsx & Signed PDF)"])
        E_Appeal_DB(["OneWayLock Authorized Exception: Update DB & Write AuditLog"])
        E_DLQ_Replay(["DlqReplayWorker Auto-Retry 5m OR Exam Officer Manual Replay (R2)"])
    end

    subgraph Lane4 ["Lane 4: Proctor & Lecturer (Audit)"]
        E05(["Proctor Checks Attendance: Match ID/CCCD with Assigned Seat 1-40"])
        E06(["Proctor Reports Exam Room Violations into exam_anti_cheat_logs (R4)"])
        E12(["Exam Officer Publishes Shift Exam (POST /publish-exam)"])
        E28_Close_Shift(["Exam Officer Closes Exam Shift after Candidates Vacate"])
        E29_Open_Audit(["Lecturer Opens Lecturer Audit Portal (Filter Shift & Seat 1-40)"])
        D_Audit_Group{"AI Doubt Classification?"}
        E_Audit_Group1(["Listen STT_MSSV.webm on Waveform Player with Citations & AI CoT"])
        E_Audit_Override(["Override Score with Mandatory override_reason (>= 10 chars)"])
        E_Audit_FastTrack(["Fast-Track Overview & Confirm AI Suggested Scores"])
        M_Audit_Done{"◇"}
        E31_Publish(["Lecturer Clicks Công Bố Điểm Once (POST /publish-grades)"])
        E_Lec_Regrade(["Assigned Lecturer Regrades on Evidence Panel & Submits Reason"])
    end

    subgraph Lane5 ["Lane 5: Department Head (Config & Appeal)"]
        E01((●))
        E02(["Create OfficialExamSession & Assign Courses"])
        E03(["Dept Head 100% Configures Follow-up 1-5; Exam Officer Configures Lab Shifts"])
        E_Dept_Assign(["Department Head Assigns Lecturer (POST /assign-lecturer)"])
        E_Dept_Decision(["Department Head Reviews & Approves Regrade Decision (APPROVED / REJECTED)"])
    end

    E01 --> E02
    E02 --> E03
    E03 --> E04
    E04 --> Obj_Shift_Scheduled
    Obj_Shift_Scheduled --> E05
    E05 --> E06
    E06 --> E07
    E07 --> E08
    E08 --> E09
    E09 --> E10
    E10 --> E11
    E11 --> D_Mic_Check
    D_Mic_Check -->|"[Mic Level < 60dB (Failed)]"| E_Backup_Station
    D_Mic_Check -->|"[Mic Level >= 60dB (Passed)]"| E_Wait_Start
    E_Backup_Station --> E_Student_Move
    E_Student_Move --> E08
    E_Wait_Start --> E12
    E12 --> E13
    E13 --> Fork_Timers
    Fork_Timers --> E14_ServerTimer
    Fork_Timers --> E14_KioskTimer
    E14_KioskTimer --> Join_Timers
    E14_ServerTimer --> Join_Timers
    Join_Timers --> D_InputMode
    D_InputMode -->|"[VoiceOnly Mode]"| E_Mode_Voice
    D_InputMode -->|"[VoiceWithTranscriptEdit Mode]"| E_Mode_Buffer
    E_Mode_Voice --> M_Mode
    E_Mode_Buffer --> M_Mode
    M_Mode --> E15
    E15 --> E16
    E16 --> E17
    E17 --> D_Shift_Followup
    D_Shift_Followup -->|"[Follow-up Enabled]"| E_AI_QuickEval
    D_Shift_Followup -->|"[No Follow-up]"| M_Followup_Done
    E_AI_QuickEval --> D_Need_Followup
    D_Need_Followup -->|"[Needs Follow-up]"| E_Gen_Followup
    D_Need_Followup -->|"[Good Answer - No Follow-up]"| M_Followup_Done
    E_Gen_Followup --> E_Play_Followup
    E_Play_Followup --> E_Speak_Followup
    E_Speak_Followup --> E_Rec_Followup
    E_Rec_Followup --> D_Followup_Limit
    D_Followup_Limit -->|"[Count < Limit]"| M_Followup_Done
    D_Followup_Limit -->|"[Count >= Limit]"| M_Followup_Done
    M_Followup_Done --> E18
    E18 --> E19
    E19 --> E20
    E20 --> D_Exam_Progress
    D_Exam_Progress -->|"[More Questions]"| E15
    D_Exam_Progress -->|"[All Questions Done]"| E_Student_Confirm
    D_Exam_Progress -->|"[Timeout 00:00]"| E_Auto_Submit
    E_Student_Confirm --> M_Submit
    E_Auto_Submit --> M_Submit
    M_Submit --> E21
    E21 --> E22
    E22 --> Obj_Ticket_Submitted
    Obj_Ticket_Submitted --> Fork_Post_Submit
    Fork_Post_Submit --> E23_Kiosk_Lock
    Fork_Post_Submit --> E25_Enqueue
    E23_Kiosk_Lock --> E24_Student_Leave
    E24_Student_Leave --> FlowEnd_Lab
    E25_Enqueue --> E26_Polly_Worker
    E26_Polly_Worker --> D_Polly_Status
    D_Polly_Status -->|"[Polly Retry Failed > 3]"| E_DLQ_Isolate
    D_Polly_Status -->|"[Polly Success]"| E_Gemini_Grading
    E_DLQ_Isolate --> E_DLQ_Replay
    E_DLQ_Replay --> E25_Enqueue
    E_Gemini_Grading --> E27_Save_AI_Graded
    E27_Save_AI_Graded --> Obj_Ticket_AiGraded
    Obj_Ticket_AiGraded --> E30_AI_Doubt
    E28_Close_Shift --> E29_Open_Audit
    E29_Open_Audit --> D_Audit_Group
    E30_AI_Doubt --> D_Audit_Group
    D_Audit_Group -->|"[Group 1: Suspicious / Fail / Conf < 0.70]"| E_Audit_Group1
    D_Audit_Group -->|"[Group 2: High Confidence (Conf >= 0.70)]"| E_Audit_FastTrack
    E_Audit_Group1 --> E_Audit_Override
    E_Audit_Override --> E_Audit_Save
    E_Audit_FastTrack --> E_Audit_Save
    E_Audit_Save --> M_Audit_Done
    M_Audit_Done --> Obj_Shift_Audited
    Obj_Shift_Audited --> E31_Publish
    E31_Publish --> E32_Validate_100
    E32_Validate_100 --> E33_OneWayLock
    E33_OneWayLock --> Obj_Shift_Locked
    Obj_Shift_Locked --> E34_Export_Reports
    Obj_Shift_Locked --> E35_Student_Portal
    E35_Student_Portal --> D_Student_Appeal
    D_Student_Appeal -->|"[Agree with Grade]"| E_Ack_Grade
    D_Student_Appeal -->|"[Submit Appeal]"| E_Submit_Appeal
    E_Submit_Appeal --> E_Dept_Assign
    E_Dept_Assign --> E_Lec_Regrade
    E_Lec_Regrade --> E_Appeal_DB
    E_Appeal_DB --> E_Student_ReAck
    E_Ack_Grade --> E_End_Final
    E_Student_ReAck --> E_End_Final
```

##### 3. Phần B — Lưu Đồ Thuật Toán Chuyên Sâu (Algorithmic Flowchart)

```mermaid
flowchart TD
    M4_Start(["Khởi chạy Ca thi thật phòng Lab (RealExamSessionShift)"]) --> M4_CheckIn["Proctor điểm danh Thẻ SV/CCCD, gán SeatNumber (1..40) & lưu cột ip_address (theo quy chuẩn ip_address) -> Vé thi SCHEDULED"]
    M4_CheckIn --> M4_KioskGuard["Máy trạm bật Fullscreen Kiosk Lockdown: Chặn Alt+Tab, Win, Esc, F11, F12, DevTools"]
    M4_KioskGuard --> M4_MicTest{"Kiểm tra phần cứng Mic-Check 30s: Cường độ âm thanh >= 60dB?"}
    M4_MicTest -- "Không đạt (< 60dB)" --> M4_SwapSeat["Báo Giám thị đổi tai nghe hoặc chuyển vị trí SeatNumber dự phòng"]
    M4_SwapSeat --> M4_MicTest
    M4_MicTest -- "Đạt chuẩn (>= 60dB)" --> M4_InProg["Chuyển trạng thái vé thi -> IN_PROGRESS & Rút đề từ kho exam_questions"]

    M4_InProg --> M4_BlurCheck{"Giám sát sự kiện window.onblur trong lúc làm bài: blur_count > 3 lần?"}
    M4_BlurCheck -- "Vi phạm (> 3 lần thoát màn hình)" --> M4_Suspend(["Đình chỉ thi tự động, lập biên bản vi phạm quy chế & báo Proctor Console"])
    M4_BlurCheck -- "An toàn (<= 3 lần)" --> M4_InputMode{"Kiểm tra cấu hình ExamInputMode của ca thi?"}

    M4_InputMode -- "VoiceOnly" --> M4_VoiceOnly["SV chỉ trả lời bằng giọng nói (kèm 1-5 câu hỏi phụ ngữ cảnh nếu bật), khóa hoàn toàn chỉnh sửa văn bản"]
    M4_InputMode -- "VoiceWithTranscriptEdit" --> M4_VoiceEdit["SV trả lời bằng giọng nói -> Mở đệm sửa thuật ngữ trong TranscriptBufferSeconds (10-300s)"]
    M4_VoiceOnly --> M4_SealR2
    M4_VoiceEdit --> M4_SealR2

    M4_SealR2["Đóng gói file ghi âm tên bất biến STT_MSSV.webm (vd: 01_SE170123.webm), tính mã băm SHA-256 tại Kiosk & Stream lên Cloudflare R2"] --> M4_Persist["Persist-First (< 100ms): INSERT exam_question_submissions & UPDATE student_exam_tickets SET status = SUBMITTED"]
    M4_Persist --> M4_LockKiosk["Khóa màn hình Kiosk tức thì: Báo đã lưu an toàn, TUYỆT ĐỐI KHÔNG hiện điểm liền, không khiếu nại tại chỗ; SV ký biên bản giấy ra về"]
    M4_Persist --> M4_Queue["Đẩy task vào BoundedChannel 1,000 slots RAM (FullMode.Wait) -> OfficialExamGradingWorker rút xử lý"]

    M4_Queue --> M4_Retry{"Gọi Whisper STT + Gemini 1.5 Pro chấm Rubric 10.0 (Polly Retry 2s -> 4s -> 8s)?"}
    M4_Retry -- "Thất bại cả 3 lần" --> M4_DLQ["Cách ly vào bảng dead_letter_queues (Tự động chấm bù sau 5 phút hoặc Cán bộ Khảo thí bấm Replay thủ công)"]
    M4_DLQ --> M4_Retry
    M4_Retry -- "Chấm thành công" --> M4_SetGraded["Cập nhật vé thi status = AI_GRADED (Lưu ai_score, confidence_score, is_suspicious)"]

    M4_SetGraded --> M4_Doubt{"AI Doubt Guard: is_suspicious == true HOẶC confidence_score < 0.70 HOẶC điểm liệt?"}
    M4_Doubt -- "Đúng (Nhóm 1: Cần hậu kiểm bắt buộc)" --> M4_Group1["Đẩy lên đầu danh sách ưu tiên: Giảng viên nghe Waveform Audio R2 (1.0x-1.5x), đối soát Transcript & AI CoT"]
    M4_Doubt -- "Sai (Nhóm 2: Độ tin cậy cao >= 0.70)" --> M4_Group2["Giảng viên rà soát nhanh trên Evidence Panel"]
    M4_Group1 --> M4_Override{"Giảng viên có điều chỉnh điểm so với AI?"}
    M4_Override -- "Có sửa điểm" --> M4_ReqReason["BẮT BUỘC nhập lý do giải trình (override_reason) -> Lưu trạng thái AUDITED"]
    M4_Override -- "Giữ nguyên điểm AI" --> M4_SetAudited["Lưu trạng thái vé thi = AUDITED"]
    M4_Group2 --> M4_SetAudited
    M4_ReqReason --> M4_SetAudited

    M4_SetAudited --> M4_CheckAll{"Giảng viên bấm Công Bố Điểm: 100% SV trong ca thi đã có điểm hoàn chỉnh?"}
    M4_CheckAll -- "Chưa đủ 100% (Còn bài chưa chấm/hậu kiểm)" --> M4_Err422Pub(["Chặn công bố điểm & Trả HTTP 422 Unprocessable Entity"])
    M4_CheckAll -- "Đủ 100% SV" --> M4_Publish["POST /api/v1/official-exams/shifts/{shiftId}/publish-grades (1 lần duy nhất): Chuyển PUBLISHED -> LOCKED (is_locked = true)"]
    M4_Publish --> M4_Export["Xuất báo cáo khảo thí kép: File Excel (.xlsx) & File PDF có chữ ký số"]
    M4_Publish --> M4_OneWay{"Phát sinh lệnh UPDATE / DELETE trên vé thi đã khóa (is_locked == true)?"}
    M4_OneWay -- "Can thiệp trực tiếp / Trái phép" --> M4_Block403(["OneWayLockInterceptor chặn cứng -> Trả HTTP 403 Forbidden"])

    M4_Publish --> M4_PortalView{"Sinh viên xem điểm trên Student Portal: Đồng ý kết quả?"}
    M4_PortalView -- "Đồng ý" --> M4_Ack(["POST /api/v1/student/official-exams/{ticketId}/acknowledge-grade -> ACKNOWLEDGED"])
    M4_PortalView -- "Không đồng ý (Phúc khảo)" --> M4_Step1Appeal["Bước 1: SV gửi POST /api/v1/student/appeals tạo AppealRequest (status = SUBMITTED_TO_EXAM_OFFICE)"]
    M4_Step1Appeal --> M4_Step2Forward["Bước 2: Khảo thí thẩm tra hợp lệ & chuyển tiếp Trưởng BM qua POST /forward-to-head (status = FORWARDED_TO_DEPT_HEAD)"]
    M4_Step2Forward --> M4_Step3Assign["Bước 3: Trưởng BM gọi POST /assign-lecturer giao GV chấm lại (status = ASSIGNED_TO_LECTURER)"]
    M4_Step3Assign --> M4_Step4Regrade["Bước 4: GV được giao gọi PUT /regrade trên Evidence Panel (status = REGRADED_BY_LECTURER, OneWayLock mở chốt hợp lệ)"]
    M4_Step4Regrade --> M4_Step5Decision["Bước 5: Trưởng BM gọi POST /decision (APPROVED / REJECTED) -> Cập nhật điểm chính thức & Khóa lại One-Way Lock"]
```

##### 4. Phần C — Vòng Đời 7 Trạng Thái Vé Thi Viết HOA Bất Biến & Vòng Đời Phúc Khảo (Ticket State Machine)

```mermaid
stateDiagram-v2
    [*] --> SCHEDULED: Khảo thí tạo ca thi & Giám thị điểm danh gán SeatNumber (1..40) + ip_address
    SCHEDULED --> IN_PROGRESS: Thí sinh vượt qua Kiosk Lockdown + Mic-Check 30s (>= 60dB) & Bắt đầu làm bài
    IN_PROGRESS --> SUBMITTED: Persist-First (< 100ms) lưu DB kèm Audio R2 (STT_MSSV.webm + SHA-256) & Khóa màn hình Kiosk
    SUBMITTED --> AI_GRADED: OfficialExamGradingWorker (Whisper STT + Gemini 1.5 Pro) chấm xong 100% câu hỏi
    AI_GRADED --> AUDITED: Giảng viên hậu kiểm qua Evidence Panel & Waveform Audio Player (nhập override_reason nếu sửa điểm)
    AUDITED --> PUBLISHED: Giảng viên / Khảo thí bấm Công Bố Điểm 1 lần duy nhất (Khi 100% SV trong ca đã có điểm)
    PUBLISHED --> LOCKED: Kích hoạt One-Way Lock (is_locked = true, OneWayLockInterceptor chặn mọi UPDATE/DELETE trái phép -> HTTP 403)

    state LOCKED {
        [*] --> GRADE_FINALIZED: Bảng điểm niêm phong & Xuất Excel (.xlsx) + PDF ký số
        GRADE_FINALIZED --> ACKNOWLEDGED: Sinh viên bấm Xác nhận đồng ý điểm (POST /acknowledge-grade)
        GRADE_FINALIZED --> APPEAL_SUBMITTED: Sinh viên nộp đơn phúc khảo lên Khảo thí (POST /api/v1/student/appeals)
        APPEAL_SUBMITTED --> APPEAL_FORWARDED: Khảo thí thẩm tra & chuyển giao Trưởng BM (POST /forward-to-head)
        APPEAL_FORWARDED --> APPEAL_ASSIGNED: Trưởng BM phân công Giảng viên chấm lại (POST /assign-lecturer)
        APPEAL_ASSIGNED --> APPEAL_REGRADED: Giảng viên chấm lại qua Evidence Panel (PUT /regrade - Bypass hợp lệ OneWayLockInterceptor)
        APPEAL_REGRADED --> APPEAL_RESOLVED: Trưởng BM phê duyệt kết quả phúc khảo (POST /decision: APPROVED / REJECTED)
        APPEAL_RESOLVED --> GRADE_FINALIZED: Cập nhật điểm phúc khảo chính thức & Khóa lại One-Way Lock
    }
```

---

### <a id="phan-65"></a>6.5. Sơ Đồ Tuần Tự Tương Tác Chi Tiết 4 Core Main Flows (Sequence Diagrams)

#### <a id="phan-651"></a>6.5.1. Sơ Đồ Tuần Tự MF-01: Luyện Tập Vấn Đáp Tự Do (Interactive Practice Sequence Diagram)

```mermaid
sequenceDiagram
    autonumber
    actor SV as Sinh viên (Student)
    participant UI as Giao diện Web (React 19)
    participant Speech as Web Speech API (TTS & STT)
    participant API as Ingestion API Gateway (.NET 8)
    participant DB as CSDL PostgreSQL 16
    participant Queue as Bounded Channel Queue (1,000 slots)
    participant Worker as Background Worker
    participant LLM as Gemini 1.5 Flash (Rubric CoT)
    participant Hub as SignalR PracticeHub

    Note over SV,UI: PHA 1: KHỞI TẠO PHIÊN LUYỆN TẬP (PER-QUESTION ON-DEMAND / FULL-SESSION PROGRESSIVE)
    SV->>UI: Chọn Môn học, chế độ làm bài & độ khó
    alt Chế độ Per-Question On-Demand
        UI->>API: POST /api/v1/practice/sessions { courseId, difficulties: ["easy","medium"], isFullSession: false }
        API->>DB: Bốc câu 1 theo độ khó đã chọn, INSERT practice_sessions (last_activity_at = NOW())
        DB-->>API: Câu 1 kèm Barem 10.0đ
    else Chế độ Full-Session Progressive
        UI->>API: POST /api/v1/practice/sessions { courseId, questionCount: 6, isFullSession: true }
        API->>DB: Bốc trọn gói 3-10 câu chia đều Dễ -> TB -> Khó, INSERT practice_sessions (last_activity_at = NOW())
        DB-->>API: Danh sách câu hỏi kèm Barem 10.0đ
    end
    API-->>UI: HTTP 200 OK (Session Data & Question)
    UI->>Speech: Đọc to câu hỏi qua Web Speech TTS
    Speech-->>SV: Giọng đọc mô phỏng giám khảo viva

    Note over SV,UI: PHA 2: TRẢ LỜI & MÀN HÌNH ĐỆM HIỆU ĐÍNH CODE-SWITCHING (MẶC ĐỊNH 60S)
    SV->>UI: Bấm Bật Micro phát biểu (hoặc gõ phím)
    UI->>Speech: Khởi tạo Web Speech STT (vi-VN)
    SV->>Speech: Trả lời câu hỏi
    Speech-->>UI: Stream văn bản transcript thời gian thực (< 0.5s)
    SV->>UI: Bấm Hoàn thành nói
    UI->>UI: Mở màn hình đệm hiệu đính Code-Switching (mặc định 60s theo Admin)
    opt Sửa lỗi chính tả thuật ngữ tiếng Anh
        SV->>UI: Sửa trực tiếp từ ngữ (ví dụ: API, Polymorphism, SQL)
    end

    Note over UI,Worker: PHA 3: NỘP BÀI & CƠ CHẾ PHÒNG THỦ PERSIST-FIRST
    SV->>UI: Bấm Nộp bài chấm điểm
    UI->>API: POST /api/v1/practice/submit
    API->>DB: INSERT practice_answers (status='pending') & UPDATE last_activity_at = NOW() < 100ms
    DB-->>API: Disk Committed
    API-->>UI: HTTP 202 Accepted
    UI->>Hub: Kết nối lắng nghe kết quả qua SignalR

    API->>Queue: Đẩy task chấm bài vào Bounded Channel
    Queue->>Worker: Worker rút task tuần tự
    Worker->>LLM: Gửi Prompt CoT 3 bước + Rubric 10.0 + Transcript
    LLM-->>Worker: JSON Scorecard (Điểm tiêu chí, Điểm tổng, Feedback)

    Note over Worker,Hub: PHA 4: KIỂM TRA CHẾ ĐỘ FOLLOW-UP & ĐẨY SCORECARD
    alt Chế độ Luyện từng câu [Per-Question] & Điểm 4.0 <= Score <= 8.0 (Admin cấu hình 1-5 câu, mặc định 2 câu)
        Worker->>LLM: Yêu cầu sinh câu hỏi phụ đào sâu A2 (khi 4.0 <= Score <= 8.0)
        LLM-->>Worker: Trả về câu hỏi phụ A2
        Worker->>DB: Ghi nhận thông tin câu hỏi phụ vào phiên luyện tập
    else Chế độ Luyện trọn gói [Full-Session]
        Worker->>DB: Không kích hoạt follow-up, hoàn thành phiên và tổng hợp Scorecard
    end

    Worker->>DB: UPDATE practice_answers (status='graded') & INSERT ai_evaluations
    Worker->>Hub: Broadcast SendScorecard(scorecardPayload)
    Hub-->>UI: Đẩy Scorecard hiển thị tức thì trên màn hình (kèm câu hỏi phụ A2 nếu có)

    opt Sinh viên trả lời câu hỏi phụ A2 (khi ở chế độ [Per-Question])
        SV->>UI: Bật Micro hoặc gõ phím trả lời phản biện A2
        UI->>API: POST /api/v1/practice/followup/submit
        API->>DB: INSERT practice_answers (is_follow_up=true, parent_answer_id) & UPDATE last_activity_at = NOW()
        API->>LLM: Chấm điểm & nhận xét câu phản biện A2
        LLM-->>API: Điểm bổ sung & Model Answer hoàn chỉnh
        API-->>UI: Hiển thị nhận xét tổng kết câu hỏi A2
    end

    Note over SV,API: PHA 5: VÒNG LẶP ON-DEMAND HOẶC TIMEOUT 5 PHÚT
    opt Sinh viên bấm "Câu tiếp theo" trong chế độ Per-Question
        SV->>UI: Bấm "Câu tiếp theo" (On-Demand)
        UI->>API: POST /api/v1/practice/sessions/{id}/next-question
        alt Quá 5 phút không tương tác (Inactivity Timeout)
            API->>DB: UPDATE practice_sessions SET status = 'completed'
            API-->>UI: HTTP 410 Gone (Session Timed Out)
            UI-->>SV: Thông báo phiên hết hạn an toàn & hiển thị Scorecard bảo toàn điểm
        else Còn thời hạn hợp lệ
            API->>DB: Áp dụng thuật toán Anti-3-Consecutive (loại trừ mức nếu 2 câu liền trước cùng độ khó)
            API->>DB: UPDATE practice_sessions SET last_activity_at = NOW()
            DB-->>API: Câu hỏi tiếp theo
            API-->>UI: HTTP 200 OK (question, hasMoreQuestions)
            UI->>Speech: Đọc to câu hỏi tiếp theo
        end
    end
```

#### <a id="phan-652"></a>6.5.2. Sơ Đồ Tuần Tự MF-02: Thi Thử Vấn Đáp Bấm Giờ (Timed Mock Exam Sequence Diagram)

```mermaid
sequenceDiagram
    autonumber
    actor SV as Sinh viên (Student)
    participant UI as Giao diện Web (React 19)
    participant Guard as Daily Quota Guard (PostgreSQL)
    participant Speech as Web Speech API (TTS & STT)
    participant Timer as Dynamic Countdown Timer
    participant API as MockExam API Gateway (.NET 8)
    participant DB as CSDL PostgreSQL
    participant Queue as Bounded Channel Queue
    participant Worker as Background Worker
    participant LLM as Gemini 1.5 Flash (Scorecard)
    participant Hub as SignalR PracticeHub

    Note over SV,Guard: GIAI ĐOẠN 1: CHỌN MÔN HỌC, CHỌN CHẾ ĐỘ FOLLOW-UP & KIỂM TRA HẠN NGẠCH NGÀY
    SV->>UI: Chọn "Thi thử vấn đáp", Chọn Môn học & Chủ động chọn chế độ: [Có Follow-up] hoặc [Không Follow-up]
    UI->>API: POST /api/v1/mock-exams/sessions (StudentId, CourseId, HasFollowUp)
    API->>Guard: Kiểm tra hạn ngạch thi thử trong ngày theo môn (used_attempts vs courses.max_mock_exams_per_day do Admin cấu hình)
    
    alt Đã chạm trần hạn ngạch ngày (used_attempts >= max_mock_exams_per_day)
        Guard-->>API: Quota Exceeded (used_attempts >= max_mock_exams_per_day)
        API-->>UI: HTTP 429 Too Many Requests ("Bạn đã hết lượt thi thử trong ngày cho môn học này (used_attempts/max_mock_exams_per_day). Hãy chuyển sang Luyện tập tự do MF-01")
        UI-->>SV: Khóa nút làm bài, hiển thị cảnh báo đỏ và nút chuyển hướng sang MF-01
    else Còn lượt thi thử hợp lệ (used_attempts < max_mock_exams_per_day)
        Guard-->>API: Quota Available (used_attempts < max_mock_exams_per_day)
        API->>DB: Bốc ngẫu nhiên N câu hỏi từ kho practice_questions (Môn học, Bloom, Barem 10.0)
        API->>DB: INSERT INTO mock_exam_sessions (has_follow_up=true/false, status='in_progress')
        DB-->>API: Khởi tạo phiên thi thử thành công
        API-->>UI: HTTP 200 OK (SessionId, Danh sách câu hỏi, Chế độ Follow-up)
        UI-->>SV: Hiển thị giao diện phòng thi thử, thanh tiến trình "Câu 1 / N"
    end

    Note over SV,Timer: GIAI ĐOẠN 2: LÀM TỪNG CÂU VỚI KỶ LUẬT VOICE-FIRST GATE & TÙY CHỌN FOLLOW-UP
    loop Cho từng câu hỏi thứ i (1 <= i <= N)
        UI->>UI: Nạp nội dung câu hỏi thứ i
        UI->>UI: KÍCH HOẠT VOICE-FIRST GATE: Ô soạn thảo Textarea bị KHÓA CỨNG (disabled)
        UI->>Timer: Kích hoạt đồng hồ đếm ngược cho câu thứ i (ví dụ: 120s)
        
        SV->>UI: Bắt buộc nhấp icon Micro để bật mic
        UI->>Speech: Bật Web Speech STT streaming
        SV->>Speech: Phát biểu câu trả lời kỹ thuật bằng giọng nói
        Speech-->>UI: Hiển thị transcript thời gian thực dưới mic
        
        SV->>UI: Bấm "Hoàn thành nói / Dừng mic"
        UI->>Speech: Dừng thu âm
        UI->>UI: MỞ KHÓA ô Textarea (enabled) & nạp transcript vừa nói vào
        
        opt Sinh viên sửa lại các từ nhận diện sai hoặc nói vấp
            SV->>UI: Nhấp vào ô Textarea sửa lại từ ngữ kỹ thuật
        end

        opt Sinh viên đã chọn chế độ Có Follow-up ([mock_exam_sessions.has_follow_up == true])
            UI->>API: Phân tích nhanh ngữ cảnh transcript câu i
            API->>LLM: Kiểm tra luận điểm cần phản biện ([Needs Follow-up] vs [Good Answer])
            alt Luận điểm cần đào sâu phản biện [Needs Follow-up]
                LLM-->>API: Trả về câu hỏi phụ ngữ cảnh (Follow-up Question)
                API-->>UI: Cấp câu hỏi phụ thời gian thực
                UI->>Speech: Web Speech TTS đọc câu hỏi phụ qua tai nghe
                SV->>Speech: Trả lời câu hỏi phụ bằng micro (Voice-First)
                Speech-->>UI: Ghi nhận transcript câu trả lời phụ vào phiên thi
            end
        end
        
        SV->>UI: Bấm "Lưu câu trả lời"
        UI->>UI: Lưu tạm câu thứ i vào bộ nhớ phiên & cập nhật tiến độ (i+1)/N
    end

    Note over SV,Worker: GIAI ĐOẠN 3: NỘP BÀI TOÀN BỘ ĐỀ & ĐIỀU PHỐI CHẤM AI 2 PHA (DUAL-PATH)
    SV->>UI: Bấm "Nộp toàn bộ bài thi thử" (hoặc AutoSubmit khi hết giờ tổng)
    UI->>API: POST /api/v1/mock-exams/submit (SessionId, ListOfAnswers)
    
    Note over API,DB: [PERSIST-FIRST < 100ms] Ghi đĩa CSDL an toàn trước khi điều phối AI
    API->>DB: INSERT INTO mock_exam_answers (status='answered')
    DB-->>API: Disk Committed (< 100ms)
    API-->>UI: HTTP 202 Accepted (submission_id)

    alt Nhánh Fast-Path: Hệ thống rảnh, AI phản hồi nhanh (< 10s)
        Note over API,LLM: [FAST-PATH] Chấm tức thì và trả điểm trực tiếp trên màn hình
        API->>Worker: Dispatch task chấm điểm trực tiếp
        Worker->>LLM: Gọi Gemini 1.5 Flash chấm bóc tách tiêu chí N câu
        LLM-->>Worker: HTTP 200 OK (Instant Feedback Scorecard: Rubric scores, strengths, weaknesses, improvement suggestions)
        Worker->>DB: UPDATE mock_exam_answers SET status='graded' & UPDATE mock_exam_sessions (total_score, scorecard_json)
        Worker->>Hub: Push sự kiện "MockExamGraded" (Instant Feedback Scorecard)
        Hub-->>UI: WebSocket push Scorecard tới Client
        UI-->>SV: Hiển thị Instant Feedback Scorecard chi tiết từng câu (Rubric, Strengths, Weaknesses, Suggestions)!
    else Nhánh Async Fallback: Hệ thống bận / tải cao / AI nghẽn
        Note over API,SV: [ASYNC FALLBACK] Báo lưu an toàn & chấm ngầm trong nền
        API->>Queue: Đẩy task vào Bounded Channel Queue chờ xử lý
        UI-->>SV: Thông báo: Đã lưu bài thi (#Mã bài), hệ thống đang tải cao sẽ xử lý ngầm, kết quả và Instant Feedback Scorecard sẽ lưu vào mục Lịch sử thi
        
        Queue->>Worker: Background Worker rút task khi có tài nguyên
        Worker->>LLM: Gọi Gemini 1.5 Flash chấm batch
        LLM-->>Worker: HTTP 200 OK (Instant Feedback Scorecard đầy đủ)
        Worker->>DB: UPDATE mock_exam_answers SET status='graded' & UPDATE mock_exam_sessions & lưu vĩnh viễn vào Lịch sử thi
        Note over Worker,DB: CSDL cập nhật hoàn tất - sinh viên tra cứu chi tiết tại GET /api/v1/mock-exams/history
    end
```

#### <a id="phan-653"></a>6.5.3. Sơ Đồ Tuần Tự MF-03: Quản Lý Ngân Hàng Câu Hỏi, AI Sinh Đề & Duyệt Bộ Môn (Question Studio Sequence Diagram)

```mermaid
sequenceDiagram
    autonumber
    actor GV as Giảng viên (Instructor)
    actor DH as Trưởng Bộ Môn (Dept Head)
    participant UI as Portal Web (React 19)
    participant Guard as Client Rubric Guard
    participant API as Question & Review API (.NET 8)
    participant DB as CSDL PostgreSQL & AuditLog
    participant AI as Gemini 1.5 Flash (AI Engine)

    Note over GV,DB: GIAI ĐOẠN 1: GIẢNG VIÊN SỬ DỤNG AI SINH CÂU HỎI TỪ CLO MÔN HỌC (3NF)
    GV->>UI: Đăng nhập JWT Role lecturer & Mở Question Studio
    GV->>UI: Chọn môn học (CourseId) cần soạn đề
    UI->>API: GET /api/v1/courses/{courseId}/clos
    API->>DB: Truy vấn bảng course_clos (3NF) theo course_id
    DB-->>API: Danh sách chuẩn đầu ra CLO (mã CLO, mô tả, bloom_level)
    API-->>UI: HTTP 200 OK (Danh sách CLOs môn học)
    UI-->>GV: Hiển thị cây chuẩn đầu ra CLOs
    GV->>UI: Chọn CLO mục tiêu, cấu hình số câu, phân bổ độ khó & Bloom 1-6; tick chọn kho practice_questions và/hoặc exam_questions
    GV->>UI: Bấm "Kích hoạt AI Sinh Câu Hỏi"
    UI->>API: POST /api/v1/questions/generate-from-clo (CourseId, SelectedCLOs, Distribution)
    API->>AI: Gửi Prompt CoT + Chuẩn đầu ra CLOs từ course_clos
    AI-->>API: JSON Structured Questions (Đề bài, Bloom 1-6, Barem Rubric ∑=10.0đ, Model Answer >= 50 chars, Key points)
    API-->>UI: HTTP 200 OK (Danh sách Draft Questions & Rubrics)
    UI-->>GV: Hiển thị Draft Preview Studio: Cho phép Giảng viên tự thiết kế theo Barem riêng
    
    Note over GV,Guard: GIAI ĐOẠN 2: TINH CHỈNH BAREM RIÊNG, CLIENT GUARD & AI SIMULATOR
    GV->>UI: Tùy chỉnh nội dung câu hỏi, điều chỉnh tiêu chí Barem riêng & Model Answer
    loop Kiểm tra tính toàn vẹn tổng điểm tức thời (Real-time Client Guard)
        UI->>Guard: Tính tổng điểm: S = Sum(criteria_max_score)
        alt Tổng điểm lệch S != 10.0đ
            Guard-->>UI: Khóa nút "Gửi lên cho Bộ Môn", cảnh báo đỏ: Tổng điểm phải = 10.0đ
        else Tổng điểm hợp lệ S == 10.0đ
            Guard-->>UI: Mở khóa nút "Gửi lên cho Bộ Môn" & mở nút "Chạy AI Simulator"
        end
    end

    opt Giảng viên kiểm chứng Barem qua AI Calibration Simulator
        GV->>UI: Nhập câu trả lời giả định & Bấm "Chạy AI Simulator"
        UI->>API: POST /api/v1/questions/simulate-grading (Question, ModelAnswer, Rubric, SimulatedAnswer)
        API->>AI: Gemini Flash chấm thử nghiệm theo Barem
        AI-->>API: JSON Scorecard & phản biện độ nhạy Barem
        API-->>UI: HTTP 200 OK (Báo cáo thẩm định giả lập)
        UI-->>GV: Hiển thị kết quả chấm thử để GV tinh chỉnh lại nếu cần
    end

    Note over GV,DH: GIAI ĐOẠN 3: GIẢNG VIÊN GỬI LÊN CHO BỘ MÔN (SUBMIT TO DEPT HEAD)
    GV->>UI: Bấm nút "Gửi lên cho Bộ Môn" (Submit to Department Head)
    UI->>API: POST /api/v1/questions/batch-submit-review (SubmittedQuestionsDto[])
    API->>API: FluentValidation kiểm tra ∑ Barem == 10.0đ & Model Answer >= 50 chars
    API->>API: Semantic Duplicate Check: So khớp tương đồng ngữ nghĩa -> Gắn cờ isFlagged = true nếu trùng lặp
    API->>DB: INSERT / UPDATE questions (approval_status='SUBMITTED_FOR_REVIEW', is_flagged, submitted_by=GV_id)
    DB-->>API: Ghi thành công trạng thái chờ duyệt
    API-->>UI: HTTP 200 OK (Submitted For Review Successfully)
    UI-->>GV: Thông báo: "Đã gửi câu hỏi lên Trưởng Bộ Môn thẩm định và phê duyệt!"

    Note over DH,DB: GIAI ĐOẠN 4: TRƯỞNG BỘ MÔN THẨM ĐỊNH & PHÊ DUYỆT / YÊU CẦU CHỈNH SỬA
    DH->>UI: Đăng nhập JWT Role department_head & Mở Cổng Thẩm định (Approval Studio)
    UI->>API: GET /api/v1/questions/pending-review?courseId={courseId}
    API->>DB: Query các câu hỏi có approval_status='SUBMITTED_FOR_REVIEW'
    DB-->>API: Danh sách câu hỏi chờ duyệt
    API-->>UI: HTTP 200 OK (Danh sách câu hỏi kèm Barem riêng và Model Answer)
    UI-->>DH: Hiển thị chi tiết từng câu hỏi để Trưởng Bộ Môn đánh giá
    
    alt Trưởng Bộ Môn chấp thuận (Phê duyệt chính thức)
        DH->>UI: Bấm "Phê duyệt" (Approve)
        UI->>API: POST /api/v1/questions/{id}/review-decision (decision='APPROVED')
        API->>DB: UPDATE questions SET approval_status='APPROVED', approved_by=DH_id
        API->>DB: INSERT INTO audit_logs (dh_id, action='APPROVE_QUESTION', timestamp)
        DB-->>API: Commit Transaction thành công
        API-->>UI: HTTP 200 OK
        UI-->>DH: Thông báo: "Câu hỏi đã được phê duyệt chính thức vào Ngân hàng Đề!"
    else Trưởng Bộ Môn yêu cầu chỉnh sửa (Request Revision)
        DH->>UI: Bấm "Yêu cầu chỉnh sửa" & nhập góp ý (review_notes)
        UI->>API: POST /api/v1/questions/{id}/review-decision (decision='NEEDS_REVISION', reviewNotes)
        API->>DB: UPDATE questions SET approval_status='NEEDS_REVISION', review_notes=reviewNotes
        DB-->>API: Ghi nhận trạng thái yêu cầu chỉnh sửa
        API-->>UI: HTTP 200 OK
        UI-->>DH: Thông báo: "Đã gửi yêu cầu chỉnh sửa kèm góp ý về cho Giảng viên!"
        API-->>GV: Gửi thông báo cho Giảng viên mở lại câu hỏi để sửa và gửi lại
    else Trưởng Bộ Môn từ chối loại hẳn (Reject)
        DH->>UI: Bấm "Từ chối" & nhập lý do (review_notes)
        UI->>API: POST /api/v1/questions/{id}/review-decision (decision='REJECTED', reviewNotes)
        API->>DB: UPDATE questions SET approval_status='REJECTED', review_notes=reviewNotes
        DB-->>API: Ghi nhận trạng thái loại hẳn khỏi ngân hàng đề
        API-->>UI: HTTP 200 OK
        UI-->>DH: Thông báo: "Đã loại hẳn câu hỏi khỏi danh sách đề!"
    end
```

#### <a id="phan-654"></a>6.5.4. Sơ Đồ Tuần Tự MF-04: Thi Thật Phòng Lab, Công Bố Điểm & Phúc Khảo Nội Bộ (Lab Exam Sequence Diagram)

```mermaid
sequenceDiagram
    autonumber
    actor DH as Trưởng Bộ Môn (Department Head)
    actor EO as Cán bộ Khảo thí (Examination Officer)
    actor PR as Cán bộ Coi thi (Proctor)
    actor GV as Giảng viên (Giám khảo / Hậu kiểm)
    actor SV as Thí sinh (Tại Máy Lab Kiosk)
    participant LabPC as Máy trạm Kiosk (Lab PC UI)
    participant Cloud as Cloudflare R2 Audio Storage
    participant API as Backend Exam API (.NET 8)
    participant DB as CSDL PostgreSQL & AuditLog
    participant Worker as Background Worker (Whisper + Gemini Pro)
    participant AuditUI as Lecturer Audit Portal
    participant Portal as Student Portal (Web UI)

    Note over DH,DB: PHA 0: CẤU HÌNH ĐỀ & THIẾT LẬP KỲ THI, CA THI
    DH->>API: 100% Thẩm quyền cấu hình Follow-up thi thật (has_follow_up, 1-5 câu, mặc định 2 câu)
    EO->>API: POST /api/v1/official-exams/sessions (Khởi tạo kỳ thi OfficialExamSession)
    EO->>API: Cấu hình Ca thi (Shifts: phòng lab, kíp thi, phân công proctor PR & examiner GV, ExamInputMode, BufferSeconds)
    API->>DB: INSERT INTO official_exam_sessions, real_exam_session_shifts
    DB-->>API: Ghi nhận cấu hình kỳ thi & ca thi thành công
    API-->>EO: HTTP 201 Created

    Note over PR,LabPC: PHA 1: ĐIỂM DANH STT, KIỂM TRA IP & MIC-CHECK 30S
    PR->>API: Điểm danh thí sinh theo STT (SeatNumber 1-40), đối chiếu thẻ SV/CCCD & kiểm tra ip_address
    EO->>API: Mở xuất bản đề ca thi (POST /publish-exam -> is_exam_published = true)
    API->>DB: Khởi tạo ca thi phòng lab (status: SCHEDULED), gán thí sinh vào Booth 1-40
    SV->>LabPC: Ngồi đúng máy theo STT, đăng nhập MSSV
    LabPC->>LabPC: Fullscreen Kiosk Lockdown (chặn F12, Alt+Tab, Ctrl+C/V, window.onblur)
    LabPC->>LabPC: Bắt buộc Mic-Check 30s kiểm tra phần cứng âm thanh (>= 60dB)
    
    Note over GV,LabPC: PHA 2: THI VẤN ĐÁP THEO EXAM_INPUT_MODE & NIÊM PHONG AUDIO
    GV->>API: Phát lệnh bắt đầu ca thi toàn phòng Lab
    API->>LabPC: SignalR đồng bộ mở đề thi kèm cấu hình ExamInputMode & Follow-up của Môn thi
    loop Cho từng câu hỏi thứ i (1 ≤ i ≤ N)
        LabPC->>LabPC: TTS đọc đề thi qua tai nghe & bật đồng hồ đếm ngược
        SV->>LabPC: Phát biểu câu trả lời vào micro (MediaRecorder thu âm webm/opus)
        opt Môn thi có cấu hình Follow-up ([OfficialExamSession.has_follow_up == true])
            LabPC->>API: AI Quick Context Evaluation (Phân tích luận điểm [Needs Follow-up])
            API-->>LabPC: Cấp câu hỏi phụ theo ngữ cảnh câu trả lời (tối đa 1–5 câu, mặc định 2 câu)
            LabPC->>LabPC: Phát audio câu hỏi phụ & SV trả lời vào micro
        end
        Note over LabPC,Cloud: Đóng gói STT_MSSV.webm, tính SHA-256 seal
        LabPC->>Cloud: Stream file audio STT_MSSV.webm lên Cloudflare R2 qua Presigned URL
        LabPC->>API: POST /api/v1/exam-lab/submit-question (STT_MSSV, audio_r2_url, sha256_hash)
        API->>DB: Lưu bản ghi nộp bài (status='SUBMITTED', audio_r2_url, sha256_hash)
    end

    Note over SV,LabPC: PHA 3: KIOSK KHÓA AN TOÀN, PERSIST FIRST (<100ms) & HÀNG ĐỢI CHỊU TẢI
    SV->>LabPC: Bấm nút xác nhận nộp toàn bài (hoặc hết giờ tự thu bài)
    LabPC->>API: POST /api/v1/official-exams/submissions (Hoàn tất bài thi)
    Note over API,DB: Persist-First Ingestion (< 100ms)
    API->>DB: INSERT exam_question_submissions & UPDATE student_exam_tickets SET status='SUBMITTED'
    LabPC-->>SV: Màn hình Kiosk KHÓA AN TOÀN (< 100ms), thông báo bài đã lưu an toàn, điểm do GV công bố
    SV->>LabPC: Ký biên bản giấy tại bàn giám thị, tháo tai nghe và trật tự rời phòng thi
    
    API->>Worker: Enqueue vào BoundedChannel 1,000 slots RAM (FullMode.Wait)
    Worker->>Worker: Gemini 1.5 Pro chấm Rubric 10.0, tính conf & is_suspicious (Polly Retry 3 lần, DLQ 5m khi lỗi)
    Worker->>DB: UPDATE student_exam_tickets SET status='AI_GRADED', ai_score=score, ai_confidence_score=conf, is_suspicious=suspicious
    Note over Worker,DB: Toàn bộ bài thi đã sẵn sàng ở trạng thái AI_GRADED cho Giảng viên hậu kiểm

    Note over GV,AuditUI: PHA 4: CỔNG HẬU KIỂM GIẢNG VIÊN & PHÂN LOẠI BÀI NGHI NGỜ (AI DOUBT GUARD)
    GV->>AuditUI: Đăng nhập Cổng Hậu kiểm, chọn ca thi phòng Lab cần thẩm định
    AuditUI->>API: GET /api/v1/audit/submissions?shiftId={shiftId}
    API->>DB: Truy vấn dữ liệu ca thi
    DB-->>API: Danh sách bài thi kèm chỉ số AI chấm
    API-->>AuditUI: Dữ liệu phân loại 2 nhóm:
    AuditUI-->>GV: Nhóm 1 (Đáng nghi ngờ / Cần can thiệp: is_suspicious==true HOẶC confidence_score < 0.70) xếp lên đầu ưu tiên; Nhóm 2 (Độ tin cậy cao)
    
    loop Giảng viên thẩm định các bài thuộc Nhóm 1 (Đáng nghi ngờ)
        GV->>AuditUI: Chọn bài thi nghi ngờ, nghe lại STT_MSSV.webm trên Waveform Player (1.0x-1.5x)
        GV->>AuditUI: Bấm vào timestamp citations để nghe chính xác đoạn phát âm nghi vấn
        alt Giảng viên đồng ý với điểm AI
            GV->>AuditUI: Giữ nguyên điểm đề xuất
        else AI chấm chưa sát do tiếng ồn / nói ngập ngừng
            GV->>AuditUI: Điều chỉnh điểm tiêu chí & Bắt buộc nhập lý do giải trình (override_reason)
        end
        AuditUI->>API: POST /api/v1/exam-audit/save-audit (status='AUDITED')
    end

    Note over GV,Portal: PHA 5: GIẢNG VIÊN CÔNG BỐ ĐIỂM (PUBLISH), ONE-WAY LOCK & PHÚC KHẢO NỘI BỘ
    GV->>AuditUI: Sau khi hoàn tất kiểm tra ca thi, bấm nút "Công Bố Điểm" (Publish Grades)
    AuditUI->>API: POST /api/v1/official-exams/shifts/{shiftId}/publish-grades
    Note over API: Kiểm tra 100% sinh viên ca thi đã có điểm hoàn chỉnh (HTTP 422 nếu thiếu)
    API->>DB: BEGIN TRANSACTION
    API->>DB: UPDATE student_exam_tickets SET status='PUBLISHED', is_locked=true
    API->>DB: UPDATE official_exam_sessions SET status='LOCKED'
    API->>DB: INSERT INTO audit_logs (gv_id, action='PUBLISH_GRADES', timestamp)
    API->>DB: COMMIT TRANSACTION
    Note over API,DB: OneWayLockInterceptor kích hoạt vĩnh viễn (Chặn mọi UPDATE/DELETE bằng HTTP 403 Forbidden; mở ngoại lệ cho Giảng viên chấm lại đơn phúc khảo)
    
    par Xuất bảng điểm cho Phòng Khảo thí
        API->>AuditUI: Xuất file bảng điểm Excel định dạng chuẩn FPT & PDF có chữ ký số
        AuditUI-->>GV: Tải file nộp về Phòng Khảo thí Đại học FPT
    and Sinh viên xem điểm trên Student Portal
        SV->>Portal: Đăng nhập Student Portal xem bảng điểm chính thức do Giảng viên công bố
        Portal->>API: GET /api/v1/student/official-exams/{ticketId}/grade
        API-->>Portal: Bảng điểm chính thức: Tổng điểm, điểm chi tiết rubric, nhận xét
        Portal-->>SV: Hiển thị bảng điểm chính thức
        alt Sinh viên chấp nhận điểm
            SV->>Portal: Bấm nút "Xác Nhận Nhận Điểm"
            Portal->>API: POST /api/v1/student/official-exams/{ticketId}/acknowledge-grade
            API->>DB: UPDATE student_exam_tickets SET student_acknowledgement_status='ACKNOWLEDGED'
            Portal-->>SV: "Xác nhận nhận điểm thành công. Chúc mừng bạn đã hoàn thành kỳ thi môn học!"
        else Sinh viên không chấp nhận điểm (Quy trình Phúc khảo 5 Bước - R7)
            SV->>Portal: Bấm nút "Nộp Đơn Phúc Khảo" trực tiếp trên Student Portal, nhập lý do
            Portal->>API: POST /api/v1/student/appeals (TicketId, Reason)
            API->>DB: INSERT INTO appeal_requests (status='SUBMITTED_TO_EXAM_OFFICE')
            API-->>Portal: HTTP 201 Created (Đơn gửi Cán bộ Khảo thí tiếp nhận)
            Portal-->>SV: Thông báo: "Đã nộp đơn phúc khảo thành công. Cán bộ Khảo thí sẽ tiếp nhận và thẩm tra hồ sơ."
            Note over EO,DH: BƯỚC 2: KHẢO THÍ THẨM TRA HỢP LỆ & CHUYỂN GIAO TRƯỞNG BỘ MÔN
            EO->>API: POST /api/v1/examination-officer/appeals/{id}/forward-to-head (Notes)
            API->>DB: UPDATE appeal_requests SET status='FORWARDED_TO_DEPT_HEAD'
            Note over DH,GV: BƯỚC 3 & 4: TRƯỞNG BỘ MÔN PHÂN CÔNG GIẢNG VIÊN CHẤM LẠI
            DH->>API: POST /api/v1/department-head/appeals/{id}/assign-lecturer (AssignedLecturerId)
            API->>DB: UPDATE appeal_requests SET assigned_lecturer_id=assigned_lecturer_id, status='ASSIGNED_TO_LECTURER'
            GV->>API: PUT /api/v1/lecturer/appeals/{id}/regrade (Chấm lại trên Evidence Panel, mở đường hợp lệ OneWayLockInterceptor)
            API->>DB: UPDATE appeal_requests SET status='REGRADED_BY_LECTURER'
            Note over DH,DB: BƯỚC 5: TRƯỞNG BỘ MÔN PHÊ DUYỆT QUYẾT ĐỊNH & CẬP NHẬT ĐIỂM
            DH->>API: POST /api/v1/department-head/appeals/{id}/decision (APPROVED | REJECTED)
            API->>DB: UPDATE appeal_requests SET status='APPROVED' & Cập nhật điểm chính thức vào vé thi
        end
    end
```

### <a id="phan-66"></a>6.6. Xử Lý 4 Cạnh Biên Nghiệp Vụ Đặc Thù (Edge-Cases Resolution)

Để bảo đảm tính ổn định tuyệt đối của hệ thống trong môi trường thi thật và thi thử, 4 cạnh biên nghiệp vụ sau đã được thiết kế và giải quyết triệt để:

1. **[Session Resumption] Phục hồi phiên thi do rớt mạng / mất điện máy Kiosk (MF-04):**
   - **Giải pháp:** Áp dụng cơ chế **Checkpoint / Heartbeat**. Backend lưu trạng thái tiến độ (question_index, câu trả lời lưu tạm) sau mỗi thao tác chuyển câu. Khi sinh viên mở lại Kiosk hoặc đăng nhập lại, hệ thống nạp lại đúng tiến độ câu hỏi hiện tại, bảo đảm không mất dữ liệu các câu đã trả lời.

2. **[R2 Direct Upload] Giải quyết nút thắt cổ chai băng thông khi nộp bài đồng loạt (MF-04):**
   - **Giải pháp:** Đổi sang kiến trúc **Presigned URL 2-bước**. Khi bắt đầu thi, Backend sinh sẵn Token và Presigned URL. Máy Kiosk sẽ upload trực tiếp file audio STT_MSSV.webm lên Cloudflare R2 qua mạng diện rộng, bỏ qua kênh mạng nội bộ của Backend. Sau khi upload thành công, Kiosk chỉ gửi mã băm SHA-256 của file về Backend để niêm phong và xác nhận nộp bài, giúp giảm 99% tải băng thông cho máy chủ .NET 8.

3. **[Booth Reassignment] Xử lý đổi máy trạm do hỏng phần cứng Micro (MF-04):**
   - **Giải pháp:** Trong giai đoạn 30s kiểm tra phần cứng (Hardware Mic-Check), nếu máy trạm không đạt chuẩn (Micro < 60dB), sinh viên báo Giám thị. Giám thị kiểm tra thực tế, lập biên bản sự cố phòng thi và báo Cán bộ Khảo thí. Cán bộ Khảo thí sử dụng quyền quản trị ca thi để gỡ niêm phong và cập nhật `ip_address` máy trạm dự phòng cho vé thi của sinh viên. Giám thị hướng dẫn sinh viên sang máy mới và điểm danh xác nhận lại vị trí chỗ ngồi. Giám thị tuyệt đối không tự ý gỡ niêm phong IP hay chuyển đổi máy trên hệ thống.

4. **[Token Expiration] Quản lý Token hết hạn trong ca thi kéo dài 60-90 phút (MF-04/MF-02):**
   - **Giải pháp:** Tích hợp cơ chế **Silent Refresh Token** ngầm bằng Axios Interceptor tại Frontend. Khi Access Token (thường có tuổi thọ 1 giờ) sắp hết hạn, hệ thống tự động xin cấp Token mới qua Refresh Token ở chế độ nền, bảo đảm luồng thi không bị gián đoạn và không văng ra màn hình đăng nhập.

### <a id="phan-67"></a>6.7. Luồng Nghiệp Vụ Bổ Trợ: Phân Cấp Học Thuật 4 Tầng (Academic Hierarchy - R5)

Để quản lý đào tạo chặt chẽ theo chuẩn phân cấp của Đại học FPT TP.HCM, hệ thống thiết lập chuỗi phân cấp học thuật 4 tầng nghiêm ngặt (Academic Hierarchy):

```mermaid
flowchart TD
    subgraph T1 [Tầng 1: Quản Trị Viên - Admin]
        A[Admin] -->|1. Tạo học kỳ Semester| B[Học kỳ Semesters]
        A -->|2. Tạo môn học Course| C[Môn học Courses]
        A -->|3. Gán Trưởng Bộ Môn| D[department_head_id trên Course]
    end

    subgraph T2 [Tầng 2: Trưởng Bộ Môn - Department Head]
        DH[Trưởng Bộ Môn] -->|4. Phân công giảng dạy N-N| E[Bảng course_lecturers]
        DH -->|5. Quản lý danh mục CLO| F[Bảng course_clos 3NF]
        DH -->|6. Quản lý cấu trúc đề thi| G[Bảng exam_structures]
    end

    subgraph T3 [Tầng 3: Giảng Viên - Lecturer]
        L[Giảng Viên được phân công] -->|7. Tạo Lớp học phần| H[Bảng classes]
        H -->|Sinh join_code & join_password_hash| I[Mật khẩu & Mã lớp]
        L -->|8. Cấp/Reset mật khẩu lớp| I
    end

    subgraph T4 [Tầng 4: Sinh Viên - Student]
        S[Sinh Viên] -->|9. Nhập join_code + mật khẩu| J[Bảng class_enrollments]
        J -->|Trạng thái ENROLLED| K[Tham gia học tập & Luyện tập]
    end

    T1 --> T2
    T2 --> T3
    T3 --> T4
```

#### Quy tắc nghiệp vụ Phân cấp học thuật (R5):
1. **Admin (Tầng 1):** Tạo học kỳ (`semesters`), môn học (`courses`), và chỉ định duy nhất một Trưởng Bộ Môn chịu trách nhiệm chuyên môn thông qua cột `department_head_id` trong bảng `courses`.
2. **Trưởng Bộ Môn (Tầng 2):** Phân công một hoặc nhiều Giảng viên phụ trách giảng dạy môn học thông qua bảng trung gian chuẩn hóa 3NF `course_lecturers` (`course_id`, `lecturer_id`, `semester_id`, `assigned_at`).
3. **Giảng Viên (Tầng 3):** Chỉ những Giảng viên được Trưởng BM phân công trong `course_lecturers` mới có quyền tạo Lớp học (`classes`) thuộc môn học đó. Mỗi lớp học sinh một `join_code` duy nhất và mật khẩu tham gia được băm an toàn (`join_password_hash`). Giảng viên có toàn quyền cấp lại hoặc đặt lại mật khẩu lớp khi cần.
4. **Sinh Viên (Tầng 4):** Sinh viên sử dụng mã lớp (`join_code`) và nhập đúng mật khẩu để ghi danh vào lớp (`class_enrollments`). Hệ thống ghi nhận trạng thái tham gia, bảo đảm quyền riêng tư và cô lập dữ liệu giữa các lớp học phần.

### <a id="phan-68"></a>6.8. Luồng Nghiệp Vụ Bổ Trợ: Ca Thi Vấn Đáp Tại Lớp & Mật Khẩu Xoay Vòng Động (In-Class Exam Shifts & TOTP - R6)

Bên cạnh kỳ thi tập trung phòng Lab (MF-04 do Khảo thí chủ trì), hệ thống hỗ trợ Giảng viên tổ chức các ca thi vấn đáp định kỳ trực tiếp trên lớp (In-Class Exam Shifts) phục vụ đánh giá tiến trình (Progress Assessment / Assignment / On-going Oral Exam):

```mermaid
sequenceDiagram
    autonumber
    actor L as Giảng Viên (Lecturer)
    actor S as Sinh Viên (Student)
    participant API as InClassShiftController (.NET 8)
    participant TOTP as TotpPasswordEngine
    participant DB as PostgreSQL 16 (3NF)
    participant CH as BoundedChannel (1000 slots)
    participant AI as Gemini 1.5 Flash Worker

    Note over L,API: 1. Khởi tạo ca thi tại lớp
    L->>API: POST /api/v1/lecturer/classes/{classId}/in-class-shifts (Tên ca, thời lượng, câu hỏi)
    API->>DB: Tạo in_class_exam_shifts (status = DRAFT / OPEN)
    API-->>L: HTTP 201 Created (Shift Info)

    Note over L,TOTP: 2. Kích hoạt mật khẩu xoay vòng động (TOTP)
    loop Mỗi chu kỳ 5 phút
        TOTP->>TOTP: Sinh mật khẩu ngẫu nhiên 6 ký tự
        TOTP->>DB: Lưu in_class_shift_passwords (valid_from, valid_to, grace_until +30..60s)
        TOTP-->>L: Hiển thị mật khẩu xoay vòng trên Dashboard máy chiếu
    end

    Note over S,API: 3. Sinh viên vào ca thi
    S->>API: POST /api/v1/student/in-class-shifts/{shiftId}/join (Nhập mật khẩu hiện tại)
    API->>DB: Kiểm tra mật khẩu (hợp lệ trong window hoặc grace period 30-60s)
    API->>DB: Ghi nhận in_class_shift_participants (status = JOINED)
    API-->>S: HTTP 200 OK (Cấp đề thi tại lớp)

    Note over S,CH: 4. Trả lời & Nộp bài thi tại lớp
    S->>API: POST /api/v1/student/in-class-shifts/{shiftId}/submit (Transcript câu trả lời)
    API->>DB: Ghi nhận in_class_exam_submissions (Persist First < 100ms)
    API->>CH: Đẩy EvaluationTask vào BoundedChannel
    API-->>S: HTTP 202 Accepted (Đã nộp bài thành công)

    Note over CH,AI: 5. AI chấm tự động ngầm
    CH->>AI: Worker đọc task, gọi Gemini 1.5 Flash CoT Grading
    AI->>DB: Cập nhật ai_score, ai_feedback vào in_class_exam_submissions

    Note over L,DB: 6. Giảng viên hậu kiểm & Thẩm định điểm
    L->>API: GET /api/v1/lecturer/in-class-shifts/{shiftId}/submissions
    API-->>L: Danh sách bài nộp kèm điểm AI gợi ý
    L->>API: PUT /api/v1/lecturer/in-class-shifts/{shiftId}/submissions/{id}/grade (final_score, feedback)
    API->>DB: Cập nhật final_score, lecturer_comment, audited_at
    L->>API: POST /api/v1/lecturer/in-class-shifts/{shiftId}/finalize (Khóa bảng điểm ca thi)
    API->>DB: Cập nhật in_class_exam_shifts (status = FINALIZED)
    API-->>L: HTTP 200 OK (Bảng điểm lớp hoàn tất)
```

#### Quy tắc kiến trúc Ca thi tại lớp & Mật khẩu xoay vòng (R6):
1. **Phân quyền độc lập:** Giảng viên phụ trách lớp toàn quyền tạo, mở ca, cấu hình đề từ kho câu hỏi môn học và đóng ca thi tại lớp (`in_class_exam_shifts`). Khảo thí và Trưởng BM không can thiệp vào ca thi nội bộ này.
2. **Bảo mật phòng thi vật lý bằng TOTP:** Để chống sinh viên bên ngoài phòng học vào thi hộ, hệ thống áp dụng cơ chế mật khẩu xoay vòng động theo thời gian:
   - Mật khẩu gồm 6 ký tự số/chữ cái ngẫu nhiên thay đổi mỗi **5 phút** một lần.
   - Bảng `in_class_shift_passwords` lưu trữ lịch sử: `valid_from`, `valid_to` và khoảng thời gian gia hạn ân hạn (`grace_until`) thêm **30–60 giây** để chống rớt mạng / độ trễ mạng khi sinh viên bấm tham gia ở giây cuối.
   - Tuyệt đối lưu trữ trạng thái cửa sổ TOTP trong CSDL chuẩn 3NF, không sử dụng bộ nhớ RAM tạm thời (`[NotMapped]` / Static Dictionary) để bảo đảm tính nhất quán khi scale ngang máy chủ.
3. **Pipeline chấm ngầm & Thẩm định điểm:**
   - Bài nộp sinh viên ghi đĩa tức thời Persist-First vào `in_class_exam_submissions` ($< 100\text{ms}$).
   - Đưa vào `BoundedChannel<EvaluationTask>` (1,000 slots) chấm tự động qua Gemini 1.5 Flash CoT.
   - Giao diện Giảng viên cho phép rà soát điểm AI gợi ý, điều chỉnh điểm (`final_score`) và ghi chú nhận xét trước khi bấm Hoàn tất / Khóa bảng điểm (`FINALIZED`).

---

## <a id="phan-7"></a>🛡️ PHẦN 7: KIẾN TRÚC PHÒNG THỦ 4 TẦNG CHỊU TẢI & CHỐNG MẤT DỮ LIỆU (ZERO DATA LOSS)

### <a id="phan-71"></a>7.1. Sơ Đồ Cơ Chế Phòng Thủ 4 Tầng Liên Hoàn (Mermaid)

```mermaid
flowchart TD
    subgraph Defense_Tier1 ["TẦNG 1: PERSIST-FIRST INGESTION (< 100ms)"]
        direction TB
        Client_Req["Request Nộp Bài (MF-01 / MF-02 / MF-04)<br>• Transcript / Audio URL / Metadata"]
        API_Gate["REST API Controller (.NET 8 Web API)<br>• Nhận Payload, kiểm tra Model State"]
        DB_Persist["Ghi Ngay Vào PostgreSQL 16 Dưới 100ms (Persist First)<br>• MF-04: Lưu exam_question_submissions & cập nhật ticket status = 'SUBMITTED'<br>• MF-01/02: Lưu answers với status = 'pending'<br>• Bản ghi được bảo vệ an toàn trên đĩa cứng"]
        HTTP_Ack["Trả Mã Phản Hồi Tức Thì: HTTP 202 Accepted<br>• Tránh treo kết nối Client, giải phóng HTTP Worker"]

        Client_Req --> API_Gate --> DB_Persist --> HTTP_Ack
    end

    subgraph Defense_Tier2 ["TẦNG 2: IN-MEMORY BOUNDED CHANNEL (1,000 SLOTS)"]
        direction TB
        Channel_Enqueue["Đẩy Tác Vụ Vào Bounded Channel<br>• System.Threading.Channels.ChannelWriter<br>• Gói gọn task: answer_id, session_id, criteria"]
        Channel_Buffer["Hàng Đợi Bộ Nhớ RAM 1,000 Slots<br>• Chế độ: BoundedChannelFullMode.Wait<br>• Tự động Backpressure khi tải cao, chống Out-Of-Memory"]
        Worker_Dequeue["GradingQueueWorker Rút Tác Vụ<br>• Background IHostedService chạy đa luồng an toàn<br>• Đọc tuần tự ChannelReader không xung đột"]

        HTTP_Ack -.->|Non-blocking dispatch| Channel_Enqueue --> Channel_Buffer --> Worker_Dequeue
    end

    subgraph Defense_Tier3 ["TẦNG 3: POLLY EXPONENTIAL BACKOFF RETRY"]
        direction TB
        AI_Call["Gọi API Dịch Vụ AI Đám Mây<br>• Whisper STT bóc băng hoặc Gemini 1.5 Flash/Pro chấm Barem"]
        Check_AI_Status{"Kết Quả Cuộc Gọi AI?"}
        AI_Success["Thành Công (HTTP 200 OK)<br>• Trả kết quả JSON Structured Scorecard"]
        Polly_Retry["Kích Hoạt Chính Sách Polly Retry<br>• Áp dụng khi gặp lỗi: HTTP 429, HTTP 503, Network Timeout<br>• Thử lại 3 lần với khoảng cách giãn: 2s -> 4s -> 8s"]
        Check_Retry_Count{"Đã Đạt Giới Hạn 3 Lần Thử Lại?"}

        Worker_Dequeue --> AI_Call --> Check_AI_Status
        Check_AI_Status -- Thành công --> AI_Success
        Check_AI_Status -- Lỗi kết nối / Quá tải --> Polly_Retry --> Check_Retry_Count
        Check_Retry_Count -- Còn lượt thử lại --> AI_Call
    end

    subgraph Defense_Tier4 ["TẦNG 4: DEAD-LETTER QUEUE (DLQ) & TỰ ĐỘNG CHẤM BÙ"]
        direction TB
        DLQ_Save["Cách Ly Vào Bảng dead_letter_queues<br>• Lưu toàn bộ answer_id, stack_trace, payload gốc, error_message<br>• Phân định flow_type: PRACTICE_MOCK vs OFFICIAL_EXAM<br>• Đặt status bài thi = 'PENDING_RETRY'"]
        Dlq_Worker["DlqReplayWorker (Background Hosted Service)<br>• Định kỳ 5 phút quét các bản ghi trong dead_letter_queues<br>• Kiểm tra sức khỏe dịch vụ AI (Circuit Breaker)"]
        Admin_DLQ_Action["Cổng Giám Sát Của Quản Trị Viên (Admin Portal)<br>• Theo dõi lỗi & Replay thủ công DLQ MF-01 & MF-02"]
        ExamOfficer_DLQ_Action["Cổng Giám Sát Của Khảo Thí (Exam Officer Console)<br>• Theo dõi lỗi & Replay thủ công DLQ thi thật MF-04"]
        Replay_Success["Chấm Bù Thành Công<br>• Cập nhật status = 'GRADED'<br>• Xóa khỏi danh sách chờ xử lý DLQ"]

        Check_Retry_Count -- Thất bại cả 3 lần --> DLQ_Save
        DLQ_Save --> Dlq_Worker
        DLQ_Save -.-> Admin_DLQ_Action
        DLQ_Save -.-> ExamOfficer_DLQ_Action
        Admin_DLQ_Action -->|Thủ công Replay MF-01/02| Channel_Enqueue
        ExamOfficer_DLQ_Action -->|Thủ công Replay MF-04| Channel_Enqueue
        Dlq_Worker -->|Tự động đẩy lại vào hàng đợi| Channel_Enqueue
        AI_Success --> Replay_Success
    end
```

### <a id="phan-72"></a>7.2. Phân Tích Kỹ Thuật Từng Tầng Chống Chịu Sự Cố

1. **Tầng 1: Persist-First Ingestion (< 100ms):**
   - Khi bài nộp từ Client gửi lên Backend (đặc biệt là bài thi thật phòng Lab MF-04 qua `POST /api/v1/official-exams/submissions`), Controller không gọi AI ngay mà chỉ thực hiện validate cú pháp, ghi ngay bản ghi vào `exam_question_submissions` và cập nhật `student_exam_tickets` với trạng thái **`SUBMITTED` trong $< 100$ms** (đối với MF-01/02 là lưu `practice_answers` / `mock_exam_answers` với trạng thái `pending`).
   - Ngay sau khi bản ghi được `SaveChanges` xuống đĩa cứng, hệ thống trả về ngay phản hồi tức thì: mã `HTTP 200 OK` hiển thị màn hình khóa an toàn trên máy Kiosk MF-04 (hoặc mã `HTTP 202 Accepted` cho MF-01/02). Thời gian thực thi toàn bộ luồng này đo lường thực tế đạt dưới 100ms. Thí sinh và máy trạm Kiosk được giải phóng kết nối ngay lập tức, bảo đảm Zero Data Loss 100%.
2. **Tầng 2: In-Memory Bounded Channel (1,000 Slots):**
   - Sử dụng thư viện chuẩn hiệu năng cao `System.Threading.Channels` của .NET 8.
   - Dung lượng hàng đợi cố định ở mức **1,000 tasks**. Cấu hình cờ `BoundedChannelFullMode.Wait`. Nếu lưu lượng đột biến vượt quá 1,000 bài nộp đang chờ, luồng ghi sẽ tạm hoãn an toàn thay vì tiếp tục cấp phát RAM, ngăn ngừa triệt để lỗi Out-Of-Memory (OOM) làm sập container.
3. **Tầng 3: Polly Exponential Backoff Retry & Quy Chuẩn Timeout Hai Tầng:**
   - Dịch vụ AI đám mây (Google Gemini API) và Cloudflare Whisper STT có thể gặp lỗi tạm thời như nghẽn mạng, lỗi `HTTP 429 Resource Exhausted` hoặc `HTTP 503 Service Unavailable`.
   - Hệ thống cấu hình chính sách Polly v8 tự động thử lại tối đa 3 lần với khoảng cách thời gian dãn cách theo hàm mũ: **2 giây $\rightarrow$ 4 giây $\rightarrow$ 8 giây** (tổng thời gian giãn cách chờ riêng giữa các lần thử là $2\text{s} + 4\text{s} + 8\text{s} = 14\text{s}$). Hơn 95% các sự cố nghẽn mạng tức thời đều được giải quyết tự động ở tầng này mà không cần can thiệp thủ công.
   - **Quy tắc phân định Timeout hai tầng (Two-Tier Timeout Separation):**
     - *Attempt Timeout (Thời hạn cho mỗi lượt gọi đơn lẻ):* Cấu hình `15s` cho Whisper STT Service và `30s` cho Gemini AI Question Generator / Evaluator (do khối lượng tạo nội dung đề bài, Barem 10.0 và Model Answer lớn).
     - *Total Request Timeout (Tổng thời hạn tối đa toàn bộ pipeline):* Cấu hình `45s` cho Whisper STT và `90s` cho Gemini AI Generator.
     - *Cảnh báo kỹ thuật Backend .NET 8:* Tuyệt đối **CẤM** gán cứng `HttpClient.Timeout = TimeSpan.FromSeconds(10)` toàn cục vì sẽ khiến runtime hủy kết nối trước khi Polly kịp hoàn thành chu kỳ thử lại thứ 2 và thứ 3. Bắt buộc sử dụng `AddResilienceHandler` hoặc `ResiliencePipelineBuilder` để quản lý độc lập Attempt Timeout và Total Timeout.
4. **Tầng 4: Dead-Letter Queue (DLQ) & Tự Động Chấm Bù Định Kỳ (Phân định theo R2):**
   - Nếu cuộc gọi AI thất bại cả 3 lần retry, task được đưa vào bảng `dead_letter_queues` trong PostgreSQL kèm toàn bộ stack trace và payload gốc, đánh dấu trạng thái bài thi là `PENDING_RETRY`.
   - Tiến trình nền `DlqReplayWorker` được kích hoạt định kỳ **5 phút / lần**. Worker quét các bản ghi lỗi, kiểm tra sức khỏe API ngoại vi và tự động nạp lại vào Bounded Channel để chấm bù.
   - **Phân định rõ ràng trách nhiệm quản lý:** Quản trị viên (Admin) giám sát và replay cho các tác vụ sự cố của luồng Luyện tập (MF-01) và Thi thử (MF-02); Cán bộ Khảo thí (Examination Officer) giám sát và replay cho các tác vụ bài thi thật phòng Lab (MF-04). Trưởng Bộ Môn bị cắt bỏ hoàn toàn khỏi quyền quản lý DLQ để bảo đảm tính chuyên môn hóa. **Cam kết bảo đảm Zero Data Loss 100% trong mọi tình huống sự cố**.

---

## <a id="phan-8"></a>🌐 PHẦN 8: MA TRẬN CỔNG MẠNG, RANH GIỚI BẢO MẬT & DANH MỤC MÃ LỖI RFC 7807

### <a id="phan-81"></a>8.1. Ma Trận Cổng Mạng & Ranh Giới Phân Vùng Bảo Mật

```
[Public Internet / WAN / Lab PC VLAN]
                 │  HTTPS 443 / HTTP 80 (TLS 1.3 / WSS)
                 ▼
     ┌───────────────────────┐
     │ Cloudflare Anycast WAF│  <-- DDoS Filter, Rate Limit
     └───────────┬───────────┘
                 │  Reverse Proxy Proxy-Pass
                 ▼
     ┌───────────────────────┐
     │  Nginx Ingress (Host) │  <-- SSL Offloading, HSTS, Route Split
     └─────┬───────────┬─────┘
           │ :3000     │ :5000 (REST & WSS)
           ▼           ▼
┌──────────────────┐ ┌───────────────────────────────────────────────┐
│ oralexam-frontend│ │              oralexam-backend                 │
│ (React 19 Vite)  │ │ (.NET 8 Clean Architecture - Kestrel Server)  │
└──────────────────┘ └───────┬───────────────────────────────┬───────┘
                             │ :5432 (Npgsql TCP Pool)       │ :443 (Outbound)
                             ▼                               ▼
                 ┌───────────────────────┐       ┌───────────────────────┐
                 │   oralexam-postgres   │       │   External Cloud:     │
                 │   (PostgreSQL 16)     │       │   • Gemini 1.5 Flash  │
                 │   CẤM MỞ RA NGOÀI!    │       │   • Gemini 1.5 Pro    │
                 └───────────────────────┘       │   • Whisper STT       │
                                                 │   • Cloudflare R2     │
                                                 │   • Google OAuth PKCE │
                                                 └───────────────────────┘
```

### <a id="phan-811"></a>8.1.1. Ma Trận Phân Quyền Vai Trò Người Dùng (RBAC Authorization Matrix - 6 Vai Trò)

Hệ thống thiết lập cơ chế phân quyền Role-Based Access Control (RBAC) nghiêm ngặt với 6 vai trò người dùng được mã hóa trong JWT Claims:

| Tính Năng / Phân Hệ Nghiệp Vụ | `student`<br>(Sinh viên) | `lecturer`<br>(Giảng viên) | `department_head`<br>(Trưởng Bộ Môn) | `examination_officer`<br>(Cán bộ Khảo thí) | `proctor`<br>(Giám thị) | `admin`<br>(Quản trị viên) |
|:---|:---:|:---:|:---:|:---:|:---:|:---:|
| **Chuỗi học thuật:** Quản trị Học kỳ & Môn học | ❌ | ❌ | ❌ | ❌ | ❌ | **✅** |
| **Chuỗi học thuật:** Gán Trưởng BM vào Môn học (`department_head_id`) | ❌ | ❌ | ❌ | ❌ | ❌ | **✅** |
| **Chuỗi học thuật:** Phân công nhiều Giảng viên vào môn học (`course_lecturers`) | ❌ | ❌ | **✅ (Chủ quản môn)** | ❌ | ❌ | **✅** |
| **Chuỗi học thuật:** Tạo Lớp học phần & Quản lý/Reset mật khẩu lớp | ❌ | **✅ (GV được giao)** | **✅** | ❌ | ❌ | **✅** |
| **Chuỗi học thuật:** Sinh viên nhập mật khẩu tự tham gia lớp (`classes/join`) | **✅** | ❌ | ❌ | ❌ | ❌ | ❌ |
| **Ca thi trong lớp:** Tạo & Bắt đầu ca thi, phát sinh OTP xoay vòng 5m (R6) | ❌ | **✅ (GV đứng lớp)** | **✅** | ❌ | ❌ | **✅** |
| **Ca thi trong lớp:** Sinh viên nhập OTP xoay vòng vào làm bài thi | **✅** | ❌ | ❌ | ❌ | ❌ | ❌ |
| **Ca thi trong lớp:** Hậu kiểm bài làm, sửa điểm thành phần & Chốt bảng điểm | ❌ | **✅ (GV đứng lớp)** | **✅** | ❌ | ❌ | **✅** |
| **MF-01:** Luyện tập vấn đáp tương tác (On-Demand & Progressive) | ✅ | ✅ | ✅ | ❌ | ❌ | ✅ |
| **MF-02:** Thi thử bấm giờ (Quota theo ngày do Admin cấu hình) | ✅ | ✅ | ✅ | ❌ | ❌ | ✅ |
| **MF-03:** Quản lý chuẩn đầu ra CLOs (`course_clos` 3NF) | ❌ | ✅ | ✅ | ❌ | ❌ | ✅ |
| **MF-03:** Soạn câu hỏi ôn luyện độc lập (`practice_questions`) | ❌ | ✅ | ✅ | ❌ | ❌ | ✅ |
| **MF-03:** Soạn câu hỏi thi thật bám sát đề thi mẫu Trưởng BM (`exam_questions`) | ❌ | ✅ | ✅ | ❌ | ❌ | ✅ |
| **MF-03:** Giảng viên gửi câu hỏi duyệt Bộ môn (`POST /batch-submit-review`) | ❌ | **✅** | ❌ | ❌ | ❌ | **✅** |
| **MF-03:** Trưởng Bộ Môn thẩm định & Phê duyệt câu hỏi (`POST /review-decision`) | ❌ | ❌ | **✅** | ❌ | ❌ | **✅** |
| **MF-03:** Đóng gói bộ đề ExamSets (`DRAFT` $\to$ `OFFICIAL` $\to$ `HANDED_OVER`) | ❌ | ❌ | **✅** | ❌ | ❌ | **✅** |
| **MF-04:** Cấu hình Follow-up thi thật (`has_follow_up`, 1-5 câu) | ❌ | ❌ | **✅ (Duy nhất 100%)** | ❌ | ❌ | ❌ |
| **MF-04:** Lập kỳ thi, ca thi phòng Lab, phân bổ đề & mở xuất bản đề ca thi | ❌ | ❌ | ❌ | **✅ (Chuyên trách)** | ❌ | **✅** |
| **MF-04:** Điểm danh thí sinh (SeatNumber 1–40, CCCD) & Báo cáo vi phạm an ninh | ❌ | ❌ | ❌ | **✅** | **✅ (Phạm vi duy nhất)** | **✅** |
| **MF-04:** Ghi nhật ký chống gian lận thời gian thực (`exam_anti_cheat_logs`) | ✅ | ❌ | ❌ | **✅** | **✅** | **✅** |
| **MF-04:** Hậu kiểm Evidence Panel & sửa điểm có lý do (`override_reason`) | ❌ | **✅** | **✅** | ❌ | ❌ | **✅** |
| **MF-04:** Công bố điểm ca thi (Publish Grades) kích hoạt One-Way Lock | ❌ | **✅** | ❌ | **✅** | ❌ | **✅** |
| **MF-04:** Sinh viên xem điểm Student Portal & Nộp đơn phúc khảo (`AppealRequest`) | **✅** | ❌ | ❌ | ❌ | ❌ | ❌ |
| **MF-04:** Khảo thí tiếp nhận & thẩm tra hợp lệ đơn phúc khảo rồi chuyển Trưởng BM | ❌ | ❌ | ❌ | **✅** | ❌ | **✅** |
| **MF-04:** Trưởng BM phân công GV chấm lại & Phê duyệt quyết định phúc khảo | ❌ | ❌ | **✅** | ❌ | ❌ | **✅** |
| **MF-04:** Giảng viên chấm lại bài phúc khảo trên Evidence Panel | ❌ | **✅ (GV được giao)** | **✅** | ❌ | ❌ | ❌ |
| **DLQ:** Giám sát & Replay Dead-Letter Queue MF-01 & MF-02 | ❌ | ❌ | ❌ | ❌ | ❌ | **✅** |
| **DLQ:** Giám sát & Replay Dead-Letter Queue MF-04 thi thật | ❌ | ❌ | ❌ | **✅** | ❌ | ❌ |
| Quản trị hệ thống, người dùng, cấu hình MF-01/02 & Audit Logs | ❌ | ❌ | ❌ | ❌ | ❌ | **✅** |

> [!IMPORTANT]
> **Quy trình Phân quyền Tác chiến Chuẩn hóa 6 Vai Trò (R2–R8):**
> 1. **Quyền hạn Giảng viên (`lecturer`):** Nhận phân công giảng dạy môn học từ Trưởng BM (`course_lecturers`); Tạo Lớp học phần thuộc môn được giao và sinh/reset mật khẩu lớp (`join_password_hash`); Tổ chức Ca thi trong lớp (`in_class_exam_shifts`), kích hoạt mật khẩu OTP tự động xoay vòng 5 phút (kèm grace period 30–60s), hậu kiểm bài làm và điều chỉnh điểm thành phần trước khi chốt bảng điểm; Soạn đề (tách độc lập kho luyện tập `practice_questions` và bám sát đề thi mẫu cho kho `exam_questions`); Chấm lại bài thi phúc khảo khi được Trưởng BM phân công.
> 2. **Thẩm quyền của Trưởng Bộ Môn (`department_head`):** Quản trị môn học và danh mục CLO 3NF; Phân công nhiều Giảng viên vào môn học (`course_lecturers`); Nắm 100% thẩm quyền cấu hình câu hỏi tiếp nối Follow-up thi thật (`has_follow_up`, 1–5 câu); Thẩm định và duyệt đề thi (`APPROVED`, `NEEDS_REVISION`, `REJECTED`); Ban hành ma trận cấu trúc đề thi mẫu; Tiếp nhận đơn phúc khảo từ Khảo thí chuyển sang, phân công Giảng viên chấm lại và thẩm định phê duyệt quyết định phúc khảo cuối cùng. **Cắt bỏ hoàn toàn quyền quản lý DLQ.**
> 3. **Thẩm quyền của Cán bộ Khảo thí (`examination_officer`):** Tiếp nhận gói đề `HANDED_OVER`, tạo kỳ thi (`OfficialExamSession`), tạo ca thi (`RealExamSessionShift`), thực hiện phân bổ đề không trùng lặp cho thí sinh trong ca (`uq_tickets_shift_exam_set`), mở xuất bản đề ca thi (`is_exam_published = true`), và thực hiện **"Công Bố Điểm"** (`POST /api/v1/official-exams/shifts/{shiftId}/publish-grades`) kích hoạt One-Way Lock; Tiếp nhận và thẩm tra hồ sơ đơn phúc khảo từ sinh viên rồi chuyển giao cho Trưởng BM; Giám sát và kích hoạt replay Dead-Letter Queue (DLQ) cho bài thi thật MF-04. **Cắt bỏ thẩm quyền cấu hình Follow-up.**
> 4. **Thẩm quyền của Cán bộ Coi thi / Giám thị (`proctor`):** Giới hạn nghiêm ngặt trong 2 nhiệm vụ: (1) Điểm danh thí sinh theo STT ca thi (SeatNumber 1–40) đối chiếu thẻ SV/CCCD và kiểm tra `ip_address`; (2) Báo cáo các hành vi vi phạm an ninh phòng thi vào `exam_anti_cheat_logs`. **Tuyệt đối cấm Proctor mở ca thi, đóng ca thi, gỡ niêm phong IP, reset máy trạm, import danh sách thí sinh, hay can thiệp điểm số.**
> 5. **Quy trình Phúc khảo Nội bộ 5 Bước (MF-04):** Sau khi công bố điểm, sinh viên nộp đơn `AppealRequest` trực tuyến $\to$ Cán bộ Khảo thí tiếp nhận đầu mối, kiểm tra tính hợp lệ và chuyển tiếp cho Trưởng Bộ Môn $\to$ Trưởng Bộ Môn phân công một Giảng viên chấm độc lập lần 2 $\to$ Giảng viên chấm lại trên Evidence Panel và gửi đề xuất điểm kèm biên bản $\to$ Trưởng Bộ Môn thẩm định phê duyệt quyết định (APPROVED / REJECTED), mở chốt One-Way Lock cập nhật điểm chính thức.

### <a id="phan-82"></a>8.2. Danh Mục Mã Lỗi Chuẩn Hóa RFC 7807 (ProblemDetails Matrix)

Tất cả các phản hồi lỗi từ API đều tuân thủ nghiêm ngặt định dạng chuẩn công nghiệp **RFC 7807 (`application/problem+json`)**:

```json
{
  "type": "https://oralexam.fpt.edu.vn/errors/quota-exceeded",
  "title": "Daily Mock Exam Quota Exceeded",
  "status": 429,
  "detail": "Bạn đã hoàn thành tối đa số lượt thi thử trong ngày đối với môn học này do Admin quy định. Vui lòng chuyển sang chế độ Luyện tập tự do hoặc quay lại vào ngày mai.",
  "instance": "/api/v1/mock-exams/start"
}
```

#### Bảng Tra Cứu Mã Trạng Thái & Lỗi Hệ Thống:

| Mã HTTP | Tên mã lỗi | Trường hợp kích hoạt trong hệ thống | Hướng xử lý phía Client UI |
|:---:|:---|:---|:---|
| **`400`** | `Bad Request` | Dữ liệu đầu vào sai định dạng (thiếu trường bắt buộc, sai kiểu dữ liệu, JSON malformed, tỷ lệ phân bổ độ khó không khớp tổng số câu). | Hiển thị thông báo lỗi trường cụ thể bên dưới input tương ứng. |
| **`401`** | `Unauthorized` | Token JWT hết hạn, token không hợp lệ hoặc thiếu tiêu đề `Authorization: Bearer <token>`. | Tự động làm mới token hoặc điều hướng về trang đăng nhập Google Workspace / Password Login. |
| **`403`** | `Forbidden` | **1.** Tài khoản không đủ quyền hạn RBAC truy cập tài nguyên (trong 6 vai trò).<br>**2.** Cố ý sửa điểm khi ca thi đã bị khóa (`is_locked=true` kích hoạt `OneWayLockInterceptor`).<br>**3.** Người dùng không mang vai trò hợp lệ cố ý kích hoạt endpoint AI sinh câu hỏi, duyệt đề Bộ môn hoặc công bố điểm. | Hiển thị màn hình từ chối quyền truy cập hoặc cảnh báo banner đỏ: *"Bạn không có quyền thực hiện thao tác này"* hoặc *"Ca thi đã được khóa một chiều"*. |
| **`404`** | `Not Found` | Không tìm thấy mã môn học, chuẩn đầu ra CLO, ID câu hỏi, ca thi hoặc sinh viên trong CSDL. | Hiển thị giao diện trạng thái 404 Not Found kèm nút quay về trang chủ. |
| **`410`** | `Gone` | **Inactivity Timeout:** Phiên luyện tập (MF-01) vượt quá 5 phút không tương tác (`SessionInactivityTimeoutMinutes = 5` phút) hoặc phiên thi thử đã quá hạn nộp bài (`ExpiresAt + 10s`). | Hiển thị hộp thoại thông báo phiên đã tự động kết thúc an toàn và điều hướng sang màn hình xem kết quả các câu đã làm. |
| **`413`** | `Payload Too Large` | **Image Size Guard:** Giảng viên / Trưởng BM tải lên ảnh sơ đồ minh họa câu hỏi thủ công (`upload-image`) có dung lượng vượt quá trần **5MB** (`5,242,880 bytes`). | Hiển thị cảnh báo đỏ: *"Dung lượng ảnh vượt quá giới hạn tối đa 5MB. Vui lòng nén ảnh trước khi tải lên"*. |
| **`415`** | `Unsupported Media Type` | **Magic Bytes Guard:** Tệp tải lên cho câu hỏi thủ công không khớp chữ ký nhị phân (Magic Bytes) hợp lệ của các định dạng ảnh được phép (`PNG`, `JPG/JPEG`, `WEBP`). | Hiển thị cảnh báo đỏ: *"Định dạng file không hợp lệ. Chỉ chấp nhận ảnh PNG, JPG/JPEG hoặc WEBP chuẩn"*. |
| **`422`** | `Unprocessable Entity` | **FluentValidation Gate:** Cán bộ lưu Barem Rubric có tổng điểm $\sum \text{Tiêu chí} \neq 10.0\text{đ}$, Model Answer $< 50$ ký tự, hoặc cập nhật điểm thẩm định mà để trống lý do giải trình. | Đánh dấu đỏ các tiêu chí điểm / ô nhập đáp án và yêu cầu cân chỉnh lại để tổng điểm đúng bằng 10.0đ và đáp án $\ge 50$ ký tự. |
| **`429`** | `Too Many Requests` | **Quota Guard:** Sinh viên thực hiện lượt thi thử vượt quá số lượt trong ngày do Admin cấu hình (`max_mock_exams_per_day`). | Hiển thị hộp thoại thông báo đã hết hạn ngạch ngày kèm nút chuyển sang chế độ Luyện tập tự do. |
| **`500`** | `Internal Server Error` | Lỗi máy chủ không mong muốn hoặc sự cố cơ sở dữ liệu nghiêm trọng. | Đưa bài thi vào cơ chế phòng thủ DLQ và hiển thị thông báo hỗ trợ kỹ thuật cho sinh viên. |
| **`502`** | `Bad Gateway` | Cổng kết nối ngoại vi bên thứ ba (Google Gemini AI, Cloudflare Whisper STT) bị mất kết nối, từ chối TCP hoặc phản hồi sai định dạng JSON Schema sau 3 lần thử lại theo cấp số nhân (Polly Retry 2s, 4s, 8s). | Hiển thị cảnh báo: *"Dịch vụ ngoại vi tạm thời gián đoạn sau 3 lần thử lại. Vui lòng kiểm tra trạng thái kết nối mạng hoặc thử lại sau"*. |
| **`504`** | `Gateway Timeout` | Dịch vụ AI (Google Gemini AI / Whisper STT) không phản hồi trong thời hạn tối đa của chu trình Resilience Pipeline sau 3 lần thử lại tự động (Polly 2s, 4s, 8s). Phân định: Attempt Timeout 30s; Total Request Timeout 90s. | Hiển thị thông báo: *"Dịch vụ AI phản hồi quá thời gian chờ quy định (Total Timeout). Vui lòng thử lại sau"*. |


### <a id="phan-83"></a>8.3. Cam Kết Mức Độ Dịch Vụ & Mục Tiêu Hiệu Năng (SLA & Latency Targets)

Nhóm phát triển cam kết các chỉ số hiệu năng kỹ thuật định lượng (Service Level Agreements):
- **Độ trễ nhận diện giọng nói Client (Web Speech STT):** $\le 500\text{ms}$ (phản xạ tức thì).
- **Thời gian ghi nhận bài thi xuống đĩa (Persist-First Database Write):** $\le 100\text{ms}$ (trả `HTTP 202 Accepted`).
- **Thời gian phản hồi chấm điểm AI (Gemini 1.5 Flash):** $1.8\text{s} - 2.5\text{s}$ (Trần tối đa $\le 10\text{s}$).
- **Thời gian đẩy Scorecard qua SignalR Core WebSocket:** $\le 100\text{ms}$ sau khi AI chấm xong.
- **Cam kết toàn vẹn dữ liệu (Zero Data Loss Guarantee):** $100\%$ bài thi được lưu đĩa an toàn và cách ly qua DLQ khi gặp sự cố mạng ngoài.
- **Cam kết pháp lý khảo thí:** $100\%$ điểm ca thi sau khi bấm nút One-Way Lock được niêm phong bất biến qua interceptor ở cấp độ CSDL.

---
*Tài liệu kiến trúc hệ thống chính thức duy nhất (Single Source of Truth) phục vụ báo cáo Hội đồng chấm tốt nghiệp Đồ án Capstone FA26SE166 — Khoa Kỹ Thuật Phần Mềm — Đại học FPT TP.HCM (FPT SG).*

