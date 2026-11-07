# Oral Examination System

Hệ thống luyện thi và đánh giá vấn đáp bằng LLM cho ngành Kỹ thuật phần mềm (Đồ án Capstone FA26SE166 — Đại học FPT TP.HCM). Hệ thống vận hành với **6 vai trò RBAC** (`admin`, `department_head`, `examination_officer`, `lecturer`, `proctor`, `student`), **38 bảng CSDL chuẩn hóa 3NF** trên PostgreSQL 16, bao phủ 4 luồng nghiệp vụ chính (`MF-01..MF-04`) và luồng Ca thi trong lớp lấy điểm thành phần với mật khẩu tự động xoay vòng 5 phút (`MF-05`).

## Thành phần

| Phần | Công nghệ | Hướng dẫn |
| --- | --- | --- |
| API | .NET 8, Clean Architecture 4 tầng, EF Core 8, PostgreSQL 16 (38 bảng 3NF) | [backend/README.md](backend/README.md) |
| Giao diện | React 19, TypeScript, Vite, Tailwind CSS v4 (6 Roles RBAC) | [frontend/README.md](frontend/README.md) |
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

Mở http://localhost:5173 (hoặc cổng 3000 trên Docker). Vite đọc các biến `VITE_*` trong `.env` ở thư mục gốc và proxy `/api`, `/hubs` tới cổng 5000.

Dừng API và Postgres, giữ dữ liệu:

```powershell
docker compose down
```

## Tài liệu

- [Kiến trúc tổng thể & Đặc tả chi tiết 4 luồng nghiệp vụ + Ca thi trong lớp (Master Architecture)](docs/01_architecture/MASTER_ARCHITECTURE.md)
- [Đề xuất thiết kế kỹ thuật R1–R8](docs/01_architecture/TECHNICAL_DESIGN_PROPOSAL_R1_R8.md)
- [Thiết kế cơ sở dữ liệu (38 bảng 3NF)](docs/02_database/ERD_DATABASE_DESIGN.md)
- [Hợp đồng API & Tích hợp](docs/03_api_and_contracts/API_CONTRACT_AND_INTEGRATION_GUIDE.md)
- [Quy trình Git](docs/04_devops_and_workflow/GIT_AND_TEAM_WORKFLOW.md)
- [Mục lục tài liệu](docs/README.md)
