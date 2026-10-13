# Backend — Oral Examination API

API của hệ thống luyện thi và đánh giá vấn đáp. Solution .NET 8 theo Clean Architecture, PostgreSQL 16 qua EF Core Code First, xác thực Google và JWT.

## Chức năng

| Nhóm | Việc API làm |
| --- | --- |
| Auth | Đăng nhập Google, cấp và thu hồi refresh token |
| Practice | Phiên luyện tập, nộp câu trả lời, câu tiếp theo, SignalR báo kết quả chấm |
| Mock exams | Thi thử có hạn ngạch |
| Question bank | Ngân hàng câu hỏi, sinh đề, gửi duyệt |
| Official exams | Ca thi, bài nộp, hậu kiểm của giảng viên |
| Appeals | Phúc khảo |
| Courses | Môn học và cấu hình liên quan |
| Storage | Nhận audio để bóc băng |

Prefix REST là `/api/v1`. Hub SignalR luyện tập là `/hubs/practice`.

## Cấu trúc

```
backend/
├── OralExamination.sln
├── Dockerfile
└── src/
    ├── Domain/            # Entity, enum
    ├── Application/       # Use case (CQRS), DTO, validator
    ├── Infrastructure/    # EF Core, Postgres, JWT, Gemini, Whisper, R2
    └── API/               # Controllers, middleware, Program.cs
```

Schema nằm ở entity trong `Domain` và Fluent API trong `Infrastructure/Persistence/OralExamDbContext.cs`. Thay đổi database đi qua file trong `Infrastructure/Persistence/Migrations/`.

## Chạy server

API và Postgres chạy bằng Docker Compose ở thư mục `oral-exam-system` (nơi có `docker-compose.yml` và `.env`).

Cần Docker Desktop đang chạy, và file `.env` (copy từ `.env.example` nếu chưa có). .NET 8 SDK chỉ cần khi tạo migration trên máy.

Trong `.env` cần có ít nhất:

```env
POSTGRES_USER=oralexam
POSTGRES_PASSWORD=<mật-khẩu>
POSTGRES_DB=oralexam_db
POSTGRES_PORT=5432
JWT_SIGNING_KEY=<chuỗi tối thiểu 32 ký tự>
GOOGLE_OAUTH_CLIENT_ID=
```

`JWT_SIGNING_KEY` bắt buộc. API từ chối khởi động nếu thiếu hoặc ngắn hơn 32 ký tự. Khóa Gemini, Cloudflare và Storage là tùy chọn; thiếu thì chức năng AI và lưu file chưa gọi được. Không commit `.env`.

Volume Postgres từng được tạo bằng script SQL thì xóa một lần. Lệnh này xóa dữ liệu Postgres local:

```powershell
docker compose down -v
docker compose up --build -d
```

Máy chưa có volume đó:

```powershell
docker compose up --build -d
```

Postgres start sau healthcheck. API chờ Postgres sẵn sàng, rồi áp migration (tối đa 5 lần, mỗi lần cách 2 giây) trước khi nhận request.

```powershell
docker compose ps
docker compose logs -f web-api
```

Log cần có dòng `EF Core migrations applied`.

| Việc | Địa chỉ |
| --- | --- |
| Swagger | http://localhost:5000/swagger |
| Health | http://localhost:5000/health |
| SignalR practice | http://localhost:5000/hubs/practice |
| Postgres từ máy host | `localhost:5432` |

Dừng server, giữ dữ liệu:

```powershell
docker compose down
```

Chạy lại khi image đã có sẵn:

```powershell
docker compose up -d
```

## Đổi schema

Sửa entity và mapping trong `OralExamDbContext`, rồi từ thư mục `backend`:

```powershell
dotnet tool install --global dotnet-ef
dotnet ef migrations add TenThayDoi -p src/Infrastructure -s src/API
```

Lệnh này chỉ thêm file migration, chưa cập nhật database. Áp lên Postgres bằng cách build lại image:

```powershell
cd ..
docker compose up --build -d
```

`docker compose build` không đụng database, vì migration chỉ chạy khi container API khởi động. `docker compose up` không có `--build` dùng image cũ và không thấy migration vừa tạo.

Máy khác pull code (kèm file migration) rồi `docker compose up --build -d`. Không chạy lại `migrations add`.

Migration đã apply thì không sửa file đó. Thêm một migration mới cho lần đổi tiếp theo. Migration vừa tạo mà chưa `up --build` thì xóa bằng:

```powershell
dotnet ef migrations remove -p src/Infrastructure -s src/API
```

## Khi không lên

- Cổng 5000 hoặc 5432 đang bị chiếm: tắt process giữ cổng, hoặc đổi `POSTGRES_PORT` trong `.env`. API trong Compose map cố định `5000:5000`.
- Log `relation ... already exists`: volume còn schema SQL cũ. Chạy `docker compose down -v` rồi `docker compose up --build -d`. Dữ liệu local sẽ mất.
- Log thiếu `JWT_SIGNING_KEY`: điền khóa vào `.env` rồi `docker compose up -d` lại.
