#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
BACKEND_DIR="$REPO_ROOT/backend"
PYTHON_BIN="${PYTHON_BIN:-python3}"

cd "$BACKEND_DIR"

PY_VERSION="$("$PYTHON_BIN" - <<'PY'
import sys
print(f"{sys.version_info.major}.{sys.version_info.minor}.{sys.version_info.micro}")
raise SystemExit(0 if sys.version_info >= (3, 12) else 1)
PY
)" || {
  echo "Python 3.12+ is required. Current '$PYTHON_BIN' is too old or missing." >&2
  echo "Install it with Homebrew: brew install python@3.12" >&2
  exit 1
}

echo "[Yui setup] Using Python $PY_VERSION"
"$PYTHON_BIN" -m venv .venv
"$BACKEND_DIR/.venv/bin/python" -m pip install --upgrade pip
"$BACKEND_DIR/.venv/bin/python" -m pip install -r requirements.txt

if [[ ! -f "$REPO_ROOT/.env" ]]; then
  cp "$REPO_ROOT/.env.example" "$REPO_ROOT/.env"
  echo "[Yui setup] Created .env from .env.example"
fi

mkdir -p "$BACKEND_DIR/data" "$REPO_ROOT/logs" "$REPO_ROOT/runtime"

echo "[Yui setup] Done."
echo "[Yui setup] Edit $REPO_ROOT/.env and set OPENAI_API_KEY before chatting."
