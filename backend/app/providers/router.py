from functools import lru_cache

from app.core.config import Settings
from app.core.capabilities import provider_options
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
        return self._resolve("chat", self.settings.chat_provider)

    def vision(self):
        return self._resolve("vision", self.settings.vision_provider)

    def tts(self, provider: str | None = None):
        selected_provider = (provider or self.settings.tts_provider).strip().lower()
        return self._resolve("tts", selected_provider)

    def stt(self):
        return self._resolve("stt", self.settings.stt_provider)

    def _resolve(self, capability: str, selected: str):
        factories = {
            ("chat", "openai"): _openai_chat_provider, ("chat", "lmstudio"): _lmstudio_chat_provider,
            ("chat", "litert_lm"): _lmstudio_chat_provider, ("chat", "xai"): _xai_chat_provider,
            ("vision", "openai"): _openai_vision_provider, ("vision", "gemini"): _gemini_vision_provider,
            ("tts", "voicevox"): _voicevox_tts_provider, ("tts", "aivis"): _aivis_tts_provider,
            ("tts", "http"): _http_tts_provider, ("stt", "openai"): _openai_stt_provider,
        }
        factory = factories.get((capability, selected))
        if selected not in provider_options(capability) or factory is None:
            raise ProviderNotImplementedError(f"{capability} provider is not implemented: {selected}")
        return factory(self.settings)
