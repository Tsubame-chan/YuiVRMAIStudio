from pydantic import BaseModel


class ConfigResponse(BaseModel):
    character_name: str
    chat_provider: str
    chat_providers: list[str]
    vision_provider: str
    vision_providers: list[str]
    tts_provider: str
    tts_providers: list[str]
    tts_recommendation: dict[str, str]
    stt_provider: str
    stt_providers: list[str]
    default_user_id: str
    limits: dict[str, int]
