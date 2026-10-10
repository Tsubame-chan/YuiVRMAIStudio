# Yui Bundled Backend

This folder is the local backend bundled with Yui VRM AI Studio for macOS.

## Normal Use

Open the Yui app. The app checks `http://127.0.0.1:8000/health` and starts
this backend automatically when a selected feature needs it and no healthy backend is already running. On-device conversation and voice can run without the Backend.

## Manual Start

Run:

```bash
./Start_Yui_Backend.command
```

The command starts Yui local services in the background. If the backend virtual
environment is missing, it runs the setup script first. Python 3.12+ is required
for that setup path.

## Manual Stop

Run:

```bash
./Stop_Yui_Backend.command
```

This command stops only services recorded as started by this installation.
An engine started separately stays running; stop it in its own application.

## Updating an existing Backend

For an existing Backend, replace `backend/.venv` as a complete environment after
stopping services and moving the old environment to private backup. Do not merge
its contents: obsolete Python modules and version metadata would remain.
Preserve Mac links/permissions, `.env`, `backend/data`, optional engines and models.
See the [shared update procedure](https://github.com/Tsubame-chan/YuiVRMAIStudio/blob/main/docs/BACKEND_UPDATE.md).

## API Keys

Set Backend API keys in its Console. The app’s Direct API key is separate.
Keep files containing keys private when backing up or sharing this folder.
Advanced users can also create a local `.env` next to this file.

## Manage AI, voices and device sync

Manual Start opens the Backend Console at `http://127.0.0.1:8000/admin/`.
Use **AI and voice** to configure providers and saved voices. Use **Connection and settings → Devices and sync** to display a one-time device registration code. A sync-capable Yui app lets you choose the shared character and review changes before applying them.

VOICEVOX, AivisSpeech and Irodori server applications are installed separately.
See the [TTS installation guide](https://github.com/Tsubame-chan/YuiVRMAIStudio/blob/main/docs/BACKEND_TTS_GUIDE.md).
