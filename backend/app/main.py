from contextlib import asynccontextmanager
from time import perf_counter
from uuid import uuid4
from app.core.runtime_events import TRACE, record, diagnose

from fastapi import FastAPI

from app.api.routes import router
from app.core.config import get_settings
from app.db.sqlite import initialize_database
from app.api.admin import public as admin_public, router as admin_router
from app.api.sync import router as sync_router, admin as sync_admin
from app.api.companion import router as companion_router
from starlette.responses import JSONResponse


@asynccontextmanager
async def lifespan(app: FastAPI):
    settings = get_settings()
    initialize_database(settings.database_url)
    # A process interruption cannot be presented as work still being executed.
    from app.core.csv_work import WorkStore
    with WorkStore(settings.database_url).db.connect() as db:
        db.execute("UPDATE csv_work SET state='failed', detail='Backendの再起動で作業が中断しました。CSVを選んで再実行してください。' WHERE state='running'")
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
app.include_router(sync_router)
app.include_router(sync_admin)
app.include_router(companion_router)
from app.api.work import router as work_router
app.include_router(work_router)


@app.middleware("http")
async def browser_boundaries(request, call_next):
    origin = request.headers.get("origin")
    if origin and origin != str(request.base_url).rstrip("/"):
        return JSONResponse({"detail": "Cross-origin browser requests are not allowed."}, status_code=403)
    if (request.url.path.startswith('/sync/') or request.url.path.startswith('/companion/v2/')) and request.method == 'POST':
        chunks, size = [], 0
        async for chunk in request.stream():
            size += len(chunk)
            maximum = 256 * 1024 if request.url.path.startswith('/companion/v2/') else 9 * 1024 * 1024
            if size > maximum:
                return JSONResponse({"detail": "同期データが上限を超えています。元データは保持しています。"}, status_code=413)
            chunks.append(chunk)
        request._body = b''.join(chunks)
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
        response.headers.setdefault("Content-Security-Policy", "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' blob:; media-src 'self' blob:; connect-src 'self'; font-src 'self'; frame-ancestors 'none'; base-uri 'none'; form-action 'self'")
    if request.url.path.startswith(('/sync/', '/companion/v2/')):
        response.headers['Cache-Control'] = 'no-store'
    return response
