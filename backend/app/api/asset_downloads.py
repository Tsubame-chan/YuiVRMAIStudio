"""Local admin acquisition commands and non-secret native progress reads."""
from fastapi import APIRouter, Depends, HTTPException, Request
from pydantic import BaseModel, ConfigDict
from app.api.admin import require_admin
from app.core.asset_downloads import AcquisitionError

admin = APIRouter(prefix='/admin/api/assets', dependencies=[Depends(require_admin)])
public = APIRouter(prefix='/assets/downloads')


def manager(request: Request):
    return request.app.state.asset_manager


class StartRequest(BaseModel):
    model_config = ConfigDict(extra='forbid', strict=True)
    repair: bool = False


@admin.get('')
async def catalog(request: Request):
    return await manager(request).available()


@public.get('/status')
@admin.get('/status')
async def status(request: Request):
    # Read-only status is reusable by native clients. No local paths, credentials,
    # dependency logs, arbitrary URLs or install commands are included.
    return manager(request).snapshot()


@admin.post('/{asset_id}/start', status_code=202)
async def start(asset_id: str, body: StartRequest, request: Request):
    m=manager(request)
    try:
        return await m.start(asset_id, repair=body.repair)
    except KeyError: raise HTTPException(404, '追加データが見つかりません。')
    except AcquisitionError as exc: raise HTTPException(409, str(exc))


@admin.post('/{asset_id}/cancel', status_code=202)
async def cancel(asset_id: str, request: Request):
    try: return await manager(request).cancel(asset_id)
    except KeyError: raise HTTPException(404, '取得処理が見つかりません。')
