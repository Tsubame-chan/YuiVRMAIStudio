#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
BACKEND_DIR="$REPO_ROOT/backend"
LOG_DIR="$REPO_ROOT/logs"
RUNTIME_DIR="$REPO_ROOT/runtime"
PYTHONHOME_CANDIDATE="$BACKEND_DIR/.venv"
export PATH="/opt/homebrew/bin:/usr/local/bin:$PATH"

source "$SCRIPT_DIR/aivis_model_sync_macos.sh"

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
AIVIS_HOST="${AIVIS_HOST:-127.0.0.1}"
AIVIS_PORT="${AIVIS_PORT:-10101}"
AIVIS_ENABLE="${AIVIS_ENABLE:-auto}"
VOICEVOX_CPU_THREADS="${VOICEVOX_CPU_THREADS:-0}"
if [[ "$VOICEVOX_CPU_THREADS" == "0" ]]; then
  VOICEVOX_CPU_THREADS="$(sysctl -n hw.logicalcpu 2>/dev/null || echo 4)"
fi
VOICEVOX_ENABLE_CANCELLABLE_SYNTHESIS="${VOICEVOX_ENABLE_CANCELLABLE_SYNTHESIS:-1}"
VOICEVOX_LOAD_ALL_MODELS="${VOICEVOX_LOAD_ALL_MODELS:-0}"
YUI_REUSE_EXISTING_BACKEND="${YUI_REUSE_EXISTING_BACKEND:-0}"
RUN_ID="$(date +%Y%m%d-%H%M%S)"

mkdir -p "$LOG_DIR" "$RUNTIME_DIR"

if [[ -d "$PYTHONHOME_CANDIDATE/lib/python3.12/encodings" ]]; then
  export PYTHONHOME="$PYTHONHOME_CANDIDATE"
fi

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

join_url() {
  local base="$1"
  local path="$2"
  if [[ "$path" == http://* || "$path" == https://* ]]; then
    printf '%s\n' "$path"
    return 0
  fi
  printf '%s/%s\n' "${base%/}" "${path#/}"
}

url_host() {
  local url="$1"
  url="${url#http://}"
  url="${url#https://}"
  url="${url%%/*}"
  url="${url##*@}"
  printf '%s\n' "${url%%:*}"
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

is_irodori_configured() {
  local enabled="${IRODORI_ENABLE:-auto}"
  [[ "$enabled" == "0" || "$enabled" == "false" || "$enabled" == "False" ]] && return 1
  [[ "$enabled" == "1" || "$enabled" == "true" || "$enabled" == "True" ]] && return 0

  local provider_key
  provider_key="$(printf '%s' "${HTTP_TTS_PROVIDER_ID:-}" | tr '[:upper:]' '[:lower:]')"
  [[ "$provider_key" == *irodori* ]]
}

resolve_irodori_mlx_python() {
  local base_dir="${IRODORI_MLX_DIR:-$HOME/Documents/Irodori TTS Local}"
  local candidates=()
  [[ -n "${IRODORI_MLX_PYTHON:-}" ]] && candidates+=("$IRODORI_MLX_PYTHON")
  candidates+=(
    "$base_dir/.venv312/bin/python"
    "$base_dir/.venv/bin/python"
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

start_irodori_if_configured() {
  is_irodori_configured || return 0

  local base_url="${IRODORI_BASE_URL:-${HTTP_TTS_BASE_URL:-http://127.0.0.1:41080}}"
  local health_endpoint="${HTTP_TTS_HEALTH_ENDPOINT:-/v1/models}"
  local health_url
  health_url="$(join_url "$base_url" "$health_endpoint")"

  if http_ok "$health_url"; then
    echo "[Yui services] Irodori TTS is already running: $health_url"
    return 0
  fi

  local out_log="$LOG_DIR/irodori-service-$RUN_ID.out.log"
  local err_log="$LOG_DIR/irodori-service-$RUN_ID.err.log"
  local host port
  host="${IRODORI_HOST:-$(url_host "$base_url")}"
  port="${IRODORI_PORT:-$(url_port "$base_url")}"

  if [[ -n "${IRODORI_START_COMMAND:-}" ]]; then
    echo "[Yui services] Starting Irodori TTS with IRODORI_START_COMMAND"
    /bin/bash -lc "$IRODORI_START_COMMAND" >"$out_log" 2>"$err_log" &
    IRODORI_PID=$!
    wait_http_ok "Irodori TTS" "$health_url" 180 || true
    return 0
  fi

  local python_bin base_dir
  base_dir="${IRODORI_MLX_DIR:-$HOME/Documents/Irodori TTS Local}"
  if python_bin="$(resolve_irodori_mlx_python)"; then
    echo "[Yui services] Starting Irodori MLX TTS on $base_url"
    (
      cd "$base_dir"
      "$python_bin" -m mlx_audio.server --host "$host" --port "$port"
    ) >"$out_log" 2>"$err_log" &
    IRODORI_PID=$!
    wait_http_ok "Irodori TTS" "$health_url" 180 || true
  else
    echo "[Yui services] Irodori TTS is configured but no local runtime was found." >&2
    echo "[Yui services] Set IRODORI_START_COMMAND or IRODORI_MLX_DIR, or start Irodori separately." >&2
  fi
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

is_aivis_configured() {
  [[ "$AIVIS_ENABLE" == "0" || "$AIVIS_ENABLE" == "false" || "$AIVIS_ENABLE" == "False" ]] && return 1
  [[ "$AIVIS_ENABLE" == "1" || "$AIVIS_ENABLE" == "true" || "$AIVIS_ENABLE" == "True" ]] && return 0
  [[ -x "$REPO_ROOT/tools/tts/aivis-engine/extracted/macOS-arm64/run" ]]
}

resolve_aivis_engine() {
  local candidates=()
  [[ -n "${AIVIS_ENGINE_EXE:-}" ]] && candidates+=("$AIVIS_ENGINE_EXE")
  candidates+=(
    "$REPO_ROOT/tools/tts/aivis-engine/extracted/macOS-arm64/run"
    "/Applications/AivisSpeech.app/Contents/Resources/engine/run"
    "$HOME/Applications/AivisSpeech.app/Contents/Resources/engine/run"
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

cleanup() {
  [[ -n "${BACKEND_PID:-}" ]] && kill "$BACKEND_PID" >/dev/null 2>&1 || true
  [[ -n "${VOICEVOX_PID:-}" ]] && kill "$VOICEVOX_PID" >/dev/null 2>&1 || true
  [[ -n "${AIVIS_PID:-}" ]] && kill "$AIVIS_PID" >/dev/null 2>&1 || true
  [[ -n "${IRODORI_PID:-}" ]] && kill "$IRODORI_PID" >/dev/null 2>&1 || true
}
trap cleanup EXIT

VOICEVOX_BASE_URL="http://$VOICEVOX_HOST:$VOICEVOX_PORT"
AIVIS_BASE_URL="${AIVIS_BASE_URL:-http://$AIVIS_HOST:$AIVIS_PORT}"
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
  "$VOICEVOX_ENGINE_PATH" "${voicevox_args[@]}" >"$VOICEVOX_OUT" 2>"$VOICEVOX_ERR" &
  VOICEVOX_PID=$!
  wait_http_ok "VOICEVOX Engine" "$VOICEVOX_BASE_URL/version" 90 || true
else
  echo "[Yui services] VOICEVOX Engine was not found. Text chat can still work, but speech playback needs VOICEVOX." >&2
  echo "[Yui services] Install VOICEVOX.app or set VOICEVOX_ENGINE_EXE=/path/to/VOICEVOX.app/Contents/Resources/vv-engine/run" >&2
fi

if is_aivis_configured; then
  if http_ok "$AIVIS_BASE_URL/version"; then
    echo "[Yui services] AivisSpeech Engine is already running: $AIVIS_BASE_URL"
  elif AIVIS_ENGINE_PATH="$(resolve_aivis_engine)"; then
    AIVIS_OUT="$LOG_DIR/aivis-service-$RUN_ID.out.log"
    AIVIS_ERR="$LOG_DIR/aivis-service-$RUN_ID.err.log"
    echo "[Yui services] Starting AivisSpeech Engine on $AIVIS_BASE_URL"
    prepare_aivis_addon_runtime
    "$AIVIS_ENGINE_PATH" --host "$AIVIS_HOST" --port "$AIVIS_PORT" --output_log_utf8 --disable_sentry >"$AIVIS_OUT" 2>"$AIVIS_ERR" &
    AIVIS_PID=$!
    wait_http_ok "AivisSpeech Engine" "$AIVIS_BASE_URL/version" 90 || true
  else
    echo "[Yui services] AivisSpeech Engine is enabled but no runtime was found." >&2
    echo "[Yui services] Set AIVIS_ENGINE_EXE=/path/to/run or install the local audition pack." >&2
  fi
fi

start_irodori_if_configured

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
    "$PYTHON_BIN" -m uvicorn main:app --host "$BACKEND_HOST" --port "$BACKEND_PORT" --no-use-colors
  ) >"$BACKEND_OUT" 2>"$BACKEND_ERR" &
  BACKEND_PID=$!
  wait_http_ok "Backend" "$BACKEND_BASE_URL/health" 90 || true
fi

echo
echo "[Yui services] Startup check:"
echo "  VOICEVOX: $VOICEVOX_BASE_URL/version"
if is_aivis_configured; then
  echo "  Aivis   : $AIVIS_BASE_URL/version"
fi
if is_irodori_configured; then
  echo "  Irodori : $(join_url "${IRODORI_BASE_URL:-${HTTP_TTS_BASE_URL:-http://127.0.0.1:41080}}" "${HTTP_TTS_HEALTH_ENDPOINT:-/v1/models}")"
fi
echo "  Backend : $BACKEND_BASE_URL/health"
echo
echo "Keep this Terminal window open while using Yui."
read -r -p "Press Enter to stop Yui local services "
