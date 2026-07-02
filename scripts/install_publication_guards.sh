#!/usr/bin/env bash
set -euo pipefail

ROOT="$(git rev-parse --show-toplevel)"
git -C "$ROOT" config core.hooksPath .githooks
chmod +x "$ROOT/.githooks/pre-commit" "$ROOT/.githooks/pre-push"

cat <<'MSG'
Publication guards installed.

Git will now run repository hygiene checks before commit and push.
Keep machine-specific deny patterns in scripts/publication_guard.local.txt.
MSG
