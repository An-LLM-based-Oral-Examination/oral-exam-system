# Hướng Dẫn Cấu Hình Google Sign-In & OAuth 2.0 Cho VPS Production (https://oralexam.io.vn)

> **Mã đề tài:** Đồ Án Tốt Nghiệp SEP490 (FA26SE166) — FPT University HCM  
> **Dự án:** Hệ Thống Luyện Thi & Đánh Giá Vấn Đáp Bằng LLM Cho Ngành Kỹ Thuật Phần Mềm (LLM Oral Exam System)  
> **Tài liệu tham chiếu:** `05_Source_Code/docs/01_architecture/MASTER_ARCHITECTURE.md` | `TECHNICAL_DESIGN_PROPOSAL_R1_R8.md`  
> **Môi trường:** Production (`https://oralexam.io.vn` — IP: `103.90.226.9`)

---

## 1. Mục Đích & Bối Cảnh Kỹ Thuật

Khi triển khai ứng dụng Frontend SPA trên tên miền VPS Production `https://oralexam.io.vn`, thư viện Google Identity Services (GSI — `https://accounts.google.com/gsi/client`) thực hiện kiểm tra đối chiếu nguồn gốc JavaScript (`window.location.origin`) với danh sách trắng được khai báo trên Google Cloud Console.

Nếu origin `https://oralexam.io.vn` chưa được thêm vào mục **Authorized JavaScript origins** của OAuth 2.0 Client ID, thư viện client của Google sẽ ghi nhật ký:
```text
[GSI_LOGGER]: The given origin is not allowed for the given client ID
```
và từ chối kết xuất (render) nút bấm đăng nhập Google Sign-In bên trong phần tử DOM.

Tài liệu này cung cấp quy trình cấu hình chuẩn từng bước trên **Google Cloud Console**, các lưu ý kỹ thuật về thời gian đồng bộ DNS/OAuth của Google, cũng như cơ chế tự phục hồi (Resilient Fallback) đã được tích hợp trong mã nguồn `LoginPage.tsx`.

---

## 2. Quy Trình Cấu Hình Từng Bước Trên Google Cloud Console

### Bước 1: Truy Cập Trang Quản Trị Google Cloud Credentials
1. Mở trình duyệt và truy cập trang quản lý thông tin xác thực Google Cloud:  
   👉 **[Google Cloud Console — APIs & Services — Credentials](https://console.cloud.google.com/apis/credentials)**
2. Đăng nhập bằng tài khoản Google quản trị dự án Capstone FA26SE166 (tài khoản sở hữu OAuth Client ID).
3. Tại thanh điều hướng trên cùng (Project Dropdown), chọn đúng Project: **`Oral Examination System`** (hoặc Project ID tương ứng của đồ án).

---

### Bước 2: Định Vị OAuth 2.0 Client ID Cho Web Application
1. Tại tab **Credentials**, cuộn xuống phần **OAuth 2.0 Client IDs**.
2. Tìm Client ID đang được sử dụng cho ứng dụng Web (loại *Web application*).  
   *Giá trị này khớp với biến môi trường `GOOGLE_OAUTH_CLIENT_ID` trên Backend và `VITE_GOOGLE_CLIENT_ID` trên Frontend.*
3. Bấm vào biểu tượng **Chỉnh sửa** (biểu tượng cây bút hoặc bấm trực tiếp vào tên Client).

---

### Bước 3: Cấu Hình Authorized JavaScript Origins (Nguồn Gốc JavaScript Được Phép)
1. Cuộn xuống phần **Authorized JavaScript origins**.
2. Bấm nút **+ ADD URI**.
3. Nhập chính xác URL gốc của VPS Production:
   ```text
   https://oralexam.io.vn
   ```
   > ⚠️ **Quy tắc bắt buộc:**
   > - Phải bắt đầu bằng giao thức bảo mật `https://` (không dùng `http://` trên domain production).
   > - **TUYỆT ĐỐI KHÔNG** để dấu gạch chéo `/` ở cuối URL (ví dụ: `https://oralexam.io.vn/` là **SAI** cú pháp của Google OAuth).
   > - Không chứa đường dẫn con (`/login`, `/auth`, v.v.).

4. Khuyến nghị bổ sung thêm domain phụ (nếu có cấu hình bản ghi DNS `www`):
   ```text
   https://www.oralexam.io.vn
   ```

5. **Giữ nguyên các URL dành cho môi trường phát triển cục bộ (Local Development)**:
   ```text
   http://localhost:5173
   http://localhost:3000
   http://localhost
   ```

---

### Bước 4: Cấu Hình Authorized Redirect URIs (URI Chuyển Hướng Được Phép)
1. Cuộn tiếp xuống phần **Authorized redirect URIs**.
2. Bấm nút **+ ADD URI** và bổ sung:
   ```text
   https://oralexam.io.vn
   https://oralexam.io.vn/login
   ```
   *(Cùng với các redirect URI local: `http://localhost:5173`, `http://localhost:3000`)*.

---

### Bước 5: Lưu Thay Đổi
1. Cuộn xuống cuối trang và bấm nút **SAVE** (LƯU).
2. Hệ thống sẽ hiển thị thông báo lưu thành công.

---

## 3. Thời Gian Hiệu Lực & Độ Trễ Truyền Bá (Propagation Delay)

- **Độ trễ cập nhật của Google:** Sau khi lưu trên Google Cloud Console, Google cần từ **5 đến 10 phút** (trong một số trường hợp hiếm có thể lên đến 30–60 phút) để đồng bộ danh sách trắng OAuth Client ID tới tất cả các edge server của Google Identity Services trên toàn cầu.
- **Xóa cache trình duyệt nếu cần:** Nếu kiểm tra ngay sau khi cấu hình mà vẫn thấy thông báo lỗi, hãy mở cửa sổ Ẩn danh (Incognito Window) hoặc nhấn `Ctrl + F5` để làm mới hoàn toàn bộ nhớ đệm script `client.js`.

---

## 4. Cơ Chế Dự Phòng Resilient Fallback Trong Mã Nguồn (`LoginPage.tsx`)

Để đảm bảo hệ thống **không bao giờ bị gián đoạn dịch vụ** (Zero Downtime / Zero-Disruption) trong thời gian chờ Google cập nhật hoặc nếu người dùng truy cập từ mạng bị chặn script Google:

1. **Bắt & Chặn Console Error Tự Động:**  
   Hook `useEffect` trong `LoginPage.tsx` chủ động chặn bắt log `[GSI_LOGGER]` (`origin is not allowed`) từ script bên thứ ba, ngăn chặn unhandled runtime exception làm crash hoặc trắng màn hình.
2. **Kiểm Tra DOM & Timeout Inspection (800ms):**  
   Nếu sau 800ms mà iframe nút đăng nhập của Google không thể render (child element count = 0), component tự động chuyển sang trạng thái Fallback.
3. **Hiển Thị Cảnh Báo Trực Quan (Alert Banner):**  
   Giao diện hiển thị hộp thông báo màu hổ phách (amber alert) thân thiện:
   > *"Google Sign-In tạm thời chưa được cấp quyền trên tên miền này. Vui lòng sử dụng Tài khoản Nhà trường cấp (username & mật khẩu) ở form bên trên để đăng nhập."*
4. **Đăng Nhập Dự Phòng Bằng Tài Khoản Nhà Trường Cấp:**  
   Toàn bộ sinh viên, giảng viên, cán bộ khảo thí và quản trị viên đều có thể đăng nhập ngay lập tức thông qua endpoint:
   `POST /api/v1/auth/password-login` (kèm cờ đổi mật khẩu bắt buộc `mustChangePassword = true` cho tài khoản mới).

---

## 5. Cấu Hình Reverse Proxy Nginx Trên VPS (`oralexam.io.vn`)

Đảm bảo tệp cấu hình Nginx production (`frontend/nginx.prod.conf` hoặc `/etc/nginx/sites-available/oralexam.io.vn`) bao gồm tiêu đề `Referrer-Policy` chuẩn để Google Identity Services nhận diện đúng origin:

```nginx
# Security Headers cho Google Identity Services & CSR
add_header Referrer-Policy "strict-origin-when-cross-origin" always;
add_header X-Content-Type-Options "nosniff" always;
add_header X-Frame-Options "SAMEORIGIN" always;
```

---

## 6. Danh Mục Kiểm Tra Xác Nhận (Verification Checklist)

| STT | Hạng Mục Kiểm Tra | Kết Quả Kỳ Vọng | Trạng Thái |
|:---:|---|---|:---:|
| 1 | Thêm `https://oralexam.io.vn` vào Authorized JavaScript origins | Google Cloud Console lưu thành công | ✅ |
| 2 | Thêm `https://oralexam.io.vn` vào Authorized redirect URIs | Google Cloud Console lưu thành công | ✅ |
| 3 | Truy cập `https://oralexam.io.vn/login` từ trình duyệt | Không có unhandled JS exception | ✅ |
| 4 | Nút Google Sign-In hiển thị bình thường sau khi Google đồng bộ | Đăng nhập tài khoản @fpt.edu.vn / @fe.edu.vn thành công | ✅ |
| 5 | Khi origin chưa đồng bộ hoặc bị lỗi mạng | Hiển thị Fallback Alert hướng dẫn dùng tài khoản trường cấp | ✅ |
| 6 | Đăng nhập bằng Form Username/Password | `POST /api/v1/auth/password-login` trả về HTTP 200 OK | ✅ |
