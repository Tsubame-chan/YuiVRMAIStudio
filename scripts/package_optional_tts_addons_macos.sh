#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
VERSION="${YUI_RELEASE_VERSION:-v0.2.0-beta.3}"
OUT_DIR="${YUI_RELEASE_OUT_DIR:-$ROOT_DIR/releases/$VERSION}"
DOWNLOAD_BASE="${YUI_RELEASE_DOWNLOAD_BASE:-https://github.com/Tsubame-chan/YuiVRMAIStudio/releases/download/$VERSION}"
MANIFEST_NAME="YuiVRMAIStudio_AssetManifest.json"
PACKAGE_NAME="YuiVRMAIStudio_TTSAddon_AivisSpeechHD_${VERSION}_macos.zip"
ASSET_JSON_NAME="YuiVRMAIStudio_TTSAddon_AivisSpeechHD_${VERSION}_macos.asset.json"
ASSET_VERSION="${YUI_TTS_ADDON_ASSET_VERSION:-2026.07.08}"
UPDATE_MANIFEST="${YUI_UPDATE_RELEASE_MANIFEST:-1}"

ENGINE_ROOT="$ROOT_DIR/tools/tts/aivis-engine/extracted/macOS-arm64"
MODEL_ROOT="$ROOT_DIR/tools/tts/aivis-models"
SELECTED_MODEL_ROOT="$MODEL_ROOT/selected"
MODEL_METADATA_ROOT="$MODEL_ROOT/metadata"

REQUIRED_PATHS=(
  "$ENGINE_ROOT/run"
  "$ENGINE_ROOT/engine_manifest.json"
  "$ENGINE_ROOT/resources/engine_manifest_assets/terms_of_service.md"
  "$ENGINE_ROOT/resources/engine_manifest_assets/dependency_licenses.json"
  "$SELECTED_MODEL_ROOT/female_voice_1.aivmx"
  "$SELECTED_MODEL_ROOT/female_voice_2.aivmx"
  "$SELECTED_MODEL_ROOT/male_voice_1.aivmx"
  "$MODEL_METADATA_ROOT/female_voice_1.json"
  "$MODEL_METADATA_ROOT/female_voice_2.json"
  "$MODEL_METADATA_ROOT/male_voice_1.json"
)

for required in "${REQUIRED_PATHS[@]}"; do
  if [[ ! -e "$required" ]]; then
    echo "Missing required optional TTS asset: $required" >&2
    exit 1
  fi
done

if [[ -e "$SELECTED_MODEL_ROOT/female_voice_3.aivmx" || -e "$MODEL_METADATA_ROOT/female_voice_3.json" ]]; then
  echo "Restricted female_voice_3 is present in the source tree and will not be packaged." >&2
  exit 1
fi

mkdir -p "$OUT_DIR"
TMP_DIR="$(mktemp -d "${TMPDIR:-/tmp}/yui-optional-tts-release.XXXXXX")"
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

copy_payload "$ENGINE_ROOT" "$PAYLOAD_DIR/tools/tts/aivis-engine/extracted/macOS-arm64"
mkdir -p "$PAYLOAD_DIR/tools/tts/aivis-models/selected" "$PAYLOAD_DIR/tools/tts/aivis-models/metadata"
for voice in female_voice_1 female_voice_2 male_voice_1; do
  copy_payload "$SELECTED_MODEL_ROOT/$voice.aivmx" "$PAYLOAD_DIR/tools/tts/aivis-models/selected/$voice.aivmx"
  copy_payload "$MODEL_METADATA_ROOT/$voice.json" "$PAYLOAD_DIR/tools/tts/aivis-models/metadata/$voice.json"
done

cat > "$PAYLOAD_DIR/tools/tts/aivis-models/README_YUI_AIVIS_ADDON.md" <<'EOF'
# Yui AivisSpeech HD Add-on

This optional package contains redistributable AivisSpeech HD model files selected for Yui VRM AI Studio.

The restricted female_voice_3 model is intentionally not included.
Model license metadata is kept next to each model under `tools/tts/aivis-models/metadata/`.
EOF

find "$PAYLOAD_DIR" -name '*.meta' -type f -delete
chmod +x "$PAYLOAD_DIR/tools/tts/aivis-engine/extracted/macOS-arm64/run" || true

if find "$PAYLOAD_DIR" \( -name '.env' -o -name 'female_voice_3.aivmx' -o -name 'female_voice_3.json' \) | grep -q .; then
  echo "Optional TTS payload contains forbidden private/restricted files." >&2
  find "$PAYLOAD_DIR" \( -name '.env' -o -name 'female_voice_3.aivmx' -o -name 'female_voice_3.json' \) >&2
  exit 1
fi

rm -f "$OUT_DIR/$PACKAGE_NAME" "$OUT_DIR/$PACKAGE_NAME.sha256" "$OUT_DIR/$ASSET_JSON_NAME"
(
  cd "$PAYLOAD_DIR"
  zip -q -r -X "$OUT_DIR/$PACKAGE_NAME" .
)

if unzip -Z1 "$OUT_DIR/$PACKAGE_NAME" | grep -Eq '(^|/)__MACOSX(/|$)|(^|/)female_voice_3\.(aivmx|json)$|(^|/)\.env$|\.meta$'; then
  echo "Optional TTS archive contains forbidden files: $OUT_DIR/$PACKAGE_NAME" >&2
  exit 1
fi

file_size() {
  stat -f '%z' "$1"
}

file_sha256() {
  shasum -a 256 "$1" | awk '{print $1}'
}

json_escape() {
  printf '%s' "$1" | sed 's/\\/\\\\/g; s/"/\\"/g'
}

ZIP_SIZE="$(file_size "$OUT_DIR/$PACKAGE_NAME")"
ZIP_SHA="$(file_sha256 "$OUT_DIR/$PACKAGE_NAME")"
(
  cd "$OUT_DIR"
  shasum -a 256 "$PACKAGE_NAME" > "$PACKAGE_NAME.sha256"
)

cat > "$OUT_DIR/$ASSET_JSON_NAME" <<JSON
{
  "id": "tts-addon-aivis-speech-hd-macos",
  "display_name": "AivisSpeech HD add-on for macOS",
  "kind": "optional_tts_addon",
  "platforms": ["macos"],
  "required_for": ["backend_tts", "aivis"],
  "optional": true,
  "version": "$ASSET_VERSION",
  "filename": "$(json_escape "$PACKAGE_NAME")",
  "url": "$(json_escape "$DOWNLOAD_BASE/$PACKAGE_NAME")",
  "parts": [],
  "sha256": "$ZIP_SHA",
  "size_bytes": $ZIP_SIZE,
  "install_root": "YuiBackend",
  "installed_paths": [
    "tools/tts/aivis-engine/extracted/macOS-arm64/run",
    "tools/tts/aivis-engine/extracted/macOS-arm64/engine_manifest.json",
    "tools/tts/aivis-engine/extracted/macOS-arm64/resources/engine_manifest_assets/terms_of_service.md",
    "tools/tts/aivis-engine/extracted/macOS-arm64/resources/engine_manifest_assets/dependency_licenses.json",
    "tools/tts/aivis-models/selected/female_voice_1.aivmx",
    "tools/tts/aivis-models/selected/female_voice_2.aivmx",
    "tools/tts/aivis-models/selected/male_voice_1.aivmx",
    "tools/tts/aivis-models/metadata/female_voice_1.json",
    "tools/tts/aivis-models/metadata/female_voice_2.json",
    "tools/tts/aivis-models/metadata/male_voice_1.json",
    "tools/tts/aivis-models/README_YUI_AIVIS_ADDON.md"
  ]
}
JSON

if [[ "$UPDATE_MANIFEST" == "1" && -f "$OUT_DIR/$MANIFEST_NAME" ]]; then
  PYTHON_BIN="${YUI_PYTHON_BIN:-$ROOT_DIR/backend/.venv/bin/python}"
  if [[ ! -x "$PYTHON_BIN" ]]; then
    PYTHON_BIN="python3"
  fi
  "$PYTHON_BIN" "$ROOT_DIR/scripts/merge_release_manifest_asset.py" \
    --manifest "$OUT_DIR/$MANIFEST_NAME" \
    --asset-json "$OUT_DIR/$ASSET_JSON_NAME"
fi

echo "Packaged optional TTS add-on:"
echo "  $OUT_DIR/$PACKAGE_NAME"
echo "  $OUT_DIR/$PACKAGE_NAME.sha256"
echo "  $OUT_DIR/$ASSET_JSON_NAME"
