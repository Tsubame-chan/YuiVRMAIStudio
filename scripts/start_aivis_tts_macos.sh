#!/usr/bin/env bash
set -euo pipefail

# Desktop voice audition harness only.
# AivisSpeech currently runs here as a PC engine process, not as an embedded
# mobile runtime. Do not treat this path as satisfying the iOS/Android
# airplane-mode requirement.

HOST="${AIVIS_HOST:-127.0.0.1}"
PORT="${AIVIS_PORT:-10101}"
ENGINE_EXE="${AIVIS_ENGINE_EXE:-}"

if [[ -z "$ENGINE_EXE" ]]; then
  candidates=(
    "$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)/tools/tts/aivis-engine/extracted/macOS-arm64/run"
    "/Applications/AivisSpeech.app/Contents/Resources/engine/run"
    "/Applications/AivisSpeech.app/Contents/Resources/vv-engine/run"
    "$HOME/Applications/AivisSpeech.app/Contents/Resources/engine/run"
    "$HOME/Applications/AivisSpeech.app/Contents/Resources/vv-engine/run"
  )
  for candidate in "${candidates[@]}"; do
    if [[ -x "$candidate" ]]; then
      ENGINE_EXE="$candidate"
      break
    fi
  done
fi

if [[ -z "$ENGINE_EXE" || ! -x "$ENGINE_EXE" ]]; then
  cat >&2 <<'EOF'
[Yui Aivis] AivisSpeech engine executable was not found.
Set AIVIS_ENGINE_EXE to the engine run executable, then run this script again.
EOF
  exit 1
fi

echo "[Yui Aivis] Starting AivisSpeech Engine at http://$HOST:$PORT"
exec "$ENGINE_EXE" \
  --host "$HOST" \
  --port "$PORT" \
  --output_log_utf8 \
  --disable_sentry
