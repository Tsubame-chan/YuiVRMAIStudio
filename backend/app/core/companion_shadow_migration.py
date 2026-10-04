"""One-way, opt-in shadow import of a validated protocol-1 character.

This does not activate protocol 2 or make protocol 1 writes dual-write. A
subsequent legacy change requires a new migration strategy, never a silent
overwrite of the imported ledger.
"""
from __future__ import annotations

import hashlib
import json
import re
from datetime import datetime, timezone

from fastapi import HTTPException

from app.core.companion_migration import preview_character, _role
from app.core.companion_v2 import CompanionStore, RecordPayload, MemoryPayload, canonical


def _mapped_id(hub: str, character: str, kind: str, legacy_id: str) -> str:
    return hashlib.sha256(f"{hub}\0{character}\0{kind}\0{legacy_id}".encode()).hexdigest()[:32]


def shadow_import_character(database_url: str, character_id: str, expected_digest: str,
                            role_overrides: dict[str, str] | None = None) -> dict:
    if len(expected_digest) != 64 or any(c not in "0123456789abcdef" for c in expected_digest):
        raise HTTPException(422, {"code": "invalid_digest"})
    report = preview_character(database_url, character_id)
    if report["legacy_digest"] != expected_digest:
        raise HTTPException(409, {"code": "legacy_changed"})
    role_overrides = role_overrides or {}
    ambiguous = {issue["item"].removeprefix("history:") for issue in report["issues"]
                 if issue["code"] == "ambiguous_role"}
    if (set(role_overrides) != ambiguous or any(role not in {"user", "assistant"} for role in role_overrides.values())
            or any(issue["code"] != "ambiguous_role" for issue in report["issues"])):
        raise HTTPException(409, {"code": "migration_preview_has_issues", "issues": report["issues"]})
    role_mapping_digest = hashlib.sha256(canonical(role_overrides).encode()).hexdigest()
    store = CompanionStore(database_url)
    with store.connect() as db:
        db.execute("BEGIN IMMEDIATE")
        current = db.execute("SELECT revision FROM sync_characters WHERE id=?", (character_id,)).fetchone()
        rows = list(db.execute("SELECT * FROM sync_items WHERE character_id=? ORDER BY kind,id", (character_id,)))
        digest = hashlib.sha256()
        for row in rows:
            digest.update(json.dumps([row["kind"], row["id"], row["version"], row["deleted"], row["value_json"]],
                                     ensure_ascii=False, separators=(",", ":")).encode("utf-8"))
        if not current or current["revision"] != report["legacy_revision"] or digest.hexdigest() != expected_digest:
            raise HTTPException(409, {"code": "legacy_changed"})
        prior = db.execute("SELECT * FROM companion_v2_legacy_imports WHERE character_id=?", (character_id,)).fetchone()
        if prior:
            if prior["legacy_digest"] != expected_digest or prior["role_mapping_digest"] != role_mapping_digest:
                raise HTTPException(409, {"code": "legacy_changed_after_import"})
            return {"status": "already_imported", "legacy_digest": expected_digest,
                    "head_seq": db.execute("SELECT head_seq FROM companion_v2_state WHERE character_id=?",
                                           (character_id,)).fetchone()[0]}
        state = db.execute("SELECT head_seq FROM companion_v2_state WHERE character_id=?", (character_id,)).fetchone()
        if state and state["head_seq"]:
            raise HTTPException(409, {"code": "v2_character_not_empty"})
        hub = store.sync.server_id
        history_by_id = {row["id"]: row for row in rows if row["kind"] == "history" and not row["deleted"]}
        automatic_sources = {}
        for row in rows:
            if row["kind"] != "memory" or row["deleted"] or not row["id"].startswith("auto-history:"):
                continue
            history_id = row["id"].removeprefix("auto-history:")
            source = history_by_id.get(history_id)
            source_value = json.loads(source["value_json"]) if source else None
            memory_value = json.loads(row["value_json"])
            if (not re.fullmatch(r"[a-f0-9]{32}", history_id) or not source_value or
                    source_value["speaker"] != "You" or source_value["text"] != memory_value["content"] or
                    memory_value.get("source_ids")):
                raise HTTPException(409, {"code": "automatic_memory_source_unavailable", "memory_id": row["id"]})
            automatic_sources[row["id"]] = history_id
        db.execute("INSERT OR IGNORE INTO companion_v2_state(character_id) VALUES(?)", (character_id,))
        seq = 0
        mapped = {}
        conversations = {}

        def record_change(entity_type: str, entity_id: str, deleted: bool = False):
            nonlocal seq
            seq += 1
            db.execute("INSERT INTO companion_v2_changes(character_id,seq,entity_id,revision,deleted,entity_type) VALUES(?,?,?,?,?,?)",
                       (character_id, seq, entity_id, 1, int(deleted), entity_type))

        def map_item(row, entity_id):
            db.execute("INSERT INTO companion_v2_legacy_map VALUES(?,?,?,?,?)",
                       (character_id, row["kind"], row["id"], entity_id, row["version"]))
            value = json.loads(row["value_json"]) if not row["deleted"] else {}
            if row["kind"] == "history":
                metadata = {key: value[key] for key in ("mode", "conversation_id", "turn_id", "speaker") if key in value}
            elif row["kind"] == "memory":
                metadata = {key: value[key] for key in
                            ("recorded_utc", "source_ids", "source_versions", "basis") if key in value}
                if row["id"] in automatic_sources:
                    metadata["source_record_id"] = _mapped_id(hub, character_id, "history", automatic_sources[row["id"]])
                    metadata["source_owned"] = False
            else:
                metadata = {}
            db.execute("INSERT INTO companion_v2_legacy_metadata VALUES(?,?,?,?,?)",
                       (character_id, row["kind"], row["id"], canonical(metadata), row["origin_device"]))
            mapped[(row["kind"], row["id"])] = entity_id

        def migration_order(row):
            if row["kind"] != "history":
                return (1 if row["kind"] == "memory" else 2, row["id"])
            if row["deleted"]:
                return (0, 1, "", "", "", 2, row["id"])
            value = json.loads(row["value_json"])
            instant = datetime.fromisoformat(value["recorded_utc"].replace("Z", "+00:00")).astimezone(timezone.utc)
            role, _ = _role(row, value)
            role = role or role_overrides[row["id"]]
            return (0, 0, instant.isoformat(), value["conversation_id"], value["turn_id"],
                    0 if role == "user" else 1, row["id"])

        for row in rows:
            if row["kind"] == "history" and not row["deleted"]:
                value = json.loads(row["value_json"])
                key = (value["conversation_id"], value["mode"])
                if key not in conversations:
                    conversation_id = _mapped_id(hub, character_id, "conversation:" + key[1], key[0])
                    db.execute("INSERT INTO companion_v2_conversations VALUES(?,?,?,?,?,?)",
                               (conversation_id, character_id, 1, key[1], None,
                                canonical({"connection_id": None, "context_policy": "private_only"})))
                    record_change("conversation", conversation_id)
                    conversations[key] = conversation_id

        for row in sorted(rows, key=migration_order):
            kind, legacy_id = row["kind"], row["id"]
            entity_id = _mapped_id(hub, character_id, kind, legacy_id)
            map_item(row, entity_id)
            if kind == "profile":
                if row["deleted"]:
                    continue
                value = json.loads(row["value_json"])
                db.execute("INSERT INTO companion_v2_profiles VALUES(?,?,?,?,?)",
                           (entity_id, character_id, legacy_id, 1, value["text"]))
                record_change("profile", entity_id)
                continue
            if kind == "history":
                if row["deleted"]:
                    db.execute("INSERT INTO companion_v2_records(id,character_id,revision,deleted,payload,author_principal,received_at) VALUES(?,?,?,?,?,?,?)",
                               (entity_id, character_id, 1, 1, None, "legacy_unknown", ""))
                    record_change("record", entity_id, True)
                    continue
                value = json.loads(row["value_json"])
                role, _ = _role(row, value)
                role = role or role_overrides[row["id"]]
                payload = RecordPayload(kind="user_utterance" if role == "user" else "assistant_utterance",
                                        conversation_id=conversations[(value["conversation_id"], value["mode"])],
                                        text=value["text"], recorded_at=value["recorded_utc"], realm="real")
                body = canonical(payload.model_dump(mode="json"))
                db.execute("INSERT INTO companion_v2_records(id,character_id,revision,deleted,payload,author_principal,received_at) VALUES(?,?,?,?,?,?,?)",
                           (entity_id, character_id, 1, 0, body, "legacy:" + role, datetime.now(timezone.utc).isoformat()))
                db.execute("INSERT INTO companion_v2_record_revisions(record_id,revision,payload,editor_principal,received_at) VALUES(?,?,?,?,?)",
                           (entity_id, 1, body, "legacy:" + role, datetime.now(timezone.utc).isoformat()))
                record_change("record", entity_id)
            elif kind == "memory":
                # A legacy memory's content is an observation of an old store,
                # not a newly attributed user utterance.
                synthetic_id = _mapped_id(hub, character_id, "memory_source", legacy_id)
                if row["deleted"]:
                    db.execute("INSERT INTO companion_v2_records(id,character_id,revision,deleted,payload,author_principal,received_at) VALUES(?,?,?,?,?,?,?)",
                               (synthetic_id, character_id, 1, 1, None, "legacy_unknown", ""))
                    record_change("record", synthetic_id, True)
                    db.execute("INSERT INTO companion_v2_memories VALUES(?,?,?,?,?)",
                               (entity_id, character_id, 1, "deleted", None))
                    record_change("memory", entity_id, True)
                    continue
                value = json.loads(row["value_json"])
                if legacy_id in automatic_sources:
                    continue
                source = RecordPayload(kind="observation", conversation_id=None, text=value["content"],
                                       recorded_at=value["recorded_utc"], realm="real")
                body = canonical(source.model_dump(mode="json"))
                now = datetime.now(timezone.utc).isoformat()
                db.execute("INSERT INTO companion_v2_records(id,character_id,revision,deleted,payload,author_principal,received_at) VALUES(?,?,?,?,?,?,?)",
                           (synthetic_id, character_id, 1, 0, body, "legacy:memory", now))
                db.execute("INSERT INTO companion_v2_record_revisions(record_id,revision,payload,editor_principal,received_at) VALUES(?,?,?,?,?)",
                           (synthetic_id, 1, body, "legacy:memory", now))
                record_change("record", synthetic_id)

        for row in rows:
            if row["kind"] != "memory" or row["deleted"]:
                continue
            value = json.loads(row["value_json"])
            source_ids = value.get("source_ids") or [row["id"]]
            source_refs = [{"record_id": (_mapped_id(hub, character_id, "history", automatic_sources[row["id"]])
                                           if row["id"] in automatic_sources else
                                           _mapped_id(hub, character_id, "memory_source", source_id)),
                            "revision": 1} for source_id in source_ids]
            payload = MemoryPayload(type="reflection", subject="legacy_unknown", text=value["content"],
                                    basis="legacy_unknown", pinned=value["pinned"], source_refs=source_refs)
            entity_id = mapped[("memory", row["id"])]
            db.execute("INSERT INTO companion_v2_memories VALUES(?,?,?,?,?)",
                       (entity_id, character_id, 1, "active", canonical(payload.model_dump(mode="json"))))
            for ref in source_refs:
                db.execute("INSERT INTO companion_v2_sources VALUES(?,?,?,?)",
                           (entity_id, ref["record_id"], 1, None))
            record_change("memory", entity_id)
        db.execute("UPDATE companion_v2_state SET head_seq=? WHERE character_id=?", (seq, character_id))
        db.execute("INSERT INTO companion_v2_legacy_imports VALUES(?,?,?,?,?)",
                   (character_id, expected_digest, current["revision"], datetime.now(timezone.utc).isoformat(),
                    role_mapping_digest))
        return {"status": "shadow_imported", "legacy_digest": expected_digest,
                "head_seq": seq, "counts": report["counts"]}
