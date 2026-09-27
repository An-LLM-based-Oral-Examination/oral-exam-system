# QUY ƯỚC GIAO TIẾP VÀ TÍCH HỢP FE - BE (API CONTRACT & INTEGRATION GUIDE)
**Dự án:** An LLM-based Oral Examination (FA26SE166)  
**Tài liệu này quy định bắt buộc (Mandatory) các chuẩn mực kết nối giữa Frontend (React/Vite) và Backend (.NET 8 Clean Architecture) cho 4 thành viên (Hoàng, Hải, Thành, Tốt).**  
**Quy chuẩn bắt buộc:** Buffer Screen = 30 giây. Quota Guard đếm 3 lượt/môn/ngày bằng PostgreSQL (không dùng Redis). Thi thật phòng Lab MF-04 dùng Whisper STT server-side.

---

## 1. QUY ƯỚC CHUNG VỀ API (RESTful)
- **Base URL:** Tất cả REST API phải bắt đầu bằng tiền tố `/api/v1/`.
- **Định dạng dữ liệu:** Bắt buộc giao tiếp bằng `application/json` (UTF-8).
- **Quy tắc Naming Convention:**
  - URL luôn dùng chữ thường và dấu gạch ngang (kebab-case). Ví dụ: `/api/v1/exam-sessions` (KHÔNG DÙNG: `/api/v1/ExamSessions` hay `/api/v1/exam_sessions`).
  - Response JSON từ Backend trả về bắt buộc định dạng **camelCase** (ví dụ: `studentId`, `totalScore`, `roomCode`) để Frontend ánh xạ thẳng vào TypeScript Interface.
  - Phân quyền theo 3 vai trò (User Roles): `Student` (Sinh viên), `Instructor` (Giảng viên), `Admin` (Quản trị viên).

---

## 2. QUY CHUẨN XỬ LÝ LỖI (GLOBAL EXCEPTION - RFC 7807)
Backend tuyệt đối KHÔNG trả về chuỗi text thô. Mọi lỗi nghiệp vụ và hệ thống (400, 401, 403, 404, 422, 429, 500) đều phải bọc trong định dạng `ProblemDetails` theo chuẩn RFC 7807:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.21",
  "title": "Unprocessable Entity",
  "status": 422,
  "detail": "Tổng điểm Rubric phải bằng chính xác 10.0.",
  "errors": {
    "Criteria": [
      "Tổng điểm hiện tại đang là 9.5, còn thiếu 0.5 điểm để đạt 10.0"
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
- `403 Forbidden`: Bị từ chối quyền truy cập (ví dụ: Sinh viên cố truy cập route quản trị, hoặc gửi request sửa bài thi khi đã bị Khóa điểm một chiều `is_locked = true`).
- `404 Not Found`: Không tìm thấy tài nguyên theo ID.
- `422 Unprocessable Entity`: Dữ liệu đúng cấu trúc cú pháp nhưng vi phạm quy tắc nghiệp vụ (ví dụ: Tổng điểm Rubric != 10.0, hoặc cập nhật điểm thẩm định mà để trống lý do giải trình).
- `429 Too Many Requests`: Vi phạm Quota Guard thi thử quá 3 lượt/môn/ngày (kiểm soát bởi PostgreSQL).
- `500 Internal Server Error`: Lỗi máy chủ chưa được xử lý.

---

## 4. BẢO MẬT & XÁC THỰC (AUTHENTICATION & JWT CONTRACT)

### 4.1. Đăng nhập hệ thống
- **Endpoint:** `POST /api/v1/auth/login`
- **Quyền truy cập:** Public
- **Request Body:**
```json
{
  "email": "instructor@oralexam.edu.vn",
  "password": "SecurePassword123!"
}
```
- **Response 200 OK:**
```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "refreshToken": "7c9e6679-7425-40de-944b-e07fc1f90ae7",
  "tokenType": "Bearer",
  "expiresIn": 86400,
  "user": {
    "id": "usr-inst-001",
    "email": "instructor@oralexam.edu.vn",
    "fullName": "ThS. Nguyễn Văn Giảng",
    "role": "Instructor"
  }
}
```
- **Response 401 Unauthorized:**
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.2",
  "title": "Unauthorized",
  "status": 401,
  "detail": "Email hoặc mật khẩu không chính xác."
}
```

### 4.2. Quy ước lưu trữ và gửi Token
- Frontend lưu `accessToken` vào Memory hoặc Session Storage (tuyệt đối không lưu LocalStorage nhằm phòng chống XSS).
- Mọi request yêu cầu xác thực phải đính kèm Header:
  `Authorization: Bearer <accessToken>`
- Khi gặp lỗi `401 Unauthorized`, Axios Interceptor tự động kích hoạt luồng Silent Refresh token để lấy token mới mà không làm gián đoạn trải nghiệm người dùng.

---

## 5. DANH MỤC API CHI TIẾT THEO TÍNH NĂNG

### 5.1. Quản lý Môn học (Subjects) & Chương (Chapters) — MF-03

#### Lấy danh sách môn học
- **Endpoint:** `GET /api/v1/subjects`
- **Quyền:** `Student`, `Instructor`, `Admin`
- **Response 200 OK:**
```json
[
  {
    "id": "sub-se01",
    "code": "SWP391",
    "name": "Software Development Project",
    "description": "Thực tập dự án phần mềm theo mô hình Agile/Scrum"
  },
  {
    "id": "sub-se02",
    "code": "SWE201",
    "name": "Software Engineering Fundamentals",
    "description": "Nhập môn Kỹ thuật Phần mềm và Thiết kế Hệ thống"
  }
]
```

#### Tạo môn học mới
- **Endpoint:** `POST /api/v1/subjects`
- **Quyền:** `Instructor`, `Admin`
- **Request Body:**
```json
{
  "code": "PRN231",
  "name": "Building Cross-Platform Back-End Applications with .NET",
  "description": "Lập trình ứng dụng phân tán và Web API chuyên sâu với .NET 8"
}
```
- **Response 201 Created:** Trả về thông tin môn học vừa tạo kèm ID.

#### Lấy danh sách chương theo môn học
- **Endpoint:** `GET /api/v1/subjects/{id}/chapters`
- **Quyền:** `Student`, `Instructor`, `Admin`
- **Response 200 OK:**
```json
[
  {
    "id": "chp-01",
    "subjectId": "sub-se01",
    "chapterNumber": 1,
    "title": "Software Requirements & User Stories"
  },
  {
    "id": "chp-02",
    "subjectId": "sub-se01",
    "chapterNumber": 2,
    "title": "System Architecture & Clean Architecture Pattern"
  }
]
```

#### Tạo chương mới
- **Endpoint:** `POST /api/v1/subjects/{id}/chapters`
- **Quyền:** `Instructor`, `Admin`
- **Request Body:**
```json
{
  "chapterNumber": 3,
  "title": "Database Optimization & High Concurrency"
}
```
- **Response 201 Created:** Trả về thông tin chương vừa tạo.

---

### 5.2. Ngân hàng Câu hỏi & Barem Rubric 10.0 — MF-03

#### Tạo câu hỏi kèm Rubric (ACID Transaction)
- **Endpoint:** `POST /api/v1/questions`
- **Quyền:** `Instructor`, `Admin`
- **Quy tắc:** Tổng `maxScore` của mảng `criteria` bắt buộc bằng 10.0m.
- **Request Body:**
```json
{
  "subjectId": "sub-se01",
  "chapterId": "chp-02",
  "title": "Giải thích nguyên lý Dependency Inversion trong kiến trúc Clean Architecture?",
  "bloomLevel": "Analyze",
  "scope": "SHARED",
  "sampleAnswer": "High-level modules không nên phụ thuộc vào low-level modules...",
  "criteria": [
    {
      "name": "Định nghĩa đúng nguyên lý DIP",
      "maxScore": 3.0,
      "description": "Nêu rõ sự tách rời giữa module cấp cao và module cấp thấp qua abstraction."
    },
    {
      "name": "Áp dụng vào Clean Architecture",
      "maxScore": 4.0,
      "description": "Chỉ rõ tầng Domain/Application định nghĩa Interface, Infrastructure triển khai."
    },
    {
      "name": "Phân tích lợi ích thực tế",
      "maxScore": 3.0,
      "description": "Dễ dàng viết Unit Test, thay thế database mà không sửa đổi nghiệp vụ lõi."
    }
  ]
}
```
- **Response 201 Created:**
```json
{
  "id": "q-1001",
  "title": "Giải thích nguyên lý Dependency Inversion trong kiến trúc Clean Architecture?",
  "totalRubricScore": 10.0,
  "createdAt": "2026-10-01T08:30:00Z"
}
```

#### Chấm thử câu hỏi bằng AI (AI Calibration — Nút gắn ở Tuần 2)
- **Endpoint:** `POST /api/v1/questions/{id}/calibrate`
- **Quyền:** `Instructor`, `Admin`
- **Request Body:**
```json
{
  "testAnswer": "Theo em Dependency Inversion là các module cấp cao không phụ thuộc cấp thấp mà cùng phụ thuộc vào Interface."
}
```
- **Response 200 OK:**
```json
{
  "estimatedScore": 7.5,
  "analysis": "Câu trả lời đúng trọng tâm định nghĩa nhưng chưa nêu được ví dụ áp dụng cụ thể vào Clean Architecture.",
  "criteriaBreakdown": [
    { "criterionName": "Định nghĩa đúng nguyên lý DIP", "score": 3.0, "comment": "Chính xác" },
    { "criterionName": "Áp dụng vào Clean Architecture", "score": 2.5, "comment": "Thiếu phần Infrastructure" },
    { "criterionName": "Phân tích lợi ích thực tế", "score": 2.0, "comment": "Chưa nhắc đến Unit Test" }
  ]
}
```

---

### 5.3. Luyện tập tương tác (Interactive Practice & Real-time) — MF-01

#### Nộp câu trả lời luyện tập (Phòng thủ 4 tầng)
- **Endpoint:** `POST /api/v1/practice/submit`
- **Quyền:** `Student`
- **Cơ chế:** Ghi nhận DB trạng thái `PENDING` (< 100ms), đẩy vào Bounded Channel (1,000 slots), phản hồi ngay 202.
- **Request Body:**
```json
{
  "questionId": "q-1001",
  "transcript": "Dependency Inversion Principle phát biểu rằng các module cấp cao không nên phụ thuộc trực tiếp vào module cấp thấp...",
  "practiceMode": "PER_QUESTION"
}
```
- **Response 202 Accepted:**
```json
{
  "submissionId": "psub-901",
  "status": "PENDING",
  "message": "Bài nộp đã được tiếp nhận vào hàng đợi xử lý. Kết quả sẽ được gửi qua SignalR."
}
```

#### Kết nối thời gian thực SignalR Hub
- **Hub URL:** `/hubs/practice`
- **Event Name:** `ReceiveScorecard`
- **Payload SignalR phát về Client:**
```json
{
  "submissionId": "psub-901",
  "questionId": "q-1001",
  "totalScore": 8.5,
  "criteriaScores": [
    { "criterionId": "crit-01", "name": "Định nghĩa đúng nguyên lý DIP", "score": 3.0, "comment": "Rất chuẩn xác" },
    { "criterionId": "crit-02", "name": "Áp dụng vào Clean Architecture", "score": 3.5, "comment": "Khá tốt" },
    { "criterionId": "crit-03", "name": "Phân tích lợi ích thực tế", "score": 2.0, "comment": "Cần nêu thêm mocking test" }
  ],
  "feedback": "Bạn nắm vững lý thuyết DIP, phát âm thuật ngữ tiếng Anh rõ ràng.",
  "deepDiveQuestion": null
}
```

---

### 5.4. Thi thử bấm giờ (Mock Exam & Voice-First) — MF-02

#### Bắt đầu thi thử (Kiểm tra Quota 3 lần/ngày bằng PostgreSQL)
- **Endpoint:** `POST /api/v1/mock-exam/start`
- **Quyền:** `Student`
- **Request Body:**
```json
{
  "subjectId": "sub-se01"
}
```
- **Response 200 OK:**
```json
{
  "sessionId": "mock-sess-501",
  "subjectCode": "SWP391",
  "totalQuestions": 10,
  "maxDurationSeconds": 900,
  "startedAt": "2026-10-10T14:00:00Z",
  "questions": [
    { "order": 1, "id": "q-1001", "title": "Giải thích nguyên lý Dependency Inversion..." }
  ]
}
```
- **Response 429 Too Many Requests (Khi vượt quá 3 lượt/ngày/môn):**
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.28",
  "title": "Too Many Requests",
  "status": 429,
  "detail": "Bạn đã sử dụng hết hạn mức 3 lượt thi thử trong ngày cho môn SWP391. Vui lòng quay lại vào ngày mai!"
}
```

#### Nộp bài thi thử (Server-side Master Timer)
- **Endpoint:** `POST /api/v1/mock-exam/submit`
- **Quyền:** `Student`
- **Quy tắc:** Server kiểm tra `SubmitTime <= EndTime + 10s`. Nếu trễ > 10s, ném lỗi từ chối chấm.
- **Request Body:**
```json
{
  "sessionId": "mock-sess-501",
  "answers": [
    { "questionId": "q-1001", "transcript": "Câu trả lời của sinh viên..." }
  ]
}
```
- **Response 200 OK:**
```json
{
  "totalScore": 7.8,
  "bloomAnalysis": [
    { "category": "Remember", "score": 9.0 },
    { "category": "Understand", "score": 8.0 },
    { "category": "Apply", "score": 7.5 },
    { "category": "Analyze", "score": 7.0 },
    { "category": "Evaluate", "score": 6.5 },
    { "category": "Create", "score": 6.0 }
  ],
  "feedback": "Năng lực tư duy logic tốt, cần rèn luyện thêm kỹ năng phân tích phản biện."
}
```

---

### 5.5. Ca thi Phòng Lab & An ninh Kiosk — MF-04 (Tạo ở Tuần 3, Tiền đề Tuần 4)

#### Tạo phiên thi phòng Lab (Giảng viên / Khảo thí thiết lập ca)
- **Endpoint:** `POST /api/v1/exam-sessions`
- **Quyền:** `Instructor`, `Admin`
- **Request Body:**
```json
{
  "subjectId": "sub-se01",
  "sessionName": "Kỳ thi Vấn đáp Cuối kỳ SWP391 - Ca 1",
  "roomCode": "LAB-302",
  "startTime": "2026-10-25T07:30:00Z",
  "endTime": "2026-10-25T11:30:00Z",
  "assignedStudents": [
    { "studentId": "SV202601", "pcNumber": 1 },
    { "studentId": "SV202602", "pcNumber": 2 },
    { "studentId": "SV202603", "pcNumber": 3 }
  ]
}
```
- **Response 201 Created:**
```json
{
  "sessionId": "lab-sess-701",
  "sessionCode": "SWP391-LAB302-C1",
  "roomCode": "LAB-302",
  "totalAssignedStudents": 3,
  "status": "SCHEDULED"
}
```

#### Tra cứu thông tin ca thi và gán máy trạm Kiosk
- **Endpoint:** `GET /api/v1/exam-sessions/{id}`
- **Quyền:** `Student`, `Instructor`, `Admin`
- **Response 200 OK:**
```json
{
  "sessionId": "lab-sess-701",
  "sessionCode": "SWP391-LAB302-C1",
  "roomCode": "LAB-302",
  "startTime": "2026-10-25T07:30:00Z",
  "endTime": "2026-10-25T11:30:00Z",
  "students": [
    { "studentId": "SV202601", "fullName": "Trần Thị Lan", "pcNumber": 1, "isSealed": false },
    { "studentId": "SV202602", "fullName": "Lê Văn Tuấn", "pcNumber": 2, "isSealed": false }
  ]
}
```

#### Niêm phong đình chỉ thi do vi phạm Kiosk Lockdown
- **Endpoint:** `POST /api/v1/exam/seal`
- **Quyền:** `Student`
- **Request Body:**
```json
{
  "sessionId": "lab-sess-701",
  "pcNumber": 2,
  "violationReason": "Alt+Tab và mở DevTools quá 3 lần",
  "violationCount": 3
}
```
- **Response 200 OK:**
```json
{
  "status": "SEALED",
  "message": "Bài thi đã bị niêm phong và đình chỉ thi do vi phạm quy chế an ninh phòng Lab."
}
```

---

### 5.6. Bóc băng Whisper STT Server-side cho MF-04 — Tuần 4

*(Lưu ý: Khác với Web Speech API client-side chỉ dùng cho MF-01/MF-02 luyện tập, thi thật phòng lab MF-04 dùng Whisper server-side để bóc băng audio chất lượng cao kèm mốc thời gian).*

- **Endpoint:** `POST /api/v1/exam/transcribe-whisper`
- **Quyền:** `Instructor`, `Admin`
- **Request Body:**
```json
{
  "submissionId": "exam-sub-888",
  "audioUrl": "https://r2.oralexam.edu.vn/exams/01_SV202601.webm"
}
```
- **Response 200 OK:**
```json
{
  "submissionId": "exam-sub-888",
  "fullTranscript": "Em xin trình bày về nguyên lý Dependency Inversion...",
  "segments": [
    { "start": 0.0, "end": 4.5, "text": "Em xin trình bày về nguyên lý Dependency Inversion..." },
    { "start": 4.6, "end": 9.2, "text": "Trong kiến trúc phần mềm, Clean Architecture phân chia các tầng..." }
  ]
}
```

---

### 5.7. Cổng Hậu kiểm Giảng viên, Sửa điểm Thẩm định & Khóa Một Chiều — MF-04

#### Lấy danh sách bài thi cần thẩm định theo phòng Lab / số máy
- **Endpoint:** `GET /api/v1/audit/submissions`
- **Quyền:** `Instructor`, `Admin`
- **Query Params:** `?sessionId=lab-sess-701&roomCode=LAB-302`
- **Response 200 OK:**
```json
[
  {
    "submissionId": "exam-sub-888",
    "studentId": "SV202601",
    "fullName": "Trần Thị Lan",
    "pcNumber": 1,
    "audioFile": "01_SV202601.webm",
    "audioSha256": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
    "aiEstimatedScore": 8.0,
    "finalScore": 8.5,
    "isOverridden": true,
    "overrideReason": "Sinh viên phát âm từ khóa hơi bé nhưng giải thích đúng bản chất IoC Container.",
    "isLocked": false
  }
]
```

#### Giảng viên sửa điểm thẩm định (Bắt buộc kèm lý do giải trình)
- **Endpoint:** `PUT /api/v1/audit/override`
- **Quyền:** `Instructor`, `Admin`
- **Quy tắc:** Chỉ được phép gọi khi `isLocked == false`. Trường `overrideReason` bắt buộc không được để trống.
- **Request Body:**
```json
{
  "submissionId": "exam-sub-888",
  "criteriaScores": [
    { "criterionId": "crit-01", "score": 3.0 },
    { "criterionId": "crit-02", "score": 3.5 },
    { "criterionId": "crit-03", "score": 2.0 }
  ],
  "overrideReason": "Sinh viên nói lưu loát, bổ sung dẫn chứng thực tế phù hợp nên cộng thêm 0.5 điểm ở tiêu chí 2."
}
```
- **Response 200 OK:**
```json
{
  "submissionId": "exam-sub-888",
  "oldScore": 8.0,
  "newScore": 8.5,
  "overrideReason": "Sinh viên nói lưu loát, bổ sung dẫn chứng thực tế phù hợp nên cộng thêm 0.5 điểm ở tiêu chí 2.",
  "auditedBy": "usr-inst-001",
  "auditedAt": "2026-10-25T14:15:00Z"
}
```
- **Response 422 Unprocessable Entity (Khi thiếu lý do giải trình):**
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.21",
  "title": "Unprocessable Entity",
  "status": 422,
  "detail": "Lý do giải trình điều chỉnh điểm số là bắt buộc và không được để trống."
}
```

#### Khóa điểm một chiều (One-Way Lock)
- **Endpoint:** `POST /api/v1/audit/lock`
- **Quyền:** `Instructor`, `Admin`
- **Cơ chế:** Cập nhật `is_locked = true`. Sau khi khóa, EF Core Interceptor chặn vĩnh viễn mọi câu lệnh UPDATE/DELETE tiếp theo lên bài thi này.
- **Request Body:**
```json
{
  "submissionId": "exam-sub-888"
}
```
- **Response 200 OK:**
```json
{
  "submissionId": "exam-sub-888",
  "isLocked": true,
  "lockedAt": "2026-10-25T15:00:00Z",
  "message": "Điểm bài thi đã được khóa một chiều thành công. Học bạ đã được niêm phong vĩnh viễn."
}
```
- **Response 403 Forbidden (Nếu cố tình sửa đổi sau khi đã khóa):**
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.4",
  "title": "Forbidden",
  "status": 403,
  "detail": "Bài thi này đã được khóa điểm một chiều (One-Way Lock). Tuyệt đối không được phép chỉnh sửa."
}
```

#### Xuất file Excel bảng điểm khảo thí chuẩn FAP
- **Endpoint:** `GET /api/v1/audit/export-excel?sessionId=lab-sess-701`
- **Quyền:** `Instructor`, `Admin`
- **Response:** File nhị phân định dạng `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet` (Content-Disposition: `attachment; filename="BangDiem_SWP391_LAB302_C1.xlsx"`).

---
*(Hợp đồng giao diện API Contract này là tài liệu pháp lý kỹ thuật bất biến giữa Frontend và Backend, bảo đảm tính toàn vẹn 100% khi tích hợp hệ thống).*
