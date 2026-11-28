#!/usr/bin/env bash
# ==============================================================================
# ORAL EXAM SYSTEM — STANDARDIZED VPS PRODUCTION DEPLOYMENT & VERIFICATION SCRIPT
# Target OS: Ubuntu 24.04 LTS (IP: 103.90.226.9 — Domain: https://oralexam.io.vn)
# Project: FA26SE166 — FPT University HCM
# Working Directory on VPS: /opt/oralexam
# Usage:
#   sudo ./scripts/deploy_vps.sh [branch_name]
#   Ví dụ:
#     sudo ./scripts/deploy_vps.sh develop
#     sudo ./scripts/deploy_vps.sh main
# ==============================================================================

set -euo pipefail

# ------------------------------------------------------------------------------
# 0. CẤU HÌNH BIẾN MÔI TRƯỜNG & THƯ MỤC
# ------------------------------------------------------------------------------
TARGET_BRANCH="${1:-develop}"
DEPLOY_DIR="${DEPLOY_DIR:-/opt/oralexam}"
TIMESTAMP="$(date +%Y%m%d_%H%M%S)"
BACKUP_DIR="${DEPLOY_DIR}/backups"
LOG_DIR="${DEPLOY_DIR}/logs"
DEPLOY_LOG="${LOG_DIR}/deploy_${TIMESTAMP}.log"

# Đảm bảo thư mục logs tồn tại
mkdir -p "${LOG_DIR}"
exec > >(tee -a "${DEPLOY_LOG}") 2>&1

echo "================================================================================"
echo "🚀 BẮT ĐẦU QUY TRÌNH TRIỂN KHAI PRODUCTION: Nhánh [${TARGET_BRANCH}]"
echo "⏰ Khởi động lúc: $(date -u '+%Y-%m-%d %H:%M:%S UTC')"
echo "📁 Thư mục làm việc: ${DEPLOY_DIR}"
echo "📝 File log: ${DEPLOY_LOG}"
echo "================================================================================"

# ------------------------------------------------------------------------------
# BƯỚC 1: PRE-FLIGHT CHECKS (Kiểm tra Docker, Swap, Disk Space)
# ------------------------------------------------------------------------------
echo ""
echo "🔍 [Bước 1/6] Kiểm tra điều kiện tiên quyết (Pre-flight checks)..."

# 1.1 Kiểm tra thư mục triển khai
if [ -d "${DEPLOY_DIR}" ]; then
  cd "${DEPLOY_DIR}"
else
  echo "⚠️ Cảnh báo: Thư mục ${DEPLOY_DIR} không tồn tại. Sử dụng thư mục hiện tại: $(pwd)"
fi

# 1.2 Kiểm tra curl & Docker Daemon
if ! command -v curl >/dev/null 2>&1; then
  echo "❌ Lỗi: Không tìm thấy công cụ curl!" >&2
  exit 1
fi

if ! docker info >/dev/null 2>&1; then
  echo "❌ Lỗi: Docker daemon không hoạt động hoặc user hiện tại không có quyền truy cập docker socket!" >&2
  exit 1
fi
echo "  ✅ Docker daemon: Đang hoạt động bình thường."

# 1.3 Kiểm tra Docker Compose v2
if ! docker compose version >/dev/null 2>&1; then
  echo "❌ Lỗi: Docker Compose v2 ('docker compose') không tìm thấy!" >&2
  exit 1
fi
echo "  ✅ Docker Compose: $(docker compose version --short)"

# 1.4 Kiểm tra dung lượng Swap (Khuyến nghị >= 2GB trên VPS Vietnix 6GB RAM)
SWAP_TOTAL_MB=$(free -m | awk '/Swap:/ {print $2}')
if [ -z "${SWAP_TOTAL_MB}" ] || [ "${SWAP_TOTAL_MB}" -eq 0 ]; then
  echo "  ⚠️ CẢNH BÁO: Hệ thống chưa kích hoạt bộ nhớ Swap! (Khuyến nghị tối thiểu 2048MB Swap)"
else
  echo "  ✅ Bộ nhớ Swap: ${SWAP_TOTAL_MB} MB khả dụng."
fi

# 1.5 Kiểm tra dung lượng đĩa khả dụng (yêu cầu tối thiểu 5GB trống)
AVAILABLE_DISK_KB=$(df -k . | awk 'NR==2 {print $4}')
AVAILABLE_DISK_GB=$((AVAILABLE_DISK_KB / 1024 / 1024))
if [ "${AVAILABLE_DISK_KB}" -lt 5242880 ]; then
  echo "  ⚠️ CẢNH BÁO: Dung lượng đĩa khả dụng dưới 5GB (${AVAILABLE_DISK_GB} GB trống)! Hãy dọn bớt docker cache."
else
  echo "  ✅ Dung lượng đĩa trống: ${AVAILABLE_DISK_GB} GB (Đạt chuẩn an toàn >= 5GB)."
fi

# 1.6 Kiểm tra file cấu hình .env
if [ ! -f ".env" ]; then
  if [ -f ".env.example" ]; then
    echo "  ⚠️ Cảnh báo: Tệp .env chưa tồn tại, đang tạo bản sao từ .env.example..."
    cp .env.example .env
  else
    echo "  ❌ Lỗi: Không tìm thấy tệp .env hoặc .env.example trong $(pwd)!" >&2
    exit 1
  fi
fi
echo "  ✅ Tệp môi trường .env: Hợp lệ."

read_env_value() {
  local key="$1"
  local env_file="${2:-.env}"
  if [[ -z "${key}" || ! -r "${env_file}" ]]; then
    printf '%s' ""
    return 0
  fi
  local escaped_key=""
  escaped_key="$(printf '%s' "${key}" | sed 's/[][\.^$*+?(){}|]/\\&/g')"
  local line=""
  line="$(grep -E "^[[:space:]]*(export[[:space:]]+)?${escaped_key}=" "${env_file}" 2>/dev/null | tail -n1 || true)"
  if [[ -z "${line}" ]]; then
    printf '%s' ""
    return 0
  fi
  line="${line%$'\r'}"
  local raw="${line#*=}"
  raw="${raw#"${raw%%[![:space:]]*}"}"
  raw="${raw%"${raw##*[![:space:]]}"}"
  if [[ -z "${raw}" ]]; then
    printf '%s' ""
    return 0
  fi
  local dq_re='^"(([^"\\]|\\.)*)"([[:space:]]*(#.*)?)?$'
  local sq_re="^'([^']*)'([[:space:]]*(#.*)?)?$"
  if [[ "${raw}" =~ ${dq_re} ]]; then
    local val="${BASH_REMATCH[1]}"
    if [[ "${val}" == *\\* ]]; then
      local unescaped=""
      local i=0
      local len=${#val}
      local ch next
      while (( i < len )); do
        ch="${val:i:1}"
        if [[ "${ch}" == "\\" ]] && (( i + 1 < len )); then
          next="${val:i+1:1}"
          if [[ "${next}" == '"' || "${next}" == "\\" ]]; then
            unescaped+="${next}"
            i=$((i + 2))
            continue
          fi
        fi
        unescaped+="${ch}"
        i=$((i + 1))
      done
      val="${unescaped}"
    fi
    printf '%s' "${val}"
    return 0
  elif [[ "${raw}" =~ ${sq_re} ]]; then
    printf '%s' "${BASH_REMATCH[1]}"
    return 0
  fi
  if [[ "${raw}" == \#* ]]; then
    raw=""
  else
    local comment_re='^(.*)[[:space:]]+#(.*)$'
    while [[ "${raw}" =~ ${comment_re} ]]; do
      raw="${BASH_REMATCH[1]}"
    done
  fi
  raw="${raw#"${raw%%[![:space:]]*}"}"
  raw="${raw%"${raw##*[![:space:]]}"}"
  printf '%s' "${raw}"
}

FRONTEND_HTTP_PORT_VAL="$(read_env_value "FRONTEND_HTTP_PORT")"
FRONTEND_HTTP_PORT_VAL="${FRONTEND_HTTP_PORT_VAL:-80}"

# 1.7 Pre-flight chứng thư ký số PDF PKI (PAdES .pfx) cho Production
PDF_SIGNING_REQUIRE_CERT_VAL="$(read_env_value "PDF_SIGNING_REQUIRE_CERTIFICATE")"
PDF_SIGNING_REQUIRE_CERT_VAL="${PDF_SIGNING_REQUIRE_CERT_VAL:-${PDF_SIGNING_REQUIRE_CERTIFICATE:-true}}"
PDF_SIGNING_REQUIRE_CERT_VAL="${PDF_SIGNING_REQUIRE_CERT_VAL#"${PDF_SIGNING_REQUIRE_CERT_VAL%%[![:space:]]*}"}"
PDF_SIGNING_REQUIRE_CERT_VAL="${PDF_SIGNING_REQUIRE_CERT_VAL%"${PDF_SIGNING_REQUIRE_CERT_VAL##*[![:space:]]}"}"
PDF_SIGNING_REQUIRE_CERT_LOWER="$(printf '%s' "${PDF_SIGNING_REQUIRE_CERT_VAL}" | tr '[:upper:]' '[:lower:]')"
if [ "${PDF_SIGNING_REQUIRE_CERT_LOWER}" != "false" ]; then
  PDF_SIGNING_CERT_B64_VAL="$(read_env_value "PDF_SIGNING_CERTIFICATE_BASE64")"
  PDF_SIGNING_CERT_B64_VAL="${PDF_SIGNING_CERT_B64_VAL:-${PDF_SIGNING_CERTIFICATE_BASE64:-}}"
  PDF_SIGNING_CERT_B64_VAL="${PDF_SIGNING_CERT_B64_VAL#"${PDF_SIGNING_CERT_B64_VAL%%[![:space:]]*}"}"
  PDF_SIGNING_CERT_B64_VAL="${PDF_SIGNING_CERT_B64_VAL%"${PDF_SIGNING_CERT_B64_VAL##*[![:space:]]}"}"
  PDF_SIGNING_CERT_PATH_VAL="$(read_env_value "PDF_SIGNING_CERTIFICATE_PATH")"
  PDF_SIGNING_CERT_PATH_VAL="${PDF_SIGNING_CERT_PATH_VAL:-${PDF_SIGNING_CERTIFICATE_PATH:-}}"
  PDF_SIGNING_CERT_PATH_VAL="${PDF_SIGNING_CERT_PATH_VAL#"${PDF_SIGNING_CERT_PATH_VAL%%[![:space:]]*}"}"
  PDF_SIGNING_CERT_PATH_VAL="${PDF_SIGNING_CERT_PATH_VAL%"${PDF_SIGNING_CERT_PATH_VAL##*[![:space:]]}"}"
  PDF_SIGNING_CERT_OK=false
  if [ -n "${PDF_SIGNING_CERT_B64_VAL}" ]; then
    PDF_SIGNING_CERT_OK=true
  elif [ -n "${PDF_SIGNING_CERT_PATH_VAL}" ]; then
    if [ -f "${PDF_SIGNING_CERT_PATH_VAL}" ] && [ -r "${PDF_SIGNING_CERT_PATH_VAL}" ]; then
      PDF_SIGNING_CERT_OK=true
    elif [[ "${PDF_SIGNING_CERT_PATH_VAL}" == /app/certs/* ]]; then
      cert_basename="$(basename "${PDF_SIGNING_CERT_PATH_VAL}")"
      if [ -n "${cert_basename}" ] && [ "${cert_basename}" != "." ] && [ "${cert_basename}" != ".." ]; then
        for candidate_cert in \
          "/opt/oralexam/certs/${cert_basename}" \
          "/etc/oralexam/certs/${cert_basename}" \
          "./certs/${cert_basename}"; do
          if [ -f "${candidate_cert}" ] && [ -r "${candidate_cert}" ]; then
            PDF_SIGNING_CERT_OK=true
            break
          fi
        done
      fi
    fi
  fi
  if [ "${PDF_SIGNING_CERT_OK}" != "true" ]; then
    echo "  ❌ LỖI: Thiếu chứng thư ký số PDF PKI (PDF_SIGNING_CERTIFICATE_BASE64 hoặc tệp đọc được tại PDF_SIGNING_CERTIFICATE_PATH) khi PDF_SIGNING_REQUIRE_CERTIFICATE=${PDF_SIGNING_REQUIRE_CERT_VAL}!" >&2
    echo "     Hãy cấu hình PDF_SIGNING_CERTIFICATE_BASE64 trong .env, mount tệp .pfx tại /opt/oralexam/certs/<ten-file>.pfx hoặc /etc/oralexam/certs/<ten-file>.pfx, hoặc đặt PDF_SIGNING_REQUIRE_CERTIFICATE=false." >&2
    exit 1
  fi
  echo "  ✅ Chứng thư ký số PDF PKI: Hợp lệ cho chế độ Production."
else
  echo "  ℹ️ Chứng thư ký số PDF PKI: PDF_SIGNING_REQUIRE_CERTIFICATE=false (bỏ qua pre-flight .pfx)."
fi

# ------------------------------------------------------------------------------
# BƯỚC 2: SAO LƯU DỮ LIỆU POSTGRESQL AN TOÀN (Safe Database Backup -Fc)
# ------------------------------------------------------------------------------
echo ""
echo "💾 [Bước 2/6] Tự động sao lưu dữ liệu PostgreSQL an toàn..."
mkdir -p "${BACKUP_DIR}"
chmod 700 "${BACKUP_DIR}"

# Đọc thông tin kết nối từ .env (chuẩn hóa mặc định khớp .env.example: oralexam / oralexam_db; cắt comment inline # an toàn)
POSTGRES_USER_VAL="$(read_env_value "POSTGRES_USER")"
POSTGRES_DB_VAL="$(read_env_value "POSTGRES_DB")"
POSTGRES_USER_VAL="${POSTGRES_USER_VAL:-oralexam}"
POSTGRES_DB_VAL="${POSTGRES_DB_VAL:-oralexam_db}"

BACKUP_FILE="${BACKUP_DIR}/oralexam_db_${TIMESTAMP}.dump"

if docker ps --format '{{.Names}}' | grep -qx "oralexam-postgres"; then
  echo "  📦 Đang trích xuất snapshot PostgreSQL (-Fc) vào: ${BACKUP_FILE}..."
  trap 'rm -f "${BACKUP_FILE}.tmp"' EXIT
  if ! (
    umask 077
    docker exec oralexam-postgres sh -c 'pg_dump -Fc -U "$1" -d "$2"' _ "${POSTGRES_USER_VAL}" "${POSTGRES_DB_VAL}" > "${BACKUP_FILE}.tmp"
  ); then
    rm -f "${BACKUP_FILE}.tmp"
    echo "  ❌ LỖI: pg_dump thất bại! Dừng triển khai để bảo vệ dữ liệu." >&2
    exit 1
  fi
  if ! test -s "${BACKUP_FILE}.tmp" || ! docker exec -i oralexam-postgres pg_restore -l < "${BACKUP_FILE}.tmp" >/dev/null; then
    rm -f "${BACKUP_FILE}.tmp"
    echo "  ❌ LỖI: Kiểm tra toàn vẹn bản backup (pg_restore -l) thất bại! Dừng triển khai." >&2
    exit 1
  fi
  mv "${BACKUP_FILE}.tmp" "${BACKUP_FILE}"
  chmod 600 "${BACKUP_FILE}"
  BACKUP_SIZE=$(du -h "${BACKUP_FILE}" | cut -f1)
  echo "  ✅ Sao lưu & kiểm chứng toàn vẹn CSDL hoàn tất (Kích thước: ${BACKUP_SIZE})."
  # Dọn dẹp các bản backup cũ hơn 7 ngày để tối ưu dung lượng đĩa
  find "${BACKUP_DIR}" -maxdepth 1 \( -name "oralexam_db_*.dump" -o -name "oralexam_db_*.sql.gz" \) -type f -mtime +7 -delete 2>/dev/null || true
  echo "  🧹 Đã đồng bộ dọn dẹp các bản backup cũ hơn 7 ngày."
else
  echo "  ℹ️ Container oralexam-postgres hiện chưa chạy. Bỏ qua bước trích xuất pg_dump trực tiếp."
fi

# ------------------------------------------------------------------------------
# BƯỚC 3: GHI NHẬN TRẠNG THÁI ROLLBACK & ĐỒNG BỘ GIT AN TOÀN
# ------------------------------------------------------------------------------
echo ""
echo "🔄 [Bước 3/6] Ghi nhận trạng thái rollback và đồng bộ Git từ origin/${TARGET_BRANCH}..."

PREV_COMMIT_SHA="$(git rev-parse HEAD 2>/dev/null || true)"
PREV_API_IMAGE_ID="$(docker inspect --format '{{.Image}}' oralexam-api 2>/dev/null || true)"
PREV_FRONTEND_IMAGE_ID="$(docker inspect --format '{{.Image}}' oralexam-frontend 2>/dev/null || true)"
PREV_API_IMAGE_NAME="$(docker inspect --format '{{.Config.Image}}' oralexam-api 2>/dev/null || true)"
PREV_FRONTEND_IMAGE_NAME="$(docker inspect --format '{{.Config.Image}}' oralexam-frontend 2>/dev/null || true)"
IS_FRESH_DEPLOY=false

if [ -z "${PREV_API_IMAGE_ID}" ] || [ -z "${PREV_FRONTEND_IMAGE_ID}" ]; then
  IS_FRESH_DEPLOY=true
  echo "  ℹ️ Chưa phát hiện đủ container cũ; đánh dấu phiên này là fresh deploy."
else
  docker tag "${PREV_API_IMAGE_ID}" oralexam-rollback-api:prev
  docker tag "${PREV_FRONTEND_IMAGE_ID}" oralexam-rollback-frontend:prev
  echo "  🧷 Đã snapshot image rollback: ${PREV_API_IMAGE_NAME} & ${PREV_FRONTEND_IMAGE_NAME}"
fi

# Xác định cú pháp compose phù hợp (nếu có compose prod thì override)
if [ -f "docker-compose.prod.yml" ] && [ -f "docker-compose.yml" ]; then
  COMPOSE_CMD=(docker compose -f docker-compose.yml -f docker-compose.prod.yml)
elif [ -f "docker-compose.prod.yml" ]; then
  COMPOSE_CMD=(docker compose -f docker-compose.prod.yml)
else
  COMPOSE_CMD=(docker compose)
fi

rollback_vps() {
  echo "  ⚠️ Triển khai không đạt healthcheck — kích hoạt rollback tự động..." >&2
  docker logs --tail 40 oralexam-api >&2 || true
  docker logs --tail 40 oralexam-frontend >&2 || true

  if [ -n "${PREV_COMMIT_SHA}" ]; then
    git reset --hard "${PREV_COMMIT_SHA}" || true
  fi

  if [ "${IS_FRESH_DEPLOY}" = "true" ]; then
    "${COMPOSE_CMD[@]}" rm -sf web-api frontend >/dev/null 2>&1 || true
    return 0
  fi

  BACKEND_IMAGE=oralexam-rollback-api:prev FRONTEND_IMAGE=oralexam-rollback-frontend:prev \
    "${COMPOSE_CMD[@]}" up -d --no-build --no-deps --force-recreate web-api frontend || true
  if [ -n "${PREV_API_IMAGE_NAME}" ]; then
    docker tag oralexam-rollback-api:prev "${PREV_API_IMAGE_NAME}" || true
  fi
  if [ -n "${PREV_FRONTEND_IMAGE_NAME}" ]; then
    docker tag oralexam-rollback-frontend:prev "${PREV_FRONTEND_IMAGE_NAME}" || true
  fi
}

echo "  📡 Thực hiện git fetch origin ${TARGET_BRANCH} --prune --tags..."
git fetch origin "${TARGET_BRANCH}" --prune --tags

echo "  🌿 Chuyển nhánh ${TARGET_BRANCH}..."
git checkout "${TARGET_BRANCH}"

echo "  🔒 Thực hiện git reset --hard origin/${TARGET_BRANCH} (Chống kẹt commit cũ)..."
git reset --hard "origin/${TARGET_BRANCH}"

echo "  🧹 Dọn dẹp tệp rác không được theo dõi (git clean -fd, bảo toàn certs/*.pfx)..."
git clean -fd -e certs/ -e "*.pfx" -e "*.p12"

CURRENT_COMMIT_HASH="$(git log -1 --format='%h')"
CURRENT_COMMIT_MSG="$(git log -1 --format='%s')"
CURRENT_COMMIT_DATE="$(git log -1 --format='%ci')"
echo "  📌 Mã nguồn hiện tại: [${CURRENT_COMMIT_HASH}] - \"${CURRENT_COMMIT_MSG}\" (${CURRENT_COMMIT_DATE})"

# ------------------------------------------------------------------------------
# BƯỚC 4: TÁI BIÊN DỊCH & KHỞI ĐỘNG CONTAINERS (Build & Up with No-Cache)
# ------------------------------------------------------------------------------
echo ""
echo "🏗️ [Bước 4/6] Tái biên dịch hình ảnh Docker và khởi động dịch vụ..."

echo "  🧪 Kiểm tra tính hợp lệ của cấu hình: ${COMPOSE_CMD[*]} config..."
"${COMPOSE_CMD[@]}" config --quiet
echo "  ✅ Cấu hình Docker Compose hợp lệ."

echo "  🔨 Tái build containers với cờ --no-cache để đảm bảo nạp mã nguồn mới nhất..."
if ! "${COMPOSE_CMD[@]}" build --no-cache; then
  rollback_vps
  exit 1
fi

if [ -d "/etc/letsencrypt/live" ] && [ -d "/etc/letsencrypt/archive" ]; then
  echo "  🔐 Đồng bộ quyền đọc chứng chỉ SSL (/etc/letsencrypt) cho non-root Nginx (GID 101)..."
  chgrp -R 101 /etc/letsencrypt/live /etc/letsencrypt/archive
  chmod -R g+rX /etc/letsencrypt/live /etc/letsencrypt/archive
fi

echo "  🚀 Khởi động các dịch vụ ngầm với cờ up -d --remove-orphans..."
if ! "${COMPOSE_CMD[@]}" up -d --remove-orphans; then
  rollback_vps
  exit 1
fi

# ------------------------------------------------------------------------------
# BƯỚC 5: TỰ ĐỘNG KIỂM TRA SỨC KHỎE HỆ THỐNG (Health Check Probes)
# ------------------------------------------------------------------------------
echo ""
echo "🩺 [Bước 5/6] Tự động kiểm tra sức khỏe hệ thống (Health Check Probes)..."

# 5.1 Kiểm tra trạng thái PostgreSQL
echo "  ⏳ Đang kiểm tra sức khỏe PostgreSQL container..."
PG_HEALTHY=false
for i in {1..30}; do
  PG_STATUS="$(docker inspect --format='{{json .State.Health.Status}}' oralexam-postgres 2>/dev/null || echo 'starting')"
  if [ "${PG_STATUS}" = "\"healthy\"" ]; then
    PG_HEALTHY=true
    echo "  ✅ PostgreSQL container đạt trạng thái healthy (lần kiểm tra ${i}/30)."
    break
  fi
  sleep 2
done

if [ "${PG_HEALTHY}" != "true" ]; then
  echo "  ⚠️ Cảnh báo: PostgreSQL chưa báo healthy sau 60s. Tiếp tục kiểm tra Web API..."
fi

# 5.2 Kiểm tra Backend Web API (/health — 90 x 2s = 180s, khớp deploy_cd_release.sh cho EF migrations + bank importer)
echo "  ⏳ Đang kiểm tra Backend Web API (http://127.0.0.1:5000/health)..."
API_HEALTHY=false
for i in {1..90}; do
  if curl -fsS --max-time 5 http://127.0.0.1:5000/health >/dev/null 2>&1; then
    API_HEALTHY=true
    echo "  ✅ Backend Web API trả về HTTP 200 OK (lần kiểm tra ${i}/90)."
    break
  fi
  sleep 2
done

if [ "${API_HEALTHY}" != "true" ]; then
  echo "  ❌ LỖI: Backend Web API không phản hồi sau 180s!" >&2
  rollback_vps
  exit 1
fi

# 5.3 Kiểm tra Nginx Frontend HTTP (/healthz)
echo "  ⏳ Đang kiểm tra Nginx Frontend probe (http://127.0.0.1:${FRONTEND_HTTP_PORT_VAL}/healthz)..."
FRONTEND_HEALTHY=false
for i in {1..15}; do
  if curl -fsS --max-time 5 "http://127.0.0.1:${FRONTEND_HTTP_PORT_VAL}/healthz" >/dev/null 2>&1; then
    FRONTEND_HEALTHY=true
    echo "  ✅ Nginx Frontend probe /healthz trả về HTTP 200 OK."
    break
  fi
  sleep 2
done

if [ "${FRONTEND_HEALTHY}" != "true" ]; then
  echo "  ❌ LỖI: Nginx Frontend HTTP /healthz không phản hồi!" >&2
  rollback_vps
  exit 1
fi

# 5.4 Kiểm tra HTTPS Domain chuẩn TLS/SNI (/healthz) khi đã có chứng chỉ SSL
if [ -f "/etc/letsencrypt/live/oralexam.io.vn/fullchain.pem" ] || [ "${REQUIRE_HTTPS_HEALTHCHECK:-false}" = "true" ]; then
  echo "  ⏳ Kiểm tra kết nối HTTPS chuẩn TLS (https://oralexam.io.vn/healthz)..."
  if curl -fsS --max-time 10 --resolve "oralexam.io.vn:443:127.0.0.1" "https://oralexam.io.vn/healthz" >/dev/null 2>&1; then
    echo "  ✅ Tên miền https://oralexam.io.vn/healthz phản hồi HTTP 200 thành công."
  else
    echo "  ❌ LỖI: Kiểm tra HTTPS /healthz thất bại!" >&2
    rollback_vps
    exit 1
  fi
else
  echo "  ℹ️ Chưa phát hiện chứng chỉ /etc/letsencrypt/live/oralexam.io.vn/fullchain.pem; bỏ qua kiểm tra HTTPS."
fi

# ------------------------------------------------------------------------------
# BƯỚC 6: DỌN DẸP TÀI NGUYÊN VÀ BÁO CÁO TRẠNG THÁI (Cleanup & Status Report)
# ------------------------------------------------------------------------------
echo ""
echo "🧹 [Bước 6/6] Dọn dẹp tài nguyên thừa và xuất báo cáo trạng thái..."

# Dọn dẹp ảnh dangling images an toàn (không xóa named volumes CSDL)
docker rmi oralexam-rollback-api:prev oralexam-rollback-frontend:prev >/dev/null 2>&1 || true
docker image prune -f >/dev/null 2>&1 || true
docker builder prune -f --keep-storage 2GB >/dev/null 2>&1 || true
echo "  ✅ Đã dọn dẹp Docker images và builder cache an toàn."

echo ""
echo "================================================================================"
echo "📊 DANH SÁCH CONTAINERS ĐANG HOẠT ĐỘNG:"
echo "================================================================================"
"${COMPOSE_CMD[@]}" ps

echo ""
echo "================================================================================"
echo "🎉 TRIỂN KHAI PRODUCTION HOÀN TẤT THÀNH CÔNG!"
echo "📌 Nhánh đã triển khai: ${TARGET_BRANCH}"
echo "📌 Commit SHA: ${CURRENT_COMMIT_HASH} - ${CURRENT_COMMIT_MSG}"
echo "🌐 URL Hệ thống: https://oralexam.io.vn"
echo "📄 File log lưu tại: ${DEPLOY_LOG}"
echo "⏰ Kết thúc lúc: $(date -u '+%Y-%m-%d %H:%M:%S UTC')"
echo "================================================================================"
