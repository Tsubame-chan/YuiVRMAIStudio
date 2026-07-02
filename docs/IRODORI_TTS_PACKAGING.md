# Irodori TTS Optional Backend Pack

This document records the intended packaging shape for the optional Irodori TTS backend.
VOICEVOX remains the default and most stable TTS path. Irodori is an optional add-on for users who want voice-design TTS.

## Goals

- Keep the default Yui setup usable with VOICEVOX.
- Allow Windows and macOS users to install an optional Irodori TTS pack by extracting a ZIP or running an installer.
- Avoid manual PATH editing for common installs.
- Keep Speed/Pitch sliders continuous and separate from Irodori voice-design instructions.
- Preserve voice consistency by using a cached reference audio anchor per voice-design preset.
- Treat macOS MLX and Windows Irodori-TTS-Server as separate backend candidates, not interchangeable builds of one engine.

## Layout

Optional binaries should be placed under the repository or installed app support folder using this shape:

```text
tools/
  tts/
    soundtouch/
      bin/
        soundstretch       # macOS/Linux
        soundstretch.exe   # Windows
```

The backend resolves SoundStretch in this order:

1. `HTTP_TTS_SOUNDSTRETCH_PATH`, if set and valid.
2. Bundled `tools/tts/soundtouch/bin/soundstretch(.exe)`.
3. `soundstretch` on `PATH`.

This makes ZIP/installer installs reproducible while still letting developers use Homebrew or another local install.

## Backend Settings

The Unity app does not switch between VOICEVOX and Irodori URLs. The app should keep using the Yui backend URL, for example `http://127.0.0.1:8000` on the same machine or `http://<Mac-or-PC-LAN-IP>:8000` from iOS. The Yui backend reads the HTTP TTS settings below and proxies requests to the selected TTS runtime.

For Irodori through the current MLX Audio compatible server:

```env
HTTP_TTS_BASE_URL=http://127.0.0.1:41080
HTTP_TTS_ENDPOINT=/v1/audio/speech
HTTP_TTS_HEALTH_ENDPOINT=/v1/models
HTTP_TTS_PROVIDER_ID=irodori
HTTP_TTS_PAYLOAD_FORMAT=openai_speech
HTTP_TTS_MODEL=mlx-community/Irodori-TTS-600M-v3-VoiceDesign-8bit
HTTP_TTS_GENDER=female
HTTP_TTS_INSTRUCT=若い女性の、明るく可愛いアニメ調の声で話してください。
HTTP_TTS_LANG_CODE=ja
HTTP_TTS_FORMAT=wav
HTTP_TTS_AUDIO_PROCESSOR=soundstretch
HTTP_TTS_SOUNDSTRETCH_PATH=
IRODORI_ENABLE=auto
IRODORI_BASE_URL=http://127.0.0.1:41080
IRODORI_MLX_DIR=<path-to-Irodori-TTS-Local>
```

Leave `HTTP_TTS_SOUNDSTRETCH_PATH` empty when using the bundled layout above.

`scripts/start_local_services_macos.sh` and `scripts/start_local_services_detached_macos.sh` start VOICEVOX, Irodori, and the Yui backend together when Irodori is configured. If the runtime lives somewhere else, set `IRODORI_START_COMMAND` to a complete command that starts the TTS server.

### macOS MLX latency profile

The macOS VoiceDesign path can reduce latency without shortening the text or forcing the user's playback speed by using a derived MLX model profile with fewer sampler steps.

Create the profile locally:

```bash
python3 scripts/create_irodori_mlx_profile.py \
  --source "<path-to-Irodori-TTS-Local>/models/Irodori-TTS-600M-v3-VoiceDesign-8bit" \
  --output "<path-to-Irodori-TTS-Local>/models/Irodori-TTS-600M-v3-VoiceDesign-8bit-steps32" \
  --num-steps 32
```

Then point Yui at the profile:

```env
HTTP_TTS_MODEL=<path-to-Irodori-TTS-Local>/models/Irodori-TTS-600M-v3-VoiceDesign-8bit-steps32
```

This profile copies only `config.json` and links the large model files from the original install. Do not commit the generated profile or model files to this repository.

Local benchmark notes as of 2026-06-28:

| MLX VoiceDesign profile | Warm median | Change |
| --- | ---: | ---: |
| `num_steps=40` default | 14.48s | baseline |
| `num_steps=32` | 12.70s | about 12% faster |
| `num_steps=30` | 12.64s | about 13% faster |

`num_steps=32` is the safer current candidate because `30` did not materially improve latency in the local test. This preserves the displayed text, keeps sentence content unchanged, and leaves the app's Speed/Pitch controls as post-processing controls instead of silently rewriting the voice style.

For Windows NVIDIA Irodori-TTS-Server validation:

```env
HTTP_TTS_BASE_URL=http://127.0.0.1:8088
HTTP_TTS_ENDPOINT=/v1/audio/speech
HTTP_TTS_HEALTH_ENDPOINT=/health
HTTP_TTS_PROVIDER_ID=irodori-server
HTTP_TTS_PAYLOAD_FORMAT=irodori_openai_speech
HTTP_TTS_VOICE=none
HTTP_TTS_MODEL=irodori-tts
HTTP_TTS_INSTRUCT=若い女性の、明るく聞き取りやすい声で話してください。
HTTP_TTS_FORMAT=wav
HTTP_TTS_AUDIO_PROCESSOR=soundstretch
HTTP_TTS_IRODORI_NUM_STEPS=24
HTTP_TTS_IRODORI_SEED=1234
HTTP_TTS_IRODORI_CHUNKING_ENABLED=false
HTTP_TTS_IRODORI_CHUNK_MIN_CHARS=120
IRODORI_ENABLE=auto
IRODORI_BASE_URL=http://127.0.0.1:8088
IRODORI_START_COMMAND=
```

Benchmark notes as of 2026-06-26:

- The OpenAI-compatible Irodori-TTS-Server installs cleanly on macOS with the MPS backend, but its default model target is `Aratako/Irodori-TTS-500M-v3`.
- Yui's current macOS path uses `mlx-community/Irodori-TTS-600M-v3-VoiceDesign-8bit`, where the caption/instruction text is part of the voice-design value.
- Treat Irodori-TTS-Server as a candidate optional backend, not a drop-in replacement, until 600M VoiceDesign and caption passthrough are verified.
- Treat Irodori-TTS-Server as the Windows NVIDIA candidate because MLX Audio is Apple Silicon-oriented and Irodori-TTS-Server has CUDA/ROCm deployment paths.
- Do not expose the rejected macOS no-ref low-step profile in the Unity UI. It was fast, but the voice quality and identity were not acceptable for Yui.
- For normal conversation, disable Irodori-TTS-Server chunking while validating. Long text should either stay on VOICEVOX/OS TTS or use a future chunk-queue/SSE playback path after quality is proven.
- For Windows NVIDIA, use the same latency policy as macOS: keep the spoken text intact, keep user speed controls meaningful, and tune generation cost first. Benchmark `HTTP_TTS_IRODORI_NUM_STEPS=16/20/24/32` with `HTTP_TTS_IRODORI_CHUNKING_ENABLED=false`, then choose the lowest step count that does not break voice quality. The documented default is `24` so the first Windows test starts from a quality-conscious point instead of the rejected fastest profile.

## Licensing Notes

- The Irodori model metadata currently used locally declares MIT license.
- Homebrew reports `sound-touch` 2.4.1 as `LGPL-2.1-or-later`.
- Do not ship third-party binaries without including their license notices.
- Rubber Band can produce high quality pitch shifting, but its GPL/commercial licensing makes it a less convenient default bundle candidate.

This is an engineering note, not legal advice. Before public release, run a dependency and license audit for the exact binaries included in the ZIP or installer.

## Safety Notice

The UI/help text should keep a short notice that voice cloning, impersonation, and unauthorized imitation of real people are prohibited. Most use cases are avatar voice design, but the warning should be visible enough for public distribution.
