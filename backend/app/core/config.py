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
    openai_realtime_model: str = "gpt-realtime"
    openai_realtime_translate_model: str = "gpt-realtime-translate"
    openai_realtime_transcribe_model: str = "gpt-realtime-whisper"
    openai_realtime_voice: str = "coral"
    openai_max_output_tokens: int = 420
    openai_vision_max_output_tokens: int = 1200

    gemini_api_key: str = ""
    gemini_vision_model: str = "gemini-2.5-flash-lite"

    voicevox_base_url: str = "http://127.0.0.1:50021"
    lmstudio_base_url: str = "http://127.0.0.1:1234/v1"

    default_user_id: str = "local_user"

    chat_provider: str = "openai"
    vision_provider: str = "openai"
    tts_provider: str = "voicevox"
    stt_provider: str = "openai"

    daily_chat_limit: int = 300
    daily_vision_limit: int = 100
    daily_stt_minutes_limit: int = 60
    daily_tts_limit: int = 300

    available_faces: tuple[str, ...] = catalog_available_faces()
    available_animations: tuple[str, ...] = catalog_available_animations()

    @field_validator("available_faces", "available_animations", mode="before")
    @classmethod
    def parse_csv_tuple(cls, value: str | tuple[str, ...] | list[str]) -> tuple[str, ...]:
        parsed = _parse_csv(value)
        return parsed if parsed else tuple()


@lru_cache
def get_settings() -> Settings:
    return Settings()
