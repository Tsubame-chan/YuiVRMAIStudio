#!/usr/bin/env bash
set -euo pipefail

BACKEND_PORT="${BACKEND_PORT:-8000}"
VOICEVOX_PORT="${VOICEVOX_PORT:-50021}"
AIVIS_PORT="${AIVIS_PORT:-10101}"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

load_env_file() {
  local env_file="$REPO_ROOT/.env"
  [[ -f "$env_file" ]] || return 0
  local line key value
  while IFS= read -r line || [[ -n "$line" ]]; do
    [[ -z "$line" || "$line" == \#* || "$line" != *=* ]] && continue
    key="${line%%=*}"
    value="${line#*=}"
    [[ "$key" =~ ^[A-Za-z_][A-Za-z0-9_]*$ ]] || continue
    [[ -n "${!key:-}" ]] && continue
    export "$key=$value"
  done < "$env_file"
}

url_port() {
  local url="$1"
  url="${url#http://}"
  url="${url#https://}"
  url="${url%%/*}"
  url="${url##*@}"
  if [[ "$url" == *:* ]]; then
    printf '%s\n' "${url##*:}"
    return 0
  fi
  case "$1" in
    https://*) printf '443\n' ;;
    *) printf '80\n' ;;
  esac
}

is_irodori_configured() {
  local enabled="${IRODORI_ENABLE:-auto}"
  [[ "$enabled" == "0" || "$enabled" == "false" || "$enabled" == "False" ]] && return 1
  [[ "$enabled" == "1" || "$enabled" == "true" || "$enabled" == "True" ]] && return 0

  local provider_key
  provider_key="$(printf '%s' "${HTTP_TTS_PROVIDER_ID:-}" | tr '[:upper:]' '[:lower:]')"
  [[ "$provider_key" == *irodori* ]]
}

load_env_file

stop_port() {
  local name="$1"
  local port="$2"
  local pids
  pids="$(/usr/sbin/lsof -tiTCP:"$port" -sTCP:LISTEN 2>/dev/null || true)"
  if [[ -z "$pids" ]]; then
    echo "[Yui services] $name is not listening on port $port."
    return 0
  fi

  echo "[Yui services] Stopping $name on port $port: $pids"
  # shellcheck disable=SC2086
  kill $pids >/dev/null 2>&1 || true

  local deadline=$((SECONDS + 10))
  while (( SECONDS < deadline )); do
    pids="$(/usr/sbin/lsof -tiTCP:"$port" -sTCP:LISTEN 2>/dev/null || true)"
    if [[ -z "$pids" ]]; then
      echo "[Yui services] $name stopped."
      return 0
    fi
    sleep 1
  done

  echo "[Yui services] $name did not stop after SIGTERM; forcing stop: $pids" >&2
  # shellcheck disable=SC2086
  kill -KILL $pids >/dev/null 2>&1 || true

  deadline=$((SECONDS + 5))
  while (( SECONDS < deadline )); do
    pids="$(/usr/sbin/lsof -tiTCP:"$port" -sTCP:LISTEN 2>/dev/null || true)"
    if [[ -z "$pids" ]]; then
      echo "[Yui services] $name stopped."
      return 0
    fi
    sleep 1
  done

  echo "[Yui services] WARNING: $name is still listening on port $port: $pids" >&2
}

stop_port "Backend" "$BACKEND_PORT"
stop_port "VOICEVOX Engine" "$VOICEVOX_PORT"
stop_port "AivisSpeech Engine" "$AIVIS_PORT"
if is_irodori_configured; then
  IRODORI_STOP_PORT="${IRODORI_PORT:-$(url_port "${IRODORI_BASE_URL:-${HTTP_TTS_BASE_URL:-http://127.0.0.1:41080}}")}"
  stop_port "Irodori TTS" "$IRODORI_STOP_PORT"
fi

echo "[Yui services] Stop request sent."
