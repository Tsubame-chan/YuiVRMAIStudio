"""Backend console's server-owned writes to a shadow-imported Companion ledger."""
from __future__ import annotations

import re
from typing import Callable

from fastapi import HTTPException

from app.core.companion_legacy_adapter import _check_legacy_result_size
from app.core.companion_legacy_projection import project_legacy_snapshot
from app.core.companion_legacy_write import prepare_legacy_changes, apply_prepared_legacy_changes
from app.core.companion_v2 import CompanionStore
from app.core.device_sync import Item, SyncStore


class CompanionBackendWriter:
    def __init__(self, database_url: str, *, require_active: bool = False):
        self.store = CompanionStore(database_url)
        self.principal = self.store.sync.ensure_backend_principal()
        self.require_active = require_active

    def snapshot(self, character: str) -> dict:
        projected = project_legacy_snapshot(self.store, self.principal, character)
        with self.store.connect() as db:
            if self.require_active and SyncStore.character_mode(db, character) != "active_v2":
                raise HTTPException(409, {"code": "companion_v2_route_changed"})
            row = db.execute("SELECT name FROM sync_characters WHERE id=?", (character,)).fetchone()
        name_profile = next((x["value"]["text"] for x in projected["items"]
                             if x["kind"] == "profile" and x["id"] == "name" and not x["deleted"]), None)
        return {"character_id": character, "name": name_profile or row["name"],
                "revision": projected["revision"], "items": projected["items"]}

    def connect(self):
        return self.store.connect()

    def append(self, character: str, operation_id: str,
               values: list[tuple[str, str, dict]],
               finalize: Callable | None = None,
               expected_revision: int | None = None) -> dict:
        if not re.fullmatch(r"[a-f0-9]{32}", operation_id):
            raise HTTPException(422, {"code": "invalid_backend_operation_id"})
        if not values:
            raise HTTPException(422, {"code": "empty_backend_write"})
        snapshot = project_legacy_snapshot(self.store, self.principal, character)
        if expected_revision is not None and snapshot["revision"] != expected_revision:
            raise HTTPException(409, {"code": "backend_context_changed"})
        existing = {(item["kind"], item["id"]) for item in snapshot["items"]}
        if any((kind, item_id) in existing for kind, item_id, _ in values):
            raise HTTPException(409, {"code": "backend_item_already_exists"})
        items = [Item(kind=kind, id=item_id, value=value) for kind, item_id, value in values]
        _check_legacy_result_size(snapshot, [item.model_dump() for item in items])
        prepared = prepare_legacy_changes(self.store, self.principal, character,
                                          operation_id, items, link_backend_turns=True)
        with self.store.connect() as db:
            imported = db.execute("SELECT legacy_revision FROM companion_v2_legacy_imports "
                                  "WHERE character_id=?", (character,)).fetchone()
        if not imported:
            raise HTTPException(409, {"code": "character_not_imported"})
        expected_head_seq = snapshot["revision"] - imported["legacy_revision"]
        def guarded_finalize(db):
            if self.require_active and SyncStore.character_mode(db, character) != "active_v2":
                raise HTTPException(409, {"code": "companion_v2_route_changed"})
            if finalize is not None:
                finalize(db)
        apply_prepared_legacy_changes(self.store, self.principal, prepared,
                                      finalize=guarded_finalize, expected_head_seq=expected_head_seq)
        return project_legacy_snapshot(self.store, self.principal, character)
