# macOS Public Alpha

This page is the entry point for trying the macOS version of Yui VRM AI Studio.

The macOS version is still experimental. The current public macOS alpha lives on a separate branch, not on main:

- GitHub branch: [`macos-public-alpha`](https://github.com/Tsubame-chan/YuiVRMAIStudio/tree/macos-public-alpha)
- Japanese guide: [`docs/MAC_PUBLIC_ALPHA.md`](MAC_PUBLIC_ALPHA.md)

## Current Status

| Area | Status |
| --- | --- |
| Distribution | Public Alpha branch |
| Target | Mainly Apple Silicon Mac |
| Local service | FastAPI |
| AI provider | OpenAI BYOK |
| TTS | Local VOICEVOX Engine |
| VRM | `.vrm` import |
| iOS relationship | Separate from the iOS Personal build |

The macOS version uses the same local-service architecture as Windows. Your OpenAI API key, VOICEVOX setup, conversation database, and audio cache stay on your local machine.

## Requirements

- Apple Silicon Mac
- Homebrew
- Python 3.12+
- An OpenAI API key
- VOICEVOX.app or VOICEVOX Engine
- Unity Hub / Unity Editor 2022.3 LTS

Current build verification uses Unity `2022.3.62f3`.
Some older status documents mention `2022.3.6f1`, but the canonical Unity project should now be treated as a `2022.3.62f3` project.

## Setup

Clone the macOS public alpha branch:

```bash
git clone https://github.com/Tsubame-chan/YuiVRMAIStudio.git
cd YuiVRMAIStudio
git checkout macos-public-alpha
```

If you already cloned the repository:

```bash
git fetch origin
git checkout macos-public-alpha
```

Install Homebrew tools:

```bash
brew install python@3.12 git git-lfs
git lfs install
```

Initialize the local service:

```bash
PYTHON_BIN=/opt/homebrew/bin/python3.12 ./scripts/setup_backend_byok_macos.sh
```

Open `.env` and set your OpenAI API key:

```bash
open -e .env
```

Minimum required setting:

```env
OPENAI_API_KEY=sk-...
```

## Start Services

Start only the local service:

```bash
./scripts/run_backend_macos.sh
```

Start the local service plus VOICEVOX:

```bash
./scripts/start_local_services_macos.sh
```

Finder launcher:

```text
Start_Yui_Local_Services.command
```

Stop services:

```bash
./scripts/stop_local_services_macos.sh
```

Or:

```text
Stop_Yui_Local_Services.command
```

## VOICEVOX

The macOS launcher mainly searches:

```text
/Applications/VOICEVOX.app/Contents/Resources/vv-engine/run
~/Applications/VOICEVOX.app/Contents/Resources/vv-engine/run
```

If VOICEVOX Engine is somewhere else, set `VOICEVOX_ENGINE_EXE`:

```bash
export VOICEVOX_ENGINE_EXE="/path/to/VOICEVOX.app/Contents/Resources/vv-engine/run"
```

Text chat can work without VOICEVOX, but Japanese speech playback needs VOICEVOX Engine.

## App

The macOS Public Alpha branch contains the macOS Unity build / packaging path. If a built `.app` is included in the distribution artifact, launch that app.

For development, open the repository's Unity project through Unity Hub:

```text
unity/
```

## Pre-Distribution Check

After producing a macOS Public Alpha artifact, run the public safety and macOS
artifact audit:

```bash
./backend/.venv/bin/python scripts/audit_distribution_release.py --require-builds --platform macos
```

The audit checks for local databases, generated speech caches, private avatar
files, Unity generated folders, and one of these macOS artifacts:

```text
builds/YuiVRMAIStudio_MacOSAlpha_v0.1.0-alpha.1/Yui VRM AI Studio.app
builds/YuiVRMAIStudio_MacOSAlpha_v0.1.0-alpha.1_macos.zip
```

To check Windows and macOS artifacts together:

```bash
./backend/.venv/bin/python scripts/audit_distribution_release.py --require-builds --platform all
```

## Current Caveats

- The macOS build is still experimental.
- Signing and notarization still need cleanup before a polished public release. The current audit validates public safety and artifact presence.
- VOICEVOX is not bundled. Install it separately.
- Do not mix iOS Personal connection defaults or owner-specific settings into the macOS Public build.
- Public builds must not include private avatars or personal bundle IDs.

## Related Docs

- Main README: [`../README.md`](../README.md)
- English README: [`../README.en.md`](../README.en.md)
- Build variants: [`BUILD_VARIANTS.md`](BUILD_VARIANTS.md)
- macOS setup history: [`MAC_SETUP.md`](MAC_SETUP.md)
- API: [`api.md`](api.md)
- External info / web search policy: [`LLM_EXTERNAL_INFO.md`](LLM_EXTERNAL_INFO.md)
