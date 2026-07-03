#!/usr/bin/env python3
"""Audit a generated Yui VRM AI Studio public distribution tree."""

from __future__ import annotations

import argparse
import hashlib
import os
import re
import sys
import zipfile
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
    ("scripts/audit_private_patterns.txt", "local private path list must not ship"),
    ("scripts/publication_guard.local.txt", "local publication guard notes must not ship"),
    ("scripts/public_templates", "local public-scene templates must not ship"),
    ("scripts/cleanup_local_artifacts_macos.sh", "local cleanup script with owner paths must not ship"),
    ("scripts/cleanup_local_artifacts.ps1", "local cleanup script with owner paths must not ship"),
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
GENERATED_FILE_PATTERNS = [
    re.compile(r".*_mldrift_(program|weight)_cache\.bin(\.meta)?$"),
]
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
    ("unity/Assets/App/Scripts/LocalAI/Runtime/YuiLocalAiAssetManifest.cs", "first-run local AI downloads need the manifest parser"),
    ("unity/Assets/App/Scripts/LocalAI/Runtime/YuiLocalAiAssetStore.cs", "first-run local AI downloads need asset planning and verification"),
    ("unity/Assets/App/Scripts/LocalAI/Runtime/YuiLocalAiAssetDownloader.cs", "first-run local AI downloads need the downloader implementation"),
    ("tools/YuiFilePickerHelper", "Windows file picker helper source should be available"),
    ("docs/SETUP_GUIDE.md", "Windows users need a first-run and backend setup guide"),
    ("docs/MAC_PUBLIC_BETA.md", "macOS users need a first-run and backend setup guide"),
    ("docs/LOCAL_AI_ASSETS.md", "source builders need local AI/TTS asset instructions"),
]

REQUIRED_WINDOWS_BUILD_PATHS = [
    ("builds/YuiVRMAIStudio_WindowsPublicBeta_v0.2.0-beta.1/Yui VRM AI Studio.exe", "public users need the Windows app executable"),
    ("builds/YuiVRMAIStudio_WindowsPublicBeta_v0.2.0-beta.1/YuiFilePickerHelper.exe", "Windows standalone image/VRM selection needs the helper beside the app exe"),
]

REQUIRED_WINDOWS_BUILD_ARCHIVES = [
    ("builds/YuiVRMAIStudio_WindowsPublicBeta_v0.2.0-beta.1_windows.zip", "public users need the downloadable Windows public beta archive"),
]

REQUIRED_MACOS_BUILD_PATHS = [
    ("builds/YuiVRMAIStudio_MacOSPublicBeta_v0.2.0-beta.1/Yui VRM AI Studio.app", "public users need the macOS app bundle"),
]

REQUIRED_MACOS_BUILD_ARCHIVES = [
    ("builds/YuiVRMAIStudio_MacOSPublicBeta_v0.2.0-beta.1_macos.zip", "public users need the downloadable macOS public beta archive"),
]

FORBIDDEN_ZIP_ENTRY_PATTERNS = [
    re.compile(r"(^|/)__MACOSX(/|$)"),
    re.compile(r"(^|/)\._[^/]+$"),
]

UNITY_TEXT_EXTENSIONS = {".unity", ".prefab", ".asset", ".controller", ".overrideController"}
SECRET_EXTENSIONS = {
    ".asset",
    ".bat",
    ".controller",
    ".cs",
    ".env",
    ".json",
    ".md",
    ".overrideController",
    ".prefab",
    ".ps1",
    ".py",
    ".txt",
    ".unity",
    ".yaml",
    ".yml",
}
SECRET_PATTERNS = [
    re.compile(r"sk-[A-Za-z0-9_-]{20,}"),
    re.compile(r"sk-proj-[A-Za-z0-9_-]{20,}"),
    re.compile(r"AIza[0-9A-Za-z_-]{20,}"),
    re.compile(r"YUI_PROFILE_PERSONAL"),
    re.compile(r"PersonalAlpha"),
    re.compile(r"Yui VRM AI Studio Personal"),
    re.compile(r"jp\.tsubamechan\.yuivrm\.personal"),
]
SELF_REFERENCE_FILES = {
    "scripts/audit_distribution_release.py",
    "scripts/audit_distribution_release.ps1",
    "scripts/publication_guard.py",
}
SELF_REFERENCE_ONLY_PATTERNS = {
    "YUI_PROFILE_PERSONAL",
    "PersonalAlpha",
    "Yui VRM AI Studio Personal",
    "jp\\.tsubamechan\\.yuivrm\\.personal",
}
UNITY_ORGANIZATION_FIELD = "organization" + "Id"

PRIVATE_TEXT_HASH_RULES = [
    (19, "d95848222a750995a3485b808064371c9779f01bcd803ff26531c7b0464dba6c"),
    (23, "6dcdb6e23d3ed173a43584f9b36922a27d97cc73df78a475ef11107a7e8e55f5"),
    (27, "66b983055669aae45058c0aa3fa80586525597dac5c9e7cbf5e47f3ee5d836f1"),
    (31, "ac70a6ba9d67ef18274d439db24d69821c436d66aecc021ed6d07a3fc248497c"),
    (29, "bbcd23d48dc5bc2f3dac1873853c11e94956817e68f1540af799202165d73687"),
    (34, "41022c0f04733657da9064590ed5a4c4668308b7578b38a877c26c5232c89817"),
    (33, "6fcc4aaf908ec51b3cb84a9fe205bbb87ab09e2916ad4ff0216b661a09c61dee"),
    (37, "22f1fd0a0b7e798bde34d64c208a73d93679b078a230b75cd8969bd88da1578a"),
    (35, "e614380e761f2d6222fbefd3ced96506aea7a1567f6067ea2902ac1ba6b2789c"),
    (24, "0f0f1373e7e54612db4fc5534ab795cf0c6b2c57207a6d5ed4a572aea931b464"),
    (39, "6d8580c7d924427d3f27a1d802027e78b3ca4c558f6b5d9e40d59903e3fe4718"),
    (36, "cd538432301f7256f68396c64e8a70e2b5511b1cb6ec229830b5673af5da52fb"),
    (24, "cbc0137d25d671277434c97dbfee806503f52ae41c81011dc0ee03979ee2fe1f"),
    (40, "b2f73d8e37cc8114fd7ecded3f819db330176c96bc8ed118dae1e91c91ac0ed6"),
    (48, "c9270edcedfdb85c8d5bf7586865338d7d4acf1c1823bf434625d067451120cf"),
    (39, "1f827faa5b87bed4f53c6e74cc036b7c28ca4eb4c9116de4b918e72e7fb43171"),
    (52, "3ea5cf741698cd926239f7c18b7973463da65ea3cf2cc71c80fa54228bcdc0d0"),
    (46, "b65c21232c131b75dfc81d67a603f187a46fa8b5d1906f901dabb6123f9ec9bb"),
    (22, "68ee7b640928cb5e9759fb5227fe96978620b9d306e859113098c9c909f4e432"),
    (50, "7db9e56ddf9f927d3a58329f86b4dd4e68716c60fcfaaadda9dc6d1c1ef48ff1"),
    (39, "8661aad5531fd4edf6152b442dd98122155b7a1793fd4897683b21156d27268e"),
    (43, "3739cde997e7110bd308839442e7966d562322df98ff5539a6bcaf3f5ddd8d4a"),
    (19, "7d5df3d4b3074d5c7b87e470309beef48980d9ad5c6971dcd0d51af6b2cf6e58"),
    (23, "fe496011fb358bcc42bb75547de9ed64087b7cbb10f10b67470f268fbc068b3c"),
    (24, "0293f9ba1af220bf1eb69e09f65a7a70cc8c3cf5043989dec7ccc87722a2c049"),
    (28, "39ca684961067043d07489b8f03de6b8dc9415630bb0c1ab67857bd2a6a6ff45"),
    (52, "48794c39732c9d1186d95b0e794c1de008047dcf3789d32c73f680f422cd57d4"),
    (57, "9710151c6643ef1240ec5282f11471815276eed23b52e83acd40a90c6caf9504"),
    (57, "b9c837032efd88ae0d08386d660c746dea4610cb9c8e73d8ba5d8d8203faf7bb"),
    (62, "dcc8455c5b77b88e5d3b86847b42e50392c9ded4fe21f70d8abc6196881de675"),
    (43, "333ef3c82d02a744ea00de90d8cd36a57a6b3eaf4b88802b5d6bfbc9abf9c40c"),
    (48, "8a84e02151c3ef8bac441536bef266e47f195c4801fc21e005f1aac5055a1530"),
    (5, "2b97a7913b83568a8d2a38be3a93261589fb5b82d0b3388e34d8cf94d2f1c1b0"),
    (13, "6a55eb4e250400eceea24188f64f15f005228ee24be16e26f877b415a6b3eafd"),
    (8, "37c6f3d3a11e196d2dec8937799d419191b610dbfb6b0f136e388c5615ef24da"),
    (6, "8cfe859bbf65bec24c1de9d48df82fc7d78a4657162eb765ecea78cb4d032154"),
    (6, "c4b54c6506f05565d71f6bba04fc3e43685ba05e1a0ea16ee4b077f785e0c8d7"),
    (15, "50f1a38f5e3ef71b369b33619ca4b555467611b27b01c77728a1c480a6be5cf6"),
    (21, "904edd0d0f476076037f4acf964ffc281a25a882e1d8ffcc933aa05485ea9a9d"),
    (4, "d3b842089a9abe6a3bcbdda47267f2e6da81c9720b486a545678bf0c16eb2af9"),
    (17, "bffb81551a240086fb8afbb39bf3a598887393c63de2a916a509dea1a9798264"),
    (5, "c4471e49f778f22b2a4b4dd96c463a3a2dd9e62462cd7ba062d23ed45d257de3"),
    (10, "c4c0168cb513cf68e310fd6ad0b01cf35e8f16ace6fbb235f295d13245c9f042"),
    (14, "deaf6e4915a0fe1e6f6286d70ee577f8478509e1cd4b72ce9ba52ca14e8b8395"),
    (12, "0563ae94557b154c65358347ad2df7b55d1eea0a3d8251ff66691249bb393198"),
    (10, "a10e6d3adb8d4bb96ce2a043b16e241af6774ebf5f57414a3f761d583ad4eced"),
    (12, "4598fac51f97c1c97660ed6e3be7530eada7ae04fb87c9aeb15565c620750dcd"),
    (15, "d299c82b9008b9d7e314b113a15f986ccd1c9f99b23779ca503b33e2fcde8172"),
]
PRIVATE_TEXT_HASHES_BY_LENGTH: dict[int, set[str]] = {}
for _length, _digest in PRIVATE_TEXT_HASH_RULES:
    PRIVATE_TEXT_HASHES_BY_LENGTH.setdefault(_length, set()).add(_digest)


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


def contains_private_hash(value: str) -> bool:
    normalized = value.replace("\\", "/").lower()
    candidates: set[str] = set()
    if len(normalized) <= 256:
        candidates.add(normalized.strip())
    for line in normalized.splitlines():
        stripped = line.strip().strip('",;')
        if stripped:
            candidates.add(stripped)
        for token in re.findall(r"[\w./ \-\u3040-\u30ff\u3400-\u9fff]+", stripped):
            token = token.strip().strip('",;')
            if token:
                candidates.add(token)
                candidates.update(part for part in token.split("/") if part)
    for candidate in candidates:
        digests = PRIVATE_TEXT_HASHES_BY_LENGTH.get(len(candidate))
        if digests and hashlib.sha256(candidate.encode()).hexdigest() in digests:
            return True
    return False


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


def forbidden_zip_entries(path: Path) -> list[str]:
    try:
        with zipfile.ZipFile(path) as archive:
            return [
                name
                for name in archive.namelist()
                if any(pattern.search(name) for pattern in FORBIDDEN_ZIP_ENTRY_PATTERNS)
            ]
    except zipfile.BadZipFile:
        return ["<invalid zip archive>"]


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
            relative_file = display(current / file_name, root)
            if file_name in GENERATED_PART_NAMES:
                report_blocker(relative_file, "generated cache/metadata must not ship")
            if any(pattern.match(file_name) for pattern in GENERATED_FILE_PATTERNS):
                report_blocker(relative_file, "LiteRT runtime cache must not ship")
            if contains_private_hash(relative_file):
                report_blocker(relative_file, "local-only avatar identifier must not ship")

        kept_dirs: list[str] = []
        for dir_name in dir_names:
            relative_dir = display(current / dir_name, root)
            if dir_name in GENERATED_PART_NAMES:
                report_blocker(relative_dir, "generated cache/metadata must not ship")
            if contains_private_hash(relative_dir):
                report_blocker(relative_dir, "local-only avatar identifier must not ship")
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
        for relative, _reason in required_build_archives:
            path = root / relative
            if not path.exists():
                continue
            bad_entries = forbidden_zip_entries(path)
            if bad_entries:
                preview = ", ".join(bad_entries[:5])
                if len(bad_entries) > 5:
                    preview += f", ... ({len(bad_entries)} total)"
                report_blocker(relative, f"public ZIP contains macOS metadata entries: {preview}")

    unity_assets = root / "unity" / "Assets"
    if unity_assets.exists():
        for path in unity_assets.rglob("*"):
            if not path.is_file() or path.suffix not in UNITY_TEXT_EXTENSIONS:
                continue
            text = read_text(path)
            if contains_private_hash(text):
                report_blocker(display(path, root), "public Unity assets must not reference local-only startup avatars")

    project_settings = root / "unity" / "ProjectSettings"
    if project_settings.exists():
        for path in project_settings.rglob("*"):
            if not path.is_file():
                continue
            text = read_text(path)
            if re.search(rf"{UNITY_ORGANIZATION_FIELD}:[ \t]*[^\r\n \t]+", text):
                report_blocker(display(path, root), "public Unity project settings must not expose private account identifiers")

    for path in root.rglob("*"):
        if not path.is_file() or has_forbidden_segment(path.relative_to(root)):
            continue
        if path.suffix not in SECRET_EXTENSIONS and path.name != ".env.example":
            continue
        relative = display(path, root)
        text = read_text(path)
        if contains_private_hash(relative) or contains_private_hash(text):
            report_blocker(relative, "local-only avatar identifier must not ship")
            continue
        for pattern in SECRET_PATTERNS:
            if relative in SELF_REFERENCE_FILES and pattern.pattern in SELF_REFERENCE_ONLY_PATTERNS:
                continue
            if pattern.search(text):
                report_blocker(relative, "possible API key, token-like secret, or private build marker")
                break

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
