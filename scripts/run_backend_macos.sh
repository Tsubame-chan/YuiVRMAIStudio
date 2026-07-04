#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
BACKEND_DIR="$REPO_ROOT/backend"
PYTHON_BIN="$BACKEND_DIR/.venv/bin/python"
export PATH="/opt/homebrew/bin:/usr/local/bin:$PATH"

if [[ ! -x "$PYTHON_BIN" ]]; then
  echo "Backend virtual environment not found: $PYTHON_BIN" >&2
  echo "Run: ./scripts/setup_backend_byok_macos.sh" >&2
  exit 1
fi

if [[ -d "$BACKEND_DIR/.venv/lib/python3.12/encodings" ]]; then
  export PYTHONHOME="$BACKEND_DIR/.venv"
fi

cd "$BACKEND_DIR"
BACKEND_HOST="${BACKEND_HOST:-127.0.0.1}"
BACKEND_PORT="${BACKEND_PORT:-8000}"
echo "Starting Yui backend at http://$BACKEND_HOST:$BACKEND_PORT"
exec "$PYTHON_BIN" -m uvicorn main:app --host "$BACKEND_HOST" --port "$BACKEND_PORT" --no-use-colors
