# 🖥️ FRONTEND MASTER TASK BOARD & EXECUTION SPECIFICATIONS (v8.0)
## ĐỒ ÁN TỐT NGHIỆP SEP490 (FA26SE166) — HỆ THỐNG LUYỆN THI & ĐÁNH GIÁ VẤN ĐÁP BẰNG LLM
### KHO MÃ NGUỒN: `05_Source_Code/frontend` | CỔNG DEV: `3000` | BACKEND API: `5000` | POSTGRESQL: `5432` (38 BẢNG 3NF — 7 BOUNDED CONTEXTS) | 6 ROLES RBAC

---

> [!IMPORTANT]
> **QUY CHUẨN TỐI THƯỢNG & NGUỒN SỰ THẬT DUY NHẤT (SINGLE SOURCE OF TRUTH):**
> - **Mã đề tài:** FA26SE166 | **Học kỳ:** Fall 2026 (FA26) — Đại học FPT TP.HCM (FPT SG)
> - **Tech Stack Chuẩn:** React 19 (`^19.2.8`), Vite (`^8.3.0`), Tailwind CSS v4, React Router DOM v7, TypeScript 5.8+, Zustand, TanStack Query v5, `@microsoft/signalr`, Web Speech API (STT/TTS), Web Audio API (Mic dB Check), Wavesurfer.js (Waveform Audio Player), Google OAuth 2.0 (Google Identity Services) & Dual Auth.
> - **Hệ thống Phân quyền & CSDL:** **6 Roles RBAC** (`admin`, `department_head`, `examination_officer`, `lecturer`, `proctor`, `student`) kết nối **38 bảng 3NF (7 Bounded Contexts)** trên PostgreSQL 16.
> - **File này là tài liệu duy nhất điều phối toàn bộ công việc Frontend** cho 2 kỹ sư: **Lê Vũ Hoàng** và **Phạm Nguyễn Đăng Hải**.
> - Tuyệt đối tuân thủ 12 cam kết kiến trúc tại `AGENTS.md` và `MASTER_ARCHITECTURE.md`. Không tự ý suy diễn thêm màn hình hoặc phá vỡ API contract.

---

# 📊 PHẦN 1: BẢNG MA TRẬN PHÂN BỔ CÔNG VIỆC (BALANCED 50/50 MATRIX)

| Kỹ sư đảm nhiệm | Chức năng chính (Heavy Core) | Các task vệ tinh ("Râu ria" hoàn thiện) | Thế mạnh chuyên môn |
| :--- | :--- | :--- | :--- |
| 🧑 **Lê Vũ Hoàng**<br>*(Lead Frontend Architect)* | **1. Module MF-01: Luyện Tập Tương Tác Giọng Nói**<br>• Practice Flow (Per-Question On-Demand & Full Progressive 3–10)<br>• Buffer Screen 60s đếm ngược SVG & Timeout 5 phút (`HTTP 410 Gone`)<br>• SignalR WebSocket client & Follow-up A2 ($4.0 \le \text{Score} \le 8.0$)<br>• Scorecard Modal chi tiết Rubric<br><br>**2. Module MF-04 (Nhánh A): An Ninh Kiosk & Giám Thị (R4)**<br>• Kiosk Check-In IP Binding STT 1–40 (FE-K01)<br>• Hardware Mic Check $\ge 60$dB trong 30s (FE-K02)<br>• Kiosk Exam Lockdown, Anti-cheat blur $\ge 3$ (FE-K03)<br>• Kiosk Submitted an toàn 0% điểm liền (FE-K05)<br>• Proctor Room Monitor 40 máy lab (FE-P01): Giám thị (`proctor`) **CHỈ điểm danh STT 1–40 và báo cáo vi phạm phòng thi** vào `exam_anti_cheat_logs` (CẤM mở/đóng ca thi, CẤM mở khóa đề, CẤM sửa điểm)<br><br>**3. Module MF-04 (Nhánh B) & MF-05: Hậu Kiểm Giảng Viên & Ca Thi Trong Lớp (R6)**<br>• Audit Evidence Page (FE-L02) & Waveform Player Wavesurfer.js tua transcript<br>• Form sửa điểm có lý do giải trình $\ge 10$ ký tự<br>• Ca Thi Trong Lớp (`in_class_exam_shifts`): Màn hình chiếu mật khẩu TOTP 6 ký tự tự động xoay vòng 5 phút (kèm grace period 60s), SV nhập mã vào thi, GV hậu kiểm & chốt bảng điểm thành phần One-Way Lock | **1. Core Architecture & Build:**<br>• Khắc phục triệt để lỗi build Vite & TypeScript (`npm run build` Exit Code 0)<br>• Sửa alias `@/utils/cn` trong `utils.ts` và `separator.tsx`<br>• Gán type chuẩn cho các ThreeScene<br><br>**2. Xác thực Kép (Dual Auth) & Route Guards 6 Roles:**<br>• Google Login ID Token button + Đăng nhập tài khoản trường cấp (`password-login`, `change-password`)<br>• Silent Refresh Token Rotation<br>• Route Guards chuẩn **6 Roles RBAC** (`admin`, `department_head`, `examination_officer`, `lecturer`, `proctor`, `student`)<br><br>**3. Dashboard Sinh viên & Giảng viên (FE-10):**<br>• `StudentDashboardPage.tsx`<br>• `LecturerDashboardPage.tsx`<br><br>**4. Hộp thư Thông báo In-App (FE-11):**<br>• `NotificationDrawer.tsx`<br>• `NotificationPage.tsx` | UX/UI Âm thanh thời gian thực, Web Speech API, SignalR WebSockets, Web Audio API đo dB, Kiosk Lockdown An ninh, Waveform Player, TOTP Countdown. |
| 🧑 **Phạm Nguyễn Đăng Hải**<br>*(Frontend Developer & DB Specialist)* | **1. Module MF-02: Thi Thử Tính Giờ Có Hạn Ngạch**<br>• Mock Exam Voice-First Gate (khóa text input)<br>• Master Countdown Timer đồng bộ server<br>• Quota Modal cảnh báo `HTTP 429` do Admin cấu hình (`max_mock_exams_per_day`)<br>• Scorecard chi tiết theo Chuẩn đầu ra CLO (`course_clos` 3NF)<br><br>**2. Module MF-03: Ngân Hàng Đề 2 Luồng Độc Lập & Rubric Studio 10.0 (R8)**<br>• Phân tách 2 luồng tạo câu hỏi: Luồng luyện tập/thi thử tạo độc lập vào `practice_questions`; luồng thi thật tạo bám sát đề thi mẫu / ma trận cấu trúc (`exam_structures`, `slot_definitions`) của Trưởng BM vào `exam_questions`<br>• Hỗ trợ đính kèm ảnh/sơ đồ kỹ thuật (`PNG/JPG/WEBP <= 5MB`) chấm Multimodal AI<br>• Rubric Criteria Editor: Tính tổng điểm $\sum \equiv 10.0$đ<br>• Question Approval: Trưởng BM duyệt đề (`APPROVED`, `NEEDS_REVISION`, `REJECTED`)<br><br>**3. Module MF-04 (Nhánh Quản Trị): Khảo Thí & Trưởng Bộ Môn (R3)**<br>• Trưởng Bộ Môn (`department_head`): Nắm **100% thẩm quyền cấu hình Follow-up thi thật** (`1–5` câu, mặc định `2`) và ma trận cấu trúc đề<br>• Cán bộ Khảo thí (`examination_officer`): Tạo kỳ thi (`OfficialExamSession`), tạo ca thi (`RealExamSessionShift`), phân bổ đề không trùng (`uq_tickets_shift_exam_set`), mở xuất bản đề (`is_exam_published = true`) và bấm "Công Bố Điểm" One-Way Lock<br><br>**4. Module MF-04 (Nhánh Phúc Khảo): Quy Trình Phúc Khảo 5 Bước Qua Khảo Thí (R7)**<br>• Bước 1: SV nộp đơn (`AppealRequestModal.tsx`) $\to$ Bước 2: Cán bộ Khảo thí thẩm tra & chuyển giao Trưởng BM (`ExamOfficerAppealsPage.tsx`) $\to$ Bước 3: Trưởng BM phân công GV chấm lại (`AppealManagementPage.tsx`) $\to$ Bước 4: GV chấm lại độc lập $\to$ Bước 5: Trưởng BM phê duyệt quyết định cuối cùng (`APPROVED`/`REJECTED`) | **1. Chuỗi Học Thuật 4 Cấp (R5) & Phân hệ Admin (FE-08, FE-09):**<br>• Chuỗi học thuật 4 cấp: Admin tạo Học kỳ/Môn học & gán Trưởng BM $\to$ Trưởng BM phân công nhiều GV vào môn (`course_lecturers`) $\to$ GV tạo Lớp học & sinh/reset mật khẩu lớp $\to$ SV nhập mật khẩu tự tham gia lớp<br>• `UserManagementPage.tsx` (FE-09: Quản lý user **6 Roles RBAC**, khóa/mở tài khoản)<br>• `SemesterManagementPage.tsx` (FE-08: CRUD học kỳ, tạo và sửa, không xóa)<br>• `AdminConfigPage.tsx` (Cấu hình `system_configs` MF-01 & quota `max_mock_exams_per_day` MF-02)<br>• `AuditLogsPage.tsx` (Xem nhật ký kiểm toán hệ thống cho Admin)<br><br>**2. Phân Định Giám Sát & Replay DLQ (R2):**<br>• `AdminDlqMonitorPage.tsx`: Admin giám sát & replay DLQ luồng MF-01/02 (`/api/v1/admin/dlq/*`)<br>• `ExamOfficerDlqPage.tsx`: Cán bộ Khảo thí (`examination_officer`) giám sát & replay DLQ luồng thi thật MF-04 (`/api/v1/examination-officer/dlq/*`)<br>• **Cắt bỏ hoàn toàn Trưởng Bộ Môn khỏi quản lý DLQ**<br><br>**3. Lịch sử Luyện tập & Thi thử (FE-02) & Báo cáo Khảo thí:**<br>• `ExamHistoryPage.tsx` (Tab Thi Thử & Tab Luyện Tập)<br>• Xuất báo cáo Khảo thí Excel (`.xlsx`) & PDF chữ ký số | Thiết kế CSDL 38 bảng 3NF (7 Bounded Contexts), State Management phức tạp, Form Studio Rubric 10.0đ, Chuỗi học thuật 4 cấp, Quản trị Khảo thí & Phúc khảo 5 bước. |

---

# 📁 PHẦN 2: CẤU TRÚC THƯ MỤC CHUẨN MỰC (`frontend/src/`)

```text
frontend/src/
├── assets/                          # SVG, Icons, Brand assets
├── routes/                          # Điều hướng React Router DOM v7
│   ├── AppRoutes.tsx                # Khai báo tuyến đường 6 Roles RBAC & Kiosk
│   ├── ProtectedRoute.tsx           # Route Guard kiểm tra Token & 6 Roles (student, lecturer, department_head, examination_officer, proctor, admin)
│   └── RoleBasedRedirect.tsx        # Điều hướng người dùng về đúng trang chủ sau login theo 6 Roles
├── types/                           # TypeScript Interfaces khớp 100% Backend DTOs (38 bảng 3NF)
│   ├── auth.types.ts                # User, Role (6 roles), GoogleLoginRequest, PasswordLoginRequest, ChangePasswordRequest, TokenPair, ProblemDetails
│   ├── academic.types.ts            # CourseDto, CourseLecturerDto, ClassDto, JoinClassRequest, InClassShiftDto, ActiveTotpDto (R5 & R6)
│   ├── practice.types.ts            # Session, NextQuestion, Answer, Scorecard, FollowUp, Progressive
│   ├── mockExam.types.ts            # MockQuota, MockSession, MockAnswer, CloScorecard
│   ├── rubric.types.ts              # Rubric, RubricCriterion (sum=10.0), QuestionDraft, ExamStructureMatrix, ReviewDecision (R8)
│   ├── officialExam.types.ts        # Season, Shift, Ticket, Submission, ExamInputMode, ProctorAttendanceDto, AntiCheatViolationDto (R3 & R4)
│   ├── appeal.types.ts              # AppealRequest, ForwardAppealRequest, AssignLecturerRequest, RegradeAppealRequest, AppealDecisionRequest (R7 5-step)
│   ├── dlq.types.ts                 # DlqTaskDto, ReplayDlqResponse (R2: Admin MF-01/02 & Exam Officer MF-04)
│   ├── user.types.ts                # UserDto, UpdateUserRoleRequest, UserStatus (FE-09 - 6 Roles)
│   ├── semester.types.ts            # SemesterDto, CreateSemesterRequest, UpdateSemesterRequest (FE-08)
│   ├── systemConfig.types.ts        # SystemConfigDto, UpdateSystemConfigRequest
│   └── notification.types.ts        # NotificationDto, NotificationType (FE-11)
├── services/                        # Axios API Client & SignalR Services
│   ├── api.client.ts                # Axios Instance (Base URL: /api/v1, Auto Bearer, Token Refresh Interceptor, RFC 7807 Toast)
│   ├── auth.service.ts              # /api/v1/auth (google-login, password-login, change-password, refresh-token, sign-out)
│   ├── academic.service.ts          # /api/v1/admin/courses, /api/v1/department-head/courses/{id}/lecturers, /api/v1/lecturer/classes, /api/v1/student/classes/join (R5)
│   ├── inClassShift.service.ts      # /api/v1/lecturer/in-class-shifts, /api/v1/student/in-class-shifts (R6 - TOTP 5 phút & chấm điểm thành phần)
│   ├── practice.service.ts          # /api/v1/practice (sessions, next-question, answers, complete, history)
│   ├── mockExam.service.ts          # /api/v1/mock-exams (sessions, answers, quota)
│   ├── question.service.ts          # /api/v1/questions (practice/exam streams, upload-image, ai-generate, batch-submit-review, decisions - R8)
│   ├── officialExam.service.ts      # /api/v1/official-exams (seasons, shifts, tickets, publish, publish-grades, evidence, reports - R3)
│   ├── proctor.service.ts           # /api/v1/proctor/shifts/{id}/attendance, /api/v1/proctor/shifts/{id}/violations (R4)
│   ├── appeal.service.ts            # /api/v1/student/appeals, /api/v1/examination-officer/appeals, /api/v1/department-head/appeals, /api/v1/lecturer/appeals (R7)
│   ├── dlq.service.ts               # /api/v1/admin/dlq/* (MF-01/02) & /api/v1/examination-officer/dlq/* (MF-04) (R2)
│   ├── user.service.ts              # /api/v1/admin/users (FE-09 - 6 Roles)
│   ├── semester.service.ts          # /api/v1/semesters (FE-08)
│   ├── systemConfig.service.ts      # /api/v1/admin/system-configs
│   ├── notification.service.ts      # /api/v1/notifications (FE-11)
│   └── signalr.service.ts           # SignalR Hub Connections (/hubs/practice & /hubs/anti-cheat)
├── stores/                          # Zustand State Stores
│   ├── useAuthStore.ts              # Token, Refresh Token, User Profile, Active Role (6 Roles RBAC)
│   ├── usePracticeStore.ts          # Current Session, Question, Answer, Scorecard, Buffer State
│   ├── useKioskStore.ts             # Kiosk IP (ip_address), SeatNumber, Focus blur count, Mic dB level, Audio stream
│   ├── useExamTimerStore.ts         # Master Timer đếm ngược đồng bộ server
│   └── useNotificationStore.ts      # Unread count, Notification list
├── hooks/                           # Custom React Hooks
│   ├── useSpeechRecognition.ts      # Web Speech API STT bóc băng tiếng Việt realtime (< 500ms) + TTS đọc đề
│   ├── useMicrophoneLevel.ts        # Web Audio API đo âm lượng mic (ngưỡng >= 60dB)
│   ├── useKioskLockdown.ts          # Fullscreen, chặn Alt+Tab, F11, F12, bắt blur >= 3 lần
│   └── usePracticeHub.ts            # SignalR Client hook lắng nghe ReceiveGradingResult, ReceiveGradingError
├── components/                      # Reusable UI Primitives
│   ├── common/                      # Button, Input, Card, Badge, Modal, Toast, Spinner, separator
│   ├── layout/                      # Navbar, Sidebar, Footer, StudentHeader, StudentSidebar, AppLayout (theo 6 Roles)
│   ├── practice/                    # BufferScreen (60s), ScorecardModal, FollowUpQuestionCard, ProgressiveSelector
│   ├── exam/                        # VoiceFirstGate, CountdownTimer, QuotaExceededModal, TotpRotatingProjector (R6)
│   ├── rubric/                      # RubricCriteriaEditor (10.0đ real-time), QuestionStreamSelector (R8), ScoreOverrideForm
│   ├── audio/                       # WaveformPlayer (Wavesurfer.js highlight transcript)
│   ├── kiosk/                       # KioskLockdownGuard, KioskHardwareMeter, KioskViolationNotice
│   ├── appeals/                     # AppealRequestModal, ForwardAppealModal, AssignLecturerModal, RegradeAppealForm, AppealDecisionModal (R7)
│   └── notifications/               # NotificationDrawer, NotificationItem
├── pages/                           # Màn hình chính phân theo 6 Roles RBAC & Kiosk
│   ├── auth/                        # LoginPage.tsx (Dual Auth), ChangePasswordPage.tsx, UnauthorizedPage.tsx
│   ├── student/                     # StudentDashboardPage, JoinClassModal, InClassExamJoinPage, PracticePage, PracticeSessionPage, MockExamPage, MockExamSessionPage, OfficialExamPage, ExamHistoryPage
│   ├── lecturer/                    # LecturerDashboardPage, ClassManagementPage (R5), InClassShiftManagementPage (R6), InClassShiftProjectorPage (R6), QuestionStudioPage (R8), CreateQuestionPage (R8), AuditEvidencePage, LecturerAppealRegradePage (R7)
│   ├── department_head/             # DepartmentHeadDashboardPage, CourseLecturerAssignmentPage (R5), ExamStructureMatrixPage (R3/R8), QuestionApprovalPage, AppealManagementPage (R7)
│   ├── examination_officer/         # ExamOfficerDashboardPage, ExamSeasonConfigPage (R3), ExamShiftManagementPage (R3), ExamOfficerAppealsPage (R7), ExamOfficerDlqPage (R2)
│   ├── proctor/                     # ProctorRoomMonitorPage (R4: Chỉ điểm danh STT 1-40 & báo cáo vi phạm an ninh)
│   ├── admin/                       # UserManagementPage (6 Roles), SemesterManagementPage, CourseManagementPage (R5), AdminConfigPage, AdminDlqMonitorPage (R2), AuditLogsPage
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
   - **Inactivity Timeout 5 phút (`SessionInactivityTimeoutMinutes = 5`):** Nếu sinh viên không tương tác quá 5 phút, hệ thống tự động khóa phiên, bắt lỗi **HTTP 410 Gone**, hiển thị thông báo phiên đã kết thúc và bảo toàn điểm các câu đã làm.
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
   - Số câu hỏi phụ tối đa: Đọc từ cấu hình `MaxPracticeFollowUpQuestions` do Admin cấu hình (1–5 câu, mặc định 2 câu; Giảng viên không được cấu hình).
4. **Component `ScorecardModal.tsx` (Bảng Điểm Rubric Chi Tiết):**
   - Hiển thị điểm số tổng kết thang điểm 10.0 (Xanh lá $\ge 8.0$, Vàng $5.0–7.9$, Đỏ $< 5.0$).
   - Accordion chi tiết từng tiêu chí Rubric: Điểm đạt / Điểm tối đa, phân tích điểm mạnh, điểm thiếu sót.
   - Nhận xét sư phạm của AI kèm nút loa TTS đọc to nhận xét.
5. **Hook `usePracticeHub.ts` (SignalR WebSocket Client):**
   - Kết nối endpoint `/hubs/practice`. Tự động `JoinSession(sessionId)`.
   - Lắng nghe sự kiện `ReceiveGradingResult` để cập nhật điểm và `ReceiveGradingError` để bắt lỗi khi AI quá tải.

---

### 🌟 KHỐI TRỌNG TÂM 2: MODULE MF-04 (NHÁNH A) — AN NINH KIOSK PHÒNG THI & GIÁM THỊ (R4)
1. **Trang `KioskCheckInPage.tsx` (`/kiosk/check-in` — FE-K01):**
   - Giao diện fullscreen Kiosk phòng Lab. Tự động phát hiện IP máy trạm (`ip_address`).
   - Nhập Mã sinh viên (MSSV) $\to$ Hệ thống đối chiếu số máy Kiosk (`SeatNumber` 1–40) trùng khớp với Số thứ tự trong danh sách ca thi phòng Lab.
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
   - **Phương thức làm bài (`ExamInputMode`):** Hiển thị theo cấu hình ca thi: `VoiceOnly` hoặc `VoiceWithTranscriptEdit`.
   - **Niêm phong âm thanh Cloudflare R2:** Stream audio trực tiếp lên Cloudflare R2 với tên file bất biến **`STT_MSSV.webm`** (ví dụ `01_SE170123.webm`), tính mã băm cryptographic SHA-256 ngay tại Kiosk để niêm phong bài thi.
4. **Trang `KioskSubmittedPage.tsx` (`/kiosk/submitted` — FE-K05):**
   - Màn hình biên nhận niêm phong an toàn sau khi sinh viên bấm nộp bài.
   - Nộp bài Persist First trong $< 100$ms (`SUBMITTED`) kèm mã băm SHA-256 của file audio R2.
   - Thông báo rõ ràng: *"Bài thi đã được lưu trữ an toàn. Kết quả sẽ do Giảng viên thẩm định và công bố trên hệ thống."*
   - **Quy tắc bất biến:** **Tuyệt đối không có điểm liền tại Kiosk và không cho khiếu nại tại chỗ**. Sinh viên ký biên bản nộp bài giấy với giám thị rồi ra về.
5. **Trang `ProctorRoomMonitorPage.tsx` (`/proctor/monitor` — FE-P01 — Thu Thuần Quyền Giám Thị R4):**
   - Màn hình dành riêng cho Giám thị phòng thi (`proctor`).
   - Hiển thị **Ma trận 40 máy lab** (Lưới 40 ô trực quan tương ứng `SeatNumber` 1–40).
   - **Chức năng điểm danh STT 1–40 (`POST /api/v1/proctor/shifts/{id}/attendance`):** Đối chiếu thẻ SV và CCCD thực tế, tick xác nhận sinh viên có mặt tại đúng ghế ngồi trên hệ thống.
   - **Báo cáo vi phạm an ninh phòng thi (`POST /api/v1/proctor/shifts/{id}/violations`):** Ghi nhận hành vi gian lận/vi phạm quy chế trực tiếp vào bảng `exam_anti_cheat_logs` và nhận cảnh báo realtime qua `/hubs/anti-cheat`.
   - **Giới hạn quyền nghiêm ngặt (R4):** Cắt bỏ hoàn toàn các nút Mở/Đóng ca thi, Mở khóa/Xuất bản đề, Gỡ niêm phong IP/Reset máy trạm, Import danh sách thí sinh hay xem/sửa điểm AI khỏi giao diện Giám thị (việc mở xuất bản đề thuộc Cán bộ Khảo thí `examination_officer`).

---

### 🌟 KHỐI TRỌNG TÂM 3: MODULE MF-04 (NHÁNH B) & MF-05 — CỔNG HẬU KIỂM GIẢNG VIÊN & CA THI TRONG LỚP (R6)
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
4. **Phân hệ Ca Thi Trong Lớp & Mật Khẩu Tự Động Xoay Vòng 5 Phút (`InClassShiftManagementPage.tsx` & `InClassShiftProjectorPage.tsx` — MF-05 / R6):**
   - **Giảng viên tạo Ca thi trong lớp (`POST /api/v1/lecturer/in-class-shifts`):** Chọn lớp học phần (`classes`) được phân công để tổ chức kiểm tra vấn đáp lấy điểm thành phần (Quiz / Progress Test).
   - **Màn hình Chiếu Mật Khẩu Xoay Vòng 5 Phút (`GET /api/v1/lecturer/in-class-shifts/{id}/active-password`):** Hiển thị mã OTP 6 ký tự cỡ lớn chiếu lên máy chiếu lớp học kèm đồng hồ đếm ngược 300 giây (5 phút) tự động làm mới mã mới (hỗ trợ grace period 30–60s cho mã liền kề trước đó).
   - **Sinh viên vào Ca thi trong lớp (`InClassExamJoinPage.tsx` — `POST /api/v1/student/in-class-shifts/{id}/join`):** Sinh viên hiện diện tại lớp nhập mã OTP 6 ký tự hợp lệ để vào làm bài vấn đáp (không lưu audio R2, chỉ lưu transcript đẩy vào `BoundedChannel` chấm tự động).
   - **Hậu kiểm & Chốt bảng điểm thành phần:** Giảng viên xem danh sách bài làm (`GET /api/v1/lecturer/in-class-shifts/{id}/submissions`), điều chỉnh điểm thành phần kèm lý do (`PUT .../submissions/{subId}/grade`), và bấm **"Chốt Bảng Điểm Thành Phần"** (`POST .../finalize-grades`) kích hoạt One-Way Lock.

---

### 🛠️ CÁC TASK VỆ TINH ("RÂU RIA") CỦA HOÀNG
1. **Khắc phục lỗi Build & TypeScript:**
   - Sửa file `src/lib/utils.ts` và `src/components/common/separator.tsx`: Chuyển `from 'cn'` thành `from '@/utils/cn'`.
   - Cài đặt đầy đủ các package thiếu trong `package.json`: `three`, `@base-ui/react`, `qrcode`.
   - Gán kiểu tường minh (type casting) cho các tham số `object: any`, `material: any`, `dataUrl: string` trong các ThreeScene để lệnh `npm run build` vượt qua 100% (Exit Code 0).
2. **Xác thực Kép (Dual Auth) & Route Guards 6 Roles RBAC (FE-Auth):**
   - Hoàn thiện `LoginPage.tsx` và `ChangePasswordPage.tsx`: Tích hợp Google Identity Services (`POST /api/v1/auth/google-login`) song song với form đăng nhập tài khoản Nhà trường cấp (`POST /api/v1/auth/password-login`) và đổi mật khẩu (`POST /api/v1/auth/change-password`). Cấm mở form tự đăng ký công khai.
   - Xử lý lưu trữ TokenPair (`accessToken`, `refreshToken`) vào `useAuthStore.ts`.
   - Triển khai Silent Refresh Token Interceptor trong `api.client.ts`: Tự động gọi `POST /api/v1/auth/refresh-token` trước khi access token hết hạn (Token Rotation).
   - Hoàn thiện Route Guard `ProtectedRoute.tsx` và `RoleBasedRedirect.tsx` phân quyền đúng **6 Roles RBAC** (`student`, `lecturer`, `department_head`, `examination_officer`, `proctor`, `admin`).
3. **Dashboard Sinh viên & Giảng viên (FE-10):**
   - `StudentDashboardPage.tsx`: Lời chào, thông tin SV, danh sách lớp học phần đã tham gia (nút nhập mật khẩu tham gia lớp mới), widget hạn ngạch thi thử hôm nay (`X/K`), banner lịch thi phòng lab & ca thi trong lớp sắp tới.
   - `LecturerDashboardPage.tsx`: Thống kê lớp học phần phụ trách, ca thi trong lớp đang mở, số bài thi cần hậu kiểm, số câu hỏi đang soạn thảo và số đơn phúc khảo được giao chấm lại.
4. **Hộp Thư Thông Báo In-App (FE-11):**
   - Xây dựng `NotificationDrawer.tsx` và `NotificationPage.tsx`.
   - Hiển thị danh sách thông báo: Lịch thi được công bố, điểm thi chính thức đã công bố, đơn phúc khảo được chuyển giao/giao chấm lại, câu hỏi chờ duyệt/bị trả về. Nút "Đánh dấu đã đọc". (Không gửi email).

---

# 🧑‍💻 PHẦN 4: NHIỆM VỤ CHI TIẾT CỦA PHẠM NGUYỄN ĐĂNG HẢI (DB & FE SPECIALIST)

### 🌟 KHỐI TRỌNG TÂM 1: MODULE MF-02 — THI THỬ VẤN ĐÁP TÍNH GIỜ CÓ HẠN NGẠCH
1. **Trang `MockExamPage.tsx` & `MockExamSessionPage.tsx` (`/student/mock-exam`):**
   - **Modal Khởi Động Thi Thử:**
     * Sinh viên chọn môn học cần thi thử.
     * Tùy chọn Follow-up chủ động: Sinh viên tick chọn **"Có Follow-up"** hoặc **"Không Follow-up"**. Ca thi có follow-up sẽ có thời lượng làm bài dài hơn do Trưởng BM cấu hình.
     * Sinh viên **không chọn topic, không chọn độ khó**. Đề thi bốc ngẫu nhiên từ kho `practice_questions` theo ma trận cấu trúc đề và tỷ lệ Bloom do Trưởng BM cấu hình.
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
   - **Cơ chế Auto-Submit:** Khi hết giờ (00:00), hệ thống tự động khóa đề và nộp bài. **Các câu chưa làm tính là bỏ trống, không chấm**.
   - **Lưu ý:** Thi thử **không lưu audio R2**, chỉ lưu bản transcript vào `mock_exam_answers`.
4. **Modal Kết Quả `MockExamScorecardModal.tsx` (Instant Feedback Theo CLO):**
   - Bung lên tức thì sau khi hoàn thành bài thi thử.
   - Hiển thị bảng điểm tổng kết thang điểm 10.0 và đánh giá chi tiết theo từng Chuẩn Đầu Ra (`course_clos` 3NF) của môn học: CLO1, CLO2, CLO3...
   - Nhận xét sư phạm từng tiêu chí giúp sinh viên nhận diện lỗ hổng kiến thức trước kỳ thi thật.

---

### 🌟 KHỐI TRỌNG TÂM 2: MODULE MF-03 — NGÂN HÀNG CÂU HỎI 2 LUỒNG ĐỘC LẬP & BAREM RUBRIC STUDIO 10.0 (R8)
1. **Trang `QuestionStudioPage.tsx` (`/lecturer/questions`):**
   - Danh sách câu hỏi môn học với 2 Tab phân tách rõ ràng theo 2 luồng nghiệp vụ độc lập (R8):
     * **Luồng 1 — Kho Luyện Tập & Thi Thử (`practice_questions`):** Tạo độc lập từ Chuẩn đầu ra môn học (`course_clos` 3NF) hoặc soạn tay phục vụ sinh viên tự do ôn luyện, không đòi hỏi bám ma trận cấu trúc đề thi phòng Lab.
     * **Luồng 2 — Kho Thi Thật (`exam_questions`):** Bắt buộc tạo bám sát **Đề thi mẫu / Ma trận Cấu trúc Đề** (`exam_structures`, `slot_definitions`, tỷ lệ Bloom) do Trưởng Bộ Môn ban hành.
   - Bộ lọc theo Môn học, CLO, Mức Bloom, Trạng thái (`DRAFT`, `SUBMITTED_FOR_REVIEW`, `APPROVED`, `NEEDS_REVISION`, `REJECTED`).
   - Nút **"Gửi Duyệt Bộ Môn"** (`POST /api/v1/questions/batch-submit-review`): Chọn các câu hỏi `DRAFT` và gửi lên Trưởng Bộ Môn thẩm định.
2. **Trang `CreateQuestionPage.tsx` & Component `RubricCriteriaEditor.tsx`:**
   - **Chọn Luồng Tạo Câu Hỏi (`QuestionStreamSelector.tsx` — R8):**
     * Chọn tạo cho `practice_questions` (chọn `course_id` + `clo_id`) hoặc tạo cho `exam_questions` (bắt buộc chọn `exam_structure_id` + `slot_definition_id` từ ma trận đề của Trưởng BM).
   - **Tính năng AI Sinh Câu Hỏi Theo CLO 3NF & Soạn Tay Đính Kèm Ảnh Multimodal:**
     * Nút **"AI Sinh Câu Hỏi Theo CLO"** (`POST /api/v1/questions/ai-generate`): Sinh câu hỏi, rubric 10.0đ và câu trả lời mẫu bám sát `course_clos` 3NF (và `slot_definition_id` nếu thuộc luồng thi thật).
     * Khi Giảng viên soạn tay (`source == "manual"`): Hỗ trợ đính kèm ảnh/sơ đồ kỹ thuật (`PNG/JPG/WEBP <= 5MB`) qua `POST /api/v1/questions/upload-image` kèm trường mô tả `imageDescription` để phục vụ Gemini Multimodal CoT Grading.
   - **Rubric Studio 10.0 Chuẩn Hóa:**
     * Form thêm/bớt các tiêu chí rubric con.
     * **Client Guard Tổng Điểm Real-Time:** Tính tổng điểm $\sum$ các tiêu chí con. Nếu $\sum \neq 10.0$đ, hiển thị cảnh báo đỏ và khóa cứng nút Lưu (`HTTP 422`); nếu $\sum \equiv 10.0$đ, hiển thị nhãn xanh hợp lệ.
     * Ràng buộc Model Answer bắt buộc $\ge 50$ ký tự.
3. **Trang `ExamStructureMatrixPage.tsx` & `QuestionApprovalPage.tsx` (`/department-head/*`):**
   - **Ban hành Ma trận Cấu trúc Đề & Cấu hình Follow-up Thi thật (R3 & R8):** Trưởng Bộ Môn tạo/cập nhật `exam_structures` và `slot_definitions`, đồng thời nắm **100% thẩm quyền cấu hình Follow-up cho đề thi/môn thi thật** (`has_follow_up`, số câu hỏi phụ `1–5` câu, mặc định `2` câu).
   - **Thẩm định & Duyệt câu hỏi (`QuestionApprovalPage.tsx`):**
     * Hiển thị chi tiết câu hỏi, CLO 3NF, Slot ma trận đề (nếu là `exam_questions`), Barem Rubric 10.0đ và Câu trả lời mẫu.
     * **3 Quyết định chính thức:** `APPROVED` (vào ngân hàng đề), `NEEDS_REVISION` (bắt buộc nhập góp ý trả về GV sửa), `REJECTED` (loại bỏ).
     * Trưởng Bộ Môn duyệt bộ đề thi chính thức (`OFFICIAL` $\to$ bàn giao `HANDED_OVER` sang cho Cán bộ Khảo thí).

---

### 🌟 KHỐI TRỌNG TÂM 3: MODULE MF-04 (NHÁNH QUẢN TRỊ KHẢO THÍ & PHÚC KHẢO 5 BƯỚC — R3 & R7)
1. **Trang `ExamSeasonConfigPage.tsx` (`/examination-officer/exam-seasons` — R3):**
   - Dành cho **Cán bộ Khảo thí (`examination_officer`)** khởi tạo Kỳ thi chính thức (`OfficialExamSession`).
   - Đưa danh sách các Môn thi vào kỳ thi, cấu hình `ExamInputMode` (`VoiceOnly` hoặc `VoiceWithTranscriptEdit`) và `TranscriptBufferSeconds` (10–300s, mặc định 60s).
   - **Lưu ý phân quyền R3:** Cán bộ Khảo thí **không** cấu hình Follow-up thi thật; hệ thống tự động kế thừa cấu hình Follow-up (`1–5` câu, mặc định `2`) từ ma trận đề thi do Trưởng Bộ Môn thiết lập.
2. **Trang `ExamShiftManagementPage.tsx` (`/examination-officer/shifts` — R3):**
   - Dành cho **Cán bộ Khảo thí (`examination_officer`)** quản lý các Ca thi phòng Lab (`RealExamSessionShift`).
   - Ghi phòng lab trực tiếp trên ca thi (ví dụ: Lab 302, Lab 405), cấu hình kíp thi, thời gian bắt đầu/kết thúc.
   - Phân công Giám thị (`proctor_user_id`) và Giảng viên chấm thi (`examiner_user_id`) cho ca thi.
   - Import danh sách sinh viên dự thi, tự động phân bổ đề thi không trùng lặp (`uq_tickets_shift_exam_set`) từ kho đề `HANDED_OVER`.
   - Nút **"Mở Xuất Bản Đề Thi Ca Thi"** (`POST /api/v1/official-exams/shifts/{id}/publish` $\to$ `is_exam_published = true`) và nút **"Công Bố Điểm Ca Thi"** (`POST /api/v1/official-exams/shifts/{shiftId}/publish-grades` $\to$ kích hoạt One-Way Lock `is_locked = true`).
3. **Quy Trình Phúc Khảo Nội Bộ 5 Bước Qua Cán Bộ Khảo Thí (R7):**
   - **Bước 1 — Sinh viên nộp đơn (`AppealRequestModal.tsx`):** Sau khi công bố điểm, sinh viên xem điểm trên Student Portal; nếu đồng ý bấm xác nhận (`POST /api/v1/student/official-exams/{ticketId}/acknowledge-grade`), nếu khiếu nại bấm **"Nộp Đơn Phúc Khảo"** (`POST /api/v1/student/appeals` $\to$ trạng thái `SUBMITTED_TO_EXAM_OFFICE`).
   - **Bước 2 — Cán bộ Khảo thí tiếp nhận & thẩm tra (`ExamOfficerAppealsPage.tsx`):** Cán bộ Khảo thí kiểm tra tính hợp lệ (thời hạn, trạng thái công bố), nhập ghi chú thẩm tra và bấm **"Chuyển Giao Trưởng Bộ Môn"** (`POST /api/v1/examination-officer/appeals/{id}/forward-to-head` $\to$ `FORWARDED_TO_DEPT_HEAD`).
   - **Bước 3 — Trưởng Bộ Môn phân công Giảng viên chấm lại (`AppealManagementPage.tsx`):** Trưởng BM xem đơn do Khảo thí chuyển sang, mở `AssignLecturerModal.tsx` chọn Giảng viên chấm phúc khảo độc lập (`POST /api/v1/department-head/appeals/{id}/assign-lecturer` $\to$ `ASSIGNED_TO_LECTURER`).
   - **Bước 4 — Giảng viên được phân công chấm lại độc lập (`LecturerAppealRegradePage.tsx`):** Giảng viên nghe lại Audio R2 trên Evidence Panel, nhập biên bản giải trình chấm lại và điểm đề xuất (`PUT /api/v1/lecturer/appeals/{id}/regrade` $\to$ `REGRADED_BY_LECTURER`).
   - **Bước 5 — Trưởng Bộ Môn phê duyệt quyết định cuối cùng (`AppealDecisionModal.tsx`):** Trưởng BM thẩm định biên bản chấm lại và ra quyết định (`POST /api/v1/department-head/appeals/{id}/decision` $\to$ `APPROVED` hoặc `REJECTED`); nếu `APPROVED`, hệ thống mở ngoại lệ One-Way Lock cập nhật điểm chính thức mới.

---

### 🛠️ CÁC TASK VỆ TINH ("RÂU RIA") CỦA HẢI
1. **Chuỗi Học Thuật 4 Cấp (R5) & Phân hệ Quản trị Admin (FE-08 & FE-09):**
   - **Chuỗi Học Thuật 4 Cấp (`CourseManagementPage.tsx`, `CourseLecturerAssignmentPage.tsx`, `ClassManagementPage.tsx`, `JoinClassModal.tsx` — R5):**
     * Cấp 1 (Admin): Tạo Học kỳ (`SemesterManagementPage.tsx`) $\to$ Tạo Môn học & gán Trưởng Bộ Môn (`PUT /api/v1/admin/courses/{id}/assign-department-head`).
     * Cấp 2 (Trưởng Bộ Môn): Phân công nhiều Giảng viên cùng quản lý một môn học trên bảng `course_lecturers` (`POST /api/v1/department-head/courses/{id}/lecturers`).
     * Cấp 3 (Giảng viên): Tạo Lớp học phần (`classes`) thuộc môn được giao và sinh/reset mật khẩu tham gia lớp (`POST /api/v1/lecturer/classes`, `POST /api/v1/lecturer/classes/{id}/reset-join-password`).
     * Cấp 4 (Sinh viên): Nhập `joinCode` + `joinPassword` để tự ghi danh vào lớp học phần (`POST /api/v1/student/classes/join`).
   - `UserManagementPage.tsx` (FE-09): Xem danh sách người dùng, khóa/mở tài khoản, gán vai trò chuẩn **6 Roles RBAC** (`student`, `lecturer`, `department_head`, `examination_officer`, `proctor`, `admin`).
   - `SemesterManagementPage.tsx` (FE-08): Quản lý học kỳ (Xem, Tạo mới, Sửa; **Cấm xóa học kỳ**).
   - `AdminConfigPage.tsx`: Cấu hình `system_configs` MF-01 (Follow-up 1–5 mặc định 2; Progressive 3–10; Buffer 10–300s mặc định 60s; Timeout `SessionInactivityTimeoutMinutes = 5` phút) và hạn ngạch thi thử theo môn (`max_mock_exams_per_day`).
   - `AuditLogsPage.tsx`: Xem nhật ký kiểm toán hệ thống (`GET /api/v1/admin/audit-logs`) dành cho Admin.
2. **Phân Định Rạch Ròi Giám Sát & Replay Dead-Letter Queue (R2):**
   - `AdminDlqMonitorPage.tsx` (`/admin/dlq`): Dành riêng cho **Admin (`admin`)** giám sát và kích hoạt Replay DLQ của luồng **Luyện tập (MF-01) và Thi thử (MF-02)** qua `/api/v1/admin/dlq/*`.
   - `ExamOfficerDlqPage.tsx` (`/examination-officer/dlq`): Dành riêng cho **Cán bộ Khảo thí (`examination_officer`)** giám sát và kích hoạt Replay DLQ của luồng **Thi thật phòng Lab (MF-04)** qua `/api/v1/examination-officer/dlq/*`.
   - **Quy tắc R2:** Cắt bỏ hoàn toàn quyền xem và replay DLQ khỏi giao diện Trưởng Bộ Môn (`department_head`).
3. **Lịch sử Thi Thử & Luyện Tập (FE-02) & Xuất Báo Cáo Khảo Thí:**
   - Hoàn thiện `ExamHistoryPage.tsx` với 2 Tab mượt mà (Tab 1: Lịch sử Luyện Tập MF-01; Tab 2: Lịch sử Thi Thử MF-02) kèm Drawer xem chi tiết transcript & rubric.
   - Tại màn hình Quản lý Ca thi của Cán bộ Khảo thí, tích hợp 2 nút xuất báo cáo: **"Xuất Báo Cáo Excel (.xlsx)"** và **"Xuất Báo Cáo PDF Ký Số"**.

---

# 🚀 PHẦN 5: CHECKLIST NGHIỆM THU TIÊU CHUẨN (ACCEPTANCE CRITERIA)

| Mã tiêu chí | Nội dung nghiệm thu kỹ thuật | Kỹ sư chịu trách nhiệm | Tiêu chuẩn đạt |
| :---: | :--- | :---: | :---: |
| **AC-01** | `npm run build` chạy thành công không có lỗi TypeScript | 🧑 Lê Vũ Hoàng | Exit Code = 0, 0 TS Error |
| **AC-02** | Xác thực Kép (Google OAuth 2.0 + Tài khoản trường cấp), Route Guards chuẩn **6 Roles RBAC** | 🧑 Lê Vũ Hoàng | Token lưu đúng Store, phân luồng chuẩn 6 Roles |
| **AC-03** | MF-01 chạy mượt cả Per-Question On-Demand và Full Progressive 3–10 câu, Timeout 5 phút (`410 Gone`) | 🧑 Lê Vũ Hoàng | SignalR nhận điểm, Anti-3-consecutive chuẩn |
| **AC-04** | Buffer Screen SVG đếm ngược đúng 60s, không lưu audio lên R2 | 🧑 Lê Vũ Hoàng | Auto-submit khi 0s, transcript lưu DB |
| **AC-05** | Kiosk Lockdown chặn phím tắt, blur $\ge 3$ đình chỉ; Giám thị (`proctor`) CHỈ điểm danh STT 1–40 & báo cáo vi phạm (R4) | 🧑 Lê Vũ Hoàng | 100% fullscreen, mic check $\ge 60$dB, Proctor đúng thẩm quyền |
| **AC-06** | Hậu kiểm GV có Wavesurfer.js tua transcript; Ca thi trong lớp chiếu mật khẩu TOTP xoay vòng 5 phút + grace period 60s (R6) | 🧑 Lê Vũ Hoàng | TOTP xoay vòng đúng 300s, chốt điểm thành phần One-Way Lock |
| **AC-07** | MF-02 Voice-First Gate khóa text, Timer đếm ngược to, bắt lỗi HTTP 429 Quota do Admin cấu hình | 🧑 Phạm Nguyễn Đăng Hải | Hiện modal cảnh báo đỏ khi quá hạn ngạch |
| **AC-08** | Ngân hàng đề phân tách 2 luồng (`practice_questions` vs `exam_questions` bám ma trận đề - R8), Rubric $\sum \equiv 10.0$đ, ảnh Multimodal | 🧑 Phạm Nguyễn Đăng Hải | Lệch 10.0đ khóa nút gửi, Model Answer $\ge 50$ ký tự |
| **AC-09** | Trưởng BM duyệt đề & cấu hình 100% Follow-up thi thật (1–5 câu, mặc định 2); Khảo thí tạo kỳ thi, ca thi, mở đề & công bố điểm (R3) | 🧑 Phạm Nguyễn Đăng Hải | Đề vào kho khi APPROVED, Khảo thí khóa 1 chiều `is_locked=true` |
| **AC-10** | Quy trình Phúc khảo nội bộ 5 bước: SV nộp $\to$ Khảo thí thẩm tra chuyển giao $\to$ Trưởng BM giao GV $\to$ GV chấm lại $\to$ Trưởng BM duyệt (R7) | 🧑 Phạm Nguyễn Đăng Hải | Đúng 5 bước trạng thái, mở ngoại lệ One-Way Lock khi APPROVED |
| **AC-11** | Chuỗi học thuật 4 cấp (`course_lecturers`, mật khẩu lớp - R5); Admin quản lý User **6 Roles RBAC** & DLQ MF-01/02; Khảo thí quản lý DLQ MF-04 (R2) | 🧑 Phạm Nguyễn Đăng Hải | Cắt bỏ Trưởng BM khỏi DLQ, gọi đúng API v1 trên 38 bảng 3NF |
| **AC-12** | Cán bộ Khảo thí xuất báo cáo ca thi ra cả file Excel (`.xlsx`) và file PDF có chữ ký số | 🧑 Phạm Nguyễn Đăng Hải | Tải đúng định dạng file từ backend |
