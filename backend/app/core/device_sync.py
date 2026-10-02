"""Owner-paired character sync. Plans never modify the agreed shared state."""
from __future__ import annotations

import hashlib
import json
import secrets
import sqlite3
import time
import re
from uuid import uuid4

from fastapi import HTTPException
from pydantic import BaseModel, ConfigDict, Field, model_validator

from app.db.sqlite import sqlite_path_from_url


class Item(BaseModel):
    model_config = ConfigDict(extra="forbid")
    kind: str = Field(pattern=r"^(profile|memory|history)$")
    id: str = Field(min_length=1, max_length=128, pattern=r"^[a-zA-Z0-9:_-]+$")
    base_version: int = Field(default=0, ge=0)
    changed: bool = True
    deleted: bool = False
    value: dict = Field(default_factory=dict)

    @model_validator(mode="after")
    def validate_value(self):
        if self.deleted:
            if self.kind == "profile": raise ValueError("Profile fields cannot be deleted.")
            if self.value: raise ValueError("Deleted items must not include content.")
            return self
        fields = {"profile": {"text": (str, 16000)},
                  "memory": {"content": (str, 4000), "pinned": (bool, 0), "recorded_utc": (str, 64)},
                  "history": {"text": (str, 100000), "speaker": (str, 256), "mode": (str, 32),
                              "recorded_utc": (str, 64), "conversation_id": (str, 128), "turn_id": (str, 128)}}[self.kind]
        optional = {"source_ids", "source_versions", "basis"} if self.kind == "memory" else set()
        if set(self.value) - optional != set(fields): raise ValueError("Invalid sync fields.")
        for key, (type_, limit) in fields.items():
            v = self.value[key]
            if type(v) is not type_ or (limit and len(v) > limit): raise ValueError("Invalid sync value.")
        if set(self.value) & optional:
            if not optional.issubset(self.value) or self.value["basis"] != "user_confirmed_connection":
                raise ValueError("Invalid memory provenance.")
            ids, versions = self.value["source_ids"], self.value["source_versions"]
            if not isinstance(ids, list) or not 2 <= len(ids) <= 8:
                raise ValueError("Invalid memory sources.")
            if any(not isinstance(id, str) or not re.fullmatch(r"[a-zA-Z0-9:_-]{1,128}", id) for id in ids) or len(set(ids)) != len(ids):
                raise ValueError("Invalid memory source ID.")
            if not isinstance(versions, list) or len(versions) != len(ids) or any(type(v) is not int or v < 1 for v in versions):
                raise ValueError("Invalid memory source versions.")
        if self.kind == "profile" and self.id not in {"name", "instruction"}: raise ValueError("Invalid profile field.")
        if self.kind == "profile" and self.id == "name" and not 1 <= len(self.value["text"]) <= 256:
            raise ValueError("Character name must be 1–256 characters.")
        return self


class PlanRequest(BaseModel):
    model_config = ConfigDict(extra="forbid")
    character_id: str = Field(pattern=r"^[a-f0-9]{32}$")
    items: list[Item] = Field(max_length=20000)

    @model_validator(mode="after")
    def unique_items(self):
        keys = [(x.kind, x.id) for x in self.items]
        if len(keys) != len(set(keys)): raise ValueError("Duplicate sync item.")
        if len(self.model_dump_json().encode()) > 8 * 1024 * 1024: raise ValueError("Sync snapshot exceeds 8 MiB.")
        return self


def encoded(value): return json.dumps(value, ensure_ascii=False, sort_keys=True, separators=(",", ":"))
def digest(value): return hashlib.sha256(value.encode()).hexdigest()


class SyncStore:
    def __init__(self, database_url):
        self.path = sqlite_path_from_url(database_url)
        with self.connect() as db:
            db.executescript("""
                CREATE TABLE IF NOT EXISTS sync_devices (id TEXT PRIMARY KEY, name TEXT NOT NULL,
                    token_hash TEXT UNIQUE NOT NULL, created REAL NOT NULL, last_seen REAL NOT NULL, revoked INTEGER NOT NULL DEFAULT 0);
                CREATE TABLE IF NOT EXISTS sync_pairing (id INTEGER PRIMARY KEY CHECK(id=1), code_hash TEXT, expires REAL, attempts INTEGER NOT NULL);
                CREATE TABLE IF NOT EXISTS sync_characters (id TEXT PRIMARY KEY, name TEXT NOT NULL, revision INTEGER NOT NULL DEFAULT 0);
                CREATE TABLE IF NOT EXISTS sync_items (character_id TEXT NOT NULL, kind TEXT NOT NULL, id TEXT NOT NULL,
                    version INTEGER NOT NULL, deleted INTEGER NOT NULL, value_json TEXT NOT NULL, origin_device TEXT NOT NULL,
                    PRIMARY KEY(character_id,kind,id));
                CREATE TABLE IF NOT EXISTS sync_plans (id TEXT PRIMARY KEY, device_id TEXT NOT NULL, character_id TEXT NOT NULL,
                    revision INTEGER NOT NULL, expires REAL NOT NULL, payload TEXT NOT NULL, result TEXT);
                CREATE TABLE IF NOT EXISTS sync_metadata (key TEXT PRIMARY KEY, value TEXT NOT NULL);
                CREATE TABLE IF NOT EXISTS sync_backend_responses (character_id TEXT, request_id TEXT, signature TEXT NOT NULL,
                    response TEXT NOT NULL, PRIMARY KEY(character_id,request_id));
            """)
            db.execute("INSERT OR IGNORE INTO sync_metadata VALUES('server_id',?)", (uuid4().hex,))
            self.server_id = db.execute("SELECT value FROM sync_metadata WHERE key='server_id'").fetchone()[0]

    def connect(self):
        db = sqlite3.connect(self.path, timeout=15)
        db.row_factory = sqlite3.Row
        return db

    def pairing_code(self):
        code = "".join(secrets.choice("ABCDEFGHJKLMNPQRSTUVWXYZ23456789") for _ in range(10))
        with self.connect() as db:
            db.execute("INSERT OR REPLACE INTO sync_pairing VALUES(1,?,?,0)", (digest(code), time.time() + 600))
        return {"code": code, "expires_in": 600}

    def pair(self, name, code):
        token = secrets.token_urlsafe(48)
        with self.connect() as db:
            db.execute("BEGIN IMMEDIATE")
            row = db.execute("SELECT * FROM sync_pairing WHERE id=1").fetchone()
            valid = row and row["expires"] > time.time() and row["attempts"] < 10
            if row: db.execute("UPDATE sync_pairing SET attempts=attempts+1 WHERE id=1")
            if not valid or not secrets.compare_digest(row["code_hash"], digest(code.strip().upper())):
                db.commit()
                raise HTTPException(401, "登録コードが無効です。PCで新しいコードを表示してください。")
            db.execute("DELETE FROM sync_pairing")
            device = uuid4().hex
            db.execute("INSERT INTO sync_devices VALUES(?,?,?,?,?,0)", (device, name, digest(token), time.time(), time.time()))
        return {"device_id": device, "token": token, "protocol": 1, "server_id": self.server_id}

    def authenticate(self, token):
        with self.connect() as db:
            row = db.execute("SELECT id FROM sync_devices WHERE token_hash=? AND revoked=0", (digest(token),)).fetchone()
            if not row: raise HTTPException(401, "端末の登録が無効です。PCで登録し直してください。")
            db.execute("UPDATE sync_devices SET last_seen=? WHERE id=?", (time.time(), row["id"]))
        return row["id"]

    def devices(self):
        with self.connect() as db:
            return [dict(x) for x in db.execute("SELECT id,name,created,last_seen,revoked FROM sync_devices ORDER BY created")]

    def revoke(self, device):
        with self.connect() as db:
            db.execute("UPDATE sync_devices SET revoked=1 WHERE id=?", (device,))
            db.execute("DELETE FROM sync_plans WHERE device_id=?", (device,))

    def characters(self):
        with self.connect() as db:
            return [dict(x) for x in db.execute("SELECT * FROM sync_characters ORDER BY name,id")]

    def create_character(self, name):
        character = uuid4().hex
        with self.connect() as db: db.execute("INSERT INTO sync_characters(id,name) VALUES(?,?)", (character, name))
        return {"id": character, "name": name, "revision": 0}

    @staticmethod
    def items(db, character):
        return [{"kind": x["kind"], "id": x["id"], "version": x["version"], "deleted": bool(x["deleted"]),
                 "value": json.loads(x["value_json"]), "origin_device": x["origin_device"]}
                for x in db.execute("SELECT * FROM sync_items WHERE character_id=? ORDER BY kind,id", (character,))]

    @staticmethod
    def require_device(db, device):
        if not db.execute("SELECT 1 FROM sync_devices WHERE id=? AND revoked=0", (device,)).fetchone():
            raise HTTPException(401, "端末の登録が解除されました。")

    def plan(self, device, request):
        with self.connect() as db:
            # One snapshot for the revision and contents even during another commit.
            db.execute("BEGIN IMMEDIATE")
            self.require_device(db, device)
            char = db.execute("SELECT revision FROM sync_characters WHERE id=?", (request.character_id,)).fetchone()
            if not char: raise HTTPException(404, "共有キャラクターが見つかりません。")
            remote = {(x["kind"], x["id"]): x for x in self.items(db, request.character_id)}
            changes, conflicts = [], []
            pulled = []
            seen = set()
            for item in request.items:
                key = (item.kind, item.id); seen.add(key)
                previous = remote.get(key)
                if item.kind == "memory" and item.id.startswith("connection:") and not item.deleted and not item.value.get("source_ids"):
                    # beta.2 clients know the original three fields. Preserve the
                    # provenance on an unchanged round trip; never turn an edited
                    # interpretation into an ungrounded original statement.
                    if previous and previous["deleted"] and item.base_version < previous["version"]:
                        # Older clients drop provenance locally. A stale cached
                        # interpretation must pull the tombstone, never resurrect.
                        continue
                    old_value = previous["value"] if previous and not previous["deleted"] else {}
                    if old_value.get("source_ids") and all(item.value.get(k) == old_value.get(k) for k in ("content", "pinned", "recorded_utc")):
                        item = item.model_copy(update={"value": old_value})
                    else:
                        raise HTTPException(422, "関連づけた記憶の編集には新しいアプリが必要です。元の記録は保持しています。")
                version = previous["version"] if previous else 0
                if item.base_version > version: raise HTTPException(409, "共有版が古い状態です。同期用DBの復元状況を確認してください。")
                same = previous and item.deleted == previous["deleted"] and encoded(item.value) == encoded(previous["value"])
                if same: continue
                if not item.changed:
                    if previous: pulled.append(previous)
                    continue
                if item.deleted and not previous and item.base_version == 0: continue
                candidate = item.model_dump()
                if item.base_version != version:
                    conflicts.append({"key": item.kind + ":" + item.id, "local": candidate, "remote": previous})
                else: changes.append(candidate)
            pulled.extend(value for key, value in remote.items() if key not in seen)
            plan_id = uuid4().hex
            payload = {"changes": changes, "conflicts": conflicts}
            db.execute("DELETE FROM sync_plans WHERE expires<?", (time.time(),))
            count = db.execute("SELECT COUNT(*) FROM sync_plans WHERE device_id=? AND result IS NULL", (device,)).fetchone()[0]
            if count >= 50: raise HTTPException(429, "差分確認が多すぎます。少し待ってからやり直してください。")
            db.execute("INSERT INTO sync_plans VALUES(?,?,?,?,?,?,NULL)",
                       (plan_id, device, request.character_id, char["revision"], time.time() + 600, encoded(payload)))
        counts = {kind: sum(x["kind"] == kind and not x["deleted"] for x in changes) for kind in ("profile", "memory", "history")}
        counts["deletions"] = sum(x["deleted"] for x in changes)
        downloads = {kind: sum(x["kind"] == kind and not x["deleted"] for x in pulled) for kind in ("profile", "memory", "history")}
        downloads["deletions"] = sum(x["deleted"] for x in pulled)
        return {"plan_id": plan_id, "revision": char["revision"], "upload": counts,
                "download": len(pulled), "download_counts": downloads, "conflicts": conflicts}

    def commit(self, device, plan_id, choices):
        with self.connect() as db:
            db.execute("BEGIN IMMEDIATE")
            self.require_device(db, device)
            row = db.execute("SELECT * FROM sync_plans WHERE id=? AND device_id=?", (plan_id, device)).fetchone()
            if not row or row["expires"] < time.time(): raise HTTPException(409, "確認の有効期限が切れました。もう一度差分を確認してください。")
            # Idempotent retry is bound to the same device and exact conflict decisions.
            if row["result"]:
                result = json.loads(row["result"])
                if result["choices"] != choices: raise HTTPException(409, "確定済みの選択を変更するには新しい差分確認が必要です。")
                return result["snapshot"]
            character = row["character_id"]
            revision = db.execute("SELECT revision FROM sync_characters WHERE id=?", (character,)).fetchone()[0]
            if revision != row["revision"]: raise HTTPException(409, "別の端末で更新されました。最新の差分を確認してください。")
            payload = json.loads(row["payload"])
            expected = {x["key"] for x in payload["conflicts"]}
            if set(choices) != expected or any(v not in {"local", "remote"} for v in choices.values()):
                raise HTTPException(422, "競合する項目ごとに使う内容を選んでください。")
            changes = payload["changes"] + [x["local"] for x in payload["conflicts"] if choices[x["key"]] == "local"]
            for item in changes:
                revision += 1
                db.execute("INSERT OR REPLACE INTO sync_items VALUES(?,?,?,?,?,?,?)",
                           (character, item["kind"], item["id"], revision, int(item["deleted"]), encoded(item["value"]), device))
                if item["kind"] == "profile" and item["id"] == "name" and not item["deleted"]:
                    db.execute("UPDATE sync_characters SET name=? WHERE id=?", (item["value"]["text"], character))
                if item["kind"] == "history" and item["deleted"]:
                    db.execute("DELETE FROM sync_backend_responses WHERE character_id=?", (character,))
            from app.core.shared_conversation import check_connection_sources, invalidate_connections
            for item in changes:
                if item["kind"] == "memory" and not item["deleted"]:
                    check_connection_sources(db, self, character, item["value"])
            revision = invalidate_connections(db, self, character, revision)
            db.execute("UPDATE sync_characters SET revision=? WHERE id=?", (revision, character))
            snapshot = {"server_id": self.server_id, "character_id": character, "revision": revision, "items": self.items(db, character)}
            if len(snapshot["items"]) > 18000 or len(encoded(snapshot).encode()) > 8 * 1024 * 1024:
                raise HTTPException(413, "共有データが同期の上限を超えています。元データは保持しています。")
            db.execute("UPDATE sync_plans SET result=? WHERE id=?", (encoded({"choices": choices, "snapshot": snapshot}), plan_id))
        return snapshot
