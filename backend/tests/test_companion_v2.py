from uuid import uuid4
import sqlite3
import json
from pathlib import Path

import pytest
from fastapi.testclient import TestClient

from app.core.config import get_settings
from app.main import app
from app.core.companion_v2 import Commit


@pytest.fixture
def client(tmp_path, monkeypatch):
    monkeypatch.setenv("DATABASE_URL", "sqlite:///" + str(tmp_path / "companion.db"))
    monkeypatch.setenv("YUI_BACKEND_SETTINGS_PATH", str(tmp_path / "settings.json"))
    monkeypatch.setenv("COMPANION_V2_TESTING_ENABLED", "true")
    get_settings.cache_clear()
    with TestClient(app, base_url="http://localhost", client=("127.0.0.1", 4321)) as c:
        c.post("/admin/session", headers={"X-Yui-Admin": "1"})
        c.headers["X-Yui-Admin"] = "1"
        code = c.post("/admin/api/sync/pairing").json()["code"]
        device = c.post("/sync/pair", json={"name": "Phone", "code": code}).json()
        c.headers["Authorization"] = "Bearer " + device["token"]
        character = c.post("/sync/characters", json={"name": "Yui"}).json()["id"]
        yield c, character, device
    get_settings.cache_clear()


def op(kind="append_record", entity=None, revision=0, text="hello"):
    payload = None if kind == "delete_record" else {
        "kind": "user_utterance", "text": text, "recorded_at": "2026-10-03T12:00:00+09:00",
        "realm": "real",
    }
    return {"op_id": uuid4().hex, "type": kind, "entity_id": entity or uuid4().hex,
            "expected_revision": revision, "payload": payload}


def memory(source_id, source_revision=1, quote="hello", basis="user_statement"):
    return {"op_id": uuid4().hex, "type": "put_memory", "entity_id": uuid4().hex,
            "expected_revision": 0, "payload": {"type": "fact", "subject": "user", "text": "User said hello",
            "basis": basis, "source_refs": [{"record_id": source_id, "revision": source_revision, "quote": quote}]}}


def commit(c, character, *operations, batch=None):
    return c.post(f"/companion/v2/characters/{character}/commit",
                  json={"batch_id": batch or uuid4().hex, "operations": operations})


def create_entity(kind, payload, entity=None):
    return {"op_id": uuid4().hex, "type": kind, "entity_id": entity or uuid4().hex,
            "expected_revision": 0, "payload": payload}


def test_conversation_binding_is_pinned_and_character_scoped(client):
    c, character, _ = client
    other = c.post("/sync/characters", json={"name": "Other"}).json()["id"]
    binding = create_entity("create_binding", {"purpose": "talk", "connection_id": None,
                                                "context_policy": "private_only"})
    conversation = create_entity("create_conversation", {"purpose": "talk",
                                                         "binding_id": binding["entity_id"]})
    first = op(text="hello")
    first["payload"]["conversation_id"] = conversation["entity_id"]
    batch = uuid4().hex
    accepted = commit(c, character, binding, conversation, first, batch=batch)
    assert accepted.status_code == 200, accepted.text
    assert commit(c, character, binding, conversation, first, batch=batch).json() == accepted.json()
    page = c.get(f"/companion/v2/characters/{character}/changes").json()
    assert [item["entity_type"] for item in page["items"]] == ["binding", "conversation", "record"]
    assert page["items"][1]["conversation"]["binding_snapshot"] == {
        "connection_id": None, "context_policy": "private_only"}
    snapshot = c.get(f"/companion/v2/characters/{character}/snapshot").json()
    assert {item["entity_type"] for item in snapshot["items"]} == {"binding", "conversation", "record"}
    caught_up = c.get(f"/companion/v2/characters/{character}/changes",
                      params={"cursor": snapshot["change_cursor"]}).json()
    assert caught_up["items"] == [] and caught_up["head_seq"] == snapshot["head_seq"]
    outsider = op(text="wrong scope")
    outsider["payload"]["conversation_id"] = conversation["entity_id"]
    assert commit(c, other, outsider).json()["detail"]["code"] == "conversation_unavailable"
    changed = op("correct_record", first["entity_id"], 1, "moved")
    changed["payload"]["conversation_id"] = None
    assert commit(c, character, changed).json()["detail"]["code"] == "conversation_immutable"


def test_conversation_binding_purpose_and_atomic_rollback(client):
    c, character, _ = client
    binding = create_entity("create_binding", {"purpose": "work"})
    wrong = create_entity("create_conversation", {"purpose": "talk", "binding_id": binding["entity_id"]})
    response = commit(c, character, binding, wrong)
    assert response.status_code == 409 and response.json()["detail"]["code"] == "binding_purpose_mismatch"
    page = c.get(f"/companion/v2/characters/{character}/changes").json()
    assert page["items"] == [] and page["head_seq"] == 0
    assert commit(c, character, binding).status_code == 200
    missing = create_entity("create_conversation", {"purpose": "work", "binding_id": uuid4().hex})
    assert commit(c, character, missing).json()["detail"]["code"] == "binding_unavailable"


def test_profile_is_versioned_private_and_does_not_write_legacy_source(client):
    c, character, _ = client
    profile = create_entity("update_profile", {"field": "name", "text": "Yui"})
    batch = uuid4().hex
    first = commit(c, character, profile, batch=batch)
    assert first.status_code == 200, first.text
    assert commit(c, character, profile, batch=batch).json() == first.json()
    snapshot = c.get(f"/companion/v2/characters/{character}/snapshot").json()
    assert snapshot["items"][0]["profile"] == {"field": "name", "text": "Yui"}
    changed = {**profile, "op_id": uuid4().hex, "expected_revision": 1,
               "payload": {"field": "name", "text": "Yui corrected"}}
    assert commit(c, character, changed).status_code == 200
    stale = c.get(f"/companion/v2/characters/{character}/changes",
                  params={"cursor": snapshot["change_cursor"]})
    assert stale.status_code == 410
    assert commit(c, character, {**changed, "op_id": uuid4().hex}).status_code == 409
    renamed_field = {**changed, "op_id": uuid4().hex, "expected_revision": 2,
                     "payload": {"field": "instruction", "text": "wrong identity"}}
    assert commit(c, character, renamed_field).json()["detail"]["code"] == "profile_identity_conflict"
    duplicate = create_entity("update_profile", {"field": "name", "text": "duplicate"})
    assert commit(c, character, duplicate).json()["detail"]["code"] == "profile_field_conflict"
    with sqlite3.connect(get_settings().database_url.removeprefix("sqlite:///")) as db:
        assert db.execute("SELECT count(*) FROM sync_items WHERE character_id=?", (character,)).fetchone()[0] == 0
        assert db.execute("SELECT name FROM sync_characters WHERE id=?", (character,)).fetchone()[0] == "Yui"


def test_reply_target_is_in_same_conversation_and_links_cannot_move(client):
    c, character, _ = client
    one = create_entity("create_conversation", {"purpose": "talk", "binding_id": None})
    two = create_entity("create_conversation", {"purpose": "work", "binding_id": None})
    assert commit(c, character, one, two).status_code == 200
    user = op(text="first question")
    user["payload"].update({"conversation_id": one["entity_id"], "turn_id": uuid4().hex})
    assert commit(c, character, user).status_code == 200
    reply = op(text="answer")
    reply["payload"].update({"kind": "assistant_utterance", "conversation_id": one["entity_id"],
                             "turn_id": user["payload"]["turn_id"],
                             "in_reply_to_record_id": user["entity_id"]})
    assert commit(c, character, reply).status_code == 200
    wrong = op(text="wrong conversation")
    wrong["payload"].update({"kind": "assistant_utterance", "conversation_id": two["entity_id"],
                             "in_reply_to_record_id": user["entity_id"]})
    assert commit(c, character, wrong).json()["detail"]["code"] == "reply_target_unavailable"
    changed = op("correct_record", reply["entity_id"], 1, "edited answer")
    changed["payload"].update({"kind": "assistant_utterance", "conversation_id": one["entity_id"],
                               "turn_id": user["payload"]["turn_id"]})
    assert commit(c, character, changed).json()["detail"]["code"] == "reply_link_immutable"
    changed["payload"]["in_reply_to_record_id"] = user["entity_id"]
    assert commit(c, character, changed).status_code == 200


def test_record_correction_cannot_relabel_speaker_or_realm(client):
    c, character, _ = client
    source = op(text="I like mango")
    assert commit(c, character, source).status_code == 200
    changed = op("correct_record", source["entity_id"], 1, "I like ginger")
    changed["payload"]["kind"] = "assistant_utterance"
    result = commit(c, character, changed)
    assert result.status_code == 409
    assert result.json()["detail"]["code"] == "record_origin_immutable"
    changed["op_id"] = uuid4().hex
    changed["payload"]["kind"] = "user_utterance"
    changed["payload"]["realm"] = "roleplay"
    assert commit(c, character, changed).json()["detail"]["code"] == "record_origin_immutable"
    snapshot = c.get(f"/companion/v2/characters/{character}/snapshot").json()
    record = next(item["record"] for item in snapshot["items"] if item["entity_id"] == source["entity_id"])
    assert record["kind"] == "user_utterance" and record["realm"] == "real"
    assert record["text"] == "I like mango"


def test_owner_context_checks_conversation_purpose_and_forgets_sources(client):
    c, character, _ = client
    conversation = create_entity("create_conversation", {"purpose": "talk", "binding_id": None})
    assert commit(c, character, conversation).status_code == 200
    source = op(text="I enjoyed mango and ginger pancakes")
    source["payload"]["conversation_id"] = conversation["entity_id"]
    assert commit(c, character, source).status_code == 200
    remembered = memory(source["entity_id"], quote="mango")
    remembered["payload"]["text"] = "I enjoyed mango pancakes"
    assert commit(c, character, remembered).status_code == 200
    path = f"/companion/v2/characters/{character}/context"
    body = {"conversation_id": conversation["entity_id"], "purpose": "talk", "query": "mango"}
    packet = c.post(path, json=body).json()
    assert {item["kind"] for item in packet["items"]} == {"memory", "record"}
    assert all(item["id"] in {source["entity_id"], remembered["entity_id"]} for item in packet["items"])
    assert c.post(path, json={**body, "purpose": "work"}).status_code == 409
    assert c.post(path, json={**body, "conversation_id": uuid4().hex}).status_code == 404
    other = c.post("/sync/characters", json={"name": "Other"}).json()["id"]
    assert c.post(f"/companion/v2/characters/{other}/context", json=body).status_code == 404
    assert commit(c, character, op("delete_record", source["entity_id"], 1)).status_code == 200
    forgotten = c.post(path, json=body).json()
    assert forgotten["items"] == [] and forgotten["privacy_epoch"] > packet["privacy_epoch"]


def test_context_requires_whole_numeric_tokens_and_normalizes_fullwidth_query(client):
    c, character, _ = client
    conversation = create_entity("create_conversation", {"purpose": "talk", "binding_id": None})
    assert commit(c, character, conversation).status_code == 200
    records = [op(text="In 1999 I ate pancakes"), op(text="In 9999 I ate mango pancakes")]
    for record in records:
        record["payload"]["conversation_id"] = conversation["entity_id"]
    assert commit(c, character, *records).status_code == 200
    result = c.post(f"/companion/v2/characters/{character}/context", json={
        "conversation_id": conversation["entity_id"], "purpose": "talk", "query": "９９９９"
    })
    assert result.status_code == 200
    assert [item["id"] for item in result.json()["items"]] == [records[1]["entity_id"]]


def test_context_finds_topic_in_natural_question_without_partial_name_match(client):
    c, character, _ = client
    conversation = create_entity("create_conversation", {"purpose": "talk", "binding_id": None})
    assert commit(c, character, conversation).status_code == 200
    records = [op(text="I enjoyed mango pancakes"),
               op(text="I met Alicia on Sunday")]
    for record in records:
        record["payload"]["conversation_id"] = conversation["entity_id"]
    assert commit(c, character, *records).status_code == 200
    path = f"/companion/v2/characters/{character}/context"
    packet = c.post(path, json={"conversation_id": conversation["entity_id"],
                                "purpose": "talk",
                                "query": "What did I say about mango pancakes?"})
    assert packet.status_code == 200
    assert [item["id"] for item in packet.json()["items"]] == [records[0]["entity_id"]]
    name = c.post(path, json={"conversation_id": conversation["entity_id"],
                              "purpose": "talk", "query": "Alice"})
    assert name.status_code == 200 and name.json()["items"] == []


def test_atomic_commit_replay_collision_and_restart(client):
    c, character, _ = client
    first = op()
    batch = uuid4().hex
    response = commit(c, character, first, batch=batch)
    assert response.status_code == 200, response.text
    assert commit(c, character, first, batch=batch).json() == response.json()
    assert commit(c, character, op(), batch=batch).status_code == 409
    assert commit(c, character, first).status_code == 409
    new = op()
    conflict = op("correct_record", first["entity_id"], 0, "stale")
    assert commit(c, character, new, conflict).status_code == 409
    page = c.get(f"/companion/v2/characters/{character}/changes").json()
    assert len(page["items"]) == 1
    assert page["head_seq"] == 1


def test_single_operation_uses_stable_id_for_timeout_retry(client):
    c, character, _ = client
    first = op(text="retry after unknown network result")
    accepted = commit(c, character, first, batch=first["op_id"])
    assert accepted.status_code == 200
    assert commit(c, character, first, batch=first["op_id"]).json() == accepted.json()
    assert len(c.get(f"/companion/v2/characters/{character}/changes").json()["items"]) == 1


def test_delete_redacts_history_and_rejects_resurrection(client):
    c, character, _ = client
    first = op(text="PRIVATE_SENTINEL")
    assert commit(c, character, first).status_code == 200
    old = c.get(f"/companion/v2/characters/{character}/changes").json()
    deletion = op("delete_record", first["entity_id"], 1)
    assert commit(c, character, deletion).status_code == 200
    stale = c.get(f"/companion/v2/characters/{character}/changes",
                  params={"cursor": old["next_cursor"]})
    assert stale.status_code == 410
    fresh = c.get(f"/companion/v2/characters/{character}/changes")
    assert fresh.status_code == 200
    assert "PRIVATE_SENTINEL" not in fresh.text
    assert all(item["deleted"] and item["record"] is None for item in fresh.json()["items"])
    assert commit(c, character, op("correct_record", first["entity_id"], 2, "resurrect")).status_code == 409
    db_path = get_settings().database_url.removeprefix("sqlite:///")
    with sqlite3.connect(db_path) as db:
        assert db.execute("SELECT count(*) FROM companion_v2_record_revisions WHERE record_id=?",
                          (first["entity_id"],)).fetchone()[0] == 0


def test_correction_keeps_revision_until_forget(client):
    c, character, _ = client
    first = op(text="original")
    assert commit(c, character, first).status_code == 200
    assert commit(c, character, op("correct_record", first["entity_id"], 1, "corrected")).status_code == 200
    db_path = get_settings().database_url.removeprefix("sqlite:///")
    with sqlite3.connect(db_path) as db:
        revisions = db.execute("SELECT revision,payload FROM companion_v2_record_revisions WHERE record_id=? ORDER BY revision",
                               (first["entity_id"],)).fetchall()
    assert [r[0] for r in revisions] == [1, 2]
    assert "original" in revisions[0][1] and "corrected" in revisions[1][1]
    page = c.get(f"/companion/v2/characters/{character}/changes").json()
    assert "original" not in str(page)


def test_memory_sources_are_validated_and_invalidated_on_correction(client):
    c, character, _ = client
    source = op(text="hello")
    assert commit(c, character, source).status_code == 200
    missing = memory(uuid4().hex)
    assert commit(c, character, missing).status_code == 410
    bad_quote = memory(source["entity_id"], quote="not said")
    assert commit(c, character, bad_quote).status_code == 422
    wrong_basis = memory(source["entity_id"], basis="agent_report")
    assert commit(c, character, wrong_basis).status_code == 422
    remembered = memory(source["entity_id"])
    assert commit(c, character, remembered).status_code == 200
    before = c.get(f"/companion/v2/characters/{character}/changes").json()
    assert any(item.get("memory", {}).get("text") == "User said hello" for item in before["items"] if item["entity_type"] == "memory")
    assert commit(c, character, op("correct_record", source["entity_id"], 1, "goodbye")).status_code == 200
    after = c.get(f"/companion/v2/characters/{character}/changes").json()
    assert "User said hello" not in str(after)
    assert any(item.get("state") == "invalidated" for item in after["items"])
    assert commit(c, character, memory(source["entity_id"])).status_code == 410
    db_path = get_settings().database_url.removeprefix("sqlite:///")
    with sqlite3.connect(db_path) as db:
        assert db.execute("SELECT count(*) FROM companion_v2_sources WHERE memory_id=?",
                          (remembered["entity_id"],)).fetchone()[0] == 0


def test_memory_correction_invalidates_old_snapshot_and_change_cursor(client):
    c, character, _ = client
    source = op(text="I like mango")
    assert commit(c, character, source).status_code == 200
    remembered = memory(source["entity_id"], quote="mango")
    remembered["payload"]["text"] = "I like mango"
    assert commit(c, character, remembered).status_code == 200
    snapshot_path = f"/companion/v2/characters/{character}/snapshot"
    assert c.get(snapshot_path).status_code == 200
    db_path = get_settings().database_url.removeprefix("sqlite:///")
    with sqlite3.connect(db_path) as db:
        token = db.execute("SELECT token FROM companion_v2_snapshots ORDER BY rowid DESC LIMIT 1").fetchone()[0]
    old_cursor = c.get(f"/companion/v2/characters/{character}/changes").json()["next_cursor"]
    updated = memory(source["entity_id"], quote="mango")
    updated["entity_id"] = remembered["entity_id"]
    updated["expected_revision"] = 1
    updated["payload"]["text"] = "I once liked mango"
    assert commit(c, character, updated).status_code == 200
    assert c.get(snapshot_path, params={"cursor": token + ":0"}).status_code == 410
    assert c.get(f"/companion/v2/characters/{character}/changes",
                 params={"cursor": old_cursor}).status_code == 410
    fresh = c.get(snapshot_path).json()
    memories = [item["memory"] for item in fresh["items"] if item["entity_type"] == "memory"]
    assert len(memories) == 1 and memories[0]["text"] == "I once liked mango"


def test_memory_batch_rollback_and_source_forget(client):
    c, character, _ = client
    source = op(text="hello secret")
    saved = memory(source["entity_id"], quote="hello")
    assert commit(c, character, source, saved).status_code == 200
    unrelated = op(text="must roll back")
    invalid = memory(uuid4().hex)
    assert commit(c, character, unrelated, invalid).status_code == 410
    assert "must roll back" not in c.get(f"/companion/v2/characters/{character}/changes").text
    assert commit(c, character, op("delete_record", source["entity_id"], 1)).status_code == 200
    response = c.get(f"/companion/v2/characters/{character}/changes")
    assert "hello secret" not in response.text
    assert "User said hello" not in response.text
    db_path = get_settings().database_url.removeprefix("sqlite:///")
    with sqlite3.connect(db_path) as db:
        assert db.execute("SELECT count(*) FROM companion_v2_sources WHERE record_id=?",
                          (source["entity_id"],)).fetchone()[0] == 0
        row = db.execute("SELECT state,payload FROM companion_v2_memories WHERE id=?",
                         (saved["entity_id"],)).fetchone()
        assert row == ("invalidated", None)


def test_cursor_is_bound_to_device(client):
    c, character, first_device = client
    assert commit(c, character, op()).status_code == 200
    cursor = c.get(f"/companion/v2/characters/{character}/changes").json()["next_cursor"]
    c.headers.pop("Authorization")
    code = c.post("/admin/api/sync/pairing").json()["code"]
    second_device = c.post("/sync/pair", json={"name": "PC", "code": code}).json()
    c.headers["Authorization"] = "Bearer " + second_device["token"]
    assert c.get(f"/companion/v2/characters/{character}/changes",
                 params={"cursor": cursor}).status_code == 410
    c.headers["Authorization"] = "Bearer " + first_device["token"]
    assert c.get(f"/companion/v2/characters/{character}/changes",
                 params={"cursor": cursor}).status_code == 200


def test_snapshot_is_fixed_paged_scoped_and_purged_on_forget(client):
    c, character, _ = client
    records = [op(text=f"record-{index}") for index in range(205)]
    for start in range(0, len(records), 50):
        assert commit(c, character, *records[start:start + 50]).status_code == 200
    path = f"/companion/v2/characters/{character}/snapshot"
    first = c.get(path).json()
    assert len(first["items"]) == 200 and first["has_more"]
    cursor = first["next_cursor"]
    later = op(text="later")
    assert commit(c, character, later).status_code == 200
    second = c.get(path, params={"cursor": cursor}).json()
    assert len(second["items"]) == 5 and second["head_seq"] == first["head_seq"]
    assert "later" not in str(second)
    other = c.post("/sync/characters", json={"name": "Other"}).json()["id"]
    assert c.get(f"/companion/v2/characters/{other}/snapshot", params={"cursor": cursor}).status_code == 410
    assert commit(c, character, op("delete_record", records[0]["entity_id"], 1)).status_code == 200
    old = c.get(path, params={"cursor": cursor})
    assert old.status_code == 410
    assert old.json()["detail"]["code"] == "privacy_reset_required"
    new = c.get(path)
    assert new.status_code == 200
    assert "record-0" not in str(new.json())
    db_path = get_settings().database_url.removeprefix("sqlite:///")
    with sqlite3.connect(db_path) as db:
        assert db.execute("SELECT count(*) FROM companion_v2_snapshot_items WHERE token=?",
                          (cursor.split(":")[0],)).fetchone()[0] == 0


def test_shared_unity_python_contract_fixture(client):
    c, character, _ = client
    fixture = Path(__file__).resolve().parents[2] / "unity/Assets/Tests/Fixtures/companion_v2_commit.json"
    body = json.loads(fixture.read_text(encoding="utf-8"))
    assert len(Commit.model_validate(body).operations) == 4
    response = c.post(f"/companion/v2/characters/{character}/commit", json=body)
    assert response.status_code == 200, response.text
    assert [item["revision"] for item in response.json()["accepted"]] == [1, 1, 1, 1]
    page = c.get(f"/companion/v2/characters/{character}/changes").json()
    assert page["items"][3]["memory"]["source_refs"][0]["revision"] == 1


def test_snapshot_quota_and_size_failure_are_atomic(client, monkeypatch):
    import app.core.companion_v2 as companion_v2
    c, character, _ = client
    assert commit(c, character, op(text="hello")).status_code == 200
    path = f"/companion/v2/characters/{character}/snapshot"
    for _ in range(5):
        assert c.get(path).status_code == 200
    db_path = get_settings().database_url.removeprefix("sqlite:///")
    with sqlite3.connect(db_path) as db:
        assert db.execute("SELECT count(*) FROM companion_v2_snapshots WHERE character_id=?",
                          (character,)).fetchone()[0] == 3
    monkeypatch.setattr(companion_v2, "MAX_SNAPSHOT_BYTES", 1)
    assert c.get(path).status_code == 413
    with sqlite3.connect(db_path) as db:
        assert db.execute("SELECT count(*) FROM companion_v2_snapshots WHERE character_id=?",
                          (character,)).fetchone()[0] == 3


def test_change_and_snapshot_pages_obey_encoded_byte_limit(client, monkeypatch):
    import app.core.companion_v2 as companion_v2
    c, character, _ = client
    assert commit(c, character, op(text="a" * 80), op(text="b" * 80)).status_code == 200
    monkeypatch.setattr(companion_v2, "MAX_PAGE_BYTES", 800)
    changes = c.get(f"/companion/v2/characters/{character}/changes")
    assert changes.status_code == 200
    assert len(changes.content) <= 800 and changes.json()["has_more"]
    next_page = c.get(f"/companion/v2/characters/{character}/changes",
                      params={"cursor": changes.json()["next_cursor"]})
    assert next_page.status_code == 200 and len(next_page.content) <= 800
    snapshot = c.get(f"/companion/v2/characters/{character}/snapshot")
    assert snapshot.status_code == 200
    assert len(snapshot.content) <= 800 and snapshot.json()["has_more"]
    monkeypatch.setattr(companion_v2, "MAX_PAGE_BYTES", 10)
    assert c.get(f"/companion/v2/characters/{character}/changes").status_code == 413


def test_cursor_scope_tampering_paging_and_privacy_reset(client):
    c, character, _ = client
    other = c.post("/sync/characters", json={"name": "Other"}).json()["id"]
    records = [op(text=f"record-{index}") for index in range(3)]
    assert commit(c, character, *records).status_code == 200
    path = f"/companion/v2/characters/{character}/changes"
    first = c.get(path, params={"limit": 1}).json()
    assert first["has_more"] and len(first["items"]) == 1
    second = c.get(path, params={"limit": 1, "cursor": first["next_cursor"]}).json()
    assert second["items"][0]["seq"] == 2
    assert c.get(path, params={"cursor": first["next_cursor"] + "x"}).status_code == 410
    assert c.get(f"/companion/v2/characters/{other}/changes",
                 params={"cursor": first["next_cursor"]}).status_code == 410
    assert commit(c, character, op("correct_record", records[0]["entity_id"], 1, "new")).status_code == 200
    assert c.get(path, params={"cursor": second["next_cursor"]}).json()["detail"]["code"] == "privacy_reset_required"


def test_auth_validation_and_body_limit(client):
    c, character, device = client
    assert c.get("/companion/v2/capabilities").json()["enabled_features"] == [
        "record_ledger_testing", "profile_version_testing", "memory_source_testing", "snapshot_testing",
        "conversation_binding_testing", "owner_context_testing", "activity_state_testing"]
    forged = op()
    forged["payload"]["author"] = "owner"
    assert commit(c, character, forged).status_code == 422
    assert commit(c, character, create_entity("update_profile", {"field": "name", "text": ""})).status_code == 422
    assert c.post(f"/companion/v2/characters/{character}/commit", json={"batch_id": uuid4().hex,
                  "operations": [op(text="x" * 32001)]}).status_code == 422
    assert commit(c, character, *(op(text="x" * 5000) for _ in range(60))).status_code == 413
    c.headers["Authorization"] = "Bearer invalid"
    assert c.get("/companion/v2/capabilities").status_code == 401
    c.headers["Authorization"] = "Bearer " + device["token"]
    c.post(f"/admin/api/sync/devices/{device['device_id']}/revoke")
    assert c.get("/companion/v2/capabilities").status_code == 401


def test_forget_source_suppresses_reextraction_without_erasing_record(client):
    c, character, _ = client
    conversation = create_entity("create_conversation", {"purpose": "talk", "binding_id": None})
    assert commit(c, character, conversation).status_code == 200
    source = op(text="I enjoyed mango pancakes")
    source["payload"]["conversation_id"] = conversation["entity_id"]
    first_memory = memory(source["entity_id"], quote="mango pancakes")
    assert commit(c, character, source, first_memory).status_code == 200
    cursor = c.get(f"/companion/v2/characters/{character}/changes").json()["next_cursor"]
    forget = {"op_id": uuid4().hex, "type": "forget_source", "entity_id": source["entity_id"],
              "expected_revision": 1, "payload": None}
    batch = uuid4().hex
    result = commit(c, character, forget, batch=batch)
    assert result.status_code == 200, result.text
    assert commit(c, character, forget, batch=batch).json() == result.json()
    assert c.get(f"/companion/v2/characters/{character}/changes", params={"cursor": cursor}).status_code == 410
    snapshot = c.get(f"/companion/v2/characters/{character}/snapshot").json()["items"]
    assert any(item["entity_type"] == "suppression" and item["entity_id"] == source["entity_id"] for item in snapshot)
    assert any(item["entity_type"] == "record" and item["record"]["text"] == source["payload"]["text"]
               for item in snapshot)
    assert any(item["entity_type"] == "memory" and item["state"] == "deleted" and item["memory"] is None
               for item in snapshot)
    assert commit(c, character, memory(source["entity_id"], quote="mango pancakes")).json()["detail"]["code"] == "source_suppressed"
    assert commit(c, character, {**forget, "op_id": uuid4().hex}).json()["detail"]["code"] == "source_already_suppressed"
    context = c.post(f"/companion/v2/characters/{character}/context", json={
        "conversation_id": conversation["entity_id"], "purpose": "talk", "query": "mango"}).json()
    assert context["items"] == []


def test_record_author_and_receive_time_come_from_server(client):
    c, character, device = client
    first = op()
    assert commit(c, character, first).status_code == 200
    page = c.get(f"/companion/v2/characters/{character}/changes").json()
    record = page["items"][0]["record"]
    assert record["author"] == "device:" + device["device_id"]
    assert record["received_at"].endswith("+00:00")
    assert record["recorded_at"] == "2026-10-03T12:00:00+09:00"
