# macOS setup

[日本語](MAC_PUBLIC_BETA.md) · [Help & FAQ](HELP.md)

## Run the app

1. Download `YuiVRMAIStudio_MacOSPublicBeta_v0.2.4-beta.2_macos.zip` from [v0.2.4-beta.2](https://github.com/Tsubame-chan/YuiVRMAIStudio/releases/tag/v0.2.4-beta.2).
2. Extract and open `Yui VRM AI Studio.app`. The bundled local runtime targets Apple Silicon.
3. Read the first-run notice and start the data download. It installs standard E2B, five Japanese voices, a dictionary and the Mac runtime: approximately 2.5GB, plus space needed for extraction.
4. Send a message, then use Settings to customize personality, voice and your VRM. Replay the illustrated tutorial from Help.

Signing/notarization are not yet provided. If macOS blocks launch, verify the download source and follow the OS Privacy & Security prompt to allow it. Do not disable system-wide security. The `.sha256` file verifies the app ZIP.

Normally you only need the app ZIP; its installer handles the split data and runtime. `Code > Download ZIP` contains source, not runnable apps or model weights.

## Choose AI and speech

- Local AI: E2B by default, usable offline after setup. Optional E4B can be downloaded in Settings; it uses more storage, memory and response time.
- OpenAI API: enter an API key in Settings. Recommended for higher-quality chat; connectivity and API charges apply separately from a ChatGPT subscription.
- Backend: optional extension route. Configure its own `.env`; the app's Direct API key is not forwarded to it.

AI and speech engines are independent. Standard speech uses five Japanese VOICEVOX voices. Local chat/STT inference uses the downloaded `YuiBackend` runtime worker without requiring its HTTP server. Optional speech engines require separate installation.

## Import your avatar

Select a `.vrm` from character Settings and wait for loading. Change appearance from My characters to retain personality, voice and memories. Export Unity/VRChat avatars to VRM first; purchase ZIPs, prefabs, FBX and unitypackage files are not direct inputs. See [avatar import](AVATAR_IMPORT.md).

## Optional Backend

Use `Start_Yui_Backend.command` / `Stop_Yui_Backend.command` inside the downloaded `YuiBackend`. Configure its provider keys and selected STT/TTS runtime separately. For a remote PC, use a reachable VPN address and server listen configuration (`BACKEND_HOST` defaults to `127.0.0.1`, `BACKEND_PORT` to `8000`; bind to the PC VPN IP and use `http://<VPN IP>:8000` in the app); a localhost-only server is not reachable from another device merely because both use a VPN.

For source setup:

```bash
PYTHON_BIN=/opt/homebrew/bin/python3.12 ./scripts/setup_backend_byok_macos.sh
open -e .env
./scripts/start_local_services_macos.sh
# Stop
./scripts/stop_local_services_macos.sh
```

Follow the scripts' prerequisites for Python and other dependencies. Backend `.env` credentials do not configure the app's Direct API key. Backend VOICEVOX or other speech engines also need their runtime.

[端末ごとの対応 / Compatibility](RUNTIME_SUPPORT.md)
