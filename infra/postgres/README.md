# PostgreSQL (Docker)

Schema khớp `docs/ERD_DATABASE_DESIGN.md` (28 bảng 3NF phân tách Practice - Exam).

Init SQL nằm trong `init/` và được mount vào `/docker-entrypoint-initdb.d` khi container start.


| File                 | Việc làm                                                                      |
| -------------------- | ----------------------------------------------------------------------------- |
| `init/01_schema.sql` | Tạo toàn bộ 28 bảng / constraints / FK                                        |
| `init/02_seed.sql`   | Seed dữ liệu mẫu (nếu có)                                                     |
| `mark_baseline.sql`  | Đánh dấu migration EF `InitialBaseline27Tables` đã apply (chạy tay khi cần)   |


---



## Chạy lần đầu (init database)

Làm **theo thứ tự** từ thư mục `05_Source_Code/` (root monorepo, nơi có `docker-compose.yml`).

### 1. Tạo file `.env`

```bash
cp .env.example .env
```

Mở `.env` và **bắt buộc** điền mật khẩu mạnh:

```env
POSTGRES_USER=oralexam
POSTGRES_PASSWORD=<mật-khẩu-mạnh>
POSTGRES_DB=oralexam
POSTGRES_PORT=5432
```

Compose **không** có password mặc định. Thiếu `POSTGRES_PASSWORD` → `docker compose up` sẽ lỗi ngay.

### 2. Start Postgres

```bash
docker compose up -d
```

Lần đầu (volume `oralexam_pgdata` còn trống), Postgres sẽ:

1. Tạo user / database theo `POSTGRES_*`
2. Chạy lần lượt mọi file trong `infra/postgres/init/` (`01_schema.sql` → `02_seed.sql`)
3. Ready khi healthcheck `pg_isready` pass

Kiểm tra container:

```bash
docker compose ps
docker compose logs -f postgres
```

Thấy log kiểu `database system is ready to accept connections` là ổn. Thoát log: `Ctrl+C`.

### 3. Kiểm tra schema đã có

```bash
docker exec -it oralexam-postgres psql -U oralexam -d oralexam -c "\dt"
```

(`oralexam` = giá trị `POSTGRES_USER` / `POSTGRES_DB` trong `.env` của bạn.)

Có danh sách bảng → init thành công.

---



## Reset database (xóa hết + init lại)

Init SQL **chỉ chạy khi volume trống**. Đổi password trong `.env` hoặc muốn schema sạch cũng cần wipe volume:

```bash
docker compose down -v
docker compose up -d
```

`-v` xóa volume `oralexam_pgdata` → lần start sau chạy lại `01_schema.sql` + `02_seed.sql`.

Chỉ stop, giữ data:

```bash
docker compose down
```

---

