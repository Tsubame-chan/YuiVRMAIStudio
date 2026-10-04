"""Prepared protocol-1 profile/history writes to the protocol-2 ledger.

Preparation is read-only. A future cutover planner persists the returned body
before dispatch, so retries replay identical v2 operation and batch IDs.
"""
from __future__ import annotations

import json
import re

from fastapi import HTTPException

from app.core.companion_shadow_migration import _mapped_id
from app.core.companion_v2 import (Commit, CompanionStore, MemoryPayload, Operation,
                                   RecordPayload, ProfilePayload, canonical)
from app.core.device_sync import Item


def _legacy_commit(batch_id: str, bodies: list[dict]) -> Commit:
    """Validate a bounded internal batch without changing the public 64-op API."""
    if not 1 <= len(bodies) <= 60000:
        raise HTTPException(413, {"code": "legacy_batch_too_large"})
    if len(canonical(bodies).encode("utf-8")) > 32 * 1024 * 1024:
        raise HTTPException(413, {"code": "legacy_batch_too_large"})
    # Commit validates its public 64-op envelope. Validate every operation in
    # that envelope, then combine only for this server-owned migration path.
    validated = []
    for start in range(0, len(bodies), 64):
        chunk = Commit.model_validate({"batch_id": batch_id,
                                       "operations": bodies[start:start + 64]})
        validated.extend(chunk.operations)
    if (len({op.op_id for op in validated}) != len(validated)
            or len({op.entity_id for op in validated}) != len(validated)):
        raise HTTPException(422, {"code": "duplicate_legacy_operation"})
    return Commit.model_construct(batch_id=batch_id, operations=validated)


def prepare_legacy_change(store: CompanionStore, principal: str, character: str,
                          plan_id: str, item: Item, pending_sources: dict | None = None) -> dict:
    if not re.fullmatch(r"[a-f0-9]{32}", plan_id):
        raise HTTPException(422, {"code": "invalid_plan_id"})
    with store.connect() as db:
        db.execute("BEGIN")
        store.authorize(db, principal, character)
        imported = db.execute("SELECT 1 FROM companion_v2_legacy_imports WHERE character_id=?",
                              (character,)).fetchone()
        if not imported:
            raise HTTPException(409, {"code": "character_not_imported"})
        native_id = item.id.startswith("v2:") and re.fullmatch(r"v2:[a-f0-9]{32}", item.id)
        if item.id.startswith("v2:") and not native_id:
            raise HTTPException(422, {"code": "invalid_native_record_id"})
        if native_id and item.kind != "history":
            raise HTTPException(409, {"code": "native_item_kind_mismatch"})
        entity_id = item.id[3:] if native_id else _mapped_id(
            store.sync.server_id, character, item.kind, item.id)
        mapped = db.execute("SELECT legacy_version FROM companion_v2_legacy_map "
                            "WHERE character_id=? AND kind=? AND legacy_id=? AND entity_id=?",
                            (character, item.kind, item.id, entity_id)).fetchone()
        if item.kind == "profile":
            current = db.execute("SELECT revision FROM companion_v2_profiles WHERE id=?", (entity_id,)).fetchone()
        elif item.kind == "history":
            current = db.execute("SELECT revision FROM companion_v2_records WHERE id=?", (entity_id,)).fetchone()
        elif item.kind == "memory":
            current = db.execute("SELECT revision FROM companion_v2_memories WHERE id=?", (entity_id,)).fetchone()
        else:
            current = None
        visible_version = ((mapped["legacy_version"] + current["revision"] - 1)
                           if mapped and current else current["revision"] if current else 0)
        if item.base_version != visible_version:
            raise HTTPException(409, {"code": "legacy_version_conflict"})
        operations = []
        metadata = {}
        update_metadata = False
        op_id = _mapped_id(store.sync.server_id, character, "legacy_operation", plan_id + ":" + item.kind + ":" + item.id)
        batch_id = _mapped_id(store.sync.server_id, character, "legacy_batch", plan_id + ":" + item.kind + ":" + item.id)
        if item.kind == "memory":
            row = db.execute("SELECT character_id,revision,state,payload FROM companion_v2_memories WHERE id=?",
                             (entity_id,)).fetchone()
            if row and (row["character_id"] != character or row["state"] != "active"):
                raise HTTPException(409, {"code": "legacy_memory_unavailable"})
            if row and item.id.startswith("auto-history:") and not item.deleted:
                raise HTTPException(409, {"code": "automatic_memory_requires_new_id"})
            old_source = None
            old_source_owned = True
            previous_metadata = db.execute(
                "SELECT metadata FROM companion_v2_legacy_metadata WHERE character_id=? AND kind='memory' AND legacy_id=?",
                (character, item.id)).fetchone()
            old_meta = json.loads(previous_metadata["metadata"]) if previous_metadata else {}
            was_linked = bool(old_meta.get("source_ids"))
            if row:
                payload = json.loads(row["payload"])
                if payload["basis"] != "legacy_unknown":
                    raise HTTPException(409, {"code": "legacy_memory_source_shared"})
                if was_linked:
                    expected = old_meta.get("source_record_refs") or [
                        {"record_id": _mapped_id(store.sync.server_id, character, "memory_source", source_id),
                         "revision": 1} for source_id in old_meta["source_ids"]]
                    if [{"record_id": x["record_id"], "revision": x["revision"]}
                            for x in payload["source_refs"]] != expected:
                        raise HTTPException(409, {"code": "legacy_memory_source_shared"})
                else:
                    original_source = _mapped_id(store.sync.server_id, character, "memory_source", item.id)
                    if len(payload["source_refs"]) != 1:
                        raise HTTPException(409, {"code": "legacy_memory_source_shared"})
                    old_source = payload["source_refs"][0]["record_id"]
                    old_source_owned = old_meta.get("source_owned", True)
                    if old_source != old_meta.get("source_record_id", original_source):
                        raise HTTPException(409, {"code": "legacy_memory_source_shared"})
                    source = db.execute("SELECT revision,deleted FROM companion_v2_records WHERE id=? AND character_id=?",
                                        (old_source, character)).fetchone()
                    if not source or source["deleted"]:
                        raise HTTPException(409, {"code": "legacy_memory_source_shared"})
            elif item.deleted:
                raise HTTPException(409, {"code": "legacy_memory_unavailable"})
            if item.deleted:
                operations.append(Operation(op_id=op_id, type="delete_memory", entity_id=entity_id,
                                            expected_revision=row["revision"]))
            else:
                linked = bool(item.value.get("source_ids"))
                if row and linked != was_linked:
                    raise HTTPException(409, {"code": "legacy_memory_basis_change"})
                if linked:
                    source_refs = []
                    for source_id, expected_version in zip(item.value["source_ids"],
                                                            item.value["source_versions"]):
                        pending = (pending_sources or {}).get(source_id)
                        if pending:
                            if expected_version != pending["version"]:
                                raise HTTPException(409, {"code": "legacy_connection_source_stale"})
                            source_refs.append({"record_id": pending["record_id"], "revision": 1})
                            continue
                        source_map = db.execute(
                            "SELECT entity_id,legacy_version FROM companion_v2_legacy_map "
                            "WHERE character_id=? AND kind='memory' AND legacy_id=?",
                            (character, source_id)).fetchone()
                        source_memory = db.execute(
                            "SELECT revision,state,payload FROM companion_v2_memories WHERE id=?",
                            (source_map["entity_id"],)).fetchone() if source_map else None
                        if not source_memory or source_memory["state"] != "active" or (
                            source_map["legacy_version"] + source_memory["revision"] - 1 != expected_version):
                            raise HTTPException(409, {"code": "legacy_connection_source_stale"})
                        source_payload = json.loads(source_memory["payload"])
                        if source_payload["basis"] != "legacy_unknown" or len(source_payload["source_refs"]) != 1:
                            raise HTTPException(409, {"code": "legacy_connection_source_not_original"})
                        source_refs.append({"record_id": source_payload["source_refs"][0]["record_id"],
                                            "revision": source_payload["source_refs"][0]["revision"]})
                    metadata = {"recorded_utc": item.value["recorded_utc"],
                                "source_ids": item.value["source_ids"],
                                "source_versions": item.value["source_versions"],
                                "basis": item.value["basis"],
                                "source_record_refs": source_refs}
                else:
                    new_source = _mapped_id(store.sync.server_id, character, "legacy_memory_revision_source",
                                            plan_id + ":" + item.id)
                    observed = RecordPayload(kind="observation", conversation_id=None,
                                             text=item.value["content"], recorded_at=item.value["recorded_utc"],
                                             realm="real")
                    operations.append(Operation(op_id=_mapped_id(store.sync.server_id, character,
                                                                  "legacy_source_append_op", plan_id + ":" + item.id),
                                                type="append_record", entity_id=new_source,
                                                expected_revision=0, payload=observed))
                    source_refs = [{"record_id": new_source, "revision": 1}]
                    metadata = {"recorded_utc": item.value["recorded_utc"],
                                "source_record_id": new_source}
                memory = MemoryPayload(type="reflection", subject="legacy_unknown",
                                       text=item.value["content"], basis="legacy_unknown",
                                       pinned=item.value["pinned"], source_refs=source_refs)
                operations.append(Operation(op_id=op_id, type="put_memory", entity_id=entity_id,
                                            expected_revision=row["revision"] if row else 0,
                                            payload=memory))
                update_metadata = True
            if old_source and old_source_owned:
                operations.append(Operation(op_id=_mapped_id(store.sync.server_id, character,
                                                              "legacy_source_delete_op", plan_id + ":" + item.id),
                                            type="delete_record", entity_id=old_source,
                                            expected_revision=source["revision"]))
        elif item.kind == "profile":
            if item.deleted:
                raise HTTPException(409, {"code": "profile_cannot_be_deleted"})
            row = db.execute("SELECT character_id,revision,field FROM companion_v2_profiles WHERE id=?",
                             (entity_id,)).fetchone()
            if row and (row["character_id"] != character or row["field"] != item.id):
                raise HTTPException(409, {"code": "profile_identity_conflict"})
            payload = ProfilePayload(field=item.id, text=item.value["text"])
            operations.append(Operation(op_id=op_id, type="update_profile", entity_id=entity_id,
                                        expected_revision=row["revision"] if row else 0, payload=payload))
        else:
            row = db.execute("SELECT character_id,revision,deleted,payload FROM companion_v2_records WHERE id=?",
                             (entity_id,)).fetchone()
            if row and row["character_id"] != character:
                raise HTTPException(409, {"code": "record_identity_conflict"})
            if native_id and not row:
                raise HTTPException(409, {"code": "native_record_unavailable"})
            if row and row["deleted"] and not item.deleted:
                raise HTTPException(409, {"code": "record_deleted"})
            if item.deleted:
                if not row or row["deleted"]:
                    raise HTTPException(409, {"code": "record_unavailable"})
                operations.append(Operation(op_id=op_id, type="delete_record", entity_id=entity_id,
                                            expected_revision=row["revision"]))
            else:
                speaker = item.value["speaker"]
                if not speaker.strip() or (item.id.endswith(":user") and speaker != "You") or (
                    item.id.endswith(":assistant") and speaker == "You"):
                    raise HTTPException(422, {"code": "history_role_unmappable"})
                mode = item.value["mode"]
                if mode not in {"talk", "work"}:
                    raise HTTPException(422, {"code": "history_mode_unmappable"})
                legacy_conversation = item.value["conversation_id"]
                native_conversation = legacy_conversation.startswith("v2:") and re.fullmatch(
                    r"v2:[a-f0-9]{32}", legacy_conversation)
                conversation_id = (legacy_conversation[3:] if native_conversation else
                                   _mapped_id(store.sync.server_id, character,
                                              "conversation:" + mode, legacy_conversation))
                conversation = db.execute("SELECT character_id,purpose FROM companion_v2_conversations WHERE id=?",
                                          (conversation_id,)).fetchone()
                if conversation and (conversation["character_id"] != character or conversation["purpose"] != mode):
                    raise HTTPException(409, {"code": "conversation_identity_conflict"})
                if native_conversation and not conversation:
                    raise HTTPException(409, {"code": "conversation_unavailable"})
                if not conversation:
                    operations.append(Operation(op_id=_mapped_id(store.sync.server_id, character,
                                                                 "legacy_conversation_op", plan_id + ":" + item.id),
                                                type="create_conversation", entity_id=conversation_id,
                                                expected_revision=0,
                                                payload={"purpose": mode, "binding_id": None}))
                turn_id = item.value["turn_id"]
                payload = RecordPayload(kind="user_utterance" if speaker == "You" else "assistant_utterance",
                                        conversation_id=conversation_id,
                                        turn_id=turn_id if re.fullmatch(r"[a-f0-9]{32}", turn_id) else None,
                                        text=item.value["text"], recorded_at=item.value["recorded_utc"], realm="real")
                operations.append(Operation(op_id=op_id, type="correct_record" if row else "append_record",
                                            entity_id=entity_id, expected_revision=row["revision"] if row else 0,
                                            payload=payload))
                metadata = {"mode": mode, "conversation_id": legacy_conversation,
                            "turn_id": turn_id, "speaker": speaker}
                update_metadata = True
        prepared = {"character_id": character, "kind": item.kind, "legacy_id": item.id,
                    "entity_id": entity_id, "batch_id": batch_id,
                    "operations": [operation.model_dump(mode="json") for operation in operations],
                    "metadata": metadata, "origin_device": principal,
                    "new_mapping": not bool(mapped) and not native_id,
                    "update_metadata": update_metadata}
        return prepared


def apply_prepared_legacy_change(store: CompanionStore, principal: str, prepared: dict) -> dict:
    character = prepared["character_id"]
    batch = _legacy_commit(prepared["batch_id"], prepared["operations"])
    return store.commit(principal, character, batch,
                        legacy_mapping=lambda db: _write_legacy_mappings(db, character, [prepared]))


def _write_legacy_mappings(db, character: str, changes: list[dict]):
    for change in changes:
        if change["new_mapping"]:
            db.execute("INSERT OR IGNORE INTO companion_v2_legacy_map VALUES(?,?,?,?,?)",
                       (character, change["kind"], change["legacy_id"], change["entity_id"], 1))
        if change["new_mapping"] or change["update_metadata"]:
            db.execute("INSERT INTO companion_v2_legacy_metadata VALUES(?,?,?,?,?) "
                       "ON CONFLICT(character_id,kind,legacy_id) DO UPDATE SET "
                       "metadata=excluded.metadata,origin_device=excluded.origin_device",
                       (character, change["kind"], change["legacy_id"],
                        canonical(change["metadata"]), change["origin_device"]))
        if not change["new_mapping"]:
            continue
        row = db.execute("SELECT entity_id FROM companion_v2_legacy_map WHERE character_id=? AND kind=? AND legacy_id=?",
                         (character, change["kind"], change["legacy_id"])).fetchone()
        if row["entity_id"] != change["entity_id"]:
            raise HTTPException(409, {"code": "legacy_mapping_conflict"})


def prepare_legacy_changes(store: CompanionStore, principal: str, character: str,
                           plan_id: str, items: list[Item], *,
                           link_backend_turns: bool = False) -> dict:
    """Prepare a whole legacy commit with one conversation creation per conversation."""
    if len({(item.kind, item.id) for item in items}) != len(items):
        raise HTTPException(422, {"code": "duplicate_legacy_item"})
    ordered = sorted(items, key=lambda item: item.kind == "memory" and bool(item.value.get("source_ids")))
    pending_sources = {}
    deleting_sources = set()
    changes = []
    for item in ordered:
        if (item.kind == "memory" and item.value.get("source_ids")
                and set(item.value["source_ids"]) & deleting_sources):
            raise HTTPException(409, {"code": "legacy_connection_source_deleted"})
        change = prepare_legacy_change(store, principal, character, plan_id, item,
                                       pending_sources)
        changes.append(change)
        if item.kind == "memory" and not item.value.get("source_ids"):
            if item.deleted:
                deleting_sources.add(item.id)
            else:
                pending_sources[item.id] = {"record_id": change["metadata"]["source_record_id"],
                                            "version": item.base_version + 1}
    if link_backend_turns:
        history = {change["legacy_id"]: change for change in changes
                   if change["kind"] == "history" and change["new_mapping"]}
        for change in changes:
            if change["kind"] != "memory" or not change["new_mapping"] or not change["legacy_id"].startswith("backend:"):
                continue
            user = history.get(change["legacy_id"] + ":user")
            if not user:
                continue
            source = next((op for op in user["operations"] if op["type"] == "append_record"), None)
            memory = next((op for op in change["operations"] if op["type"] == "put_memory"), None)
            if not source or not memory or source["payload"]["text"] != memory["payload"]["text"]:
                continue
            owned_source = change["metadata"]["source_record_id"]
            change["operations"] = [op for op in change["operations"]
                                    if not (op["type"] == "append_record" and op["entity_id"] == owned_source)]
            memory["payload"]["source_refs"] = [{"record_id": user["entity_id"], "revision": 1}]
            change["metadata"]["source_record_id"] = user["entity_id"]
            change["metadata"]["source_owned"] = False
    history_changes = {change["legacy_id"]: change for change in changes if change["kind"] == "history"}
    for change in changes:
        if change["kind"] != "memory" or not change["legacy_id"].startswith("auto-history:") or not change["new_mapping"]:
            continue
        history_id = change["legacy_id"].removeprefix("auto-history:")
        if not re.fullmatch(r"[a-f0-9]{32}", history_id):
            raise HTTPException(409, {"code": "automatic_memory_source_unavailable"})
        memory = next((op for op in change["operations"] if op["type"] == "put_memory"), None)
        if memory is None:
            continue
        current = history_changes.get(history_id)
        if current:
            source = next((op for op in current["operations"] if op["type"] == "append_record"), None)
            if not source:
                raise HTTPException(409, {"code": "automatic_memory_source_unavailable"})
            source_id, source_revision, source_payload = current["entity_id"], 1, source["payload"]
        else:
            with store.connect() as db:
                mapped = db.execute("SELECT entity_id FROM companion_v2_legacy_map "
                                    "WHERE character_id=? AND kind='history' AND legacy_id=?",
                                    (character, history_id)).fetchone()
                record = db.execute("SELECT revision,deleted,payload FROM companion_v2_records "
                                    "WHERE id=? AND character_id=?", (mapped["entity_id"], character)).fetchone() if mapped else None
            if not record or record["deleted"]:
                raise HTTPException(409, {"code": "automatic_memory_source_unavailable"})
            source_id, source_revision, source_payload = mapped["entity_id"], record["revision"], json.loads(record["payload"])
        if source_payload["kind"] != "user_utterance" or source_payload["text"] != memory["payload"]["text"]:
            raise HTTPException(409, {"code": "automatic_memory_source_unavailable"})
        owned_source = change["metadata"]["source_record_id"]
        change["operations"] = [op for op in change["operations"]
                                if not (op["type"] == "append_record" and op["entity_id"] == owned_source)]
        memory["payload"]["source_refs"] = [{"record_id": source_id, "revision": source_revision}]
        change["metadata"]["source_record_id"] = source_id
        change["metadata"]["source_owned"] = False
    operations = []
    deferred_source_deletes = []
    conversations = {}
    for change in changes:
        for operation in change["operations"]:
            if change["kind"] == "memory" and operation["type"] == "delete_record":
                deferred_source_deletes.append(operation)
                continue
            if operation["type"] == "create_conversation":
                prior = conversations.get(operation["entity_id"])
                if prior:
                    if prior["payload"] != operation["payload"]:
                        raise HTTPException(409, {"code": "conversation_identity_conflict"})
                    continue
                conversations[operation["entity_id"]] = operation
            operations.append(operation)
    operations.extend(deferred_source_deletes)
    return {"character_id": character,
            "batch_id": _mapped_id(store.sync.server_id, character, "legacy_plan_batch", plan_id),
            "operations": operations, "changes": changes}


def apply_prepared_legacy_changes(store: CompanionStore, principal: str, prepared: dict,
                                  finalize=None, expected_head_seq: int | None = None) -> dict:
    """Replay a whole prepared plan as one atomic v2 commit."""
    character = prepared["character_id"]
    batch = _legacy_commit(prepared["batch_id"], prepared["operations"])
    def same_transaction(db):
        _write_legacy_mappings(db, character, prepared["changes"])
        if finalize is not None:
            finalize(db)
    return store.commit(principal, character, batch, legacy_mapping=same_transaction,
                        expected_head_seq=expected_head_seq)
