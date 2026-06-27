#!/usr/bin/env bash
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
UNITY_PROJECT="$REPO_ROOT/unity"
LOG_DIR="$REPO_ROOT/logs"
UNITY_EDITOR="${UNITY_EDITOR:-/Applications/Unity/Hub/Editor/2022.3.62f3/Unity.app/Contents/MacOS/Unity}"
RUN_ID="$(date +%Y%m%d-%H%M%S)"
LOG_FILE="$LOG_DIR/unity-ios-personal-export-$RUN_ID.log"

if [[ ! -x "$UNITY_EDITOR" ]]; then
  echo "Unity editor executable not found: $UNITY_EDITOR" >&2
  echo "Example:" >&2
  echo "UNITY_EDITOR=/Applications/Unity/Hub/Editor/2022.3.62f3/Unity.app/Contents/MacOS/Unity $0" >&2
  exit 1
fi

mkdir -p "$LOG_DIR"

echo "[Yui iOS] Unity editor : $UNITY_EDITOR"
echo "[Yui iOS] Unity project: $UNITY_PROJECT"
echo "[Yui iOS] Log file     : $LOG_FILE"

"$UNITY_EDITOR" \
  -batchmode \
  -quit \
  -projectPath "$UNITY_PROJECT" \
  -executeMethod YuiPhysicalAI.Editor.YuiPublicWindowsBuildTools.BuildIOSPersonalAlpha \
  -logFile "$LOG_FILE"

echo "[Yui iOS] Export finished. Log: $LOG_FILE"
