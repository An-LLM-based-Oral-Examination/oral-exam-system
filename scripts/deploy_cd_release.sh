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
  local raw=""
  raw="$(grep -E "^${key}=" .env 2>/dev/null | tail -n1 | cut -d '=' -f2- | tr -d '\r' || true)"
  raw="${raw#"${raw%%[![:space:]]*}"}"
  if [[ -z "$raw" ]]; then
    printf '%s' ""
    return 0
  fi
  if [[ "$raw" == \"* ]]; then
    raw="${raw#\"}"
    raw="${raw%%\"*}"
  elif [[ "$raw" == \'* ]]; then
    raw="${raw#\'}"
    raw="${raw%%\'*}"
  else
    raw="${raw%%#*}"
  fi
  raw="${raw#"${raw%%[![:space:]]*}"}"
  raw="${raw%"${raw##*[![:space:]]}"}"
  printf '%s' "$raw"
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
previous_backend_image="$(docker inspect --format '{{.Config.Image}}' oralexam-api 2>/dev/null || true)"
previous_frontend_image="$(docker inspect --format '{{.Config.Image}}' oralexam-frontend 2>/dev/null || true)"
is_fresh_deploy=false
if [[ -z "$previous_backend_image" || -z "$previous_frontend_image" ]]; then
  is_fresh_deploy=true
  echo "No existing API/frontend containers detected; treating as fresh deploy."
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

docker exec oralexam-postgres sh -c 'pg_dump -Fc -U "$POSTGRES_USER" -d "$POSTGRES_DB"' > "${backup_file}.tmp"
test -s "${backup_file}.tmp"
docker exec -i oralexam-postgres pg_restore -l < "${backup_file}.tmp" >/dev/null
mv "${backup_file}.tmp" "$backup_file"
chmod 600 "$backup_file"

env_backup="${backup_file}.env"
cp .env "$env_backup"
chmod 600 "$env_backup"

find "$backup_dir" -maxdepth 1 \( -name 'oralexam-before-release-*.dump' -o -name 'oralexam-before-release-*.dump.env' \) -type f -mtime +7 -delete 2>/dev/null || true

export BACKEND_IMAGE="$backend_image"
export FRONTEND_IMAGE="$frontend_image"
"${compose[@]}" config --quiet
"${compose[@]}" pull web-api frontend

rollback() {
  echo "Release healthcheck failed; restoring previous state" >&2
  git checkout --detach "$previous_sha"
  if [[ "$is_fresh_deploy" == "true" ]]; then
    "${compose[@]}" rm -sf web-api frontend >/dev/null 2>&1 || true
    return 0
  fi
  export BACKEND_IMAGE="$previous_backend_image"
  export FRONTEND_IMAGE="$previous_frontend_image"
  "${compose[@]}" up -d --no-build --no-deps --force-recreate web-api frontend
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

echo "Release ${release_sha} healthy; database backup: ${backup_file}"
