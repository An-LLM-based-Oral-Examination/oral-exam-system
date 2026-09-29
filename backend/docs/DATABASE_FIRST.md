# Database First → EF Core

Chạy từ thư mục `backend/`.

## Scaffold từ DB

```powershell
dotnet ef dbcontext scaffold "Host=localhost;Port=5432;Database=oralexam;Username=oralexam;Password=oralexam_dev" Npgsql.EntityFrameworkCore.PostgreSQL -p src/Infrastructure -s src/API -c OralExamDbContext --output-dir ../Domain/Entities --namespace OralExamination.Domain.Entities --context-dir Persistence --no-onconfiguring -f
```

| Flag | Cần vì |
|------|--------|
| connection + `Npgsql...` | Kết nối Postgres |
| `-p` / `-s` | Project EF + startup |
| `-c OralExamDbContext` | Giữ đúng tên context (tránh đổi tên theo DB) |
| `--output-dir` + `--namespace` | Entity nằm ở Domain |
| `--context-dir` | DbContext nằm ở `Persistence/` |
| `--no-onconfiguring` | Dùng DI, không hard-code connection |
| `-f` | Ghi đè khi scaffold lại |

## Migration (đổi model sau này)

```powershell
dotnet ef migrations add TenThayDoi -p src/Infrastructure -s src/API
dotnet ef database update -p src/Infrastructure -s src/API
```

## Lưu ý

- Không `database update` lại `InitialBaseline` trên DB đã có bảng.
- Connection string: `src/API/appsettings.Development.json`
