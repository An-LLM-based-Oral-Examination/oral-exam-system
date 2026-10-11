# QUY ƯỚC GIAO TIẾP VÀ TÍCH HỢP FE - BE (API CONTRACT & INTEGRATION GUIDE v4.0)
**Dự án:** An LLM-based Oral Examination (FA26SE166) — FPT University HCMC (FPT SG)  
**Tài liệu này quy định bắt buộc (Mandatory) các chuẩn mực kết nối giữa Frontend (React 19 / Vite) và Backend (.NET 8 Clean Architecture) cho 4 thành viên:**  
- 🧑 **Nguyễn Quang Thành:** Team Leader & Lead BE Architect (MediatR CQRS, System.Threading.Channels, One-Way Lock, System Integration).  
- 🧑 **Nguyễn Trọng Tốt:** BE Developer, AI Integration & QA Lead (Gemini 1.5 Flash/Pro CoT, Whisper STT, Barem Rubric 10.0, Unit & Architecture Tests).  
- 🧑 **Nguyễn Đăng Hải:** DB Specialist & Frontend Developer (phụ trách Database 30 bảng PostgreSQL và dồn toàn lực phát triển Frontend; tuyệt đối không code C# Backend).  
- 🧑 **Lê Vũ Hoàng:** Lead Frontend Architect & Fullstack Coordinator (React 19 Vite, Tailwind CSS v4, Web Speech API STT/TTS, Buffer Screen, Kiosk Lockdown, Waveform Player, Contract-First API Sync).  

**Quy chuẩn cốt lõi:**  
- **Tách biệt hoàn toàn:** Kho luyện tập mở (`practice_questions`, `practice_answers`) và kho thi cử bảo mật (`exam_questions`, `mock_exam_*`, `official_exam_*`).  
- **Buffer Screen:** Thời gian đệm cấu hình động 10–300 giây (mặc định 60s do Admin cấu hình chung qua `system_configs` cho MF-01) cho phép sinh viên nghe TTS, nói qua Mic, xem transcript Web Speech và chỉnh sửa trước khi nộp bài.  
- **Quota Guard:** Đếm trực tiếp từ PostgreSQL theo hạn ngạch môn học do Trưởng Bộ Môn cấu hình (`max_mock_exams_per_day`) cho thi thử (MF-02).  
- **Hàng đợi RAM:** `BoundedChannel` 1,000 slots trả `202 Accepted` trong $< 100$ms, kết quả trả qua WebSocket SignalR `/hubs/practice`.  
- **Thi thật phòng Lab (MF-04):** Bóc băng Whisper STT Server-side kèm timestamps, Cloudflare R2 lưu `STT_MSSV.webm`, băm SHA-256 niêm phong, và cơ chế Khóa điểm một chiều `OneWayLockInterceptor` (`HTTP 403`).

---

## 1. QUY ƯỚC CHUNG VỀ API (RESTFUL)
- **Base URL:** Tất cả REST API phải bắt đầu bằng tiền tố `/api/v1/`.
- **Định dạng dữ liệu:** Bắt buộc giao tiếp bằng `application/json` (UTF-8).
- **Quy tắc Naming Convention:**
  - URL luôn dùng chữ thường và dấu gạch ngang (kebab-case). Ví dụ: `/api/v1/official-exams/shifts` (KHÔNG DÙNG: `/api/v1/OfficialExams` hay `/api/v1/exam_sessions`).
  - Response JSON từ Backend trả về bắt buộc định dạng **camelCase** (ví dụ: `studentId`, `totalScore`, `roomLab`, `seatNumber`) để Frontend ánh xạ thẳng vào TypeScript Interface.
  - Phân quyền theo 5 vai trò (User Roles): `student`, `lecturer`, `department_head`, `proctor`, `admin`.

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
- `403 Forbidden`: Bị từ chối quyền truy cập (ví dụ: Sinh viên cố truy cập route quản trị, người dùng không có quyền gọi API sinh câu hỏi FLM [yêu cầu `lecturer` hoặc `department_head`], hoặc gửi request sửa bài thi khi đã bị Khóa điểm một chiều `is_locked = true`).
- `404 Not Found`: Không tìm thấy tài nguyên theo ID.
- `410 Gone`: Phiên luyện tập đã kết thúc tự động do quá thời gian không tương tác (`SessionInactivityTimeoutMinutes = 10` phút). Toàn bộ dữ liệu điểm số và câu trả lời đã làm trước đó được bảo toàn nguyên vẹn, sinh viên không thể tương tác thêm trong phiên này.
- `422 Unprocessable Entity`: Dữ liệu đúng cú pháp nhưng vi phạm quy tắc nghiệp vụ (ví dụ: Tổng điểm Rubric != 10.0, Model Answer < 50 ký tự, hoặc cập nhật điểm thẩm định mà để trống lý do giải trình).
- `429 Too Many Requests`: Vi phạm Quota Guard thi thử quá 3 lượt/môn/ngày (kiểm soát bởi PostgreSQL).
- `500 Internal Server Error`: Lỗi máy chủ chưa được xử lý.
- `502 Bad Gateway`: Lỗi kết nối hoặc phản hồi không hợp lệ từ dịch vụ upstream bên thứ 3 (FLM Adapter API, Cloudflare Whisper STT, Google Gemini AI) sau khi đã thực thi chính sách thử lại Polly Retry Policy.
- `504 Gateway Timeout`: Quá thời gian chờ phản hồi từ dịch vụ ngoại vi sau khi cạn kiệt chu trình Polly Retry (3 lần: 2s, 4s, 8s). Phân định rõ hai tầng timeout: Thời gian chờ mỗi lần gọi đơn lẻ (Attempt Timeout: 10s cho FLM API, 30s cho Gemini AI Generation) và Tổng thời gian chờ tối đa của toàn bộ Resilience Pipeline (Total Request Timeout: 60s cho FLM API, 90s cho Gemini AI Generation).

---

## 4. BẢO MẬT & XÁC THỰC (AUTHENTICATION & JWT CONTRACT)

### 4.1. Đăng nhập hệ thống (Google OAuth 2.0 PKCE)

#### Đăng nhập Google OAuth 2.0 PKCE (Quy chuẩn Toàn Hệ Thống)
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

#### Cơ chế Đăng nhập Xác thực Toàn Hệ Thống
- Hệ thống áp dụng chuẩn bảo mật **100% Google OAuth 2.0 PKCE** cho toàn bộ 5 vai trò (`student`, `lecturer`, `department_head`, `proctor`, `admin`), hỗ trợ mọi tài khoản email Google hợp lệ.
- **Tuyệt đối không sử dụng endpoint đăng nhập mật khẩu cục bộ (`POST /api/v1/auth/login`)**: Loại bỏ hoàn toàn nguy cơ lưu trữ hash mật khẩu và tấn công brute-force.
- Mọi phiên làm việc sau khi xác thực qua Google Callback nhận cặp `accessToken` (JWT 15 phút) và `refreshToken` (7 ngày).

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

### 4.2. Quy ước lưu trữ và gửi Token
- Frontend lưu `accessToken` vào Memory hoặc Session Storage (tuyệt đối không lưu LocalStorage nhằm phòng chống XSS).
- Mọi request yêu cầu xác thực phải đính kèm Header:
  `Authorization: Bearer <accessToken>`
- Khi gặp lỗi `401 Unauthorized`, Axios Interceptor tự động kích hoạt luồng Silent Refresh token để lấy token mới.

### 4.3. Ma trận Phân quyền (RBAC Matrix) & JWT Claims
Hệ thống xác thực người dùng dựa trên JWT Bearer token chứa claim `role`. Bảng ma trận phân quyền chi tiết cho 5 vai trò:

| Phân hệ / Endpoint API | `student` | `lecturer` | `department_head` | `proctor` | `admin` |
|:---|:---:|:---:|:---:|:---:|:---:|
| Quản lý Học thuật & Đề cương (`/api/v1/academic/*`) | ✅ (xem) | ✅ | ✅ | ✅ (xem) | ✅ |
| Luyện tập tự do MF-01 (`/api/v1/practice/*`) | ✅ | ✅ | ✅ | ❌ | ✅ |
| Thi thử bấm giờ MF-02 (`/api/v1/mock-exams/*`) | ✅ | ✅ | ✅ | ❌ | ✅ |
| Soạn câu hỏi & Barem thủ công (`/api/v1/rubrics`, `/api/v1/practice/questions`) | ❌ | ✅ | ✅ | ❌ | ✅ |
| Phê duyệt câu hỏi thi chính thức (`approved_by`) | ❌ | ❌ | ✅ | ❌ | ✅ |
| **Lấy đề cương FLM (`GET /api/v1/flm/courses/{id}/syllabus`)** | ❌ | **✅** | **✅** | ❌ | **✅** |
| **Giảng viên sử dụng AI sinh câu hỏi theo barem của mình (`POST /api/v1/questions/generate-from-flm`)** | ❌ | **✅** | **✅** | ❌ | **✅** |
| **Giảng viên gửi câu hỏi duyệt Bộ môn (`POST /api/v1/questions/batch-submit-review`)** | ❌ | **✅** | ❌ | ❌ | **✅** |
| **Trưởng Bộ Môn thẩm định & duyệt đề (`POST /api/v1/questions/{id}/review-decision`)** | ❌ | ❌ | **✅** | ❌ | **✅** |
| Giám sát phòng thi Lab MF-04 (`/api/v1/official-exams/shifts/*`) | ❌ | ✅ | ✅ | ✅ | ✅ |
| Thẩm định điểm & Công bố điểm (Publish Grades) MF-04 | ❌ | ✅ | ✅ | ❌ | ✅ |
| Sinh viên nộp đơn phúc khảo nội bộ (`POST /api/v1/appeals`) | ✅ | ❌ | ❌ | ❌ | ✅ |
| Tra cứu danh sách đơn phúc khảo (`GET /api/v1/appeals`) | ✅ (của mình) | ✅ (được giao) | ✅ | ❌ | ✅ |
| Trưởng Bộ Môn giao Giảng viên chấm lại (`PUT /api/v1/appeals/{id}/assign-lecturer`) | ❌ | ❌ | ✅ | ❌ | ✅ |
| Giảng viên chấm lại / Thẩm định phúc khảo (`PUT /api/v1/appeals/{id}/review`) | ❌ | ✅ | ✅ | ❌ | ✅ |
| Quản trị hệ thống, DLQ Replay & Audit Logs (`/api/v1/admin/*`) | ❌ | ❌ | ❌ | ❌ | ✅ |

*Ghi chú quan trọng:* Tính năng AI sinh câu hỏi từ FLM/Syllabus mở quyền sử dụng cho cả **Giảng viên (`lecturer`)** và **Trưởng Bộ Môn (`department_head`)**. Giảng viên được tự thiết kế Barem Rubric riêng 10.0đ, tinh chỉnh câu hỏi và sau đó ấn **"Gửi lên cho Bộ Môn"** (`SUBMITTED_FOR_REVIEW`) để Trưởng Bộ Môn thẩm định và phê duyệt chính thức vào ngân hàng đề. Tại MF-04, sau khi kết thúc ca thi, AI chấm điểm ngầm chuyển Giảng viên thẩm định các bài nghi ngờ qua Evidence Panel (AudioURL, Transcript Whisper gốc, AI CoT); khi 100% sinh viên có điểm, Giảng viên ấn **"Công Bố Điểm"** (`Publish Grades`); Sinh viên xem điểm trên Student Portal, nếu không đồng ý thì nộp đơn phúc khảo nội bộ (`POST /api/v1/appeals`) trực tiếp trên Student Portal để chuyển Trưởng Bộ Môn tiếp nhận và giao cho một Giảng viên chấm lại (`PUT /api/v1/appeals/{id}/assign-lecturer`).

---

## 5. DANH MỤC API CHI TIẾT THEO TÍNH NĂNG & PHÂN HỆ

#### Bảng Mục Lục API Nhanh (Quick Navigation Index)
| STT | Phân hệ API | Tiền tố Endpoint | Vai trò chính |
|:---:|:---|:---|:---|
| 5.1 | Quản lý Học thuật | `/api/v1/academic/*` | Admin, Lecturer, Student |
| 5.2 | Quản lý Barem Rubric | `/api/v1/rubrics/*` | Lecturer, Department Head |
| 5.3 | Ngân hàng Câu hỏi | `/api/v1/questions/*` | Lecturer, Department Head |
| 5.4 | Tích hợp FLM & AI Gen | `/api/v1/flm/*` | Lecturer, Department Head |
| 5.5 | Luyện tập tự do MF-01 | `/api/v1/practice/*` | Student |
| 5.6 | Thi thử bấm giờ MF-02 | `/api/v1/mock-exams/*` | Student |
| 5.7 | Thi thật phòng Lab MF-04 | `/api/v1/official-exams/*` | Student, Proctor |
| 5.8 | Hậu kiểm & Công bố điểm | `/api/v1/official-exams/shifts/*` | Lecturer |
| 5.9 | Quản trị DLQ & Audit | `/api/v1/admin/*` | Admin |
| 5.10 | Phúc khảo nội bộ MF-04 | `/api/v1/appeals` | Student, Department Head |
| 5.11 | Thông báo trong ứng dụng FE-11 | `/api/v1/notifications` | All Roles |

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
- **Endpoint:** `GET /api/v1/academic/courses`
- **Quyền:** `student`, `lecturer`, `department_head`, `proctor`, `admin`
- **Response 200 OK:**
```json
[
  {
    "id": "22222222-0000-0000-0000-000000000001",
    "code": "PRN231",
    "name": "Building Cross-Platform Back-End Applications with .NET",
    "credits": 3,
    "semesterId": "11111111-0000-0000-0000-000000000001",
    "hasFollowUp": true,
    "transcriptBufferSeconds": 60,
    "maxFollowUpQuestions": 1,
    "isActive": true
  }
]
```

#### Cập nhật cấu hình động môn học (Admin / Giảng viên / Trưởng Bộ Môn)
- **Endpoint:** `PUT /api/v1/academic/courses/{id}/configurations`
- **Quyền:** `admin`, `department_head`, `lecturer`
- **Mô tả:** Cập nhật các tham số vận hành của môn học cho hệ thống luyện tập. Admin/Trưởng Bộ Môn cấu hình số lượng câu hỏi phụ tối đa cho hệ thống luyện tập MF-01 (`maxFollowUpQuestions` từ 1 đến 5 câu, mặc định 2 câu); cấu hình thời gian đệm hiệu đính phiên âm (`transcriptBufferSeconds` từ 10 đến 300 giây, mặc định 60 giây).
- **Request Body:**
```json
{
  "hasFollowUp": true,
  "transcriptBufferSeconds": 60,
  "maxFollowUpQuestions": 2
}
```
- **Response 200 OK:**
```json
{
  "id": "22222222-0000-0000-0000-000000000001",
  "code": "PRN231",
  "hasFollowUp": true,
  "transcriptBufferSeconds": 60,
  "maxFollowUpQuestions": 2,
  "updatedAt": "2026-10-10T10:00:00Z"
}
```
- **Response 422 Unprocessable Entity:** Nếu `transcriptBufferSeconds` không nằm trong khoảng 10–300s hoặc `maxFollowUpQuestions` không nằm trong khoảng 1–5 câu.

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
  3. `source`: Cố định `'manual'` khi tạo thủ công (hoặc `'flm_api'` khi sinh từ FLM).

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

### 5.4. Tích hợp FLM Adapter & Sinh Câu Hỏi Tự Động Bằng AI (FLM AI Generator) — MF-03
*(Phân hệ CSDL: `courses`, `rubrics`, `rubric_criteria`, `practice_questions`, `exam_questions`)*

#### 1. Lấy cấu trúc Đề cương & CLOs từ FLM Adapter
- **Endpoint:** `GET /api/v1/flm/courses/{courseId}/syllabus`
- **Quyền truy cập:** `lecturer`, `department_head`, `admin`
- **Mô tả:** Trích xuất thông tin Đề cương môn học (Syllabus), danh sách chuẩn đầu ra CLOs (Course Learning Outcomes), danh mục chủ đề kiến thức và bài học từ API hệ thống FPT Learning Material (FLM). Hỗ trợ cơ chế phân trang và lọc từ khóa cho các môn học quy mô lớn (> 50 bài học/chủ đề như PRN211, SWP391, PRN231).
- **Path Parameters:**
  - `courseId` (`uuid`, bắt buộc): Định danh duy nhất của môn học trong hệ thống.
- **Query Parameters (Phân trang & Lọc đề cương):**
  - `pageNumber` (`int`, tuỳ chọn, mặc định: `1`, ràng buộc: $\ge 1$): Số thứ tự trang danh mục chủ đề cần lấy.
  - `pageSize` (`int`, tuỳ chọn, mặc định: `20`, ràng buộc: $1 \le \text{pageSize} \le 100$): Số lượng chủ đề / bài học trên mỗi trang.
  - `searchTopic` (`string`, tuỳ chọn): Chuỗi từ khóa lọc tên chủ đề / bài học.
  - `cloCode` (`string`, tuỳ chọn): Mã CLO cụ thể để lọc các chủ đề đảm nhiệm chuẩn đầu ra đó (vd: `CLO1`, `CLO2`).

##### Bảng đặc tả DTO Phản hồi (`FlmSyllabusResponseDto`)
| Thuộc tính | Kiểu dữ liệu | Bắt buộc / Nullable | Ràng buộc / Enum | Mô tả nghiệp vụ |
|:---|:---|:---:|:---|:---|
| `courseId` | `uuid` | Bắt buộc (Non-null) | UUID hợp lệ | Định danh môn học |
| `courseCode` | `string` | Bắt buộc (Non-null) | Mã môn chuẩn (vd: `PRN231`, `PRN211`, `SWP391`) | Mã môn học chuẩn FPTU |
| `courseName` | `string` | Bắt buộc (Non-null) | Tối đa 200 ký tự | Tên đầy đủ của môn học |
| `syllabusCode` | `string` | Bắt buộc (Non-null) | Khóa phiên bản FLM | Mã phiên bản đề cương môn học |
| `totalCredits` | `int` | Bắt buộc (Non-null) | $1 \le \text{credits} \le 10$ | Số tín chỉ của môn học |
| `clos` | `List<CloDto>` | Bắt buộc (Non-null) | Tối thiểu 1 CLO | Danh sách chuẩn đầu ra toàn diện của môn học |
| `topics` | `List<TopicDto>` | Bắt buộc (Non-null) | Tối thiểu 1 chủ đề | Danh mục chủ đề kiến thức / bài học theo trang hiện tại |
| `pagination` | `FlmPaginationMetadataDto` | Bắt buộc (Non-null) | Metadata phân trang | Thông tin chỉ số phân trang danh mục chủ đề |

*Chi tiết `CloDto`:*
- `cloCode` (`string`, Non-null): Mã chuẩn đầu ra (vd: `CLO1`, `CLO2`, `CLO3`, `CLO4`).
- `description` (`string`, Non-null): Nội dung diễn giải chuẩn đầu ra kiến thức/kỹ năng.
- `targetBloomLevel` (`string`, Non-null): Mức nhận thức Bloom mục tiêu (`Remember`, `Understand`, `Apply`, `Analyze`, `Evaluate`, `Create`).

*Chi tiết `TopicDto`:*
- `topicId` (`string`, Non-null): Mã định danh chủ đề kiến thức (vd: `topic-01`, `topic-02`).
- `sessionNumber` (`int`, Non-null): Số thứ tự buổi học / slot trong khung chương trình đào tạo FPT (từ slot $1$ đến $30$).
- `topicName` (`string`, Non-null): Tên chủ đề / bài học trong đề cương môn học.
- `mappedClos` (`List<string>`, Non-null): Danh sách mã CLO tương ứng mà chủ đề này phụ trách đáp ứng.

*Chi tiết `FlmPaginationMetadataDto`:*
- `pageNumber` (`int`, Non-null): Số trang hiện tại ($\ge 1$).
- `pageSize` (`int`, Non-null): Kích thước số bản ghi trên trang ($1 \le \text{pageSize} \le 100$).
- `totalTopics` (`int`, Non-null): Tổng số chủ đề / bài học trong toàn bộ đề cương môn học.
- `totalPages` (`int`, Non-null): Tổng số trang phân danh mục ($\lceil \text{totalTopics} / \text{pageSize} \rceil$).

- **Response 200 OK:**
```json
{
  "courseId": "22222222-0000-0000-0000-000000000001",
  "courseCode": "PRN231",
  "courseName": "Building Cross-Platform Back-End Applications with .NET",
  "syllabusCode": "PRN231_FA26_v1.0",
  "totalCredits": 3,
  "clos": [
    {
      "cloCode": "CLO1",
      "description": "Hiểu và giải thích các nguyên lý Clean Architecture, SOLID và Design Patterns trong .NET",
      "targetBloomLevel": "Understand"
    },
    {
      "cloCode": "CLO2",
      "description": "Áp dụng Entity Framework Core và Dapper để xây dựng tầng Data Access Layer tối ưu",
      "targetBloomLevel": "Apply"
    },
    {
      "cloCode": "CLO3",
      "description": "Phân tích và thiết kế Web API chuẩn RESTful, tích hợp Authentication JWT và RBAC",
      "targetBloomLevel": "Analyze"
    },
    {
      "cloCode": "CLO4",
      "description": "Đánh giá và tối ưu hiệu năng ứng dụng, xử lý Concurrency và Resilient Messaging",
      "targetBloomLevel": "Evaluate"
    }
  ],
  "topics": [
    {
      "topicId": "topic-01",
      "sessionNumber": 1,
      "topicName": "Clean Architecture Overview & Dependency Injection Container",
      "mappedClos": ["CLO1"]
    },
    {
      "topicId": "topic-02",
      "sessionNumber": 4,
      "topicName": "EF Core Resilient Transactions, Migration & AsNoTracking Optimization",
      "mappedClos": ["CLO2", "CLO4"]
    },
    {
      "topicId": "topic-03",
      "sessionNumber": 7,
      "topicName": "Web API Security, JWT Bearer Tokens & RBAC Authorization Pipeline",
      "mappedClos": ["CLO3"]
    }
  ],
  "pagination": {
    "pageNumber": 1,
    "pageSize": 20,
    "totalTopics": 24,
    "totalPages": 2
  }
}
```

##### Phản hồi lỗi chuẩn hóa RFC 7807 (`GET /api/v1/flm/courses/{courseId}/syllabus`)
- **401 Unauthorized:**
```json
{
  "type": "https://oralexam.fpt.edu.vn/errors/unauthorized",
  "title": "Unauthorized",
  "status": 401,
  "detail": "Yêu cầu xác thực Bearer Token hợp lệ để truy cập tài nguyên đề cương FLM.",
  "instance": "/api/v1/flm/courses/22222222-0000-0000-0000-000000000001/syllabus"
}
```
- **403 Forbidden:**
```json
{
  "type": "https://oralexam.fpt.edu.vn/errors/forbidden",
  "title": "Forbidden",
  "status": 403,
  "detail": "Bạn không có quyền truy cập đề cương FLM. Yêu cầu vai trò 'lecturer', 'department_head' hoặc 'admin'.",
  "instance": "/api/v1/flm/courses/22222222-0000-0000-0000-000000000001/syllabus"
}
```
- **404 Not Found:**
```json
{
  "type": "https://oralexam.fpt.edu.vn/errors/course-not-found",
  "title": "Course or Syllabus Not Found",
  "status": 404,
  "detail": "Không tìm thấy môn học hoặc môn học chưa được liên kết cấu trúc đề cương trên FLM.",
  "instance": "/api/v1/flm/courses/22222222-0000-0000-0000-000000000001/syllabus"
}
```
- **502 Bad Gateway (FLM Service Connection Refused / Invalid Upstream Response):**
```json
{
  "type": "https://oralexam.fpt.edu.vn/errors/flm-bad-gateway",
  "title": "FLM External Service Bad Gateway",
  "status": 502,
  "detail": "Không thể thiết lập kết nối tới cổng dịch vụ FPT Learning Material (FLM API) hoặc dịch vụ trả về dữ liệu không hợp lệ sau 3 lần thử lại tự động (Polly Backoff 2s, 4s, 8s). Vui lòng liên hệ quản trị viên FLM hoặc thử lại sau.",
  "instance": "/api/v1/flm/courses/22222222-0000-0000-0000-000000000001/syllabus"
}
```
- **504 Gateway Timeout (FLM Adapter Timeout sau 3 lần Polly Retry):**
```json
{
  "type": "https://oralexam.fpt.edu.vn/errors/flm-gateway-timeout",
  "title": "FLM External Service Gateway Timeout",
  "status": 504,
  "detail": "Cổng kết nối FPT Learning Material (FLM API) không phản hồi trong giới hạn thời gian quy định (Attempt Timeout 10s / Total Request Timeout 60s) sau 3 lần thử lại tự động (Polly 2s, 4s, 8s). Vui lòng thử lại sau.",
  "instance": "/api/v1/flm/courses/22222222-0000-0000-0000-000000000001/syllabus"
}
```

> [!WARNING]
> **Quy Chuẩn Kiến Trúc Cấu Hình HttpClient & Polly v8 Resilience Handler Trong Backend .NET 8:**  
> - **CẤM TUYỆT ĐỐI** gán cứng `HttpClient.Timeout = TimeSpan.FromSeconds(10)` ở cấp độ client toàn cục. Tổng thời gian giãn cách nghỉ của 3 lần retry Polly ($2\text{s} + 4\text{s} + 8\text{s} = 14\text{s}$) đã vượt quá 10s. Nếu đặt timeout 10s ở cấp độ `HttpClient`, runtime sẽ ném `TaskCanceledException` ngay tại giây thứ 10, triệt tiêu hoàn toàn cơ chế thử lại của Polly!  
> - **Chuẩn cấu hình khuyến nghị:** Sử dụng `builder.Services.AddHttpClient(...).AddResilienceHandler(...)` phân tách rõ ràng 2 tầng:  
>   1. `AttemptTimeout = TimeSpan.FromSeconds(10)` (chờ tối đa 10s cho mỗi HTTP call đơn lẻ tới FLM API).  
>   2. `RetryStrategyOptions: MaxRetryAttempts = 3, Delay = 2s, BackoffType = Exponential`.  
>   3. `TotalRequestTimeout = TimeSpan.FromSeconds(60)` (bao trọn thời gian thực thi của cả 4 lần thử và 14s dãn cách backoff).



---

#### 2. Kích hoạt AI sinh câu hỏi từ dữ liệu đề cương FLM
- **Endpoint:** `POST /api/v1/questions/generate-from-flm`
- **Quyền:** `lecturer`, `department_head`, `admin`. **Giảng viên sử dụng AI sinh câu hỏi theo barem của mình**, kích hoạt Gemini AI Engine phân tích chuẩn đầu ra FLM để sinh bộ câu hỏi vấn đáp kèm Barem Rubric riêng 10.0đ và Model Answer $\ge 50$ ký tự, tự do chỉnh sửa trước khi gửi lên Bộ Môn thẩm định (`SUBMITTED_FOR_REVIEW`). Trưởng bộ môn (`department_head`) và Quản trị viên (`admin`) cũng có quyền thực hiện. Nếu người dùng mang vai trò khác (`proctor`, `student`), hệ thống chặn ngay lập tức và trả về `HTTP 403 Forbidden`.
- **Mô tả:** Giảng viên hoặc Trưởng bộ môn chọn môn học, chọn CLOs/chủ đề cần tạo câu hỏi. Hệ thống gửi đề cương sang Gemini AI Engine để sinh bộ câu hỏi vấn đáp kèm Barem Rubric $\sum = 10.0$đ và Đáp án mẫu chuẩn (Model Answer $\ge 50$ ký tự).

##### Bảng đặc tả DTO Yêu cầu (`GenerateQuestionsFromFlmRequestDto`)
| Thuộc tính | Kiểu dữ liệu | Bắt buộc / Nullable | Ràng buộc / Validation | Mô tả nghiệp vụ |
|:---|:---|:---:|:---|:---|
| `courseId` | `uuid` | Bắt buộc (Non-null) | Khóa ngoại tồn tại trong `courses` | Môn học cần sinh câu hỏi |
| `selectedCloCodes` | `List<string>` | Bắt buộc (Non-null) | Ít nhất 1 CLO hợp lệ trong syllabus | Danh sách CLOs cần bao phủ |
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

##### Bảng đặc tả DTO Phản hồi (`GenerateQuestionsFromFlmResponseDto`)
| Thuộc tính | Kiểu dữ liệu | Bắt buộc / Nullable | Ràng buộc / Enum | Mô tả nghiệp vụ |
|:---|:---|:---:|:---|:---|
| `courseId` | `uuid` | Bắt buộc (Non-null) | UUID | Môn học |
| `courseCode` | `string` | Bắt buộc (Non-null) | Mã môn | Mã môn học |
| `generatedCount` | `int` | Bắt buộc (Non-null) | Bằng số câu yêu cầu | Số câu hỏi thực tế AI đã sinh thành công |
| `source` | `string` | Bắt buộc (Non-null) | Cố định `'flm_api'` | Nguồn gốc câu hỏi |
| `questions` | `List<GeneratedQuestionDraftDto>` | Bắt buộc (Non-null) | Danh sách câu hỏi dự thảo | Danh sách câu hỏi kèm barem và đáp án |

*Chi tiết `GeneratedQuestionDraftDto`:*
- `tempId` (`string`, Non-null): Định danh tạm thời trong phiên preview (vd: `draft-flm-001`).
- `cloCode` (`string`, Non-null): Mã CLO mà câu hỏi đáp ứng.
- `title` (`string`, Non-null): Tiêu đề tóm tắt nội dung câu hỏi.
- `content` (`string`, Non-null): Nội dung đề bài câu hỏi vấn đáp chi tiết.
- `difficulty` (`string`, Non-null): Độ khó (`easy`, `medium`, `hard`).
- `bloomLevel` (`string`, Non-null): Mức Bloom (`Remember` đến `Create`).
- `source` (`string`, Non-null): Cố định `'flm_api'`.
- `usageScope` (`string`, Non-null): `'practice'`, `'exam'`, hoặc `'shared'`.
- `modelAnswer` (`string`, Non-null): Câu trả lời mẫu chuẩn mực, **bắt buộc $\ge 50$ ký tự**.
- `keyPoints` (`List<string>`, Non-null): Danh sách các ý chính bắt buộc phải có trong câu trả lời.
- `hasFollowUp` (`bool`, Non-null): Cờ cho phép hỏi câu phụ chuyên sâu.
- `followUpPrompt` (`string`, Nullable): Nội dung gợi mở câu hỏi xoáy phản biện (nếu có).
- `rubric` (`FlmRubricDraftDto`, Non-null): Barem đánh giá tương ứng.

*Chi tiết `FlmRubricDraftDto`:*
- `name` (`string`, Non-null): Tên Barem đánh giá.
- `totalMaxScore` (`decimal`, Non-null): **Khóa cứng bất biến $10.00$**.
- `criteria` (`List<FlmRubricCriterionDraftDto>`, Non-null): Tối thiểu 2 tiêu chí con, **$\sum \text{maxScore} \equiv 10.00$**.

*Chi tiết `FlmRubricCriterionDraftDto`:*
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
  "source": "flm_api",
  "questions": [
    {
      "tempId": "draft-flm-001",
      "cloCode": "CLO1",
      "title": "Phân tích nguyên lý Dependency Inversion trong Clean Architecture",
      "content": "Hãy giải thích nguyên lý Dependency Inversion (DIP) và minh họa cách tổ chức các tầng trong Clean Architecture .NET để tuân thủ nguyên lý này.",
      "difficulty": "medium",
      "bloomLevel": "Understand",
      "source": "flm_api",
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
      "tempId": "draft-flm-002",
      "cloCode": "CLO3",
      "title": "Thiết kế cơ chế xác thực JWT và phân quyền RBAC trong ASP.NET Core Web API",
      "content": "Hãy phân tích quy trình xác thực người dùng bằng JWT Bearer Token kết hợp phân quyền Role-based Access Control (RBAC) trong Clean Architecture ASP.NET Core 8.",
      "difficulty": "hard",
      "bloomLevel": "Analyze",
      "source": "flm_api",
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

##### Phản hồi lỗi chuẩn hóa RFC 7807 (`POST /api/v1/questions/generate-from-flm`)
- **400 Bad Request (Tham số đầu vào không hợp lệ):**
```json
{
  "type": "https://oralexam.fpt.edu.vn/errors/invalid-parameters",
  "title": "Invalid Generation Parameters",
  "status": 400,
  "detail": "Tổng phân bổ độ khó (easy: 0 + medium: 1 + hard: 2 = 3) không khớp với số câu yêu cầu (numberOfQuestions: 2).",
  "instance": "/api/v1/questions/generate-from-flm"
}
```
- **401 Unauthorized:**
```json
{
  "type": "https://oralexam.fpt.edu.vn/errors/unauthorized",
  "title": "Unauthorized",
  "status": 401,
  "detail": "Thiếu hoặc token JWT đã hết hạn.",
  "instance": "/api/v1/questions/generate-from-flm"
}
```
- **403 Forbidden (Yêu cầu vai trò Giảng viên hoặc Trưởng Bộ Môn):**
```json
{
  "type": "https://oralexam.fpt.edu.vn/errors/forbidden",
  "title": "Forbidden - Lecturer Or Department Head Required",
  "status": 403,
  "detail": "Tính năng kích hoạt AI sinh câu hỏi từ FLM yêu cầu vai trò Giảng viên ('lecturer') hoặc Trưởng Bộ Môn ('department_head'). Vai trò của bạn (Sinh viên/Giám thị) không được phép truy cập.",
  "instance": "/api/v1/questions/generate-from-flm"
}
```
- **404 Not Found:**
```json
{
  "type": "https://oralexam.fpt.edu.vn/errors/course-not-found",
  "title": "Course Not Found",
  "status": 404,
  "detail": "Không tìm thấy môn học tương ứng với courseId đã cung cấp.",
  "instance": "/api/v1/questions/generate-from-flm"
}
```
- **422 Unprocessable Entity:**
```json
{
  "type": "https://oralexam.fpt.edu.vn/errors/clo-not-found-in-syllabus",
  "title": "CLO Not In Syllabus",
  "status": 422,
  "detail": "Mã chuẩn đầu ra 'CLO99' không tồn tại trong cấu trúc đề cương hiện hành của môn học.",
  "instance": "/api/v1/questions/generate-from-flm"
}
```
- **502 Bad Gateway (Gemini AI Service Error / Malformed JSON Output sau 3 lần Polly Retry):**
```json
{
  "type": "https://oralexam.fpt.edu.vn/errors/ai-bad-gateway",
  "title": "AI Generator Bad Gateway",
  "status": 502,
  "detail": "Dịch vụ Google Gemini AI gặp sự cố kết nối hoặc trả về cấu trúc dữ liệu không hợp lệ (sai JSON Schema chuẩn câu hỏi/rubric) sau 3 lần thử lại tự động theo cấp số nhân (Polly 2s, 4s, 8s). Vui lòng thử lại sau.",
  "instance": "/api/v1/questions/generate-from-flm"
}
```
- **504 Gateway Timeout (Gemini AI Timeout sau 3 lần Polly Retry):**
```json
{
  "type": "https://oralexam.fpt.edu.vn/errors/ai-generator-timeout",
  "title": "AI Generator Service Timeout",
  "status": 504,
  "detail": "Dịch vụ Google Gemini AI không hoàn tất sinh câu hỏi trong thời hạn quy định (Attempt Timeout 30s / Total Request Timeout 90s) sau 3 lần thử lại tự động theo cấp số nhân (Polly 2s, 4s, 8s). Vui lòng thử lại với số lượng câu hỏi ít hơn.",
  "instance": "/api/v1/questions/generate-from-flm"
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
| `source` | `string` | Bắt buộc (Non-null) | `manual` \| `flm_api` | Nguồn gốc câu hỏi |
| `modelAnswer` | `string` | Bắt buộc (Non-null) | **Độ dài $\ge 50$ ký tự** | Đáp án mẫu chuẩn |
| `keyPoints` | `List<string>` | Bắt buộc (Non-null) | Tối thiểu 1 ý chính | Các luận điểm then chốt |
| `hasFollowUp` | `bool` | Bắt buộc (Non-null) | `true` \| `false` | Cờ hỏi xoáy |
| `followUpPrompt`| `string` | Nullable | Tuỳ chọn | Gợi ý câu phụ |
| `rubric` | `FlmRubricDraftDto` | Bắt buộc (Non-null) | **$\sum \text{maxScore} \equiv 10.00$** | Barem Rubric 10.0 |

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
      "source": "flm_api",
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
- `source` (`string`, Non-null): `'flm_api'` hoặc `'manual'`.
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
      "source": "flm_api",
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
  * **Lazy Inactivity Timeout:** Kiểm tra nếu thời gian không tương tác vượt quá `SessionInactivityTimeoutMinutes` (10 phút), tự động kết thúc phiên (`status = "completed"`) và trả về mã lỗi HTTP 410 Gone.
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
  "detail": "Phiên luyện tập đã kết thúc tự động do không có tương tác trong hơn 10 phút."
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
- **Response 410 Gone (Khi phiên bị timeout quá 10 phút không tương tác):**
```json
{
  "type": "https://tools.ietf.org/html/rfc7807",
  "title": "Session Timed Out",
  "status": 410,
  "detail": "Phiên luyện tập đã kết thúc tự động do không có tương tác trong hơn 10 phút."
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
- **Response 410 Gone (Khi phiên bị timeout quá 10 phút không tương tác):**
```json
{
  "type": "https://tools.ietf.org/html/rfc7807",
  "title": "Session Timed Out",
  "status": 410,
  "detail": "Phiên luyện tập đã kết thúc tự động do không có tương tác trong hơn 10 phút."
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
*(Phân hệ CSDL: `exam_structures`, `exam_sets`, `mock_exam_quotas`, `mock_exam_sessions`, `mock_exam_answers`)*

#### 1. Bắt đầu thi thử (Hạn ngạch thi thử do Trưởng Bộ Môn cấu hình động bằng PostgreSQL)
- **Endpoint:** `POST /api/v1/mock-exams/sessions/start`
- **Quyền:** `student`
- **Mô tả:** Sinh viên chủ động lựa chọn môn học và chế độ Có/Không Follow-up (`hasFollowUp: true/false`). Hạn ngạch số lượt thi thử trong ngày (`max_mock_exams_per_day`) do Trưởng Bộ Môn cấu hình động theo từng môn học trong bảng `courses`.
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
  "courseCode": "PRN231",
  "examSetCode": "SET-PRN231-01",
  "hasFollowUp": true,
  "remainingDailyQuota": 2,
  "durationMinutes": 30,
  "startedAt": "2026-10-10T14:00:00Z",
  "expiresAt": "2026-10-10T14:30:00Z",
  "questions": [
    { "orderIndex": 1, "id": "99999999-0000-0000-0000-000000000001", "title": "Phân tích nguyên lý DIP...", "bloomLevel": "Analyze" }
  ]
}
```
- **Response 429 Too Many Requests (Khi vượt quá hạn ngạch thi thử trong ngày):**
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.28",
  "title": "Too Many Requests",
  "status": 429,
  "detail": "Bạn đã sử dụng hết hạn ngạch lượt thi thử trong ngày cho môn học PRN231 theo quy định của Bộ Môn. Vui lòng quay lại vào ngày mai!"
}
```

#### 2. Nộp bài thi thử (Kiểm tra Server-side Master Timer & Trả Instant Feedback Scorecard)
- **Endpoint:** `POST /api/v1/mock-exams/sessions/submit`
- **Quyền:** `student`
- **Quy tắc:** Server kiểm tra `Now <= ExpiresAt + 10s`. Nếu trễ > 10s $\rightarrow$ Server từ chối tính điểm.
- **Request Body:**
```json
{
  "sessionId": "88888888-0000-0000-0000-000000000001",
  "answers": [
    {
      "examQuestionId": "99999999-0000-0000-0000-000000000001",
      "answerText": "Câu trả lời của sinh viên...",
      "timeTakenSeconds": 180
    }
  ]
}
```
- **Response 200 OK:**
```json
{
  "sessionId": "88888888-0000-0000-0000-000000000001",
  "courseCode": "PRN231",
  "totalScore": 8.20,
  "status": "GRADED",
  "submittedAt": "2026-10-10T14:28:30Z",
  "detailedScorecard": [
    {
      "questionId": "99999999-0000-0000-0000-000000000001",
      "title": "Phân tích nguyên lý DIP trong Clean Architecture",
      "score": 8.50,
      "criteriaScores": [
        { "criterionName": "Khái niệm và bản chất DIP", "score": 2.80, "maxScore": 3.00, "comment": "Giải thích chuẩn xác" },
        { "criterionName": "Hiện thực hóa DIP trong Clean Architecture", "score": 3.50, "maxScore": 4.00, "comment": "Tốt, cần nêu thêm DI Lifetime" },
        { "criterionName": "Lợi ích Unit Testing", "score": 2.20, "maxScore": 3.00, "comment": "Đã nêu được mock interface" }
      ],
      "strengths": "Nắm vững nguyên lý Dependency Inversion, liên hệ chính xác với tầng Infrastructure.",
      "weaknesses": "Chưa làm rõ cơ chế quản lý vòng đời phụ thuộc (Scoped vs Singleton).",
      "improvementSuggestions": "Nên ôn lại bài giảng về Service Lifetime trong ASP.NET Core để câu trả lời trọn vẹn hơn."
    }
  ]
}
```

#### 3. Tra cứu lịch sử thi thử và Instant Feedback Scorecard
- **Endpoint:** `GET /api/v1/mock-exams/history`
- **Quyền:** `student`
- **Query Params:** `courseId` (UUID, optional), `page` (default 1), `pageSize` (default 10)
- **Response 200 OK:**
```json
{
  "totalSessions": 12,
  "page": 1,
  "pageSize": 10,
  "sessions": [
    {
      "sessionId": "88888888-0000-0000-0000-000000000001",
      "courseCode": "PRN231",
      "courseName": "Building Cross-Platform Applications with .NET",
      "examSetCode": "SET-PRN231-01",
      "totalScore": 8.20,
      "status": "GRADED",
      "submittedAt": "2026-10-10T14:28:30Z",
      "questionsCount": 3,
      "detailedScorecard": [
        {
          "questionId": "99999999-0000-0000-0000-000000000001",
          "title": "Phân tích nguyên lý DIP trong Clean Architecture",
          "score": 8.50,
          "criteriaScores": [
            { "criterionName": "Khái niệm và bản chất DIP", "score": 2.80, "maxScore": 3.00, "comment": "Giải thích chuẩn xác" },
            { "criterionName": "Hiện thực hóa DIP trong Clean Architecture", "score": 3.50, "maxScore": 4.00, "comment": "Tốt, cần nêu thêm DI Lifetime" },
            { "criterionName": "Lợi ích Unit Testing", "score": 2.20, "maxScore": 3.00, "comment": "Đã nêu được mock interface" }
          ],
          "strengths": "Nắm vững nguyên lý Dependency Inversion, liên hệ chính xác với tầng Infrastructure.",
          "weaknesses": "Chưa làm rõ cơ chế quản lý vòng đời phụ thuộc (Scoped vs Singleton).",
          "improvementSuggestions": "Nên ôn lại bài giảng về Service Lifetime trong ASP.NET Core để câu trả lời trọn vẹn hơn."
        }
      ]
    }
  ]
}
```

---

### 5.7. Thi Thật Phòng Lab & An Ninh Kiosk (MF-04)
*(Phân hệ CSDL: `official_exam_sessions`, `real_exam_session_shifts`, `student_exam_tickets`, `exam_question_submissions`)*

#### 0. Khởi tạo kỳ thi & Cấu hình môn thi (Trưởng Bộ Môn)
- **Endpoint:** `POST /api/v1/official-exams/sessions`
- **Quyền:** `department_head`, `admin`
- **Mô tả:** Trưởng Bộ Môn khởi tạo kỳ thi (`OfficialExamSession` / Exam Season), gán môn thi vào kỳ thi, cấu hình Follow-up môn thi trong kỳ thi (`hasFollowUp: boolean`, `maxFollowUpQuestions: 1..2`) và `examInputMode` (`VoiceOnly`, `VoiceWithTranscriptEdit`) cùng `transcriptBufferSeconds` (10..300s).
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
- **Gán danh sách thí sinh:** Danh sách thí sinh của ca thi được hệ thống tự động trích xuất và gán trực tiếp từ lớp học đã có trong hệ thống (`classes`, `class_enrollments`), hoặc Giám thị / Giảng viên có thể import danh sách dự phòng từ file Excel (EPPlus) qua endpoint: `POST /api/v1/official-exams/shifts/{id}/import-roster`.
- **Quyền:** `lecturer`, `department_head`, `proctor`, `admin`
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
    { "ticketId": "cccccccc-0000-0000-0000-000000000002", "studentCode": "SE170002", "fullName": "Nguyễn Đăng Hải", "seatNumber": 2 }
  ]
}
```

#### 2. Lấy Presigned URL tải lên âm thanh Cloudflare R2
- **Endpoint:** `POST /api/v1/official-exams/tickets/{id}/presigned-url`
- **Quyền:** `student`, `proctor`
- **Request Body:**
```json
{
  "fileName": "01_SE170001.webm",
  "contentType": "audio/webm"
}
```
- **Response 200 OK:**
```json
{
  "uploadUrl": "https://r2.oralexam.edu.vn/exams/FA26/PRN231/01_SE170001.webm?X-Amz-Signature=...",
  "storageKey": "exams/FA26/PRN231/01_SE170001.webm"
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
    "fullName": "Nguyễn Đăng Hải",
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

#### 3. Giảng viên bấm "Công Bố Điểm" & Kích hoạt Khóa điểm một chiều (One-Way Lock)
- **Endpoint:** `POST /api/v1/official-exams/shifts/{shiftId}/publish-grades`
- **Quyền:** `lecturer`, `department_head`, `admin`
- **Cơ chế:** 
  1. **Điều kiện tiên quyết bất biến:** Kiểm tra **100% sinh viên trong ca thi đã có điểm hoàn chỉnh** (tất cả các vé thi đều có `final_score` hoặc `ai_score` hợp lệ, không có vé nào còn ở trạng thái chưa chấm). Nếu phát hiện bất kỳ bài thi nào chưa có điểm, hệ thống từ chối và trả về `HTTP 422 Unprocessable Entity`.
  2. Giảng viên sau khi rà soát toàn bộ ca thi trên Cổng Hậu kiểm, bấm nút **"Công Bố Điểm"**.
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

### 5.9. Quản trị Admin, Cứu hộ Dead-Letter Queue (DLQ Replay) & Audit Logs
*(Phân hệ CSDL: `dead_letter_queues`, `audit_logs`)*

#### Tra cứu danh sách task lỗi trong Dead-Letter Queue
- **Endpoint:** `GET /api/v1/admin/dlq?status=pending`
- **Quyền:** `admin`
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

#### Admin kích hoạt cứu hộ chấm bù bài thi (DLQ Replay)
- **Endpoint:** `POST /api/v1/admin/dlq/{id}/replay`
- **Quyền:** `admin`
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

#### 1. Sinh viên nộp đơn phúc khảo bài thi chính thức
- **Endpoint:** `POST /api/v1/appeals`
- **Quyền:** `student` (chính chủ sở hữu vé thi đã công bố điểm)
- **Mô tả:** Sau khi bài thi được công bố điểm (`PUBLISHED`), nếu sinh viên không đồng ý với kết quả đánh giá, sinh viên nộp đơn phúc khảo trực tiếp trên Student Portal kèm lý do cụ thể. Hệ thống tạo bản ghi `appeal_requests` ở trạng thái `PENDING` và chuyển đến Trưởng Bộ Môn (`department_head`) của môn học tương ứng để tiếp nhận và giao cho một Giảng viên chấm lại (`PUT /api/v1/appeals/{id}/assign-lecturer`).
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
  "status": "PENDING",
  "assignedTo": "e4c5b2a1-0001-4000-8000-000000000003",
  "createdAt": "2026-10-26T08:30:00Z",
  "message": "Đã tiếp nhận đơn phúc khảo thành công. Đơn đã được chuyển đến Trưởng Bộ Môn để tiếp nhận và phân công Giảng viên chấm lại bài thi."
}
```
- **Response 422 Unprocessable Entity:** Nếu vé thi chưa ở trạng thái `PUBLISHED` hoặc lý do phúc khảo để trống / dưới 10 ký tự.
- **Response 409 Conflict:** Nếu sinh viên đã có đơn phúc khảo đang xử lý (`PENDING`) cho vé thi này.

#### 2. Tra cứu danh sách đơn phúc khảo
- **Endpoint:** `GET /api/v1/appeals`
- **Quyền:** `department_head`, `admin`, `lecturer` (giảng viên xem đơn được phân công), `student` (sinh viên chỉ xem đơn của chính mình)
- **Query Params:**
  - `status`: `"PENDING"` | `"IN_REVIEW"` | `"APPROVED"` | `"REJECTED"` (optional)
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
      "status": "PENDING",
      "assignedToName": "Trần Thị C (Trưởng Bộ Môn SE)",
      "createdAt": "2026-10-26T08:30:00Z",
      "reviewedAt": null,
      "proposedScore": null,
      "reviewNotes": null
    }
  ]
}
```

#### 3. Giảng viên được phân công hoặc Trưởng Bộ Môn ra quyết định thẩm định phúc khảo
- **Endpoint:** `PUT /api/v1/appeals/{id}/review`
- **Quyền:** `lecturer`, `department_head`, `admin`
- **Mô tả:** Giảng viên được phân công chấm lại (hoặc Trưởng Bộ Môn) trực tiếp nghe lại file ghi âm và đối soát Evidence Panel để đưa ra phán quyết:
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

#### 4. Trưởng Bộ Môn giao giảng viên chấm lại đơn phúc khảo
- **Endpoint:** `PUT /api/v1/appeals/{id}/assign-lecturer`
- **Quyền:** `department_head`, `admin`
- **Mô tả:** Trưởng Bộ Môn ủy quyền / giao đơn phúc khảo cho Giảng viên chuyên môn thẩm định bài thi trên Evidence Panel và chấm lại. Hệ thống cập nhật `assigned_to = assignedLecturerId`, chuyển trạng thái đơn sang `IN_REVIEW`, và tự động gửi thông báo in-app (FE-11) tới Giảng viên được giao.
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
  "status": "IN_REVIEW",
  "assignedTo": "33333333-0000-0000-0000-000000000001",
  "assignedLecturerName": "Nguyen Trong Tot",
  "assignedAt": "2026-10-26T10:00:00Z",
  "notes": "Nhờ thầy Tốt nghe lại đoạn trả lời 01:25-02:10 và chấm lại cho sinh viên này do micro phòng thi bị rè.",
  "message": "Đã phân công giảng viên chấm lại đơn phúc khảo thành công."
}
```
- **Response 404 Not Found:** Nếu không tìm thấy đơn phúc khảo.
- **Response 422 Unprocessable Entity:** Nếu `assignedLecturerId` rỗng, người được gán không có vai trò `lecturer`, hoặc đơn không ở trạng thái `PENDING`.

---

### 5.11. Hộp thư Thông báo trong Ứng dụng (In-App Notifications) — FE-11
*(Phân hệ CSDL: `notifications`)*

Hộp thư thông báo trong ứng dụng phục vụ hiển thị chuông thông báo (Bell Icon) cho 5 vai trò người dùng, hỗ trợ 5 nhóm sự kiện chính:
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
*(Hợp đồng giao diện API Contract này là tài liệu pháp lý kỹ thuật bất biến giữa Frontend và Backend, bảo đảm tính toàn vẹn 100% khi tích hợp hệ thống).*

