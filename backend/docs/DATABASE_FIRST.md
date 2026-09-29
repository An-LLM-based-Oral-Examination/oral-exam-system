# Database First → EF Core

Chạy từ thư mục `backend/`.

Password lấy từ file `.env` ở root monorepo (không hard-code trong git).

## Scaffold từ DB

```powershell
dotnet ef dbcontext scaffold "Host=localhost;Port=5432;Database=oralexam;Username=oralexam;Password=<POSTGRES_PASSWORD>" Npgsql.EntityFrameworkCore.PostgreSQL -p src/Infrastructure -s src/API -c OralExamDbContext --output-dir ../Domain/Entities --namespace OralExamination.Domain.Entities --context-dir Persistence --no-onconfiguring -f
```


| Flag                           | Cần vì                     |
| ------------------------------ | -------------------------- |
| connection + `Npgsql...`       | Kết nối Postgres           |
| `-p` / `-s`                    | Project EF + startup       |
| `-c OralExamDbContext`         | Giữ đúng tên context       |
| `--output-dir` + `--namespace` | Entity ở Domain            |
| `--context-dir`                | DbContext ở `Persistence/` |
| `--no-onconfiguring`           | Dùng DI                    |
| `-f`                           | Ghi đè khi scaffold lại    |




## Migration

```powershell
dotnet ef migrations add TenThayDoi -p src/Infrastructure -s src/API
dotnet ef database update -p src/Infrastructure -s src/API
```



## Lưu ý

- Không `database update` lại `InitialBaseline` trên DB đã có bảng.
- Dev: password lấy từ `.env` — set biến môi trường (ASP.NET Core đọc `ConnectionStrings__DefaultConnection`) hoặc sửa local `src/API/appsettings.Development.json` (không commit password thật)

