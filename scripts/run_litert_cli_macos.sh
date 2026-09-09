#!/usr/bin/env bash
set -euo pipefail
RUNTIME_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
if [[ -d "$RUNTIME_ROOT/backend/.venv/lib/python3.12/encodings" ]]; then
  export PYTHONHOME="$RUNTIME_ROOT/backend/.venv"
fi
exec "$RUNTIME_ROOT/backend/.venv/bin/python3" -c 'from litert_lm_cli.main import main; main()' "$@"
