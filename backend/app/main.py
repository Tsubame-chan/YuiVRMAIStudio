from contextlib import asynccontextmanager
from time import perf_counter
from uuid import uuid4
from app.core.runtime_events import TRACE, record, diagnose

from fastapi import FastAPI

from app.api.routes import router
from app.core.config import get_settings
from app.db.sqlite import initialize_database
from app.api.admin import public as admin_public, router as admin_router
from starlette.responses import JSONResponse


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
app.include_router(admin_public)
app.include_router(admin_router)


@app.middleware("http")
async def browser_boundaries(request, call_next):
    origin = request.headers.get("origin")
    if origin and origin != str(request.base_url).rstrip("/"):
        return JSONResponse({"detail": "Cross-origin browser requests are not allowed."}, status_code=403)
    path = request.url.path.removeprefix('/admin/api/run')
    operation = next((name for route, name in {
        '/chat': 'chat', '/tts': 'tts', '/stt': 'stt', '/vision': 'vision',
        '/realtime/audio': 'realtime', '/external/weather/current': 'weather',
        '/admin/api/probe/': 'probe', '/admin/api/voice-preview': 'tts', '/admin/api/voice-library': 'settings', '/admin/api/settings': 'settings',
    }.items() if path == route or (route in {'/tts', '/admin/api/probe/'} and path.startswith(route)) or (path.startswith('/admin/api/voice-endpoints/') and name=='probe')), '')
    token = TRACE.set(uuid4().hex[:8]) if operation else None
    started = perf_counter()
    if operation: record('started', operation=operation)
    try:
        response = await call_next(request)
    except Exception as exc:
        if operation: diagnose(exc, operation=operation)
        if operation: record('failed', operation=operation, status=500, elapsed_ms=int((perf_counter()-started)*1000))
        raise
    else:
        if operation: record('completed' if response.status_code < 400 else 'failed', operation=operation,
                             status=response.status_code, elapsed_ms=int((perf_counter()-started)*1000))
    finally:
        trace = TRACE.get()
        if token is not None: TRACE.reset(token)
    if operation: response.headers["X-Yui-Trace"] = trace
    if request.url.path.startswith("/admin"):
        response.headers["Cache-Control"] = "no-store"
        response.headers["X-Content-Type-Options"] = "nosniff"
        response.headers["Referrer-Policy"] = "no-referrer"
        response.headers["Content-Security-Policy"] = "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' blob:; media-src 'self' blob:; connect-src 'self'; font-src 'self'; frame-ancestors 'none'; base-uri 'none'; form-action 'self'"
    return response
