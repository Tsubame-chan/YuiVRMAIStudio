#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
VENV_DIR="${YUI_LITERT_LM_VENV:-$HOME/.cache/yui-vrm-ai-studio/litert-lm-venv}"
HF_HOME="${YUI_LITERT_HF_HOME:-$HOME/.cache/yui-vrm-ai-studio/huggingface}"
HOST="${YUI_LITERT_LM_HOST:-127.0.0.1}"
PORT="${YUI_LITERT_LM_PORT:-9379}"
MODEL_REPO="${YUI_LITERT_LM_REPO:-litert-community/gemma-4-E4B-it-litert-lm}"
MODEL_FILE="${YUI_LITERT_LM_FILE:-gemma-4-E4B-it.litertlm}"
MODEL_ALIAS="${YUI_LITERT_LM_ALIAS:-gemma4-e4b}"

PYTHON_BIN="${PYTHON_BIN:-python3}"

mkdir -p "$VENV_DIR" "$HF_HOME"
if [ ! -x "$VENV_DIR/bin/litert-lm" ]; then
  "$PYTHON_BIN" -m venv "$VENV_DIR"
  "$VENV_DIR/bin/python" -m pip install --upgrade pip litert-lm
fi

export HF_HOME

"$VENV_DIR/bin/litert-lm" import \
  --from-huggingface-repo="$MODEL_REPO" \
  "$MODEL_FILE" \
  "$MODEL_ALIAS"

cat <<EOF
Yui LiteRT-LM local server
  base URL: http://$HOST:$PORT/v1
  backend env:
    CHAT_PROVIDER=litert_lm
    LITERT_LM_BASE_URL=http://$HOST:$PORT/v1
    LITERT_LM_CHAT_MODEL=$MODEL_ALIAS,gpu
EOF

cd "$ROOT_DIR"
"$VENV_DIR/bin/litert-lm" serve --host "$HOST" --port "$PORT"
