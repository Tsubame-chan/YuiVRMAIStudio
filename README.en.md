# Yui VRM AI Studio

[日本語](README.md)

A Unity application for talking with your VRM avatar using text, voice and images. Use the included public default avatar or import your own VRM.

## Downloads and source status

| Item | Status |
| --- | --- |
| Windows / macOS apps | Available in [v0.2.0-beta.5](https://github.com/Tsubame-chan/YuiVRMAIStudio/releases/tag/v0.2.0-beta.5) |
| main branch | Updated source as of 2026-09-23; newer than the beta.5 binaries |
| [Source validation snapshot](https://github.com/Tsubame-chan/YuiVRMAIStudio/releases/tag/dev-snapshot-20260923) | Standard VOICEVOX models and terms, not a finished application release |
| iOS / Android | Implementation and validation source; no store release |

`Code → Download ZIP` contains source, not built apps or large AI models. To try the published app, download the beta.5 ZIP for your OS. Its first-run downloader installs Local AI, VOICEVOX and the platform-specific Backend bundle. Keep its manifest, split archives and checksums together.

See [Windows setup](docs/SETUP_GUIDE.md), [macOS setup](docs/MAC_PUBLIC_BETA.en.md), [large assets](docs/LOCAL_AI_ASSETS.md) and [current source status](docs/SOURCE_STATUS.md).

## Implemented in the current source

- VRM 0.x / 1.0 import, characters and outfits, lip sync, blinking and Humanoid idle-pose adjustment.
- Short conversation in Talk; detailed screen output and spoken summaries in Work.
- Text, microphone and image input. On mobile the paperclip opens the photo picker; take a photo in the camera app first. Desktop retains image/camera selection.
- On-device Gemma, Direct OpenAI and Backend AI. Choose the voice engine independently of the language model.
- Persistent local conversation history, saved answers, read-aloud and Secret Mode.
- Japanese / English UI, shared typography, Soft Gradient backgrounds and connection diagnostics.
- Backend web search, speech and image processing. Realtime conversation/translation remain experimental advanced options.

Availability depends on the OS, runtime, models and connected services. This list does not claim acceptance on every device.

## AI and voices

Direct OpenAI uses the key saved in the app and does not require a PC Backend. API usage charges apply. Backend requests use the Backend's own configuration; the app key is not forwarded to it. An on-device language model can use a reachable Backend voice engine.

On-device VOICEVOX requires Core, its dictionary and model files. Beta.5 provides the Meimei Himari model. The separate source snapshot supplies the current source's five standard voices; see [source status](docs/SOURCE_STATUS.md). The beta.5 manifest references the published beta.3 macOS AivisSpeech HD add-on. Irodori and Windows voice add-ons are not included in that manifest. Backend VOICEVOX, AivisSpeech and HTTP TTS require separately configured engines.

## Bring your avatar

In the current source, use Settings → Character → Import avatar. My characters → Change outfit keeps the character identity while changing appearance.

Export VRChat avatars from their configured Unity project with an existing VRM converter. Custom shaders, clothing menus and contact features are not reproduced completely. Purchased ZIPs, `.unitypackage` files and FBX cannot be imported directly. See the [avatar guide](docs/AVATAR_IMPORT.md).

## Privacy and development

The current Apple implementation stores API keys in Keychain and asks before first sending content to external AI or a Backend. Secret Mode does not prevent requests to the selected external service. These changes do not retroactively update beta.5. See [privacy](docs/PRIVACY.md).

Development uses Unity 2022.3.62f3. Model weights, generated builds, personal avatars, conversations and secrets are excluded from Git. Source builds also require the relevant SDKs, native libraries, models and signing configuration.

- [Source status and known limits](docs/SOURCE_STATUS.md)
- [Runtime support](docs/RUNTIME_SUPPORT.md)
- [Quality and validation](docs/QUALITY_AND_VALIDATION.md)
- [Public player asset validation](docs/PUBLIC_PLAYER_ASSET_VALIDATION.md)
- [API](docs/api.md)
