# PC Backend Bundling Architecture

This document records the desktop architecture decision for Yui VRM AI Studio.
It should be used as the source of truth when changing first-run downloads,
desktop packaging, backend startup, and optional voice/model packs.

## Decision

The Windows and macOS builds should move toward a bundled local backend. The
desktop app should remain usable without a user manually starting a server, but
advanced users must still be able to start and stop the backend by itself.

The backend may be delivered in either of two equivalent ways:

- included beside the desktop app in the release ZIP; or
- downloaded on first launch from GitHub Releases through the same manifest
  system that installs the minimum Local AI data.

For large public releases, the first-launch download path is preferred because
it keeps the app ZIP smaller while still giving the user a single in-app setup
flow.

The app and backend share one product distribution, but they have separate
responsibilities:

- Unity owns the visible user experience, VRM rendering, audio input/output,
  local fallback runtime selection, and first-run download of the minimum local
  AI data required by the app.
- The backend owns network-facing integrations, OpenAI and external provider
  routing, web search, backend TTS runtimes, conversation storage, future update
  checks, and optional add-on pack management.

## Data Ownership

Minimum app-local AI data stays under Unity control. This includes the compact
desktop Local AI pack that lets the app perform basic local chat and local
VOICEVOX speech without a separately configured backend.

Future continuous updates and optional add-on packs should be backend-managed.
The backend can download, verify, and stage additional models, voice packs, and
provider runtimes while Unity only displays progress and consumes the resulting
manifest/state.

## Writable Locations

Do not design updates around modifying the installed app bundle in place.
macOS app bundles, Windows install folders, code signatures, Gatekeeper, and
antivirus tools all make in-place mutation fragile.

Use this split instead:

- App folder: mostly immutable application files, Unity player data, bundled
  backend source/runtime, launcher scripts, and license documents.
- User data folder: LocalAI data, optional voice packs, backend data, update
  manifests, generated cache, logs, and user settings.

Unity and the backend should communicate through explicit paths and manifests,
not by guessing each other's directory structure.

When the backend is installed by first-launch download, install it to:

- macOS: `Application.persistentDataPath/YuiBackend`
- Windows: `%LOCALAPPDATA%`/Unity persistent data equivalent + `YuiBackend`

Unity may also look for `YuiBackend` next to the app bundle/executable when a
release intentionally ships the backend inside the app ZIP.

The macOS public bundle can include a ready-to-run `.venv` because it is built on
macOS. The Windows public bundle is currently produced from macOS, so it ships
backend source plus setup/start/stop scripts. On first backend start, Windows
creates `backend\.venv` locally through `scripts\setup_backend_byok.ps1`. A
future Windows build machine can replace this with a prebuilt Windows Python
runtime or wheelhouse if fully offline first-run setup becomes a release
requirement.

## Desktop And Mobile Roles

The PC edition is the complete edition. A Windows or macOS install should be
able to host the full local experience: Unity app, bundled backend, local data,
backend provider routing, optional voice runtimes, web-enabled features, and
LAN/VPN access for the user's own devices.

The mobile edition is a self-contained companion client. It should include
enough local AI capability to work without a backend, but it becomes smoother
and more capable when connected to the user's PC backend.

The recommended "full power away from home" setup is:

1. Install and run the PC edition at home.
2. Connect the phone/tablet to the same private network through a user-managed
   VPN such as Tailscale or another trusted VPN.
3. Point the mobile app at the PC backend.

This lets the user carry their home AI environment outside the house without a
public hosted Yui server. The VPN provider itself does not create a separate
Yui usage fee, but the user's cellular carrier may still count the small amount
of data transferred over the SIM/mobile network.

## Backend Startup Contract

On desktop startup, Unity should:

1. Check whether the configured backend URL is already healthy.
2. Reuse the existing backend if it is healthy.
3. Start the bundled backend only when no healthy backend is available.
4. Record that Unity owns the spawned process.
5. On app quit, stop only the backend process it spawned.

Manual backend launchers remain part of the distribution:

- macOS: `Start_Yui_Backend.command` and `Stop_Yui_Backend.command`
- Windows: `Start_Yui_Backend.bat` and `Stop_Yui_Backend.bat`

The stop command may force-stop well-known Yui local service ports, but Unity's
automatic shutdown should be conservative and avoid killing a backend that was
started externally for mobile or LAN use.

## Voice And Model Redistribution

Public desktop packages must exclude voice or model files whose license blocks
unaltered redistribution. In the current local Aivis model set,
`female_voice_3` is `七日週_T2モデル` and uses a custom Tαkoe Project license
that prohibits unmodified redistribution. It must not be included in public
release assets, private-to-public migration output, or bundled backend packs.

The currently safe Aivis candidates are:

- `female_voice_1`: `まい`, ACML 1.0
- `female_voice_2`: `中2`, ACML 1.0
- `male_voice_1`: `阿井田 茂`, ACML 1.0

Every redistributed third-party runtime or model pack must include the matching
license, notice, attribution, and source-reference material required by that
component.

## Implementation Phases

1. Remove restricted Aivis voice assets and stop advertising them.
2. Package the macOS backend with clear start/stop commands.
3. Add Unity-side macOS backend discovery, health check, startup, and shutdown.
4. Verify the macOS release flow and fresh install behavior.
5. Extend the same contract to Windows after the macOS path is stable.

This order keeps the licensing cleanup independent from the process-management
work and lets macOS validate the architecture before Windows-specific packaging
is added.
