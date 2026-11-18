#!/usr/bin/env bash
# ==============================================================================
# SCRIPT VẬN HÀNH PURGE DỮ LIỆU AN TOÀN CHO VPS PRODUCTION (FA26SE166)
# ==============================================================================
# Quy trình chuẩn:
#   0. Pre-flight check: Đọc cấu hình .env (không execute), kiểm tra Docker container
#      PostgreSQL đang chạy và sẵn sàng công cụ pg_dump, pg_restore, psql bên trong.
#   1. Step 1: Tự động sao lưu nhị phân toàn bộ CSDL (docker exec pg_dump -Fc)
#   2. Step 2: Cổng kiểm định toàn vẹn bản sao lưu (docker exec -i pg_restore -l)
#   3. Step 3: Thực thi purge_test_data_commit.sql (hoặc --dry-run) qua docker exec -i psql
#   4. Step 4: Truy vấn hậu kiểm độc lập xác nhận 3 môn cốt lõi & ENT% còn nguyên vẹn
# ==============================================================================

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"
ENV_FILE="${ENV_FILE:-${REPO_ROOT}/.env}"

read_env_var() {
    local key="$1"
    local file="$2"
    if [[ ! -f "$file" ]]; then
        return 0
    fi
    local line
    line="$(grep -E "^[[:space:]]*${key}=" "$file" | tail -n 1 || true)"
    if [[ -z "$line" ]]; then
        return 0
    fi
    local val="${line#*=}"
    val="${val%$'\r'}"
    val="$(printf '%s' "$val" | sed -E "s/^[[:space:]]+//; s/[[:space:]]+$//; s/^(['\"])(.*)\\1$/\\2/")"
    printf '%s' "$val"
}

_env_pg_user="$(read_env_var "POSTGRES_USER" "$ENV_FILE")"
_env_pg_db="$(read_env_var "POSTGRES_DB" "$ENV_FILE")"

PG_CONTAINER="${PG_CONTAINER:-oralexam-postgres}"
PGUSER="${PGUSER:-${_env_pg_user:-oralexam}}"
PGDATABASE="${PGDATABASE:-${_env_pg_db:-oralexam_db}}"
BACKUP_DIR="${BACKUP_DIR:-/tmp/db_backups}"

DRY_RUN=false
if [[ "${1:-}" == "--dry-run" ]]; then
    DRY_RUN=true
    SQL_FILE="${SCRIPT_DIR}/cleanup_concurrency_test_data_dryrun.sql"
else
    SQL_FILE="${SCRIPT_DIR}/purge_test_data_commit.sql"
fi

echo "=============================================================================="
echo "🛡️  FA26SE166 — QUY TRÌNH PURGE DỮ LIỆU AN TOÀN (DOCKER POSTGRES)"
echo "=============================================================================="
echo "  - Chế độ thực thi     : $([[ "$DRY_RUN" == "true" ]] && echo "DRY-RUN (ROLLBACK)" || echo "COMMIT")"
echo "  - Container DB        : ${PG_CONTAINER}"
echo "  - CSDL Mục tiêu       : ${PGDATABASE}"
echo "  - Người dùng DB       : ${PGUSER}"
echo "  - Thư mục sao lưu     : ${BACKUP_DIR}"
echo "  - Tệp SQL thực thi    : ${SQL_FILE}"
echo "=============================================================================="

# ------------------------------------------------------------------------------
# BƯỚC 0: PRE-FLIGHT CHECK (DOCKER CONTAINER & CÔNG CỤ TRONG CONTAINER)
# ------------------------------------------------------------------------------
echo "==> [Pre-flight] Kiểm tra Docker và container PostgreSQL..."
if ! command -v docker >/dev/null 2>&1; then
    echo "❌ [ERROR] Không tìm thấy công cụ 'docker' trong PATH hệ thống!" >&2
    exit 1
fi

is_running="$(docker inspect -f '{{.State.Running}}' "$PG_CONTAINER" 2>/dev/null || echo "false")"
if [[ "$is_running" != "true" ]]; then
    echo "❌ [ERROR] Container PostgreSQL '${PG_CONTAINER}' chưa chạy (State.Running=${is_running})!" >&2
    exit 1
fi

if ! docker exec "$PG_CONTAINER" sh -c 'command -v pg_dump >/dev/null 2>&1 && command -v pg_restore >/dev/null 2>&1 && command -v psql >/dev/null 2>&1'; then
    echo "❌ [ERROR] Thiếu pg_dump, pg_restore hoặc psql bên trong container '${PG_CONTAINER}'!" >&2
    exit 1
fi
echo "    ✓ Container '${PG_CONTAINER}' đang chạy và có đầy đủ pg_dump, pg_restore, psql."

if [[ ! -f "$SQL_FILE" ]]; then
    echo "❌ [ERROR] Không tìm thấy tệp SQL: ${SQL_FILE}" >&2
    exit 1
fi
echo "    ✓ Đã xác nhận: tệp SQL tồn tại."

mkdir -p "$BACKUP_DIR"

# ------------------------------------------------------------------------------
# BƯỚC 1: SAO LƯU NHỊ PHÂN CSDL TỰ ĐỘNG (AUTOMATED BINARY BACKUP)
# ------------------------------------------------------------------------------
TIMESTAMP="$(date -u +%Y%m%d_%H%M%SZ)"
BACKUP_FILE="${BACKUP_DIR}/${PGDATABASE}_pre_purge_${TIMESTAMP}.dump"

echo ""
echo "==> [Bước 1/4] Tiến hành sao lưu nhị phân CSDL trước khi can thiệp..."
echo "    Tệp sao lưu đích: ${BACKUP_FILE}"

docker exec "$PG_CONTAINER" pg_dump -U "$PGUSER" -d "$PGDATABASE" -Fc > "$BACKUP_FILE"
if [[ ! -s "$BACKUP_FILE" ]]; then
    echo "❌ [FATAL ERROR] Tệp sao lưu rỗng: ${BACKUP_FILE}" >&2
    exit 1
fi
chmod 600 "$BACKUP_FILE"

BACKUP_SIZE="$(du -h "$BACKUP_FILE" | cut -f1)"
echo "    ✓ Sao lưu hoàn tất thành công (Kích thước: ${BACKUP_SIZE})."

# ------------------------------------------------------------------------------
# BƯỚC 2: CỔNG KIỂM ĐỊNH TOÀN VẸN BẢN SAO LƯU (BACKUP INTEGRITY GATE)
# ------------------------------------------------------------------------------
echo ""
echo "==> [Bước 2/4] Kiểm định tính toàn vẹn của tệp sao lưu (pg_restore -l)..."

if ! docker exec -i "$PG_CONTAINER" pg_restore -l < "$BACKUP_FILE" >/dev/null 2>&1; then
    echo "❌ [FATAL ERROR] Cổng kiểm định sao lưu THẤT BẠI! Tệp ${BACKUP_FILE} không hợp lệ hoặc bị hỏng." >&2
    echo "    DỪNG TOÀN BỘ TIẾN TRÌNH. Không có bất kỳ lệnh SQL nào được thực thi trên CSDL." >&2
    exit 1
fi
echo "    ✓ Cổng kiểm định sao lưu ĐẠT CHUẨN. Bản sao lưu có cấu trúc TOC hoàn toàn hợp lệ."

# ------------------------------------------------------------------------------
# BƯỚC 3: THỰC THI SQL PURGE TRONG KHỐI TRANSACTION
# ------------------------------------------------------------------------------
echo ""
echo "==> [Bước 3/4] Thực thi kịch bản SQL (${SQL_FILE})..."

docker exec -i "$PG_CONTAINER" psql -U "$PGUSER" -d "$PGDATABASE" -v ON_ERROR_STOP=1 < "$SQL_FILE"

echo "    ✓ Thực thi kịch bản SQL hoàn tất không có lỗi."

# ------------------------------------------------------------------------------
# BƯỚC 4: TRUY VẤN HẬU KIỂM TOÀN VẸN CÁC DANH MỤC CỐT LÕI VÀ TIẾNG ANH
# ------------------------------------------------------------------------------
echo ""
echo "==> [Bước 4/4] Chạy truy vấn hậu kiểm độc lập trên CSDL..."

docker exec -i "$PG_CONTAINER" psql -U "$PGUSER" -d "$PGDATABASE" -v ON_ERROR_STOP=1 <<'EOSQL'
DO $$
DECLARE
    v_core_count integer;
    v_ent_count integer;
BEGIN
    SELECT COUNT(*) INTO v_core_count
    FROM courses
    WHERE code IN ('PRF192', 'CSD201', 'DBI202');

    IF v_core_count <> 3 THEN
        RAISE EXCEPTION '[VERIFICATION FAILED] Số môn cốt lõi (PRF192, CSD201, DBI202) = %, kỳ vọng = 3', v_core_count;
    END IF;

    SELECT COUNT(*) INTO v_ent_count
    FROM courses
    WHERE code LIKE 'ENT%';

    IF v_ent_count < 6 THEN
        RAISE EXCEPTION '[VERIFICATION FAILED] Số môn tiếng Anh (ENT%%) = %, kỳ vọng >= 6', v_ent_count;
    END IF;

    RAISE NOTICE '[VERIFICATION PASSED] CSDL đã được xác thực an toàn tuyệt đối: 3 môn cốt lõi, % môn ENT.', v_ent_count;
END $$;

SELECT code, name, is_active FROM courses ORDER BY code;
EOSQL

echo ""
echo "=============================================================================="
echo "🎉 HOÀN TẤT THÀNH CÔNG!"
echo "    Bản sao lưu dự phòng: ${BACKUP_FILE}"
echo "=============================================================================="
