# 🔐 TÀI LIỆU HƯỚNG DẪN TÍCH HỢP AUTHENTICATION CHO FRONTEND
> **Dự án:** Hệ Thống Luyện Thi & Đánh Giá Vấn Đáp Bằng LLM Cho Ngành Kỹ Thuật Phần Mềm (FA26SE166)  
> **Người nhận:** Lê Vũ Hoàng (Lead Frontend) & Phạm Nguyễn Đăng Hải (Frontend Developer)  
> **Backend Version:** .NET 8 Clean Architecture — API Version `v1`  
> **Base URL:** `http://localhost:5000/api/v1` (hoặc cấu hình qua biến môi trường `VITE_API_BASE_URL`)

---

## 📌 1. TỔNG QUAN LUỒNG XÁC THỰC KÉP (DUAL AUTHENTICATION)

Hệ thống sử dụng cơ chế **Xác thực Kép (Dual Auth)** gồm **Google OAuth 2.0 (Google Identity Services)** và **Tài khoản `username` & `password` do Nhà trường cấp** kết hợp **JWT Access Token + Refresh Token Rotation**:
- ✅ **Hỗ trợ đăng nhập kép:** Đăng nhập bằng tài khoản Google (`POST /api/v1/auth/google-login`) hoặc bằng tài khoản/mật khẩu do Nhà trường cấp (`POST /api/v1/auth/password-login`), kèm chức năng đổi mật khẩu (`POST /api/v1/auth/change-password`). Không mở form tự đăng ký công khai bằng mật khẩu.
- ✅ **Chấp nhận mọi email domain hợp lệ** (Gmail cá nhân, email `@fpt.edu.vn`, hoặc mã số SV/GV do trường cấp).
- ⏱️ **Access Token:** Có hiệu lực trong **15 phút (900 giây)**. Đính kèm vào Header `Authorization: Bearer <accessToken>` ở mọi API được bảo vệ.
- 🔄 **Refresh Token:** Có hiệu lực trong **7 ngày**. Được dùng để cấp lại Access Token mới tự động ngầm (Silent Refresh) khi Access Token hết hạn, hỗ trợ Token Rotation chống lộ lọt.
- 🛡️ **Phòng chống Replay Attack (RFC 6819):** Nếu một Refresh Token cũ đã bị thu hồi mà vẫn bị gửi lên, toàn bộ các phiên đăng nhập khác của user sẽ bị server vô hiệu hóa ngay lập tức.

---

## 📋 2. BẢNG DANH MỤC 5 ENDPOINTS AUTHENTICATION

| Method | Endpoint | Quyền (Auth) | Mục đích |
| :---: | :--- | :---: | :--- |
| `POST` | `/api/v1/auth/google-login` | Public | Đăng nhập bằng Google ID Token |
| `POST` | `/api/v1/auth/password-login` | Public | Đăng nhập bằng tài khoản `username`/`email` & `password` do Nhà trường cấp |
| `POST` | `/api/v1/auth/change-password` | Authenticated | Đổi mật khẩu cho tài khoản được Nhà trường cấp |
| `POST` | `/api/v1/auth/refresh-token` | Public | Tự động làm mới Access Token (Token Rotation) |
| `POST` | `/api/v1/auth/sign-out` | Public | Đăng xuất & thu hồi Refresh Token trên Server |

---

## 🚀 3. ĐẶC TẢ CHI TIẾT TỪNG ENDPOINT

### 3.1. Đăng nhập bằng Google (`POST /api/v1/auth/google-login`)
Sau khi người dùng đăng nhập Google trên Frontend và nhận được Google `credential` (ID Token dạng JWT), Frontend gửi `idToken` lên endpoint này.

#### Request Headers:
```http
Content-Type: application/json
```

#### Request Body:
```json
{
  "idToken": "eyJhbGciOiJSUzI1NiIsImtpZCI6IjEy..."
}
```

#### Response Thành công (`200 OK`):
```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "refreshToken": "4a7f83b16d0e4ef9a2c34790b8f10255c2f829f7bc74479e917d23a10529bf14",
  "tokenType": "Bearer",
  "expiresIn": 900,
  "user": {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "email": "student01@gmail.com",
    "fullName": "Nguyễn Văn A",
    "studentCode": "SE170123",
    "role": "student"
  }
}
```
*Lưu ý về `role`:* Có 5 roles chuẩn viết thường: `"student"`, `"lecturer"`, `"department_head"`, `"proctor"`, `"admin"`.
- Nếu email đăng nhập lần đầu tiên: Backend tự động gán `role = "student"`, `studentCode = null`.
- Nếu email đã có sẵn trong CSDL: Backend bảo toàn nguyên vẹn `role` và `studentCode` đã lưu trong CSDL.

#### Response Thất bại:
- `401 Unauthorized`: Token của Google không hợp lệ, giả mạo hoặc đã hết hạn.
```json
{
  "type": "https://tools.ietf.org/html/rfc7231",
  "title": "Không xác thực được.",
  "status": 401,
  "detail": "ID Token của Google không hợp lệ."
}
```
- `403 Forbidden`: Tài khoản đã bị Quản trị viên khóa (`is_active = false`).
```json
{
  "type": "https://tools.ietf.org/html/rfc7231",
  "title": "Tài khoản bị khóa.",
  "status": 403,
  "detail": "Tài khoản đã bị khóa."
}
```
- `400 Bad Request`: `idToken` rỗng hoặc null.

---

### 3.2. Cấp lại Access Token (`POST /api/v1/auth/refresh-token`)
Được gọi tự động trong Axios Interceptor khi Access Token hết hạn (nhận mã `401 Unauthorized` từ API nghiệp vụ).

#### Request Body:
```json
{
  "refreshToken": "4a7f83b16d0e4ef9a2c34790b8f10255c2f829f7bc74479e917d23a10529bf14"
}
```

#### Response Thành công (`200 OK`):
```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "refreshToken": "8b9e11c27e1f4cf1b3d45801c9a21366d3a930a8cd85580fa28e34b21630ca25",
  "tokenType": "Bearer",
  "expiresIn": 900
}
```
*Cơ chế Token Rotation:* 
- Refresh Token cũ bị thu hồi ngay lập tức.
- Frontend **BẮT BUỘC cập nhật cả `accessToken` mới và `refreshToken` mới** vào bộ nhớ để sử dụng cho lần kế tiếp.

#### Response Thất bại:
- `401 Unauthorized`: Refresh Token không hợp lệ, đã hết hạn, hoặc bị thu hồi do phát hiện tấn công replay attack.
```json
{
  "type": "https://tools.ietf.org/html/rfc7231",
  "title": "Token không hợp lệ.",
  "status": 401,
  "detail": "Refresh token đã bị thu hồi. Toàn bộ phiên đăng nhập đã bị vô hiệu hóa vì phát hiện nghi ngờ rò rỉ token."
}
```
*(Khi nhận lỗi này, Frontend tự động xóa bỏ toàn bộ token và chuyển hướng về màn hình đăng nhập `/login`).*
- `403 Forbidden`: Người dùng đã bị khóa tài khoản trong thời gian đang mở phiên làm việc.

---

### 3.3. Đăng xuất an toàn (`POST /api/v1/auth/sign-out`)
Thu hồi phiên làm việc trên server. Endpoint này có tính chất **Idempotent** (gọi nhiều lần vẫn an toàn trả `200 OK`).

#### Request Body:
```json
{
  "refreshToken": "4a7f83b16d0e4ef9a2c34790b8f10255c2f829f7bc74479e917d23a10529bf14"
}
```

#### Response Thành công (`200 OK`):
```json
{
  "message": "Đăng xuất thành công."
}
```
Sau khi gọi xong, Frontend xóa trắng `accessToken`, `refreshToken` và `userProfile` trong SessionStorage/Memory rồi redirect về `/login`.

---

## 💻 4. HƯỚNG DẪN CODE MẪU CHO TEAM FRONTEND (TYPESCRIPT + REACT)

### 4.1. Cập nhật `src/types/auth.types.ts`
```typescript
export type UserRole = 'student' | 'lecturer' | 'department_head' | 'proctor' | 'admin';

export interface UserProfile {
  id: string;
  email: string;
  fullName: string;
  studentCode: string | null;
  role: UserRole;
}

export interface GoogleLoginResponse {
  accessToken: string;
  refreshToken: string;
  tokenType: string;
  expiresIn: number;
  user: UserProfile;
}

export interface RefreshTokenResponse {
  accessToken: string;
  refreshToken: string;
  tokenType: string;
  expiresIn: number;
}
```

### 4.2. Cập nhật `src/services/auth.service.ts`
```typescript
import apiClient from './api.client';
import type { GoogleLoginResponse, RefreshTokenResponse, UserProfile } from '@/types/auth.types';

const ACCESS_TOKEN_KEY = 'oral_exam_access_token';
const REFRESH_TOKEN_KEY = 'oral_exam_refresh_token';
const USER_PROFILE_KEY = 'oral_exam_user_profile';

export const authService = {
  /**
   * Đăng nhập bằng Google ID Token
   */
  async loginWithGoogle(idToken: string): Promise<GoogleLoginResponse> {
    const response = await apiClient.post<GoogleLoginResponse>('/auth/google-login', { idToken });
    const data = response.data;

    // Lưu vào sessionStorage để duy trì phiên làm việc
    sessionStorage.setItem(ACCESS_TOKEN_KEY, data.accessToken);
    sessionStorage.setItem(REFRESH_TOKEN_KEY, data.refreshToken);
    sessionStorage.setItem(USER_PROFILE_KEY, JSON.stringify(data.user));

    return data;
  },

  /**
   * Cấp lại token ngầm (Silent Refresh)
   */
  async refreshToken(): Promise<RefreshTokenResponse> {
    const currentRefreshToken = sessionStorage.getItem(REFRESH_TOKEN_KEY);
    if (!currentRefreshToken) {
      throw new Error('No refresh token available');
    }

    const response = await apiClient.post<RefreshTokenResponse>('/auth/refresh-token', {
      refreshToken: currentRefreshToken,
    });

    const data = response.data;
    sessionStorage.setItem(ACCESS_TOKEN_KEY, data.accessToken);
    sessionStorage.setItem(REFRESH_TOKEN_KEY, data.refreshToken);

    return data;
  },

  /**
   * Đăng xuất và thu hồi Refresh Token trên server
   */
  async logout(): Promise<void> {
    const refreshToken = sessionStorage.getItem(REFRESH_TOKEN_KEY);
    try {
      if (refreshToken) {
        await apiClient.post('/auth/sign-out', { refreshToken });
      }
    } catch {
      // Dù API lỗi mạng vẫn xóa session tại client
    } finally {
      sessionStorage.removeItem(ACCESS_TOKEN_KEY);
      sessionStorage.removeItem(REFRESH_TOKEN_KEY);
      sessionStorage.removeItem(USER_PROFILE_KEY);
    }
  },

  getAccessToken(): string | null {
    return sessionStorage.getItem(ACCESS_TOKEN_KEY);
  },

  getCurrentUser(): UserProfile | null {
    const userStr = sessionStorage.getItem(USER_PROFILE_KEY);
    if (!userStr) return null;
    try {
      return JSON.parse(userStr) as UserProfile;
    } catch {
      return null;
    }
  },
};
```

### 4.3. Cập nhật Axios Interceptor Tự Động Refresh Token (`src/services/api.client.ts`)
```typescript
import axios from 'axios';
import { authService } from './auth.service';

const apiClient = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL || 'http://localhost:5000/api/v1',
  headers: {
    'Content-Type': 'application/json',
  },
});

// 1. Request Interceptor: Tự động gán Bearer Token vào header
apiClient.interceptors.request.use((config) => {
  const token = authService.getAccessToken();
  if (token && config.headers) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

// 2. Response Interceptor: Tự động làm mới token khi nhận mã 401
let isRefreshing = false;
let failedQueue: Array<{ resolve: (token: string) => void; reject: (err: unknown) => void }> = [];

const processQueue = (error: unknown, token: string | null = null) => {
  failedQueue.forEach((prom) => {
    if (error) {
      prom.reject(error);
    } else if (token) {
      prom.resolve(token);
    }
  });
  failedQueue = [];
};

apiClient.interceptors.response.use(
  (response) => response,
  async (error) => {
    const originalRequest = error.config;

    // Bỏ qua các endpoint auth không cần refresh
    if (
      originalRequest.url?.includes('/auth/google-login') ||
      originalRequest.url?.includes('/auth/refresh-token')
    ) {
      return Promise.reject(error);
    }

    if (error.response?.status === 401 && !originalRequest._retry) {
      if (isRefreshing) {
        return new Promise((resolve, reject) => {
          failedQueue.push({ resolve, reject });
        })
          .then((token) => {
            originalRequest.headers.Authorization = `Bearer ${token}`;
            return apiClient(originalRequest);
          })
          .catch((err) => Promise.reject(err));
      }

      originalRequest._retry = true;
      isRefreshing = true;

      try {
        const data = await authService.refreshToken();
        processQueue(null, data.accessToken);
        originalRequest.headers.Authorization = `Bearer ${data.accessToken}`;
        return apiClient(originalRequest);
      } catch (refreshError) {
        processQueue(refreshError, null);
        await authService.logout();
        window.location.href = '/login';
        return Promise.reject(refreshError);
      } finally {
        isRefreshing = false;
      }
    }

    return Promise.reject(error);
  }
);

export default apiClient;
```

---

## 🎨 5. TÍCH HỢP NÚT GOOGLE SIGN-IN TRÊN MÀN HÌNH `LoginPage.tsx`

Frontend có thể dùng trực tiếp thư viện chính thức **Google Identity Services (GSI)**:

1. Thêm Google Client ID vào `frontend/.env`:
   ```env
   VITE_GOOGLE_CLIENT_ID=your_google_client_id_here.apps.googleusercontent.com
   ```
2. Thêm script vào thẻ `<head>` của `frontend/index.html`:
   ```html
   <script src="https://accounts.google.com/gsi/client" async defer></script>
   ```
3. Khởi tạo nút đăng nhập trong component `LoginPage.tsx`:
   ```tsx
   useEffect(() => {
     /* global google */
     if (window.google) {
       window.google.accounts.id.initialize({
         client_id: import.meta.env.VITE_GOOGLE_CLIENT_ID,
         callback: async (response: { credential?: string }) => {
           if (response.credential) {
             try {
               const loginResult = await authService.loginWithGoogle(response.credential);
               // Điều hướng theo vai trò (Role-Based Redirect)
               switch (loginResult.user.role) {
                 case 'student':
                   navigate('/student/dashboard');
                   break;
                 case 'lecturer':
                   navigate('/lecturer/dashboard');
                   break;
                 case 'department_head':
                   navigate('/department_head/dashboard');
                   break;
                 case 'admin':
                   navigate('/admin/dashboard');
                   break;
                 default:
                   navigate('/');
               }
             } catch (err) {
               console.error('Đăng nhập thất bại:', err);
             }
           }
         },
       });

       window.google.accounts.id.renderButton(
         document.getElementById('googleSignInBtn')!,
         { theme: 'outline', size: 'large', width: '100%', text: 'signin_with' }
       );
     }
   }, []);
   ```

---

## 🛠️ 6. CÔNG CỤ HỖ TRỢ TEST DÀNH CHO DEVELOPER

Backend đã chuẩn bị sẵn trang test ID Token tại:
[`docs/tools/google-id-token.html`](file:///d:/Đồ Án/05_Source_Code/docs/tools/google-id-token.html)
- Mở file này trên trình duyệt, đăng nhập tài khoản Google bất kỳ để lấy chuỗi `idToken` dạng JWT.
- Sao chép chuỗi `idToken` này dán vào Postman/Swagger hoặc gọi trực tiếp API `POST /api/v1/auth/google-login` để kiểm thử trả về token thật từ backend.
