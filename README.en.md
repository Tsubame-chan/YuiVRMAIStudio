# Yui VRM AI Studio

[日本語README](README.md)

**Turn your favorite VRM character into an AI agent that can talk, see, search, and remember.**

Yui VRM AI Studio is a BYOK AI avatar studio that combines a Unity VRM avatar app with a local AI helper service. You use your own API key, and conversation data and settings are primarily managed on your own PC or Mac.

The project currently documents Desktop Public Alpha paths for Windows and macOS. The iOS build is a personal device-testing alpha, not a public distribution target.

## Where To Start

| Target | Status | Start here |
| --- | --- | --- |
| Windows Desktop Public Alpha | Public BYOK alpha | [`docs/SETUP_GUIDE.md`](docs/SETUP_GUIDE.md) |
| macOS Desktop Public Alpha | Experimental public alpha | [`macos-public-alpha` branch](https://github.com/Tsubame-chan/YuiVRMAIStudio/tree/macos-public-alpha) / [`docs/MAC_PUBLIC_ALPHA.en.md`](docs/MAC_PUBLIC_ALPHA.en.md) |
| iOS Personal Alpha | Personal device testing | Not a public distribution target. Policy: [`docs/BUILD_VARIANTS.md`](docs/BUILD_VARIANTS.md) |
| Android | Not implemented yet | Future candidate |

The Windows setup and current Windows artifact live on this main branch. The macOS alpha is still experimental, so it is maintained on a dedicated branch.

## What It Does

- Load VRM 1.0 / VRM 0.x `.vrm` avatars and talk with them as AI characters.
- Use text chat, voice input, and Japanese voice responses.
- Use image input / vision and screen context.
- Keep conversation history and local memory.
- Ask current-information questions with web-search assistance for weather, events, news, places, and similar queries.
- Try low-latency conversation experiments through the OpenAI Realtime API.
- Use Realtime VOICEVOX mode with OpenAI Realtime STT and VOICEVOX TTS.
- Use realtime translation mode.

## How It Works

The app UI runs in Unity. AI provider calls, the conversation database, speech generation, and image processing are handled by a local helper service running on the same machine.

For normal use, follow the platform-specific setup guide and start the local services through the provided launcher scripts. Connection details and ports are configurable, so this README does not assume any developer-specific URL or private IP address.

## Provider Status

### Main providers

- OpenAI: chat / STT / vision / realtime / translation / hosted web search
- VOICEVOX Engine: Japanese TTS runtime

### Implemented, not fully verified

- Generic HTTP TTS adapter for experimental JSON-in/audio-out TTS services such as Irodori TTS
- Open-Meteo current weather API as an experimental structured-information path separate from web search
- LM Studio local chat provider through an experimental OpenAI-compatible `/chat/completions` connection
- Grok / xAI chat provider through xAI's OpenAI-compatible `/chat/completions` endpoint

### Candidates

- Ollama local LLM
- provider selection UI
- OS-native TTS, model-native TTS, and lightweight mobile TTS paths
- dedicated map, calendar, or other structured APIs

## Requirements

Common:

- An OpenAI API key
- A VRM file if you want to use your own `.vrm` avatar
- VOICEVOX or VOICEVOX Engine if you want Japanese voice playback

Platform details:

- Windows: [`docs/SETUP_GUIDE.md`](docs/SETUP_GUIDE.md)
- macOS: [`docs/MAC_PUBLIC_ALPHA.en.md`](docs/MAC_PUBLIC_ALPHA.en.md)

### TTS / Irodori Validation

VOICEVOX is the standard Japanese TTS path. Irodori TTS is still under validation, and the candidate runtime differs by OS.

- macOS Apple Silicon: MLX VoiceDesign path in [`docs/IRODORI_TTS_PACKAGING.md`](docs/IRODORI_TTS_PACKAGING.md)
- Windows NVIDIA: Irodori-TTS-Server path in [`docs/IRODORI_TTS_WINDOWS_NVIDIA.md`](docs/IRODORI_TTS_WINDOWS_NVIDIA.md)
- Windows CPU / no GPU: VOICEVOX is recommended

This repository does not bundle Irodori model files or TTS server runtimes. It only keeps setup notes and configuration examples; users install the required TTS runtime in their own environment. If Irodori fails, `TTS_FALLBACK_PROVIDER=voicevox` can return speech generation to VOICEVOX.

## Use Your Own VRM Character

This alpha imports `.vrm` files only.
It cannot directly load a VRChat SDK avatar, Unity scene, Unity prefab, `.unitypackage`, or an avatar that only exists as an uploaded VRChat avatar.

If your avatar is managed in a VRChat Unity project, check whether the original BOOTH/distribution package includes a `.vrm` file.
If not, export or convert a separate VRM copy through a Unity/UniVRM or Blender/VRM workflow first.

## Personal / Public / Platform Boundaries

This project separates public builds from personal builds.

- Public: external users, BYOK, public-safe avatars, public-safe defaults.
- Personal: owner devices, private avatars, personal bundle IDs, and owner-specific defaults when needed.

The policy lives in [`docs/BUILD_VARIANTS.md`](docs/BUILD_VARIANTS.md). Public builds should not contain personal avatars, owner-specific settings, personal bundle IDs, or secrets.

## Privacy / Data Flow

Yui VRM AI Studio is BYOK. Your API key is stored locally in your `.env` file.

Depending on enabled features, the following data may be sent to configured external AI providers:

- chat messages
- voice input
- uploaded images
- screenshots / screen context
- translation audio or text
- prompts that need web search

The following data is stored locally:

- `.env`
- SQLite conversation database
- generated VOICEVOX audio cache
- logs

Be careful with sensitive screen or audio content when using screen context or realtime translation.

## Roadmap

### Public Alpha 0.1

- Stabilize Windows Desktop Public Alpha
- Maintain macOS Desktop Public Alpha branch
- OpenAI chat / STT / vision / web search
- VOICEVOX TTS
- conversation history and memory
- image and screen context
- realtime experimental modes

### Next

- improve macOS distribution flow
- continue viewer-mode control validation on Windows / macOS / iOS
- test Irodori TTS and other services through the generic HTTP TTS adapter
- provider selection UI
- real-key verification for the Grok / xAI API provider
- device verification for the LM Studio provider, plus an Ollama local LLM provider
- chat integration for the dedicated weather API, plus map, calendar, or other structured APIs

### Future

- public iOS / Android evaluation
- external app audio bridge
- realtime translation for YouTube / games / streams / calls
- physical AI / external device integration

## Docs

- Windows setup: [`docs/SETUP_GUIDE.md`](docs/SETUP_GUIDE.md)
- macOS Public Alpha: [`docs/MAC_PUBLIC_ALPHA.en.md`](docs/MAC_PUBLIC_ALPHA.en.md)
- Irodori Windows NVIDIA validation: [`docs/IRODORI_TTS_WINDOWS_NVIDIA.md`](docs/IRODORI_TTS_WINDOWS_NVIDIA.md)
- Irodori optional backend packaging: [`docs/IRODORI_TTS_PACKAGING.md`](docs/IRODORI_TTS_PACKAGING.md)
- TTS benchmark notes: [`docs/IRODORI_TTS_BENCHMARK_20260626.md`](docs/IRODORI_TTS_BENCHMARK_20260626.md)
- API details: [`docs/api.md`](docs/api.md)
- external information / web search policy: [`docs/LLM_EXTERNAL_INFO.md`](docs/LLM_EXTERNAL_INFO.md)
- build variants: [`docs/BUILD_VARIANTS.md`](docs/BUILD_VARIANTS.md)
- release checklist: [`docs/ALPHA_RELEASE_CHECKLIST.md`](docs/ALPHA_RELEASE_CHECKLIST.md)

## Troubleshooting

For first-run problems, start with the setup guide for your platform.

- Windows: [`docs/SETUP_GUIDE.md#troubleshooting`](docs/SETUP_GUIDE.md#troubleshooting)
- macOS: [`docs/MAC_PUBLIC_ALPHA.en.md`](docs/MAC_PUBLIC_ALPHA.en.md)

Common causes:

- the local helper service is not running
- `.env` does not contain `OPENAI_API_KEY`
- VOICEVOX Engine is missing or not running
- on Windows, `YuiFilePickerHelper.exe` is not next to the app executable

Developer API details and deeper diagnostics are documented in [`docs/api.md`](docs/api.md).

## License And Credits

Project code is released under the MIT License. See [`LICENSE`](LICENSE).

Third-party assets and libraries keep their own licenses.

- UnityChan assets are distributed under the Unity-Chan License Terms.
- VOICEVOX/VOICEVOX Engine is not bundled. Install it separately and follow the VOICEVOX terms.
- If you publish generated speech, include the required VOICEVOX credit for the selected voice. The default alpha voice is `VOICEVOX:冥鳴ひまり`.
- ChatdollKit, lilToon, UniVRM, and other Unity packages remain under their respective licenses.
