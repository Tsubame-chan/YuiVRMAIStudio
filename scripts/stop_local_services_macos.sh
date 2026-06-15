#!/usr/bin/env bash
set -euo pipefail

BACKEND_PORT="${BACKEND_PORT:-8000}"
VOICEVOX_PORT="${VOICEVOX_PORT:-50021}"

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

echo "[Yui services] Stop request sent."
