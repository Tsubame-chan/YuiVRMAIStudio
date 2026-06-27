# Documentation Hygiene

## Purpose

Keep handoff and status documents useful instead of letting them become a second, conflicting codebase.

## Folders

- `docs/status_YYYYMMDD_topic.md`: current status documents that summarize decisions and known issues.
- `docs/handoffs/active/`: handoff documents still useful for ongoing work.
- `docs/handoffs/old/`: historical handoffs kept only for audit/debug context.
- `docs/archive/`: obsolete planning or migration notes that should not guide current work.

## Rules

1. Keep at most one active handoff per workstream.
2. When a new handoff supersedes an old one, move the old one to `docs/handoffs/old/`.
3. Do not keep duplicate root-level handoff files.
4. If two documents contain the same setup steps, keep the newer tested version and archive the older one.
5. Status documents must name the date, affected platform, known working behavior, known broken behavior, and next verification step.
6. Build/debug logs belong in `logs/`; handoff documents should link or summarize them, not paste large logs.
7. Do not delete historical docs during active debugging unless they are generated noise such as cache READMEs.
8. Do not publish handoff prompts or restart inventories. Public repositories should carry durable setup, release, and architecture docs only.
9. Put exact local-only identifiers in `scripts/publication_guard.local.txt`, not in tracked documentation or audit scripts.

## Publication Guard

Run the guard before publishing docs or generated public copies:

```bash
python3 scripts/publication_guard.py --scope working --check-git-metadata
```

Install the hooks once per clone:

```bash
./scripts/install_publication_guards.sh
```

See `docs/PUBLICATION_GUARDRAILS.md` for the full policy.

## Current Cleanup Recommendation

Root-level handoff files should not remain in the workspace. Keep active
handoffs under `docs/handoffs/active/` in the private workspace and move
superseded handoffs to `docs/handoffs/old/`.

When a handoff is no longer needed, keep a durable public-safe summary in a
dated status or architecture document and leave local-only prompts out of the
generated public repository.

`public/YuiVRMAIStudio_Public/` is a generated or nested public copy. Do not
use it as canonical source unless it has been regenerated or explicitly
refreshed from the current canonical docs.
