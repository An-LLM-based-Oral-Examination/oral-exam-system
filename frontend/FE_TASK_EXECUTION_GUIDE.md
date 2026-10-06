# 🎨 FRONTEND TASK & EXECUTION GUIDE (MASTER CONSOLIDATED v3.0)
## ĐỒ ÁN TỐT NGHIỆP SEP490 (FA26SE166) — HỆ THỐNG VẤN ĐÁP AI (ORAL EXAM SYSTEM)
**Dành cho:** 🧑 Lê Vũ Hoàng (FE Lead) & 🧑 Phạm Nguyễn Đăng Hải (FE Developer)
**Công nghệ:** React 19, TypeScript, Vite, Tailwind CSS v4, SignalR Client, Web Speech API, Recharts, Lucide-react.
**Quy chuẩn cốt lõi:** Buffer Screen = 30s | Voice-First Gate | Quota 3 lượt/ngày/môn | Kiosk 3 vi phạm | Khóa điểm 1 chiều (`is_locked=true`).

---

## 📁 1. CẤU TRÚC THƯ MỤC & MA TRẬN PHÂN BỔ FILE (`frontend/src/`)

Toàn bộ khung sườn đã được khởi tạo sẵn sàng trên nhánh `feature/fe-ui-setup`. Dưới đây là phân định file độc quyền:

```text
frontend/src/
├── assets/                  # Logo, icon SVG tĩnh
├── routes/                  # AppRoutes.tsx, ProtectedRoute.tsx, RoleBasedRedirect.tsx
├── components/
│   ├── common/              # Button.tsx, Input.tsx, Card.tsx, Badge.tsx (Bộ primitives tái sử dụng)
│   ├── layout/              # AppLayout.tsx, Navbar.tsx, Sidebar.tsx, Footer.tsx
│   ├── practice/            # BufferScreen.tsx (30s), ScorecardModal.tsx (MF-01)
│   ├── exam/                # VoiceFirstGate.tsx, CountdownTimer.tsx (MF-02)
│   ├── audio/               # WaveformPlayer.tsx (R2 WebM)
│   └── rubric/              # ScoreOverrideForm.tsx (MF-04)
├── context/
│   └── AuthContext.tsx      # Quản lý phiên đăng nhập, JWT, 3 vai trò (Student, Instructor, Admin)
├── hooks/
│   ├── useSpeechRecognition.ts # Hook Web Speech API TTS đọc đề + STT ghi âm (MF-01/MF-02)
│   ├── useSignalR.ts        # Hook WebSocket SignalR Hub /hubs/practice
│   └── useKioskLockdown.ts  # Hook Kiosk Mode bắt F12, chuột phải, đổi tab 3 strikes (MF-04)
├── pages/
│   ├── auth/                # LoginPage.tsx (có sẵn Dev Role Switcher), UnauthorizedPage.tsx (403)
│   ├── student/             # Dashboard, Practice, Mock Exam, Exam History và Official Exam
│   └── lecturer/            # Dashboard, Question Studio, Create Question và Audit Evidence
├── services/
│   ├── api.client.ts        # Axios Client gắn sẵn Bearer Token & bắt lỗi RFC 7807 ProblemDetails
│   ├── auth.service.ts      # Hợp đồng API Auth
│   ├── question.service.ts  # Hợp đồng API Ngân hàng câu hỏi
│   ├── practice.service.ts  # Hợp đồng API Luyện tập
│   ├── mockExam.service.ts  # Hợp đồng API Thi thử
│   └── officialExam.service.ts # Hợp đồng API Thi chính thức và hậu kiểm
├── types/
│   ├── auth.types.ts        # User, UserRole, LoginResponse, ProblemDetails
│   ├── rubric.types.ts      # BloomLevel, ScopeType, RubricCriterion, Question
│   ├── practice.types.ts    # PracticeSession, ScorecardPayload
│   ├── mockExam.types.ts    # MockExamSession
│   └── officialExam.types.ts # ExamSession, CandidateSubmission
└── utils/
    └── cn.ts                # Utility clsx + twMerge tối ưu Tailwind CSS
```

### Bảng Phân Quyền Sở Hữu File (File Ownership Matrix):
| Phân hệ / Thành phần | Tệp mã nguồn thực tế | Người sở hữu | Trách nhiệm kỹ thuật |
|:---|:---|:---:|:---|
| **Core UI & Primitives** | `src/utils/cn.ts`, `src/components/common/*` | Hoàng | Đảm bảo components giao diện dùng chung chuẩn styling Tailwind, dễ mở rộng. |
| **App Shell & Auth** | `src/components/layout/*`, `src/context/AuthContext.tsx`, `src/pages/auth/*` | Hoàng | Xây dựng shell co giãn, điều hướng phân quyền 3 vai (`Student`, `Instructor`, `Admin`). |
| **MF-03: Ngân Hàng Đề** | `src/pages/lecturer/QuestionStudioPage.tsx`, `src/services/question.service.ts` | Hải | Render danh sách câu hỏi, thanh tìm kiếm và bộ lọc đa tiêu chí (Môn, Bloom, Scope). |
| **MF-03: Barem Rubric 10.0** | `src/pages/lecturer/CreateQuestionPage.tsx` | Hải | Form thêm/xóa tiêu chí động; **Client Guard tổng điểm bắt buộc = 10.0đ**, AI Simulator. |
| **MF-01: Voice AI** | `src/hooks/useSpeechRecognition.ts`, `src/pages/student/PracticeSessionPage.tsx` | Hoàng | Web Speech TTS đọc đề, STT thu âm real-time, chọn 2 chế độ (Chấm từng câu / Làm cả bộ). |
| **MF-01: Đệm 30s & SignalR** | `src/components/practice/BufferScreen.tsx`, `ScorecardModal.tsx`, `src/hooks/useSignalR.ts` | Hải | **Màn hình đệm 30s** sửa từ Code-Switching, kết nối SignalR Hub nhận điểm Scorecard và A2. |
| **MF-02: Voice-First** | `src/components/exam/VoiceFirstGate.tsx`, `src/pages/student/MockExamPage.tsx` | Hoàng | **Khóa cứng Textarea**, bắt buộc nói mic xong mới cho phép sửa; bắt lỗi Quota 3 lượt (`HTTP 429`). |
| **MF-02: Timer & Radar** | `src/components/exam/CountdownTimer.tsx`, Biểu đồ Recharts Radar | Hải | Master Timer đồng bộ thời gian máy chủ, AutoSubmitGuard khi hết giờ, biểu đồ mạng nhện Bloom. |
| **MF-04: Kiosk Lockdown** | `src/hooks/useKioskLockdown.ts`, giao diện Kiosk trạm thi Lab | Hoàng | Chặn F12, chuột phải, chuyển tab (3 vi phạm đình chỉ thi), stream audio R2 `STT_MSSV.webm`. |
| **MF-04: Cổng Hậu Kiểm** | `src/pages/lecturer/AuditEvidencePage.tsx`, `src/components/audio/*`, `src/components/rubric/*` | Hải | Danh sách thí sinh theo số máy STT, Waveform Player 1.5x, sửa điểm giải trình, **Khóa điểm 1 chiều**. |

---

## 🔌 2. HỢP ĐỒNG GIAO TIẾP VỚI BACKEND (API & REALTIME CONTRACTS)

- **Base URL:** `/api/v1/`
- **Xác thực JWT:** Token lưu trong `sessionStorage` (chống XSS). Mọi request qua `apiClient` tự động gắn Header: `Authorization: Bearer <accessToken>`.
- **Chuẩn lỗi hệ thống RFC 7807 (ProblemDetails):** Mọi lỗi (400, 401, 403, 422, 429) đều trả JSON chuẩn `{ title, status, detail, errors }`. Frontend tự động bắt `detail` hiện Toast thông báo lỗi.
- **Tiếp nhận bài nộp MF-01 (HTTP 202 Accepted):** Khi gọi `POST /api/v1/practice/submit`, Backend phản hồi `202 Accepted` trong `< 100ms`. Frontend chuyển trạng thái chờ và nhận kết quả qua WebSocket.
- **Real-time SignalR Hub:** Kết nối tới `/hubs/practice`, lắng nghe event `ReceiveScorecard(ScorecardPayload)`.
- **Bảo mật One-Way Lock:** Bài thi đã khóa (`is_locked = true`) bị Backend chặn cứng mọi lệnh sửa (`HTTP 403 Forbidden`).

---

## 📅 3. LỊCH BÁO CÁO 2 NGÀY/LẦN & CHECKLIST CHI TIẾT 4 TUẦN (BI-DAILY AGILITY)

Mỗi tuần cố định 3 mốc báo cáo chính:
- **Thứ 2:** Họp giao việc đầu tuần (Kick-off)
- **Thứ 4 (Ngày 2):** Báo cáo Sync 1 — Trình diễn tính năng 1
- **Thứ 6 (Ngày 4):** Báo cáo Sync 2 — Trình diễn tính năng 2
- **Chủ Nhật (Ngày 6):** Gate Review tuần — Ghép nối API Backend, kiểm tra 6 test cases

---

### 🏃 TUẦN 1: MÓNG HỆ THỐNG, APP SHELL & MF-03 (NGÂN HÀNG ĐỀ & RUBRIC 10.0)

#### 📍 Buổi Báo Cáo 1 — Thứ 4 (Ngày 2 của Tuần 1)
* **🧑 Hoàng (FE Lead):**
  - **Task FE-1.1 & FE-1.2:** Hoàn thiện App Shell `AppLayout.tsx`, `Sidebar.tsx`, `Navbar.tsx` và trang `LoginPage.tsx`.
  - **Nội dung demo:** Giao diện co giãn responsive, chuyển đổi menu mượt mà, form đăng nhập hiển thị đúng input email/mật khẩu.
* **🧑 Hải (FE Dev):**
  - **Task FE-1.5:** Hoàn thiện trang `QuestionStudioPage.tsx`.
  - **Nội dung demo:** Render bảng danh sách câu hỏi mẫu với thanh tìm kiếm và bộ lọc Môn học (PRN231, SWD392) cùng Cấp độ nhận thức Bloom.
* **Tiêu chí nghiệm thu (DoD):** `npm run build` thành công đạt Exit Code 0, không có lỗi TypeScript hay console error.

#### 📍 Buổi Báo Cáo 2 — Thứ 6 (Ngày 4 của Tuần 1)
* **🧑 Hoàng (FE Lead):**
  - **Task FE-1.3 & FE-1.4:** Hoàn thiện `ProtectedRoute.tsx` và `AuthContext.tsx`.
  - **Nội dung demo:** Dùng Dev Role Switcher đăng nhập tài khoản `Student`, bấm vào menu Ngân hàng đề $\rightarrow$ Hệ thống tự động chặn và đẩy sang `/unauthorized` (403 Forbidden). Đổi sang `Instructor` $\rightarrow$ truy cập thành công.
* **🧑 Hải (FE Dev):**
  - **Task FE-1.6 & FE-1.7:** Hoàn thiện form `CreateQuestionPage.tsx`.
  - **Nội dung demo:** Bảng tiêu chí Rubric động (thêm/xóa dòng). **Client Guard**: Nhập tổng điểm 9.5đ $\rightarrow$ Nút "Lưu câu hỏi" lập tức bị vô hiệu hóa kèm chữ đỏ cảnh báo; sửa thành đúng 10.0đ $\rightarrow$ Nút sáng lên cho phép lưu.
* **Tiêu chí nghiệm thu (DoD):** Invariant bất biến $\sum \text{Rubric} = 10.0$ hoạt động chuẩn xác 100% trên client.

#### 📍 Buổi Báo Cáo 3 — Chủ Nhật (Ngày 6 của Tuần 1 — Gate 1 Review)
* **Cả 2 thành viên:**
  - Ghép API thật với Backend: `POST /api/v1/auth/login`, `GET /api/v1/questions`, `POST /api/v1/questions`.
  - Chụp ảnh 6 Test Cases cho MF-03 lưu vào `docs/test_evidences/MF03/`.
  - Merge nhánh `feature/fe-ui-setup` vào `develop`.

---

### 🏃 TUẦN 2: MF-01 (LUYỆN TẬP VẤN ĐÁP TỰ DO & SIGNALR REALTIME)

#### 📍 Buổi Báo Cáo 1 — Thứ 4 (Ngày 2 của Tuần 2)
* **🧑 Hoàng (FE Lead):**
  - **Task FE-2.1:** Hoàn thiện hook `useSpeechRecognition.ts` và nút Micro trên `PracticeSessionPage.tsx`.
  - **Nội dung demo:** Bấm "Nghe đề" trình duyệt phát âm đọc to câu hỏi tiếng Việt bằng Web Speech TTS. Bấm "Nói" micro thu âm và xuất text thời gian thực.
* **🧑 Hải (FE Dev):**
  - **Task FE-2.4:** Hoàn thiện component `BufferScreen.tsx`.
  - **Nội dung demo:** Khi bấm dừng nói, modal đếm lùi **30 giây** hiện lên kèm thanh tiến trình mượt mà, cho phép gõ bàn phím sửa từ vựng tiếng Anh chuyên ngành (Code-Switching).
* **Tiêu chí nghiệm thu (DoD):** Đồng hồ 30 giây chạy mượt, đếm về 0 tự động nộp bài.

#### 📍 Buổi Báo Cáo 2 — Thứ 6 (Ngày 4 của Tuần 2)
* **🧑 Hoàng (FE Lead):**
  - **Task FE-2.2 & FE-2.3:** Hoàn thiện bộ chọn 2 chế độ luyện tập (Chấm từng câu vs Làm cả bộ) và nút **"Chấm thử bằng AI" (AI Calibration)** trên trang tạo đề.
  - **Nội dung demo:** Chuyển đổi giữa 2 chế độ luyện tập, bấm nút calibrate gọi API phản hồi kết quả AI chấm thử câu trả lời mẫu của giảng viên.
* **🧑 Hải (FE Dev):**
  - **Task FE-2.5 & FE-2.6:** Hoàn thiện hook `useSignalR.ts` và modal `ScorecardModal.tsx`.
  - **Nội dung demo:** Thiết lập kết nối WebSocket tới `/hubs/practice`. Sau khi nộp bài nhận HTTP 202 Accepted, khi Worker chấm xong, pop-up `ScorecardModal.tsx` tự động bung lên hiển thị điểm số, nhận xét và câu hỏi xoáy A2.
* **Tiêu chí nghiệm thu (DoD):** Phản hồi 202 trong `< 100ms`, điểm AI trả về thời gian thực không cần tải lại trang.

#### 📍 Buổi Báo Cáo 3 — Chủ Nhật (Ngày 6 của Tuần 2 — Gate 2 Review)
* **Cả 2 thành viên:**
  - Demo thông suốt toàn bộ chu trình MF-01: Nghe TTS $\rightarrow$ Nói mic $\rightarrow$ Sửa từ ở màn hình đệm 30s $\rightarrow$ Đẩy vào hàng đợi 202 Accepted $\rightarrow$ Nhận điểm Scorecard qua SignalR.
  - Chụp ảnh 6 Test Cases cho MF-01 lưu vào `docs/test_evidences/MF01/`.

---

### 🏃 TUẦN 3: MF-02 (THI THỬ BẤM GIỜ, CHỐT CHẶN VOICE-FIRST & QUOTA GUARD)

#### 📍 Buổi Báo Cáo 1 — Thứ 4 (Ngày 2 của Tuần 3)
* **🧑 Hoàng (FE Lead):**
  - **Task FE-3.1 & FE-3.2:** Hoàn thiện `VoiceFirstGate.tsx` trên trang `MockExamPage.tsx`.
  - **Nội dung demo:** Ô Textarea bị khóa cứng kèm biểu tượng ổ khóa; nếu cố tình gõ phím hệ thống chặn lại và cảnh báo bắt buộc nói mic. Chỉ mở khóa sau khi bấm dừng thu âm.
* **🧑 Hải (FE Dev):**
  - **Task FE-3.3:** Hoàn thiện `CountdownTimer.tsx`.
  - **Nội dung demo:** Đồng hồ đếm ngược toàn bài và từng câu đồng bộ mốc thời gian máy chủ của ca thi (không phụ thuộc giờ máy tính cá nhân).
* **Tiêu chí nghiệm thu (DoD):** Chặn đứng 100% hành vi gõ văn bản trực tiếp khi chưa thu âm bằng mic.

#### 📍 Buổi Báo Cáo 2 — Thứ 6 (Ngày 4 của Tuần 3)
* **🧑 Hoàng (FE Lead):**
  - **Task FE-3.5:** Xử lý ngoại lệ hạn ngạch Quota Guard trên giao diện thi thử.
  - **Nội dung demo:** Nếu tài khoản đã thi >= 3 lần trong ngày cho môn học $\rightarrow$ Backend trả `HTTP 429 Too Many Requests`. Giao diện hiển thị banner cảnh báo đỏ theo chuẩn RFC 7807 và khóa nút bắt đầu thi.
* **🧑 Hải (FE Dev):**
  - **Task FE-3.4:** Hoàn thiện `BloomRadar.tsx` bằng Recharts.
  - **Nội dung demo:** Cơ chế `AutoSubmitGuard` tự động thu bài khi hết giờ và biểu đồ mạng nhện Recharts hiển thị 6 trục kỹ năng nhận thức Bloom tại trang kết quả.
* **Tiêu chí nghiệm thu (DoD):** Chặn đúng lần thi thứ 4 trong ngày; biểu đồ Radar render sắc nét, phản ánh đúng dữ liệu điểm.

#### 📍 Buổi Báo Cáo 3 — Chủ Nhật (Ngày 6 của Tuần 3 — Gate 3 Review)
* **Cả 2 thành viên:**
  - Trình diễn luồng thi thử 5 câu hỏi hoàn chỉnh với cơ chế phân nhánh Dual-Path AI (Fast-Path $< 10$s hoặc Async Fallback xem lại sau tại F8).
  - Chạy Regression Test đảm bảo các tính năng mới của MF-02 không làm gãy luồng MF-01.
  - Chụp ảnh 6 Test Cases cho MF-02 lưu vào `docs/test_evidences/MF02/`.

---

### 🏃 TUẦN 4: MF-04 (THI THẬT PHÒNG LAB, KIOSK LOCKDOWN & CỔNG THẨM ĐỊNH ĐIỂM)

#### 📍 Buổi Báo Cáo 1 — Thứ 4 (Ngày 2 của Tuần 4)
* **🧑 Hoàng (FE Lead):**
  - **Task FE-4.1 & FE-4.2:** Hoàn thiện `useKioskLockdown.ts` trên giao diện trạm thi phòng máy.
  - **Nội dung demo:** Bấm F12, chuột phải, hoặc đổi tab: Vi phạm lần 1-2 hiện cảnh báo vàng/đỏ; vi phạm lần 3 tự động khóa cứng màn hình và đình chỉ thi. **Màn hình phòng thi TUYỆT ĐỐI 0% HIỆN ĐIỂM SỐ**.
* **🧑 Hải (FE Dev):**
  - **Task FE-4.3:** Hoàn thiện giao diện Cổng hậu kiểm Giảng viên `AuditEvidencePage.tsx`.
  - **Nội dung demo:** Hiển thị danh sách thí sinh theo số máy trạm phòng Lab (STT 01 $\rightarrow$ 40).
* **Tiêu chí nghiệm thu (DoD):** Kiosk chặn 100% các phím tắt gian lận phổ biến; phòng thi nộp bài an toàn không lộ điểm.

#### 📍 Buổi Báo Cáo 2 — Thứ 6 (Ngày 4 của Tuần 4)
* **🧑 Hoàng (FE Lead):**
  - **Task FE-4.4:** Tích hợp luồng streaming upload audio WebM lên Cloudflare R2 theo đúng quy chuẩn tên bắt buộc: **`STT_MSSV.webm`** kèm chuỗi mã băm SHA-256 niêm phong pháp lý.
* **🧑 Hải (FE Dev):**
  - **Task FE-4.5:** Hoàn thiện `WaveformPlayer.tsx` và `ScoreOverrideForm.tsx`.
  - **Nội dung demo:** Nghe lại audio tốc độ 1.0x, 1.25x, 1.5x; form điều chỉnh điểm bắt buộc nhập lý do giải trình; nút **"Khóa Điểm Một Chiều (One-Way Lock)"** chuyển trạng thái bài thi sang `is_locked = true` đóng băng toàn bộ tương tác.
* **Tiêu chí nghiệm thu (DoD):** Sau khi bấm Khóa điểm, nút bấm chuyển xám và không thể chỉnh sửa thêm; xuất file Excel FAP Khảo thí tải về đúng mẫu.

#### 📍 Buổi Báo Cáo 3 — Chủ Nhật (Ngày 6 của Tuần 4 — Final Gate Review & Bàn Giao)
* **Cả 2 thành viên:**
  - Chạy toàn bộ bộ kiểm thử tích hợp và E2E cho cả 4 Core Main Flows (MF-01 đến MF-04).
  - Lập báo cáo nghiệm thu `FINAL_TEST_REPORT.md` chứng minh 100% các ca kiểm thử then chốt đều đạt PASS.
  - Diễn tập demo toàn hệ thống trên môi trường thật, sẵn sàng bảo vệ thành công trước Hội đồng tốt nghiệp FPTU!

---

## 📋 4. KHUNG 6 TEST CASES BẮT BUỘC CHO TỪNG TÍNH NĂNG (REPORT 5 PREP)

Với mỗi tính năng hoàn thành, thành viên phụ trách phải chụp ảnh bằng chứng cho 6 kịch bản kiểm thử:
1. **TC-01 (Happy Path):** Thao tác chuẩn với dữ liệu hợp lệ (VD: Nói đủ 60s, nộp bài thành công).
2. **TC-02 (Boundary):** Giá trị biên tối thiểu/tối đa (VD: Trả lời đúng 10s hoặc chạm ngưỡng tối đa 120s).
3. **TC-03 (Validation):** Dữ liệu rỗng hoặc sai (VD: Bấm nộp khi chưa thu âm).
4. **TC-04 (Business Rule):** Vi phạm luật nghiệp vụ (VD: Tổng Rubric != 10.0, thi thử quá 3 lượt/ngày).
5. **TC-05 (Security/Auth):** Truy cập trái phép (VD: Sinh viên vào trang hậu kiểm).
6. **TC-06 (Exception/Network):** Mất kết nối mạng (VD: Rớt mạng khi đang nộp bài).
