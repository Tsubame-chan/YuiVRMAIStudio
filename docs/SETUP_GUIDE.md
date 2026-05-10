# Setup Guide

This guide is for the public BYOK Windows alpha. BYOK means you provide your
own API key and run the backend locally on your PC.

Most first-run problems are setup-related rather than app bugs: Python PATH,
VOICEVOX location, `.env`, or Windows SmartScreen. Follow the steps in order
once, then use Troubleshooting only if something stops.

## 1. Download The Project

From GitHub, use either:

- `Code` -> `Download ZIP`, then extract the ZIP.
- `git clone <repository-url>` if Git is installed.

Use a simple folder path if possible, for example:

```text
C:\YuiVRMAIStudio
```

The app can work from folders with spaces, but simple paths make Windows and
PowerShell setup easier to troubleshoot.

## 2. Install Python

Install Python 3.12 or newer from:

```text
https://www.python.org/downloads/windows/
```

During installation, enable:

```text
Add python.exe to PATH
```

Check the install:

```powershell
py -3.12 --version
```

If you use Anaconda or Miniconda, prefer `py -3.12` for this project so the
backend virtual environment is created with python.org Python.

## 3. Prepare VOICEVOX Engine

Yui VRM AI Studio needs VOICEVOX Engine, specifically `vv-engine\run.exe`.
The normal VOICEVOX app usually includes this engine, so installing VOICEVOX
from the official site is the easiest path for most users:

```text
https://voicevox.hiroshiba.jp/
```

Advanced users can also download the standalone VOICEVOX Engine package:

```text
https://github.com/VOICEVOX/voicevox_engine/releases
```

The start script automatically checks the common install locations:

```text
%LOCALAPPDATA%\Programs\VOICEVOX\vv-engine\run.exe
%ProgramFiles%\VOICEVOX\vv-engine\run.exe
```

If VOICEVOX Engine is somewhere else, set `VOICEVOX_ENGINE_EXE` to the full path of
`vv-engine\run.exe`.

VOICEVOX/VOICEVOX Engine is not bundled with this project. Install it separately
and follow the VOICEVOX terms and credit requirements.

The launcher does not automate the VOICEVOX GUI. It launches `vv-engine\run.exe`
directly and uses it as a local API server at `http://127.0.0.1:50021`. On first
launch, VOICEVOX Engine may spend a short time loading voices or preparing its
local engine. If it looks quiet for a moment, wait until the launcher says the
service is ready.

## 4. Create An OpenAI API Key

Create an API key at:

```text
https://platform.openai.com/api-keys
```

OpenAI API usage is normally billed by usage and may require billing setup on
your OpenAI account. Keep the key private. Do not paste it into GitHub, chats,
screenshots, or issue reports.

Typical alpha use is light if you leave experimental realtime features off.
If you only want to try the main features, 5 USD of API credit is usually
enough to play with normal chat, voice input, image checks, and translation
tests. Heavy realtime experiments, long sessions, or high-volume image/audio
use will cost more.

## 5. Run Backend Setup

Open PowerShell in the repository folder and run:

```powershell
.\scripts\setup_backend_byok.ps1
```

If PowerShell says scripts are disabled, run this once:

```powershell
Set-ExecutionPolicy RemoteSigned -Scope CurrentUser
```

Then run the setup script again.

The setup script creates a Python virtual environment and creates `.env` from
`.env.example` if needed.

## 6. Edit `.env`

Open `.env` in Notepad or your editor and set:

```text
OPENAI_API_KEY=your_api_key_here
```

If Windows is awkward about opening `.env`, run this from the repository folder:

```powershell
notepad .env
```

Use this shape, with no quotes:

```text
OPENAI_API_KEY=sk-...
```

The model names in `.env.example` are defaults for this alpha. If your account
cannot use one of them, replace it with a model available to your account.

## 7. Start Local Services

Double-click:

```text
Start_Yui_Backend_And_VOICEVOX.bat
```

Keep the launcher window open while using the app. It starts both:

- the local backend at `http://127.0.0.1:8000`
- VOICEVOX Engine at `http://127.0.0.1:50021`

Startup may take a little while on the first run. When the log stops at the
ready state and no new error appears, you can launch the app.

When finished, press Enter in that launcher window. This is the normal way to
stop both the backend and VOICEVOX Engine. If the window was closed by mistake
or a process remains stuck, double-click:

```text
Stop_Yui_Backend_And_VOICEVOX.bat
```

Treat the stop batch as a force-stop helper. In normal use, the start batch is
enough for both startup and shutdown.

The VOICEVOX launcher chooses CPU threads automatically from your PC's logical
processor count. It is not fixed to the developer's machine.

## 8. Launch The App

Run:

```text
builds\YuiVRMAIStudio_PublicAlpha_v0.1.0-alpha.1\Yui VRM AI Studio.exe
```

Keep this file in the same folder:

```text
builds\YuiVRMAIStudio_PublicAlpha_v0.1.0-alpha.1\YuiFilePickerHelper.exe
```

Windows may show SmartScreen because this alpha exe is unsigned. Click
`More info`, then `Run anyway` if you trust the build.

## 9. Use Your Own VRM

The public build supports local `.vrm` import through UniVRM:

- VRM 1.0
- VRM 0.x

It does not directly load VRChat SDK avatars, Unity scenes, Unity prefabs,
`.unitypackage` files, or avatars that only exist as uploaded VRChat avatars.
VRChat-specific setup such as expressions menus, FX controllers, PhysBones,
contacts, constraints, and avatar descriptor settings should be treated as
VRChat-only data unless you rebuild equivalent behavior for VRM.

In the app:

1. Open Settings.
2. Click the `Custom VRM` import button.
3. Select your `.vrm` file.

After import succeeds, the app switches to the custom avatar immediately, saves
the selected path locally, and tries to reload it on the next launch. The app
also auto-normalizes the avatar height and position. You can fine tune camera
and view settings from Settings.

Some advanced VRM materials, expressions, or nonstandard rigs may not look
perfect in this alpha.

If your avatar currently lives in a VRChat Unity project, check whether the
original BOOTH/download package includes a ready-made `.vrm`. If not, create a
separate VRM copy with a Unity/UniVRM or Blender/VRM export workflow, then
import that exported `.vrm` into Yui VRM AI Studio.

## Troubleshooting

Backend is not running:

```powershell
.\scripts\check_backend.ps1
```

Or open:

```text
http://127.0.0.1:8000/health
```

VOICEVOX is not found:

- Install the normal VOICEVOX app or standalone VOICEVOX Engine.
- Confirm `vv-engine\run.exe` exists.
- Or set `VOICEVOX_ENGINE_EXE` to the full path of `vv-engine\run.exe`.

`.env` is hard to open:

- Run `notepad .env` from the repository folder.
- Make sure the file is named `.env`, not `.env.txt`.

Chat does not respond:

- Confirm `.env` exists.
- Confirm `OPENAI_API_KEY` is not empty.
- Confirm your OpenAI account can use the configured model names.

Voice does not play:

- Confirm VOICEVOX is running.
- Open `http://127.0.0.1:50021/version` in a browser.

File picker does not open:

- Keep `YuiFilePickerHelper.exe` beside `Yui VRM AI Studio.exe`.

Logs are written under:

```text
logs/
```

## FAQ

Can I use it without VOICEVOX?

Text chat can still work, but local Japanese speech playback needs VOICEVOX.

Can I use it without an OpenAI API key?

Not for the main public alpha chat/STT/vision path. This release expects BYOK
OpenAI API setup.

Does it support macOS or Linux?

Not yet. This alpha currently targets Windows.

Can I change the character name?

Yes. Use Settings -> Character Name.
