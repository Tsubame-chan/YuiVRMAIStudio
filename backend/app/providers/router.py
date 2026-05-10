from functools import lru_cache

from app.core.config import Settings
from app.providers.gemini_vision import GeminiVisionProvider
from app.providers.openai_chat import OpenAIChatProvider
from app.providers.openai_stt import OpenAISTTProvider
from app.providers.openai_vision import OpenAIVisionProvider
from app.providers.voicevox_tts import VoiceVoxProvider


class ProviderNotImplementedError(NotImplementedError):
    pass


@lru_cache(maxsize=8)
def _openai_chat_provider(settings: Settings) -> OpenAIChatProvider:
    return OpenAIChatProvider(settings)


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
def _openai_stt_provider(settings: Settings) -> OpenAISTTProvider:
    return OpenAISTTProvider(settings)


class ProviderRouter:
    def __init__(self, settings: Settings):
        self.settings = settings

    def chat(self):
        if self.settings.chat_provider == "openai":
            return _openai_chat_provider(self.settings)
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

    def tts(self):
        if self.settings.tts_provider == "voicevox":
            return _voicevox_tts_provider(self.settings)
        raise ProviderNotImplementedError(
            f"TTS provider is not implemented: {self.settings.tts_provider}"
        )

    def stt(self):
        if self.settings.stt_provider == "openai":
            return _openai_stt_provider(self.settings)
        raise ProviderNotImplementedError(
            f"STT provider is not implemented: {self.settings.stt_provider}"
        )
