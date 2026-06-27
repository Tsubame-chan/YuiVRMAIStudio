# LLM External Information Policy

Last updated: 2026-06-17

This project gives the assistant external information through the backend, not through per-platform Unity code.
The same behavior should apply to iOS, Android, macOS, and Windows clients as long as they use the shared backend.

## Current implementation

- Normal chat uses the OpenAI Responses API with the hosted `web_search` tool when current information is likely needed.
- Realtime VOICEVOX uses OpenAI Realtime for speech-to-text, then uses the same Responses API path for text reply generation, so web search behavior matches normal chat.
- Realtime OpenAI voice mode still uses the Realtime API directly. Treat it as lower priority until the voice-mode architecture is revisited.

## Configuration

Environment variables:

- `OPENAI_WEB_SEARCH_ENABLED`: master flag. `false` disables hosted web search.
- `OPENAI_WEB_SEARCH_MODE`: `auto`, `always`, or `off`.
- `OPENAI_WEB_SEARCH_CONTEXT_SIZE`: `low`, `medium`, or `high`.
- `OPENAI_WEB_SEARCH_COUNTRY`: ISO country hint, default `JP`.
- `OPENAI_WEB_SEARCH_CITY`: optional city hint.
- `OPENAI_WEB_SEARCH_REGION`: optional region hint.
- `OPENAI_WEB_SEARCH_TIMEZONE`: timezone hint, default `Asia/Tokyo`.

Default policy:

- Use `auto` for daily use. The backend offers web search for weather, news, maps/place-like queries, prices, schedules, releases, and other current facts.
- Use `always` only for experiments where the model should be able to decide on every turn. This may increase latency and cost.
- Use `off` for offline/private demos or when a model/tool compatibility issue appears.

## Scope boundaries

Web search can answer many weather, place, opening-hours, news, and live-fact questions, but it is not a full replacement for dedicated APIs.

Add dedicated provider modules when the app needs:

- Stable structured weather data or alerts.
- Map routing, travel time, place IDs, or navigation handoff.
- Calendar, email, task, or personal-account access.
- Commerce, reservation, posting, or any real-world action.

Dedicated integrations should live in backend provider modules and expose shared API behavior to all Unity clients. Avoid platform-specific implementations unless the platform feature itself is native-only, such as camera capture or photo picker access.

Current dedicated API groundwork:

- `GET /external/weather/current?location=...` returns structured current
  weather via Open-Meteo. This is separate from OpenAI hosted web search and can
  be tested without an API key.
