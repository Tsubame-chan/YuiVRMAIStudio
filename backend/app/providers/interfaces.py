from abc import ABC, abstractmethod
from typing import Protocol

from app.models.chat import ChatRequest, ChatResponse
from app.models.stt import STTResponse
from app.models.tts import TTSRequest, TTSResponse
from app.models.vision import VisionResponse


class Provider(ABC):
    name: str


class ChatProvider(Provider):
    @abstractmethod
    async def generate(
        self,
        request: ChatRequest,
        history: list[dict[str, str]] | None = None,
    ) -> ChatResponse:
        raise NotImplementedError


class VisionProvider(Provider):
    @abstractmethod
    async def analyze_image(
        self,
        *,
        image_bytes: bytes,
        prompt_type: str,
        mime_type: str = "image/jpeg",
    ) -> VisionResponse:
        raise NotImplementedError


class TTSProvider(Provider):
    @abstractmethod
    async def synthesize(self, request: TTSRequest) -> TTSResponse:
        raise NotImplementedError


class STTProvider(Provider):
    @abstractmethod
    async def transcribe(self, *, audio_bytes: bytes, filename: str) -> STTResponse:
        raise NotImplementedError


class ProviderRouter(Protocol):
    def chat(self) -> ChatProvider:
        ...

    def vision(self) -> VisionProvider:
        ...

    def tts(self) -> TTSProvider:
        ...

    def stt(self) -> STTProvider:
        ...
