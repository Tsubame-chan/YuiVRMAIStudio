# Yui Bundled Backend for Windows

This folder is the local backend bundled with Yui VRM AI Studio for Windows.

## Normal Use

Open the Yui app. The app checks `http://127.0.0.1:8000/health` and starts
this backend automatically when a selected feature needs it and no healthy backend is already running. On-device conversation and voice can run without the Backend.

The public bundle includes a portable Python runtime under `backend\.venv`, so
normal users do not need to install Python separately.

## Manual Start

Run:

```bat
Start_Yui_Backend.bat
```

The command uses the bundled Python runtime and starts Yui local services. Keep
the window open while using the backend manually.

## Manual Stop

Run:

```bat
Stop_Yui_Backend.bat
```

This command stops only services recorded as started by this installation.
An engine started separately stays running; stop it in its own application.

## API Keys

Set Backend API keys in its Console. The app’s Direct API key is separate.
Keep files containing keys private when backing up or sharing this folder.
Advanced users can also create a local `.env` next to this file.

## Manage AI, voices and device sync

Manual Start opens the Backend Console at `http://127.0.0.1:8000/admin/`.
Use **AI and voice** to configure providers and saved voices. Use **Connection and settings → Devices and sync** to display a one-time device registration code. A sync-capable Yui app lets you choose the shared character and review changes before applying them.

VOICEVOX, AivisSpeech and Irodori server applications are installed separately.
See the [TTS installation guide](https://github.com/Tsubame-chan/YuiVRMAIStudio/blob/main/docs/BACKEND_TTS_GUIDE.md).

## Optional Irodori V4.1 (NVIDIA)

The release has a separate optional Irodori V4.1 package with the INT8 model,
codec and three synthetic reference voices. Extract its `YuiIrodoriV4` folder
inside this Backend folder, stop the Backend, and run `Install_Irodori_V4.bat`.
The installer verifies package hashes and installs Python/CUDA dependencies.
Internet, Git for Windows and a compatible NVIDIA GPU/driver are required.
VOICEVOX stays the default. See the Windows Irodori guide for saved-setting
precedence and the remaining native Windows validation.
