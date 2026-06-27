# Mac Setup

> Current public-facing macOS setup starts at `docs/MAC_PUBLIC_ALPHA.md`.
> This file is a historical setup log from the initial macOS migration and still contains older Unity `2022.3.6f1` notes.
> For current Unity work, treat the canonical project as Unity `2022.3.62f3`.

This guide records the macOS migration steps verified on 2026-06-08.

## Verified Environment

- macOS 26.5.1 on Apple Silicon arm64
- Xcode Command Line Tools 26.5
- Homebrew at `/opt/homebrew/bin/brew`
- Python 3.12.13 from Homebrew
- Git LFS 3.7.1
- Rosetta 2
- Unity Hub
- Unity Editor 2022.3.6f1

## One-Time Machine Setup

Install Command Line Tools:

```bash
softwareupdate --install "Command Line Tools for Xcode 26.5-26.5"
```

Install Homebrew, then add it to the shell:

```bash
echo 'eval "$(/opt/homebrew/bin/brew shellenv)"' >> ~/.zprofile
eval "$(/opt/homebrew/bin/brew shellenv)"
```

Install project tooling:

```bash
brew install python@3.12 git git-lfs gh
git lfs install
softwareupdate --install-rosetta --agree-to-license
```

Rosetta is required because Unity 2022.3.6f1 includes an x86_64 Unity Package
Manager executable even when the editor itself runs as arm64.

Install Unity Hub and Unity Editor `2022.3.6f1`. The project version is recorded
in:

```text
unity/ProjectSettings/ProjectVersion.txt
```

## Backend Setup

From the repository root:

```bash
cd "/path/to/Yui VRM AI Studio"
PYTHON_BIN=/opt/homebrew/bin/python3.12 ./scripts/setup_backend_byok_macos.sh
```

All `./scripts/...` commands below assume the terminal is already in the
repository root. If the prompt is at `~`, replace the example path above with
your local repository path and run `cd` first.

Edit `.env` and set local secrets:

```bash
open -e .env
```

At minimum, chat features need:

```env
OPENAI_API_KEY=sk-...
```

Start only the backend:

```bash
./scripts/run_backend_macos.sh
```

Check:

```bash
curl -fsS http://127.0.0.1:8000/health
curl -fsS http://127.0.0.1:8000/config
```

Start backend plus VOICEVOX if VOICEVOX is installed:

```bash
./scripts/start_local_services_macos.sh
```

For Finder-based startup, double-click:

```text
Start_Yui_Local_Services.command
```

To send a stop request without using the launcher window, double-click:

```text
Stop_Yui_Local_Services.command
```

The default VOICEVOX endpoint is:

```text
http://127.0.0.1:50021
```

## Unity Setup

Open this folder in Unity Hub:

```text
unity
```

The project has been verified with:

```bash
/Applications/Unity/Hub/Editor/2022.3.6f1/Unity.app/Contents/MacOS/Unity \
  -batchmode \
  -quit \
  -projectPath "/path/to/Yui VRM AI Studio/unity" \
  -logFile /private/tmp/yui-unity-final-20260608.log
```

Successful verification generated:

```text
unity/Library/ScriptAssemblies/Assembly-CSharp.dll
unity/Library/ScriptAssemblies/Assembly-CSharp-Editor.dll
```

`unity/Library` is generated local editor state. It can be deleted and rebuilt
by Unity, but keeping it locally makes subsequent opens faster.

## Verified Commands

Backend:

```bash
backend/.venv/bin/python -m compileall -q backend/app backend/main.py
cd backend
.venv/bin/python -m pytest tests
```

The existing backend tests passed:

```text
5 passed
```

## Notes

- `.env` is local-only and must not be committed.
- Python virtual environments are rebuilt on macOS; do not copy Windows `.venv`.
- Windows scripts remain as documentation for the original launch flow.
- `Start_Yui_Backend_And_VOICEVOX.bat` and related `.ps1` scripts are Windows-only.
- Mac equivalents live under `scripts/*_macos.sh`.
- Mac standalone file selection still needs a native macOS implementation for
  built app distribution. In the Unity Editor, file selection works through
  `EditorUtility.OpenFilePanel`.
