# Windows Installer Skeleton

This folder contains the Inno Setup script for a Windows desktop beta installer.

The installer is intentionally BYOK and local-first:

- it installs Yui files;
- the first launch downloads required local AI/backend data from GitHub Releases;
- it does not ask for or store API keys;
- advanced users can still run backend setup scripts manually when working from source.

Expected source layout before compiling:

```text
public/YuiVRMAIStudio_Public/
  builds/YuiVRMAIStudio_WindowsPublicBeta_v0.2.0-beta.3/Yui VRM AI Studio.exe
  backend/
  scripts/
  docs/
  Start_Yui_Backend_And_VOICEVOX.bat
  Stop_Yui_Backend_And_VOICEVOX.bat
```

Compile with Inno Setup after a Windows app build exists.
