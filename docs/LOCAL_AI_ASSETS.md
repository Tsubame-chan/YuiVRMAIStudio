# Local AI and TTS assets

Large model weights, dictionaries and generated apps are distributed through GitHub Releases, not Git. This page separates the published beta.5 downloads from the newer source snapshot.

## Published downloads

The [beta.5 release](https://github.com/Tsubame-chan/YuiVRMAIStudio/releases/tag/v0.2.0-beta.5) contains Windows/macOS apps, the first-run manifest, Local AI data and platform Backend bundles. The first-run downloader verifies and installs the selected assets.

The beta.5 Local AI archive includes:

- `Models/gemma-4-E2B-it.litertlm`
- `Voicevox/Models/meimei_himari_1.vvm`
- `Voicevox/open_jtalk_dic_utf_8-1.11/`
- its model registry file

The manifest also references the **macOS AivisSpeech HD add-on hosted under beta.3**. That archive exists, and its GitHub digest matches the beta.5 manifest. Additional Voices downloads it when applicable. It contains the selected voices, runtime and Japanese BERT dependency. It is not an iOS/Android add-on.

Irodori and Windows voice add-ons are not present in this manifest. Their source adapters do not imply that their runtime data is shipped.

## Current source: five standard voices

The [source snapshot](https://github.com/Tsubame-chan/YuiVRMAIStudio/releases/tag/dev-snapshot-20260923) contains `Yui_StandardVoices_20260923.zip` and a checksum. It adds the four VVM files used by the five standard voices in current source. See [SOURCE_STATUS.md](SOURCE_STATUS.md) for IDs, model names and limitations.

This separate archive is for source validation. Beta.5's downloader is unchanged and does not install it automatically. It is not a replacement for Gemma, the dictionary, native libraries or the Backend bundle.

## Restore data for source builds

1. Download both `YuiVRMAIStudio_LocalAIAssets_DesktopMinimum_v0.2.0-beta.5.zip.part-*` files and the matching `.sha256` from beta.5.
2. Join the parts in filename order and verify the resulting archive against the manifest/checksum. Extract it to a temporary directory.
3. Copy its `Models` and `Voicevox` data into `unity/Assets/StreamingAssets/YuiLocalAI/`. Keep the current source's `local_ai_model_packs.json`; do not replace it with an older registry.
4. Verify the new standard-voices ZIP checksum and extract it at the repository root. Its paths already start with `unity/Assets/StreamingAssets/YuiLocalAI/Voicevox/`.
5. Prepare the OS-specific native runtime/SDK and build. Mobile build guards require all declared bundled files; compiling without model data is not a working mobile application.

Example on macOS/Linux, after downloading into one directory:

```bash
cat YuiVRMAIStudio_LocalAIAssets_DesktopMinimum_v0.2.0-beta.5.zip.part-* > YuiVRMAIStudio_LocalAIAssets_DesktopMinimum_v0.2.0-beta.5.zip
shasum -a 256 -c YuiVRMAIStudio_LocalAIAssets_DesktopMinimum_v0.2.0-beta.5.zip.sha256
shasum -a 256 -c Yui_StandardVoices_20260923.zip.sha256
```

Voice output must follow the included individual voice terms and credits. VVM files share several styles; only supported standard styles are exposed by the app. No character artwork is included in the voice snapshot.

## Backend and distribution boundary

Backend source lives in `backend/`. The beta.5 Backend bundles are older than current main; use the current Backend source when testing current code. Never ship a local `.env`, conversation DB, generated speech or user cache.

Packaging scripts explicitly select the reviewed model files. `package_minimum_local_ai_assets_macos.sh` packages source-build data; `package_desktop_local_ai_release_assets_macos.sh` packages runtime data and its manifest. Both require the assets to be restored first. Use a new version/output directory for a new candidate and verify its app, Backend, model archives and checksums together. Do not overwrite old released archives.
