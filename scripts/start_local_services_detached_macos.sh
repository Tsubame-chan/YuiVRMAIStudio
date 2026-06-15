#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
BACKEND_DIR="$REPO_ROOT/backend"
LOG_DIR="$REPO_ROOT/logs"
RUNTIME_DIR="$REPO_ROOT/runtime"

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

load_env_file

BACKEND_HOST="${BACKEND_HOST:-127.0.0.1}"
BACKEND_PORT="${BACKEND_PORT:-8000}"
VOICEVOX_HOST="${VOICEVOX_HOST:-127.0.0.1}"
VOICEVOX_PORT="${VOICEVOX_PORT:-50021}"
VOICEVOX_CPU_THREADS="${VOICEVOX_CPU_THREADS:-0}"
if [[ "$VOICEVOX_CPU_THREADS" == "0" ]]; then
  VOICEVOX_CPU_THREADS="$(sysctl -n hw.logicalcpu 2>/dev/null || echo 4)"
fi
VOICEVOX_ENABLE_CANCELLABLE_SYNTHESIS="${VOICEVOX_ENABLE_CANCELLABLE_SYNTHESIS:-1}"
VOICEVOX_LOAD_ALL_MODELS="${VOICEVOX_LOAD_ALL_MODELS:-0}"
YUI_REUSE_EXISTING_BACKEND="${YUI_REUSE_EXISTING_BACKEND:-0}"
RUN_ID="$(date +%Y%m%d-%H%M%S)"

mkdir -p "$LOG_DIR" "$RUNTIME_DIR"

http_ok() {
  /usr/bin/curl -fsS --max-time 2 "$1" >/dev/null 2>&1
}

wait_http_ok() {
  local name="$1"
  local url="$2"
  local timeout="${3:-90}"
  local deadline=$((SECONDS + timeout))
  while (( SECONDS < deadline )); do
    if http_ok "$url"; then
      echo "[Yui services] $name is ready: $url"
      return 0
    fi
    sleep 1
  done
  echo "[Yui services] WARNING: $name did not become ready within ${timeout}s: $url" >&2
  return 1
}

stop_port_listener() {
  local name="$1"
  local port="$2"
  local pids
  pids="$(/usr/sbin/lsof -tiTCP:"$port" -sTCP:LISTEN 2>/dev/null || true)"
  if [[ -z "$pids" ]]; then
    return 0
  fi

  echo "[Yui services] Stopping existing $name on port $port: $pids"
  # shellcheck disable=SC2086
  kill $pids >/dev/null 2>&1 || true

  local deadline=$((SECONDS + 10))
  while (( SECONDS < deadline )); do
    pids="$(/usr/sbin/lsof -tiTCP:"$port" -sTCP:LISTEN 2>/dev/null || true)"
    [[ -z "$pids" ]] && return 0
    sleep 1
  done

  echo "[Yui services] $name did not stop after SIGTERM; forcing stop: $pids" >&2
  # shellcheck disable=SC2086
  kill -KILL $pids >/dev/null 2>&1 || true

  deadline=$((SECONDS + 5))
  while (( SECONDS < deadline )); do
    pids="$(/usr/sbin/lsof -tiTCP:"$port" -sTCP:LISTEN 2>/dev/null || true)"
    [[ -z "$pids" ]] && return 0
    sleep 1
  done

  echo "[Yui services] WARNING: $name is still listening on port $port: $pids" >&2
}

resolve_voicevox_engine() {
  local candidates=()
  [[ -n "${VOICEVOX_ENGINE_EXE:-}" ]] && candidates+=("$VOICEVOX_ENGINE_EXE")
  candidates+=(
    "/Applications/VOICEVOX.app/Contents/Resources/vv-engine/run"
    "$HOME/Applications/VOICEVOX.app/Contents/Resources/vv-engine/run"
  )

  local candidate
  for candidate in "${candidates[@]}"; do
    if [[ -x "$candidate" ]]; then
      printf '%s\n' "$candidate"
      return 0
    fi
  done

  return 1
}

VOICEVOX_BASE_URL="http://$VOICEVOX_HOST:$VOICEVOX_PORT"
BACKEND_BASE_URL="http://$BACKEND_HOST:$BACKEND_PORT"

echo "[Yui services] Repository: $REPO_ROOT"
echo "[Yui services] Logs: $LOG_DIR"

if http_ok "$VOICEVOX_BASE_URL/version"; then
  echo "[Yui services] VOICEVOX Engine is already running: $VOICEVOX_BASE_URL"
elif VOICEVOX_ENGINE_PATH="$(resolve_voicevox_engine)"; then
  VOICEVOX_OUT="$LOG_DIR/voicevox-service-$RUN_ID.out.log"
  VOICEVOX_ERR="$LOG_DIR/voicevox-service-$RUN_ID.err.log"
  echo "[Yui services] Starting VOICEVOX Engine on $VOICEVOX_BASE_URL"
  voicevox_args=(
    --host "$VOICEVOX_HOST"
    --port "$VOICEVOX_PORT"
    --cpu_num_threads "$VOICEVOX_CPU_THREADS"
    --output_log_utf8
  )
  if [[ "$VOICEVOX_ENABLE_CANCELLABLE_SYNTHESIS" != "0" ]]; then
    voicevox_args+=(--enable_cancellable_synthesis --init_processes 1)
  fi
  if [[ "$VOICEVOX_LOAD_ALL_MODELS" == "1" ]]; then
    voicevox_args+=(--load_all_models)
  fi
  nohup "$VOICEVOX_ENGINE_PATH" "${voicevox_args[@]}" >"$VOICEVOX_OUT" 2>"$VOICEVOX_ERR" < /dev/null &
  voicevox_pid=$!
  echo "$voicevox_pid" > "$RUNTIME_DIR/voicevox.pid"
  disown "$voicevox_pid" 2>/dev/null || true
  wait_http_ok "VOICEVOX Engine" "$VOICEVOX_BASE_URL/version" 90 || true
else
  echo "[Yui services] VOICEVOX Engine was not found. Text chat can still work, but speech playback needs VOICEVOX." >&2
fi

PYTHON_BIN="$BACKEND_DIR/.venv/bin/python"
if [[ ! -x "$PYTHON_BIN" ]]; then
  echo "Backend virtual environment not found: $PYTHON_BIN" >&2
  echo "Run: ./scripts/setup_backend_byok_macos.sh" >&2
  exit 1
fi

if http_ok "$BACKEND_BASE_URL/health" && [[ "$YUI_REUSE_EXISTING_BACKEND" == "1" ]]; then
  echo "[Yui services] Reusing existing backend: $BACKEND_BASE_URL"
else
  stop_port_listener "Backend" "$BACKEND_PORT"
  BACKEND_OUT="$LOG_DIR/backend-service-$RUN_ID.out.log"
  BACKEND_ERR="$LOG_DIR/backend-service-$RUN_ID.err.log"
  echo "[Yui services] Starting backend on $BACKEND_BASE_URL"
  (
    cd "$BACKEND_DIR"
    nohup "$PYTHON_BIN" -m uvicorn main:app --host "$BACKEND_HOST" --port "$BACKEND_PORT" --no-use-colors \
      >"$BACKEND_OUT" 2>"$BACKEND_ERR" < /dev/null &
    backend_pid=$!
    echo "$backend_pid" > "$RUNTIME_DIR/backend.pid"
    disown "$backend_pid" 2>/dev/null || true
  )
  wait_http_ok "Backend" "$BACKEND_BASE_URL/health" 90 || true
fi

echo
echo "[Yui services] Startup check:"
echo "  VOICEVOX: $VOICEVOX_BASE_URL/version"
echo "  Backend : $BACKEND_BASE_URL/health"
echo
echo "[Yui services] Services are running in the background."
echo "[Yui services] Stop them with: ./scripts/stop_local_services_macos.sh"
