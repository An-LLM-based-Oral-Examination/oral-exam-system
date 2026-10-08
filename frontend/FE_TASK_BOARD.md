# 🖥️ FRONTEND FUNCTIONAL TASK BOARD & SCREEN SPECIFICATIONS (v7.0 — FUNCTIONAL BREAKDOWN THEO 4 KHỐI CHỨC NĂNG ĐỘC LẬP)
## ĐỒ ÁN TỐT NGHIỆP SEP490 (FA26SE166) — HỆ THỐNG LUYỆN THI & ĐÁNH GIÁ VẤN ĐÁP BẰNG LLM
### KHO MÃ NGUỒN: `05_Source_Code/frontend` | CỔNG DỊCH VỤ DEV: `3000` | BACKEND API: `5000`

---

> [!IMPORTANT]
> **THÔNG TIN DỰ ÁN & PHÂN CÔNG NHÂN SỰ FRONTEND CHUẨN XÁC 100%:**
> - **Mã đề tài:** FA26SE166 | **Học kỳ:** Fall 2026 (FA26) — Đại học FPT TP.HCM (FPT SG)
> - **Tech Stack Frontend:** React 19 (`^19.2.8`), Vite (`^8.3.0`), Tailwind CSS v4, React Router DOM v7, TypeScript 5.8+, Zustand, TanStack React Query v5, `@microsoft/signalr`, Web Speech API (STT/TTS), Web Audio API (Mic dB), Wavesurfer.js (Waveform Audio Player).
> - **Phân công nhân sự Frontend theo chỉ đạo chính thức:**
>   - 🧑 **Lê Vũ Hoàng (Lead Frontend Architect):**
>     * Kiến trúc nền tảng React 19 SPA, Vite, Tailwind CSS v4, React Router DOM v7 (Route Guards 5 roles: `student`, `lecturer`, `department_head`, `proctor`, `admin`), Axios Client với Interceptor xử lý lỗi RFC 7807 `ProblemDetails`.
>     * Khối 1 (MF-01 UI Core): `PracticePage.tsx` (Per & Full, Progressive 3-10 câu), `BufferScreen.tsx` (màn hình đệm 60s SVG countdown), `FollowUpQuestionCard.tsx`, `ScorecardModal.tsx`, custom hooks `usePracticeHub.ts` (SignalR WebSocket client) và `useSpeechRecognition.ts` (Web Speech API tiếng Việt realtime $< 500$ms).
>     * Khối 4 (MF-04 UI Kiosk & Hậu kiểm): `ProctorRoomMonitorPage.tsx` (FE-P01 ma trận 40 máy lab), `KioskCheckInPage.tsx` (FE-K01 IP Binding), `KioskHardwareMicCheckPage.tsx` (FE-K02 đo dB mic $\ge 60$dB trong 30s), `KioskExamRoomPage.tsx` (FE-K03 Fullscreen lockdown, chặn phím tắt hệ thống, phát hiện mất focus blur $\ge 3$ lần chuyển `KioskSuspendedPage.tsx`, stream audio Cloudflare R2 `STT_MSSV.webm` kèm SHA-256 niêm phong), `KioskSubmittedPage.tsx` (FE-K05 biên nhận niêm phong an toàn: 0% điểm liền, 0% khiếu nại tại chỗ), `AuditEvidencePage.tsx` (FE-L02 Cổng Hậu kiểm Giảng viên: Wavesurfer.js sóng âm R2, tua audio theo transcript, sửa điểm kèm lý do $\ge 10$ ký tự, nút "Công Bố Điểm" Atomic 100%).
>     * UI Vệ tinh: `LoginPage.tsx` (Google OAuth PKCE mở cho mọi email Google), Hộp thư thông báo `NotificationDrawer.tsx` / `NotificationPage.tsx` (FE-11), Dashboard Giảng viên & Sinh viên (FE-10).
>   - 🧑 **Nguyễn Đăng Hải (Frontend Developer & DB Specialist):**
>     * Database: Đã hoàn thành 100% 28 bảng CSDL PostgreSQL 16 trong 6 Bounded Contexts. **Hải tuyệt đối KHÔNG code logic C# Backend** mà chuyển sang **dồn toàn lực phát triển Frontend**:
>     * Khối 2 (MF-02 UI Core): `MockExamPage.tsx` Voice-First (khóa cứng input text, Master Timer đếm ngược to đồng bộ server), `QuotaExceededModal.tsx` (modal cảnh báo đỏ khi gặp lỗi HTTP 429 quá hạn ngạch Trưởng BM đặt), `MockExamScorecardModal.tsx` (Scorecard chi tiết theo chuẩn đầu ra CLO), `ExamHistoryPage.tsx` (FE-02 Tab Thi Thử).
>     * Khối 3 (MF-03 UI Core): `QuestionStudioPage.tsx` (soạn câu hỏi thủ công, AI gen từ FLM Syllabus theo barem riêng, Model Answer $\ge 50$ ký tự), `RubricCriteriaEditor.tsx` (Studio tính tổng điểm real-time 10.0đ: lệch 10.0đ tô đỏ khóa nút gửi, đúng 10.0đ sáng xanh), `QuestionBankSelector.tsx` (checkbox tick chọn 2 kho `practice_questions` / `exam_questions`, nút gửi duyệt Bộ Môn), `QuestionApprovalPage.tsx` (Trưởng Bộ Môn duyệt đề 3 quyết định: `APPROVED`, `NEEDS_REVISION` kèm góp ý, `REJECTED`).
>     * Khối 4 (MF-04 UI Quản trị & Phúc khảo): `ExamSeasonConfigPage.tsx` (Trưởng Bộ Môn khởi tạo kỳ thi, gán môn, cấu hình Ca thi chọn phòng lab trực tiếp trên ca và gán người coi thi, follow-up 1-5 câu, `ExamInputMode` VoiceOnly/VoiceWithTranscriptEdit - loại bỏ hoàn toàn VoiceAndTextInput), `AppealRequestModal.tsx` (Sinh viên nộp đơn phúc khảo nội bộ), `AppealManagementPage.tsx` (Trưởng Bộ Môn tiếp nhận danh sách phúc khảo, xem Evidence Panel và giao một Giảng viên chấm lại hoặc tự chấm).
>     * UI Vệ tinh: `UserManagementPage.tsx` (FE-09 quản lý người dùng 5 roles, khóa/mở tài khoản), `SemesterManagementPage.tsx` (FE-08 CRUD học kỳ), `ExamHistoryPage.tsx` (FE-02 Tab Luyện Tập), `AdminConfigPage.tsx`, `AdminDlqMonitorPage.tsx`, `AuditLogsPage.tsx`.

---

## 📁 1. CẤU TRÚC THƯ MỤC CHUẨN MỰC (REACT 19 + TAILWIND CSS v4)

```text
frontend/src/
├── routes/                          # Tuyến đường React Router DOM v7
│   ├── AppRoutes.tsx                # Khai báo toàn bộ routes của hệ thống theo 4 Khối chức năng
│   ├── ProtectedRoute.tsx           # Route Guard kiểm tra Auth & Role (student, lecturer, department_head, proctor, admin)
│   └── RoleBasedRedirect.tsx        # Điều hướng người dùng về đúng Dashboard sau khi login
├── types/                           # TypeScript Interfaces khớp 100% Backend DTOs
│   ├── auth.types.ts
│   ├── practice.types.ts            # Session, Question, Answer, Scorecard, FollowUp, ProgressiveOption
│   ├── mockExam.types.ts            # Quota, MockSession, MockAnswer, CloScorecard
│   ├── officialExam.types.ts        # Season, Shift, Ticket (7 trạng thái HOA), Submission, Audit
│   ├── rubric.types.ts              # Rubric, RubricCriterion (sum=10.0), QuestionBank
│   ├── appeal.types.ts              # AppealRequest, AppealDecision, LecturerAssignment
│   ├── user.types.ts                # UserManagement, UserRole, UserStatus (FE-09)
│   ├── semester.types.ts            # SemesterDto, CreateSemesterRequest (FE-08)
│   └── notification.types.ts        # NotificationDto, NotificationType (FE-11)
├── services/                        # API Services & WebSocket Client
│   ├── api.client.ts                # Axios Instance (Base URL: http://localhost:5000/api/v1, RFC 7807 Toast Interceptor)
│   ├── auth.service.ts
│   ├── practice.service.ts
│   ├── mockExam.service.ts
│   ├── question.service.ts
│   ├── officialExam.service.ts
│   ├── appeal.service.ts
│   ├── user.service.ts              # User Management API FE-09
│   ├── semester.service.ts          # Semester CRUD API FE-08
│   ├── notification.service.ts      # In-app Notification API FE-11
│   └── signalr.service.ts           # SignalR Hub Connection (/hubs/practice)
├── stores/                          # Zustand State Stores
│   ├── useAuthStore.ts              # Token, User profile, Active role
│   ├── usePracticeStore.ts          # Phiên luyện tập, danh sách câu, trạng thái nộp, scorecard
│   ├── useKioskStore.ts             # Trạng thái Kiosk, blur_count, mic_level, stream audio
│   ├── useExamTimerStore.ts         # Đồng hồ đếm ngược đồng bộ Master Timer
│   └── useNotificationStore.ts      # Unread count, notification list
├── hooks/                           # Custom Hooks nghiệp vụ
│   ├── useSpeechRecognition.ts      # Web Speech API bóc băng tiếng Việt thời gian thực (< 500ms)
│   ├── useAudioRecorder.ts          # MediaRecorder ghi âm .webm, băm SHA-256
│   ├── useMicrophoneLevel.ts        # Web Audio API đo dB mic (ngưỡng >= 60dB)
│   ├── useKioskLockdown.ts          # Fullscreen, chặn Alt+Tab, F11, F12, bắt onblur >= 3 lần
│   └── usePracticeHub.ts            # SignalR Client hook lắng nghe ReceiveGradingResult, ReceiveGradingError
├── components/                      # Reusable UI Components
│   ├── common/                      # Button, Modal, Card, Badge, Toast, Spinner, Input
│   ├── layout/                      # Navbar, Sidebar, Footer, KioskHeader, ProctorHeader
│   ├── practice/                    # BufferScreen, FollowUpQuestionCard, ScorecardModal, ModeSelector, ProgressiveSelector
│   ├── rubric/                      # RubricCriteriaEditor (tính tổng 10.0đ real-time), QuestionBankSelector, QuestionPreviewCard
│   ├── audio/                       # WaveformPlayer (Wavesurfer.js highlight transcript)
│   ├── kiosk/                       # KioskMicCheck, KioskQuestionViewer, KioskLockdownGuard
│   ├── appeals/                     # AppealRequestModal, AppealReviewCard, AssignLecturerModal
│   └── notifications/               # NotificationDrawer, NotificationItem
└── pages/                           # Các màn hình chính (Phân rã theo 4 Khối Chức Năng)
    ├── auth/                        # LoginPage.tsx (Google OAuth PKCE mọi email)
    ├── block1_practice/             # PracticePage.tsx, StudentDashboardPage.tsx, ExamHistoryPage.tsx (Tab Luyện Tập)
    ├── block2_mock_exam/            # MockExamPage.tsx, ExamHistoryPage.tsx (Tab Thi Thử)
    ├── block3_rubric_studio/        # QuestionStudioPage.tsx, QuestionApprovalPage.tsx, UserManagementPage.tsx, LecturerDashboardPage.tsx
    ├── block4_official_exam/        # ExamSeasonConfigPage.tsx, ProctorRoomMonitorPage.tsx, KioskCheckInPage.tsx, KioskHardwareMicCheckPage.tsx, KioskExamRoomPage.tsx, KioskSuspendedPage.tsx, KioskSubmittedPage.tsx, AuditEvidencePage.tsx, AppealManagementPage.tsx, SemesterManagementPage.tsx, NotificationPage.tsx, AdminConfigPage.tsx, AdminDlqMonitorPage.tsx, AuditLogsPage.tsx
    └── shared/                      # NotFoundPage.tsx, ForbiddenPage.tsx
```

---

## 🎨 2. CHI TIẾT TỪNG MÀN HÌNH THEO 4 KHỐI CHỨC NĂNG ĐỘC LẬP

---

### KHỐI 1 (MF-01): LUYỆN TẬP VẤN ĐÁP TƯƠNG TÁC (INTERACTIVE PRACTICE) & VỆ TINH

#### 🖥️ Màn hình FE-1.1: `PracticePage.tsx` (Phòng Luyện Tập Tương Tác MF-01)
* **Kỹ sư phụ trách:** 🧑 **Lê Vũ Hoàng**
* **Mô tả giao diện:**
  - Header phòng luyện tập: Tên môn, mã phiên, chế độ làm bài đang chọn (`[Per-Question]` hoặc `[Full-Session]`).
  - **Modal Cài Đặt Đầu Phiên (Mode & Question Count Selector):**
    * Chọn chế độ làm bài: `[Per-Question]` (Luyện từng câu, có cơ chế hỏi phụ khi điểm 4.0–8.0) hoặc `[Full-Session]` (Luyện trọn gói, không có hỏi phụ, nhận Scorecard tổng kết sau khi làm hết).
    * Chọn độ khó: "Dễ", "Trung bình", "Khó", hoặc **"progressive" ("Ngẫu nhiên từ dễ đến khó")**.
    * Chọn số lượng câu hỏi: Slider hoặc Input Number từ **3 đến 10 câu** (ràng buộc cấu hình `system_configs`).
    * Nếu kho đề không đủ câu hỏi, hiển thị Toast cảnh báo tiếng Việt rõ ràng từ API: *"Kho đề hiện tại chỉ có 5 câu hỏi Khó, vui lòng chọn số lượng ít hơn"*.
  - Khu vực hiển thị câu hỏi: Câu hỏi hiện tại ($i/N$), mức Bloom, danh sách các tiêu chí Barem Rubric $\sum \equiv 10.0$đ để sinh viên tham chiếu khi trả lời.
  - Bộ điều khiển Micro: Nút tròn lớn ở giữa màn hình (nhấn để bắt đầu nói, nhấn lần nữa để kết thúc). Bắt sóng âm thời gian thực bằng Web Speech API bóc băng tiếng Việt trực tiếp lên khung transcript tạm thời.
  - Sau khi kết thúc nói: Tự động chuyển tiếp sang màn hình đệm `BufferScreen.tsx`.

#### 🖥️ Màn hình FE-1.2: Component `BufferScreen.tsx` (Vùng Đệm Hiệu Đính Transcript)
* **Kỹ sư phụ trách:** 🧑 **Lê Vũ Hoàng**
* **Mô tả giao diện:**
  - Layout vùng đệm hiện đại, tập trung cao độ, nền tối dịu mắt.
  - **Đồng hồ đếm ngược SVG hình tròn (Circular Countdown):** Đếm ngược theo cấu hình động môn học `transcript_buffer_seconds` (từ 10–300s, **giá trị mặc định là 60 giây**).
  - Khung soạn thảo hiệu đính: Hiển thị văn bản transcript do Whisper/Web Speech nhận diện. Cho phép sinh viên chỉnh sửa các thuật ngữ kỹ thuật tiếng Anh (Code-Switching SE Glossary: Singleton, Microservices, AsNoTracking, CQRS...) bị phiên âm nhầm.
  - Nút **"Nghe Lại Câu Hỏi"**: Sử dụng Web Speech TTS đọc lại câu hỏi của giảng viên.
  - Hai nút hành động:
    * Nút xanh: **"Nộp Ngay"** $\to$ Lưu bài và gửi sang hàng đợi chấm điểm.
    * Khi đồng hồ đếm ngược về 0: Hệ thống tự động khóa ô gõ và tự động nộp bài (Auto-Submit).
  - Tối ưu hóa: Stream trực tiếp audio qua Cloudflare Whisper, không upload lên R2, không lưu `AudioUrl`.

#### 🖥️ Màn hình FE-1.3: Component `FollowUpQuestionCard.tsx` (Thẻ Câu Hỏi Phụ Đào Sâu)
* **Kỹ sư phụ trách:** 🧑 **Lê Vũ Hoàng**
* **Mô tả giao diện:**
  - Chỉ kích hoạt khi sinh viên chọn chế độ `[Per-Question]` và điểm số của câu trả lời rơi vào khoảng ranh giới **$4.0 \le \text{Score} \le 8.0$**.
  - Thiết kế thẻ màu vàng cam hổ phách (Amber), nhãn nổi bật: **"AI Follow-up: Câu Hỏi Đào Sâu Chuyên Sâu"** (số lượng câu hỏi phụ tối đa từ 1 đến 5 câu do Admin cấu hình, mặc định 2 câu).
  - Nội dung câu hỏi phụ do Gemini AI sinh dựa trên chuỗi suy luận CoT nhằm kiểm tra độ hiểu sâu của sinh viên tại các điểm còn mơ hồ.
  - Micro kích hoạt lại để sinh viên trả lời câu hỏi phụ; tiếp tục mở vùng đệm 60s trước khi nộp.

#### 🖥️ Màn hình FE-1.4: Component `ScorecardModal.tsx` (Bảng Điểm Rubric Chi Tiết)
* **Kỹ sư phụ trách:** 🧑 **Lê Vũ Hoàng**
* **Mô tả giao diện:**
  - Modal dạng Glassmorphism hiện đại bung lên khi AI chấm xong.
  - Vòng tròn điểm tổng kết lớn ở trên cùng (thang điểm 10.0, tô màu xanh lá nếu $\ge 8.0$, màu vàng nếu $5.0–7.9$, màu đỏ nếu $< 5.0$).
  - Accordion chi tiết từng tiêu chí Barem Rubric: Điểm đạt được / Điểm tối đa của tiêu chí, điểm mạnh, điểm thiếu sót kỹ thuật.
  - Nhận xét sư phạm tổng quát của AI kèm nút loa **"Nghe Nhận Xét"** sử dụng Web Speech TTS đọc to lời góp ý.
  - Nút **"Tiếp Tục Câu Tiếp Theo"** (hoặc "Xem Lại Lịch Sử" nếu là câu cuối cùng).

#### 🖥️ Hook FE-1.5: `usePracticeHub.ts` & `useSpeechRecognition.ts`
* **Kỹ sư phụ trách:** 🧑 **Lê Vũ Hoàng**
* **Mô tả kỹ thuật:**
  - `usePracticeHub.ts`: Quản lý kết nối SignalR Hub `/hubs/practice`, tự động `JoinSession(sessionId)`, lắng nghe sự kiện `ReceiveGradingResult` để cập nhật trạng thái chấm và bung `ScorecardModal`, bắt lỗi qua `ReceiveGradingError`.
  - `useSpeechRecognition.ts`: Bóc băng tiếng Việt thời gian thực bằng Web Speech API với độ trễ $< 500$ms, hỗ trợ nhận diện ngắt quãng và tự động phục hồi khi rớt mic.

#### 🖥️ Màn hình FE-1.6 (Vệ tinh): `StudentDashboardPage.tsx` (FE-10: Cổng Thông Tin Sinh Viên)
* **Kỹ sư phụ trách:** 🧑 **Lê Vũ Hoàng (Layout & Core UI) & 🧑 Nguyễn Đăng Hải (Tích hợp Dữ liệu)**
* **Mô tả giao diện:**
  - Lời chào sinh viên, MSSV, avatar, chuông thông báo (kèm badge số lượng thông báo chưa đọc).
  - **Khu vực Thẻ Môn Học:** Render các môn học đang theo học (PRN231, SWD392...). Mỗi thẻ hiển thị: Tên môn, tín chỉ, thời gian đệm `transcript_buffer_seconds`, tổng số câu hỏi luyện tập có sẵn.
  - Hai nút hành động: Nút xanh **"Luyện Tập Tự Do (MF-01)"** $\to$ Điều hướng sang `PracticePage`; Nút tím **"Thi Thử Bấm Giờ (MF-02)"** $\to$ Mở modal cài đặt thi thử.
  - **Khu vực Hạn Ngạch Thi Thử Hôm Nay:** Widget hiển thị Quota thi thử hôm nay: *"Đã dùng X/K lượt thi thử hôm nay"* (màu xanh nếu $< K$, màu đỏ rực nếu đã hết hạn ngạch).
  - **Khu vực Lịch Thi Thật Phòng Lab:** Banner thông báo ca thi phòng Lab sắp diễn ra (Phòng Lab 302, Ghế số 15, Ca thi 08:00).

#### 🖥️ Màn hình FE-1.7 (Vệ tinh): `ExamHistoryPage.tsx` (FE-02: Tab Luyện Tập)
* **Kỹ sư phụ trách:** 🧑 **Nguyễn Đăng Hải**
* **Mô tả giao diện:**
  - Tab 1: **"Lịch Sử Luyện Tập"** (MF-01).
  - Bảng danh sách các phiên luyện tập: Mã phiên, Tên môn học, Chế độ (`[Per-Question]` / `[Full-Session]`), Số câu hỏi, Thời gian làm bài, Điểm trung bình.
  - Nút **"Xem Chi Tiết Scorecard"**: Mở Drawer trượt từ bên phải hiển thị toàn bộ câu hỏi, transcript trả lời của sinh viên, điểm từng tiêu chí rubric và nhận xét AI của từng câu.

---

### KHỐI 2 (MF-02): THI THỬ VẤN ĐÁP BẤM GIỜ (TIMED MOCK EXAM) & VỆ TINH

#### 🖥️ Màn hình FE-2.1: `MockExamPage.tsx` (Phòng Thi Thử Bấm Giờ MF-02)
* **Kỹ sư phụ trách:** 🧑 **Nguyễn Đăng Hải**
* **Mô tả giao diện:**
  - **Modal Khởi Động Thi Thử:**
    * Sinh viên chọn môn học để thi thử.
    * Tùy chọn Follow-up chủ động: Sinh viên tick chọn **"Có Follow-up"** (AI hỏi đào sâu ngữ cảnh) hoặc **"Không Follow-up"** (Làm đề thi thẳng tính giờ).
    * Hiển thị cảnh báo hạn ngạch: *"Lượt thi này sẽ tính vào hạn ngạch K lượt/ngày của bạn"*.
  - **Giao diện làm bài chính:**
    * **Đồng hồ Master Timer đếm ngược to ở giữa trên cùng**: Đồng bộ chính xác với Server (`duration_minutes`), hiển thị định dạng `MM:SS`. Khi còn dưới 3 phút chuyển sang màu cam nhấp nháy, dưới 1 phút chuyển màu đỏ rực kèm còi cảnh báo.
    * Đề bài rút từ ma trận Bloom kho `practice_questions` (cô lập an toàn kho `exam_questions`).
    * Sinh viên **không chọn topic hay độ khó**, tập trung trả lời câu hỏi xuất hiện trên màn hình.

#### 🖥️ Cổng Kiểm Soát FE-2.2: Voice-First Gate (Khóa Cứng Bàn Phím)
* **Kỹ sư phụ trách:** 🧑 **Nguyễn Đăng Hải**
* **Mô tả kỹ thuật:**
  - Khóa cứng toàn bộ ô nhập văn bản (`disabled` và `readonly`), ẩn ô gõ bàn phím.
  - Bắt buộc 100% sinh viên phải kích hoạt Micro để trả lời câu hỏi.
  - Hiển thị trực quan thanh sóng âm (Audio VU Meter) thời gian thực khi sinh viên đang phát biểu.

#### 🖥️ Modal Cảnh Báo FE-2.3: `QuotaExceededModal.tsx` (Chặn Hạn Ngạch HTTP 429)
* **Kỹ sư phụ trách:** 🧑 **Nguyễn Đăng Hải**
* **Mô tả giao diện:**
  - Kích hoạt khi Backend trả về mã lỗi `HTTP 429 Too Many Requests`.
  - Thiết kế Modal cảnh báo màu đỏ rực, biểu tượng chiếc khiên chặn hạn ngạch.
  - Tiêu đề: **"ĐÃ ĐẠT GIỚI HẠN THI THỬ TRONG NGÀY"**.
  - Nội dung: *"Bạn đã sử dụng hết hạn ngạch K lượt thi thử hôm nay cho môn học này theo quy định của Trưởng Bộ Môn. Vui lòng quay lại vào ngày mai hoặc chuyển sang chế độ Luyện tập tự do (MF-01) để tiếp tục ôn luyện."*.
  - Nút hành động: Nút xanh **"Chuyển Sang Luyện Tập Tự Do"** và Nút phụ **"Về Trang Chủ"**.

#### 🖥️ Modal Kết Quả FE-2.4: `MockExamScorecardModal.tsx` (Scorecard Chuẩn Đầu Ra CLO)
* **Kỹ sư phụ trách:** 🧑 **Nguyễn Đăng Hải**
* **Mô tả giao diện:**
  - Bung lên ngay sau khi sinh viên hoàn thành bài thi thử (Instant Feedback).
  - Hiển thị bảng tổng kết điểm số bám sát ma trận Chuẩn Đầu Ra (CLOs) của môn học:
    * Điểm tổng kết bài thi thử (Thang điểm 10.0).
    * Bảng đánh giá mức độ đạt được theo từng CLO: CLO1 (Hiểu kiến trúc), CLO2 (Phân tích thiết kế), CLO3 (Bảo mật & Tối ưu)...
    * Nhận xét sư phạm từng phần chỉ rõ điểm mạnh và lỗ hổng kiến thức cần củng cố trước kỳ thi thật.
  - Nút **"Lưu & Xem Lịch Sử"** lưu kết quả vào hồ sơ sinh viên.

#### 🖥️ Màn hình FE-2.5 (Vệ tinh): `ExamHistoryPage.tsx` (FE-02: Tab Thi Thử)
* **Kỹ sư phụ trách:** 🧑 **Nguyễn Đăng Hải**
* **Mô tả giao diện:**
  - Tab 2: **"Lịch Sử Thi Thử Bấm Giờ"** (MF-02).
  - Bảng danh sách: Mã bài thi, Ngày thi, Điểm tổng kết, Đánh giá CLO, Trạng thái thời gian (Đúng giờ / Nộp muộn `is_late`).
  - Xem lại chi tiết từng lượt thi thử để so sánh biểu đồ tiến bộ điểm số qua các ngày.

---

### KHỐI 3 (MF-03): NGÂN HÀNG ĐỀ & RUBRIC STUDIO 10.0 & VỆ TINH

#### 🖥️ Màn hình FE-3.1 (Vệ tinh): `LoginPage.tsx` (Google OAuth PKCE Mọi Email)
* **Kỹ sư phụ trách:** 🧑 **Lê Vũ Hoàng**
* **Mô tả giao diện:**
  - Layout căn giữa hiện đại (Clean Glassmorphism), nền xám xanh FPT (`#F8FAFC`), logo Đại học FPT nổi bật góc trên.
  - Tiêu đề: *"Hệ Thống Luyện Thi & Đánh Giá Vấn Đáp Bằng LLM"*, phụ đề: *"FA26SE166 — Đại học FPT TP.HCM"*.
  - Nút bấm chính: **"Đăng nhập với tài khoản Google"** có icon Google chuẩn.
  - **Mở rộng cho mọi email Google:** Không giới hạn domain cứng `@fpt.edu.vn`, cho phép mọi tài khoản Google hợp lệ đăng nhập để kiểm thử và demo bảo vệ đồ án.
  - Xử lý Google OAuth PKCE: Lưu JWT Token vào `useAuthStore` và tự động điều hướng người dùng về đúng Dashboard theo 1 trong 5 vai trò hệ thống (`student`, `lecturer`, `department_head`, `proctor`, `admin`).

#### 🖥️ Màn hình FE-3.2 (Vệ tinh): `UserManagementPage.tsx` (FE-09: Quản Lý Người Dùng)
* **Kỹ sư phụ trách:** 🧑 **Nguyễn Đăng Hải**
* **Mô tả giao diện:**
  - Giao diện dành riêng cho `admin`.
  - Thanh tìm kiếm theo tên, email, bộ lọc vai trò (`student`, `lecturer`, `department_head`, `proctor`, `admin`) và trạng thái (`Active` / `Blocked`).
  - Bảng người dùng: Avatar, Họ và tên, Email, Vai trò hiện tại, Ngày tham gia, Trạng thái tài khoản.
  - Cột Hành động:
    * Nút **"Đổi Vai Trò"**: Dropdown cho phép chuyển đổi vai trò của người dùng sang 1 trong 5 vai trò cố định.
    * Nút **"Khóa / Mở Khóa"**: Switch bật/tắt kích hoạt tài khoản (`is_active`).

#### 🖥️ Màn hình FE-3.3: `QuestionStudioPage.tsx` (Soạn Đề & AI Sinh Đề Từ FLM)
* **Kỹ sư phụ trách:** 🧑 **Lê Vũ Hoàng (Layout & Preview Studio) & 🧑 Nguyễn Đăng Hải (Editor Logic)**
* **Mô tả giao diện:**
  - **Tab 1 — "AI Sinh Đề Tự Động Từ FLM Theo Barem Riêng":**
    * Giảng viên chọn môn học, nhập nội dung Syllabus FLM hoặc dán đề cương môn học, chọn CLO mục tiêu và mức độ Bloom.
    * Nhập barem tiêu chí riêng mong muốn của giảng viên.
    * Nút **"Kích Hoạt AI Sinh Đề"** $\to$ Gọi Gemini AI sinh câu hỏi, Model Answer $\ge 50$ ký tự và barem rubric $\sum \equiv 10.0$đ.
    * Khung Preview Studio hiển thị kết quả sinh để Giảng viên tự do chỉnh sửa nội dung đề bài, barem và câu trả lời mẫu trước khi lưu.
  - **Tab 2 — "Soạn Đề Thủ Công":**
    * Ô nhập nội dung câu hỏi vấn đáp.
    * Ô nhập câu trả lời mẫu chuẩn (Model Answer, bắt buộc $\ge 50$ ký tự, có bộ đếm ký tự thời gian thực).
    * Bộ chọn Cấp độ Bloom (Nhận biết, Thông hiểu, Vận dụng, Phân tích, Đánh giá, Sáng tạo).
  - Tích hợp `RubricCriteriaEditor.tsx` và `QuestionBankSelector.tsx`.

#### 🖥️ Component FE-3.4: `RubricCriteriaEditor.tsx` (Barem Rubric Studio 10.0đ)
* **Kỹ sư phụ trách:** 🧑 **Nguyễn Đăng Hải**
* **Mô tả giao diện:**
  - Danh sách các tiêu chí rubric con (Tên tiêu chí, Mô tả đánh giá, Điểm tối đa).
  - Nút **"Thêm Tiêu Chí Con"** và nút xóa từng tiêu chí.
  - **Bộ Tính Tổng Điểm Thời Gian Thực (Real-time Rubric Sum Counter):**
    * Hiển thị tổng điểm lớn góc trên: $\sum = \text{X.X} / 10.0$ điểm.
    * **Nếu tổng điểm $\ne 10.0$ điểm** (kể cả 9.9đ hay 10.1đ): Khung tổng điểm tô màu đỏ rực, hiển thị dòng cảnh báo: *"Tổng điểm rubric phải đúng bằng 10.0 điểm để đảm bảo chuẩn khảo thí"*, đồng thời **khóa cứng nút "Gửi Duyệt"** (`disabled`).
    * **Nếu tổng điểm $= 10.0$ điểm**: Khung tổng điểm sáng xanh lục an toàn, mở khóa nút gửi duyệt.

#### 🖥️ Component FE-3.5: `QuestionBankSelector.tsx` (Tick Chọn 2 Kho Đề & Gửi Duyệt)
* **Kỹ sư phụ trách:** 🧑 **Nguyễn Đăng Hải**
* **Mô tả giao diện:**
  - Hai ô Checkbox lớn trực quan:
    * [x] **"Lưu vào Kho Câu Hỏi Luyện Tập (`practice_questions`)"**: Dùng cho sinh viên ôn tập tự do và thi thử.
    * [x] **"Lưu vào Kho Câu Hỏi Thi Thật Phòng Lab (`exam_questions`)"**: Dùng cho các kỳ thi thật chính thức.
    * Giảng viên có thể tick chọn 1 trong 2 hoặc tick chọn cả 2 kho cùng lúc.
  - Nút bấm chính: **"Gửi Lên Cho Bộ Môn Duyệt"** (`POST /api/v1/questions/batch-submit-review`). Câu hỏi chuyển trạng thái `SUBMITTED_FOR_REVIEW` và hiển thị Toast thông báo thành công.

#### 🖥️ Màn hình FE-3.6: `QuestionApprovalPage.tsx` (Trưởng Bộ Môn Thẩm Định Đề)
* **Kỹ sư phụ trách:** 🧑 **Nguyễn Đăng Hải**
* **Mô tả giao diện:**
  - Giao diện dành riêng cho **Trưởng Bộ Môn (`department_head`)**.
  - Danh sách câu hỏi chờ thẩm định từ các giảng viên gửi lên.
  - Xem chi tiết từng câu: Đề bài, Giảng viên tạo, Môn học, CLO, Model Answer ($\ge 50$ ký tự), Barem rubric chi tiết ($\sum \equiv 10.0$đ), Kho đề lưu trữ.
  - **3 Nút Quyết Định Thẩm Định:**
    1. Nút xanh: **"Phê Duyệt (APPROVED)"** $\to$ Lưu chính thức vào ngân hàng đề môn học.
    2. Nút vàng: **"Yêu Cầu Chỉnh Sửa (NEEDS_REVISION)"** $\to$ Mở modal yêu cầu nhập góp ý chỉnh sửa $\ge 10$ ký tự, trả về cho giảng viên.
    3. Nút đỏ: **"Từ Chối (REJECTED)"** $\to$ Mở modal nhập lý do từ chối, loại bỏ câu hỏi.

#### 🖥️ Màn hình FE-3.7 (Vệ tinh): `LecturerDashboardPage.tsx` (FE-10: Cổng Giảng Viên)
* **Kỹ sư phụ trách:** 🧑 **Lê Vũ Hoàng**
* **Mô tả giao diện:**
  - Thống kê tổng số câu hỏi đã tạo, số câu hỏi đang chờ Trưởng Bộ Môn duyệt, số câu hỏi đã được phê duyệt, số câu hỏi cần chỉnh sửa.
  - Danh sách nhanh các ca thi phòng Lab cần hậu kiểm điểm số sau thi.
  - Lối tắt truy cập nhanh: "Soạn đề mới / AI Gen", "Cổng Hậu kiểm Evidence Panel".

---

### KHỐI 4 (MF-04): THI THẬT PHÒNG LAB, CÔNG BỐ ĐIỂM & PHÚC KHẢO NỘI BỘ & VỆ TINH

#### 🖥️ Màn hình FE-4.1: `ExamSeasonConfigPage.tsx` (Trưởng BM Cấu Hình Kỳ Thi & Ca Thi)
* **Kỹ sư phụ trách:** 🧑 **Nguyễn Đăng Hải**
* **Mô tả giao diện:**
  - Giao diện dành cho **Trưởng Bộ Môn (`department_head`)**.
  - Khởi tạo Kỳ thi (`OfficialExamSession`, ví dụ Kỳ thi Kết thúc môn FA26), chọn danh sách môn thi trong kỳ thi.
  - **Cấu hình Ca thi (`RealExamSessionShift`):**
    * Chọn phòng máy lab trực tiếp trên ca thi (Phòng Lab 301, 302...).
    * Phân công người coi thi trực tiếp (chọn Giám thị `proctor` hoặc Giảng viên `lecturer`).
    * Thiết lập thời gian ca thi, số lượng máy trạm tối đa (40 máy).
  - **Cấu hình Môn thi trong kỳ thi:**
    * Cấu hình Follow-up: Bật/tắt (`has_follow_up`) và số câu hỏi phụ (`max_follow_up_questions`, 1–5 câu, mặc định 2 câu).
    * Cấu hình phương thức làm bài `ExamInputMode`: Chọn `VoiceOnly` hoặc `VoiceWithTranscriptEdit` (**loại bỏ hoàn toàn lựa chọn `VoiceAndTextInput`**).
    * Cấu hình thời gian đệm `TranscriptBufferSeconds` (10–300s, mặc định 60s).

#### 🖥️ Màn hình FE-4.2: `ProctorRoomMonitorPage.tsx` (FE-P01: Giám Thị Phòng Thi)
* **Kỹ sư phụ trách:** 🧑 **Lê Vũ Hoàng**
* **Mô tả giao diện:**
  - Giao diện dành cho **Giám thị phòng thi (`proctor`)**.
  - Thanh điều khiển: Nút **"Bắt Đầu Ca Thi"**, nút **"Kết Thúc Ca Thi"**, đồng hồ đếm ngược ca thi phòng Lab.
  - **Ma Trận Giám Sát 40 Máy Trạm Phòng Lab (Lưới 8x5 tương ứng Ghế 1–40):**
    * Mỗi ô máy hiển thị: Số ghế (01–40), MSSV, Họ tên thí sinh, Địa chỉ IP máy trạm (`ip_address`), Trạng thái vé thi (7 trạng thái HOA: `SCHEDULED`, `IN_PROGRESS`, `SUBMITTED`...).
    * Đèn trạng thái máy trạm: Màu xám (Chưa check-in), Màu xanh lá (Đang thi bình thường), Màu vàng (Cảnh báo mất focus), Màu đỏ (Đã bị đình chỉ thi).
    * Nút hành động trên từng máy: **"Đình Chỉ Thi Thủ Công"** (lập biên bản khi phát hiện gian lận tại chỗ).

#### 🖥️ Màn hình FE-4.3: `KioskCheckInPage.tsx` (FE-K01: Check-in Kiosk IP Binding)
* **Kỹ sư phụ trách:** 🧑 **Lê Vũ Hoàng**
* **Mô tả giao diện:**
  - Giao diện máy trạm Kiosk phòng Lab khóa cứng.
  - Ô nhập: **Số thứ tự ghế ngồi (STT 1–40)** và **Mã số sinh viên (MSSV)**.
  - Nút **"Xác Nhận Check-in Phòng Thi"**:
    * Gửi request lên Backend kiểm tra ràng buộc địa chỉ IP máy trạm `ip_address` với số ghế đã đăng ký.
    * Nếu sai IP: Hiển thị thông báo đỏ rực `HTTP 403 Forbidden`: *"Vị trí máy trạm không khớp với số ghế đã phân công. Vui lòng liên hệ Giám thị phòng thi!"*.
    * Nếu khớp IP: Chuyển sang màn hình kiểm tra micro phần cứng.

#### 🖥️ Màn hình FE-4.4: `KioskHardwareMicCheckPage.tsx` (FE-K02: Kiểm Tra Micro Phần Cứng)
* **Kỹ sư phụ trách:** 🧑 **Lê Vũ Hoàng**
* **Mô tả giao diện:**
  - Bắt buộc kiểm tra micro trong vòng 30 giây trước khi mở đề thi.
  - **Thanh đo cường độ âm lượng thời gian thực (VU Meter):** Sử dụng Web Audio API phân tích tín hiệu âm thanh từ micro phần cứng.
  - Yêu cầu thí sinh đọc to đoạn văn mẫu kiểm tra âm thanh: Cường độ âm lượng bắt buộc phải chạm ngưỡng **$\ge 60$ dB**.
  - Đèn tín hiệu: Chuyển sang màu xanh lục khi đạt $\ge 60$dB. Lúc này nút **"Vào Phòng Thi Chính Thức"** mới sáng lên cho phép click.

#### 🖥️ Màn hình FE-4.5: `KioskExamRoomPage.tsx` (FE-K03: Phòng Thi Kiosk Lockdown)
* **Kỹ sư phụ trách:** 🧑 **Lê Vũ Hoàng**
* **Mô tả giao diện & Cơ chế phong tỏa Kiosk:**
  - Kích hoạt chế độ toàn màn hình Fullscreen Lockdown (`requestFullscreen`), vô hiệu hóa chuột phải, vô hiệu hóa phím tắt hệ thống (Alt+Tab, Windows, F11, F12, DevTools, Esc).
  - **Cơ chế bắt sự kiện mất tiêu điểm (`window.onblur`):**
    * Mất focus lần 1: Bung Modal cảnh báo màu vàng: *"CẢNH BÁO VI PHẠM LẦN 1/3: Tuyệt đối không chuyển cửa sổ hoặc click ra ngoài phòng thi!"*.
    * Mất focus lần 2: Cảnh báo màu cam kèm còi bíp: *"CẢNH BÁO VI PHẠM LẦN 2/3: Lần vi phạm tiếp theo bạn sẽ bị ĐÌNH CHỈ THI ngay lập tức!"*.
    * **Mất focus lần 3 (`blur_count >= 3`)**: Lập tức chuyển hướng sang `KioskSuspendedPage.tsx`, khóa chết máy trạm và tự động nộp bài lập biên bản vi phạm.
  - **Luồng thi:** Hiển thị câu hỏi thi thật rút từ kho `exam_questions`.
  - **Niêm phong âm thanh (Cloudflare R2):** Stream trực tiếp audio lên Cloudflare R2 với tên file bất biến **`STT_MSSV.webm`** (ví dụ `01_SE170123.webm`), tính toán mã băm SHA-256 niêm phong file tại máy trạm.
  - Nộp bài: Nhận phản hồi Persist First $< 100$ms lưu DB trạng thái `SUBMITTED`.

#### 🖥️ Màn hình FE-4.6: `KioskSuspendedPage.tsx` (FE-K04: Màn Hình Đình Chỉ Thi)
* **Kỹ sư phụ trách:** 🧑 **Lê Vũ Hoàng**
* **Mô tả giao diện:**
  - Nền đỏ thẫm toàn màn hình, biểu tượng cảnh báo vi phạm lớn.
  - Tiêu đề: **"BÀI THI ĐÃ BỊ ĐÌNH CHỈ DO VI PHẠM QUY CHẾ PHÒNG THI"**.
  - Lý do: *"Hệ thống phát hiện máy trạm mất tiêu điểm (Focus Loss) quá 3 lần quy định"*.
  - Khóa toàn bộ bàn phím và chuột. Thông báo: *"Thí sinh giữ nguyên vị trí và chờ Giám thị phòng thi lập biên bản"*.

#### 🖥️ Màn hình FE-4.7: `KioskSubmittedPage.tsx` (FE-K05: Biên Nhận Niêm Phong An Toàn)
* **Kỹ sư phụ trách:** 🧑 **Lê Vũ Hoàng**
* **Mô tả giao diện (Quy tắc bất biến MF-04):**
  - Nền xanh dịu an toàn, biểu tượng ổ khóa niêm phong bảo mật.
  - Tiêu đề: **"BÀI THI ĐÃ ĐƯỢC NIÊM PHONG VÀ LƯU TRỮ AN TOÀN"**.
  - Thông tin biên nhận điện tử:
    * Mã thí sinh: `SE170123` | STT: `01`.
    * File âm thanh niêm phong: `01_SE170123.webm`.
    * Mã băm toàn vẹn SHA-256: `e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855`.
    * Thời gian nộp: `08:45:22 15/10/2026`.
  - **Dòng thông báo quan trọng:** *"Bài thi đã được lưu trữ an toàn. Kết quả sẽ do Giảng viên thẩm định và công bố trên hệ thống Student Portal. Thí sinh ký biên bản nộp bài giấy và rời khỏi phòng thi."*
  - **Cam kết kỹ thuật:** **0% hiển thị điểm liền, 0% khiếu nại tại chỗ**. Tuyệt đối không có nút xem điểm hay phúc khảo trên máy Kiosk.

#### 🖥️ Màn hình FE-4.8: `AuditEvidencePage.tsx` (FE-L02: Cổng Hậu Kiểm Evidence Panel)
* **Kỹ sư phụ trách:** 🧑 **Lê Vũ Hoàng**
* **Mô tả giao diện:**
  - Cổng Hậu kiểm dành riêng cho **Giảng viên (`lecturer`)**.
  - **Tự động phân loại 2 nhóm bài thi:**
    * **Nhóm 1 (Đáng nghi ngờ / Fail / Cần can thiệp):** Các bài có `is_suspicious == true`, `confidence_score < 0.70`, hoặc bài thi bị fail/điểm liệt. **Sinh viên bắt buộc phải đợi Giảng viên chấm hết toàn bộ các bài trong nhóm này**.
    * **Nhóm 2 (Độ tin cậy cao):** Các bài thi AI chấm chuẩn xác, giảng viên rà soát nhanh.
  - **Evidence Panel (Bảng Bằng Chứng Đối Chiếu):**
    * **Waveform Audio Player (Wavesurfer.js):** Vẽ biểu đồ sóng âm thanh trực tiếp từ Cloudflare R2 (`STT_MSSV.webm`). Click vào từng đoạn văn bản transcript để tua nhanh audio đến đúng vị trí thí sinh đang nói.
    * Bảng đối chiếu song song: Transcript Whisper gốc vs Chuỗi suy luận AI CoT từng tiêu chí rubric.
    * Khung điều chỉnh điểm: Cho phép Giảng viên nhập điểm mới, **bắt buộc nhập lý do giải trình (`override_reason`) $\ge 10$ ký tự**.
  - **Nút "Công Bố Điểm (Publish Grades)" Atomic 100%:**
    * Chỉ sáng xanh cho phép click khi **100% sinh viên trong ca thi đã có điểm hoàn chỉnh**.
    * Khi click: Kích hoạt One-Way Lock (`is_locked = true` $\to$ HTTP 403), giải phóng điểm gửi về cho sinh viên trên Student Portal.

#### 🖥️ Component FE-4.9: `AppealRequestModal.tsx` (Nộp Đơn Phúc Khảo Nội Bộ)
* **Kỹ sư phụ trách:** 🧑 **Nguyễn Đăng Hải**
* **Mô tả giao diện:**
  - Bung lên từ Student Portal sau khi Giảng viên đã công bố điểm chính thức.
  - Hiển thị thông tin điểm hiện tại và tiêu chí rubric muốn phúc khảo.
  - Ô nhập lý do khiếu nại (bắt buộc $\ge 20$ ký tự).
  - Nút **"Nộp Đơn Phúc Khảo"**: Gửi đơn lên hệ thống (`POST /api/v1/appeals`), tạo thực thể `AppealRequest` tự động gán cho Trưởng Bộ Môn thẩm định.

#### 🖥️ Màn hình FE-4.10: `AppealManagementPage.tsx` (Trưởng BM Thẩm Định & Giao Chấm Lại)
* **Kỹ sư phụ trách:** 🧑 **Nguyễn Đăng Hải**
* **Mô tả giao diện:**
  - Giao diện dành riêng cho **Trưởng Bộ Môn (`department_head`)**.
  - Danh sách các đơn phúc khảo nội bộ chờ xử lý của sinh viên.
  - Xem Evidence Panel của bài thi bị khiếu nại (Audio R2, Transcript, Điểm cũ, Lý do khiếu nại).
  - **Hai phương thức thẩm định:**
    1. **Giao cho Giảng viên chấm lại:** Mở modal chọn một Giảng viên trong bộ môn (`assign-lecturer`) để thẩm định độc lập. Giảng viên được giao sẽ nhận thông báo in-app và truy cập Evidence Panel để chấm lại.
    2. **Trưởng Bộ Môn tự ra quyết định:** Nhập điểm mới chấp thuận (`APPROVED`) hoặc giữ nguyên điểm từ chối (`REJECTED`) kèm kết luận thẩm định.

#### 🖥️ Màn hình FE-4.11 (Vệ tinh): `SemesterManagementPage.tsx` (FE-08: Quản Lý Học Kỳ)
* **Kỹ sư phụ trách:** 🧑 **Nguyễn Đăng Hải**
* **Mô tả giao diện:**
  - Giao diện dành cho `admin`.
  - Danh sách các học kỳ: Mã học kỳ (FA26, SP26...), Tên học kỳ, Ngày bắt đầu, Ngày kết thúc, Trạng thái (Đang diễn ra, Sắp tới, Đã kết thúc).
  - Modal Thêm / Chỉnh sửa học kỳ. Nút đặt làm học kỳ mặc định.

#### 🖥️ Component FE-4.12 (Vệ tinh): `NotificationDrawer.tsx` / `NotificationPage.tsx` (FE-11)
* **Kỹ sư phụ trách:** 🧑 **Lê Vũ Hoàng**
* **Mô tả giao diện:**
  - Hộp thư thông báo in-app hiển thị dạng Drawer trượt từ cạnh phải hoặc trang riêng.
  - Danh sách thông báo theo thứ tự thời gian: Biểu tượng loại thông báo (Lịch thi, Điểm công bố, Duyệt đề, Phúc khảo), Tiêu đề, Nội dung tóm tắt, Thời gian.
  - Đánh dấu đã đọc khi click vào thông báo; Nút **"Đánh dấu tất cả là đã đọc"**. Điều hướng nhanh đến trang liên quan khi click thông báo.

#### 🖥️ Màn hình FE-4.13 (Vệ tinh): Admin Configuration, DLQ & Audit Logs
* **Kỹ sư phụ trách:** 🧑 **Nguyễn Đăng Hải**
* **Mô tả giao diện:**
  - `AdminConfigPage.tsx`: Cấu hình số câu hỏi follow-up luyện tập MF-01 (1–5 câu, mặc định 2 câu).
  - `AdminDlqMonitorPage.tsx`: Giám sát hàng đợi chết `dead_letter_queues`, xem chi tiết lỗi và nút "Replay Chấm Bù".
  - `AuditLogsPage.tsx`: Bảng tra cứu nhật ký kiểm toán hệ thống (ai đã sửa điểm, duyệt đề, thời gian, IP).

---

## 📊 3. BẢNG TỔNG HỢP MA TRẬN 28 MÀN HÌNH & PHÂN CÔNG TÁC VỤ FRONTEND

| Mã Màn Hình | Tên Tệp Component | Phân Hệ / Khối Chức Năng | Kỹ Sư Phụ Trách | Trạng Thái Kỹ Thuật |
|:---|:---|:---:|:---:|:---:|
| **FE-1.1** | `PracticePage.tsx` (Per & Full, Progressive 3-10 câu) | Khối 1 (MF-01) | **🧑 Lê Vũ Hoàng** | ⏳ Kế hoạch Sprint (Scaffolding ready, backlog for Sprint) |
| **FE-1.2** | `BufferScreen.tsx` (Vùng đệm hiệu đính 60s SVG countdown) | Khối 1 (MF-01) | **🧑 Lê Vũ Hoàng** | ⏳ Kế hoạch Sprint (Scaffolding ready, backlog for Sprint) |
| **FE-1.3** | `FollowUpQuestionCard.tsx` (Hỏi phụ 1-5 câu khi 4.0-8.0) | Khối 1 (MF-01) | **🧑 Lê Vũ Hoàng** | ⏳ Kế hoạch Sprint (Scaffolding ready, backlog for Sprint) |
| **FE-1.4** | `ScorecardModal.tsx` (Bảng điểm Rubric chi tiết + TTS) | Khối 1 (MF-01) | **🧑 Lê Vũ Hoàng** | ⏳ Kế hoạch Sprint (Scaffolding ready, backlog for Sprint) |
| **FE-1.5** | `usePracticeHub.ts` & `useSpeechRecognition.ts` (SignalR & STT) | Khối 1 (MF-01) | **🧑 Lê Vũ Hoàng** | ⏳ Kế hoạch Sprint (Scaffolding ready, backlog for Sprint) |
| **FE-1.6** | `StudentDashboardPage.tsx` (FE-10 Thẻ môn, tiến độ) | Khối 1 (Vệ tinh) | **🧑 Hoàng + Hải** | ⏳ Kế hoạch Sprint (Scaffolding ready, backlog for Sprint) |
| **FE-1.7** | `ExamHistoryPage.tsx` (FE-02 Tab Luyện Tập) | Khối 1 (Vệ tinh) | **🧑 Nguyễn Đăng Hải** | ⏳ Kế hoạch Sprint (Scaffolding ready, backlog for Sprint) |
| **FE-2.1** | `MockExamPage.tsx` (Thi thử Voice-First, Master Timer) | Khối 2 (MF-02) | **🧑 Nguyễn Đăng Hải** | ⏳ Kế hoạch Sprint (Scaffolding ready, backlog for Sprint) |
| **FE-2.2** | Voice-First Gate (Khóa cứng input text, chỉ dùng Mic) | Khối 2 (MF-02) | **🧑 Nguyễn Đăng Hải** | ⏳ Kế hoạch Sprint (Scaffolding ready, backlog for Sprint) |
| **FE-2.3** | `QuotaExceededModal.tsx` (Chặn Quota HTTP 429) | Khối 2 (MF-02) | **🧑 Nguyễn Đăng Hải** | ⏳ Kế hoạch Sprint (Scaffolding ready, backlog for Sprint) |
| **FE-2.4** | `MockExamScorecardModal.tsx` (Scorecard chuẩn đầu ra CLO) | Khối 2 (MF-02) | **🧑 Nguyễn Đăng Hải** | ⏳ Kế hoạch Sprint (Scaffolding ready, backlog for Sprint) |
| **FE-2.5** | `ExamHistoryPage.tsx` (FE-02 Tab Thi Thử) | Khối 2 (Vệ tinh) | **🧑 Nguyễn Đăng Hải** | ⏳ Kế hoạch Sprint (Scaffolding ready, backlog for Sprint) |
| **FE-3.1** | `LoginPage.tsx` (Google OAuth PKCE mở mọi email) | Khối 3 (Vệ tinh) | **🧑 Lê Vũ Hoàng** | ⏳ Kế hoạch Sprint (Scaffolding ready, backlog for Sprint) |
| **FE-3.2** | `UserManagementPage.tsx` (FE-09 Quản lý người dùng 5 roles) | Khối 3 (Vệ tinh) | **🧑 Nguyễn Đăng Hải** | ⏳ Kế hoạch Sprint (Scaffolding ready, backlog for Sprint) |
| **FE-3.3** | `QuestionStudioPage.tsx` (Soạn đề, AI Gen FLM theo barem) | Khối 3 (MF-03) | **🧑 Hoàng + Hải** | ⏳ Kế hoạch Sprint (Scaffolding ready, backlog for Sprint) |
| **FE-3.4** | `RubricCriteriaEditor.tsx` (Barem Studio tính tổng 10.0đ) | Khối 3 (MF-03) | **🧑 Nguyễn Đăng Hải** | ⏳ Kế hoạch Sprint (Scaffolding ready, backlog for Sprint) |
| **FE-3.5** | `QuestionBankSelector.tsx` (Tick chọn 2 kho & Gửi duyệt) | Khối 3 (MF-03) | **🧑 Nguyễn Đăng Hải** | ⏳ Kế hoạch Sprint (Scaffolding ready, backlog for Sprint) |
| **FE-3.6** | `QuestionApprovalPage.tsx` (Trưởng BM duyệt 3 quyết định) | Khối 3 (MF-03) | **🧑 Nguyễn Đăng Hải** | ⏳ Kế hoạch Sprint (Scaffolding ready, backlog for Sprint) |
| **FE-3.7** | `LecturerDashboardPage.tsx` (FE-10 Cổng Giảng viên) | Khối 3 (Vệ tinh) | **🧑 Lê Vũ Hoàng** | ⏳ Kế hoạch Sprint (Scaffolding ready, backlog for Sprint) |
| **FE-4.1** | `ExamSeasonConfigPage.tsx` (Trưởng BM cấu hình ca thi, phòng lab) | Khối 4 (MF-04) | **🧑 Nguyễn Đăng Hải** | ⏳ Kế hoạch Sprint (Scaffolding ready, backlog for Sprint) |
| **FE-4.2** | `ProctorRoomMonitorPage.tsx` (FE-P01 Giám sát 40 máy lab) | Khối 4 (MF-04) | **🧑 Lê Vũ Hoàng** | ⏳ Kế hoạch Sprint (Scaffolding ready, backlog for Sprint) |
| **FE-4.3** | `KioskCheckInPage.tsx` (FE-K01 IP Binding ghế 1-40) | Khối 4 (MF-04) | **🧑 Lê Vũ Hoàng** | ⏳ Kế hoạch Sprint (Scaffolding ready, backlog for Sprint) |
| **FE-4.4** | `KioskHardwareMicCheckPage.tsx` (FE-K02 Mic VU Meter >=60dB)| Khối 4 (MF-04) | **🧑 Lê Vũ Hoàng** | ⏳ Kế hoạch Sprint (Scaffolding ready, backlog for Sprint) |
| **FE-4.5** | `KioskExamRoomPage.tsx` (FE-K03 Lockdown, blur >=3, R2 audio) | Khối 4 (MF-04) | **🧑 Lê Vũ Hoàng** | ⏳ Kế hoạch Sprint (Scaffolding ready, backlog for Sprint) |
| **FE-4.6** | `KioskSuspendedPage.tsx` (FE-K04 Màn hình đình chỉ thi) | Khối 4 (MF-04) | **🧑 Lê Vũ Hoàng** | ⏳ Kế hoạch Sprint (Scaffolding ready, backlog for Sprint) |
| **FE-4.7** | `KioskSubmittedPage.tsx` (FE-K05 Niêm phong: 0% điểm liền) | Khối 4 (MF-04) | **🧑 Lê Vũ Hoàng** | ⏳ Kế hoạch Sprint (Scaffolding ready, backlog for Sprint) |
| **FE-4.8** | `AuditEvidencePage.tsx` (FE-L02 Wavesurfer.js, Publish Atomic) | Khối 4 (MF-04) | **🧑 Lê Vũ Hoàng** | ⏳ Kế hoạch Sprint (Scaffolding ready, backlog for Sprint) |
| **FE-4.9** | `AppealRequestModal.tsx` (Sinh viên nộp đơn phúc khảo nội bộ) | Khối 4 (MF-04) | **🧑 Nguyễn Đăng Hải** | ⏳ Kế hoạch Sprint (Scaffolding ready, backlog for Sprint) |
| **FE-4.10** | `AppealManagementPage.tsx` (Trưởng BM giao GV chấm lại) | Khối 4 (MF-04) | **🧑 Nguyễn Đăng Hải** | ⏳ Kế hoạch Sprint (Scaffolding ready, backlog for Sprint) |
| **FE-4.11** | `SemesterManagementPage.tsx` (FE-08 CRUD Học kỳ) | Khối 4 (Vệ tinh) | **🧑 Nguyễn Đăng Hải** | ⏳ Kế hoạch Sprint (Scaffolding ready, backlog for Sprint) |
| **FE-4.12** | `NotificationDrawer.tsx` / `NotificationPage.tsx` (FE-11 In-app) | Khối 4 (Vệ tinh) | **🧑 Lê Vũ Hoàng** | ⏳ Kế hoạch Sprint (Scaffolding ready, backlog for Sprint) |
| **FE-4.13** | `AdminConfigPage.tsx`, `AdminDlqMonitorPage.tsx`, `AuditLogsPage.tsx` | Khối 4 (Vệ tinh) | **🧑 Nguyễn Đăng Hải** | ⏳ Kế hoạch Sprint (Scaffolding ready, backlog for Sprint) |
