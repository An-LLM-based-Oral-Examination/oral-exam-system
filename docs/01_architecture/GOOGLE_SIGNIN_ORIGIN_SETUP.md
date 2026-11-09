# HƯỚNG DẪN CẤU HÌNH GOOGLE SIGN-IN & AUTHORIZED JAVASCRIPT ORIGINS TRÊN GOOGLE CLOUD CONSOLE

> **Mã đề tài:** Đồ Án Tốt Nghiệp SEP490 (FA26SE166) — Đại học FPT TP.HCM  
> **Dự án:** Hệ Thống Luyện Thi & Đánh Giá Vấn Đáp Bằng LLM Cho Ngành Kỹ Thuật Phần Mềm (LLM Oral Exam System)  
> **Môi trường:** VPS Production (`https://oralexam.io.vn` — IP: `103.90.226.9`)  
> **Tài liệu tham chiếu:** `MASTER_ARCHITECTURE.md`, `TECHNICAL_DESIGN_PROPOSAL_R1_R8.md`  
> **Quy chuẩn an ninh:** Google Identity Services (GIS) — RFC 6749 OAuth 2.0 Client Security  

---

## 1. Mục Đích & Nguyên Lý Bắt Buộc (Why This Setup is Required)

Hệ thống Oral Exam hỗ trợ cơ chế Xác Thực Kép (**Dual Auth**):
1. **Google OAuth 2.0 PKCE** (`POST /api/v1/auth/google-login`) dành cho sinh viên và giảng viên sử dụng email tổ chức (`@fpt.edu.vn`, `@fe.edu.vn`).
2. **Tài khoản Nhà trường cấp** (`POST /api/v1/auth/password-login`) dành cho tài khoản cán bộ, khảo thí, admin và sinh viên dự phòng.

Khi chạy trên máy khách (Client Browser), thư viện **Google Identity Services** (`https://accounts.google.com/gsi/client`) tự động trích xuất nguồn gốc `window.location.origin` (tức `https://oralexam.io.vn`) và đối chiếu với danh sách **Authorized JavaScript origins** được lưu trữ tại Google Cloud Console.

### Hậu Quả Nếu Chưa Thêm Domain Vào Google Cloud Console:
- Google Identity Services chặn không cho render iframe nút bấm Google Sign-In.
- Console trình duyệt ghi log:
  ```text
  [GSI_LOGGER]: The given origin is not allowed for the given client ID.
  ```
- Thư viện Google không ném JavaScript exception chuẩn qua `try/catch` mà chỉ log lỗi nội bộ và để trống phần tử DOM.
- Mặc dù giao diện `LoginPage.tsx` đã có cơ chế tự phục hồi (**GSI Resilience Watchdog**) hiển thị cảnh báo thân thiện và dẫn hướng sang Form tài khoản trường cấp, việc cấu hình chính xác trên Google Cloud Console là **điều kiện tiên quyết** để kích hoạt tính năng đăng nhập Google cho toàn thể sinh viên và giảng viên.

---

## 2. Thông Số Cấu Hình Chuẩn Cho Hệ Thống

| Tham Số | Giá Trị Môi Trường Production | Ghi Chú |
|---|---|---|
| **Google Cloud Project ID** | `oralexam-fa26se166` (hoặc Project ID chứa OAuth 2.0 Client ID) | Chọn đúng project quản trị của đồ án |
| **Client Type** | `Web application` | Loại ứng dụng Web |
| **OAuth 2.0 Client ID** | Được cấu hình tại `GOOGLE_OAUTH_CLIENT_ID` (BE) và `VITE_GOOGLE_CLIENT_ID` (FE) | Chuỗi định dạng `*.apps.googleusercontent.com` |
| **Authorized JavaScript origins** | `https://oralexam.io.vn`<br/>`https://www.oralexam.io.vn` | **KHÔNG** có dấu gạch chéo `/` ở cuối, **KHÔNG** có subpath |
| **Authorized redirect URIs** | `https://oralexam.io.vn`<br/>`https://oralexam.io.vn/login` | Dành cho luồng callback nếu cần |
| **Local Dev Origins (Giữ lại)** | `http://localhost:3000`<br/>`http://localhost:5173` | Giữ nguyên để phục vụ kiểm thử cục bộ |

---

## 3. Quy Trình Cấu Hình Chi Tiết Từng Bước (Step-by-Step Guide)

### Bước 1: Đăng nhập Google Cloud Console
1. Mở trình duyệt và truy cập: **[https://console.cloud.google.com/apis/credentials](https://console.cloud.google.com/apis/credentials)**.
2. Đăng nhập bằng tài khoản Google nắm quyền Chủ sở hữu (Owner / Editor) của dự án Capstone FA26SE166.
3. Trên thanh điều hướng trên cùng, nhấp vào thanh chọn dự án (**Select a project**) và chọn đúng Project của hệ thống (ví dụ: `Oral Exam System`).

---

### Bước 2: Truy cập cấu hình OAuth 2.0 Client ID
1. Trong menu bên trái, vào **APIs & Services** $\rightarrow$ **Credentials** (Thông tin xác thực).
2. Tại bảng **OAuth 2.0 Client IDs**, nhấp vào tên của Client ID dành cho Web Application (ví dụ: `Oral Exam Web Client` hoặc ID tương ứng với giá trị `VITE_GOOGLE_CLIENT_ID`).
3. Màn hình chi tiết cấu hình Client ID sẽ xuất hiện.

---

### Bước 3: Cấu hình Authorized JavaScript origins
1. Cuộn xuống phần **Authorized JavaScript origins** (Nguồn gốc JavaScript được phép).
2. Nhấp vào nút **+ ADD URI**.
3. Thêm URI chính xác của VPS Production:
   ```text
   https://oralexam.io.vn
   ```
4. Tiếp tục nhấp **+ ADD URI** để thêm domain phụ (nếu DNS có cấu hình CNAME `www`):
   ```text
   https://www.oralexam.io.vn
   ```
5. **CÁC NGUYÊN TẮC CÚ PHÁP BẮT BUỘC CỦA GOOGLE:**
   - ⚠️ **CẤM** có dấu gạch chéo `/` ở cuối: `https://oralexam.io.vn/` $\rightarrow$ **SAI (Google sẽ báo lỗi cú pháp)**.
   - ⚠️ **CẤM** chứa đường dẫn con (subpath): `https://oralexam.io.vn/login` $\rightarrow$ **SAI**.
   - ⚠️ **CẤM** dùng giao thức không bảo mật `http://` cho domain public: `http://oralexam.io.vn` $\rightarrow$ **SAI**.
   - ✅ Giữ nguyên các origin `http://localhost:3000`, `http://localhost:5173` để các lập trình viên tiếp tục debug ở máy cá nhân.

---

### Bước 4: Cấu hình Authorized redirect URIs
1. Cuộn tiếp xuống phần **Authorized redirect URIs** (URI chuyển hướng được phép).
2. Nhấp nút **+ ADD URI** và bổ sung:
   ```text
   https://oralexam.io.vn
   https://oralexam.io.vn/login
   ```
3. Giữ nguyên các URI chuyển hướng local:
   ```text
   http://localhost:3000
   http://localhost:5173
   ```

---

### Bước 5: Lưu thay đổi (Save Changes)
1. Cuộn xuống cuối trang và nhấp nút **SAVE** (Lưu).
2. Chờ màn hình thông báo cập nhật thành công (Credentials updated).

---

## 4. Lưu Ý Quan Trọng Về Thời Gian Truyền Bá & Bộ Nhớ Đệm (Propagation Delay & Cache)

1. **Độ trễ truyền bá của Google (Propagation Delay):**
   - Sau khi nhấn **SAVE**, Google cần từ **5 đến 10 phút** (đôi khi lên đến 30 phút vào giờ cao điểm) để đồng bộ dữ liệu OAuth Client ID tới toàn bộ các edge servers của Google Identity Services trên toàn cầu.
   - Trong khoảng thời gian này, script Google có thể vẫn tiếp tục trả về lỗi origin mismatch.
2. **Bộ nhớ đệm trình duyệt (Browser Cache):**
   - File script `https://accounts.google.com/gsi/client` và các iframe handshake của Google thường được trình duyệt cache.
   - Để kiểm tra chính xác ngay sau khi hết thời gian truyền bá, quản trị viên nên:
     + Mở cửa sổ ẩn danh (**Ctrl + Shift + N** trên Chrome / Edge).
     + Hoặc xóa cache cứng bằng tổ hợp phím **Ctrl + F5**.
3. **Cấu hình Referrer-Policy trên Nginx VPS:**
   - Đảm bảo reverse proxy Nginx trên VPS (`05_Source_Code/frontend/nginx.prod.conf`) luôn đính kèm header:
     ```nginx
     add_header Referrer-Policy "strict-origin-when-cross-origin" always;
     ```
   - Tiêu đề này đảm bảo trình duyệt gửi `origin` chính xác (`https://oralexam.io.vn`) đến máy chủ xác thực của Google khi thực hiện handshake cross-origin.

---

## 5. Cơ Chế Tự Phục Hồi Khi GSI Gặp Sự Cố (Frontend Resilience Architecture)

Tại `05_Source_Code/frontend/src/pages/auth/LoginPage.tsx`, hệ thống đã được trang bị cơ chế tự phục hồi đa tầng (**Multi-Layer Resilience Watchdog**):

1. **Hàm `safeSerialize` chống văng lỗi Circular JSON:**
   Khi chặn bắt log từ `console.error` và `console.warn`, hệ thống tuần tự hóa đối số qua `safeSerialize` thay vì gọi `JSON.stringify` trực tiếp. Cơ chế này loại bỏ 100% rủi ro crash do `TypeError: Converting circular structure to JSON` từ các extension trình duyệt.
2. **Bộ lọc lỗi `[GSI_LOGGER]` không phân biệt hoa thường:**
   Bắt chính xác các chuỗi `[gsi_logger]`, `origin is not allowed`, `not allowed for the given client id` để kích hoạt cờ `googleOriginError = true`.
3. **Bắt sự kiện lỗi nạp Script (`script.onerror`):**
   Nếu mạng phòng thi, adblocker hoặc tường lửa chặn không cho tải script `accounts.google.com/gsi/client`, sự kiện `error` lập tức kích hoạt chế độ dự phòng.
4. **Bộ đếm thời gian an toàn toàn cục (Global Fallback Watchdog 2.5s):**
   Nếu sau 2.5 giây kể từ khi mount component, nút Google Sign-In vẫn không chứa bất kỳ phần tử con nào trong DOM (`childElementCount === 0`), hệ thống tự động hiển thị banner thông báo thân thiện.
5. **Banner thông báo trực quan & Dẫn hướng mượt mà:**
   Banner cảnh báo hiển thị:
   > *"Đăng nhập Google hiện chưa khả dụng trên tên miền này. Vui lòng đăng nhập bằng tài khoản nhà trường cấp bên dưới."*  
   Đồng thời form đăng nhập bằng username & mật khẩu (`/api/v1/auth/password-login`) vẫn hoạt động 100% bình thường, không gây gián đoạn phiên làm việc của người dùng.

---

## 6. Danh Mục Kiểm Tra Xác Nhận (Admin Verification Checklist)

| STT | Bước Kiểm Tra | Hành Động | Kết Quả Đạt Chuẩn |
|:---:|---|---|---|
| 1 | **Khai báo Origin** | Thêm `https://oralexam.io.vn` vào Authorized JavaScript origins | Google Cloud Console báo Saved thành công |
| 2 | **Khai báo Subdomain** | Thêm `https://www.oralexam.io.vn` vào Authorized JavaScript origins | Google Cloud Console báo Saved thành công |
| 3 | **Khai báo Redirect** | Thêm `https://oralexam.io.vn/login` vào Authorized redirect URIs | Google Cloud Console báo Saved thành công |
| 4 | **Đợi Propagation** | Đợi 5–10 phút sau khi cấu hình | Hoàn tất thời gian truyền bá của Google |
| 5 | **Kiểm thử Ẩn danh** | Mở trình duyệt ẩn danh truy cập `https://oralexam.io.vn/login` | Nút Google Sign-In hiển thị đầy đủ, không còn alert origin error |
| 6 | **Kiểm thử Đăng nhập** | Bấm nút Google Sign-In và chọn tài khoản FPT | Đăng nhập thành công, điều hướng vào Dashboard theo Role |
| 7 | **Kiểm thử Dự phòng** | Kiểm thử form đăng nhập bằng tài khoản trường cấp | Đăng nhập thành công với username và password |
