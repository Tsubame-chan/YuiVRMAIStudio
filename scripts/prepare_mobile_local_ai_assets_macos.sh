#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
VENV_DIR="${YUI_LITERT_LM_VENV:-$HOME/.cache/yui-vrm-ai-studio/litert-lm-venv}"
HF_HOME="${YUI_LITERT_HF_HOME:-$HOME/.cache/yui-vrm-ai-studio/huggingface}"
MODEL_REPO="${YUI_LITERT_LM_REPO:-litert-community/gemma-4-E2B-it-litert-lm}"
MODEL_FILE="${YUI_LITERT_LM_FILE:-gemma-4-E2B-it.litertlm}"
OUTPUT_DIR="$ROOT_DIR/unity/Assets/StreamingAssets/YuiLocalAI/Models"
PYTHON_BIN="${PYTHON_BIN:-python3}"

mkdir -p "$VENV_DIR" "$HF_HOME" "$OUTPUT_DIR"
if [ ! -x "$VENV_DIR/bin/litert-lm" ]; then
  "$PYTHON_BIN" -m venv "$VENV_DIR"
  "$VENV_DIR/bin/python" -m pip install --upgrade pip litert-lm
fi

export HF_HOME

"$VENV_DIR/bin/litert-lm" run \
  --from-huggingface-repo="$MODEL_REPO" \
  "$MODEL_FILE" \
  --backend=cpu \
  --cache=no \
  --prompt="ok" >/tmp/yui-litert-asset-warmup.log 2>&1 || true

MODEL_PATH="$(find "$HF_HOME" -path "*$MODEL_REPO*" -name "$MODEL_FILE" -print -quit)"
if [ -z "$MODEL_PATH" ]; then
  MODEL_PATH="$(find "$HF_HOME" -name "$MODEL_FILE" -print -quit)"
fi
if [ -z "$MODEL_PATH" ]; then
  echo "Could not find downloaded model file: $MODEL_FILE" >&2
  exit 1
fi

cp -L "$MODEL_PATH" "$OUTPUT_DIR/$MODEL_FILE"
find "$OUTPUT_DIR" -name '*_mldrift_*_cache.bin' -delete
find "$OUTPUT_DIR" -name '*_mldrift_*_cache.bin.meta' -delete
du -h "$OUTPUT_DIR/$MODEL_FILE"
echo "Prepared mobile local AI model asset: $OUTPUT_DIR/$MODEL_FILE"
