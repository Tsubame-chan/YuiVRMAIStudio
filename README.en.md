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
| Mac | [macOS app ZIP](https://github.com/Tsubame-chan/YuiVRMAIStudio/releases/download/v0.2.5-beta.1/YuiVRMAIStudio_MacOSPublicBeta_v0.2.5-beta.1_macos.zip) (Apple Silicon runtime) |
| Windows | [Windows app ZIP](https://github.com/Tsubame-chan/YuiVRMAIStudio/releases/download/v0.2.5-beta.1/YuiVRMAIStudio_WindowsPublicBeta_v0.2.5-beta.1_windows.zip) |
| iPhone / iPad | iOS 26+. [App Store](https://apps.apple.com/jp/app/id6815341780): free Japan release |

The desktop version is **v0.2.5-beta.1**. It includes the Backend Console, saved voices and paired character sync. See [Backend setup](docs/BACKEND_CONSOLE.md). Extract the ZIP, open the app and follow the first-run data notice. Allow approximately 2.5GB of downloads plus room for extraction; Wi-Fi is recommended. Mac signing/notarization is not yet provided. Setup: [Mac](docs/MAC_PUBLIC_BETA.en.md) / [Windows](docs/SETUP_GUIDE.md).

**GitHub's “Code → Download ZIP” is source code.** To use the app, choose a download above.

## Get started

1. Send a message to begin. Use Talk for short conversations, or Work for longer advice and tasks.
2. Change personality, voice and avatar in Settings. Your own avatar needs a **VRM file**. See [avatar preparation and import](docs/AVATAR_IMPORT.en.md).
3. Replay the tutorial from Help whenever you need it.

Choose **offline, on-device AI** or **OpenAI API**. The lightweight E2B model is standard; Mac and iOS offer optional E4B for better responses at the cost of longer waits and higher device load. OpenAI API is recommended for higher-quality conversation. **An API key and API charges are required**, separately from a ChatGPT subscription.

Standard speech uses five Japanese VOICEVOX voices. The latest source adds optional downloads in Mac/iOS settings: three Japanese Irodori voices (about 1.96GB) and six English Kokoro voices (about 67.4MB). These additional voices are not included in the existing 0.2.4 or desktop beta.2 apps. See [Voice downloads](docs/LOCAL_AI_ASSETS.md), [Help](docs/HELP.md), [Backend Console](docs/BACKEND_CONSOLE.md) and the [Backend TTS installation guide (Japanese)](docs/BACKEND_TTS_GUIDE.md).

## Changes in 0.2.5

- Optional downloads for three Japanese Irodori voices and six English Kokoro voices. Irodori starts with Bright and cheerful.
- Consistently thin scrollbars and a single scrolling area in model details.
- Irodori V4.1 and the same three voice presets in the Mac Backend.

The desktop release includes these updates. iOS 0.2.5 has been uploaded to Apple; the currently available App Store version is 0.2.4. Native Windows acceptance remains pending. See the [optional Irodori V4.1 NVIDIA package](docs/IRODORI_TTS_WINDOWS_NVIDIA.md).

## Memory and privacy

Memories are separate for each character. Sync-capable apps can share personality, memories and history through a Backend after pairing and confirming the changes. See [device compatibility](docs/RUNTIME_SUPPORT.md) for supported versions and setup. Secret mode reads existing memories without saving the new conversation. When you choose an external API, conversation and relevant personality settings/memories are sent to that service.

AI replies and memory retrieval can be wrong. Check important information. [How memory works](docs/CONVERSATION_IDENTITY.md) · [Privacy](docs/PRIVACY.md)


[Device compatibility](docs/RUNTIME_SUPPORT.md) · [AI and voice downloads](docs/LOCAL_AI_ASSETS.md)
