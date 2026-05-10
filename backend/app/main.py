from contextlib import asynccontextmanager

from fastapi import FastAPI

from app.api.routes import router
from app.core.config import get_settings
from app.db.sqlite import initialize_database


@asynccontextmanager
async def lifespan(app: FastAPI):
    settings = get_settings()
    initialize_database(settings.database_url)
    yield


settings = get_settings()

app = FastAPI(
    title="Yui VRM AI Studio Backend",
    version=settings.app_version,
    description="Local BYOK backend for Yui VRM AI Studio.",
    lifespan=lifespan,
)
app.include_router(router)
