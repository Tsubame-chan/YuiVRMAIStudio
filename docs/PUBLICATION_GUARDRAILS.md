# Publication Guardrails

This repository has to separate public source from local development state.
The risky material is not only API keys. It also includes local paths, account
identifiers, branch names, commit authors, generated Unity folders, build
outputs, and handoff notes that were useful during development but confusing or
unsafe in a public repository.

## What Commonly Leaks

- Local identity: machine user paths, personal email addresses, Unity account
  identifiers, and maintainer commit metadata.
- Local notes: handoff prompts, restart inventories, debug logs, and status
  notes that contain private workflow details.
- Generated state: Unity `Library`, `Temp`, `Logs`, `UserSettings`, build
  outputs, local databases, generated audio, and cache folders.
- Secrets: `.env`, API keys, service tokens, cookies, and copied backend
  configuration.
- Tooling residue: branch names or text copied from an AI coding session.

## Why It Happens

The project is developed in a mixed workspace that contains public code,
personal builds, generated Unity state, and session handoff documents. A human
checklist is too easy to skip, especially when a different session prepares a
push from a branch or clean copy. The guardrails therefore fail closed before
the push instead of relying on someone reading the right document.

## Guardrail Layers

1. Local hooks in `.githooks/` run before commit and push.
2. `scripts/publication_guard.py` scans staged or tracked files for public
   blockers.
3. `scripts/audit_distribution_release.py` verifies release-specific source
   requirements and Unity/public build hygiene.
4. GitHub Actions runs the same guard after a push or pull request, so direct
   GUI changes and other local sessions are checked too.
5. Machine-specific exact patterns belong in
   `scripts/publication_guard.local.txt`, which is gitignored.

## One-Time Local Setup

Run this once in each clone used for publication:

```bash
./scripts/install_publication_guards.sh
```

For maintainer public pushes, configure Git with the public GitHub identity:

```bash
git config user.name "Tsubame-chan"
git config user.email "Tsubame-chan@users.noreply.github.com"
```

Add local-only identifiers that should never leave this machine to:

```text
scripts/publication_guard.local.txt
```

Each non-comment line is a regular expression, optionally followed by a reason:

```text
private-avatar-name|private avatar name must not ship
private-account-fragment|local account identifier must not ship
```

Do not put real private identifiers in tracked audit scripts. Keep exact
needles in the local pattern file.

## Manual Checks

Before publishing from a clean public copy:

```bash
python3 scripts/publication_guard.py --scope tracked --check-git-metadata --maintainer-mode
python3 scripts/audit_distribution_release.py --project-root .
```

Before committing a large documentation or Unity change:

```bash
python3 scripts/publication_guard.py --scope working --check-git-metadata
```

If the guard fails, fix the source of the leak. Do not bypass the hook unless
the same commit has passed these commands in a clean public clone.
