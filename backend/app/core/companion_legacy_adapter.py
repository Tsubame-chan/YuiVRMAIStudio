"""Protocol-1 plan/commit compatibility against a shadow-imported v2 ledger.

The product routes use it only for explicitly trial-switched characters.
"""
from __future__ import annotations

import json
import time
from uuid import uuid4

from fastapi import HTTPException

from app.core.companion_legacy_projection import project_legacy_snapshot
from app.core.companion_legacy_write import prepare_legacy_changes, apply_prepared_legacy_changes
from app.core.companion_v2 import CompanionStore
from app.core.device_sync import Item, PlanRequest, SyncStore, encoded


def _check_legacy_result_size(snapshot: dict, selected: list[dict]) -> None:
    projected = {(item["kind"], item["id"]): item for item in snapshot["items"]}
    for item in selected:
        key = (item["kind"], item["id"])
        before = projected.get(key)
        projected[key] = {"kind": item["kind"], "id": item["id"],
                          "version": (before["version"] if before else 0) + 1,
                          "deleted": item["deleted"], "value": item["value"],
                          "origin_device": "0" * 32}
    if len(projected) > 18000:
        raise HTTPException(413, {"code": "legacy_projection_too_large"})
    candidate = {"server_id": snapshot["server_id"],
                 "character_id": snapshot["character_id"],
                 "revision": snapshot["revision"] + 60000,
                 "items": sorted(projected.values(), key=lambda item: (item["kind"], item["id"]))}
    # Leave a few bytes per item for larger revision numbers and origin IDs.
    if len(encoded(candidate).encode("utf-8")) + len(projected) * 16 > 8 * 1024 * 1024:
        raise HTTPException(413, {"code": "legacy_projection_too_large"})


class LegacyV2Adapter:
    def __init__(self, database_url: str, *, require_active: bool = False):
        self.store = CompanionStore(database_url)
        self.sync = self.store.sync
        self.require_active = require_active

    def _check_route(self, db, character: str) -> None:
        if self.require_active and SyncStore.character_mode(db, character) != "active_v2":
            raise HTTPException(409, {"code": "companion_v2_route_changed"})

    def plan(self, principal: str, request: PlanRequest) -> dict:
        snapshot = project_legacy_snapshot(self.store, principal, request.character_id)
        remote = {(item["kind"], item["id"]): item for item in snapshot["items"]}
        changes, conflicts, pulled, seen = [], [], [], set()
        for item in request.items:
            key = (item.kind, item.id)
            seen.add(key)
            previous = remote.get(key)
            if item.kind == "history" and previous is None and item.base_version == 0 and not item.deleted:
                # The app archive uses a random row ID while Backend shared
                # chat uses the stable request/role ID. They are the same turn
                # only when the conversation, role and complete text agree.
                role = "user" if item.value.get("speaker") == "You" else "assistant"
                backend_id = "backend:" + item.value.get("turn_id", "") + ":" + role
                authored = remote.get(("history", backend_id))
                if authored:
                    if authored["deleted"]:
                        continue  # A forgotten turn must never be reintroduced.
                    value = authored["value"]
                    if (value["conversation_id"] != item.value.get("conversation_id")
                            or value["mode"] != item.value.get("mode")
                            or value["text"] != item.value.get("text")):
                        raise HTTPException(409, {"code": "backend_turn_identity_conflict"})
                    continue  # Pull the server ID and replace the local alias.
            version = previous["version"] if previous else 0
            if item.base_version > version:
                raise HTTPException(409, {"code": "legacy_version_conflict"})
            same = (previous and item.deleted == previous["deleted"]
                    and encoded(item.value) == encoded(previous["value"]))
            if same:
                continue
            if not item.changed:
                if previous:
                    pulled.append(previous)
                continue
            if previous and previous["deleted"] and not item.deleted:
                redacted = item.model_dump()
                redacted.update(deleted=True, value={})
                conflicts.append({"key": item.kind + ":" + item.id, "local": redacted,
                                  "remote": previous, "remote_deletion_wins": True})
                continue
            if item.deleted and not previous and item.base_version == 0:
                continue
            candidate = item.model_dump()
            if item.base_version != version:
                conflicts.append({"key": item.kind + ":" + item.id,
                                  "local": candidate, "remote": previous})
            else:
                changes.append(candidate)
        pulled.extend(value for key, value in remote.items() if key not in seen)
        possible = changes + [x["local"] for x in conflicts]
        if len(remote) + len({(x["kind"], x["id"]) for x in possible} - set(remote)) > 18000:
            raise HTTPException(413, {"code": "legacy_projection_too_large"})
        plan_id = uuid4().hex
        with self.store.connect() as db:
            db.execute("BEGIN IMMEDIATE")
            self.store.authorize(db, principal, request.character_id)
            self._check_route(db, request.character_id)
            imported = db.execute("SELECT legacy_revision FROM companion_v2_legacy_imports WHERE character_id=?",
                                  (request.character_id,)).fetchone()
            state = db.execute("SELECT head_seq FROM companion_v2_state WHERE character_id=?",
                               (request.character_id,)).fetchone()
            if not imported or not state or imported["legacy_revision"] + state["head_seq"] != snapshot["revision"]:
                raise HTTPException(409, {"code": "legacy_plan_revision_conflict"})
            payload = {"mode": "companion_v2", "changes": changes, "conflicts": conflicts,
                       "expected_head_seq": state["head_seq"]}
            db.execute("DELETE FROM sync_plans WHERE expires<?", (time.time(),))
            count = db.execute("SELECT COUNT(*) FROM sync_plans WHERE device_id=? AND result IS NULL",
                               (principal,)).fetchone()[0]
            if count >= 50:
                raise HTTPException(429, "差分確認が多すぎます。少し待ってからやり直してください。")
            db.execute("INSERT INTO sync_plans VALUES(?,?,?,?,?,?,NULL)",
                       (plan_id, principal, request.character_id, snapshot["revision"],
                        time.time() + 600, encoded(payload)))
        counts = {kind: sum(x["kind"] == kind and not x["deleted"] for x in changes)
                  for kind in ("profile", "memory", "history")}
        counts["deletions"] = sum(x["deleted"] for x in changes)
        downloads = {kind: sum(x["kind"] == kind and not x["deleted"] for x in pulled)
                     for kind in ("profile", "memory", "history")}
        downloads["deletions"] = sum(x["deleted"] for x in pulled)
        return {"plan_id": plan_id, "revision": snapshot["revision"], "upload": counts,
                "download": len(pulled), "download_counts": downloads, "conflicts": conflicts}

    def commit(self, principal: str, plan_id: str, choices: dict[str, str]) -> dict:
        with self.store.connect() as db:
            db.execute("BEGIN IMMEDIATE")
            SyncStore.require_device(db, principal)
            row = db.execute("SELECT * FROM sync_plans WHERE id=? AND device_id=?",
                             (plan_id, principal)).fetchone()
            if not row or row["expires"] < time.time():
                raise HTTPException(409, "確認の有効期限が切れました。もう一度差分を確認してください。")
            self._check_route(db, row["character_id"])
            payload = json.loads(row["payload"])
            if row["result"]:
                stored = json.loads(row["result"])
                if stored.get("mode") != "companion_v2":
                    raise HTTPException(409, {"code": "legacy_plan_mode_mismatch"})
                if stored["choices"] != choices:
                    raise HTTPException(409, "確定済みの選択を変更するには新しい差分確認が必要です。")
                db.commit()
                return project_legacy_snapshot(self.store, principal, row["character_id"])
            if payload.get("mode") != "companion_v2":
                raise HTTPException(409, {"code": "legacy_plan_mode_mismatch"})
            expected = {x["key"] for x in payload["conflicts"]}
            if set(choices) != expected or any(value not in {"local", "remote"} for value in choices.values()):
                raise HTTPException(422, "競合する項目ごとに使う内容を選んでください。")
            if any(x["remote"] and x["remote"].get("deleted") and choices[x["key"]] == "local"
                   for x in payload["conflicts"]):
                raise HTTPException(409, "削除済みの項目は同じIDで復元できません。最新の状態を取得してください。")
            character = row["character_id"]
            if "prepared" in payload:
                if payload["choices"] != choices:
                    raise HTTPException(409, {"code": "prepared_choice_mismatch"})
                prepared = payload["prepared"]
                if prepared:
                    committed = db.execute("SELECT 1 FROM companion_v2_batches WHERE principal=? AND batch_id=?",
                                           (principal, prepared["batch_id"])).fetchone()
                    if not committed:
                        db.commit()
                        current = project_legacy_snapshot(self.store, principal, character)
                        if current["revision"] != row["revision"]:
                            raise HTTPException(409, "別の端末で更新されました。最新の差分を確認してください。")
            else:
                # Projection uses another connection, so release the write lock
                # before reading the v2 state and preparing operations.
                db.commit()
                snapshot = project_legacy_snapshot(self.store, principal, character)
                if snapshot["revision"] != row["revision"]:
                    raise HTTPException(409, "別の端末で更新されました。最新の差分を確認してください。")
                selected = payload["changes"] + [x["local"] for x in payload["conflicts"]
                                                  if choices[x["key"]] == "local"]
                _check_legacy_result_size(snapshot, selected)
                prepared = (prepare_legacy_changes(self.store, principal, character, plan_id,
                                                   [Item.model_validate(item) for item in selected])
                            if selected else None)
                with self.store.connect() as locked:
                    locked.execute("BEGIN IMMEDIATE")
                    self._check_route(locked, character)
                    current = locked.execute("SELECT payload,result FROM sync_plans WHERE id=? AND device_id=?",
                                             (plan_id, principal)).fetchone()
                    if not current or current["result"]:
                        raise HTTPException(409, {"code": "plan_changed"})
                    current_payload = json.loads(current["payload"])
                    if current_payload != payload:
                        raise HTTPException(409, {"code": "plan_changed"})
                    locked.execute("UPDATE sync_plans SET payload=? WHERE id=?",
                                   (encoded({**payload, "choices": choices, "prepared": prepared}), plan_id))
        def finalize(db):
            self._check_route(db, character)
            changed = db.execute("UPDATE sync_plans SET payload='{}',result=? "
                                 "WHERE id=? AND device_id=? AND result IS NULL",
                                 (encoded({"choices": choices, "mode": "companion_v2"}),
                                  plan_id, principal)).rowcount
            if changed != 1:
                raise HTTPException(409, {"code": "plan_changed"})
        if prepared:
            apply_prepared_legacy_changes(self.store, principal, prepared, finalize=finalize,
                                          expected_head_seq=payload["expected_head_seq"])
        else:
            with self.store.connect() as db:
                db.execute("BEGIN IMMEDIATE")
                finalize(db)
        return project_legacy_snapshot(self.store, principal, character)
