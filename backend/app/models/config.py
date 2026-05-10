from pydantic import BaseModel


class ConfigResponse(BaseModel):
    character_name: str
    chat_provider: str
    vision_provider: str
    tts_provider: str
    stt_provider: str
    default_user_id: str
    limits: dict[str, int]

