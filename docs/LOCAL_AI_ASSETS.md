# Local AI And TTS Assets

Yui VRM AI Studio keeps large local AI/TTS assets out of git history. The code,
settings, and lightweight manifests live in the repository. The model files,
voice dictionaries, voice models, and generated app builds are distributed as
GitHub Release assets or prepared locally before building.

## Quick Choice

| Case | What to download |
| --- | --- |
| I want to run the app now | Download the latest Desktop Public Beta app ZIP from GitHub Releases. It includes the app plus the minimum local set: Local Gemma SLM + Local VOICEVOX. |
| I want optional/high-quality voices | Install the matching backend runtime, such as AivisSpeech HD or Irodori TTS, or download optional voice asset ZIPs if a Release provides them. |
| I downloaded `Code > Download ZIP` | That is source code only. It does not include generated app builds or large local AI/TTS assets. |
| I want to build from source | Clone the repo, then restore the local AI/TTS assets before building. |

## Why The Assets Are Separate

The local asset set can include multi-GB Gemma files and large voice runtimes.
Those files do not belong in normal git commits because they make clone/pull
slow, can exceed GitHub file limits, and may have different redistribution
rules from the project code.

The source repository intentionally keeps only files such as:

- `unity/Assets/StreamingAssets/YuiLocalAI/local_ai_model_packs.json`
- runtime selection code
- capability diagnostics
- setup scripts and documentation

The Release app ZIP includes the minimum user-facing local set:

- one desktop Local Gemma SLM pack
- local VOICEVOX voice model and OpenJTalk dictionary

The source repository intentionally does not commit:

- `*.litertlm` Gemma model files
- `*.vvm` VOICEVOX voice model files
- OpenJTalk dictionary binaries
- Aivis embedded model/runtime files
- generated Windows/macOS app builds

## Expected Release Assets

A complete Beta Release should include:

```text
YuiVRMAIStudio_WindowsPublicBeta_<version>_windows.zip
YuiVRMAIStudio_MacOSPublicBeta_<version>_macos.zip
YuiVRMAIStudio_LocalAIAssets_Minimum_<version>.zip
YuiVRMAIStudio_LocalAIAssets_OptionalVoices_<version>.zip
```

The app ZIP is for normal users and contains the minimum local
set. Because these files are large, Release assets may be split into `.part-*`
files, but no extra data should be required for the minimum app experience.
The `LocalAIAssets_Minimum` ZIP is mainly for source builders or as a fallback
distribution when a platform artifact must be split because of hosting limits.
Optional voice packs are for larger or more experimental runtimes such as
embedded Aivis. Backend AivisSpeech HD and Irodori TTS are usually installed as
separate local runtimes instead of being bundled into the minimum app ZIP.

## Restore Assets For A Source Build

Extract the minimum LocalAI asset ZIP into the repository root so these paths
exist:

```text
unity/Assets/StreamingAssets/YuiLocalAI/Models/gemma-4-E4B-it.litertlm
unity/Assets/StreamingAssets/YuiLocalAI/Voicevox/Models/meimei_himari_1.vvm
unity/Assets/StreamingAssets/YuiLocalAI/Voicevox/open_jtalk_dic_utf_8-1.11/
```

Optional embedded voice assets, when distributed, restore under:

```text
unity/Assets/StreamingAssets/YuiLocalAI/Aivis/
```

After restoring assets, open the Unity project and build the target platform.
If the assets are absent, the app should still compile, but Local Gemma and
local VOICEVOX may appear unavailable until the matching files are installed or
bundled. Release app ZIPs should not put normal users in this state.

## Backend Is Separate

The backend source is in this repository. Users run it locally with the setup
scripts. Backend runtime data, `.env`, local databases, generated audio, and
private caches are never shipped through git.

Backend-only features include:

- realtime talk modes
- realtime translation
- memory DB
- backend VOICEVOX/Aivis/Irodori TTS routing
- backend provider integrations that need local service state

Local Gemma and local VOICEVOX are fallback paths, not a replacement for the
full backend feature set.

## Maintainer Packaging

On macOS, create the minimum source-build asset pack with:

```bash
YUI_RELEASE_VERSION=v0.2.0-beta.1 ./scripts/package_minimum_local_ai_assets_macos.sh
```

By default the script also creates `.part-000`, `.part-001`, ... files beside
the ZIP. Upload the split parts if the hosting target has a per-file size
limit, and keep the `.sha256` file with them. Users can reassemble split parts
on macOS/Linux with:

```bash
cat YuiVRMAIStudio_LocalAIAssets_Minimum_v0.2.0-beta.1.zip.part-* > YuiVRMAIStudio_LocalAIAssets_Minimum_v0.2.0-beta.1.zip
shasum -a 256 -c YuiVRMAIStudio_LocalAIAssets_Minimum_v0.2.0-beta.1.zip.sha256
```
