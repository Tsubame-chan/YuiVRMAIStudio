# Yui VRM AI Studio Product Direction

Date: 2026-07-14

This document is the product decision record for the next development phase. It replaces the idea that Yui should win by adding more chat, TTS, or character-gimmick features one at a time.

## Product definition

Yui VRM AI Studio is a **user-owned character AI homebase**.

- The PC edition is the complete homebase. It owns heavy inference, the backend, files, long-running work, optional TTS, memory, and permissioned actions.
- The iOS and Android editions remain useful alone with local AI and OS-native input, but become the pocket client for the same homebase when connected to the user's PC.
- The character, approved memory, task state, and results should remain continuous across devices.
- The user chooses what is local, what is sent to a configured provider, and which actions may modify files or external services.

The shortest consumer promise is:

> 自分のキャラクターがPCで作業を手伝い、外出時も同じ相手・記憶・仕事の続きへつながる。

This is materially different from placing a VRM on a generic chatbot. The character becomes the stable interface to the user's own AI environment.

## First target audience

The first target is a VRChat or VRM user who already owns a character and wants to use that character outside VRChat.

That audience has unusually strong character attachment and often already has a Unity/VCC avatar project. The largest adoption barrier is not AI configuration; it is producing a loadable `.vrm` copy without breaking materials, expressions, PhysBones, or licensing boundaries. Removing that barrier is therefore a P0 product feature, not peripheral tooling.

The planned answer is `Yui Avatar Bridge`, documented in [`YUI_AVATAR_BRIDGE_ARCHITECTURE.md`](YUI_AVATAR_BRIDGE_ARCHITECTURE.md).

## Flagship loops

### 1. Bring your own character

1. Add Yui Avatar Bridge to the existing VCC avatar project.
2. Select the avatar and run `Yui > Export Avatar for Yui`.
3. Review validation results and save the exported avatar.
4. Open it in Yui and see a thumbnail-backed avatar library entry.

Target promise: **自分のVRChatアバターを、数分でYuiへ。**

### 2. Talk and Work

- `Talk`: low-latency conversation. Output remains short and is spoken naturally.
- `Work`: the screen receives a complete answer, source list, draft, or procedure; only a one- or two-sentence conclusion is spoken.

This separation removes the current conflict between TTS latency and useful work output. The initial protocol and Unity control were implemented on 2026-07-14.

### 3. Share context and finish something

1. The user intentionally attaches an image, screen region/window, or supported file.
2. Yui shows where processing will run: local, home PC, or configured cloud provider.
3. Yui produces a reusable result: explanation, translation, summary, rewrite, draft, or extracted data.
4. The result can be copied or saved. Any external side effect requires confirmation.

### 4. Continue away from the PC

1. A long task starts on the PC homebase.
2. The mobile app shows queued/running/completed state through an authenticated connection.
3. The user can read the result, ask a follow-up, or send a new task home.
4. The PC remains the authority for files, memory, models, and audit history.

VPN software itself is outside Yui and has no Yui usage fee. Mobile carrier traffic still counts normally. Directly exposing an unauthenticated backend port is never an acceptable onboarding path.

## Competitor-derived feature list

These projects overlap with parts of Yui but are not treated as identical products. Their strongest interaction patterns should be adapted without copying branding or implementation.

| Priority | Source | Useful observation | Yui adaptation | Status |
| --- | --- | --- | --- | --- |
| P0 | [Webcam Motion Capture beta](https://webcammotioncapture.info/ja/beta.php) | A Unity/VCC tool exports the selected VRChat avatar into a runtime-loadable form. | VPM-distributed Yui Avatar Bridge, initially exporting portable VRM plus diagnostics. | Architecture defined |
| P0 | [Utsuwa](https://github.com/The-Lab-by-Ordinary-Company/utsuwa) | Character-first screen, one composer for text/image/voice, whole-window drop state, removable attachments, explicit listening/transcribing state. | Replace fragmented task inputs with one attachment-aware composer and visible processing state. | Command labels improved; unified composer pending |
| P0 | [AIRI](https://github.com/moeru-ai/airi) | Compact status/control islands, expandable advanced controls, detailed loading only on demand, desktop-to-pocket connection. | Keep the character dominant; expose connection/routing state compactly and expand details only when needed. | Capability foundation exists; UI pending |
| P0 | Internal need | Spoken responses and work documents require different lengths. | `Talk / Work`, separate `text` and `spoken_text`, 420 vs 2200 token budgets. | Implemented |
| P1 | [AITuberKit](https://github.com/tegnike/aituber-kit) | Everyday actions stay visible; screen/camera/image tools live in a small contextual tools menu; Quick Start is separate from detailed settings. | Keep Message/Mic/Send immediate; move less frequent context tools into one `Attach` menu; split Basic and Advanced settings. | Pending |
| P1 | [Amica](https://github.com/semperai/amica) | Avatar choice and `Load VRM` are first-class visual actions. | Add an avatar library with thumbnails, import state, rename, replace, and delete. | Pending |
| P1 | [AIRI](https://github.com/moeru-ai/airi) | Screen source selection distinguishes applications, displays, and devices with clear empty/loading states. | Add `Window / Display / Region / Camera` source tabs and a persistent shared-context indicator. | Pending |
| P1 | Utsuwa | Privacy text changes based on whether media stays local or is sent to a provider. | Show a route badge on every attachment and before a remote upload. | Pending |
| P1 | AIRI Pocket and current Yui backend | A mobile stage can connect to a desktop runtime through a secure channel. | Device pairing, authenticated task inbox, reconnect state, same memory/task IDs. | Foundation only |
| P2 | AIRI/Utsuwa memory work | Persistent memory is useful only when its behavior is visible. | Review, edit, pin, forget, export, and approve durable facts; semantic retrieval later. | Pending |
| P2 | AITuberKit tools | Active tools are visibly highlighted and only relevant tools are shown. | Capability-aware action menu and explicit active-capture state. | Pending |

## UI direction

### First viewport

The first viewport must answer three questions without opening Settings:

1. Which character is here?
2. Can I talk, show something, or ask for work?
3. Is processing local, on my PC backend, or through an external provider?

The avatar stays visually primary. The bottom surface becomes a unified composer rather than a developer console.

Recommended composer order:

1. `Attach` icon: Image, Camera, Window, Display, Region, File.
2. Message/task input with removable attachment thumbnails.
3. `Mic` state button with listening/transcribing feedback.
4. `Send` command.
5. `Talk / Work` segmented control adjacent to the current task state.

The 2026-07-14 implementation is an incremental bridge: `Img / Look / Rec / Go` became `Image / Camera / Mic / Send`, and a `Talk / Work` segment is created at runtime. The full unified composer remains a dedicated UI task.

### Status hierarchy

- Always visible: Ready, Listening, Transcribing, Working, Speaking, Offline, Downloading.
- Compact route: Local, Home PC, Direct API, Realtime.
- Expandable detail: provider/model, download bytes, retry reason, diagnostics.
- Active capture must never be represented only by color.

### Settings hierarchy

- Basic: character, avatar, voice, AI mode, microphone, camera.
- Downloads: minimum data, additional voices, repair/update.
- Connections: home PC pairing, backend, direct API.
- Memory and privacy: remembered facts, route rules, clear/export.
- Advanced: provider URLs, model IDs, developer diagnostics.

## Features not to build as product pillars

- Automatic diaries, conversation summaries, or emotional artifacts generated without an explicit request.
- Forced first-launch naming or irreversible “awakening” rituals.
- Relationship levels, streak pressure, or dating-sim progress meters.
- A VRChat in-world bridge where Yui's avatar is invisible to other participants.
- More TTS engines as a substitute for a useful workflow.
- Autonomous file/app changes without preview, confirmation, and an audit record.

These may exist as opt-in experiments later, but they do not define the product.

## Prioritized implementation sequence

### Gate 0: Beta reliability

- Keep Windows/macOS source, release binaries, first-run manifests, and README behavior aligned.
- Pass backend and Unity tests in canonical and generated public trees.
- Document Apple Silicon support and unsigned/notarized macOS limitations honestly.
- Verify clean Windows installation on Windows hardware.

### Phase 1: Identity and useful output

- Ship Talk/Work end to end.
- Build Yui Avatar Bridge MVP and avatar library.
- Unify composer attachments and drag/drop.
- Add copy/save result actions and clear working state.

### Phase 2: Intentional context

- Add OS window/display/region capture.
- Add TXT/MD/PDF ingestion first; Office files after a stable extraction contract.
- Show local/home-PC/cloud route before processing.
- Record completed assist loops, not raw conversation volume.

### Phase 3: PC/mobile continuity

- Pair mobile with the homebase using short-lived credentials.
- Add a task inbox and result synchronization.
- Keep files and heavy processing on the PC unless the user chooses otherwise.
- Make offline mobile fallback and connected-homebase mode visibly distinct.

### Phase 4: Trustworthy memory and actions

- Add memory review/edit/pin/forget/export and approval policy.
- Add a common action schema, confirmation UI, audit log, and failure recovery.
- Start with copy/save/note/file operations before broad application control.

## Success criteria

The north-star event is a **completed assist loop**:

1. intentional context or task supplied;
2. useful result generated;
3. result copied, saved, or applied after confirmation.

Track activation to first completed loop, repeated loop use, avatar-import completion, PC/mobile handoff completion, and unexpected capture/action incidents. Do not optimize primarily for chat turns, response length, or daily streaks.

## Source notes

- VCC is the supported manager for VRChat Unity avatar projects and VPM packages: [VCC overview](https://vcc.docs.vrchat.com/), [VPM packages](https://vcc.docs.vrchat.com/vpm/packages/), [community repositories](https://vcc.docs.vrchat.com/guides/community-repositories/).
- UniVRM supports VRM 0.x/1.0 import and export in Unity and across Windows, macOS, iOS, and Android: [UniVRM repository](https://github.com/vrm-c/UniVRM).
- Detailed competitive evidence and beta findings are preserved in [`reports/beta_readiness_product_direction_20260714/evidence_snapshot.md`](reports/beta_readiness_product_direction_20260714/evidence_snapshot.md).
