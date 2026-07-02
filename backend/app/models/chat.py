from typing import Literal

from pydantic import BaseModel, Field, field_validator

from app.core.animation_catalog import available_animations, available_faces
from app.models.common import RequestContext


class ChatRequest(BaseModel):
    request_id: str = Field(..., description="Idempotency key supplied by the client.")
    user_id: str = "local_user"
    message: str
    context: RequestContext = Field(default_factory=RequestContext)
    mode: Literal["standard"] = "standard"
    secret: bool = False
    custom_instruction: str = ""
    character_name: str = ""


class ChatResponse(BaseModel):
    text: str
    face: str = "Neutral"
    animation: str = "idle_normal"
    voice_style: str = "normal"
    should_use_vision: bool = False
    memory_action: Literal["none", "save", "update"] = "none"
    should_tts: bool = True


class OpenAIChatOutput(BaseModel):
    text: str
    face: str
    animation: str
    voice_style: Literal["normal", "excited", "sad"] = "normal"
    should_use_vision: bool = False
    memory_action: Literal["none", "save", "update"] = "none"
    should_tts: bool = True

    @field_validator("face")
    @classmethod
    def validate_face(cls, value: str) -> str:
        return value if value in available_faces() else "Neutral"

    @field_validator("animation")
    @classmethod
    def validate_animation(cls, value: str) -> str:
        return value if value in available_animations() else "idle_normal"


class ConversationItem(BaseModel):
    role: str
    message: str
    created_at: str


class RecentConversationsResponse(BaseModel):
    items: list[ConversationItem]
