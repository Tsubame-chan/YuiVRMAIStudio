"""Loss-aware protocol-1 view of an imported protocol-2 character.

This is a read-only building block for the cutover adapter. It never writes to
sync_items or silently flattens source-backed protocol-2 memories.
"""
from __future__ import annotations

import json

from fastapi import HTTPException

from app.core.companion_v2 import CompanionStore
from app.core.device_sync import encoded
from app.core.companion_shadow_migration import _mapped_id


def project_legacy_snapshot(store: CompanionStore, principal: str, character: str) -> dict:
    with store.connect() as db:
        db.execute("BEGIN")
        store.authorize(db, principal, character)
        imported = db.execute("SELECT legacy_revision FROM companion_v2_legacy_imports WHERE character_id=?",
                              (character,)).fetchone()
        if not imported:
            raise HTTPException(409, {"code": "character_not_imported"})
        state = db.execute("SELECT head_seq FROM companion_v2_state WHERE character_id=?",
                           (character,)).fetchone()
        mapped = {(row["kind"], row["entity_id"]): row for row in db.execute(
            "SELECT kind,legacy_id,entity_id,legacy_version FROM companion_v2_legacy_map WHERE character_id=?",
            (character,))}
        legacy_meta = {(row["kind"], row["legacy_id"]): row for row in db.execute(
            "SELECT kind,legacy_id,metadata,origin_device FROM companion_v2_legacy_metadata "
            "WHERE character_id=?", (character,))}
        # Imports made before legacy_metadata existed still need a read-only
        # compatibility path. New imports never duplicate private text here.
        legacy = {(row["kind"], row["id"]): row for row in db.execute(
            "SELECT kind,id,value_json,origin_device FROM sync_items WHERE character_id=?", (character,))}
        conversations = {row["id"]: row["purpose"] for row in db.execute(
            "SELECT id,purpose FROM companion_v2_conversations WHERE character_id=?", (character,))}
        items = []

        def identity(kind: str, entity_id: str, revision: int):
            old = mapped.get((kind, entity_id))
            legacy_id = old["legacy_id"] if old else "v2:" + entity_id
            metadata = legacy_meta.get((kind, legacy_id))
            fallback = legacy.get((kind, legacy_id)) if not metadata else None
            version = old["legacy_version"] + revision - 1 if old else revision
            origin = (metadata["origin_device"] if metadata else
                      fallback["origin_device"] if fallback else "companion-v2")
            data = (json.loads(metadata["metadata"]) if metadata else
                    json.loads(fallback["value_json"]) if fallback else None)
            return legacy_id, version, origin, data

        for row in db.execute("SELECT id,field,revision,payload FROM companion_v2_profiles "
                              "WHERE character_id=? ORDER BY field", (character,)):
            legacy_id, version, origin, _ = identity("profile", row["id"], row["revision"])
            if ("profile", row["id"]) not in mapped:
                legacy_id = row["field"]
            if legacy_id != row["field"]:
                raise HTTPException(409, {"code": "profile_unrepresentable"})
            items.append({"kind": "profile", "id": legacy_id, "version": version,
                          "deleted": False, "value": {"text": row["payload"]}, "origin_device": origin})

        for row in db.execute("SELECT id,revision,deleted,payload,author_principal FROM companion_v2_records "
                              "WHERE character_id=? ORDER BY id", (character,)):
            old = mapped.get(("history", row["id"]))
            if row["deleted"]:
                if old:
                    legacy_id, version, origin, _ = identity("history", row["id"], row["revision"])
                    items.append({"kind": "history", "id": legacy_id, "version": version,
                                  "deleted": True, "value": {}, "origin_device": origin})
                continue
            payload = json.loads(row["payload"])
            if payload["kind"] not in {"user_utterance", "assistant_utterance"} or payload["realm"] != "real":
                if old:
                    raise HTTPException(409, {"code": "history_unrepresentable"})
                continue
            legacy_id, version, origin, metadata = identity("history", row["id"], row["revision"])
            if metadata:
                mode, conversation_id, turn_id = (metadata["mode"], metadata["conversation_id"],
                                                  metadata["turn_id"])
            else:
                conversation_id = payload.get("conversation_id")
                mode = conversations.get(conversation_id)
                if mode not in {"talk", "work"}:
                    raise HTTPException(409, {"code": "history_conversation_unrepresentable"})
                conversation_id = "v2:" + conversation_id
                turn_id = payload.get("turn_id") or row["id"]
                if origin == "companion-v2" and row["author_principal"].startswith("device:"):
                    origin = row["author_principal"].removeprefix("device:")
            items.append({"kind": "history", "id": legacy_id, "version": version,
                          "deleted": False, "value": {"text": payload["text"],
                          "speaker": metadata.get("speaker") if metadata and metadata.get("speaker") else
                                     ("You" if payload["kind"] == "user_utterance" else "Assistant"),
                          "mode": mode, "recorded_utc": payload["recorded_at"],
                          "conversation_id": conversation_id, "turn_id": turn_id},
                          "origin_device": origin})

        for row in db.execute("SELECT id,revision,state,payload FROM companion_v2_memories "
                              "WHERE character_id=? ORDER BY id", (character,)):
            old = mapped.get(("memory", row["id"]))
            if not old:
                continue  # A new source-backed Memory has no safe protocol-1 representation.
            legacy_id, version, origin, metadata = identity("memory", row["id"], row["revision"])
            if row["state"] != "active":
                items.append({"kind": "memory", "id": legacy_id, "version": version,
                              "deleted": True, "value": {}, "origin_device": origin})
                continue
            if not metadata:
                raise HTTPException(409, {"code": "memory_metadata_unavailable"})
            previous = metadata
            payload = json.loads(row["payload"])
            if payload["basis"] != "legacy_unknown" or not previous.get("recorded_utc"):
                raise HTTPException(409, {"code": "memory_basis_unrepresentable"})
            source_ids = previous.get("source_ids") or [legacy_id]
            expected_refs = (previous["source_record_refs"] if previous.get("source_record_refs") else
                             [{"record_id": previous["source_record_id"], "revision": 1}]
                             if previous.get("source_record_id") else
                             [{"record_id": _mapped_id(store.sync.server_id, character,
                                                       "memory_source", source_id), "revision": 1}
                              for source_id in source_ids])
            actual_refs = [{"record_id": ref["record_id"], "revision": ref["revision"]}
                           for ref in payload["source_refs"]]
            if actual_refs != expected_refs:
                raise HTTPException(409, {"code": "memory_sources_unrepresentable"})
            value = {"content": payload["text"], "pinned": payload["pinned"],
                     "recorded_utc": previous["recorded_utc"]}
            if previous.get("source_ids"):
                value.update(source_ids=previous["source_ids"],
                             source_versions=previous["source_versions"], basis=previous["basis"])
            items.append({"kind": "memory", "id": legacy_id, "version": version,
                          "deleted": False, "value": value, "origin_device": origin})

        items.sort(key=lambda item: (item["kind"], item["id"]))
        result = {"server_id": store.sync.server_id, "character_id": character,
                  "revision": imported["legacy_revision"] + (state["head_seq"] if state else 0),
                  "items": items}
        if len(items) > 18000 or len(encoded(result).encode("utf-8")) > 8 * 1024 * 1024:
            raise HTTPException(413, {"code": "legacy_projection_too_large"})
        return result
