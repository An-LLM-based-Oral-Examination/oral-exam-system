# Frontend — Oral Examination

Giao diện React 19 cho hệ thống luyện thi và đánh giá vấn đáp. Ứng dụng gọi API .NET ở cổng 5000, phân quyền **6 vai trò RBAC** (`admin`, `department_head`, `examination_officer`, `lecturer`, `proctor`, `student`) và nhận kết quả chấm luyện tập/cảnh báo phòng thi qua SignalR.

## Màn hình theo 6 Roles RBAC

| Nhóm | Đường dẫn & Chức năng chính |
| --- | --- |
| Đăng nhập | `/login` (Hỗ trợ xác thực kép: Google OAuth 2.0 hoặc tài khoản `username`/`password` do Nhà trường cấp) |
| Sinh viên (`student`) | `/student/overview`, `/practice` (MF-01), `/mock-exam` (MF-02), tham gia lớp bằng mật khẩu lớp (`classes/join`), tham gia ca thi trong lớp bằng mã OTP xoay vòng 5 phút (`in-class-shifts`), `/exam` (Kiosk MF-04), `/results` (xem/xác nhận điểm hoặc nộp đơn phúc khảo lên Cán bộ Khảo thí) |
| Giảng viên (`lecturer`) | Quản lý lớp học phần thuộc môn được Trưởng BM phân công (`course_lecturers`) & mật khẩu lớp; tổ chức Ca thi trong lớp lấy điểm thành phần với mật khẩu xoay vòng 5 phút (`in_class_exam_shifts`) & hậu kiểm điểm thành phần; soạn câu hỏi ôn tập độc lập (`practice_questions`) và câu hỏi thi thật theo đề mẫu Trưởng BM (`exam_questions`) & Rubric Studio 10.0; hậu kiểm thi thật (Evidence Panel & Waveform Player); chấm lại phúc khảo khi Trưởng BM phân công |
| Trưởng Bộ Môn (`department_head`) | Quản lý danh mục CLO 3NF (`course_clos`); phân công nhiều Giảng viên vào môn học (`course_lecturers`); duyệt câu hỏi (`APPROVED`/`NEEDS_REVISION`/`REJECTED`), ban hành ma trận cấu trúc/đề thi mẫu, duyệt bộ đề chính thức (`OFFICIAL` $\to$ `HANDED_OVER`); cấu hình đề thi thử; **độc quyền cấu hình Follow-up cho đề/ca thi thật (1–5 câu, mặc định 2)**; tiếp nhận đơn phúc khảo từ Khảo thí chuyển giao, phân công GV chấm lại và phê duyệt quyết định phúc khảo |
| Cán bộ Khảo thí (`examination_officer`) | Tạo kỳ thi (`OfficialExamSession`), ca thi phòng lab (`RealExamSessionShift`), phân công giám thị & giám khảo, mở xuất bản đề (`is_exam_published = true`), import DSSV & phân bổ đề không trùng; công bố điểm ca thi 1 lần duy nhất (`One-Way Lock`) & xuất báo cáo Excel/PDF ký số; tiếp nhận, thẩm tra hợp lệ đơn phúc khảo từ sinh viên và chuyển giao cho Trưởng BM; **giám sát & kích hoạt replay DLQ cho luồng thi thật MF-04** |
| Giám thị (`proctor`) | **CHỈ** điểm danh thí sinh theo STT (Ghế 1–40, đối chiếu thẻ SV/CCCD) và báo cáo vi phạm an ninh trực tiếp tại phòng thi vào `exam_anti_cheat_logs` (Cấm mở/đóng ca thi, cấm xuất bản đề, cấm gỡ niêm phong IP, cấm xem/sửa điểm) |
| Quản trị viên (`admin`) | Quản lý người dùng (khóa/mở, gán 6 roles); quản lý chuỗi học thuật: tạo Học kỳ $\to$ tạo Môn học $\to$ gán Trưởng Bộ Môn (`department_head_user_id`); cấu hình động `system_configs` MF-01; cấu hình hạn ngạch thi thử MF-02 (`max_mock_exams_per_day`); **giám sát & kích hoạt replay DLQ cho luồng Luyện tập (MF-01) và Thi thử (MF-02)**; xem Audit Logs |

Đăng nhập hỗ trợ cơ chế xác thực kép (Dual Auth): Google OAuth 2.0 hoặc tài khoản `username` & `password` do Nhà trường cấp phát (kèm đổi mật khẩu; không mở form tự đăng ký công khai).

## Cấu trúc

```
frontend/
├── Dockerfile
├── vite.config.ts          # Dev server cổng 5173, proxy /api và /hubs
└── src/
    ├── routes/             # Route và phân quyền 6 roles
    ├── pages/              # auth, student, lecturer, department-head, examination-officer, proctor, admin
    ├── components/         # layout, practice, exam, rubric, audio
    ├── services/           # Gọi API
    ├── hooks/              # SignalR, speech, kiosk
    └── stores/             # Zustand state stores
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
