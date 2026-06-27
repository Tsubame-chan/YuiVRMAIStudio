from functools import lru_cache
from pathlib import Path

from pydantic import field_validator
from pydantic_settings import BaseSettings, SettingsConfigDict

from app.core.animation_catalog import (
    available_animations as catalog_available_animations,
    available_faces as catalog_available_faces,
)


ROOT_DIR = Path(__file__).resolve().parents[3]


def _parse_csv(value: str | tuple[str, ...] | list[str]) -> tuple[str, ...]:
    if isinstance(value, tuple):
        return value
    if isinstance(value, list):
        return tuple(str(item).strip() for item in value if str(item).strip())
    if not isinstance(value, str) or value.strip() == "":
        return ()
    return tuple(item.strip() for item in value.split(",") if item.strip())


class Settings(BaseSettings):
    model_config = SettingsConfigDict(
        env_file=ROOT_DIR / ".env",
        env_file_encoding="utf-8",
        env_ignore_empty=True,
        enable_decoding=False,
        extra="ignore",
        frozen=True,
    )

    app_version: str = "0.1.0-alpha.1"
    character_name: str = "Yui"

    database_url: str = "sqlite:///./data/yui.db"

    openai_api_key: str = ""
    openai_chat_model: str = "gpt-5.4-mini"
    openai_vision_model: str = "gpt-5.4-mini"
    openai_vision_detail: str = "auto"
    openai_transcribe_model: str = "gpt-4o-mini-transcribe"
    openai_realtime_model: str = "gpt-realtime-2"
    openai_realtime_translate_model: str = "gpt-realtime-translate"
    openai_realtime_transcribe_model: str = "gpt-realtime-whisper"
    openai_realtime_voice: str = "coral"
    openai_realtime_translation_language: str = "en"
    openai_max_output_tokens: int = 420
    openai_vision_max_output_tokens: int = 1200
    openai_web_search_enabled: bool = True
    openai_web_search_mode: str = "auto"
    openai_web_search_context_size: str = "low"
    openai_web_search_country: str = "JP"
    openai_web_search_city: str = ""
    openai_web_search_region: str = ""
    openai_web_search_timezone: str = "Asia/Tokyo"

    gemini_api_key: str = ""
    gemini_vision_model: str = "gemini-2.5-flash-lite"
    xai_api_key: str = ""
    xai_base_url: str = "https://api.x.ai/v1"
    xai_chat_model: str = "grok-4.3"

    voicevox_base_url: str = "http://127.0.0.1:50021"
    http_tts_base_url: str = ""
    http_tts_endpoint: str = "/tts"
    http_tts_api_key: str = ""
    http_tts_health_endpoint: str = ""
    http_tts_provider_id: str = ""
    http_tts_payload_format: str = "generic"
    http_tts_voice: str = ""
    http_tts_model: str = ""
    http_tts_gender: str = ""
    http_tts_instruct: str = ""
    http_tts_lang_code: str = ""
    http_tts_format: str = "wav"
    http_tts_audio_processor: str = "auto"
    http_tts_soundstretch_path: str = ""
    http_tts_irodori_num_steps: int = 0
    http_tts_irodori_seed: int = 0
    http_tts_irodori_cfg_scale_text: float = 0.0
    http_tts_irodori_cfg_scale_caption: float = 0.0
    http_tts_irodori_cfg_scale_speaker: float = 0.0
    http_tts_irodori_chunking_enabled: bool = False
    http_tts_irodori_chunk_min_chars: int = 0
    http_tts_irodori_first_sentence_chunk_min_chars: int = 0
    lmstudio_base_url: str = "http://127.0.0.1:1234/v1"
    lmstudio_chat_model: str = "local-model"
    open_meteo_geocoding_base_url: str = "https://geocoding-api.open-meteo.com/v1"
    open_meteo_forecast_base_url: str = "https://api.open-meteo.com/v1"

    default_user_id: str = "local_user"

    chat_provider: str = "openai"
    vision_provider: str = "openai"
    tts_provider: str = "voicevox"
    tts_fallback_provider: str = "voicevox"
    stt_provider: str = "openai"

    daily_chat_limit: int = 300
    daily_vision_limit: int = 100
    daily_stt_minutes_limit: int = 60
    daily_tts_limit: int = 300
    tts_audio_cache_max_files: int = 300
    tts_audio_cache_max_mb: int = 256
    tts_audio_cache_max_age_hours: int = 24

    available_faces: tuple[str, ...] = catalog_available_faces()
    available_animations: tuple[str, ...] = catalog_available_animations()

    @field_validator("available_faces", "available_animations", mode="before")
    @classmethod
    def parse_csv_tuple(cls, value: str | tuple[str, ...] | list[str]) -> tuple[str, ...]:
        parsed = _parse_csv(value)
        return parsed if parsed else tuple()

    @field_validator("openai_web_search_country", mode="before")
    @classmethod
    def normalize_country_code(cls, value: str) -> str:
        text = str(value or "").strip().upper()
        return text[:2] if text else ""

    @field_validator("openai_web_search_mode", mode="before")
    @classmethod
    def normalize_web_search_mode(cls, value: str) -> str:
        text = str(value or "auto").strip().lower()
        return text if text in {"off", "auto", "always"} else "auto"

    @field_validator("openai_web_search_context_size", mode="before")
    @classmethod
    def normalize_web_search_context_size(cls, value: str) -> str:
        text = str(value or "low").strip().lower()
        return text if text in {"low", "medium", "high"} else "low"

    @field_validator("http_tts_payload_format", mode="before")
    @classmethod
    def normalize_http_tts_payload_format(cls, value: str) -> str:
        text = str(value or "generic").strip().lower().replace("-", "_")
        return text if text in {"generic", "openai_speech", "irodori_openai_speech"} else "generic"

    @field_validator("http_tts_audio_processor", mode="before")
    @classmethod
    def normalize_http_tts_audio_processor(cls, value: str) -> str:
        text = str(value or "auto").strip().lower().replace("-", "_")
        return text if text in {"auto", "none", "soundstretch"} else "auto"


@lru_cache
def get_settings() -> Settings:
    return Settings()
