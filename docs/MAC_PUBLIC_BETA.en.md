# macOS Desktop Public Beta Setup

This page is the entry point for trying the macOS build. Start from the macOS Beta release files on GitHub Releases. GitHub `Code > Download ZIP` is source code only and does not include the built app or large local AI/TTS data.

Current packaging note: `v0.2.0-beta.3` is the current macOS runnable app Release. It includes the first-run downloader, which fetches missing Local AI/TTS data from the GitHub Releases manifest.

- Japanese guide: [`MAC_PUBLIC_BETA.md`](MAC_PUBLIC_BETA.md)

## Run It First

1. From the `v0.2.0-beta.3` GitHub Release, download these files whose names include `MacOSPublicBeta` into the same folder.
   - `YuiVRMAIStudio_MacOSPublicBeta_v0.2.0-beta.3_macos.zip`
   - `YuiVRMAIStudio_MacOSPublicBeta_v0.2.0-beta.3_macos.zip.sha256`
2. Open that folder in Terminal and verify sha256.
3. Extract the ZIP and launch `Yui VRM AI Studio.app`.

```bash
shasum -a 256 -c YuiVRMAIStudio_MacOSPublicBeta_v0.2.0-beta.3_macos.zip.sha256
```

This beta is not fully signed/notarized yet. If macOS blocks the first launch, confirm that you trust the downloaded artifact, then allow it from System Settings or the right-click open flow.

If Local AI/TTS data is missing on first launch, the in-app downloader fetches the required data from GitHub Releases.

`WindowsPublicBeta` is for Windows. `YuiVRMAIStudio_LocalAIAssets_DesktopMinimum` / `LocalAIAssets_Minimum` is for people building from source in Unity or validating the first-run downloader. You do not need either one just to try the macOS app.

## Download Types

| Download | Use |
| --- | --- |
| macOS ZIP / `.part-*` from Releases | For normal users. Includes the `.app` and minimum local AI/TTS set. Join split files before unzipping. |
| `Code > Download ZIP` | Source code only. Does not include the app bundle or large models. |
| `YuiVRMAIStudio_LocalAIAssets_DesktopMinimum` / `LocalAIAssets_Minimum` | For source builders or first-run downloader validation. |
| Optional voice/runtime | Extra voice choices such as AivisSpeech HD or Irodori TTS. Not required for the app to run. |

## What Works

- No backend: Local Gemma SLM, Local VOICEVOX, VRM display, basic chat.
- With an OpenAI API key: Direct OpenAI API, stronger chat/vision/STT paths.
- With the backend: realtime talk, realtime translation, memory DB, Backend VOICEVOX, AivisSpeech HD, and Irodori TTS.

The default `Auto Select` mode is recommended. It prefers the backend when healthy and falls back to local/direct modes when the backend is unavailable.

## Backend Setup

Only set this up when you want the full feature set. Backend scripts live in the source repository, not inside the app ZIP.

Requirements:

- Apple Silicon Mac
- Homebrew
- Python 3.12+
- OpenAI API key
- Optional external TTS runtimes such as VOICEVOX Engine, AivisSpeech HD, or Irodori TTS

Install tools:

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

Start:

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
