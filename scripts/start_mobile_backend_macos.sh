#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

tailscale_bin="$(command -v tailscale || true)"
if [[ -z "$tailscale_bin" && -x /Applications/Tailscale.app/Contents/MacOS/Tailscale ]]; then
  tailscale_bin=/Applications/Tailscale.app/Contents/MacOS/Tailscale
fi
if [[ -z "${YUI_BACKEND_VPN_HOST:-}" ]]; then
  [[ -n "$tailscale_bin" ]] || { echo "Tailscaleを接続するか、YUI_BACKEND_VPN_HOSTにこのPCのVPNアドレスを指定してください。" >&2; exit 1; }
  export YUI_BACKEND_VPN_HOST="$("$tailscale_bin" ip -4 2>/dev/null | head -n 1)"
fi
[[ -n "$YUI_BACKEND_VPN_HOST" ]] || { echo "VPNアドレスを確認できません。Tailscaleの接続を確認してください。" >&2; exit 1; }
export BACKEND_HOST="${BACKEND_HOST:-127.0.0.1}"
export BACKEND_PORT="${BACKEND_PORT:-8000}"
export VOICEVOX_HOST="${VOICEVOX_HOST:-127.0.0.1}"
export VOICEVOX_PORT="${VOICEVOX_PORT:-50021}"
export YUI_REUSE_EXISTING_BACKEND="${YUI_REUSE_EXISTING_BACKEND:-0}"

"$SCRIPT_DIR/start_local_services_detached_macos.sh"

echo
echo "[Yui services] Configure the iPhone app Backend URL to the Yui backend, not to VOICEVOX or Irodori:"
echo "  VPN      : http://$YUI_BACKEND_VPN_HOST:$BACKEND_PORT"
echo "  Local    : http://127.0.0.1:$BACKEND_PORT"
echo
echo "[Yui services] Keep this Mac awake while using the iPhone app."
