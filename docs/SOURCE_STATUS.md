# Current source and releases / 現在のソースと配布版

Updated: 2026-10-03. This source accompanies desktop **v0.2.4-beta.1** and the iOS **0.2.4 (14)** review submission. Desktop beta.5 and dev-snapshot-20260923 remain historical releases; their binaries are not updated in place.

## What changed

- Character-specific personality, common response instructions and per-model instructions are combined for normal chat. Local and Direct API chat retain the same character settings.
- Persistent character memories, retrieval and editing/deletion; restart and AI-route switching preserve the character. Different characters do not share memories. Secret mode reads existing memories but does not save the new exchange.
- E2B is the standard local model. E4B is an optional in-app download on macOS/iOS. Talk/Work have separate local inference settings and share model-capability routing.
- Desktop local chat/STT run through an isolated Python worker; VOICEVOX uses the Mac native bridge or Windows worker; the HTTP Backend server is not required for that path. Desktop runtime data is downloaded separately.
- Japanese/English UI, four illustrated tutorial pages, help replay, model help and download status, avatar loading feedback and character/outfit separation.
- VRM import, blink/lip sync/springs, file-picker recovery, and resize/camera aspect corrections.
- Public screenshots and beginner documentation reflect the current screens; only Unityちゃん appears in the tutorial/marketing captures.

## Acceptance boundaries

The pre-submission regression run passed **130 Python tests**, **485 Unity tests**, with **7 skipped**. Mac live E2B, E4B and Direct API tests covered character memory, Secret mode, restart, corrections and user/agent roles. These counts describe that run, not a fresh count for every build.

The owner accepted the iOS candidate on iPhone 16 Pro, including model switching and a fresh install. **App Store review is pending**, not approved. Japan/free distribution is configured. Backend acceptance was explicitly deferred.

The new desktop apps are published as a prerelease. macOS packaging/runtime checks are separate from the iPhone results. Windows uses the shared local inference worker with an OS-specific runtime bundle. Android has source integrations but no accepted public app. Intel Mac local inference is not covered by the Apple Silicon runtime bundle.

Local-model factual, arithmetic and subject-attribution mistakes remain. Memory retrieval is bounded and may miss relevant information; it is not perfect recall or cross-device sync. English UI does not mean an English TTS model is included. Third-party avatar shader/menu/physics compatibility is not universal.

See [runtime support](RUNTIME_SUPPORT.md), [quality checks](QUALITY_AND_VALIDATION.md), [help](HELP.md) and [data distribution](DISTRIBUTION.md).
