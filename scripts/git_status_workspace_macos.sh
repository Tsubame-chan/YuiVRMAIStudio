#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PUBLIC_REPO="$ROOT_DIR/public/YuiVRMAIStudio_Public"

echo "Workspace root:"
echo "  $ROOT_DIR"
if git -C "$ROOT_DIR" rev-parse --is-inside-work-tree >/dev/null 2>&1; then
  echo "Root Git status:"
  git -C "$ROOT_DIR" status --short
else
  echo "Root Git status:"
  echo "  not a Git repository by design"
fi

echo ""
echo "Public repository:"
echo "  $PUBLIC_REPO"
if [ -d "$PUBLIC_REPO/.git" ]; then
  echo "Public Git branch:"
  git -C "$PUBLIC_REPO" branch --show-current
  echo "Public Git remote:"
  git -C "$PUBLIC_REPO" remote -v
  echo "Public Git status:"
  git -C "$PUBLIC_REPO" status --short
else
  echo "  missing .git directory"
fi
