#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
BACKEND_DIR="$REPO_ROOT/backend"
PYTHON_BIN="$BACKEND_DIR/.venv/bin/python"

if [[ ! -x "$PYTHON_BIN" ]]; then
  echo "Backend virtual environment not found: $PYTHON_BIN" >&2
  echo "Run: ./scripts/setup_backend_byok_macos.sh" >&2
  exit 1
fi

cd "$BACKEND_DIR"
echo "Starting Yui backend at http://127.0.0.1:8000"
exec "$PYTHON_BIN" -m uvicorn main:app --host 127.0.0.1 --port 8000 --no-use-colors
