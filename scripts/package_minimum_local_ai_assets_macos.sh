#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
VERSION="${YUI_RELEASE_VERSION:-v0.2.0-beta.1}"
OUT_DIR="${YUI_RELEASE_OUT_DIR:-$ROOT_DIR/releases/$VERSION}"
PACKAGE_NAME="YuiVRMAIStudio_LocalAIAssets_Minimum_${VERSION}.zip"
SPLIT_SIZE="${YUI_RELEASE_SPLIT_SIZE:-1900m}"

ASSET_ROOT="$ROOT_DIR/unity/Assets/StreamingAssets/YuiLocalAI"
REQUIRED_PATHS=(
  "$ASSET_ROOT/local_ai_model_packs.json"
  "$ASSET_ROOT/Models/gemma-4-E4B-it.litertlm"
  "$ASSET_ROOT/Voicevox/Models/meimei_himari_1.vvm"
  "$ASSET_ROOT/Voicevox/open_jtalk_dic_utf_8-1.11"
)

for required in "${REQUIRED_PATHS[@]}"; do
  if [[ ! -e "$required" ]]; then
    echo "Missing required minimum local asset: $required" >&2
    exit 1
  fi
done

mkdir -p "$OUT_DIR"
TMP_DIR="$(mktemp -d "${TMPDIR:-/tmp}/yui-min-local-assets.XXXXXX")"
trap 'rm -rf "$TMP_DIR"' EXIT

copy_path() {
  local relative="$1"
  local source="$ROOT_DIR/$relative"
  local destination="$TMP_DIR/$relative"
  mkdir -p "$(dirname "$destination")"
  if [[ -d "$source" ]]; then
    ditto "$source" "$destination"
  else
    cp -p "$source" "$destination"
  fi
  if [[ -f "$source.meta" ]]; then
    cp -p "$source.meta" "$destination.meta"
  fi
}

copy_path "unity/Assets/StreamingAssets/YuiLocalAI/local_ai_model_packs.json"
copy_path "unity/Assets/StreamingAssets/YuiLocalAI/Models"
rm -f "$TMP_DIR/unity/Assets/StreamingAssets/YuiLocalAI/Models/gemma-4-E2B-it.litertlm"*
rm -f "$TMP_DIR/unity/Assets/StreamingAssets/YuiLocalAI/Models/gemma-4-12B-it.litertlm"*
copy_path "unity/Assets/StreamingAssets/YuiLocalAI/Voicevox/Models"
copy_path "unity/Assets/StreamingAssets/YuiLocalAI/Voicevox/open_jtalk_dic_utf_8-1.11"

(
  cd "$TMP_DIR"
  ditto -c -k --sequesterRsrc --keepParent unity "$OUT_DIR/$PACKAGE_NAME"
)

if [[ "${YUI_RELEASE_SPLIT:-1}" != "0" ]]; then
  rm -f "$OUT_DIR/$PACKAGE_NAME.part-"*
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

echo "Packaged minimum local AI/TTS assets:"
echo "  $OUT_DIR/$PACKAGE_NAME"
echo "Checksums:"
echo "  $OUT_DIR/$PACKAGE_NAME.sha256"
