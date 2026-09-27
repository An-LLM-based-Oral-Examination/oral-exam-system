# 🎨 FRONTEND — TASK EXECUTION GUIDE (v2 — ĐÃ SỬA SAU PHẢN BIỆN)
**Dành cho:** Lê Vũ Hoàng (FE Lead) & Phạm Nguyễn Đăng Hải (FE Developer)  
**Công nghệ:** React 19, TypeScript, Vite, Tailwind CSS, Shadcn/ui, SignalR Client, Web Speech API, Recharts.  
**Buffer Screen = 30 giây** (khớp Swimlane). Repo đã được khởi tạo sẵn — không cần `npm create vite`.

---

## 📁 1. CẤU TRÚC THƯ MỤC CẦN TRIỂN KHAI (`frontend/src/`)

```text
frontend/src/
├── assets/                  # Logo, icon SVG tĩnh
├── components/
│   ├── ui/                  # Button, Input, Modal, Toast (Shadcn)
│   ├── layout/              # Header.tsx, Sidebar.tsx, Footer.tsx
│   ├── practice/            # BufferScreen.tsx, ScorecardModal.tsx
│   ├── exam/                # VoiceFirstGate.tsx, CountdownTimer.tsx, BloomRadar.tsx
│   └── audit/               # AudioPlayer.tsx, ScoreOverrideForm.tsx
├── context/                 # AuthContext.tsx, ExamContext.tsx
├── hooks/
│   ├── useWebSpeech.ts      # TTS + STT cho MF-01/MF-02
│   ├── useSignalR.ts        # Kết nối PracticeHub
│   └── useKiosk.ts          # Bắt onblur, F12, chuột phải (MF-04)
├── pages/
│   ├── auth/                # LoginPage.tsx
│   ├── question-bank/       # QuestionListPage.tsx, CreateQuestionPage.tsx
│   ├── practice/            # PracticeSessionPage.tsx
│   ├── mock-exam/           # MockExamPage.tsx
│   └── lecturer/            # AuditPortalPage.tsx
├── services/
│   ├── api.ts               # Axios Interceptor (token + RFC 7807 error handler)
│   ├── authService.ts
│   ├── questionService.ts
│   └── examService.ts
└── types/
    ├── auth.types.ts
    ├── question.types.ts
    └── exam.types.ts
```

---

## 📦 2. THƯ VIỆN CẦN CÀI (`npm i`)
```bash
npm install react-router-dom axios react-hook-form zod @hookform/resolvers
npm install @microsoft/signalr recharts lucide-react clsx tailwind-merge
```

---

## 🔌 3. HỢP ĐỒNG GIAO TIẾP VỚI BACKEND (TÓM TẮT)
*(Chi tiết đầy đủ xem file `docs/API_CONTRACT_AND_INTEGRATION_GUIDE.md`)*

- **Base URL:** `/api/v1/`
- **Auth:** Gọi `POST /api/v1/auth/login` → nhận `{ accessToken, refreshToken }`. Đính kèm `Authorization: Bearer <token>` cho mọi request.
- **Lỗi:** BE luôn trả `ProblemDetails` (RFC 7807). FE đọc trường `detail` hiện Toast đỏ, đọc mảng `errors` highlight input lỗi.
- **Real-time:** SignalR Hub tại `/hubs/practice`. Lắng nghe event `ReceiveScorecard`.
- **HTTP 202 Accepted:** Khi FE gọi `POST /api/v1/practice/submit`, BE trả 202 ngay (bài đã vào hàng đợi). FE chờ SignalR trả điểm sau — không cần polling.

---

## 📋 4. CHECKLIST TASK CHI TIẾT TỪNG TUẦN

### 🏃 TUẦN 1: NỀN TẢNG UI & MF-03 (QUẢN LÝ ĐỀ & BAREM 10.0)

#### 🧑 Hoàng (FE Lead):
- [ ] **FE-1.1:** Cấu hình Tailwind CSS, Shadcn/ui, Prettier, ESLint, alias `@/` trong `tsconfig.json` và `vite.config.ts`.
- [ ] **FE-1.2:** Dựng `Sidebar.tsx` (Menu: Luyện tập, Thi thử, Ngân hàng đề, Hậu kiểm) và `Header.tsx` (hiện tên user, nút logout).
- [ ] **FE-1.3:** Tạo `AuthGuard.tsx` phân quyền 3 vai (`Student`, `Instructor`, `Admin`). Đọc role từ JWT decoded. Route `/question-bank/*` chỉ cho `Instructor/Admin`.
- [ ] **FE-1.4:** Dựng `LoginPage.tsx` (email + password). Gọi `POST /api/v1/auth/login`. Lưu token vào Session Storage. Chuyển hướng theo role.
- **Báo cáo:** Build 0 lỗi. Đăng nhập Student bị văng khỏi trang tạo đề. Đăng nhập Instructor vào được.

#### 🧑 Hải (FE Developer):
- [ ] **FE-1.5:** Dựng `QuestionListPage.tsx` gọi `GET /api/v1/questions` hiển thị bảng (Tên câu hỏi, Môn, Bloom Level, Ngày tạo).
- [ ] **FE-1.6:** Dựng `CreateQuestionPage.tsx` gồm:
  - Dropdown chọn Môn học (gọi `GET /api/v1/subjects`), chọn Chương, chọn Bloom Level.
  - Bảng tiêu chí Rubric động: Thêm/Xóa dòng (Tên tiêu chí + Điểm tối đa).
- [ ] **FE-1.7:** Áp Zod schema validate tổng điểm:
  ```ts
  z.object({
    criteria: z.array(criterionSchema).refine(
      items => items.reduce((sum, c) => sum + c.maxScore, 0) === 10,
      "Tổng điểm Rubric bắt buộc = 10.0"
    )
  })
  ```
  Nếu tổng != 10 → Disable nút Lưu + hiện chữ đỏ cảnh báo.
- **Báo cáo:** Nhập điểm lẻ (3.5 + 4.5 + 1.5 = 9.5) → nút Lưu mờ đi. Nhập đúng 10 → nút sáng lên.

---

### 🏃 TUẦN 2: MF-01 (LUYỆN TẬP & SIGNALR) + NÚT CHẤM THỬ AI

#### 🧑 Hoàng (FE Lead):
- [ ] **FE-2.1:** Viết hook `useWebSpeech.ts`:
  - `speak(text)`: `window.speechSynthesis.speak(new SpeechSynthesisUtterance(text))`.
  - `startListening()` / `stopListening()`: `new webkitSpeechRecognition()`, trả về `transcript` realtime.
- [ ] **FE-2.2:** Dựng `PracticeSessionPage.tsx`:
  - Nút "Nghe câu hỏi" (kích TTS).
  - Nút "Bắt đầu nói" / "Dừng nói" (icon Micro nhấp nháy đỏ khi đang thu).
  - Hiển thị văn bản nhận diện vào ô Transcript realtime.
- [ ] **FE-2.3:** Thêm nút **"Chấm thử bằng AI"** trên `CreateQuestionPage.tsx` (Tuần 1 chưa có Gemini Service, giờ mới gắn được). Gọi `POST /api/v1/questions/{id}/calibrate` → hiện kết quả AI chấm mẫu.
- **Báo cáo:** Web đọc to câu hỏi. Micro bắt giọng hiện chữ. Nút chấm thử trả điểm mẫu.

#### 🧑 Hải (FE Developer):
- [ ] **FE-2.4:** Dựng `BufferScreen.tsx`:
  - Đếm ngược **30 giây** (progress bar tụt lùi).
  - Cho phép SV gõ sửa lại từ vựng kỹ thuật tiếng Anh bị STT nhận sai (Code-Switching).
  - Hết 30s → tự động submit transcript hiện tại.
- [ ] **FE-2.5:** Viết hook `useSignalR.ts` kết nối `http://localhost:5265/hubs/practice`.
- [ ] **FE-2.6:** Dựng `ScorecardModal.tsx`:
  - Lắng nghe event `ReceiveScorecard` từ SignalR.
  - Hiển thị điểm AI, feedback nhận xét, nút "Luyện tập tiếp" / "Kết thúc".
- **Báo cáo:** Thanh 30s chạy mượt (requestAnimationFrame). Modal điểm bật lên khi BE bắn tín hiệu.

---

### 🏃 TUẦN 3: MF-02 (THI THỬ BẤM GIỜ & VOICE-FIRST)

#### 🧑 Hoàng (FE Lead):
- [ ] **FE-3.1:** Dựng `MockExamPage.tsx`.
- [ ] **FE-3.2:** Xây dựng `VoiceFirstGate.tsx`:
  - `<textarea disabled={!isRecordingFinished}>` kèm icon ổ khóa.
  - Click vào ô khi chưa nói → Toast Error: *"Bạn phải trả lời bằng giọng nói trước!"*.
  - Thu âm xong → ổ khóa mở → cho phép sửa text.
- **Báo cáo:** Cố gõ phím trước khi nói → bị chặn. Nói xong → ô mở ra.

#### 🧑 Hải (FE Developer):
- [ ] **FE-3.3:** Dựng `CountdownTimer.tsx`:
  - Đếm lùi thời gian toàn bài (vd: 15 phút) và thời gian từng câu (vd: 90 giây).
  - Thời gian đồng bộ với Server (lấy `startTime` + `maxDuration` từ BE, không dùng `Date.now()` thuần client).
  - Tự động gọi `handleSubmitExam()` khi hết giờ.
- [ ] **FE-3.4:** Dựng `BloomRadar.tsx` dùng `Recharts`:
  - Biểu đồ mạng nhện 6 trục: Nhớ, Hiểu, Vận dụng, Phân tích, Đánh giá, Sáng tạo.
  - Nhận data từ payload kết quả thi thử.
- **Báo cáo:** Đồng hồ về 0 → tự nộp bài. Radar chart hiện đẹp.

---

### 🏃 TUẦN 4: MF-04 (THI THẬT LAB KIOSK & CỔNG HẬU KIỂM)

#### 🧑 Hoàng (FE Lead):
- [ ] **FE-4.1:** Viết hook `useKiosk.ts`:
  - Bắt `window.addEventListener('blur', ...)` (Alt+Tab, đổi tab).
  - Chặn chuột phải: `document.addEventListener('contextmenu', e => e.preventDefault())`.
  - Chặn F12, Ctrl+C, Ctrl+V, Ctrl+Shift+I.
- [ ] **FE-4.2:** Giao diện cảnh báo vi phạm Kiosk:
  - Vi phạm 1 & 2: Banner đỏ cảnh báo.
  - Vi phạm lần 3: Tự niêm phong màn hình, gọi `POST /api/v1/exam/seal`.
- **Báo cáo:** Ấn F12 ba lần → màn hình đỏ báo đình chỉ thi.

#### 🧑 Hải (FE Developer):
- [ ] **FE-4.3:** Dựng `AuditPortalPage.tsx` (dành riêng Lecturer):
  - Danh sách bài thi SV theo phòng/số máy (gọi `GET /api/v1/audit/submissions`).
- [ ] **FE-4.4:** Dựng `AudioPlayer.tsx`:
  - Phát file `.webm` từ Cloudflare R2.
  - Timeline: Bấm timestamp → đoạn transcript tương ứng highlight vàng.
- [ ] **FE-4.5:** Dựng `ScoreOverrideForm.tsx`:
  - Ô sửa điểm từng tiêu chí (điểm AI đề xuất hiện sẵn, GV ghi đè được).
  - **Ô bắt buộc nhập lý do giải trình** (textarea `required`).
  - Nút "Lưu điểm" → gọi `PUT /api/v1/audit/override`.
  - Nút "Khóa vĩnh viễn" → gọi `POST /api/v1/audit/lock` (hiện confirm dialog trước khi khóa).
- **Báo cáo:** Tua audio → transcript highlight. Ghi đè điểm thành công. Khóa xong → nút mờ đi vĩnh viễn.
