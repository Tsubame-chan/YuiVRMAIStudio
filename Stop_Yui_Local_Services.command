#!/usr/bin/env bash
cd "$(dirname "$0")" || exit 1
./scripts/stop_local_services_macos.sh
printf '\nPress Return to close this window '
read -r _
