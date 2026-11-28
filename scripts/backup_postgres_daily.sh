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

ENV_FILE="${DEPLOY_DIR}/.env"
if [[ -r "${ENV_FILE}" ]]; then
  POSTGRES_USER_VAL="$(read_env_value "POSTGRES_USER" "${ENV_FILE}")"
  POSTGRES_DB_VAL="$(read_env_value "POSTGRES_DB" "${ENV_FILE}")"
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

(
  umask 077
  docker exec "${POSTGRES_CONTAINER}" sh -c 'pg_dump -Fc -U "$1" -d "$2"' _ "${POSTGRES_USER_VAL}" "${POSTGRES_DB_VAL}" > "${tmp_file}"
)
test -s "${tmp_file}"
docker exec -i "${POSTGRES_CONTAINER}" pg_restore -l < "${tmp_file}" >/dev/null

mv "${tmp_file}" "${backup_file}"
chmod 600 "${backup_file}"

find "${BACKUP_DIR}" -maxdepth 1 -name 'oralexam_daily_*.dump' -type f -mtime +"${RETENTION_DAYS}" -delete 2>/dev/null || true

backup_size="$(du -h "${backup_file}" | cut -f1)"
echo "Daily PostgreSQL backup complete: ${backup_file} (${backup_size}), retention > ${RETENTION_DAYS}d pruned."
