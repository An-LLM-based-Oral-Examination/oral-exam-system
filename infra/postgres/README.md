# PostgreSQL (Docker)

Schema khớp `REVIEW 1/Database_LLMOramExam.md` + ERD_v1.

## Chạy lần đầu

Từ thư mục `oral-exam-system/`:

```bash
cp .env.example .env
docker compose up -d
```

Init scripts trong `init/` chỉ chạy khi volume **trống** (lần đầu).

## Kết nối

| | |
|--|--|
| Host | `localhost` |
| Port | `5432` |
| DB | `oralexam` |
| User / Pass | `oralexam` / `oralexam_dev` |

```text
Host=localhost;Port=5432;Database=oralexam;Username=oralexam;Password=oralexam_dev
```

```bash
docker exec -it oralexam-postgres psql -U oralexam -d oralexam
\dt
```

## Reset schema (xóa hết data)

```bash
docker compose down -v
docker compose up -d
```
