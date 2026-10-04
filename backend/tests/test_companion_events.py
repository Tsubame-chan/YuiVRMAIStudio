import base64
import httpx
import socket
from uuid import uuid4

from app.core.companion_grants import GrantStore
from app.core.companion_activity import ActivityStore, ActivityReport
from app.core.companion_v2 import CompanionStore
from app.core.config import get_settings
from tools.companion_events import CompanionEvents, EventParams, EVENT
from test_companion_activity import setup_work
from test_companion_v2 import client


def test_events_deliver_ids_once_and_revoke_stops_future_delivery(client, monkeypatch):
    owner, character, _ = client
    connection, conversation = setup_work(owner, character)
    grant = owner.post("/companion/v2/grants", json={
        "connection_id": connection, "character_ids": [character],
        "operations": ["read_inbox", "acknowledge", "post_update"]}).json()
    store = GrantStore(CompanionStore(get_settings().database_url))
    callback_bodies = []

    def callback(request):
        body = __import__("json").loads(request.content)
        callback_bodies.append(body)
        if body.get("type") == "verification":
            return httpx.Response(200, json={"challenge": body["challenge"]})
        return httpx.Response(200, json={"accepted": True})

    monkeypatch.setattr("tools.companion_events._callback_url", lambda value: value)
    events = CompanionEvents(CompanionStore(get_settings().database_url),
        lambda conn, char: store.authorize(grant["token"], conn, char, "read_inbox"),
        httpx.Client(transport=httpx.MockTransport(callback)))
    secret = "whsec_" + base64.b64encode(b"s" * 32).decode()
    params = EventParams(name=EVENT,
        arguments={"connection_id": connection, "character_id": character},
        delivery={"mode": "webhook", "url": "https://callback.openai.com/test", "secret": secret})
    assert events.subscribe(params)["id"].startswith("sub_")
    activity = uuid4().hex
    assert owner.post(f"/companion/v2/characters/{character}/activities", json={
        "op_id": uuid4().hex, "activity_id": activity,
        "conversation_id": conversation, "request": "SYNTHETIC_PRIVATE_TEXT"}).status_code == 200
    monkeypatch.setattr("tools.companion_events._callback_url",
                        lambda value: (_ for _ in ()).throw(socket.gaierror("temporary DNS failure")))
    assert events.deliver_once() == 0
    monkeypatch.setattr("tools.companion_events._callback_url", lambda value: value)
    assert events.deliver_once() == 1
    assert events.deliver_once() == 0
    delivered = callback_bodies[-1]
    assert delivered["name"] == EVENT
    assert delivered["data"]["activity_id"] == activity
    assert "SYNTHETIC_PRIVATE_TEXT" not in str(delivered)
    question_id = uuid4().hex
    activity_store = ActivityStore(CompanionStore(get_settings().database_url))
    activity_store.report_for_test(character, activity, connection, ActivityReport(
        op_id=question_id, request_revision=1, expected_revision=1,
        kind="question", text="Which option?"))
    assert owner.post(f"/companion/v2/characters/{character}/activities/{activity}/commands",
        json={"op_id": uuid4().hex, "expected_revision": 2, "request_revision": 1,
              "command": "answer_question", "question_op_id": question_id,
              "text": "The first option"}).status_code == 200
    assert events.deliver_once() == 1
    answer_event = callback_bodies[-1]
    assert answer_event["data"]["kind"] == "answer"
    assert "The first option" not in str(answer_event)
    assert events.deliver_once() == 0
    with events.companion.connect() as db:
        db.execute("UPDATE companion_v2_event_subscriptions SET expires_at=0")
    assert events.deliver_once() == 0
    with events.companion.connect() as db:
        assert db.execute("SELECT COUNT(*) FROM companion_v2_event_subscriptions").fetchone()[0] == 0
    events.subscribe(params)
    owner.delete("/companion/v2/grants/" + grant["grant_id"])
    with events.companion.connect() as db:
        assert db.execute("SELECT COUNT(*) FROM companion_v2_event_subscriptions WHERE grant_id=?",
                          (grant["grant_id"],)).fetchone()[0] == 0
    second = uuid4().hex
    assert owner.post(f"/companion/v2/characters/{character}/activities", json={
        "op_id": uuid4().hex, "activity_id": second,
        "conversation_id": conversation, "request": "SECOND_PRIVATE_TEXT"}).status_code == 200
    assert events.deliver_once() == 0
