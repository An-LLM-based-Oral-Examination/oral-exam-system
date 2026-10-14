# Oral Examination System

Hệ thống luyện thi và đánh giá vấn đáp cho ngành Kỹ thuật phần mềm. Sinh viên luyện tập và thi thử bằng giọng nói. Giảng viên quản lý ngân hàng câu hỏi, chấm và hậu kiểm. API chấm theo rubric bằng LLM.

Đồ án capstone FA26SE166.

## Thành phần

| Phần | Công nghệ | Hướng dẫn |
| --- | --- | --- |
| API | .NET 8, Clean Architecture, EF Core, PostgreSQL 16 | [backend/README.md](backend/README.md) |
| Giao diện | React 19, TypeScript, Vite | [frontend/README.md](frontend/README.md) |
| Cơ sở dữ liệu | PostgreSQL 16 trong Docker, schema từ EF Core migration | [backend/README.md](backend/README.md) |

Biến môi trường nằm ở một file `.env` trong thư mục này. Copy từ `.env.example` nếu chưa có. Không commit `.env`.

## Cấu trúc

```
oral-exam-system/
├── .env                    # Postgres, JWT, Google, và biến VITE_ của frontend
├── docker-compose.yml      # Postgres + API
├── backend/
│   └── src/
│       ├── Domain/
│       ├── Application/
│       ├── Infrastructure/
│       └── API/
├── frontend/
│   └── src/
└── docs/
```

## Chạy

Cần Docker Desktop. .NET 8 SDK chỉ cần khi tạo migration. Node.js cần khi chạy giao diện trên máy.

Từ thư mục này:

```powershell
docker compose up --build -d
```

API: http://localhost:5000/swagger. Health: http://localhost:5000/health.

Postgres trong container áp migration lúc API khởi động. Lần đầu, nếu volume cũ được tạo bằng script SQL, xem mục xử lý trong [backend/README.md](backend/README.md).

Giao diện, từ `frontend`:

```powershell
npm install
npm run dev
```

Mở http://localhost:5173. Vite đọc các biến `VITE_*` trong `.env` ở thư mục gốc và proxy `/api`, `/hubs` tới cổng 5000.

Dừng API và Postgres, giữ dữ liệu:

```powershell
docker compose down
```

## Tài liệu

- [Đặc tả 4 luồng nghiệp vụ](docs/01_architecture/MO_TA_4_LUONG_MAINFLOW_NGHIEP_VU.md)
- [Thiết kế cơ sở dữ liệu](docs/02_database/ERD_DATABASE_DESIGN.md)
- [Hợp đồng API](docs/03_api_and_contracts/API_CONTRACT_AND_INTEGRATION_GUIDE.md)
- [Quy trình Git](docs/04_devops_and_workflow/GIT_AND_TEAM_WORKFLOW.md)
- [Mục lục tài liệu](docs/README.md)
