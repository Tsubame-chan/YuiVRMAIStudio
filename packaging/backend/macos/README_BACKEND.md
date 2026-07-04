# Yui Bundled Backend

This folder is the local backend bundled with Yui VRM AI Studio for macOS.

## Normal Use

Open the Yui app. The app checks `http://127.0.0.1:8000/health` and starts
this backend automatically when no healthy backend is already running.

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

This command asks the known local Yui service ports to stop. Use it when a
backend process remains alive after closing the app or when you want to free the
ports before testing another build.

## API Keys

The backend bundle includes `.env.example` only. It must never include a real
`.env` or API key. Users can set API keys in the app settings, or advanced users
can create a local `.env` next to this file.

## Restricted Voice Assets

`female_voice_3` / `七日週_T2モデル` is intentionally excluded because its custom
license blocks unmodified redistribution.

