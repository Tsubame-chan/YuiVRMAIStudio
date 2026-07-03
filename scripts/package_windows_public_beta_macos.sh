#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
VERSION="${YUI_RELEASE_VERSION:-v0.2.0-beta.3}"
APP_SOURCE="${YUI_WINDOWS_APP_SOURCE:-$ROOT_DIR/builds/YuiVRMAIStudio_WindowsPublicBeta_$VERSION}"
OUT_DIR="${YUI_RELEASE_OUT_DIR:-$ROOT_DIR/releases/$VERSION}"
PACKAGE_NAME="YuiVRMAIStudio_WindowsPublicBeta_${VERSION}_windows.zip"
SPLIT_SIZE="${YUI_RELEASE_SPLIT_SIZE:-1900m}"

if [[ ! -d "$APP_SOURCE" ]]; then
  echo "Missing Windows app build directory: $APP_SOURCE" >&2
  exit 1
fi

if [[ ! -f "$APP_SOURCE/Yui VRM AI Studio.exe" ]]; then
  echo "Missing Windows executable: $APP_SOURCE/Yui VRM AI Studio.exe" >&2
  exit 1
fi

if [[ ! -f "$APP_SOURCE/YuiFilePickerHelper.exe" ]]; then
  HELPER_SOURCE="$ROOT_DIR/tools/YuiFilePickerHelper/YuiFilePickerHelper.cs"
  if [[ ! -f "$HELPER_SOURCE" ]]; then
    echo "Missing Windows file picker helper source: $HELPER_SOURCE" >&2
    exit 1
  fi
  if ! command -v mcs >/dev/null 2>&1; then
    echo "Missing Windows file picker helper and mcs is not available to build it: $APP_SOURCE/YuiFilePickerHelper.exe" >&2
    exit 1
  fi
  mcs -target:winexe -sdk:4.5 \
    -r:System.Windows.Forms.dll \
    -r:System.Drawing.dll \
    -out:"$APP_SOURCE/YuiFilePickerHelper.exe" \
    "$HELPER_SOURCE"
fi

mkdir -p "$OUT_DIR"
rm -f "$OUT_DIR/$PACKAGE_NAME" "$OUT_DIR/$PACKAGE_NAME.part-"* "$OUT_DIR/$PACKAGE_NAME.sha256"

(
  cd "$(dirname "$APP_SOURCE")"
  COPYFILE_DISABLE=1 zip -q -r -X "$OUT_DIR/$PACKAGE_NAME" "$(basename "$APP_SOURCE")" \
    -x "*/__MACOSX/*" "*/._*"
)

if unzip -Z1 "$OUT_DIR/$PACKAGE_NAME" | grep -Eq '(^|/)__MACOSX(/|$)|(^|/)\._[^/]+$'; then
  echo "Archive contains macOS metadata: $OUT_DIR/$PACKAGE_NAME" >&2
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

echo "Packaged Windows public beta app:"
echo "  $OUT_DIR/$PACKAGE_NAME"
echo "Checksums:"
echo "  $OUT_DIR/$PACKAGE_NAME.sha256"
