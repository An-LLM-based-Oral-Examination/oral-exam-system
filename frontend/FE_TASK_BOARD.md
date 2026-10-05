# 🖥️ FRONTEND FUNCTIONAL TASK BOARD & SCREEN SPECIFICATIONS (v6.0)
## ĐỒ ÁN TỐT NGHIỆP SEP490 (FA26SE166) — HỆ THỐNG LUYỆN THI & ĐÁNH GIÁ VẤN ĐÁP BẰNG LLM
### KHO MÃ NGUỒN: `05_Source_Code/frontend` | CỔNG DỊCH VỤ DEV: `3000` | BACKEND API: `5000`

---

> [!IMPORTANT]
> **THÔNG TIN DỰ ÁN & PHÂN CÔNG NHÂN SỰ FRONTEND:**
> - **Mã đề tài:** FA26SE166 | **Học kỳ:** Fall 2026 (FA26) — Đại học FPT TP.HCM (FPT SG)
> - **Tech Stack Frontend:** React 19 (`^19.2.8`), Vite (`^8.3.0`), Tailwind CSS v4, React Router DOM v7, TypeScript 5.8+, Zustand, TanStack React Query v5, `@microsoft/signalr`, Web Speech API (STT/TTS), Web Audio API (Mic dB), Wavesurfer.js (Waveform Audio Player).
> - **Phân công nhân sự Frontend:**
>   - 🧑 **Lê Vũ Hoàng (Lead Frontend Architect):** Kiến trúc React 19, Routing 5 phân vùng, Axios Interceptor RFC 7807, Kiosk Lockdown chống gian lận (Fullscreen, chặn phím tắt, blur $\ge 3$ đình chỉ), Web Audio API Mic-Check ($\ge 60$dB), Bóc băng Web Speech STT/TTS tiếng Việt, Màn hình đệm Buffer Screen đếm ngược, Waveform Audio Player Wavesurfer.js, Upload Cloudflare R2 `STT_MSSV.webm` kèm băm SHA-256, và SignalR Client `/hubs/practice`.
>   - 🧑 **Nguyễn Đăng Hải (Frontend Developer & DB Specialist):** Sau khi hoàn tất 28 bảng CSDL, Hải chuyển sang **dồn toàn lực phát triển Frontend**, đảm nhận: Student Portal Dashboard, Giao diện Luyện tập tự do MF-01, Màn hình Thi thử MF-02 Voice-First (khóa text), Modal chặn hạn ngạch Quota K=3 (HTTP 429), Rubric Criteria Editor (tính tổng real-time 10.0đ), Giao diện duyệt câu hỏi của Trưởng Bộ Môn (Approved / Needs Revision / Rejected), Cấu hình ca thi và Follow-up môn thi của Trưởng BM, Giao diện nộp & thẩm định Phúc khảo nội bộ `AppealRequest`.

---

## 📁 1. CẤU TRÚC THƯ MỤC CHUẨN MỰC (REACT 19 + TAILWIND CSS v4)

```text
frontend/src/
├── routes/                          # Tuyến đường React Router DOM v7
│   ├── AppRoutes.tsx                # Khai báo toàn bộ routes của hệ thống
│   ├── ProtectedRoute.tsx           # Route Guard kiểm tra Auth & Role (student, lecturer, department_head, proctor, admin)
│   └── RoleBasedRedirect.tsx        # Điều hướng người dùng về đúng Dashboard sau khi login
├── types/                           # TypeScript Interfaces khớp 100% Backend DTOs
│   ├── auth.types.ts
│   ├── practice.types.ts            # Session, Question, Answer, Scorecard, FollowUp
│   ├── mockExam.types.ts            # Quota, MockSession, MockAnswer
│   ├── officialExam.types.ts        # Season, Shift, Ticket, Submission, Audit
│   ├── rubric.types.ts              # Rubric, RubricCriterion, QuestionBank
│   └── appeal.types.ts              # AppealRequest, AppealDecision
├── services/                        # API Services & WebSocket Client
│   ├── api.client.ts                # Axios Instance (Base URL: http://localhost:5000/api/v1, RFC 7807 Toast Interceptor)
│   ├── auth.service.ts
│   ├── practice.service.ts
│   ├── mockExam.service.ts
│   ├── question.service.ts
│   ├── officialExam.service.ts
│   ├── appeal.service.ts
│   └── signalr.service.ts           # SignalR Hub Connection (/hubs/practice)
├── stores/                          # Zustand State Stores
│   ├── useAuthStore.ts              # Token, User profile, Active role
│   ├── usePracticeStore.ts          # Phiên luyện tập, danh sách câu, trạng thái nộp, scorecard
│   ├── useKioskStore.ts             # Trạng thái Kiosk, blur_count, mic_level, stream audio
│   └── useExamTimerStore.ts         # Đồng hồ đếm ngược đồng bộ Master Timer
├── hooks/                           # Custom Hooks nghiệp vụ
│   ├── useSpeechRecognition.ts      # Web Speech API bóc băng tiếng Việt thời gian thực (< 500ms)
│   ├── useAudioRecorder.ts          # MediaRecorder ghi âm .webm, băm SHA-256
│   ├── useMicrophoneLevel.ts        # Web Audio API đo dB mic (ngưỡng >= 60dB)
│   └── useKioskLockdown.ts          # Fullscreen, chặn Alt+Tab, F11, F12, bắt onblur
├── components/                      # Reusable UI Components
│   ├── common/                      # Button, Modal, Card, Badge, Toast, Spinner, Input
│   ├── layout/                      # Navbar, Sidebar, Footer, KioskHeader, ProctorHeader
│   ├── practice/                    # BufferScreen, FollowUpQuestionCard, ScorecardModal, ModeSelector
│   ├── rubric/                      # RubricCriteriaEditor (tính tổng 10.0đ real-time), QuestionPreviewCard
│   ├── audio/                       # WaveformPlayer (Wavesurfer.js highlight transcript)
│   ├── kiosk/                       # KioskMicCheck, KioskQuestionViewer, KioskLockdownGuard
│   └── appeals/                     # AppealRequestModal, AppealReviewCard
└── pages/                           # Các màn hình chính (24 Màn hình)
    ├── auth/                        # LoginPage.tsx
    ├── student/                     # StudentDashboardPage.tsx, PracticePage.tsx, MockExamPage.tsx, ExamHistoryPage.tsx, AppealPage.tsx
    ├── lecturer/                    # LecturerDashboardPage.tsx, QuestionStudioPage.tsx, AuditEvidencePage.tsx
    ├── department_head/             # DepartmentHeadDashboardPage.tsx, QuestionApprovalPage.tsx, ExamSeasonConfigPage.tsx, AppealManagementPage.tsx
    ├── proctor/                     # ProctorRoomMonitorPage.tsx
    ├── kiosk/                       # KioskCheckInPage.tsx, KioskHardwareMicCheckPage.tsx, KioskExamRoomPage.tsx, KioskSuspendedPage.tsx, KioskSubmittedPage.tsx
    └── admin/                       # AdminConfigPage.tsx, AdminDlqMonitorPage.tsx
```

---

## 🎨 2. CHI TIẾT TỪNG MÀN HÌNH & MÔ TẢ GIAO DIỆN (SCREEN SPECIFICATIONS & WIREFRAMES)

---

### PHÂN HỆ 1: DÀNH CHO SINH VIÊN (STUDENT PORTAL)

#### 🖥️ Màn hình FE-S01: `LoginPage.tsx` (Đăng Nhập Google FPT)
* **Người phụ trách:** 🧑 **Lê Vũ Hoàng**
* **Mô tả giao diện:**
  - Layout căn giữa hiện đại (Clean Glassmorphism), nền xám xanh FPT (`#F8FAFC`), logo Đại học FPT nổi bật góc trên.
  - Tiêu đề: *"Hệ Thống Luyện Thi & Đánh Giá Vấn Đáp Bằng LLM"*, phụ đề: *"FA26SE166 — Đại học FPT TP.HCM"*.
  - Nút bấm chính: **"Đăng nhập với tài khoản Google FPT (@fpt.edu.vn)"** có icon Google màu chuẩn.
  - Khi click nút: Mở popup Google OAuth PKCE; đăng nhập xong tự động lưu JWT vào `useAuthStore` và điều hướng về trang theo Role.

#### 🖥️ Màn hình FE-S02: `StudentDashboardPage.tsx` (Cổng Thông Tin Sinh Viên)
* **Người phụ trách:** 🧑 **Nguyễn Đăng Hải**
* **Mô tả giao diện:**
  - Header: Lời chào sinh viên, MSSV, avatar và nút Logout.
  - **Khu vực 1 — Thẻ Môn Học Đang Luyện Tập:**
    - Render danh sách thẻ môn học (PRN231, SWD392). Mỗi thẻ hiển thị: Tên môn, Số tín chỉ, Thời gian đệm cấu hình môn (`transcript_buffer_seconds`), Số câu hỏi trong ngân hàng.
    - Hai nút hành động trên mỗi thẻ môn:
      1. Nút xanh: **"Luyện Tập Tự Do (MF-01)"** $\to$ Điều hướng sang `PracticePage`.
      2. Nút tím: **"Thi Thử Bấm Giờ (MF-02)"** $\to$ Mở modal cài đặt thi thử.
  - **Khu vực 2 — Trạng Thái Hạn Ngạch Hôm Nay:**
    - Widget tròn hiển thị Quota thi thử: *"Đã dùng X/3 lượt thi thử hôm nay"* (màu xanh nếu $<3$, màu đỏ rực nếu $=3$).
  - **Khu vực 3 — Lịch Thi Thật Phòng Lab (Nếu có):**
    - Banner vàng thông báo: Ca thi phòng Lab sắp diễn ra (Phòng Lab 302, Ghế số 15, Giờ thi 08:00 15/10/2026).

#### 🖥️ Màn hình FE-S03: `PracticePage.tsx` (Phòng Luyện Tập Tương Tác MF-01)
* **Người phụ trách:** 🧑 **Nguyễn Đăng Hải & 🧑 Lê Vũ Hoàng**
* **Mô tả bố cục giao diện (Wireframe Layout):**
  ```text
  ┌────────────────────────────────────────────────────────────────────────┐
  │ [Logo FPT] SWD392 - Luyện Tập Tương Tác     [Chế độ: Per-Question] [X] │
  ├────────────────────────────────────────────────────────────────────────┤
  │ TIẾN ĐỘ: Câu 1 / 3 ━━━━━━━━━●───────────────  Thời gian đệm môn: 60s  │
  ├────────────────────────────────────────────────────────────────────────┤
  │ [THẺ CÂU HỎI]                                                          │
  │ "Trình bày sự khác biệt giữa Monolithic Architecture và Microservices?" │
  │ [Icon Loa - Bấm để nghe đọc TTS câu hỏi]   Cấp độ Bloom: [Analyze]     │
  │ Tiêu chí: Khái niệm (3đ) | Ưu nhược điểm (4đ) | Kịch bản áp dụng (3đ)   │
  ├────────────────────────────────────────────────────────────────────────┤
  │ PHƯƠNG THỨC LÀM BÀI:  (•) Nói qua Micro    ( ) Gõ phím                 │
  │ ┌────────────────────────────────────────────────────────────────────┐ │
  │ │  [ICON MICRO TO ĐANG NHẤP NHÁY ĐỎ - "Đang thu âm..."]             │ │
  │ │  "Văn bản bóc băng thời gian thực: Microservices chia nhỏ hệ thống│ │
  │ │  thành các dịch vụ độc lập triển khai riêng biệt..."               │ │
  │ └────────────────────────────────────────────────────────────────────┘ │
  │ [Nút Đỏ: Dừng Nói & Chuyển Sang Vùng Đệm]    [Nút Xám: Bắt Đầu Lại]   │
  └────────────────────────────────────────────────────────────────────────┘
  ```
* **Mô tả luồng tương tác:**
  - Đầu phiên: Modal cho sinh viên chọn Upfront: `[Per-Question]` (Luyện từng câu) vs `[Full-Session]` (Luyện cả phiên).
  - Web Speech TTS tự động đọc câu hỏi bằng giọng tiếng Việt tự nhiên.
  - Sinh viên bấm Micro $\to$ `useSpeechRecognition` bóc băng chữ nhảy trực tiếp lên màn hình ($< 500$ms độ trễ).
  - Bấm Dừng Nói $\to$ Tự động bung Modal `BufferScreen.tsx`.

#### 🖥️ Màn hình FE-S04: `BufferScreen.tsx` (Vùng Đệm Hiệu Đính Code-Switching)
* **Người phụ trách:** 🧑 **Nguyễn Đăng Hải & 🧑 Lê Vũ Hoàng**
* **Mô tả giao diện:**
  - Modal chiếm trọn tâm điểm màn hình, viền bo tròn lớn.
  - Header: **"VÙNG ĐỆM HIỆU ĐÍNH THUẬT NGỮ CHUYÊN NGÀNH"**.
  - **Đồng hồ đếm ngược hình tròn (SVG Circular Progress):** Nhận prop `transcript_buffer_seconds` từ Backend (mặc định 60s). Khi còn 10 giây cuối cùng, viền tròn đổi sang màu đỏ nhấp nháy.
  - Ô Textarea rộng hiển thị bản transcript vừa bóc băng: Sinh viên được quyền click chuột gõ phím trực tiếp sửa các từ nhận dạng sai (VD: sửa "đốc cơ" $\to$ "Docker", "mai cơ rô" $\to$ "Microservices").
  - Nút bấm:
    - Nút xanh lá nổi bật: **"Xác Nhận Nộp Ngay"** (bỏ qua thời gian đếm ngược còn lại).
    - Nút viền xám: **"Nghe Lại Câu Trả Lời"** (Web Speech TTS đọc lại những gì vừa bóc băng).
    - Nút đỏ: **"Hủy & Thu Âm Lại"**.
  - **Hành vi khi hết giờ:** Đồng hồ về 0 $\to$ Tự động kích hoạt nộp bài ngay lập tức (`POST /api/v1/practice/sessions/{id}/answers`).

#### 🖥️ Màn hình FE-S05: `FollowUpQuestionCard.tsx` (Thẻ Câu Hỏi Phụ Đào Sâu)
* **Người phụ trách:** 🧑 **Nguyễn Đăng Hải**
* **Mô tả giao diện:**
  - Xuất hiện trong chế độ `[Per-Question]` khi điểm câu trả lời rơi vào khoảng **$4.0 \le \text{Score} \le 8.0$** và số câu hỏi phụ $\le$ số lượng tối đa Admin cấu hình (`1 <= max_follow_up_questions <= 5`, mặc định 2 câu).
  - Thẻ card viền tím ánh kim nổi bật:
    - Badge: `Câu hỏi chuyên sâu đào sâu (1/2)`.
    - Tiêu đề: *"AI nhận thấy bạn cần giải thích rõ hơn luận điểm sau:"*.
    - Nội dung câu hỏi phụ do Gemini vừa sinh ra (VD: *"Bạn vừa nhắc tới Eventual Consistency, hãy giải thích cách xử lý khi dữ liệu bị xung đột?"*).
    - Nút Loa: Phát âm thanh TTS câu hỏi phụ.
  - Hai nút lựa chọn:
    1. Nút tím: **"Chấp Nhận Trả Lời Đào Sâu"** $\to$ Kích hoạt micro thu âm câu trả lời bổ sung.
    2. Nút xám: **"Bỏ Qua & Xem Bảng Điểm"** $\to$ Chốt điểm và mở `ScorecardModal`.

#### 🖥️ Màn hình FE-S06: `ScorecardModal.tsx` (Bảng Điểm Rubric & Nhận Xét Sư Phạm)
* **Người phụ trách:** 🧑 **Nguyễn Đăng Hải**
* **Mô tả giao diện:**
  - Header: Pháo hoa chúc mừng hoặc hiệu ứng hoàn thành câu hỏi.
  - **Vòng tròn điểm tổng:** Hiển thị điểm số to rõ (VD: `8.5 / 10.0`), đổi màu theo mức (Xanh $\ge 8.0$, Vàng $5.0 - 7.9$, Đỏ $< 5.0$).
  - **Accordion chi tiết tiêu chí Rubric con:**
    - Tiêu chí 1: Khái niệm kiến trúc (Đạt: 2.5 / 3.0đ) — *Nhận xét: Nêu chính xác định nghĩa.*
    - Tiêu chí 2: Ưu nhược điểm (Đạt: 3.5 / 4.0đ) — *Nhận xét: Phân tích rất sâu sắc.*
    - Tiêu chí 3: Kịch bản áp dụng (Đạt: 2.5 / 3.0đ) — *Nhận xét: Ví dụ thực tế thuyết phục.*
  - **Khung nhận xét sư phạm tổng thể của AI:** Đoạn văn ngắn phân tích điểm mạnh và điểm cần cải thiện. Có nút loa để Web Speech TTS đọc to nhận xét cho sinh viên nghe.
  - Nút chuyển tiếp: **"Làm Câu Tiếp Theo"** hoặc **"Hoàn Tất Phiên Luyện Tập"**.

#### 🖥️ Màn hình FE-S07: `MockExamPage.tsx` (Phòng Thi Thử Bấm Giờ MF-02)
* **Người phụ trách:** 🧑 **Nguyễn Đăng Hải & 🧑 Lê Vũ Hoàng**
* **Mô tả giao diện:**
  - Modal bắt đầu thi thử: Sinh viên **chủ động tự chọn Có/Không Follow-up** trước khi bấm làm bài.
  - Header cố định trên cùng:
    - Tên đề thi thử, Tên môn học.
    - **Đồng hồ Master Timer đếm ngược to ở giữa:** Đếm ngược thời gian làm bài (VD: `14:59`), đồng bộ với Server Timer. Khi còn 60 giây cuối cùng hiển thị viền đỏ cảnh báo.
  - **Quy tắc Voice-First Gate:** Khung Textbox gõ phím bị khóa cứng hoàn toàn (`disabled` / `readonly`), có biểu tượng ổ khóa kèm dòng chữ: *"Thi thử vấn đáp bắt buộc phát biểu qua Micro"*.
  - Nút bấm micro to ở giữa màn hình để sinh viên trả lời từng câu. Hết giờ làm bài server tự động thu bài.

#### 🖥️ Màn hình FE-S08: `ExamHistoryPage.tsx` (Lịch Sử Thi & Bảng Điểm CLO)
* **Người phụ trách:** 🧑 **Nguyễn Đăng Hải**
* **Mô tả giao diện:**
  - Bảng danh sách các phiên luyện tập và thi thử đã thực hiện.
  - Cột: Ngày làm, Môn học, Loại hình (Luyện tập / Thi thử), Số câu, Điểm tổng kết, Trạng thái.
  - Click vào từng dòng: Bung Drawer bên phải hiển thị lại nguyên vẹn **Scorecard chi tiết từng câu theo ma trận chuẩn đầu ra CLO** và các câu hỏi phụ đã trả lời.

#### 🖥️ Màn hình FE-S09: `AppealRequestModal.tsx` (Nộp Đơn Phúc Khảo Nội Bộ MF-04)
* **Người phụ trách:** 🧑 **Nguyễn Đăng Hải**
* **Mô tả giao diện:**
  - Dành cho sinh viên khi đăng nhập Student Portal tại nhà xem bảng điểm ca thi thật phòng Lab đã được Giảng viên công bố.
  - Nếu sinh viên không đồng ý điểm: Bấm nút **"Nộp Đơn Phúc Khảo"** mở modal:
    - Hiển thị thông tin ca thi: Kỳ thi, Môn thi, Ngày thi, Điểm chính thức đã công bố.
    - Ô Textarea bắt buộc: *"Lý do xin phúc khảo (Nêu rõ câu hỏi và căn cứ khiếu nại, tối thiểu 20 ký tự)"*.
    - Cảnh báo: *"Đơn phúc khảo sẽ được chuyển trực tiếp cho Trưởng Bộ Môn thẩm định độc lập. Quyết định của Trưởng Bộ Môn là quyết định cuối cùng."*
    - Nút bấm: **"Gửi Đơn Phúc Khảo"** (`POST /api/v1/appeals`). Sau khi gửi, nút bị khóa để chống gửi trùng lặp.

---

### PHÂN HỆ 2: DÀNH CHO GIẢNG VIÊN (LECTURER PORTAL)

#### 🖥️ Màn hình FE-L01: `QuestionStudioPage.tsx` (Rubric Studio 10.0 & AI Generator MF-03)
* **Người phụ trách:** 🧑 **Lê Vũ Hoàng & 🧑 Nguyễn Đăng Hải**
* **Mô tả bố cục giao diện (Wireframe Layout):**
  ```text
  ┌────────────────────────────────────────────────────────────────────────┐
  │ [Logo FPT] NGÂN HÀNG CÂU HỎI & RUBRIC STUDIO 10.0         [Lecturer]   │
  ├────────────────────────────────────────────────────────────────────────┤
  │ [Nút: + Tạo Câu Hỏi Thủ Công]    [Nút Tím: AI Sinh Câu Hỏi Từ FLM]   │
  ├────────────────────────────────────────────────────────────────────────┤
  │ MODAL PREVIEW STUDIO / SOẠN THẢO:                                      │
  │ • Môn học: [SWD392]   • Chuẩn đầu ra CLO: [CLO2]   • Mức Bloom: [Apply]│
  │ • Nội dung đề bài: [                                                ] │
  │ • Câu trả lời mẫu (Model Answer >= 50 ký tự): [                     ] │
  │                                                                        │
  │ BAREM RUBRIC CHI TIẾT:                                                 │
  │ ┌────────────────────────────────────────────────────────────────────┐ │
  │ │ Tiêu chí 1: [Khái niệm cơ bản         ] Trọng số: [ 3.0 ] điểm [X] │ │
  │ │ Tiêu chí 2: [Phân tích ưu nhược điểm  ] Trọng số: [ 4.0 ] điểm [X] │ │
  │ │ Tiêu chí 3: [Ví dụ minh họa thực tế   ] Trọng số: [ 3.0 ] điểm [X] │ │
  │ │ [+ Thêm tiêu chí con]                                              │ │
  │ └────────────────────────────────────────────────────────────────────┘ │
  │ TỔNG ĐIỂM BAREM: [ 10.0 / 10.0 đ ]  <-- Màu Xanh lá (Hợp lệ)           │
  │ (Nếu != 10.0đ: Tô Đỏ rực "Tổng điểm phải đúng 10.0đ", nút Gửi bị Khóa)│
  ├────────────────────────────────────────────────────────────────────────┤
  │ [Nút Lưu Nháp DRAFT]     [Nút Xanh: GỬI LÊN CHO TRƯỞNG BỘ MÔN DUYỆT]   │
  └────────────────────────────────────────────────────────────────────────┘
  ```
* **Mô tả tương tác & Ràng buộc:**
  - Giảng viên tự do chỉnh sửa nội dung, tiêu chí và câu trả lời mẫu.
  - Hàm `reduce` tính tổng điểm real-time: Lệch 10.0đ (dù 9.9đ hay 10.1đ) thì ô tổng điểm đổi sang màu đỏ rực, nút *"Gửi lên cho Trưởng Bộ Môn duyệt"* bị disable cứng. Đúng 10.0đ thì nút sáng lên cho phép bấm gửi (`SUBMITTED_FOR_REVIEW`).

#### 🖥️ Màn hình FE-L02: `AuditEvidencePage.tsx` (Cổng Hậu Kiểm Evidence Panel MF-04)
* **Người phụ trách:** 🧑 **Lê Vũ Hoàng**
* **Mô tả bố cục giao diện (Wireframe Layout):**
  ```text
  ┌────────────────────────────────────────────────────────────────────────┐
  │ CA THI: FA26 - SWD392 - Ca 1 (Phòng 302)   [Nút: CÔNG BỐ ĐIỂM (100%)]  │
  ├────────────────────────────────────────┬───────────────────────────────┤
  │ DANH SÁCH BÀI THI CA THI (40 Sinh viên)│ EVIDENCE PANEL CHI TIẾT       │
  │                                        │ Thí sinh: SE170123 - Ghế: 01  │
  │ 🔴 NHÓM 1: CẦN CAN THIỆP / NGHI NGỜ (3)│ 1. WAVEFORM AUDIO PLAYER (R2) │
  │ [!] 01 - SE170123 - Điểm AI: 4.5 [FAIL]│ [► Phát] ||||| | ||||| | ||| │
  │ [!] 05 - SE170456 - is_suspicious=true │ (Sóng âm thanh thực tế .webm) │
  │ [!] 12 - SE170789 - confidence=0.62    │                               │
  │                                        │ 2. TRANSCRIPT WHISPER GỐC     │
  │ 🟢 NHÓM 2: ĐỘ TIN CẬY CAO (37 Thí sinh)│ "Kiến trúc microservices chia │
  │ [✓] 02 - SE170234 - Điểm AI: 8.5       │ nhỏ... [click từ để tua audio]│
  │ [✓] 03 - SE170345 - Điểm AI: 9.0       │                               │
  │ ...                                    │ 3. PHÂN TÍCH AI CHAIN-OF-THOUGHT│
  │                                        │ - Luận điểm 1: Đạt 2/3đ       │
  │                                        │ - Luận điểm 2: Thiếu ý...     │
  │                                        ├───────────────────────────────┤
  │                                        │ ĐIỀU CHỈNH ĐIỂM GIẢNG VIÊN:   │
  │                                        │ Điểm mới: [ 6.5 ] / 10.0đ     │
  │                                        │ Lý do sửa điểm (>= 10 ký tự): │
  │                                        │ [Em nói đúng ý microservices] │
  │                                        │ [Nút: Lưu Điểm Điều Chỉnh]    │
  └────────────────────────────────────────┴───────────────────────────────┘
  ```
* **Mô tả tương tác & Ràng buộc:**
  - **Phân loại 2 nhóm:** Nhóm 1 hiển thị viền đỏ nhấp nháy trên đầu danh sách. Sinh viên bắt buộc phải đợi Giảng viên chấm hết toàn bộ các bài trong nhóm này.
  - Tích hợp Wavesurfer.js vẽ biểu đồ sóng âm thanh thực tế từ Cloudflare R2 `STT_MSSV.webm`.
  - Giảng viên click vào từ nào trên transcript thì audio tự tua đến đúng giây đó.
  - Sửa điểm bắt buộc nhập lý do giải trình $\ge 10$ ký tự.
  - **Nút "Công Bố Điểm":** Kiểm tra Atomic: Chỉ sáng xanh cho phép bấm khi 100% sinh viên trong ca thi đã có điểm hoàn chỉnh. Bấm xong niêm phong khóa một chiều (`is_locked = true`).

---

### PHÂN HỆ 3: DÀNH CHO TRƯỞNG BỘ MÔN (DEPARTMENT HEAD PORTAL)

#### 🖥️ Màn hình FE-D01: `ExamSeasonConfigPage.tsx` (Quản Trị Kỳ Thi & Cấu Hình Môn Thi MF-04)
* **Người phụ trách:** 🧑 **Nguyễn Đăng Hải**
* **Mô tả giao diện:**
  - Trưởng Bộ Môn khởi tạo kỳ thi (`OfficialExamSession`, ví dụ Kỳ thi Kết thúc môn FA26).
  - Đưa danh sách các môn thi thuộc kỳ thi đó vào hệ thống.
  - **Khi click vào từng môn thi trong kỳ thi:**
    1. Cấu hình danh sách **Ca thi** (`RealExamSessionShift`: phòng máy lab, kíp thi, ngày thi, phân công giám thị).
    2. **Cấu hình Follow-up:** Switch bật/tắt hỏi chuyên sâu (`has_follow_up`) và chọn số câu hỏi phụ (`max_follow_up_questions` từ 1–2 câu, đồng bộ cho tất cả các ca thi của môn).
    3. Cấu hình `ExamInputMode` (`VoiceOnly`, `VoiceWithTranscriptEdit`, `VoiceAndTextInput`) và thời gian đệm `TranscriptBufferSeconds` (10–300s).

#### 🖥️ Màn hình FE-D02: `QuestionApprovalPage.tsx` (Thẩm Định Đề Thi MF-03)
* **Người phụ trách:** 🧑 **Nguyễn Đăng Hải**
* **Mô tả giao diện:**
  - Bảng danh sách câu hỏi do Giảng viên nộp lên chờ duyệt (`SUBMITTED_FOR_REVIEW`).
  - Hiển thị: Môn học, Giảng viên tạo, Ngày nộp, Cấp độ Bloom, Barem Rubric tổng 10.0đ.
  - Modal xem chi tiết đề bài, Model Answer $\ge 50$ ký tự, tiêu chí barem.
  - **3 Nút quyết định của Trưởng Bộ Môn:**
    1. Nút xanh: **"Phê duyệt" (`APPROVED`)** $\to$ Chuyển câu hỏi vào ngân hàng đề chính thức.
    2. Nút vàng: **"Yêu cầu chỉnh sửa" (`NEEDS_REVISION`)** $\to$ Bắt buộc nhập ô góp ý sửa đổi gửi về cho Giảng viên.
    3. Nút đỏ: **"Từ chối" (`REJECTED`)** $\to$ Loại bỏ câu hỏi kèm lý do.

#### 🖥️ Màn hình FE-D03: `AppealManagementPage.tsx` (Thẩm Định Phúc Khảo Nội Bộ MF-04)
* **Người phụ trách:** 🧑 **Nguyễn Đăng Hải**
* **Mô tả giao diện:**
  - Danh sách đơn phúc khảo do sinh viên gửi lên (`PENDING`, `IN_REVIEW`).
  - Click vào từng đơn:
    - Hiển thị lý do khiếu nại của sinh viên.
    - Mở Evidence Panel độc lập: Nghe lại audio Cloudflare R2, xem transcript Whisper, xem điểm gốc của Giảng viên và lý do điều chỉnh cũ.
    - Quyết định của Trưởng Bộ Môn:
      - Nhập điểm đề xuất mới $\to$ Bấm **"Chấp Thuận & Cập Nhật Điểm"** (`APPROVED`).
      - Bấm **"Bác Đơn Phúc Khảo"** (`REJECTED`) kèm lý do giải trình.
    - Điểm mới được cập nhật trực tiếp về Portal sinh viên.

---

### PHÂN HỆ 4: DÀNH CHO GIÁM THỊ PHÒNG THI (PROCTOR PORTAL)

#### 🖥️ Màn hình FE-P01: `ProctorRoomMonitorPage.tsx` (Giám Sát Phòng Thi Phòng Lab MF-04)
* **Người phụ trách:** 🧑 **Lê Vũ Hoàng**
* **Mô tả giao diện:**
  - Tiêu đề ca thi: Phòng Lab 302, Kíp thi 08:00 - 09:30, Môn SWD392.
  - Nút lớn: **"MỞ CA THI PHÒNG LAB"** (kích hoạt cho phép thí sinh check-in).
  - **Ma trận 40 ô máy trạm (Seat Grid 1–40):**
    - Mỗi ô đại diện cho 1 máy trạm: Số thứ tự (STT 1–40), MSSV, Họ tên, IP máy trạm `ip_address`.
    - Màu sắc trạng thái trực quan:
      - *Xám:* Chưa check-in (`SCHEDULED`).
      - *Vàng:* Đang kiểm tra micro phần cứng.
      - *Xanh dương:* Đang làm bài thi (`IN_PROGRESS`).
      - *Xanh lá:* Đã nộp bài thành công (`SUBMITTED`).
      - *Đỏ nhấp nháy:* Vi phạm mất focus Kiosk (`blur_count >= 1`) hoặc Bị đình chỉ (`SUSPENDED`).
  - Giám thị có thể click vào máy trạm để xem chi tiết log vi phạm hoặc bấm đình chỉ thi thủ công nếu phát hiện gian lận tại phòng.

---

### PHÂN HỆ 5: DÀNH CHO MÁY TRẠM PHÒNG LAB KIOSK (KIOSK EXAM WORKSTATION)

#### 🖥️ Màn hình FE-K01: `KioskCheckInPage.tsx` (Đăng Nhập Máy Trạm Kiosk)
* **Người phụ trách:** 🧑 **Lê Vũ Hoàng**
* **Mô tả giao diện:**
  - Giao diện phong tỏa toàn màn hình, không có thanh cuộn và menu trình duyệt.
  - Ô nhập: Số ghế / STT (1–40) và Mã số sinh viên (MSSV).
  - Khi bấm *"Xác nhận máy trạm"*:
    - Backend tự trích xuất IP client đối chiếu với cột `ip_address` trên vé thi ứng với số ghế.
    - Nếu không khớp IP quy định $\to$ Bung thông báo đỏ cấm thi: *"Sai máy trạm quy định cho ghế này. Vui lòng ngồi đúng vị trí!"* (`HTTP 403`).
    - Nếu khớp IP $\to$ Chuyển sang màn hình Mic-Check 30s.

#### 🖥️ Màn hình FE-K02: `KioskHardwareMicCheckPage.tsx` (Kiểm Tra Micro Phần Cứng 30s)
* **Người phụ trách:** 🧑 **Lê Vũ Hoàng**
* **Mô tả giao diện:**
  - Tiêu đề: *"KIỂM TRA PHẦN CỨNG MICROPHONE TRƯỚC KHI VÀO THI"*.
  - Hướng dẫn: *"Hãy nói 'Xin chào hệ thống' vào tai nghe phòng máy để kiểm tra độ nhạy âm thanh"*.
  - **Thanh đo âm lượng thời gian thực (Audio VU Meter):** Dùng Web Audio API phân tích âm lượng.
    - Vạch đo nhảy từ 0 đến 100dB.
    - Ngưỡng chuẩn: Vạch kẻ đỏ tại **60dB**.
    - Khi âm lượng phát biểu $\ge 60$dB $\to$ Thanh đo đổi sang màu xanh lá rực rỡ, vòng tròn đếm ngược hoàn tất và nút **"BẮT ĐẦU VÀO PHÒNG THI"** sáng lên cho phép click.
    - Nếu mic hỏng hoặc âm lượng quá nhỏ $< 60$dB $\to$ Nút bấm bị vô hiệu hóa, thông báo: *"Âm lượng micro chưa đạt chuẩn 60dB. Vui lòng kiểm tra jack cắm tai nghe hoặc báo giám thị"*.

#### 🖥️ Màn hình FE-K03: `KioskExamRoomPage.tsx` (Phòng Thi Vấn Đáp Phong Tỏa MF-04)
* **Người phụ trách:** 🧑 **Lê Vũ Hoàng**
* **Mô tả tương tác & Cơ chế Lockdown chống gian lận:**
  - **Fullscreen Lockdown:** Ép toàn màn hình (`requestFullscreen()`).
  - **Vô hiệu hóa phím tắt hệ thống:** Chặn `Alt+Tab`, `Windows`, `Escape`, `F11`, `F12`, `Ctrl+Shift+I` (chống mở DevTools), chặn click chuột phải.
  - **Lắng nghe sự kiện mất Focus (`window.onblur`):**
    - Mất focus lần 1 $\to$ Bung Modal cảnh báo màu vàng to giữa màn hình: *"CẢNH BÁO VI PHẠM LẦN 1/3: Tuyệt đối không chuyển cửa sổ hoặc click ra ngoài phòng thi!"*.
    - Mất focus lần 2 $\to$ Cảnh báo màu cam kèm còi bíp: *"CẢNH BÁO VI PHẠM LẦN 2/3: Lần vi phạm tiếp theo bạn sẽ bị ĐÌNH CHỈ THI ngay lập tức!"*.
    - **Mất focus lần 3 (`blur_count >= 3`)**: Lập tức chuyển hướng sang `KioskSuspendedPage.tsx`, khóa chết máy trạm và tự động nộp bài lập biên bản vi phạm.
  - Luồng thi: Hiển thị câu hỏi thi thật rút từ `exam_questions`; Stream audio trực tiếp lên Cloudflare R2 với tên file bất biến **`STT_MSSV.webm`** (ví dụ `01_SE170123.webm`), tính toán mã băm SHA-256 niêm phong file tại máy trạm.
  - Nộp bài: Nhận phản hồi Persist First $< 100$ms lưu DB `SUBMITTED`.

#### 🖥️ Màn hình FE-K04: `KioskSuspendedPage.tsx` (Màn Hình Đình Chỉ Thi)
* **Người phụ trách:** 🧑 **Lê Vũ Hoàng**
* **Mô tả giao diện:**
  - Nền đỏ thẫm toàn màn hình, biểu tượng cảnh báo vi phạm lớn.
  - Tiêu đề: **"BÀI THI ĐÃ BỊ ĐÌNH CHỈ DO VI PHẠM QUY CHẾ PHÒNG THI"**.
  - Lý do: *"Hệ thống phát hiện máy trạm mất tiêu điểm (Focus Loss) quá 3 lần quy định"*.
  - Khóa toàn bộ bàn phím và chuột. Thông báo: *"Thí sinh giữ nguyên vị trí và chờ Giám thị phòng thi xử lý lập biên bản"*.

#### 🖥️ Màn hình FE-K05: `KioskSubmittedPage.tsx` (Nộp Bài Thành Công Khóa An Toàn)
* **Người phụ trách:** 🧑 **Lê Vũ Hoàng**
* **Mô tả giao diện (Quy tắc bất biến MF-04):**
  - Nền xanh dịu an toàn, biểu tượng ổ khóa niêm phong bảo mật.
  - Tiêu đề: **"BÀI THI ĐÃ ĐƯỢC NIÊM PHONG VÀ LƯU TRỮ AN TOÀN"**.
  - Thông tin biên nhận điện tử:
    - Mã thí sinh: `SE170123` | STT: `01`.
    - File âm thanh niêm phong: `01_SE170123.webm`.
    - Mã băm toàn vẹn SHA-256: `e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855`.
    - Thời gian nộp: `08:45:22 15/10/2026`.
  - **Dòng thông báo quan trọng:** *"Bài thi đã được lưu trữ an toàn. Điểm số sẽ do Giảng viên thẩm định và công bố chính thức trên Student Portal. Thí sinh ký biên bản nộp bài giấy và rời khỏi phòng thi."*
  - **Cam kết kỹ thuật:** **0% hiển thị điểm liền, 0% khiếu nại tại chỗ**. Không có bất kỳ nút bấm nào để xem điểm hay phúc khảo trên máy Kiosk.

---

### PHÂN HỆ 6: DÀNH CHO QUẢN TRỊ VIÊN HỆ THỐNG (ADMIN PORTAL)

#### 🖥️ Màn hình FE-A01: `AdminConfigPage.tsx` (Cấu Hình Follow-up Hệ Thống Luyện Tập)
* **Người phụ trách:** 🧑 **Nguyễn Đăng Hải**
* **Mô tả giao diện:**
  - Quản trị thông số toàn hệ thống dành riêng cho `admin`.
  - Mục cấu hình: **"Số lượng câu hỏi Follow-up tối đa cho hệ thống luyện tập (MF-01)"**:
    - Input number có giới hạn: từ **1 đến 5 câu** (giá trị mặc định: **2 câu**).
    - Dòng ghi chú giải thích: *"Giảng viên không cấu hình follow-up trong MF-01. Đây là cấu hình dùng chung cho toàn bộ sinh viên khi luyện tập từng câu [Per-Question]."*.
    - Nút bấm: **"Lưu Cấu Hình Hệ Thống"** (`PUT /api/v1/admin/practice-config`).

#### 🖥️ Màn hình FE-A02: `AdminDlqMonitorPage.tsx` (Giám Sát Dead-Letter Queue & Chấm Bù)
* **Người phụ trách:** 🧑 **Nguyễn Đăng Hải & 🧑 Lê Vũ Hoàng**
* **Mô tả giao diện:**
  - Bảng theo dõi các task chấm điểm bị lỗi quá 3 lần retry (`dead_letter_queues`).
  - Cột: ID task, SessionId, Mã sinh viên, Thời gian lỗi, Số lần đã retry, Chi tiết lỗi (VD: `Google Gemini HTTP 429 Rate Limit`), Trạng thái (`PENDING_RETRY` / `RESOLVED`).
  - Nút hành động: **"Kích Hoạt Chấm Bù Ngay (Replay)"** cho từng task hoặc replay toàn bộ hàng đợi.

---

## 📊 3. BẢNG TỔNG HỢP MA TRẬN 24 MÀN HÌNH & PHÂN CÔNG TÁC VỤ FRONTEND

| Mã Màn Hình | Tên Tệp Component | Phân Hệ / Vai Trò | Kỹ Sư Phụ Trách | Trạng Thái Kỹ Thuật |
|:---|:---|:---:|:---:|:---:|
| **FE-S01** | `LoginPage.tsx` | Student / All | **🧑 Lê Vũ Hoàng** | ⏳ Cần ghép API Google PKCE |
| **FE-S02** | `StudentDashboardPage.tsx` | Student | **🧑 Nguyễn Đăng Hải** | ⏳ Cần tạo component |
| **FE-S03** | `PracticePage.tsx` (MF-01) | Student | **🧑 Nguyễn Đăng Hải + Hoàng** | ⏳ Cần tạo component |
| **FE-S04** | `BufferScreen.tsx` (Đệm 60s) | Student | **🧑 Nguyễn Đăng Hải + Hoàng** | ⏳ Cần tạo component |
| **FE-S05** | `FollowUpQuestionCard.tsx` | Student | **🧑 Nguyễn Đăng Hải** | ⏳ Cần tạo component |
| **FE-S06** | `ScorecardModal.tsx` | Student | **🧑 Nguyễn Đăng Hải** | ⏳ Cần tạo component |
| **FE-S07** | `MockExamPage.tsx` (MF-02 Voice-First) | Student | **🧑 Nguyễn Đăng Hải + Hoàng** | ⏳ Cần tạo component |
| **FE-S08** | `ExamHistoryPage.tsx` | Student | **🧑 Nguyễn Đăng Hải** | ⏳ Cần tạo component |
| **FE-S09** | `AppealRequestModal.tsx` (MF-04) | Student | **🧑 Nguyễn Đăng Hải** | ⏳ Cần tạo component |
| **FE-L01** | `QuestionStudioPage.tsx` (Barem 10.0đ) | Lecturer | **🧑 Lê Vũ Hoàng + Hải** | ⏳ Cần tạo component |
| **FE-L02** | `AuditEvidencePage.tsx` (Wavesurfer.js) | Lecturer | **🧑 Lê Vũ Hoàng** | ⏳ Cần tạo component |
| **FE-D01** | `ExamSeasonConfigPage.tsx` (MF-04) | Dept Head | **🧑 Nguyễn Đăng Hải** | ⏳ Cần tạo component |
| **FE-D02** | `QuestionApprovalPage.tsx` (MF-03) | Dept Head | **🧑 Nguyễn Đăng Hải** | ⏳ Cần tạo component |
| **FE-D03** | `AppealManagementPage.tsx` (MF-04) | Dept Head | **🧑 Nguyễn Đăng Hải** | ⏳ Cần tạo component |
| **FE-P01** | `ProctorRoomMonitorPage.tsx` (STT 1-40) | Proctor | **🧑 Lê Vũ Hoàng** | ⏳ Cần tạo component |
| **FE-K01** | `KioskCheckInPage.tsx` (IP Binding) | Kiosk Lab | **🧑 Lê Vũ Hoàng** | ⏳ Cần tạo component |
| **FE-K02** | `KioskHardwareMicCheckPage.tsx` (>=60dB)| Kiosk Lab | **🧑 Lê Vũ Hoàng** | ⏳ Cần tạo component |
| **FE-K03** | `KioskExamRoomPage.tsx` (Lockdown) | Kiosk Lab | **🧑 Lê Vũ Hoàng** | ⏳ Cần tạo component |
| **FE-K04** | `KioskSuspendedPage.tsx` (Đình chỉ) | Kiosk Lab | **🧑 Lê Vũ Hoàng** | ⏳ Cần tạo component |
| **FE-K05** | `KioskSubmittedPage.tsx` (Niêm phong) | Kiosk Lab | **🧑 Lê Vũ Hoàng** | ⏳ Cần tạo component |
| **FE-A01** | `AdminConfigPage.tsx` (Follow-up 1-5) | Admin | **🧑 Nguyễn Đăng Hải** | ⏳ Cần tạo component |
| **FE-A02** | `AdminDlqMonitorPage.tsx` (DLQ Replay) | Admin | **🧑 Nguyễn Đăng Hải + Hoàng** | ⏳ Cần tạo component |
