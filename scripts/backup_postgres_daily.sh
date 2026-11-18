#!/usr/bin/env bash
# ==============================================================================
# Oral Exam System — Daily PostgreSQL Backup (-Fc) & Retention Pruning (AZ-N02)
# ==============================================================================
set -euo pipefail

DEPLOY_DIR="${DEPLOY_DIR:-/opt/oralexam}"
BACKUP_DIR="${BACKUP_DIR:-${DEPLOY_DIR}/backups/daily}"
RETENTION_DAYS="${BACKUP_RETENTION_DAYS:-7}"
POSTGRES_CONTAINER="${POSTGRES_CONTAINER:-oralexam-postgres}"
LOCK_FILE="${LOCK_FILE:-/tmp/oralexam-postgres-backup.lock}"

if command -v flock >/dev/null 2>&1; then
  exec 200>"${LOCK_FILE}"
  if ! flock -n 200; then
    echo "Another daily backup process is already running (${LOCK_FILE}); exiting." >&2
    exit 1
  fi
fi

ENV_FILE="${DEPLOY_DIR}/.env"
if [[ -r "${ENV_FILE}" ]]; then
  POSTGRES_USER_VAL="$(grep -E '^POSTGRES_USER=' "${ENV_FILE}" 2>/dev/null | tail -n1 | cut -d '=' -f2- | tr -d '\r"'\''' || true)"
  POSTGRES_DB_VAL="$(grep -E '^POSTGRES_DB=' "${ENV_FILE}" 2>/dev/null | tail -n1 | cut -d '=' -f2- | tr -d '\r"'\''' || true)"
else
  POSTGRES_USER_VAL="${POSTGRES_USER:-}"
  POSTGRES_DB_VAL="${POSTGRES_DB:-}"
fi

POSTGRES_USER_VAL="${POSTGRES_USER_VAL:-oralexam}"
POSTGRES_DB_VAL="${POSTGRES_DB_VAL:-oralexam_db}"

if ! docker ps --format '{{.Names}}' | grep -qx "${POSTGRES_CONTAINER}"; then
  echo "Container ${POSTGRES_CONTAINER} is not running; cannot create daily backup." >&2
  exit 1
fi

mkdir -p "${BACKUP_DIR}"
chmod 700 "${BACKUP_DIR}"

timestamp="$(date -u +%Y%m%dT%H%M%SZ)"
backup_file="${BACKUP_DIR}/oralexam_daily_${timestamp}.dump"
tmp_file="${backup_file}.tmp"
trap 'rm -f "${tmp_file}"' EXIT

docker exec "${POSTGRES_CONTAINER}" sh -c 'pg_dump -Fc -U "$1" -d "$2"' _ "${POSTGRES_USER_VAL}" "${POSTGRES_DB_VAL}" > "${tmp_file}"
test -s "${tmp_file}"
docker exec -i "${POSTGRES_CONTAINER}" pg_restore -l < "${tmp_file}" >/dev/null

mv "${tmp_file}" "${backup_file}"
chmod 600 "${backup_file}"

find "${BACKUP_DIR}" -maxdepth 1 -name 'oralexam_daily_*.dump' -type f -mtime +"${RETENTION_DAYS}" -delete 2>/dev/null || true

backup_size="$(du -h "${backup_file}" | cut -f1)"
echo "Daily PostgreSQL backup complete: ${backup_file} (${backup_size}), retention > ${RETENTION_DAYS}d pruned."
