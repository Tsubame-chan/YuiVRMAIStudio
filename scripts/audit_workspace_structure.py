#!/usr/bin/env python3
"""Audit the local workspace layout without deleting or moving files."""

from __future__ import annotations

import argparse
import os
from dataclasses import dataclass
from pathlib import Path


@dataclass(frozen=True)
class Finding:
    severity: str
    path: str
    message: str


GENERATED_DIR_NAMES = {
    ".pytest_cache",
    "__pycache__",
    "Library",
    "Logs",
    "Temp",
    "UserSettings",
    "obj",
}

GENERATED_FILE_NAMES = {
    ".DS_Store",
    "CACHEDIR.TAG",
}

DESCEND_SKIP_DIR_NAMES = {
    ".git",
    ".venv",
    "__pycache__",
    ".pytest_cache",
    "Library",
    "Logs",
    "Temp",
    "UserSettings",
    "node_modules",
    "obj",
}

TOP_LEVEL_ALLOWED_DIRS = {
    ".git",
    ".pytest_cache",
    "_private_migration",
    "backend",
    "builds",
    "deploy",
    "docs",
    "downloads",
    "icon",
    "installer",
    "logs",
    "public",
    "runtime",
    "scripts",
    "tools",
    "unity",
}


def display(path: Path, root: Path) -> str:
    return path.relative_to(root).as_posix()


def collect_findings(root: Path) -> list[Finding]:
    findings: list[Finding] = []
    seen: set[tuple[str, str]] = set()

    def add(severity: str, path: str, message: str) -> None:
        key = (severity, path)
        if key not in seen:
            findings.append(Finding(severity, path, message))
            seen.add(key)

    for child in root.iterdir():
        if child.is_dir() and child.name not in TOP_LEVEL_ALLOWED_DIRS:
            add("WARN", child.name, "unexpected top-level directory")

    public_root = root / "public" / "YuiVRMAIStudio_Public"
    if public_root.exists():
        if (public_root / ".git").exists():
            add("WARN", "public/YuiVRMAIStudio_Public/.git", "nested public repository exists; treat it as a separate repo, not generated source")
        for relative in [
            "unity/Library",
            "unity/Logs",
            "unity/UserSettings",
            "backend/.pytest_cache",
            "scripts/__pycache__",
        ]:
            if (public_root / relative).exists():
                add("BLOCKER", f"public/YuiVRMAIStudio_Public/{relative}", "generated/local state is present in public distribution copy")

    patch_test = root / "builds" / "unity_2022_3_62_patch_test" / "unity"
    if patch_test.exists():
        add("WARN", display(patch_test, root), "secondary Unity project exists under builds; decide whether this is a temporary worktree or the source of truth")

    root_unity = root / "unity"
    if root_unity.exists() and patch_test.exists():
        add("WARN", "unity + builds/unity_2022_3_62_patch_test/unity", "two Unity projects exist; changes can diverge unless one is declared canonical")

    for current_raw, dir_names, file_names in os.walk(root):
        current = Path(current_raw)
        relative_current = current.relative_to(root)
        if any(part in {".git", ".venv", "Library"} for part in relative_current.parts):
            dir_names[:] = []
            continue

        for file_name in file_names:
            if file_name in GENERATED_FILE_NAMES:
                add("INFO", display(current / file_name, root), "generated metadata file")

        kept_dirs: list[str] = []
        for dir_name in dir_names:
            path = current / dir_name
            relative = display(path, root)
            if dir_name in GENERATED_DIR_NAMES:
                if relative.startswith("public/YuiVRMAIStudio_Public/"):
                    severity = "BLOCKER"
                elif relative.startswith("unity/") or relative.startswith("builds/"):
                    severity = "INFO"
                else:
                    severity = "WARN"
                add(severity, relative, "generated/local cache directory")

            if dir_name not in DESCEND_SKIP_DIR_NAMES:
                kept_dirs.append(dir_name)
        dir_names[:] = kept_dirs

    return findings


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--project-root", default=".", type=Path)
    parser.add_argument("--fail-on-blocker", action="store_true")
    args = parser.parse_args()

    root = args.project_root.expanduser().resolve()
    findings = collect_findings(root)
    blockers = [finding for finding in findings if finding.severity == "BLOCKER"]

    print("Yui VRM AI Studio workspace structure audit")
    print(f"Project: {root}")
    print("")
    if not findings:
        print("No structural findings.")
        return 0

    for finding in findings:
        print(f"{finding.severity}: {finding.path} - {finding.message}")

    print("")
    print(f"Summary: {len(blockers)} blocker(s), {len(findings)} total finding(s).")
    if blockers and args.fail_on_blocker:
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
