#!/usr/bin/env python3
"""Block public pushes that contain local-only identity, notes, or secrets."""

from __future__ import annotations

import argparse
import hashlib
import os
import re
import subprocess
import sys
from dataclasses import dataclass
from pathlib import Path


LOCAL_PATTERN_FILE = Path("scripts/publication_guard.local.txt")
ALLOWED_MAINTAINER_NAME = "Tsubame-chan"
ALLOWED_MAINTAINER_EMAIL_SUFFIX = "@users.noreply.github.com"
AI_TOOL_MARKER = "co" + "dex"
PRIVATE_WEBMAIL_DOMAIN = "g" + "mail"
PRIVATE_WEBMAIL_TOKEN = PRIVATE_WEBMAIL_DOMAIN + "-com"
UNITY_ORGANIZATION_FIELD = "organization" + "Id"
SESSION_NOTE_PREFIX = "handoff" + "_prompt"
RESTART_NOTE_PREFIX = "PROJECT_RESTART" + "_INVENTORY"
MAX_REPORTED_FAILURES = 200
FORBIDDEN_GIT_METADATA_PATTERNS = [
    re.compile(r"179289185"),
    re.compile(r"Bella0331", re.IGNORECASE),
    re.compile(r"KentonoMacBook", re.IGNORECASE),
    re.compile(r"Kento Ogata", re.IGNORECASE),
]

TEXT_EXTENSIONS = {
    "",
    ".asset",
    ".bat",
    ".cfg",
    ".command",
    ".controller",
    ".cs",
    ".env",
    ".html",
    ".json",
    ".md",
    ".meta",
    ".plist",
    ".prefab",
    ".ps1",
    ".py",
    ".sh",
    ".txt",
    ".unity",
    ".xml",
    ".yaml",
    ".yml",
    ".overrideController",
}

SKIP_PARTS = {
    ".git",
    ".venv",
    "Library",
    "Logs",
    "Temp",
    "UserSettings",
    "__pycache__",
    "node_modules",
}
SELF_REFERENCE_FILES = {
    "scripts/audit_distribution_release.py",
    "scripts/audit_distribution_release.ps1",
    "scripts/publication_guard.py",
}
SELF_REFERENCE_ONLY_REASONS = {
    "personal build profile marker must not ship",
    "personal alpha build path/name must not ship",
    "personal product name must not ship",
    "personal bundle identifier must not ship",
    "private avatar slot id must not ship",
}


@dataclass(frozen=True)
class Rule:
    pattern: re.Pattern[str]
    reason: str


PATH_RULES = [
    Rule(re.compile(r"(^|/)scripts/(audit_private_patterns|publication_guard\.local)\.txt$", re.IGNORECASE), "local-only audit pattern files must not ship"),
    Rule(re.compile(r"(^|/)docs/handoffs(/|$)", re.IGNORECASE), "handoff/session notes are local-only"),
    Rule(re.compile(rf"(^|/)docs/{RESTART_NOTE_PREFIX}_[0-9]{{8}}\.md$", re.IGNORECASE), "restart inventory is local-only"),
    Rule(re.compile(rf"(^|/){SESSION_NOTE_PREFIX}_.*\.md$", re.IGNORECASE), "root handoff prompts are local-only"),
    Rule(re.compile(r"(^|/)builds(/|$)", re.IGNORECASE), "generated builds must be distributed outside git history"),
    Rule(re.compile(r"(^|/)unity/(Library|Logs|Temp|UserSettings)(/|$)", re.IGNORECASE), "Unity generated/local folders must not ship"),
    Rule(re.compile(r"(^|/)\.env$", re.IGNORECASE), "real environment files must not ship"),
    Rule(re.compile(r"(^|/)backend/data/(?!\.gitkeep$)", re.IGNORECASE), "local databases and generated audio must not ship"),
]

TEXT_RULES = [
    Rule(re.compile(re.escape(AI_TOOL_MARKER), re.IGNORECASE), "AI-session marker must not appear in public text"),
    Rule(re.compile(r"/Users/[A-Za-z0-9._-]+"), "macOS user home path must not ship"),
    Rule(re.compile(r"\\Users\\[A-Za-z0-9._-]+", re.IGNORECASE), "Windows user home path must not ship"),
    Rule(re.compile(rf"\b[A-Za-z0-9._%+-]+@{PRIVATE_WEBMAIL_DOMAIN}\.com\b", re.IGNORECASE), "private webmail address must not ship"),
    Rule(re.compile(rf"\b[A-Za-z0-9._%+-]+-{PRIVATE_WEBMAIL_TOKEN}\b", re.IGNORECASE), "Unity-style private account identifier must not ship"),
    Rule(re.compile(rf"{UNITY_ORGANIZATION_FIELD}:[ \t]*[^\r\n \t]+", re.IGNORECASE), "Unity organization id must be blank in public settings"),
    Rule(re.compile(r"sk-proj-[A-Za-z0-9_-]{20,}"), "OpenAI project key must not ship"),
    Rule(re.compile(r"sk-[A-Za-z0-9_-]{20,}"), "OpenAI API key must not ship"),
    Rule(re.compile(r"AIza[0-9A-Za-z_-]{20,}"), "Google API key must not ship"),
    Rule(re.compile(r"gh[pousr]_[A-Za-z0-9_]{20,}"), "GitHub token must not ship"),
    Rule(re.compile(r"YUI_PROFILE_PERSONAL"), "personal build profile marker must not ship"),
    Rule(re.compile(r"PersonalAlpha", re.IGNORECASE), "personal alpha build path/name must not ship"),
    Rule(re.compile(r"Yui VRM AI Studio Personal", re.IGNORECASE), "personal product name must not ship"),
    Rule(re.compile(r"jp\.tsubamechan\.yuivrm\.personal", re.IGNORECASE), "personal bundle identifier must not ship"),
]

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


def run_git(args: list[str], root: Path, check: bool = True) -> subprocess.CompletedProcess[str]:
    return subprocess.run(
        ["git", *args],
        cwd=root,
        check=check,
        text=True,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
    )


def repo_root() -> Path:
    result = run_git(["rev-parse", "--show-toplevel"], Path.cwd())
    return Path(result.stdout.strip()).resolve()


def load_local_rules(root: Path) -> list[Rule]:
    path = root / LOCAL_PATTERN_FILE
    if not path.exists():
        return []

    rules: list[Rule] = []
    for line_number, raw_line in enumerate(path.read_text(encoding="utf-8", errors="ignore").splitlines(), start=1):
        line = raw_line.strip()
        if not line or line.startswith("#"):
            continue
        if "|" in line:
            pattern, reason = line.split("|", 1)
            reason = reason.strip() or "local private pattern"
        else:
            pattern, reason = line, "local private pattern"
        try:
            rules.append(Rule(re.compile(pattern, re.IGNORECASE), reason))
        except re.error as exc:
            raise SystemExit(f"{LOCAL_PATTERN_FILE}:{line_number}: invalid regex: {exc}") from exc
    return rules


def nul_lines(value: str) -> list[str]:
    return [item for item in value.split("\0") if item]


def paths_for_scope(root: Path, scope: str) -> list[Path]:
    if scope == "staged":
        result = run_git(["diff", "--cached", "--name-only", "-z", "--diff-filter=ACMRT"], root)
    elif scope == "tracked":
        result = run_git(["ls-files", "-z"], root)
    else:
        tracked = nul_lines(run_git(["ls-files", "-z"], root).stdout)
        untracked = nul_lines(run_git(["ls-files", "--others", "--exclude-standard", "-z"], root).stdout)
        return [root / item for item in sorted(set(tracked + untracked))]
    return [root / item for item in nul_lines(result.stdout)]


def is_skipped(path: Path, root: Path) -> bool:
    try:
        relative = path.relative_to(root)
    except ValueError:
        return True
    return any(part in SKIP_PARTS for part in relative.parts)


def is_text_candidate(path: Path) -> bool:
    return path.suffix in TEXT_EXTENSIONS or path.name in {".env", ".env.example", ".gitignore"}


def looks_binary(path: Path) -> bool:
    try:
        with path.open("rb") as handle:
            return b"\0" in handle.read(4096)
    except OSError:
        return True


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


def audit_paths(root: Path, paths: list[Path]) -> tuple[list[str], int]:
    failures: list[str] = []
    omitted = 0
    all_text_rules = [*TEXT_RULES, *load_local_rules(root)]

    def report(message: str) -> None:
        nonlocal omitted
        if len(failures) < MAX_REPORTED_FAILURES:
            failures.append(message)
        else:
            omitted += 1

    for path in paths:
        if not path.exists() or is_skipped(path, root):
            continue
        relative = path.relative_to(root).as_posix()
        if contains_private_hash(relative):
            report(f"{relative}: local-only avatar identifier must not ship")
            continue

        for rule in PATH_RULES:
            if rule.pattern.search(relative):
                report(f"{relative}: {rule.reason}")
                break

        if not path.is_file() or not is_text_candidate(path) or looks_binary(path):
            continue
        text = read_text(path)
        if contains_private_hash(text):
            report(f"{relative}: local-only avatar identifier must not ship")
            continue
        for rule in all_text_rules:
            if relative in SELF_REFERENCE_FILES and rule.reason in SELF_REFERENCE_ONLY_REASONS:
                continue
            if rule.pattern.search(text):
                report(f"{relative}: {rule.reason}")
                break

    return failures, omitted


def audit_git_metadata(root: Path, maintainer_mode: bool) -> list[str]:
    failures: list[str] = []

    branch = run_git(["branch", "--show-current"], root, check=False).stdout.strip()
    if branch and AI_TOOL_MARKER.lower() in branch.lower():
        failures.append("current branch name contains an AI-session marker")

    log = run_git(
        ["log", "--format=%H%x00%an%x00%ae%x00%cn%x00%ce", "--max-count=200"],
        root,
        check=False,
    ).stdout
    for raw_entry in log.splitlines():
        fields = raw_entry.split("\0")
        if len(fields) != 5:
            continue
        commit_sha, author_name, author_email, committer_name, committer_email = fields
        metadata = "\n".join([author_name, author_email, committer_name, committer_email])
        if any(pattern.search(metadata) for pattern in FORBIDDEN_GIT_METADATA_PATTERNS):
            failures.append(f"{commit_sha[:12]} has forbidden public git author/committer metadata")

    if not maintainer_mode:
        return failures

    name = run_git(["config", "--get", "user.name"], root, check=False).stdout.strip()
    email = run_git(["config", "--get", "user.email"], root, check=False).stdout.strip()
    if name and name != ALLOWED_MAINTAINER_NAME:
        failures.append(f"git user.name should be {ALLOWED_MAINTAINER_NAME!r} for maintainer public pushes")
    if email and not email.endswith(ALLOWED_MAINTAINER_EMAIL_SUFFIX):
        failures.append("git user.email should use the GitHub noreply address for maintainer public pushes")

    return failures


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--scope", choices=["staged", "tracked", "working"], default="tracked")
    parser.add_argument("--check-git-metadata", action="store_true")
    parser.add_argument(
        "--check-readme-routing",
        action="store_true",
        help="Deprecated compatibility flag kept for older GitHub workflows.",
    )
    parser.add_argument("--maintainer-mode", action="store_true")
    args = parser.parse_args()

    root = repo_root()
    failures, omitted = audit_paths(root, paths_for_scope(root, args.scope))
    if args.check_git_metadata:
        for failure in audit_git_metadata(root, args.maintainer_mode):
            if len(failures) < MAX_REPORTED_FAILURES:
                failures.append(failure)
            else:
                omitted += 1

    if failures:
        print("Publication guard failed:")
        for failure in failures:
            print(f"- {failure}")
        if omitted:
            print(f"- ... {omitted} more issue(s) omitted")
        print("")
        print(f"Add private exact-match rules to {LOCAL_PATTERN_FILE} when a local-only identifier is specific to this machine.")
        return 1

    print("Publication guard passed.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
