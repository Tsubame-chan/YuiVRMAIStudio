"""Authenticated native-device API and localhost-only pairing controls."""
from fastapi import APIRouter, Depends, HTTPException, Request
from pydantic import BaseModel, ConfigDict, Field
from app.core.config import get_settings
from app.core.device_sync import SyncStore, PlanRequest
from app.core.companion_migration import preview_character
from app.core.companion_shadow_migration import shadow_import_character
from app.core.companion_cutover import (preview_cutover, activate_trial_cutover,
                                       deactivate_unwritten_trial_cutover)
from app.core.companion_legacy_adapter import LegacyV2Adapter
from app.core.companion_backend_writer import CompanionBackendWriter
from app.api.admin import require_admin
from app.core.shared_conversation import (SharedChat, MemoryConnection, ConfirmConnection,
    snapshot, shared_chat, connection_draft, confirm_connection)

router = APIRouter(prefix="/sync")
admin = APIRouter(prefix="/admin/api/sync", dependencies=[Depends(require_admin)])

def store(): return SyncStore(get_settings().database_url)

def character_store(db: SyncStore, character_id: str):
    with db.connect() as connection:
        mode = db.character_mode(connection, character_id)
    return CompanionBackendWriter(get_settings().database_url, require_active=True) if mode == "active_v2" else db

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

class ShadowImport(BaseModel):
    model_config = ConfigDict(extra="forbid")
    expected_digest: str = Field(pattern=r"^[a-f0-9]{64}$")
    role_overrides: dict[str, str] = Field(default_factory=dict, max_length=20000)

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
    with db.connect() as connection:
        mode = db.character_mode(connection, body.character_id)
    if mode == "active_v2":
        return LegacyV2Adapter(get_settings().database_url, require_active=True).plan(owner_device, body)
    return db.plan(owner_device, body)

@router.post("/commit")
def commit(body: Commit, owner_device: str = Depends(device), db: SyncStore = Depends(store)):
    with db.connect() as connection:
        row = connection.execute("SELECT character_id FROM sync_plans WHERE id=? AND device_id=?",
                                 (body.plan_id, owner_device)).fetchone()
        mode = db.character_mode(connection, row["character_id"]) if row else "v1"
    if mode == "active_v2":
        return LegacyV2Adapter(get_settings().database_url, require_active=True).commit(owner_device, body.plan_id, body.choices)
    return db.commit(owner_device, body.plan_id, body.choices)


@admin.get("/characters")
def console_characters(db: SyncStore = Depends(store)):
    return {"items": db.characters()}


@admin.get("/characters/{character_id}")
def console_character(character_id: str, db: SyncStore = Depends(store)):
    return snapshot(character_store(db, character_id), character_id)


@admin.get("/characters/{character_id}/companion-dry-run")
def companion_dry_run(character_id: str):
    return preview_character(get_settings().database_url, character_id)


@admin.post("/characters/{character_id}/companion-shadow-import")
def companion_shadow_import(character_id: str, body: ShadowImport):
    settings = get_settings()
    if not settings.companion_v2_testing_enabled:
        raise HTTPException(404, "Companion v2 is not enabled.")
    db = SyncStore(settings.database_url)
    with db.connect() as connection:
        if db.character_mode(connection, character_id) == "active_v2":
            raise HTTPException(409, {"code": "character_already_active_v2"})
    return shadow_import_character(settings.database_url, character_id, body.expected_digest, body.role_overrides)


@admin.get("/characters/{character_id}/companion-cutover-preview")
def companion_cutover_preview(character_id: str, device_id: str):
    settings = get_settings()
    if not settings.companion_v2_testing_enabled:
        raise HTTPException(404, "Companion v2 is not enabled.")
    return preview_cutover(settings.database_url, device_id, character_id)


@admin.post("/characters/{character_id}/companion-trial-cutover")
def companion_trial_cutover(character_id: str, device_id: str):
    settings = get_settings()
    if not (settings.companion_v2_testing_enabled and settings.companion_v2_cutover_testing_enabled):
        raise HTTPException(404, "Companion trial cutover is not enabled.")
    return activate_trial_cutover(settings.database_url, device_id, character_id)


@admin.post("/characters/{character_id}/companion-trial-cutover-revert")
def companion_trial_cutover_revert(character_id: str, device_id: str):
    settings = get_settings()
    if not (settings.companion_v2_testing_enabled and settings.companion_v2_cutover_testing_enabled):
        raise HTTPException(404, "Companion trial cutover is not enabled.")
    return deactivate_unwritten_trial_cutover(settings.database_url, device_id, character_id)


@admin.post("/characters/{character_id}/chat")
async def console_chat(character_id: str, body: SharedChat, db: SyncStore = Depends(store), settings=Depends(get_settings)):
    return await shared_chat(character_store(db, character_id), settings, character_id, body)


@admin.post("/characters/{character_id}/connection-preview")
def preview_connection(character_id: str, body: MemoryConnection, db: SyncStore = Depends(store)):
    return connection_draft(character_store(db, character_id), character_id, body)


@admin.post("/characters/{character_id}/connections")
def save_connection(character_id: str, body: ConfirmConnection, db: SyncStore = Depends(store)):
    return confirm_connection(character_store(db, character_id), character_id, body)


from app.core.shared_conversation import LegacyImport, ConfirmImport, preview_legacy, import_legacy


@admin.post("/characters/{character_id}/import-preview")
def preview_backend_import(character_id: str, body: LegacyImport, db: SyncStore = Depends(store)):
    return preview_legacy(character_store(db, character_id), character_id, body)


@admin.post("/characters/{character_id}/import")
def save_backend_import(character_id: str, body: ConfirmImport, db: SyncStore = Depends(store)):
    return import_legacy(character_store(db, character_id), character_id, body)
