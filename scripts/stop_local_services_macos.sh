#!/usr/bin/env bash
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
PYTHON_BIN="$REPO_ROOT/backend/.venv/bin/python"
if [[ ! -x "$PYTHON_BIN" ]]; then echo "Backend Python was not found. Stop services in their own apps." >&2; exit 1; fi
if [[ -d "$REPO_ROOT/backend/.venv/lib/python3.12/encodings" ]]; then export PYTHONHOME="$REPO_ROOT/backend/.venv"; fi
"$PYTHON_BIN" "$SCRIPT_DIR/service_ownership.py" stop-all --directory "$REPO_ROOT/runtime/owned-services"
echo "[Yui services] Processes started outside this installation were retained."
