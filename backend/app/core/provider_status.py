import logging
from typing import Any

import httpx

from app.core.config import Settings
from app.models.provider_status import (
    ProviderStatusItem,
    ProviderStatusResponse,
    SystemStatusItem,
)


logger = logging.getLogger(__name__)


def build_provider_status(
    settings: Settings,
    *,
    database_ok: bool,
    voicevox_status: dict[str, Any],
) -> ProviderStatusResponse:
    backend_status = "ok" if database_ok else "degraded"
    return ProviderStatusResponse(
        status=backend_status,
        backend=SystemStatusItem(status=backend_status),
        database=SystemStatusItem(
            status="ok" if database_ok else "error",
            detail="" if database_ok else "SQLite health check failed.",
        ),
        providers={
            "openai": ProviderStatusItem(
                status="configured" if settings.openai_api_key else "missing_key",
                category="cloud_ai",
                requires_api_key=True,
                chat_model=settings.openai_chat_model,
                vision_model=settings.openai_vision_model,
                stt_model=settings.openai_transcribe_model,
                realtime_model=settings.openai_realtime_model,
            ),
            "gemini": ProviderStatusItem(
                status="configured" if settings.gemini_api_key else "missing_key",
                category="cloud_vision",
                requires_api_key=True,
                vision_model=settings.gemini_vision_model,
            ),
            "voicevox": ProviderStatusItem(
                status=str(voicevox_status.get("status", "unknown")),
                detail=str(voicevox_status.get("detail", "")),
                category="local_tts",
                is_local=True,
                base_url=settings.voicevox_base_url,
                version=str(voicevox_status.get("version", "")),
                speakers=voicevox_status.get("speakers"),
            ),
            "lmstudio": ProviderStatusItem(
                status="not_implemented",
                detail="Local chat provider is planned but not available in this build.",
                category="local_chat",
                is_local=True,
                base_url=settings.lmstudio_base_url,
            ),
        },
    )


async def probe_voicevox(settings: Settings) -> dict[str, Any]:
    if settings.tts_provider != "voicevox":
        return {
            "status": "disabled",
            "detail": f"TTS provider is set to {settings.tts_provider}.",
        }

    try:
        async with httpx.AsyncClient(base_url=settings.voicevox_base_url, timeout=2.0) as client:
            version_response = await client.get("/version")
            version_response.raise_for_status()
            speakers_response = await client.get("/speakers")
            speakers = None
            if speakers_response.status_code == 200:
                payload = speakers_response.json()
                if isinstance(payload, list):
                    speakers = len(payload)
            return {
                "status": "ok",
                "version": str(version_response.json()).strip('"'),
                "speakers": speakers,
            }
    except httpx.HTTPError as exc:
        logger.info("VOICEVOX provider status probe failed: %s", exc)
        return {
            "status": "offline",
            "detail": "VOICEVOX Engine is not reachable.",
        }
