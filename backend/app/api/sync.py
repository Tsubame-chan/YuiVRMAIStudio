"""Authenticated native-device API and localhost-only pairing controls."""
from fastapi import APIRouter, Depends, HTTPException, Request
from pydantic import BaseModel, ConfigDict, Field
from app.core.config import get_settings
from app.core.device_sync import SyncStore, PlanRequest
from app.api.admin import require_admin

router = APIRouter(prefix="/sync")
admin = APIRouter(prefix="/admin/api/sync", dependencies=[Depends(require_admin)])

def store(): return SyncStore(get_settings().database_url)

def device(request: Request, db: SyncStore = Depends(store)):
    scheme, _, token = request.headers.get("authorization", "").partition(" ")
    if scheme.lower() != "bearer" or not token or len(token) > 256: raise HTTPException(401, "端末をPCの管理画面で登録してください。")
    return db.authenticate(token)

class Pair(BaseModel):
    model_config = ConfigDict(extra="forbid")
    name: str = Field(min_length=1, max_length=80)
    code: str = Field(min_length=10, max_length=16)

class Character(BaseModel):
    model_config = ConfigDict(extra="forbid")
    name: str = Field(min_length=1, max_length=256)

class Commit(BaseModel):
    model_config = ConfigDict(extra="forbid")
    plan_id: str = Field(pattern=r"^[a-f0-9]{32}$")
    choices: dict[str, str] = Field(default_factory=dict, max_length=20000)

@admin.post("/pairing")
def pairing(db: SyncStore = Depends(store)): return db.pairing_code()

@admin.get("/devices")
def devices(db: SyncStore = Depends(store)): return {"items": db.devices()}

@admin.post("/devices/{device_id}/revoke")
def revoke(device_id: str, db: SyncStore = Depends(store)):
    db.revoke(device_id)
    return {"ok": True}

@router.post("/pair")
def pair(body: Pair, db: SyncStore = Depends(store)): return db.pair(body.name, body.code)

@router.get("/characters")
def characters(_: str = Depends(device), db: SyncStore = Depends(store)):
    return {"protocol": 1, "server_id": db.server_id, "items": db.characters()}

@router.post("/characters")
def character(body: Character, _: str = Depends(device), db: SyncStore = Depends(store)):
    return db.create_character(body.name)

@router.post("/plan")
def plan(body: PlanRequest, owner_device: str = Depends(device), db: SyncStore = Depends(store)):
    return db.plan(owner_device, body)

@router.post("/commit")
def commit(body: Commit, owner_device: str = Depends(device), db: SyncStore = Depends(store)):
    return db.commit(owner_device, body.plan_id, body.choices)
