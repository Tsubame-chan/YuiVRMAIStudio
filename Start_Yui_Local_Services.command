#!/usr/bin/env bash
cd "$(dirname "$0")" || exit 1
export YUI_REUSE_EXISTING_BACKEND="${YUI_REUSE_EXISTING_BACKEND:-1}"
./scripts/start_local_services_detached_macos.sh
printf '\nYui local services are running in the background.\nPress Return to close this window '
read -r _
