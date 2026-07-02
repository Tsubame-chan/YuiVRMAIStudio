from pydantic import BaseModel
from pydantic import Field
from pydantic import field_validator


class TTSRequest(BaseModel):
    request_id: str | None = None
    provider: str | None = None
    text: str
    speaker_id: int = 14
    speed_scale: float | None = Field(default=1.0, ge=0.5, le=2.0)
    pitch_scale: float | None = Field(default=0.0, ge=-0.5, le=0.5)
    intonation_scale: float | None = Field(default=1.0, ge=0.0, le=2.0)
    volume_scale: float | None = Field(default=1.0, ge=0.0, le=2.0)
    pre_phoneme_length: float | None = Field(default=0.1, ge=0.0, le=1.5)
    post_phoneme_length: float | None = Field(default=0.1, ge=0.0, le=1.5)
    voice_gender: str | None = None
    voice_instruct: str | None = None
    voice_lang_code: str | None = None

    @field_validator("provider", mode="before")
    @classmethod
    def normalize_provider(cls, value: str | None) -> str | None:
        if value is None:
            return None
        text = str(value).strip().lower()
        return text or None

    @field_validator("voice_gender", "voice_instruct", "voice_lang_code", mode="before")
    @classmethod
    def normalize_optional_text(cls, value: str | None) -> str | None:
        if value is None:
            return None
        text = str(value).strip()
        return text or None


class TTSResponse(BaseModel):
    audio_url: str
    format: str = "wav"
    duration_ms: int | None = None
