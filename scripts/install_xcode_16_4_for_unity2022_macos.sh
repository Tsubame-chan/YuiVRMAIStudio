#!/usr/bin/env bash
set -euo pipefail

if ! command -v xcodes >/dev/null 2>&1; then
  echo "xcodes CLI is not installed." >&2
  echo "Install it with: brew install xcodesorg/made/xcodes" >&2
  exit 1
fi

echo "[Yui iOS] Installing Xcode 16.4 side by side for Unity 2022 iOS build checks."
echo "[Yui iOS] This does not replace /Applications/Xcode.app."
echo "[Yui iOS] Apple ID authentication may be requested by xcodes."
echo "[Yui iOS] If Apple returns 403 Unauthorized, open https://developer.apple.com/account/"
echo "[Yui iOS] and accept any pending Developer Terms or Agreements, then rerun this script."
echo "[Yui iOS] You do not need a paid Apple Developer Program membership just to test on your own iPhone."
echo "[Yui iOS] Using the standard unarchiver. It is slower, but avoids xcodes experimental unxip crashes."

if [[ -n "${XCODE_XIP_PATH:-}" ]]; then
  if [[ ! -f "$XCODE_XIP_PATH" ]]; then
    echo "XCODE_XIP_PATH does not exist: $XCODE_XIP_PATH" >&2
    exit 1
  fi

  echo "[Yui iOS] Installing from local XIP: $XCODE_XIP_PATH"
  xcodes install 16.4 --path "$XCODE_XIP_PATH" --directory /Applications
else
  xcodes install 16.4 --directory /Applications
fi

echo
echo "[Yui iOS] Installed Xcodes:"
xcodes installed
echo
echo "[Yui iOS] Re-run the build check with:"
echo "DEVELOPER_DIR=/Applications/Xcode_16.4.app/Contents/Developer ./scripts/build_ios_personal_xcode_macos.sh"
