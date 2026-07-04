#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
OUT_PARENT="${YUI_BACKEND_BUNDLE_OUT_PARENT:-$ROOT_DIR/releases/backend-bundle-windows}"
BUNDLE_DIR="$OUT_PARENT/YuiBackend"

rm -rf "$BUNDLE_DIR"
mkdir -p "$BUNDLE_DIR"

rsync -a --delete \
  --exclude '.venv/' \
  --exclude '__pycache__/' \
  --exclude '.pytest_cache/' \
  --exclude 'data/*.db' \
  --exclude 'data/*.db-*' \
  --exclude 'data/audio/' \
  "$ROOT_DIR/backend/" "$BUNDLE_DIR/backend/"

mkdir -p "$BUNDLE_DIR/scripts"
for script in \
  setup_backend_byok.ps1 \
  start_local_services.ps1 \
  stop_local_services.ps1 \
  run_backend.ps1 \
  run_voicevox_engine_optimized.ps1; do
  cp -p "$ROOT_DIR/scripts/$script" "$BUNDLE_DIR/scripts/$script"
done

cp -p "$ROOT_DIR/.env.example" "$BUNDLE_DIR/.env.example"
cp -p "$ROOT_DIR/packaging/backend/windows/Start_Yui_Backend.bat" "$BUNDLE_DIR/Start_Yui_Backend.bat"
cp -p "$ROOT_DIR/packaging/backend/windows/Stop_Yui_Backend.bat" "$BUNDLE_DIR/Stop_Yui_Backend.bat"
cp -p "$ROOT_DIR/packaging/backend/windows/README_BACKEND_WINDOWS.md" "$BUNDLE_DIR/README_BACKEND_WINDOWS.md"

mkdir -p "$BUNDLE_DIR/backend/data" "$BUNDLE_DIR/logs" "$BUNDLE_DIR/runtime"

if find "$BUNDLE_DIR" \( -name '.env' -o -name 'female_voice_3.aivmx' -o -name 'female_voice_3.json' \) | grep -q .; then
  echo "Backend bundle contains forbidden private/restricted files." >&2
  find "$BUNDLE_DIR" \( -name '.env' -o -name 'female_voice_3.aivmx' -o -name 'female_voice_3.json' \) >&2
  exit 1
fi

echo "Packaged Yui Windows backend bundle:"
echo "  $BUNDLE_DIR"
