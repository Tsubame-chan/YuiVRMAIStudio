# Store release acceptance

Updated: 2026-09-22. **Not approved for public store distribution yet.**

Published GitHub downloads remain `v0.2.0-beta.5`; development builds and unpublished source changes are not that release. A successful Unity/Xcode build does not establish device acceptance.

## Scope

The initial product must support its bundled avatar, VRM import, text/voice conversation and the AI/TTS combinations offered on the selected device. Existing VRChat-to-VRM converters are the evaluated import path; arbitrary shaders, FX menus and original PhysBone behaviour are not promised. Automatic resting poses have limitations. Topic-separated conversation sessions and a full manual IK editor are deferred; neither should appear as a working feature.

## Required before distribution

| Gate | Evidence required |
| --- | --- |
| Fresh install | Bundled avatar appears; required local data exists; no hidden desktop Backend dependency on iOS. Mobile builds fail if the E2B model, default voice or dictionary is missing. |
| Conversation | Actual-device microphone → transcription → response → speech, plus text/images, for the offered local/API/Backend routes. Cancel, permission refusal, network loss and retry preserve user input. |
| Avatar import | Native document picker, cancellation, invalid files, import, lip-sync, switching and restart work on the release OS. Purchased personal avatars are never in public builds. |
| Data handling | Content-transfer permission precedes external requests, including voice/fallback routes. Refusal sends nothing; revocation works. Apple key migration and removal work after restart. Conversation bodies do not enter normal diagnostic logs. |
| Store metadata | Working privacy/support URLs; accurate App Privacy disclosures, age rating, licences/credits and review instructions. No developer keys or private user data in the archive. |
| Performance | Measure memory/termination, thermal behaviour, long conversations and background/resume on the supported iPhone range. Inspect a Release archive's actual install size; do not use Xcode project size as a proxy. Exclude machine-generated inference caches. |
| Distribution coherence | Source, app ZIP, Backend bundle, model manifest, checksums and README refer to the same candidate. Rebuild from a fresh sanitized tree and verify real downloaded assets. Docker context matches its Dockerfile. |

The current mobile path bundles its AI data; the desktop first-run download manifest does not provide mobile assets. The minimum-data packaging script uses E2B and the default VOICEVOX voice only. A future mobile download flow needs its own tested manifest and lifecycle, not a desktop manifest relabelled as mobile.

macOS validation does not establish Windows/Android/iOS acceptance. Publish an explicit platform matrix rather than marking untested targets ready.

## Distribution stages

Apple's [review guidelines](https://developer.apple.com/app-store/review/guidelines/#beta-testing) direct beta testing to TestFlight. An App Store version must meet the production requirements for its declared scope. Apple's [data-use rules](https://developer.apple.com/app-store/review/guidelines/#data-use-and-sharing) also require disclosure and permission for third-party AI sharing. Passing local checks does not guarantee Apple's approval.

Personal verification builds must use a separate output and bundle identifier until accepted. Preserve the previous stable personal app and data. Do not publish private build outputs or copy private assets into the public source tree.
