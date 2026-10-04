from uuid import uuid4
import pytest
from fastapi import HTTPException

from app.core.companion_v2 import CompanionStore
from app.core.companion_activity import ActivityStore, ActivityReport
from app.core.config import get_settings
from test_companion_v2 import client, commit, create_entity, op


def setup_work(c, character):
    connection = uuid4().hex
    binding = create_entity("create_binding", {"purpose": "work", "connection_id": connection,
                                                "context_policy": "private_only"})
    conversation = create_entity("create_conversation", {"purpose": "work", "binding_id": binding["entity_id"]})
    assert commit(c, character, binding, conversation).status_code == 200
    return connection, conversation["entity_id"]


def test_owner_activity_list_pages_without_losing_older_work(client):
    c, character, _ = client
    connection, conversation = setup_work(c, character)
    path = f"/companion/v2/characters/{character}/activities"
    ids = [uuid4().hex for _ in range(3)]
    for index, activity_id in enumerate(ids):
        assert c.post(path, json={"op_id": uuid4().hex, "activity_id": activity_id,
            "conversation_id": conversation, "request": f"Task {index}"}).status_code == 200
    first = c.get(path, params={"connection_id": connection, "limit": 2}).json()
    assert [item["activity_id"] for item in first["items"]] == ids[::-1][:2]
    assert first["next_before"] == ids[1]
    second = c.get(path, params={"connection_id": connection, "limit": 2,
                                 "before": first["next_before"]}).json()
    assert [item["activity_id"] for item in second["items"]] == [ids[0]]
    assert second["next_before"] is None
    assert c.get(path, params={"before": uuid4().hex}).status_code == 422


def test_answer_question_preserves_original_request_and_question_report(client):
    c, character, _ = client
    connection, conversation = setup_work(c, character)
    activity = uuid4().hex
    path = f"/companion/v2/characters/{character}/activities/{activity}"
    assert c.post(path.rsplit("/", 1)[0], json={"op_id": uuid4().hex,
        "activity_id": activity, "conversation_id": conversation,
        "request": "Compare three choices"}).status_code == 200
    store = ActivityStore(CompanionStore(get_settings().database_url))
    question_id = uuid4().hex
    question = store.report_for_test(character, activity, connection, ActivityReport(
        op_id=question_id, request_revision=1, expected_revision=1,
        kind="question", text="Which region?"))
    assert question["state"] == "waiting_user"
    answer = {"op_id": uuid4().hex, "expected_revision": 2,
              "request_revision": 1, "command": "answer_question",
              "question_op_id": question_id, "text": "Tokyo"}
    accepted = c.post(path + "/commands", json=answer)
    assert accepted.status_code == 200, accepted.text
    assert accepted.json()["revision"] == 3
    assert c.post(path + "/commands", json=answer).json() == accepted.json()
    saved = c.get(path).json()
    assert saved["request"] == "Compare three choices"
    assert saved["request_revision"] == 1
    assert saved["reports"][0]["text"] == "Which region?"
    assert saved["answers"][0]["text"] == "Tokyo"
    inbox = store.read_outbox_for_test(connection)
    assert len(inbox) == 1 and inbox[0]["kind"] == "answer"
    assert inbox[0]["answer"] == {"question_op_id": question_id, "text": "Tokyo"}
    assert inbox[0]["request"] is None
    assert c.post(path + "/commands", json={**answer, "op_id": uuid4().hex,
        "expected_revision": 3}).status_code == 409
    result = store.report_for_test(character, activity, connection, ActivityReport(
        op_id=uuid4().hex, request_revision=1, expected_revision=3,
        kind="result", text="Here are the Tokyo options"))
    assert result["applied"] and result["state"] == "completed"
    assert store.read_outbox_for_test(connection) == []


def test_activity_request_revisions_late_report_and_no_cached_request_text(client):
    c, character, _ = client
    connection, conversation = setup_work(c, character)
    activity = uuid4().hex
    body = {"op_id": uuid4().hex, "activity_id": activity, "conversation_id": conversation,
            "request": "OLD_PRIVATE_TASK"}
    path = f"/companion/v2/characters/{character}/activities"
    created = c.post(path, json=body)
    assert created.status_code == 200, created.text
    assert created.json()["state"] == "queued" and "OLD_PRIVATE_TASK" not in created.text
    assert c.post(path, json=body).json() == created.json()
    snapshot_path = f"/companion/v2/characters/{character}/snapshot"
    assert c.get(snapshot_path).status_code == 200
    cursor = c.get(f"/companion/v2/characters/{character}/changes").json()["next_cursor"]
    details = c.get(path + "/" + activity).json()
    assert details["request"] == "OLD_PRIVATE_TASK" and details["binding_snapshot"]["connection_id"] == connection
    command = {"op_id": uuid4().hex, "expected_revision": 1, "request_revision": 1,
               "command": "update_request", "text": "NEW_TASK"}
    changed = c.post(path + "/" + activity + "/commands", json=command)
    assert changed.status_code == 200 and changed.json()["request_revision"] == 2
    assert "OLD_PRIVATE_TASK" not in changed.text
    assert c.post(path + "/" + activity + "/commands", json=command).json() == changed.json()
    assert c.get(f"/companion/v2/characters/{character}/changes", params={"cursor": cursor}).status_code == 410
    assert "OLD_PRIVATE_TASK" not in c.get(snapshot_path).text
    store = ActivityStore(CompanionStore(get_settings().database_url))
    stale = ActivityReport(op_id=uuid4().hex, request_revision=1, expected_revision=1,
                           kind="result", text="finished old task")
    late = store.report_for_test(character, activity, connection, stale)
    assert not late["applied"] and late["state"] == "queued"
    current = ActivityReport(op_id=uuid4().hex, request_revision=2, expected_revision=2,
                             kind="result", text="finished new task")
    applied = store.report_for_test(character, activity, connection, current)
    assert applied["applied"] and applied["state"] == "completed"
    assert applied["completion_basis"] == "external_report"
    assert store.report_for_test(character, activity, connection, current) == applied
    details = c.get(path + "/" + activity).json()
    assert [report["applied"] for report in details["reports"]] == [0, 1]
    assert store.read_outbox_for_test(connection) == []


def test_activity_cancel_does_not_claim_external_stop_and_scope_is_checked(client):
    c, character, _ = client
    connection, conversation = setup_work(c, character)
    activity = uuid4().hex
    path = f"/companion/v2/characters/{character}/activities"
    assert c.post(path, json={"op_id": uuid4().hex, "activity_id": activity,
                              "conversation_id": conversation, "request": "do something"}).status_code == 200
    other = c.post("/sync/characters", json={"name": "Other"}).json()["id"]
    assert c.get(f"/companion/v2/characters/{other}/activities/{activity}").status_code == 404
    cancel = {"op_id": uuid4().hex, "expected_revision": 1, "request_revision": 1,
              "command": "cancel"}
    stopped = c.post(path + "/" + activity + "/commands", json=cancel)
    assert stopped.status_code == 200
    assert stopped.json()["state"] == "cancel_requested" and stopped.json()["stop_state"] == "requested"
    store = ActivityStore(CompanionStore(get_settings().database_url))
    result = ActivityReport(op_id=uuid4().hex, request_revision=1, expected_revision=2,
                            kind="result", text="late result")
    assert not store.report_for_test(character, activity, connection, result)["applied"]
    ack = ActivityReport(op_id=uuid4().hex, request_revision=1, expected_revision=2,
                         kind="cancel_ack", text="stopped")
    assert store.report_for_test(character, activity, connection, ack)["state"] == "cancelled"
    assert c.get(path + "/" + activity).json()["stop_state"] == "confirmed"
    assert store.read_outbox_for_test(connection) == []


def test_work_report_keeps_full_result_and_independent_short_speech(client):
    c, character, _ = client
    connection, conversation = setup_work(c, character)
    activity = uuid4().hex
    path = f"/companion/v2/characters/{character}/activities"
    assert c.post(path, json={"op_id": uuid4().hex, "activity_id": activity,
                              "conversation_id": conversation, "request": "Research this"}).status_code == 200
    store = ActivityStore(CompanionStore(get_settings().database_url))
    detail = "Detailed finding with sources. " * 100
    report = ActivityReport(op_id=uuid4().hex, request_revision=1, expected_revision=1,
                            kind="result", text=detail,
                            spoken_text="結論は一つだよ。詳しい根拠は画面で見てね。")
    assert store.report_for_test(character, activity, connection, report)["applied"]
    saved = c.get(path + "/" + activity).json()["reports"][0]
    assert saved["text"] == detail
    assert saved["spoken_text"] == "結論は一つだよ。詳しい根拠は画面で見てね。"
    assert len(saved["spoken_text"]) < len(saved["text"])

    second = uuid4().hex
    assert c.post(path, json={"op_id": uuid4().hex, "activity_id": second,
                              "conversation_id": conversation, "request": "Another task"}).status_code == 200
    no_speech = ActivityReport(op_id=uuid4().hex, request_revision=1, expected_revision=1,
                               kind="result", text="Misleading first sentence. " * 100)
    assert store.report_for_test(character, second, connection, no_speech)["applied"]
    saved_fallback = c.get(path + "/" + second).json()["reports"][0]
    assert saved_fallback["spoken_text"] == "作業結果を画面にまとめたよ。"


def test_work_speech_is_claimed_once_and_stale_results_never_speak(client):
    c, character, _ = client
    connection, conversation = setup_work(c, character)
    path = f"/companion/v2/characters/{character}/activities"
    activity = uuid4().hex
    assert c.post(path, json={"op_id": uuid4().hex, "activity_id": activity,
                              "conversation_id": conversation, "request": "Find sources"}).status_code == 200
    report_id = uuid4().hex
    store = ActivityStore(CompanionStore(get_settings().database_url))
    assert store.report_for_test(character, activity, connection, ActivityReport(
        op_id=report_id, request_revision=1, expected_revision=1,
        kind="result", text="long result"))["applied"]
    speech = path + f"/{activity}/reports/{report_id}/speech"
    claimed = c.post(speech + "/claim").json()
    assert claimed["claimed"]
    assert c.post(speech + "/claim").json() == {"claimed": False, "reason": "already_claimed"}
    token = claimed["claim_token"]
    assert c.post(speech + "/completed", json={"claim_token": token}).status_code == 409
    assert c.post(speech + "/started", json={"claim_token": token}).json()["state"] == "started"
    assert c.post(speech + "/claim").json()["claimed"] is False
    assert c.post(speech + "/completed", json={"claim_token": token}).json()["state"] == "completed"
    assert c.post(speech + "/claim").json()["claimed"] is False

    stale_activity = uuid4().hex
    assert c.post(path, json={"op_id": uuid4().hex, "activity_id": stale_activity,
                              "conversation_id": conversation, "request": "old request"}).status_code == 200
    stale_report = uuid4().hex
    assert c.post(path + f"/{stale_activity}/commands", json={
        "op_id": uuid4().hex, "expected_revision": 1,
        "request_revision": 1, "command": "cancel"}).status_code == 200
    assert not store.report_for_test(character, stale_activity, connection, ActivityReport(
        op_id=stale_report, request_revision=1, expected_revision=1,
        kind="result", text="late result"))["applied"]
    assert c.post(path + f"/{stale_activity}/reports/{stale_report}/speech/claim").json() == {
        "claimed": False, "reason": "not_presentable"}


def test_activity_ids_and_operations_cannot_collide_with_ledger(client):
    c, character, _ = client
    connection, conversation = setup_work(c, character)
    activity = uuid4().hex
    path = f"/companion/v2/characters/{character}/activities"
    create_op = uuid4().hex
    assert c.post(path, json={"op_id": create_op, "activity_id": activity,
                              "conversation_id": conversation, "request": "private task"}).status_code == 200
    conflicting_record = op(entity=activity)
    assert commit(c, character, conflicting_record).json()["detail"]["code"] == "record_id_conflict"
    conflicting_binding = create_entity("create_binding", {"purpose": "work"}, entity=activity)
    assert commit(c, character, conflicting_binding).json()["detail"]["code"] == "entity_id_conflict"
    reused_op = op()
    reused_op["op_id"] = create_op
    assert commit(c, character, reused_op).json()["detail"]["code"] == "idempotency_mismatch"
    ledger_op = op()
    assert commit(c, character, ledger_op).status_code == 200
    assert c.post(path + "/" + activity + "/commands", json={
        "op_id": ledger_op["op_id"], "expected_revision": 1, "request_revision": 1,
        "command": "cancel"}).json()["detail"]["code"] == "idempotency_mismatch"
    report = ActivityReport(op_id=uuid4().hex, request_revision=1, expected_revision=1,
                            kind="progress", text="quotes private task")
    store = ActivityStore(CompanionStore(get_settings().database_url))
    assert store.report_for_test(character, activity, connection, report)["applied"]
    assert c.post(path + "/" + activity + "/commands", json={
        "op_id": uuid4().hex, "expected_revision": 2, "request_revision": 1,
        "command": "update_request", "text": "replacement"}).status_code == 200
    assert "quotes private task" not in c.get(path + "/" + activity).text


def test_outbox_is_durable_and_never_delivers_superseded_request(client):
    c, character, _ = client
    connection, conversation = setup_work(c, character)
    activity = uuid4().hex
    path = f"/companion/v2/characters/{character}/activities"
    assert c.post(path, json={"op_id": uuid4().hex, "activity_id": activity,
                              "conversation_id": conversation, "request": "OLD_REQUEST"}).status_code == 200
    store = ActivityStore(CompanionStore(get_settings().database_url))
    first = store.read_outbox_for_test(connection)
    assert len(first) == 1 and first[0]["request"] == "OLD_REQUEST"
    assert c.post(path + "/" + activity + "/commands", json={
        "op_id": uuid4().hex, "expected_revision": 1, "request_revision": 1,
        "command": "update_request", "text": "NEW_REQUEST"}).status_code == 200
    restarted = ActivityStore(CompanionStore(get_settings().database_url))
    latest = restarted.read_outbox_for_test(connection)
    assert len(latest) == 1 and latest[0]["request"] == "NEW_REQUEST"
    assert restarted.acknowledge_outbox_for_test(connection, first[0]["notification_id"])["state"] == "superseded"
    assert restarted.acknowledge_outbox_for_test(connection, latest[0]["notification_id"])["state"] == "acknowledged"
    assert restarted.read_outbox_for_test(connection) == []
    assert c.post(path + "/" + activity + "/commands", json={
        "op_id": uuid4().hex, "expected_revision": 2, "request_revision": 2,
        "command": "cancel"}).status_code == 200
    cancel_notice = restarted.read_outbox_for_test(connection)
    assert len(cancel_notice) == 1 and cancel_notice[0]["kind"] == "cancel"
    assert cancel_notice[0]["request"] is None
    with pytest.raises(HTTPException) as denied:
        restarted.acknowledge_outbox_for_test(uuid4().hex, cancel_notice[0]["notification_id"])
    assert denied.value.status_code == 404


def test_owner_can_reattach_to_work_after_restart_without_other_connection(client):
    c, character, _ = client
    connection, conversation = setup_work(c, character)
    other_connection, other_conversation = setup_work(c, character)
    path = f"/companion/v2/characters/{character}/activities"
    own = uuid4().hex
    other = uuid4().hex
    for activity_id, conversation_id, request in (
            (own, conversation, "own work"),
            (other, other_conversation, "other connection work")):
        assert c.post(path, json={"op_id": uuid4().hex, "activity_id": activity_id,
                                  "conversation_id": conversation_id, "request": request}).status_code == 200
    fetched = c.get(path, params={"connection_id": connection, "limit": 1})
    assert fetched.status_code == 200, fetched.text
    assert [item["activity_id"] for item in fetched.json()["items"]] == [own]
    assert "other connection work" not in fetched.text
    assert c.get(path, params={"connection_id": other_connection}).json()["items"][0]["activity_id"] == other
    assert c.get(path, params={"connection_id": "invalid"}).status_code == 422
    c.headers.pop("Authorization")
    assert c.get(path, params={"connection_id": connection}).status_code in (401, 403)
