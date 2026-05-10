from typing import Literal

from pydantic import BaseModel


RealtimeMode = Literal["voice", "voice_text", "translate", "transcribe"]


class RealtimeStatusResponse(BaseModel):
    configured: bool
    default_mode: str = "stable"
    voice_model: str
    translation_model: str
    transcription_model: str
    voice: str
    modes: list[str]
    warning: str


class RealtimeProbeRequest(BaseModel):
    mode: RealtimeMode = "voice"
    connect: bool = False


class RealtimeProbeResponse(BaseModel):
    ok: bool
    mode: RealtimeMode
    model: str
    endpoint: str
    connected: bool = False
    first_event_type: str | None = None
    message: str


class RealtimeAudioResponse(BaseModel):
    text: str = ""
    audio_base64: str = ""
    audio_format: str = "pcm16"
    sample_rate: int = 24000
    events: list[str] = []
