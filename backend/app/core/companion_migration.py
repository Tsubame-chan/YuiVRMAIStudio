"""Read-only legacy inventory before any protocol-2 migration is enabled."""
from __future__ import annotations

import hashlib
import json
import re
import sqlite3
from datetime import datetime

from fastapi import HTTPException

from app.db.sqlite import sqlite_path_from_url
from app.core.device_sync import Item


def _role(item: sqlite3.Row, value: dict) -> tuple[str | None, str]:
    suffix_role = ("user" if item["id"].endswith(":user") else
                   "assistant" if item["id"].endswith(":assistant") else None)
    speaker_role = {"You": "user", "Assistant": "assistant"}.get(value.get("speaker"))
    if suffix_role and speaker_role and suffix_role != speaker_role:
        return None, "conflicting_role"
    if suffix_role == "user":
        return "user", "id_suffix"
    if suffix_role == "assistant":
        return "assistant", "id_suffix"
    if item["origin_device"] == "backend-import" and item["id"].startswith("legacy:"):
        if value.get("speaker") == "You":
            return "user", "imported_speaker"
        if value.get("speaker") == "Assistant":
            return "assistant", "imported_speaker"
    if value.get("speaker") == "You":
        return "user", "app_speaker"
    if value.get("speaker") == "Assistant":
        return "assistant", "app_speaker"
    return None, "ambiguous"


def preview_character(database_url: str, character_id: str) -> dict:
    """Return counts, hashes and issues only; never write or return conversation text."""
    path = sqlite_path_from_url(database_url)
    with sqlite3.connect(path.as_uri() + "?mode=ro", uri=True) as db:
        db.row_factory = sqlite3.Row
        db.execute("PRAGMA query_only=ON")
        db.execute("BEGIN")
        character = db.execute("SELECT revision FROM sync_characters WHERE id=?", (character_id,)).fetchone()
        if not character:
            raise HTTPException(404, "共有キャラクターが見つかりません。")
        hub = db.execute("SELECT value FROM sync_metadata WHERE key='server_id'").fetchone()[0]
        rows = list(db.execute("SELECT * FROM sync_items WHERE character_id=? ORDER BY kind,id", (character_id,)))
        items = {(row["kind"], row["id"]): row for row in rows}
        counts = {"profile": 0, "memory": 0, "history": 0, "tombstones": 0,
                  "linked_memories": 0, "history_backed_memories": 0,
                  "roles_user": 0, "roles_assistant": 0}
        issues = []
        corpus = hashlib.sha256()
        for row in rows:
            key = f'{row["kind"]}:{row["id"]}'
            corpus.update(json.dumps([row["kind"], row["id"], row["version"], row["deleted"], row["value_json"]],
                                     ensure_ascii=False, separators=(",", ":")).encode("utf-8"))
            if row["deleted"]:
                counts["tombstones"] += 1
                continue
            if row["kind"] not in {"profile", "memory", "history"}:
                issues.append({"item": key, "code": "unknown_kind"})
                continue
            counts[row["kind"]] += 1
            try:
                value = json.loads(row["value_json"])
            except (TypeError, ValueError):
                issues.append({"item": key, "code": "invalid_json"})
                continue
            if not isinstance(value, dict):
                issues.append({"item": key, "code": "invalid_value"})
                continue
            try:
                Item.model_validate({"kind": row["kind"], "id": row["id"], "value": value})
            except ValueError:
                issues.append({"item": key, "code": "invalid_value"})
                continue
            if row["kind"] == "history":
                role, evidence = _role(row, value)
                if role is None:
                    issues.append({"item": key, "code": evidence if evidence == "conflicting_role" else "ambiguous_role"})
                else:
                    counts[f"roles_{role}"] += 1
                if value.get("mode") not in {"talk", "work"}:
                    issues.append({"item": key, "code": "unknown_mode"})
                if not value.get("conversation_id"):
                    issues.append({"item": key, "code": "missing_conversation"})
            if row["kind"] in {"history", "memory"}:
                content = value.get("text") if row["kind"] == "history" else value.get("content")
                if not content or (row["kind"] == "history" and len(content) > 32000):
                    issues.append({"item": key, "code": "record_content_unmappable"})
                try:
                    recorded = datetime.fromisoformat(value["recorded_utc"].replace("Z", "+00:00"))
                    if recorded.tzinfo is None:
                        raise ValueError("naive datetime")
                except (KeyError, TypeError, ValueError):
                    issues.append({"item": key, "code": "invalid_recorded_at"})
            if row["kind"] == "memory" and row["id"].startswith("auto-history:"):
                history_id = row["id"].removeprefix("auto-history:")
                source = items.get(("history", history_id))
                try:
                    source_value = json.loads(source["value_json"]) if source and not source["deleted"] else None
                except (TypeError, ValueError):
                    source_value = None
                if (not re.fullmatch(r"[a-f0-9]{32}", history_id) or not isinstance(source_value, dict) or
                        _role(source, source_value)[0] != "user" or source_value.get("text") != value.get("content") or
                        value.get("source_ids")):
                    issues.append({"item": key, "code": "automatic_memory_source_unavailable"})
                else:
                    counts["history_backed_memories"] += 1
            if row["kind"] == "memory" and value.get("source_ids"):
                counts["linked_memories"] += 1
                source_ids = value["source_ids"]
                versions = value.get("source_versions")
                if not isinstance(source_ids, list) or not isinstance(versions, list) or len(source_ids) != len(versions):
                    issues.append({"item": key, "code": "invalid_sources"})
                    continue
                for source_id, version in zip(source_ids, versions):
                    source = items.get(("memory", source_id))
                    if not source or source["deleted"] or source["version"] != version:
                        issues.append({"item": key, "code": "source_unavailable"})
                        break
        digest = corpus.hexdigest()
        return {"hub_id": hub, "character_id": character_id, "legacy_revision": character["revision"],
                "legacy_digest": digest, "counts": counts, "issues": issues,
                "ready_for_mapping": not issues, "writes_performed": False}
