from pydantic import BaseModel, Field


class MemorySaveRequest(BaseModel):
    user_id: str = "local_user"
    content: str
    importance: int = Field(default=3, ge=1, le=5)
    tags: list[str] = Field(default_factory=list)


class MemorySearchRequest(BaseModel):
    user_id: str = "local_user"
    query: str
    limit: int = Field(default=5, ge=1, le=20)


class MemoryItem(BaseModel):
    id: str
    content: str
    importance: int
    tags: list[str] = Field(default_factory=list)


class MemorySearchResponse(BaseModel):
    items: list[MemoryItem] = Field(default_factory=list)

