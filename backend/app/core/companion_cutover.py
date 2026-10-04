"""Read-only inventory for the future per-character v1→v2 cutover gate.

The inventory endpoint never activates a character. A separate trial gate is
disabled by default; device acceptance and public release remain outstanding.
"""
from __future__ import annotations

from fastapi import HTTPException

from app.core.companion_legacy_projection import project_legacy_snapshot
from app.core.companion_migration import preview_character
from app.core.companion_v2 import CompanionStore
from app.core.device_sync import SyncStore


def preview_cutover(database_url: str, principal: str, character: str, *, lock_writes: bool = True) -> dict:
    store = CompanionStore(database_url, initialize=False)
    blockers: list[str] = []
    # Until this character is explicitly switched, its production routes read
    # and write sync_items even if its shadow inventory matches exactly.
    routing_blockers = ["device_sync_routes_use_v1", "backend_console_routes_use_v1"]
    with store.connect() as db:
        # Keep v1, v2 and the projection on one frozen database state. This is
        # an inventory only; no activation decision is made here.
        db.execute("BEGIN IMMEDIATE" if lock_writes else "BEGIN")
        store.authorize(db, principal, character)
        if SyncStore.character_mode(db, character) == "active_v2":
            routing_blockers = []
        if not db.execute("SELECT 1 FROM sqlite_master WHERE type='table' "
                          "AND name='companion_v2_legacy_imports'").fetchone():
            return {"character_id": character, "status": "not_imported",
                    "blockers": ["character_not_imported"],
                    "routing_blockers": routing_blockers, "activation_ready": False}
        imported = db.execute("SELECT legacy_digest,legacy_revision FROM companion_v2_legacy_imports "
                              "WHERE character_id=?", (character,)).fetchone()
        if not imported:
            return {"character_id": character, "status": "not_imported",
                    "blockers": ["character_not_imported"],
                    "routing_blockers": routing_blockers, "activation_ready": False}
        original_digest = imported["legacy_digest"]
        original_revision = imported["legacy_revision"]
        pending = db.execute("SELECT COUNT(*) FROM sync_plans WHERE character_id=? AND result IS NULL",
                             (character,)).fetchone()[0]
        v2_batches = db.execute("SELECT COUNT(*) FROM companion_v2_batches WHERE character_id=?",
                                (character,)).fetchone()[0]
        activity_ops = db.execute("SELECT COUNT(*) FROM companion_v2_activity_ops WHERE character_id=?",
                                  (character,)).fetchone()[0]
        legacy_items = SyncStore.items(db, character)
        missing_metadata = db.execute(
            "SELECT COUNT(*) FROM sync_items AS item LEFT JOIN companion_v2_legacy_metadata AS meta "
            "ON meta.character_id=item.character_id AND meta.kind=item.kind AND meta.legacy_id=item.id "
            "WHERE item.character_id=? AND meta.legacy_id IS NULL", (character,)).fetchone()[0]
        legacy = preview_character(database_url, character)
        if legacy["legacy_revision"] != original_revision or legacy["legacy_digest"] != original_digest:
            blockers.append("legacy_changed_after_shadow")
        if legacy["issues"]:
            blockers.append("legacy_preview_has_issues")
        if missing_metadata:
            blockers.append("legacy_metadata_incomplete")
        if pending:
            blockers.append("pending_legacy_plans")
        if v2_batches or activity_ops:
            blockers.append("v2_changed_after_shadow")
        projected_count = None
        try:
            projected = project_legacy_snapshot(store, principal, character)
            projected_count = len(projected["items"])
            normalized = lambda items: {(x["kind"], x["id"]):
                                        (x["version"], x["deleted"], x["value"], x["origin_device"])
                                        for x in items}
            if normalized(legacy_items) != normalized(projected["items"]):
                blockers.append("legacy_projection_differs")
        except HTTPException as error:
            detail = error.detail
            blockers.append(detail.get("code", "legacy_projection_failed")
                            if isinstance(detail, dict) else "legacy_projection_failed")
    return {"character_id": character,
            "status": "inventory_matches" if not blockers else "blocked",
            "activation_ready": False,
            "blockers": sorted(set(blockers)),
            "routing_blockers": routing_blockers,
            "legacy_items": len(legacy_items), "projected_items": projected_count,
            "missing_legacy_metadata": missing_metadata,
            "legacy_revision": legacy["legacy_revision"],
            "imported_revision": original_revision,
            "pending_plans": pending, "v2_batches": v2_batches,
            "v2_activity_ops": activity_ops}


def activate_trial_cutover(database_url: str, principal: str, character: str) -> dict:
    """Atomically select v2 on a trial database after a fresh inventory."""
    sync = SyncStore(database_url)
    with sync.connect() as db:
        db.execute("BEGIN IMMEDIATE")
        SyncStore.require_device(db, principal)
        if sync.character_mode(db, character) != "v1":
            raise HTTPException(409, {"code": "character_already_active_v2"})
        # The outer write lock freezes every v1/v2 writer while the read-only
        # inventory opens its own snapshot connections.
        report = preview_cutover(database_url, principal, character, lock_writes=False)
        if report["status"] != "inventory_matches" or report["blockers"]:
            raise HTTPException(409, {"code": "cutover_inventory_blocked",
                                      "blockers": report["blockers"]})
        db.execute("INSERT INTO sync_character_routing VALUES(?, 'active_v2')", (character,))
    return {"character_id": character, "mode": "active_v2", "trial_only": True}


def deactivate_unwritten_trial_cutover(database_url: str, principal: str, character: str) -> dict:
    """Revert routing only while no post-shadow v2 work could be lost."""
    sync = SyncStore(database_url)
    with sync.connect() as db:
        db.execute("BEGIN IMMEDIATE")
        SyncStore.require_device(db, principal)
        if sync.character_mode(db, character) != "active_v2":
            raise HTTPException(409, {"code": "character_not_active_v2"})
        tables = (("companion_v2_batches", "character_id"),
                  ("companion_v2_activity_ops", "character_id"),
                  ("companion_v2_activities", "character_id"))
        changed = any(db.execute(f"SELECT 1 FROM {table} WHERE {column}=? LIMIT 1",
                                 (character,)).fetchone() for table, column in tables)
        pending = db.execute("SELECT 1 FROM sync_plans WHERE character_id=? AND result IS NULL LIMIT 1",
                             (character,)).fetchone()
        if changed or pending:
            raise HTTPException(409, {"code": "trial_cutover_has_writes"})
        db.execute("DELETE FROM sync_character_routing WHERE character_id=?", (character,))
    return {"character_id": character, "mode": "v1", "trial_only": True}
