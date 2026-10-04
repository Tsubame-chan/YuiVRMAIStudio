"""Opt-in protocol-2 foundation for paired devices."""
import os
from fastapi import APIRouter, Depends, HTTPException, Query
from pydantic import BaseModel, ConfigDict, Field

from app.api.sync import device
from app.core.companion_v2 import Commit, CompanionStore
from app.core.companion_context import ContextRequest, owner_context
from app.core.companion_activity import ActivityStore, CreateActivity, ActivityCommand
from app.core.companion_grants import GrantStore, IssueGrant
from app.core.companion_oauth import CompanionOAuthStore
from app.core.config import get_settings

router = APIRouter(prefix="/companion/v2")


class SpeechReceipt(BaseModel):
    model_config = ConfigDict(extra="forbid")
    claim_token: str = Field(min_length=20, max_length=128)


def store():
    settings = get_settings()
    if not settings.companion_v2_testing_enabled:
        raise HTTPException(404, "Companion v2 is not enabled.")
    return CompanionStore(settings.database_url)


@router.post("/grants")
def issue_grant(body: IssueGrant, principal: str = Depends(device), db: CompanionStore = Depends(store)):
    return GrantStore(db).issue(principal, body)


@router.delete("/grants/{grant_id}")
def revoke_grant(grant_id: str, principal: str = Depends(device), db: CompanionStore = Depends(store)):
    if len(grant_id) != 32 or any(c not in "0123456789abcdef" for c in grant_id):
        raise HTTPException(422, "Invalid grant ID")
    return GrantStore(db).revoke(principal, grant_id)


@router.post("/grants/{grant_id}/oauth-approval")
def issue_oauth_approval(grant_id: str, principal: str = Depends(device),
                         db: CompanionStore = Depends(store)):
    if len(grant_id) != 32 or any(c not in "0123456789abcdef" for c in grant_id):
        raise HTTPException(422, "Invalid grant ID")
    issuer = os.environ.get("YUI_COMPANION_OAUTH_ISSUER", "")
    resource = os.environ.get("YUI_COMPANION_OAUTH_RESOURCE", "")
    if not issuer or not resource:
        raise HTTPException(404, "Companion OAuth is not configured.")
    return CompanionOAuthStore(db, issuer, resource).issue_owner_approval(principal, grant_id)


@router.get("/capabilities")
def capabilities(principal: str = Depends(device), db: CompanionStore = Depends(store)):
    issuer = os.environ.get("YUI_COMPANION_OAUTH_ISSUER", "")
    resource = os.environ.get("YUI_COMPANION_OAUTH_RESOURCE", "")
    return {"hub_id": db.sync.server_id, "protocol": 2,
            "enabled_features": ["record_ledger_testing", "profile_version_testing",
                                 "memory_source_testing", "snapshot_testing",
                                 "conversation_binding_testing", "owner_context_testing",
                                 "activity_state_testing"],
            "oauth_issuer": issuer if issuer and resource else None,
            "mcp_resource": resource if issuer and resource else None,
            "limits": {"commit_operations": 64, "commit_bytes": 256 * 1024, "changes": 200}}


@router.post("/characters/{character_id}/commit")
def commit(character_id: str, body: Commit, principal: str = Depends(device), db: CompanionStore = Depends(store)):
    if len(character_id) != 32 or any(c not in "0123456789abcdef" for c in character_id):
        raise HTTPException(422, "Invalid character ID")
    return db.commit(principal, character_id, body)


@router.get("/characters/{character_id}/changes")
def changes(character_id: str, cursor: str | None = Query(None, max_length=1024),
            limit: int = Query(200, ge=1, le=200), principal: str = Depends(device),
            db: CompanionStore = Depends(store)):
    if len(character_id) != 32 or any(c not in "0123456789abcdef" for c in character_id):
        raise HTTPException(422, "Invalid character ID")
    return db.changes(principal, character_id, cursor, limit)


@router.get("/characters/{character_id}/snapshot")
def snapshot(character_id: str, cursor: str | None = Query(None, max_length=128),
             principal: str = Depends(device), db: CompanionStore = Depends(store)):
    if len(character_id) != 32 or any(c not in "0123456789abcdef" for c in character_id):
        raise HTTPException(422, "Invalid character ID")
    return db.snapshot(principal, character_id, cursor)


@router.post("/characters/{character_id}/context")
def context(character_id: str, body: ContextRequest, principal: str = Depends(device),
            db: CompanionStore = Depends(store)):
    if len(character_id) != 32 or any(c not in "0123456789abcdef" for c in character_id):
        raise HTTPException(422, "Invalid character ID")
    return owner_context(db, principal, character_id, body)


@router.post("/characters/{character_id}/activities")
def create_activity(character_id: str, body: CreateActivity, principal: str = Depends(device),
                    db: CompanionStore = Depends(store)):
    if len(character_id) != 32 or any(c not in "0123456789abcdef" for c in character_id):
        raise HTTPException(422, "Invalid character ID")
    return ActivityStore(db).create(principal, character_id, body)


@router.get("/characters/{character_id}/activities")
def list_activities(character_id: str, connection_id: str | None = Query(None),
                    limit: int = Query(50, ge=1, le=100),
                    before: str | None = Query(None),
                    principal: str = Depends(device), db: CompanionStore = Depends(store)):
    if len(character_id) != 32 or any(c not in "0123456789abcdef" for c in character_id):
        raise HTTPException(422, "Invalid character ID")
    return ActivityStore(db).list_for_owner(principal, character_id, connection_id, limit, before)


@router.get("/characters/{character_id}/activities/{activity_id}")
def get_activity(character_id: str, activity_id: str, principal: str = Depends(device),
                 db: CompanionStore = Depends(store)):
    if len(character_id) != 32 or any(c not in "0123456789abcdef" for c in character_id):
        raise HTTPException(422, "Invalid character ID")
    if len(activity_id) != 32 or any(c not in "0123456789abcdef" for c in activity_id):
        raise HTTPException(422, "Invalid activity ID")
    return ActivityStore(db).get(principal, character_id, activity_id)


@router.post("/characters/{character_id}/activities/{activity_id}/reports/{report_op_id}/speech/claim")
def claim_activity_speech(character_id: str, activity_id: str, report_op_id: str,
                          principal: str = Depends(device), db: CompanionStore = Depends(store)):
    for value in (character_id, activity_id, report_op_id):
        if len(value) != 32 or any(c not in "0123456789abcdef" for c in value):
            raise HTTPException(422, "Invalid ID")
    return ActivityStore(db).claim_speech(principal, character_id, activity_id, report_op_id)


@router.post("/characters/{character_id}/activities/{activity_id}/reports/{report_op_id}/speech/{stage}")
def activity_speech_receipt(character_id: str, activity_id: str, report_op_id: str,
                            stage: str, body: SpeechReceipt,
                            principal: str = Depends(device), db: CompanionStore = Depends(store)):
    for value in (character_id, activity_id, report_op_id):
        if len(value) != 32 or any(c not in "0123456789abcdef" for c in value):
            raise HTTPException(422, "Invalid ID")
    return ActivityStore(db).mark_speech(principal, character_id, activity_id,
                                         report_op_id, body.claim_token, stage)


@router.post("/characters/{character_id}/activities/{activity_id}/commands")
def activity_command(character_id: str, activity_id: str, body: ActivityCommand,
                     principal: str = Depends(device), db: CompanionStore = Depends(store)):
    if len(character_id) != 32 or any(c not in "0123456789abcdef" for c in character_id):
        raise HTTPException(422, "Invalid character ID")
    if len(activity_id) != 32 or any(c not in "0123456789abcdef" for c in activity_id):
        raise HTTPException(422, "Invalid activity ID")
    return ActivityStore(db).command(principal, character_id, activity_id, body)
