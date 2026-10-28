# 🖥️ FRONTEND MASTER TASK BOARD & EXECUTION SPECIFICATIONS (v8.0)
## ĐỒ ÁN TỐT NGHIỆP SEP490 (FA26SE166) — HỆ THỐNG LUYỆN THI & ĐÁNH GIÁ VẤN ĐÁP BẰNG LLM
### KHO MÃ NGUỒN: `05_Source_Code/frontend` | CỔNG DEV: `3000` | BACKEND API: `5000` | POSTGRESQL: `5432`

---

> [!IMPORTANT]
> **QUY CHUẨN TỐI THƯỢNG & NGUỒN SỰ THẬT DUY NHẤT (SINGLE SOURCE OF TRUTH):**
> - **Mã đề tài:** FA26SE166 | **Học kỳ:** Fall 2026 (FA26) — Đại học FPT TP.HCM (FPT SG)
> - **Tech Stack Chuẩn:** React 19 (`^19.2.8`), Vite (`^8.3.0`), Tailwind CSS v4, React Router DOM v7, TypeScript 5.8+, Zustand, TanStack Query v5, `@microsoft/signalr`, Web Speech API (STT/TTS), Web Audio API (Mic dB Check), Wavesurfer.js (Waveform Audio Player), Google OAuth 2.0 (Google Identity Services).
> - **File này là tài liệu duy nhất điều phối toàn bộ công việc Frontend** cho 2 kỹ sư: **Lê Vũ Hoàng** và **Phạm Nguyễn Đăng Hải**.
> - Tuyệt đối tuân thủ 12 cam kết kiến trúc tại `AGENTS.md` và `MASTER_ARCHITECTURE.md`. Không tự ý suy diễn thêm màn hình hoặc phá vỡ API contract.

---

# 📊 PHẦN 1: BẢNG MA TRẬN PHÂN BỔ CÔNG VIỆC (BALANCED 50/50 MATRIX)

| Kỹ sư đảm nhiệm | Chức năng chính (Heavy Core) | Các task vệ tinh ("Râu ria" hoàn thiện) | Thế mạnh chuyên môn |
| :--- | :--- | :--- | :--- |
| 🧑 **Lê Vũ Hoàng**<br>*(Lead Frontend Architect)* | **1. Module MF-01: Luyện Tập Tương Tác Giọng Nói**<br>• Practice Flow (Per-Question On-Demand & Full Progressive 3–10)<br>• Buffer Screen 60s đếm ngược SVG<br>• SignalR WebSocket client & Follow-up A2<br>• Scorecard Modal chi tiết Rubric<br><br>**2. Module MF-04 (Nhánh A): An Ninh Kiosk & Giám Thị**<br>• Kiosk Check-In IP Binding STT 1–40 (FE-K01)<br>• Hardware Mic Check $\ge 60$dB trong 30s (FE-K02)<br>• Kiosk Exam Lockdown, Anti-cheat blur $\ge 3$ (FE-K03)<br>• Kiosk Submitted an toàn 0% điểm liền (FE-K05)<br>• Proctor Room Monitor 40 máy lab (FE-P01)<br><br>**3. Module MF-04 (Nhánh B): Cổng Hậu Kiểm Giảng Viên**<br>• Audit Evidence Page (FE-L02)<br>• Waveform Player Wavesurfer.js tua transcript<br>• Form sửa điểm có lý do giải trình $\ge 10$ ký tự<br>• Nút "Công Bố Điểm" One-Way Lock Atomic | **1. Core Architecture & Build:**<br>• Khắc phục triệt để lỗi build Vite & TypeScript (`npm run build` Exit Code 0)<br>• Sửa alias `@/utils/cn` trong `utils.ts` và `separator.tsx`<br>• Gán type chuẩn cho các ThreeScene<br><br>**2. Auth & Session (Google OAuth 2.0):**<br>• Google Login ID Token button<br>• Silent Refresh Token Rotation<br>• Route Guards 5 roles<br><br>**3. Dashboard Sinh viên & Giảng viên (FE-10):**<br>• `StudentDashboardPage.tsx`<br>• `LecturerDashboardPage.tsx`<br><br>**4. Hộp thư Thông báo In-App (FE-11):**<br>• `NotificationDrawer.tsx`<br>• `NotificationPage.tsx` | UX/UI Âm thanh thời gian thực, Web Speech API, SignalR WebSockets, Web Audio API đo dB, Kiosk Lockdown An ninh, Waveform Player. |
| 🧑 **Phạm Nguyễn Đăng Hải**<br>*(Frontend Developer & DB Specialist)* | **1. Module MF-02: Thi Thử Tính Giờ Có Hạn Ngạch**<br>• Mock Exam Voice-First Gate (khóa text input)<br>• Master Countdown Timer đồng bộ server<br>• Quota Modal cảnh báo `HTTP 429` do Admin cấu hình<br>• Scorecard chi tiết theo Chuẩn đầu ra CLO<br><br>**2. Module MF-03: Ngân Hàng Đề & Rubric Studio 10.0**<br>• Question Studio: Soạn tay & AI sinh từ FLM CLO<br>• Rubric Criteria Editor: Tính tổng điểm $\sum \equiv 10.0$đ<br>• Checkbox tick 2 kho (`practice` / `exam`)<br>• Question Approval: Trưởng BM duyệt đề (`APPROVED`, `NEEDS_REVISION`, `REJECTED`)<br><br>**3. Module MF-04 (Nhánh Quản Trị): Kỳ Thi & Ca Thi**<br>• Exam Season Config: Tạo kỳ thi, gán môn, follow-up 1–5 câu, `ExamInputMode`<br>• Exam Shift Management: Ca thi phòng lab, kíp thi, phân công giám thị/GV, gán lớp<br><br>**4. Module MF-04 (Nhánh Phúc Khảo): Phúc Khảo Nội Bộ**<br>• Appeal Request Modal: Sinh viên nộp đơn khi xem điểm<br>• Appeal Management Page: Trưởng BM tiếp nhận và giao GV chấm lại | **1. Phân hệ Quản trị Admin (FE-08 & FE-09):**<br>• `UserManagementPage.tsx` (FE-09: Quản lý user 5 roles, khóa/mở tài khoản; không tạo user bằng mật khẩu)<br>• `SemesterManagementPage.tsx` (FE-08: CRUD học kỳ, tạo và sửa, không xóa)<br>• `AdminConfigPage.tsx` (Cấu hình `system_configs` MF-01: progressive, buffer, follow-up, inactivity timeout)<br>• `AuditLogsPage.tsx` (Xem nhật ký kiểm toán hệ thống cho Admin)<br><br>**2. Dashboard & Giám sát DLQ Trưởng Bộ Môn (FE-10):**<br>• `DepartmentHeadDashboardPage.tsx` (Quản lý kỳ thi, giám sát & replay DLQ)<br><br>**3. Lịch sử Luyện tập & Thi thử (FE-02):**<br>• `ExamHistoryPage.tsx` (Tab Thi Thử & Tab Luyện Tập)<br><br>**4. Báo Cáo Khảo Thí Phòng Lab:**<br>• Xuất file Excel (`.xlsx`) & PDF chữ ký số | State Management phức tạp, Form Studio Rubric 10.0đ, Quản trị Kỳ thi/Ca thi, Workflow Thẩm định duyệt đề, Quy trình Phúc khảo, Phân hệ Admin CRUD. |

---

# 📁 PHẦN 2: CẤU TRÚC THƯ MỤC CHUẨN MỰC (`frontend/src/`)

```text
frontend/src/
├── assets/                          # SVG, Icons, Brand assets
├── routes/                          # Điều hướng React Router DOM v7
│   ├── AppRoutes.tsx                # Khai báo tuyến đường 5 roles & Kiosk
│   ├── ProtectedRoute.tsx           # Route Guard kiểm tra Token & 5 Roles (student, lecturer, department_head, proctor, admin)
│   └── RoleBasedRedirect.tsx        # Điều hướng người dùng về đúng trang chủ sau login
├── types/                           # TypeScript Interfaces khớp 100% Backend DTOs
│   ├── auth.types.ts                # User, Role, GoogleLoginRequest, TokenPair, ProblemDetails
│   ├── practice.types.ts            # Session, NextQuestion, Answer, Scorecard, FollowUp, Progressive
│   ├── mockExam.types.ts            # MockQuota, MockSession, MockAnswer, CloScorecard
│   ├── rubric.types.ts              # Rubric, RubricCriterion (sum=10.0), QuestionDraft, ReviewDecision
│   ├── officialExam.types.ts        # Season, Shift, Ticket, Submission, ExamInputMode
│   ├── appeal.types.ts              # AppealRequest, AppealDecision, AssignLecturerRequest
│   ├── user.types.ts                # UserDto, UpdateUserRoleRequest, UserStatus (FE-09)
│   ├── semester.types.ts            # SemesterDto, CreateSemesterRequest, UpdateSemesterRequest (FE-08)
│   ├── systemConfig.types.ts        # SystemConfigDto, UpdateSystemConfigRequest
│   └── notification.types.ts        # NotificationDto, NotificationType (FE-11)
├── services/                        # Axios API Client & SignalR Services
│   ├── api.client.ts                # Axios Instance (Base URL: /api/v1, Auto Bearer, Token Refresh Interceptor, RFC 7807 Toast)
│   ├── auth.service.ts              # /api/v1/auth (google-login, refresh-token, sign-out)
│   ├── practice.service.ts          # /api/v1/practice (sessions, next-question, answers, complete, history)
│   ├── mockExam.service.ts          # /api/v1/mock-exams (sessions, answers, quota)
│   ├── question.service.ts          # /api/v1/questions (crud, ai-generate, batch-submit-review, decisions)
│   ├── officialExam.service.ts      # /api/v1/official-exams (seasons, shifts, tickets, publish-grades, evidence)
│   ├── appeal.service.ts            # /api/v1/appeals (create, get-list, assign-lecturer, review)
│   ├── user.service.ts              # /api/v1/admin/users (FE-09)
│   ├── semester.service.ts          # /api/v1/semesters (FE-08)
│   ├── systemConfig.service.ts      # /api/v1/admin/system-configs
│   ├── notification.service.ts      # /api/v1/notifications (FE-11)
│   └── signalr.service.ts           # SignalR Hub Connection (/hubs/practice)
├── stores/                          # Zustand State Stores
│   ├── useAuthStore.ts              # Token, Refresh Token, User Profile, Active Role
│   ├── usePracticeStore.ts          # Current Session, Question, Answer, Scorecard, Buffer State
│   ├── useKioskStore.ts             # Kiosk IP, SeatNumber, Focus blur count, Mic dB level, Audio stream
│   ├── useExamTimerStore.ts         # Master Timer đếm ngược đồng bộ server
│   └── useNotificationStore.ts      # Unread count, Notification list
├── hooks/                           # Custom React Hooks
│   ├── useSpeechRecognition.ts      # Web Speech API STT bóc băng tiếng Việt realtime (< 500ms) + TTS đọc đề
│   ├── useMicrophoneLevel.ts        # Web Audio API đo âm lượng mic (ngưỡng >= 60dB)
│   ├── useKioskLockdown.ts          # Fullscreen, chặn Alt+Tab, F11, F12, bắt blur >= 3 lần
│   └── usePracticeHub.ts            # SignalR Client hook lắng nghe ReceiveGradingResult, ReceiveGradingError
├── components/                      # Reusable UI Primitives
│   ├── common/                      # Button, Input, Card, Badge, Modal, Toast, Spinner, separator
│   ├── layout/                      # Navbar, Sidebar, Footer, StudentHeader, StudentSidebar, AppLayout
│   ├── practice/                    # BufferScreen (60s), ScorecardModal, FollowUpQuestionCard, ProgressiveSelector
│   ├── exam/                        # VoiceFirstGate, CountdownTimer, QuotaExceededModal
│   ├── rubric/                      # RubricCriteriaEditor (10.0đ real-time), QuestionBankSelector, ScoreOverrideForm
│   ├── audio/                       # WaveformPlayer (Wavesurfer.js highlight transcript)
│   ├── kiosk/                       # KioskLockdownGuard, KioskHardwareMeter, KioskViolationNotice
│   ├── appeals/                     # AppealRequestModal, AssignLecturerModal
│   └── notifications/               # NotificationDrawer, NotificationItem
├── pages/                           # Màn hình chính phân theo 5 Roles
│   ├── auth/                        # LoginPage.tsx, UnauthorizedPage.tsx
│   ├── student/                     # StudentDashboardPage, PracticePage, PracticeSessionPage, MockExamPage, MockExamSessionPage, OfficialExamPage, ExamHistoryPage
│   ├── lecturer/                    # LecturerDashboardPage, QuestionStudioPage, CreateQuestionPage, AuditEvidencePage
│   ├── department_head/             # DepartmentHeadDashboardPage, QuestionApprovalPage, ExamSeasonConfigPage, ExamShiftManagementPage, AppealManagementPage, DepartmentHeadDlqPage
│   ├── proctor/                     # ProctorRoomMonitorPage
│   ├── admin/                       # UserManagementPage, SemesterManagementPage, AdminConfigPage, AuditLogsPage
│   └── kiosk/                       # KioskCheckInPage, KioskHardwareMicCheckPage, KioskExamRoomPage, KioskSuspendedPage, KioskSubmittedPage
└── utils/
    └── cn.ts                        # Utility clsx + twMerge tối ưu Tailwind CSS
```

---

# 🧑‍💻 PHẦN 3: NHIỆM VỤ CHI TIẾT CỦA LÊ VŨ HOÀNG (LEAD FRONTEND ARCHITECT)

### 🌟 KHỐI TRỌNG TÂM 1: MODULE MF-01 — PHÒNG LUYỆN TẬP TƯƠNG TÁC GIỌNG NÓI
1. **Trang `PracticePage.tsx` & `PracticeSessionPage.tsx` (`/student/practice`):**
   - **Modal Cài Đặt Đầu Phiên (Pre-Session Setup):**
     * Chọn môn học.
     * Chọn chế độ làm bài: `[Per-Question On-Demand]` (luyện từng câu, lấy câu tiếp theo on-demand) hoặc `[Full-Session Progressive]` (luyện trọn gói 3–10 câu từ Dễ $\to$ Khó).
     * Với Per-Question: Cho phép chọn đơn lẻ hoặc tổ hợp độ khó (`easy`, `medium`, `hard`). Không bắt buộc chốt số câu.
     * Với Full-Session: Slider nhập số lượng câu hỏi từ **3 đến 10 câu** (ràng buộc bởi `MinMixedPracticeQuestions = 3` và `MaxMixedPracticeQuestions = 10` từ `system_configs`).
     * Gọi API `POST /api/v1/practice/sessions` để khởi tạo phiên.
   - **Giao diện làm bài:**
     * Hiển thị câu hỏi hiện tại, mức Bloom, và tiêu chí Barem Rubric $\sum \equiv 10.0$đ để sinh viên tham chiếu.
     * Nút Mic lớn ở trung tâm. Nhấn để thu âm $\to$ Web Speech API bóc băng tiếng Việt hiển thị preview văn bản tức thì.
     * Nhấn dừng nói $\to$ Tự động chuyển tiếp vào màn hình đệm `BufferScreen.tsx`.
   - **Inactivity Timeout 5 phút:** Nếu sinh viên không tương tác quá 5 phút, hệ thống tự động khóa phiên, bắt lỗi **HTTP 410 Gone**, hiển thị thông báo phiên đã kết thúc và bảo toàn điểm các câu đã làm.
   - **Nút "Lấy Câu Tiếp Theo" (Per-Question):** Gọi API `POST /api/v1/practice/sessions/{id}/next-question`. Áp dụng thuật toán Anti-3-Consecutive. Nếu hết câu, hiển thị hoàn tất phiên.
   - **Nút "Kết Thúc Phiên":** Gọi API `POST /api/v1/practice/sessions/{id}/complete` để hoàn tất và lưu bảng điểm.
2. **Component `BufferScreen.tsx` (Vùng Đệm Hiệu Đính Transcript 60s):**
   - **Đồng hồ đếm ngược SVG tròn (Circular Countdown):** Đếm ngược theo giá trị `transcript_buffer_seconds` (Admin cấu hình 10–300s, **mặc định 60 giây**).
   - Ô soạn thảo text cho phép sinh viên nghe lại TTS câu hỏi và sửa các từ ngữ chuyên ngành SE bị phiên âm sai (Code-Switching: Dependency Injection, Singleton, CQRS, Docker...).
   - Nút **"Nộp Ngay"**: Lưu bài `practice_answers` (Persist First) và chuyển sang trạng thái chờ AI chấm.
   - Hết giờ (0s) $\to$ Tự động nộp bài (Auto-submit).
   - **Lưu ý:** Không stream audio lên R2, không lưu `AudioUrl`.
3. **Component `FollowUpQuestionCard.tsx` (Câu Hỏi Phụ Đào Sâu A2):**
   - Kích hoạt khi sinh viên làm chế độ Per-Question và điểm rơi vào khoảng ranh giới **$4.0 \le \text{Score} \le 8.0$**.
   - Thẻ màu hổ phách Amber nổi bật. Hiển thị câu hỏi phụ do AI Gemini sinh ra để kiểm tra mức độ hiểu sâu.
   - Cho phép sinh viên bật Mic trả lời và cũng có màn hình đệm 60s trước khi nộp.
   - Số câu hỏi phụ tối đa: Đọc từ cấu hình `MaxPracticeFollowUpQuestions` (1–5 câu, mặc định 2 câu).
4. **Component `ScorecardModal.tsx` (Bảng Điểm Rubric Chi Tiết):**
   - Hiển thị điểm số tổng kết thang điểm 10.0 (Xanh lá $\ge 8.0$, Vàng $5.0–7.9$, Đỏ $< 5.0$).
   - Accordion chi tiết từng tiêu chí Rubric: Điểm đạt / Điểm tối đa, phân tích điểm mạnh, điểm thiếu sót.
   - Nhận xét sư phạm của AI kèm nút loa TTS đọc to nhận xét.
5. **Hook `usePracticeHub.ts` (SignalR WebSocket Client):**
   - Kết nối endpoint `/hubs/practice`. Tự động `JoinSession(sessionId)`.
   - Lắng nghe sự kiện `ReceiveGradingResult` để cập nhật điểm và `ReceiveGradingError` để bắt lỗi khi AI quá tải.

---

### 🌟 KHỐI TRỌNG TÂM 2: MODULE MF-04 (NHÁNH A) — AN NINH KIOSK PHÒNG THI & GIÁM THỊ
1. **Trang `KioskCheckInPage.tsx` (`/kiosk/check-in` — FE-K01):**
   - Giao diện fullscreen Kiosk phòng Lab. Tự động phát hiện IP máy trạm (`ip_address`).
   - Nhập Mã sinh viên (MSSV) $\to$ Hệ thống đối chiếu số máy Kiosk (SeatNumber 1–40) trùng khớp với Số thứ tự trong danh sách ca thi phòng Lab.
   - Khóa nút "Tiếp Tục" nếu sinh viên ngồi sai vị trí máy.
2. **Trang `KioskHardwareMicCheckPage.tsx` (`/kiosk/hardware-check` — FE-K02):**
   - Tích hợp Web Audio API và `useMicrophoneLevel.ts`.
   - Sinh viên nói thử vào Micro trong 30 giây. Hiển thị thước đo âm lượng thời gian thực (VU Meter).
   - Bắt buộc âm lượng đạt ngưỡng **$\ge 60$dB** mới mở khóa nút "Vào Phòng Thi". Nếu micro rè/yếu dưới 60dB, hiển thị cảnh báo đỏ yêu cầu đổi tai nghe hoặc báo giám thị.
3. **Trang `KioskExamRoomPage.tsx` (`/kiosk/exam` — FE-K03):**
   - **Chế độ Phong Tỏa Kiosk (Lockdown Mode):** Ép buộc Fullscreen. Vô hiệu hóa 100% phím tắt hệ thống: Alt+Tab, phím Windows, F11, F12, Ctrl+Shift+I, chuột phải.
   - **Cơ Chế Bắt Mất Focus (Anti-Cheat 3 Strikes):** Hook `useKioskLockdown.ts` lắng nghe `window.onblur`.
     * Lần 1: Cảnh báo vàng toàn màn hình.
     * Lần 2: Cảnh báo cam nghiêm trọng.
     * Lần 3: Lập tức khóa bài thi, tự động chuyển sang trang `KioskSuspendedPage.tsx` thông báo bị đình chỉ thi và gửi còi báo động về máy giám thị.
   - **Phương thức làm bài (`ExamInputMode`):** Hiển thị theo cấu hình của Trưởng BM: `VoiceOnly` hoặc `VoiceWithTranscriptEdit`.
   - **Niêm phong âm thanh Cloudflare R2:** Stream audio trực tiếp lên Cloudflare R2 với tên file bất biến **`STT_MSSV.webm`** (ví dụ `01_SE170123.webm`), tính mã băm cryptographic SHA-256 ngay tại Kiosk để niêm phong bài thi.
4. **Trang `KioskSubmittedPage.tsx` (`/kiosk/submitted` — FE-K05):**
   - Màn hình biên nhận niêm phong an toàn sau khi sinh viên bấm nộp bài.
   - Nộp bài Persist First trong $< 100$ms (`SUBMITTED`) kèm mã băm SHA-256 của file audio R2.
   - Thông báo rõ ràng: *"Bài thi đã được lưu trữ an toàn. Kết quả sẽ do Giảng viên thẩm định và công bố trên hệ thống."*
   - **Quy tắc bất biến:** **Tuyệt đối không có điểm liền tại Kiosk và không cho khiếu nại tại chỗ**. Sinh viên ký biên bản nộp bài giấy với giám thị rồi ra về.
5. **Trang `ProctorRoomMonitorPage.tsx` (`/proctor/monitor` — FE-P01):**
   - Màn hình dành cho Giám thị phòng thi (hoặc Giảng viên được phân công coi thi).
   - Hiển thị **Ma trận 40 máy lab** (Lưới 40 ô trực quan tương ứng SeatNumber 1–40).
   - Chức năng điểm danh: Đối chiếu thẻ SV và CCCD thực tế, tick xác nhận sinh viên có mặt trên hệ thống.
   - Các nút điều khiển ca thi: "Mở Ca Thi", "Bắt Đầu Tính Giờ", "Khóa Ca Thi".
   - Bảng cảnh báo an ninh thời gian thực: Nếu có máy Kiosk nào vi phạm mất focus (blur) hoặc bị đình chỉ, ô máy đó lập tức nhấp nháy đỏ kèm âm thanh cảnh báo.

---

### 🌟 KHỐI TRỌNG TÂM 3: MODULE MF-04 (NHÁNH B) — CỔNG HẬU KIỂM GIẢNG VIÊN (AUDIT EVIDENCE)
1. **Trang `AuditEvidencePage.tsx` (`/lecturer/audit-evidence` — FE-L02):**
   - Cổng Giảng viên hậu kiểm toàn diện bài thi vấn đáp phòng Lab sau khi AI đã chấm ngầm.
   - **Tự động phân loại 2 nhóm bài thi:**
     * **Nhóm 1 (Cần xem lại / Fail / Đáng nghi ngờ):** Các bài có `is_suspicious == true`, `confidence_score < 0.70` (do phòng ồn, phát âm không rõ, mâu thuẫn chuỗi CoT) hoặc bài thi bị fail/điểm liệt. Giảng viên **bắt buộc phải rà soát và nghe lại 100% các bài trong nhóm này**.
     * **Nhóm 2 (Độ tin cậy cao):** Danh sách các bài thi AI chấm đạt độ tin cậy cao để GV rà soát nhanh.
   - **Evidence Panel đa chiều:**
     * Bên trái: Thông tin thí sinh, đề bài, Barem Rubric 10.0đ.
     * Ở giữa: Bản transcript Whisper bóc băng gốc song song với bản transcript sinh viên đã hiệu đính.
     * Bên phải: Phân tích chuỗi suy luận CoT (Chain-of-Thought) của AI cho từng tiêu chí rubric.
2. **Component `WaveformPlayer.tsx` (Trình Phát Sóng Âm R2):**
   - Tích hợp Wavesurfer.js hiển thị biểu đồ sóng âm chân thực từ file ghi âm `STT_MSSV.webm` trên Cloudflare R2.
   - Điều khiển tốc độ phát: 1.0x, 1.25x, 1.5x.
   - Tính năng Interactive Transcript: Click vào bất kỳ câu nào trong transcript, thanh timeline sóng âm sẽ tự động nhảy đến đúng đoạn audio đó để GV nghe kiểm chứng.
3. **Component `ScoreOverrideForm.tsx` (Form Điều Chỉnh Điểm):**
   - Giảng viên điều chỉnh lại điểm từng tiêu chí rubric.
   - Bắt buộc nhập lý do giải trình (`override_reason`) **tối thiểu 10 ký tự** (nếu để trống hoặc ngắn hơn, nút Lưu sẽ bị vô hiệu hóa).
4. **Nút "Công Bố Điểm" (Publish Grades):**
   - Chỉ mở khóa khi **100% sinh viên trong ca thi đã có điểm hoàn chỉnh** (đã xử lý xong toàn bộ bài nghi ngờ/fail).
   - Gọi API `POST /api/v1/official-exams/shifts/{shiftId}/publish-grades`.
   - Kích hoạt One-Way Lock (`is_locked = true`), chặn mọi thao tác sửa điểm trái phép và gửi điểm về Student Portal.

---

### 🛠️ CÁC TASK VỆ TINH ("RÂU RIA") CỦA HOÀNG
1. **Khắc phục lỗi Build & TypeScript:**
   - Sửa file `src/lib/utils.ts` và `src/components/common/separator.tsx`: Chuyển `from 'cn'` thành `from '@/utils/cn'`.
   - Cài đặt đầy đủ các package thiếu trong `package.json`: `three`, `@base-ui/react`, `qrcode`.
   - Gán kiểu tường minh (type casting) cho các tham số `object: any`, `material: any`, `dataUrl: string` trong các ThreeScene để lệnh `npm run build` vượt qua 100% (Exit Code 0).
2. **Xác thực Google OAuth 2.0 (FE-Auth):**
   - Hoàn thiện `LoginPage.tsx`: Tích hợp Google Identity Services (Google Sign-In button), lấy Google ID Token gửi `POST /api/v1/auth/google-login`.
   - Xử lý lưu trữ TokenPair (`accessToken`, `refreshToken`) vào `useAuthStore.ts`.
   - Triển khai Silent Refresh Token Interceptor trong `api.client.ts`: Tự động gọi `POST /api/v1/auth/refresh-token` trước khi access token hết hạn (Token Rotation).
   - Hoàn thiện Route Guard `ProtectedRoute.tsx` phân quyền đúng 5 roles (`student`, `lecturer`, `department_head`, `proctor`, `admin`).
3. **Dashboard Sinh viên & Giảng viên (FE-10):**
   - `StudentDashboardPage.tsx`: Lời chào, thông tin SV, danh sách môn học, widget hạn ngạch thi thử hôm nay (`X/K`), banner lịch thi phòng lab sắp tới.
   - `LecturerDashboardPage.tsx`: Thống kê ca thi sắp coi, số bài thi cần hậu kiểm, số câu hỏi đang soạn thảo.
4. **Hộp Thư Thông Báo In-App (FE-11):**
   - Xây dựng `NotificationDrawer.tsx` và `NotificationPage.tsx`.
   - Hiển thị danh sách thông báo: Lịch thi được công bố, điểm thi chính thức đã công bố, đơn phúc khảo được giao chấm, câu hỏi chờ duyệt/bị trả về. Nút "Đánh dấu đã đọc". (Không gửi email).

---

# 🧑‍💻 PHẦN 4: NHIỆM VỤ CHI TIẾT CỦA PHẠM NGUYỄN ĐĂNG HẢI (DB & FE SPECIALIST)

### 🌟 KHỐI TRỌNG TÂM 1: MODULE MF-02 — THI THỬ VẤN ĐÁP TÍNH GIỜ CÓ HẠN NGẠCH
1. **Trang `MockExamPage.tsx` & `MockExamSessionPage.tsx` (`/student/mock-exam`):**
   - **Modal Khởi Động Thi Thử:**
     * Sinh viên chọn môn học cần thi thử.
     * Tùy chọn Follow-up chủ động: Sinh viên tick chọn **"Có Follow-up"** hoặc **"Không Follow-up"**. Ca thi có follow-up sẽ có thời lượng làm bài dài hơn do Trưởng BM cấu hình.
     * Sinh viên **không chọn topic, không chọn độ khó**. Đề thi bốc ngẫu nhiên từ kho `practice_questions` theo ma trận Bloom do Trưởng BM cấu hình.
     * Gọi API `POST /api/v1/mock-exams/sessions` để bắt đầu.
   - **Xử lý Chặn Hạn Ngạch HTTP 429 (`QuotaExceededModal.tsx`):**
     * Nếu sinh viên đã dùng hết hạn ngạch thi thử trong ngày (`max_mock_exams_per_day` do Admin cấu hình cho môn học đó), API trả về `HTTP 429 Too Many Requests`.
     * Bung Modal cảnh báo màu đỏ: *"Bạn đã dùng hết hạn ngạch K lượt thi thử hôm nay. Vui lòng quay lại vào ngày mai hoặc chuyển sang Luyện tập tự do (MF-01)"*. Kèm nút chuyển nhanh sang Luyện tập.
2. **Component `VoiceFirstGate.tsx` (Cổng Kiểm Soát Voice-First):**
   - Khóa cứng toàn bộ ô nhập liệu văn bản (`disabled` và `readonly`), ẩn ô gõ phím.
   - Bắt buộc sinh viên phải sử dụng Micro để trả lời câu hỏi. Hiển thị sóng âm thời gian thực khi đang nói.
3. **Component `CountdownTimer.tsx` (Đồng Hồ Đếm Ngược Master Timer):**
   - Hiển thị đồng hồ đếm ngược `MM:SS` to ở vị trí cố định trên cùng, đồng bộ chính xác với thời lượng server.
   - Cảnh báo trực quan: Dưới 3 phút đổi sang màu cam nhấp nháy; dưới 1 phút chuyển màu đỏ rực.
   - **Cơ chế Auto-Submit:** Khi hết giờ (00:00), hệ thống tự động khóa đề và nộp bài. **Các câu chưa làm tính là bỏ trống, 0 điểm**.
   - **Lưu ý:** Thi thử **không lưu audio**, chỉ lưu bản transcript vào `mock_exam_answers`.
4. **Modal Kết Quả `MockExamScorecardModal.tsx` (Instant Feedback Theo CLO):**
   - Bung lên tức thì sau khi hoàn thành bài thi thử.
   - Hiển thị bảng điểm tổng kết thang điểm 10.0 và đánh giá chi tiết theo từng Chuẩn Đầu Ra (CLOs) của môn học: CLO1 (Kiến trúc), CLO2 (Thiết kế), CLO3 (Tối ưu)...
   - Nhận xét sư phạm từng tiêu chí giúp sinh viên nhận diện lỗ hổng kiến thức trước kỳ thi thật.

---

### 🌟 KHỐI TRỌNG TÂM 2: MODULE MF-03 — NGÂN HÀNG CÂU HỎI & BAREM RUBRIC STUDIO 10.0
1. **Trang `QuestionStudioPage.tsx` (`/lecturer/questions`):**
   - Danh sách câu hỏi môn học: Bộ lọc theo Môn học, Mức Bloom, Kho lưu trữ (`practice_questions`, `exam_questions`), Trạng thái (`DRAFT`, `SUBMITTED_FOR_REVIEW`, `APPROVED`, `NEEDS_REVISION`, `REJECTED`).
   - Nút **"Gửi Duyệt Bộ Môn"** (`POST /api/v1/questions/batch-submit-review`): Chọn các câu hỏi `DRAFT` và gửi lên Trưởng Bộ Môn thẩm định.
2. **Trang `CreateQuestionPage.tsx` & Component `RubricCriteriaEditor.tsx` (Hỗ trợ soạn tay đính kèm ảnh/sơ đồ kỹ thuật `PNG/JPG/WEBP <= 5MB` qua `POST /api/v1/questions/upload-image` kèm `imageDescription` chấm Multimodal AI):**
   - **Tính năng AI Sinh Câu Hỏi Từ FLM Syllabus:**
     * Nút **"AI Sinh Câu Hỏi Từ Syllabus"**: Gọi API sinh câu hỏi, rubric và câu trả lời mẫu bám sát các CLO có sẵn trên syllabus FLM của môn học.
     * Giảng viên không bắt buộc chọn từng CLO1, CLO2; có thể chọn Topic để thu hẹp phạm vi trước khi sinh.
     * Hệ thống chỉ đọc syllabus từ FLM, không ghi ngược lên FLM.
     * Giảng viên cũng có toàn quyền soạn tay 100% nội dung câu hỏi, rubric và sample answer.
   - **Rubric Studio 10.0 Chuẩn Hóa:**
     * Form thêm/bớt các tiêu chí rubric con.
     * **Client Guard Tổng Điểm Real-Time:** Tính tổng điểm $\sum$ các tiêu chí con. Nếu $\sum \neq 10.0$đ, hiển thị cảnh báo đỏ và khóa cứng nút Lưu; nếu $\sum \equiv 10.0$đ, hiển thị nhãn xanh hợp lệ.
     * Ràng buộc Model Answer bắt buộc $\ge 50$ ký tự.
   - **Tick Chọn Kho Câu Hỏi (`QuestionBankSelector.tsx`):**
     * Hai checkbox riêng biệt: `[ ] Kho Luyện Tập (practice_questions)` và `[ ] Kho Thi Thật (exam_questions)`. Cho phép tick một kho hoặc cả hai kho.
3. **Trang `QuestionApprovalPage.tsx` (`/department-head/question-approval`):**
   - Màn hình dành riêng cho Trưởng Bộ Môn để thẩm định đề thi do Giảng viên gửi lên.
   - Hiển thị chi tiết câu hỏi, CLO tương ứng, Barem Rubric 10.0đ và Câu trả lời mẫu.
   - **3 Quyết định chính thức:**
     * Bấm **"Phê duyệt"** (`APPROVED`): Lưu chính thức vào ngân hàng đề môn học.
     * Bấm **"Yêu cầu chỉnh sửa"** (`NEEDS_REVISION`): Mở popup bắt buộc nhập lý do góp ý để trả về cho Giảng viên sửa.
     * Bấm **"Từ chối"** (`REJECTED`): Nhập lý do từ chối và loại bỏ câu hỏi.

---

### 🌟 KHỐI TRỌNG TÂM 3: MODULE MF-04 (NHÁNH QUẢN TRỊ) — KỲ THI, CA THI & PHÚC KHẢO NỘI BỘ
1. **Trang `ExamSeasonConfigPage.tsx` (`/department-head/exam-seasons`):**
   - Trưởng Bộ Môn khởi tạo Kỳ thi chính thức (`OfficialExamSession` / Exam Season).
   - Đưa danh sách các Môn thi vào kỳ thi.
   - **Cấu hình Follow-up môn thi trong kỳ thi:** Bật/tắt `has_follow_up` và số câu hỏi phụ tối đa (`max_follow_up_questions` từ 1–5 câu, mặc định 2 câu).
   - **Cấu hình Phương thức làm bài (`ExamInputMode`):** Chọn 1 trong 2 chế độ trong scope: `VoiceOnly` hoặc `VoiceWithTranscriptEdit` (loại bỏ hoàn toàn `VoiceAndTextInput`).
   - Cấu hình thời gian đệm sửa transcript của kỳ thi: `TranscriptBufferSeconds` từ 10–300s (mặc định 60s).
2. **Trang `ExamShiftManagementPage.tsx` (`/department-head/shifts`):**
   - Quản lý danh sách các Ca thi (`RealExamSessionShift`).
   - **Ghi phòng lab trực tiếp trên ca thi** (ví dụ: Lab 302, Lab 405; không dùng danh mục phòng riêng).
   - Cấu hình kíp thi, thời gian bắt đầu / kết thúc ca thi.
   - **Phân công người coi thi:** Chọn Giảng viên hoặc Giám thị coi thi cho ca đó.
   - **Gán danh sách thí sinh:** Chọn lớp học đã có trong hệ thống để tự động nạp danh sách sinh viên vào ca thi (Excel chỉ là giải pháp dự phòng khi lớp chưa có trên hệ thống).
3. **Component `AppealRequestModal.tsx` (Sinh Viên Nộp Đơn Phúc Khảo):**
   - Hiển thị trên Student Portal khi sinh viên xem bảng điểm chính thức do GV công bố.
   - Sinh viên không đồng ý với điểm có thể nhấn nút **"Nộp Đơn Phúc Khảo"** (`POST /api/v1/appeals`).
   - Form nhập lý do khiếu nại, chọn câu hỏi cần phúc khảo và cam kết quy chế.
4. **Trang `AppealManagementPage.tsx` (`/department-head/appeals`):**
   - Trưởng Bộ Môn tiếp nhận danh sách các đơn phúc khảo nội bộ (`AppealRequest`).
   - Xem Evidence Panel của bài thi (Audio Cloudflare R2, Transcript Whisper, Điểm cũ).
   - **Giao Giảng viên chấm lại:** Nút mở modal `AssignLecturerModal.tsx` chọn một Giảng viên để giao nhiệm vụ chấm lại (`PUT /api/v1/appeals/{id}/assign-lecturer`).
   - Giảng viên được phân công sẽ nhận được thông báo in-app và One-Way Lock mở đường hợp lệ để cập nhật điểm số mới sau phúc khảo.

---

### 🛠️ CÁC TASK VỆ TINH ("RÂU RIA") CỦA HẢI
1. **Phân hệ Quản trị Admin (FE-08 & FE-09):**
   - `UserManagementPage.tsx` (FE-09): Xem danh sách người dùng, xem chi tiết, khóa hoặc mở tài khoản, gán vai trò trong 5 roles (`student`, `lecturer`, `department_head`, `proctor`, `admin`) cho các tài khoản Google OAuth hoặc tài khoản `username`/`password` do Nhà trường cấp (không mở form tự đăng ký công khai).
   - `SemesterManagementPage.tsx` (FE-08): Quản lý học kỳ: Xem danh sách, tạo mới học kỳ, sửa học kỳ (mã, tên, ngày bắt đầu/kết thúc, trạng thái active). **Quy tắc: Không cho phép xóa học kỳ**.
   - `AdminConfigPage.tsx`: Màn hình Admin cấu hình hệ thống bảng `system_configs` và hạn ngạch thi thử theo môn (`max_mock_exams_per_day`):
     * Số câu hỏi phụ follow-up MF-01: 1–5 câu (mặc định 2).
     * Ràng buộc số câu tiến trình progressive MF-01: Min = 3, Max = 10.
     * Thời gian đệm sửa transcript MF-01: 10–300s (mặc định 60s).
     * Thời gian timeout không tương tác: `SessionInactivityTimeoutMinutes = 5` (phút).
   - `AuditLogsPage.tsx`: Xem nhật ký kiểm toán hệ thống (`GET /api/v1/admin/audit-logs`) dành cho Admin. Màn hình theo dõi và kích hoạt Replay Dead-Letter Queue (`GET /api/v1/dlq/tasks`, `POST /api/v1/dlq/tasks/{id}/replay`) được phân quyền cho Trưởng Bộ Môn (`department_head`).
2. **Dashboard Trưởng Bộ Môn (FE-10):**
   - `DepartmentHeadDashboardPage.tsx`: Thống kê tổng số môn học phụ trách, số kỳ thi đang mở, số đề thi đang chờ duyệt, số đơn phúc khảo cần phân công chấm lại, và giám sát Dead-Letter Queue (DLQ) để phục hồi bài thi.
3. **Lịch sử Thi Thử & Luyện Tập (FE-02):**
   - Hoàn thiện `ExamHistoryPage.tsx` với 2 Tab mượt mà:
     * Tab 1: Lịch sử Luyện Tập (MF-01 qua API `GET /api/v1/practice/student/history`).
     * Tab 2: Lịch sử Thi Thử (MF-02).
   - Drawer xem chi tiết bài làm, transcript và điểm từng tiêu chí rubric.
4. **Module Xuất Báo Cáo Khảo Thí:**
   - Tại màn hình quản lý ca thi, tích hợp 2 nút hành động:
     * Nút xanh: **"Xuất Báo Cáo Excel (.xlsx)"** $\to$ Tải về bảng điểm chuẩn mẫu Khảo thí Đại học FPT.
     * Nút đỏ: **"Xuất Báo Cáo PDF Ký Số"** $\to$ Tải về file PDF tổng kết ca thi có đóng dấu điện tử/chữ ký số.

---

# 🚀 PHẦN 5: CHECKLIST NGHIỆM THU TIÊU CHUẨN (ACCEPTANCE CRITERIA)

| Mã tiêu chí | Nội dung nghiệm thu kỹ thuật | Kỹ sư chịu trách nhiệm | Tiêu chuẩn đạt |
| :---: | :--- | :---: | :---: |
| **AC-01** | `npm run build` chạy thành công không có lỗi TypeScript | 🧑 Lê Vũ Hoàng | Exit Code = 0, 0 TS Error |
| **AC-02** | Đăng nhập Google OAuth 2.0 mở cho mọi email, silent refresh token rotation | 🧑 Lê Vũ Hoàng | Token lưu đúng Store, tự refresh sau 55p |
| **AC-03** | MF-01 chạy mượt cả Per-Question On-Demand và Full Progressive 3–10 câu | 🧑 Lê Vũ Hoàng | SignalR nhận điểm, Anti-3-consecutive chuẩn |
| **AC-04** | Buffer Screen SVG đếm ngược đúng 60s, không lưu audio lên R2 | 🧑 Lê Vũ Hoàng | Auto-submit khi 0s, transcript lưu DB |
| **AC-05** | Kiosk Lockdown chặn phím tắt, vi phạm blur $\ge 3$ chuyển Suspended | 🧑 Lê Vũ Hoàng | 100% fullscreen, mic check $\ge 60$dB |
| **AC-06** | Hậu kiểm Giảng viên có Wavesurfer.js tua transcript, sửa điểm có lý do $\ge 10$ ký tự | 🧑 Lê Vũ Hoàng | Khóa 1 chiều `is_locked=true` khi công bố |
| **AC-07** | MF-02 Voice-First Gate khóa text, Timer đếm ngược to, bắt lỗi HTTP 429 Quota | 🧑 Phạm Nguyễn Đăng Hải | Hiện modal cảnh báo đỏ khi quá hạn ngạch |
| **AC-08** | Rubric Studio kiểm soát tổng điểm $\sum \equiv 10.0$đ, AI sinh từ FLM CLO | 🧑 Phạm Nguyễn Đăng Hải | Lệch 10.0đ khóa nút gửi, Model Answer $\ge 50$ ký tự |
| **AC-09** | Trưởng BM duyệt đề (3 quyết định) và cấu hình Ca thi phòng lab, follow-up, kíp thi | 🧑 Phạm Nguyễn Đăng Hải | Đề vào kho khi APPROVED, ca thi bind lớp |
| **AC-10** | Sinh viên nộp phúc khảo nội bộ, Trưởng BM giao GV chấm lại | 🧑 Phạm Nguyễn Đăng Hải | One-Way Lock mở đường cho GV được giao |
| **AC-11** | Admin quản lý User 5 roles (không tạo user bằng mật khẩu), CRUD học kỳ (không xóa) | 🧑 Phạm Nguyễn Đăng Hải | UI thao tác mượt, gọi đúng API v1 |
| **AC-12** | Xuất báo cáo Khảo thí ra cả file Excel (.xlsx) và file PDF có chữ ký số | 🧑 Phạm Nguyễn Đăng Hải | Tải đúng định dạng file từ backend |
