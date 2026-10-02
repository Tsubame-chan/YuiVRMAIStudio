# Yui VRM AI Studio

[日本語](README.md) · [Help & FAQ](docs/HELP.md) · [Report an issue](https://github.com/Tsubame-chan/YuiVRMAIStudio/issues)

**AI chat with your own VRM avatar.**

Bring your favorite avatar into everyday conversation, advice and small tasks. Describe its personality and speaking style in your own words, or take a break and view it from any angle.

<p>
  <img src="docs/images/avatar-chat.jpg" width="300" alt="Unity-chan speaking during an actual AI conversation">
  <img src="docs/images/avatar-viewer.jpg" width="300" alt="Rotate and zoom your avatar in the viewer">
</p>

These promotional images use the included Unity-chan in the real Mac application. Their captions are Japanese; the app has Japanese and English UI. Layout varies with the device and window size.

## Download

[Planned App Store page](https://apps.apple.com/jp/app/id6815341780) — available after approval/publication.

| Platform | Availability |
| --- | --- |
| macOS / Windows | [v0.2.4-beta.1](https://github.com/Tsubame-chan/YuiVRMAIStudio/releases/tag/v0.2.4-beta.1) |
| iPhone / iPad | 0.2.4 (14) submitted to App Review. Free launch in Japan after approval; not publicly available yet |
| Android | Development source; distribution and device acceptance remain incomplete |

Download and extract the application ZIP for your desktop OS. On first launch, confirm the download of AI, voice and runtime data. Use Wi-Fi and allow enough free storage. Once prepared, on-device chat works offline.

**`Code → Download ZIP` is source for developers. It does not include built applications or large models.**

Setup: [macOS](docs/MAC_PUBLIC_BETA.en.md) / [Windows](docs/SETUP_GUIDE.md). The Mac runtime targets Apple Silicon; code signing and notarization are not yet provided. Windows is distributed as a beta.

## Make it your companion

- **Bring your avatar.** Import VRM 0.x / 1.0, manage characters and outfits, and display blinking, lip sync and supported secondary motion.
- **Shape its personality and replies.** Combine character personality, shared AI instructions and per-model instructions. Local inference settings include context, output/thinking budgets and temperature, saved separately for Talk and Work.
- **Keep a shared history with each character.** Character-scoped memories persist on the device across restarts and switches between local AI and API chat. Inspect, edit or delete them; other characters do not share them.
- **Choose the task.** Talk favors short conversations; Work provides longer explanations and task assistance. Send text, microphone input and images, save replies or read them aloud.
- **Enjoy a digital figure.** Rotate and zoom in the viewer. A four-page tutorial introduces the basics and can be reopened from Help.

<details>
<summary>Show personality, model settings and offline chat</summary>

<p>
  <img src="docs/images/customization.jpg" width="300" alt="Character personality and local inference settings">
  <img src="docs/images/offline-chat.jpg" width="300" alt="Standard 2B and optional 4B model selection">
</p>

</details>

## Choose AI and voice separately

| Conversation route | What to expect |
| --- | --- |
| On-device AI | Standard Gemma 4 E2B. macOS and iOS also offer optional E4B in Settings: higher quality, with more storage, memory use and latency |
| OpenAI API | Recommended for higher-quality conversation. Requires an API key, connectivity and API usage charges, separate from a ChatGPT subscription |
| PC Backend | Optional extensions using configured TTS/STT, search and other services. Management remains CLI-based and some features are experimental |

On-device VOICEVOX supplies five standard Japanese voices. English UI is available, but a dedicated English TTS model is not included yet. A configured Backend can provide additional voice engines.

The app's OpenAI key is for direct access and is separate from the Backend's `.env`; it is not forwarded to the Backend. Desktop setup downloads a runtime bundle containing a local inference worker. On-device AI does not require the Backend server to be running.

## Import your avatar

Use Settings → Character → Import avatar. My characters → Change outfit changes appearance while retaining character identity.

Export Unity/VRChat avatars as VRM from their configured Unity project. Purchased ZIPs, `.unitypackage` files and FBX cannot be loaded directly. Custom shaders, clothing menus and PhysBone behavior are not reproduced completely. Use models you have permission to use. See the [avatar guide](docs/AVATAR_IMPORT.md).

## Memory and privacy

Secret Mode can read the selected character's existing memories, but does not save new conversation history or memories. Its private conversation is not carried forward after leaving Secret Mode. Memories are not synchronized between devices.

External AI receives conversation content and relevant personality settings and memories. Secret Mode does not prevent those requests. Apple builds store API keys in Keychain. See [privacy](docs/PRIVACY.md).

**Replies and memory retrieval can be wrong.** Local models still make speaker-attribution, knowledge and arithmetic mistakes. A larger model or different settings cannot guarantee correctness. Verify important information.

## Development and specifications

Unity **2022.3.62f3** / UniVRM **0.131.2**. Unity 6 migration has not been performed. Unity-chan is the public default avatar; personal avatars, keys and conversations are not distributed. Source builds need model data, platform SDKs, native libraries and signing configuration separately.

- [Source and validation status](docs/SOURCE_STATUS.md)
- [Platform support and limits](docs/RUNTIME_SUPPORT.md)
- [AI and voice data](docs/LOCAL_AI_ASSETS.md)
- [Conversation, character and memory identity](docs/CONVERSATION_IDENTITY.md)
- [Quality and public asset checks](docs/QUALITY_AND_VALIDATION.md)
- [Distribution architecture](docs/DISTRIBUTION.md)
- [API](docs/api.md)

Verification differs between platforms and devices. The linked documents distinguish implemented source, shipped binaries and actual device acceptance.
