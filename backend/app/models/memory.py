from pydantic import BaseModel, Field


class MemoryScope(BaseModel):
    user_id: str = "local_user"
    character_id: str | None = Field(default=None, max_length=128, pattern=r"^[A-Za-z0-9_.:-]+$")


class MemorySaveRequest(MemoryScope):
    user_id: str = "local_user"
    content: str = Field(min_length=1, max_length=20000)
    importance: int = Field(default=3, ge=1, le=5)
    tags: list[str] = Field(default_factory=list)


class MemorySearchRequest(MemoryScope):
    user_id: str = "local_user"
    query: str
    limit: int = Field(default=5, ge=1, le=20)
    offset: int = Field(default=0, ge=0, le=1000000)


class MemoryItem(BaseModel):
    id: str
    content: str
    importance: int
    tags: list[str] = Field(default_factory=list)


class MemorySearchResponse(BaseModel):
    items: list[MemoryItem] = Field(default_factory=list)

