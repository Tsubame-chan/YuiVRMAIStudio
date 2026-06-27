# Build Variants

## Purpose

Yui VRM AI Studio currently has two distribution profiles:

- **Personal**: private builds for the repository owner's own devices and workflow.
- **Public**: shareable BYOK alpha builds for other users.

These names are provisional, but the separation is real. Treat them as product profiles, not as one-off build tweaks.

Related rules:

- Workspace layout and generated artifact boundaries: `docs/PROJECT_STRUCTURE.md`
- Version and artifact naming: `docs/VERSIONING_AND_NAMING.md`
- Current structural audit: `docs/PROJECT_AUDIT_20260617.md`

## Core Rule

Platform differences and product-profile differences must stay separate.

- Platform difference: iOS, Android, Windows, macOS, WebGL, device permissions, native audio sessions, signing, packaging.
- Product-profile difference: default avatar, bundled private assets, app name, bundle ID, backend defaults, onboarding copy, public-safe docs.
- Runtime preference: user-selected avatar, backend URL, mic device, volume, conversation mode.

Do not fix a Personal issue by changing a Public default unless the same behavior is actually desired for Public.

## Variant Matrix

| Area | Personal | Public |
| --- | --- | --- |
| Audience | One owner, trusted local devices | External users, BYOK alpha |
| Bundle/package ID | Personal-only ID, e.g. `jp.tsubamechan.yuivrm.personal` | Public ID, never sharing Personal IDs |
| Default avatar | Owner-selected private avatar or chosen personal default | Public-safe demo avatar only |
| Bundled private assets | Allowed if private build only | Forbidden |
| Backend default | Can target local mobile backend/Tailscale during development | Localhost/BYOK setup by default |
| Secrets | Never committed; local only | Never committed; user supplies BYOK |
| First-run UX | May assume owner's preferred defaults | Must be generic and explain setup through docs/UI |
| Docs | Internal handoff/status allowed | Public-safe setup/release docs only |
| Signing | Personal Apple/team/dev signing | Release/distribution signing per platform |

## Defaults Policy

### Avatar Defaults

Personal builds may default to the owner's preferred avatar. Public builds must default to a redistributable avatar.

Runtime avatar selection should still persist after the user changes it. Reinstall/clear-data behavior may reset to the profile default.

Implementation direction:

- Keep a build-profile default avatar setting.
- Keep runtime user selection in PlayerPrefs or equivalent.
- Do not encode Personal avatar defaults in a generic public scene.
- Public build audit must fail if private avatar paths or names leak into public assets.

Current implementation:

- `YUI_PROFILE_PERSONAL` builds default to `demo_kikyo`.
- `YUI_PROFILE_PUBLIC` builds default to `unitychan_default`.
- `YuiBuildProfile.DefaultAvatarSlot` is the single runtime source for profile avatar defaults.
- Build scripts must set exactly one profile define before exporting/building.

### Backend Defaults

Personal mobile builds may use a local-network/Tailscale backend during testing, but this should be a profile default or developer override.

Public builds should default to the documented BYOK/local backend flow. Public users should not inherit a private Tailscale IP, personal Mac hostname, or private bundle setting.

### Audio And Realtime

Realtime audio policy should be shared across profiles:

- assistant playback and realtime input must coordinate through common code
- platform-specific audio session changes belong under platform adapters
- Personal and Public should not diverge in realtime state logic

Profile-specific differences should be limited to whether realtime features are enabled by default and how experimental warnings are presented.

## Platform Policy

### iOS

iOS needs signing, permissions, ATS/local-network allowances, and native audio session handling. Managed iOS shims belong under `Assets/App/Scripts/Platform/iOS`; native iOS code belongs in `Assets/Plugins/iOS`. Neither belongs in Public/Personal gameplay logic.

### Android

Android should follow the same realtime input/playback policy. If audio focus or speaker routing is needed, add an Android platform adapter rather than branching in chat UI code.

### Windows

Windows Public alpha is currently the primary public distribution target. Installer scripts, VOICEVOX discovery, and BYOK setup must remain public-safe and reproducible.

### macOS

macOS is currently used heavily for development and backend hosting. A future macOS app should get its own profile/platform packaging path instead of borrowing iOS device assumptions.

## Build Ownership

Each build script should make its target explicit in the name and logs:

- profile: `personal` or `public`
- platform: `ios`, `android`, `windows`, `macos`
- configuration: `debug`, `alpha`, `release`

Examples:

- `export_ios_personal_unity_macos.sh`
- `build_ios_personal_xcode_macos.sh`
- `prepare_public_repository.py`
- future: `build_windows_public_alpha.ps1`
- future: `export_android_personal_unity_macos.sh`

Build profile defines:

- Personal: `YUI_PROFILE_PERSONAL`
- Public: `YUI_PROFILE_PUBLIC`

Do not rely on scene serialized defaults for profile-specific behavior.

## Release Gate

Before calling a build releasable:

1. Confirm the profile and platform.
2. Confirm the default avatar is correct for that profile.
3. Confirm backend defaults do not leak private endpoints.
4. Confirm secrets are not present.
5. Confirm public docs match the actual setup flow.
6. Confirm realtime behavior on that platform or explicitly mark it experimental/disabled.
7. Record the result in a dated status document.

## Current Decisions

- The iOS work in `builds/unity_2022_3_62_patch_test` is a **Personal iOS Debug/Alpha** path.
- The GitHub-facing package under `public/YuiVRMAIStudio_Public` is a **Public Windows BYOK Alpha** path.
- Personal avatar defaults should be restored through a Personal build-profile setting, not by changing Public defaults.
- Realtime audio fixes should be implemented once in shared realtime policy and platform adapters, not separately per Personal/Public build.
