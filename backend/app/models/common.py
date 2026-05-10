from typing import Any

from pydantic import BaseModel, Field


class ErrorResponse(BaseModel):
    detail: str


class RequestContext(BaseModel):
    vision_result_id: str | None = None
    screen_context: str | None = None
    extra: dict[str, Any] = Field(default_factory=dict)

