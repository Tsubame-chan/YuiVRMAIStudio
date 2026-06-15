#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")" || exit 1
export YUI_REUSE_EXISTING_BACKEND="${YUI_REUSE_EXISTING_BACKEND:-1}"
if bash ./scripts/start_local_services_detached_macos.sh; then
  printf '\nYui local services are running in the background.\nPress Return to close this window '
else
  status=$?
  printf '\nYui local services failed to start. Exit code: %s\nPress Return to close this window ' "$status"
  read -r _
  exit "$status"
fi
read -r _
