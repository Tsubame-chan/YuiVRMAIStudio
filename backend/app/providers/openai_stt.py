import io

from fastapi.concurrency import run_in_threadpool
from openai import OpenAI, OpenAIError

from app.core.config import Settings
from app.models.stt import STTResponse
from app.providers.interfaces import STTProvider


class STTProviderError(RuntimeError):
    pass


class OpenAISTTProvider(STTProvider):
    name = "openai"

    def __init__(self, settings: Settings):
        if not settings.openai_api_key:
            raise STTProviderError("OPENAI_API_KEY is not configured.")
        if not settings.openai_transcribe_model:
            raise STTProviderError("OPENAI_TRANSCRIBE_MODEL is not configured.")
        self.settings = settings
        self.client = OpenAI(api_key=settings.openai_api_key)

    async def transcribe(self, *, audio_bytes: bytes, filename: str) -> STTResponse:
        try:
            text = await run_in_threadpool(self._transcribe_sync, audio_bytes, filename)
            return STTResponse(text=text.strip(), confidence=None)
        except OpenAIError as exc:
            raise STTProviderError(str(exc)) from exc
        except Exception as exc:
            raise STTProviderError(str(exc)) from exc

    def _transcribe_sync(self, audio_bytes: bytes, filename: str) -> str:
        audio_file = io.BytesIO(audio_bytes)
        audio_file.name = filename or "audio.wav"
        transcription = self.client.audio.transcriptions.create(
            model=self.settings.openai_transcribe_model,
            file=audio_file,
            language="ja",
            response_format="json",
        )
        return getattr(transcription, "text", "") or ""
