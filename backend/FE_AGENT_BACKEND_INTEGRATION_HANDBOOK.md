# 🧭 SỔ TAY TÍCH HỢP BACKEND CHO FE-AGENT & ĐỘI NGŨ FRONTEND (FE-AGENT BACKEND INTEGRATION HANDBOOK)

> **Dự án:** Hệ Thống Luyện Thi & Đánh Giá Vấn Đáp Bằng LLM Cho Ngành Kỹ Thuật Phần Mềm (**FA26SE166** — FPTU SG, SEP490)  
> **Đối tượng đọc:** AI Agents phụ trách Frontend (`frontend/**`) và đội ngũ phát triển Frontend (**Thành**: Admin FE + MF-05 FE; **Hoàng**: Core SPA + MF-01 + Web Kiosk/Proctor + Evidence Panel + Auth + Notifications; **Hải**: PortalChrome + Student Dashboard/History + MF-02 + Trưởng BM MF-03 + Khảo thí/Phúc khảo MF-04).  
> **Nguồn sự thật mã nguồn:** Trích xuất trực tiếp 100% từ mã nguồn Backend `.NET 8` đã biên dịch sạch (`Release -warnaserror`, `3,048 / 3,048` unit tests PASS, thẩm định Red-Team độc lập đạt `[VERDICT: PASS]`).  
> **Tài liệu liên kết trong kho Git:** [AGENTS.md](../AGENTS.md) · [backend/AGENTS.md](./AGENTS.md) · [frontend/AGENTS.md](../frontend/AGENTS.md) · [QUESTION_APPROVAL_API.md](./QUESTION_APPROVAL_API.md) · [SHIFT_MANAGEMENT_API.md](./SHIFT_MANAGEMENT_API.md)

---

## 1. BỨC TRANH KIẾN TRÚC TOÀN HỆ THỐNG (SYSTEM MENTAL MODEL)

### 1.1. Hai Phân Hệ Client & Backend 4 Tầng
```text
┌────────────────────────────────────────────────────────────────────────────┐
│ CLIENT TIER                                                                │
│  1. Web SPA (frontend/ — React 19 + Vite + Router v7 + Tailwind v4 :3000)  │
│     • Phục vụ đủ 6 vai trò RBAC theo chuẩn UI Calm Academic Depth          │
│     • OfficialExamPage.tsx đóng vai trò Web Kiosk Prototype/Fallback MF-04 │
│  2. Desktop Kiosk (OralExamination.DesktopKiosk — Target BYOD App MF-04)   │
│     • Khóa vé theo thiết bị (X-Device-Id + X-Kiosk-Session-Token)          │
│     • Heartbeat định kỳ + App-Scoped Anti-Cheat + Upload R2 STT_MSSV.webm  │
└───────────────────────────┬───────────────────────────────┬────────────────┘
                            │ REST /api/v1/* (RFC 7807)     │ SignalR WSS
                            ▼                               ▼
┌────────────────────────────────────────────────────────────────────────────┐
│ BACKEND (.NET 8 Clean Architecture — backend/ — Port 5000)                 │
│  • API Layer: 30 REST Controllers đang hoạt động (+1 NonController shim)   │
│               3 SignalR Hubs (/hubs/practice, /hubs/in-class-shift,        │
│               /hubs/anti-cheat) · 5 Background Workers                     │
│  • Application Layer: CQRS MediatR + FluentValidation + DeviceGuard        │
│  • Infrastructure Layer: EF Core 8 (41 DbSet / PostgreSQL 16 + pg_trgm)    │
│    Polly Resilience (Gemini, Whisper, GoogleTts độc lập) · Cloudflare R2   │
│    OneWayLockInterceptor · PAdES PKI PDF Signer · ClosedXML Excel Exporter │
│  • Domain Layer: 38 bảng nghiệp vụ 3NF + 3 bảng bổ trợ                     │
└────────────────────────────────────────────────────────────────────────────┘
```

### 1.2. Dòng Dữ Liệu Học Thuật Xuyên Suốt
`Học kỳ (Semester)` → `Chương trình (AcademicProgram)` → `Môn học (Course)` & `CourseOffering` → `Trưởng BM (department_head)` → `Phân công GV (course_lecturers)` → `CourseCLO (3NF)` & `Rubric (Σ = 10.00)` → **2 Kho Đề Cô Lập (R8)**:
- **Kho `practice_questions` (Zero-R2 — chỉ lưu text/transcript):** Dùng chung cho **MF-01** (Luyện tập), **MF-02** (Thi thử có hạn ngạch GMT+7) và **MF-05** (Ca thi trong lớp TOTP 5 phút).
- **Kho `exam_questions` (Bảo mật — lưu audio R2 `STT_MSSV.webm` + SHA-256):** Chỉ dùng cho **MF-04** (Thi chính thức phòng Lab, Đề mẫu + $N$ bộ đề + Lọc trùng nạp kho kỳ sau + Hậu kiểm Evidence Panel + Công bố điểm One-Way Lock + Phúc khảo 5 bước).

### 1.3. Năm Background Workers Đang Chạy Song Song
| Worker | Trách nhiệm | Tác động trực tiếp tới FE |
|:---|:---|:---|
| `GradingQueueWorker` | Chấm ngầm MF-01 (`Practice`) & MF-05 (`InClassShifts`), `MaxDegreeOfParallelism = 12`, đẩy sự kiện qua `PracticeHub` & `InClassShiftHub`. | FE nhận `202 Accepted` (<100ms), hiển thị trạng thái *Đang chấm AI* và lắng nghe SignalR (kèm fallback polling). |
| `OfficialExamGradingDispatcher` | Đẩy bài nộp MF-04 (`SUBMITTED`) từ DB (`grading_jobs`) vào kênh chấm song song. | Thí sinh Kiosk **không bao giờ thấy điểm ngay** (0% hiện điểm tại phòng thi). |
| `OfficialExamGradingWorker` | Chấm bài MF-04 (câu chính + câu phụ), tính `confidence_score`, cờ `is_suspicious`, chuyển trạng thái vé sang `AI_GRADED` mà không ghi đè vé đã `AUDITED`/`PUBLISHED`/`LOCKED`. | Giám khảo mở Evidence Panel thấy danh sách Nhóm 1 (nghi ngờ / confidence < 0.70) & Nhóm 2. |
| `OfficialExamDlqReplayWorker` | Quét `dead_letter_queues` định kỳ **5 phút/lần** để tự phục hồi tác vụ MF-04 (tối đa 9 lần trước khi `abandoned`, tự `resolved` nếu vé đã `PUBLISHED`/`LOCKED`). | Khảo thí xem & bấm replay/resolve tại `/examination-officer/dlq`; Admin xem MF-01/02/05 tại `/admin/dlq`. |
| `InClassShiftAutoCloseWorker` | Tự đóng các ca MF-05 quá `end_time`. | Trạng thái ca tự chuyển `CLOSED`; GV vẫn hậu kiểm & bấm Chốt sổ (`FINALIZED`). |

---

## 2. XÁC THỰC KÉP, ÁNH XẠ 6 ROLES & HỢP ĐỒNG LỖI RFC 7807

### 2.1. Bảng Ánh Xạ Vai Trò BE ↔ FE (Cực Kỳ Quan Trọng — Tránh Nhầm `lecturer` vs `Instructor`)
Backend lưu và phát trong JWT Claim `role` bằng **`snake_case`**, còn React `AuthContext` đang ánh xạ sang **`PascalCase`**:

| Backend JWT / DB `role` | Frontend `AuthContext` Role | Cổng mặc định trên FE (`AppRoutes.tsx`) | Phạm vi sở hữu nghiệp vụ |
|:---|:---|:---|:---|
| `student` | `Student` | `/student/overview` | Lớp học, MF-01, MF-02, MF-05, Thi thật MF-04 (`/exam`), Lịch sử & Phúc khảo (`/results`) |
| `lecturer` | `Instructor` | `/lecturer/dashboard` | Lớp học & MF-05 (`Class.LecturerId`), Soạn câu hỏi & Bộ đề MF-03, Giám khảo hậu kiểm & chấm phúc khảo |
| `department_head` | `DepartmentHead` | `/department-head/dashboard` | CLO 3NF, Phân công GV, `ExamStructure`, Đề mẫu & Duyệt/Bàn giao bộ đề MF-03, Tổng hợp lọc kho kỳ sau, Follow-up MF-04 |
| `examination_officer` | `ExaminationOfficer` | `/examination-officer/dashboard` | Kỳ thi `FE`/`RE`, Import DSSV, Chia phòng cân bằng ca cuối, Mở khóa thiết bị BYOD, Công bố điểm + Excel/PDF PKI, Điều phối Phúc khảo 5 bước, DLQ MF-04 |
| `proctor` | `Proctor` | `/proctor/attendance` | **CHỈ** điểm danh ghế `1..maxSeats`, kiểm tra thẻ SV/CCCD và báo cáo vi phạm lên `AntiCheatHub` |
| `admin` | `Admin` | `/admin/dashboard` | Người dùng, Học kỳ, Chương trình, Môn học, Course Offerings, `system_configs` MF-01, Quota MF-02, Notifications, Audit Logs, Runtime/Abuse Diagnostics, DLQ MF-01/02/05 |

> [!CAUTION]
> **Giám khảo (`Examiner`) KHÔNG phải role thứ 7:** Giám khảo chỉ là Giảng viên (`lecturer`) được gán `examiner_user_id` trên ca thi MF-04 hoặc `regrade_lecturer_id` trên đơn phúc khảo.

### 2.2. Chuẩn Xác Thực Kép, Cookie HttpOnly & Thu Hồi Phiên Tức Thì
- **Transport:** `apiClient` (`frontend/src/services/api.client.ts`) đã bật `withCredentials: true`, tự gắn `Authorization: Bearer <accessToken>` từ `sessionStorage`, và dùng Single-Flight Refresh + `BroadcastChannel`/Web Locks để xoay vòng token an toàn.
- **Google OAuth 2.0 (Chống Replay ID-Token):**
  1. Gọi `POST /api/v1/auth/google-challenge` → nhận `{ nonce, expiresIn }` + trình duyệt tự lưu HttpOnly nonce cookie.
  2. Gắn `nonce` vào Google Identity Services → gửi `POST /api/v1/auth/google-login` với `{ idToken }`.
  3. BE cấp `accessToken` (JWT 15 phút) trong JSON và gắn **HttpOnly cookie `oes_refresh_session`** (`SameSite=Strict`, `Path=/api/v1/auth`).
- **Tài khoản nhà trường cấp:** `POST /api/v1/auth/password-login` (`{ username, password }`) → trả `{ accessToken, tokenType, expiresIn, mustChangePassword, user }`.
  - Nếu `mustChangePassword === true`: mọi API nghiệp vụ khác sẽ bị `MustChangePasswordMiddleware` chặn **`403 Forbidden`** kèm `errorCode = "MUST_CHANGE_PASSWORD"`. FE bắt buộc điều hướng sang `/change-password` và gọi `POST /api/v1/auth/change-password`.
- **Thu hồi phiên tức thì (`auth_version`):** Khi Admin khóa user / đổi role / reset mật khẩu hoặc user đổi mật khẩu, `users.auth_version` tăng lên; mọi JWT/Refresh Token cũ lập tức nhận `401 Unauthorized`.
- **Xung đột xoay vòng Refresh Token (`409 Conflict`):** Khi 2 tab gọi `POST /api/v1/auth/refresh-token` đồng thời trên cùng refresh token, tab thua lock nhận `409 Conflict` (BE KHÔNG xóa cookie `oes_refresh_session` ở nhánh `409` để tab thắng tiếp tục phiên).

### 2.3. Bảng Ánh Xạ HTTP Status Code (RFC 7807 `ProblemDetails`) → 7 Trạng Thái UI
Mọi lỗi BE đều trả chuẩn JSON `{ type, title, status, detail, errors?, errorCode?, traceId? }`:

| HTTP Status | Ý nghĩa thực tế từ Backend | Hành vi FE chuẩn (*Calm Academic Depth*) |
|:---:|:---|:---|
| `200` / `201` / `204` | Thành công đồng bộ | Hiện toast ngắn gọn / cập nhật state lạc quan có kiểm soát. |
| `202 Accepted` | Persist-First thành công (<100ms), đã đưa vào hàng đợi chấm AI | Chuyển thẻ bài nộp sang trạng thái *Đang chấm AI*, lắng nghe SignalR + polling fallback. |
| `400 Bad Request` | Sai cú pháp JSON, thiếu trường bắt buộc, `X-Device-Id` sai regex `^[A-Za-z0-9._:-]{16,128}$`, `audioLevelDb` ngoài `[0..150]` | Hiện `detail` tiếng Việt + highlight field trong `errors`. |
| `401 Unauthorized` | Hết hạn JWT hoặc `auth_version` đã đổi | `apiClient` tự thử refresh 1 lần; nếu vẫn 401 → đăng xuất sạch về `/login`. |
| `403 Forbidden` | Sai RBAC, chưa `enrolled` lớp active (Enrollment Gate), `MUST_CHANGE_PASSWORD`, hoặc đụng **One-Way Lock (`is_locked = true`)** | Nếu `MUST_CHANGE_PASSWORD` → ép chuyển `/change-password`; ngược lại hiện trạng thái *Không có quyền / Bảng điểm đã khóa cứng*. |
| `404 Not Found` | Tài nguyên không tồn tại hoặc bị ẩn ngoài phạm vi môn/lớp được phân công | Hiện Empty/NotFound state kèm nút quay lại danh sách. |
| `409 Conflict` | Xóa CLO/Rubric đang được tham chiếu; trùng đề mẫu duy nhất (`uq_exam_sets_active_sample_structure`); thiếu `ExamStructure` MF-02; sai thiết bị/phiên Kiosk (`DEVICE_MISMATCH`, `ACTIVE_SESSION_CONFLICT`, `DEVICE_IN_USE_OTHER_TICKET`); replay DLQ đang chạy trong vòng 2 phút; vi phạm `ShiftMutationGuard`; gửi anti-cheat trên vé đã kết thúc | Hiển thị banner cảnh báo rõ lý do xung đột (với Kiosk `DEVICE_MISMATCH` → khóa màn hình thi và hướng dẫn báo Giám thị / Khảo thí mở khóa thiết bị). |
| `410 Gone` | Phiên MF-01 quá 5 phút không tương tác hoặc phiên MF-02 hết giờ | Khóa nhập câu trả lời, chuyển sang màn tổng kết / giữ nguyên điểm các câu đã làm. |
| `413` / `415` | Ảnh câu hỏi vượt `5MB` hoặc sai Magic Bytes (`PNG`/`JPG`/`WEBP`) | Báo lỗi ngay dưới input upload ảnh, giữ nguyên form đang soạn. |
| `422 Unprocessable Entity` | Vi phạm bất biến nghiệp vụ: Rubric $\sum \neq 10.00$ hoặc vượt 2 chữ số thập phân; `sampleAnswer < 50` ký tự; tỷ lệ câu vàng bộ đề `> 70%`; câu hỏi trùng đề đã duyệt cùng đợt; MF-05 `selectedCloIds.length > totalQuestionsPerStudent` hoặc thiếu câu trong pool; quá hạn phúc khảo 3 ngày | Highlight đúng vi phạm bằng `detail` trả về từ BE, không xóa dữ liệu form của người dùng. |
| `429 Too Many Requests` | Hết hạn ngạch thi thử MF-02 theo môn trong ngày (GMT+7) hoặc vượt Rate Limit Auth/Public | Đọc `Retry-After` (nếu có), vô hiệu hóa nút bấm và hiển thị thời điểm được thử lại. |
| `502` / `504` | Lỗi/timeout dịch vụ upstream AI/STT sau Polly Retry (2s–4s–8s) | Thông báo lỗi thân thiện kèm nút **Thử lại** (không làm mất transcript đang giữ trên client). |

---

## 3. BẢN ĐỒ TOÀN BỘ 30 CONTROLLERS BACKEND (API SURFACE ATLAS)

> Ghi chú: `OfficialExamCheckInController` có gắn `[NonController]` (chỉ giữ tương thích tham chiếu unit test); mọi route `tickets/current` & `tickets/{ticketId}/check-in` thực tế đi qua `OfficialExamsController`.

### 3.1. Xác thực, Thông báo, Tiện ích & Công khai
| Controller | Base Route | Endpoints chính | Roles |
|:---|:---|:---|:---|
| `AuthController` | `/api/v1/auth` | `GET me`, `POST google-challenge`, `POST google-login`, `POST password-login`, `POST refresh-token`, `POST sign-out`, `POST change-password` | Public / Authenticated |
| `NotificationsController` | `/api/v1/notifications` | `GET /`, `PUT\|POST\|PATCH {id}/read`, `PUT\|POST read-all` | Authenticated |
| `SpeechController` | `/api/v1/speech` | `GET voices` (Public), `GET\|POST tts`, `POST stt`, `GET edge-cooldown/status`, `POST edge-cooldown/reset` | Authenticated (cooldown: `admin,department_head`) |
| `StorageController` | `/api/v1/storage` | `POST upload-audio` | Authenticated |
| `VerificationController` | `/api/v1/verification` | `GET grades/{certificateHash}` (tra cứu QR bảng điểm PDF ký số PKI, rate-limited `PublicVerifyPolicy`) | **AllowAnonymous** |

### 3.2. Quản trị Hệ thống & Đào tạo (Admin, Department Head, Academic)
| Controller | Base Route | Endpoints chính | Roles |
|:---|:---|:---|:---|
| `AdminController` | `/api/v1/admin` | `GET\|POST users`, `PUT users/{id}`, `PUT\|PATCH users/{id}/status`, `PUT\|PATCH users/{id}/role`, `POST users/{id}/reset-password`, `GET\|POST notifications`, `DELETE notifications/{id}`, `GET\|PUT system-configs` (+ `/api/v1/system-configs/{key}`), `GET audit-logs`, `GET diagnostics/runtime` | `admin` |
| `AbuseDiagnosticsController` | `/api/v1/diagnostics/abuse` | `GET /` (thống kê chặn rate-limit/abuse) | `admin` |
| `AcademicProgramsController` | `/api/v1/academic-programs` | `GET /` (Authenticated), `POST /`, `PUT {id}`, `PUT {id}/department-head` | `admin` (write) |
| `SemestersController` | `/api/v1/semesters` | `GET /`, `POST /`, `PUT {id}`, `GET {id}/course-offerings`, `PUT {id}/course-offerings`, `GET classes` | `admin` / `department_head` / `lecturer` / `examination_officer` |
| `CoursesController` | `/api/v1/courses` | `GET /`, `GET /api/v1/admin/courses`, `POST /`, `PUT {id}`, `GET\|PUT {id}/configurations`, `GET {id}/clos`, `GET managed-for-clos`, `POST\|PUT\|DELETE {id}/clos/{cloId}` (chặn xóa 409 nếu đã tham chiếu), `POST {id}/assign-department-head`, `GET\|POST {id}/lecturers` | `admin` / `department_head` / `lecturer` |
| `CourseOfferingsController` | `/api/v1/course-offerings` | `GET\|PUT {id}/lecturers`, `GET {id}/lecturer-directory`, `GET /api/v1/lecturer/course-offerings` | `department_head` / `lecturer` |
| `ClassesController` | `/api/v1` | `GET\|POST lecturer/classes`, `POST lecturer/course-offerings/{courseOfferingId}/classes`, `GET student/classes/available`, `GET classes/{id}`, `PUT lecturer/classes/{id}/password`, `POST student/classes/join`, `GET student/classes/my-classes`, `GET lecturer/classes/students/lookup`, `POST\|DELETE lecturer/classes/{classId}/students` | `lecturer` / `department_head` / `student` |
| `LecturerController` | `/api/v1/lecturer` | `GET dashboard` (+ `/api/v1/dashboard/lecturer`), `GET courses` | `lecturer,department_head` |
| `StudentController` | `/api/v1/student` | `GET dashboard`, `GET official-exams/{ticketId}/grade`, `POST official-exams/{ticketId}/acknowledge-grade` | `student` |
| `DeadLetterQueueController` | `/api/v1/admin/dlq`, `/api/v1/examination-officer/dlq`, `/api/v1/dlq` | `GET tasks`, `POST tasks/{id}/replay`, `POST tasks/{id}/resolve` | `admin` (MF-01/02/05), `examination_officer` (MF-04) |

### 3.3. Năm Luồng Nghiệp Vụ Cốt Lõi (`MF-01` .. `MF-05`)
| Luồng | Controller | Base Route | Endpoints chính |
|:---|:---|:---|:---|
| **MF-01** | `PracticeController` | `/api/v1/practice` | `POST sessions`, `GET sessions/{sessionId}`, `POST sessions/{sessionId}/next-question`, `POST sessions/{sessionId}/answers`, `POST sessions/{sessionId}/batch-answers`, `POST sessions/{sessionId}/complete`, `GET student/history` |
| **MF-02** | `MockExamsController` | `/api/v1/mock-exams` | `GET quota`, `POST sessions`, `POST sessions/{id}/answers`, `POST sessions/{id}/pause`, `POST sessions/{id}/resume`, `POST sessions/{id}/submit`, `GET history`, `GET history/{sessionId}` |
| **MF-03** | `QuestionsController`, `RubricsController`, `FlmController`, `QuestionReviewRequestsController`, `LecturerQuestionReviewRequestsController`, `ExamStructuresController`, **`OfficialExamCurationController`** | `/api/v1/questions`, `/api/v1/rubrics`, `/api/v1/exam-structures`, `/api/v1/official-exams` | Upload ảnh (`POST upload-image`), Sinh AI từ CLO (`POST generate-from-clo`), CRUD Draft/Approved, Batch Submit/Approve, **CRUD Rubric đầy đủ (`GET/POST/PUT/DELETE /api/v1/rubrics`)**, **ExamStructures M3 (`requiredSetCount`, `slotDefinitions`)**, **MF-03 M3 Đề mẫu / Chọn câu nhãn vàng / Trả về sửa / Tổng hợp lọc kho kỳ sau** |
| **MF-04** | `OfficialExamsController`, `OfficialExamSubmissionsController`, **`OfficialExamKioskController`**, `ProctorController`, `AppealsController` | `/api/v1/official-exams`, `/api/v1/proctor`, `/api/v1/appeals` | Kỳ thi `FE`/`RE` 6 bước (`eligible-courses`, `sessions/batch`, `candidates/import/preview\|commit`, `enrollment-estimate`, `shifts/batch`, `auto-allocate-students`, `distribute-tickets`, `publish`), Check-in khóa BYOD, **Kiosk Heartbeat (`POST tickets/{ticketId}/heartbeat`)**, Audio presigned R2 + Submit câu chính/phụ, Anti-Cheat, Proctor điểm danh/vi phạm, Hậu kiểm Evidence Panel (`override-score`, `lock`), `publish-grades` (One-Way Lock), Export `.xlsx`/`.pdf` PKI, Phúc khảo 5 bước |
| **MF-05** | `InClassShiftsController` | `/api/v1/in-class-shifts` | `GET pool-stats`, `POST /`, `PUT\|DELETE {id}`, `POST {id}/activate\|close`, `GET {id}/current-password` (TOTP 5p + `grace_until` 60s), `POST {id}/join\|resume\|submit-answer\|finish`, `POST {id}/submissions/{submissionId}/next-followup`, `GET {id}/submissions`, `PUT {id}/submissions/{submissionId}/grade`, `POST {id}/finalize-grades` (`FINALIZED` + One-Way Lock), `GET {id}/results` |

---

## 4. ĐẶC TẢ CHI TIẾT CÁC HỢP ĐỒNG BACKEND MỚI & ĐÃ NÂNG CẤP (PR-1 → PR-6)

Đây là các endpoint & trường DTO **mới hoàn thiện trên Backend** mà FE-Agent cần đọc kỹ để nối vào giao diện và service layer.

### 4.1. MF-03 M3: Cấu Trúc Đề, Đề Mẫu, Soạn $N$ Bộ Đề, Nhãn Vàng ≤ 70%, Cảnh Báo Đỏ & Lọc Trùng Kho Kỳ Sau

#### A. Nâng cấp Cấu trúc Đề (`ExamStructuresController` — `/api/v1/exam-structures`)
- **Quyền đọc (`GET /api/v1/exam-structures`, `GET /api/v1/exam-structures/{id}`):**
  - `admin` & `examination_officer`: xem toàn hệ thống.
  - `department_head`: chỉ xem các môn mình phụ trách (`courses.department_head_user_id` hoặc `academic_programs.department_head_user_id`).
  - `lecturer`: chỉ xem các môn mình được phân công giảng dạy (`course_lecturers` active).
- **Quyền ghi (`POST`, `PUT` — chỉ `department_head` phụ trách môn):**
  - Bổ sung 2 trường M3 trong Request & Response:
    - `requiredSetCount`: `number` (`1..20`, mặc định `1`) — số lượng bộ đề cần soạn $N$.
    - `slotDefinitions`: `string` (JSON array mô tả ma trận từng slot câu hỏi, mặc định `"[]"`; BE xác thực cú pháp JSON array hợp lệ).
  - BE luôn kiểm tra tổng `bloomDistribution` khớp `totalQuestions` trên cả `POST` và `PUT`.

```json
// POST /api/v1/exam-structures  hoặc  PUT /api/v1/exam-structures/{id}
{
  "courseId": "guid",
  "name": "Cấu trúc FE SWP391 Fall 2026",
  "totalQuestions": 4,
  "durationMinutes": 20,
  "bloomDistribution": "{\"Understand\":1,\"Apply\":2,\"Analyze\":1}",
  "isActive": true,
  "requiredSetCount": 6,
  "slotDefinitions": "[{\"slotIndex\":1,\"cloCode\":\"CLO1\",\"bloomLevel\":\"Understand\",\"difficulty\":\"easy\"}]"
}
```

#### B. Lấy danh sách câu hỏi khả dụng để soạn Bộ đề (`GET /api/v1/official-exams/structures/{structureId}/available-questions`)
- **Roles:** `lecturer`, `department_head` (phải thuộc phạm vi môn học của `structureId`, nếu không → `403`).
- **Query params tùy chọn:** `cloId`, `difficulty` (`easy|medium|hard`), `bloomLevel`, `search`.
- **Quy tắc lọc tự động phía BE (FE không cần tự lọc lại):**
  1. Chỉ lấy câu `exam_questions` (`APPROVED`, `is_active = true`, `is_deleted = false`) có `bank_curation_status IN ('BANK_READY', 'CURATED_INTO_BANK')` (hoặc câu `PENDING_CURATION` do **chính actor đó tạo** — riêng tư).
  2. **Ẩn tuyệt đối** mọi câu đang nằm trong **Đề mẫu** (`is_sample_set = true`) của cấu trúc đề này.
  3. **Ẩn tuyệt đối** mọi câu đã nằm trong bộ đề được duyệt (`OFFICIAL` hoặc `HANDED_OVER`) của cấu trúc đề này trong đợt hiện tại.
  4. **Loại trừ nội bộ GV:** Ẩn mọi câu kho mà **chính GV đang đăng nhập** đã chọn ở các bộ đề trước (`DRAFT`, `SUBMITTED_FOR_REVIEW`, `OFFICIAL`, `HANDED_OVER`, `REJECTED` active) thuộc cùng cấu trúc đề.
  5. **Nhãn vàng ẩn danh:** Nếu câu kho đang có mặt trong bộ đề `SUBMITTED_FOR_REVIEW` của **GV khác**, BE trả `isYellowPendingByOtherLecturer: true` và `yellowWarningLabel: "Đã có người chọn, đang chờ duyệt"` (**tuyệt đối ẩn danh tính GV kia**).

```json
// Response 200 OK (AvailableExamSetQuestionsResponseDto)
{
  "structureId": "guid",
  "courseId": "guid",
  "courseCode": "SWP391",
  "requiredSetCount": 6,
  "yellowThresholdPercent": 70,
  "totalAvailable": 42,
  "yellowCount": 5,
  "items": [
    {
      "questionId": "guid",
      "title": "Phân tích kiến trúc Clean Architecture",
      "content": "...",
      "difficulty": "medium",
      "bloomLevel": "Analyze",
      "cloId": "guid",
      "cloCode": "CLO2",
      "rubricId": "guid",
      "source": "manual",
      "approvalStatus": "APPROVED",
      "bankCurationStatus": "BANK_READY",
      "isOwnQuestion": false,
      "isYellowPendingByOtherLecturer": true,
      "yellowWarningLabel": "Đã có người chọn, đang chờ duyệt",
      "imageUrl": null,
      "imageDescription": null,
      "createdAt": "2026-11-24T08:00:00Z"
    }
  ]
}
```

#### C. Tạo Đề mẫu hoặc Bộ đề Thi thật kèm Câu tạo tay inline (`POST /api/v1/official-exams/exam-sets`)
- **Roles:** `lecturer`, `department_head`.
- **Quy tắc mới quan trọng:**
  - **Đề mẫu (`isSampleSet: true`):** Chỉ `department_head` mới được tạo; mỗi `structureId` chỉ có **tối đa 1 Đề mẫu active** (khóa cả ở ứng dụng lẫn PostgreSQL partial unique index `uq_exam_sets_active_sample_structure` → tạo trùng trả `409 Conflict`).
  - **Câu tạo tay riêng tư (`inlineManualQuestions`):** GV có thể tạo mới trực tiếp câu hỏi trong lúc soạn bộ đề. BE tự gắn `bank_curation_status = "PENDING_CURATION"` (GV khác không nhìn thấy), yêu cầu `sampleAnswer` tối thiểu **50 ký tự** (`422` nếu thiếu) và Rubric $\sum \equiv 10.00$ đúng tới 2 chữ số thập phân (`decimal.Round(maxScore, 2) == maxScore`).
  - **Chống BOLA câu draft:** GV không thể nhét câu hỏi `DRAFT`/`SUBMITTED_FOR_REVIEW` của GV khác vào bộ đề (`403 Forbidden`).
  - **Kiểm tra phạm vi môn:** Nếu truyền `sessionId`, `session.CourseId` bắt buộc khớp `courseId` (`400`/`422`).
  - **Kiểm tra lặp nội bộ & câu đề mẫu:** Cấm chọn câu Đề mẫu, câu đã duyệt đợt này, câu `EXCLUDED_FROM_BANK`, hoặc câu chính GV đã dùng ở bộ đề trước cùng cấu trúc (`422`).

```json
// Request POST /api/v1/official-exams/exam-sets
{
  "structureId": "guid",
  "courseId": "guid",
  "sessionId": null,
  "setCode": "SWP391-FA26-SET01",
  "isSampleSet": false,
  "questionIds": ["guid-1", "guid-2"],
  "inlineManualQuestions": [
    {
      "title": "Xử lý deadlock trong EF Core",
      "content": "Trình bày nguyên nhân và cách phòng tránh deadlock khi 2 giao dịch cập nhật chéo bảng...",
      "sampleAnswer": "Câu trả lời mẫu chi tiết tối thiểu 50 ký tự giải thích cơ chế khóa tài nguyên theo thứ tự nhất quán và advisory lock...",
      "keyPoints": ["Thứ tự khóa nhất quán", "Advisory lock"],
      "difficulty": "hard",
      "bloomLevel": "Evaluate",
      "cloId": "guid-clo",
      "rubric": {
        "name": "Rubric Deadlock",
        "description": null,
        "totalMaxScore": 10.0,
        "criteria": [
          { "criterionName": "Phân tích nguyên nhân", "maxScore": 5.0, "weight": 0.5, "bloomLevel": "Analyze", "description": "...", "orderIndex": 1 },
          { "criterionName": "Đề xuất giải pháp", "maxScore": 5.0, "weight": 0.5, "bloomLevel": "Evaluate", "description": "...", "orderIndex": 2 }
        ]
      }
    }
  ]
}
```

#### C2. Chỉnh sửa & Xóa Bộ đề ở trạng thái `DRAFT` / `REJECTED` (MỚI — Hardening PR 2)
1. **Cập nhật bộ đề (`PUT /api/v1/official-exams/exam-sets/{examSetId}` — `lecturer` chủ sở hữu hoặc `department_head` phụ trách môn):**
   - Chỉ áp dụng khi bộ đề đang `DRAFT` hoặc `REJECTED`, còn `isActive = true` và chưa gắn `StudentExamTickets` (`409 Conflict` nếu `SUBMITTED_FOR_REVIEW`/`OFFICIAL`/`HANDED_OVER`/đã gắn vé).
   - Request (`UpdateExamSetRequest`): `{ "setCode": "SWP391-SET01-V2", "questionIds": ["guid-1", "guid-2"], "inlineManualQuestions": [...] }`.
   - Kiểm tra đầy đủ luật MF-03 M3 (loại trừ chính `examSetId`, khớp `ExamStructure.TotalQuestions` nếu `> 0`, chặn câu Đề mẫu / đã duyệt đợt này / câu GV đã dùng ở bộ khác). Khi sửa bộ đề `REJECTED`, BE tự chuyển trạng thái về `DRAFT` để GV bấm gửi duyệt lại (`POST .../submit-review`).
   - Response: `200 OK` (`ExamSetDto`) | `403` | `404` | `409` | `422`.
2. **Xóa mềm bộ đề (`DELETE /api/v1/official-exams/exam-sets/{examSetId}` — `lecturer` chủ sở hữu hoặc `department_head` phụ trách môn):**
   - Chỉ áp dụng cho bộ đề `DRAFT` hoặc `REJECTED` chưa gắn vé (`409 Conflict` nếu trạng thái khóa hoặc đã gán vé).
   - Vô hiệu hóa mềm (`is_active = false`), gỡ liên kết `exam_set_questions` để trả các câu kho về lại danh sách khả dụng và cho phép tái sử dụng `setCode`.
   - Response: `204 NoContent` | `403` | `404` | `409`.

#### D. Gửi duyệt Bộ đề (`POST /api/v1/official-exams/exam-sets/{id}/submit-review`)
- **Roles:** `lecturer`, `department_head` (chặn gửi duyệt Đề mẫu `isSampleSet = true` → `422`).
- **Ngưỡng ≤ 70% câu vàng & chặn câu đã duyệt:**
  - Nếu bộ đề chứa bất kỳ câu nào đã được duyệt trong bộ đề `OFFICIAL`/`HANDED_OVER` khác cùng cấu trúc → **chặn `422`** (yêu cầu thay câu).
  - Nếu gửi lại bộ đề từng bị trả về (`REJECTED`), BE kiểm tra lại loại trừ nội bộ GV.
  - Tính `yellowOverlapRatio = (số câu vàng đang chờ duyệt ở GV khác) / (tổng số câu trong bộ đề)`. Nếu `> 70%` → **chặn nộp `422 Unprocessable Entity`** kèm tỷ lệ cụ thể trong `detail`.

#### E. Xem Danh sách Bộ đề kèm Cảnh báo Vàng/Đỏ (`GET /api/v1/official-exams/exam-sets?courseId=...`)
- **Roles:** `lecturer`, `department_head`, `examination_officer`.
- **Lưu ý hiển thị theo vai trò:**
  - `lecturer` thấy bộ đề do mình tạo **CỘNG THÊM** Đề mẫu active (`isSampleSet: true`) của môn để tham khảo cấu trúc chuẩn.
  - Mỗi `ExamSetDto` trả thêm các cờ cảnh báo:
    - `isSampleSet`: `boolean`
    - `overlapSeverity`: `"NONE"` | `"YELLOW_PENDING"` | `"RED_APPROVED"`
    - `yellowOverlapRatio`: `number` (0..100, %)
    - `yellowOverlapQuestionCount`: `number`
    - `redOverlapQuestionCount`: `number`
  - Mỗi câu trong `questions[]` (`ExamSetQuestionItemDto`) trả thêm:
    - `overlapSeverity`: `"NONE"` | `"YELLOW_PENDING"` | `"RED_APPROVED"`
    - `duplicateInSets`: `string[]` (mã `SetCode` bị trùng — với TBM hiển thị đầy đủ mã bộ đề để ra quyết định; với GV ẩn danh mã bộ đề người khác trên câu vàng/đỏ)
    - `bankCurationStatus`: `"BANK_READY"` | `"PENDING_CURATION"` | `"CURATED_INTO_BANK"` | `"EXCLUDED_FROM_BANK"`
    - `cloId`: `string | null`

#### F. Trưởng BM Duyệt, Trả về sửa & Bàn giao Bộ đề
1. **Duyệt (`POST /api/v1/official-exams/exam-sets/{id}/approve` — `department_head`):**
   - Request: `{ "reviewNotes": "Đạt yêu cầu" }` (tùy chọn).
   - Chặn tự duyệt bộ đề (`CreatedBy == ActorUserId` → `403`) và chặn tự duyệt câu hỏi chưa duyệt trong bộ đề (`SubmittedBy == ActorUserId` → `403`).
   - Chặn duyệt Đề mẫu (`isSampleSet = true` → `422`), yêu cầu đúng trạng thái `SUBMITTED_FOR_REVIEW` (`409`), chặn bộ đề rỗng (`422`), chặn bộ đề chứa câu Đề mẫu hoặc `EXCLUDED_FROM_BANK` (`409`).
   - **Bất biến #7 khi cascade duyệt câu lẻ:** Mọi câu chưa `APPROVED` trong bộ đề bắt buộc có `sampleAnswer >= 50` ký tự và Rubric `10.00` đúng 2 chữ số thập phân (`numeric(4,2)`) (`422` nếu vi phạm); các câu đã `APPROVED` trước đó được giữ nguyên `ApprovedBy`/`ApprovedAt`; tự động đóng các `QuestionReviewRequestItem` đang `PENDING`.
   - Nếu bộ đề đang chờ duyệt có câu trùng với đề đã duyệt trước đó (cảnh báo đỏ), TBM vẫn có quyền chủ động duyệt hoặc chọn **Trả về sửa** bên dưới; response trả `hasDuplicates`, `duplicateQuestionCount`, `duplicateWarnings[]`, `questionsCascadedCount`.
2. **Trả về chỉnh sửa (`POST /api/v1/official-exams/exam-sets/{examSetId}/return-for-revision` — `department_head`):**
   - Chỉ áp dụng cho bộ đề `SUBMITTED_FOR_REVIEW`. Bắt buộc `reviewNotes` không rỗng (`422`).
   - Chuyển trạng thái bộ đề sang `REJECTED`, đồng thời chuyển mọi câu tạo tay inline (`PENDING_CURATION` đang `SUBMITTED_FOR_REVIEW`) trong bộ đề sang `NEEDS_REVISION` để GV sửa lại nội dung câu hoặc dùng `PUT /exam-sets/{examSetId}` thay câu; gửi thông báo kèm lý do cho GV tạo đề.
   - Response `200 OK`: `{ examSetId, setCode, status: "REJECTED", reviewNotes, returnedAt }`.
3. **Bàn giao cho Khảo thí (`POST /api/v1/official-exams/exam-sets/{id}/hand-over` — `department_head`):**
   - Chuyển `OFFICIAL` → `HANDED_OVER` (chỉ bộ đề `HANDED_OVER` mới được Khảo thí phát cho phòng thi MF-04).

#### G. Tổng hợp Lọc Trùng Văn Bản (`70–79%` Vàng / `>= 80%` Đỏ) Nạp Kho Kỳ Sau
1. **Lấy danh sách câu chờ lọc (`GET /api/v1/official-exams/courses/{courseId}/bank-curation-candidates?band=RED|YELLOW|CLEAR` — `department_head`):**
   - Chỉ liệt kê các câu tạo tay đã được duyệt thi (`approval_status = 'APPROVED'`, `bank_curation_status = 'PENDING_CURATION'`, `is_active = true`, `is_deleted = false`).
   - So sánh trigram (`pg_trgm` / `TrigramSimilarityCalculator` đồng nhất làm tròn 1 chữ số thập phân trước khi phân nhóm):
     - `RED_80_PLUS`: độ tương đồng `>= 80.0%`
     - `YELLOW_70_79`: `70.0% <= độ tương đồng < 80.0%`
     - `CLEAR`: `< 70.0%`
   - Response `200 OK` (`BankCurationCandidatesResponseDto`): chứa thống kê `{ totalPendingCount, redCount, yellowCount, clearCount }` và `items[]` với `matches[]` đầy đủ `title`, `content`, `similarityPercent`, `band`, `isFromExistingBank`, `sourceSetCodes[]` để FE dựng giao diện **đối chiếu 2 cột**.
2. **Ra quyết định nạp/loại kho lẻ hoặc hàng loạt (`POST /api/v1/official-exams/courses/{courseId}/bank-curation/decide` — `department_head`):**
   - Request:
     ```json
     {
       "questionIds": ["guid-1", "guid-2"],
       "decision": "CURATED_INTO_BANK",
       "notes": "Câu chất lượng tốt, không trùng ý"
     }
     ```
   - `decision` hợp lệ: `"CURATED_INTO_BANK"` (nạp vào kho chọn chung kỳ sau) hoặc `"EXCLUDED_FROM_BANK"` (ẩn vĩnh viễn khỏi pool chọn nhưng giữ mềm bản ghi để bảo toàn lịch sử bài thi/phúc khảo).
   - Response `200 OK`: `{ courseId, decision, processedCount, questionIds, curatedAt }`.

---

### 4.2. MF-03: Quản Trị Barem Rubric Đầy Đủ (`RubricsController` — `/api/v1/rubrics`)
- `GET /api/v1/rubrics?courseId={guid}` · `GET /api/v1/rubrics/{id}` · `POST /api/v1/rubrics`
- **MỚI — `PUT /api/v1/rubrics/{id}` (`lecturer`, `department_head`):**
  - Request: `{ name, description, criteria: [{ criterionName, maxScore, weight, bloomLevel, description, orderIndex }] }`.
  - **Luật bất biến:** `SUM(criteria.maxScore) === 10.00`, mỗi `maxScore` tối đa 2 chữ số thập phân, `criterionName <= 200` ký tự, `bloomLevel` thuộc 6 mức chuẩn (`422` nếu vi phạm).
  - **Khóa bảo toàn lịch sử (`409 Conflict`):** Chặn sửa Rubric nếu đang được dùng bởi bộ đề `OFFICIAL`/`HANDED_OVER`, hoặc đã có bài làm (`practice_answers`, `mock_exam_answers`, `in_class_exam_submissions`, `exam_question_submissions`), hoặc câu hỏi đã `APPROVED` mà người sửa là `lecturer`.
- **MỚI — `DELETE /api/v1/rubrics/{id}` (`lecturer`, `department_head`):**
  - Vô hiệu hóa mềm (`is_active = false`); chặn `409 Conflict` nếu bất kỳ câu hỏi chưa xóa nào đang tham chiếu Rubric này.

---

### 4.3. MF-04: Kiosk BYOD Heartbeat, Ràng Buộc Thiết Bị & App-Scoped Anti-Cheat

#### A. Ba Header Bắt Buộc Của Kiosk BYOD
Mọi request từ Desktop Kiosk hoặc Web Kiosk Prototype (`OfficialExamPage.tsx`) trên các route `/api/v1/official-exams/tickets/*` & `/api/v1/official-exams/anti-cheat/log` cần gắn:
- `X-Device-Id`: mã thiết bị `16–128` ký tự khớp regex `^[A-Za-z0-9._:-]{16,128}$` (truyền lần đầu khi `POST .../check-in`, BE gắn chặt vào `student_exam_tickets.device_id` và trả về `activeSessionToken`).
- `X-Kiosk-Session-Token`: chuỗi token phiên nhận được từ response của `POST /api/v1/official-exams/tickets/{ticketId}/check-in` (`activeSessionToken`) — bảo đảm **1 thiết bị + 1 phiên hoạt động duy nhất**.
- `X-Kiosk-Version`: phiên bản Kiosk (ví dụ `"web-prototype-1.0"` hoặc `"desktop-kiosk-1.0.0"`).

> [!IMPORTANT]
> **Không còn kiểm tra khớp `ip_address`:** BE đã chuyển sang khóa theo `X-Device-Id` + `X-Kiosk-Session-Token` để phù hợp Wi-Fi/NAT BYOD (`ip_address` chỉ ghi log kiểm toán). Khi SV cần đổi máy, **Cán bộ Khảo thí** gọi `POST /api/v1/official-exams/tickets/{ticketId}/unlock-device` với `{ "reason": "..." }`.

#### B. MỚI — Kiosk Heartbeat (`POST /api/v1/official-exams/tickets/{ticketId}/heartbeat`)
- **Role:** `student` (chủ sở hữu vé). **Bắt buộc** có `X-Device-Id` và `X-Kiosk-Session-Token` đã gắn với vé.
- **Tần suất khuyến nghị trên FE/Kiosk:** 15–30 giây/lần trong suốt trạng thái `IN_PROGRESS` (và dừng ngay khi `shouldTerminateExam === true` hoặc unmount).
- **Request Body (tùy chọn):**
  ```json
  {
    "audioLevelDb": 68,
    "isFullscreen": true,
    "displayCount": 1,
    "deviceId": "optional-fallback-if-no-header",
    "kioskVersion": "desktop-kiosk-1.0.0"
  }
  ```
  - Lưu ý kiểm tra biên: `audioLevelDb` hợp lệ trong `[0..150]` (ngoài khoảng trả `400 Bad Request`); nếu `0 <= audioLevelDb < 10`, BE gắn cảnh báo `MICROPHONE_LOW_SIGNAL` trong `notice`.
- **Response `200 OK` (`KioskHeartbeatResponse`):**
  ```json
  {
    "ticketId": "guid",
    "shiftId": "guid",
    "ticketStatus": "IN_PROGRESS",
    "shiftStatus": "in_progress",
    "serverUtcNow": "2026-11-24T09:00:00Z",
    "remainingShiftSeconds": 1140,
    "isLocked": false,
    "deviceUnlockRequired": false,
    "shouldTerminateExam": false,
    "notice": null
  }
  ```
- **Xử lý trên FE:**
  - Đồng bộ đồng hồ đếm ngược bằng `serverUtcNow` & `remainingShiftSeconds`.
  - Nếu `isLocked === true` hoặc `shouldTerminateExam === true`: dừng thu âm, khóa giao diện thi, hiển thị `notice` (KHÔNG hiện điểm).
  - Nếu nhận `409 Conflict` (`type` chứa `device-mismatch` hoặc `active-session-conflict`): dừng phiên thi ngay và hướng dẫn báo Giám thị / Khảo thí mở khóa.

#### C. Danh Mục `AntiCheatEventType` Đầy Đủ (`POST /api/v1/official-exams/anti-cheat/log`)
- Đã hỗ trợ thêm 5 sự kiện App-Scoped BYOD mới bên cạnh 7 sự kiện cũ:
  - Cơ bản: `TAB_SWITCH`, `WINDOW_BLUR`, `FULLSCREEN_EXIT`, `MULTIPLE_FACES`, `VOICE_DETECTED`, `DEVICE_DISCONNECTION`, `DISALLOWED_PROCESS`.
  - **MỚI (PR-6 BYOD):** `MULTI_MONITOR`, `VM_DETECTED`, `REMOTE_CONTROL`, `AUDIO_TAMPER`, `HEARTBEAT_TIMEOUT`.
- **Tự động nâng mức nghiêm trọng (`critical`):** `VM_DETECTED`, `REMOTE_CONTROL`, `AUDIO_TAMPER` được BE tự ép `severity = "critical"` bất kể client gửi mức thấp hơn.
- **Chống spam & giới hạn:** Truyền `clientEventId` (UUID) để idempotent; `details` tối đa `2000` ký tự; BE chặn SV gửi sự kiện mới sau khi vé đã ở trạng thái kết thúc (`SUBMITTED`, `AI_GRADED`, `AUDITED`, `PUBLISHED` → `409`).
- **Quy tắc khóa 3 cảnh cáo (`blur > 3`):** Lần vi phạm thứ 4 tự chuyển vé sang `LOCKED`, đẩy cảnh báo real-time tới `AntiCheatHub`.

---

### 4.4. MF-04: Xác Thực Bảng Điểm PDF Ký Số PKI Qua QR (Public)
- **Endpoint:** `GET /api/v1/verification/grades/{certificateHash}` (`AllowAnonymous`, rate-limited).
- **Mục đích:** Khi Khảo thí xuất PDF bảng điểm (`GET /api/v1/official-exams/shifts/{shiftId}/export-pdf`), PDF nhúng chữ ký số PAdES + mã băm SHA-256 `certificateHash` (64 ký tự hex) in kèm QR. Bất kỳ ai quét QR có thể mở trang tra cứu công khai gọi API này để xác minh bảng điểm hợp lệ và chưa bị chỉnh sửa.

---

### 4.5. MF-05: TOTP 5 Phút Kèm Cột 3NF `grace_until` & Phân Quyền DLQ
- `GET /api/v1/in-class-shifts/{id}/current-password`: Xoay vòng OTP 6 ký tự mỗi **5 phút (300 giây)**. Backend lưu cột 3NF `in_class_shift_passwords.grace_until = valid_to + 60s` để SV lỡ gõ chậm tối đa 60s quanh mốc chuyển mã vẫn gọi `POST /api/v1/student/in-class-shifts/{id}/join` thành công mượt mà.
- **Ẩn điểm tuyệt đối trước `FINALIZED`:** Trong MF-05, `GET /api/v1/student/in-class-shifts/{id}/results` chỉ trả `aiScore`/`finalScore`/`aiFeedback`/`lecturerNotes` khi ca đã chốt (`FINALIZED`, `isLocked = true`). Trước đó SV chỉ thấy trạng thái nộp/chấm và câu hỏi phụ.
- **DLQ & Runtime Diagnostics (`Admin` vs `ExaminationOfficer`):**
  - `IN_CLASS_EXAM` DLQ thuộc quyền **`admin`** (`/api/v1/admin/dlq/tasks?flowType=IN_CLASS_EXAM`), cùng với `PRACTICE` và `MOCK_EXAM`.
  - `OFFICIAL_EXAM` DLQ thuộc quyền **`examination_officer`** (`/api/v1/examination-officer/dlq/tasks`).
  - `GET /api/v1/admin/diagnostics/runtime` trả `deadLetterQueue.totalPending` bao gồm cả `byFlowType.officialExam` để Admin quan sát tổng quan sức khỏe hệ thống (nhưng Admin vẫn bị chặn `403` nếu cố replay/resolve DLQ `OFFICIAL_EXAM`).
  - `POST .../dlq/tasks/{id}/replay`: Trả **`409 Conflict`** nếu tác vụ đã được replay trong vòng 2 phút gần nhất (tránh nhấn đúp gây chấm trùng); nếu replay tác vụ `abandoned`, BE tự reset `retryCount = 0`.

---

## 5. HỢP ĐỒNG REAL-TIME SIGNALR (3 HUBS CHÍNH THỨC)

Cả 3 Hub đều hỗ trợ JWT qua `accessTokenFactory: () => ensureFreshAccessToken(90)` (đã có sẵn trong `frontend/src/services/signalr.service.ts`) và **bắt buộc đóng kết nối (`connection.stop()`) trong cleanup của `useEffect`**.

| Hub URL | Roles được phép `Join*` | Client → Server Methods | Server → Client Events | Ghi chú bảo mật & Fallback |
|:---|:---|:---|:---|:---|
| `/hubs/practice` (`PracticeHub`) | `student` (active + chủ sở hữu `session.StudentId`) | `JoinSession(sessionId: string)`, `LeaveSession(sessionId: string)` | `ReceiveGradingResult(PracticeAnswerDetailDto)`, `ReceiveGradingError(answerId, error)` | Khóa BOLA + kiểm tra `User.IsActive && Role == "student"`. Fallback polling `GET /api/v1/practice/sessions/{id}` mỗi 3–5s nếu mất WSS. |
| `/hubs/in-class-shift` (`InClassShiftHub`) | `lecturer` (`Class.LecturerId`) hoặc `student` (`enrolled` lớp active) | `JoinShift(shiftId)` (alias `JoinInClassShiftGroup`), `LeaveShift(shiftId)` (alias `LeaveInClassShiftGroup`) | `ReceiveGradingResult(PracticeAnswerDetailDto)`, `InClassSubmissionGraded(payload)`, `InClassShiftGradesFinalized(payload)`, `ReceiveGradingError(answerId, error)` | Server tách 3 nhóm `audience`, `staff` (GV nhận điểm AI real-time), `student:{userId}` (SV **không** nhận điểm lẻ trước khi `InClassShiftGradesFinalized` phát ra). |
| `/hubs/anti-cheat` (`AntiCheatHub`) | `proctor`, `lecturer` (Giám khảo ca đó), `department_head` (TBM môn đó), `examination_officer` | `JoinShift(shiftId: string)`, `LeaveShift(shiftId: string)` | `ReceiveAntiCheatAlert(ExamAntiCheatLogDto)` | Tự động đẩy khi SV log vi phạm (`POST .../anti-cheat/log`) hoặc Giám thị ghi biên bản (`POST /api/v1/proctor/shifts/{shiftId}/violations`). |

---

## 6. MƯỜI HAI BẤT BIẾN & CÁC QUY TẮC CẤM KỴ KHI NỐI FE

1. **Tuyệt đối không tin ẩn nút trên UI thay cho BE:** Luôn xử lý `403`/`409`/`422` từ BE cleanly.
2. **Zero-R2 cho MF-01, MF-02, MF-05:** Chỉ thu âm cục bộ để chạy STT lấy text transcript; **CHỈ MF-04** mới gọi `audio-upload-authorizations` → `PUT` trực tiếp lên Presigned R2 URL (`STT_MSSV.webm`) → `PUT .../audio` hoặc `PUT .../questions/{questionId}/answers` kèm `audioHashSha256`.
3. **Kiosk MF-04 = 0% hiện điểm tại phòng thi:** Sau khi `PUT .../submission` trả `202 Accepted`, hiển thị màn xác nhận đã nộp bài và khóa Kiosk — không gọi API xem điểm hay phúc khảo từ màn Kiosk.
4. **One-Way Lock sau công bố điểm:** Khi ca thi thật đã `publish-grades` hoặc ca MF-05 đã `finalize-grades`, mọi thao tác sửa điểm đều bị khóa (`isLocked = true`). Ngoại lệ duy nhất ở MF-04 là quy trình Phúc khảo 5 bước do `examination_officer` phê duyệt cuối cùng.
5. **Barem Rubric 10.00đ:** Kiểm tra phía client `Math.round(sum * 100) === 1000` và tối đa 2 chữ số thập phân trước khi gửi; luôn hiển thị lỗi `422`/`409` nếu Rubric đang bị khóa bởi bộ đề/bài làm.
6. **Không hardcode cấu hình động:**
   - Buffer screen (10–300s, mặc định 60s), số câu follow-up, min/max Progressive (3–10) đọc từ API session/system-configs.
   - Hạn ngạch thi thử MF-02 đọc từ `GET /api/v1/mock-exams/quota?courseId=...` (KHÔNG hardcode `3`).
   - Sức chứa phòng thi MF-04 đọc từ `maxSeats` (mặc định `24`, trần vật lý `40` — KHÔNG hardcode `40`).
7. **Không dùng Three.js / hiệu ứng 3D gây nhiễu** trên màn hình đang làm bài thi, đồng hồ đếm ngược, màn hậu kiểm và màn coi thi.
8. **Không trộn hai kho câu hỏi R8:** Màn Luyện tập / Thi thử / Ca thi trong lớp chỉ tương tác `practice_questions` (`usageScope: "practice"`); màn Cấu trúc đề / Bộ đề thi thật / Lọc kho chỉ tương tác `exam_questions` (`usageScope: "exam"`).

---

## 7. BẢN ĐỒ KHOẢNG TRỐNG FE HIỆN TẠI & CHECKLIST NỐI BE (EXACT WIRING PLAYBOOK)

Qua đối chiếu trực tiếp mã nguồn `frontend/src/**` với Backend đã hoàn tất trên nhánh này, đây là **danh sách việc cụ thể để FE-Agent / Team FE nối ngay** (chia đúng theo ranh giới sở hữu `frontend/AGENTS.md`):

### 7.1. Phần Ưu Tiên Cao — Nối Đầy Đủ MF-03 M3 (Phạm vi: Hải — TBM & Hoàng/Hải — Giảng viên)
| STT | File FE mục tiêu | Việc cần làm (đối chiếu 100% BE đã sẵn sàng) |
|:---:|:---|:---|
| 1 | `frontend/src/types/officialExam.types.ts` | Bổ sung vào `ExamStructure`, `CreateExamStructureRequest`, `UpdateExamStructureRequest`: `requiredSetCount?: number`, `slotDefinitions?: string`. Bổ sung vào `ExamSet`: `isSampleSet`, `overlapSeverity`, `yellowOverlapRatio`, `redOverlapQuestionCount`, `yellowOverlapQuestionCount`. Bổ sung vào `ExamSetQuestion`: `overlapSeverity`, `bankCurationStatus`, `cloId`. Thêm types mới: `CreateExamSetRequest`, `UpdateExamSetRequest`, `CreateInlineExamQuestionDto`, `AvailableExamSetQuestionsResponse`, `ReturnExamSetForRevisionResponse`, `BankCurationCandidatesResponse`, `CurateExamQuestionBankRequest`, `CurateExamQuestionBankResult`. |
| 2 | `frontend/src/services/officialExam.service.ts` | Cập nhật `normalizeExamStructure`, `normalizeExamSet`, `normalizeExamSetQuestion` đọc đủ các field mới ở trên. Thêm 10 hàm service: `getAvailableExamSetQuestions`, `createExamSet`, `updateExamSet`, `deleteExamSet`, `submitExamSetForReview`, `approveExamSet`, `returnExamSetForRevision`, `handOverExamSet`, `getBankCurationCandidates`, `decideBankCuration`. |
| 3 | `frontend/src/pages/lecturer/OfficialExamManagementPage.tsx` (hoặc tách tab/trang con cho TBM) | Thêm input `requiredSetCount` (1..20) & editor `slotDefinitions` trên form Cấu trúc đề; thêm thao tác tạo **1 Đề mẫu** (`isSampleSet: true`); thêm bảng duyệt bộ đề hiển thị badge vàng/đỏ (`overlapSeverity`, `yellowOverlapRatio`, `duplicateInSets`) với 3 nút **Duyệt** / **Trả về chỉnh sửa** (bắt buộc nhập lý do) / **Bàn giao Khảo thí**; thêm tab **Tổng hợp lọc kho kỳ sau** đối chiếu 2 cột (`70–79%` vàng, `>= 80%` đỏ) với nút nạp/loại lẻ hoặc hàng loạt. |
| 4 | Trang soạn Bộ đề cho Giảng viên (cổng `Instructor` trong `AppRoutes.tsx`) | Thêm màn **Soạn / Sửa / Xóa Bộ Đề Thi Thật** cho GV: chọn Cấu trúc đề → xem Đề mẫu tham khảo → gọi `GET .../available-questions` (hiển thị nhãn vàng ẩn danh *"Đã có người chọn, đang chờ duyệt"*) → cho phép chọn câu kho + thêm `inlineManualQuestions` → hiển thị thanh tiến độ tỷ lệ câu vàng (cảnh báo khi chạm ngưỡng `70%`) → tạo (`POST`), chỉnh sửa lại bộ `DRAFT`/`REJECTED` (`PUT`), xóa mềm (`DELETE`) & bấm **Gửi TBM duyệt**. |

### 7.4. Bổ Sung Chuẩn Mã Lỗi & Bảo Mật Mới Nhất Sau Hardening PR 1 & PR 2 (FE Bắt Buộc Xử Lý)
- **MF-02 Quota vs Rate Limit (`429`):** Lỗi vượt hạn ngạch thi thử theo ngày luôn trả `ProblemDetails.extensions.errorCode = "QUOTA_EXCEEDED"` (tách biệt rõ với `"RATE_LIMITED"` của middleware, bao gồm bucket `"class-join"` 15 req/phút trên `POST /api/v1/classes/join`).
- **Speech / Storage Audio Magic-Bytes (`415`):** `POST /api/v1/speech/transcribe` và `POST /api/v1/storage/upload-audio` kiểm tra magic-bytes 16 byte đầu (WebM, WAV, OGG, MP3, MP4/M4A ISO BMFF, FLAC) và trả `415 Unsupported Media Type` (`errorCode: "UNSUPPORTED_AUDIO_MEDIA_TYPE"`) nếu file rác/sai định dạng.
- **MF-02 Pause/Resume (`15 phút` trần tối đa) & `409 Conflict`:** Thời gian cộng bù tạm dừng được kẹp tối đa `15 phút` so với thời lượng gốc; gọi `submit-answer` khi phiên đang tạm dừng trả `409 Conflict` (không trả `410`). `GET /api/v1/mock-exams/history` tự kẹp an toàn `pageIndex >= 1`, `pageSize ∈ [1..100]`.
- **MF-01 `409` vs `410`:** Phiên đã hoàn tất hợp lệ (`completed`) trả `409 Conflict` trên mọi endpoint (`next-question`, `submit-answer`, `batch-answers`); chỉ phiên hết hạn không tương tác `> 5 phút` mới trả `410 Gone`. Chặn kết hợp `practiceMode = "per_question"` với `difficulty = "progressive"` (`400`).
- **MF-05 `POST /finish` khóa vĩnh viễn (`finished_at`) & `UpdateShift` `422`:** Sau khi SV bấm nộp hết bài (`POST /finish`) hoặc bị auto-submit, mọi lần gọi `submit-answer` / `next-followup` sau đó đều trả `409 Conflict`. Hạn chót cá nhân kẹp `Min(joinedAt + duration, shift.EndTime)`. `PUT /api/v1/in-class-shifts/{id}` trả `422` nếu `selectedCloIds.Count > totalQuestionsPerStudent` hoặc chuyển về `SELECTED_POOL`.
- **MF-04 BYOD, Proctor & RFC 7807:** Sau khi Khảo thí bấm `unlock-device`, SV bắt buộc check-in gắn lại `X-Device-Id` mới trước khi gửi `anti-cheat/log` hoặc tiếp tục thi (`409 DEVICE_MISMATCH`); `proctor` bị chặn khỏi `/check-in` (`403`) và không thể kích hoạt auto-lock `WINDOW_BLUR`; `AppealsController`, `CoursesController`, `NotificationsController`, `QuestionsController` đã chuẩn hóa mã `403`/`404`/`409` RFC 7807.

### 7.2. Phần Ưu Tiên Cao — Nối Kiosk BYOD Heartbeat & Anti-Cheat Mới (Phạm vi: Hoàng — Web Kiosk Prototype)
| STT | File FE mục tiêu | Việc cần làm |
|:---:|:---|:---|
| 1 | `frontend/src/types/kiosk.types.ts` | Thêm `KioskHeartbeatRequest`, `KioskHeartbeatResponse` và union type `AntiCheatEventType` đủ 12 giá trị (thêm `MULTI_MONITOR`, `VM_DETECTED`, `REMOTE_CONTROL`, `AUDIO_TAMPER`, `HEARTBEAT_TIMEOUT`). |
| 2 | `frontend/src/services/kioskExam.service.ts` | Thêm `sendHeartbeat(ticketId, payload?, headers?)` gọi `POST /official-exams/tickets/{ticketId}/heartbeat` kèm 3 header BYOD (`X-Device-Id`, `X-Kiosk-Session-Token`, `X-Kiosk-Version`). |
| 3 | `frontend/src/pages/student/OfficialExamPage.tsx` & `frontend/src/hooks/useKioskLockdown.ts` | Gọi `sendHeartbeat` định kỳ 20s khi đang thi `IN_PROGRESS`; đồng bộ `remainingShiftSeconds` từ `serverUtcNow`; dừng phiên ngay khi `shouldTerminateExam` / `isLocked` / `409 DEVICE_MISMATCH` / `ACTIVE_SESSION_CONFLICT`. |

### 7.3. Phần Bổ Sung Hoàn Thiện — Rubric Studio CRUD, QR Verification, DLQ & `ai_generated`
| STT | File FE mục tiêu | Chủ sở hữu | Việc cần làm |
|:---:|:---|:---|:---|
| 1 | `frontend/src/types/rubric.types.ts` & `frontend/src/services/question.service.ts` (hoặc `rubric.service.ts`) | Hoàng / Hải | Mở rộng `QuestionSource = 'manual' | 'ai_generated' | 'flm_api'`; thêm service gọi `GET/POST/PUT/DELETE /api/v1/rubrics` kèm xử lý `409 Conflict` khi Rubric đang bị khóa bởi câu/bài thi. |
| 2 | `frontend/src/routes/AppRoutes.tsx` + trang công khai mới `VerifyGradePage.tsx` | Hải / Hoàng | Thêm route công khai `/verify/grades/:certificateHash` gọi `GET /api/v1/verification/grades/{certificateHash}` để quét mã QR trên PDF bảng điểm ký số PKI. |
| 3 | `frontend/src/pages/lecturer/DeadLetterQueuePage.tsx` & `frontend/src/components/admin/RuntimeDiagnosticsPanel.tsx` | Thành | Đảm bảo bộ lọc Admin DLQ có tùy chọn `IN_CLASS_EXAM` bên cạnh `PRACTICE`, `MOCK_EXAM`; hiển thị thông báo rõ khi `POST .../replay` trả `409 Conflict` (đang replay trong vòng 2 phút); hiển thị `byFlowType.officialExam` trong bảng sức khỏe tổng quan Admin. |
| 4 | `frontend/src/pages/lecturer/CreateQuestionPage.tsx` | Hoàng / Thành | Kiểm tra bắt buộc chọn `cloId` khi GV tạo câu hỏi `practice` để `GET /api/v1/in-class-shifts/pool-stats` (`SELECTED_CLOS` MF-05) luôn đủ câu theo từng CLO. |

---

## 8. QUY TRÌNH CHẠY CỤC BỘ & CỔNG KIỂM CHỨNG CHO FE-AGENT

### 8.1. Khởi chạy Backend + PostgreSQL 16 cục bộ để tích hợp FE thật
```powershell
# Tại thư mục gốc repo: D:\Đồ Án\05_Source_Code

# 1. Bật PostgreSQL 16 (hoặc toàn bộ stack)
docker compose up -d postgres

# 2. Chạy Backend API trên http://localhost:5000 (Swagger: http://localhost:5000/swagger)
dotnet run --project backend/src/API/OralExamination.API.csproj

# 3. Chạy Frontend Vite Dev Server trên http://localhost:3000 (proxy /api và /hubs sang :5000)
cd frontend
npm run dev
```

### 8.2. Cổng Kiểm Chứng Cứng Bắt Buộc Trước Khi FE-Agent Tạo Commit / PR
```powershell
cd "D:\Đồ Án\05_Source_Code\frontend"
npm run lint
npx tsc -b
npx vitest run
npm run build
```
- **Lưu ý quy chuẩn Git & CI:** Commit message bắt buộc đúng chuẩn Conventional Commits (`feat(ui): ...`, `fix(ui): ...` — scope hợp lệ theo `commitlint.config.mjs`), dòng tiêu đề $\le 100$ ký tự, viết thường, không dấu chấm cuối câu, mọi dòng body $\le 100$ ký tự (CI chạy `Commit Message Linter`).
- **Tôn trọng ranh giới sở hữu:** Không sửa đè code `backend/**` trong PR Frontend; nếu phát hiện thiếu field hoặc bug BE, ghi rõ endpoint + payload tái hiện vào issue/comment để Backend xử lý.
