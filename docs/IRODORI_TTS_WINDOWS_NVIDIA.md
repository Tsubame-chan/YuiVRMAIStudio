# Irodori TTS Windows NVIDIA Notes

VOICEVOX remains the default TTS for all public builds. Irodori-TTS-Server is a Windows NVIDIA candidate, not a replacement for the macOS MLX VoiceDesign path.

## Recommended Split

| Environment | Recommended TTS |
| --- | --- |
| macOS Apple Silicon | Irodori MLX VoiceDesign, optional |
| Windows NVIDIA | Irodori-TTS-Server CUDA, candidate |
| Windows CPU only | VOICEVOX; Irodori is not recommended |
| Mobile / low-spec PC | OS TTS or future cloud TTS candidate |

## Phase 1 Setup Target

For the first Windows NVIDIA validation, use Irodori-TTS-Server through Docker or a local Python environment with CUDA support.

This repository does not bundle Irodori model files or the Irodori-TTS-Server runtime. Install the server separately, then point Yui's HTTP TTS adapter at it.

The Windows app should still use the Yui backend URL, normally `http://127.0.0.1:8000`. Do not put the Irodori server URL into the app's Backend URL field. `HTTP_TTS_BASE_URL` is a backend-side setting used by the Yui backend to call Irodori.

The initial Yui-side settings are:

```env
TTS_PROVIDER=http
TTS_FALLBACK_PROVIDER=voicevox
HTTP_TTS_BASE_URL=http://127.0.0.1:8088
HTTP_TTS_ENDPOINT=/v1/audio/speech
HTTP_TTS_HEALTH_ENDPOINT=/health
HTTP_TTS_PROVIDER_ID=irodori-server
HTTP_TTS_PAYLOAD_FORMAT=irodori_openai_speech
HTTP_TTS_VOICE=none
HTTP_TTS_MODEL=irodori-tts
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

If `IRODORI_START_COMMAND` is set, `scripts/start_local_services.ps1` starts Irodori-TTS-Server before launching the Yui backend. For Docker-based installs, put the full Docker command there. If it is empty, the script warns and continues with VOICEVOX plus the Yui backend, so the app can still run with fallback TTS.

Use `ref_audio` only after no-ref quality is checked. On macOS/MPS, the first ref run was slow, and ref-enabled short text was not faster than MLX VoiceDesign. The sample starts at `24` steps to avoid defaulting new users to the rejected fastest profile.

## Health Check

After starting Irodori-TTS-Server, verify that the server is reachable:

```powershell
curl http://127.0.0.1:8088/health
curl http://127.0.0.1:8088/v1/models
```

Then verify that Yui can see the configured HTTP TTS provider:

```powershell
curl http://127.0.0.1:8000/providers/status
```

Expected Yui-side signals:

- `providers.http_tts.status` is `ok` or `configured`.
- `providers.http_tts.engine` is `irodori_server`.
- `providers.http_tts.recommendation` mentions Windows NVIDIA.
- `TTS_FALLBACK_PROVIDER=voicevox` is set so failed experimental TTS requests can fall back to VOICEVOX.

## Benchmark Gate

Run the benchmark with environment metadata preserved:

```powershell
python scripts/bench_tts.py `
  --engine irodori-server-direct `
  --text short_b --text medium_c `
  --iterations 5 `
  --num-steps 24 `
  --seed 1234 `
  --chunking-enabled false `
  --phase windows-nvidia-warm-cache-miss `
  --run-id windows-nvidia-irodori-server-steps24
```

Then repeat for `--num-steps 16`, `20`, and `32` so speed and quality can be compared on the actual Windows GPU.

For ref-enabled tests, add:

```powershell
--server-ref-wav "C:\path\to\reference.wav" `
--server-caption "若い女性の、明るく聞き取りやすい声で話してください。"
```

Record and compare:

- `summary.md`: median and max elapsed time.
- `results.jsonl`: per-file latency and audio metrics.
- `environment.json`: OS, CPU, Docker, and NVIDIA GPU hints.

## Adoption Gate

Treat Irodori-TTS-Server as a Windows NVIDIA TTS candidate only if:

- `short_b` warm cache-miss median is within 2-4 seconds.
- `medium_c` warm cache-miss median is within 5-9 seconds.
- Failed generations are 0-1 out of 5.
- no-ref voice quality is useful enough compared with VOICEVOX.
- ref-enabled mode either improves enough on CUDA or has a clear prewarm UX.

If these are not met, keep Irodori-TTS-Server behind advanced settings and leave VOICEVOX as the practical Windows default.

## UI Policy

Use labels that make the engines visibly different:

- `VOICEVOX`: standard, stable default.
- `Irodori MLX VoiceDesign`: macOS Apple Silicon, high-quality character voice candidate.
- `Irodori-TTS-Server for Windows NVIDIA`: Windows NVIDIA candidate, experimental elsewhere. Keep it behind setup guidance until Windows real-device listening confirms voice quality.

Do not label both Irodori paths as the same engine. They have different runtime requirements and different VoiceDesign compatibility risks.
