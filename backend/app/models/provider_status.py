from pydantic import BaseModel, Field


class ProviderStatusItem(BaseModel):
    status: str
    detail: str = ""
    category: str = ""
    requires_api_key: bool = False
    is_local: bool = False
    base_url: str = ""
    chat_model: str = ""
    vision_model: str = ""
    stt_model: str = ""
    realtime_model: str = ""
    version: str = ""
    speakers: int | None = None
    engine: str = ""
    recommendation: str = ""


class SystemStatusItem(BaseModel):
    status: str
    detail: str = ""


class ProviderStatusResponse(BaseModel):
    status: str
    backend: SystemStatusItem
    database: SystemStatusItem
    providers: dict[str, ProviderStatusItem] = Field(default_factory=dict)
