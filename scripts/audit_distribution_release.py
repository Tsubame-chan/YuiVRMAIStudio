#!/usr/bin/env python3
"""Audit a generated Yui VRM AI Studio public distribution tree."""

from __future__ import annotations

import argparse
import os
import re
import sys
from pathlib import Path


PRIVATE_BLOCKERS = [
    ("unity/Assets/App/Editor/YuiAvatarSceneSetup.cs", "local-only editor scene setup script must not ship"),
    ("unity/Assets/App/Editor/YuiAvatarSceneSetup.cs.meta", "local-only editor scene setup script metadata must not ship"),
    ("unity/Assets/App/Scripts/Avatar/YuiKikyoAvatarDefaults.cs", "personal avatar defaults must not ship in public source"),
    ("unity/Assets/App/Scripts/Avatar/YuiKikyoAvatarDefaults.cs.meta", "personal avatar defaults metadata must not ship in public source"),
    (".env", "real secrets must stay local/server-side"),
    ("backend/data/yui.db", "local conversation database must not ship"),
    ("backend/data/yui.db-wal", "local conversation database WAL must not ship"),
    ("backend/data/yui.db-shm", "local conversation database SHM must not ship"),
    ("backend/data/yui_test.db", "local test database must not ship"),
    ("backend/data/audio", "local generated audio cache must not ship"),
]

GENERATED_BLOCKERS = [
    (".DS_Store", "macOS Finder metadata must not ship"),
    (".pytest_cache", "pytest cache must not ship"),
    ("backend/.pytest_cache", "pytest cache must not ship"),
    ("backend/__pycache__", "Python bytecode cache must not ship"),
    ("scripts/__pycache__", "Python bytecode cache must not ship"),
    ("unity/.vs", "local IDE metadata must not ship"),
    ("unity/Library", "Unity Library is generated and must not ship"),
    ("unity/Logs", "Unity logs are generated and must not ship"),
    ("unity/UserSettings", "Unity user settings are local and must not ship"),
    ("unity/Temp", "Unity Temp is generated and must not ship"),
    ("unity/obj", "IDE build intermediates must not ship"),
]

GENERATED_PART_NAMES = {".DS_Store", "__pycache__", ".pytest_cache"}
DESCEND_SKIP_DIR_NAMES = {
    ".git",
    ".venv",
    "__pycache__",
    ".pytest_cache",
    "Library",
    "Temp",
    "Logs",
    "logs",
    "builds",
    "downloads",
    "UserSettings",
    "node_modules",
    "obj",
}

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

REQUIRED_WINDOWS_BUILD_PATHS = [
    ("builds/YuiVRMAIStudio_PublicAlpha_v0.1.0-alpha.1/Yui VRM AI Studio.exe", "public users need the Windows app executable"),
    ("builds/YuiVRMAIStudio_PublicAlpha_v0.1.0-alpha.1/YuiFilePickerHelper.exe", "Windows standalone image/VRM selection needs the helper beside the app exe"),
]

REQUIRED_WINDOWS_BUILD_ARCHIVES = [
    ("builds/YuiVRMAIStudio_PublicAlpha_v0.1.0-alpha.1_windows.zip", "public users need the downloadable Windows public alpha archive"),
]

REQUIRED_MACOS_BUILD_PATHS = [
    ("builds/YuiVRMAIStudio_MacOSAlpha_v0.1.0-alpha.1/Yui VRM AI Studio.app", "public users need the macOS app bundle"),
]

REQUIRED_MACOS_BUILD_ARCHIVES = [
    ("builds/YuiVRMAIStudio_MacOSAlpha_v0.1.0-alpha.1_macos.zip", "public users need the downloadable macOS public alpha archive"),
]

UNITY_TEXT_EXTENSIONS = {".unity", ".prefab", ".asset", ".controller", ".overrideController"}
SECRET_EXTENSIONS = {".cs", ".py", ".ps1", ".bat", ".md", ".json", ".yaml", ".yml", ".txt", ".env"}
SECRET_PATTERNS = [
    re.compile(r"sk-[A-Za-z0-9_-]{20,}"),
    re.compile(r"sk-proj-[A-Za-z0-9_-]{20,}"),
    re.compile(r"AIza[0-9A-Za-z_-]{20,}"),
]
UNITY_ORGANIZATION_FIELD = "organization" + "Id"


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
    ignored = DESCEND_SKIP_DIR_NAMES
    return any(part in ignored for part in path.parts)


def read_text(path: Path) -> str:
    try:
        return path.read_text(encoding="utf-8", errors="ignore")
    except OSError:
        return ""


def required_build_sets(platform: str) -> tuple[list[tuple[str, str]], list[tuple[str, str]]]:
    normalized = platform.strip().lower()
    if normalized == "windows":
        return REQUIRED_WINDOWS_BUILD_PATHS, REQUIRED_WINDOWS_BUILD_ARCHIVES
    if normalized == "macos":
        return REQUIRED_MACOS_BUILD_PATHS, REQUIRED_MACOS_BUILD_ARCHIVES
    if normalized == "all":
        return (
            REQUIRED_WINDOWS_BUILD_PATHS + REQUIRED_MACOS_BUILD_PATHS,
            REQUIRED_WINDOWS_BUILD_ARCHIVES + REQUIRED_MACOS_BUILD_ARCHIVES,
        )
    raise ValueError(f"Unsupported platform: {platform}")


def audit(root: Path, require_builds: bool, platform: str = "windows") -> int:
    failures = 0
    reported_blockers: set[str] = set()

    def report_blocker(relative: str, reason: str) -> None:
        nonlocal failures
        if relative in reported_blockers:
            return
        print(f"BLOCKER: {relative} - {reason}")
        reported_blockers.add(relative)
        failures += 1

    print("Yui VRM AI Studio distribution release audit")
    print(f"Project: {root}")
    print("")

    for relative, reason in load_private_blockers(root):
        path = root / relative
        if path.exists():
            report_blocker(relative, reason)

    for relative, reason in GENERATED_BLOCKERS:
        path = root / relative
        if path.exists():
            report_blocker(relative, reason)

    for current_raw, dir_names, file_names in os.walk(root):
        current = Path(current_raw)
        relative_current = current.relative_to(root)
        if any(part in DESCEND_SKIP_DIR_NAMES for part in relative_current.parts):
            dir_names[:] = []
            continue

        for file_name in file_names:
            if file_name in GENERATED_PART_NAMES:
                report_blocker(display(current / file_name, root), "generated cache/metadata must not ship")

        kept_dirs: list[str] = []
        for dir_name in dir_names:
            if dir_name in GENERATED_PART_NAMES:
                report_blocker(display(current / dir_name, root), "generated cache/metadata must not ship")
            if dir_name not in DESCEND_SKIP_DIR_NAMES:
                kept_dirs.append(dir_name)
        dir_names[:] = kept_dirs

    for relative, reason in REQUIRED_SOURCE_PATHS:
        path = root / relative
        if not path.exists():
            print(f"MISSING: {relative} - {reason}")
            failures += 1

    if require_builds:
        required_build_paths, required_build_archives = required_build_sets(platform)
        has_expanded_build = all((root / relative).exists() for relative, _reason in required_build_paths)
        has_build_archive = any((root / relative).exists() for relative, _reason in required_build_archives)
        if not has_expanded_build and not has_build_archive:
            for relative, reason in required_build_paths + required_build_archives:
                if not (root / relative).exists():
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
                    report_blocker(display(path, root), f"public Unity assets must not reference private startup avatars ({term})")
                    break

    project_settings = root / "unity" / "ProjectSettings"
    if project_settings.exists():
        for path in project_settings.rglob("*"):
            if not path.is_file():
                continue
            text = read_text(path)
            if re.search(rf"{UNITY_ORGANIZATION_FIELD}:[ \t]*[^\r\n \t]+", text):
                report_blocker(display(path, root), "public Unity project settings must not expose personal account identifiers")

    for path in root.rglob("*"):
        if not path.is_file() or has_forbidden_segment(path.relative_to(root)):
            continue
        if path.suffix not in SECRET_EXTENSIONS and path.name != ".env.example":
            continue
        text = read_text(path)
        if any(pattern.search(text) for pattern in SECRET_PATTERNS):
            report_blocker(display(path, root), "possible API key or token-like secret")

    if failures:
        print("")
        print(f"Distribution release audit failed with {failures} issue(s).")
        return 1

    print("Distribution release audit passed.")
    return 0


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--project-root", default=".", help="Generated public project root to audit.")
    parser.add_argument("--require-builds", action="store_true", help="Also require selected public build artifacts.")
    parser.add_argument(
        "--platform",
        choices=("windows", "macos", "all"),
        default="windows",
        help="Build artifact set to require when --require-builds is used.",
    )
    args = parser.parse_args()

    root = Path(args.project_root).expanduser().resolve()
    return audit(root, args.require_builds, platform=args.platform)


if __name__ == "__main__":
    raise SystemExit(main())
