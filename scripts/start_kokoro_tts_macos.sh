#!/usr/bin/env bash
set -euo pipefail

# Desktop voice audition harness only.
# This starts a local HTTP service on macOS so voices can be evaluated quickly.
# It is not the mobile offline implementation. iOS/Android must use an
# in-process embedded runtime such as ONNX Runtime Mobile or sherpa-onnx.

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
VENV_DIR="${KOKORO_VENV_DIR:-$HOME/.cache/yui-vrm-ai-studio/kokoro-tts-venv}"
MODEL_DIR="${KOKORO_MODEL_DIR:-$HOME/.cache/yui-vrm-ai-studio/kokoro}"
HOST="${KOKORO_HOST:-127.0.0.1}"
PORT="${KOKORO_PORT:-41081}"
VOICE="${KOKORO_VOICE:-af_heart}"
PYTHON_BIN="${PYTHON_BIN:-python3}"
KOKORO_MODEL_VARIANT="${KOKORO_MODEL_VARIANT:-int8}"

mkdir -p "$VENV_DIR" "$MODEL_DIR"

if [[ ! -x "$VENV_DIR/bin/python" ]]; then
  "$PYTHON_BIN" -m venv "$VENV_DIR"
fi

"$VENV_DIR/bin/python" -m pip install --upgrade pip >/dev/null
"$VENV_DIR/bin/python" -m pip install --upgrade kokoro-onnx fastapi uvicorn soundfile numpy >/dev/null

case "$KOKORO_MODEL_VARIANT" in
  f32|full)
    DEFAULT_MODEL_NAME="kokoro-v1.0.onnx"
    ;;
  fp16)
    DEFAULT_MODEL_NAME="kokoro-v1.0.fp16.onnx"
    ;;
  int8|*)
    DEFAULT_MODEL_NAME="kokoro-v1.0.int8.onnx"
    ;;
esac

MODEL_PATH="${KOKORO_MODEL:-$MODEL_DIR/$DEFAULT_MODEL_NAME}"
VOICES_PATH="${KOKORO_VOICES:-$MODEL_DIR/voices-v1.0.bin}"

if [[ ! -f "$MODEL_PATH" ]]; then
  curl -L --fail --output "$MODEL_PATH" \
    "https://github.com/thewh1teagle/kokoro-onnx/releases/download/model-files-v1.0/$(basename "$MODEL_PATH")"
fi

if [[ ! -f "$VOICES_PATH" ]]; then
  curl -L --fail --output "$VOICES_PATH" \
    "https://github.com/thewh1teagle/kokoro-onnx/releases/download/model-files-v1.0/voices-v1.0.bin"
fi

echo "[Yui Kokoro] Starting Kokoro ONNX TTS at http://$HOST:$PORT"
echo "[Yui Kokoro] Voice: $VOICE"
exec "$VENV_DIR/bin/python" "$ROOT_DIR/scripts/local_tts/yui_kokoro_tts_server.py" \
  --host "$HOST" \
  --port "$PORT" \
  --model "$MODEL_PATH" \
  --voices "$VOICES_PATH" \
  --voice "$VOICE"
