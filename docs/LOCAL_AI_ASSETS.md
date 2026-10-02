# Local AI and TTS assets

Current desktop data: [v0.2.4-beta.1](https://github.com/Tsubame-chan/YuiVRMAIStudio/releases/tag/v0.2.4-beta.1), asset version **2026.10.03**. Large weights, dictionaries and generated apps are Release assets, not Git source. Older beta.5/snapshot data remains unchanged and should not be mixed with this release.

## Normal installation

Download the app ZIP for your OS. The desktop app asks before fetching its required data, verifies SHA-256, extracts into staging, checks required files and records installed versions. It uses the manifest pinned to its own release. Normally you do not manually join files or install Python.

The common archive is approximately **2.46GB**, split into two `.part-*` files to stay below GitHub's per-file limit. It contains standard E2B, four VOICEVOX VVM files for five standard voices, the Open JTalk dictionary, license notices and the model catalog. macOS/Windows runtime bundles are separate, approximately **76MB / 67MB**. They include the local inference worker and Backend source; local inference does not require its HTTP server.

| Standard voice | Style ID | VVM |
| --- | --- | --- |
| 冥鳴ひまり / Meimei Himari | 14 | meimei_himari_1.vvm |
| 四国めたん / Shikoku Metan | 2 | metan_zundamon_0.vvm |
| ずんだもん / Zundamon | 3 | metan_zundamon_0.vvm |
| 九州そら / Kyushu Sora | 16 | kyushu_sora_2.vvm |
| 小夜/SAYO | 46 | sayo_15.vvm |

The app exposes standard styles only; VVMs may contain shared weights for other styles. Follow the individual terms in `Voicevox/Licenses` and credit speech output as required. The archive does not include voice-character artwork or private avatars.

macOS/iOS offer optional E4B in Settings. macOS uses the model catalog's HTTPS source; iOS uses Apple-Hosted Background Assets. iOS includes the standard E2B/voice data and has no mandatory first-run extra download. Desktop and iOS distribution are different. Additional experimental voice packs are not included in this manifest.

## Restore source-build data

Download both common `.part-*` files and the matching `.sha256`, join them in filename order, then verify. On macOS/Linux:

```bash
cat YuiVRMAIStudio_LocalAIAssets_DesktopMinimum_v0.2.4-beta.1.zip.part-* > YuiVRMAIStudio_LocalAIAssets_DesktopMinimum_v0.2.4-beta.1.zip
shasum -a 256 -c YuiVRMAIStudio_LocalAIAssets_DesktopMinimum_v0.2.4-beta.1.zip.sha256
unzip YuiVRMAIStudio_LocalAIAssets_DesktopMinimum_v0.2.4-beta.1.zip -d restored-data
```

Copy the extracted `Models`, `Voicevox` and matching catalog into `unity/Assets/StreamingAssets/YuiLocalAI/`. Inspect the ZIP's directory layout first; do not blindly overwrite other project files. Prepare OS-specific SDK/native libraries/runtime separately. Compiling without required mobile data is not a usable mobile build.

Runtime archives are delivered as `.zip.part-000` plus checksum. For manual recovery, copy/join parts into the manifest's full `.zip` filename and verify the full SHA-256; keep the `YuiBackend` directory layout. A source build's local `.venv` is not interchangeable with every shipped OS runtime.

## Release policy

Never ship `.env`, conversation databases, generated speech, private avatars or user caches. Use a fresh public source and new release/output directory, validate app/data/runtime together, and do not overwrite old releases. See [distribution architecture](DISTRIBUTION.md), [public asset validation](PUBLIC_PLAYER_ASSET_VALIDATION.md) and [runtime support](RUNTIME_SUPPORT.md).

日本語: 通常はOS別アプリZIPだけ取得し、初回案内から必要データを導入します。GitのソースZIPにモデルはありません。標準はE2Bと日本語5声。macOS/iOSのE4Bは任意です。iOSは標準データ同梱、desktopは初回取得という違いがあります。異なる版のmanifest/runtime/モデルを手動で混ぜないでください。
