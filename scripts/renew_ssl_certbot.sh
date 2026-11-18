#!/usr/bin/env bash
# ==============================================================================
# Oral Exam System — Let's Encrypt SSL Renewal & Non-Root Nginx Reload (AZ-N02)
# - Ensures /etc/letsencrypt/{live,archive} is readable by container GID 101 (nginx)
# - Runs certbot renew via webroot (/var/www/certbot)
# - Reloads oralexam-frontend and verifies strict HTTPS /healthz (no -k)
# ==============================================================================
set -euo pipefail

DOMAIN="${ORALEXAM_DOMAIN:-oralexam.io.vn}"
CERTBOT_WEBROOT="${CERTBOT_WEBROOT:-/var/www/certbot}"
FRONTEND_CONTAINER="${FRONTEND_CONTAINER:-oralexam-frontend}"
LETSENCRYPT_DIR="${LETSENCRYPT_DIR:-/etc/letsencrypt}"
NGINX_GID="${NGINX_GID:-101}"
CERTBOT_USE_DOCKER="${CERTBOT_USE_DOCKER:-false}"
SYNC_ONLY=false

if [[ "${1:-}" == "--sync-permissions-only" ]]; then
  SYNC_ONLY=true
fi

command -v curl >/dev/null 2>&1 || { echo "curl is required" >&2; exit 1; }
command -v docker >/dev/null 2>&1 || { echo "docker is required" >&2; exit 1; }

sync_cert_permissions_and_reload() {
  if [[ -d "${LETSENCRYPT_DIR}/live" && -d "${LETSENCRYPT_DIR}/archive" ]]; then
    chgrp -R "${NGINX_GID}" "${LETSENCRYPT_DIR}/live" "${LETSENCRYPT_DIR}/archive"
    chmod -R g+rX "${LETSENCRYPT_DIR}/live" "${LETSENCRYPT_DIR}/archive"
  fi
  if docker ps --format '{{.Names}}' | grep -qx "${FRONTEND_CONTAINER}"; then
    docker exec "${FRONTEND_CONTAINER}" nginx -t
    docker exec "${FRONTEND_CONTAINER}" nginx -s reload
  fi
}

if [[ "$SYNC_ONLY" == "true" ]]; then
  sync_cert_permissions_and_reload
else
  mkdir -p "${CERTBOT_WEBROOT}"
  hook_cmd="if [ -d '${LETSENCRYPT_DIR}/live' ] && [ -d '${LETSENCRYPT_DIR}/archive' ]; then chgrp -R '${NGINX_GID}' '${LETSENCRYPT_DIR}/live' '${LETSENCRYPT_DIR}/archive' && chmod -R g+rX '${LETSENCRYPT_DIR}/live' '${LETSENCRYPT_DIR}/archive'; fi && if docker ps --format '{{.Names}}' | grep -qx '${FRONTEND_CONTAINER}'; then docker exec '${FRONTEND_CONTAINER}' nginx -t && docker exec '${FRONTEND_CONTAINER}' nginx -s reload; fi"

  if [[ "${CERTBOT_USE_DOCKER}" == "true" ]]; then
    docker run --rm \
      -v "${LETSENCRYPT_DIR}:/etc/letsencrypt" \
      -v "${CERTBOT_WEBROOT}:/var/www/certbot" \
      certbot/certbot renew --webroot -w /var/www/certbot --non-interactive
    sync_cert_permissions_and_reload
  else
    command -v certbot >/dev/null 2>&1 || { echo "certbot binary not found (or set CERTBOT_USE_DOCKER=true)" >&2; exit 1; }
    certbot renew --webroot -w "${CERTBOT_WEBROOT}" --non-interactive --deploy-hook "$hook_cmd"
    sync_cert_permissions_and_reload
  fi
fi

if ! curl -fsS --max-time 10 --resolve "${DOMAIN}:443:127.0.0.1" "https://${DOMAIN}/healthz" >/dev/null; then
  echo "HTTPS healthz check failed for https://${DOMAIN}/healthz after SSL renewal/reload" >&2
  exit 1
fi

echo "SSL certificate renewal & HTTPS /healthz verification succeeded for ${DOMAIN}."
