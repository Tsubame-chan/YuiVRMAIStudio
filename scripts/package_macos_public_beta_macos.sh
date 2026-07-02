#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
VERSION="${YUI_RELEASE_VERSION:-v0.2.0-beta.1}"
APP_SOURCE="${YUI_MACOS_APP_SOURCE:-$ROOT_DIR/builds/YuiVRMAIStudio_MacOSPublicBeta_$VERSION/Yui VRM AI Studio.app}"
OUT_DIR="${YUI_RELEASE_OUT_DIR:-$ROOT_DIR/releases/$VERSION}"
PACKAGE_NAME="YuiVRMAIStudio_MacOSPublicBeta_${VERSION}_macos.zip"
SPLIT_SIZE="${YUI_RELEASE_SPLIT_SIZE:-1900m}"

if [[ ! -d "$APP_SOURCE" ]]; then
  echo "Missing macOS app bundle: $APP_SOURCE" >&2
  exit 1
fi

mkdir -p "$OUT_DIR"
rm -f "$OUT_DIR/$PACKAGE_NAME" "$OUT_DIR/$PACKAGE_NAME.part-"* "$OUT_DIR/$PACKAGE_NAME.sha256"

(
  cd "$(dirname "$APP_SOURCE")"
  ditto -c -k --norsrc --noextattr --noqtn --noacl --keepParent "$(basename "$APP_SOURCE")" "$OUT_DIR/$PACKAGE_NAME"
)

if unzip -Z1 "$OUT_DIR/$PACKAGE_NAME" | grep -q '^__MACOSX/'; then
  echo "Archive contains __MACOSX metadata: $OUT_DIR/$PACKAGE_NAME" >&2
  exit 1
fi

if [[ "${YUI_RELEASE_SPLIT:-1}" != "0" ]]; then
  split -b "$SPLIT_SIZE" -d -a 3 "$OUT_DIR/$PACKAGE_NAME" "$OUT_DIR/$PACKAGE_NAME.part-"
  (
    cd "$OUT_DIR"
    shasum -a 256 "$PACKAGE_NAME" "$PACKAGE_NAME.part-"* > "$PACKAGE_NAME.sha256"
  )
else
  (
    cd "$OUT_DIR"
    shasum -a 256 "$PACKAGE_NAME" > "$PACKAGE_NAME.sha256"
  )
fi

echo "Packaged macOS public beta app:"
echo "  $OUT_DIR/$PACKAGE_NAME"
echo "Checksums:"
echo "  $OUT_DIR/$PACKAGE_NAME.sha256"
