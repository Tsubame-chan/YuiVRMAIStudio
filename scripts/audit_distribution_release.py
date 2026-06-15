#!/usr/bin/env python3
"""Audit a generated Yui VRM AI Studio public distribution tree."""

from __future__ import annotations

import argparse
import re
import sys
from pathlib import Path


PRIVATE_BLOCKERS = [
    ("unity/Assets/App/Editor/YuiAvatarSceneSetup.cs", "local-only editor scene setup script must not ship"),
    ("unity/Assets/App/Editor/YuiAvatarSceneSetup.cs.meta", "local-only editor scene setup script metadata must not ship"),
    (".env", "real secrets must stay local/server-side"),
    ("backend/data/yui.db", "local conversation database must not ship"),
    ("backend/data/yui.db-wal", "local conversation database WAL must not ship"),
    ("backend/data/yui.db-shm", "local conversation database SHM must not ship"),
    ("backend/data/yui_test.db", "local test database must not ship"),
    ("backend/data/audio", "local generated audio cache must not ship"),
]

PRIVATE_PATTERN_FILE = "scripts/audit_private_patterns.txt"

REQUIRED_SOURCE_PATHS = [
    (".env.example", "first-time contributors need a safe environment template"),
    ("LICENSE", "public repositories need a project license"),
    ("backend/requirements.txt", "public users need backend dependencies for BYOK setup"),
    ("backend/main.py", "public users need the FastAPI backend entrypoint"),
    ("backend/app/main.py", "public users need the FastAPI backend app source"),
    ("unity/Assets/UnityChan/Prefabs/unitychan.prefab", "UnityChan default avatar is the release baseline"),
    ("tools/YuiFilePickerHelper", "Windows file picker helper source should be available"),
    ("docs/PUBLIC_BYOK_SETUP.md", "public users need BYOK setup instructions"),
    ("docs/GITHUB_PUBLICATION.md", "release maintainers need publication instructions"),
]

REQUIRED_BUILD_PATHS = [
    ("builds/YuiVRMAIStudio_PublicAlpha_v0.1.0-alpha.1/Yui VRM AI Studio.exe", "public users need the Windows app executable"),
    ("builds/YuiVRMAIStudio_PublicAlpha_v0.1.0-alpha.1/YuiFilePickerHelper.exe", "Windows standalone image/VRM selection needs the helper beside the app exe"),
]

UNITY_TEXT_EXTENSIONS = {".unity", ".prefab", ".asset", ".controller", ".overrideController"}
SECRET_EXTENSIONS = {".cs", ".py", ".ps1", ".bat", ".md", ".json", ".yaml", ".yml", ".txt", ".env"}
SECRET_PATTERNS = [
    re.compile(r"sk-[A-Za-z0-9_-]{20,}"),
    re.compile(r"sk-proj-[A-Za-z0-9_-]{20,}"),
    re.compile(r"AIza[0-9A-Za-z_-]{20,}"),
]


def load_private_blockers(root: Path) -> list[tuple[str, str]]:
    blockers = list(PRIVATE_BLOCKERS)
    pattern_file = root / PRIVATE_PATTERN_FILE
    if not pattern_file.exists():
        return blockers

    for line_number, raw_line in enumerate(pattern_file.read_text(encoding="utf-8", errors="ignore").splitlines(), start=1):
        line = raw_line.strip()
        if not line or line.startswith("#"):
            continue
        if "|" in line:
            relative, reason = line.split("|", 1)
        else:
            relative, reason = line, "local private asset must not ship"
        relative = relative.strip().replace("\\", "/")
        reason = reason.strip() or "local private asset must not ship"
        if not relative:
            print(f"WARNING: ignoring empty private blocker in {PRIVATE_PATTERN_FILE}:{line_number}")
            continue
        blockers.append((relative, reason))

    return blockers


def display(path: Path, root: Path) -> str:
    return path.relative_to(root).as_posix()


def has_forbidden_segment(path: Path) -> bool:
    ignored = {".git", ".venv", "Library", "Temp", "Logs", "logs", "builds", "downloads", "__pycache__"}
    return any(part in ignored for part in path.parts)


def read_text(path: Path) -> str:
    try:
        return path.read_text(encoding="utf-8", errors="ignore")
    except OSError:
        return ""


def audit(root: Path, require_builds: bool) -> int:
    failures = 0

    print("Yui VRM AI Studio distribution release audit")
    print(f"Project: {root}")
    print("")

    for relative, reason in load_private_blockers(root):
        path = root / relative
        if path.exists():
            print(f"BLOCKER: {relative} - {reason}")
            failures += 1

    for relative, reason in REQUIRED_SOURCE_PATHS:
        path = root / relative
        if not path.exists():
            print(f"MISSING: {relative} - {reason}")
            failures += 1

    if require_builds:
        for relative, reason in REQUIRED_BUILD_PATHS:
            path = root / relative
            if not path.exists():
                print(f"MISSING: {relative} - {reason}")
                failures += 1

    unity_assets = root / "unity" / "Assets"
    if unity_assets.exists():
        private_terms = ["Yui AIAvatar", "Yui Avatar", "demo_kikyo"]
        for path in unity_assets.rglob("*"):
            if not path.is_file() or path.suffix not in UNITY_TEXT_EXTENSIONS:
                continue
            text = read_text(path)
            for term in private_terms:
                if term in text:
                    print(f"BLOCKER: {display(path, root)} - public Unity assets must not reference private startup avatars ({term})")
                    failures += 1
                    break

    project_settings = root / "unity" / "ProjectSettings"
    if project_settings.exists():
        for path in project_settings.rglob("*"):
            if not path.is_file():
                continue
            text = read_text(path)
            if re.search(r"organizationId:[ \t]*[^\r\n \t]+", text):
                print(f"BLOCKER: {display(path, root)} - public Unity project settings must not expose personal account identifiers")
                failures += 1

    for path in root.rglob("*"):
        if not path.is_file() or has_forbidden_segment(path.relative_to(root)):
            continue
        if path.suffix not in SECRET_EXTENSIONS and path.name != ".env.example":
            continue
        text = read_text(path)
        if any(pattern.search(text) for pattern in SECRET_PATTERNS):
            print(f"BLOCKER: {display(path, root)} - possible API key or token-like secret")
            failures += 1

    if failures:
        print("")
        print(f"Distribution release audit failed with {failures} issue(s).")
        return 1

    print("Distribution release audit passed.")
    return 0


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--project-root", default=".", help="Generated public project root to audit.")
    parser.add_argument("--require-builds", action="store_true", help="Also require Windows public build artifacts.")
    args = parser.parse_args()

    root = Path(args.project_root).expanduser().resolve()
    return audit(root, args.require_builds)


if __name__ == "__main__":
    raise SystemExit(main())
