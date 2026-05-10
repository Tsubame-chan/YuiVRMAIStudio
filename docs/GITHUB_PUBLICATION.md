# GitHub Publication Plan

This project should be published from a generated public repository folder, not
from the private development tree.

## Supported Public Paths

1. GitHub source release
   - Users clone or download the repository.
   - Users run the BYOK setup script.
   - Users install VOICEVOX separately.
   - Users enter their own API key in `.env`.

2. Windows installer release
   - Installer lays down the Unity app, backend source, scripts, and docs.
   - Installer does not bundle VOICEVOX.
   - User installs VOICEVOX separately, then sets `OPENAI_API_KEY`.

The first public target is Windows. Unity source may be portable later, but the
documented path should stay Windows-only until other platforms are tested.

## Do Not Publish

- Private Yui/Kikyo avatar assets or scene objects.
- `.env`
- local databases under `backend/data/`
- logs
- Unity generated folders: `Library`, `Temp`, `Logs`, `UserSettings`
- old handoff/session docs containing local paths or private workflow notes
- API keys, service tokens, cookies, or personal machine paths

## Build the Public Repository Folder

From the private development repository:

```powershell
.\scripts\prepare_public_repository.ps1 `
  -SourceRoot "C:\path\to\YuiVRMAIStudio-UnityChan-Release"
```

The script writes:

```text
public/YuiVRMAIStudio_Public
```

Then:

```powershell
cd .\public\YuiVRMAIStudio_Public
git init
git add .
git commit -m "Initial public BYOK Windows alpha"
```

Run the audit before pushing:

```powershell
.\scripts\audit_distribution_release.ps1 -ProjectRoot .
```

## VOICEVOX Policy

Do not bundle VOICEVOX in the first public release. Treat it as a prerequisite
and link users to the official VOICEVOX download page.

The start script searches for VOICEVOX Engine in this order:

1. `-VoicevoxEngineExe`
2. `VOICEVOX_ENGINE_EXE`
3. `%LOCALAPPDATA%\Programs\VOICEVOX\vv-engine\run.exe`
4. `%ProgramFiles%\VOICEVOX\vv-engine\run.exe`

## Public README Focus

The public README should lead with:

- what this is
- current Windows alpha status
- prerequisites
- quick setup
- API key setup
- VOICEVOX prerequisite
- how to start/stop services
- license and asset notes

## Windows App Builds

Two Windows app builds are expected:

- Personal alpha: built from the private development Unity project for the
  developer's own daily use.
- Public alpha: built from the UnityChan-only public source, bundled with
  UnityChan and allowing users to import their own VRM.

Use the safe build methods:

```powershell
Unity.exe -batchmode -quit `
  -projectPath "C:\path\to\YuiVRMAIStudio\unity" `
  -executeMethod YuiPhysicalAI.Editor.YuiPublicWindowsBuildTools.BuildWindowsPersonalAlpha

Unity.exe -batchmode -quit `
  -projectPath "C:\path\to\YuiVRMAIStudio_Public\unity" `
  -executeMethod YuiPhysicalAI.Editor.YuiPublicWindowsBuildTools.BuildWindowsPublicAlpha
```

Do not use older build tooling that depends on private Kikyo/Yui setup paths for
the public release.
