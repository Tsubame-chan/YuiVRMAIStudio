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
    http_tts_status: dict[str, Any] | None = None,
) -> ProviderStatusResponse:
    backend_status = "ok" if database_ok else "degraded"
    http_tts_status = http_tts_status or {
        "status": "configured" if settings.http_tts_base_url else "not_configured",
        "detail": "not probed" if settings.http_tts_base_url else "",
    }
    http_tts_detail_parts = []
    if settings.http_tts_provider_id:
        http_tts_detail_parts.append(f"provider={settings.http_tts_provider_id}")
    if http_tts_status.get("detail"):
        http_tts_detail_parts.append(str(http_tts_status.get("detail")))
    http_tts_engine = _http_tts_engine(settings)
    if http_tts_engine == "irodori_server":
        http_tts_detail_parts.extend(
            (
                f"num_steps={settings.http_tts_irodori_num_steps or 'default'}",
                f"chunking={str(settings.http_tts_irodori_chunking_enabled).lower()}",
            )
        )
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
            "xai": ProviderStatusItem(
                status="configured" if settings.xai_api_key else "missing_key",
                category="cloud_ai",
                requires_api_key=True,
                chat_model=settings.xai_chat_model,
                base_url=settings.xai_base_url,
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
            "http_tts": ProviderStatusItem(
                status=str(http_tts_status.get("status", "unknown")),
                detail="; ".join(http_tts_detail_parts),
                category="external_tts",
                requires_api_key=bool(settings.http_tts_api_key),
                base_url=settings.http_tts_base_url,
                engine=http_tts_engine,
                recommendation=_http_tts_recommendation(http_tts_engine),
            ),
            "lmstudio": ProviderStatusItem(
                status="configured" if settings.lmstudio_base_url else "not_configured",
                detail="OpenAI-compatible local chat endpoint.",
                category="local_chat",
                is_local=True,
                base_url=settings.lmstudio_base_url,
                chat_model=settings.lmstudio_chat_model,
            ),
        },
    )


def _http_tts_engine(settings: Settings) -> str:
    provider_key = " ".join(
        (
            settings.http_tts_provider_id,
            settings.http_tts_model,
            settings.http_tts_base_url,
        )
    ).lower()
    if settings.http_tts_payload_format == "irodori_openai_speech":
        return "irodori_server"
    if settings.http_tts_payload_format == "openai_speech" and "irodori" in provider_key:
        return "irodori_mlx"
    if settings.http_tts_base_url:
        return "http_tts"
    return ""


def _http_tts_recommendation(engine: str) -> str:
    if engine == "irodori_server":
        return "Windows NVIDIA candidate; verify model quality before exposing broadly."
    if engine == "irodori_mlx":
        return "macOS Apple Silicon candidate; keep VOICEVOX as default."
    if engine == "http_tts":
        return "External TTS adapter; verify latency, quality, and fallback behavior."
    return ""


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


async def probe_http_tts(settings: Settings) -> dict[str, Any]:
    if not settings.http_tts_base_url:
        return {"status": "not_configured", "detail": ""}
    if settings.tts_provider != "http":
        return {
            "status": "configured",
            "detail": f"TTS provider is set to {settings.tts_provider}.",
        }
    if not settings.http_tts_health_endpoint:
        return {"status": "configured", "detail": "not probed"}

    endpoint = settings.http_tts_health_endpoint.strip()
    endpoint = endpoint if endpoint.startswith("/") else f"/{endpoint}"
    try:
        async with httpx.AsyncClient(base_url=settings.http_tts_base_url, timeout=2.0) as client:
            response = await client.get(endpoint)
            response.raise_for_status()
            return {"status": "ok", "detail": endpoint}
    except httpx.HTTPError as exc:
        logger.info("HTTP TTS provider status probe failed: %s", exc)
        return {"status": "offline", "detail": "HTTP TTS service is not reachable."}
