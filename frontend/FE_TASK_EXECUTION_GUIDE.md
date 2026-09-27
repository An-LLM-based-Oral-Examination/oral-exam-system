# 🎨 FRONTEND IMPLEMENTATION & TASK EXECUTION GUIDE
**Dành cho:** Lê Vũ Hoàng (FE Lead) & Phạm Nguyễn Đăng Hải (FE Developer)  
**Công nghệ:** React 19, TypeScript, Vite, Tailwind CSS, Shadcn/ui, SignalR Client, Web Speech API, Recharts.

---

## 📁 1. CẤU TRÚC THƯ MỤC CẦN TRIỂN KHAI TRONG `frontend/src/`

```text
frontend/src/
├── assets/             # Logo, icon SVG tĩnh
├── components/         # Reusable UI components
│   ├── ui/             # Button, Input, Modal, Toast (Shadcn style)
│   ├── layout/         # Header.tsx, Sidebar.tsx, Footer.tsx
│   ├── practice/       # BufferScreen.tsx, ScorecardModal.tsx (MF-01)
│   ├── exam/           # VoiceFirstGate.tsx, CountdownTimer.tsx, BloomRadar.tsx (MF-02)
│   └── audit/          # AudioPlayer.tsx, ScoreOverrideForm.tsx (MF-04)
├── context/            # AuthContext.tsx, ExamContext.tsx
├── hooks/              # Custom hooks
│   ├── useWebSpeech.ts # TTS và STT wrapper (MF-01)
│   ├── useSignalR.ts   # Kết nối SignalR Hub PracticeHub
│   └── useKiosk.ts     # Bắt window onblur, F12, Right click (MF-04)
├── pages/              # Màn hình chính
│   ├── auth/           # LoginPage.tsx
│   ├── question-bank/  # QuestionListPage.tsx, CreateQuestionModal.tsx (MF-03)
│   ├── practice/       # PracticeSessionPage.tsx (MF-01)
│   ├── mock-exam/      # MockExamPage.tsx (MF-02)
│   └── lecturer/       # AuditPortalPage.tsx (MF-04)
├── services/           # Axios API instances & API calls
│   ├── api.ts          # Axios Interceptor bọc token & RFC 7807 error
│   ├── questionService.ts
│   ├── examService.ts
│   └── authService.ts
└── types/              # Type definitions
    ├── question.types.ts
    ├── exam.types.ts
    └── auth.types.ts
```

---

## 📦 2. CÁC THƯ VIỆN CẦN CÀI ĐẶT NGAY (`npm i`)
```bash
npm install lucide-react clsx tailwind-merge @microsoft/signalr recharts react-router-dom axios react-hook-form zod @hookform/resolvers
```

---

## 📋 3. CHECKLIST TRIỂN KHAI CHI TIẾT THEO TUẦN (SPRINTS)

### 🏃 TUẦN 1: NỀN TẢNG UI & MF-03 (QUẢN LÝ ĐỀ & BAREM 10.0)

#### 🧑 Hoàng (FE Lead):
- [ ] **Task FE-1.1:** Cài Tailwind CSS & cấu hình alias `@/` trong `tsconfig.json` và `vite.config.ts`.
- [ ] **Task FE-1.2:** Dựng `src/components/layout/Sidebar.tsx` và `Header.tsx` (Menu: Luyện tập, Thi thử, Ngân hàng đề, Hậu kiểm).
- [ ] **Task FE-1.3:** Tạo `src/components/AuthGuard.tsx` đọc `role` từ `localStorage` / Context (Chỉ cho role `Lecturer` vào trang tạo đề).
- [ ] **Task FE-1.4:** Dựng trang `src/pages/auth/LoginPage.tsx` (Form email, password, nút đăng nhập).

#### 🧑 Hải (FE Developer):
- [ ] **Task FE-1.5:** Dựng trang `src/pages/question-bank/QuestionListPage.tsx` hiển thị bảng danh sách câu hỏi.
- [ ] **Task FE-1.6:** Dựng Modal `CreateQuestionModal.tsx` gồm:
  - Input: Tiêu đề câu hỏi, Môn học (PRN231, SWD392...), Độ khó Bloom.
  - Bảng tiêu chí Rubric động (Thêm/Xóa dòng tiêu chí: Tên tiêu chí, Điểm tối đa).
- [ ] **Task FE-1.7:** Áp dụng Zod schema trong `CreateQuestionModal.tsx`:
  - Công thức: `z.object({ criteria: z.array(...).refine(items => items.reduce((sum, c) => sum + c.score, 0) === 10, "Tổng điểm Rubric bắt buộc = 10.0") })`.
  - Nếu tổng khác 10 -> Disable nút `Lưu câu hỏi` và hiện chữ đỏ cảnh báo.

---

### 🏃 TUẦN 2: MF-01 (LUYỆN TẬP ĐA PHƯƠNG THỨC & SIGNALR)

#### 🧑 Hoàng (FE Lead):
- [ ] **Task FE-2.1:** Viết hook `src/hooks/useWebSpeech.ts`:
  - `speak(text)`: Sử dụng `window.speechSynthesis` đọc câu hỏi.
  - `startListening()` / `stopListening()`: Sử dụng `window.webkitSpeechRecognition` bắt giọng nói và trả về `transcript`.
- [ ] **Task FE-2.2:** Dựng `src/pages/practice/PracticeSessionPage.tsx`:
  - Nút "Nghe lại câu hỏi" (kích hoạt TTS).
  - Nút "Bắt đầu nói" / "Dừng nói" có hiệu ứng sóng âm hoặc icon Micro nhấp nháy đỏ.
- [ ] **Task FE-2.3:** Hiển thị văn bản nhận diện được vào ô Transcript theo thời gian thực.

#### 🧑 Hải (FE Developer):
- [ ] **Task FE-2.4:** Dựng `src/components/practice/BufferScreen.tsx`:
  - Màn hình đệm 60s đếm ngược với progress bar.
  - Cho phép sinh viên gõ bàn phím sửa lại các từ vựng kỹ thuật tiếng Anh (Code-Switching) bị STT nhận sai.
- [ ] **Task FE-2.5:** Viết `src/hooks/useSignalR.ts` kết nối tới Backend URL `http://localhost:5265/hubs/practice`.
- [ ] **Task FE-2.6:** Dựng `src/components/practice/ScorecardModal.tsx`:
  - Lắng nghe event `ReceiveScorecard`.
  - Hiển thị điểm số AI chấm, feedback nhận xét và nút "Luyện tập tiếp" / "Kết thúc".

---

### 🏃 TUẦN 3: MF-02 (THI THỬ BẤM GIỜ & VOICE-FIRST GATE)

#### 🧑 Hoàng (FE Lead):
- [ ] **Task FE-3.1:** Dựng `src/pages/mock-exam/MockExamPage.tsx`.
- [ ] **Task FE-3.2:** Xây dựng chốt chặn `src/components/exam/VoiceFirstGate.tsx`:
  - Input text bị khóa `readOnly = true` kèm icon ổ khóa.
  - Chỉ khi người dùng bấm nút hoàn thành ghi âm Micro thì ổ khóa mới mở để cho phép gõ chỉnh sửa.
  - Nếu click vào ô trước khi nói -> bắn thông báo Toast: *"Bạn phải trả lời bằng giọng nói trước!"*.

#### 🧑 Hải (FE Developer):
- [ ] **Task FE-3.3:** Dựng `src/components/exam/CountdownTimer.tsx`:
  - Đếm lùi thời gian toàn bài (vd: 15 phút) và thời gian từng câu (vd: 90 giây).
  - Tự động gọi hàm `handleSubmitExam()` khi thời gian về 00:00.
- [ ] **Task FE-3.4:** Dựng `src/components/exam/BloomRadar.tsx` dùng thư viện `Recharts`:
  - Biểu đồ mạng nhện gồm 6 trục: Nhớ, Hiểu, Vận dụng, Phân tích, Đánh giá, Sáng tạo.
  - Đổ dữ liệu từ payload kết quả thi thử của Backend.

---

### 🏃 TUẦN 4: MF-04 (THI THẬT LAB KIOSK & CỔNG HẬU KIỂM)

#### 🧑 Hoàng (FE Lead):
- [ ] **Task FE-4.1:** Viết hook `src/hooks/useKiosk.ts`:
  - Bắt sự kiện `window.addEventListener('blur', ...)` khi sinh viên Alt+Tab hoặc chuyển tab.
  - Bắt sự kiện `document.addEventListener('contextmenu', e => e.preventDefault())` chặn chuột phải.
  - Bắt phím F12, Ctrl+C, Ctrl+V, Ctrl+Shift+I.
- [ ] **Task FE-4.2:** Tạo giao diện cảnh báo vi phạm Kiosk:
  - Vi phạm lần 1 & 2: Banner đỏ cảnh báo.
  - Vi phạm lần 3: Tự động niêm phong màn hình, gọi API đình chỉ thi.

#### 🧑 Hải (FE Developer):
- [ ] **Task FE-4.3:** Dựng trang `src/pages/lecturer/AuditPortalPage.tsx` dành riêng cho Giảng viên:
  - Danh sách bài thi của sinh viên theo số máy phòng Lab.
- [ ] **Task FE-4.4:** Dựng `src/components/audit/AudioPlayer.tsx`:
  - Trình phát âm thanh WebM phát bản ghi từ Cloudflare R2 / Backend.
  - Thanh timeline: Bấm vào timestamp nào thì đoạn transcript tương ứng của sinh viên được highlight vàng.
- [ ] **Task FE-4.5:** Form ghi đè điểm (`ScoreOverrideForm.tsx`):
  - Ô sửa điểm từng tiêu chí + Ô bắt buộc nhập lý do giải trình.
  - Nút "Xác nhận & Khóa điểm vĩnh viễn" (Gửi lệnh One-Way Lock).
