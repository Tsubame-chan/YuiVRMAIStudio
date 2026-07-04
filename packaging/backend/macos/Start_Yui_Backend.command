#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

if [[ ! -x "backend/.venv/bin/python" ]]; then
  echo "[Yui Backend] Backend virtual environment is missing."
  echo "[Yui Backend] Running first-time backend setup. Python 3.12+ is required."
  if command -v python3.12 >/dev/null 2>&1; then
    PYTHON_BIN=python3.12 ./scripts/setup_backend_byok_macos.sh
  else
    PYTHON_BIN=python3 ./scripts/setup_backend_byok_macos.sh
  fi
fi

YUI_REUSE_EXISTING_BACKEND=1 ./scripts/start_local_services_detached_macos.sh
echo
echo "[Yui Backend] Backend startup requested."
echo "[Yui Backend] You can close this window."

