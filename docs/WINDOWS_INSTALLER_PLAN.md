# Windows Installer Plan

The installer should be a convenience wrapper around the same BYOK local setup.
It should not hide the fact that users need their own API key and a separate
VOICEVOX install.

## Installer Contents

- Windows Unity app build.
- Backend Python source.
- `.env.example`.
- start/stop scripts.
- setup scripts.
- public docs.
- UnityChan license/credit files.

## Installer Must Not Bundle

- VOICEVOX app, engine, or voice libraries.
- private Yui/Kikyo avatars.
- `.env`.
- local databases.
- logs.
- user API keys.

## First Installer Technology

Use Inno Setup first. It is simple, Windows-native, scriptable, and enough for
an alpha installer. WiX/MSIX can come later if signing, updates, or enterprise
installation become important.

## Install Flow

1. Install files to `%LOCALAPPDATA%\YuiVRMAIStudio` by default.
2. Create Start Menu shortcuts:
   - Yui VRM AI Studio
   - Start Yui Backend + VOICEVOX
   - Stop Yui Services
   - Setup Backend BYOK
3. Open the public BYOK setup document after install.
4. Do not ask for the OpenAI API key inside the installer in the first version.
   Keep key entry in `.env` or a future in-app setup screen.

## Prerequisite Flow

The installer readme should say:

- Install Python 3.12+.
- Install VOICEVOX from the official site.
- Run `scripts\setup_backend_byok.ps1`.
- Set `OPENAI_API_KEY` in `.env`.
- Start services.

The start script already searches the normal VOICEVOX install path and supports
`VOICEVOX_ENGINE_EXE` for custom locations.
