#!/usr/bin/env bash
# ==============================================================================
# Oral Exam System — Runtime Diagnostics & Alerting Script (AZ-N19)
# Consumes GET /api/v1/admin/diagnostics/runtime (camelCase JSON)
# ==============================================================================
set -euo pipefail

BASE_URL="${ORALEXAM_API_BASE_URL:-http://127.0.0.1:5000}"
BASE_URL="${BASE_URL%/}"

MAX_WORKING_SET_MB="${MAX_WORKING_SET_MB:-2048}"
MAX_5XX_COUNT="${MAX_5XX_COUNT:-10}"
MAX_5XX_RATE_PCT="${MAX_5XX_RATE_PCT:-5.0}"
MAX_DLQ_PENDING="${MAX_DLQ_PENDING:-0}"
MAX_QUEUE_PCT="${MAX_QUEUE_PCT:-80}"
CHECK_HOST_RAM="${CHECK_HOST_RAM:-false}"
MAX_HOST_RAM_MB="${MAX_HOST_RAM_MB:-4500}"

command -v curl >/dev/null 2>&1 || { echo "curl is required" >&2; exit 1; }
PYTHON_BIN=""
for candidate in python3 python; do
  if command -v "${candidate}" >/dev/null 2>&1 && "${candidate}" -c 'import json' >/dev/null 2>&1; then
    PYTHON_BIN="$(command -v "${candidate}")"
    break
  fi
done
if [[ -z "${PYTHON_BIN}" ]]; then
  echo "python3 or python is required for JSON parsing" >&2
  exit 1
fi

send_alert() {
  local severity="$1"
  local message="$2"

  if [[ -n "${TELEGRAM_BOT_TOKEN:-}" && -n "${TELEGRAM_CHAT_ID:-}" ]]; then
    local tg_payload
    tg_payload="$("${PYTHON_BIN}" -c 'import json,sys; print(json.dumps({"chat_id": sys.argv[1], "text": f"[{sys.argv[2]}] OralExam Runtime Alert:\n{sys.argv[3]}"}))' "${TELEGRAM_CHAT_ID}" "${severity}" "${message}")"
    curl -fsS --max-time 10 -H "Content-Type: application/json" -d "${tg_payload}" \
      "https://api.telegram.org/bot${TELEGRAM_BOT_TOKEN}/sendMessage" >/dev/null || \
      echo "WARN: Failed to send Telegram alert" >&2
  fi

  if [[ -n "${ALERT_WEBHOOK_URL:-}" ]]; then
    local wh_payload
    wh_payload="$("${PYTHON_BIN}" -c 'import json,sys; print(json.dumps({"service": "oralexam-api", "severity": sys.argv[1], "text": sys.argv[2]}))' "${severity}" "${message}")"
    curl -fsS --max-time 10 -H "Content-Type: application/json" -d "${wh_payload}" \
      "${ALERT_WEBHOOK_URL}" >/dev/null || \
      echo "WARN: Failed to send webhook alert" >&2
  fi

  echo "[${severity}] ${message}" >&2
}

token="${ORALEXAM_ADMIN_TOKEN:-}"
if [[ -z "${token}" ]]; then
  if [[ -z "${ORALEXAM_ADMIN_USERNAME:-}" || -z "${ORALEXAM_ADMIN_PASSWORD:-}" ]]; then
    echo "Set ORALEXAM_ADMIN_TOKEN or both ORALEXAM_ADMIN_USERNAME and ORALEXAM_ADMIN_PASSWORD." >&2
    exit 2
  fi
  login_payload="$("${PYTHON_BIN}" -c 'import json,sys; print(json.dumps({"username": sys.argv[1], "password": sys.argv[2]}))' "${ORALEXAM_ADMIN_USERNAME}" "${ORALEXAM_ADMIN_PASSWORD}")"
  if ! login_resp="$(curl -fsS --max-time 10 -H "Content-Type: application/json" -d "${login_payload}" "${BASE_URL}/api/v1/auth/password-login")"; then
    send_alert "CRITICAL" "Admin login failed at ${BASE_URL}/api/v1/auth/password-login"
    exit 1
  fi
  set +e
  token="$("${PYTHON_BIN}" -c '
import json, sys
data = json.loads(sys.stdin.read())
if data.get("mustChangePassword") is True:
    print("MUST_CHANGE_PASSWORD", file=sys.stderr)
    sys.exit(3)
tok = data.get("accessToken", "")
if not tok:
    sys.exit(4)
print(tok)
' <<< "${login_resp}")"
  login_parse_rc=$?
  set -e
  if [[ ${login_parse_rc} -eq 3 ]]; then
    send_alert "CRITICAL" "Admin login blocked: mustChangePassword is true at ${BASE_URL}/api/v1/auth/password-login"
    exit 3
  elif [[ ${login_parse_rc} -ne 0 || -z "${token}" ]]; then
    send_alert "CRITICAL" "Admin login failed: missing accessToken (rc=${login_parse_rc}) at ${BASE_URL}/api/v1/auth/password-login"
    exit 4
  fi
fi

if ! diag_json="$(curl -fsS --max-time 10 -H "Authorization: Bearer ${token}" "${BASE_URL}/api/v1/admin/diagnostics/runtime")"; then
  send_alert "CRITICAL" "Diagnostics endpoint unreachable or returned non-200: ${BASE_URL}/api/v1/admin/diagnostics/runtime"
  exit 1
fi

host_used_mb="0"
if [[ "${CHECK_HOST_RAM}" == "true" ]] && command -v free >/dev/null 2>&1; then
  host_used_mb="$(free -m | awk '/Mem:/ {print $3}' || echo 0)"
fi

set +e
eval_output="$(DIAG_JSON="${diag_json}" "${PYTHON_BIN}" - "${MAX_WORKING_SET_MB}" "${MAX_5XX_COUNT}" "${MAX_5XX_RATE_PCT}" "${MAX_DLQ_PENDING}" "${MAX_QUEUE_PCT}" "${CHECK_HOST_RAM}" "${MAX_HOST_RAM_MB}" "${host_used_mb}" <<'PY'
import json, os, sys

max_ws = float(sys.argv[1])
max_5xx = int(sys.argv[2])
max_5xx_rate = float(sys.argv[3])
max_dlq = int(sys.argv[4])
max_queue_pct = float(sys.argv[5])
check_host_ram = sys.argv[6].lower() == "true"
max_host_ram = int(sys.argv[7])
host_used_mb = int(sys.argv[8])

d = json.loads(os.environ["DIAG_JSON"])
sys_info = d["system"]
perf = d["performance"]
dlq = d["deadLetterQueue"]
by_flow = dlq.get("byFlowType", {})
gq = d["boundedChannels"]["gradingQueue"]

ws_mb = float(sys_info["workingSetMb"])
err_5xx = int(perf["serverErrors5xx"])
err_rate = float(perf["serverErrorRatePercent"])
dlq_pending = int(dlq["totalPending"])
q_count = int(gq["count"])
q_cap = max(int(gq["capacity"]), 1)
q_pct = (q_count * 100.0) / q_cap

alerts = []
if ws_mb >= max_ws:
    alerts.append(f"API workingSetMb={ws_mb:.1f}MB >= {max_ws:.0f}MB")
if err_5xx > max_5xx or err_rate > max_5xx_rate:
    alerts.append(f"5xx errors={err_5xx} (rate={err_rate:.2f}%) exceeded threshold ({max_5xx}, {max_5xx_rate:.2f}%)")
if dlq_pending > max_dlq:
    alerts.append(
        f"DLQ pending={dlq_pending} > {max_dlq} "
        f"(practice={by_flow.get('practice', 0)}, mockExam={by_flow.get('mockExam', 0)}, inClassExam={by_flow.get('inClassExam', 0)}, officialExam={by_flow.get('officialExam', 0)})"
    )
if q_pct >= max_queue_pct:
    alerts.append(f"BoundedChannel gradingQueue={q_count}/{q_cap} ({q_pct:.1f}%) >= {max_queue_pct:.0f}%")
if check_host_ram and host_used_mb > max_host_ram:
    alerts.append(f"Host RAM used={host_used_mb}MB > {max_host_ram}MB")

if alerts:
    print(" | ".join(alerts))
    sys.exit(10)

print(
    f"OK timestamp={d.get('timestamp')} workingSetMb={ws_mb:.1f} "
    f"5xx={err_5xx} ({err_rate:.2f}%) dlqPending={dlq_pending} "
    f"gradingQueue={q_count}/{q_cap} ({q_pct:.1f}%)"
)
PY
)"
eval_rc=$?
set -e

if [[ ${eval_rc} -eq 10 ]]; then
  send_alert "WARNING" "${eval_output}"
  exit 1
elif [[ ${eval_rc} -ne 0 ]]; then
  send_alert "CRITICAL" "Failed to parse runtime diagnostics JSON (${eval_rc}): ${eval_output}"
  exit 1
fi

echo "${eval_output}"
