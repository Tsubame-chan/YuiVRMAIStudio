# Local AI And TTS Assets

Yui VRM AI Studio keeps large local AI/TTS assets out of git history. The code,
settings, and lightweight manifests live in the repository. The model files,
voice dictionaries, voice models, and generated app builds are distributed as
GitHub Release assets or prepared locally before building.

## Quick Choice

| Case | What to download |
| --- | --- |
| I want to run the app now | Download the current runnable Desktop Public Beta app release files from GitHub Releases. At the moment, the app ZIPs are in `v0.2.0-beta.1`; the Latest `v0.2.0-beta.2` Release is for the first-run downloader manifest and Desktop Local AI asset pack. |
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

A complete Beta Release should include the app artifacts and checksum files.
Large artifacts may be uploaded as split `.part-*` files instead of one ZIP.

```text
YuiVRMAIStudio_WindowsPublicBeta_<version>_windows.zip.part-*
YuiVRMAIStudio_WindowsPublicBeta_<version>_windows.zip.sha256
YuiVRMAIStudio_MacOSPublicBeta_<version>_macos.zip.part-*
YuiVRMAIStudio_MacOSPublicBeta_<version>_macos.zip.sha256
YuiVRMAIStudio_LocalAIAssets_DesktopMinimum_<version>.zip.part-*
YuiVRMAIStudio_LocalAIAssets_DesktopMinimum_<version>.zip.sha256
```

The app ZIP is for normal users and contains the minimum local
set. Because these files are large, Release assets may be split into `.part-*`
files, but no extra data should be required for the minimum app experience.
The `YuiVRMAIStudio_LocalAIAssets_DesktopMinimum` ZIP, and the older
`LocalAIAssets_Minimum` ZIP naming, are mainly for source builders or as a fallback
distribution when a platform artifact must be split because of hosting limits.
Optional voice packs may be added in future Releases for larger or more
experimental embedded runtimes. Backend AivisSpeech HD and Irodori TTS are
usually installed as separate local runtimes instead of being bundled into the
minimum app ZIP.

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

## Future Download And Update Direction

The current Beta bundles the minimum Local Gemma and Local VOICEVOX set inside
the desktop app artifacts so users can try the app without extra setup.

The preferred longer-term distribution model is:

- keep the app download smaller,
- let the app download large Local Gemma data from GitHub Releases on first run,
- verify downloaded files with sha256 before enabling the local model, and
- expose app update checks through the app UI while still using GitHub Releases
  as the trusted source.

This keeps the public distribution on GitHub while reducing the need for users
to manually choose and join multiple large `.part-*` files.

## Maintainer Packaging

Set `YUI_RELEASE_VERSION` to the release version being prepared. Do not reuse
`v0.2.0-beta.1` for rebuilt app ZIPs, because that tag is the older runnable
app release before the first-run downloader source was published.

On macOS, create the macOS app archive with:

```bash
YUI_RELEASE_VERSION=v0.2.0-beta.3 ./scripts/package_macos_public_beta_macos.sh
```

Create the minimum source-build asset pack with:

```bash
YUI_RELEASE_VERSION=v0.2.0-beta.3 ./scripts/package_minimum_local_ai_assets_macos.sh
```

By default the script also creates `.part-000`, `.part-001`, ... files beside
the ZIP. Upload the split parts if the hosting target has a per-file size
limit, and keep the `.sha256` file with them. Users can reassemble split parts
on macOS/Linux with:

```bash
cat YuiVRMAIStudio_LocalAIAssets_DesktopMinimum_v0.2.0-beta.2.zip.part-* > YuiVRMAIStudio_LocalAIAssets_DesktopMinimum_v0.2.0-beta.2.zip
shasum -a 256 -c YuiVRMAIStudio_LocalAIAssets_DesktopMinimum_v0.2.0-beta.2.zip.sha256
```
