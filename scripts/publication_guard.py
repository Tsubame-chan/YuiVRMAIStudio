#!/usr/bin/env python3
"""Block public pushes that contain local-only identity, notes, or secrets."""

from __future__ import annotations

import argparse
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
PERSONAL_WEBMAIL_DOMAIN = "g" + "mail"
PERSONAL_WEBMAIL_TOKEN = PERSONAL_WEBMAIL_DOMAIN + "-com"
UNITY_ORGANIZATION_FIELD = "organization" + "Id"
SESSION_NOTE_PREFIX = "handoff" + "_prompt"
RESTART_NOTE_PREFIX = "PROJECT_RESTART" + "_INVENTORY"
MAX_REPORTED_FAILURES = 200

TEXT_EXTENSIONS = {
    "",
    ".bat",
    ".cfg",
    ".command",
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
    Rule(re.compile(rf"\b[A-Za-z0-9._%+-]+@{PERSONAL_WEBMAIL_DOMAIN}\.com\b", re.IGNORECASE), "personal webmail address must not ship"),
    Rule(re.compile(rf"\b[A-Za-z0-9._%+-]+-{PERSONAL_WEBMAIL_TOKEN}\b", re.IGNORECASE), "Unity-style personal account identifier must not ship"),
    Rule(re.compile(rf"{UNITY_ORGANIZATION_FIELD}:[ \t]*[^\r\n \t]+", re.IGNORECASE), "Unity organization id must be blank in public settings"),
    Rule(re.compile(r"sk-proj-[A-Za-z0-9_-]{20,}"), "OpenAI project key must not ship"),
    Rule(re.compile(r"sk-[A-Za-z0-9_-]{20,}"), "OpenAI API key must not ship"),
    Rule(re.compile(r"AIza[0-9A-Za-z_-]{20,}"), "Google API key must not ship"),
    Rule(re.compile(r"gh[pousr]_[A-Za-z0-9_]{20,}"), "GitHub token must not ship"),
]


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


def read_text(path: Path) -> str:
    try:
        return path.read_text(encoding="utf-8", errors="ignore")
    except OSError:
        return ""


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

        for rule in PATH_RULES:
            if rule.pattern.search(relative):
                report(f"{relative}: {rule.reason}")
                break

        if not path.is_file() or not is_text_candidate(path):
            continue
        text = read_text(path)
        for rule in all_text_rules:
            if rule.pattern.search(text):
                report(f"{relative}: {rule.reason}")
                break

    return failures, omitted


def audit_git_metadata(root: Path, maintainer_mode: bool) -> list[str]:
    failures: list[str] = []

    branch = run_git(["branch", "--show-current"], root, check=False).stdout.strip()
    if branch and AI_TOOL_MARKER.lower() in branch.lower():
        failures.append("current branch name contains an AI-session marker")

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
