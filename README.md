# Yui VRM AI Studio

Yui VRM AI Studio is a Windows-first local AI avatar studio. It pairs a Unity
avatar viewer with a local Python backend so you can chat, speak, analyze
images, and experiment with a bundled UnityChan avatar or your own VRM model.

This alpha is BYOK: you run the backend locally and provide your own API key.

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
- VOICEVOX Engine from https://voicevox.hiroshiba.jp/
- PowerShell

The default model names in `.env.example` are starting points for this alpha.
If a model is unavailable for your account or region, check the current OpenAI
documentation and replace the model name in `.env`.

Normal alpha use is usually light on API usage if experimental realtime modes
are left off. Longer realtime sessions, image-heavy use, or repeated audio
tests can increase cost, so keep an eye on your OpenAI usage page.

## Quick Start

1. Download this repository from GitHub:
   - Click `Code` -> `Download ZIP`, then extract it.
   - Or run `git clone <repository-url>` if you use Git.
2. Put the extracted folder somewhere simple, such as `C:\YuiVRMAIStudio`.
3. Install Python 3.12+. In the installer, enable `Add python.exe to PATH`.
4. Install VOICEVOX.
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

8. Launch:

```text
builds\YuiVRMAIStudio_PublicAlpha_v0.1.0-alpha.1\Yui VRM AI Studio.exe
```

Keep the launcher window open while using the app. Press Enter in that window
when finished; it stops both the backend and VOICEVOX Engine. If the launcher
window was closed by mistake, double-click `Stop_Yui_Backend_And_VOICEVOX.bat`.

Windows may show SmartScreen for the unsigned alpha exe. Click `More info`,
then `Run anyway` if you trust this local build. Windows may also show a UAC
prompt when VOICEVOX starts. Allow it if needed; on some Windows configurations
the local VOICEVOX Engine needs elevated access to its user dictionary folder.

## Use Your Own VRM

This alpha imports `.vrm` files only. It cannot directly load a VRChat SDK
avatar, Unity scene, Unity prefab, `.unitypackage`, or an avatar that only
exists as an uploaded VRChat avatar. If your avatar is managed as a VRChat
Unity project, export or convert a separate VRM copy first.

1. Start the backend and VOICEVOX.
2. Launch `Yui VRM AI Studio.exe`.
3. Open Settings.
4. Click the `Custom VRM` import button.
5. Select your `.vrm` file.

The app loads VRM 1.0 and VRM 0.x files through UniVRM. After a successful
import, it immediately switches the active avatar to `Custom VRM`, saves the
file path locally, and tries to restore it on the next launch. Very custom
materials, expressions, or rigs may need follow-up tuning.

The public build should show only `UnityChan Default` and `Custom VRM` unless
you add your own local avatars.

## Features

- Text chat with a local FastAPI backend.
- Voice input through OpenAI transcription.
- Japanese speech playback through local VOICEVOX Engine.
- Image import and screen-look experiments for vision-enabled chat.
- Realtime experimental modes in the Unity UI.
- Character name customization.
- UnityChan default avatar plus local custom VRM import.

Full setup notes are in `docs/SETUP_GUIDE.md`. API details are in
`docs/api.md`. Release readiness checks are in
`docs/ALPHA_RELEASE_CHECKLIST.md`.

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
- VOICEVOX is not found: install VOICEVOX in the default location or set
  `VOICEVOX_ENGINE_EXE` to the full path of `vv-engine\run.exe`.
- Chat does not respond: confirm `OPENAI_API_KEY` is set in `.env`.
- File picker does not open: keep `YuiFilePickerHelper.exe` beside the app exe.

## License And Credits

Project code is released under the MIT License. See `LICENSE`.

Third-party assets and libraries keep their own licenses. In particular:

- UnityChan assets are distributed under the Unity-Chan License Terms.
- VOICEVOX is not bundled. Install it separately and follow the VOICEVOX terms.
- If you publish generated speech, include the required VOICEVOX credit for the
  selected voice. The default alpha voice is VOICEVOX:冥鳴ひまり.
- ChatdollKit, lilToon, UniVRM, and other Unity packages remain under their
  respective licenses.


