"""Isolated protocol-2 record ledger. No legacy data is migrated here."""
from __future__ import annotations

import hashlib
import hmac
import json
import base64
import secrets
import time
import sqlite3
from datetime import datetime, timezone
from typing import Literal

from fastapi import HTTPException
from pydantic import BaseModel, ConfigDict, Field, model_validator

from app.core.device_sync import SyncStore


ID = r"^[a-f0-9]{32}$"
MAX_SNAPSHOT_BYTES = 128 * 1024 * 1024
MAX_PAGE_BYTES = 512 * 1024


class RecordPayload(BaseModel):
    model_config = ConfigDict(extra="forbid")
    kind: Literal["user_utterance", "assistant_utterance", "observation", "external_report", "tool_result", "correction"]
    conversation_id: str | None = Field(default=None, pattern=ID)
    turn_id: str | None = Field(default=None, pattern=ID)
    in_reply_to_record_id: str | None = Field(default=None, pattern=ID)
    text: str = Field(min_length=1, max_length=32000)
    recorded_at: datetime
    realm: Literal["real", "roleplay"] = "real"

    @model_validator(mode="after")
    def check(self):
        if self.recorded_at.tzinfo is None:
            raise ValueError("recorded_at requires a timezone")
        if len(self.text.encode("utf-8")) > 128 * 1024:
            raise ValueError("record text exceeds 128 KiB")
        if self.in_reply_to_record_id and not self.conversation_id:
            raise ValueError("reply requires conversation_id")
        return self


class SourceRef(BaseModel):
    model_config = ConfigDict(extra="forbid")
    record_id: str = Field(pattern=ID)
    revision: int = Field(ge=1)
    quote: str | None = Field(default=None, min_length=1, max_length=4000)


class MemoryPayload(BaseModel):
    model_config = ConfigDict(extra="forbid")
    type: Literal["fact", "preference", "episode", "relationship", "reflection"]
    subject: str = Field(min_length=1, max_length=256)
    text: str = Field(min_length=1, max_length=4000)
    basis: Literal["user_statement", "user_confirmed", "agent_report", "tool_evidence", "ai_inference", "legacy_unknown"]
    pinned: bool = False
    source_refs: list[SourceRef] = Field(min_length=1, max_length=32)

    @model_validator(mode="after")
    def unique_sources(self):
        keys = [(ref.record_id, ref.revision) for ref in self.source_refs]
        if len(keys) != len(set(keys)):
            raise ValueError("duplicate source reference")
        return self


class ProfilePayload(BaseModel):
    model_config = ConfigDict(extra="forbid")
    field: Literal["name", "instruction"]
    text: str = Field(max_length=16000)

    @model_validator(mode="after")
    def valid_text(self):
        if self.field == "name" and not 1 <= len(self.text) <= 256:
            raise ValueError("character name must be 1–256 characters")
        return self


class BindingPayload(BaseModel):
    model_config = ConfigDict(extra="forbid")
    purpose: Literal["talk", "work"]
    # Connection selection is stored for later grant validation. This trial
    # ledger never dispatches to a connection or exposes external context.
    connection_id: str | None = Field(default=None, pattern=ID)
    context_policy: Literal["private_only", "approved_shared"] = "private_only"


class ConversationPayload(BaseModel):
    model_config = ConfigDict(extra="forbid")
    purpose: Literal["talk", "work"]
    binding_id: str | None = Field(pattern=ID)


class Operation(BaseModel):
    model_config = ConfigDict(extra="forbid")
    op_id: str = Field(pattern=ID)
    type: Literal["append_record", "correct_record", "delete_record", "put_memory", "delete_memory", "update_profile",
                  "create_binding", "create_conversation", "forget_source"]
    entity_id: str = Field(pattern=ID)
    expected_revision: int = Field(ge=0)
    payload: RecordPayload | MemoryPayload | ProfilePayload | BindingPayload | ConversationPayload | None = None

    @model_validator(mode="after")
    def check(self):
        if self.type in {"delete_record", "delete_memory", "forget_source"} and self.payload is not None:
            raise ValueError("delete has no payload")
        if self.type in {"append_record", "correct_record"} and not isinstance(self.payload, RecordPayload):
            raise ValueError("record payload required")
        if self.type == "put_memory" and not isinstance(self.payload, MemoryPayload):
            raise ValueError("memory payload required")
        if self.type == "update_profile" and not isinstance(self.payload, ProfilePayload):
            raise ValueError("profile payload required")
        if self.type == "create_binding" and not isinstance(self.payload, BindingPayload):
            raise ValueError("binding payload required")
        if self.type == "create_conversation" and not isinstance(self.payload, ConversationPayload):
            raise ValueError("conversation payload required")
        if self.type in {"create_binding", "create_conversation"} and self.expected_revision != 0:
            raise ValueError("creation requires expected_revision 0")
        if self.type == "append_record" and self.expected_revision != 0:
            raise ValueError("append_record requires expected_revision 0")
        return self


class Commit(BaseModel):
    model_config = ConfigDict(extra="forbid")
    batch_id: str = Field(pattern=ID)
    operations: list[Operation] = Field(min_length=1, max_length=64)

    @model_validator(mode="after")
    def unique(self):
        if len({op.op_id for op in self.operations}) != len(self.operations):
            raise ValueError("duplicate op_id")
        if len({op.entity_id for op in self.operations}) != len(self.operations):
            raise ValueError("multiple operations on one entity")
        return self


def canonical(value: object) -> str:
    return json.dumps(value, ensure_ascii=False, sort_keys=True, separators=(",", ":"), default=str)


def hash_value(value: object) -> str:
    return hashlib.sha256(canonical(value).encode()).hexdigest()


class CompanionStore:
    def __init__(self, database_url: str, *, initialize: bool = True):
        self.sync = SyncStore(database_url, initialize=initialize)
        if not initialize:
            return
        with self.connect() as db:
            db.executescript("""
                CREATE TABLE IF NOT EXISTS companion_v2_state (
                    character_id TEXT PRIMARY KEY, head_seq INTEGER NOT NULL DEFAULT 0,
                    privacy_epoch INTEGER NOT NULL DEFAULT 0);
                CREATE TABLE IF NOT EXISTS companion_v2_metadata (
                    key TEXT PRIMARY KEY, value TEXT NOT NULL);
                CREATE TABLE IF NOT EXISTS companion_v2_records (
                    id TEXT PRIMARY KEY, character_id TEXT NOT NULL, revision INTEGER NOT NULL,
                    deleted INTEGER NOT NULL DEFAULT 0, payload TEXT,
                    author_principal TEXT NOT NULL DEFAULT 'legacy_unknown', received_at TEXT NOT NULL DEFAULT '');
                CREATE TABLE IF NOT EXISTS companion_v2_record_revisions (
                    record_id TEXT NOT NULL, revision INTEGER NOT NULL, payload TEXT,
                    editor_principal TEXT NOT NULL DEFAULT 'legacy_unknown', received_at TEXT NOT NULL DEFAULT '',
                    PRIMARY KEY(record_id,revision),
                    FOREIGN KEY(record_id) REFERENCES companion_v2_records(id));
                CREATE TABLE IF NOT EXISTS companion_v2_memories (
                    id TEXT PRIMARY KEY, character_id TEXT NOT NULL, revision INTEGER NOT NULL,
                    state TEXT NOT NULL, payload TEXT);
                CREATE TABLE IF NOT EXISTS companion_v2_bindings (
                    id TEXT PRIMARY KEY, character_id TEXT NOT NULL, revision INTEGER NOT NULL,
                    payload TEXT NOT NULL);
                CREATE TABLE IF NOT EXISTS companion_v2_conversations (
                    id TEXT PRIMARY KEY, character_id TEXT NOT NULL, revision INTEGER NOT NULL,
                    purpose TEXT NOT NULL, binding_id TEXT, binding_snapshot TEXT NOT NULL,
                    FOREIGN KEY(binding_id) REFERENCES companion_v2_bindings(id));
                CREATE TABLE IF NOT EXISTS companion_v2_profiles (
                    id TEXT PRIMARY KEY, character_id TEXT NOT NULL, field TEXT NOT NULL,
                    revision INTEGER NOT NULL, payload TEXT NOT NULL,
                    UNIQUE(character_id,field));
                CREATE TABLE IF NOT EXISTS companion_v2_legacy_map (
                    character_id TEXT NOT NULL, kind TEXT NOT NULL, legacy_id TEXT NOT NULL,
                    entity_id TEXT NOT NULL, legacy_version INTEGER NOT NULL,
                    PRIMARY KEY(character_id,kind,legacy_id), UNIQUE(entity_id));
                CREATE TABLE IF NOT EXISTS companion_v2_legacy_metadata (
                    character_id TEXT NOT NULL, kind TEXT NOT NULL, legacy_id TEXT NOT NULL,
                    metadata TEXT NOT NULL, origin_device TEXT NOT NULL,
                    PRIMARY KEY(character_id,kind,legacy_id));
                CREATE TABLE IF NOT EXISTS companion_v2_legacy_imports (
                    character_id TEXT PRIMARY KEY, legacy_digest TEXT NOT NULL,
                    legacy_revision INTEGER NOT NULL, imported_at TEXT NOT NULL,
                    role_mapping_digest TEXT NOT NULL DEFAULT '');
                CREATE TABLE IF NOT EXISTS companion_v2_activities (
                    id TEXT PRIMARY KEY, character_id TEXT NOT NULL, conversation_id TEXT NOT NULL,
                    request TEXT NOT NULL, request_revision INTEGER NOT NULL, revision INTEGER NOT NULL,
                    state TEXT NOT NULL, stop_state TEXT NOT NULL, completion_basis TEXT,
                    binding_snapshot TEXT NOT NULL,
                    FOREIGN KEY(conversation_id) REFERENCES companion_v2_conversations(id));
                CREATE TABLE IF NOT EXISTS companion_v2_activity_ops (
                    principal TEXT NOT NULL, op_id TEXT NOT NULL, digest TEXT NOT NULL,
                    character_id TEXT NOT NULL, result TEXT NOT NULL,
                    PRIMARY KEY(principal,op_id));
                CREATE TABLE IF NOT EXISTS companion_v2_sources (
                    memory_id TEXT NOT NULL, record_id TEXT NOT NULL, record_revision INTEGER NOT NULL,
                    quote TEXT, PRIMARY KEY(memory_id,record_id,record_revision),
                    FOREIGN KEY(memory_id) REFERENCES companion_v2_memories(id),
                    FOREIGN KEY(record_id,record_revision) REFERENCES companion_v2_record_revisions(record_id,revision));
                CREATE TABLE IF NOT EXISTS companion_v2_suppressions (
                    record_id TEXT PRIMARY KEY, character_id TEXT NOT NULL,
                    record_revision INTEGER NOT NULL,
                    FOREIGN KEY(record_id) REFERENCES companion_v2_records(id));
                CREATE TABLE IF NOT EXISTS companion_v2_changes (
                    character_id TEXT NOT NULL, seq INTEGER NOT NULL, entity_id TEXT NOT NULL,
                    revision INTEGER NOT NULL, deleted INTEGER NOT NULL, entity_type TEXT NOT NULL DEFAULT 'record',
                    PRIMARY KEY(character_id,seq));
                CREATE TABLE IF NOT EXISTS companion_v2_batches (
                    principal TEXT NOT NULL, batch_id TEXT NOT NULL, digest TEXT NOT NULL,
                    character_id TEXT NOT NULL, result TEXT NOT NULL,
                    PRIMARY KEY(principal,batch_id));
                CREATE TABLE IF NOT EXISTS companion_v2_ops (
                    principal TEXT NOT NULL, op_id TEXT NOT NULL, digest TEXT NOT NULL,
                    character_id TEXT NOT NULL, batch_id TEXT NOT NULL,
                    PRIMARY KEY(principal,op_id));
                CREATE TABLE IF NOT EXISTS companion_v2_snapshots (
                    token TEXT PRIMARY KEY, principal TEXT NOT NULL, character_id TEXT NOT NULL,
                    privacy_epoch INTEGER NOT NULL, head_seq INTEGER NOT NULL,
                    expires_at REAL NOT NULL);
                CREATE TABLE IF NOT EXISTS companion_v2_snapshot_items (
                    token TEXT NOT NULL, position INTEGER NOT NULL, payload TEXT NOT NULL,
                    PRIMARY KEY(token,position),
                    FOREIGN KEY(token) REFERENCES companion_v2_snapshots(token) ON DELETE CASCADE);
            """)
            columns = {row[1] for row in db.execute("PRAGMA table_info(companion_v2_changes)")}
            if "entity_type" not in columns:
                db.execute("ALTER TABLE companion_v2_changes ADD COLUMN entity_type TEXT NOT NULL DEFAULT 'record'")
            record_columns = {row[1] for row in db.execute("PRAGMA table_info(companion_v2_records)")}
            if "author_principal" not in record_columns:
                db.execute("ALTER TABLE companion_v2_records ADD COLUMN author_principal TEXT NOT NULL DEFAULT 'legacy_unknown'")
            if "received_at" not in record_columns:
                db.execute("ALTER TABLE companion_v2_records ADD COLUMN received_at TEXT NOT NULL DEFAULT ''")
            revision_columns = {row[1] for row in db.execute("PRAGMA table_info(companion_v2_record_revisions)")}
            if "editor_principal" not in revision_columns:
                db.execute("ALTER TABLE companion_v2_record_revisions ADD COLUMN editor_principal TEXT NOT NULL DEFAULT 'legacy_unknown'")
            if "received_at" not in revision_columns:
                db.execute("ALTER TABLE companion_v2_record_revisions ADD COLUMN received_at TEXT NOT NULL DEFAULT ''")
            import_columns = {row[1] for row in db.execute("PRAGMA table_info(companion_v2_legacy_imports)")}
            if "role_mapping_digest" not in import_columns:
                db.execute("ALTER TABLE companion_v2_legacy_imports ADD COLUMN role_mapping_digest TEXT NOT NULL DEFAULT ''")
            db.execute("INSERT OR IGNORE INTO companion_v2_metadata VALUES('cursor_key',?)",
                       (secrets.token_hex(32),))

    def connect(self):
        db = self.sync.connect()
        db.execute("PRAGMA foreign_keys=ON")
        db.execute("PRAGMA busy_timeout=15000")
        db.execute("PRAGMA secure_delete=ON")
        return db

    @staticmethod
    def authorize(db: sqlite3.Connection, principal: str, character: str):
        SyncStore.require_device(db, principal)
        if not db.execute("SELECT 1 FROM sync_characters WHERE id=?", (character,)).fetchone():
            raise HTTPException(404, "共有キャラクターが見つかりません。")

    def commit(self, principal: str, character: str, batch: Commit, *,
               legacy_mapping=None, expected_head_seq: int | None = None):
        digest = hash_value({"method": "commit", "hub": self.sync.server_id, "character": character,
                             "operations": batch.model_dump(mode="json")})
        with self.connect() as db:
            db.execute("BEGIN IMMEDIATE")
            self.authorize(db, principal, character)
            prior = db.execute("SELECT * FROM companion_v2_batches WHERE principal=? AND batch_id=?",
                               (principal, batch.batch_id)).fetchone()
            if prior:
                if prior["digest"] != digest or prior["character_id"] != character:
                    raise HTTPException(409, {"code": "idempotency_mismatch"})
                return json.loads(prior["result"])
            for op in batch.operations:
                op_digest = hash_value({"hub": self.sync.server_id, "character": character,
                                        "operation": op.model_dump(mode="json")})
                prior_op = db.execute("SELECT * FROM companion_v2_ops WHERE principal=? AND op_id=?",
                                      (principal, op.op_id)).fetchone()
                if db.execute("SELECT 1 FROM companion_v2_activity_ops WHERE principal=? AND op_id=?",
                              (principal, op.op_id)).fetchone():
                    raise HTTPException(409, {"code": "idempotency_mismatch"})
                if prior_op:
                    raise HTTPException(409, {"code": "idempotency_mismatch" if prior_op["digest"] != op_digest else "operation_already_committed"})
            db.execute("INSERT OR IGNORE INTO companion_v2_state(character_id) VALUES(?)", (character,))
            state = db.execute("SELECT * FROM companion_v2_state WHERE character_id=?", (character,)).fetchone()
            if expected_head_seq is not None and state["head_seq"] != expected_head_seq:
                raise HTTPException(409, {"code": "legacy_plan_revision_conflict"})
            seq, epoch, results = state["head_seq"], state["privacy_epoch"], []
            for op in batch.operations:
                if op.type == "forget_source":
                    source = db.execute("SELECT revision,deleted FROM companion_v2_records WHERE id=? AND character_id=?",
                                        (op.entity_id, character)).fetchone()
                    if not source or source["deleted"] or source["revision"] != op.expected_revision:
                        raise HTTPException(409, {"code": "source_revision_conflict", "entity_id": op.entity_id})
                    if db.execute("SELECT 1 FROM companion_v2_suppressions WHERE record_id=?",
                                  (op.entity_id,)).fetchone():
                        raise HTTPException(409, {"code": "source_already_suppressed"})
                    db.execute("INSERT INTO companion_v2_suppressions VALUES(?,?,?)",
                               (op.entity_id, character, source["revision"]))
                    dependents = [row[0] for row in db.execute(
                        "SELECT DISTINCT memory_id FROM companion_v2_sources WHERE record_id=?", (op.entity_id,))]
                    for memory_id in dependents:
                        memory_row = db.execute("SELECT revision,state FROM companion_v2_memories WHERE id=?", (memory_id,)).fetchone()
                        db.execute("DELETE FROM companion_v2_sources WHERE memory_id=?", (memory_id,))
                        if memory_row and memory_row["state"] == "active":
                            revision = memory_row["revision"] + 1
                            db.execute("UPDATE companion_v2_memories SET revision=?,state='deleted',payload=NULL WHERE id=?",
                                       (revision, memory_id))
                            seq += 1
                            db.execute("INSERT INTO companion_v2_changes VALUES(?,?,?,?,?,?)",
                                       (character, seq, memory_id, revision, 1, "memory"))
                    seq += 1
                    epoch += 1
                    db.execute("INSERT INTO companion_v2_changes VALUES(?,?,?,?,?,?)",
                               (character, seq, op.entity_id, source["revision"], 0, "suppression"))
                    db.execute("INSERT INTO companion_v2_ops VALUES(?,?,?,?,?)",
                               (principal, op.op_id, hash_value({"hub": self.sync.server_id, "character": character,
                                                                 "operation": op.model_dump(mode="json")}), character, batch.batch_id))
                    results.append({"op_id": op.op_id, "entity_id": op.entity_id,
                                    "revision": source["revision"], "seq": seq})
                    continue
                if op.type in {"create_binding", "create_conversation"}:
                    if any(db.execute(f"SELECT 1 FROM {table} WHERE id=?", (op.entity_id,)).fetchone()
                           for table in ("companion_v2_records", "companion_v2_memories",
                                         "companion_v2_bindings", "companion_v2_conversations",
                                         "companion_v2_profiles", "companion_v2_activities")):
                        raise HTTPException(409, {"code": "entity_id_conflict", "entity_id": op.entity_id})
                    if op.type == "create_binding":
                        snapshot = op.payload.model_dump(mode="json")
                        db.execute("INSERT INTO companion_v2_bindings VALUES(?,?,?,?)",
                                   (op.entity_id, character, 1, canonical(snapshot)))
                        entity_type = "binding"
                    else:
                        binding_snapshot = {"connection_id": None, "context_policy": "private_only"}
                        if op.payload.binding_id:
                            binding = db.execute("SELECT * FROM companion_v2_bindings WHERE id=?",
                                                 (op.payload.binding_id,)).fetchone()
                            if not binding or binding["character_id"] != character:
                                raise HTTPException(409, {"code": "binding_unavailable"})
                            selected = json.loads(binding["payload"])
                            if selected["purpose"] != op.payload.purpose:
                                raise HTTPException(409, {"code": "binding_purpose_mismatch"})
                            binding_snapshot = {"connection_id": selected["connection_id"],
                                                "context_policy": selected["context_policy"]}
                        db.execute("INSERT INTO companion_v2_conversations VALUES(?,?,?,?,?,?)",
                                   (op.entity_id, character, 1, op.payload.purpose,
                                    op.payload.binding_id, canonical(binding_snapshot)))
                        entity_type = "conversation"
                    seq += 1
                    db.execute("INSERT INTO companion_v2_changes(character_id,seq,entity_id,revision,deleted,entity_type) VALUES(?,?,?,?,?,?)",
                               (character, seq, op.entity_id, 1, 0, entity_type))
                    db.execute("INSERT INTO companion_v2_ops VALUES(?,?,?,?,?)",
                               (principal, op.op_id, hash_value({"hub": self.sync.server_id, "character": character,
                                                                 "operation": op.model_dump(mode="json")}), character, batch.batch_id))
                    results.append({"op_id": op.op_id, "entity_id": op.entity_id, "revision": 1, "seq": seq})
                    continue
                if op.type == "update_profile":
                    if any(db.execute(f"SELECT 1 FROM {table} WHERE id=?", (op.entity_id,)).fetchone()
                           for table in ("companion_v2_records", "companion_v2_memories",
                                         "companion_v2_bindings", "companion_v2_conversations",
                                         "companion_v2_activities")):
                        raise HTTPException(409, {"code": "entity_id_conflict", "entity_id": op.entity_id})
                    row = db.execute("SELECT character_id,field,revision FROM companion_v2_profiles WHERE id=?",
                                     (op.entity_id,)).fetchone()
                    if row and (row["character_id"] != character or row["field"] != op.payload.field):
                        raise HTTPException(409, {"code": "profile_identity_conflict"})
                    current = row["revision"] if row else 0
                    if current != op.expected_revision:
                        raise HTTPException(409, {"code": "revision_conflict", "entity_id": op.entity_id,
                                                  "current_revision": current})
                    if not row and db.execute(
                        "SELECT 1 FROM companion_v2_profiles WHERE character_id=? AND field=?",
                        (character, op.payload.field)).fetchone():
                        raise HTTPException(409, {"code": "profile_field_conflict"})
                    revision = current + 1
                    if row:
                        db.execute("UPDATE companion_v2_profiles SET revision=?,payload=? WHERE id=?",
                                   (revision, op.payload.text, op.entity_id))
                        epoch += 1
                    else:
                        db.execute("INSERT INTO companion_v2_profiles VALUES(?,?,?,?,?)",
                                   (op.entity_id, character, op.payload.field, revision, op.payload.text))
                    seq += 1
                    db.execute("INSERT INTO companion_v2_changes(character_id,seq,entity_id,revision,deleted,entity_type) "
                               "VALUES(?,?,?,?,0,'profile')", (character, seq, op.entity_id, revision))
                    db.execute("INSERT INTO companion_v2_ops VALUES(?,?,?,?,?)",
                               (principal, op.op_id, hash_value({"hub": self.sync.server_id, "character": character,
                                                                 "operation": op.model_dump(mode="json")}), character, batch.batch_id))
                    results.append({"op_id": op.op_id, "entity_id": op.entity_id,
                                    "revision": revision, "seq": seq})
                    continue
                if op.type in {"put_memory", "delete_memory"}:
                    row = db.execute("SELECT * FROM companion_v2_memories WHERE id=?", (op.entity_id,)).fetchone()
                    if (any(db.execute(f"SELECT 1 FROM {table} WHERE id=?", (op.entity_id,)).fetchone()
                            for table in ("companion_v2_records", "companion_v2_bindings", "companion_v2_conversations",
                                          "companion_v2_profiles", "companion_v2_activities"))
                            or (row and row["character_id"] != character)):
                        raise HTTPException(409, {"code": "entity_id_conflict", "entity_id": op.entity_id})
                    current = row["revision"] if row else 0
                    if current != op.expected_revision or (row and row["state"] == "deleted"):
                        raise HTTPException(409, {"code": "revision_conflict", "entity_id": op.entity_id,
                                                  "current_revision": current})
                    if op.type == "delete_memory" and not row:
                        raise HTTPException(409, {"code": "revision_conflict", "entity_id": op.entity_id,
                                                  "current_revision": 0})
                    if op.type == "put_memory":
                        for ref in op.payload.source_refs:
                            if db.execute("SELECT 1 FROM companion_v2_suppressions WHERE record_id=?",
                                          (ref.record_id,)).fetchone():
                                raise HTTPException(410, {"code": "source_suppressed", "record_id": ref.record_id})
                            source = db.execute("SELECT r.character_id,r.deleted,r.revision AS current_revision,v.payload FROM companion_v2_records r JOIN companion_v2_record_revisions v ON v.record_id=r.id AND v.revision=? WHERE r.id=?",
                                                (ref.revision, ref.record_id)).fetchone()
                            if not source or source["character_id"] != character or source["deleted"] or source["current_revision"] != ref.revision:
                                raise HTTPException(410, {"code": "source_unavailable", "record_id": ref.record_id})
                            source_payload = json.loads(source["payload"])
                            if ref.quote and ref.quote not in source_payload["text"]:
                                raise HTTPException(422, {"code": "quote_mismatch", "record_id": ref.record_id})
                            required_kind = {"user_statement": "user_utterance", "agent_report": "external_report",
                                             "tool_evidence": "tool_result"}.get(op.payload.basis)
                            if required_kind and source_payload["kind"] != required_kind:
                                raise HTTPException(422, {"code": "basis_source_mismatch"})
                    revision = current + 1
                    deleted = op.type == "delete_memory"
                    payload = None if deleted else canonical(op.payload.model_dump(mode="json"))
                    if row:
                        db.execute("UPDATE companion_v2_memories SET revision=?,state=?,payload=? WHERE id=?",
                                   (revision, "deleted" if deleted else "active", payload, op.entity_id))
                        db.execute("DELETE FROM companion_v2_sources WHERE memory_id=?", (op.entity_id,))
                    else:
                        db.execute("INSERT INTO companion_v2_memories VALUES(?,?,?,?,?)",
                                   (op.entity_id, character, revision, "active", payload))
                    if not deleted:
                        for ref in op.payload.source_refs:
                            db.execute("INSERT INTO companion_v2_sources VALUES(?,?,?,?)",
                                       (op.entity_id, ref.record_id, ref.revision, ref.quote))
                    seq += 1
                    db.execute("INSERT INTO companion_v2_changes(character_id,seq,entity_id,revision,deleted,entity_type) VALUES(?,?,?,?,?,?)",
                               (character, seq, op.entity_id, revision, int(deleted), "memory"))
                    if deleted or row:
                        # A corrected or withdrawn memory must not survive in a
                        # previously materialized snapshot or cursor page.
                        epoch += 1
                    db.execute("INSERT INTO companion_v2_ops VALUES(?,?,?,?,?)",
                               (principal, op.op_id, hash_value({"hub": self.sync.server_id, "character": character,
                                                                 "operation": op.model_dump(mode="json")}), character, batch.batch_id))
                    results.append({"op_id": op.op_id, "entity_id": op.entity_id, "revision": revision, "seq": seq})
                    continue
                row = db.execute("SELECT * FROM companion_v2_records WHERE id=?", (op.entity_id,)).fetchone()
                if (any(db.execute(f"SELECT 1 FROM {table} WHERE id=?", (op.entity_id,)).fetchone()
                        for table in ("companion_v2_memories", "companion_v2_bindings", "companion_v2_conversations",
                                      "companion_v2_profiles", "companion_v2_activities"))
                        or (row and row["character_id"] != character)):
                    raise HTTPException(409, {"code": "record_id_conflict", "entity_id": op.entity_id})
                current = row["revision"] if row else 0
                if current != op.expected_revision or (row and row["deleted"]):
                    raise HTTPException(409, {"code": "revision_conflict", "entity_id": op.entity_id,
                                              "current_revision": current, "deleted": bool(row and row["deleted"])})
                if op.type == "append_record" and row or op.type != "append_record" and not row:
                    raise HTTPException(409, {"code": "revision_conflict", "entity_id": op.entity_id,
                                              "current_revision": current})
                if op.type in {"append_record", "correct_record"}:
                    conversation_id = op.payload.conversation_id
                    if conversation_id:
                        conversation = db.execute("SELECT character_id FROM companion_v2_conversations WHERE id=?",
                                                  (conversation_id,)).fetchone()
                        if not conversation or conversation["character_id"] != character:
                            raise HTTPException(409, {"code": "conversation_unavailable"})
                    if row and json.loads(row["payload"])["conversation_id"] != conversation_id:
                        raise HTTPException(409, {"code": "conversation_immutable"})
                    if op.payload.in_reply_to_record_id:
                        target = db.execute("SELECT character_id,deleted,payload FROM companion_v2_records WHERE id=?",
                                            (op.payload.in_reply_to_record_id,)).fetchone()
                        if (not target or target["character_id"] != character or target["deleted"]
                                or json.loads(target["payload"]).get("conversation_id") != conversation_id):
                            raise HTTPException(409, {"code": "reply_target_unavailable"})
                        if target["character_id"] == character and op.payload.in_reply_to_record_id == op.entity_id:
                            raise HTTPException(409, {"code": "reply_target_unavailable"})
                    if row and op.type == "correct_record":
                        prior_payload = json.loads(row["payload"])
                        if (prior_payload.get("kind") != op.payload.kind
                                or prior_payload.get("realm") != op.payload.realm):
                            raise HTTPException(409, {"code": "record_origin_immutable"})
                        if (prior_payload.get("turn_id") != op.payload.turn_id
                                or prior_payload.get("in_reply_to_record_id") != op.payload.in_reply_to_record_id):
                            raise HTTPException(409, {"code": "reply_link_immutable"})
                revision = current + 1
                deleted = op.type == "delete_record"
                payload = None if deleted else canonical(op.payload.model_dump(mode="json"))
                received_at = datetime.now(timezone.utc).isoformat()
                if row:
                    db.execute("UPDATE companion_v2_records SET revision=?,deleted=?,payload=? WHERE id=?",
                               (revision, int(deleted), payload, op.entity_id))
                else:
                    db.execute("INSERT INTO companion_v2_records(id,character_id,revision,deleted,payload,author_principal,received_at) VALUES(?,?,?,?,?,?,?)",
                               (op.entity_id, character, revision, 0, payload, "device:" + principal, received_at))
                if deleted:
                    dependent_ids = [x[0] for x in db.execute("SELECT DISTINCT memory_id FROM companion_v2_sources WHERE record_id=?", (op.entity_id,))]
                    db.execute("DELETE FROM companion_v2_sources WHERE record_id=?", (op.entity_id,))
                    db.execute("DELETE FROM companion_v2_record_revisions WHERE record_id=?", (op.entity_id,))
                else:
                    db.execute("INSERT INTO companion_v2_record_revisions(record_id,revision,payload,editor_principal,received_at) VALUES(?,?,?,?,?)",
                               (op.entity_id, revision, payload, "device:" + principal, received_at))
                seq += 1
                db.execute("INSERT INTO companion_v2_changes(character_id,seq,entity_id,revision,deleted,entity_type) VALUES(?,?,?,?,?,?)",
                           (character, seq, op.entity_id, revision, int(deleted), "record"))
                if deleted or op.type == "correct_record":
                    epoch += 1
                    if not deleted:
                        dependent_ids = [x[0] for x in db.execute("SELECT DISTINCT memory_id FROM companion_v2_sources WHERE record_id=? AND record_revision=?",
                                                                  (op.entity_id, current))]
                    for memory_id in dependent_ids:
                        memory = db.execute("SELECT revision,state FROM companion_v2_memories WHERE id=?", (memory_id,)).fetchone()
                        if memory and memory["state"] == "active":
                            memory_revision = memory["revision"] + 1
                            db.execute("UPDATE companion_v2_memories SET revision=?,state='invalidated',payload=NULL WHERE id=?",
                                       (memory_revision, memory_id))
                            # Source quotes can contain the corrected text. Once
                            # invalidated, no old citation should remain readable.
                            db.execute("DELETE FROM companion_v2_sources WHERE memory_id=?", (memory_id,))
                            seq += 1
                            db.execute("INSERT INTO companion_v2_changes(character_id,seq,entity_id,revision,deleted,entity_type) VALUES(?,?,?,?,?,?)",
                                       (character, seq, memory_id, memory_revision, 1, "memory"))
                db.execute("INSERT INTO companion_v2_ops VALUES(?,?,?,?,?)",
                           (principal, op.op_id, hash_value({"hub": self.sync.server_id, "character": character,
                                                             "operation": op.model_dump(mode="json")}), character, batch.batch_id))
                results.append({"op_id": op.op_id, "entity_id": op.entity_id, "revision": revision, "seq": seq})
            db.execute("UPDATE companion_v2_state SET head_seq=?,privacy_epoch=? WHERE character_id=?",
                       (seq, epoch, character))
            if epoch != state["privacy_epoch"]:
                db.execute("DELETE FROM companion_v2_snapshot_items WHERE token IN (SELECT token FROM companion_v2_snapshots WHERE character_id=?)",
                           (character,))
            result = {"accepted": results, "head_seq": seq, "privacy_epoch": epoch}
            db.execute("INSERT INTO companion_v2_batches VALUES(?,?,?,?,?)",
                       (principal, batch.batch_id, digest, character, canonical(result)))
            if legacy_mapping is not None:
                legacy_mapping(db)
            if epoch != state["privacy_epoch"]:
                # Pending test-adapter plans can still contain a client's old
                # upload text. A correction or deletion must retire them in
                # the same transaction as the privacy change.
                db.execute("DELETE FROM sync_plans WHERE character_id=? AND result IS NULL "
                           "AND json_extract(payload,'$.mode')='companion_v2'", (character,))
                # Cached Backend replies may repeat text or reflect context
                # that the owner has since corrected or forgotten.
                db.execute("DELETE FROM sync_backend_responses WHERE character_id=?", (character,))
                if SyncStore.character_mode(db, character) == "active_v2":
                    # The shadow's compatibility metadata was checked before
                    # activation. Retain no pre-cutover plaintext copy after
                    # the owner corrects or forgets any v2 source.
                    db.execute("DELETE FROM sync_items WHERE character_id=?", (character,))
            return result

    @staticmethod
    def _cursor_key(db: sqlite3.Connection) -> bytes:
        return bytes.fromhex(db.execute("SELECT value FROM companion_v2_metadata WHERE key='cursor_key'").fetchone()[0])

    @classmethod
    def _encode_cursor(cls, db: sqlite3.Connection, principal: str, character: str, seq: int, epoch: int) -> str:
        body = canonical({"principal": principal, "character": character, "seq": seq, "epoch": epoch}).encode()
        signature = hmac.digest(cls._cursor_key(db), body, "sha256")
        return base64.urlsafe_b64encode(body + signature).decode().rstrip("=")

    @classmethod
    def _decode_cursor(cls, db: sqlite3.Connection, token: str, principal: str, character: str) -> tuple[int, int]:
        try:
            raw = base64.urlsafe_b64decode(token + "=" * (-len(token) % 4))
            body, signature = raw[:-32], raw[-32:]
            if not hmac.compare_digest(signature, hmac.digest(cls._cursor_key(db), body, "sha256")):
                raise ValueError("invalid signature")
            value = json.loads(body)
            if value["principal"] != principal or value["character"] != character:
                raise ValueError("wrong scope")
            seq, epoch = value["seq"], value["epoch"]
            if type(seq) is not int or type(epoch) is not int or seq < 0 or epoch < 0:
                raise ValueError("invalid position")
            return seq, epoch
        except (ValueError, KeyError, TypeError, UnicodeDecodeError, base64.binascii.Error):
            raise HTTPException(410, {"code": "invalid_cursor"}) from None

    def changes(self, principal: str, character: str, cursor: str | None, limit: int):
        with self.connect() as db:
            db.execute("BEGIN")
            self.authorize(db, principal, character)
            state = db.execute("SELECT * FROM companion_v2_state WHERE character_id=?", (character,)).fetchone()
            head = state["head_seq"] if state else 0
            current_epoch = state["privacy_epoch"] if state else 0
            after, epoch = self._decode_cursor(db, cursor, principal, character) if cursor else (0, current_epoch)
            if epoch != current_epoch:
                raise HTTPException(410, {"code": "privacy_reset_required"})
            if after > head:
                raise HTTPException(410, {"code": "invalid_cursor"})
            rows = db.execute("SELECT * FROM companion_v2_changes WHERE character_id=? AND seq>? ORDER BY seq LIMIT ?",
                              (character, after, limit + 1)).fetchall()
            page = []
            size = 0
            for change in rows[:limit]:
                if change["entity_type"] == "suppression":
                    item = {"seq": change["seq"], "entity_type": "suppression",
                            "entity_id": change["entity_id"], "revision": change["revision"],
                            "suppressed": True}
                elif change["entity_type"] == "activity":
                    row = db.execute("SELECT * FROM companion_v2_activities WHERE id=?", (change["entity_id"],)).fetchone()
                    item = {"seq": change["seq"], "entity_type": "activity", "entity_id": row["id"],
                            "revision": row["revision"], "activity": {"conversation_id": row["conversation_id"],
                            "request": row["request"], "request_revision": row["request_revision"],
                            "state": row["state"], "stop_state": row["stop_state"],
                            "completion_basis": row["completion_basis"],
                            "binding_snapshot": json.loads(row["binding_snapshot"])}}
                elif change["entity_type"] == "profile":
                    row = db.execute("SELECT * FROM companion_v2_profiles WHERE id=?", (change["entity_id"],)).fetchone()
                    item = {"seq": change["seq"], "entity_type": "profile", "entity_id": row["id"],
                            "revision": row["revision"], "profile": {"field": row["field"],
                            "text": row["payload"]}}
                elif change["entity_type"] == "binding":
                    row = db.execute("SELECT * FROM companion_v2_bindings WHERE id=?", (change["entity_id"],)).fetchone()
                    item = {"seq": change["seq"], "entity_type": "binding", "entity_id": row["id"],
                            "revision": row["revision"], "binding": json.loads(row["payload"])}
                elif change["entity_type"] == "conversation":
                    row = db.execute("SELECT * FROM companion_v2_conversations WHERE id=?", (change["entity_id"],)).fetchone()
                    item = {"seq": change["seq"], "entity_type": "conversation", "entity_id": row["id"],
                            "revision": row["revision"], "conversation": {"purpose": row["purpose"],
                            "binding_id": row["binding_id"], "binding_snapshot": json.loads(row["binding_snapshot"])}}
                elif change["entity_type"] == "memory":
                    row = db.execute("SELECT * FROM companion_v2_memories WHERE id=?", (change["entity_id"],)).fetchone()
                    item = {"seq": change["seq"], "entity_type": "memory", "entity_id": change["entity_id"],
                            "revision": row["revision"], "state": row["state"],
                            "memory": json.loads(row["payload"]) if row["payload"] else None}
                else:
                    record = db.execute("SELECT * FROM companion_v2_records WHERE id=?", (change["entity_id"],)).fetchone()
                    item = {"seq": change["seq"], "entity_type": "record", "entity_id": change["entity_id"],
                            "revision": record["revision"], "deleted": bool(record["deleted"]),
                            "record": None if record["deleted"] else {**json.loads(record["payload"]),
                                       "author": record["author_principal"], "received_at": record["received_at"]}}
                item_size = len(canonical(item).encode("utf-8"))
                if page and size + item_size > 500 * 1024:
                    break
                page.append(item)
                size += item_size
            while True:
                next_seq = page[-1]["seq"] if page else after
                if not page and after < head:
                    raise HTTPException(413, {"code": "change_item_too_large"})
                result = {"items": page, "next_cursor": self._encode_cursor(db, principal, character, next_seq, current_epoch),
                          "privacy_epoch": current_epoch, "head_seq": head, "has_more": next_seq < head}
                if len(canonical(result).encode("utf-8")) <= MAX_PAGE_BYTES:
                    return result
                page.pop()

    def snapshot(self, principal: str, character: str, cursor: str | None):
        with self.connect() as db:
            db.execute("BEGIN IMMEDIATE")
            self.authorize(db, principal, character)
            now = time.time()
            db.execute("DELETE FROM companion_v2_snapshots WHERE expires_at<?", (now,))
            if cursor:
                token, sep, position_text = cursor.partition(":")
                if not sep or len(token) != 32 or not position_text.isdecimal():
                    raise HTTPException(410, {"code": "invalid_snapshot_cursor"})
                position = int(position_text)
                row = db.execute("SELECT * FROM companion_v2_snapshots WHERE token=?", (token,)).fetchone()
                if not row or row["principal"] != principal or row["character_id"] != character:
                    raise HTTPException(410, {"code": "invalid_snapshot_cursor"})
                state = db.execute("SELECT privacy_epoch FROM companion_v2_state WHERE character_id=?", (character,)).fetchone()
                if row["privacy_epoch"] != (state["privacy_epoch"] if state else 0):
                    raise HTTPException(410, {"code": "privacy_reset_required"})
            else:
                state = db.execute("SELECT * FROM companion_v2_state WHERE character_id=?", (character,)).fetchone()
                epoch = state["privacy_epoch"] if state else 0
                head = state["head_seq"] if state else 0
                token, position = secrets.token_hex(16), 0
                db.execute("INSERT INTO companion_v2_snapshots VALUES(?,?,?,?,?,?)",
                           (token, principal, character, epoch, head, now + 600))
                count, total_bytes = 0, 0
                for row in db.execute("SELECT * FROM companion_v2_records WHERE character_id=? ORDER BY id", (character,)):
                    item = {"entity_type": "record", "entity_id": row["id"], "revision": row["revision"],
                            "deleted": bool(row["deleted"]), "record": {**json.loads(row["payload"]),
                            "author": row["author_principal"], "received_at": row["received_at"]} if row["payload"] else None}
                    body = canonical(item)
                    total_bytes += len(body.encode("utf-8"))
                    if total_bytes > MAX_SNAPSHOT_BYTES:
                        raise HTTPException(413, {"code": "snapshot_too_large"})
                    db.execute("INSERT INTO companion_v2_snapshot_items VALUES(?,?,?)", (token, count, body))
                    count += 1
                for row in db.execute("SELECT * FROM companion_v2_memories WHERE character_id=? ORDER BY id", (character,)):
                    item = {"entity_type": "memory", "entity_id": row["id"], "revision": row["revision"],
                            "state": row["state"], "memory": json.loads(row["payload"]) if row["payload"] else None}
                    body = canonical(item)
                    total_bytes += len(body.encode("utf-8"))
                    if total_bytes > MAX_SNAPSHOT_BYTES:
                        raise HTTPException(413, {"code": "snapshot_too_large"})
                    db.execute("INSERT INTO companion_v2_snapshot_items VALUES(?,?,?)", (token, count, body))
                    count += 1
                for row in db.execute("SELECT * FROM companion_v2_suppressions WHERE character_id=? ORDER BY record_id", (character,)):
                    item = {"entity_type": "suppression", "entity_id": row["record_id"],
                            "revision": row["record_revision"], "suppressed": True}
                    body = canonical(item)
                    total_bytes += len(body.encode("utf-8"))
                    if total_bytes > MAX_SNAPSHOT_BYTES:
                        raise HTTPException(413, {"code": "snapshot_too_large"})
                    db.execute("INSERT INTO companion_v2_snapshot_items VALUES(?,?,?)", (token, count, body))
                    count += 1
                for row in db.execute("SELECT * FROM companion_v2_bindings WHERE character_id=? ORDER BY id", (character,)):
                    item = {"entity_type": "binding", "entity_id": row["id"], "revision": row["revision"],
                            "binding": json.loads(row["payload"])}
                    body = canonical(item)
                    total_bytes += len(body.encode("utf-8"))
                    if total_bytes > MAX_SNAPSHOT_BYTES:
                        raise HTTPException(413, {"code": "snapshot_too_large"})
                    db.execute("INSERT INTO companion_v2_snapshot_items VALUES(?,?,?)", (token, count, body))
                    count += 1
                for row in db.execute("SELECT * FROM companion_v2_conversations WHERE character_id=? ORDER BY id", (character,)):
                    item = {"entity_type": "conversation", "entity_id": row["id"], "revision": row["revision"],
                            "conversation": {"purpose": row["purpose"], "binding_id": row["binding_id"],
                            "binding_snapshot": json.loads(row["binding_snapshot"])}}
                    body = canonical(item)
                    total_bytes += len(body.encode("utf-8"))
                    if total_bytes > MAX_SNAPSHOT_BYTES:
                        raise HTTPException(413, {"code": "snapshot_too_large"})
                    db.execute("INSERT INTO companion_v2_snapshot_items VALUES(?,?,?)", (token, count, body))
                    count += 1
                for row in db.execute("SELECT * FROM companion_v2_profiles WHERE character_id=? ORDER BY field", (character,)):
                    item = {"entity_type": "profile", "entity_id": row["id"], "revision": row["revision"],
                            "profile": {"field": row["field"], "text": row["payload"]}}
                    body = canonical(item)
                    total_bytes += len(body.encode("utf-8"))
                    if total_bytes > MAX_SNAPSHOT_BYTES:
                        raise HTTPException(413, {"code": "snapshot_too_large"})
                    db.execute("INSERT INTO companion_v2_snapshot_items VALUES(?,?,?)", (token, count, body))
                    count += 1
                for row in db.execute("SELECT * FROM companion_v2_activities WHERE character_id=? ORDER BY id", (character,)):
                    item = {"entity_type": "activity", "entity_id": row["id"], "revision": row["revision"],
                            "activity": {"conversation_id": row["conversation_id"], "request": row["request"],
                            "request_revision": row["request_revision"], "state": row["state"],
                            "stop_state": row["stop_state"], "completion_basis": row["completion_basis"],
                            "binding_snapshot": json.loads(row["binding_snapshot"])}}
                    body = canonical(item)
                    total_bytes += len(body.encode("utf-8"))
                    if total_bytes > MAX_SNAPSHOT_BYTES:
                        raise HTTPException(413, {"code": "snapshot_too_large"})
                    db.execute("INSERT INTO companion_v2_snapshot_items VALUES(?,?,?)", (token, count, body))
                    count += 1
                db.execute("DELETE FROM companion_v2_snapshots WHERE token IN (SELECT token FROM companion_v2_snapshots WHERE principal=? AND character_id=? ORDER BY expires_at DESC,token DESC LIMIT -1 OFFSET 3)",
                           (principal, character))
                row = db.execute("SELECT * FROM companion_v2_snapshots WHERE token=?", (token,)).fetchone()
            page, size = [], 0
            for item in db.execute("SELECT position,payload FROM companion_v2_snapshot_items WHERE token=? AND position>=? ORDER BY position LIMIT 201",
                                   (token, position)):
                item_size = len(item["payload"].encode("utf-8"))
                if page and size + item_size > 500 * 1024:
                    break
                if len(page) == 200:
                    break
                page.append(json.loads(item["payload"]))
                size += item_size
            total = db.execute("SELECT count(*) FROM companion_v2_snapshot_items WHERE token=?", (token,)).fetchone()[0]
            if position > total:
                raise HTTPException(410, {"code": "invalid_snapshot_cursor"})
            while True:
                next_position = position + len(page)
                if not page and position < total:
                    raise HTTPException(413, {"code": "snapshot_item_too_large"})
                result = {"items": page, "next_cursor": f"{token}:{next_position}" if next_position < total else None,
                          "has_more": next_position < total, "head_seq": row["head_seq"],
                          "privacy_epoch": row["privacy_epoch"],
                          "change_cursor": self._encode_cursor(db, principal, character,
                                                               row["head_seq"], row["privacy_epoch"]),
                          "expires_in": max(0, int(row["expires_at"] - now))}
                if len(canonical(result).encode("utf-8")) <= MAX_PAGE_BYTES:
                    return result
                page.pop()
