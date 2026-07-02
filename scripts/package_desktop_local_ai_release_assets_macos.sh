#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
VERSION="${YUI_RELEASE_VERSION:-v0.2.0-beta.2}"
OUT_DIR="${YUI_RELEASE_OUT_DIR:-$ROOT_DIR/releases/$VERSION}"
PACKAGE_NAME="YuiVRMAIStudio_LocalAIAssets_DesktopMinimum_${VERSION}.zip"
MANIFEST_NAME="YuiVRMAIStudio_AssetManifest.json"
SPLIT_SIZE="${YUI_RELEASE_SPLIT_SIZE:-1900m}"
DOWNLOAD_BASE="${YUI_RELEASE_DOWNLOAD_BASE:-https://github.com/Tsubame-chan/YuiVRMAIStudio/releases/download/$VERSION}"
FULL_ZIP_URL="${YUI_FULL_ZIP_URL:-}"

ASSET_ROOT="$ROOT_DIR/unity/Assets/StreamingAssets/YuiLocalAI"
REQUIRED_PATHS=(
  "$ASSET_ROOT/local_ai_model_packs.json"
  "$ASSET_ROOT/Models/gemma-4-E4B-it.litertlm"
  "$ASSET_ROOT/Voicevox/Models/meimei_himari_1.vvm"
  "$ASSET_ROOT/Voicevox/open_jtalk_dic_utf_8-1.11"
)

for required in "${REQUIRED_PATHS[@]}"; do
  if [[ ! -e "$required" ]]; then
    echo "Missing required local AI asset: $required" >&2
    exit 1
  fi
done

mkdir -p "$OUT_DIR"
TMP_DIR="$(mktemp -d "${TMPDIR:-/tmp}/yui-desktop-local-ai-release.XXXXXX")"
PAYLOAD_DIR="$TMP_DIR/payload"
trap 'rm -rf "$TMP_DIR"' EXIT

copy_payload() {
  local source="$1"
  local destination="$2"
  mkdir -p "$(dirname "$destination")"
  if [[ -d "$source" ]]; then
    ditto "$source" "$destination"
  else
    cp -p "$source" "$destination"
  fi
}

copy_payload "$ASSET_ROOT/local_ai_model_packs.json" "$PAYLOAD_DIR/local_ai_model_packs.json"
copy_payload "$ASSET_ROOT/Models/gemma-4-E4B-it.litertlm" "$PAYLOAD_DIR/Models/gemma-4-E4B-it.litertlm"
copy_payload "$ASSET_ROOT/Voicevox/Models" "$PAYLOAD_DIR/Voicevox/Models"
copy_payload "$ASSET_ROOT/Voicevox/open_jtalk_dic_utf_8-1.11" "$PAYLOAD_DIR/Voicevox/open_jtalk_dic_utf_8-1.11"

rm -f "$OUT_DIR/$PACKAGE_NAME" "$OUT_DIR/$PACKAGE_NAME.part-"* "$OUT_DIR/$PACKAGE_NAME.sha256" "$OUT_DIR/$MANIFEST_NAME"
(
  cd "$PAYLOAD_DIR"
  zip -q -r -X "$OUT_DIR/$PACKAGE_NAME" .
)

if unzip -Z1 "$OUT_DIR/$PACKAGE_NAME" | grep -q '^__MACOSX/'; then
  echo "Archive contains __MACOSX metadata: $OUT_DIR/$PACKAGE_NAME" >&2
  exit 1
fi

split -b "$SPLIT_SIZE" -d -a 3 "$OUT_DIR/$PACKAGE_NAME" "$OUT_DIR/$PACKAGE_NAME.part-"
(
  cd "$OUT_DIR"
  shasum -a 256 "$PACKAGE_NAME" "$PACKAGE_NAME.part-"* > "$PACKAGE_NAME.sha256"
)

json_escape() {
  printf '%s' "$1" | sed 's/\\/\\\\/g; s/"/\\"/g'
}

file_size() {
  stat -f '%z' "$1"
}

file_sha256() {
  shasum -a 256 "$1" | awk '{print $1}'
}

ZIP_SIZE="$(file_size "$OUT_DIR/$PACKAGE_NAME")"
ZIP_SHA="$(file_sha256 "$OUT_DIR/$PACKAGE_NAME")"
PARTS_JSON=""
for part_path in "$OUT_DIR/$PACKAGE_NAME.part-"*; do
  part_file="$(basename "$part_path")"
  part_sha="$(file_sha256 "$part_path")"
  part_size="$(file_size "$part_path")"
  part_json="        {
          \"filename\": \"$(json_escape "$part_file")\",
          \"url\": \"$(json_escape "$DOWNLOAD_BASE/$part_file")\",
          \"sha256\": \"$part_sha\",
          \"size_bytes\": $part_size
        }"
  if [[ -n "$PARTS_JSON" ]]; then
    PARTS_JSON="$PARTS_JSON,
$part_json"
  else
    PARTS_JSON="$part_json"
  fi
done

cat > "$OUT_DIR/$MANIFEST_NAME" <<JSON
{
  "schema_version": 1,
  "release_version": "$VERSION",
  "minimum_app_version": "${YUI_MINIMUM_APP_VERSION:-0.2.0-beta.2}",
  "assets": [
    {
      "id": "desktop-local-ai-minimum",
      "display_name": "Yui Desktop Local AI minimum assets",
      "kind": "desktop_local_ai_minimum",
      "platforms": ["macos", "windows"],
      "required_for": ["local_chat", "local_tts"],
      "optional": false,
      "version": "${YUI_LOCAL_AI_ASSET_VERSION:-2026.07.02}",
      "filename": "$(json_escape "$PACKAGE_NAME")",
      "url": "$(json_escape "$FULL_ZIP_URL")",
      "parts": [
$PARTS_JSON
      ],
      "sha256": "$ZIP_SHA",
      "size_bytes": $ZIP_SIZE,
      "install_root": "YuiLocalAI",
      "installed_paths": [
        "local_ai_model_packs.json",
        "Models/gemma-4-E4B-it.litertlm",
        "Voicevox/Models/meimei_himari_1.vvm",
        "Voicevox/open_jtalk_dic_utf_8-1.11"
      ]
    }
  ]
}
JSON

echo "Packaged desktop local AI release assets:"
echo "  $OUT_DIR/$PACKAGE_NAME"
echo "  $OUT_DIR/$PACKAGE_NAME.sha256"
echo "  $OUT_DIR/$MANIFEST_NAME"

if [[ "${YUI_KEEP_FULL_ZIP:-0}" != "1" ]]; then
  rm -f "$OUT_DIR/$PACKAGE_NAME"
  echo "Removed unsplit local ZIP after generating release parts:"
  echo "  $OUT_DIR/$PACKAGE_NAME"
fi
