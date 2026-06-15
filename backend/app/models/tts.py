from pydantic import BaseModel
from pydantic import Field


class TTSRequest(BaseModel):
    request_id: str | None = None
    text: str
    speaker_id: int = 14
    speed_scale: float | None = Field(default=1.0, ge=0.5, le=2.0)
    pitch_scale: float | None = Field(default=0.0, ge=-0.15, le=0.15)
    intonation_scale: float | None = Field(default=1.0, ge=0.0, le=2.0)
    volume_scale: float | None = Field(default=1.0, ge=0.0, le=2.0)
    pre_phoneme_length: float | None = Field(default=0.1, ge=0.0, le=1.5)
    post_phoneme_length: float | None = Field(default=0.1, ge=0.0, le=1.5)


class TTSResponse(BaseModel):
    audio_url: str
    format: str = "wav"
    duration_ms: int | None = None
