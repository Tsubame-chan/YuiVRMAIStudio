#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
OUT_PARENT="${YUI_BACKEND_BUNDLE_OUT_PARENT:-$ROOT_DIR/releases/backend-bundle-windows}"
BUNDLE_DIR="$OUT_PARENT/YuiBackend"
INCLUDE_PYTHON_RUNTIME="${YUI_INCLUDE_WINDOWS_PYTHON_RUNTIME:-1}"
WINDOWS_PYTHON_VERSION="${YUI_WINDOWS_PYTHON_VERSION:-3.12.10}"
WINDOWS_PYTHON_ARCHIVE="python-${WINDOWS_PYTHON_VERSION}-embed-amd64.zip"
WINDOWS_PYTHON_URL="${YUI_WINDOWS_PYTHON_URL:-https://www.python.org/ftp/python/${WINDOWS_PYTHON_VERSION}/${WINDOWS_PYTHON_ARCHIVE}}"
WINDOWS_PYTHON_CACHE="${YUI_WINDOWS_PYTHON_CACHE:-$ROOT_DIR/.cache/windows-python/$WINDOWS_PYTHON_ARCHIVE}"

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

if [[ "$INCLUDE_PYTHON_RUNTIME" == "1" ]]; then
  PYTHON_MAJOR_MINOR="${WINDOWS_PYTHON_VERSION%.*}"
  PYTHON_ABI="cp${PYTHON_MAJOR_MINOR/./}"
  PYTHON_PTH_NAME="python${PYTHON_MAJOR_MINOR/./}._pth"
  PYTHON_RUNTIME_DIR="$BUNDLE_DIR/backend/.venv/Scripts"
  PYTHON_SITE_PACKAGES="$BUNDLE_DIR/backend/.venv/Lib/site-packages"
  TEMP_REQUIREMENTS="$(mktemp "${TMPDIR:-/tmp}/yui-windows-backend-requirements.XXXXXX")"
  trap 'rm -f "$TEMP_REQUIREMENTS"' EXIT

  mkdir -p "$(dirname "$WINDOWS_PYTHON_CACHE")" "$PYTHON_RUNTIME_DIR" "$PYTHON_SITE_PACKAGES"
  if [[ ! -f "$WINDOWS_PYTHON_CACHE" ]]; then
    TMP_DOWNLOAD="$WINDOWS_PYTHON_CACHE.download"
    rm -f "$TMP_DOWNLOAD"
    curl -L -o "$TMP_DOWNLOAD" "$WINDOWS_PYTHON_URL"
    mv "$TMP_DOWNLOAD" "$WINDOWS_PYTHON_CACHE"
  fi

  unzip -q "$WINDOWS_PYTHON_CACHE" -d "$PYTHON_RUNTIME_DIR"
  cat > "$PYTHON_RUNTIME_DIR/$PYTHON_PTH_NAME" <<PTH
python${PYTHON_MAJOR_MINOR/./}.zip
.
../Lib/site-packages
import site
PTH
  cat > "$BUNDLE_DIR/backend/.venv/pyvenv.cfg" <<CFG
home = Scripts
include-system-site-packages = false
version = $WINDOWS_PYTHON_VERSION
CFG

  sed 's/^uvicorn\[standard\]/uvicorn/' "$ROOT_DIR/backend/requirements.txt" > "$TEMP_REQUIREMENTS"
  PIP_PYTHON="${YUI_PIP_PYTHON:-$ROOT_DIR/backend/.venv/bin/python}"
  "$PIP_PYTHON" -m pip install --upgrade \
    --target "$PYTHON_SITE_PACKAGES" \
    --platform win_amd64 \
    --python-version "$PYTHON_MAJOR_MINOR" \
    --implementation cp \
    --abi "$PYTHON_ABI" \
    --only-binary=:all: \
    -r "$TEMP_REQUIREMENTS"
fi

if find "$BUNDLE_DIR" \( -name '.env' -o -name 'female_voice_3.aivmx' -o -name 'female_voice_3.json' \) | grep -q .; then
  echo "Backend bundle contains forbidden private/restricted files." >&2
  find "$BUNDLE_DIR" \( -name '.env' -o -name 'female_voice_3.aivmx' -o -name 'female_voice_3.json' \) >&2
  exit 1
fi

echo "Packaged Yui Windows backend bundle:"
echo "  $BUNDLE_DIR"
