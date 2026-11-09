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

# 1.2 Kiểm tra Docker Daemon
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

# 1.4 Kiểm tra dung lượng Swap (Khuyến nghị $\ge$ 2GB trên VPS Vietnix 6GB RAM)
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
  echo "  ✅ Dung lượng đĩa trống: ${AVAILABLE_DISK_GB} GB (Đạt chuẩn an toàn $\ge$ 5GB)."
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

# ------------------------------------------------------------------------------
# BƯỚC 2: SAO LƯU DỮ LIỆU POSTGRESQL AN TOÀN (Safe Database Backup)
# ------------------------------------------------------------------------------
echo ""
echo "💾 [Bước 2/6] Tự động sao lưu dữ liệu PostgreSQL an toàn..."
mkdir -p "${BACKUP_DIR}"

# Đọc thông tin kết nối từ .env nếu có
POSTGRES_USER_VAL="$(grep -E '^POSTGRES_USER=' .env 2>/dev/null | cut -d '=' -f2- | tr -d '\r"' || echo 'postgres')"
POSTGRES_DB_VAL="$(grep -E '^POSTGRES_DB=' .env 2>/dev/null | cut -d '=' -f2- | tr -d '\r"' || echo 'oral_exam_db')"
POSTGRES_USER_VAL="${POSTGRES_USER_VAL:-postgres}"
POSTGRES_DB_VAL="${POSTGRES_DB_VAL:-oral_exam_db}"

BACKUP_FILE="${BACKUP_DIR}/oralexam_db_${TIMESTAMP}.sql.gz"

if docker ps --format '{{.Names}}' | grep -q "^oralexam-postgres$"; then
  echo "  📦 Đang trích xuất snapshot PostgreSQL vào: ${BACKUP_FILE}..."
  if docker exec oralexam-postgres pg_dump -U "${POSTGRES_USER_VAL}" -d "${POSTGRES_DB_VAL}" 2>/dev/null | gzip > "${BACKUP_FILE}"; then
    BACKUP_SIZE=$(du -h "${BACKUP_FILE}" | cut -f1)
    echo "  ✅ Sao lưu CSDL hoàn tất thành công (Kích thước: ${BACKUP_SIZE})."
    # Dọn dẹp các bản backup cũ hơn 7 ngày để tối ưu dung lượng đĩa
    find "${BACKUP_DIR}" -name "oralexam_db_*.sql.gz" -type f -mtime +7 -delete 2>/dev/null || true
    echo "  🧹 Đã đồng bộ dọn dẹp các bản backup cũ hơn 7 ngày."
  else
    echo "  ⚠️ Cảnh báo: Sao lưu pg_dump trả về cảnh báo. Kiểm tra quyền của container postgres."
  fi
else
  echo "  ℹ️ Container oralexam-postgres hiện chưa chạy. Bỏ qua bước trích xuất pg_dump trực tiếp."
fi

# ------------------------------------------------------------------------------
# BƯỚC 3: ĐỒNG BỘ GIT AN TOÀN CHỐNG KẸT COMMIT CŨ (Safe Git Synchronization)
# ------------------------------------------------------------------------------
echo ""
echo "🔄 [Bước 3/6] Đồng bộ Git an toàn từ remote origin/${TARGET_BRANCH}..."

echo "  📡 Thực hiện git fetch origin ${TARGET_BRANCH} --prune --tags..."
git fetch origin "${TARGET_BRANCH}" --prune --tags

echo "  🌿 Chuyển nhánh ${TARGET_BRANCH}..."
git checkout "${TARGET_BRANCH}"

echo "  🔒 Thực hiện git reset --hard origin/${TARGET_BRANCH} (Chống kẹt commit cũ)..."
git reset --hard "origin/${TARGET_BRANCH}"

echo "  🧹 Dọn dẹp tệp rác không được theo dõi (git clean -fd)..."
git clean -fd

CURRENT_COMMIT_HASH="$(git log -1 --format='%h')"
CURRENT_COMMIT_MSG="$(git log -1 --format='%s')"
CURRENT_COMMIT_DATE="$(git log -1 --format='%ci')"
echo "  📌 Mã nguồn hiện tại: [${CURRENT_COMMIT_HASH}] - \"${CURRENT_COMMIT_MSG}\" (${CURRENT_COMMIT_DATE})"

# ------------------------------------------------------------------------------
# BƯỚC 4: TÁI BIÊN DỊCH & KHỞI ĐỘNG CONTAINERS (Build & Up with No-Cache)
# ------------------------------------------------------------------------------
echo ""
echo "🏗️ [Bước 4/6] Tái biên dịch hình ảnh Docker và khởi động dịch vụ..."

# Xác định cú pháp compose phù hợp (nếu có compose prod thì override)
if [ -f "docker-compose.prod.yml" ] && [ -f "docker-compose.yml" ]; then
  COMPOSE_CMD="docker compose -f docker-compose.yml -f docker-compose.prod.yml"
elif [ -f "docker-compose.prod.yml" ]; then
  COMPOSE_CMD="docker compose -f docker-compose.prod.yml"
else
  COMPOSE_CMD="docker compose"
fi

echo "  🧪 Kiểm tra tính hợp lệ của cấu hình: ${COMPOSE_CMD} config..."
${COMPOSE_CMD} config >/dev/null
echo "  ✅ Cấu hình Docker Compose hợp lệ."

echo "  🔨 Tái build containers với cờ --no-cache để đảm bảo nạp mã nguồn mới nhất..."
${COMPOSE_CMD} build --no-cache

echo "  🚀 Khởi động các dịch vụ ngầm với cờ up -d --remove-orphans..."
${COMPOSE_CMD} up -d --remove-orphans

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
  if [ "${PG_STATUS}" == "\"healthy\"" ]; then
    PG_HEALTHY=true
    echo "  ✅ PostgreSQL container đạt trạng thái healthy (lần kiểm tra ${i}/30)."
    break
  fi
  sleep 2
done

if [ "${PG_HEALTHY}" != "true" ]; then
  echo "  ⚠️ Cảnh báo: PostgreSQL chưa báo healthy sau 60s. Tiếp tục kiểm tra Web API..."
fi

# 5.2 Kiểm tra Backend Web API (/health)
echo "  ⏳ Đang kiểm tra Backend Web API (http://127.0.0.1:5000/health)..."
API_HEALTHY=false
for i in {1..30}; do
  if curl -s -f http://127.0.0.1:5000/health >/dev/null 2>&1; then
    API_HEALTHY=true
    echo "  ✅ Backend Web API trả về HTTP 200 OK (lần kiểm tra ${i}/30)."
    break
  fi
  sleep 2
done

if [ "${API_HEALTHY}" != "true" ]; then
  echo "  ❌ LỖI: Backend Web API không phản hồi sau 60s! Trích xuất nhật ký lỗi gần nhất:" >&2
  docker logs --tail 40 oralexam-api >&2 || true
  exit 1
fi

# 5.3 Kiểm tra Nginx Frontend (/healthz)
echo "  ⏳ Đang kiểm tra Nginx Frontend probe (http://127.0.0.1/healthz)..."
if curl -s -f http://127.0.0.1/healthz >/dev/null 2>&1; then
  echo "  ✅ Nginx Frontend probe /healthz trả về HTTP 200 OK."
elif curl -s -f http://127.0.0.1:80/healthz >/dev/null 2>&1; then
  echo "  ✅ Nginx Frontend probe /healthz trên cổng 80 trả về HTTP 200 OK."
else
  echo "  ⚠️ Chú ý: Kiểm tra truy cập Nginx cổng 80/443."
fi

# 5.4 Kiểm tra HTTPS Domain công khai (https://oralexam.io.vn)
echo "  ⏳ Kiểm tra kết nối tên miền công cộng (https://oralexam.io.vn)..."
if curl -s -k -f --connect-timeout 5 https://oralexam.io.vn/ >/dev/null 2>&1; then
  echo "  ✅ Tên miền https://oralexam.io.vn phản hồi HTTP 200 thành công."
else
  echo "  ℹ️ Chưa phản hồi HTTPS công cộng (Có thể đang chờ trỏ DNS hoặc chứng chỉ SSL nội bộ)."
fi

# ------------------------------------------------------------------------------
# BƯỚC 6: DỌN DẸP TÀI NGUYÊN VÀ BÁO CÁO TRẠNG THÁI (Cleanup & Status Report)
# ------------------------------------------------------------------------------
echo ""
echo "🧹 [Bước 6/6] Dọn dẹp tài nguyên thừa và xuất báo cáo trạng thái..."

# Dọn dẹp ảnh dangling images an toàn (không xóa named volumes CSDL)
docker image prune -f >/dev/null 2>&1 || true
docker builder prune -f --keep-storage 2GB >/dev/null 2>&1 || true
echo "  ✅ Đã dọn dẹp Docker images và builder cache an toàn."

echo ""
echo "================================================================================"
echo "📊 DANH SÁCH CONTAINERS ĐANG HOẠT ĐỘNG:"
echo "================================================================================"
${COMPOSE_CMD} ps

echo ""
echo "================================================================================"
echo "🎉 TRIỂN KHAI PRODUCTION HOÀN TẤT THÀNH CÔNG!"
echo "📌 Nhánh đã triển khai: ${TARGET_BRANCH}"
echo "📌 Commit SHA: ${CURRENT_COMMIT_HASH} - ${CURRENT_COMMIT_MSG}"
echo "🌐 URL Hệ thống: https://oralexam.io.vn"
echo "📄 File log lưu tại: ${DEPLOY_LOG}"
echo "⏰ Kết thúc lúc: $(date -u '+%Y-%m-%d %H:%M:%S UTC')"
echo "================================================================================"
