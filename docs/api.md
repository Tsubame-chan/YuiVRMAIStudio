# Backend API Contract

FastAPI generates the source-of-truth schema at `/openapi.json`.

## Phase 1 Endpoints

- `GET /health`: Backend liveness, SQLite availability, and local configuration presence checks. This endpoint does not call OpenAI, Gemini, VOICEVOX, HTTP TTS, or any paid provider.
- `GET /config`: Runtime configuration for the Unity client, excluding secrets.
  It also exposes `chat_providers`, `vision_providers`, `tts_providers`, and
  `stt_providers`, the provider options that the client can offer without
  exposing provider secrets.
- `GET /providers/status`: User-facing provider diagnostics for onboarding and
  help screens. This endpoint reports backend/database state, cloud provider key
  presence, local provider URLs, and a low-timeout local VOICEVOX probe. It does
  not call paid cloud APIs.

`/providers/status` is the preferred endpoint for UI troubleshooting because it
can distinguish `missing_key`, `configured`, `ok`, `offline`, `disabled`, and
`not_implemented` provider states without exposing secrets.

## Phase 2 Endpoints

- `POST /chat`: Generates a structured character response through the configured chat provider. The current implementation supports OpenAI Responses API Structured Outputs, an experimental LM Studio OpenAI-compatible local chat provider, and an experimental Grok / xAI chat provider.

`/chat` stores recent turns in SQLite and uses `request_id` as an idempotency key. If the same `request_id` is sent again, the cached response is returned without a second provider call.

`mode=standard` is the low-latency Talk path and uses `OPENAI_MAX_OUTPUT_TOKENS` (420 by default). `mode=work` uses `OPENAI_WORK_MAX_OUTPUT_TOKENS` (2200 by default) for a complete on-screen result. `ChatResponse.text` is the display result; `spoken_text` is the shorter speech payload. In Work mode the provider is instructed to keep `spoken_text` to one or two sentences so TTS latency does not grow with the document-sized result.

When `OPENAI_API_KEY` is missing, `/chat` returns `503` and does not call OpenAI.

Set `CHAT_PROVIDER=lmstudio` to use a local LM Studio server through
`LMSTUDIO_BASE_URL` and `LMSTUDIO_CHAT_MODEL`. The default base URL is
`http://127.0.0.1:1234/v1`, matching LM Studio's OpenAI-compatible server path.
LM Studio responses are normalized into the same `ChatResponse` shape as OpenAI;
plain text is accepted, but JSON matching the chat schema gives better face,
animation, memory, and TTS control.

Set `CHAT_PROVIDER=xai` and `XAI_API_KEY` to use Grok / xAI chat completions.
The default `XAI_BASE_URL` is `https://api.x.ai/v1`, and the backend posts to
`/chat/completions`. Configure `XAI_CHAT_MODEL` to choose the Grok model.

`/chat` also injects up to five local memories into the model context. If the structured chat response returns `memory_action=save`, the backend stores the user's latest message as an automatic memory tagged `auto` and `chat`.

OpenAI chat can use hosted web search for current information when enabled by backend settings. Realtime VOICEVOX uses the same text-generation path after STT. See `docs/LLM_EXTERNAL_INFO.md`.

## Phase 3 Endpoints

- `POST /tts`: Generates speech audio through the configured TTS provider. The default provider is VOICEVOX Engine.
- `GET /audio/{filename}`: Serves generated TTS audio files to Unity. The allow-list currently supports `wav`, `mp3`, and `ogg`.

Generated audio files are runtime cache files under `backend/data/audio`. The VOICEVOX cache is bounded by `TTS_AUDIO_CACHE_MAX_FILES`, `TTS_AUDIO_CACHE_MAX_MB`, and `TTS_AUDIO_CACHE_MAX_AGE_HOURS`; see `docs/PROJECT_AUDIT_20260617.md`.
- `POST /memory/save`: Stores a simple local memory in SQLite.
- `POST /memory/search`: Searches local memories with a simple SQLite LIKE query.
- `GET /usage`: Returns today's local usage counts.
- `GET /external/weather/current?location=Tokyo`: Resolves a place name through
  Open-Meteo geocoding, then returns current structured weather data. Open-Meteo
  does not require an API key. Configure `OPEN_METEO_GEOCODING_BASE_URL` and
  `OPEN_METEO_FORECAST_BASE_URL` only when using a proxy or test service.

When the selected TTS provider is unavailable or misconfigured, `/tts` returns `502`.

`/tts` accepts optional `speed_scale` from `0.5` to `2.0`. The current default VOICEVOX talk style is 冥鳴ひまり / ノーマル (`speaker_id = 14`) with `speed_scale = 1.0`, and the Unity chat scene sends those values.

Set `TTS_PROVIDER=http` to use the experimental generic HTTP TTS adapter. Configure `HTTP_TTS_BASE_URL`, `HTTP_TTS_ENDPOINT`, `HTTP_TTS_PROVIDER_ID`, `HTTP_TTS_VOICE`, `HTTP_TTS_MODEL`, and `HTTP_TTS_FORMAT` for services such as Irodori TTS or future JSON-in/audio-out TTS backends. `HTTP_TTS_PAYLOAD_FORMAT=generic` sends Yui's generic JSON payload. `HTTP_TTS_PAYLOAD_FORMAT=openai_speech` sends an OpenAI-compatible speech payload with `model`, `input`, `voice`, `speed`, `pitch`, and `response_format`, which matches the local Irodori / MLX Audio `/v1/audio/speech` endpoint. `HTTP_TTS_PAYLOAD_FORMAT=irodori_openai_speech` targets Irodori-TTS-Server and sends an OpenAI-style request with an `irodori` options object for `num_steps`, `seed`, CFG values, and punctuation chunking controls. For Irodori voice design, set `HTTP_TTS_GENDER`, `HTTP_TTS_INSTRUCT`, and `HTTP_TTS_LANG_CODE`; for example `female`, a short Japanese voice-style instruction, and `ja`. For Irodori quality, Yui keeps the voice-design instruction separate from continuous slider controls. Set `HTTP_TTS_AUDIO_PROCESSOR=soundstretch` and optionally `HTTP_TTS_SOUNDSTRETCH_PATH` to apply Speed/Pitch as WAV post-processing through SoundTouch's `soundstretch` utility. `/providers/status` reports whether this adapter is configured. If `HTTP_TTS_HEALTH_ENDPOINT` is set, it also performs a low-timeout GET probe against that endpoint; otherwise it reports `configured` with `not probed`.

For Irodori-TTS-Server validation, keep `HTTP_TTS_IRODORI_CHUNKING_ENABLED=false` for normal short conversation. Treat it as a Windows NVIDIA candidate until real-device quality and latency are verified; do not expose a no-ref low-step profile as a user-facing replacement for the macOS MLX VoiceDesign path.

`POST /tts` and `POST /tts/audio` also accept an optional request-level
`provider` field. When omitted, the backend uses `TTS_PROVIDER`. When set to
`voicevox` or `http`, that provider is used for that request only. This lets the
Unity client test VOICEVOX and external HTTP TTS side by side without restarting
the backend.

When the default TTS provider fails and the request did not explicitly select a
provider, the backend falls back to `TTS_FALLBACK_PROVIDER`. The default fallback
is `voicevox`, which keeps experimental HTTP TTS backends from breaking ordinary
speech playback when VOICEVOX is available.

## Phase 5 Endpoints

- `POST /stt`: Accepts `multipart/form-data` with an `audio` file, optional `duration_ms`, and returns transcribed text.

The current implementation supports OpenAI Transcriptions API via `OPENAI_TRANSCRIBE_MODEL`. The default model is `gpt-4o-mini-transcribe`. Audio uploads are limited to 25 MB and are logged as `stt` usage; `duration_ms` is used to report `stt_minutes` in `/usage`.

## Phase 7 Endpoints

- `POST /vision`: Accepts `multipart/form-data` with an `image` file and optional `prompt_type` (`screen`, `camera`, or `general`), then returns a short Japanese visual summary and structured object hints.

The current default uses OpenAI Vision through `OPENAI_VISION_MODEL` and `VISION_PROVIDER=openai`. Gemini Vision code may exist in older development builds, but it is not part of the current provider roadmap. `/health` only reports whether provider keys are configured; it does not call OpenAI, Gemini, VOICEVOX, HTTP TTS, or any paid provider.

## Regenerating `openapi.json`

From the repository root:

```powershell
py -3.12 -m venv backend\.venv
.\backend\.venv\Scripts\Activate.ps1
python -m pip install -r backend\requirements.txt
python scripts\generate_openapi.py
```

Whenever Pydantic request or response models change, regenerate `openapi.json` and review the diff before updating Unity DTOs.
