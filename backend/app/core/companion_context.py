"""Owner-only, source-checked trial ContextPacket for protocol 2."""
from __future__ import annotations

import json
import re
import unicodedata
from typing import Literal

from fastapi import HTTPException
from pydantic import BaseModel, ConfigDict, Field

from app.core.companion_v2 import CompanionStore, ID


class ContextRequest(BaseModel):
    model_config = ConfigDict(extra="forbid")
    conversation_id: str = Field(pattern=ID)
    purpose: Literal["talk", "work"]
    realm: Literal["real", "roleplay"] = "real"
    query: str = Field(min_length=1, max_length=2000)
    budget_chars: int = Field(default=4000, ge=256, le=12000)


def _score(query: str, text: str) -> float:
    query = unicodedata.normalize("NFKC", query).casefold().strip()
    text = unicodedata.normalize("NFKC", text).casefold()
    if not query:
        return 0.0
    # Match Latin words as whole tokens. Requiring every word in a natural
    # question would discard the relevant source when it lacks words such as
    # "what" or "about". Numeric tokens remain mandatory to avoid citing
    # "1999" for a question about "9999".
    query_words = re.findall(r"[a-z0-9]+", query)
    text_words = set(re.findall(r"[a-z0-9]+", text))
    if any(any(char.isdigit() for char in word) and word not in text_words
           for word in query_words):
        return 0.0
    if query in text:
        return 2.0
    stopwords = {"a", "an", "and", "about", "at", "be", "can", "did", "do", "for",
                 "how", "i", "in", "is", "it", "me", "my", "of", "on", "say",
                 "said", "tell", "the", "to", "was", "what", "when", "where",
                 "which", "who", "why", "with"}
    terms = {word for word in query_words if word not in stopwords and len(word) >= 2}
    matched = terms & text_words
    if matched:
        return 1.0 + len(matched) / max(1, len(terms))
    if query_words and not re.search(r"[^\x00-\x7f]", query):
        return 0.0
    if len(query) < 2:
        return 0.0
    grams = {query[index:index + 2] for index in range(len(query) - 1)}
    overlap = len({gram for gram in grams if gram in text}) / len(grams)
    # Japanese questions often wrap a short topic (e.g. 紅茶) in a much longer
    # sentence. Keep that distinctive topic even when whole-query overlap is
    # low; common self-reference must not make every personal record a match.
    topic = {gram for gram in grams if gram in text
             and re.search(r"[一-龯ァ-ヿ]", gram)
             and gram not in {"私は", "僕は", "俺は", "私の", "僕の", "です", "ます", "あなた"}}
    if topic:
        return max(overlap, 0.4)
    return overlap if overlap >= 0.35 else 0.0


def owner_context(store: CompanionStore, principal: str, character: str, request: ContextRequest,
                  conversation_filter: str | None = None,
                  *, allow_new_conversation: bool = False) -> dict:
    with store.connect() as db:
        db.execute("BEGIN")
        store.authorize(db, principal, character)
        conversation = db.execute("SELECT purpose FROM companion_v2_conversations WHERE id=? AND character_id=?",
                                  (request.conversation_id, character)).fetchone()
        if not conversation and not allow_new_conversation:
            raise HTTPException(404, {"code": "conversation_unavailable"})
        if conversation and conversation["purpose"] != request.purpose:
            raise HTTPException(409, {"code": "conversation_purpose_mismatch"})
        state = db.execute("SELECT head_seq,privacy_epoch FROM companion_v2_state WHERE character_id=?",
                           (character,)).fetchone()
        head, epoch = (state["head_seq"], state["privacy_epoch"]) if state else (0, 0)
        candidates = []
        for row in db.execute("SELECT id,revision,payload FROM companion_v2_memories WHERE character_id=? AND state='active'",
                              (character,)):
            payload = json.loads(row["payload"])
            score = _score(request.query, payload["text"] + " " + payload["subject"])
            if score <= 0:
                continue
            refs = payload["source_refs"]
            valid = True
            for ref in refs:
                if db.execute("SELECT 1 FROM companion_v2_suppressions WHERE record_id=?",
                              (ref["record_id"],)).fetchone():
                    valid = False
                    break
                source = db.execute("SELECT revision,deleted,payload FROM companion_v2_records WHERE id=? AND character_id=?",
                                    (ref["record_id"], character)).fetchone()
                source_payload = json.loads(source["payload"]) if source else None
                if (not source or source["deleted"] or source["revision"] != ref["revision"]
                        or source_payload["realm"] != request.realm
                        or (conversation_filter is not None and
                            source_payload.get("conversation_id") != conversation_filter)):
                    valid = False
                    break
            if not valid:
                continue
            candidates.append((score + 1, row["id"], {"kind": "memory", "id": row["id"],
                              "revision": row["revision"], "text": payload["text"],
                              "basis": payload["basis"], "source_refs": refs}))
        for row in db.execute("SELECT id,revision,payload,author_principal FROM companion_v2_records WHERE character_id=? AND deleted=0",
                              (character,)):
            payload = json.loads(row["payload"])
            if payload["kind"] != "user_utterance" or payload["realm"] != request.realm:
                continue
            if conversation_filter is not None and payload.get("conversation_id") != conversation_filter:
                continue
            score = _score(request.query, payload["text"])
            if score <= 0:
                continue
            if db.execute("SELECT 1 FROM companion_v2_suppressions WHERE record_id=?", (row["id"],)).fetchone():
                continue
            candidates.append((score, row["id"], {"kind": "record", "id": row["id"],
                              "revision": row["revision"], "text": payload["text"],
                              "author": row["author_principal"], "recorded_at": payload["recorded_at"]}))
        candidates.sort(key=lambda item: (-item[0], item[1]))
        selected, used = [], 0
        for _, _, item in candidates:
            length = len(item["text"])
            if length > request.budget_chars - used:
                continue
            selected.append(item)
            used += length
            if len(selected) == 20:
                break
        return {"conversation_id": request.conversation_id, "purpose": request.purpose,
                "realm": request.realm, "head_seq": head, "privacy_epoch": epoch,
                "items": selected, "truncated": len(selected) < len(candidates)}
