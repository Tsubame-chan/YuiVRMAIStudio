"""Declared capabilities shared by runtime routing and the management UI.

Add an adapter and its descriptor here together. A URL or an API key alone is
configuration, never proof that a provider can actually complete a request.
"""
from dataclasses import dataclass, asdict


@dataclass(frozen=True)
class ProviderDefinition:
    id: str
    label: str
    description: str
    capabilities: tuple[str, ...]
    prefix: str
    credential: str = ""
    endpoint: str = ""
    local: bool = False


PROVIDERS = (
    ProviderDefinition("openai", "OpenAI", "会話・画像・音声認識・リアルタイム音声をまとめて。", ("chat", "vision", "stt", "realtime"), "openai_", "openai_api_key"),
    ProviderDefinition("lmstudio", "LM Studio", "PCで動かすモデルをOpenAI互換APIで接続。", ("chat",), "lmstudio_", endpoint="lmstudio_base_url", local=True),
    ProviderDefinition("litert_lm", "LiteRT-LM", "別プロセスのGemmaサーバーを接続。", ("chat",), "litert_lm_", endpoint="litert_lm_base_url", local=True),
    ProviderDefinition("xai", "xAI", "Grokを会話に使用。", ("chat",), "xai_", "xai_api_key", "xai_base_url"),
    ProviderDefinition("voicevox", "VOICEVOX", "PCのVOICEVOX Engineで日本語の声を合成。", ("tts",), "voicevox_", endpoint="voicevox_base_url", local=True),
    ProviderDefinition("aivis", "AivisSpeech", "導入済みのAivisSpeech Engineで声を選ぶ。", ("tts",), "aivis_", endpoint="aivis_base_url", local=True),
    ProviderDefinition("http", "外部TTS", "Irodori・Kokoroなど、対応HTTPサーバーを接続。", ("tts",), "http_tts_", "http_tts_api_key", "http_tts_base_url"),
    ProviderDefinition("gemini", "Gemini", "画像を読み取り、内容を理解。", ("vision",), "gemini_", "gemini_api_key"),
)
PROVIDER_BY_ID = {p.id: p for p in PROVIDERS}
CAPABILITIES = {"chat": "会話", "tts": "読み上げ", "stt": "音声認識", "vision": "画像", "realtime": "Realtime"}


def provider_options(capability: str) -> list[str]:
    return [p.id for p in PROVIDERS if capability in p.capabilities]


def provider_catalog() -> list[dict]:
    return [asdict(p) for p in PROVIDERS]
