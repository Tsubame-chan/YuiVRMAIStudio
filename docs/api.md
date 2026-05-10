# Backend API Contract

FastAPI generates the source-of-truth schema at `/openapi.json`.

## Phase 1 Endpoints

- `GET /health`: Backend liveness, SQLite availability, and local configuration presence checks. This endpoint does not call OpenAI, Gemini, VOICEVOX, or any paid provider.
- `GET /config`: Runtime configuration for the Unity client, excluding secrets.

## Phase 2 Endpoints

- `POST /chat`: Generates a structured character response through the configured chat provider. The current implementation supports OpenAI Responses API Structured Outputs.

`/chat` stores recent turns in SQLite and uses `request_id` as an idempotency key. If the same `request_id` is sent again, the cached response is returned without a second provider call.

When `OPENAI_API_KEY` is missing, `/chat` returns `503` and does not call OpenAI.

`/chat` also injects up to five local memories into the model context. If the structured chat response returns `memory_action=save`, the backend stores the user's latest message as an automatic memory tagged `auto` and `chat`.

## Phase 3 Endpoints

- `POST /tts`: Generates WAV audio through VOICEVOX Engine.
- `GET /audio/{filename}`: Serves generated WAV files to Unity.
- `POST /memory/save`: Stores a simple local memory in SQLite.
- `POST /memory/search`: Searches local memories with a simple SQLite LIKE query.
- `GET /usage`: Returns today's local usage counts.

When VOICEVOX Engine is not running, `/tts` returns `502`.

`/tts` accepts optional `speed_scale` from `0.5` to `2.0`. The current default VOICEVOX talk style is 冥鳴ひまり / ノーマル (`speaker_id = 14`) with `speed_scale = 1.0`, and the Unity chat scene sends those values.

## Phase 5 Endpoints

- `POST /stt`: Accepts `multipart/form-data` with an `audio` file, optional `duration_ms`, and returns transcribed text.

The current implementation supports OpenAI Transcriptions API via `OPENAI_TRANSCRIBE_MODEL`. The default model is `gpt-4o-mini-transcribe`. Audio uploads are limited to 25 MB and are logged as `stt` usage; `duration_ms` is used to report `stt_minutes` in `/usage`.

## Phase 7 Endpoints

- `POST /vision`: Accepts `multipart/form-data` with an `image` file and optional `prompt_type` (`screen`, `camera`, or `general`), then returns a short Japanese visual summary and structured object hints.

The current default uses OpenAI Vision through `OPENAI_VISION_MODEL` and `VISION_PROVIDER=openai`. Gemini Vision remains available as a fallback through `GEMINI_API_KEY`, `GEMINI_VISION_MODEL`, and `VISION_PROVIDER=gemini`. `/health` only reports whether provider keys are configured; it does not call OpenAI, Gemini, VOICEVOX, or any paid provider.

## Regenerating `openapi.json`

From the repository root:

```powershell
py -3.12 -m venv backend\.venv
.\backend\.venv\Scripts\Activate.ps1
python -m pip install -r backend\requirements.txt
python scripts\generate_openapi.py
```

Whenever Pydantic request or response models change, regenerate `openapi.json` and review the diff before updating Unity DTOs.
