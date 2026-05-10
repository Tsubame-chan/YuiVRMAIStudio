from pydantic import BaseModel


class UsageLimits(BaseModel):
    chat_count: int
    vision_count: int
    stt_minutes: int
    tts_count: int


class UsageResponse(BaseModel):
    date: str
    chat_count: int
    vision_count: int
    stt_minutes: float
    tts_count: int
    limits: UsageLimits

