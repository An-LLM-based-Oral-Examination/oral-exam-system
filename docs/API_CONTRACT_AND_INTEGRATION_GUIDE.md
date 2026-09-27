# QUY ƯỚC GIAO TIẾP VÀ TÍCH HỢP FE - BE (API CONTRACT & INTEGRATION GUIDE)
Tài liệu này quy định bắt buộc (Mandatory) các tiêu chuẩn kết nối giữa Frontend (React/Vite) và Backend (.NET 8 Clean Architecture) để đảm bảo không xảy ra xung đột khi ráp code.

---

## 1. QUY ƯỚC CHUNG VỀ API (RESTful)
- **Base URL:** Tất cả API phải bắt đầu bằng `/api/v1/`.
- **Định dạng dữ liệu:** Bắt buộc giao tiếp bằng `application/json`.
- **Quy tắc Naming Convention:** 
  - URL luôn dùng viết thường và dấu gạch ngang (kebab-case). Ví dụ: `/api/v1/question-banks` (KHÔNG DÙNG: `/api/v1/QuestionBanks`).
  - Response JSON từ BE trả về bắt buộc phải format dạng **camelCase** (ví dụ: `studentId`, `totalScore`) để Frontend Map thẳng vào Object TypeScript dễ dàng.

---

## 2. QUY CHUẨN XỬ LÝ LỖI (GLOBAL EXCEPTION - RFC 7807)
Để FE bắt lỗi dễ dàng và hiển thị Toast/Alert, Backend KHÔNG ĐƯỢC trả về chuỗi text thô. Bất cứ lỗi nào (400, 401, 403, 404, 422, 500) BE đều phải bọc trong chuẩn `ProblemDetails` của Microsoft:

**Ví dụ một JSON báo lỗi từ BE trả về FE:**
```json
{
  "type": "https://datatracker.ietf.org/doc/html/rfc9110#section-15.5.21",
  "title": "Unprocessable Entity",
  "status": 422,
  "detail": "Tổng điểm Rubric phải bằng chính xác 10.0.",
  "errors": {
    "RubricCriteria": ["Tổng điểm hiện tại đang là 9.5"]
  }
}
```
**Quy ước cho FE:** 
- FE viết một hàm Axios Interceptor chặn toàn bộ HTTP Response.
- Nếu `status >= 400`, lấy chuỗi `detail` hiển thị lên Toast Error đỏ. Nếu có mảng `errors`, highlight đỏ tương ứng vào các input field bị sai.

---

## 3. BẢO MẬT & XÁC THỰC (AUTHENTICATION)
- **Login:** Gọi API `POST /api/v1/auth/login`. Nhận về `{ "accessToken": "ey...", "refreshToken": "ey..." }`.
- **Lưu trữ FE:** FE lưu `accessToken` vào Memory hoặc Session Storage (tuyệt đối không lưu LocalStorage tránh XSS).
- **Gửi Token:** FE đính kèm Header: `Authorization: Bearer <accessToken>` cho tất cả các request ngoại trừ Login.
- **Hết hạn Token (401):** Nếu FE gọi API bị dính lỗi 401 Unauthorized, Axios Interceptor tự động gọi ngầm API Refresh Token, lấy token mới và tự động gọi lại API vừa thất bại (Silent Refresh).

---

## 4. QUY ƯỚC KẾT NỐI REAL-TIME (SIGNALR)
Sử dụng cho luồng MF-01 Luyện tập (Backend chấm AI xong đẩy điểm về thẳng FE không cần F5).
- **Hub Endpoint:** `/hubs/practice`
- **Giao thức:** WebSockets (fallback xuống Server-Sent Events nếu mạng kém).
- **Event Name FE cần lắng nghe (On):** `ReceiveScorecard`
- **Payload FE sẽ nhận được:**
```json
{
  "questionId": "1234-abcd",
  "aiScore": 8.5,
  "bloomAnalysis": [
    { "category": "Remember", "point": 2.0 },
    { "category": "Understand", "point": 2.5 }
  ],
  "feedbackText": "Phát âm tốt, nhưng sai thuật ngữ polymorphism."
}
```

---

## 5. QUY ƯỚC MÃ CODE HTTP TRẢ VỀ CƠ BẢN
- `200 OK`: Truy vấn / Cập nhật thành công.
- `201 Created`: Tạo mới thành công (Kèm theo URL để lấy dữ liệu vừa tạo ở Header Location).
- `202 Accepted`: Dùng riêng cho MF-01. (Nghĩa là BE đã ném bài thi vào Hàng đợi 4 tầng thành công, FE cứ yên tâm đợi SignalR trả điểm sau, không cần chờ request này hoàn tất).
- `400 Bad Request`: Data gửi lên FE bị sai cấu trúc.
- `401 Unauthorized`: Hết hạn hoặc không có Token.
- `403 Forbidden`: Chặn quyền (Ví dụ SV cố tình gọi API của Giảng viên, hoặc gọi API sửa bài thi khi đã bị One-Way Lock).
- `422 Unprocessable Entity`: Data đúng cấu trúc nhưng sai logic nghiệp vụ (Ví dụ Rubric != 10.0).
- `429 Too Many Requests`: Vi phạm Quota Guard thi thử quá 3 lần/ngày.
