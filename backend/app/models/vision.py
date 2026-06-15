from typing import Any

from pydantic import BaseModel, Field


class VisionObject(BaseModel):
    type: str
    brand_or_type: str | None = None
    color: str | None = None
    shape: str | None = None
    position: str | None = None
    visible_details: list[str] = Field(default_factory=list)
    estimated_total_volume_ml: int | None = None
    estimated_remaining_ratio: float | None = None
    estimated_consumed_ml: int | None = None
    confidence: str | None = None


class VisionStructured(BaseModel):
    objects: list[VisionObject] = Field(default_factory=list)
    extra: dict[str, Any] = Field(default_factory=dict)


class VisionResponse(BaseModel):
    vision_result_id: str
    summary: str
    structured: VisionStructured = Field(default_factory=VisionStructured)
    created_at: str


class OpenAIVisionExtra(BaseModel):
    scene: str | None = None
    notable_details: list[str] = Field(default_factory=list)
    uncertainties: list[str] = Field(default_factory=list)


class OpenAIVisionOutput(BaseModel):
    summary: str
    objects: list[VisionObject] = Field(default_factory=list)
    extra: OpenAIVisionExtra = Field(default_factory=OpenAIVisionExtra)
