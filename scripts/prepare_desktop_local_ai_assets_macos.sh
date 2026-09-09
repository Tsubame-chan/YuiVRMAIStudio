#!/usr/bin/env bash
set -euo pipefail
ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
VENV_DIR="${YUI_LITERT_LM_VENV:-$ROOT_DIR/backend/.venv}"
MODEL_REPO="${YUI_DESKTOP_LITERT_LM_REPO:-litert-community/gemma-4-E2B-it-litert-lm}"
MODEL_FILE="${YUI_DESKTOP_LITERT_LM_FILE:-gemma-4-E2B-it.litertlm}"
MODEL_REVISION="${YUI_DESKTOP_LITERT_LM_REVISION:-b3ca0d2f076785a8f4b2219ddbd2bdb99954eae1}"
OUTPUT_DIR="$ROOT_DIR/unity/Assets/StreamingAssets/YuiLocalAI/Models"
PYTHON_BIN="${PYTHON_BIN:-python3}"
if [[ ! -x "$VENV_DIR/bin/python" ]]; then "$PYTHON_BIN" -m venv "$VENV_DIR"; fi
"$VENV_DIR/bin/python" -m pip install 'litert-lm==0.17.0' 'huggingface_hub>=0.34,<2'
mkdir -p "$OUTPUT_DIR"
DOWNLOAD_DIR="$(mktemp -d "$OUTPUT_DIR/.gemma-download.XXXXXX")"
trap 'rm -rf "$DOWNLOAD_DIR"' EXIT
"$VENV_DIR/bin/python" - "$MODEL_REPO" "$MODEL_FILE" "$MODEL_REVISION" "$DOWNLOAD_DIR" "$OUTPUT_DIR" <<'PY'
import hashlib, os, pathlib, sys
from huggingface_hub import hf_hub_download
repo, filename, revision, temporary, output = sys.argv[1:]
if pathlib.Path(filename).name != filename:
    raise SystemExit('Model filename must not contain a directory.')
source = pathlib.Path(hf_hub_download(repo_id=repo, filename=filename, revision=revision, local_dir=temporary))
h = hashlib.sha256()
with source.open('rb') as stream:
    for chunk in iter(lambda: stream.read(8 * 1024 * 1024), b''):
        h.update(chunk)
if (repo, filename, revision) == ('litert-community/gemma-4-E2B-it-litert-lm', 'gemma-4-E2B-it.litertlm', 'b3ca0d2f076785a8f4b2219ddbd2bdb99954eae1'):
    if h.hexdigest() != '181938105e0eefd105961417e8da75903eacda102c4fce9ce90f50b97139a63c':
        raise SystemExit('Official model checksum mismatch; installed model was not replaced.')
target = pathlib.Path(output) / filename
os.replace(source, target)
print(f'Prepared {target.name}: {target.stat().st_size} bytes, SHA-256 {h.hexdigest()}')
PY
