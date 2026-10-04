"""Owner-issued, revocable credentials for an external Companion connection.

The token is returned once. Only its digest is stored. An MCP adapter must
call authorize for every read/write; a connection ID alone is never a grant.
"""
from __future__ import annotations

import hashlib
import json
import os
import secrets
import time
from typing import Literal

from fastapi import HTTPException
from pydantic import BaseModel, ConfigDict, Field

from app.core.companion_v2 import CompanionStore, ID
from app.core.companion_activity import ActivityStore, ActivityReport
from app.core.companion_context import ContextRequest, owner_context


Operation = Literal["read_inbox", "read_context", "acknowledge", "post_update", "request_input"]
ALLOWED = {"read_inbox", "read_context", "acknowledge", "post_update", "request_input"}


class IssueGrant(BaseModel):
    model_config = ConfigDict(extra="forbid")
    connection_id: str = Field(pattern=ID)
    character_ids: list[str] = Field(min_length=1, max_length=8)
    operations: list[Operation] = Field(min_length=1, max_length=5)
    ttl_seconds: int = Field(default=86400, ge=300, le=30 * 86400)


class GrantStore:
    def __init__(self, companion: CompanionStore):
        self.companion = companion
        self.oauth_resource = os.environ.get("YUI_COMPANION_OAUTH_RESOURCE", "").rstrip("/")
        with companion.connect() as db:
            db.execute("""CREATE TABLE IF NOT EXISTS companion_v2_grants (
                id TEXT PRIMARY KEY, owner_principal TEXT NOT NULL, connection_id TEXT NOT NULL,
                token_digest TEXT NOT NULL UNIQUE, created_at REAL NOT NULL, expires_at REAL NOT NULL,
                revoked_at REAL)""")
            db.execute("""CREATE TABLE IF NOT EXISTS companion_v2_grant_characters (
                grant_id TEXT NOT NULL, character_id TEXT NOT NULL,
                PRIMARY KEY(grant_id, character_id),
                FOREIGN KEY(grant_id) REFERENCES companion_v2_grants(id) ON DELETE CASCADE)""")
            db.execute("""CREATE TABLE IF NOT EXISTS companion_v2_grant_operations (
                grant_id TEXT NOT NULL, operation TEXT NOT NULL,
                PRIMARY KEY(grant_id, operation),
                FOREIGN KEY(grant_id) REFERENCES companion_v2_grants(id) ON DELETE CASCADE)""")
            db.execute("""CREATE TABLE IF NOT EXISTS companion_v2_oauth_access (
                token_digest TEXT PRIMARY KEY, grant_id TEXT NOT NULL, resource TEXT NOT NULL,
                scope TEXT NOT NULL, expires_at REAL NOT NULL, revoked_at REAL,
                FOREIGN KEY(grant_id) REFERENCES companion_v2_grants(id) ON DELETE CASCADE)""")

    def issue(self, owner: str, request: IssueGrant) -> dict:
        characters = set(request.character_ids)
        operations = set(request.operations)
        if len(characters) != len(request.character_ids) or len(operations) != len(request.operations):
            raise HTTPException(422, {"code": "duplicate_grant_scope"})
        if not operations <= ALLOWED:
            raise HTTPException(422, {"code": "invalid_grant_operation"})
        token = "yui_cg_" + secrets.token_urlsafe(32)
        grant_id = secrets.token_hex(16)
        now = time.time()
        with self.companion.connect() as db:
            db.execute("BEGIN IMMEDIATE")
            for character in sorted(characters):
                self.companion.authorize(db, owner, character)
            db.execute("INSERT INTO companion_v2_grants VALUES(?,?,?,?,?,?,NULL)",
                       (grant_id, owner, request.connection_id, self._digest(token), now,
                        now + request.ttl_seconds))
            db.executemany("INSERT INTO companion_v2_grant_characters VALUES(?,?)",
                           ((grant_id, character) for character in sorted(characters)))
            db.executemany("INSERT INTO companion_v2_grant_operations VALUES(?,?)",
                           ((grant_id, operation) for operation in sorted(operations)))
        return {"grant_id": grant_id, "connection_id": request.connection_id,
                "character_ids": sorted(characters), "operations": sorted(operations),
                "expires_at": now + request.ttl_seconds, "token": token}

    @staticmethod
    def _digest(token: str) -> str:
        return hashlib.sha256(token.encode("ascii")).hexdigest()

    def authorize(self, token: str, connection_id: str, character_id: str,
                  operation: Operation) -> str:
        if (not token.startswith(("yui_cg_", "yui_at_")) or len(token) > 128 or
                not token.isascii() or operation not in ALLOWED):
            raise HTTPException(401, {"code": "invalid_companion_grant"})
        with self.companion.connect() as db:
            oauth = token.startswith("yui_at_")
            if oauth and not self.oauth_resource:
                raise HTTPException(401, {"code": "oauth_resource_not_configured"})
            row = db.execute("""SELECT g.id,g.owner_principal FROM companion_v2_grants g
                WHERE g.connection_id=? AND g.revoked_at IS NULL
                  AND g.expires_at>? AND EXISTS (
                      SELECT 1 FROM companion_v2_grant_characters c
                      WHERE c.grant_id=g.id AND c.character_id=?) AND EXISTS (
                      SELECT 1 FROM companion_v2_grant_operations o
                      WHERE o.grant_id=g.id AND o.operation=?) AND (
                    (?=0 AND g.token_digest=?) OR
                    (?=1 AND EXISTS (SELECT 1 FROM companion_v2_oauth_access t
                      WHERE t.grant_id=g.id AND t.token_digest=? AND t.resource=?
                        AND instr(' ' || t.scope || ' ', ' ' || ? || ' ')>0
                        AND t.revoked_at IS NULL AND t.expires_at>?)))""",
                (connection_id, time.time(), character_id, operation, int(oauth), self._digest(token),
                 int(oauth), self._digest(token), self.oauth_resource,
                 "yui:read" if operation in {"read_inbox", "read_context"} else "yui:write",
                 time.time())).fetchone()
            if row is None:
                raise HTTPException(403, {"code": "companion_grant_denied"})
            # A removed character or device cannot retain external access.
            self.companion.authorize(db, row["owner_principal"], character_id)
            return "grant:" + row["id"]

    def authenticate_transport(self, token: str) -> None:
        """Reject unknown/expired credentials before MCP discovery or tool calls."""
        if not token.startswith(("yui_cg_", "yui_at_")) or len(token) > 128 or not token.isascii():
            raise HTTPException(401, {"code": "invalid_companion_grant"})
        with self.companion.connect() as db:
            if token.startswith("yui_at_"):
                row = db.execute("""SELECT 1 FROM companion_v2_oauth_access t
                    JOIN companion_v2_grants g ON g.id=t.grant_id
                    WHERE t.token_digest=? AND t.resource=? AND t.revoked_at IS NULL
                      AND t.expires_at>? AND g.revoked_at IS NULL AND g.expires_at>?""",
                    (self._digest(token), self.oauth_resource, time.time(), time.time())).fetchone()
            else:
                row = db.execute("""SELECT 1 FROM companion_v2_grants
                    WHERE token_digest=? AND revoked_at IS NULL AND expires_at>?""",
                    (self._digest(token), time.time())).fetchone()
            if row is None:
                raise HTTPException(401, {"code": "invalid_companion_grant"})

    def allowed_operations(self, token: str) -> set[str]:
        """Tool discovery for the authenticated grant, including OAuth scopes."""
        self.authenticate_transport(token)
        with self.companion.connect() as db:
            if token.startswith("yui_at_"):
                row = db.execute("""SELECT t.grant_id,t.scope FROM companion_v2_oauth_access t
                    JOIN companion_v2_grants g ON g.id=t.grant_id
                    WHERE t.token_digest=? AND t.resource=? AND t.revoked_at IS NULL
                      AND t.expires_at>? AND g.revoked_at IS NULL AND g.expires_at>?""",
                    (self._digest(token), self.oauth_resource, time.time(), time.time())).fetchone()
            else:
                row = db.execute("SELECT id AS grant_id,'yui:read yui:write' AS scope "
                                 "FROM companion_v2_grants WHERE token_digest=? "
                                 "AND revoked_at IS NULL AND expires_at>?",
                                 (self._digest(token), time.time())).fetchone()
            if row is None:
                raise HTTPException(401, {"code": "invalid_companion_grant"})
            scopes = set(row["scope"].split())
            allowed = {"read_inbox", "read_context"} if "yui:read" in scopes else set()
            if "yui:write" in scopes:
                allowed.update({"acknowledge", "post_update", "request_input"})
            return {item[0] for item in db.execute(
                "SELECT operation FROM companion_v2_grant_operations WHERE grant_id=?",
                (row["grant_id"],)) if item[0] in allowed}

    def issue_oauth_access(self, grant_id: str, resource: str, scope: str,
                           ttl_seconds: int = 3600) -> dict:
        """Mint an audience-bound child credential after a separate OAuth code exchange.

        This method does not authenticate the browser. Only a verified
        authorization server may call it; it is not exposed on a route.
        """
        scopes = set(scope.split())
        if (not self.oauth_resource or resource != self.oauth_resource or
                not scopes or not scopes <= {"yui:read", "yui:write"} or
                len(scopes) != len(scope.split()) or not 60 <= ttl_seconds <= 3600):
            raise HTTPException(400, {"code": "invalid_oauth_access_request"})
        token = "yui_at_" + secrets.token_urlsafe(32)
        now = time.time()
        with self.companion.connect() as db:
            db.execute("BEGIN IMMEDIATE")
            grant = db.execute("""SELECT expires_at FROM companion_v2_grants
                WHERE id=? AND revoked_at IS NULL AND expires_at>?""",
                (grant_id, now)).fetchone()
            if grant is None:
                raise HTTPException(403, {"code": "companion_grant_denied"})
            for wanted_scope in scopes:
                operations = ("read_inbox", "read_context") if wanted_scope == "yui:read" else (
                    "acknowledge", "post_update", "request_input")
                placeholders = ",".join("?" for _ in operations)
                if not db.execute("SELECT 1 FROM companion_v2_grant_operations "
                                  f"WHERE grant_id=? AND operation IN ({placeholders})",
                                  (grant_id, *operations)).fetchone():
                    raise HTTPException(403, {"code": "companion_grant_scope_denied"})
            expiry = min(now + ttl_seconds, grant["expires_at"])
            db.execute("INSERT INTO companion_v2_oauth_access VALUES(?,?,?,?,?,NULL)",
                       (self._digest(token), grant_id, resource, " ".join(sorted(scopes)), expiry))
        return {"access_token": token, "token_type": "Bearer", "expires_in": max(1, int(expiry - now)),
                "scope": " ".join(sorted(scopes))}

    def revoke(self, owner: str, grant_id: str) -> dict:
        with self.companion.connect() as db:
            db.execute("BEGIN IMMEDIATE")
            row = db.execute("SELECT owner_principal,revoked_at FROM companion_v2_grants WHERE id=?",
                             (grant_id,)).fetchone()
            if row is None or row["owner_principal"] != owner:
                raise HTTPException(404, {"code": "grant_unavailable"})
            if row["revoked_at"] is None:
                db.execute("UPDATE companion_v2_grants SET revoked_at=? WHERE id=?", (time.time(), grant_id))
            if db.execute("SELECT 1 FROM sqlite_master WHERE type='table' AND name='companion_v2_event_subscriptions'").fetchone():
                ids = [item[0] for item in db.execute(
                    "SELECT id FROM companion_v2_event_subscriptions WHERE grant_id=?", (grant_id,))]
                db.execute("DELETE FROM companion_v2_event_subscriptions WHERE grant_id=?", (grant_id,))
                if ids:
                    db.executemany("DELETE FROM companion_v2_event_deliveries WHERE subscription_id=?",
                                   ((item,) for item in ids))
        return {"grant_id": grant_id, "revoked": True}


class ExternalActivityService:
    """Authenticated domain seam for the opt-in private MCP trial.

    Transport authentication supplies the grant token from a header, never
    from model-visible tool arguments. This does not replace OAuth for ChatGPT.
    """

    def __init__(self, companion: CompanionStore):
        self.grants = GrantStore(companion)
        self.activities = ActivityStore(companion)

    def read_inbox(self, token: str, connection_id: str, character_id: str,
                   limit: int = 20) -> list[dict]:
        self.grants.authorize(token, connection_id, character_id, "read_inbox")
        return self.activities.read_outbox_for_test(connection_id, limit, character_id)

    def acknowledge(self, token: str, connection_id: str, character_id: str,
                    notification_id: str) -> dict:
        self.grants.authorize(token, connection_id, character_id, "acknowledge")
        with self.activities.companion.connect() as db:
            row = db.execute("""SELECT a.character_id FROM companion_v2_activity_outbox o
                JOIN companion_v2_activities a ON a.id=o.activity_id
                WHERE o.id=? AND o.connection_id=?""", (notification_id, connection_id)).fetchone()
            if row is None or row["character_id"] != character_id:
                raise HTTPException(404, {"code": "notification_unavailable"})
        return self.activities.acknowledge_outbox_for_test(connection_id, notification_id)

    def post_update(self, token: str, connection_id: str, character_id: str,
                    activity_id: str, report: ActivityReport) -> dict:
        self.grants.authorize(token, connection_id, character_id, "post_update")
        return self.activities.report_for_test(character_id, activity_id, connection_id, report)

    def read_context(self, token: str, connection_id: str, character_id: str,
                     request: ContextRequest) -> dict:
        grant_principal = self.grants.authorize(token, connection_id, character_id, "read_context")
        grant_id = grant_principal.removeprefix("grant:")
        with self.activities.companion.connect() as db:
            row = db.execute("""SELECT c.purpose,c.binding_snapshot,g.owner_principal,
                       s.privacy_epoch FROM companion_v2_conversations c
                       JOIN companion_v2_grants g ON g.id=?
                       LEFT JOIN companion_v2_state s ON s.character_id=c.character_id
                       WHERE c.id=? AND c.character_id=?""",
                       (grant_id, request.conversation_id, character_id)).fetchone()
            if row is None:
                raise HTTPException(404, {"code": "conversation_unavailable"})
            binding = json.loads(row["binding_snapshot"])
            if binding.get("connection_id") != connection_id or row["purpose"] != request.purpose:
                raise HTTPException(403, {"code": "conversation_scope_denied"})
            owner = row["owner_principal"]
            epoch = row["privacy_epoch"] or 0
        packet = owner_context(self.activities.companion, owner, character_id, request,
                               conversation_filter=request.conversation_id)
        self.grants.authorize(token, connection_id, character_id, "read_context")
        with self.activities.companion.connect() as db:
            current = db.execute("SELECT privacy_epoch FROM companion_v2_state WHERE character_id=?",
                                 (character_id,)).fetchone()
            if (current["privacy_epoch"] if current else 0) != epoch:
                raise HTTPException(409, {"code": "context_changed_during_read"})
        return packet
