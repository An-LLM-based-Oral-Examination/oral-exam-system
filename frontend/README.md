# Frontend — Oral Examination

Giao diện React 19 cho hệ thống luyện thi và đánh giá vấn đáp. Ứng dụng gọi API .NET ở cổng 5000 và nhận kết quả chấm luyện tập qua SignalR.

## Màn hình

| Nhóm | Đường dẫn |
| --- | --- |
| Đăng nhập | `/login` (Hỗ trợ xác thực kép: Google OAuth 2.0 hoặc tài khoản `username`/`password` do Nhà trường cấp) |
| Sinh viên (`student`) | `/student/overview`, `/practice`, `/mock-exam`, `/exam` (Kiosk), `/results` |
| Giảng viên (`lecturer`) | Dashboard, ngân hàng câu hỏi, AI sinh đề & Rubric Studio 10.0, hậu kiểm (Evidence Panel & Waveform Player), chấm phúc khảo |
| Trưởng Bộ Môn (`department_head`) | Duyệt câu hỏi (`APPROVED`/`NEEDS_REVISION`/`REJECTED`), cấu hình đề thi thử, quản trị kỳ thi & ca thi, phân công phúc khảo, giám sát & replay DLQ |
| Giám thị (`proctor`) | Điểm danh STT (Ghế 1–40), đối chiếu thẻ SV/CCCD, mở ca thi, giám sát phòng máy Kiosk |
| Quản trị viên (`admin`) | Quản lý người dùng (khóa/mở, gán 5 roles), quản lý học kỳ, cấu hình động `system_configs` MF-01, cấu hình hạn ngạch thi thử MF-02 (`max_mock_exams_per_day`), xem Audit Logs |

Đăng nhập hỗ trợ cơ chế xác thực kép (Dual Auth): Google OAuth 2.0 hoặc tài khoản `username` & `password` do Nhà trường cấp phát (kèm đổi mật khẩu; không mở form tự đăng ký công khai).

## Cấu trúc

```
frontend/
├── Dockerfile
├── vite.config.ts          # Dev server cổng 5173, proxy /api và /hubs
└── src/
    ├── routes/             # Route và phân quyền 5 roles
    ├── pages/              # auth, student, lecturer, department-head, proctor, admin
    ├── components/         # layout, practice, exam, rubric, audio
    ├── services/           # Gọi API
    ├── hooks/              # SignalR, speech, kiosk
    └── context/            # Phiên đăng nhập
```

## Chạy giao diện

Backend cần đang chạy tại `http://localhost:5000` (xem `backend/README.md`).

Từ thư mục `frontend`:

```powershell
npm install
npm run dev
```

Mở http://localhost:5173.

Vite đọc biến `VITE_*` từ file `.env` ở thư mục `oral-exam-system`, không đọc file env trong `frontend/`. Chỉ tên bắt đầu bằng `VITE_` được đưa vào trình duyệt. Khóa Postgres, JWT, Gemini và Cloudflare trong cùng file đó không vào bundle frontend.

Các biến frontend:

```env
VITE_API_BASE_URL=/api/v1
VITE_API_PROXY_TARGET=http://localhost:5000
VITE_SIGNALR_HUB_URL=/hubs/practice
VITE_GOOGLE_CLIENT_ID=<cùng giá trị với GOOGLE_OAUTH_CLIENT_ID>
```

`/api` và `/hubs` đi qua proxy của Vite tới `VITE_API_PROXY_TARGET`. `VITE_GOOGLE_CLIENT_ID` là Client ID công khai của Google, dùng cho nút đăng nhập.

Kiểm tra kiểu và build:

```powershell
npm run build
```

## Image Docker

`frontend/Dockerfile` nhận `VITE_API_BASE_URL`, `VITE_SIGNALR_HUB_URL` và `VITE_GOOGLE_CLIENT_ID` lúc build. `docker-compose.prod.yml` truyền Client ID từ `GOOGLE_OAUTH_CLIENT_ID` trong `.env` gốc. Đổi các biến `VITE_*` xong phải build lại image, vì Vite gắn giá trị vào file tĩnh lúc `npm run build`.
