#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
VENV_DIR="${YUI_LITERT_LM_VENV:-$HOME/.cache/yui-vrm-ai-studio/litert-lm-venv-py3}"
HF_HOME="${YUI_LITERT_HF_HOME:-$HOME/.cache/yui-vrm-ai-studio/huggingface}"
HOST="${YUI_LITERT_LM_HOST:-127.0.0.1}"
PORT="${YUI_LITERT_LM_PORT:-9379}"
MODEL_REPO="${YUI_LITERT_LM_REPO:-litert-community/gemma-4-E2B-it-litert-lm}"
MODEL_FILE="${YUI_LITERT_LM_FILE:-gemma-4-E2B-it.litertlm}"
MODEL_ALIAS="${YUI_LITERT_LM_ALIAS:-gemma4-e2b}"

# Python 3.9 may install the CLI but cannot import its kw_only dataclasses.
if [[ -z "${PYTHON_BIN:-}" ]]; then
  for candidate in "$ROOT_DIR/backend/.venv/bin/python" python3.13 python3.12 python3.11 python3.10 python3; do
    if "$candidate" -c 'import sys; sys.exit(sys.version_info < (3, 10))' >/dev/null 2>&1; then
      PYTHON_BIN="$candidate"
      break
    fi
  done
fi
if [[ -z "${PYTHON_BIN:-}" ]] || ! "$PYTHON_BIN" -c 'import sys; sys.exit(sys.version_info < (3, 10))'; then
  echo "LiteRT-LM requires Python 3.10 or newer. Set PYTHON_BIN to a supported interpreter." >&2
  exit 1
fi
if [[ -x "$VENV_DIR/bin/python" ]] && ! "$VENV_DIR/bin/python" -c 'import sys; sys.exit(sys.version_info < (3, 10))'; then
  echo "Existing LiteRT-LM environment uses unsupported Python. Set YUI_LITERT_LM_VENV to a new directory; the old environment was preserved." >&2
  exit 1
fi

mkdir -p "$VENV_DIR" "$HF_HOME"
if [ ! -x "$VENV_DIR/bin/litert-lm" ]; then
  "$PYTHON_BIN" -m venv "$VENV_DIR"
  "$VENV_DIR/bin/python" -m pip install --upgrade pip "litert-lm==${YUI_LITERT_LM_VERSION:-0.17.0}"
fi

export HF_HOME

MODEL_PATH="${YUI_LITERT_LM_MODEL_PATH:-$ROOT_DIR/unity/Assets/StreamingAssets/YuiLocalAI/Models/$MODEL_FILE}"
if [[ -f "$MODEL_PATH" ]]; then
  "$VENV_DIR/bin/litert-lm" import "$MODEL_PATH" "$MODEL_ALIAS"
else
  "$VENV_DIR/bin/litert-lm" import \
    --from-huggingface-repo="$MODEL_REPO" \
    "$MODEL_FILE" \
    "$MODEL_ALIAS"
fi

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
