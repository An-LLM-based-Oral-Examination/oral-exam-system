# QUY ƯỚC GIAO TIẾP VÀ TÍCH HỢP FE - BE (API CONTRACT & INTEGRATION GUIDE v4.0)
**Dự án:** An LLM-based Oral Examination (FA26SE166) — FPT University HCMC (FPT SG)  
**Tài liệu này quy định bắt buộc (Mandatory) các chuẩn mực kết nối giữa Frontend (React 19 / Vite) và Backend (.NET 8 Clean Architecture) cho 4 thành viên:**  
- 🧑 **Nguyễn Quang Thành:** Team Leader & Lead BE Architect (MediatR CQRS, System.Threading.Channels, One-Way Lock, System Integration).  
- 🧑 **Nguyễn Trọng Tốt:** BE Developer, AI Integration & QA Lead (Gemini 1.5 Flash/Pro CoT, Whisper STT, Barem Rubric 10.0, Unit & Architecture Tests).  
- 🧑 **Phạm Nguyễn Đăng Hải:** DB Specialist & Frontend Developer (phụ trách Database 38 bảng 3NF trong 7 Bounded Contexts PostgreSQL và dồn toàn lực phát triển Frontend; tuyệt đối không code C# Backend).  
- 🧑 **Lê Vũ Hoàng:** Lead Frontend Architect & Fullstack Coordinator (React 19 Vite, Tailwind CSS v4, Web Speech API STT/TTS, Buffer Screen, Kiosk Lockdown, Waveform Player, Contract-First API Sync).  

**Quy chuẩn cốt lõi:**  
- **Tách biệt hoàn toàn:** Kho luyện tập mở (`practice_questions`, `practice_answers`) và kho thi cử bảo mật (`exam_questions`, `mock_exam_*`, `official_exam_*`).  
- **Buffer Screen:** Thời gian đệm cấu hình động 10–300 giây (mặc định 60s do Admin cấu hình chung qua `system_configs` cho MF-01) cho phép sinh viên nghe TTS, nói qua Mic, xem transcript Web Speech và chỉnh sửa trước khi nộp bài.  
- **Quota Guard:** Đếm trực tiếp từ PostgreSQL theo hạn ngạch môn học do Admin cấu hình (`max_mock_exams_per_day`) cho thi thử (MF-02).  
- **Hàng đợi RAM:** `BoundedChannel` 1,000 slots trả `202 Accepted` trong $< 100$ms, kết quả trả qua WebSocket SignalR `/hubs/practice`.  
- **Thi thật phòng Lab (MF-04):** Bóc băng Whisper STT Server-side kèm timestamps, Cloudflare R2 lưu `STT_MSSV.webm`, băm SHA-256 niêm phong, và cơ chế Khóa điểm một chiều `OneWayLockInterceptor` (`HTTP 403`).

---

## 1. QUY ƯỚC CHUNG VỀ API (RESTFUL)
- **Base URL:** Tất cả REST API phải bắt đầu bằng tiền tố `/api/v1/`.
- **Định dạng dữ liệu:** Bắt buộc giao tiếp bằng `application/json` (UTF-8).
- **Quy tắc Naming Convention:**
  - URL luôn dùng chữ thường và dấu gạch ngang (kebab-case). Ví dụ: `/api/v1/official-exams/shifts` (KHÔNG DÙNG: `/api/v1/OfficialExams` hay `/api/v1/exam_sessions`).
  - Response JSON từ Backend trả về bắt buộc định dạng **camelCase** (ví dụ: `studentId`, `totalScore`, `roomLab`, `seatNumber`) để Frontend ánh xạ thẳng vào TypeScript Interface.
  - Phân quyền theo 6 vai trò (User Roles): `admin`, `department_head`, `examination_officer`, `lecturer`, `proctor`, `student`.

---

## 2. QUY CHUẨN XỬ LÝ LỖI (GLOBAL EXCEPTION - RFC 7807)
Backend tuyệt đối KHÔNG trả về chuỗi text thô. Mọi lỗi nghiệp vụ và hệ thống (400, 401, 403, 404, 410, 422, 429, 500) đều phải bọc trong định dạng `ProblemDetails` theo chuẩn RFC 7807:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.21",
  "title": "Unprocessable Entity",
  "status": 422,
  "detail": "Tổng điểm Rubric phải bằng chính xác 10.00.",
  "errors": {
    "Criteria": [
      "Tổng điểm hiện tại đang là 9.50, còn thiếu 0.50 điểm để đạt 10.00"
    ]
  }
}
```

**Quy ước cho Frontend:**
- Cấu hình Axios Interceptor chặn HTTP Responses.
- Khi `status >= 400`: Đọc chuỗi `detail` để hiển thị Toast thông báo lỗi màu đỏ.
- Nếu có object `errors`: Tự động highlight viền đỏ và hiển thị thông điệp lỗi dưới từng input tương ứng.

---

## 3. QUY ƯỚC MÃ HTTP STATUS CODE
- `200 OK`: Truy vấn hoặc cập nhật dữ liệu thành công.
- `201 Created`: Tạo mới tài nguyên thành công (kèm Header `Location` nếu có).
- `202 Accepted`: Dùng riêng cho tiếp nhận bài nộp MF-01. Backend đã đưa bài làm vào hàng đợi bộ nhớ Bounded Channel (< 100ms), Frontend không cần chờ mà sẽ nhận điểm qua SignalR.
- `400 Bad Request`: Payload gửi lên sai cấu trúc cú pháp JSON.
- `401 Unauthorized`: Chưa đăng nhập hoặc Token JWT hết hạn / không hợp lệ.
- `403 Forbidden`: Bị từ chối quyền truy cập (ví dụ: Sinh viên cố truy cập route quản trị, người dùng không có quyền gọi API sinh câu hỏi [yêu cầu `lecturer` hoặc `department_head`], hoặc gửi request sửa bài thi khi đã bị Khóa điểm một chiều `is_locked = true`).
- `404 Not Found`: Không tìm thấy tài nguyên theo ID.
- `410 Gone`: Phiên luyện tập đã kết thúc tự động do quá thời gian không tương tác (`SessionInactivityTimeoutMinutes = 5` phút). Toàn bộ dữ liệu điểm số và câu trả lời đã làm trước đó được bảo toàn nguyên vẹn, sinh viên không thể tương tác thêm trong phiên này.
- `413 Payload Too Large`: Dung lượng tệp ảnh sơ đồ đính kèm câu hỏi thủ công (`POST /api/v1/questions/upload-image` hoặc `POST /api/v1/storage/upload-question-image`) vượt quá giới hạn tối đa **5MB** (`5,242,880 bytes`).
- `415 Unsupported Media Type`: Tệp ảnh tải lên không khớp chữ ký nhị phân (Magic Bytes) hợp lệ của các định dạng `PNG`, `JPG/JPEG` hoặc `WEBP`.
- `422 Unprocessable Entity`: Dữ liệu đúng cú pháp nhưng vi phạm quy tắc nghiệp vụ (ví dụ: Tổng điểm Rubric != 10.0, Model Answer < 50 ký tự, hoặc cập nhật điểm thẩm định mà để trống lý do giải trình).
- `429 Too Many Requests`: Vi phạm Quota Guard thi thử vượt quá hạn ngạch ngày theo môn học (`max_mock_exams_per_day` do Admin cấu hình, kiểm soát bởi PostgreSQL).
- `500 Internal Server Error`: Lỗi máy chủ chưa được xử lý.
- `502 Bad Gateway`: Lỗi kết nối hoặc phản hồi không hợp lệ từ dịch vụ upstream bên thứ 3 (Cloudflare Whisper STT, Google Gemini AI) sau khi đã thực thi chính sách thử lại Polly Retry Policy.
- `504 Gateway Timeout`: Quá thời gian chờ phản hồi từ dịch vụ ngoại vi sau khi cạn kiệt chu trình Polly Retry (3 lần: 2s, 4s, 8s). Phân định rõ hai tầng timeout: Thời gian chờ mỗi lần gọi đơn lẻ (Attempt Timeout: 15s cho Whisper STT, 30s cho Gemini AI Generation) và Tổng thời gian chờ tối đa của toàn bộ Resilience Pipeline (Total Request Timeout: 45s cho Whisper STT, 90s cho Gemini AI Generation).

---

## 4. BẢO MẬT & XÁC THỰC KÉP (DUAL AUTHENTICATION & JWT CONTRACT)

### 4.1. Đăng nhập hệ thống (Google OAuth 2.0 PKCE & Tài khoản Nhà trường cấp)

#### 4.1.1. Đăng nhập Google OAuth 2.0 PKCE
- **Endpoint:** `POST /api/v1/auth/google-login`
- **Quyền truy cập:** Public
- **Request Body:**
```json
{
  "idToken": "eyJhbGciOiJSUzI1NiIsImtpZCI6Ij...",
  "role": "student"
}
```
- **Response 200 OK:**
```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "refreshToken": "7c9e6679-7425-40de-944b-e07fc1f90ae7",
  "tokenType": "Bearer",
  "expiresIn": 900,
  "user": {
    "id": "e4c5b2a1-0001-4000-8000-000000000004",
    "email": "hoanglvse170001@fpt.edu.vn",
    "fullName": "Lê Vũ Hoàng",
    "studentCode": "SE170001",
    "role": "student"
  }
}
```

#### 4.1.2. Đăng nhập bằng Tài khoản & Mật khẩu do Nhà trường cấp
- **Endpoint:** `POST /api/v1/auth/password-login`
- **Quyền truy cập:** Public
- **Request Body:**
```json
{
  "usernameOrEmail": "SE170001",
  "password": "SchoolProvisionedPassword123!"
}
```
- **Response 200 OK:** Trả về cùng cấu trúc `AuthResponse` (`accessToken`, `refreshToken`, `tokenType`, `expiresIn`, `user`). Nếu sai thông tin đăng nhập trả `401 Unauthorized`; nếu tài khoản bị khóa (`is_active = false`) trả `403 Forbidden`.

#### 4.1.3. Đổi mật khẩu tài khoản Nhà trường cấp
- **Endpoint:** `POST /api/v1/auth/change-password`
- **Quyền truy cập:** Authenticated (`student`, `lecturer`, `department_head`, `examination_officer`, `proctor`, `admin`)
- **Request Body:**
```json
{
  "currentPassword": "SchoolProvisionedPassword123!",
  "newPassword": "NewStrongPassword456!",
  "confirmNewPassword": "NewStrongPassword456!"
}
```
- **Response 200 OK:** `{ "message": "Đổi mật khẩu thành công." }` (Cập nhật `password_hash` BCrypt và `password_changed_at`).

#### Cơ chế Xác thực Kép Toàn Hệ Thống (Dual Authentication)
- Hệ thống hỗ trợ xác thực kép (**Google OAuth 2.0 PKCE** hoặc **Tài khoản `username`/`password` do Nhà trường cấp**) cho toàn bộ 6 vai trò (`student`, `lecturer`, `department_head`, `examination_officer`, `proctor`, `admin`).
- **Không mở form tự đăng ký công khai bằng mật khẩu**: Tài khoản mật khẩu do Nhà trường/Admin cấp phát sẵn (lưu `password_hash` chuẩn BCrypt).
- Mọi phiên làm việc sau khi xác thực thành công đều nhận cặp `accessToken` (JWT 15 phút) và `refreshToken` (7 ngày, hỗ trợ Token Rotation).

#### Cấp lại Access Token mới (Silent Refresh Token)
- **Endpoint:** `POST /api/v1/auth/refresh-token`
- **Quyền truy cập:** Public (kèm Refresh Token trong Body hoặc HttpOnly Cookie)
- **Request Body:**
```json
{
  "refreshToken": "7c9e6679-7425-40de-944b-e07fc1f90ae7"
}
```
- **Response 200 OK:**
```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.new...",
  "refreshToken": "8d0f7780-8536-51ef-055c-f18fd2e01bf8",
  "tokenType": "Bearer",
  "expiresIn": 900
}
```

#### Đăng xuất (Thu hồi Refresh Token phía Server)
- **Endpoint:** `POST /api/v1/auth/sign-out`
- **Quyền truy cập:** Public
- **Request Body:**
```json
{
  "refreshToken": "7c9e6679-7425-40de-944b-e07fc1f90ae7"
}
```
- **Response 200 OK:**
```json
{
  "message": "Đăng xuất thành công."
}
```
- **Hành vi:** Thu hồi Refresh Token phía server (đặt `revoked_at`). Endpoint là **idempotent** — gọi nhiều lần với cùng token hoặc token không tồn tại đều trả 200 OK. Frontend cần xóa Access Token và Refresh Token khỏi bộ nhớ sau khi gọi.
- **Response 400 Bad Request:** Khi `refreshToken` rỗng hoặc null.

### 4.2. Quy ước lưu trữ và gửi Token
- Frontend lưu `accessToken` vào Memory hoặc Session Storage (tuyệt đối không lưu LocalStorage nhằm phòng chống XSS).
- Mọi request yêu cầu xác thực phải đính kèm Header:
  `Authorization: Bearer <accessToken>`
- Khi gặp lỗi `401 Unauthorized`, Axios Interceptor tự động kích hoạt luồng Silent Refresh token để lấy token mới.

### 4.3. Ma trận Phân quyền (RBAC Matrix) & JWT Claims
Hệ thống xác thực người dùng dựa trên JWT Bearer token chứa claim `role`. Bảng ma trận phân quyền chi tiết cho 6 vai trò:

| Phân hệ / Endpoint API | `student` | `lecturer` | `department_head` | `examination_officer` | `proctor` | `admin` |
|:---|:---:|:---:|:---:|:---:|:---:|:---:|
| Quản lý Học thuật (`/api/v1/academic/*`) | ✅ (xem) | ✅ | ✅ | ✅ (xem) | ✅ (xem) | ✅ |
| Chuỗi học thuật 4 cấp (R5): Admin gán Trưởng BM vào môn (`PUT /api/v1/admin/courses/{id}/assign-department-head`) | ❌ | ❌ | ❌ | ❌ | ❌ | **✅** |
| Chuỗi học thuật 4 cấp (R5): Trưởng BM phân công nhiều GV vào môn (`POST /api/v1/department-head/courses/{id}/lecturers`) | ❌ | ❌ | **✅** | ❌ | ❌ | **✅** |
| Chuỗi học thuật 4 cấp (R5): GV tạo lớp & mật khẩu lớp (`POST /api/v1/lecturer/classes`, `POST /api/v1/lecturer/classes/{id}/reset-join-password`) | ❌ | **✅** | ❌ | ❌ | ❌ | **✅** |
| Chuỗi học thuật 4 cấp (R5): SV nhập mật khẩu tự tham gia lớp (`POST /api/v1/student/classes/join`) | **✅** | ❌ | ❌ | ❌ | ❌ | **✅** |
| Luyện tập tự do MF-01 (`/api/v1/practice/*`) | ✅ | ✅ | ✅ | ❌ | ❌ | ✅ |
| Thi thử bấm giờ MF-02 (`/api/v1/mock-exams/*`) | ✅ | ✅ | ✅ | ❌ | ❌ | ✅ |
| Quản lý Chuẩn đầu ra CLOs (`/api/v1/courses/{id}/clos`) | ❌ | ✅ | ✅ | ❌ | ❌ | ✅ |
| Phân tách 2 luồng tạo câu hỏi (R8): Luồng luyện tập/thi thử tạo độc lập vào `practice_questions` (`/api/v1/practice/questions`) | ❌ | **✅** | **✅** | ❌ | ❌ | **✅** |
| Phân tách 2 luồng tạo câu hỏi (R8): Luồng thi thật tạo bám sát đề thi mẫu / ma trận cấu trúc (`exam_structures`, `slot_definitions`) của Trưởng BM vào `exam_questions` (`/api/v1/official-exams/questions`) | ❌ | **✅** | **✅** | ❌ | ❌ | **✅** |
| Soạn câu hỏi & Barem thủ công (`/api/v1/questions`, `/api/v1/rubrics`) | ❌ | ✅ | ✅ | ❌ | ❌ | ✅ |
| Kích hoạt AI sinh câu hỏi từ CLO (`POST /api/v1/questions/generate-from-clo`) | ❌ | **✅** | **✅** | ❌ | ❌ | **✅** |
| Giảng viên gửi câu hỏi duyệt Bộ môn (`POST /api/v1/questions/batch-submit-review`) | ❌ | **✅** | ❌ | ❌ | ❌ | **✅** |
| Trưởng Bộ Môn thẩm định câu hỏi (`POST /api/v1/questions/{id}/review-decision`) | ❌ | ❌ | **✅** | ❌ | ❌ | **✅** |
| Đóng gói & Duyệt bộ đề ExamSets (`/api/v1/exam-sets/*`) | ❌ | ❌ | **✅** | ❌ | ❌ | **✅** |
| Quản lý Kỳ thi & Ca thi Phòng Lab MF-04 (`/api/v1/official-exams/*`): Khảo thí tạo kỳ thi, ca thi, xuất bản đề, công bố điểm; Trưởng BM chỉ cấu hình Follow-up thi thật (`1–5` câu, mặc định `2`) | ❌ | ❌ | **✅** (Follow-up) | **✅** (Kỳ/Ca thi) | ❌ | **✅** |
| Phân bổ đề thi không trùng lặp (`uq_tickets_shift_exam_set`) | ❌ | ❌ | ❌ | **✅** | ❌ | **✅** |
| Giám thị Phòng Lab (`/api/v1/proctor/*`): điểm danh ghế 1–40 (`POST /api/v1/proctor/shifts/{id}/attendance`) & báo cáo vi phạm phòng thi (`POST /api/v1/proctor/shifts/{id}/violations`) | ❌ | ❌ | ❌ | ❌ | **✅** | **✅** |
| Ghi nhật ký chống gian lận thời gian thực (`POST /api/v1/official-exams/anti-cheat/log`) | **✅** | ❌ | ❌ | **✅** | **✅** | **✅** |
| Thẩm định điểm (Evidence Panel), sửa điểm có lý do MF-04 | ❌ | **✅** | **✅** | ❌ | ❌ | **✅** |
| Công bố điểm ca thi (Publish Grades) kích hoạt One-Way Lock | ❌ | **✅** | ❌ | **✅** | ❌ | **✅** |
| Sinh viên xem điểm Student Portal & Xác nhận điểm | **✅** | ❌ | ❌ | ❌ | ❌ | **✅** |
| Phúc khảo nội bộ MF-04 (Bước 1/5): SV nộp đơn (`POST /api/v1/student/appeals`) | **✅** | ❌ | ❌ | ❌ | ❌ | **✅** |
| Phúc khảo nội bộ MF-04 (Bước 2/5): Cán bộ Khảo thí tiếp nhận & thẩm tra chuyển giao (`POST /api/v1/examination-officer/appeals/{id}/forward-to-head`) | ❌ | ❌ | ❌ | **✅** | ❌ | **✅** |
| Phúc khảo nội bộ MF-04 (Bước 3/5): Trưởng BM phân công GV (`POST /api/v1/department-head/appeals/{id}/assign-lecturer`) | ❌ | ❌ | **✅** | ❌ | ❌ | **✅** |
| Phúc khảo nội bộ MF-04 (Bước 4/5): GV chấm lại (`PUT /api/v1/lecturer/appeals/{id}/regrade`) | ❌ | **✅** | ❌ | ❌ | ❌ | **✅** |
| Phúc khảo nội bộ MF-04 (Bước 5/5): Trưởng BM phê duyệt quyết định (`POST /api/v1/department-head/appeals/{id}/decision`) | ❌ | ❌ | **✅** | ❌ | ❌ | **✅** |
| Ca thi trong lớp & TOTP 5m (R6 - MF-05): GV tạo ca thi, lấy mật khẩu TOTP 6 ký tự xoay vòng 5 phút + grace period 60s, hậu kiểm & sửa điểm thành phần, chốt điểm One-Way Lock (`POST /api/v1/lecturer/in-class-shifts`, `GET .../active-password`, `PUT .../submissions/{subId}/grade`, `POST .../finalize-grades`) | ❌ | **✅** | ❌ | ❌ | ❌ | **✅** |
| Ca thi trong lớp & TOTP 5m (R6 - MF-05): SV nhập mã vào thi (`POST /api/v1/student/in-class-shifts/{id}/join`) | **✅** | ❌ | ❌ | ❌ | ❌ | **✅** |
| Quản trị hệ thống, người dùng, cấu hình & Audit Logs (`/api/v1/admin/*`) | ❌ | ❌ | ❌ | ❌ | ❌ | **✅** |
| DLQ Replay (MF-01/02): Giám sát & kích hoạt replay luồng Luyện tập & Thi thử (`/api/v1/admin/dlq/*`) | ❌ | ❌ | **❌** | ❌ | ❌ | **✅** |
| DLQ Replay (MF-04): Giám sát & kích hoạt replay luồng Thi thật phòng Lab (`/api/v1/examination-officer/dlq/*`) | ❌ | ❌ | **❌** | **✅** | ❌ | **✅** |

*Ghi chú quan trọng:* Tính năng AI sinh câu hỏi từ CLO môn học (`POST /api/v1/questions/generate-from-clo`) mở quyền sử dụng cho cả **Giảng viên (`lecturer`)** và **Trưởng Bộ Môn (`department_head`)**. Luồng luyện tập/thi thử tạo độc lập vào `practice_questions`; luồng thi thật tạo bám sát đề thi mẫu / ma trận cấu trúc (`exam_structures`, `slot_definitions`) của Trưởng Bộ Môn vào `exam_questions` (R8). Giảng viên tự thiết kế Barem Rubric riêng 10.0đ, tinh chỉnh câu hỏi và ấn **"Gửi lên cho Bộ Môn"** (`SUBMITTED_FOR_REVIEW`) để Trưởng Bộ Môn thẩm định (`APPROVED`, `NEEDS_REVISION`, `REJECTED`). Trưởng Bộ Môn nắm 100% quyền cấu hình Follow-up thi thật (`1–5` câu, mặc định `2`), định nghĩa khung `exam_structures` với `slot_definitions` (JSONB) và đóng gói các bộ đề `exam_sets` (`DRAFT` $\to$ `OFFICIAL` $\to$ `HANDED_OVER`) bàn giao cho Cán bộ Khảo thí (`examination_officer`) (R3). Cán bộ Khảo thí tạo kỳ thi, ca thi, phân bổ đề không trùng lặp (`uq_tickets_shift_exam_set`), xuất bản đề ca thi và công bố điểm. Giám thị (`proctor`) chỉ điểm danh ghế 1–40 (`POST /api/v1/proctor/shifts/{id}/attendance`) và báo cáo vi phạm phòng thi (`POST /api/v1/proctor/shifts/{id}/violations`), bị loại bỏ khỏi quản lý ca thi (R4). Về DLQ Replay (R2): `/api/v1/admin/dlq/*` (MF-01/02) thuộc `admin` và `/api/v1/examination-officer/dlq/*` (MF-04) thuộc `examination_officer`; cắt bỏ hoàn toàn quyền DLQ khỏi `department_head`. Về Phúc khảo nội bộ 5 bước (R7): Sinh viên nộp đơn (`POST /api/v1/student/appeals`) $\to$ Cán bộ Khảo thí tiếp nhận & thẩm tra chuyển giao (`POST /api/v1/examination-officer/appeals/{id}/forward-to-head`) $\to$ Trưởng Bộ Môn phân công Giảng viên chấm lại (`POST /api/v1/department-head/appeals/{id}/assign-lecturer`) $\to$ Giảng viên chấm lại (`PUT /api/v1/lecturer/appeals/{id}/regrade`) $\to$ Trưởng Bộ Môn phê duyệt quyết định (`POST /api/v1/department-head/appeals/{id}/decision`).

---

## 5. DANH MỤC API CHI TIẾT THEO TÍNH NĂNG & PHÂN HỆ

#### Bảng Mục Lục API Nhanh (Quick Navigation Index)
| STT | Phân hệ API | Tiền tố Endpoint | Vai trò chính |
|:---:|:---|:---|:---|
| 5.1 | Quản lý Học thuật | `/api/v1/academic/*` | Admin, Lecturer, Student |
| 5.2 | Quản lý Barem Rubric | `/api/v1/rubrics/*` | Lecturer, Department Head |
| 5.3 | Ngân hàng Câu hỏi & Đóng gói Bộ đề | `/api/v1/questions/*`, `/api/v1/exam-sets/*` | Lecturer, Department Head |
| 5.4 | Chuẩn đầu ra CLO & AI Gen | `/api/v1/courses/*/clos`, `/api/v1/questions/generate-from-clo` | Lecturer, Department Head |
| 5.5 | Luyện tập tự do MF-01 | `/api/v1/practice/*` | Student |
| 5.6 | Thi thử bấm giờ MF-02 | `/api/v1/mock-exams/*` | Student |
| 5.7 | Quản lý Kỳ thi & Ca thi Phòng Lab MF-04 | `/api/v1/official-exams/*` | Examination Officer (tạo kỳ thi, ca thi, xuất bản đề, công bố điểm), Department Head (chỉ cấu hình Follow-up thi thật `1–5` câu mặc định `2`) |
| 5.8 | Hậu kiểm & Công bố điểm | `/api/v1/official-exams/shifts/*` | Lecturer, Examination Officer |
| 5.9 | Quản trị DLQ & Audit | `/api/v1/admin/dlq/*` (MF-01/02), `/api/v1/examination-officer/dlq/*` (MF-04), `/api/v1/admin/*` | Admin (DLQ MF-01/02 & Audit), Examination Officer (DLQ MF-04) |
| 5.10 | Phúc khảo nội bộ 5 bước MF-04 | `/api/v1/student/appeals`, `/api/v1/examination-officer/appeals/*`, `/api/v1/department-head/appeals/*`, `/api/v1/lecturer/appeals/*` | Student, Examination Officer, Department Head, Lecturer |
| 5.11 | Thông báo trong ứng dụng FE-11 | `/api/v1/notifications` | All Roles |
| 5.12 | Giám thị Phòng Lab (`/api/v1/proctor/*`) & Dashboard GV | `/api/v1/proctor/shifts/{id}/attendance`, `/api/v1/proctor/shifts/{id}/violations`, `/api/v1/lecturer/dashboard` | Proctor (chỉ điểm danh ghế 1–40 & báo cáo vi phạm phòng thi), Lecturer, Department Head |
| 5.13 | Chuỗi phân cấp học thuật 4 cấp (R5) | `/api/v1/admin/courses/{id}/assign-department-head`, `/api/v1/department-head/courses/{id}/lecturers`, `/api/v1/lecturer/classes`, `/api/v1/student/classes/join` | Admin, Department Head, Lecturer, Student |
| 5.14 | Ca thi trong lớp & mật khẩu tự động xoay vòng 5 phút (R6 - MF-05) | `/api/v1/lecturer/in-class-shifts/*`, `/api/v1/student/in-class-shifts/{id}/join` | Lecturer, Student |
| 5.15 | Phân tách 2 luồng tạo câu hỏi (R8) & Dịch vụ Giọng nói STT/TTS | `/api/v1/practice/questions`, `/api/v1/official-exams/questions`, `/api/v1/speech/*` | Lecturer, Department Head, Public |

### 5.1. Quản lý Học thuật: Học kỳ, Môn học, Lớp học & Danh sách Sinh viên
*(Phân hệ CSDL: `semesters`, `courses`, `classes`, `class_enrollments`)*

#### Lấy danh sách học kỳ
- **Endpoint:** `GET /api/v1/academic/semesters`
- **Quyền:** `student`, `lecturer`, `department_head`, `proctor`, `admin`
- **Response 200 OK:**
```json
[
  {
    "id": "11111111-0000-0000-0000-000000000001",
    "code": "FA26",
    "name": "Fall 2026",
    "startDate": "2026-09-01T00:00:00Z",
    "endDate": "2026-12-31T23:59:59Z",
    "isActive": true
  }
]
```

#### Tạo mới học kỳ (FE-08)
- **Endpoint:** `POST /api/v1/academic/semesters`
- **Quyền:** `admin`
- **Request Body:**
```json
{
  "code": "SP27",
  "name": "Spring 2027",
  "startDate": "2027-01-05T00:00:00Z",
  "endDate": "2027-05-15T23:59:59Z",
  "isActive": true
}
```
- **Response 201 Created:**
```json
{
  "id": "11111111-0000-0000-0000-000000000002",
  "code": "SP27",
  "name": "Spring 2027",
  "startDate": "2027-01-05T00:00:00Z",
  "endDate": "2027-05-15T23:59:59Z",
  "isActive": true,
  "createdAt": "2026-10-10T08:00:00Z"
}
```
- **Response 400 Bad Request:** Nếu mã học kỳ `code` đã tồn tại hoặc `endDate <= startDate`.

#### Cập nhật học kỳ (FE-08)
- **Endpoint:** `PUT /api/v1/academic/semesters/{id}`
- **Quyền:** `admin`
- **Request Body:**
```json
{
  "name": "Spring 2027 (Điều chỉnh)",
  "startDate": "2027-01-10T00:00:00Z",
  "endDate": "2027-05-20T23:59:59Z",
  "isActive": true
}
```
- **Response 200 OK:**
```json
{
  "id": "11111111-0000-0000-0000-000000000002",
  "code": "SP27",
  "name": "Spring 2027 (Điều chỉnh)",
  "startDate": "2027-01-10T00:00:00Z",
  "endDate": "2027-05-20T23:59:59Z",
  "isActive": true,
  "updatedAt": "2026-10-10T08:30:00Z"
}
```
- **Response 404 Not Found:** Nếu không tìm thấy học kỳ tương ứng.


#### Lấy danh sách môn học
- **Endpoint:** `GET /api/v1/courses` (Alias: `GET /api/v1/academic/courses`)
- **Quyền:** `student`, `lecturer`, `department_head`, `examination_officer`, `proctor`, `admin`
- **Response 200 OK (`List<CourseDto>`):**
```json
[
  {
    "id": "22222222-0000-0000-0000-000000000001",
    "code": "PRN231",
    "name": "Building Cross-Platform Back-End Applications with .NET",
    "credits": 3,
    "description": "Phát triển ứng dụng Backend đa nền tảng với .NET 8, RESTful Web API và Clean Architecture",
    "semesterId": "11111111-0000-0000-0000-000000000001",
    "departmentHeadUserId": "e4c5b2a1-0001-4000-8000-000000000005",
    "hasFollowUp": true,
    "transcriptBufferSeconds": 60,
    "maxFollowUpQuestions": 2,
    "maxMockExamsPerDay": 3,
    "mockExamDurationMinutes": 30,
    "mockExamTotalQuestions": 5,
    "mockExamBloomDistribution": "{"Understand": 2, "Apply": 2, "Analyze": 1}",
    "examInputMode": "VoiceWithTranscriptEdit",
    "isActive": true
  }
]
```

#### Tạo mới & Cập nhật môn học (Admin — Chuỗi học thuật R5)
- **Tạo mới:** `POST /api/v1/courses` (Alias: `POST /api/v1/admin/courses`)
- **Cập nhật:** `PUT /api/v1/courses/{id}` (Alias: `PUT /api/v1/admin/courses/{id}`)
- **Quyền:** `admin`
- **Request Body (`CreateCourseRequestDto` / `UpdateCourseRequestDto`):**
```json
{
  "code": "PRN231",
  "name": "Building Cross-Platform Back-End Applications with .NET",
  "credits": 3,
  "description": "Phát triển ứng dụng Backend đa nền tảng với .NET 8",
  "semesterId": "11111111-0000-0000-0000-000000000001",
  "departmentHeadUserId": "e4c5b2a1-0001-4000-8000-000000000005",
  "maxMockExamsPerDay": 3,
  "mockExamDurationMinutes": 30,
  "mockExamTotalQuestions": 5,
  "isActive": true
}
```

#### Lấy & Cập nhật cấu hình động môn học (Admin / Trưởng Bộ Môn)
- **Lấy cấu hình:** `GET /api/v1/courses/{id}/configurations` (Alias: `GET /api/v1/courses/{id}/exam-config`)
- **Cập nhật cấu hình:** `PUT /api/v1/courses/{id}/configurations` (Alias: `PUT /api/v1/courses/{id}/exam-config`)
- **Quyền:** `department_head`, `admin` (Giảng viên `lecturer` bị chặn `HTTP 403 Forbidden`; riêng trường `maxMockExamsPerDay` chỉ `admin` được phép truyền cập nhật).
- **Mô tả:** Trưởng Bộ Môn cấu hình tính năng hỏi xoáy Follow-up (`hasFollowUp`, `maxFollowUpQuestions` 1–5 câu), thời gian đệm hiệu đính (`transcriptBufferSeconds` 10–300s), phương thức làm bài (`examInputMode`) và cấu hình đề thi thử mặc định của môn học (`mockExamDurationMinutes` 5–180 phút, `mockExamTotalQuestions` 1–20 câu, `mockExamBloomDistribution`).
- **Request Body (`UpdateCourseConfigRequestDto`):**
```json
{
  "hasFollowUp": true,
  "transcriptBufferSeconds": 60,
  "maxFollowUpQuestions": 2,
  "examInputMode": "VoiceWithTranscriptEdit",
  "mockExamDurationMinutes": 30,
  "mockExamTotalQuestions": 5,
  "mockExamBloomDistribution": "{"Understand": 2, "Apply": 2, "Analyze": 1}",
  "maxMockExamsPerDay": 3
}
```
- **Response 200 OK (`CourseConfigurationDto`):**
```json
{
  "courseId": "22222222-0000-0000-0000-000000000001",
  "code": "PRN231",
  "name": "Building Cross-Platform Back-End Applications with .NET",
  "transcriptBufferSeconds": 60,
  "maxFollowUpQuestions": 2,
  "hasFollowUp": true,
  "examInputMode": "VoiceWithTranscriptEdit",
  "maxMockExamsPerDay": 3,
  "mockExamDurationMinutes": 30,
  "mockExamTotalQuestions": 5,
  "mockExamBloomDistribution": "{"Understand": 2, "Apply": 2, "Analyze": 1}"
}
```
- **Response 422 Unprocessable Entity:** Nếu `transcriptBufferSeconds` ngoài khoảng 10–300s, `maxFollowUpQuestions` ngoài khoảng 1–5 câu, `mockExamDurationMinutes` ngoài khoảng 5–180 phút, hoặc `mockExamTotalQuestions` ngoài khoảng 1–20 câu.

#### Quản lý lớp học & sinh viên trong lớp
- **Endpoint:** `GET /api/v1/academic/classes?courseId={courseId}`
- **Quyền:** `lecturer`, `department_head`, `admin`
- **Response 200 OK:**
```json
[
  {
    "id": "33333333-0000-0000-0000-000000000001",
    "code": "SE1801-NET",
    "courseId": "22222222-0000-0000-0000-000000000001",
    "courseCode": "PRN231",
    "semesterId": "11111111-0000-0000-0000-000000000001",
    "lecturerId": "e4c5b2a1-0001-4000-8000-000000000002",
    "lecturerName": "Nguyễn Trọng Tốt",
    "maxStudents": 35,
    "enrolledStudentsCount": 32
  }
]
```

---

### 5.2. Quản lý Barem Đánh giá (Rubrics & Criteria $\sum = 10.0$đ) — MF-03
*(Phân hệ CSDL: `rubrics`, `rubric_criteria`)*

#### Tạo Rubric mới (ACID Transaction)
- **Endpoint:** `POST /api/v1/rubrics`
- **Quyền:** `lecturer`, `department_head`, `admin`
- **Quy tắc bất biến:** Tổng `maxScore` của mảng `criteria` bắt buộc bằng 10.00. Nếu khác 10.00 $\rightarrow$ Trả `422 Unprocessable Entity`.
- **Request Body:**
```json
{
  "courseId": "22222222-0000-0000-0000-000000000001",
  "name": "Barem Vấn đáp Clean Architecture & Dependency Inversion",
  "description": "Đánh giá hiểu biết về Clean Architecture 4 tầng và nguyên lý DIP",
  "criteria": [
    {
      "criterionName": "Định nghĩa và bản chất nguyên lý DIP",
      "maxScore": 3.00,
      "weight": 0.30,
      "bloomLevel": "Understand",
      "description": "Nêu rõ high-level modules không phụ thuộc low-level modules, cả hai phụ thuộc abstraction."
    },
    {
      "criterionName": "Áp dụng DIP vào Clean Architecture trong .NET",
      "maxScore": 4.00,
      "weight": 0.40,
      "bloomLevel": "Apply",
      "description": "Chỉ rõ cách Interface đặt ở Application/Domain, triển khai ở Infrastructure qua Dependency Injection."
    },
    {
      "criterionName": "Lợi ích Unit Testing và Loose Coupling",
      "maxScore": 3.00,
      "weight": 0.30,
      "bloomLevel": "Analyze",
      "description": "Khả năng mock Interface để test độc lập, thay đổi database không ảnh hưởng nghiệp vụ lõi."
    }
  ]
}
```
- **Response 201 Created:**
```json
{
  "id": "44444444-0000-0000-0000-000000000001",
  "name": "Barem Vấn đáp Clean Architecture & Dependency Inversion",
  "totalMaxScore": 10.00,
  "criteriaCount": 3,
  "createdAt": "2026-10-01T08:30:00Z"
}
```

---

### 5.3. Ngân hàng Câu hỏi Luyện tập & Câu hỏi Thi cử (Bóc tách riêng)
*(Phân hệ CSDL: `practice_questions` vs `exam_questions`)*

#### 1. Tạo câu hỏi Luyện tập (Kho mở — Sinh viên xem được đáp án mẫu & ý chính sau khi làm)
- **Endpoint:** `POST /api/v1/practice/questions`
- **Quyền:** `lecturer`, `department_head`, `admin`
- **Ràng buộc nghiệp vụ (Invariants):**
  1. `sampleAnswer` (Đáp án mẫu): **Bắt buộc $\ge 50$ ký tự**. Vi phạm bị chặn bởi FluentValidation với mã `HTTP 422 Unprocessable Entity`.
  2. `rubricId`: Phải trỏ tới Barem Rubric hợp lệ có $\sum \text{maxScore} \equiv 10.00$đ.
  3. `source`: Cố định `'manual'` khi tạo thủ công (hoặc `'ai_generated'` khi sinh từ AI).

##### Bảng đặc tả DTO Yêu cầu (`CreatePracticeQuestionRequestDto`)
| Thuộc tính | Kiểu dữ liệu | Bắt buộc / Nullable | Ràng buộc / Validation | Mô tả nghiệp vụ |
|:---|:---|:---:|:---|:---|
| `courseId` | `uuid` | Bắt buộc (Non-null) | FK tồn tại trong `courses` | Môn học trực thuộc |
| `rubricId` | `uuid` | Bắt buộc (Non-null) | FK tồn tại trong `rubrics` | Barem Rubric $\sum = 10.0$đ |
| `title` | `string` | Bắt buộc (Non-null) | Tối đa 300 ký tự | Tiêu đề câu hỏi |
| `content` | `string` | Bắt buộc (Non-null) | Không rỗng | Nội dung đề bài chi tiết |
| `sampleAnswer` | `string` | Bắt buộc (Non-null) | **Độ dài $\ge 50$ ký tự** | Đáp án mẫu chuẩn mực |
| `keyPoints` | `List<string>` | Bắt buộc (Non-null) | Tối thiểu 1 ý chính | Các luận điểm then chốt |
| `difficulty` | `string` | Bắt buộc (Non-null) | `easy` \| `medium` \| `hard` | Độ khó câu hỏi |
| `bloomLevel` | `string` | Bắt buộc (Non-null) | 6 mức Bloom | Bậc nhận thức Bloom (1 đến 6) |
| `source` | `string` | Bắt buộc (Non-null) | Cố định `'manual'` | Nguồn gốc tạo đề |
| `hasFollowUp` | `bool` | Bắt buộc (Non-null) | `true` \| `false` | Cờ bật hỏi xoáy Follow-up |
| `followUpPrompt`| `string` | Nullable | Tuỳ chọn | Gợi ý câu hỏi phụ phản biện |
| `imageUrl` | `string` | Nullable | Tối đa 1000 ký tự; chỉ hợp lệ khi `source == "manual"` | Đường dẫn ảnh/sơ đồ kỹ thuật minh họa câu hỏi (UML, ERD, Sequence, Kiến trúc) đã tải lên qua `POST /api/v1/questions/upload-image` |
| `imageDescription` | `string` | Nullable | Tuỳ chọn | Mô tả chi tiết nội dung sơ đồ/hình ảnh để cung cấp ngữ cảnh chấm thi đa phương thức (Multimodal AI Grading) |

- **Request Body:**
```json
{
  "courseId": "22222222-0000-0000-0000-000000000001",
  "rubricId": "44444444-0000-0000-0000-000000000001",
  "title": "Phân tích nguyên lý Dependency Inversion trong Clean Architecture",
  "content": "Hãy giải thích nguyên lý Dependency Inversion (DIP) và minh họa cách áp dụng trong Clean Architecture .NET.",
  "sampleAnswer": "Dependency Inversion Principle (DIP) là nguyên lý chữ D trong SOLID. High-level modules không nên phụ thuộc vào low-level modules; cả hai phải phụ thuộc vào abstractions (Interface). Trong Clean Architecture, tầng Application/Domain định nghĩa abstraction và tầng Infrastructure hiện thực hóa nó thông qua cơ chế Dependency Injection trong IoC Container.",
  "keyPoints": [
    "High-level không phụ thuộc low-level",
    "Cùng phụ thuộc vào abstraction (Interface)",
    "Domain/Application khai báo Interface",
    "Infrastructure hiện thực hóa qua IoC Container"
  ],
  "difficulty": "medium",
  "bloomLevel": "Analyze",
  "source": "manual",
  "hasFollowUp": true,
  "followUpPrompt": "Nếu cần thay đổi thư viện ngoài từ Entity Framework sang Dapper, tầng nào bị ảnh hưởng?"
}
```
- **Response 201 Created:**
```json
{
  "id": "55555555-0000-0000-0000-000000000001",
  "courseId": "22222222-0000-0000-0000-000000000001",
  "rubricId": "44444444-0000-0000-0000-000000000001",
  "title": "Phân tích nguyên lý Dependency Inversion trong Clean Architecture",
  "source": "manual",
  "isActive": true,
  "createdAt": "2026-10-02T10:00:00Z"
}
```

---

#### 2. Tạo câu hỏi Thi cử chính thức (Kho bảo mật — Yêu cầu `approved_by`, đáp án mẫu được bảo mật)
- **Endpoint:** `POST /api/v1/official-exams/questions`
- **Quyền:** `department_head`, `admin`
- **Ràng buộc nghiệp vụ (Invariants):**
  1. `sampleAnswer` (Đáp án chuẩn đối chiếu): **Bắt buộc $\ge 50$ ký tự**, được bảo mật tuyệt đối, chỉ phục vụ cho AI CoT chấm thi trong kỳ thi thật phòng Lab MF-04.
  2. Bắt buộc ghi nhận `approvedBy` là User ID của cán bộ thẩm định (Trưởng Bộ Môn hoặc Admin).
  3. `rubricId`: Barem Rubric bắt buộc có $\sum \text{maxScore} \equiv 10.00$đ.

- **Request Body:**
```json
{
  "courseId": "22222222-0000-0000-0000-000000000001",
  "rubricId": "44444444-0000-0000-0000-000000000001",
  "title": "Thiết kế Middleware thẩm định JWT trong ASP.NET Core 8",
  "content": "Hãy phân tích chu trình Request Pipeline và cách thức đăng ký JwtBearerHandler bảo đảm kiểm tra quyền RBAC.",
  "sampleAnswer": "Quy trình xác thực JWT trong ASP.NET Core 8 được thực hiện qua chuỗi Middleware. JwtBearerHandler giải mã token từ Authorization Header, kiểm tra tính hợp lệ của chữ ký HMAC/RSA, kiểm tra thời hạn exp, và trích xuất danh sách Claims (Role, UserId) gán vào HttpContext.User.ClaimsPrincipal để các Authorization Policies kiểm soát quyền truy cập.",
  "keyPoints": [
    "Giải mã header Authorization Bearer",
    "Xác thực chữ ký số HMAC/RSA và hạn exp",
    "Nạp ClaimsPrincipal vào HttpContext.User"
  ],
  "difficulty": "hard",
  "bloomLevel": "Evaluate",
  "source": "manual",
  "approvedBy": "e4c5b2a1-0001-4000-8000-000000000005"
}
```
- **Response 201 Created:**
```json
{
  "id": "66666666-0000-0000-0000-000000000001",
  "courseId": "22222222-0000-0000-0000-000000000001",
  "rubricId": "44444444-0000-0000-0000-000000000001",
  "title": "Thiết kế Middleware thẩm định JWT trong ASP.NET Core 8",
  "source": "manual",
  "approvedBy": "e4c5b2a1-0001-4000-8000-000000000005",
  "isActive": true,
  "createdAt": "2026-10-02T10:00:00Z"
}
```


---

#### 3. Tải lên Hình ảnh / Sơ đồ Minh họa cho Câu hỏi Thủ công (Question Image Upload — Multimodal Support)
- **Endpoint chính:** `POST /api/v1/questions/upload-image`
- **Endpoint bí danh (Alias tương thích):** `POST /api/v1/storage/upload-question-image`
- **Quyền:** `lecturer`, `department_head`, `admin`
- **Content-Type:** `multipart/form-data`
- **Form Data Fields:**
  - `file` (`IFormFile`, bắt buộc): Tệp hình ảnh sơ đồ kỹ thuật (`PNG`, `JPG/JPEG`, `WEBP`), dung lượng tối đa **5MB** (`5,242,880 bytes`).
  - `description` (hoặc `imageDescription`, `string`, tùy chọn): Mô tả nội dung sơ đồ/hình ảnh để hỗ trợ AI chấm điểm đa phương thức (Multimodal AI Grading).
- **Ràng buộc bảo mật & Lưu trữ:**
  1. Kiểm tra dung lượng `<= 5MB` (vượt ngưỡng trả `HTTP 413 Payload Too Large`).
  2. Kiểm tra chữ ký nhị phân (Magic Bytes: `PNG` `89 50 4E 47 0D 0A 1A 0A`, `JPG/JPEG` `FF D8 FF`, `WEBP` `RIFF....WEBP`). Sai định dạng trả `HTTP 415 Unsupported Media Type`.
  3. Lưu trữ kép: Lưu bản tĩnh tại `wwwroot/uploads/questions/{yyyyMM}/{guid}.{ext}` (phục vụ qua `/uploads/questions/{yyyyMM}/{guid}.{ext}`) đồng thời đồng bộ lên Cloudflare R2 (`objectKey = questions/{yyyyMM}/{guid}.{ext}`).
  4. **Quy tắc nghiệp vụ:** Chỉ câu hỏi biên soạn thủ công (`source == "manual"`) mới được đính kèm `imageUrl`. Khi Trưởng BM duyệt đề với `targetUsageScope: "both"`, `imageUrl` và `imageDescription` được tự động đồng bộ sang cả 2 kho `exam_questions` và `practice_questions`.
- **Response 200 OK:**
```json
{
  "imageUrl": "/uploads/questions/202610/a1b2c3d4e5f6478980123456789abcde.png",
  "objectKey": "questions/202610/a1b2c3d4e5f6478980123456789abcde.png",
  "r2PublicUrl": "https://r2.oralexam.edu.vn/questions/202610/a1b2c3d4e5f6478980123456789abcde.png",
  "contentType": "image/png",
  "sizeBytes": 245120,
  "sha256Hash": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
  "imageDescription": "Sơ đồ 4 vòng tròn đồng tâm Clean Architecture thể hiện hướng mũi tên phụ thuộc hướng vào tầng Domain trung tâm.",
  "uploadedAt": "2026-10-21T07:00:00Z"
}
```

---

### 5.4. Quản lý Chuẩn đầu ra (Course CLOs 3NF) & Sinh Câu Hỏi Tự Động Bằng AI (AI Generator) — MF-03
*(Phân hệ CSDL: `courses`, `course_clos`, `rubrics`, `rubric_criteria`, `practice_questions`, `exam_questions`, `exam_structures`, `exam_sets`)*

#### 1. Lấy danh sách Chuẩn đầu ra CLOs của môn học từ CSDL 3NF
- **Endpoint:** `GET /api/v1/courses/{courseId}/clos`
- **Quyền truy cập:** `lecturer`, `department_head`, `examination_officer`, `admin`
- **Mô tả:** Trích xuất toàn bộ danh sách Chuẩn đầu ra CLOs (Course Learning Outcomes) được lưu trữ chuẩn hóa 3NF trong bảng `course_clos` của môn học. Hỗ trợ tìm kiếm, lọc theo mức độ nhận thức Bloom và phân trang.
- **Path Parameters:**
  - `courseId` (`uuid`, bắt buộc): Định danh duy nhất của môn học trong hệ thống.
- **Query Parameters (Phân trang & Lọc CLOs):**
  - `pageNumber` (`int`, tuỳ chọn, mặc định: `1`, ràng buộc: $\ge 1$): Số thứ tự trang danh mục CLO.
  - `pageSize` (`int`, tuỳ chọn, mặc định: `20`, ràng buộc: $1 \le \text{pageSize} \le 100$): Số lượng CLO trên mỗi trang.
  - `search` (`string`, tuỳ chọn): Chuỗi từ khóa tìm kiếm theo mã hoặc mô tả CLO.
  - `bloomLevel` (`string`, tuỳ chọn): Mức nhận thức Bloom mục tiêu (`Remember`, `Understand`, `Apply`, `Analyze`, `Evaluate`, `Create`).

##### Bảng đặc tả DTO Phản hồi (`CourseClosResponseDto`)
| Thuộc tính | Kiểu dữ liệu | Bắt buộc / Nullable | Ràng buộc / Enum | Mô tả nghiệp vụ |
|:---|:---|:---:|:---|:---|
| `courseId` | `uuid` | Bắt buộc (Non-null) | UUID hợp lệ | Định danh môn học |
| `courseCode` | `string` | Bắt buộc (Non-null) | Mã môn chuẩn (vd: `PRN231`, `PRN211`, `SWP391`) | Mã môn học chuẩn FPTU |
| `courseName` | `string` | Bắt buộc (Non-null) | Tối đa 200 ký tự | Tên đầy đủ của môn học |
| `totalClos` | `int` | Bắt buộc (Non-null) | $\ge 0$ | Tổng số CLOs của môn học |
| `clos` | `List<CourseCloDto>` | Bắt buộc (Non-null) | Danh sách CLOs | Danh sách chuẩn đầu ra chi tiết |

*Chi tiết `CourseCloDto`:*
- `id` (`uuid`, Non-null): Khóa chính chuẩn đầu ra trong bảng `course_clos`.
- `courseId` (`uuid`, Non-null): Khóa ngoại liên kết môn học.
- `cloCode` (`string`, Non-null): Mã chuẩn đầu ra (vd: `CLO1`, `CLO2`, `CLO3`, `CLO4`).
- `description` (`string`, Non-null): Nội dung diễn giải chi tiết chuẩn đầu ra kiến thức/kỹ năng.
- `targetBloomLevel` (`string`, Non-null): Mức nhận thức Bloom mục tiêu (`Remember`, `Understand`, `Apply`, `Analyze`, `Evaluate`, `Create`).
- `isActive` (`bool`, Non-null): Trạng thái hoạt động của chuẩn đầu ra.
- `createdAt` (`DateTime`, Non-null): Thời điểm tạo UTC.

- **Response 200 OK:**
```json
{
  "courseId": "22222222-0000-0000-0000-000000000001",
  "courseCode": "PRN231",
  "courseName": "Building Cross-Platform Back-End Applications with .NET",
  "totalClos": 4,
  "clos": [
    {
      "id": "33333333-0000-0000-0000-000000000001",
      "courseId": "22222222-0000-0000-0000-000000000001",
      "cloCode": "CLO1",
      "description": "Hiểu và giải thích các nguyên lý Clean Architecture, SOLID và Design Patterns trong .NET",
      "targetBloomLevel": "Understand",
      "isActive": true,
      "createdAt": "2026-10-01T08:00:00Z"
    },
    {
      "id": "33333333-0000-0000-0000-000000000002",
      "courseId": "22222222-0000-0000-0000-000000000001",
      "cloCode": "CLO2",
      "description": "Áp dụng Entity Framework Core và Dapper để xây dựng tầng Data Access Layer tối ưu",
      "targetBloomLevel": "Apply",
      "isActive": true,
      "createdAt": "2026-10-01T08:00:00Z"
    },
    {
      "id": "33333333-0000-0000-0000-000000000003",
      "courseId": "22222222-0000-0000-0000-000000000001",
      "cloCode": "CLO3",
      "description": "Phân tích và thiết kế Web API chuẩn RESTful, tích hợp Authentication JWT và RBAC",
      "targetBloomLevel": "Analyze",
      "isActive": true,
      "createdAt": "2026-10-01T08:00:00Z"
    },
    {
      "id": "33333333-0000-0000-0000-000000000004",
      "courseId": "22222222-0000-0000-0000-000000000001",
      "cloCode": "CLO4",
      "description": "Đánh giá và tối ưu hiệu năng ứng dụng, xử lý Concurrency và Resilient Messaging",
      "targetBloomLevel": "Evaluate",
      "isActive": true,
      "createdAt": "2026-10-01T08:00:00Z"
    }
  ]
}
```

##### Phản hồi lỗi chuẩn hóa RFC 7807 (`GET /api/v1/courses/{courseId}/clos`)
- **401 Unauthorized:**
```json
{
  "type": "https://oralexam.fpt.edu.vn/errors/unauthorized",
  "title": "Unauthorized",
  "status": 401,
  "detail": "Yêu cầu xác thực Bearer Token hợp lệ để truy cập danh sách CLO môn học.",
  "instance": "/api/v1/courses/22222222-0000-0000-0000-000000000001/clos"
}
```
- **403 Forbidden:**
```json
{
  "type": "https://oralexam.fpt.edu.vn/errors/forbidden",
  "title": "Forbidden",
  "status": 403,
  "detail": "Bạn không có quyền truy cập chuẩn đầu ra môn học. Yêu cầu vai trò 'lecturer', 'department_head', 'examination_officer' hoặc 'admin'.",
  "instance": "/api/v1/courses/22222222-0000-0000-0000-000000000001/clos"
}
```
- **404 Not Found:**
```json
{
  "type": "https://oralexam.fpt.edu.vn/errors/course-not-found",
  "title": "Course Not Found",
  "status": 404,
  "detail": "Không tìm thấy môn học tương ứng với courseId đã cung cấp.",
  "instance": "/api/v1/courses/22222222-0000-0000-0000-000000000001/clos"
}
```

---

#### 1b. Thêm mới Chuẩn đầu ra CLO vào CSDL 3NF
- **Endpoint:** `POST /api/v1/courses/{courseId}/clos`
- **Quyền truy cập:** `department_head`, `admin`
- **Mô tả:** Trưởng Bộ Môn hoặc Quản trị viên nhập liệu chuẩn đầu ra mới cho môn học, lưu trữ trực tiếp vào bảng `course_clos`.
- **Path Parameters:**
  - `courseId` (`uuid`, bắt buộc): Định danh môn học.
- **Request Body (`CreateCourseCloRequestDto`):**
```json
{
  "cloCode": "CLO5",
  "description": "Vận dụng kiểm thử tự động xUnit, Moq và NetArchTest để khóa ranh giới kiến trúc Clean Architecture",
  "targetBloomLevel": "Apply"
}
```
- **Response 201 Created:**
```json
{
  "id": "33333333-0000-0000-0000-000000000005",
  "courseId": "22222222-0000-0000-0000-000000000001",
  "cloCode": "CLO5",
  "description": "Vận dụng kiểm thử tự động xUnit, Moq và NetArchTest để khóa ranh giới kiến trúc Clean Architecture",
  "targetBloomLevel": "Apply",
  "isActive": true,
  "createdAt": "2026-10-01T08:30:00Z"
}
```
- **Response 409 Conflict:** Khi mã CLO (`cloCode`) đã tồn tại trong môn học.

---

#### 2. Kích hoạt AI sinh câu hỏi từ dữ liệu CLOs (Kèm kiểm tra cờ trùng lặp isFlagged)
- **Endpoint:** `POST /api/v1/questions/generate-from-clo`
- **Quyền:** `department_head`, `admin`. **Giảng viên sử dụng AI sinh câu hỏi theo barem của mình**, kích hoạt Gemini AI Engine phân tích chuẩn đầu ra `course_clos` để sinh bộ câu hỏi vấn đáp kèm Barem Rubric riêng 10.0đ và Model Answer $\ge 50$ ký tự, tự do chỉnh sửa trước khi gửi lên Bộ Môn thẩm định (`SUBMITTED_FOR_REVIEW`). Hệ thống tự động so khớp độ tương đồng ngữ nghĩa với ngân hàng câu hỏi hiện có; nếu phát hiện câu hỏi tương đồng cao, hệ thống tự động gắn cờ cảnh báo trùng lặp `isFlagged: true` kèm lý do `flagReason`. Trưởng bộ môn (`department_head`) và Quản trị viên (`admin`) cũng có quyền thực hiện. Nếu người dùng mang vai trò khác (`proctor`, `student`, `examination_officer`), hệ thống chặn ngay lập tức và trả về `HTTP 403 Forbidden`.
- **Mô tả:** Giảng viên hoặc Trưởng bộ môn chọn môn học, chọn CLOs/chủ đề cần tạo câu hỏi. Hệ thống gửi thông tin sang Gemini AI Engine để sinh bộ câu hỏi vấn đáp kèm Barem Rubric $\sum = 10.0$đ và Đáp án mẫu chuẩn (Model Answer $\ge 50$ ký tự).

##### Bảng đặc tả DTO Yêu cầu (`GenerateQuestionsFromCloRequestDto`)
| Thuộc tính | Kiểu dữ liệu | Bắt buộc / Nullable | Ràng buộc / Validation | Mô tả nghiệp vụ |
|:---|:---|:---:|:---|:---|
| `courseId` | `uuid` | Bắt buộc (Non-null) | Khóa ngoại tồn tại trong `courses` | Môn học cần sinh câu hỏi |
| `selectedCloCodes` | `List<string>` | Bắt buộc (Non-null) | Ít nhất 1 CLO hợp lệ trong `course_clos` | Danh sách CLOs cần bao phủ |
| `topics` | `List<string>` | Bắt buộc (Non-null) | Ít nhất 1 chủ đề kiến thức | Danh sách chủ đề bài học |
| `numberOfQuestions` | `int` | Bắt buộc (Non-null) | $1 \le \text{count} \le 10$ | Số lượng câu hỏi cần sinh trong 1 mẻ |
| `usageScope` | `string` | Bắt buộc (Non-null) | `practice` \| `exam` \| `shared` | Phạm vi sử dụng dự kiến |
| `targetBloomLevels` | `List<string>` | Bắt buộc (Non-null) | 1-6 mức: `Remember` đến `Create` | Các bậc Bloom mục tiêu phân bổ |
| `difficultyDistribution` | `DifficultyDistributionDto` | Bắt buộc (Non-null) | Tổng số câu `easy + medium + hard == numberOfQuestions` | Phân bổ tỷ lệ độ khó câu hỏi |

*Chi tiết `DifficultyDistributionDto`:*
- `easy` (`int`, Non-null): Số câu dễ ($\ge 0$).
- `medium` (`int`, Non-null): Số câu trung bình ($\ge 0$).
- `hard` (`int`, Non-null): Số câu khó ($\ge 0$).
- *Validation Rule:* `easy + medium + hard == numberOfQuestions` (nếu không bằng $\to$ `400 Bad Request`).

- **Request Body:**
```json
{
  "courseId": "22222222-0000-0000-0000-000000000001",
  "selectedCloCodes": ["CLO1", "CLO3"],
  "topics": [
    "Clean Architecture & Dependency Injection",
    "Web API Security, JWT & RBAC Authorization"
  ],
  "numberOfQuestions": 2,
  "usageScope": "shared",
  "targetBloomLevels": ["Understand", "Analyze"],
  "difficultyDistribution": {
    "easy": 0,
    "medium": 1,
    "hard": 1
  }
}
```

##### Bảng đặc tả DTO Phản hồi (`GenerateQuestionsFromCloResponseDto`)
| Thuộc tính | Kiểu dữ liệu | Bắt buộc / Nullable | Ràng buộc / Enum | Mô tả nghiệp vụ |
|:---|:---|:---:|:---|:---|
| `courseId` | `uuid` | Bắt buộc (Non-null) | UUID | Môn học |
| `courseCode` | `string` | Bắt buộc (Non-null) | Mã môn | Mã môn học |
| `generatedCount` | `int` | Bắt buộc (Non-null) | Bằng số câu yêu cầu | Số câu hỏi thực tế AI đã sinh thành công |
| `source` | `string` | Bắt buộc (Non-null) | Cố định `'ai_generated'` | Nguồn gốc câu hỏi |
| `questions` | `List<GeneratedQuestionDraftDto>` | Bắt buộc (Non-null) | Danh sách câu hỏi dự thảo | Danh sách câu hỏi kèm barem, đáp án và cờ trùng lặp |

*Chi tiết `GeneratedQuestionDraftDto`:*
- `tempId` (`string`, Non-null): Định danh tạm thời trong phiên preview (vd: `draft-clo-001`).
- `cloCode` (`string`, Non-null): Mã CLO mà câu hỏi đáp ứng.
- `title` (`string`, Non-null): Tiêu đề tóm tắt nội dung câu hỏi.
- `content` (`string`, Non-null): Nội dung đề bài câu hỏi vấn đáp chi tiết.
- `difficulty` (`string`, Non-null): Độ khó (`easy`, `medium`, `hard`).
- `bloomLevel` (`string`, Non-null): Mức Bloom (`Remember` đến `Create`).
- `source` (`string`, Non-null): Cố định `'ai_generated'`.
- `usageScope` (`string`, Non-null): `'practice'`, `'exam'`, hoặc `'shared'`.
- `modelAnswer` (`string`, Non-null): Câu trả lời mẫu chuẩn mực, **bắt buộc $\ge 50$ ký tự**.
- `keyPoints` (`List<string>`, Non-null): Danh sách các ý chính bắt buộc phải có trong câu trả lời.
- `hasFollowUp` (`bool`, Non-null): Cờ cho phép hỏi câu phụ chuyên sâu.
- `followUpPrompt` (`string`, Nullable): Nội dung gợi mở câu hỏi xoáy phản biện (nếu có).
- `isFlagged` (`bool`, Non-null): Cờ đánh dấu câu hỏi có dấu hiệu trùng lặp ngữ nghĩa với câu hỏi đã có.
- `flagReason` (`string`, Nullable): Diễn giải lý do gắn cờ trùng lặp (ví dụ: *"Trùng lặp 88% nội dung với câu hỏi ID 55555555-0000-0000-0000-000000000001"*).
- `rubric` (`RubricDraftDto`, Non-null): Barem đánh giá tương ứng.

*Chi tiết `RubricDraftDto`:*
- `name` (`string`, Non-null): Tên Barem đánh giá.
- `totalMaxScore` (`decimal`, Non-null): **Khóa cứng bất biến $10.00$**.
- `criteria` (`List<RubricCriterionDraftDto>`, Non-null): Tối thiểu 2 tiêu chí con, **$\sum \text{maxScore} \equiv 10.00$**.

*Chi tiết `RubricCriterionDraftDto`:*
- `criterionName` (`string`, Non-null): Tên tiêu chí đánh giá con.
- `maxScore` (`decimal`, Non-null): Điểm tối đa của tiêu chí ($> 0$).
- `weight` (`decimal`, Non-null): Trọng số tiêu chí ($0 < \text{weight} < 1$, $\sum \text{weight} \equiv 1.0$).
- `bloomLevel` (`string`, Non-null): Mức Bloom tương ứng tiêu chí.
- `description` (`string`, Non-null): Mô tả hướng dẫn cán bộ chấm và căn cứ đối chiếu AI.

- **Response 200 OK:**
```json
{
  "courseId": "22222222-0000-0000-0000-000000000001",
  "courseCode": "PRN231",
  "generatedCount": 2,
  "source": "ai_generated",
  "questions": [
    {
      "tempId": "draft-clo-001",
      "cloCode": "CLO1",
      "title": "Phân tích nguyên lý Dependency Inversion trong Clean Architecture",
      "content": "Hãy giải thích nguyên lý Dependency Inversion (DIP) và minh họa cách tổ chức các tầng trong Clean Architecture .NET để tuân thủ nguyên lý này.",
      "difficulty": "medium",
      "bloomLevel": "Understand",
      "source": "ai_generated",
      "usageScope": "shared",
      "modelAnswer": "Dependency Inversion Principle (DIP) là nguyên lý chữ D trong SOLID. Nguyên lý phát biểu rằng các module cấp cao không nên phụ thuộc vào module cấp thấp, cả hai nên phụ thuộc vào abstractions (Interface). Trong Clean Architecture, Domain và Application đóng vai trò module cấp cao khai báo các interfaces (như IApplicationDbContext, IUserRepository), trong khi tầng Infrastructure đóng vai trò module cấp thấp hiện thực hóa các interfaces đó. Nhờ cơ chế Dependency Injection trong Program.cs, luồng điều khiển phụ thuộc từ ngoài vào trong, đảm bảo tính độc lập và khả năng unit test.",
      "keyPoints": [
        "Module cấp cao không phụ thuộc module cấp thấp",
        "Cả hai phụ thuộc vào abstraction (Interface)",
        "Application/Domain định nghĩa abstraction, Infrastructure hiện thực hóa",
        "Dependency Injection IoC Container quản lý vòng đời phụ thuộc"
      ],
      "hasFollowUp": true,
      "followUpPrompt": "Nếu cần thay đổi cơ chế ORM từ EF Core sang Dapper thì tầng Application có cần sửa đổi code không? Vì sao?",
      "isFlagged": false,
      "flagReason": null,
      "rubric": {
        "name": "Barem Rubric CLO1 - Clean Architecture & DIP",
        "totalMaxScore": 10.00,
        "criteria": [
          {
            "criterionName": "Khái niệm và bản chất nguyên lý DIP",
            "maxScore": 3.00,
            "weight": 0.30,
            "bloomLevel": "Understand",
            "description": "Nêu rõ high-level và low-level modules đều phụ thuộc vào abstractions; abstractions không phụ thuộc chi tiết."
          },
          {
            "criterionName": "Hiện thực hóa DIP trong Clean Architecture",
            "maxScore": 4.00,
            "weight": 0.40,
            "bloomLevel": "Apply",
            "description": "Minh họa vị trí đặt Interface ở Application/Domain và Implementation ở Infrastructure; đăng ký qua IoC Service Collection."
          },
          {
            "criterionName": "Phân tích lợi ích kiến trúc và khả năng kiểm thử",
            "maxScore": 3.00,
            "weight": 0.30,
            "bloomLevel": "Analyze",
            "description": "Giải thích khả năng cô lập lỗi, mock interface trong Unit Testing và bảo vệ nghiệp vụ lõi khi thay đổi hạ tầng."
          }
        ]
      }
    },
    {
      "tempId": "draft-clo-002",
      "cloCode": "CLO3",
      "title": "Thiết kế cơ chế xác thực JWT và phân quyền RBAC trong ASP.NET Core Web API",
      "content": "Hãy phân tích quy trình xác thực người dùng bằng JWT Bearer Token kết hợp phân quyền Role-based Access Control (RBAC) trong Clean Architecture ASP.NET Core 8.",
      "difficulty": "hard",
      "bloomLevel": "Analyze",
      "source": "ai_generated",
      "usageScope": "shared",
      "modelAnswer": "Quy trình xác thực JWT Bearer trong ASP.NET Core 8 gồm 3 bước chính: Đầu tiên, Client gửi thông tin đăng nhập lên AuthController; Server xác thực qua IdP và ký phát sinh Access Token (chứa các Claims: sub, email, role) kèm hạn sử dụng ngắn (15 phút) cùng Refresh Token. Thứ hai, Middleware JwtBearerHandler giải mã, xác minh chữ ký HMAC SHA-256 hoặc RSA của token trên mỗi request và nạp ClaimsPrincipal vào HttpContext.User. Thứ ba, tầng API áp dụng AuthorizeAttribute kết hợp Roles hoặc Policies (ví dụ: department_head, lecturer) để kiểm soát quyền truy cập chi tiết tại từng Controller/Action.",
      "keyPoints": [
        "Xác thực qua IdP và phát sinh Access Token chứa Claims",
        "Middleware JwtBearerHandler giải mã và xác minh chữ ký cryptographic",
        "Nạp ClaimsPrincipal vào HttpContext.User",
        "Áp dụng AuthorizeAttribute lọc quyền theo RBAC Matrix"
      ],
      "hasFollowUp": true,
      "followUpPrompt": "Làm thế nào để thu hồi ngay lập tức một Access Token chưa hết hạn khi tài khoản người dùng bị khóa khẩn cấp?",
      "isFlagged": false,
      "flagReason": null,
      "rubric": {
        "name": "Barem Rubric CLO3 - Web API JWT & RBAC",
        "totalMaxScore": 10.00,
        "criteria": [
          {
            "criterionName": "Cấu trúc và quy trình ký phát sinh JWT Token",
            "maxScore": 3.00,
            "weight": 0.30,
            "bloomLevel": "Understand",
            "description": "Nêu rõ 3 phần Header, Payload, Signature; quy trình phát sinh Access Token kèm Claims và Refresh Token."
          },
          {
            "criterionName": "Cấu hình xác thực Middleware trong ASP.NET Core",
            "maxScore": 4.00,
            "weight": 0.40,
            "bloomLevel": "Apply",
            "description": "Chỉ rõ thứ tự AddAuthentication, AddJwtBearer, UseAuthentication trước UseAuthorization trong Program.cs."
          },
          {
            "criterionName": "Phân tích phân quyền RBAC và an ninh bảo mật",
            "maxScore": 3.00,
            "weight": 0.30,
            "bloomLevel": "Analyze",
            "description": "Phân tích cơ chế gán role qua ClaimTypes.Role, phòng chống rò rỉ token và thu hồi token qua Blacklist / Invalidation."
          }
        ]
      }
    }
  ]
}
```

##### Phản hồi lỗi chuẩn hóa RFC 7807 (`POST /api/v1/questions/generate-from-clo`)
- **400 Bad Request (Tham số đầu vào không hợp lệ):**
```json
{
  "type": "https://oralexam.fpt.edu.vn/errors/invalid-parameters",
  "title": "Invalid Generation Parameters",
  "status": 400,
  "detail": "Tổng phân bổ độ khó (easy: 0 + medium: 1 + hard: 2 = 3) không khớp với số câu yêu cầu (numberOfQuestions: 2).",
  "instance": "/api/v1/questions/generate-from-clo"
}
```
- **401 Unauthorized:**
```json
{
  "type": "https://oralexam.fpt.edu.vn/errors/unauthorized",
  "title": "Unauthorized",
  "status": 401,
  "detail": "Thiếu hoặc token JWT đã hết hạn.",
  "instance": "/api/v1/questions/generate-from-clo"
}
```
- **403 Forbidden (Yêu cầu vai trò Giảng viên hoặc Trưởng Bộ Môn):**
```json
{
  "type": "https://oralexam.fpt.edu.vn/errors/forbidden",
  "title": "Forbidden - Lecturer Or Department Head Required",
  "status": 403,
  "detail": "Tính năng kích hoạt AI sinh câu hỏi từ CLO yêu cầu vai trò Giảng viên ('lecturer') hoặc Trưởng Bộ Môn ('department_head'). Vai trò của bạn không được phép truy cập.",
  "instance": "/api/v1/questions/generate-from-clo"
}
```
- **404 Not Found:**
```json
{
  "type": "https://oralexam.fpt.edu.vn/errors/course-not-found",
  "title": "Course Not Found",
  "status": 404,
  "detail": "Không tìm thấy môn học tương ứng với courseId đã cung cấp.",
  "instance": "/api/v1/questions/generate-from-clo"
}
```
- **422 Unprocessable Entity:**
```json
{
  "type": "https://oralexam.fpt.edu.vn/errors/clo-not-found-in-course",
  "title": "CLO Not In Course",
  "status": 422,
  "detail": "Mã chuẩn đầu ra 'CLO99' không tồn tại trong danh mục course_clos của môn học.",
  "instance": "/api/v1/questions/generate-from-clo"
}
```
- **502 Bad Gateway (Gemini AI Service Error / Malformed JSON Output sau 3 lần Polly Retry):**
```json
{
  "type": "https://oralexam.fpt.edu.vn/errors/ai-bad-gateway",
  "title": "AI Generator Bad Gateway",
  "status": 502,
  "detail": "Dịch vụ Google Gemini AI gặp sự cố kết nối hoặc trả về cấu trúc dữ liệu không hợp lệ (sai JSON Schema chuẩn câu hỏi/rubric) sau 3 lần thử lại tự động theo cấp số nhân (Polly 2s, 4s, 8s). Vui lòng thử lại sau.",
  "instance": "/api/v1/questions/generate-from-clo"
}
```
- **504 Gateway Timeout (Gemini AI Timeout sau 3 lần Polly Retry):**
```json
{
  "type": "https://oralexam.fpt.edu.vn/errors/ai-generator-timeout",
  "title": "AI Generator Service Timeout",
  "status": 504,
  "detail": "Dịch vụ Google Gemini AI không hoàn tất sinh câu hỏi trong thời hạn quy định (Attempt Timeout 30s / Total Request Timeout 90s) sau 3 lần thử lại tự động theo cấp số nhân (Polly 2s, 4s, 8s). Vui lòng thử lại với số lượng câu hỏi ít hơn.",
  "instance": "/api/v1/questions/generate-from-clo"
}
```


---

#### 3. Giảng viên gửi mẻ câu hỏi vấn đáp lên Bộ Môn thẩm định
- **Endpoint:** `POST /api/v1/questions/batch-submit-review`
- **Quyền:** `lecturer`, `admin`
- **Mô tả:** Giảng viên sau khi thiết kế câu hỏi theo Barem riêng ($\sum \equiv 10.0$đ), tùy chỉnh nội dung câu hỏi, tiêu chí barem và Model Answer ($\ge 50$ ký tự), bấm nút **"Gửi lên cho Bộ Môn"** (`Submit to Department Head`). Hệ thống kiểm tra hợp lệ toàn diện và cập nhật trạng thái các câu hỏi từ `DRAFT` thành `SUBMITTED_FOR_REVIEW`, gán `submitted_by = current_user_id`.
- **Invariants:**
  1. Từng câu hỏi trong danh sách bắt buộc có Barem Rubric đạt chuẩn $\sum \text{maxScore} \equiv 10.00$đ.
  2. Đáp án mẫu Model Answer bắt buộc $\ge 50$ ký tự.
  3. Nếu có bất kỳ câu hỏi nào không thỏa mãn, hệ thống từ chối toàn bộ mẻ và trả về `422 Unprocessable Entity`.

##### Bảng đặc tả DTO Yêu cầu (`BatchSubmitQuestionsForReviewRequestDto`)
| Thuộc tính | Kiểu dữ liệu | Bắt buộc / Nullable | Ràng buộc / Validation | Mô tả nghiệp vụ |
|:---|:---|:---:|:---|:---|
| `courseId` | `uuid` | Bắt buộc (Non-null) | FK tồn tại trong `courses` | Môn học trực thuộc |
| `questionIds` | `List<uuid>` | Bắt buộc (Non-null) | Tối thiểu 1 câu hỏi | Danh sách ID các câu hỏi dự thảo gửi thẩm định |
| `submissionNotes` | `string` | Nullable | Tối đa 1000 ký tự | Ghi chú của Giảng viên gửi kèm cho Trưởng Bộ Môn |

- **Request Body:**
```json
{
  "courseId": "22222222-0000-0000-0000-000000000001",
  "questionIds": [
    "55555555-0000-0000-0000-000000000001",
    "55555555-0000-0000-0000-000000000002"
  ],
  "submissionNotes": "Kính gửi Trưởng Bộ Môn thẩm định bộ câu hỏi vấn đáp Clean Architecture & Bảo mật Web API chuẩn bị cho kỳ bảo vệ FA26."
}
```

##### Bảng đặc tả DTO Phản hồi (`BatchSubmitQuestionsForReviewResponseDto`)
| Thuộc tính | Kiểu dữ liệu | Bắt buộc / Nullable | Ràng buộc / Enum | Mô tả nghiệp vụ |
|:---|:---|:---:|:---|:---|
| `courseId` | `uuid` | Bắt buộc (Non-null) | UUID | Môn học |
| `submittedCount` | `int` | Bắt buộc (Non-null) | Số câu gửi thành công | Tổng số câu hỏi đã gửi thẩm định |
| `approvalStatus` | `string` | Bắt buộc (Non-null) | Cố định `'SUBMITTED_FOR_REVIEW'` | Trạng thái phê duyệt mới |
| `submittedBy` | `uuid` | Bắt buộc (Non-null) | UUID Giảng viên | Người đệ trình |
| `submittedAt` | `DateTime` | Bắt buộc (Non-null) | ISO 8601 UTC | Thời điểm gửi thẩm định |
| `message` | `string` | Bắt buộc (Non-null) | Thông báo | Thông điệp phản hồi người dùng |

- **Response 200 OK:**
```json
{
  "courseId": "22222222-0000-0000-0000-000000000001",
  "submittedCount": 2,
  "approvalStatus": "SUBMITTED_FOR_REVIEW",
  "submittedBy": "e4c5b2a1-0001-4000-8000-000000000004",
  "submittedAt": "2026-10-02T10:30:00Z",
  "message": "Đã gửi thành công 2 câu hỏi lên Trưởng Bộ Môn để thẩm định và phê duyệt."
}
```

##### Phản hồi lỗi chuẩn hóa RFC 7807 (`POST /api/v1/questions/batch-submit-review`)
- **400 Bad Request:**
```json
{
  "type": "https://oralexam.fpt.edu.vn/errors/empty-submission-batch",
  "title": "Empty Submission Batch",
  "status": 400,
  "detail": "Danh sách câu hỏi đệ trình không được để trống.",
  "instance": "/api/v1/questions/batch-submit-review"
}
```
- **403 Forbidden:**
```json
{
  "type": "https://oralexam.fpt.edu.vn/errors/forbidden",
  "title": "Forbidden - Lecturer Role Required",
  "status": 403,
  "detail": "Chỉ Giảng viên ('lecturer') hoặc Quản trị viên ('admin') mới có quyền đệ trình câu hỏi lên Bộ Môn.",
  "instance": "/api/v1/questions/batch-submit-review"
}
```
- **422 Unprocessable Entity:**
```json
{
  "type": "https://oralexam.fpt.edu.vn/errors/question-review-invariant-violation",
  "title": "Question Review Invariant Violation",
  "status": 422,
  "detail": "Câu hỏi '55555555-0000-0000-0000-000000000002' có Model Answer ngắn hơn 50 ký tự hoặc tổng điểm Barem lệch 10.00đ. Vui lòng hoàn thiện trước khi gửi thẩm định.",
  "instance": "/api/v1/questions/batch-submit-review"
}
```

---

#### 4. Trưởng Bộ Môn thẩm định & Phê duyệt / Yêu cầu chỉnh sửa câu hỏi
- **Endpoint:** `POST /api/v1/questions/{id}/review-decision`
- **Quyền:** `department_head`, `admin`
- **Mô tả:** Trưởng Bộ Môn truy cập Cổng Thẩm định Đề thi, rà soát từng câu hỏi do Giảng viên đệ trình (nội dung, mức Bloom, đáp án mẫu, tiêu chí Barem). Ra quyết định:
  - Phê duyệt chính thức (`APPROVED`): Câu hỏi chính thức gia nhập ngân hàng đề (phân bổ vào `practice_questions` hoặc `exam_questions` theo `usageScope`), gán `approved_by = current_user_id`.
  - Yêu cầu sửa đổi (`NEEDS_REVISION`): Trả lại cho Giảng viên kèm lý do thẩm định chi tiết (`reviewNotes`) để Giảng viên chỉnh sửa và nộp lại.
  - Từ chối loại hẳn (`REJECTED`): Bác bỏ và loại hẳn câu hỏi khỏi ngân hàng đề kèm lý do thẩm định chi tiết (`reviewNotes`).

##### Bảng đặc tả DTO Yêu cầu (`QuestionReviewDecisionRequestDto`)
| Thuộc tính | Kiểu dữ liệu | Bắt buộc / Nullable | Ràng buộc / Validation | Mô tả nghiệp vụ |
|:---|:---|:---:|:---|:---|
| `decision` | `string` | Bắt buộc (Non-null) | `APPROVED` \| `REJECTED` \| `NEEDS_REVISION` | Quyết định thẩm định của Trưởng Bộ Môn |
| `reviewNotes` | `string` | Bắt buộc (Non-null) | Độ dài $\ge 10$ ký tự | Lý do nhận xét, chỉ dẫn sửa đổi hoặc căn cứ phê duyệt |
| `targetUsageScope` | `string` | Nullable | `practice` \| `exam` \| `shared` | Phạm vi sử dụng chính thức (khi phê duyệt `APPROVED`) |

- **Request Body:**
```json
{
  "decision": "APPROVED",
  "reviewNotes": "Đề tài bám sát chuẩn đầu ra CLO1 và CLO3, Barem điểm phân bổ hợp lý, câu hỏi phụ phản biện có tính phân loại cao.",
  "targetUsageScope": "shared"
}
```

##### Bảng đặc tả DTO Phản hồi (`QuestionReviewDecisionResponseDto`)
| Thuộc tính | Kiểu dữ liệu | Bắt buộc / Nullable | Ràng buộc / Enum | Mô tả nghiệp vụ |
|:---|:---|:---:|:---|:---|
| `questionId` | `uuid` | Bắt buộc (Non-null) | UUID | Khóa chính câu hỏi |
| `approvalStatus` | `string` | Bắt buộc (Non-null) | `APPROVED` \| `REJECTED` \| `NEEDS_REVISION` | Trạng thái thẩm định mới |
| `reviewedBy` | `uuid` | Bắt buộc (Non-null) | UUID Trưởng Bộ Môn | Cán bộ thẩm định |
| `reviewedAt` | `DateTime` | Bắt buộc (Non-null) | ISO 8601 UTC | Thời điểm thẩm định |
| `reviewNotes` | `string` | Bắt buộc (Non-null) | Chuỗi nhận xét | Ghi chú thẩm định |
| `message` | `string` | Bắt buộc (Non-null) | Thông báo | Thông điệp phản hồi |

- **Response 200 OK:**
```json
{
  "questionId": "55555555-0000-0000-0000-000000000001",
  "approvalStatus": "APPROVED",
  "reviewedBy": "e4c5b2a1-0001-4000-8000-000000000005",
  "reviewedAt": "2026-10-02T11:00:00Z",
  "reviewNotes": "Đề tài bám sát chuẩn đầu ra CLO1 và CLO3, Barem điểm phân bổ hợp lý, câu hỏi phụ phản biện có tính phân loại cao.",
  "message": "Câu hỏi đã được Trưởng Bộ Môn phê duyệt chính thức và đưa vào ngân hàng đề thi."
}
```

##### Phản hồi lỗi chuẩn hóa RFC 7807 (`POST /api/v1/questions/{id}/review-decision`)
- **400 Bad Request:**
```json
{
  "type": "https://oralexam.fpt.edu.vn/errors/invalid-review-decision",
  "title": "Invalid Review Decision",
  "status": 400,
  "detail": "Quyết định thẩm định 'INVALID_STATUS' không hợp lệ. Chỉ chấp nhận APPROVED, REJECTED hoặc NEEDS_REVISION.",
  "instance": "/api/v1/questions/55555555-0000-0000-0000-000000000001/review-decision"
}
```
- **403 Forbidden:**
```json
{
  "type": "https://oralexam.fpt.edu.vn/errors/forbidden",
  "title": "Forbidden - Department Head or Admin Required",
  "status": 403,
  "detail": "Chỉ Trưởng Bộ Môn ('department_head') hoặc Quản trị viên ('admin') mới có quyền thẩm định và phê duyệt câu hỏi.",
  "instance": "/api/v1/questions/55555555-0000-0000-0000-000000000001/review-decision"
}
```
- **404 Not Found:**
```json
{
  "type": "https://oralexam.fpt.edu.vn/errors/question-not-found",
  "title": "Question Not Found",
  "status": 404,
  "detail": "Không tìm thấy câu hỏi với ID được cung cấp.",
  "instance": "/api/v1/questions/55555555-0000-0000-0000-000000000001/review-decision"
}
```
- **422 Unprocessable Entity:**
```json
{
  "type": "https://oralexam.fpt.edu.vn/errors/review-notes-required",
  "title": "Review Notes Required",
  "status": 422,
  "detail": "Ghi chú thẩm định 'reviewNotes' bắt buộc đạt tối thiểu 10 ký tự khi đưa ra quyết định thẩm định đề thi.",
  "instance": "/api/v1/questions/55555555-0000-0000-0000-000000000001/review-decision"
}
```

---

#### 5. Phê duyệt lưu hàng loạt câu hỏi vào ngân hàng đề chính thức
- **Endpoint:** `POST /api/v1/questions/batch-approve`
- **Quyền:** `department_head`, `admin`
- **Mô tả:** Trưởng bộ môn hoặc Quản trị viên thẩm định danh sách câu hỏi do Giảng viên đệ trình (hoặc do Trưởng Bộ Môn trực tiếp tạo), rà soát nội dung/tiêu chí Barem, và bấm Phê duyệt lưu hàng loạt vào CSDL trong một ACID Transaction duy nhất.
- **Quy tắc điều phối lưu trữ theo `usageScope` (Storage Dispatching Policy):**
  - Khi `usageScope == 'practice'`: Bản ghi được ghi vào bảng `practice_questions` (Kho tự luyện tập mở cho sinh viên ôn luyện, xem đáp án mẫu).
  - Khi `usageScope == 'exam'`: Bản ghi được ghi vào bảng `exam_questions` (Kho đề thi bảo mật, gán `approved_by = current_user_id`, đáp án mẫu được bảo mật tuyệt đối).
  - Khi `usageScope == 'shared'`: Bản ghi được ghi đồng thời vào cả hai bảng `practice_questions` và `exam_questions` trong cùng một Transaction, phục vụ cả luyện tập lẫn thi.
- **Quy tắc bất biến (Invariants):**
  1. Mỗi câu hỏi bắt buộc có Barem Rubric với tổng điểm $\sum \text{MaxScore}_j \equiv 10.00$đ.
  2. Mỗi câu hỏi bắt buộc có Model Answer $\ge 50$ ký tự.
  3. Nếu bất kỳ câu hỏi nào trong lô vi phạm $\to$ toàn bộ Transaction bị Rollback và trả về `HTTP 422 Unprocessable Entity`.

##### Bảng đặc tả DTO Yêu cầu (`BatchApproveQuestionsRequestDto`)
| Thuộc tính | Kiểu dữ liệu | Bắt buộc / Nullable | Ràng buộc / Validation | Mô tả nghiệp vụ |
|:---|:---|:---:|:---|:---|
| `courseId` | `uuid` | Bắt buộc (Non-null) | FK tồn tại trong `courses` | Môn học trực thuộc |
| `questions` | `List<ApprovedQuestionItemDto>` | Bắt buộc (Non-null) | Tối thiểu 1 câu hỏi | Danh sách câu hỏi phê duyệt |

*Chi tiết `ApprovedQuestionItemDto`:*
| Thuộc tính | Kiểu dữ liệu | Bắt buộc / Nullable | Ràng buộc / Validation | Mô tả nghiệp vụ |
|:---|:---|:---:|:---|:---|
| `title` | `string` | Bắt buộc (Non-null) | Tối đa 300 ký tự | Tiêu đề câu hỏi |
| `content` | `string` | Bắt buộc (Non-null) | Không rỗng | Nội dung đề bài |
| `difficulty` | `string` | Bắt buộc (Non-null) | `easy` \| `medium` \| `hard` | Độ khó câu hỏi |
| `bloomLevel` | `string` | Bắt buộc (Non-null) | 6 mức Bloom | Bậc nhận thức |
| `usageScope` | `string` | Bắt buộc (Non-null) | `practice` \| `exam` \| `shared` | Phạm vi lưu kho |
| `source` | `string` | Bắt buộc (Non-null) | `manual` \| `ai_generated` | Nguồn gốc câu hỏi |
| `modelAnswer` | `string` | Bắt buộc (Non-null) | **Độ dài $\ge 50$ ký tự** | Đáp án mẫu chuẩn |
| `keyPoints` | `List<string>` | Bắt buộc (Non-null) | Tối thiểu 1 ý chính | Các luận điểm then chốt |
| `hasFollowUp` | `bool` | Bắt buộc (Non-null) | `true` \| `false` | Cờ hỏi xoáy |
| `followUpPrompt`| `string` | Nullable | Tuỳ chọn | Gợi ý câu phụ |
| `rubric` | `RubricDraftDto` | Bắt buộc (Non-null) | **$\sum \text{maxScore} \equiv 10.00$** | Barem Rubric 10.0 |

- **Request Body:**
```json
{
  "courseId": "22222222-0000-0000-0000-000000000001",
  "questions": [
    {
      "title": "Phân tích nguyên lý Dependency Inversion trong Clean Architecture",
      "content": "Hãy giải thích nguyên lý Dependency Inversion (DIP) và minh họa cách tổ chức các tầng trong Clean Architecture .NET để tuân thủ nguyên lý này.",
      "difficulty": "medium",
      "bloomLevel": "Understand",
      "usageScope": "shared",
      "source": "ai_generated",
      "modelAnswer": "Dependency Inversion Principle (DIP) là nguyên lý chữ D trong SOLID. Nguyên lý phát biểu rằng các module cấp cao không nên phụ thuộc vào module cấp thấp, cả hai nên phụ thuộc vào abstractions (Interface)...",
      "keyPoints": [
        "Module cấp cao không phụ thuộc module cấp thấp",
        "Cả hai phụ thuộc vào abstraction (Interface)",
        "Application/Domain định nghĩa abstraction, Infrastructure hiện thực hóa"
      ],
      "hasFollowUp": true,
      "followUpPrompt": "Nếu cần thay đổi cơ chế ORM từ EF Core sang Dapper thì tầng Application có cần sửa đổi code không? Vì sao?",
      "rubric": {
        "name": "Barem Rubric CLO1 - Clean Architecture & DIP",
        "totalMaxScore": 10.00,
        "criteria": [
          {
            "criterionName": "Khái niệm và bản chất nguyên lý DIP",
            "maxScore": 3.00,
            "weight": 0.30,
            "bloomLevel": "Understand",
            "description": "Nêu rõ high-level và low-level modules đều phụ thuộc vào abstractions."
          },
          {
            "criterionName": "Hiện thực hóa DIP trong Clean Architecture",
            "maxScore": 4.00,
            "weight": 0.40,
            "bloomLevel": "Apply",
            "description": "Minh họa vị trí đặt Interface ở Application/Domain và Implementation ở Infrastructure."
          },
          {
            "criterionName": "Phân tích lợi ích kiến trúc và khả năng kiểm thử",
            "maxScore": 3.00,
            "weight": 0.30,
            "bloomLevel": "Analyze",
            "description": "Giải thích khả năng cô lập lỗi, mock interface trong Unit Testing."
          }
        ]
      }
    }
  ]
}
```

##### Bảng đặc tả DTO Phản hồi (`BatchApproveQuestionsResponseDto`)
| Thuộc tính | Kiểu dữ liệu | Bắt buộc / Nullable | Ràng buộc / Enum | Mô tả nghiệp vụ |
|:---|:---|:---:|:---|:---|
| `courseId` | `uuid` | Bắt buộc (Non-null) | UUID | Môn học |
| `totalApproved` | `int` | Bắt buộc (Non-null) | Số câu lưu thành công | Tổng số câu hỏi đã phê duyệt |
| `approvedQuestions` | `List<ApprovedQuestionResultDto>` | Bắt buộc (Non-null) | Danh sách kết quả | Chi tiết câu hỏi đã lưu |

*Chi tiết `ApprovedQuestionResultDto`:*
- `questionId` (`uuid`, Non-null): Khóa chính câu hỏi vừa tạo trong CSDL.
- `rubricId` (`uuid`, Non-null): Khóa chính Barem Rubric tương ứng.
- `title` (`string`, Non-null): Tiêu đề câu hỏi.
- `usageScope` (`string`, Non-null): `'practice'`, `'exam'`, hoặc `'shared'`.
- `source` (`string`, Non-null): `'ai_generated'` hoặc `'manual'`.
- `status` (`string`, Non-null): Cố định `'active'`.
- `approvedBy` (`uuid`, Non-null): User ID của cán bộ phê duyệt.
- `createdAt` (`DateTime`, Non-null): Thời điểm lưu trữ UTC.

- **Response 201 Created:**
```json
{
  "courseId": "22222222-0000-0000-0000-000000000001",
  "totalApproved": 1,
  "approvedQuestions": [
    {
      "questionId": "55555555-0000-0000-0000-000000000002",
      "rubricId": "44444444-0000-0000-0000-000000000002",
      "title": "Phân tích nguyên lý Dependency Inversion trong Clean Architecture",
      "usageScope": "shared",
      "source": "ai_generated",
      "status": "active",
      "approvedBy": "e4c5b2a1-0001-4000-8000-000000000005",
      "createdAt": "2026-10-02T10:00:00Z"
    }
  ]
}
```

##### Phản hồi lỗi chuẩn hóa RFC 7807 (`POST /api/v1/questions/batch-approve`)
- **400 Bad Request:**
```json
{
  "type": "https://oralexam.fpt.edu.vn/errors/empty-batch",
  "title": "Empty Questions Batch",
  "status": 400,
  "detail": "Danh sách câu hỏi phê duyệt không được rỗng.",
  "instance": "/api/v1/questions/batch-approve"
}
```
- **401 Unauthorized:**
```json
{
  "type": "https://oralexam.fpt.edu.vn/errors/unauthorized",
  "title": "Unauthorized",
  "status": 401,
  "detail": "Thiếu token hoặc token xác thực không hợp lệ.",
  "instance": "/api/v1/questions/batch-approve"
}
```
- **403 Forbidden:**
```json
{
  "type": "https://oralexam.fpt.edu.vn/errors/forbidden",
  "title": "Forbidden",
  "status": 403,
  "detail": "Chỉ 'department_head' hoặc 'admin' mới có quyền phê duyệt lưu hàng loạt câu hỏi.",
  "instance": "/api/v1/questions/batch-approve"
}
```
- **404 Not Found:**
```json
{
  "type": "https://oralexam.fpt.edu.vn/errors/course-not-found",
  "title": "Course Not Found",
  "status": 404,
  "detail": "Môn học không tồn tại trong hệ thống.",
  "instance": "/api/v1/questions/batch-approve"
}
```
- **422 Unprocessable Entity (Vi phạm ràng buộc Barem hoặc Model Answer):**
```json
{
  "type": "https://oralexam.fpt.edu.vn/errors/rubric-score-invariant-violation",
  "title": "Rubric Total Score Or Model Answer Invariant Violation",
  "status": 422,
  "detail": "Câu hỏi tại vị trí index [0] ('Phân tích nguyên lý DIP') có tổng điểm Barem Rubric là 9.50đ (lệch so với 10.00đ bắt buộc) hoặc Model Answer ngắn hơn 50 ký tự. Toàn bộ transaction phê duyệt bị hủy bỏ.",
  "instance": "/api/v1/questions/batch-approve"
}
```

---

#### 5b. Tra cứu danh sách câu hỏi có phân trang & lọc theo kho (`GET /api/v1/questions`)
- **Endpoint:** `GET /api/v1/questions`
- **Quyền:** `lecturer`, `department_head`, `admin`
- **Query Parameters (`GetQuestionsQuery`):**
  - `courseId` (`uuid`, tùy chọn): Lọc theo môn học.
  - `approvalStatus` (`string`, tùy chọn): Lọc theo trạng thái duyệt (`DRAFT`, `SUBMITTED_FOR_REVIEW`, `APPROVED`, `NEEDS_REVISION`, `REJECTED`).
  - `difficulty` (`string`, tùy chọn): `easy` | `medium` | `hard`.
  - `bloomLevel` (`string`, tùy chọn): `Remember` | `Understand` | `Apply` | `Analyze` | `Evaluate` | `Create`.
  - `source` (`string`, tùy chọn): `manual` | `ai_generated`.
  - `usageScope` (`string`, tùy chọn): `"practice"` (chỉ lấy kho luyện tập `practice_questions`), `"exam"` (chỉ lấy kho thi thật `exam_questions`), hoặc `"both"` / bỏ trống (hợp nhất từ cả 2 kho, tự động khử trùng các câu hỏi `both` theo `(CourseId, RubricId, Title)`).
  - `pageNumber` (`int`, mặc định `1`): Trang hiện tại (phân trang trực tiếp tại SQL Server-side).
  - `pageSize` (`int`, mặc định `20`, tối đa `100`): Số bản ghi mỗi trang.
- **Response 200 OK (`GetQuestionsResponseDto`):**
```json
{
  "totalCount": 42,
  "pageNumber": 1,
  "pageSize": 20,
  "items": [
    {
      "id": "55555555-0000-0000-0000-000000000001",
      "courseId": "22222222-0000-0000-0000-000000000001",
      "courseCode": "PRN231",
      "rubricId": "44444444-0000-0000-0000-000000000001",
      "rubricName": "Barem Vấn đáp Clean Architecture & DIP",
      "title": "Phân tích nguyên lý Dependency Inversion trong Clean Architecture",
      "difficulty": "medium",
      "bloomLevel": "Analyze",
      "source": "manual",
      "approvalStatus": "APPROVED",
      "usageScope": "both",
      "submittedBy": "e4c5b2a1-0001-4000-8000-000000000002",
      "approvedBy": "e4c5b2a1-0001-4000-8000-000000000005",
      "reviewNotes": "Đề bài chuẩn mực, bám sát CLO1.",
      "imageUrl": "/uploads/questions/202610/a1b2c3d4e5f6478980123456789abcde.png",
      "imageDescription": "Sơ đồ 4 vòng tròn đồng tâm Clean Architecture.",
      "cloId": "33333333-0000-0000-0000-000000000001",
      "cloCode": "CLO1",
      "structureId": "44444444-1111-2222-3333-444444444444",
      "isActive": true,
      "createdAt": "2026-10-02T10:00:00Z"
    }
  ]
}
```

#### 5c. Xem chi tiết câu hỏi kèm Barem Rubric, CLO & Ma trận Đề (`GET /api/v1/questions/{id}`)
- **Endpoint:** `GET /api/v1/questions/{id}`
- **Quyền:** `lecturer`, `department_head`, `admin`
- **Mô tả:** Truy xuất chi tiết câu hỏi từ kho `exam_questions` hoặc `practice_questions`, tự động nhận diện `usageScope` (`"practice"`, `"exam"`, hoặc `"both"` nếu câu hỏi được đồng bộ ở cả 2 kho) và nạp kèm chuẩn đầu ra `cloCode`, `structureId` cùng toàn bộ tiêu chí Barem Rubric 10.0đ.
- **Response 200 OK (`QuestionDetailDto`):**
```json
{
  "id": "55555555-0000-0000-0000-000000000001",
  "courseId": "22222222-0000-0000-0000-000000000001",
  "courseCode": "PRN231",
  "title": "Phân tích nguyên lý Dependency Inversion trong Clean Architecture",
  "content": "Hãy giải thích nguyên lý Dependency Inversion (DIP) và minh họa cách áp dụng trong Clean Architecture .NET.",
  "sampleAnswer": "Dependency Inversion Principle (DIP) là nguyên lý chữ D trong SOLID. High-level modules không nên phụ thuộc vào low-level modules; cả hai phải phụ thuộc vào abstractions...",
  "keyPoints": [
    "High-level không phụ thuộc low-level",
    "Cùng phụ thuộc vào abstraction (Interface)"
  ],
  "difficulty": "medium",
  "bloomLevel": "Analyze",
  "source": "manual",
  "approvalStatus": "APPROVED",
  "usageScope": "both",
  "submittedBy": "e4c5b2a1-0001-4000-8000-000000000002",
  "approvedBy": "e4c5b2a1-0001-4000-8000-000000000005",
  "reviewNotes": "Đề bài chuẩn mực, bám sát CLO1.",
  "hasFollowUp": true,
  "followUpPrompt": "Nếu thay đổi EF Core sang Dapper thì tầng nào bị ảnh hưởng?",
  "imageUrl": "/uploads/questions/202610/a1b2c3d4e5f6478980123456789abcde.png",
  "imageDescription": "Sơ đồ 4 vòng tròn đồng tâm Clean Architecture.",
  "cloId": "33333333-0000-0000-0000-000000000001",
  "cloCode": "CLO1",
  "structureId": "44444444-1111-2222-3333-444444444444",
  "isActive": true,
  "createdAt": "2026-10-02T10:00:00Z",
  "rubric": {
    "id": "44444444-0000-0000-0000-000000000001",
    "name": "Barem Vấn đáp Clean Architecture & DIP",
    "description": "Đánh giá hiểu biết về Clean Architecture 4 tầng và nguyên lý DIP",
    "totalMaxScore": 10.00,
    "criteria": [
      {
        "id": "aaaaaaaa-0000-0000-0000-000000000001",
        "criterionName": "Định nghĩa và bản chất nguyên lý DIP",
        "description": "Nêu rõ high-level modules không phụ thuộc low-level modules.",
        "maxScore": 3.00,
        "weight": 0.30,
        "bloomLevel": "Understand",
        "orderIndex": 1
      },
      {
        "id": "aaaaaaaa-0000-0000-0000-000000000002",
        "criterionName": "Áp dụng DIP vào Clean Architecture trong .NET",
        "description": "Chỉ rõ cách Interface đặt ở Application/Domain, triển khai ở Infrastructure.",
        "maxScore": 4.00,
        "weight": 0.40,
        "bloomLevel": "Apply",
        "orderIndex": 2
      },
      {
        "id": "aaaaaaaa-0000-0000-0000-000000000003",
        "criterionName": "Lợi ích Unit Testing và Loose Coupling",
        "description": "Khả năng mock Interface để test độc lập.",
        "maxScore": 3.00,
        "weight": 0.30,
        "bloomLevel": "Analyze",
        "orderIndex": 3
      }
    ]
  }
}
```

---

#### 5b. Tra cứu danh sách câu hỏi có phân trang & lọc theo kho (`GET /api/v1/questions`)
- **Endpoint:** `GET /api/v1/questions`
- **Quyền:** `lecturer`, `department_head`, `admin`
- **Query Parameters (`GetQuestionsQuery`):**
  - `courseId` (`uuid`, tùy chọn): Lọc theo môn học.
  - `approvalStatus` (`string`, tùy chọn): Lọc theo trạng thái duyệt (`DRAFT`, `SUBMITTED_FOR_REVIEW`, `APPROVED`, `NEEDS_REVISION`, `REJECTED`).
  - `difficulty` (`string`, tùy chọn): `easy` | `medium` | `hard`.
  - `bloomLevel` (`string`, tùy chọn): `Remember` | `Understand` | `Apply` | `Analyze` | `Evaluate` | `Create`.
  - `source` (`string`, tùy chọn): `manual` | `ai_generated`.
  - `usageScope` (`string`, tùy chọn): `"practice"` (chỉ lấy kho luyện tập `practice_questions`), `"exam"` (chỉ lấy kho thi thật `exam_questions`), hoặc `"both"` / bỏ trống (hợp nhất từ cả 2 kho, tự động khử trùng các câu hỏi `both` theo `(CourseId, RubricId, Title)`).
  - `pageNumber` (`int`, mặc định `1`): Trang hiện tại (phân trang trực tiếp tại SQL Server-side).
  - `pageSize` (`int`, mặc định `20`, tối đa `100`): Số bản ghi mỗi trang.
- **Response 200 OK (`GetQuestionsResponseDto`):**
```json
{
  "totalCount": 42,
  "pageNumber": 1,
  "pageSize": 20,
  "items": [
    {
      "id": "55555555-0000-0000-0000-000000000001",
      "courseId": "22222222-0000-0000-0000-000000000001",
      "courseCode": "PRN231",
      "rubricId": "44444444-0000-0000-0000-000000000001",
      "rubricName": "Barem Vấn đáp Clean Architecture & DIP",
      "title": "Phân tích nguyên lý Dependency Inversion trong Clean Architecture",
      "difficulty": "medium",
      "bloomLevel": "Analyze",
      "source": "manual",
      "approvalStatus": "APPROVED",
      "usageScope": "both",
      "submittedBy": "e4c5b2a1-0001-4000-8000-000000000002",
      "approvedBy": "e4c5b2a1-0001-4000-8000-000000000005",
      "reviewNotes": "Đề bài chuẩn mực, bám sát CLO1.",
      "imageUrl": "/uploads/questions/202610/a1b2c3d4e5f6478980123456789abcde.png",
      "imageDescription": "Sơ đồ 4 vòng tròn đồng tâm Clean Architecture.",
      "cloId": "33333333-0000-0000-0000-000000000001",
      "cloCode": "CLO1",
      "structureId": "44444444-1111-2222-3333-444444444444",
      "isActive": true,
      "createdAt": "2026-10-02T10:00:00Z"
    }
  ]
}
```

#### 5c. Xem chi tiết câu hỏi kèm Barem Rubric, CLO & Ma trận Đề (`GET /api/v1/questions/{id}`)
- **Endpoint:** `GET /api/v1/questions/{id}`
- **Quyền:** `lecturer`, `department_head`, `admin`
- **Mô tả:** Truy xuất chi tiết câu hỏi từ kho `exam_questions` hoặc `practice_questions`, tự động nhận diện `usageScope` (`"practice"`, `"exam"`, hoặc `"both"` nếu câu hỏi được đồng bộ ở cả 2 kho) và nạp kèm chuẩn đầu ra `cloCode`, `structureId` cùng toàn bộ tiêu chí Barem Rubric 10.0đ.
- **Response 200 OK (`QuestionDetailDto`):**
```json
{
  "id": "55555555-0000-0000-0000-000000000001",
  "courseId": "22222222-0000-0000-0000-000000000001",
  "courseCode": "PRN231",
  "title": "Phân tích nguyên lý Dependency Inversion trong Clean Architecture",
  "content": "Hãy giải thích nguyên lý Dependency Inversion (DIP) và minh họa cách áp dụng trong Clean Architecture .NET.",
  "sampleAnswer": "Dependency Inversion Principle (DIP) là nguyên lý chữ D trong SOLID. High-level modules không nên phụ thuộc vào low-level modules; cả hai phải phụ thuộc vào abstractions...",
  "keyPoints": [
    "High-level không phụ thuộc low-level",
    "Cùng phụ thuộc vào abstraction (Interface)"
  ],
  "difficulty": "medium",
  "bloomLevel": "Analyze",
  "source": "manual",
  "approvalStatus": "APPROVED",
  "usageScope": "both",
  "submittedBy": "e4c5b2a1-0001-4000-8000-000000000002",
  "approvedBy": "e4c5b2a1-0001-4000-8000-000000000005",
  "reviewNotes": "Đề bài chuẩn mực, bám sát CLO1.",
  "hasFollowUp": true,
  "followUpPrompt": "Nếu thay đổi EF Core sang Dapper thì tầng nào bị ảnh hưởng?",
  "imageUrl": "/uploads/questions/202610/a1b2c3d4e5f6478980123456789abcde.png",
  "imageDescription": "Sơ đồ 4 vòng tròn đồng tâm Clean Architecture.",
  "cloId": "33333333-0000-0000-0000-000000000001",
  "cloCode": "CLO1",
  "structureId": "44444444-1111-2222-3333-444444444444",
  "isActive": true,
  "createdAt": "2026-10-02T10:00:00Z",
  "rubric": {
    "id": "44444444-0000-0000-0000-000000000001",
    "name": "Barem Vấn đáp Clean Architecture & DIP",
    "description": "Đánh giá hiểu biết về Clean Architecture 4 tầng và nguyên lý DIP",
    "totalMaxScore": 10.00,
    "criteria": [
      {
        "id": "aaaaaaaa-0000-0000-0000-000000000001",
        "criterionName": "Định nghĩa và bản chất nguyên lý DIP",
        "description": "Nêu rõ high-level modules không phụ thuộc low-level modules.",
        "maxScore": 3.00,
        "weight": 0.30,
        "bloomLevel": "Understand",
        "orderIndex": 1
      },
      {
        "id": "aaaaaaaa-0000-0000-0000-000000000002",
        "criterionName": "Áp dụng DIP vào Clean Architecture trong .NET",
        "description": "Chỉ rõ cách Interface đặt ở Application/Domain, triển khai ở Infrastructure.",
        "maxScore": 4.00,
        "weight": 0.40,
        "bloomLevel": "Apply",
        "orderIndex": 2
      },
      {
        "id": "aaaaaaaa-0000-0000-0000-000000000003",
        "criterionName": "Lợi ích Unit Testing và Loose Coupling",
        "description": "Khả năng mock Interface để test độc lập.",
        "maxScore": 3.00,
        "weight": 0.30,
        "bloomLevel": "Analyze",
        "orderIndex": 3
      }
    ]
  }
}
```

---

#### 6. Giảng viên gửi bộ đề thi lên Trưởng Bộ Môn thẩm định
- **Endpoint:** `POST /api/v1/exam-sets/{id}/submit-review`
- **Quyền:** `lecturer`, `admin`
- **Mô tả:** Giảng viên sau khi tạo và chọn lọc các câu hỏi vào bộ đề thi `exam_sets` (tuân thủ cấu trúc khung `exam_structures`), gửi bộ đề lên Trưởng Bộ Môn để thẩm định. Hệ thống kiểm tra số lượng câu hỏi, cơ cấu mức Bloom/CLO theo khung và chuyển trạng thái từ `DRAFT` thành `SUBMITTED_FOR_REVIEW`.
- **Path Parameters:**
  - `id` (`uuid`, bắt buộc): Định danh duy nhất của bộ đề thi.
- **Request Body (`SubmitExamSetReviewRequestDto`):**
```json
{
  "notes": "Kính gửi Trưởng Bộ Môn thẩm định bộ đề thi số 01 cho môn PRN231 kỳ FA26."
}
```
- **Response 200 OK (`SubmitExamSetReviewResponseDto`):**
```json
{
  "examSetId": "66666666-0000-0000-0000-000000000001",
  "status": "SUBMITTED_FOR_REVIEW",
  "submittedAt": "2026-10-05T09:00:00Z",
  "message": "Bộ đề thi đã được gửi lên Trưởng Bộ Môn để thẩm định thành công."
}
```
- **Phản hồi lỗi chuẩn hóa RFC 7807 (`POST /api/v1/exam-sets/{id}/submit-review`):**
  - `400 Bad Request`: Bộ đề thi không có câu hỏi nào hoặc thiếu số lượng câu hỏi theo `exam_structures`.
  - `403 Forbidden`: Người dùng không có vai trò Giảng viên hoặc Quản trị viên.
  - `404 Not Found`: Không tìm thấy bộ đề thi với ID cung cấp.

---

#### 7. Trưởng Bộ Môn thẩm định & Phê duyệt / Từ chối bộ đề thi
- **Endpoint:** `PUT /api/v1/exam-sets/{id}/review-decision`
- **Quyền:** `department_head`, `admin`
- **Mô tả:** Trưởng Bộ Môn rà soát toàn bộ câu hỏi và cấu trúc của bộ đề thi `exam_sets`. Ra quyết định:
  - Phê duyệt chính thức (`OFFICIAL`): Bộ đề thi đạt chuẩn, sẵn sàng để bàn giao cho Khảo thí.
  - Từ chối (`REJECTED`): Trả lại bộ đề kèm lý do nhận xét chi tiết (`reviewNotes`).
- **Path Parameters:**
  - `id` (`uuid`, bắt buộc): Định danh duy nhất của bộ đề thi.
- **Request Body (`ExamSetReviewDecisionRequestDto`):**
```json
{
  "decision": "OFFICIAL",
  "reviewNotes": "Bộ đề thi đảm bảo ma trận Bloom, đủ chuẩn đầu ra CLO1-CLO4 và phân bố thời gian hợp lý."
}
```
- **Response 200 OK (`ExamSetReviewDecisionResponseDto`):**
```json
{
  "examSetId": "66666666-0000-0000-0000-000000000001",
  "status": "OFFICIAL",
  "approvedBy": "e4c5b2a1-0001-4000-8000-000000000005",
  "reviewedAt": "2026-10-05T10:00:00Z",
  "message": "Bộ đề thi đã được phê duyệt chính thức (OFFICIAL)."
}
```
- **Phản hồi lỗi chuẩn hóa RFC 7807 (`PUT /api/v1/exam-sets/{id}/review-decision`):**
  - `400 Bad Request`: Quyết định phê duyệt không hợp lệ (chỉ chấp nhận `OFFICIAL` hoặc `REJECTED`).
  - `403 Forbidden`: Người dùng không có vai trò Trưởng Bộ Môn hoặc Quản trị viên.
  - `404 Not Found`: Không tìm thấy bộ đề thi tương ứng.

---

#### 8. Trưởng Bộ Môn bàn giao bộ đề thi chính thức cho Cán bộ Khảo thí
- **Endpoint:** `POST /api/v1/exam-sets/{id}/hand-over`
- **Quyền:** `department_head`, `admin`
- **Mô tả:** Trưởng Bộ Môn bàn giao bộ đề thi đã được phê duyệt `OFFICIAL` sang cho Cán bộ Khảo thí (`examination_officer`). Chuyển trạng thái bộ đề từ `OFFICIAL` sang `HANDED_OVER`. Bộ đề ở trạng thái `HANDED_OVER` là điều kiện tiên quyết để Khảo thí có thể phân bổ vào các ca thi phòng Lab (`distribute-tickets` với ràng buộc `uq_tickets_shift_exam_set`).
- **Path Parameters:**
  - `id` (`uuid`, bắt buộc): Định danh duy nhất của bộ đề thi.
- **Request Body (`HandOverExamSetRequestDto`):**
```json
{
  "targetSessionId": "77777777-0000-0000-0000-000000000001",
  "notes": "Bàn giao bộ đề chính thức cho kỳ thi vấn đáp kết thúc môn FA26."
}
```
- **Response 200 OK (`HandOverExamSetResponseDto`):**
```json
{
  "examSetId": "66666666-0000-0000-0000-000000000001",
  "sessionId": "77777777-0000-0000-0000-000000000001",
  "status": "HANDED_OVER",
  "handedOverAt": "2026-10-06T08:00:00Z",
  "message": "Bộ đề thi đã được bàn giao thành công cho Cán bộ Khảo thí."
}
```
- **Phản hồi lỗi chuẩn hóa RFC 7807 (`POST /api/v1/exam-sets/{id}/hand-over`):**
  - `400 Bad Request`: Bộ đề thi chưa được phê duyệt `OFFICIAL` (không thể bàn giao đề ở trạng thái `DRAFT` hoặc `REJECTED`).
  - `403 Forbidden`: Chỉ Trưởng Bộ Môn hoặc Quản trị viên mới có quyền bàn giao đề.
  - `404 Not Found`: Không tìm thấy bộ đề thi hoặc kỳ thi tương ứng.

---

### 5.5. Luyện tập Tương tác Tự do (Interactive Practice) — MF-01
*(Phân hệ CSDL: `practice_sessions`, `practice_answers`, `ai_evaluations`, `ai_evaluation_details`)*

#### 1. Khởi tạo phiên luyện tập
- **Endpoint:** `POST /api/v1/practice/sessions`
- **Quyền:** `student`
- **Mô tả:** Khởi tạo phiên luyện tập cho cả 2 chế độ `[Per-Question]` và `[Full-Session]`. Hỗ trợ chọn độ khó cố định (`easy`, `medium`, `hard`) hoặc ngẫu nhiên tăng dần từ Dễ đến Khó (`progressive` với 3 đến 10 câu hỏi, cấu hình động qua `system_configs`).
- **Request Body:**
```json
{
  "studentId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "courseId": "22222222-0000-0000-0000-000000000001",
  "difficulties": ["easy", "medium"],
  "isFullSession": false,
  "topic": "Clean Architecture & CQRS",
  "questionCount": null
}
```
- **Bảng Đặc tả Tham số Request:**
| Tên trường | Kiểu | Ràng buộc | Mô tả |
|:---|:---|:---:|:---|
| `courseId` | `uuid` | Bắt buộc | ID môn học sinh viên muốn luyện tập |
| `difficulties` | `string[]` | Tùy chọn (Khuyên dùng) | Mảng các mức độ khó muốn luyện tập, ví dụ: `["easy"]`, `["easy", "medium"]`, `["easy", "hard"]`, `["medium", "hard"]`, hoặc `["easy", "medium", "hard"]` |
| `difficulty` | `string` | Tùy chọn (Tương thích ngược) | Mức độ khó đơn lẻ: `"easy"`, `"medium"`, `"hard"`, hoặc `"progressive"` (ngẫu nhiên từ Dễ $\to$ Khó, tự động chia đều cả 3 mức `["easy", "medium", "hard"]`) |
| `questionCount` | `int?` | Tùy chọn / Bắt buộc | **Chế độ [Per-Question]:** Tùy chọn (không bắt buộc truyền hoặc để `null`, cấp ngay Câu 1 khi khởi tạo và sinh viên lấy câu tiếp theo on-demand). <br>**Chế độ [Full-Session] hoặc khi chọn progressive:** Bắt buộc từ 3 đến 10 câu (do `MinMixedPracticeQuestions = 3` và `MaxMixedPracticeQuestions = 10` trong `system_configs` quy định) |
| `isFullSession` | `bool` | Tùy chọn | Mặc định `false`: `false` cho chế độ `[Per-Question On-Demand]` (luyện từng câu có follow-up khi điểm 4.0–8.0), `true` cho chế độ `[Full-Session Progressive]` (luyện trọn gói từ Dễ $\to$ Khó không follow-up) |
| `topic` | `string?` | Tùy chọn | Chủ đề hoặc từ khóa bài học muốn tập trung ôn luyện |
| `studentId` | `uuid` | Tùy chọn | ID tài khoản sinh viên (mặc định tự động lấy từ JWT Claims nếu để trống) |

- **Response 201 Created (hoặc 200 OK):**
```json
{
  "sessionId": "b4a3c2d1-9876-4abc-9def-0123456789ab",
  "transcriptBufferSeconds": 60,
  "questions": [
    {
      "questionId": "11111111-2222-3333-4444-555555555555",
      "content": "Giải thích nguyên lý Dependency Inversion Principle (DIP) trong SOLID và cho ví dụ áp dụng trong .NET 8.",
      "imageUrl": "/uploads/questions/202610/a1b2c3d4e5f6478980123456789abcde.png",
      "imageDescription": "Sơ đồ 4 vòng tròn đồng tâm Clean Architecture.",
      "rubricCriteria": [
        "Định nghĩa chính xác DIP (High-level không phụ thuộc Low-level)",
        "Mô tả vai trò của Interface/Abstraction",
        "Ví dụ triển khai Dependency Injection trong C# / .NET 8"
      ]
    },
    {
      "questionId": "66666666-7777-8888-9999-000000000000",
      "content": "Phân biệt sự khác nhau giữa CQRS Command và CQRS Query.",
      "rubricCriteria": [
        "Mục đích Command làm thay đổi trạng thái (State change)",
        "Mục đích Query chỉ đọc dữ liệu (Read-only, AsNoTracking)",
        "Lợi ích tách biệt hai mô hình"
      ]
    }
  ]
}
```
- **Response 400 Bad Request:** Nếu kho đề không đủ câu hỏi cho các mức độ yêu cầu (kèm thông báo tiếng Việt rõ ràng) hoặc `questionCount` vi phạm ngưỡng cấu hình:
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "Dữ liệu đầu vào không hợp lệ.",
  "status": 400,
  "detail": "Kho đề hiện tại chỉ có 2 câu hỏi mức Khó, không đủ để tạo phiên với 5 câu hỏi."
}
```

#### 2. Lấy thông tin chi tiết phiên luyện tập
- **Endpoint:** `GET /api/v1/practice/sessions/{sessionId}`
- **Quyền:** `student`, `lecturer`, `admin`
- **Mô tả:** Lấy thông tin chi tiết phiên luyện tập và danh sách câu trả lời đã nộp (kèm điểm số nếu đã chấm xong).
- **Response 200 OK:**
```json
{
  "sessionId": "b4a3c2d1-9876-4abc-9def-0123456789ab",
  "courseId": "22222222-0000-0000-0000-000000000001",
  "courseCode": "PRN231",
  "courseName": "Building Cross-Platform Applications with .NET",
  "studentId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "studentName": "Lê Vũ Hoàng",
  "practiceMode": "per_question",
  "status": "in_progress",
  "transcriptBufferSeconds": 60,
  "startedAt": "2026-10-10T08:00:00Z",
  "endedAt": null,
  "questions": [
    {
      "questionId": "11111111-2222-3333-4444-555555555555",
      "content": "Giải thích nguyên lý Dependency Inversion Principle (DIP)...",
      "imageUrl": "/uploads/questions/202610/a1b2c3d4e5f6478980123456789abcde.png",
      "imageDescription": "Sơ đồ 4 vòng tròn đồng tâm Clean Architecture.",
      "rubricCriteria": [
        "Định nghĩa chính xác DIP",
        "Mô tả vai trò Interface/Abstraction",
        "Ví dụ triển khai DI trong .NET 8"
      ]
    }
  ],
  "answers": [
    {
      "answerId": "77777777-0000-0000-0000-000000000001",
      "questionId": "11111111-2222-3333-4444-555555555555",
      "answerText": "Theo em Dependency Inversion phát biểu rằng các module cấp cao không nên phụ thuộc vào module cấp thấp...",
      "isFollowUp": false,
      "parentAnswerId": null,
      "status": "graded",
      "submittedAt": "2026-10-10T08:00:05Z",
      "totalScore": 8.50,
      "feedback": "Bạn nắm vững lý thuyết DIP, đối chiếu chính xác giữa Domain và Infrastructure.",
      "confidenceScore": 0.94,
      "isSuspicious": false,
      "needsFollowUp": false,
      "followUpPrompt": null,
      "criteriaScores": [
        {
          "criterionId": "aaaaaaaa-0000-0000-0000-000000000001",
          "criterionName": "Định nghĩa và bản chất nguyên lý DIP",
          "score": 3.00,
          "comment": "Chính xác, định nghĩa chuẩn mực"
        },
        {
          "criterionId": "aaaaaaaa-0000-0000-0000-000000000002",
          "criterionName": "Áp dụng DIP vào Clean Architecture trong .NET",
          "score": 3.50,
          "comment": "Giải thích tốt, cần lưu ý thêm về DI Lifetime"
        },
        {
          "criterionId": "aaaaaaaa-0000-0000-0000-000000000003",
          "criterionName": "Lợi ích Unit Testing và Loose Coupling",
          "score": 2.00,
          "comment": "Đã nêu được mock nhưng chưa nhấn mạnh isolation"
        }
      ]
    }
  ]
}
```

#### 3. Lấy câu hỏi tiếp theo theo yêu cầu [Per-Question On-Demand]
- **Endpoint:** `POST /api/v1/practice/sessions/{sessionId}/next-question` (hoặc `POST /api/v1/practice/sessions/{sessionId}/next-question?studentId={GUID}`)
- **Quyền:** `student`
- **Request Headers:**
  * `Authorization: Bearer <JWT_ACCESS_TOKEN>`
- **Path Parameters:**
  * `sessionId` (*uuid*, bắt buộc): ID phiên luyện tập.
- **Query Parameters:**
  * `studentId` (*uuid*, tùy chọn): ID tài khoản sinh viên (mặc định tự lấy từ JWT Claims).
- **Request Body:** Không có (Empty Body).
- **Mô tả nghiệp vụ:**
  * Cấp câu hỏi tiếp theo cho sinh viên trong chế độ `[Per-Question]` theo nhu cầu (On-Demand).
  * **Thuật toán Anti-3-Consecutive Randomizer:** Kiểm tra 2 câu hỏi chính liền trước. Nếu cả 2 câu có cùng độ khó $D$ và sinh viên chọn từ 2 mức trở lên, loại trừ mức $D$ để bốc mức khác (đảm bảo tối đa 2 câu cùng mức trong 3 câu liên tiếp).
  * **Không lặp câu đã làm:** Loại trừ toàn bộ câu hỏi đã làm trong phiên (`Id NOT IN (...)`).
  * **Lazy Inactivity Timeout:** Kiểm tra nếu thời gian không tương tác vượt quá `SessionInactivityTimeoutMinutes` (5 phút), tự động kết thúc phiên (`status = "completed"`) và trả về mã lỗi HTTP 410 Gone.
  * Tự động cập nhật `last_activity_at = DateTime.UtcNow` khi bốc câu hỏi thành công.
- **Response 200 OK (Khi còn câu hỏi khả dụng):**
```json
{
  "hasMoreQuestions": true,
  "message": null,
  "question": {
    "id": "77777777-8888-9999-aaaa-bbbbbbbbbbbb",
    "content": "Giải thích cách hoạt động của Pattern Matching trong C# 12 và so sánh với switch-case truyền thống.",
    "difficulty": "medium",
    "questionOrder": 2,
    "imageUrl": null,
    "imageDescription": null,
    "rubricCriteria": [
      "Khái niệm Pattern Matching và các dạng pattern",
      "Tính an toàn kiểu dữ liệu so với switch-case",
      "Ví dụ minh họa code C#"
    ]
  }
}
```
- **Response 200 OK (Khi đã hoàn thành toàn bộ câu hỏi theo các mức đã chọn):**
```json
{
  "hasMoreQuestions": false,
  "message": "Đã hoàn thành toàn bộ câu hỏi khả dụng theo mức độ đã chọn.",
  "question": null
}
```
- **Response 410 Gone (Session Timed Out RFC 7807):**
```json
{
  "type": "https://tools.ietf.org/html/rfc7807",
  "title": "Session Timed Out",
  "status": 410,
  "detail": "Phiên luyện tập đã kết thúc tự động do không có tương tác trong hơn 5 phút."
}
```
- **Response 404 Not Found (Session Not Found RFC 7807):**
```json
{
  "type": "https://tools.ietf.org/html/rfc7807",
  "title": "Session Not Found",
  "status": 404,
  "detail": "Phiên luyện tập không tồn tại hoặc không thuộc về sinh viên này."
}
```
- **Response 400 Bad Request (Yêu cầu không hợp lệ RFC 7807):**
```json
{
  "type": "https://tools.ietf.org/html/rfc7807",
  "title": "Yêu cầu không hợp lệ",
  "status": 400,
  "detail": "Tính năng bốc câu hỏi theo yêu cầu chỉ áp dụng cho chế độ [Per-Question]."
}
```

#### 4. Nộp câu trả lời luyện tập (Phòng thủ 4 tầng — Hàng đợi Bounded Channel 1,000 slots)
- **Endpoint:** `POST /api/v1/practice/sessions/{sessionId}/answers`
- **Quyền:** `student`
- **Cơ chế:** Ghi DB `PENDING` (< 100ms), đẩy vào Bounded Channel, phản hồi ngay `202 Accepted`.
- **Request Body:**
```json
{
  "questionId": "55555555-0000-0000-0000-000000000001",
  "studentId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "answerText": "Theo em Dependency Inversion phát biểu rằng các module cấp cao không nên phụ thuộc vào module cấp thấp...",
  "isFollowUp": false,
  "parentAnswerId": null
}
```
- **Response 202 Accepted:**
```json
{
  "answerId": "77777777-0000-0000-0000-000000000001"
}
```
- **Response 410 Gone (Khi phiên bị timeout quá 5 phút không tương tác):**
```json
{
  "type": "https://tools.ietf.org/html/rfc7807",
  "title": "Session Timed Out",
  "status": 410,
  "detail": "Phiên luyện tập đã kết thúc tự động do không có tương tác trong hơn 5 phút."
}
```

#### 5. Nộp câu trả lời hàng loạt (Batch Submit cho Full-Session)
- **Endpoint:** `POST /api/v1/practice/sessions/{sessionId}/batch-submit`
- **Quyền:** `student`
- **Mô tả:** Dùng cho chế độ `[Full-Session]` khi sinh viên hoàn thành toàn bộ các câu hỏi và bấm nộp toàn bộ.
- **Request Body:**
```json
{
  "studentId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "answers": [
    {
      "questionId": "55555555-0000-0000-0000-000000000001",
      "answerText": "Theo em Dependency Inversion..."
    },
    {
      "questionId": "55555555-0000-0000-0000-000000000002",
      "answerText": "Sự khác biệt giữa Command và Query..."
    }
  ]
}
```
- **Response 202 Accepted:** Không có response body (Empty body với status `202 Accepted`). Toàn bộ câu trả lời được đưa vào hàng đợi chấm điểm ngầm.
- **Response 410 Gone (Khi phiên bị timeout quá 5 phút không tương tác):**
```json
{
  "type": "https://tools.ietf.org/html/rfc7807",
  "title": "Session Timed Out",
  "status": 410,
  "detail": "Phiên luyện tập đã kết thúc tự động do không có tương tác trong hơn 5 phút."
}
```

#### 6. Hoàn tất phiên luyện tập (CompleteSession)
- **Endpoint:** `POST /api/v1/practice/sessions/{sessionId}/complete`
- **Quyền:** `student`
- **Mô tả:** Gọi khi sinh viên hoàn thành phiên luyện tập hoặc muốn kết thúc phiên để cập nhật trạng thái `status = "completed"` và ghi nhận `ended_at = DateTime.UtcNow`.
- **Request Headers:**
  * `Authorization: Bearer <JWT_ACCESS_TOKEN>`
- **Response 200 OK:**
```json
{
  "message": "Phiên luyện tập đã kết thúc thành công."
}
```

#### 7. Lấy danh sách lịch sử luyện tập (GetStudentHistory)
- **Endpoint:** `GET /api/v1/practice/student/history`
- **Quyền:** `student`
- **Query Params:**
  * `studentId`: (*uuid*, optional - nếu không truyền sẽ lấy từ JWT token của sinh viên đang đăng nhập).
- **Mô tả:** Lấy toàn bộ lịch sử các phiên luyện tập của sinh viên để hiển thị lên Tab Luyện Tập của trang Lịch sử (FE-02) trên Student Portal.
- **Response 200 OK:**
```json
[
  {
    "sessionId": "e2a3b4c5-6789-0123-4567-89abcdef0123",
    "courseId": "22222222-0000-0000-0000-000000000001",
    "courseCode": "PRN231",
    "courseName": "Building Cross-Platform Applications with .NET",
    "practiceMode": "per_question",
    "status": "completed",
    "startedAt": "2026-10-08T03:00:00Z",
    "endedAt": "2026-10-08T03:25:30Z",
    "totalQuestions": 5,
    "answeredQuestions": 6,
    "averageScore": 7.80
  }
]
```

#### 8. Tải lên âm thanh nhận diện Whisper STT (Upload Audio Stream)
- **Endpoint:** `POST /api/v1/storage/upload-audio`
- **Quyền:** `student`
- **Content-Type:** `multipart/form-data`
- **Mô tả:** Stream âm thanh nhận diện qua Whisper STT server-side để nhận diện và trả về văn bản bóc băng (`transcript`) cho màn hình đệm. MF-01 KHÔNG lưu audio vào Cloudflare R2 hay CSDL.
- **Response 200 OK:**
```json
{
  "transcript": "Theo em Dependency Inversion phát biểu rằng các module cấp cao không nên phụ thuộc vào module cấp thấp...",
  "durationSeconds": 14.5
}
```

#### 9. Kết nối thời gian thực SignalR Hub
- **Hub URL:** `/hubs/practice`
- **Client Invokes:** `JoinSession(sessionId)`, `LeaveSession(sessionId)`
- **Sự kiện Realtime Backend phát về Client:**
  * 🎯 `ReceiveGradingResult`: Kích hoạt khi AI hoàn tất chấm điểm:
```json
{
  "answerId": "77777777-0000-0000-0000-000000000001",
  "questionId": "55555555-0000-0000-0000-000000000001",
  "answerText": "Theo em Dependency Inversion phát biểu rằng...",
  "isFollowUp": false,
  "parentAnswerId": null,
  "status": "graded",
  "submittedAt": "2026-10-10T08:00:05Z",
  "totalScore": 8.50,
  "feedback": "Bạn nắm vững lý thuyết DIP, đối chiếu chính xác giữa Domain và Infrastructure.",
  "confidenceScore": 0.94,
  "isSuspicious": false,
  "needsFollowUp": false,
  "followUpPrompt": null,
  "criteriaScores": [
    {
      "criterionId": "aaaaaaaa-0000-0000-0000-000000000001",
      "criterionName": "Định nghĩa và bản chất nguyên lý DIP",
      "score": 3.00,
      "comment": "Chính xác, định nghĩa chuẩn mực"
    },
    {
      "criterionId": "aaaaaaaa-0000-0000-0000-000000000002",
      "criterionName": "Áp dụng DIP vào Clean Architecture trong .NET",
      "score": 3.50,
      "comment": "Giải thích tốt, cần lưu ý thêm về DI Lifetime"
    },
    {
      "criterionId": "aaaaaaaa-0000-0000-0000-000000000003",
      "criterionName": "Lợi ích Unit Testing và Loose Coupling",
      "score": 2.00,
      "comment": "Đã nêu được mock nhưng chưa nhấn mạnh isolation"
    }
  ]
}
```
  * ⚠️ `ReceiveGradingError`: `(Guid answerId, string error)` - Phát thông báo khi tác vụ chấm điểm nền gặp lỗi xử lý.

---

### 5.6. Thi Thử Bấm Giờ (Mock Exam & Voice-First) — MF-02
*(Phân hệ CSDL: `courses`, `exam_structures`, `exam_sets`, `mock_exam_quotas`, `mock_exam_sessions`, `mock_exam_answers`, `ai_evaluations`, `ai_evaluation_details`)*  
*(Xem chi tiết mẫu tích hợp Frontend React 19 tại Mục 6.4 bên dưới)*

#### 1. Tra cứu hạn ngạch thi thử trong ngày (Dynamic Daily Quota)
- **Endpoint:** `GET /api/v1/mock-exams/quota?courseId={courseId}`
- **Quyền:** `student`, `lecturer`, `department_head`, `admin`
- **Mô tả:** Kiểm tra số lượt thi thử sinh viên đã dùng và còn lại trong ngày hôm nay (theo giờ Việt Nam GMT+7) đối với môn học `courseId`. Hạn ngạch tối đa mỗi ngày (`max_mock_exams_per_day`) do Admin cấu hình động trên từng môn học (mặc định $K = 3$).
- **Response 200 OK:**
```json
{
  "courseId": "22222222-0000-0000-0000-000000000001",
  "studentId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "quotaDate": "2026-10-14",
  "usedCount": 1,
  "remainingCount": 2,
  "maxDailyQuota": 3,
  "canStartExam": true
}
```

#### 2. Khởi tạo ca thi thử (Bốc đề từ kho `practice_questions` theo ma trận Bloom)
- **Endpoint:** `POST /api/v1/mock-exams/sessions`
- **Quyền:** `student`
- **Mô tả:** Sinh viên chọn môn học và tùy chọn Có/Không Follow-up (`hasFollowUp: true/false`). Hệ thống kiểm tra hạn ngạch ngày, trừ 1 lượt thi thử, và bốc đề ngẫu nhiên từ kho `practice_questions` (tuyệt đối cô lập khỏi kho `exam_questions`) theo phân bổ ma trận Bloom của môn học. Thời gian làm bài: 30 phút (không follow-up) hoặc 45 phút (có follow-up).
- **Request Body:**
```json
{
  "courseId": "22222222-0000-0000-0000-000000000001",
  "hasFollowUp": true
}
```
- **Response 200 OK:**
```json
{
  "sessionId": "88888888-0000-0000-0000-000000000001",
  "courseId": "22222222-0000-0000-0000-000000000001",
  "courseCode": "PRN231",
  "courseName": "Building Cross-Platform Applications with .NET",
  "hasFollowUp": true,
  "durationMinutes": 45,
  "startedAt": "2026-10-14T14:00:00Z",
  "expiresAt": "2026-10-14T14:45:00Z",
  "usedQuota": 2,
  "remainingQuota": 1,
  "maxDailyQuota": 3,
  "questions": [
    {
      "id": "99999999-0000-0000-0000-000000000001",
      "orderIndex": 1,
      "title": "Phân tích nguyên lý DIP trong Clean Architecture",
      "imageUrl": "/uploads/questions/202610/a1b2c3d4e5f6478980123456789abcde.png",
      "imageDescription": "Sơ đồ 4 vòng tròn đồng tâm Clean Architecture.",
      "content": "Hãy phân tích trách nhiệm và quy tắc phụ thuộc của 4 phân tầng trong Clean Architecture.",
      "bloomLevel": "Analyze",
      "difficulty": "medium",
      "rubricCriteria": [
        { "name": "Hiểu rõ 4 phân tầng", "maxScore": 5.0 },
        { "name": "Quy tắc phụ thuộc một chiều", "maxScore": 5.0 }
      ]
    }
  ]
}
```
- **Response 429 Too Many Requests (Khi vượt quá hạn ngạch thi thử trong ngày):**
```json
{
  "type": "https://oralexam.fpt.edu.vn/errors/quota-exceeded",
  "title": "Too Many Requests",
  "status": 429,
  "detail": "Bạn đã sử dụng hết hạn ngạch thi thử trong ngày (3/3 lượt) cho môn học này."
}
```

#### 3. Nộp câu trả lời Voice-First qua Micro (Stream Whisper STT — Không lưu R2)
- **Endpoint:** `POST /api/v1/mock-exams/sessions/{id}/answers`
- **Quyền:** `student`
- **Content-Type:** `multipart/form-data`
- **Form Data Fields:**
  - `QuestionId` (`Guid`, bắt buộc): Mã câu hỏi trong đề thi.
  - `File` (`IFormFile`, bắt buộc nếu không truyền `Transcript`): File âm thanh `.webm`, `.mp3`, `.wav`, `.ogg` (tối đa 25MB).
  - `TimeTakenSeconds` (`int`, bắt buộc): Thời gian trả lời câu hỏi (giây).
  - `Transcript` (`string`, tùy chọn): Bản văn bản hiệu đính.
- **Quy tắc bất biến:** Stream trực tiếp vào Cloudflare Whisper STT trong RAM, **tuyệt đối KHÔNG lưu file audio lên Cloudflare R2**, chỉ lưu transcript vào `mock_exam_answers` (`audio_url = null`). Nếu quá hạn `ExpiresAt + 10s`, trả về `HTTP 410 Gone`.
- **Response 200 OK:**
```json
{
  "answerId": "123e4567-e89b-12d3-a456-426614174000",
  "questionId": "99999999-0000-0000-0000-000000000001",
  "transcript": "Clean Architecture có 4 tầng chính: Domain chứa entities, Application điều phối use case...",
  "status": "answered",
  "answeredAt": "2026-10-14T14:15:30Z"
}
```

#### 4. Nộp bài thi thử chính thức & Nhận Instant Feedback Scorecard (3–5s)
- **Endpoint:** `POST /api/v1/mock-exams/sessions/{id}/submit`
- **Quyền:** `student`
- **Quy tắc:** Kiểm tra Server Master Timer (`DateTime.UtcNow <= ExpiresAt + 10s`). Nếu nộp quá hạn, đánh dấu `isOverdue = true`, tự động khóa ca thi, chấm các câu đã trả lời trước đó và gán 0 điểm cho các câu bỏ trống. Chấm điểm đồng thời song song (`Task.WhenAll`) qua Google Gemini 1.5 Flash theo Barem Rubric 10.0đ CLO và trả về Scorecard tức thì.
- **Response 200 OK:**
```json
{
  "sessionId": "88888888-0000-0000-0000-000000000001",
  "courseId": "22222222-0000-0000-0000-000000000001",
  "courseCode": "PRN231",
  "courseName": "Building Cross-Platform Applications with .NET",
  "totalScore": 8.75,
  "classification": "Giỏi",
  "totalQuestions": 5,
  "answeredQuestions": 5,
  "durationMinutes": 45,
  "isOverdue": false,
  "startedAt": "2026-10-14T14:00:00Z",
  "submittedAt": "2026-10-14T14:28:30Z",
  "questions": [
    {
      "questionId": "99999999-0000-0000-0000-000000000001",
      "orderIndex": 1,
      "title": "Phân tích nguyên lý DIP trong Clean Architecture",
      "imageUrl": "/uploads/questions/202610/a1b2c3d4e5f6478980123456789abcde.png",
      "imageDescription": "Sơ đồ 4 vòng tròn đồng tâm Clean Architecture.",
      "content": "Hãy phân tích trách nhiệm và quy tắc phụ thuộc của 4 phân tầng trong Clean Architecture.",
      "bloomLevel": "Analyze",
      "difficulty": "medium",
      "transcript": "Clean Architecture có 4 tầng chính...",
      "score": 8.75,
      "feedback": "Nắm vững nguyên lý Dependency Inversion, liên hệ chính xác với tầng Infrastructure.",
      "confidenceScore": 0.96,
      "criteriaScores": [
        {
          "criterionId": "912ec803-b7b4-4b55-8855-467f5fe00111",
          "criterionName": "Hiểu rõ 4 phân tầng",
          "bloomLevel": "Understand",
          "maxScore": 5.0,
          "score": 4.5,
          "comment": "Liệt kê chính xác trách nhiệm của cả 4 tầng."
        },
        {
          "criterionId": "912ec803-b7b4-4b55-8855-467f5fe00222",
          "criterionName": "Quy tắc phụ thuộc một chiều",
          "bloomLevel": "Apply",
          "maxScore": 5.0,
          "score": 4.25,
          "comment": "Nêu đúng Dependency Rule hướng tâm."
        }
      ]
    }
  ]
}
```

#### 5. Tra cứu danh sách lịch sử thi thử (Student Portal — FE-02)
- **Endpoint:** `GET /api/v1/mock-exams/history?courseId={guid}&pageIndex=1&pageSize=10`
- **Quyền:** `student`
- **Query Params:** `courseId` (UUID, optional), `pageIndex` (default 1), `pageSize` (default 10)
- **Response 200 OK:**
```json
{
  "items": [
    {
      "sessionId": "88888888-0000-0000-0000-000000000001",
      "courseId": "22222222-0000-0000-0000-000000000001",
      "courseCode": "PRN231",
      "courseName": "Building Cross-Platform Applications with .NET",
      "startedAt": "2026-10-14T14:00:00Z",
      "durationMinutes": 45,
      "totalScore": 8.75,
      "status": "graded",
      "answeredQuestions": 5,
      "totalQuestions": 5
    }
  ],
  "pageIndex": 1,
  "pageSize": 10,
  "totalCount": 1,
  "totalPages": 1
}
```

#### 6. Xem lại chi tiết bài thi thử & Scorecard trong quá khứ
- **Endpoint:** `GET /api/v1/mock-exams/history/{sessionId}`
- **Quyền:** `student`
- **Mô tả:** Trả về toàn bộ đề thi, transcript câu trả lời và Scorecard nhận xét chi tiết của AI cho bài thi thử `sessionId` (cấu trúc JSON giống hệt `MockExamScorecardDto` ở mục 4).

---

### 5.7. Thi Thật Phòng Lab & An Ninh Kiosk (MF-04)
*(Phân hệ CSDL: `official_exam_sessions`, `real_exam_session_shifts`, `student_exam_tickets`, `exam_question_submissions`)*

#### 0. Khởi tạo kỳ thi & Cấu hình môn thi (Cán bộ Khảo thí)
- **Endpoint:** `POST /api/v1/official-exams/sessions`
- **Quyền:** `examination_officer`, `admin`
- **Mô tả:** Cán bộ Khảo thí (`examination_officer`) khởi tạo kỳ thi (`OfficialExamSession` / Exam Season), gán môn thi vào kỳ thi, thiết lập `examInputMode` (`VoiceOnly`, `VoiceWithTranscriptEdit`) cùng `transcriptBufferSeconds` (10..300s). Riêng cấu hình Follow-up thi thật (`hasFollowUp: boolean`, `maxFollowUpQuestions` từ `1–5` câu, mặc định `2`) thuộc 100% thẩm quyền của Trưởng Bộ Môn (`department_head`) (R3).
- **Request Body:**
```json
{
  "courseId": "22222222-0000-0000-0000-000000000001",
  "examStructureId": "44444444-0000-0000-0000-000000000001",
  "title": "Kỳ thi Vấn đáp Kết thúc môn FA26 - PRN231",
  "examDate": "2026-11-15",
  "hasFollowUp": true,
  "maxFollowUpQuestions": 1,
  "examInputMode": "VoiceOnly",
  "transcriptBufferSeconds": 60
}
```
- **Response 201 Created:**
```json
{
  "id": "77777777-0000-0000-0000-000000000001",
  "courseId": "22222222-0000-0000-0000-000000000001",
  "examStructureId": "44444444-0000-0000-0000-000000000001",
  "title": "Kỳ thi Vấn đáp Kết thúc môn FA26 - PRN231",
  "examDate": "2026-11-15",
  "hasFollowUp": true,
  "maxFollowUpQuestions": 1,
  "examInputMode": "VoiceOnly",
  "transcriptBufferSeconds": 60,
  "createdBy": "11111111-0000-0000-0000-000000000004",
  "status": "scheduled",
  "createdAt": "2026-10-10T08:00:00Z"
}
```

#### 1. Tạo ca thi phòng Lab & Gán danh sách thí sinh (Từ lớp học hoặc Import Excel FPT)
- **Tạo ca thi:** `POST /api/v1/official-exams/shifts`
- **Gán danh sách thí sinh:** Danh sách thí sinh của ca thi được hệ thống tự động trích xuất và gán trực tiếp từ lớp học đã có trong hệ thống (`classes`, `class_enrollments`), hoặc Cán bộ Khảo thí (`examination_officer`) có thể import danh sách dự phòng từ file Excel (EPPlus) qua endpoint: `POST /api/v1/official-exams/shifts/{id}/import-roster` (Giám thị `proctor` không có quyền quản lý ca thi hay import danh sách).
- **Quyền:** `examination_officer`, `admin`
- **Content-Type:** `multipart/form-data` (khi import Excel) hoặc `application/json` (khi gán từ lớp)
- **Cấu hình hình thức thi:** `allowTranscriptEdit: boolean`, `examInputMode: "VoiceOnly" | "VoiceWithTranscriptEdit"` (Nếu `VoiceOnly`: Khóa cứng 100% phím máy Kiosk, chỉ trả lời qua mic).
- **Response 200 OK:**
```json
{
  "shiftId": "bbbbbbbb-0000-0000-0000-000000000001",
  "roomLab": "LAB-302",
  "courseCode": "PRN231",
  "allowTranscriptEdit": false,
  "examInputMode": "VoiceOnly",
  "totalImported": 40,
  "assignedTickets": [
    { "ticketId": "cccccccc-0000-0000-0000-000000000001", "studentCode": "SE170001", "fullName": "Lê Vũ Hoàng", "seatNumber": 1 },
    { "ticketId": "cccccccc-0000-0000-0000-000000000002", "studentCode": "SE170002", "fullName": "Phạm Nguyễn Đăng Hải", "seatNumber": 2 }
  ]
}
```

#### 1b. Phân bổ đề thi không trùng lặp cho ca thi (Ràng buộc uq_tickets_shift_exam_set)
- **Endpoint:** `POST /api/v1/official-exams/shifts/{id}/distribute-tickets`
- **Quyền:** `examination_officer`, `admin`
- **Mô tả:** Cán bộ Khảo thí kích hoạt phân bổ các bộ đề thi đã bàn giao (`HANDED_OVER`) cho từng thí sinh trong ca thi. Ràng buộc toàn vẹn CSDL `uq_tickets_shift_exam_set` đảm bảo không có hai thí sinh nào trong cùng một ca thi nhận cùng một bộ đề (`exam_set_id`).
- **Path Parameters:**
  - `id` (`uuid`, bắt buộc): Định danh duy nhất của ca thi (`shiftId`).
- **Request Body:**
```json
{
  "allowRandomShuffle": true
}
```
- **Response 200 OK:**
```json
{
  "shiftId": "bbbbbbbb-0000-0000-0000-000000000001",
  "totalDistributed": 40,
  "message": "Đã phân bổ thành công bộ đề thi cho 40 thí sinh, đảm bảo không trùng lặp đề trong ca thi.",
  "distributedTickets": [
    {
      "ticketId": "cccccccc-0000-0000-0000-000000000001",
      "studentCode": "SE170001",
      "seatNumber": 1,
      "examSetId": "66666666-0000-0000-0000-000000000001"
    },
    {
      "ticketId": "cccccccc-0000-0000-0000-000000000002",
      "studentCode": "SE170002",
      "seatNumber": 2,
      "examSetId": "66666666-0000-0000-0000-000000000002"
    }
  ]
}
```

#### 1c. Xuất bản đề thi ca thi phòng Lab
- **Endpoint:** `POST /api/v1/official-exams/shifts/{id}/publish-exam`
- **Quyền:** `examination_officer`, `admin`
- **Mô tả:** Cán bộ Khảo thí mở xuất bản đề thi cho ca thi (`is_exam_published = true`). Các máy trạm Kiosk trong phòng thi chỉ được phép tải nội dung câu hỏi về máy sau khi đề thi ca thi đã được xuất bản chính thức.
- **Path Parameters:**
  - `id` (`uuid`, bắt buộc): Định danh ca thi (`shiftId`).
- **Response 200 OK:**
```json
{
  "shiftId": "bbbbbbbb-0000-0000-0000-000000000001",
  "isExamPublished": true,
  "publishedAt": "2026-11-15T07:30:00Z",
  "message": "Đề thi ca thi đã được xuất bản thành công. Các máy trạm Kiosk có thể tải đề thi."
}
```

#### 1d. Ghi nhật ký chống gian lận thời gian thực (Realtime Anti-Cheat Logging)
- **Endpoint:** `POST /api/v1/official-exams/anti-cheat/log`
- **Quyền:** `student` (từ Kiosk máy thi), `proctor`, `examination_officer`, `admin`
- **Mô tả:** Máy trạm Kiosk hoặc Giám thị gửi log vi phạm an ninh phòng thi theo thời gian thực để lưu vết vào bảng `exam_anti_cheat_logs`. Hệ thống tự động đếm số lần mất tiêu điểm (`WINDOW_BLUR > 3` lần $\to$ đình chỉ thi).
- **Request Body (`LogAntiCheatEventRequestDto`):**
```json
{
  "ticketId": "cccccccc-0000-0000-0000-000000000001",
  "eventType": "WINDOW_BLUR",
  "severity": "HIGH",
  "details": "Thí sinh chuyển đổi cửa sổ ứng dụng (Alt+Tab hoặc click chuột ra ngoài cửa sổ Kiosk) lần 2/3.",
  "ipAddress": "192.168.1.101"
}
```
- **Response 200 OK:**
```json
{
  "logId": "aaaa1111-0000-0000-0000-000000000001",
  "ticketId": "cccccccc-0000-0000-0000-000000000001",
  "eventType": "WINDOW_BLUR",
  "severity": "HIGH",
  "recordedAt": "2026-11-15T08:15:30Z",
  "blurCount": 2,
  "isSuspended": false,
  "message": "Nhật ký an ninh đã được ghi nhận."
}
```

#### 2. Quy trình 2 bước tải âm thanh bài thi lên Cloudflare R2

##### Bước 1: Yêu cầu ủy quyền tải lên âm thanh (Presigned Upload Authorization)
- **Endpoint:** `POST /api/v1/official-exams/tickets/{ticketId}/questions/{questionId}/audio-upload-authorizations`
- **Quyền:** `student`, `proctor`
- **Request Body:**
```json
{
  "fileName": "01_SE170001.webm",
  "contentType": "audio/webm",
  "expectedSizeBytes": 1548290
}
```
- **Response 200 OK:**
```json
{
  "uploadUrl": "https://r2.oralexam.edu.vn/exams/FA26/PRN231/01_SE170001.webm?X-Amz-Signature=...",
  "storageKey": "exams/FA26/PRN231/01_SE170001.webm",
  "expiresIn": 900
}
```

##### Bước 2: Xác nhận và niêm phong âm thanh bài thi
- **Endpoint:** `PUT /api/v1/official-exams/tickets/{ticketId}/questions/{questionId}/audio`
- **Quyền:** `student`, `proctor`
- **Request Body:**
```json
{
  "audioR2Url": "https://r2.oralexam.edu.vn/exams/FA26/PRN231/01_SE170001.webm",
  "audioHashSha256": "3a7acb4f9e1d84f85e8a71917f85897f26792617f6946028a36809cf525ee821",
  "durationSeconds": 240
}
```
- **Response 200 OK:**
```json
{
  "ticketId": "cccccccc-0000-0000-0000-000000000001",
  "questionId": "99999999-0000-0000-0000-000000000001",
  "status": "AUDIO_ATTACHED",
  "audioHashSha256": "3a7acb4f9e1d84f85e8a71917f85897f26792617f6946028a36809cf525ee821",
  "message": "File âm thanh đã được niêm phong và liên kết với bài nộp câu hỏi thành công."
}
```

#### 3. Nộp bài thi thật & Khóa màn hình Kiosk lưu an toàn (AI Instant Grading ngầm)
- **Endpoint:** `POST /api/v1/official-exams/submissions`
- **Quyền:** `student`
- **Request Body:**
```json
{
  "ticketId": "cccccccc-0000-0000-0000-000000000001",
  "examQuestionId": "99999999-0000-0000-0000-000000000001",
  "audioR2Url": "https://r2.oralexam.edu.vn/exams/FA26/PRN231/01_SE170001.webm",
  "audioHashSha256": "3a7acb4f9e1d84f85e8a71917f85897f26792617f6946028a36809cf525ee821",
  "timeSpentSeconds": 240
}
```
- **Response 200 OK — Khóa an toàn máy Kiosk (Không trả điểm về Kiosk):**
```json
{
  "ticketId": "cccccccc-0000-0000-0000-000000000001",
  "status": "SUBMITTED",
  "submittedAt": "2026-10-25T14:40:00Z",
  "kioskLockNotice": "Bài thi của bạn đã được ghi nhận an toàn và nộp thành công vào hệ thống. Điểm số chính thức sẽ do Giảng viên thẩm định và công bố trên Cổng thông tin Sinh viên (Student Portal). Thí sinh vui lòng giữ trật tự và rời khỏi phòng thi theo hiệu lệnh của Cán bộ coi thi.",
  "isKioskLocked": true
}
```
- **Cơ chế Persist First, Hàng đợi chịu tải & Chấm ngầm:**
  1. **Persist-First Ingestion (< 100ms):** Dữ liệu bài thi nộp từ Kiosk được lưu ngay vào CSDL bảng `exam_question_submissions`, cập nhật `student_exam_tickets` với trạng thái **`SUBMITTED` trong $< 100$ms** kèm mã băm SHA-256 niêm phong audio Cloudflare R2 (`STT_MSSV.webm`). Máy trạm Kiosk khóa cứng bảo mật tức thì, tuyệt đối không hiển thị điểm và không tiếp nhận khiếu nại tại phòng thi.
  2. **Hàng đợi BoundedChannel 1,000 slots RAM (`FullMode.Wait`):** Non-blocking dispatch tác vụ chấm điểm sang hàng đợi bộ nhớ, cách ly tải khi 40 máy nộp bài đồng thời.
  3. **Chấm ngầm & AI Doubt Guard:** Gemini 1.5 Pro rút task từ hàng đợi chấm Rubric ngầm chỉ dựa trên bản transcript, trích xuất `totalScore`, `aiConfidenceScore`, `isSuspicious` (`true` nếu `aiConfidenceScore < 0.70` hoặc phát hiện dị thường), cập nhật vé thi sang `AI_GRADED`.
  4. **Chống quá tải Polly Retry & DLQ 5 phút:** Nếu AI quá tải (HTTP 429) hoặc timeout, Polly tự động retry 3 lần (2s $\to$ 4s $\to$ 8s). Nếu thất bại sau 3 lần, bài nộp chuyển vào bảng `dead_letter_queues` (`PENDING_RETRY`), tiến trình nền `DlqReplayWorker` định kỳ 5 phút quét và chấm bù tự động, cam kết Zero Data Loss 100%.

#### 4. Tra cứu điểm thi chính thức trên Student Portal & Hướng dẫn phúc khảo nội bộ
- **Endpoint:** `GET /api/v1/student/official-exams/{ticketId}/grade`
- **Quyền:** `student` (chính chủ sở hữu vé thi)
- **Mô tả:** Sau khi Giảng viên hoàn tất hậu kiểm và bấm "Công Bố Điểm", sinh viên đăng nhập Student Portal để tra cứu bảng điểm chi tiết. Nếu sinh viên đồng ý với kết quả, sinh viên bấm xác nhận nhận điểm. Nếu không đồng ý, sinh viên có thể nộp đơn Phúc khảo nội bộ trực tiếp trên hệ thống (`POST /api/v1/appeals`) để chuyển tới Trưởng Bộ Môn thẩm định lại.
- **Response 200 OK:**
```json
{
  "ticketId": "cccccccc-0000-0000-0000-000000000001",
  "studentCode": "SE170001",
  "fullName": "Lê Vũ Hoàng",
  "courseCode": "PRN231",
  "status": "PUBLISHED",
  "isLocked": true,
  "totalScore": 8.50,
  "publishedAt": "2026-10-25T16:00:00Z",
  "publishedBy": "Nguyễn Trọng Tốt",
  "studentAcknowledgementStatus": "PENDING",
  "detailedScorecard": [
    {
      "questionId": "99999999-0000-0000-0000-000000000001",
      "score": 8.50,
      "criteriaScores": [
        { "criterionName": "Hiểu đúng bản chất kiến trúc", "score": 3.00, "maxScore": 3.00, "comment": "Nắm vững lý thuyết" },
        { "criterionName": "Liên hệ thực tiễn dự án", "score": 3.50, "maxScore": 4.00, "comment": "Giải thích rõ ràng" },
        { "criterionName": "Phản biện câu hỏi phụ", "score": 2.00, "maxScore": 3.00, "comment": "Đạt yêu cầu" }
      ],
      "strengths": "Phát âm rõ ràng, luận điểm mạch lạc, đúng trọng tâm đề bài.",
      "weaknesses": "Phần câu hỏi phụ trả lời hơi ngắn.",
      "improvementSuggestions": "Cần tự tin hơn khi giải thích các edge cases."
    }
  ],
  "internalAppeal": {
    "canAppeal": true,
    "instruction": "Trường hợp không đồng ý với kết quả điểm thi vấn đáp đã công bố, sinh viên có thể nộp đơn Phúc khảo nội bộ trực tiếp trên Student Portal kèm lý do chi tiết. Đơn phúc khảo sẽ được chuyển tới Trưởng Bộ Môn tiếp nhận và giao cho một Giảng viên chấm lại toàn bộ bài thi.",
    "appealDeadlineDays": 3,
    "appealEndpoint": "POST /api/v1/appeals"
  }
}
```

#### 5. Sinh viên xác nhận nhận điểm trên Student Portal
- **Endpoint:** `POST /api/v1/student/official-exams/{ticketId}/acknowledge-grade`
- **Quyền:** `student` (chính chủ sở hữu vé thi)
- **Request Body:**
```json
{
  "acknowledgement": true,
  "notes": "Em đồng ý với kết quả đánh giá vấn đáp của Giảng viên."
}
```
- **Response 200 OK:**
```json
{
  "ticketId": "cccccccc-0000-0000-0000-000000000001",
  "studentAcknowledgementStatus": "ACKNOWLEDGED",
  "acknowledgedAt": "2026-10-25T16:30:00Z",
  "message": "Sinh viên đã xác nhận đồng ý với điểm số chính thức thành công."
}
```

#### 6. Lấy trạng thái vé thi Kiosk (Khôi phục kết nối mạng / Reload bảo vệ)
- **Endpoint:** `GET /api/v1/official-exams/tickets/{ticketId}`
- **Quyền:** `student`, `proctor`, `lecturer`
- **Mục đích:** Hỗ trợ máy trạm Kiosk khôi phục lại trạng thái làm bài nếu bị rớt mạng hoặc refresh trình duyệt. Nếu bài thi đã nộp, Kiosk lập tức duy trì màn hình khóa an toàn bảo mật, không để lộ điểm thi.
##### Bảng đặc tả DTO Phản hồi (`GetExamTicketResponseDto`)
| Thuộc tính | Kiểu dữ liệu | Bắt buộc / Nullable | Ràng buộc / Enum | Mô tả nghiệp vụ |
|:---|:---|:---:|:---|:---|
| `ticketId` | `uuid` | Bắt buộc | UUID | Định danh vé thi |
| `seatNumber` | `int` | Bắt buộc | 1..40 | Số thứ tự máy trạm (Booth 1-40) |
| `studentCode` | `string` | Bắt buộc | Format MSSV | Mã số sinh viên |
| `fullName` | `string` | Bắt buộc | Max 150 chars | Họ và tên sinh viên |
| `status` | `string` | Bắt buộc | Enum 7 bước | Trạng thái vé thi (`SCHEDULED`, `IN_PROGRESS`, `SUBMITTED`, `AI_GRADED`, `AUDITED`, `PUBLISHED`, `LOCKED`) |
| `isKioskLocked` | `boolean` | Bắt buộc | Boolean | Trạng thái khóa màn hình Kiosk |
| `kioskLockNotice` | `string` | Nullable | Text | Thông báo hiển thị trên màn hình khóa Kiosk |
| `allowTranscriptEdit` | `boolean` | Bắt buộc | Boolean | Cờ cấu hình môn: Cho phép mở màn hình đệm sửa transcript |
| `examInputMode` | `string` | Bắt buộc | Enum 2 chế độ | Phương thức làm bài của môn (`VoiceOnly`, `VoiceWithTranscriptEdit`) để Kiosk thiết lập khóa cứng bàn phím |

- **Response 200 OK:**
```json
{
  "ticketId": "cccccccc-0000-0000-0000-000000000001",
  "seatNumber": 1,
  "studentCode": "SE170001",
  "fullName": "Lê Vũ Hoàng",
  "status": "SUBMITTED",
  "isKioskLocked": true,
  "kioskLockNotice": "Bài thi đã được nộp thành công và đang được lưu trữ an toàn.",
  "allowTranscriptEdit": false,
  "examInputMode": "VoiceOnly"
}
```

---

### 5.8. Hậu Kiểm Giảng Viên, Sửa Điểm Thẩm Định, Khóa Một Chiều & Xuất Bảng Điểm Khảo Thí
*(Phân hệ CSDL: `lecturer_audits`, `lecturer_audit_details`)*

#### 1. Lấy danh sách bài thi cần thẩm định theo Ca thi / Phòng Lab (Tự động phân 2 nhóm & Evidence Panel)
- **Endpoint:** `GET /api/v1/audit/submissions?shiftId={shiftId}&filter={filter}`
- **Quyền:** `lecturer`, `department_head`, `admin`
- **Query Params:**
  - `shiftId`: UUID ca thi phòng Lab bắt buộc.
  - `filter`: 
    - `"suspicious"`: **Nhóm 1 — Đáng nghi ngờ & Độ tin cậy thấp** (`is_suspicious == true` hoặc `ai_confidence_score < 0.70`). Được ưu tiên hiển thị đầu danh sách để Giảng viên đối soát Evidence Panel và thẩm định trước.
    - `"trusted"`: **Nhóm 2 — Độ tin cậy cao** (`is_suspicious == false` và `ai_confidence_score >= 0.70`).
    - `"all"`: Toàn bộ danh sách bài thi trong ca.
- **Evidence Panel:** Cung cấp đầy đủ 3 thành phần chứng cứ pháp lý gồm: `audioR2Url` (kèm `audioHashSha256`), `transcriptWhisper` (bản bóc băng gốc) và `aiChainOfThought` (chuỗi tư duy suy luận 3 bước của AI).
- **Response 200 OK:**
```json
[
  {
    "ticketId": "cccccccc-0000-0000-0000-000000000002",
    "studentCode": "SE170002",
    "fullName": "Phạm Nguyễn Đăng Hải",
    "seatNumber": 2,
    "status": "AI_GRADED",
    "audioR2Url": "https://r2.oralexam.edu.vn/exams/FA26/PRN231/02_SE170002.webm",
    "audioHashSha256": "4b8bdb50af2e95a96f9b82028a969a8037803728f7a57139b47910da636ff932",
    "transcriptWhisper": "Về kiến trúc Clean Architecture, tầng Domain chứa...",
    "aiChainOfThought": "1. Khảo sát Transcript: Thí sinh trình bày đúng định nghĩa DIP nhưng thiếu minh họa Service Lifetime.\n2. Barem Rubric: Tiêu chí 1 đạt 2.5/3.0đ, Tiêu chí 2 đạt 2.0/4.0đ, Tiêu chí 3 đạt 2.0/3.0đ.\n3. Kết luận: Tổng điểm 6.5đ, độ tin cậy 0.65 do tạp âm môi trường.",
    "aiScore": 6.50,
    "aiConfidenceScore": 0.65,
    "isSuspicious": true,
    "suspiciousReason": "Độ tin cậy AI < 0.70 và phát hiện tạp âm môi trường tại giây 01:15 - 01:40",
    "timestampsCitations": [
      { "start": "00:15", "end": "00:45", "claim": "Nêu khái niệm Domain Entity", "isVerified": true },
      { "start": "01:15", "end": "01:40", "claim": "Phân tích IoC Container", "isVerified": false }
    ],
    "finalScore": null,
    "isLocked": false,
    "auditedBy": null,
    "overrideReason": null
  },
  {
    "ticketId": "cccccccc-0000-0000-0000-000000000001",
    "studentCode": "SE170001",
    "fullName": "Lê Vũ Hoàng",
    "seatNumber": 1,
    "status": "AI_GRADED",
    "audioR2Url": "https://r2.oralexam.edu.vn/exams/FA26/PRN231/01_SE170001.webm",
    "audioHashSha256": "3a7acb4f9e1d84f85e8a71917f85897f26792617f6946028a36809cf525ee821",
    "transcriptWhisper": "Em xin trình bày về nguyên lý Dependency Inversion...",
    "aiChainOfThought": "1. Khảo sát Transcript: Trình bày trọn vẹn khái niệm DIP và tách tầng Clean Arch.\n2. Barem Rubric: Tiêu chí 1 đạt 3.0/3.0đ, Tiêu chí 2 đạt 3.0/4.0đ, Tiêu chí 3 đạt 2.0/3.0đ.\n3. Kết luận: Tổng điểm 8.0đ, độ tin cậy 0.92.",
    "aiScore": 8.00,
    "aiConfidenceScore": 0.92,
    "isSuspicious": false,
    "suspiciousReason": null,
    "timestampsCitations": [
      { "start": "00:10", "end": "01:20", "claim": "Định nghĩa DIP và tách tầng Clean Architecture", "isVerified": true },
      { "start": "01:25", "end": "02:10", "claim": "Minh họa DI IoC ServiceCollection", "isVerified": true }
    ],
    "finalScore": 8.00,
    "isLocked": false,
    "auditedBy": null,
    "overrideReason": null
  }
]
```

#### 2. Giảng viên sửa điểm thẩm định (Bắt buộc kèm lý do giải trình)
- **Endpoint:** `PUT /api/v1/audit/override`
- **Quyền:** `lecturer`, `department_head`, `admin`
- **Quy tắc:** Chỉ được phép gọi khi bài thi chưa bị khóa (`is_locked == false`). Giảng viên sử dụng Waveform Audio Player nghe lại các đoạn âm thanh nghi ngờ (được AI gắn nhãn timestamps citations) để chấm lại điểm từng tiêu chí barem. Trường `overrideReason` bắt buộc $\ge 10$ ký tự để lưu vết kiểm toán pháp lý.
- **Request Body:**
```json
{
  "ticketId": "cccccccc-0000-0000-0000-000000000002",
  "auditedScore": 7.50,
  "overrideReason": "Đã nghe lại Waveform đoạn 01:15-01:40, sinh viên có giải thích đúng bản chất IoC Container dù giọng nói hơi nhỏ do micro phòng Lab.",
  "criteriaScores": [
    { "criterionId": "crit-01", "auditedScore": 3.00, "comment": "Hiểu rõ khái niệm" },
    { "criterionId": "crit-02", "auditedScore": 2.50, "comment": "Cộng 1.0 điểm sau khi nghe lại đoạn 01:15" },
    { "criterionId": "crit-03", "auditedScore": 2.00, "comment": "Đạt yêu cầu phản biện" }
  ]
}
```
- **Response 200 OK:** Cập nhật điểm thẩm định thành công, chuyển trạng thái vé thi sang `AUDITED` và tự động ghi log vào `audit_logs`.
- **Response 422 Unprocessable Entity:** Nếu để trống hoặc lý do giải trình $< 10$ ký tự.

#### 3. Công Bố Điểm ca thi & Kích hoạt Khóa điểm một chiều (One-Way Lock)
- **Endpoint:** `POST /api/v1/official-exams/shifts/{shiftId}/publish-grades`
- **Quyền:** `examination_officer`, `lecturer`, `admin`
- **Cơ chế:** 
  1. **Điều kiện tiên quyết bất biến:** Kiểm tra **100% sinh viên trong ca thi đã có điểm hoàn chỉnh** (tất cả các vé thi đều có `final_score` hoặc `ai_score` hợp lệ, không có vé nào còn ở trạng thái chưa chấm). Nếu phát hiện bất kỳ bài thi nào chưa có điểm, hệ thống từ chối và trả về `HTTP 422 Unprocessable Entity`.
  2. Cán bộ Khảo thí hoặc Giảng viên sau khi rà soát toàn bộ ca thi trên Cổng Hậu kiểm, bấm nút **"Công Bố Điểm"**.
  3. Hệ thống chuyển trạng thái toàn bộ vé thi trong ca từ `AUDITED` (hoặc `AI_GRADED`) sang `PUBLISHED`.
  4. Kích hoạt vĩnh viễn cơ chế **Khóa một chiều (One-Way Lock)**: thiết lập `is_locked = true` trên toàn bộ vé thi của ca, kích hoạt `OneWayLockInterceptor`. Sau thời điểm này, mọi thao tác sửa điểm đều bị chặn và trả về `HTTP 403 Forbidden` (ngoại trừ duy nhất đường hợp lệ khi Giảng viên được Trưởng Bộ Môn phân công chấm lại đơn phúc khảo nội bộ cập nhật điểm thông qua luồng thẩm định phúc khảo có kiểm soát và ghi log kiểm toán).
  5. Mở quyền cho sinh viên tra cứu điểm trên Student Portal.
- **Request Body:** `{}`
- **Response 200 OK:**
```json
{
  "shiftId": "bbbbbbbb-0000-0000-0000-000000000001",
  "publishedCount": 40,
  "status": "PUBLISHED",
  "isLocked": true,
  "publishedAt": "2026-10-25T16:00:00Z",
  "publishedBy": "Nguyễn Trọng Tốt",
  "message": "Đã công bố điểm chính thức cho toàn bộ ca thi phòng Lab. Cơ chế Khóa một chiều (One-Way Lock) đã kích hoạt thành công."
}
```
- **Response 422 Unprocessable Entity (Nếu chưa hoàn tất điểm 100% thí sinh):**
```json
{
  "type": "https://oralexam.fpt.edu.vn/errors/incomplete-shift-grades",
  "title": "Incomplete Shift Grades",
  "status": 422,
  "detail": "Không thể công bố điểm: Vẫn còn 2 sinh viên trong ca thi chưa có điểm hoàn chỉnh. Vui lòng hoàn tất thẩm định điểm toàn bộ sinh viên trước khi công bố.",
  "uncompletedTickets": [
    { "ticketId": "cccccccc-0000-0000-0000-000000000003", "studentCode": "SE170003", "fullName": "Trần Văn A", "status": "SUBMITTED" },
    { "ticketId": "cccccccc-0000-0000-0000-000000000015", "studentCode": "SE170015", "fullName": "Lê Thị B", "status": "SUBMITTED" }
  ]
}
```
- **Response 403 Forbidden (Nếu cố tình sửa đổi sau khi ca thi đã công bố điểm):**
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.4",
  "title": "Forbidden - One-Way Lock Activated",
  "status": 403,
  "detail": "Ca thi và bài thi này đã được Giảng viên công bố điểm và khóa một chiều (One-Way Lock). Hồ sơ khảo thí đã niêm phong vĩnh viễn, tuyệt đối không được phép chỉnh sửa trực tiếp (ngoại trừ quy trình chấm lại phúc khảo hợp lệ do Trưởng Bộ Môn phân công cho Giảng viên)."
}
```

#### 4. Khóa điểm riêng lẻ từng vé thi (One-Way Lock từng bài thi)
- **Endpoint:** `POST /api/v1/audit/lock`
- **Quyền:** `lecturer`, `department_head`, `admin`
- **Request Body:**
```json
{
  "ticketId": "cccccccc-0000-0000-0000-000000000001"
}
```
- **Response 200 OK:**
```json
{
  "ticketId": "cccccccc-0000-0000-0000-000000000001",
  "isLocked": true,
  "lockedAt": "2026-10-25T15:00:00Z",
  "message": "Điểm bài thi đã được khóa một chiều thành công. Hồ sơ khảo thí đã được niêm phong vĩnh viễn (chỉ mở đường cập nhật điểm khi có quyết định phân công chấm lại phúc khảo từ Trưởng Bộ Môn)."
}
```

#### 5. Xuất file Excel bảng điểm khảo thí định dạng chuẩn FPT
- **Endpoint:** `GET /api/v1/audit/export-excel?shiftId={shiftId}`
- **Quyền:** `lecturer`, `department_head`, `admin`
- **Response:** File nhị phân `.xlsx` (MIME: `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`).

---

### 5.9. Cứu hộ Dead-Letter Queue (DLQ Replay) & Quản trị Audit Logs
*(Phân hệ CSDL: `dead_letter_queues`, `audit_logs`)*

#### Tra cứu danh sách task lỗi trong Dead-Letter Queue
- **Endpoint:** `GET /api/v1/admin/dlq/tasks?status=pending` (cho luồng Luyện tập MF-01 & Thi thử MF-02) và `GET /api/v1/examination-officer/dlq/tasks?status=pending` (cho luồng Thi thật phòng Lab MF-04) (Alias tương thích: `GET /api/v1/dlq/tasks?status=pending`)
- **Quyền (Phân định R2):** `/api/v1/admin/dlq/*` thuộc `admin` (giám sát & replay DLQ luồng MF-01/02); `/api/v1/examination-officer/dlq/*` thuộc `examination_officer` (giám sát & replay DLQ luồng MF-04). **Cắt bỏ hoàn toàn quyền DLQ khỏi `department_head` (`❌`)**.
- **Response 200 OK:**
```json
[
  {
    "id": "dddddddd-0000-0000-0000-000000000001",
    "taskType": "practice_grading",
    "errorMessage": "Gemini API Gateway Timeout (504) after 3 retries",
    "retryCount": 3,
    "status": "pending",
    "createdAt": "2026-10-15T10:30:00Z"
  }
]
```

#### Kích hoạt cứu hộ chấm bù bài thi (DLQ Replay)
- **Endpoint:** `POST /api/v1/admin/dlq/tasks/{id}/replay` (luồng MF-01/02) và `POST /api/v1/examination-officer/dlq/tasks/{id}/replay` (luồng MF-04) (Alias tương thích: `POST /api/v1/dlq/tasks/{id}/replay`)
- **Quyền (Phân định R2):** `admin` (độc quyền replay DLQ cho luồng Luyện tập MF-01 & Thi thử MF-02 qua `/api/v1/admin/dlq/*`), `examination_officer` (độc quyền replay DLQ cho luồng Thi thật phòng Lab MF-04 qua `/api/v1/examination-officer/dlq/*`). **Cắt bỏ hoàn toàn quyền DLQ khỏi `department_head` (`❌`)**.
- **Response 200 OK:**
```json
{
  "dlqId": "dddddddd-0000-0000-0000-000000000001",
  "status": "requeued",
  "message": "Đã đẩy bài thi trở lại hàng đợi Bounded Channel để Worker tiến hành chấm bù thành công."
}
```

#### Tra cứu nhật ký kiểm toán hệ thống (Audit Logs)
- **Endpoint:** `GET /api/v1/admin/audit-logs?entityName=student_exam_tickets`
- **Quyền:** `admin`
- **Response 200 OK:**
```json
[
  {
    "id": "eeeeeeee-0000-0000-0000-000000000001",
    "userEmail": "totnd@fe.edu.vn",
    "action": "OVERRIDE_SCORE",
    "entityName": "student_exam_tickets",
    "entityId": "cccccccc-0000-0000-0000-000000000001",
    "oldValues": "{\"score\": 8.00}",
    "newValues": "{\"score\": 8.50, \"reason\": \"Sinh viên nói lưu loát...\"}",
    "ipAddress": "10.1.20.15",
    "createdAt": "2026-10-25T14:15:00Z"
  }
]
```

### 5.10. Phân Hệ Phúc Khảo Nội Bộ (Internal Appeal Requests) — MF-04
*(Phân hệ CSDL: `appeal_requests`)*

#### 1. Bước 1/5 — Sinh viên nộp đơn phúc khảo bài thi chính thức (`POST /api/v1/student/appeals`)
- **Endpoint:** `POST /api/v1/student/appeals` (Alias tương thích: `POST /api/v1/appeals`)
- **Quyền:** `student` (chính chủ sở hữu vé thi đã công bố điểm)
- **Mô tả:** Sau khi bài thi được công bố điểm (`PUBLISHED`), nếu sinh viên không đồng ý với kết quả đánh giá, sinh viên nộp đơn phúc khảo trực tiếp trên Student Portal kèm lý do cụ thể. Hệ thống tạo bản ghi `appeal_requests` ở trạng thái `SUBMITTED_TO_EXAM_OFFICE` gửi lên **Cán bộ Khảo thí (`examination_officer`)** để tiếp nhận & thẩm tra hợp lệ (`POST /api/v1/examination-officer/appeals/{id}/forward-to-head`) trước khi chuyển giao cho **Trưởng Bộ Môn (`department_head`)** phân công Giảng viên chấm lại (`POST /api/v1/department-head/appeals/{id}/assign-lecturer`).
- **Request Body:**
```json
{
  "ticketId": "cccccccc-0000-0000-0000-000000000001",
  "reason": "Em xin phúc khảo câu 2 phần Dependency Injection: Em đã giải thích đúng cơ chế phân biệt giữa Scoped và Transient Lifetime nhưng bị chấm trừ điểm."
}
```
- **Response 201 Created:**
```json
{
  "appealId": "dddddddd-0000-0000-0000-000000000001",
  "ticketId": "cccccccc-0000-0000-0000-000000000001",
  "studentId": "aaaaaaa1-0000-0000-0000-000000000001",
  "status": "SUBMITTED_TO_EXAM_OFFICE",
  "assignedTo": "e4c5b2a1-0001-4000-8000-000000000003",
  "createdAt": "2026-10-26T08:30:00Z",
  "message": "Đã tiếp nhận đơn phúc khảo thành công. Đơn đã được chuyển đến Cán bộ Khảo thí để thẩm tra tính hợp lệ và chuyển giao cho Trưởng Bộ Môn phân công Giảng viên chấm lại bài thi."
}
```
- **Response 422 Unprocessable Entity:** Nếu vé thi chưa ở trạng thái `PUBLISHED` hoặc lý do phúc khảo để trống / dưới 10 ký tự.
- **Response 409 Conflict:** Nếu sinh viên đã có đơn phúc khảo đang xử lý (`PENDING`) cho vé thi này.

#### 2. Tra cứu danh sách đơn phúc khảo
- **Endpoint:** `GET /api/v1/appeals`
- **Quyền:** `examination_officer`, `department_head`, `admin`, `lecturer` (giảng viên xem đơn được phân công), `student` (sinh viên chỉ xem đơn của chính mình)
- **Query Params:**
  - `status`: `"SUBMITTED_TO_EXAM_OFFICE"` | `"FORWARDED_TO_DEPT_HEAD"` | `"ASSIGNED_TO_LECTURER"` | `"REGRADED_BY_LECTURER"` | `"APPROVED"` | `"REJECTED"` (optional)
  - `courseId`: UUID môn học (optional)
  - `page`: default 1
  - `pageSize`: default 10
- **Response 200 OK:**
```json
{
  "totalAppeals": 3,
  "page": 1,
  "pageSize": 10,
  "items": [
    {
      "appealId": "dddddddd-0000-0000-0000-000000000001",
      "ticketId": "cccccccc-0000-0000-0000-000000000001",
      "studentCode": "SE170001",
      "fullName": "Lê Vũ Hoàng",
      "courseCode": "PRN231",
      "currentScore": 8.50,
      "reason": "Em xin phúc khảo câu 2 phần Dependency Injection...",
      "status": "SUBMITTED_TO_EXAM_OFFICE",
      "assignedToName": "Trần Thị C (Trưởng Bộ Môn SE)",
      "createdAt": "2026-10-26T08:30:00Z",
      "reviewedAt": null,
      "proposedScore": null,
      "reviewNotes": null
    }
  ]
}
```

#### 2b. Bước 2/5 — Cán bộ Khảo thí tiếp nhận, thẩm tra hợp lệ & chuyển giao Trưởng Bộ Môn (`POST /api/v1/examination-officer/appeals/{id}/forward-to-head`)
- **Endpoint:** `POST /api/v1/examination-officer/appeals/{id}/forward-to-head`
- **Quyền:** `examination_officer`, `admin`
- **Mô tả:** Cán bộ Khảo thí tiếp nhận đơn phúc khảo (`SUBMITTED_TO_EXAM_OFFICE`), thẩm tra tính hợp lệ (thời hạn nộp đơn, trạng thái vé thi đã công bố điểm), nhập ghi chú thẩm tra (`verificationNotes`) và chuyển giao hồ sơ sang cho Trưởng Bộ Môn của môn học (`status = "FORWARDED_TO_DEPT_HEAD"`).
- **Request Body:**
```json
{
  "isValid": true,
  "verificationNotes": "Đơn nộp đúng hạn quy định 3 ngày kể từ khi công bố điểm, hồ sơ vé thi và file ghi âm R2 đầy đủ."
}
```
- **Response 200 OK:**
```json
{
  "appealId": "dddddddd-0000-0000-0000-000000000001",
  "status": "FORWARDED_TO_DEPT_HEAD",
  "verifiedBy": "11111111-0000-0000-0000-000000000004",
  "forwardedAt": "2026-10-26T09:15:00Z",
  "message": "Đã thẩm tra hợp lệ và chuyển giao đơn phúc khảo cho Trưởng Bộ Môn thành công."
}
```

#### 3. Bước 3/5 — Trưởng Bộ Môn phân công Giảng viên chấm lại (`POST /api/v1/department-head/appeals/{id}/assign-lecturer`)
- **Endpoint:** `POST /api/v1/department-head/appeals/{id}/assign-lecturer` (Alias tương thích: `PUT /api/v1/appeals/{id}/assign-lecturer`)
- **Quyền:** `department_head`, `admin`
- **Mô tả:** Trưởng Bộ Môn tiếp nhận đơn đã qua thẩm tra của Khảo thí (`FORWARDED_TO_DEPT_HEAD`) và phân công một Giảng viên chuyên môn độc lập chấm lại bài thi (`status = "ASSIGNED_TO_LECTURER"`).

#### 4. Bước 4/5 — Giảng viên được phân công chấm lại độc lập trên Evidence Panel (`PUT /api/v1/lecturer/appeals/{id}/regrade`)
- **Endpoint:** `PUT /api/v1/lecturer/appeals/{id}/regrade` (Alias tương thích: `PUT /api/v1/appeals/{id}/regrade`)
- **Quyền:** `lecturer`, `admin`
- **Mô tả:** Giảng viên được phân công truy cập Evidence Panel, nghe lại file ghi âm `STT_MSSV.webm` trên Waveform Player, đối soát Transcript Whisper và nhập biên bản giải trình (`regradeNotes` $\ge 10$ ký tự) kèm điểm đề xuất mới (`proposedScore`), chuyển trạng thái đơn sang `REGRADED_BY_LECTURER`.
- **Request Body:**
```json
{
  "proposedScore": 9.00,
  "regradeNotes": "Sau khi nghe lại đoạn âm thanh 01:25-02:10 trên Waveform Player, sinh viên đã giải thích chính xác cơ chế Scoped Service Provider. Đề xuất tăng 0.5 điểm cho tiêu chí 2."
}
```
- **Response 200 OK:**
```json
{
  "appealId": "dddddddd-0000-0000-0000-000000000001",
  "status": "REGRADED_BY_LECTURER",
  "proposedScore": 9.00,
  "regradedBy": "33333333-0000-0000-0000-000000000001",
  "regradedAt": "2026-10-26T15:30:00Z",
  "message": "Giảng viên đã hoàn tất chấm lại và gửi đề xuất điểm lên Trưởng Bộ Môn phê duyệt."
}
```

#### 5. Bước 5/5 — Trưởng Bộ Môn thẩm định & phê duyệt quyết định phúc khảo cuối cùng (`POST /api/v1/department-head/appeals/{id}/decision`)
- **Endpoint:** `POST /api/v1/department-head/appeals/{id}/decision` (Alias tương thích: `PUT /api/v1/appeals/{id}/review`)
- **Quyền:** `department_head`, `admin`
- **Mô tả:** Trưởng Bộ Môn (`department_head`) xem xét biên bản chấm lại và đề xuất điểm của Giảng viên (`REGRADED_BY_LECTURER`), đối soát Evidence Panel để đưa ra quyết định phê duyệt cuối cùng:
  - Nếu `decision = "APPROVED"`: Chấp thuận phúc khảo, cập nhật điểm số mới (`proposedScore`), ghi nhận lý do điều chỉnh (`reviewNotes`), mở khóa một chiều có kiểm soát để cập nhật điểm chính thức vào vé thi và ghi log kiểm toán.
  - Nếu `decision = "REJECTED"`: Bác bỏ yêu cầu phúc khảo, giữ nguyên điểm thi, ghi rõ lý do giải trình vào `reviewNotes`.
- **Request Body:**
```json
{
  "decision": "APPROVED",
  "proposedScore": 9.00,
  "reviewNotes": "Sau khi nghe lại đoạn âm thanh 01:25-02:10 trên Waveform Player và đối soát Transcript Whisper, sinh viên đã giải thích rất chính xác cơ chế Scoped Service Provider. Đồng ý tăng 0.5 điểm cho tiêu chí 2."
}
```
- **Response 200 OK:**
```json
{
  "appealId": "dddddddd-0000-0000-0000-000000000001",
  "ticketId": "cccccccc-0000-0000-0000-000000000001",
  "status": "APPROVED",
  "finalScore": 9.00,
  "reviewedBy": "Trần Thị C",
  "reviewedAt": "2026-10-27T09:15:00Z",
  "message": "Đã phê duyệt phúc khảo và cập nhật điểm chính thức thành công."
}
```
- **Response 422 Unprocessable Entity:** Nếu thiếu trường `decision`, `reviewNotes` dưới 10 ký tự hoặc `proposedScore` nằm ngoài khoảng 0.0–10.0.

#### 5b. Chi tiết Payload Phân công Giảng viên chấm lại (Bước 3/5 — `POST /api/v1/department-head/appeals/{id}/assign-lecturer`)
- **Endpoint:** `PUT /api/v1/appeals/{id}/assign-lecturer`
- **Quyền:** `department_head`, `admin`
- **Mô tả:** Trưởng Bộ Môn ủy quyền / giao đơn phúc khảo đã được Cán bộ Khảo thí thẩm tra (`FORWARDED_TO_DEPT_HEAD`) cho Giảng viên chuyên môn thẩm định bài thi trên Evidence Panel và chấm lại. Hệ thống cập nhật `assigned_to = assignedLecturerId`, chuyển trạng thái đơn sang `ASSIGNED_TO_LECTURER`, và tự động gửi thông báo in-app (FE-11) tới Giảng viên được giao.
- **Request Body:**
```json
{
  "assignedLecturerId": "33333333-0000-0000-0000-000000000001",
  "notes": "Nhờ thầy Tốt nghe lại đoạn trả lời 01:25-02:10 và chấm lại cho sinh viên này do micro phòng thi bị rè."
}
```
- **Response 200 OK:**
```json
{
  "appealId": "dddddddd-0000-0000-0000-000000000001",
  "ticketId": "cccccccc-0000-0000-0000-000000000001",
  "status": "ASSIGNED_TO_LECTURER",
  "assignedTo": "33333333-0000-0000-0000-000000000001",
  "assignedLecturerName": "Nguyen Trong Tot",
  "assignedAt": "2026-10-26T10:00:00Z",
  "notes": "Nhờ thầy Tốt nghe lại đoạn trả lời 01:25-02:10 và chấm lại cho sinh viên này do micro phòng thi bị rè.",
  "message": "Đã phân công giảng viên chấm lại đơn phúc khảo thành công."
}
```
- **Response 404 Not Found:** Nếu không tìm thấy đơn phúc khảo.
- **Response 422 Unprocessable Entity:** Nếu `assignedLecturerId` rỗng, người được gán không có vai trò `lecturer`, hoặc đơn không ở trạng thái `FORWARDED_TO_DEPT_HEAD`.

---

### 5.11. Hộp thư Thông báo trong Ứng dụng (In-App Notifications) — FE-11
*(Phân hệ CSDL: `notifications`)*

Hộp thư thông báo trong ứng dụng phục vụ hiển thị chuông thông báo (Bell Icon) cho 6 vai trò người dùng (`student`, `lecturer`, `department_head`, `examination_officer`, `proctor`, `admin`), hỗ trợ 5 nhóm sự kiện chính:
1. `EXAM_SCHEDULE_PUBLISHED`: Lịch thi và danh sách ca thi được công bố.
2. `EXAM_GRADE_PUBLISHED`: Điểm thi chính thức của môn thi được Giảng viên công bố.
3. `APPEAL_LECTURER_ASSIGNED`: Giảng viên được Trưởng Bộ Môn giao chấm lại đơn phúc khảo.
4. `QUESTION_REVIEW_SUBMITTED`: Trưởng Bộ Môn nhận được câu hỏi dự thảo do Giảng viên gửi duyệt.
5. `QUESTION_NEEDS_REVISION`: Giảng viên nhận được yêu cầu hiệu chỉnh câu hỏi từ Trưởng Bộ Môn.

#### 1. Lấy danh sách thông báo của người dùng
- **Endpoint:** `GET /api/v1/notifications`
- **Quyền:** Authenticated (`student`, `lecturer`, `department_head`, `proctor`, `admin`)
- **Query Params:**
  - `unreadOnly`: `boolean` (mặc định `false`, nếu `true` chỉ lấy thông báo chưa đọc).
  - `page`: `int` (mặc định 1).
  - `pageSize`: `int` (mặc định 20, tối đa 50).
- **Response 200 OK:**
```json
{
  "totalCount": 5,
  "unreadCount": 2,
  "page": 1,
  "pageSize": 20,
  "items": [
    {
      "id": "eeeeeeee-0000-0000-0000-000000000001",
      "title": "Điểm thi vấn đáp đã được công bố",
      "message": "Điểm thi môn PRN231 - Ca 1 Sáng đã được Giảng viên công bố chính thức. Sinh viên vui lòng kiểm tra và xác nhận điểm hoặc gửi đơn phúc khảo trong vòng 3 ngày.",
      "type": "EXAM_GRADE_PUBLISHED",
      "isRead": false,
      "metadata": {
        "ticketId": "cccccccc-0000-0000-0000-000000000001",
        "courseCode": "PRN231",
        "shiftId": "bbbbbbbb-0000-0000-0000-000000000001"
      },
      "createdAt": "2026-10-25T15:00:00Z",
      "readAt": null
    },
    {
      "id": "eeeeeeee-0000-0000-0000-000000000002",
      "title": "Phân công chấm lại đơn phúc khảo",
      "message": "Trưởng Bộ Môn đã phân công bạn thẩm định lại đơn phúc khảo của sinh viên Lê Vũ Hoàng (SE170001) môn PRN231.",
      "type": "APPEAL_LECTURER_ASSIGNED",
      "isRead": false,
      "metadata": {
        "appealId": "dddddddd-0000-0000-0000-000000000001",
        "ticketId": "cccccccc-0000-0000-0000-000000000001"
      },
      "createdAt": "2026-10-26T10:00:00Z",
      "readAt": null
    }
  ]
}
```

#### 2. Đánh dấu một thông báo đã đọc
- **Endpoint:** `PUT /api/v1/notifications/{id}/read`
- **Quyền:** Authenticated (chính chủ sở hữu thông báo)
- **Response 200 OK:**
```json
{
  "id": "eeeeeeee-0000-0000-0000-000000000001",
  "isRead": true,
  "readAt": "2026-10-25T15:05:00Z"
}
```
- **Response 404 Not Found:** Nếu không tìm thấy thông báo hoặc thông báo không thuộc người dùng hiện tại.

#### 3. Đánh dấu tất cả thông báo đã đọc
- **Endpoint:** `PUT /api/v1/notifications/read-all`
- **Quyền:** Authenticated
- **Response 200 OK:**
```json
{
  "updatedCount": 2,
  "message": "Đã đánh dấu tất cả thông báo là đã đọc."
}
```

---

### 5.12. Giám thị Phòng Lab 40 Ghế (`/api/v1/proctor/*` — R4) & Dashboard Giảng viên
*(Phân hệ CSDL: `student_exam_tickets`, `exam_anti_cheat_logs`)*

#### 1. Điểm danh ghế ngồi 1–40 phòng Lab (`POST /api/v1/proctor/shifts/{id}/attendance`)
- **Endpoint:** `POST /api/v1/proctor/shifts/{id}/attendance` (Kèm tra cứu danh sách ghế: `GET /api/v1/proctor/shifts/{id}/attendance`)
- **Quyền:** `proctor`, `admin`
- **Mô tả (R4):** Giám thị phòng Lab (`proctor`) CHỈ có quyền điểm danh số thứ tự ghế (`SeatNumber` 1–40) đối chiếu thẻ Sinh viên / CCCD thực tế tại phòng thi. **Cắt bỏ hoàn toàn các quyền lạm quyền của Giám thị:** CẤM mở/đóng ca thi, CẤM xuất bản đề, CẤM đình chỉ/reset máy trạm, CẤM import danh sách sinh viên, CẤM xem điểm AI hay sửa điểm.
- **Request Body:**
```json
{
  "ticketId": "cccccccc-0000-0000-0000-000000000001",
  "seatNumber": 1,
  "isPresent": true,
  "identityVerified": true,
  "notes": "Đã đối chiếu thẻ SV và CCCD khớp thông tin tại ghế số 01."
}
```
- **Response 200 OK:**
```json
{
  "shiftId": "bbbbbbbb-0000-0000-0000-000000000001",
  "ticketId": "cccccccc-0000-0000-0000-000000000001",
  "seatNumber": 1,
  "isPresent": true,
  "checkedInByProctorAt": "2026-11-15T07:45:00Z",
  "message": "Đã xác nhận điểm danh thí sinh tại ghế số 1 thành công."
}
```

#### 2. Báo cáo vi phạm an ninh phòng thi (`POST /api/v1/proctor/shifts/{id}/violations`)
- **Endpoint:** `POST /api/v1/proctor/shifts/{id}/violations`
- **Quyền:** `proctor`, `admin`
- **Mô tả (R4):** Giám thị ghi nhận trực tiếp hành vi vi phạm quy chế phòng thi vào bảng `exam_anti_cheat_logs`.
- **Request Body:**
```json
{
  "ticketId": "cccccccc-0000-0000-0000-000000000002",
  "seatNumber": 2,
  "violationType": "UNAUTHORIZED_MATERIAL",
  "severity": "HIGH",
  "description": "Thí sinh sử dụng tài liệu giấy không được phép trong phòng thi vấn đáp."
}
```
- **Response 201 Created:**
```json
{
  "violationLogId": "aaaa2222-0000-0000-0000-000000000002",
  "shiftId": "bbbbbbbb-0000-0000-0000-000000000001",
  "ticketId": "cccccccc-0000-0000-0000-000000000002",
  "recordedAt": "2026-11-15T08:20:00Z",
  "message": "Đã ghi nhận biên bản vi phạm phòng thi vào exam_anti_cheat_logs thành công."
}
```

---

### 5.13. Chuỗi Phân Cấp Học Thuật 4 Cấp & Tự Tham Gia Lớp Học Phần (R5)
*(Phân hệ CSDL: `semesters`, `courses`, `course_lecturers`, `classes`, `class_enrollments`)*

#### 1. Cấp 1 — Admin gán Trưởng Bộ Môn vào quản lý môn học (`PUT /api/v1/admin/courses/{id}/assign-department-head`)
- **Endpoint:** `PUT /api/v1/admin/courses/{id}/assign-department-head`
- **Quyền:** `admin`
- **Mô tả:** Sau khi tạo Học kỳ (`semesters`) và Môn học (`courses`), Admin gán một Trưởng Bộ Môn (`department_head`) phụ trách chuyên môn cho môn học (`courses.department_head_user_id`).
- **Request Body:**
```json
{
  "departmentHeadUserId": "e4c5b2a1-0001-4000-8000-000000000005"
}
```
- **Response 200 OK:**
```json
{
  "courseId": "22222222-0000-0000-0000-000000000001",
  "courseCode": "PRN231",
  "departmentHeadUserId": "e4c5b2a1-0001-4000-8000-000000000005",
  "updatedAt": "2026-10-01T08:00:00Z",
  "message": "Đã gán Trưởng Bộ Môn phụ trách môn học thành công."
}
```

#### 2. Cấp 2 — Trưởng Bộ Môn phân công nhiều Giảng viên vào môn học (`POST /api/v1/department-head/courses/{id}/lecturers`)
- **Endpoint:** `POST /api/v1/department-head/courses/{id}/lecturers`
- **Quyền:** `department_head`, `admin`
- **Mô tả:** Trưởng Bộ Môn phân công một hoặc nhiều Giảng viên (`lecturer`) vào giảng dạy và biên soạn câu hỏi cho môn học thông qua bảng liên kết chuẩn 3NF `course_lecturers`.
- **Request Body:**
```json
{
  "lecturerUserIds": [
    "e4c5b2a1-0001-4000-8000-000000000002",
    "e4c5b2a1-0001-4000-8000-000000000003"
  ]
}
```
- **Response 200 OK:**
```json
{
  "courseId": "22222222-0000-0000-0000-000000000001",
  "assignedCount": 2,
  "lecturerUserIds": [
    "e4c5b2a1-0001-4000-8000-000000000002",
    "e4c5b2a1-0001-4000-8000-000000000003"
  ],
  "message": "Đã phân công danh sách Giảng viên vào môn học (bảng course_lecturers) thành công."
}
```

#### 3. Cấp 3 — Giảng viên tạo Lớp học phần & sinh/reset mật khẩu lớp (`POST /api/v1/lecturer/classes`, `POST /api/v1/lecturer/classes/{id}/reset-join-password`)
- **Endpoint tạo lớp:** `POST /api/v1/lecturer/classes`
- **Endpoint reset mật khẩu lớp:** `POST /api/v1/lecturer/classes/{id}/reset-join-password`
- **Quyền:** `lecturer`, `admin`
- **Mô tả:** Giảng viên được phân công trong `course_lecturers` tạo Lớp học phần thuộc môn học, hệ thống sinh mã lớp (`join_code`) và mật khẩu tham gia lớp (`join_password_hash`). Giảng viên có thể reset mật khẩu lớp bất kỳ lúc nào.
- **Request Body (`POST /api/v1/lecturer/classes`):**
```json
{
  "courseId": "22222222-0000-0000-0000-000000000001",
  "semesterId": "11111111-0000-0000-0000-000000000001",
  "classCode": "SE1801-PRN231",
  "maxStudents": 35,
  "joinPassword": "ClassSecret2026"
}
```
- **Response 201 Created:**
```json
{
  "classId": "33333333-0000-0000-0000-000000000001",
  "classCode": "SE1801-PRN231",
  "joinCode": "PRN231-SE1801",
  "courseId": "22222222-0000-0000-0000-000000000001",
  "lecturerId": "e4c5b2a1-0001-4000-8000-000000000002",
  "maxStudents": 35,
  "createdAt": "2026-10-02T08:00:00Z"
}
```

#### 4. Cấp 4 — Sinh viên nhập mật khẩu tự tham gia Lớp học phần (`POST /api/v1/student/classes/join`)
- **Endpoint:** `POST /api/v1/student/classes/join`
- **Quyền:** `student`, `admin`
- **Mô tả:** Sinh viên nhập mã lớp (`classCode` / `joinCode`) và mật khẩu lớp (`joinPassword`) do Giảng viên cung cấp để tự ghi danh vào bảng `class_enrollments`.
- **Request Body:**
```json
{
  "classCode": "SE1801-PRN231",
  "joinPassword": "ClassSecret2026"
}
```
- **Response 200 OK:**
```json
{
  "enrollmentId": "99991111-0000-0000-0000-000000000001",
  "classId": "33333333-0000-0000-0000-000000000001",
  "classCode": "SE1801-PRN231",
  "studentId": "e4c5b2a1-0001-4000-8000-000000000004",
  "enrolledAt": "2026-10-02T09:00:00Z",
  "message": "Tham gia lớp học phần thành công."
}
```

---

### 5.14. Ca Thi Trong Lớp & Mật Khẩu Tự Động Xoay Vòng 5 Phút (In-Class Formative Exam Shifts — R6 / MF-05)
*(Phân hệ CSDL: `in_class_exam_shifts`, `in_class_shift_passwords`, `in_class_exam_submissions`)*

#### 1. Giảng viên tạo Ca thi trong lớp (`POST /api/v1/lecturer/in-class-shifts`)
- **Endpoint:** `POST /api/v1/lecturer/in-class-shifts`
- **Quyền:** `lecturer`, `admin`
- **Mô tả:** Giảng viên tổ chức kiểm tra vấn đáp lấy điểm thành phần (Progress Test / Quiz) trực tiếp trên lớp học phần được phân công (`classes`).
- **Request Body:**
```json
{
  "classId": "33333333-0000-0000-0000-000000000001",
  "title": "Progress Test 1 - Clean Architecture & SOLID",
  "durationMinutes": 20,
  "questionCount": 3,
  "hasFollowUp": true,
  "passwordRotationSeconds": 300,
  "gracePeriodSeconds": 60
}
```
- **Response 201 Created:**
```json
{
  "shiftId": "77778888-0000-0000-0000-000000000001",
  "classId": "33333333-0000-0000-0000-000000000001",
  "title": "Progress Test 1 - Clean Architecture & SOLID",
  "status": "ACTIVE",
  "passwordRotationSeconds": 300,
  "gracePeriodSeconds": 60,
  "createdAt": "2026-10-15T08:00:00Z"
}
```

#### 2. Lấy mật khẩu TOTP 6 ký tự xoay vòng 5 phút + Grace Period 60s (`GET /api/v1/lecturer/in-class-shifts/{id}/active-password`)
- **Endpoint:** `GET /api/v1/lecturer/in-class-shifts/{id}/active-password`
- **Quyền:** `lecturer`, `admin`
- **Mô tả:** Hệ thống tự động sinh mật khẩu ca thi gồm 6 ký tự và xoay vòng định kỳ sau mỗi 5 phút (`300` giây) để Giảng viên chiếu lên màn hình lớp học, hỗ trợ khoảng gia hạn an toàn (`gracePeriodSeconds = 60` giây) cho mật khẩu liền kề trước đó nhằm chống trễ mạng và lệch đồng hồ client-server.
- **Response 200 OK:**
```json
{
  "shiftId": "77778888-0000-0000-0000-000000000001",
  "currentOtpCode": "A8F3K9",
  "generatedAt": "2026-10-15T08:05:00Z",
  "expiresAt": "2026-10-15T08:10:00Z",
  "gracePeriodSeconds": 60,
  "remainingSeconds": 245
}
```

#### 3. Sinh viên nhập mã OTP xoay vòng vào thi trong lớp (`POST /api/v1/student/in-class-shifts/{id}/join`)
- **Endpoint:** `POST /api/v1/student/in-class-shifts/{id}/join`
- **Quyền:** `student`, `admin`
- **Mô tả:** Sinh viên thuộc lớp học phần hiện diện tại lớp nhập mã OTP 6 ký tự hiện hành (hoặc mã liền trước còn trong 60s grace period) để vào làm bài vấn đáp. Không lưu audio lên R2, chỉ lưu transcript và đẩy vào `BoundedChannel` chấm tự động theo Rubric.
- **Request Body:**
```json
{
  "otpCode": "A8F3K9"
}
```
- **Response 200 OK:**
```json
{
  "submissionId": "88889999-0000-0000-0000-000000000001",
  "shiftId": "77778888-0000-0000-0000-000000000001",
  "status": "IN_PROGRESS",
  "joinedAt": "2026-10-15T08:06:12Z",
  "questions": [
    {
      "questionId": "55555555-0000-0000-0000-000000000001",
      "title": "Phân tích nguyên lý DIP trong Clean Architecture",
      "content": "Hãy giải thích nguyên lý Dependency Inversion và cách áp dụng trong .NET 8."
    }
  ]
}
```

#### 4. Giảng viên hậu kiểm & điều chỉnh điểm thành phần (`PUT /api/v1/lecturer/in-class-shifts/{id}/submissions/{subId}/grade`)
- **Endpoint:** `PUT /api/v1/lecturer/in-class-shifts/{id}/submissions/{subId}/grade`
- **Quyền:** `lecturer`, `admin`
- **Mô tả:** Giảng viên xem lại bài làm, transcript và điểm AI gợi ý của sinh viên trong lớp, điều chỉnh điểm thành phần kèm lý do giải trình trước khi chốt bảng điểm.
- **Request Body:**
```json
{
  "adjustedScore": 8.50,
  "overrideReason": "Sinh viên trả lời đúng ý chính về IoC Container trên lớp, cộng thêm 0.5 điểm."
}
```
- **Response 200 OK:**
```json
{
  "submissionId": "88889999-0000-0000-0000-000000000001",
  "finalScore": 8.50,
  "updatedAt": "2026-10-15T09:00:00Z",
  "message": "Đã cập nhật điểm thành phần thành công."
}
```

#### 5. Giảng viên chốt điểm thành phần One-Way Lock (`POST /api/v1/lecturer/in-class-shifts/{id}/finalize-grades`)
- **Endpoint:** `POST /api/v1/lecturer/in-class-shifts/{id}/finalize-grades`
- **Quyền:** `lecturer`, `admin`
- **Mô tả:** Giảng viên chốt bảng điểm thành phần của ca thi trong lớp, kích hoạt One-Way Lock (`is_locked = true`) bảo toàn điểm quá trình.
- **Response 200 OK:**
```json
{
  "shiftId": "77778888-0000-0000-0000-000000000001",
  "status": "FINALIZED",
  "isLocked": true,
  "finalizedAt": "2026-10-15T09:15:00Z",
  "message": "Đã chốt bảng điểm ca thi trong lớp và kích hoạt One-Way Lock thành công."
}
```

---

### 5.15. Phân Tách 2 Luồng Tạo Câu Hỏi (R8) & Dịch Vụ Giọng Nói AI Đa Tầng (Speech STT / TTS)

#### 0. Quy tắc Phân tách Độc lập 2 Luồng Tạo Câu hỏi (R8)
- **Luồng 1 — Luyện tập & Thi thử (MF-01 / MF-02):** Tạo độc lập từ Chuẩn đầu ra CLO (`course_clos` 3NF) hoặc Giảng viên soạn tay, lưu trực tiếp vào bảng `practice_questions` (`POST /api/v1/practice/questions` hoặc `POST /api/v1/questions/draft` với `usageScope: "practice"`). Phục vụ sinh viên tự do ôn luyện và thi thử, không đòi hỏi bám ma trận cấu trúc đề thi phòng Lab.
- **Luồng 2 — Thi thật Phòng Lab (MF-04):** Bắt buộc bám sát Đề thi mẫu / Ma trận Cấu trúc Đề (`exam_structures`, `slot_definitions`, tỷ lệ Bloom) do Trưởng Bộ Môn ban hành, lưu vào bảng bảo mật `exam_questions` (`POST /api/v1/official-exams/questions` hoặc `POST /api/v1/questions/draft` với `usageScope: "exam"`), gửi Trưởng Bộ Môn duyệt (`APPROVED`) trước khi đóng gói vào `exam_sets` (`OFFICIAL` $\to$ `HANDED_OVER`).

#### 1. Danh sách giọng đọc AI Giám khảo (`GET /api/v1/speech/voices`)
- **Quyền:** Public (`AllowAnonymous`)
- **Response 200 OK:** Danh sách 2 giọng chuẩn tiếng Việt (`vi-VN-HoaiMyNeural` - Nữ, `vi-VN-NamMinhNeural` - Nam).

#### 2. Tổng hợp giọng đọc câu hỏi giám khảo — Text-to-Speech (`POST /api/v1/speech/tts` & `GET /api/v1/speech/tts`)
- **Quyền:** Public (`AllowAnonymous`)
- **Request Body (`POST`):** `{ "text": "...", "voiceName": "vi-VN-HoaiMyNeural", "speakingRate": 1.0 }`
- **Response 200 OK:** Stream âm thanh `audio/mpeg` kèm các Headers chẩn đoán (`X-TTS-Provider`, `X-TTS-Voice`, `X-TTS-Fallback`, `X-TTS-Cache`). Tự động chuẩn hóa phát âm từ viết tắt Kỹ thuật Phần mềm (`SeTerminologyNormalizer.PrepareTextForTts`) và chuyển đổi dự phòng 3 tầng (Edge Neural TTS $\to$ Google Translate TTS $\to$ Offline WAV Synth).

#### 3. Bóc băng giọng nói sinh viên — Speech-to-Text (`POST /api/v1/speech/stt`)
- **Quyền:** Public (`AllowAnonymous`), `multipart/form-data` (`file`, `vocabularyHint`, `clientTranscript`)
- **Response 200 OK:** Trả về `transcript` đã khử ảo giác âm thanh câm (Whisper `no_speech_prob` / `avg_logprob`), chuẩn hóa thuật ngữ SE (Code-Switching Việt - Anh) và tự động dự phòng sang Gemini Multimodal STT khi cần.

#### 4. Bảng Ánh Xạ Nhanh Các Endpoint Thực Thi Trên Backend (Cập nhật đồng bộ cho Frontend)
| Chức năng | Phương thức & Đường dẫn chính xác trên Backend | Ghi chú tích hợp FE |
| :--- | :--- | :--- |
| **Tải lên ảnh/sơ đồ câu hỏi (MF-03)** | `POST /api/v1/questions/upload-image`<br>`POST /api/v1/storage/upload-question-image` | `multipart/form-data` (`file`, `description`); kiểm tra Magic Bytes (`PNG`, `JPG/JPEG`, `WEBP` $\le 5\text{MB}$), trả `413` / `415` nếu vi phạm |
| **Tạo câu hỏi nháp 2 kho (MF-03)** | `POST /api/v1/questions/draft` | Hỗ trợ `usageScope: "practice" \| "exam" \| "both"`, đính kèm `imageUrl` & `imageDescription` cho câu hỏi thủ công (`source == "manual"`) |
| **Gửi duyệt câu hỏi (MF-03)** | `POST /api/v1/questions/batch-submit-review` | Chuyển trạng thái sang `SUBMITTED_FOR_REVIEW` |
| **Trưởng BM duyệt đề (MF-03)** | `POST /api/v1/questions/{id}/review-decision` | Hỗ trợ `targetUsageScope: "both"`, trả `403 Forbidden` nếu người gọi không phải `department_head` / `admin` |
| **Tạo kỳ thi & mở ca thi (MF-04)** | `POST /api/v1/official-exams/sessions`<br>`POST /api/v1/official-exams/shifts` | Hỗ trợ đồng thời cả `/api/v1/official-exams/*` và `/api/v1/officialexams/*` |
| **Gán ghế 1–40 & IP máy trạm** | `POST /api/v1/official-exams/tickets` | Ràng buộc `seatNumber` (1–40) và `ipAddress` (`ip_address`) |
| **Kiosk lấy vé & Check-in (MF-04)** | `GET /api/v1/official-exams/tickets/current`<br>`POST /api/v1/official-exams/tickets/{ticketId}/check-in` | Tự động đọc IP kết nối (hỗ trợ `X-Forwarded-For`), chuyển vé sang `IN_PROGRESS` |
| **Kiosk cấp quyền R2 & Niêm phong SHA-256** | `POST /api/v1/official-exams/tickets/{ticketId}/audio-upload-authorizations`<br>`PUT /api/v1/official-exams/tickets/{ticketId}/audio` | Đặt tên file bất biến `STT_MSSV.webm` và niêm phong mã băm SHA-256 |
| **Kiosk nộp bài Persist First (< 100ms)** | `PUT /api/v1/official-exams/tickets/{ticketId}/submission`<br>`GET /api/v1/official-exams/tickets/{ticketId}/submission` | Trả `202 Accepted` (`SUBMITTED`), đẩy hàng đợi chấm ngầm `BoundedOfficialExamGradingQueue` |
| **Giảng viên xem Evidence Panel (MF-04)** | `GET /api/v1/official-exams/shifts/{shiftId}/audit-evidence` | Alias tương thích: `/api/v1/official-exams/shifts/{shiftId}/audit-submissions` |
| **Công bố điểm & Khóa 1 chiều (MF-04)** | `POST /api/v1/official-exams/shifts/{shiftId}/publish-grades` | Kích hoạt `is_locked = true` và `OneWayLockInterceptor` (`HTTP 403`) |
| **Xuất Báo cáo Excel / PDF & QR Verify** | `GET /api/v1/official-exams/shifts/{shiftId}/export-excel`<br>`GET /api/v1/official-exams/shifts/{shiftId}/export-pdf`<br>`GET /api/v1/verification/grades/{certificateHash}` | Kèm Header `X-Certificate-Hash` và tra cứu công khai qua QR |
| **Phúc khảo nội bộ 5 bước (MF-04 - R7)** | `POST /api/v1/student/appeals`<br>`POST /api/v1/examination-officer/appeals/{id}/forward-to-head`<br>`POST /api/v1/department-head/appeals/{id}/assign-lecturer`<br>`PUT /api/v1/lecturer/appeals/{id}/regrade`<br>`POST /api/v1/department-head/appeals/{id}/decision` | Luồng 5 bước: SV nộp $\to$ Khảo thí thẩm tra chuyển giao $\to$ Trưởng BM phân công GV $\to$ GV chấm lại $\to$ Trưởng BM phê duyệt quyết định |
| **DLQ Replay phân tách (R2)** | `/api/v1/admin/dlq/*` (MF-01/02)<br>`/api/v1/examination-officer/dlq/*` (MF-04) | Admin quản lý DLQ MF-01/02; Cán bộ Khảo thí quản lý DLQ MF-04; cắt bỏ Trưởng BM khỏi DLQ |
| **Giám thị Phòng Lab (R4)** | `POST /api/v1/proctor/shifts/{id}/attendance`<br>`POST /api/v1/proctor/shifts/{id}/violations` | Chỉ điểm danh ghế 1–40 & báo cáo vi phạm vào `exam_anti_cheat_logs` |
| **Chuỗi phân cấp học thuật 4 cấp (R5)** | `PUT /api/v1/admin/courses/{id}/assign-department-head`<br>`POST /api/v1/department-head/courses/{id}/lecturers`<br>`POST /api/v1/lecturer/classes`<br>`POST /api/v1/lecturer/classes/{id}/reset-join-password`<br>`POST /api/v1/student/classes/join` | Admin gán Trưởng BM $\to$ Trưởng BM phân công nhiều GV (`course_lecturers`) $\to$ GV tạo lớp & mật khẩu $\to$ SV tự tham gia lớp |
| **Ca thi trong lớp & TOTP 5m (R6 - MF-05)** | `POST /api/v1/lecturer/in-class-shifts`<br>`GET /api/v1/lecturer/in-class-shifts/{id}/active-password`<br>`POST /api/v1/student/in-class-shifts/{id}/join`<br>`PUT /api/v1/lecturer/in-class-shifts/{id}/submissions/{subId}/grade`<br>`POST /api/v1/lecturer/in-class-shifts/{id}/finalize-grades` | Mật khẩu 6 ký tự xoay vòng 5 phút + grace period 60s, hậu kiểm điểm thành phần & chốt One-Way Lock |
| **Student Dashboard & Xác nhận điểm** | `GET /api/v1/student/dashboard`<br>`POST /api/v1/student/official-exams/{ticketId}/acknowledge-grade` | Đồng bộ hạn ngạch thi thử theo múi giờ Việt Nam (GMT+7) |

---
---

## 6. BẢNG TỔNG HỢP MASTER API MATRIX & HƯỚNG DẪN KẾT NỐI FRONTEND (REACT 19 VITE)

### 6.1. Cấu hình Kết nối Chuẩn Cho Frontend (`.env` & Axios / SignalR)

```env
# File .env tại thư mục frontend/
VITE_API_BASE_URL=http://localhost:5000/api/v1
VITE_BACKEND_ORIGIN=http://localhost:5000
VITE_GOOGLE_CLIENT_ID=<GOOGLE_OAUTH_CLIENT_ID_CUA_TRUONG>
```

#### Quy tắc Kết nối HTTP (Axios) & WebSocket (SignalR):
1. **REST API (`Axios`):**
   - Base URL: `${import.meta.env.VITE_API_BASE_URL}` (`http://localhost:5000/api/v1`).
   - Đính kèm header `Authorization: Bearer ${accessToken}` trên mọi request sau khi đăng nhập.
   - Bật `withCredentials: true` nếu cần gửi cookie, Backend đã cấu hình CORS `"AllowFrontend"` cho phép `AllowCredentials()` và expose đầy đủ các headers âm thanh (`X-STT-Confidence`, `X-STT-Provider`, `X-TTS-Provider`, `X-TTS-Voice`, `X-TTS-Rate`, `X-TTS-Fallback`, `X-TTS-Cache`).
   - Lưu ý Middleware `MustChangePasswordMiddleware`: Nếu tài khoản trường cấp có `mustChangePassword == true`, mọi API ngoài `/api/v1/auth/change-password`, `/api/v1/auth/password-login`, `/api/v1/auth/google-login`, `/api/v1/auth/refresh-token`, `/api/v1/auth/sign-out` sẽ trả `HTTP 403 Forbidden` yêu cầu đổi mật khẩu lần đầu.
2. **Realtime SignalR Hubs (`@microsoft/signalr`):**
   - Backend cung cấp **2 SignalR Hubs** đều hỗ trợ xác thực JWT qua `accessTokenFactory` (tự động truyền `?access_token=<JWT>` khi nâng cấp WebSocket):
     - **Hub 1 — Chấm điểm Luyện tập MF-01:** `http://localhost:5000/hubs/practice`
       - Метоds Client gọi lên Server: `JoinSession(sessionId: string)`, `LeaveSession(sessionId: string)`
       - Events Server bắn về Client: `ReceiveGradingResult(payload: PracticeAnswerResultDto)`, `ReceiveGradingError(answerId: string, error: string)`
     - **Hub 2 — Giám sát Gian lận Phòng Lab MF-04:** `http://localhost:5000/hubs/anti-cheat`
       - Methods Client gọi lên Server: `JoinShift(shiftId: string)`, `LeaveShift(shiftId: string)`
       - Events Server bắn về Client: `ReceiveAntiCheatAlert(alert: ExamAntiCheatLogDto)`

---

### 6.2. Đặc tả API Dashboard Sinh Viên & Giảng Viên (FE-10)

#### 1. Bảng điều khiển Sinh viên (`GET /api/v1/student/dashboard`)
- **Endpoint:** `GET /api/v1/student/dashboard` (Tùy chọn cho GV/Admin xem hộ: `?studentId={guid}`)
- **Quyền:** `student`, `lecturer`, `department_head`, `admin`
- **Response 200 OK (`StudentDashboardDto`):**
  - Tổng hợp số liệu luyện tập MF-01 (`totalPracticeSessions`, `completedPracticeSessions`, `averagePracticeScore`, `recentPracticeSessions`), thi thử MF-02 (`totalMockExams`, `averageMockExamScore`, `recentMockExams`), hạn ngạch thi thử hôm nay theo từng môn (`courseQuotas`), độ thông thạo tiêu chí CLO/Rubric (`criterionMasteries`), và danh sách vé thi thật phòng Lab sắp tới / đã công bố điểm (`upcomingAndRecentOfficialExams`).

#### 2. Bảng điều khiển Giảng viên & Trưởng Bộ Môn (`GET /api/v1/lecturer/dashboard`)
- **Endpoint:** `GET /api/v1/lecturer/dashboard` (Alias: `GET /api/v1/dashboard/lecturer`, hỗ trợ lọc `?courseId={guid}`)
- **Quyền:** `lecturer`, `department_head`, `admin`
- **Response 200 OK (`LecturerDashboardDto`):**
  - Trả về thống kê nhanh (`pendingQuestionsCount`, `suspiciousTicketsCount`, `assignedAppealsCount`, `questionsNeedingRevisionCount`), danh sách ca thi được phân công hậu kiểm (`assignedShifts`), danh sách đơn phúc khảo được giao chấm lại (`assignedAppeals`), và danh sách câu hỏi cần chỉnh sửa (`questionsNeedingRevision` — bao gồm cả câu hỏi thuộc kho `exam_questions` lẫn kho `practice_questions` có trạng thái `NEEDS_REVISION`).

---

### 6.3. Bảng Tra Cứu Toàn Bộ 23 Controllers & Endpoints Trên Backend

| # | Controller | HTTP Method & Route Chuẩn | Roles Được Phép | Chức Năng Nghiệp Vụ |
| :-: | :--- | :--- | :--- | :--- |
| 1 | `AuthController` | `POST /api/v1/auth/google-login`<br>`POST /api/v1/auth/password-login`<br>`POST /api/v1/auth/change-password`<br>`POST /api/v1/auth/refresh-token`<br>`POST /api/v1/auth/sign-out`<br>`GET /api/v1/auth/me` | Public / Authenticated | Đăng nhập kép (Google OAuth 2.0 & Tài khoản trường cấp), đổi mật khẩu lần đầu, làm mới JWT token, đăng xuất và lấy thông tin user hiện tại |
| 2 | `SemestersController` | `GET /api/v1/semesters` (`/api/v1/academic/semesters`)<br>`POST /api/v1/semesters`<br>`PUT /api/v1/semesters/{id}` | All Roles (GET)<br>`admin` (POST/PUT) | Danh sách học kỳ, tạo mới và cập nhật học kỳ (FE-08, không xóa) |
| 3 | `CoursesController` | `GET /api/v1/courses` (`/api/v1/academic/courses`)<br>`POST /api/v1/courses` (`/api/v1/admin/courses`)<br>`PUT /api/v1/courses/{id}`<br>`GET /api/v1/courses/{id}/configurations`<br>`PUT /api/v1/courses/{id}/configurations`<br>`GET /api/v1/courses/{id}/clos`<br>`POST /api/v1/courses/{id}/clos`<br>`PUT /api/v1/courses/{id}/clos/{cloId}`<br>`DELETE /api/v1/courses/{id}/clos/{cloId}`<br>`POST /api/v1/admin/courses/{id}/assign-department-head`<br>`POST /api/v1/department-head/courses/{id}/lecturers`<br>`GET /api/v1/courses/{id}/lecturers` | Authenticated (GET)<br>`admin` (Tạo/Sửa môn, Gán TBM, Quota)<br>`department_head,admin` (Cấu hình môn, CLOs 3NF, Phân công GV) | Quản lý môn học, cấu hình động môn học (Buffer, Follow-up, Cấu hình thi thử MF-02), quản lý chuẩn đầu ra `course_clos` 3NF và phân công giảng viên `course_lecturers` (R5) |
| 4 | `ClassesController` | `POST /api/v1/lecturer/classes`<br>`PUT /api/v1/lecturer/classes/{id}/password`<br>`POST /api/v1/lecturer/classes/{id}/reset-password`<br>`POST /api/v1/student/classes/join`<br>`GET /api/v1/student/classes/my-classes` | `lecturer,department_head,admin` (Tạo lớp/Reset pass)<br>`student,admin` (Tham gia/Xem lớp) | Tạo lớp học phần, sinh/đổi mật khẩu lớp (`join_code`, `join_password_hash`) và sinh viên tự ghi danh vào lớp (R5) |
| 5 | `RubricsController` | `GET /api/v1/rubrics?courseId={id}`<br>`GET /api/v1/rubrics/{id}`<br>`POST /api/v1/rubrics`<br>`PUT /api/v1/rubrics/{id}`<br>`DELETE /api/v1/rubrics/{id}` | `lecturer,department_head,admin` | Quản lý Barem Rubric chuẩn hóa $\sum = 10.00$đ và các tiêu chí con theo thang Bloom (MF-03) |
| 6 | `QuestionsController` | `POST /api/v1/questions/upload-image`<br>`POST /api/v1/questions/generate-from-clo`<br>`POST /api/v1/questions` (`/draft`)<br>`POST /api/v1/practice/questions`<br>`POST /api/v1/official-exams/questions`<br>`POST /api/v1/questions/batch-submit-review`<br>`POST /api/v1/questions/{id}/review-decision`<br>`POST /api/v1/questions/batch-approve`<br>`GET /api/v1/questions`<br>`GET /api/v1/questions/{id}` | `lecturer,department_head,admin` (Riêng `review-decision`, `batch-approve`, `official-exams/questions`: `department_head,admin`) | Tải ảnh minh họa ($\le 5	ext{MB}$ Magic Bytes), AI sinh câu hỏi từ CLO, tạo câu hỏi 2 kho độc lập (`practice` / `exam` / `both`), gửi duyệt và Trưởng BM phê duyệt (MF-03 / R8) |
| 7 | `ExamStructuresController` | `GET /api/v1/exam-structures`<br>`GET /api/v1/exam-structures/{id}`<br>`POST /api/v1/exam-structures`<br>`PUT /api/v1/exam-structures/{id}` | Authenticated (GET)<br>`department_head,admin` (POST/PUT) | Quản lý ma trận cấu trúc đề thi mẫu (`exam_structures`, `slot_definitions`, phân bổ Bloom) |
| 8 | `PracticeController` | `POST /api/v1/practice/sessions`<br>`GET /api/v1/practice/sessions/{id}`<br>`POST /api/v1/practice/sessions/{id}/next-question`<br>`POST /api/v1/practice/sessions/{id}/answers`<br>`POST /api/v1/practice/sessions/{id}/batch-answers` (`/batch-submit`)<br>`POST /api/v1/practice/sessions/{id}/complete`<br>`GET /api/v1/practice/student/history` | `student,lecturer,department_head,admin` | Luồng Luyện tập tương tác tự do MF-01: Per-Question On-Demand (Anti-3-Consecutive, Follow-up khi điểm 4.0–8.0) & Full-Session Progressive (3–10 câu), Timeout 5 phút (`410 Gone`) |
| 9 | `MockExamsController` | `GET /api/v1/mock-exams/quota`<br>`POST /api/v1/mock-exams/sessions` (`/start`)<br>`GET /api/v1/mock-exams/sessions/{id}`<br>`POST /api/v1/mock-exams/sessions/{id}/answers`<br>`POST /api/v1/mock-exams/sessions/{id}/submit`<br>`GET /api/v1/mock-exams/history`<br>`GET /api/v1/mock-exams/history/{sessionId}` | `student,lecturer,department_head,admin` | Luồng Thi thử bấm giờ có hạn ngạch MF-02: Kiểm tra hạn ngạch ngày (`429`), bốc đề từ `practice_questions`, trả lời Voice-First (Zero R2), chấm tức thì trả Scorecard |
| 10 | `OfficialExamsController` | `GET /api/v1/official-exams/sessions`<br>`POST /api/v1/official-exams/sessions`<br>`PUT /api/v1/official-exams/sessions/{id}/follow-up-config`<br>`POST /api/v1/official-exams/shifts`<br>`POST /api/v1/official-exams/shifts/{id}/publish-exam`<br>`POST /api/v1/official-exams/tickets`<br>`GET /api/v1/official-exams/shifts/{shiftId}/audit-evidence`<br>`PUT /api/v1/official-exams/tickets/{ticketId}/audit-score`<br>`POST /api/v1/official-exams/shifts/{shiftId}/publish-grades`<br>`GET /api/v1/official-exams/shifts/{shiftId}/export-excel`<br>`GET /api/v1/official-exams/shifts/{shiftId}/export-pdf`<br>`POST /api/v1/official-exams/anti-cheat/log` | `examination_officer,admin` (Kỳ thi/Ca thi/Mở đề/Publish điểm)<br>`department_head,admin` (Cấu hình Follow-up thi thật)<br>`lecturer,department_head,admin` (Evidence Panel & Sửa điểm) | Quản lý kỳ thi, ca thi phòng Lab, phát đề không trùng, hậu kiểm Evidence Panel, công bố điểm kích hoạt `OneWayLockInterceptor` (`403`) và xuất Excel/PDF ký số (MF-04) |
| 11 | `OfficialExamCheckInController` | `GET /api/v1/official-exams/tickets/current`<br>`POST /api/v1/official-exams/tickets/{ticketId}/check-in` | `student,proctor,examination_officer,admin` | Máy trạm Kiosk lấy vé thi hiện tại và Check-in khóa địa chỉ `ip_address` máy trạm (MF-04) |
| 12 | `OfficialExamSubmissionsController` | `POST /api/v1/official-exams/tickets/{ticketId}/audio-upload-authorizations`<br>`PUT /api/v1/official-exams/tickets/{ticketId}/audio`<br>`PUT /api/v1/official-exams/tickets/{ticketId}/submission`<br>`GET /api/v1/official-exams/tickets/{ticketId}/submission` | `student,proctor,lecturer,department_head,examination_officer,admin` | Cấp Presigned URL tải audio `STT_MSSV.webm` lên Cloudflare R2, niêm phong SHA-256 và nộp bài Persist-First $< 100$ms (MF-04) |
| 13 | `ProctorController` | `GET /api/v1/proctor/shifts/{id}/attendance`<br>`POST /api/v1/proctor/shifts/{id}/attendance`<br>`POST /api/v1/proctor/shifts/{id}/violations` | `proctor,admin` | Giám thị phòng Lab điểm danh ghế 1–40 đối chiếu thẻ SV/CCCD và ghi biên bản vi phạm vào `exam_anti_cheat_logs` (R4) |
| 14 | `AppealsController` | `POST /api/v1/student/appeals` (`/api/v1/appeals`)<br>`GET /api/v1/appeals`<br>`POST /api/v1/examination-officer/appeals/{id}/forward-to-head`<br>`POST /api/v1/department-head/appeals/{id}/assign-lecturer`<br>`PUT /api/v1/lecturer/appeals/{id}/regrade`<br>`POST /api/v1/department-head/appeals/{id}/decision` | `student` (Bước 1)<br>`examination_officer` (Bước 2)<br>`department_head` (Bước 3 & 5)<br>`lecturer` (Bước 4)<br>`admin` (All) | Quy trình Phúc khảo nội bộ 5 bước chuẩn mực (R7), mở ngoại lệ có kiểm soát cho `OneWayLockInterceptor` khi Trưởng BM phê duyệt `APPROVED` |
| 15 | `DeadLetterQueueController` | `GET /api/v1/admin/dlq/tasks`<br>`POST /api/v1/admin/dlq/tasks/{id}/replay`<br>`GET /api/v1/examination-officer/dlq/tasks`<br>`POST /api/v1/examination-officer/dlq/tasks/{id}/replay` | `admin` (DLQ MF-01/02)<br>`examination_officer,admin` (DLQ MF-04) | Giám sát và kích hoạt chấm bù (Replay) hàng đợi lỗi DLQ phân tách độc lập theo luồng nghiệp vụ (R2) |
| 16 | `AdminController` | `GET /api/v1/admin/users`<br>`PUT /api/v1/admin/users/{id}/status`<br>`PUT /api/v1/admin/users/{id}/role`<br>`GET /api/v1/admin/system-configs`<br>`PUT /api/v1/admin/system-configs/{key}`<br>`GET /api/v1/admin/audit-logs` | `admin` | Quản trị người dùng (FE-09: khóa/mở, gán role 6 vai trò), cấu hình tham số hệ thống `system_configs` và tra cứu nhật ký kiểm toán `audit_logs` |
| 17 | `StudentController` | `GET /api/v1/student/dashboard`<br>`GET /api/v1/student/official-exams/{ticketId}/grade`<br>`POST /api/v1/student/official-exams/{ticketId}/acknowledge-grade` | `student,admin` (Dashboard & Grade hỗ trợ GV/TBM xem) | Bảng điều khiển học tập sinh viên (FE-10), xem bảng điểm thi thật sau công bố và xác nhận điểm `ACKNOWLEDGED` |
| 18 | `LecturerController` | `GET /api/v1/lecturer/dashboard` (`/api/v1/dashboard/lecturer`) | `lecturer,department_head,admin` | Bảng điều khiển giảng viên & Trưởng Bộ Môn: tổng hợp câu hỏi chờ duyệt, câu hỏi cần sửa (`NEEDS_REVISION` cả 2 kho), ca thi hậu kiểm và đơn phúc khảo |
| 19 | `NotificationsController` | `GET /api/v1/notifications`<br>`PUT /api/v1/notifications/{id}/read`<br>`PUT /api/v1/notifications/read-all` | Authenticated (All 6 Roles) | Hộp thư thông báo chuông trong ứng dụng (FE-11) cho 5 sự kiện nghiệp vụ |
| 20 | `SpeechController` | `GET /api/v1/speech/voices`<br>`POST /api/v1/speech/tts` (`GET /api/v1/speech/tts`)<br>`POST /api/v1/speech/stt` | Public (`AllowAnonymous`) | Tổng hợp giọng đọc Giám khảo AI (TTS 3 tầng dự phòng) và nhận diện giọng nói sinh viên (Resilient STT Gemini + Cloudflare Whisper) |
| 21 | `StorageController` | `POST /api/v1/storage/upload-audio`<br>`POST /api/v1/storage/upload-question-image` | `student,lecturer,department_head,admin` | Stream âm thanh bóc băng nhanh cho màn hình đệm MF-01 và alias tải ảnh câu hỏi MF-03 |
| 22 | `FlmController` | `GET /api/v1/courses/{courseId}/flm` | `lecturer,department_head,examination_officer,admin` | Alias tương thích ngược tra cứu danh mục CLO môn học |
| 23 | `VerificationController` | `GET /api/v1/verification/grades/{certificateHash}` | Public (`AllowAnonymous`) | Xác thực công khai chữ ký số SHA-256 trên báo cáo bảng điểm PDF qua mã QR |

---
### 6.4. Mẫu Code Tích Hợp Frontend React 19 (Dual Auth, MF-01 SignalR Hub & MF-02 Voice-First)

#### 1. Đăng nhập Kép (Dual Auth: Google OAuth 2.0 & Tài khoản Trường cấp)
- **Biến môi trường `.env`:** `VITE_GOOGLE_CLIENT_ID=<google-oauth-client-id>.apps.googleusercontent.com`
- **Luồng Google Login:** Dùng `@react-oauth/google` lấy `credentialResponse.credential` (`idToken`) $\to$ gọi `POST /api/v1/auth/google-login` với `{ idToken }`.
- **Luồng Tài khoản Trường cấp (`username`/`password`):** Gọi `POST /api/v1/auth/password-login` với `{ username, password }`. Nếu response trả về `mustChangePassword: true`, điều hướng bắt buộc sang màn hình `POST /api/v1/auth/change-password` trước khi vào hệ thống.
- **Silent Refresh Token Rotation:** Khi Axios nhận `HTTP 401 Unauthorized`, tự động gọi `POST /api/v1/auth/refresh-token` với `{ refreshToken }` để lấy cặp `accessToken` + `refreshToken` mới.

#### 2. Hook SignalR `usePracticeHub.ts` & Màn Hình Đệm 60s (MF-01)
- Kết nối tới `${import.meta.env.VITE_HUB_BASE_URL}/hubs/practice` kèm `accessTokenFactory: () => localStorage.getItem('access_token') ?? ''`.
- Sau khi `connection.start()`, gọi `await connection.invoke('JoinSession', sessionId)`.
- Lắng nghe `connection.on('ReceiveGradingResult', (result: PracticeAnswerResultDto) => { ... })`:
  - Nếu `result.needsFollowUp === true` và `result.followUpPrompt`: Hiển thị thẻ câu hỏi phụ (Follow-up A2) ngay trong phiên.
  - Nếu `result.needsFollowUp === false`: Mở modal Scorecard hiển thị điểm tổng và chi tiết từng tiêu chí Rubric.
- **Xử lý `HTTP 410 Gone` (Inactivity Timeout 5 phút):** Khi API trả về `status === 410`, hiển thị thông báo phiên đã tự động hoàn tất do quá 5 phút không tương tác và mở bảng tổng kết điểm các câu đã làm.

#### 3. Thu Âm Voice-First Gate & Bóc Băng STT (MF-02)
- Trong MF-02 (Thi thử bấm giờ), **khóa cứng ô nhập văn bản thủ công** khi chưa thu âm (`hasRecordedAudio === false`).
- Sử dụng `MediaRecorder` (`audio/webm`) thu âm giọng nói sinh viên $\to$ gửi `FormData` lên `POST /api/v1/speech/stt` (hoặc `POST /api/v1/storage/upload-audio`) để nhận `transcript` $\to$ mở màn hình đệm đếm ngược `transcriptBufferSeconds` (10–300s, mặc định 60s) cho phép sinh viên sửa thuật ngữ chuyên ngành tiếng Anh (Code-Switching) $\to$ gửi `POST /api/v1/mock-exams/sessions/{sessionId}/answers` kèm `{ questionId, transcript, timeSpentSeconds }`.

---
*(Hợp đồng giao diện API Contract này là tài liệu pháp lý kỹ thuật bất biến giữa Frontend và Backend, bảo đảm tính toàn vẹn 100% khi tích hợp hệ thống).*

