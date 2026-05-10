from pathlib import Path
from datetime import date
import logging

from fastapi import APIRouter, Depends, File, Form, HTTPException, UploadFile, WebSocket, WebSocketDisconnect, status
from fastapi.responses import FileResponse

from app.core.config import Settings, get_settings
from app.db.repositories import ChatRepository, MemoryRepository, UsageRepository
from app.db.sqlite import check_database
from app.models.chat import ChatRequest, ChatResponse, RecentConversationsResponse
from app.models.config import ConfigResponse
from app.models.health import HealthResponse
from app.models.memory import (
    MemoryItem,
    MemorySaveRequest,
    MemorySearchRequest,
    MemorySearchResponse,
)
from app.models.realtime import (
    RealtimeAudioResponse,
    RealtimeProbeRequest,
    RealtimeProbeResponse,
    RealtimeStatusResponse,
)
from app.models.stt import STTResponse
from app.models.tts import TTSRequest, TTSResponse
from app.models.usage import UsageLimits, UsageResponse
from app.models.vision import VisionResponse
from app.providers.openai_chat import ChatProviderError, ProviderConfigurationError
from app.providers.openai_realtime import RealtimeProvider, RealtimeProviderError
from app.providers.openai_stt import STTProviderError
from app.providers.openai_vision import VisionProviderError as OpenAIVisionProviderError
from app.providers.router import ProviderNotImplementedError, ProviderRouter
from app.providers.gemini_vision import VisionProviderError as GeminiVisionProviderError
from app.providers.voicevox_tts import TTSProviderError

router = APIRouter()
logger = logging.getLogger(__name__)


def get_chat_repository(settings: Settings = Depends(get_settings)) -> ChatRepository:
    return ChatRepository(settings.database_url)


def get_memory_repository(settings: Settings = Depends(get_settings)) -> MemoryRepository:
    return MemoryRepository(settings.database_url)


def get_usage_repository(settings: Settings = Depends(get_settings)) -> UsageRepository:
    return UsageRepository(settings.database_url)


@router.get("/health", response_model=HealthResponse)
def health(settings: Settings = Depends(get_settings)) -> HealthResponse:
    database_ok = check_database(settings.database_url)
    return HealthResponse(
        status="ok" if database_ok else "degraded",
        version=settings.app_version,
        database="ok" if database_ok else "error",
        providers={
            "openai_configured": bool(settings.openai_api_key),
            "gemini_configured": bool(settings.gemini_api_key),
            "chat_provider": settings.chat_provider,
            "vision_provider": settings.vision_provider,
            "tts_provider": settings.tts_provider,
            "stt_provider": settings.stt_provider,
            "voicevox_base_url": settings.voicevox_base_url,
            "lmstudio_base_url": settings.lmstudio_base_url,
        },
        features={
            "stable_chat": True,
            "voice_input": bool(settings.openai_api_key),
            "vision": bool(settings.openai_api_key or settings.gemini_api_key),
            "local_voicevox_tts": settings.tts_provider == "voicevox",
            "realtime": bool(settings.openai_api_key),
            "realtime_voicevox": bool(settings.openai_api_key),
            "app_awareness_context": False,
        },
    )


@router.get("/config", response_model=ConfigResponse)
def config(settings: Settings = Depends(get_settings)) -> ConfigResponse:
    return ConfigResponse(
        character_name=settings.character_name,
        chat_provider=settings.chat_provider,
        vision_provider=settings.vision_provider,
        tts_provider=settings.tts_provider,
        stt_provider=settings.stt_provider,
        default_user_id=settings.default_user_id,
        limits={
            "daily_chat": settings.daily_chat_limit,
            "daily_vision": settings.daily_vision_limit,
            "daily_stt_minutes": settings.daily_stt_minutes_limit,
            "daily_tts": settings.daily_tts_limit,
        },
    )


@router.get("/realtime/status", response_model=RealtimeStatusResponse)
def realtime_status(settings: Settings = Depends(get_settings)) -> RealtimeStatusResponse:
    return RealtimeProvider(settings).status()


@router.post("/realtime/probe", response_model=RealtimeProbeResponse)
async def realtime_probe(
    request: RealtimeProbeRequest,
    settings: Settings = Depends(get_settings),
) -> RealtimeProbeResponse:
    return await RealtimeProvider(settings).probe(request.mode, request.connect)


@router.post("/realtime/audio", response_model=RealtimeAudioResponse)
async def realtime_audio(
    audio: UploadFile = File(...),
    mode: str = Form("voice"),
    instructions: str = Form(""),
    settings: Settings = Depends(get_settings),
) -> RealtimeAudioResponse:
    provider = RealtimeProvider(settings)
    try:
        normalized_mode = "translate" if mode == "translate" else "voice"
        return await provider.respond_to_wav(
            await audio.read(),
            normalized_mode,  # type: ignore[arg-type]
            instructions=instructions,
        )
    except RealtimeProviderError as exc:
        raise HTTPException(
            status_code=status.HTTP_502_BAD_GATEWAY,
            detail=str(exc),
        ) from exc


@router.websocket("/realtime/stream")
async def realtime_stream(
    websocket: WebSocket,
    settings: Settings = Depends(get_settings),
    repository: ChatRepository = Depends(get_chat_repository),
    memory_repository: MemoryRepository = Depends(get_memory_repository),
) -> None:
    await websocket.accept()
    try:
        await RealtimeProvider(settings, repository, memory_repository).relay_unity_stream(websocket)
    except WebSocketDisconnect:
        return
    except Exception as exc:
        logger.exception("Realtime stream failed")
        try:
            await websocket.send_json({"type": "error", "message": str(exc)})
        except Exception:
            return


@router.post("/chat", response_model=ChatResponse)
async def chat(
    request: ChatRequest,
    settings: Settings = Depends(get_settings),
    repository: ChatRepository = Depends(get_chat_repository),
    memory_repository: MemoryRepository = Depends(get_memory_repository),
) -> ChatResponse:
    cached = None if request.secret else repository.get_cached_response(request.request_id)
    if cached is not None:
        return cached

    provider_router = ProviderRouter(settings)
    try:
        provider = provider_router.chat()
        history = [] if request.secret else repository.list_recent_messages(request.user_id)
        if not request.secret:
            if request.context.extra is None:
                request.context.extra = {}
            request.context.extra["memories"] = [
                item.model_dump()
                for item in _memory_context(
                    memory_repository=memory_repository,
                    user_id=request.user_id,
                    query=request.message,
                )
            ]
        response = await provider.generate(request, history=history)
    except ProviderConfigurationError as exc:
        raise HTTPException(
            status_code=status.HTTP_503_SERVICE_UNAVAILABLE,
            detail=str(exc),
        ) from exc
    except ProviderNotImplementedError as exc:
        raise HTTPException(
            status_code=status.HTTP_501_NOT_IMPLEMENTED,
            detail=str(exc),
        ) from exc
    except ChatProviderError as exc:
        raise HTTPException(
            status_code=status.HTTP_502_BAD_GATEWAY,
            detail="Chat provider request failed.",
        ) from exc

    if not request.secret:
        repository.save_chat_turn(
            request_id=request.request_id,
            user_id=request.user_id,
            user_message=request.message,
            response=response,
            provider=provider.name,
            model=settings.openai_chat_model,
        )
    if not request.secret and response.memory_action == "save":
        memory_repository.save(
            MemorySaveRequest(
                user_id=request.user_id,
                content=f"User said: {request.message}",
                importance=3,
                tags=["auto", "chat"],
            )
        )
    return response


def _memory_context(
    *,
    memory_repository: MemoryRepository,
    user_id: str,
    query: str,
) -> list[MemoryItem]:
    memories = memory_repository.search(
        MemorySearchRequest(user_id=user_id, query=query, limit=5)
    )
    if memories:
        return memories
    return memory_repository.list_recent(user_id=user_id, limit=5)


@router.get("/conversations/recent", response_model=RecentConversationsResponse)
def recent_conversations(
    user_id: str = "local_user",
    limit: int = 20,
    repository: ChatRepository = Depends(get_chat_repository),
) -> RecentConversationsResponse:
    limit = max(1, min(limit, 100))
    return RecentConversationsResponse(
        items=repository.list_recent_conversations(user_id=user_id, limit=limit)
    )


@router.delete("/conversations")
def clear_conversations(
    user_id: str = "local_user",
    repository: ChatRepository = Depends(get_chat_repository),
    memory_repository: MemoryRepository = Depends(get_memory_repository),
) -> dict[str, int | str]:
    deleted = repository.clear_user_cache(user_id=user_id)
    deleted["memories"] = memory_repository.clear_user_memories(user_id=user_id)
    return {"status": "ok", **deleted}


@router.post("/tts", response_model=TTSResponse)
async def tts(
    request: TTSRequest,
    settings: Settings = Depends(get_settings),
    usage_repository: UsageRepository = Depends(get_usage_repository),
) -> TTSResponse:
    provider_router = ProviderRouter(settings)
    try:
        provider = provider_router.tts()
        response = await provider.synthesize(request)
        usage_repository.log(
            request_id=request.request_id,
            user_id=settings.default_user_id,
            provider=provider.name,
            model=None,
            operation="tts",
            metadata={
                "speaker_id": request.speaker_id,
                "format": response.format,
                "speed_scale": request.speed_scale,
            },
        )
        return response
    except ProviderNotImplementedError as exc:
        raise HTTPException(
            status_code=status.HTTP_501_NOT_IMPLEMENTED,
            detail=str(exc),
        ) from exc
    except TTSProviderError as exc:
        raise HTTPException(
            status_code=status.HTTP_502_BAD_GATEWAY,
            detail="TTS provider request failed.",
        ) from exc


_AUDIO_DIR = (Path(__file__).resolve().parents[2] / "data" / "audio").resolve()


@router.post("/tts/audio")
async def tts_audio(
    request: TTSRequest,
    settings: Settings = Depends(get_settings),
    usage_repository: UsageRepository = Depends(get_usage_repository),
) -> FileResponse:
    provider_router = ProviderRouter(settings)
    try:
        provider = provider_router.tts()
        response = await provider.synthesize(request)
        usage_repository.log(
            request_id=request.request_id,
            user_id=settings.default_user_id,
            provider=provider.name,
            model=None,
            operation="tts",
            metadata={
                "speaker_id": request.speaker_id,
                "format": response.format,
                "speed_scale": request.speed_scale,
                "direct_audio": True,
            },
        )
        filename = response.audio_url.rsplit("/", 1)[-1]
        return _audio_file_response(filename)
    except ProviderNotImplementedError as exc:
        raise HTTPException(
            status_code=status.HTTP_501_NOT_IMPLEMENTED,
            detail=str(exc),
        ) from exc
    except TTSProviderError as exc:
        raise HTTPException(
            status_code=status.HTTP_502_BAD_GATEWAY,
            detail="TTS provider request failed.",
        ) from exc


@router.get("/audio/{filename}")
def audio(filename: str) -> FileResponse:
    return _audio_file_response(filename)


def _audio_file_response(filename: str) -> FileResponse:
    # Reject obvious traversal patterns and any path separator before touching the filesystem.
    if (
        not filename
        or "/" in filename
        or "\\" in filename
        or ".." in filename
        or "\x00" in filename
        or filename.startswith(".")
    ):
        raise HTTPException(status_code=status.HTTP_404_NOT_FOUND, detail="Audio not found.")

    # VOICEVOX is the only Phase 2 TTS writer, so `/audio` intentionally serves
    # WAV only. If a future cloud TTS adds MP3/OGG output, relax this allow-list
    # together with the provider writer and returned media_type.
    if not filename.lower().endswith(".wav"):
        raise HTTPException(status_code=status.HTTP_404_NOT_FOUND, detail="Audio not found.")

    candidate = (_AUDIO_DIR / filename).resolve()
    # `is_relative_to` ensures the resolved path is contained inside the audio dir.
    if not candidate.is_relative_to(_AUDIO_DIR) or not candidate.is_file():
        raise HTTPException(status_code=status.HTTP_404_NOT_FOUND, detail="Audio not found.")

    return FileResponse(candidate, media_type="audio/wav", filename=filename)


@router.post("/stt", response_model=STTResponse)
async def stt(
    audio: UploadFile = File(...),
    duration_ms: int | None = Form(default=None),
    settings: Settings = Depends(get_settings),
    usage_repository: UsageRepository = Depends(get_usage_repository),
) -> STTResponse:
    audio_bytes = await audio.read()
    if not audio_bytes:
        raise HTTPException(status_code=status.HTTP_400_BAD_REQUEST, detail="Audio file is empty.")
    if len(audio_bytes) > 25 * 1024 * 1024:
        raise HTTPException(
            status_code=status.HTTP_413_REQUEST_ENTITY_TOO_LARGE,
            detail="Audio file is larger than 25 MB.",
        )

    provider_router = ProviderRouter(settings)
    try:
        provider = provider_router.stt()
        response = await provider.transcribe(
            audio_bytes=audio_bytes,
            filename=audio.filename or "audio.wav",
        )
        usage_repository.log(
            request_id=None,
            user_id=settings.default_user_id,
            provider=provider.name,
            model=settings.openai_transcribe_model,
            operation="stt",
            metadata={
                "filename": audio.filename,
                "content_type": audio.content_type,
                "bytes": len(audio_bytes),
                "duration_ms": duration_ms,
            },
        )
        return response
    except ProviderNotImplementedError as exc:
        raise HTTPException(
            status_code=status.HTTP_501_NOT_IMPLEMENTED,
            detail=str(exc),
        ) from exc
    except STTProviderError as exc:
        raise HTTPException(
            status_code=status.HTTP_502_BAD_GATEWAY,
            detail="STT provider request failed.",
        ) from exc


@router.post("/vision", response_model=VisionResponse)
async def vision(
    image: UploadFile = File(...),
    prompt_type: str = Form(default="screen"),
    settings: Settings = Depends(get_settings),
    usage_repository: UsageRepository = Depends(get_usage_repository),
) -> VisionResponse:
    image_bytes = await image.read()
    if not image_bytes:
        raise HTTPException(status_code=status.HTTP_400_BAD_REQUEST, detail="Image file is empty.")
    if len(image_bytes) > 20 * 1024 * 1024:
        raise HTTPException(
            status_code=status.HTTP_413_REQUEST_ENTITY_TOO_LARGE,
            detail="Image file is larger than 20 MB.",
        )

    content_type = image.content_type or "image/jpeg"
    if content_type not in {"image/jpeg", "image/png", "image/webp", "image/heic", "image/heif"}:
        raise HTTPException(
            status_code=status.HTTP_415_UNSUPPORTED_MEDIA_TYPE,
            detail=f"Unsupported image content type: {content_type}",
        )

    provider_router = ProviderRouter(settings)
    try:
        provider = provider_router.vision()
        response = await provider.analyze_image(
            image_bytes=image_bytes,
            prompt_type=prompt_type,
            mime_type=content_type,
        )
        usage_repository.log(
            request_id=response.vision_result_id,
            user_id=settings.default_user_id,
            provider=provider.name,
            model=(
                settings.openai_vision_model
                if provider.name == "openai"
                else settings.gemini_vision_model
            ),
            operation="vision",
            metadata={
                "filename": image.filename,
                "content_type": content_type,
                "bytes": len(image_bytes),
                "prompt_type": prompt_type,
            },
        )
        return response
    except ProviderNotImplementedError as exc:
        raise HTTPException(
            status_code=status.HTTP_501_NOT_IMPLEMENTED,
            detail=str(exc),
        ) from exc
    except (GeminiVisionProviderError, OpenAIVisionProviderError) as exc:
        logger.exception(
            "Vision provider request failed. filename=%s content_type=%s bytes=%s prompt_type=%s",
            image.filename,
            content_type,
            len(image_bytes),
            prompt_type,
        )
        raise HTTPException(
            status_code=status.HTTP_502_BAD_GATEWAY,
            detail=f"Vision provider request failed: {_safe_error_detail(exc)}",
        ) from exc


def _safe_error_detail(exc: Exception, max_length: int = 300) -> str:
    detail = str(exc).strip() or exc.__class__.__name__
    detail = detail.replace("\r", " ").replace("\n", " ")
    return detail[:max_length]


@router.post("/memory/save", response_model=MemoryItem)
def memory_save(
    request: MemorySaveRequest,
    repository: MemoryRepository = Depends(get_memory_repository),
) -> MemoryItem:
    return repository.save(request)


@router.post("/memory/search", response_model=MemorySearchResponse)
def memory_search(
    request: MemorySearchRequest,
    repository: MemoryRepository = Depends(get_memory_repository),
) -> MemorySearchResponse:
    return MemorySearchResponse(items=repository.search(request))


@router.get("/usage", response_model=UsageResponse)
def usage(
    user_id: str | None = None,
    settings: Settings = Depends(get_settings),
    repository: UsageRepository = Depends(get_usage_repository),
) -> UsageResponse:
    return repository.daily_usage(
        target_date=date.today(),
        user_id=user_id,
        limits=UsageLimits(
            chat_count=settings.daily_chat_limit,
            vision_count=settings.daily_vision_limit,
            stt_minutes=settings.daily_stt_minutes_limit,
            tts_count=settings.daily_tts_limit,
        ),
    )
