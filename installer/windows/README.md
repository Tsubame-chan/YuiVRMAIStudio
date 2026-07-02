# Windows Installer Skeleton

This folder contains the first Inno Setup script for a Windows alpha installer.

The installer is intentionally BYOK:

- it installs Yui files;
- it does not install VOICEVOX;
- it does not ask for or store API keys;
- users run `scripts\setup_backend_byok.ps1` and edit `.env`.

Expected source layout before compiling:

```text
public/YuiVRMAIStudio_Public/
  builds/YuiVRMAIStudio_PublicAlpha_v0.1.0-alpha.1/Yui VRM AI Studio.exe
  backend/
  scripts/
  docs/
  Start_Yui_Backend_And_VOICEVOX.bat
  Stop_Yui_Backend_And_VOICEVOX.bat
```

Compile with Inno Setup after a Windows app build exists.
