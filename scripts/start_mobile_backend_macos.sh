#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

export BACKEND_HOST="${BACKEND_HOST:-0.0.0.0}"
export BACKEND_PORT="${BACKEND_PORT:-8000}"
export VOICEVOX_HOST="${VOICEVOX_HOST:-127.0.0.1}"
export VOICEVOX_PORT="${VOICEVOX_PORT:-50021}"
export YUI_REUSE_EXISTING_BACKEND="${YUI_REUSE_EXISTING_BACKEND:-0}"

"$SCRIPT_DIR/start_local_services_detached_macos.sh"

echo
echo "[Yui mobile backend] Configure the iPhone app Backend URL to one of these:"
if command -v tailscale >/dev/null 2>&1; then
  tailscale_ip="$(tailscale ip -4 2>/dev/null | head -n 1 || true)"
  if [[ -n "$tailscale_ip" ]]; then
    echo "  Tailscale: http://$tailscale_ip:$BACKEND_PORT"
  fi
fi

wifi_ip="$(ipconfig getifaddr en0 2>/dev/null || true)"
if [[ -n "$wifi_ip" ]]; then
  echo "  Wi-Fi    : http://$wifi_ip:$BACKEND_PORT"
fi
echo "  Local    : http://127.0.0.1:$BACKEND_PORT"
echo
echo "[Yui mobile backend] Keep this Mac awake while using the iPhone app."
