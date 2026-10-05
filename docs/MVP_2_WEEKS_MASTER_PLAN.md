# 🎯 KẾ HOẠCH TÁC CHIẾN SPRINT MVP 2 TUẦN (MASTER PLAN v5.0)
## ĐỒ ÁN TỐT NGHIỆP SEP490 (FA26SE166) — HỆ THỐNG LUYỆN THI & ĐÁNH GIÁ VẤN ĐÁP BẰNG LLM
### MA TRẬN PHÂN BỔ CÂN BẰNG TẢI 4 THÀNH VIÊN (25% MỖI NGƯỜI) | LỊCH REVIEW THỨ 3 - THỨ 5 - THỨ 7

---

> [!IMPORTANT]
> **NGUYÊN TẮC CÂN BẰNG TẢI & PHÂN ĐỊNH RANH GIỚI TRÁCH NHIỆM:**
> - **Tổng khối lượng công việc được chia đều 4 phần (25% / người), khắc phục triệt để tình trạng dồn tải lên Lead BE:**
>   - 🧑 **Nguyễn Quang Thành (25% — Lead BE & Core Architecture):** Khung Clean Architecture 4 tầng, DI, Middleware RFC 7807, Auth Google PKCE, Hàng đợi BoundedChannel 1,000 slots, SignalR Hub, Quota Guard MF-02 ($K \le 3$), Ca thi Kiosk IP Binding MF-04, Khóa 1 chiều `OneWayLockInterceptor`, Phúc khảo nội bộ Backend, và Xuất Excel Khảo thí FPT.
>   - 🧑 **Nguyễn Trọng Tốt (25% — BE Developer, AI Specialist & QA Lead):** San sẻ tải BE: Đảm nhận trọn vẹn Phân hệ Barem Rubric 10.0đ & Ngân hàng câu hỏi MF-03 (`Features/Rubrics/*`, `Features/Questions/*`), Gemini 1.5 CoT 3 bước, Whisper STT Server-side, Cloudflare R2 Presigned URL + SHA-256, AI Doubt Guard, Cổng Hậu kiểm Evidence Panel Backend, và toàn bộ bộ Test Suites xUnit / NetArchTest.
>   - 🧑 **Nguyễn Đăng Hải (25% — DB & Infrastructure + Frontend Developer):** Khởi tạo 28 bảng PostgreSQL 16 (DDL, Constraints, Indexes, Seed, Docker Compose). **Sau khi xong DB, Hải chuyển 100% sang code Frontend**: Student Portal Dashboard, Giao diện Luyện tập MF-01, Giao diện Thi thử MF-02 (Voice-First khóa text), Giao diện duyệt đề Trưởng BM, và Phân hệ Phúc khảo (Sinh viên & Trưởng BM).
>   - 🧑 **Lê Vũ Hoàng (25% — Lead Frontend Architect):** Kiến trúc tổng thể React 19, Vite, Tailwind CSS v4, Router v7, Zustand, Axios Interceptor RFC 7807, Buffer Screen đếm ngược `transcript_buffer_seconds`, Kiosk Lockdown Anti-Cheat (Fullscreen, chặn phím, 3-blur đình chỉ), Web Audio đo Mic $\ge 60$dB, Waveform Audio Player tua theo từ, và SignalR Client.

---

## 📊 BẢNG TỔNG HỢP TIẾN ĐỘ & PHÂN VAI THEO TIMELINE 2 TUẦN

```text
TUẦN 1: NỀN TẢNG HỆ THỐNG, LUỒNG LUYỆN TẬP (MF-01) & RUBRIC STUDIO 10.0 (MF-03)
├── Thứ 3: Setup Móng CSDL (Hải) + Clean Arch & Auth (Thành) + Rubric Validation (Tốt) + Base React 19 (Hoàng)
├── Thứ 5: Hàng Đợi 1,000 slots & SignalR (Thành) + Gemini CoT (Tốt) + Buffer Screen STT (Hoàng) + Student Portal (Hải)
└── Thứ 7 [GATE 1]: API Rubric & FLM (Tốt) + Worker Polly Retry (Thành) + Rubric Studio UI (Hoàng) + Duyệt Đề UI (Hải)

TUẦN 2: THI THỬ (MF-02), KIOSK LAB (MF-04), HẬU KIỂM EVIDENCE & PHÚC KHẢO NỘI BỘ
├── Thứ 3: Quota Guard K=3 & Timer (Thành) + Bốc Đề Bloom & Test (Tốt) + Thi Thử Voice-First UI (Hải) + Timer Store (Hoàng)
├── Thứ 5: Kiosk IP Check-in (Thành) + R2 Audio SHA-256 & STT (Tốt) + Kiosk Lockdown 3-Blur (Hoàng) + Appeals UI (Hải)
└── Thứ 7 [FINAL GATE]: Khóa 1 Chiều & Excel (Thành) + Evidence Panel BE & Test (Tốt) + Waveform Player (Hoàng) + Review Appeals (Hải)
```

---

## 📅 CHI TIẾT LỊCH TRÌNH REVIEW & TIÊU CHÍ NGHIỆM THU (SPRINT MVP)

### 🏃 TUẦN 1: NỀN TẢNG, LUỒNG LUYỆN TẬP MF-01 & QUẢN LÝ ĐỀ MF-03

| Mốc Review | Mã Task | Tên Chức Năng | Phụ Trách | Yêu Cầu Nghiệp Vụ Cần Đạt | Tiêu Chí Nghiệm Thu (Live Demo Criteria) |
|:---|:---|:---|:---:|:---|:---|
| **Thứ 3** *(Tuần 1)* | **DB-01** | Khởi tạo CSDL 28 bảng PostgreSQL 16 | **Hải** | Tạo 28 bảng DDL, FK/PK, Check constraints (`transcript_buffer_seconds` 10-300s, `courses.max_follow_up_questions` 1-5 do Admin cấu hình, `official_exam_sessions.max_follow_up_questions` 1-2 do Trưởng BM cấu hình, `credits > 0`), Indexes, Docker Compose. | `docker compose up -d` chạy mượt; DBeaver hiển thị đủ 28 bảng; query dữ liệu seed `FA26`, `PRN231` thành công. |
| **Thứ 3** *(Tuần 1)* | **BE-01** | Setup Clean Arch & Exception RFC 7807 | **Thành** | Khởi tạo Solution .NET 8 (4 project), MediatR CQRS, DI Container, GlobalExceptionMiddleware chuẩn RFC 7807 `ProblemDetails`, CORS `AllowFrontend`. | Chạy `dotnet run` lên Swagger port `5000`; gọi API ném lỗi cố ý trả về đúng chuẩn JSON RFC 7807 có status code. |
| **Thứ 3** *(Tuần 1)* | **BE-02** | Validation Rules & Domain Entities | **Tốt** | Định nghĩa Domain Enums, Exceptions, FluentValidation rules cơ bản cho Barem Rubric $\sum = 10.0$đ, khởi tạo test project xUnit. | Chạy `dotnet test` kiểm thử rule Rubric: tổng 9.5đ văng `DomainValidationException`; tổng 10.0đ pass 100%. |
| **Thứ 3** *(Tuần 1)* | **FE-01** | Base React 19 & Router 4 Vùng | **Hoàng** | Setup Vite React 19 + Tailwind CSS v4, Router DOM v7 chia 4 phân vùng (Student, Lecturer, DeptHead, Kiosk), Axios Interceptor bắt RFC 7807. | `npm run dev` bật port `3000`; click chuyển 4 layout không vỡ; API Backend từ chối bung Toast đỏ tiếng Việt. |
| **Thứ 5** *(Tuần 1)* | **BE-03** | Hàng Đợi BoundedChannel & SignalR | **Thành** | `BoundedChannel<T>` (1,000 slots); API `POST /api/v1/practice/answers` lưu DB `PENDING`, trả `202 Accepted` $< 100$ms; SignalR `PracticeHub`. | Dùng Postman nộp bài nhận `202 Accepted` trong $< 80$ms; Postman WebSocket kết nối `/hubs/practice` nhận event test. |
| **Thứ 5** *(Tuần 1)* | **BE-04** | Gemini 1.5 Flash CoT Grading Core | **Tốt** | Service kết nối Gemini 1.5 Flash API; Prompt Chain-of-Thought (CoT) 3 bước; logic sinh câu hỏi phụ cho MF-01 [Per-Question] tự động có follow-up đào sâu (1-5 câu do Admin cấu hình, mặc định 2 câu; hoàn toàn không xét điểm 4.0 - 8.0). | Console test / Unit test truyền transcript tiếng Việt $\rightarrow$ Gemini trả về JSON điểm số và nhận xét sư phạm hợp lệ. |
| **Thứ 5** *(Tuần 1)* | **FE-02** | Buffer Screen & Web Speech STT | **Hoàng** | Hook `useSpeechRecognition.ts` thu âm Web Speech API; `BufferScreen.tsx` đếm ngược theo cấu hình động `transcript_buffer_seconds` (mặc định 60s). | Bấm Mic nói: "Kiến trúc Clean Architecture" $\rightarrow$ Text hiển thị real-time; đồng hồ 60s đếm ngược cho phép sửa text. |
| **Thứ 5** *(Tuần 1)* | **FE-03** | Student Portal Dashboard | **Hải** | Trang chủ sinh viên: Danh sách môn học đang học, trạng thái lớp, nút bấm "Luyện tập" dẫn sang luồng MF-01. | Đăng nhập tài khoản Student $\rightarrow$ Thấy đúng danh sách môn học; bấm nút Luyện tập chuyển mượt vào màn hình làm bài. |
| **Thứ 7** *(Tuần 1)* | **BE-05** | CQRS Rubric Studio & Gen Đề FLM (MF-03) | **Tốt** | API `POST /api/v1/rubrics` (ép cứng $\sum = 10.0$đ); API Giảng viên dùng AI gen đề FLM theo barem riêng `POST /api/v1/questions/generate-from-flm`, kiểm tra/chỉnh sửa barem & Model Answer $\ge 50$ ký tự, rồi bấm "Gửi lên cho Bộ Môn" `POST /api/v1/questions/batch-submit-review`. | Gửi Rubric 9.5đ $\rightarrow$ Ném lỗi `HTTP 422`; Giảng viên chỉnh sửa barem 10.0đ gửi duyệt $\rightarrow$ Lưu thành công trạng thái `SUBMITTED_FOR_REVIEW`. |
| **Thứ 7** *(Tuần 1)* | **BE-06** | Grading Background Worker & Polly | **Thành** | `GradingQueueWorker.cs` rút task từ Channel, gọi Gemini chấm, lưu DB, bắn `ReceiveScorecard` qua SignalR; Polly Retry (2s-4s-8s) + DLQ. | Bài nộp 202 $\rightarrow$ Worker chấm ngầm $\rightarrow$ Sau 3s SignalR Client nhận được Scorecard JSON hoàn chỉnh. |
| **Thứ 7** *(Tuần 1)* | **FE-04** | Giao Diện Rubric Studio 10.0đ | **Hoàng** | `RubricCriteriaEditor.tsx`: Tính tổng real-time. Nếu $\sum \neq 10.0$đ tô đỏ ô tổng và Disable cứng nút Nộp; đủ 10.0đ nút sáng xanh. | Nhập 3 tiêu chí: 4đ + 4đ + 1.5đ = 9.5đ $\rightarrow$ Hiện viền đỏ, nút Nộp bị khóa. Sửa thành 2đ $\rightarrow$ Nút Nộp mở. |
| **Thứ 7** *(Tuần 1)* | **FE-05** | Giao Diện Duyệt Đề Bộ Môn | **Hải** | `QuestionApprovalPage.tsx` cho Trưởng BM: Bảng danh sách câu hỏi; Modal xem Barem và Model Answer; nút Approve / Reject / Needs Revision. | Trưởng BM bấm "Yêu cầu chỉnh sửa" kèm lý do $\rightarrow$ Gọi API thành công, câu hỏi chuyển trạng thái `NEEDS_REVISION`. |

---

### 🏃 TUẦN 2: THI THỬ (MF-02), KIOSK LAB (MF-04), HẬU KIỂM EVIDENCE & PHÚC KHẢO

| Mốc Review | Mã Task | Tên Chức Năng | Phụ Trách | Yêu Cầu Nghiệp Vụ Cần Đạt | Tiêu Chí Nghiệm Thu (Live Demo Criteria) |
|:---|:---|:---|:---:|:---|:---|
| **Thứ 3** *(Tuần 2)* | **BE-07** | Quota Guard MF-02 ($K \le 3$) & Master Timer | **Thành** | `QuotaService.cs` đếm lượt thi/ngày/môn từ PostgreSQL; Lượt $\ge 4 \rightarrow$ Ném `HTTP 429 Too Many Requests`; Server Master Timer chống nộp trễ $> 10$s. | Gọi API bắt đầu thi thử lần 1, 2, 3 thành công; lần 4 lập tức nhận mã `HTTP 429`. Thử nộp bài quá giờ $\rightarrow$ Server từ chối. |
| **Thứ 3** *(Tuần 2)* | **BE-08** | Bốc Đề Bloom & Unit Test MF-02 | **Tốt** | Thuật toán bốc đề ngẫu nhiên theo tỷ lệ Bloom từ kho `practice_questions` (cô lập an toàn kho `exam_questions`); Sinh viên chủ động tự chọn Có/Không Follow-up; AI Follow-up ngữ cảnh đào sâu `[Needs Follow-up]` (1-2 câu); Unit tests. | Gọi API sinh đề 5 câu $\rightarrow$ Trả đúng tỷ lệ Bloom từ kho practice; chạy test suite Quota Guard đạt 100% PASS. |
| **Thứ 3** *(Tuần 2)* | **FE-06** | Màn Hình Thi Thử Voice-First | **Hải** | `MockExamPage.tsx`: Rút đề thi thử; **Khóa cứng ô gõ text** (buộc phải nói qua Mic); Hiển thị hạn ngạch còn lại: "Còn X/3 lượt hôm nay". | Textbox bị vô hiệu hóa gõ phím; bấm Mic nói bình thường; sau 3 lượt thi thử, bấm lần 4 bung modal chặn 429 trực quan. |
| **Thứ 3** *(Tuần 2)* | **FE-07** | Lịch Sử Thi & Exam Timer Store | **Hoàng** | `ExamHistoryPage.tsx` xem lại Scorecard chi tiết theo từng CLO; `useExamTimerStore.ts` đồng bộ thời gian máy chủ, cảnh báo đỏ ở 60s cuối. | Làm bài xong chuyển về Lịch sử thi hiển thị Scorecard chi tiết; đồng hồ nhấp nháy đỏ khi sắp hết giờ. |
| **Thứ 5** *(Tuần 2)* | **BE-09** | Kỳ Thi, Kiosk IP Binding & Check-in MF-04 | **Thành** | API Trưởng BM khởi tạo kỳ thi `POST /official-exams/sessions` (cấu hình `ExamInputMode`, Follow-up 1-2 câu); API mở ca thi `POST /official-exams/shifts`; Check-in kiểm tra `RemoteIpAddress` khớp 100% với `ip_address` trên vé thi (ghế 1-40), sai trả `HTTP 403`. | Dùng IP máy khác gọi check-in $\rightarrow$ Bị chặn `403` "Sai máy trạm"; đúng IP $\rightarrow$ Chuyển vé thi sang `IN_PROGRESS`. |
| **Thứ 5** *(Tuần 2)* | **BE-10** | R2 Audio SHA-256, Persist First & Chấm Ngầm Queue | **Tốt** | Presigned URL Cloudflare R2 file `STT_MSSV.webm`; Xác thực mã băm SHA-256 niêm phong audio; Tiếp nhận bài thi lưu DB trạng thái `SUBMITTED` ($< 100$ms Persist First); Chấm ngầm qua `BoundedChannel` 1,000 slots / DLQ Replay (5m) chống quá tải AI (Zero Data Loss); Doubt Guard (`confidence < 0.70` $\rightarrow$ `is_suspicious = true`). | Nộp bài thi thật phản hồi $< 100$ms (Persist First); File audio upload lên R2 đúng tên `STT_MSSV.webm`; Worker chấm ngầm an toàn qua Queue/DLQ; Bật cờ `is_suspicious = true` đối với bài thi nhiễu mic. |
| **Thứ 5** *(Tuần 2)* | **FE-08** | Kiosk Lockdown Anti-Cheat | **Hoàng** | `useKioskLockdown.ts`: Ép Fullscreen; Chặn Alt+Tab, F11, F12, DevTools; Bắt `window.onblur` $\ge 3$ lần lập tức đình chỉ thi; Mic check 30s $\ge 60$dB; Băm SHA-256 audio client. | Click chuột ra ngoài 3 lần $\rightarrow$ Tự văng modal "ĐÌNH CHỈ THI"; Mic chưa đạt 60dB không mở nút thi; Audio upload R2 kèm mã băm SHA-256. |
| **Thứ 5** *(Tuần 2)* | **FE-09** | Form Phúc Khảo & Quản Lý Đơn | **Hải** | `AppealRequestModal.tsx` cho sinh viên nộp phúc khảo; `AppealManagementPage.tsx` cho Trưởng BM tiếp nhận đơn và xem lại bài thi. | Sinh viên bấm nộp phúc khảo tại bảng điểm $\rightarrow$ Trưởng BM vào dashboard thấy ngay đơn trạng thái `PENDING`. |
| **Thứ 7** *(Tuần 2)* | **BE-11** | Khóa Điểm 1 Chiều & Xuất Excel FPT | **Thành** | `OneWayLockInterceptor.cs` chặn sửa/xóa khi `is_locked = true` (`HTTP 403`); `PublishGradesCommand` (Atomic 100% sinh viên có điểm); `ExcelService` EPPlus xuất bảng điểm FPT. | Chưa đủ 100% điểm không cho Publish; Publish xong cố tình sửa điểm văng ngay `HTTP 403`; Xuất file Excel đúng chuẩn Khảo thí FPT. |
| **Thứ 7** *(Tuần 2)* | **BE-12** | API Hậu Kiểm Evidence Panel & Full Tests | **Tốt** | API Cổng Hậu kiểm cung cấp đủ 3 thành phần: `audioR2Url`, `transcriptWhisper`, `aiChainOfThought`; API Phúc khảo gán Trưởng BM; Chạy toàn bộ Unit & Arch Tests. | Evidence Panel trả đủ 3 thành phần dữ liệu; Trưởng BM cập nhật điểm mới thành công; Toàn bộ 270+ Unit Tests đạt 100% PASS. |
| **Thứ 7** *(Tuần 2)* | **FE-10** | Evidence Panel & Waveform Audio Player | **Hoàng** | `WaveformPlayer.tsx`: Vẽ sóng âm thanh Wavesurfer.js; Click vào từ trên transcript nhảy audio đến đúng giây đó; Highlight bài nghi ngờ `is_suspicious = true`. | Giảng viên nghe lại audio, click vào chữ tự động tua đến đúng giây; bài thi vi phạm hoặc điểm ranh giới được tô viền đỏ nổi bật. |
| **Thứ 7** *(Tuần 2)* | **FE-11** | Tổng Duyệt Thẩm Định & Phúc Khảo E2E | **Hải** | Rà soát toàn bộ trải nghiệm E2E: Sinh viên xem điểm sau Publish $\rightarrow$ Nộp đơn phúc khảo $\rightarrow$ Trưởng BM chấm lại $\rightarrow$ Cập nhật bảng điểm chính thức. | Demo chu trình phúc khảo trơn tru; bảng điểm sinh viên cập nhật điểm số mới ngay sau khi Trưởng BM phê duyệt. |

---

## 🛡️ ĐIỀU KIỆN CHỐT CHẶN BẢN QUYỀN & CHẤT LƯỢNG (HARD VERIFICATION GATES)

1. **Gate 1 (Cuối Tuần 1 - Thứ 7):**
   - Backend `dotnet build` đạt 0 Warnings, 0 Errors.
   - Luồng MF-01 chạy trọn vẹn: Sinh viên thu âm tiếng Việt $\rightarrow$ Buffer Screen 60s $\rightarrow$ Nộp bài trả 202 $\rightarrow$ Worker chạy ngầm chấm CoT $\rightarrow$ SignalR pop-up Scorecard.
   - Luồng MF-03 chạy trọn vẹn: Giảng viên tạo Barem 10.0đ $\rightarrow$ Gen đề FLM $\rightarrow$ Nộp bộ môn $\rightarrow$ Trưởng BM duyệt.
2. **Gate 2 (Cuối Tuần 2 - Thứ 7 - Tổng Duyệt Bàn Giao MVP):**
   - Full Test Suites: Toàn bộ Unit Tests xUnit và NetArchTest Clean Architecture đạt **100% PASS (Exit Code 0)**.
   - Frontend `npm run build` thành công, không có lỗi TypeScript hoặc cảnh báo nghiêm trọng.
   - Thông suốt 4 Main Flows: Luyện tập (MF-01), Thi thử Quota $K=3$ (MF-02), Soạn & Duyệt đề 10.0đ (MF-03), Kiosk Lockdown thi thật phòng Lab, Hậu kiểm Waveform, Khóa 1 chiều, Xuất Excel FPT và Phúc khảo nội bộ (MF-04).
