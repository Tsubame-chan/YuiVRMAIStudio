#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
OUT_PARENT="${YUI_BACKEND_BUNDLE_OUT_PARENT:-$ROOT_DIR/releases/backend-bundle-macos}"
BUNDLE_DIR="$OUT_PARENT/YuiBackend"
INCLUDE_VENV="${YUI_INCLUDE_BACKEND_VENV:-1}"
INCLUDE_TTS_TOOLS="${YUI_INCLUDE_BACKEND_TTS_TOOLS:-1}"

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
  aivis_model_sync_macos.sh \
  setup_backend_byok_macos.sh \
  start_local_services_macos.sh \
  start_local_services_detached_macos.sh \
  stop_local_services_macos.sh \
  run_backend_macos.sh \
  start_aivis_tts_macos.sh \
  start_litert_lm_server_macos.sh; do
  cp -p "$ROOT_DIR/scripts/$script" "$BUNDLE_DIR/scripts/$script"
done

cp -p "$ROOT_DIR/.env.example" "$BUNDLE_DIR/.env.example"
cp -p "$ROOT_DIR/packaging/backend/macos/Start_Yui_Backend.command" "$BUNDLE_DIR/Start_Yui_Backend.command"
cp -p "$ROOT_DIR/packaging/backend/macos/Stop_Yui_Backend.command" "$BUNDLE_DIR/Stop_Yui_Backend.command"
cp -p "$ROOT_DIR/packaging/backend/macos/README_BACKEND.md" "$BUNDLE_DIR/README_BACKEND.md"

if [[ "$INCLUDE_VENV" == "1" && -d "$ROOT_DIR/backend/.venv" ]]; then
  rsync -a --delete \
    --exclude '__pycache__/' \
    --exclude '*.pyc' \
    "$ROOT_DIR/backend/.venv/" "$BUNDLE_DIR/backend/.venv/"

  VENV_PYTHON="$ROOT_DIR/backend/.venv/bin/python3"
  if [[ -e "$VENV_PYTHON" ]]; then
    RESOLVED_VENV_PYTHON="$VENV_PYTHON"
    if [[ -L "$VENV_PYTHON" ]]; then
      RESOLVED_VENV_PYTHON="$(readlink "$VENV_PYTHON")"
      if [[ "$RESOLVED_VENV_PYTHON" != /* ]]; then
        RESOLVED_VENV_PYTHON="$(cd "$(dirname "$VENV_PYTHON")" && cd "$(dirname "$RESOLVED_VENV_PYTHON")" && pwd -P)/$(basename "$RESOLVED_VENV_PYTHON")"
      fi
    fi
    RESOLVED_LIBPYTHON="$(cd "$(dirname "$RESOLVED_VENV_PYTHON")/../lib" 2>/dev/null && pwd -P)/libpython3.12.dylib"
    if [[ -f "$RESOLVED_LIBPYTHON" ]]; then
      mkdir -p "$BUNDLE_DIR/backend/.venv/lib"
      cp -p "$RESOLVED_LIBPYTHON" "$BUNDLE_DIR/backend/.venv/lib/libpython3.12.dylib"
    fi
    RESOLVED_STDLIB_DIR="$(cd "$(dirname "$RESOLVED_VENV_PYTHON")/../lib/python3.12" 2>/dev/null && pwd -P || true)"
    if [[ -n "$RESOLVED_STDLIB_DIR" && -d "$RESOLVED_STDLIB_DIR" ]]; then
      mkdir -p "$BUNDLE_DIR/backend/.venv/lib/python3.12"
      rsync -a \
        --exclude 'site-packages/' \
        "$RESOLVED_STDLIB_DIR/" "$BUNDLE_DIR/backend/.venv/lib/python3.12/"
    fi
  fi
fi

if [[ "$INCLUDE_TTS_TOOLS" == "1" && -d "$ROOT_DIR/tools/tts" ]]; then
  mkdir -p "$BUNDLE_DIR/tools"
  rsync -a --delete \
    --exclude 'aivis-models/metadata/female_voice_3.json' \
    --exclude 'aivis-models/selected/female_voice_3.aivmx' \
    --exclude '__pycache__/' \
    --exclude '.pytest_cache/' \
    "$ROOT_DIR/tools/tts/" "$BUNDLE_DIR/tools/tts/"
fi

if find "$BUNDLE_DIR" \( -name '.env' -o -name 'female_voice_3.aivmx' -o -name 'female_voice_3.json' \) | grep -q .; then
  echo "Backend bundle contains forbidden private/restricted files." >&2
  find "$BUNDLE_DIR" \( -name '.env' -o -name 'female_voice_3.aivmx' -o -name 'female_voice_3.json' \) >&2
  exit 1
fi

chmod +x "$BUNDLE_DIR/Start_Yui_Backend.command" \
  "$BUNDLE_DIR/Stop_Yui_Backend.command" \
  "$BUNDLE_DIR/scripts/"*.sh

mkdir -p "$BUNDLE_DIR/backend/data" "$BUNDLE_DIR/logs" "$BUNDLE_DIR/runtime"

echo "Packaged Yui backend bundle:"
echo "  $BUNDLE_DIR"
