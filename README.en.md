# Yui VRM AI Studio

[日本語README](README.md)

**Turn your favorite VRM character into an AI agent that can talk, see, remember, and translate.**

Yui VRM AI Studio is a local AI avatar studio for Windows. It pairs a Unity VRM avatar app with a local Python backend so you can connect a character to chat, voice input, Japanese VOICEVOX speech, image input, screen context, memory, and realtime conversation modes.

This alpha is BYOK: you run the backend locally and provide your own OpenAI API key.

Voice output currently focuses on Japanese speech because it uses local VOICEVOX Engine. The UI and text chat can still be used in English.

## In 30 Seconds

- Load a favorite VRM character and talk with it as an AI agent.
- Use text, voice, images, and screen context in conversation.
- Play Japanese character speech through VOICEVOX Engine.
- Bring your own OpenAI API key.
- This is still an alpha, so setup and some features are experimental.

## Available Now

- UnityChan Default avatar
- VRM 1.0 and VRM 0.x import
- Text chat
- Character name, personality, tone, and prompt customization
- Voice input through OpenAI transcription
- Japanese speech playback through local VOICEVOX Engine
- Image input and screen context
- Conversation history and memory
- Local BYOK backend configured through `.env`

## Experimental Features

- Low-latency voice conversation via OpenAI Realtime API
- Japanese character voice mode using OpenAI Realtime text output and VOICEVOX
- Realtime translation mode

## Provider Status

### Main providers in this alpha

- OpenAI: chat / STT / vision / realtime / translation
- VOICEVOX Engine: local Japanese TTS

### Implemented, not fully verified

- Gemini Vision provider is implemented in the backend, but not fully verified in this alpha.

### Planned

- Grok / xAI API
- Ollama / LM Studio
- provider selection UI

## Current Alpha

- Version: `0.1.0-alpha.1`
- Platform: Windows 10/11
- Bundled avatar: UnityChan Default
- Custom avatar support: local VRM 1.0 and VRM 0.x `.vrm` import
- Speech: local VOICEVOX Engine, installed separately
- Backend: FastAPI on `127.0.0.1:8000`

Current Windows app build:

```text
builds/YuiVRMAIStudio_PublicAlpha_v0.1.0-alpha.1/Yui VRM AI Studio.exe
```

Keep this helper next to the app exe so Windows standalone file selection works:

```text
builds/YuiVRMAIStudio_PublicAlpha_v0.1.0-alpha.1/YuiFilePickerHelper.exe
```

## Requirements

- Windows 10 or Windows 11
- Python 3.12+ from https://www.python.org/downloads/windows/
- An OpenAI API key from https://platform.openai.com/api-keys
- VOICEVOX Engine, or the `vv-engine\run.exe` bundled with VOICEVOX:
  - https://voicevox.hiroshiba.jp/
  - Standalone Engine releases: https://github.com/VOICEVOX/voicevox_engine/releases
- PowerShell

Normal alpha use is usually light on API usage if experimental realtime modes are left off. If you only want to try the main features, 5 USD of API credit is usually enough to play with chat, voice input, image input, and translation. Longer realtime sessions, image-heavy use, or repeated audio tests can increase cost, so keep an eye on your OpenAI usage page.

## Quick Start

1. Download this repository from GitHub:
   - Click `Code` -> `Download ZIP`, then extract it.
   - Or run `git clone <repository-url>` if you use Git.
2. Put the extracted folder somewhere simple, such as `C:\YuiVRMAIStudio`.
3. Install Python 3.12+. In the installer, enable `Add python.exe to PATH`.
4. Prepare VOICEVOX Engine.
   - Installing the normal VOICEVOX app is also OK if it includes `vv-engine\run.exe`.
   - Advanced users can download the standalone Windows VOICEVOX Engine release.
5. From this repository folder, run:

```powershell
.\scripts\setup_backend_byok.ps1
```

If PowerShell says scripts are disabled, run this once:

```powershell
Set-ExecutionPolicy RemoteSigned -Scope CurrentUser
```

6. Edit `.env` and set:

```text
OPENAI_API_KEY=your_api_key_here
```

7. Double-click:

```text
Start_Yui_Backend_And_VOICEVOX.bat
```

This starts both the local backend and VOICEVOX Engine. It does not automate the VOICEVOX GUI; it launches `vv-engine\run.exe` directly and uses it as a local API server at `http://127.0.0.1:50021`. Keep the launcher window open while using the app.

Press Enter in that window when finished; it normally stops both services.

`Stop_Yui_Backend_And_VOICEVOX.bat` is a force-stop helper for cases where the launcher window was closed by mistake or a process remains stuck.

8. Launch:

```text
builds\YuiVRMAIStudio_PublicAlpha_v0.1.0-alpha.1\Yui VRM AI Studio.exe
```

Windows may show SmartScreen for the unsigned alpha exe. Click `More info`, then `Run anyway` if you trust this local build. Windows may also show a UAC prompt when VOICEVOX starts.

## Use Your Own VRM Character

This alpha imports `.vrm` files only. It cannot directly load a VRChat SDK avatar, Unity scene, Unity prefab, `.unitypackage`, or an avatar that only exists as an uploaded VRChat avatar. If your avatar is managed as a VRChat Unity project, export or convert a separate VRM copy first.

1. Start `Start_Yui_Backend_And_VOICEVOX.bat`.
2. Launch `Yui VRM AI Studio.exe`.
3. Open Settings.
4. Click the `Custom VRM` import button.
5. Select your `.vrm` file.

The app loads VRM 1.0 and VRM 0.x files through UniVRM. After a successful import, it immediately switches the active avatar to `Custom VRM`, saves the file path locally, and tries to restore it on the next launch. Very custom materials, expressions, or rigs may need follow-up tuning.

## Privacy / Data Flow

Yui VRM AI Studio is BYOK. Your API key is stored locally in your `.env` file.

Depending on enabled features, the following data may be sent to configured external AI providers:

- chat messages
- voice input
- uploaded images
- screenshots / screen context
- translation audio or text

The following data is stored locally:

- `.env`
- SQLite conversation database
- generated VOICEVOX audio cache
- logs

Be careful with sensitive screen or audio content when using screen context or realtime translation.

## Roadmap

### Public Alpha 0.1

- VRM display and control
- local FastAPI backend
- OpenAI chat / STT / vision
- VOICEVOX TTS
- conversation history and memory
- image and screen context
- realtime experimental modes

### Next

- external app audio bridge
- realtime translation for YouTube / games / streams / calls
- provider selection UI
- Gemini Vision provider validation
- Grok / xAI API provider
- Ollama / LM Studio local LLM provider

### Future

- mobile app development
- physical AI / external device integration
- richer Unity scenes, animations, and interactions

## Relationship To Yui Physical AI

This repository started as the first Unity core runtime for the broader Yui Physical AI concept.

The current alpha focuses on running a VRM character as an AI agent on Windows. The long-term direction is desktop -> external app integration -> mobile app development -> physical AI / external device interfaces.

## Docs

Full setup notes are in `docs/SETUP_GUIDE.md`. API details are in `docs/api.md`. Release readiness checks are in `docs/ALPHA_RELEASE_CHECKLIST.md`.

## Troubleshooting

Check whether the backend is listening:

```powershell
.\scripts\check_backend.ps1
```

Useful local URLs while the backend is running:

- http://127.0.0.1:8000/health
- http://127.0.0.1:8000/config
- http://127.0.0.1:8000/usage
- http://127.0.0.1:8000/docs

Common first-run issues:

- Backend does not start: run `.\scripts\setup_backend_byok.ps1` again.
- VOICEVOX is not found: install the normal VOICEVOX app or standalone VOICEVOX Engine, then make sure `vv-engine\run.exe` exists. If needed, set `VOICEVOX_ENGINE_EXE` to the full path of `vv-engine\run.exe`.
- Chat does not respond: confirm `OPENAI_API_KEY` is set in `.env`.
- File picker does not open: keep `YuiFilePickerHelper.exe` beside the app exe.
- Cannot stop services normally: press Enter in the launcher window first. Use `Stop_Yui_Backend_And_VOICEVOX.bat` only as a force-stop helper.

## License And Credits

Project code is released under the MIT License. See `LICENSE`.

Third-party assets and libraries keep their own licenses. In particular:

- UnityChan assets are distributed under the Unity-Chan License Terms.
- VOICEVOX/VOICEVOX Engine is not bundled. Install it separately and follow the VOICEVOX terms.
- If you publish generated speech, include the required VOICEVOX credit for the selected voice. The default alpha voice is VOICEVOX:冥鳴ひまり.
- ChatdollKit, lilToon, UniVRM, and other Unity packages remain under their respective licenses.
