#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

./scripts/stop_local_services_macos.sh
echo
echo "[Yui Backend] Stop request sent."

