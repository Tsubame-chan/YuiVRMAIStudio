from pydantic import BaseModel, Field


class HealthResponse(BaseModel):
    status: str
    version: str
    api_schema_version: str = "2026-05-10"
    min_client_schema_version: str = "2026-05-10"
    database: str
    providers: dict[str, str | bool]
    features: dict[str, bool] = Field(default_factory=dict)
