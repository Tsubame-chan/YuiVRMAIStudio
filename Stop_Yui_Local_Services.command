#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")" || exit 1
if bash ./scripts/stop_local_services_macos.sh; then
  printf '\nPress Return to close this window '
else
  status=$?
  printf '\nYui local services failed to stop cleanly. Exit code: %s\nPress Return to close this window ' "$status"
  read -r _
  exit "$status"
fi
read -r _
