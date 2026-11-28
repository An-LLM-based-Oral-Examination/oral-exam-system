#!/usr/bin/env bash
# Invoked only by the manual, CI-gated CD workflow on the VPS.
set -euo pipefail

if [[ $# -ne 4 ]]; then
  echo "Usage: deploy_cd_release.sh <commit-sha> <backend-image> <frontend-image> <previous-sha>" >&2
  exit 2
fi

release_sha="$1"
backend_image="$2"
frontend_image="$3"
previous_sha="$4"
if [[ ! "$release_sha" =~ ^[a-f0-9]{40}$ ]]; then
  echo "Invalid release SHA" >&2
  exit 2
fi
if [[ ! "$previous_sha" =~ ^[a-f0-9]{40}$ ]]; then
  echo "Invalid previous SHA" >&2
  exit 2
fi

command -v curl >/dev/null 2>&1 || { echo "curl is required" >&2; exit 1; }

cd /opt/oralexam
if [[ "$(git rev-parse HEAD)" != "$release_sha" ]] || ! git cat-file -e "${previous_sha}^{commit}"; then
  echo "Release or previous commit is unavailable" >&2
  exit 1
fi
test -r .env || { echo "Missing or unreadable production .env" >&2; exit 1; }
test -f docker-compose.yml || { echo "Missing compose file" >&2; exit 1; }
test -f docker-compose.prod.yml || { echo "Missing production compose override" >&2; exit 1; }
docker info >/dev/null

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

frontend_http_port="$(read_env_value "FRONTEND_HTTP_PORT")"
frontend_http_port="${frontend_http_port:-80}"

pdf_require_cert="$(read_env_value "PDF_SIGNING_REQUIRE_CERTIFICATE")"
pdf_require_cert="${pdf_require_cert:-${PDF_SIGNING_REQUIRE_CERTIFICATE:-true}}"
pdf_require_cert="${pdf_require_cert#"${pdf_require_cert%%[![:space:]]*}"}"
pdf_require_cert="${pdf_require_cert%"${pdf_require_cert##*[![:space:]]}"}"
pdf_require_cert_lower="$(printf '%s' "$pdf_require_cert" | tr '[:upper:]' '[:lower:]')"
if [[ "$pdf_require_cert_lower" != "false" ]]; then
  pdf_cert_b64="$(read_env_value "PDF_SIGNING_CERTIFICATE_BASE64")"
  pdf_cert_b64="${pdf_cert_b64:-${PDF_SIGNING_CERTIFICATE_BASE64:-}}"
  pdf_cert_b64="${pdf_cert_b64#"${pdf_cert_b64%%[![:space:]]*}"}"
  pdf_cert_b64="${pdf_cert_b64%"${pdf_cert_b64##*[![:space:]]}"}"
  pdf_cert_path="$(read_env_value "PDF_SIGNING_CERTIFICATE_PATH")"
  pdf_cert_path="${pdf_cert_path:-${PDF_SIGNING_CERTIFICATE_PATH:-}}"
  pdf_cert_path="${pdf_cert_path#"${pdf_cert_path%%[![:space:]]*}"}"
  pdf_cert_path="${pdf_cert_path%"${pdf_cert_path##*[![:space:]]}"}"
  pdf_cert_ok=false
  if [[ -n "$pdf_cert_b64" ]]; then
    pdf_cert_ok=true
  elif [[ -n "$pdf_cert_path" ]]; then
    if [[ -f "$pdf_cert_path" && -r "$pdf_cert_path" ]]; then
      pdf_cert_ok=true
    elif [[ "$pdf_cert_path" == /app/certs/* ]]; then
      cert_basename="$(basename "$pdf_cert_path")"
      if [[ -n "$cert_basename" && "$cert_basename" != "." && "$cert_basename" != ".." ]]; then
        for candidate_cert in \
          "/opt/oralexam/certs/${cert_basename}" \
          "/etc/oralexam/certs/${cert_basename}" \
          "./certs/${cert_basename}"; do
          if [[ -f "$candidate_cert" && -r "$candidate_cert" ]]; then
            pdf_cert_ok=true
            break
          fi
        done
      fi
    fi
  fi
  if [[ "$pdf_cert_ok" != "true" ]]; then
    echo "Missing PDF signing PKI certificate (PDF_SIGNING_CERTIFICATE_BASE64 or readable host file for PDF_SIGNING_CERTIFICATE_PATH) while PDF_SIGNING_REQUIRE_CERTIFICATE=${pdf_require_cert}" >&2
    exit 1
  fi
fi

compose=(docker compose -f docker-compose.yml -f docker-compose.prod.yml)
pg_user="$(read_env_value "POSTGRES_USER")"
pg_db="$(read_env_value "POSTGRES_DB")"
pg_user="${pg_user:-oralexam}"
pg_db="${pg_db:-oralexam_db}"

previous_backend_image_id="$(docker inspect --format '{{.Image}}' oralexam-api 2>/dev/null || true)"
previous_frontend_image_id="$(docker inspect --format '{{.Image}}' oralexam-frontend 2>/dev/null || true)"
previous_backend_image_name="$(docker inspect --format '{{.Config.Image}}' oralexam-api 2>/dev/null || true)"
previous_frontend_image_name="$(docker inspect --format '{{.Config.Image}}' oralexam-frontend 2>/dev/null || true)"
is_fresh_deploy=false
if [[ -z "$previous_backend_image_id" || -z "$previous_frontend_image_id" ]]; then
  is_fresh_deploy=true
  echo "No existing API/frontend containers detected; treating as fresh deploy."
else
  docker tag "$previous_backend_image_id" oralexam-rollback-backend:prev
  docker tag "$previous_frontend_image_id" oralexam-rollback-frontend:prev
  echo "Tagged rollback images: ${previous_backend_image_name:-$previous_backend_image_id} & ${previous_frontend_image_name:-$previous_frontend_image_id}"
fi

wait_healthy() {
  local container="$1"
  for ((attempt=1; attempt<=90; attempt++)); do
    if [[ "$(docker inspect --format '{{.State.Health.Status}}' "$container" 2>/dev/null || true)" == healthy ]]; then
      return 0
    fi
    sleep 2
  done
  return 1
}

backup_dir=/opt/oralexam/backups
mkdir -p "$backup_dir"
chmod 700 "$backup_dir"
backup_file="$backup_dir/oralexam-before-release-${release_sha}-$(date -u +%Y%m%dT%H%M%SZ).dump"

if ! docker ps --format '{{.Names}}' | grep -qx 'oralexam-postgres'; then
  echo "PostgreSQL container not running; starting postgres service before pre-release backup..."
  "${compose[@]}" up -d postgres
  wait_healthy oralexam-postgres
fi

trap 'rm -f "${backup_file}.tmp"' EXIT
(
  umask 077
  docker exec oralexam-postgres sh -c 'pg_dump -Fc -U "$POSTGRES_USER" -d "$POSTGRES_DB"' > "${backup_file}.tmp"
)
test -s "${backup_file}.tmp"
docker exec -i oralexam-postgres pg_restore -l < "${backup_file}.tmp" >/dev/null
mv "${backup_file}.tmp" "$backup_file"
chmod 600 "$backup_file"

env_backup="${backup_file}.env"
(umask 077 && cp .env "$env_backup")
chmod 600 "$env_backup"

find "$backup_dir" -maxdepth 1 \( -name 'oralexam-before-release-*.dump' -o -name 'oralexam-before-release-*.dump.env' \) -type f -mtime +7 -delete 2>/dev/null || true

export BACKEND_IMAGE="$backend_image"
export FRONTEND_IMAGE="$frontend_image"
"${compose[@]}" config --quiet
"${compose[@]}" pull web-api frontend

rollback() {
  echo "Release healthcheck failed; dumping container logs and restoring previous state..." >&2
  "${compose[@]}" logs --tail=40 web-api frontend >&2 || true
  git checkout --detach "$previous_sha" || true
  echo "NOTE: If forward-only EF Core migrations already ran on PostgreSQL, restore the pre-release backup with:" >&2
  echo "  docker exec -i oralexam-postgres pg_restore -c --if-exists -U \"${pg_user}\" -d \"${pg_db}\" < \"${backup_file}\"" >&2
  if [[ "$is_fresh_deploy" == "true" ]]; then
    "${compose[@]}" rm -sf web-api frontend >/dev/null 2>&1 || true
    return 0
  fi
  export BACKEND_IMAGE="oralexam-rollback-backend:prev"
  export FRONTEND_IMAGE="oralexam-rollback-frontend:prev"
  "${compose[@]}" up -d --no-build --no-deps --force-recreate web-api frontend || true
  if [[ -n "$previous_backend_image_name" ]]; then
    docker tag oralexam-rollback-backend:prev "$previous_backend_image_name" || true
  fi
  if [[ -n "$previous_frontend_image_name" ]]; then
    docker tag oralexam-rollback-frontend:prev "$previous_frontend_image_name" || true
  fi
  wait_healthy oralexam-api && wait_healthy oralexam-frontend
}

if ! "${compose[@]}" up -d --no-build --no-deps --force-recreate web-api; then
  rollback
  exit 1
fi
if ! wait_healthy oralexam-api; then
  rollback
  exit 1
fi
if [[ -d /etc/letsencrypt/live && -d /etc/letsencrypt/archive ]]; then
  chgrp -R 101 /etc/letsencrypt/live /etc/letsencrypt/archive
  chmod -R g+rX /etc/letsencrypt/live /etc/letsencrypt/archive
fi
if ! "${compose[@]}" up -d --no-build --no-deps --force-recreate frontend; then
  rollback
  exit 1
fi
if ! wait_healthy oralexam-frontend; then
  rollback
  exit 1
fi

if ! curl -fsS --max-time 5 http://127.0.0.1:5000/health >/dev/null; then
  rollback
  exit 1
fi
if ! curl -fsS --max-time 5 "http://127.0.0.1:${frontend_http_port}/healthz" >/dev/null; then
  rollback
  exit 1
fi
if [[ -f /etc/letsencrypt/live/oralexam.io.vn/fullchain.pem || "${REQUIRE_HTTPS_HEALTHCHECK:-false}" == "true" ]]; then
  if ! curl -fsS --max-time 10 --resolve "oralexam.io.vn:443:127.0.0.1" "https://oralexam.io.vn/healthz" >/dev/null; then
    rollback
    exit 1
  fi
fi

docker rmi oralexam-rollback-backend:prev oralexam-rollback-frontend:prev >/dev/null 2>&1 || true

echo "Release ${release_sha} healthy; database backup: ${backup_file}"
