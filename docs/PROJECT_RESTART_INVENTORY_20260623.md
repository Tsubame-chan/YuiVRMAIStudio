# Project Restart Inventory - 2026-06-23

## Purpose

This document consolidates the restart state before doing more GitHub-facing work. It is an internal planning document, not a public landing page.

Recommended execution order:

1. Clean up documentation inventory.
2. Organize app implementation follow-ups.
3. Prepare a safe local/GitHub synchronization path.
4. Reflect the agreed public documentation state to GitHub.

## Current Ground Truth

- Local workspace: current private development workspace
- Local git state: `macos-app` branch, no commits, no remote, many untracked files.
- GitHub repository: `Tsubame-chan/YuiVRMAIStudio`
- GitHub default branch: `main`
- macOS public branch: `macos-public-alpha`

Do not treat this local folder as a normal synchronized clone of the public GitHub repository. For GitHub-facing updates, use a clean clone/worktree or narrow GitHub Contents API updates.

## Documentation Inventory

### Canonical Public Entry Points

Keep these as the public-facing entry set:

- `README.md`
- `README.en.md`
- `docs/SETUP_GUIDE.md`
- `docs/MAC_PUBLIC_ALPHA.md`
- `docs/MAC_PUBLIC_ALPHA.en.md`
- `docs/BUILD_VARIANTS.md`
- `docs/LLM_EXTERNAL_INFO.md`
- `docs/api.md`

Current direction:

- Root README stays platform-neutral and public-facing.
- Windows-specific setup stays in `docs/SETUP_GUIDE.md`.
- macOS public alpha setup starts from `docs/MAC_PUBLIC_ALPHA.md` and `.en.md`.
- iOS remains Personal Alpha unless a future public policy changes.
- Public docs must not include private avatars, private endpoints, Tailscale defaults, owner-device defaults, or local-only handoff content.

### Durable Internal Docs

Keep these as internal project governance/status documents:

- `docs/DOCUMENTATION_HYGIENE.md`
- `docs/PROJECT_STRUCTURE.md`
- `docs/PROJECT_AUDIT_20260617.md`
- `docs/GITHUB_PUBLICATION.md`
- `docs/PUBLIC_BYOK_SETUP.md`
- `docs/VERSIONING_AND_NAMING.md`
- `docs/ALPHA_RELEASE_CHECKLIST.md`
- `docs/WINDOWS_INSTALLER_PLAN.md`
- `docs/status_20260617_cross_platform_realtime_audio.md`

`docs/status_20260617_cross_platform_realtime_audio.md` is the best durable summary for the iOS realtime/audio merge state.

### Historical Or Superseded Docs

These should remain available for audit/debug context but should not guide new work without cross-checking a newer status document:

- `docs/status_20260616_ios_personal.md`
- `docs/MAC_SETUP.md`
- `docs/handoffs/old/handoff_prompt_20260612.md`
- `docs/handoffs/old/handoff_prompt_20260614_realtime.md`
- `docs/handoffs/old/handoff_prompt_20260615_chatpanel_split.md`
- `docs/superpowers/plans/2026-06-17-cross-platform-build-variants-and-realtime-audio.md`

`docs/MAC_SETUP.md` already has a warning that the current public macOS setup starts at `docs/MAC_PUBLIC_ALPHA.md`.

### Active Handoffs

Active handoffs after this cleanup:

- `docs/handoffs/active/handoff_prompt_20260619_readme_github_docs.md`
- `docs/handoffs/active/handoff_prompt_20260617_ios_device_build.md`

Recommended next cleanup decision:

- Keep the 2026-06-19 README/GitHub handoff active until public docs are reflected to GitHub again.
- Move the 2026-06-17 iOS device handoff to `docs/handoffs/old/` after the current iOS device/runtime state is either verified or replaced by a newer status document.

### Nested Public Copy

`public/YuiVRMAIStudio_Public/` appears to be a generated/nested public repo copy and is not the canonical source for this local workspace.

It still contains older Windows-only README language and localhost-heavy top-level setup prose. Do not use it as the basis for GitHub publication unless it is regenerated or refreshed from the canonical docs first.

## App Implementation Follow-Ups

### Highest Confidence Completed Items

From the handoff/status chain:

- iOS image picker and LOOK capture were implemented and user-tested successfully.
- LLM web search support was implemented and user-tested successfully.
- Realtime VOICEVOX multi-turn issue was fixed and user-tested successfully.
- Shared iOS/mobile changes from `builds/unity_2022_3_62_patch_test/unity` were merged into canonical `unity/`.
- Canonical `unity/` now targets Unity `2022.3.62f3`.

### Remaining Product/UI Work

Known follow-ups:

- Chat bubble copy/link interaction works, but the visual design was disliked and accepted only temporarily.
- macOS distribution/signing/notarization flow still needs productization.
- Provider selection UI is still a roadmap item.
- Gemini Vision provider exists but is not fully verified for Public Alpha.
- Grok/xAI, Ollama/LM Studio, and structured weather/map/calendar APIs are candidates, not current public guarantees.

### Remaining Platform Work

Current platform priorities:

- Windows Public Alpha: keep BYOK/local launcher flow stable and documented.
- macOS Public Alpha: keep branch-specific entry docs accurate; avoid iOS Personal defaults leaking into macOS Public.
- iOS Personal: keep private/personal assets and bundle IDs out of Public builds.
- Android: future candidate only.

Generated build/output cleanup:

- `builds/unity_2022_3_62_patch_test/` is legacy reference material after the canonical merge.
- Before deleting or archiving generated workspaces, verify no source-only files remain unmerged.
- New artifacts should move toward `artifacts/` as described in `docs/PROJECT_STRUCTURE.md`.

## Safe GitHub Synchronization Plan

Do not push this local folder directly.

Recommended path:

1. Create a clean clone outside the current mixed workspace, for example under `/private/tmp` or a dedicated `worktrees/` path.
2. Fetch `main` and `macos-public-alpha`.
3. Compare the clean clone against the local canonical docs:
   - `README.md`
   - `README.en.md`
   - `docs/SETUP_GUIDE.md`
   - `docs/MAC_PUBLIC_ALPHA.md`
   - `docs/MAC_PUBLIC_ALPHA.en.md`
   - `docs/DOCUMENTATION_HYGIENE.md`
   - this inventory document if it should be committed
4. Apply only intentional doc changes.
5. Verify public docs do not regress into Windows-only framing or owner-specific setup.
6. Commit from the clean clone only.

If using the GitHub Contents API instead, update a narrow file set and fetch each file back after update.

## GitHub Reflection Checklist

Before updating GitHub:

- Root README starts as a multi-platform Desktop Public Alpha entry point.
- `README.en.md` matches the Japanese README in structure and claims.
- `docs/SETUP_GUIDE.md` is clearly Windows-specific.
- `docs/MAC_PUBLIC_ALPHA.md` and `.en.md` exist on `main` as entry docs pointing to `macos-public-alpha`.
- No top-level README presents `127.0.0.1`, private IPs, Tailscale, or owner device defaults as universal setup.
- Public docs do not include private avatar names, private bundle IDs except in policy examples, local DerivedData paths, logs, or handoff-only instructions.
- `public/YuiVRMAIStudio_Public/` is either regenerated/refreshed or ignored for this GitHub update.

## Next Recommended Action

Prepare a clean GitHub synchronization workspace and compare live GitHub contents against the current canonical docs. After that comparison, decide whether to commit:

- only public docs, or
- public docs plus internal governance docs such as this inventory and `docs/DOCUMENTATION_HYGIENE.md`.
