# Source snapshot: 2026-09-23

This page describes the current source, not the beta.5 application binaries. The source snapshot is not an App Store release or a claim of complete device acceptance.

## Implemented changes

Conversation routing and independent AI/TTS selection; Apple Keychain; external-send consent; Japanese/English UI; character/outfit management; VRM presentation; persistent conversation history; image/file picker integration; mobile recording Cancel/Send separation; baseline-preserving blink; additional standard VOICEVOX voices.

## Verification and open limits

- Unity tests: 411 passed, 0 failed, 6 skipped in the latest full run.
- Speech transcript regression: pause suffix retention, overlapping revisions, zero-duration partials, empty updates and full final replacement pass in Swift.
- Five standard voices produced non-silent WAVs through the macOS native Core implementation, including switching back to the first voice.
- macOS Unity build and signed iOS development build succeeded.
- A device report confirmed Direct API voice, photo input and saved avatar/voice restoration on iPhone 16 Pro. Local speech input lost the beginning after a pause; a timestamp-based transcript fix is implemented but still needs spoken device re-verification.
- Updated iPhone blink/voice-picker behavior, clean public install/download, lower-end device performance, Windows/Android acceptance and store submission remain incomplete.
- Avatar pose correction is not a guarantee of natural results for every body or outfit. Gallery rotation currently moves the camera, so it does not itself stimulate hair physics.

No personal/test avatars or personal conversation data are included in this repository or the voice archive.

## 音声データ

[dev-snapshot-20260923](https://github.com/Tsubame-chan/YuiVRMAIStudio/releases/tag/dev-snapshot-20260923) provides `Yui_StandardVoices_20260923.zip` and its SHA-256 checksum for current source builds. Verify the checksum, then extract at the repository root. It places files under `unity/Assets/StreamingAssets/YuiLocalAI/Voicevox/`.

| Voice / standard style | ID | Model |
| --- | --- | --- |
| 冥鳴ひまり / Meimei Himari | 14 | meimei_himari_1.vvm |
| 四国めたん / Shikoku Metan | 2 | metan_zundamon_0.vvm |
| ずんだもん / Zundamon | 3 | metan_zundamon_0.vvm |
| 九州そら / Kyushu Sora | 16 | kyushu_sora_2.vvm |
| 小夜/SAYO | 46 | sayo_15.vvm |

The app exposes standard styles only. VVM files share data across styles; this archive does not claim to contain only standard-style weights. Terms are included in `Voicevox/Licenses`; credit output voices as required by the individual voice terms. The archive has no character artwork, AI model, dictionary or native runtime. Obtain the other required assets using [LOCAL_AI_ASSETS.md](LOCAL_AI_ASSETS.md) and the OS build tooling. Beta.5's downloader does not automatically install this archive.

## Distribution boundary

The beta.5 apps and download manifest remain unchanged. The new snapshot publishes source and voice data only. Do not replace beta.5's manifest with untested generated output. App binaries, Backend bundles and model data must be verified as a matching set before a new application release.
