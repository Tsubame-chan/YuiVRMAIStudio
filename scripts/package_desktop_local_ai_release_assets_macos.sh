#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
VERSION="${YUI_RELEASE_VERSION:-v0.2.0-beta.2}"
OUT_DIR="${YUI_RELEASE_OUT_DIR:-$ROOT_DIR/releases/$VERSION}"
PACKAGE_NAME="YuiVRMAIStudio_LocalAIAssets_DesktopMinimum_${VERSION}.zip"
BACKEND_PACKAGE_NAME="YuiVRMAIStudio_BackendBundle_${VERSION}_macos.zip"
WINDOWS_BACKEND_PACKAGE_NAME="YuiVRMAIStudio_BackendBundle_${VERSION}_windows.zip"
MANIFEST_NAME="YuiVRMAIStudio_AssetManifest.json"
SPLIT_SIZE="${YUI_RELEASE_SPLIT_SIZE:-1900m}"
DOWNLOAD_BASE="${YUI_RELEASE_DOWNLOAD_BASE:-https://github.com/Tsubame-chan/YuiVRMAIStudio/releases/download/$VERSION}"
FULL_ZIP_URL="${YUI_FULL_ZIP_URL:-}"
INCLUDE_BACKEND_ASSET="${YUI_INCLUDE_BACKEND_RELEASE_ASSET:-1}"

ASSET_ROOT="$ROOT_DIR/unity/Assets/StreamingAssets/YuiLocalAI"
DESKTOP_GEMMA_MODEL="gemma-4-E4B-it.litertlm"
if [[ ! -f "$ASSET_ROOT/Models/$DESKTOP_GEMMA_MODEL" && -f "$ASSET_ROOT/Models/gemma-4-E2B-it.litertlm" ]]; then
  DESKTOP_GEMMA_MODEL="gemma-4-E2B-it.litertlm"
fi
REQUIRED_PATHS=(
  "$ASSET_ROOT/local_ai_model_packs.json"
  "$ASSET_ROOT/Models/$DESKTOP_GEMMA_MODEL"
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
copy_payload "$ASSET_ROOT/Models/$DESKTOP_GEMMA_MODEL" "$PAYLOAD_DIR/Models/$DESKTOP_GEMMA_MODEL"
copy_payload "$ASSET_ROOT/Voicevox/Models" "$PAYLOAD_DIR/Voicevox/Models"
copy_payload "$ASSET_ROOT/Voicevox/open_jtalk_dic_utf_8-1.11" "$PAYLOAD_DIR/Voicevox/open_jtalk_dic_utf_8-1.11"
find "$PAYLOAD_DIR" -name '*.meta' -type f -delete

rm -f "$OUT_DIR/$PACKAGE_NAME" "$OUT_DIR/$PACKAGE_NAME.part-"* "$OUT_DIR/$PACKAGE_NAME.sha256" \
  "$OUT_DIR/$BACKEND_PACKAGE_NAME" "$OUT_DIR/$BACKEND_PACKAGE_NAME.part-"* "$OUT_DIR/$BACKEND_PACKAGE_NAME.sha256" \
  "$OUT_DIR/$WINDOWS_BACKEND_PACKAGE_NAME" "$OUT_DIR/$WINDOWS_BACKEND_PACKAGE_NAME.part-"* "$OUT_DIR/$WINDOWS_BACKEND_PACKAGE_NAME.sha256" \
  "$OUT_DIR/$MANIFEST_NAME"
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

parts_json_for_package() {
  local package="$1"
  local parts_json=""
  local part_path part_file part_sha part_size part_json
  for part_path in "$OUT_DIR/$package.part-"*; do
    [[ -e "$part_path" ]] || continue
    part_file="$(basename "$part_path")"
    part_sha="$(file_sha256 "$part_path")"
    part_size="$(file_size "$part_path")"
    part_json="        {
          \"filename\": \"$(json_escape "$part_file")\",
          \"url\": \"$(json_escape "$DOWNLOAD_BASE/$part_file")\",
          \"sha256\": \"$part_sha\",
          \"size_bytes\": $part_size
        }"
    if [[ -n "$parts_json" ]]; then
      parts_json="$parts_json,
$part_json"
    else
      parts_json="$part_json"
    fi
  done
  printf '%s\n' "$parts_json"
}

ZIP_SIZE="$(file_size "$OUT_DIR/$PACKAGE_NAME")"
ZIP_SHA="$(file_sha256 "$OUT_DIR/$PACKAGE_NAME")"
PARTS_JSON="$(parts_json_for_package "$PACKAGE_NAME")"
BACKEND_ASSET_JSON=""

if [[ "$INCLUDE_BACKEND_ASSET" == "1" ]]; then
  BACKEND_PARENT="$TMP_DIR/backend-release"
  YUI_BACKEND_BUNDLE_OUT_PARENT="$BACKEND_PARENT" \
    YUI_INCLUDE_BACKEND_TTS_TOOLS="${YUI_INCLUDE_BACKEND_TTS_TOOLS:-0}" \
    "$ROOT_DIR/scripts/package_backend_bundle_macos.sh"
  (
    cd "$BACKEND_PARENT/YuiBackend"
    zip -q -r -X "$OUT_DIR/$BACKEND_PACKAGE_NAME" .
  )
  if unzip -Z1 "$OUT_DIR/$BACKEND_PACKAGE_NAME" | grep -Eq '(^|/)__MACOSX(/|$)|(^|/)female_voice_3\.(aivmx|json)$|(^|/)\.env$'; then
    echo "Backend archive contains forbidden private/restricted files: $OUT_DIR/$BACKEND_PACKAGE_NAME" >&2
    exit 1
  fi
  split -b "$SPLIT_SIZE" -d -a 3 "$OUT_DIR/$BACKEND_PACKAGE_NAME" "$OUT_DIR/$BACKEND_PACKAGE_NAME.part-"
  (
    cd "$OUT_DIR"
    shasum -a 256 "$BACKEND_PACKAGE_NAME" "$BACKEND_PACKAGE_NAME.part-"* > "$BACKEND_PACKAGE_NAME.sha256"
  )
  BACKEND_ZIP_SIZE="$(file_size "$OUT_DIR/$BACKEND_PACKAGE_NAME")"
  BACKEND_ZIP_SHA="$(file_sha256 "$OUT_DIR/$BACKEND_PACKAGE_NAME")"
  BACKEND_PARTS_JSON="$(parts_json_for_package "$BACKEND_PACKAGE_NAME")"
  BACKEND_ASSET_JSON=",
    {
      \"id\": \"desktop-backend-bundle-macos\",
      \"display_name\": \"Yui macOS bundled backend\",
      \"kind\": \"desktop_backend_bundle\",
      \"platforms\": [\"macos\"],
      \"required_for\": [\"desktop_backend\", \"web_search\", \"backend_tts\", \"memory\"],
      \"optional\": false,
      \"version\": \"${YUI_BACKEND_ASSET_VERSION:-2026.07.04}\",
      \"filename\": \"$(json_escape "$BACKEND_PACKAGE_NAME")\",
      \"url\": \"$(json_escape "$DOWNLOAD_BASE/$BACKEND_PACKAGE_NAME")\",
      \"parts\": [
$BACKEND_PARTS_JSON
      ],
      \"sha256\": \"$BACKEND_ZIP_SHA\",
      \"size_bytes\": $BACKEND_ZIP_SIZE,
      \"install_root\": \"YuiBackend\",
      \"installed_paths\": [
        \"Start_Yui_Backend.command\",
        \"Stop_Yui_Backend.command\",
        \"scripts/start_local_services_detached_macos.sh\",
        \"scripts/stop_local_services_macos.sh\",
        \"backend/main.py\",
        \"backend/app/main.py\"
      ]
    }"

  WINDOWS_BACKEND_PARENT="$TMP_DIR/backend-release-windows"
  YUI_BACKEND_BUNDLE_OUT_PARENT="$WINDOWS_BACKEND_PARENT" \
    "$ROOT_DIR/scripts/package_backend_bundle_windows_macos.sh"
  (
    cd "$WINDOWS_BACKEND_PARENT/YuiBackend"
    zip -q -r -X "$OUT_DIR/$WINDOWS_BACKEND_PACKAGE_NAME" .
  )
  if unzip -Z1 "$OUT_DIR/$WINDOWS_BACKEND_PACKAGE_NAME" | grep -Eq '(^|/)__MACOSX(/|$)|(^|/)female_voice_3\.(aivmx|json)$|(^|/)\.env$'; then
    echo "Windows backend archive contains forbidden private/restricted files: $OUT_DIR/$WINDOWS_BACKEND_PACKAGE_NAME" >&2
    exit 1
  fi
  split -b "$SPLIT_SIZE" -d -a 3 "$OUT_DIR/$WINDOWS_BACKEND_PACKAGE_NAME" "$OUT_DIR/$WINDOWS_BACKEND_PACKAGE_NAME.part-"
  (
    cd "$OUT_DIR"
    shasum -a 256 "$WINDOWS_BACKEND_PACKAGE_NAME" "$WINDOWS_BACKEND_PACKAGE_NAME.part-"* > "$WINDOWS_BACKEND_PACKAGE_NAME.sha256"
  )
  WINDOWS_BACKEND_ZIP_SIZE="$(file_size "$OUT_DIR/$WINDOWS_BACKEND_PACKAGE_NAME")"
  WINDOWS_BACKEND_ZIP_SHA="$(file_sha256 "$OUT_DIR/$WINDOWS_BACKEND_PACKAGE_NAME")"
  WINDOWS_BACKEND_PARTS_JSON="$(parts_json_for_package "$WINDOWS_BACKEND_PACKAGE_NAME")"
  BACKEND_ASSET_JSON="$BACKEND_ASSET_JSON,
    {
      \"id\": \"desktop-backend-bundle-windows\",
      \"display_name\": \"Yui Windows bundled backend\",
      \"kind\": \"desktop_backend_bundle\",
      \"platforms\": [\"windows\"],
      \"required_for\": [\"desktop_backend\", \"web_search\", \"backend_tts\", \"memory\"],
      \"optional\": false,
      \"version\": \"${YUI_BACKEND_ASSET_VERSION:-2026.07.04}\",
      \"filename\": \"$(json_escape "$WINDOWS_BACKEND_PACKAGE_NAME")\",
      \"url\": \"$(json_escape "$DOWNLOAD_BASE/$WINDOWS_BACKEND_PACKAGE_NAME")\",
      \"parts\": [
$WINDOWS_BACKEND_PARTS_JSON
      ],
      \"sha256\": \"$WINDOWS_BACKEND_ZIP_SHA\",
      \"size_bytes\": $WINDOWS_BACKEND_ZIP_SIZE,
      \"install_root\": \"YuiBackend\",
      \"installed_paths\": [
        \"Start_Yui_Backend.bat\",
        \"Stop_Yui_Backend.bat\",
        \"scripts/start_local_services.ps1\",
        \"scripts/setup_backend_byok.ps1\",
        \"scripts/stop_local_services.ps1\",
        \"backend/main.py\",
        \"backend/app/main.py\"
      ]
    }"
fi

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
        "Models/$DESKTOP_GEMMA_MODEL",
        "Voicevox/Models/meimei_himari_1.vvm",
        "Voicevox/open_jtalk_dic_utf_8-1.11"
      ]
    }$BACKEND_ASSET_JSON
  ]
}
JSON

echo "Packaged desktop local AI release assets:"
echo "  $OUT_DIR/$PACKAGE_NAME"
echo "  $OUT_DIR/$PACKAGE_NAME.sha256"
if [[ "$INCLUDE_BACKEND_ASSET" == "1" ]]; then
  echo "  $OUT_DIR/$BACKEND_PACKAGE_NAME"
  echo "  $OUT_DIR/$BACKEND_PACKAGE_NAME.sha256"
  echo "  $OUT_DIR/$WINDOWS_BACKEND_PACKAGE_NAME"
  echo "  $OUT_DIR/$WINDOWS_BACKEND_PACKAGE_NAME.sha256"
fi
echo "  $OUT_DIR/$MANIFEST_NAME"

if [[ "${YUI_KEEP_FULL_ZIP:-0}" != "1" ]]; then
  rm -f "$OUT_DIR/$PACKAGE_NAME"
  if [[ "$INCLUDE_BACKEND_ASSET" == "1" ]]; then
    rm -f "$OUT_DIR/$BACKEND_PACKAGE_NAME"
    rm -f "$OUT_DIR/$WINDOWS_BACKEND_PACKAGE_NAME"
  fi
  sed -i '' "/  $(printf '%s' "$PACKAGE_NAME" | sed 's/[.[\*^$()+?{}|\\]/\\&/g')$/d" "$OUT_DIR/$PACKAGE_NAME.sha256"
  if [[ "$INCLUDE_BACKEND_ASSET" == "1" ]]; then
    sed -i '' "/  $(printf '%s' "$BACKEND_PACKAGE_NAME" | sed 's/[.[\*^$()+?{}|\\]/\\&/g')$/d" "$OUT_DIR/$BACKEND_PACKAGE_NAME.sha256"
    sed -i '' "/  $(printf '%s' "$WINDOWS_BACKEND_PACKAGE_NAME" | sed 's/[.[\*^$()+?{}|\\]/\\&/g')$/d" "$OUT_DIR/$WINDOWS_BACKEND_PACKAGE_NAME.sha256"
  fi
  echo "Removed unsplit local ZIP after generating release parts:"
  echo "  $OUT_DIR/$PACKAGE_NAME"
  if [[ "$INCLUDE_BACKEND_ASSET" == "1" ]]; then
    echo "  $OUT_DIR/$BACKEND_PACKAGE_NAME"
    echo "  $OUT_DIR/$WINDOWS_BACKEND_PACKAGE_NAME"
  fi
fi
