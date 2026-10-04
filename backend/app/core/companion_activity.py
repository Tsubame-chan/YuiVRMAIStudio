"""Durable, opt-in Activity state and private connector outbox."""
from __future__ import annotations

import json
import secrets
import time
from typing import Literal

from fastapi import HTTPException
from pydantic import BaseModel, ConfigDict, Field, model_validator

from app.core.companion_v2 import CompanionStore, ID, canonical, hash_value


class CreateActivity(BaseModel):
    model_config = ConfigDict(extra="forbid")
    op_id: str = Field(pattern=ID)
    activity_id: str = Field(pattern=ID)
    conversation_id: str = Field(pattern=ID)
    request: str = Field(min_length=1, max_length=16000)


class ActivityCommand(BaseModel):
    model_config = ConfigDict(extra="forbid")
    op_id: str = Field(pattern=ID)
    expected_revision: int = Field(ge=1)
    request_revision: int = Field(ge=1)
    command: Literal["update_request", "answer_question", "cancel"]
    text: str | None = Field(default=None, min_length=1, max_length=16000)
    question_op_id: str | None = Field(default=None, pattern=ID)

    @model_validator(mode="after")
    def check(self):
        if self.command in {"update_request", "answer_question"} and not self.text:
            raise ValueError("command requires text")
        if self.command == "answer_question" and not self.question_op_id:
            raise ValueError("answer_question requires question_op_id")
        if self.command != "answer_question" and self.question_op_id is not None:
            raise ValueError("question_op_id is only for answer_question")
        if self.command == "cancel" and self.text is not None:
            raise ValueError("cancel has no text")
        return self


class ActivityReport(BaseModel):
    model_config = ConfigDict(extra="forbid")
    op_id: str = Field(pattern=ID)
    request_revision: int = Field(ge=1)
    expected_revision: int = Field(ge=1)
    kind: Literal["acknowledged", "progress", "question", "result", "cancel_ack"]
    text: str = Field(min_length=1, max_length=16000)
    spoken_text: str | None = Field(default=None, max_length=240)


class ActivityStore:
    DEFAULT_SPOKEN_TEXT = {
        "acknowledged": "依頼が受け付けられたよ。",
        "progress": "作業が進んでいるよ。",
        "question": "確認してほしいことがあるよ。",
        "result": "作業結果を画面にまとめたよ。",
        "cancel_ack": "作業の停止が確認できたよ。",
    }

    def __init__(self, companion: CompanionStore):
        self.companion = companion
        with companion.connect() as db:
            db.executescript("""
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
                CREATE TABLE IF NOT EXISTS companion_v2_activity_reports (
                    activity_id TEXT NOT NULL, op_id TEXT NOT NULL, principal TEXT NOT NULL,
                    request_revision INTEGER NOT NULL, kind TEXT NOT NULL, text TEXT NOT NULL,
                    spoken_text TEXT, applied INTEGER NOT NULL, PRIMARY KEY(principal,op_id),
                    FOREIGN KEY(activity_id) REFERENCES companion_v2_activities(id));
                CREATE TABLE IF NOT EXISTS companion_v2_activity_answers (
                    activity_id TEXT NOT NULL, op_id TEXT PRIMARY KEY,
                    question_op_id TEXT NOT NULL, request_revision INTEGER NOT NULL,
                    text TEXT NOT NULL, created_at REAL NOT NULL,
                    FOREIGN KEY(activity_id) REFERENCES companion_v2_activities(id));
                CREATE TABLE IF NOT EXISTS companion_v2_activity_outbox (
                    id TEXT PRIMARY KEY, activity_id TEXT NOT NULL, connection_id TEXT NOT NULL,
                    request_revision INTEGER NOT NULL, kind TEXT NOT NULL, state TEXT NOT NULL,
                    created_at REAL NOT NULL, acknowledged_at REAL,
                    answer_op_id TEXT,
                    FOREIGN KEY(activity_id) REFERENCES companion_v2_activities(id));
                CREATE INDEX IF NOT EXISTS companion_v2_activity_outbox_connection
                    ON companion_v2_activity_outbox(connection_id, created_at, id);
                CREATE TABLE IF NOT EXISTS companion_v2_activity_speech (
                    report_op_id TEXT PRIMARY KEY, activity_id TEXT NOT NULL,
                    state TEXT NOT NULL, device_principal TEXT,
                    claim_digest TEXT, lease_until REAL,
                    started_at REAL, completed_at REAL,
                    FOREIGN KEY(activity_id) REFERENCES companion_v2_activities(id));
            """)
            if "spoken_text" not in {row["name"] for row in db.execute(
                    "PRAGMA table_info(companion_v2_activity_reports)")}:
                db.execute("ALTER TABLE companion_v2_activity_reports ADD COLUMN spoken_text TEXT")
            if "answer_op_id" not in {row["name"] for row in db.execute(
                    "PRAGMA table_info(companion_v2_activity_outbox)")}:
                db.execute("ALTER TABLE companion_v2_activity_outbox ADD COLUMN answer_op_id TEXT")

    @staticmethod
    def _item(row) -> dict:
        return {"activity_id": row["id"], "character_id": row["character_id"],
                "conversation_id": row["conversation_id"], "request": row["request"],
                "request_revision": row["request_revision"], "revision": row["revision"],
                "state": row["state"], "stop_state": row["stop_state"],
                "completion_basis": row["completion_basis"],
                "binding_snapshot": json.loads(row["binding_snapshot"])}

    @staticmethod
    def _receipt(row, seq: int) -> dict:
        return {"activity_id": row["id"], "revision": row["revision"],
                "request_revision": row["request_revision"], "state": row["state"],
                "stop_state": row["stop_state"], "seq": seq}

    def _replay(self, db, principal: str, character: str, op_id: str, value: object):
        digest = hash_value({"hub": self.companion.sync.server_id, "character": character, "action": value})
        if db.execute("SELECT 1 FROM companion_v2_ops WHERE principal=? AND op_id=?", (principal, op_id)).fetchone():
            raise HTTPException(409, {"code": "idempotency_mismatch"})
        old = db.execute("SELECT * FROM companion_v2_activity_ops WHERE principal=? AND op_id=?",
                         (principal, op_id)).fetchone()
        if old:
            if old["digest"] != digest or old["character_id"] != character:
                raise HTTPException(409, {"code": "idempotency_mismatch"})
            return digest, json.loads(old["result"])
        return digest, None

    @staticmethod
    def _change(db, character: str, activity_id: str, revision: int, privacy_change: bool = False):
        db.execute("INSERT OR IGNORE INTO companion_v2_state(character_id) VALUES(?)", (character,))
        db.execute("UPDATE companion_v2_state SET head_seq=head_seq+1,privacy_epoch=privacy_epoch+? WHERE character_id=?",
                   (int(privacy_change), character))
        if privacy_change:
            db.execute("DELETE FROM companion_v2_snapshot_items WHERE token IN (SELECT token FROM companion_v2_snapshots WHERE character_id=?)",
                       (character,))
        seq = db.execute("SELECT head_seq FROM companion_v2_state WHERE character_id=?", (character,)).fetchone()[0]
        db.execute("INSERT INTO companion_v2_changes(character_id,seq,entity_id,revision,deleted,entity_type) VALUES(?,?,?,?,0,'activity')",
                   (character, seq, activity_id, revision))
        return seq

    @staticmethod
    def _enqueue(db, row, kind: str, answer_op_id: str | None = None):
        connection_id = json.loads(row["binding_snapshot"]).get("connection_id")
        if not connection_id:
            return
        db.execute("UPDATE companion_v2_activity_outbox SET state='superseded' WHERE activity_id=? AND state='pending'",
                   (row["id"],))
        db.execute("""INSERT INTO companion_v2_activity_outbox
                   (id,activity_id,connection_id,request_revision,kind,state,created_at,
                    acknowledged_at,answer_op_id) VALUES(?,?,?,?,?,?,?,NULL,?)""",
                   (secrets.token_hex(16), row["id"], connection_id, row["request_revision"],
                    kind, "pending", time.time(), answer_op_id))

    def read_outbox_for_test(self, connection_id: str, limit: int = 20,
                             character_id: str | None = None) -> list[dict]:
        """Synthetic connector seam; no public MCP/HTTP access is exposed."""
        if not connection_id or limit < 1 or limit > 20:
            raise HTTPException(422, {"code": "invalid_outbox_request"})
        with self.companion.connect() as db:
            rows = db.execute("""SELECT o.*,a.character_id,a.conversation_id,a.request,a.state AS activity_state,
                              a.revision AS activity_revision,
                              a.request_revision AS current_request_revision
                              FROM companion_v2_activity_outbox o JOIN companion_v2_activities a ON a.id=o.activity_id
                              WHERE o.connection_id=? AND o.state='pending'
                                AND (? IS NULL OR a.character_id=?)
                              ORDER BY o.created_at,o.id LIMIT ?""",
                              (connection_id, character_id, character_id, limit)).fetchall()
            result = []
            for row in rows:
                current = row["request_revision"] == row["current_request_revision"]
                answer = None
                if row["kind"] == "answer" and current:
                    answer_row = db.execute("""SELECT question_op_id,text
                        FROM companion_v2_activity_answers WHERE op_id=? AND activity_id=?""",
                        (row["answer_op_id"], row["activity_id"])).fetchone()
                    if answer_row is None:
                        raise RuntimeError("Pending answer is missing its durable payload")
                    answer = dict(answer_row)
                result.append({"notification_id": row["id"], "activity_id": row["activity_id"],
                               "character_id": row["character_id"],
                               "conversation_id": row["conversation_id"],
                               "request_revision": row["request_revision"],
                               "activity_revision": row["activity_revision"], "kind": row["kind"],
                               "request": row["request"] if row["kind"] == "request" and current else None,
                               "answer": answer})
            return result

    def acknowledge_outbox_for_test(self, connection_id: str, notification_id: str) -> dict:
        """Receipt means inbox delivery only, never Activity completion."""
        with self.companion.connect() as db:
            db.execute("BEGIN IMMEDIATE")
            row = db.execute("SELECT * FROM companion_v2_activity_outbox WHERE id=? AND connection_id=?",
                             (notification_id, connection_id)).fetchone()
            if not row:
                raise HTTPException(404, {"code": "notification_unavailable"})
            if row["state"] == "pending":
                db.execute("UPDATE companion_v2_activity_outbox SET state='acknowledged',acknowledged_at=? WHERE id=?",
                           (time.time(), notification_id))
                return {"state": "acknowledged"}
            return {"state": row["state"]}

    def create(self, principal: str, character: str, body: CreateActivity) -> dict:
        with self.companion.connect() as db:
            db.execute("BEGIN IMMEDIATE")
            self.companion.authorize(db, principal, character)
            digest, replay = self._replay(db, principal, character, body.op_id,
                                          {"method": "create_activity", "body": body.model_dump(mode="json")})
            if replay is not None:
                return replay
            if any(db.execute(f"SELECT 1 FROM {table} WHERE id=?", (body.activity_id,)).fetchone()
                   for table in ("companion_v2_activities", "companion_v2_records", "companion_v2_memories",
                                 "companion_v2_bindings", "companion_v2_conversations", "companion_v2_profiles")):
                raise HTTPException(409, {"code": "activity_id_conflict"})
            conversation = db.execute("SELECT * FROM companion_v2_conversations WHERE id=? AND character_id=?",
                                      (body.conversation_id, character)).fetchone()
            if not conversation or conversation["purpose"] != "work":
                raise HTTPException(409, {"code": "work_conversation_required"})
            db.execute("INSERT INTO companion_v2_activities VALUES(?,?,?,?,?,?,?,?,?,?)",
                       (body.activity_id, character, body.conversation_id, body.request, 1, 1,
                        "queued", "not_requested", None, conversation["binding_snapshot"]))
            created_row = db.execute("SELECT * FROM companion_v2_activities WHERE id=?",
                                     (body.activity_id,)).fetchone()
            self._enqueue(db, created_row, "request")
            seq = self._change(db, character, body.activity_id, 1)
            result = self._receipt(db.execute("SELECT * FROM companion_v2_activities WHERE id=?",
                                              (body.activity_id,)).fetchone(), seq)
            db.execute("INSERT INTO companion_v2_activity_ops VALUES(?,?,?,?,?)",
                       (principal, body.op_id, digest, character, canonical(result)))
            return result

    def get(self, principal: str, character: str, activity_id: str) -> dict:
        with self.companion.connect() as db:
            self.companion.authorize(db, principal, character)
            row = db.execute("SELECT * FROM companion_v2_activities WHERE id=? AND character_id=?",
                             (activity_id, character)).fetchone()
            if not row:
                raise HTTPException(404, {"code": "activity_unavailable"})
            result = self._item(row)
            result["reports"] = [dict(report) for report in db.execute(
                "SELECT op_id,request_revision,kind,text,spoken_text,applied FROM companion_v2_activity_reports WHERE activity_id=? ORDER BY rowid",
                (activity_id,))]
            result["answers"] = [dict(answer) for answer in db.execute(
                "SELECT op_id,question_op_id,request_revision,text FROM companion_v2_activity_answers "
                "WHERE activity_id=? ORDER BY rowid", (activity_id,))]
            return result

    def claim_speech(self, principal: str, character: str, activity_id: str,
                     report_op_id: str) -> dict:
        """Claim one report for auto-speech; delivery is separate from completion."""
        now = time.time()
        with self.companion.connect() as db:
            db.execute("BEGIN IMMEDIATE")
            self.companion.authorize(db, principal, character)
            row = db.execute("""SELECT a.state,a.request_revision,r.request_revision AS report_revision,
                       r.applied,r.kind FROM companion_v2_activities a
                       JOIN companion_v2_activity_reports r ON r.activity_id=a.id
                       WHERE a.id=? AND a.character_id=? AND r.op_id=?""",
                       (activity_id, character, report_op_id)).fetchone()
            if (row is None):
                raise HTTPException(404, {"code": "report_unavailable"})
            if (not row["applied"] or row["request_revision"] != row["report_revision"] or
                    row["kind"] not in {"question", "result"} or
                    row["state"] in {"cancel_requested", "cancelled", "failed"}):
                return {"claimed": False, "reason": "not_presentable"}
            delivery = db.execute("SELECT * FROM companion_v2_activity_speech WHERE report_op_id=?",
                                  (report_op_id,)).fetchone()
            if delivery and (delivery["state"] in {"started", "completed"} or
                             delivery["state"] == "claimed" and delivery["lease_until"] > now):
                return {"claimed": False, "reason": "already_claimed"}
            claim = "yui_sc_" + secrets.token_urlsafe(32)
            from hashlib import sha256
            digest = sha256(claim.encode("ascii")).hexdigest()
            db.execute("""INSERT INTO companion_v2_activity_speech
                       (report_op_id,activity_id,state,device_principal,claim_digest,lease_until)
                       VALUES(?,?,'claimed',?,?,?)
                       ON CONFLICT(report_op_id) DO UPDATE SET state='claimed',
                         device_principal=excluded.device_principal,
                         claim_digest=excluded.claim_digest,lease_until=excluded.lease_until""",
                       (report_op_id, activity_id, principal, digest, now + 60))
            return {"claimed": True, "claim_token": claim, "lease_seconds": 60}

    def mark_speech(self, principal: str, character: str, activity_id: str,
                    report_op_id: str, claim_token: str, stage: str) -> dict:
        if stage not in {"started", "completed"} or not claim_token.startswith("yui_sc_"):
            raise HTTPException(422, {"code": "invalid_speech_receipt"})
        from hashlib import sha256
        now = time.time()
        with self.companion.connect() as db:
            db.execute("BEGIN IMMEDIATE")
            self.companion.authorize(db, principal, character)
            row = db.execute("""SELECT s.*,a.character_id,a.state AS activity_state,
                       a.request_revision,r.request_revision AS report_revision,r.applied
                       FROM companion_v2_activity_speech s
                       JOIN companion_v2_activities a ON a.id=s.activity_id
                       LEFT JOIN companion_v2_activity_reports r ON r.op_id=s.report_op_id
                       WHERE s.report_op_id=? AND s.activity_id=?""",
                       (report_op_id, activity_id)).fetchone()
            if (row is None or row["character_id"] != character or
                    row["device_principal"] != principal or
                    row["claim_digest"] != sha256(claim_token.encode("ascii")).hexdigest()):
                raise HTTPException(403, {"code": "speech_claim_denied"})
            if (row["activity_state"] in {"cancel_requested", "cancelled", "failed"} or
                    not row["applied"] or row["request_revision"] != row["report_revision"]):
                raise HTTPException(409, {"code": "speech_activity_stopped"})
            if stage == "started":
                if row["state"] == "started":
                    return {"state": "started"}
                if row["state"] != "claimed" or row["lease_until"] <= now:
                    raise HTTPException(409, {"code": "speech_claim_expired"})
                db.execute("UPDATE companion_v2_activity_speech SET state='started',started_at=? WHERE report_op_id=?",
                           (now, report_op_id))
            else:
                if row["state"] == "completed":
                    return {"state": "completed"}
                if row["state"] != "started":
                    raise HTTPException(409, {"code": "speech_not_started"})
                db.execute("UPDATE companion_v2_activity_speech SET state='completed',completed_at=? WHERE report_op_id=?",
                           (now, report_op_id))
            return {"state": stage}

    def list_for_owner(self, principal: str, character: str,
                       connection_id: str | None = None, limit: int = 50,
                       before: str | None = None) -> dict:
        """Owner catch-up after app restart; the external MCP cannot call this."""
        if not 1 <= limit <= 100 or (connection_id is not None and
                                    (len(connection_id) != 32 or any(
                                        c not in "0123456789abcdef" for c in connection_id))) or (
                before is not None and (len(before) != 32 or any(
                    c not in "0123456789abcdef" for c in before))):
            raise HTTPException(422, {"code": "invalid_activity_limit"})
        with self.companion.connect() as db:
            self.companion.authorize(db, principal, character)
            before_rowid = None
            if before is not None:
                cursor = db.execute("SELECT rowid FROM companion_v2_activities "
                                    "WHERE id=? AND character_id=?", (before, character)).fetchone()
                if cursor is None:
                    raise HTTPException(422, {"code": "invalid_activity_cursor"})
                before_rowid = cursor["rowid"]
            rows = db.execute("""SELECT * FROM companion_v2_activities
                WHERE character_id=? AND (? IS NULL OR
                    json_extract(binding_snapshot,'$.connection_id')=?)
                  AND (? IS NULL OR rowid<?)
                ORDER BY rowid DESC LIMIT ?""",
                (character, connection_id, connection_id, before_rowid,
                 before_rowid, limit + 1)).fetchall()
            items = rows[:limit]
            return {"items": [self._item(row) for row in items],
                    "next_before": items[-1]["id"] if len(rows) > limit else None}

    def command(self, principal: str, character: str, activity_id: str, body: ActivityCommand) -> dict:
        with self.companion.connect() as db:
            db.execute("BEGIN IMMEDIATE")
            self.companion.authorize(db, principal, character)
            digest, replay = self._replay(db, principal, character, body.op_id,
                                          {"method": "activity_command", "activity_id": activity_id,
                                           "body": body.model_dump(mode="json")})
            if replay is not None:
                return replay
            row = db.execute("SELECT * FROM companion_v2_activities WHERE id=? AND character_id=?",
                             (activity_id, character)).fetchone()
            if not row:
                raise HTTPException(404, {"code": "activity_unavailable"})
            if row["revision"] != body.expected_revision or row["request_revision"] != body.request_revision:
                raise HTTPException(409, {"code": "activity_revision_conflict", "current_revision": row["revision"],
                                          "request_revision": row["request_revision"]})
            if row["state"] in {"completed", "failed", "cancel_requested", "cancelled"}:
                raise HTTPException(409, {"code": "activity_terminal_or_stopping"})
            if body.command == "answer_question":
                if row["state"] != "waiting_user":
                    raise HTTPException(409, {"code": "activity_not_waiting_for_answer"})
                latest = db.execute("""SELECT op_id FROM companion_v2_activity_reports
                    WHERE activity_id=? AND request_revision=? AND kind='question' AND applied=1
                    ORDER BY rowid DESC LIMIT 1""",
                    (activity_id, row["request_revision"])).fetchone()
                if latest is None or latest["op_id"] != body.question_op_id:
                    raise HTTPException(409, {"code": "question_no_longer_current"})
                db.execute("""INSERT INTO companion_v2_activity_answers
                    VALUES(?,?,?,?,?,?)""", (activity_id, body.op_id, body.question_op_id,
                    row["request_revision"], body.text, time.time()))
                db.execute("UPDATE companion_v2_activities SET revision=revision+1,state='queued' WHERE id=?",
                           (activity_id,))
            elif body.command == "update_request":
                # Reports for the superseded request may quote its private text.
                db.execute("DELETE FROM companion_v2_activity_reports WHERE activity_id=?", (activity_id,))
                db.execute("DELETE FROM companion_v2_activity_answers WHERE activity_id=?", (activity_id,))
                db.execute("UPDATE companion_v2_activities SET request=?,request_revision=request_revision+1,revision=revision+1,state='queued' WHERE id=?",
                           (body.text, activity_id))
            else:
                db.execute("UPDATE companion_v2_activities SET revision=revision+1,state='cancel_requested',stop_state='requested' WHERE id=?",
                           (activity_id,))
            updated = db.execute("SELECT * FROM companion_v2_activities WHERE id=?", (activity_id,)).fetchone()
            self._enqueue(db, updated, "answer" if body.command == "answer_question" else
                          "request" if body.command == "update_request" else "cancel",
                          body.op_id if body.command == "answer_question" else None)
            seq = self._change(db, character, activity_id, updated["revision"],
                               privacy_change=body.command == "update_request")
            result = self._receipt(updated, seq)
            db.execute("INSERT INTO companion_v2_activity_ops VALUES(?,?,?,?,?)",
                       (principal, body.op_id, digest, character, canonical(result)))
            return result

    def report_for_test(self, character: str, activity_id: str, connection_id: str,
                        body: ActivityReport) -> dict:
        """Mock-only transition seam. There is intentionally no HTTP route."""
        with self.companion.connect() as db:
            db.execute("BEGIN IMMEDIATE")
            row = db.execute("SELECT * FROM companion_v2_activities WHERE id=? AND character_id=?",
                             (activity_id, character)).fetchone()
            if not row:
                raise HTTPException(404, {"code": "activity_unavailable"})
            binding = json.loads(row["binding_snapshot"])
            if not connection_id or binding.get("connection_id") != connection_id:
                raise HTTPException(403, {"code": "connection_unavailable"})
            principal = "connection:" + connection_id
            digest, replay = self._replay(db, principal, character, body.op_id,
                                          {"method": "activity_report", "activity_id": activity_id,
                                           "body": body.model_dump(mode="json")})
            if replay is not None:
                return replay
            current = row["request_revision"] == body.request_revision and row["revision"] == body.expected_revision
            applies = current and (
                (body.kind == "cancel_ack" and row["state"] == "cancel_requested")
                or (body.kind != "cancel_ack" and row["state"] not in
                    {"cancel_requested", "cancelled", "completed", "failed"}))
            state, basis, stop_state = row["state"], row["completion_basis"], row["stop_state"]
            if applies:
                db.execute("""UPDATE companion_v2_activity_outbox SET state='acknowledged',acknowledged_at=?
                              WHERE activity_id=? AND request_revision=? AND state='pending'
                                AND kind IN (?,?)""",
                           (time.time(), activity_id, body.request_revision,
                            "cancel" if body.kind == "cancel_ack" else "request",
                            "cancel" if body.kind == "cancel_ack" else "answer"))
                state = {"acknowledged": "accepted", "progress": "running", "question": "waiting_user",
                         "result": "completed", "cancel_ack": "cancelled"}[body.kind]
                if body.kind == "result":
                    basis = "external_report"
                if body.kind == "cancel_ack":
                    stop_state = "confirmed"
                db.execute("UPDATE companion_v2_activities SET revision=revision+1,state=?,completion_basis=?,stop_state=? WHERE id=?",
                           (state, basis, stop_state, activity_id))
                self._change(db, character, activity_id, row["revision"] + 1)
            db.execute("""INSERT INTO companion_v2_activity_reports
                       (activity_id,op_id,principal,request_revision,kind,text,spoken_text,applied)
                       VALUES(?,?,?,?,?,?,?,?)""",
                       (activity_id, body.op_id, principal, body.request_revision, body.kind,
                        body.text, " ".join(body.spoken_text.split()) if body.spoken_text and
                        body.spoken_text.strip() else self.DEFAULT_SPOKEN_TEXT[body.kind], int(applies)))
            result = {"applied": bool(applies), "state": state, "completion_basis": basis,
                      "revision": row["revision"] + int(applies)}
            db.execute("INSERT INTO companion_v2_activity_ops VALUES(?,?,?,?,?)",
                       (principal, body.op_id, digest, character, canonical(result)))
            return result
