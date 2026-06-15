# Public BYOK Setup

This project is designed so public users bring their own API key and run the
backend locally. For a beginner-friendly setup walkthrough, start with
`docs/SETUP_GUIDE.md`.

## What Is Included

- Unity client source.
- Python FastAPI backend source.
- Safe `.env.example` template.
- UnityChan release avatar assets.
- Local VOICEVOX integration for Japanese speech, when VOICEVOX Engine is
  installed separately.

## What Must Not Be Published

- `.env`
- `backend/data/*.db`
- `logs/`
- private Yui/Kikyo avatar assets
- private Yui/Kikyo scene objects
- personal API keys, tokens, cookies, or local service logs

The public Unity build should use UnityChan as the only bundled avatar.

## First-Time Backend Setup

Requirements:

- Windows 10/11
- Python 3.12+
- An OpenAI API key
- Optional: VOICEVOX Engine for local Japanese TTS

Links:

- Python: https://www.python.org/downloads/windows/
- OpenAI API keys: https://platform.openai.com/api-keys
- VOICEVOX: https://voicevox.hiroshiba.jp/

From the repository root:

```powershell
.\scripts\setup_backend_byok.ps1
```

Then edit `.env` and set:

```text
OPENAI_API_KEY=your_api_key_here
```

To pass the key during setup without printing it:

```powershell
.\scripts\setup_backend_byok.ps1 -OpenAIApiKey "your_api_key_here"
```

## Start Local Services

For the local alpha workflow, double-click:

```text
Start_Yui_Backend_And_VOICEVOX.bat
```

Keep the launcher window open. Press Enter in that window when finished; it
stops both the backend and VOICEVOX Engine.

## Use Your Own VRM

The public build ships with UnityChan as the default bundled avatar. You can
still use your own VRM:

This alpha imports `.vrm` files only. It does not directly load VRChat SDK
avatars, Unity prefabs, Unity scenes, `.unitypackage` files, or avatars that
only exist as uploaded VRChat avatars.

1. Start the backend and VOICEVOX.
2. Launch `Yui VRM AI Studio.exe`.
3. Open settings.
4. Click the `Custom VRM` import button.
5. Select your `.vrm` file.

The app loads VRM 1.0 and VRM 0.x files through UniVRM. After import succeeds,
it switches to the custom avatar immediately and stores the selected path
locally for the next launch.

If no private development avatar is bundled, the avatar list should show only
`UnityChan Default` and `Custom VRM`.

## Release Safety Check

Before publishing a public copy, run the audit against the UnityChan release
copy, not the private development tree:

```powershell
.\scripts\audit_distribution_release.ps1 -ProjectRoot .
```

The audit should fail if private avatars, `.env`, local databases, obvious API
keys, or private scene object names are present.
