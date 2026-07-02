from functools import lru_cache

from app.core.config import Settings
from app.providers.gemini_vision import GeminiVisionProvider
from app.providers.http_tts import HttpTTSProvider
from app.providers.lmstudio_chat import LMStudioChatProvider
from app.providers.openai_chat import OpenAIChatProvider
from app.providers.openai_stt import OpenAISTTProvider
from app.providers.openai_vision import OpenAIVisionProvider
from app.providers.voicevox_tts import AivisSpeechProvider, VoiceVoxProvider
from app.providers.xai_chat import XAIChatProvider


class ProviderNotImplementedError(NotImplementedError):
    pass


@lru_cache(maxsize=8)
def _openai_chat_provider(settings: Settings) -> OpenAIChatProvider:
    return OpenAIChatProvider(settings)


@lru_cache(maxsize=8)
def _lmstudio_chat_provider(settings: Settings) -> LMStudioChatProvider:
    return LMStudioChatProvider(settings)


@lru_cache(maxsize=8)
def _xai_chat_provider(settings: Settings) -> XAIChatProvider:
    return XAIChatProvider(settings)


@lru_cache(maxsize=8)
def _openai_vision_provider(settings: Settings) -> OpenAIVisionProvider:
    return OpenAIVisionProvider(settings)


@lru_cache(maxsize=8)
def _gemini_vision_provider(settings: Settings) -> GeminiVisionProvider:
    return GeminiVisionProvider(settings)


@lru_cache(maxsize=8)
def _voicevox_tts_provider(settings: Settings) -> VoiceVoxProvider:
    return VoiceVoxProvider(settings)


@lru_cache(maxsize=8)
def _aivis_tts_provider(settings: Settings) -> AivisSpeechProvider:
    return AivisSpeechProvider(settings)


@lru_cache(maxsize=8)
def _http_tts_provider(settings: Settings) -> HttpTTSProvider:
    return HttpTTSProvider(settings)


@lru_cache(maxsize=8)
def _openai_stt_provider(settings: Settings) -> OpenAISTTProvider:
    return OpenAISTTProvider(settings)


class ProviderRouter:
    def __init__(self, settings: Settings):
        self.settings = settings

    def chat(self):
        if self.settings.chat_provider == "openai":
            return _openai_chat_provider(self.settings)
        if self.settings.chat_provider == "lmstudio":
            return _lmstudio_chat_provider(self.settings)
        if self.settings.chat_provider == "litert_lm":
            return _lmstudio_chat_provider(self.settings)
        if self.settings.chat_provider == "xai":
            return _xai_chat_provider(self.settings)
        raise ProviderNotImplementedError(
            f"Chat provider is not implemented: {self.settings.chat_provider}"
        )

    def vision(self):
        if self.settings.vision_provider == "openai":
            return _openai_vision_provider(self.settings)
        if self.settings.vision_provider == "gemini":
            return _gemini_vision_provider(self.settings)
        raise ProviderNotImplementedError(
            f"Vision provider is not implemented: {self.settings.vision_provider}"
        )

    def tts(self, provider: str | None = None):
        selected_provider = (provider or self.settings.tts_provider).strip().lower()
        if selected_provider == "voicevox":
            return _voicevox_tts_provider(self.settings)
        if selected_provider == "aivis":
            return _aivis_tts_provider(self.settings)
        if selected_provider == "http":
            return _http_tts_provider(self.settings)
        raise ProviderNotImplementedError(
            f"TTS provider is not implemented: {selected_provider}"
        )

    def stt(self):
        if self.settings.stt_provider == "openai":
            return _openai_stt_provider(self.settings)
        raise ProviderNotImplementedError(
            f"STT provider is not implemented: {self.settings.stt_provider}"
        )
