# macOS Desktop Public Beta Setup

This page is the entry point for trying the macOS build. Start from the macOS Beta release files on GitHub Releases. GitHub `Code > Download ZIP` is source code only and does not include the built app or large local AI/TTS data.

`v0.2.0-beta.3` is the current macOS runnable app Release. On first launch, the app downloads missing Local AI/TTS data and the macOS backend bundle automatically.

- Japanese guide: [`MAC_PUBLIC_BETA.md`](MAC_PUBLIC_BETA.md)

## Run It First

1. From the `v0.2.0-beta.3` GitHub Release, download the app ZIP whose name includes `MacOSPublicBeta`.
   - `YuiVRMAIStudio_MacOSPublicBeta_v0.2.0-beta.3_macos.zip`
2. Extract the ZIP and launch `Yui VRM AI Studio.app`.
3. Start the first-run download when prompted. The required Local AI/TTS data and macOS backend bundle are installed automatically.

This beta is not fully signed/notarized yet. If macOS blocks the first launch, confirm that you trust the downloaded artifact, then allow it from System Settings or the right-click open flow.

The `.sha256` file is optional and only needed if you want to verify download integrity. Optional voices such as AivisSpeech HD can be installed later from Settings through `Additional Voices`.

`WindowsPublicBeta` is for Windows. `YuiVRMAIStudio_LocalAIAssets_DesktopMinimum`, `YuiVRMAIStudio_BackendBundle`, and older `LocalAIAssets_Minimum` downloads are normally fetched by the app or used by people validating the first-run downloader. You do not need to download them manually just to try the macOS app.

## Download Types

| Download | Use |
| --- | --- |
| macOS app ZIP from Releases | For normal users. Download this, unzip it, and launch the app. |
| `.sha256` | Optional integrity check for the app ZIP. |
| `Code > Download ZIP` | Source code only. Does not include the app bundle or large models. |
| `YuiVRMAIStudio_LocalAIAssets_DesktopMinimum` / `LocalAIAssets_Minimum` | Normally downloaded by the app. Manual use is for source builds, validation, or recovery. |
| `YuiVRMAIStudio_BackendBundle` | Normally downloaded by the app for full PC features; source builders can inspect it manually. |
| Optional voice/runtime | Extra voice choices such as AivisSpeech HD or Irodori TTS. Supported add-ons are installed from Settings through `Additional Voices`. Not required for the app to run. |

## What Works

- No backend: Local Gemma SLM, Local VOICEVOX, VRM display, basic chat.
- With an OpenAI API key: Direct OpenAI API, stronger chat/vision/STT paths.
- With the downloaded backend: realtime talk, realtime translation, memory DB, web search, Backend VOICEVOX, AivisSpeech HD, and Irodori TTS.

The default `Auto Select` mode is recommended. It prefers the backend when healthy and falls back to local/direct modes when the backend is unavailable.

## Backend Setup

Normally the first-run downloader installs `YuiBackend` and the app auto-starts it for the full PC feature set. Manual setup is mainly for source builds, debugging, or replacing the downloaded backend.

Requirements:

- Apple Silicon Mac
- Downloaded `YuiBackend`
- OpenAI API key
- Optional external TTS runtimes such as VOICEVOX Engine, AivisSpeech HD, or Irodori TTS

The macOS backend bundle includes a runnable `.venv`. Install Homebrew and
Python only for source builds or fallback setup when the bundled venv is
missing.

```bash
brew install python@3.12 git git-lfs
git lfs install
```

Initialize the backend:

```bash
PYTHON_BIN=/opt/homebrew/bin/python3.12 ./scripts/setup_backend_byok_macos.sh
```

Set your OpenAI API key in `.env`:

```bash
open -e .env
```

```env
OPENAI_API_KEY=sk-...
```

## Start And Stop Backend

If the first-run downloader installed `YuiBackend`, use the downloaded commands:

```text
YuiBackend/Start_Yui_Backend.command
```

```text
YuiBackend/Stop_Yui_Backend.command
```

For source checkouts, start:

```bash
./scripts/start_local_services_macos.sh
```

Finder launcher:

```text
Start_Yui_Local_Services.command
```

Stop:

```bash
./scripts/stop_local_services_macos.sh
```

Or:

```text
Stop_Yui_Local_Services.command
```

## VOICEVOX

The bundled Local VOICEVOX fallback is enough for minimum Japanese speech. Install VOICEVOX Engine separately when you want backend VOICEVOX tuning or your own Engine-side voice setup.

The macOS launcher mainly searches:

```text
/Applications/VOICEVOX.app/Contents/Resources/vv-engine/run
~/Applications/VOICEVOX.app/Contents/Resources/vv-engine/run
```

If VOICEVOX Engine is somewhere else, set `VOICEVOX_ENGINE_EXE`:

```bash
export VOICEVOX_ENGINE_EXE="/path/to/VOICEVOX.app/Contents/Resources/vv-engine/run"
```

## Use Your Own VRM

The app imports `.vrm` files. It cannot directly load VRChat SDK avatars, Unity prefabs, Unity scenes, `.unitypackage` files, or avatars that only exist as uploaded VRChat avatars.

Open Settings in the app and choose your `.vrm` from Custom VRM.

## Source Builds

Restore the `YuiVRMAIStudio_LocalAIAssets_DesktopMinimum` asset pack, or the older `LocalAIAssets_Minimum` release asset, into the repository root before opening the Unity project. See [`LOCAL_AI_ASSETS.md`](LOCAL_AI_ASSETS.md).

Current build verification uses Unity `2022.3.62f3`.
