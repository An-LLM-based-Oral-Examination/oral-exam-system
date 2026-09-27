# BẢNG PHÂN CÔNG NHIỆM VỤ & LỘ TRÌNH AGILE 4 TUẦN (v2 — ĐÃ SỬA SAU PHẢN BIỆN)
**Dự án:** An LLM-based Oral Examination  
**Đội hình (2 FE — 2 BE):** Hoàng (FE Lead), Hải (FE), Thành (BE Lead), Tốt (BE/AI/QA).  
**Quy tắc:** Review tiến độ mỗi 3 ngày. Buffer screen = 30 giây (khớp Swimlane). Quota 3 lượt/môn/ngày kiểm soát bằng PostgreSQL.

---

## 👨‍💻 PHẦN 1: PHÂN CÔNG VAI TRÒ

### Lê Vũ Hoàng (FE Lead)
- Kiến trúc FE, Layout, Auth Guard, tích hợp Web Speech API (STT/TTS cho MF-01, MF-02).
- Kiosk Lockdown (MF-04 phía client).

### Phạm Nguyễn Đăng Hải (FE Developer)
- Component chức năng: Form Rubric (Zod validate tổng = 10), BufferScreen 30s, CountdownTimer, BloomRadar, Audio Player Audit Portal.
- SignalR client, Scorecard Modal.

### Nguyễn Quang Thành (Leader, BE)
- Kiến trúc Clean Architecture, **Auth API đăng nhập 3 vai (Student / Instructor / Admin)**, EF Core Migrations 11 bảng, Seed Data.
- Hàng đợi 4 tầng, SignalR Hub, Server-side Timer.
- **API tạo ca thi Lab** (session, room, gán SV vào số máy) — tiền đề bắt buộc cho MF-04.
- API khóa điểm One-Way Lock, xuất Excel FAP.

### Nguyễn Trọng Tốt (BE, AI & QA)
- CRUD Question + Rubric (ACID Transaction), CRUD Subject/Chapter.
- Gemini Service (CoT Prompt, Structured Output), Deep-dive A2.
- **Whisper STT** cho bài thi Lab (MF-04). Cloudflare R2 lưu audio, băm SHA-256.
- **API sửa điểm kèm lý do** (trước khi khóa). Lưu score + feedback AI xuống DB (một người sở hữu duy nhất).
- Unit Test, Regression Test, Test Report.

---

## 📅 PHẦN 2: LỘ TRÌNH 4 TUẦN

### TUẦN 1: NỀN TẢNG & MF-03
| Người | Công việc | Báo cáo |
|---|---|---|
| **Hoàng** | Cấu hình Tailwind, Shadcn, Prettier, ESLint (repo đã có sẵn). Layout (Sidebar, Header). Login Page. AuthGuard phân quyền 3 vai. | Build 0 lỗi. Đăng nhập đúng vai thì vào, sai thì văng. |
| **Hải** | Trang danh sách câu hỏi. Form tạo câu hỏi + Rubric (Zod: tổng != 10 → khóa nút Lưu). | Demo nhập 3.5 + 4.5 + 1.5 = 9.5 → nút mờ đi. |
| **Thành** | Clean Architecture, JWT Auth API (`POST /api/v1/auth/login`, trả token 3 role). EF Core 11 bảng + Migration + Seed (5 GV, 50 SV). OpenAPI Contract. | Swagger hiện đủ endpoint. DB hiện 11 bảng. Login trả token. |
| **Tốt** | CRUD Question + Rubric (ACID Transaction). CRUD Subject/Chapter cơ bản. FluentValidation tổng = 10. Unit Test (payload 9.0 → HTTP 422). | Test Runner 100% Pass. API tạo câu hỏi thành công trên Swagger. |

### TUẦN 2: MF-01 & AI CORE
| Người | Công việc | Báo cáo |
|---|---|---|
| **Hoàng** | Hook `useWebSpeech` (TTS đọc câu hỏi, STT thu âm). Trang Practice. Nút "Chấm thử bằng AI" trên form tạo câu hỏi (gọi Gemini Service của Tốt). | Web cất tiếng đọc, micro bắt giọng hiện chữ realtime. |
| **Hải** | BufferScreen **30 giây** đếm ngược. SignalR client (`useSignalR`). ScorecardModal pop-up khi nhận event. | Thanh 30s chạy mượt. Modal điểm bật lên khi có tín hiệu. |
| **Thành** | Hàng đợi 4 tầng (Bounded Channel 1000 slots, Polly 2s-4s-8s, DLQ). SignalR `PracticeHub`. API `POST /api/v1/practice/submit` → HTTP 202. API lưu Practice History. | Gửi 100 request → log hàng đợi nhả task. SignalR nhận message. |
| **Tốt** | GeminiService gọi Gemini 1.5 Flash. CoT Prompt 3 bước. Ép JSON Schema trả về `{score, feedback}`. Lưu score + feedback AI xuống DB (sở hữu duy nhất). | Log AI trả JSON thuần khiết. DB có bản ghi điểm. |

### TUẦN 3: MF-02 & CHUẨN BỊ MF-04
| Người | Công việc | Báo cáo |
|---|---|---|
| **Hoàng** | Trang Mock Exam. Voice-First Gate (khóa textarea cho tới khi thu âm xong). | Cố gõ phím trước khi nói → bị chặn. |
| **Hải** | CountdownTimer (sync server time, auto-submit khi hết giờ). BloomRadar 6 trục Recharts. | Đồng hồ về 0 → tự nộp bài. Radar chart hiện đẹp. |
| **Thành** | Quota Guard bằng **PostgreSQL** (đếm 3 lần/môn/ngày). Server-side Timer (từ chối nộp trễ > 10s). **API tạo ca thi Lab** (tạo session, room, gán SV vào số máy). | Thi lần 4 → HTTP 429. Nộp trễ → bị từ chối. Swagger hiện API tạo ca thi. |
| **Tốt** | AI Deep-dive A2 (điểm 4.0-8.0 → sinh câu hỏi đào sâu). Regression Test MF-01 + MF-02. Load test hàng đợi 4 tầng (tuần trước đã code xong). | Demo AI đẻ câu hỏi xoáy. Test Report không regression. |

### TUẦN 4: MF-04 & NGHIỆM THU
| Người | Công việc | Báo cáo |
|---|---|---|
| **Hoàng** | Kiosk Lockdown (`onblur`, chặn F12/Ctrl+C). 3 lần vi phạm → đình chỉ thi. | Ấn F12 3 lần → màn hình đỏ niêm phong. |
| **Hải** | Trang Audit Portal. Audio Player có timeline nhảy timestamp ↔ transcript. Form ghi đè điểm (bắt buộc nhập lý do). Nút "Khóa vĩnh viễn". | Tua audio → transcript highlight. Ghi đè điểm → gọi API sửa → gọi API khóa. |
| **Thành** | API khóa điểm One-Way Lock (`is_locked = true`). API xuất Excel FAP (EPPlus). | Postman sửa bài đã lock → HTTP 403. Excel tải về đúng format. |
| **Tốt** | **Whisper STT** bóc băng audio lab (MF-04). Cloudflare R2 lưu `STT_MSSV.webm` + SHA-256 niêm phong. **API sửa điểm kèm lý do** (`PUT /api/v1/audit/override`). Test Report UAT. | Sửa 1 byte audio → SHA-256 phát hiện. API override hoạt động. |
