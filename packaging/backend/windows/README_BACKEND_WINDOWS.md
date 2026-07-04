# Yui Bundled Backend for Windows

This folder is the local backend bundled with Yui VRM AI Studio for Windows.

## Normal Use

Open the Yui app. The app checks `http://127.0.0.1:8000/health` and starts
this backend automatically when no healthy backend is already running.

On Windows, the first backend start creates `backend\.venv` if it is missing.
That setup requires Python 3.12+ and internet access for Python packages.

## Manual Start

Run:

```bat
Start_Yui_Backend.bat
```

The command creates the backend virtual environment if needed, then starts Yui
local services. Keep the window open while using the backend manually.

## Manual Stop

Run:

```bat
Stop_Yui_Backend.bat
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
