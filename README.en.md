# Yui VRM AI Studio

[日本語README](README.md)

**Turn your favorite VRM character into an AI agent that can talk, see, search, and remember.**

Yui VRM AI Studio turns your own VRM character into a desktop AI avatar that can talk through text, voice, images, and screen context. It is for people who want their VRChat/VRM character, original character, or favorite avatar to become something they can speak with, remember with, search with, and keep around while working or playing.

The project is now a Desktop Public Beta for Windows and macOS. Download the app ZIP, unzip it, and launch it; the required Local Gemma / VOICEVOX data and desktop backend bundle are downloaded on first launch. OpenAI API keys and optional higher-quality voice add-ons can be added later from inside the app.

## What The Experience Is

- Show your own `.vrm` character on screen and talk with that character.
- Use text input, voice input, image input, and selected camera/screen context.
- Keep conversation history and local memory as the app grows toward a persistent AI avatar.
- Try the app after the first launch download, then add BYOK settings or optional voice runtimes for the richer feature set.
- Use VOICEVOX as the standard Japanese voice fallback, with optional AivisSpeech HD and Irodori TTS paths.

## Where To Start

| Target | Status | Start here |
| --- | --- | --- |
| Windows Desktop Public Beta | Public beta | [`docs/SETUP_GUIDE.md`](docs/SETUP_GUIDE.md) |
| macOS Desktop Public Beta | Public beta | [`docs/MAC_PUBLIC_BETA.en.md`](docs/MAC_PUBLIC_BETA.en.md) |
| iOS / Android | Future public candidates | Desktop Beta is the current priority |

Windows and macOS setup docs now follow the same design model. Runnable builds and large Local AI/TTS assets are distributed through [GitHub Releases](https://github.com/Tsubame-chan/YuiVRMAIStudio/releases).

`v0.2.0-beta.3` is the current Desktop Public Beta Release. Normal users should download only the Windows or macOS app ZIP. The app downloads the larger required data on first launch.

## Which Download To Use

Download the app ZIP for your OS from GitHub Releases, unzip it, and launch it.

- macOS: `YuiVRMAIStudio_MacOSPublicBeta_v0.2.0-beta.3_macos.zip`
- Windows: `YuiVRMAIStudio_WindowsPublicBeta_v0.2.0-beta.3_windows.zip`

On first launch, the app shows a download screen. Start it and the required Local AI/TTS data and Yui Backend bundle are installed automatically. Normal users do not need to install Python or set up the backend by hand.

Release assets such as `YuiVRMAIStudio_LocalAIAssets_DesktopMinimum` and `YuiVRMAIStudio_BackendBundle_*` are normally downloaded by the app. Download them manually only for source builds, downloader validation, or manual recovery.

The `.sha256` files are optional integrity checks. `Code > Download ZIP` and `git clone` are source-code paths; they do not include generated app builds or large models.

TTS distribution is intentionally split. The required first-run download stays focused on the minimum VOICEVOX path, while AivisSpeech HD and Irodori TTS are treated as higher-quality optional voice add-ons. Supported add-ons can be installed from Settings through `Additional Voices`. The current Release includes a macOS AivisSpeech HD add-on. It is large because it includes the Aivis runtime, selected AIVMX models, and the Japanese BERT dependency Aivis needs at startup. Windows add-ons and Irodori TTS will use the same flow once their redistributable runtimes are ready.

## What It Does

- Load VRM 1.0 / VRM 0.x `.vrm` avatars and talk with them as AI characters.
- Use text chat, voice input, and Japanese voice responses.
- Use image input / vision and screen context.
- Keep conversation history and local memory.
- Ask current-information questions with web-search assistance for weather, events, news, places, and similar queries.
- Try low-latency conversation experiments through the OpenAI Realtime API.
- Use Realtime VOICEVOX mode with OpenAI Realtime STT and VOICEVOX TTS.
- Use Auto Select to prefer backend capabilities and fall back to local runtime when needed.
- Use Local Gemma SLM for offline or weak-network lightweight chat.
- See and choose between local VOICEVOX, backend VOICEVOX, AivisSpeech HD, and Irodori TTS capability states.
- Use realtime translation mode.

## How It Works, Briefly

The app UI runs in Unity. AI provider calls, the conversation database, speech generation, web search, and image processing are handled by either the downloaded local backend on the same machine or by the app's embedded local runtime.

When the backend is running, Yui can use higher-quality conversation paths, realtime talk/translation, memory DB, web search, and backend TTS providers. Without the backend, the app remains usable through Direct API mode, Local Gemma, and local VOICEVOX fallback. Connection details and ports are configurable, so this README does not assume any developer-specific URL or private IP address.

## Technical Notes: Provider Status

### Main providers

- OpenAI: chat / STT / vision / realtime / translation / hosted web search
- VOICEVOX Engine: Japanese TTS runtime
- Local Gemma SLM: offline-first local chat fallback
- Local VOICEVOX: built-in/native Japanese TTS fallback where supported

### Implemented, not fully verified

- Generic HTTP TTS adapter for experimental JSON-in/audio-out TTS services such as Irodori TTS
- Open-Meteo current weather API as an experimental structured-information path separate from web search
- LM Studio local chat provider through an experimental OpenAI-compatible `/chat/completions` connection
- Grok / xAI chat provider through xAI's OpenAI-compatible `/chat/completions` endpoint
- Shared capability diagnostics for Settings and Help, so backend/local/direct availability is labeled through one policy

### Beta Confidence Notes

- `Auto Select` is the recommended first choice. It prefers the backend when healthy and falls back to local/direct paths when needed.
- Release app ZIPs are expected to trigger the first-run downloader when required local/backend data is missing.
- Provider/model availability can change on the external service side. Check Settings and Help connection status when something looks unavailable.

### Candidates

- OpenAI-compatible local LLM providers such as LM Studio and Ollama
- provider selection UI
- broader OS-native STT/TTS support and quality checks
- dedicated map, calendar, or other structured APIs

## Requirements

Minimum:

- The Windows or macOS Beta release files from GitHub Releases
- A VRM file if you want to use your own `.vrm` avatar

Optional:

- An OpenAI API key for Direct API, higher-quality vision, or STT paths
- The downloaded local backend bundle for realtime talk/translation, memory DB, web search, and backend TTS
- VOICEVOX Engine, AivisSpeech HD, Irodori TTS, or another supported runtime when extending Japanese voice playback

Platform details:

- Windows: [`docs/SETUP_GUIDE.md`](docs/SETUP_GUIDE.md)
- macOS: [`docs/MAC_PUBLIC_BETA.en.md`](docs/MAC_PUBLIC_BETA.en.md)

### Git Clone vs Release Artifacts

The git repository does not commit large Gemma model files, voice models,
voice dictionaries, backend bundles, or generated app builds. Those belong in
GitHub Release Beta artifacts because of size and license boundaries. Release
app artifacts are meant to download the required local/backend data on first
launch. Optional voices and source-build assets are only needed when you choose
those paths. See
[`docs/LOCAL_AI_ASSETS.md`](docs/LOCAL_AI_ASSETS.md).

### TTS / Irodori Validation

VOICEVOX is the standard Japanese TTS fallback. On desktop, local VOICEVOX is the default no-backend path, while backend VOICEVOX, AivisSpeech HD, and Irodori TTS provide richer options when the backend is configured. Irodori TTS is still under validation, and the candidate runtime differs by OS.

- macOS Apple Silicon: MLX VoiceDesign path in [`docs/IRODORI_TTS_PACKAGING.md`](docs/IRODORI_TTS_PACKAGING.md)
- Windows NVIDIA: Irodori-TTS-Server path in [`docs/IRODORI_TTS_WINDOWS_NVIDIA.md`](docs/IRODORI_TTS_WINDOWS_NVIDIA.md)
- Windows CPU / no GPU: VOICEVOX is recommended

Large model files and TTS server runtimes are not committed to git because of size and license boundaries. They are either installed by users or distributed separately through GitHub Release assets when appropriate. If Irodori fails, `TTS_FALLBACK_PROVIDER=voicevox` can return speech generation to VOICEVOX.

## Use Your Own VRM Character

This beta imports `.vrm` files only.
It cannot directly load a VRChat SDK avatar, Unity scene, Unity prefab, `.unitypackage`, or an avatar that only exists as an uploaded VRChat avatar.

If your avatar is managed in a VRChat Unity project, check whether the original BOOTH/distribution package includes a `.vrm` file.
If not, export or convert a separate VRM copy through a Unity/UniVRM or Blender/VRM workflow first.

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

### Desktop Public Beta

- Unify Windows / macOS Desktop Public Beta behavior
- OpenAI chat / STT / vision / web search
- VOICEVOX TTS
- Local Gemma SLM and Direct API fallback
- Auto Select with backend preference and local fallback
- Backend VOICEVOX / AivisSpeech HD / Irodori TTS selection
- conversation history and memory
- image and screen context
- realtime experimental modes

### Next

- improve Windows / macOS Release distribution flow
- move the in-app optional voice download flow closer to voice selection
- verify the first-run downloader on real desktop installs and extend it toward update checks
- continue viewer-mode and desktop usability improvements
- test Irodori TTS and other services through the generic HTTP TTS adapter
- provider selection UI
- real-key verification for the Grok / xAI API provider
- device verification for OpenAI-compatible local LLM providers, starting with LM Studio
- chat integration for the dedicated weather API, plus map, calendar, or other structured APIs

### Future

- public iOS / Android evaluation
- external app audio bridge
- realtime translation for YouTube / games / streams / calls
- physical AI / external device integration

## Docs

- Windows setup: [`docs/SETUP_GUIDE.md`](docs/SETUP_GUIDE.md)
- macOS Public Beta: [`docs/MAC_PUBLIC_BETA.en.md`](docs/MAC_PUBLIC_BETA.en.md)
- Irodori Windows NVIDIA validation: [`docs/IRODORI_TTS_WINDOWS_NVIDIA.md`](docs/IRODORI_TTS_WINDOWS_NVIDIA.md)
- Irodori optional backend packaging: [`docs/IRODORI_TTS_PACKAGING.md`](docs/IRODORI_TTS_PACKAGING.md)
- API details: [`docs/api.md`](docs/api.md)
- external information / web search policy: [`docs/LLM_EXTERNAL_INFO.md`](docs/LLM_EXTERNAL_INFO.md)
- local AI/TTS asset distribution: [`docs/LOCAL_AI_ASSETS.md`](docs/LOCAL_AI_ASSETS.md)
- quality and validation policy: [`docs/QUALITY_AND_VALIDATION.md`](docs/QUALITY_AND_VALIDATION.md)

## Troubleshooting

For first-run problems, start with the setup guide for your platform.

- Windows: [`docs/SETUP_GUIDE.md#troubleshooting`](docs/SETUP_GUIDE.md#troubleshooting)
- macOS: [`docs/MAC_PUBLIC_BETA.en.md`](docs/MAC_PUBLIC_BETA.en.md)

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
- VOICEVOX-related runtimes and voice assets follow the upstream VOICEVOX terms.
- If you publish generated speech, include the required VOICEVOX credit for the selected voice. The default beta voice is `VOICEVOX:冥鳴ひまり`.
- ChatdollKit, lilToon, UniVRM, and other Unity packages remain under their respective licenses.
