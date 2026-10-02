# Yui VRM AI Studio

[日本語](README.md) · [Help](docs/HELP.md) · [Report an issue](https://github.com/Tsubame-chan/YuiVRMAIStudio/issues)

**Make your favorite VRM avatar your conversation partner.**

Import your avatar for AI conversation, advice and small tasks. Describe its personality and speaking style in your own words. No avatar yet? Start with the included Unity-chan.

<p>
  <img src="docs/images/avatar-chat.jpg" width="300" alt="An avatar replying with speech and lip movement">
  <img src="docs/images/avatar-viewer.jpg" width="300" alt="Rotate and zoom your avatar in the viewer">
</p>

## What you can do

- **Bring your own appearance.** Import VRM avatars. Change outfits while keeping the same character's personality and memories.
- **Choose its personality and replies.** Give instructions such as “be more friendly” or “start with the conclusion.” Advanced local-AI settings are also available.
- **Keep the conversation going.** Character memories are saved on your device and used across restarts and AI switches. View, edit or delete them.
- **Enjoy the avatar from any angle.** Switch to the viewer to rotate and zoom, like a digital figure.

<details>
<summary>See personality and AI settings</summary>

<p>
  <img src="docs/images/customization.jpg" width="300" alt="Character personality and response settings">
  <img src="docs/images/offline-chat.jpg" width="300" alt="Lightweight and higher-quality local models">
</p>

The images show the real app with the included Unity-chan. Captions are Japanese; the app has Japanese and English menus. Layout varies by device and window size.
</details>

## Download

| Your device | Download |
| --- | --- |
| Mac | [macOS app ZIP](https://github.com/Tsubame-chan/YuiVRMAIStudio/releases/download/v0.2.4-beta.1/YuiVRMAIStudio_MacOSPublicBeta_v0.2.4-beta.1_macos.zip) (Apple Silicon runtime) |
| Windows | [Windows app ZIP](https://github.com/Tsubame-chan/YuiVRMAIStudio/releases/download/v0.2.4-beta.1/YuiVRMAIStudio_WindowsPublicBeta_v0.2.4-beta.1_windows.zip) |
| iPhone / iPad | iOS 26+. [Planned App Store page](https://apps.apple.com/jp/app/id6815341780): free Japan release under review, available after publication |

The desktop version is **v0.2.4-beta.1**. Extract the ZIP, open the app and follow the first-run data notice. Allow approximately 2.5GB of downloads plus room for extraction; Wi-Fi is recommended. Mac signing/notarization is not yet provided. Setup: [Mac](docs/MAC_PUBLIC_BETA.en.md) / [Windows](docs/SETUP_GUIDE.md).

**GitHub's “Code → Download ZIP” is source code.** To use the app, choose a download above.

## Get started

1. Send a message to begin. Use Talk for short conversations, or Work for longer advice and tasks.
2. Change personality, voice and avatar in Settings. Your own avatar needs a **VRM file**. See [avatar preparation and import](docs/AVATAR_IMPORT.en.md).
3. Replay the tutorial from Help whenever you need it.

Choose **offline, on-device AI** or **OpenAI API**. The lightweight E2B model is standard; Mac and iOS offer optional E4B for better responses at the cost of longer waits and higher device load. OpenAI API is recommended for higher-quality conversation. **An API key and API charges are required**, separately from a ChatGPT subscription.

Standard speech uses five Japanese VOICEVOX voices. Menus support Japanese and English, but an English speech model is not included. Optional PC Backend features are covered in [Help](docs/HELP.md).

## Memory and privacy

Memories are separate for each character and are not synchronized between devices. Secret mode reads existing memories without saving the new conversation. When you choose an external API, conversation and relevant personality settings/memories are sent to that service.

AI replies and memory retrieval can be wrong. Check important information. [How memory works](docs/CONVERSATION_IDENTITY.md) · [Privacy](docs/PRIVACY.md)

<details>
<summary>Developer documentation</summary>

Unity **2022.3.62f3** / UniVRM **0.131.2**. Model/voice data and platform SDKs/runtimes are separate from source. No public Android app is provided.

- [Source status](docs/SOURCE_STATUS.md) / [Runtime support](docs/RUNTIME_SUPPORT.md)
- [Model and voice data](docs/LOCAL_AI_ASSETS.md) / [Distribution](docs/DISTRIBUTION.md)
- [Quality and asset checks](docs/QUALITY_AND_VALIDATION.md) / [API](docs/api.md)

</details>
