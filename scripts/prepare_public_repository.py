#!/usr/bin/env python3
"""Generate a UnityChan-only public repository copy from the private tree."""

from __future__ import annotations

import argparse
import re
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path


ROOT_FILES = [
    ".dockerignore",
    ".env.example",
    ".gitignore",
    "LICENSE",
    "README.md",
    "README.en.md",
    "Start_Yui_Backend_And_VOICEVOX.bat",
    "Start_Yui_Local_Services.command",
    "Stop_Yui_Backend_And_VOICEVOX.bat",
    "Stop_Yui_Local_Services.command",
    "openapi.json",
]

TREE_COPIES = [
    ("backend", {".venv", "__pycache__", "data"}, {"*.pyc"}),
    ("deploy", set(), set()),
    ("icon", set(), set()),
    ("installer", set(), set()),
    ("scripts", set(), {"check_phase1.ps1", "create_unitychan_release_copy.ps1", "prepare_public_repository.ps1"}),
    ("tools", set(), set()),
    ("unity/Assets", set(), set()),
    ("unity/Packages", set(), set()),
    ("unity/ProjectSettings", set(), set()),
]

DOC_FILES = [
    "docs/ALPHA_RELEASE_CHECKLIST.md",
    "docs/PUBLIC_BYOK_SETUP.md",
    "docs/SETUP_GUIDE.md",
    "docs/GITHUB_PUBLICATION.md",
    "docs/MAC_SETUP.md",
    "docs/WINDOWS_INSTALLER_PLAN.md",
    "docs/api.md",
]

REMOVE_AFTER_COPY = [
    "unity/Assets/App/Editor",
    "unity/Assets/App/Editor.meta",
]

PRIVATE_COPY_EXCLUSION_FILE = "scripts/audit_private_patterns.txt"

EDITOR_KEEP_FILES = [
    "unity/Assets/App/Editor/YuiPublicWindowsBuildTools.cs",
    "unity/Assets/App/Editor/YuiPublicWindowsBuildTools.cs.meta",
    "unity/Assets/App/Editor/YuiGenerateNeutralIdleClip.cs",
    "unity/Assets/App/Editor/YuiGenerateNeutralIdleClip.cs.meta",
]


def should_ignore(name: str, exclude_dirs: set[str], exclude_files: set[str]) -> bool:
    if name in exclude_dirs:
        return True
    return any(Path(name).match(pattern) for pattern in exclude_files)


def copy_file(source_root: Path, destination_root: Path, relative: str) -> None:
    source = source_root / relative
    if not source.exists():
        return
    destination = destination_root / relative
    destination.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(source, destination)


def copy_tree(source_root: Path, destination_root: Path, relative: str, exclude_dirs: set[str], exclude_files: set[str]) -> None:
    source = source_root / relative
    if not source.exists():
        return
    destination = destination_root / relative

    def ignore(_directory: str, names: list[str]) -> set[str]:
        return {name for name in names if should_ignore(name, exclude_dirs, exclude_files)}

    shutil.copytree(source, destination, ignore=ignore, dirs_exist_ok=True)


def load_local_private_paths(source_root: Path) -> list[str]:
    pattern_file = source_root / PRIVATE_COPY_EXCLUSION_FILE
    if not pattern_file.exists():
        return []

    paths: list[str] = []
    for raw_line in pattern_file.read_text(encoding="utf-8", errors="ignore").splitlines():
        line = raw_line.strip()
        if not line or line.startswith("#"):
            continue
        relative = line.split("|", 1)[0].strip().replace("\\", "/")
        if relative:
            paths.append(relative)
    return paths


def private_unity_asset_roots(private_paths: list[str]) -> set[str]:
    roots: set[str] = set()
    prefix = "unity/Assets/"
    for relative in private_paths:
        if not relative.startswith(prefix):
            continue
        remainder = relative[len(prefix):]
        first_segment = remainder.split("/", 1)[0]
        if first_segment and "." not in first_segment:
            roots.add(first_segment)
    return roots


def remove_path(destination_root: Path, relative: str) -> None:
    path = destination_root / relative
    if path.is_dir():
        shutil.rmtree(path)
    elif path.exists():
        path.unlink()


def remove_private_paths(destination_root: Path, private_paths: list[str]) -> None:
    for relative in private_paths:
        remove_path(destination_root, relative)
        if not relative.endswith(".meta"):
            remove_path(destination_root, relative + ".meta")


def update_public_gitignore(destination_root: Path) -> None:
    path = destination_root / ".gitignore"
    if not path.exists():
        return
    lines = path.read_text(encoding="utf-8", errors="ignore").splitlines()
    filtered = [line for line in lines if line not in {"public/", "tmp_kikyo_pkg/"}]
    path.write_text("\n".join(filtered).rstrip() + "\n", encoding="utf-8")


def apply_public_templates(destination_root: Path) -> None:
    template_root = Path(__file__).resolve().parent / "public_templates"
    if not template_root.exists():
        return
    for source in template_root.rglob("*"):
        if not source.is_file():
            continue
        relative = source.relative_to(template_root)
        destination = destination_root / "unity" / relative
        destination.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source, destination)


def sanitize_project_settings(destination_root: Path) -> None:
    path = destination_root / "unity" / "ProjectSettings" / "ProjectSettings.asset"
    if not path.exists():
        return
    text = path.read_text(encoding="utf-8", errors="ignore")
    text = re.sub(r"organizationId:[ \t]*[^\r\n \t]+", "organizationId: ", text)
    path.write_text(text, encoding="utf-8")


def preserve_existing_builds(destination: Path) -> Path | None:
    builds = destination / "builds"
    if not builds.exists():
        return None
    backup = Path(tempfile.mkdtemp(prefix="YuiVRMAIStudio_PublicBuilds_"))
    shutil.copytree(builds, backup / "builds")
    return backup


def restore_existing_builds(destination: Path, backup: Path | None) -> None:
    if backup is None:
        return
    source = backup / "builds"
    if source.exists():
        shutil.copytree(source, destination / "builds", dirs_exist_ok=True)
    shutil.rmtree(backup, ignore_errors=True)


def run_audit(destination: Path, require_builds: bool) -> int:
    script = Path(__file__).with_name("audit_distribution_release.py")
    command = [sys.executable, str(script), "--project-root", str(destination)]
    if require_builds:
        command.append("--require-builds")
    return subprocess.call(command)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source-root", default=Path(__file__).resolve().parents[1], type=Path)
    parser.add_argument("--destination-root", default=None, type=Path)
    parser.add_argument("--require-builds", action="store_true", help="Require Windows public build artifacts during audit.")
    parser.add_argument("--skip-audit", action="store_true", help="Generate the tree without running the public audit.")
    args = parser.parse_args()

    source_root = args.source_root.expanduser().resolve()
    destination = args.destination_root
    if destination is None:
        destination = source_root / "public" / "YuiVRMAIStudio_Public"
    destination = destination.expanduser().resolve()

    if destination == destination.anchor:
        print(f"Refusing to write to filesystem root: {destination}", file=sys.stderr)
        return 2
    if source_root == destination or source_root in destination.parents and destination.name == source_root.name:
        print(f"Refusing unsafe destination: {destination}", file=sys.stderr)
        return 2

    build_backup = preserve_existing_builds(destination)
    if destination.exists():
        shutil.rmtree(destination)
    destination.mkdir(parents=True)

    for relative in ROOT_FILES:
        copy_file(source_root, destination, relative)
    update_public_gitignore(destination)

    private_paths = load_local_private_paths(source_root)
    unity_asset_exclude_dirs = private_unity_asset_roots(private_paths)
    for relative, exclude_dirs, exclude_files in TREE_COPIES:
        if relative == "unity/Assets":
            exclude_dirs = set(exclude_dirs) | unity_asset_exclude_dirs
        copy_tree(source_root, destination, relative, exclude_dirs, exclude_files)

    for relative in REMOVE_AFTER_COPY:
        remove_path(destination, relative)
    remove_private_paths(destination, private_paths)
    for relative in EDITOR_KEEP_FILES:
        copy_file(source_root, destination, relative)
    for relative in DOC_FILES:
        copy_file(source_root, destination, relative)

    apply_public_templates(destination)
    sanitize_project_settings(destination)
    restore_existing_builds(destination, build_backup)

    if not args.skip_audit:
        result = run_audit(destination, args.require_builds)
        if result != 0:
            print("")
            print(f"Public repository was generated but did not pass audit: {destination}")
            return result

    print("")
    print("Public repository prepared:")
    print(f"  {destination}")
    print("")
    print("Next:")
    print(f"  cd {destination}")
    print("  git init")
    print("  git add .")
    print('  git commit -m "Initial public BYOK Windows alpha"')
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
