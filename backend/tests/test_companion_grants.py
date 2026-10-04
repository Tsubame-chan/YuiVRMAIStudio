from uuid import uuid4
import importlib

import pytest
from fastapi import HTTPException
from fastapi.testclient import TestClient

from app.core.companion_v2 import CompanionStore
from app.core.companion_grants import GrantStore, ExternalActivityService
from app.core.companion_activity import ActivityReport
from app.core.companion_context import ContextRequest
from app.core.config import get_settings
from test_companion_v2 import client
from test_companion_activity import setup_work
from test_companion_v2 import commit, op


def test_owner_grant_is_one_time_scoped_revocable_and_durable(client):
    c, character, _ = client
    other = c.post("/sync/characters", json={"name": "Other"}).json()["id"]
    connection = uuid4().hex
    issued = c.post("/companion/v2/grants", json={
        "connection_id": connection, "character_ids": [character],
        "operations": ["read_inbox", "post_update"], "ttl_seconds": 300})
    assert issued.status_code == 200, issued.text
    data = issued.json()
    token = data["token"]
    assert token.startswith("yui_cg_")
    grant = GrantStore(CompanionStore(get_settings().database_url))
    assert grant.authorize(token, connection, character, "read_inbox") == "grant:" + data["grant_id"]
    with grant.companion.connect() as db:
        assert db.execute("SELECT token_digest FROM companion_v2_grants WHERE id=?",
                          (data["grant_id"],)).fetchone()[0] != token
    for wrong_connection, wrong_character, wrong_operation in (
            (uuid4().hex, character, "read_inbox"), (connection, other, "read_inbox"),
            (connection, character, "read_context")):
        with pytest.raises(HTTPException) as denied:
            grant.authorize(token, wrong_connection, wrong_character, wrong_operation)
        assert denied.value.status_code == 403
    revoked = c.delete("/companion/v2/grants/" + data["grant_id"])
    assert revoked.status_code == 200 and revoked.json()["revoked"]
    with pytest.raises(HTTPException):
        GrantStore(CompanionStore(get_settings().database_url)).authorize(
            token, connection, character, "read_inbox")


def test_grant_rejects_duplicate_scope_and_unpaired_requests(client):
    c, character, _ = client
    request = {"connection_id": uuid4().hex, "character_ids": [character, character],
               "operations": ["read_inbox"]}
    assert c.post("/companion/v2/grants", json=request).status_code == 422
    c.headers.pop("Authorization")
    request["character_ids"] = [character]
    assert c.post("/companion/v2/grants", json=request).status_code in (401, 403)


def test_external_activity_seam_checks_grant_before_every_operation(client):
    c, character, _ = client
    connection, conversation = setup_work(c, character)
    other = c.post("/sync/characters", json={"name": "Other"}).json()["id"]
    other_binding = {"op_id": uuid4().hex, "type": "create_binding", "entity_id": uuid4().hex,
                     "expected_revision": 0, "payload": {"purpose": "work", "connection_id": connection}}
    other_conversation = {"op_id": uuid4().hex, "type": "create_conversation", "entity_id": uuid4().hex,
                          "expected_revision": 0, "payload": {"purpose": "work", "binding_id": other_binding["entity_id"]}}
    from test_companion_v2 import commit
    assert commit(c, other, other_binding, other_conversation).status_code == 200
    path = f"/companion/v2/characters/{character}/activities"
    activity = uuid4().hex
    assert c.post(path, json={"op_id": uuid4().hex, "activity_id": activity,
                              "conversation_id": conversation, "request": "PRIVATE_YUI_REQUEST"}).status_code == 200
    other_activity = uuid4().hex
    assert c.post(f"/companion/v2/characters/{other}/activities", json={
        "op_id": uuid4().hex, "activity_id": other_activity,
        "conversation_id": other_conversation["entity_id"],
        "request": "OTHER_CHARACTER_REQUEST"}).status_code == 200
    issued = c.post("/companion/v2/grants", json={
        "connection_id": connection, "character_ids": [character],
        "operations": ["read_inbox", "acknowledge", "post_update"]}).json()
    service = ExternalActivityService(CompanionStore(get_settings().database_url))
    token = issued["token"]
    inbox = service.read_inbox(token, connection, character)
    assert len(inbox) == 1 and inbox[0]["request"] == "PRIVATE_YUI_REQUEST"
    assert "OTHER_CHARACTER_REQUEST" not in str(inbox)
    with pytest.raises(HTTPException) as denied:
        service.read_inbox(token, connection, other)
    assert denied.value.status_code == 403
    with pytest.raises(HTTPException) as denied:
        service.acknowledge(token, connection, character,
                            service.activities.read_outbox_for_test(connection, character_id=other)[0]["notification_id"])
    assert denied.value.status_code == 404
    assert service.acknowledge(token, connection, character, inbox[0]["notification_id"])["state"] == "acknowledged"
    report = ActivityReport(op_id=uuid4().hex, request_revision=1, expected_revision=1,
                            kind="result", text="done")
    assert service.post_update(token, connection, character, activity, report)["applied"]
    c.delete("/companion/v2/grants/" + issued["grant_id"])
    with pytest.raises(HTTPException):
        service.read_inbox(token, connection, character)


def test_external_connector_receives_answer_in_same_activity(client):
    c, character, _ = client
    connection, conversation = setup_work(c, character)
    activity = uuid4().hex
    path = f"/companion/v2/characters/{character}/activities"
    assert c.post(path, json={"op_id": uuid4().hex, "activity_id": activity,
                              "conversation_id": conversation,
                              "request": "Find restaurants"}).status_code == 200
    issued = c.post("/companion/v2/grants", json={
        "connection_id": connection, "character_ids": [character],
        "operations": ["read_inbox", "acknowledge", "post_update"]}).json()
    service = ExternalActivityService(CompanionStore(get_settings().database_url))
    token = issued["token"]
    initial = service.read_inbox(token, connection, character)[0]
    service.acknowledge(token, connection, character, initial["notification_id"])
    question_id = uuid4().hex
    asked = service.post_update(token, connection, character, activity, ActivityReport(
        op_id=question_id, request_revision=1, expected_revision=1,
        kind="question", text="Which area?"))
    assert asked["state"] == "waiting_user"
    answer = {"op_id": uuid4().hex, "expected_revision": 2, "request_revision": 1,
              "command": "answer_question", "question_op_id": question_id, "text": "Tokyo"}
    accepted = c.post(path + "/" + activity + "/commands", json=answer)
    assert accepted.status_code == 200 and accepted.json()["state"] == "queued"
    inbox = service.read_inbox(token, connection, character)
    assert len(inbox) == 1 and inbox[0]["kind"] == "answer"
    assert inbox[0]["activity_revision"] == 3
    assert inbox[0]["request"] is None
    assert inbox[0]["answer"] == {"question_op_id": question_id, "text": "Tokyo"}
    assert c.get(path + "/" + activity).json()["request"] == "Find restaurants"
    service.acknowledge(token, connection, character, inbox[0]["notification_id"])
    done = service.post_update(token, connection, character, activity, ActivityReport(
        op_id=uuid4().hex, request_revision=1,
        expected_revision=inbox[0]["activity_revision"],
        kind="result", text="Here are the Tokyo options"))
    assert done["state"] == "completed"


def test_private_mcp_requires_header_and_filters_character(client, monkeypatch):
    c, character, _ = client
    connection, conversation = setup_work(c, character)
    activity = uuid4().hex
    assert c.post(f"/companion/v2/characters/{character}/activities", json={
        "op_id": uuid4().hex, "activity_id": activity, "conversation_id": conversation,
        "request": "MCP_PRIVATE_REQUEST"}).status_code == 200
    grant = c.post("/companion/v2/grants", json={
        "connection_id": connection, "character_ids": [character],
        "operations": ["read_inbox", "acknowledge", "post_update"]}).json()
    monkeypatch.setenv("YUI_COMPANION_MCP_TESTING_ENABLED", "true")
    adapter = importlib.reload(importlib.import_module("tools.companion_mcp_http"))
    with TestClient(adapter.app, base_url="http://127.0.0.1:8767") as mcp_client:
        payload = {"jsonrpc": "2.0", "id": 1, "method": "tools/call", "params": {
            "_meta": {"io.modelcontextprotocol/protocolVersion": "2026-07-28",
                      "io.modelcontextprotocol/clientCapabilities": {}},
            "name": "yui_read_inbox", "arguments": {"connection_id": connection,
                                                   "character_id": character}}}
        headers = {"Accept": "application/json, text/event-stream", "MCP-Protocol-Version": "2026-07-28",
                   "mcp-method": "tools/call", "mcp-name": "yui_read_inbox"}
        assert mcp_client.post("/mcp", json=payload, headers=headers).status_code == 401

        headers["Authorization"] = "Bearer " + grant["token"]
        response = mcp_client.post("/mcp", json=payload, headers=headers)
        assert response.status_code == 200, response.text
        assert "MCP_PRIVATE_REQUEST" in response.text
        assert grant["token"] not in response.text
        notification_id = ExternalActivityService(
            CompanionStore(get_settings().database_url)).read_inbox(
                grant["token"], connection, character)[0]["notification_id"]
        payload["id"] = 2
        payload["params"]["name"] = "yui_acknowledge"
        payload["params"]["arguments"] = {"connection_id": connection,
                                              "character_id": character,
                                              "notification_id": notification_id}
        headers["mcp-name"] = "yui_acknowledge"
        acknowledged = mcp_client.post("/mcp", json=payload, headers=headers)
        assert acknowledged.status_code == 200 and "acknowledged" in acknowledged.text
        payload["id"] = 3
        payload["params"]["name"] = "yui_post_update"
        payload["params"]["arguments"] = {"connection_id": connection,
                                              "character_id": character, "activity_id": activity,
                                              "op_id": uuid4().hex, "request_revision": 1,
                                              "expected_revision": 1, "kind": "result", "text": "done"}
        headers["mcp-name"] = "yui_post_update"
        reported = mcp_client.post("/mcp", json=payload, headers=headers)
        assert reported.status_code == 200 and '"applied":true' in reported.text.replace(" ", "")
        c.delete("/companion/v2/grants/" + grant["grant_id"])
        assert mcp_client.post("/mcp", json=payload, headers=headers).status_code == 401


def test_external_context_requires_granted_bound_work_conversation(client):
    c, character, _ = client
    connection, conversation = setup_work(c, character)
    another_connection, another_conversation = setup_work(c, character)
    record = op(text="The launch password is never shared")
    record["payload"]["conversation_id"] = conversation
    assert commit(c, character, record).status_code == 200
    unrelated = op(text="Unrelated launch password is secret")
    unrelated["payload"]["conversation_id"] = another_conversation
    assert commit(c, character, unrelated).status_code == 200
    grant = c.post("/companion/v2/grants", json={
        "connection_id": connection, "character_ids": [character],
        "operations": ["read_context"]}).json()
    service = ExternalActivityService(CompanionStore(get_settings().database_url))
    request = ContextRequest(conversation_id=conversation, purpose="work", query="launch password")
    packet = service.read_context(grant["token"], connection, character, request)
    assert packet["conversation_id"] == conversation
    assert any(item["text"] == "The launch password is never shared" for item in packet["items"])
    assert "Unrelated launch password is secret" not in str(packet)
    for wrong in (ContextRequest(conversation_id=another_conversation, purpose="work", query="launch"),
                  ContextRequest(conversation_id=conversation, purpose="talk", query="launch")):
        with pytest.raises(HTTPException) as denied:
            service.read_context(grant["token"], connection, character, wrong)
        assert denied.value.status_code == 403
    with pytest.raises(HTTPException):
        service.read_context(grant["token"], another_connection, character, request)


def test_private_mcp_context_tool_observes_conversation_scope(client, monkeypatch):
    c, character, _ = client
    connection, conversation = setup_work(c, character)
    record = op(text="Project deadline is Friday")
    record["payload"]["conversation_id"] = conversation
    assert commit(c, character, record).status_code == 200
    grant = c.post("/companion/v2/grants", json={
        "connection_id": connection, "character_ids": [character],
        "operations": ["read_context"]}).json()
    monkeypatch.setenv("YUI_COMPANION_MCP_TESTING_ENABLED", "true")
    adapter = importlib.reload(importlib.import_module("tools.companion_mcp_http"))
    with TestClient(adapter.app, base_url="http://127.0.0.1:8767") as mcp_client:
        payload = {"jsonrpc": "2.0", "id": 1, "method": "tools/call", "params": {
            "_meta": {"io.modelcontextprotocol/protocolVersion": "2026-07-28",
                      "io.modelcontextprotocol/clientCapabilities": {}},
            "name": "yui_read_context", "arguments": {
                "connection_id": connection, "character_id": character,
                "conversation_id": conversation, "purpose": "work", "query": "deadline"}}}
        headers = {"Accept": "application/json, text/event-stream",
                   "MCP-Protocol-Version": "2026-07-28", "mcp-method": "tools/call",
                   "mcp-name": "yui_read_context", "Authorization": "Bearer " + grant["token"]}
        response = mcp_client.post("/mcp", json=payload, headers=headers)
        assert response.status_code == 200, response.text
        assert "Project deadline is Friday" in response.text
        assert grant["token"] not in response.text


def test_private_mcp_oauth_discovery_requires_explicit_https_configuration(client, monkeypatch):
    monkeypatch.setenv("YUI_COMPANION_MCP_TESTING_ENABLED", "true")
    monkeypatch.setenv("YUI_COMPANION_OAUTH_RESOURCE", "https://example.test/private-mcp")
    monkeypatch.setenv("YUI_COMPANION_OAUTH_ISSUER", "https://auth.example.test")
    adapter = importlib.reload(importlib.import_module("tools.companion_mcp_http"))
    with TestClient(adapter.app, base_url="http://127.0.0.1:8767") as mcp_client:
        metadata = mcp_client.get("/.well-known/oauth-protected-resource/private-mcp")
        assert metadata.status_code == 200
        assert metadata.json()["resource"] == "https://example.test/private-mcp"
        assert metadata.json()["authorization_servers"] == ["https://auth.example.test"]
        denied = mcp_client.post("/mcp", json={"jsonrpc": "2.0", "id": 1,
                                                  "method": "resources/list", "params": {}})
        assert denied.status_code == 401
        assert denied.headers["www-authenticate"].startswith(
            'Bearer resource_metadata="https://example.test/.well-known/oauth-protected-resource/private-mcp"')
    monkeypatch.setenv("YUI_COMPANION_OAUTH_ISSUER", "http://localhost:8080")
    with pytest.raises(ValueError):
        importlib.reload(adapter)


def test_oauth_child_credential_is_audience_scoped_and_parent_revocable(client, monkeypatch):
    c, character, _ = client
    connection = uuid4().hex
    grant = c.post("/companion/v2/grants", json={
        "connection_id": connection, "character_ids": [character],
        "operations": ["read_inbox", "post_update"]}).json()
    resource = "https://example.test/private-mcp"
    monkeypatch.setenv("YUI_COMPANION_OAUTH_RESOURCE", resource)
    store = GrantStore(CompanionStore(get_settings().database_url))
    read = store.issue_oauth_access(grant["grant_id"], resource, "yui:read")
    write = store.issue_oauth_access(grant["grant_id"], resource, "yui:write")
    store.authenticate_transport(read["access_token"])
    assert store.authorize(read["access_token"], connection, character, "read_inbox")
    assert store.authorize(write["access_token"], connection, character, "post_update")
    with pytest.raises(HTTPException):
        store.authorize(read["access_token"], connection, character, "post_update")
    with pytest.raises(HTTPException):
        store.issue_oauth_access(grant["grant_id"], "https://other.example/mcp", "yui:read")
    monkeypatch.setenv("YUI_COMPANION_OAUTH_RESOURCE", "https://other.example/mcp")
    with pytest.raises(HTTPException):
        GrantStore(CompanionStore(get_settings().database_url)).authenticate_transport(read["access_token"])
    c.delete("/companion/v2/grants/" + grant["grant_id"])
    with pytest.raises(HTTPException):
        store.authenticate_transport(write["access_token"])
