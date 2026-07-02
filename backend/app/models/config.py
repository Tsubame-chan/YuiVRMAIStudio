from pydantic import BaseModel, Field


class ConfigResponse(BaseModel):
    character_name: str
    chat_provider: str
    chat_providers: list[str]
    vision_provider: str
    vision_providers: list[str]
    tts_provider: str
    tts_providers: list[str]
    tts_recommendation: dict[str, str]
    tts_voice_options: dict[str, list[dict[str, str | int]]] = Field(default_factory=dict)
    tts_default_voice: dict[str, int] = Field(default_factory=dict)
    stt_provider: str
    stt_providers: list[str]
    default_user_id: str
    limits: dict[str, int]
