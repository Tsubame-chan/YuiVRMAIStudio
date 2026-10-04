import base64
import hashlib
import importlib
from urllib.parse import parse_qs, urlsplit
from uuid import uuid4

from fastapi.testclient import TestClient

from app.core.companion_grants import GrantStore
from app.core.companion_oauth import verify_chatgpt_client_metadata
from app.core.companion_v2 import CompanionStore
from app.core.config import get_settings
from test_companion_v2 import client
from test_companion_activity import setup_work


ISSUER = "https://auth.example.test"
RESOURCE = "https://mcp.example.test/mcp"
CLIENT = "https://chatgpt.com/oauth/client.json"
REDIRECT = "https://chatgpt.com/connector_platform_oauth_redirect"
VERIFIER = "v" * 64
CHALLENGE = base64.urlsafe_b64encode(hashlib.sha256(VERIFIER.encode()).digest()).rstrip(b"=").decode()


def test_owner_approved_oauth_pkce_exchange_and_parent_revocation(client, monkeypatch):
    owner, character, _ = client
    connection, conversation = setup_work(owner, character)
    activity = uuid4().hex
    assert owner.post(f"/companion/v2/characters/{character}/activities", json={
        "op_id": uuid4().hex, "activity_id": activity,
        "conversation_id": conversation, "request": "SYNTHETIC_OAUTH_WORK"}).status_code == 200
    grant = owner.post("/companion/v2/grants", json={
        "connection_id": connection, "character_ids": [character],
        "operations": ["read_inbox", "post_update"]}).json()
    monkeypatch.setenv("YUI_COMPANION_OAUTH_RESOURCE", RESOURCE)
    monkeypatch.setenv("YUI_COMPANION_OAUTH_ISSUER", ISSUER)
    monkeypatch.setenv("YUI_COMPANION_OAUTH_METADATA_URL",
                       ISSUER + "/.well-known/oauth-protected-resource")
    monkeypatch.setenv("YUI_COMPANION_OAUTH_TESTING_ENABLED", "true")
    monkeypatch.setattr("app.core.companion_oauth.verify_chatgpt_client_metadata", lambda: None)
    approval = owner.post(f"/companion/v2/grants/{grant['grant_id']}/oauth-approval")
    assert approval.status_code == 200, approval.text
    assert approval.json()["approval_secret"].startswith("yui_oa_")
    adapter = importlib.reload(importlib.import_module("tools.companion_oauth_http"))
    values = {"client_id": CLIENT, "redirect_uri": REDIRECT, "resource": RESOURCE,
              "scope": "yui:read yui:write", "response_type": "code", "code_challenge": CHALLENGE,
              "code_challenge_method": "S256", "state": "test-state"}
    with TestClient(adapter.app, base_url=ISSUER) as auth:
        metadata = auth.get("/.well-known/oauth-authorization-server").json()
        assert metadata["issuer"] == ISSUER
        assert metadata["code_challenge_methods_supported"] == ["S256"]
        assert "refresh_token" in metadata["grant_types_supported"]
        assert auth.get("/.well-known/oauth-protected-resource").json()["resource"] == RESOURCE
        form = auth.get("/authorize", params=values)
        assert form.status_code == 200
        assert form.headers["referrer-policy"] == "same-origin"
        assert "form-action 'self' https://chatgpt.com" in form.headers["content-security-policy"]
        assert "yui_oa_" not in form.text
        assert auth.get("/authorize", params={**values, "state": ""}).status_code == 400
        assert auth.get("/authorize", params={**values, "state": "x" * 513}).status_code == 400
        assert auth.get("/authorize", params=list(values.items()) +
                        [("state", "second")]).status_code == 400
        assert auth.get("/authorize", params={**values, "redirect_uri": "https://evil.test/"}).status_code == 400
        denied = auth.post("/authorize", data={**values,
            "approval_secret": approval.json()["approval_secret"]}, follow_redirects=False)
        assert denied.status_code == 403  # Browser origin is mandatory.
        accepted = auth.post("/authorize", data={**values,
            "approval_secret": approval.json()["approval_secret"]},
            headers={"Origin": ISSUER}, follow_redirects=False)
        assert accepted.status_code == 302
        query = parse_qs(urlsplit(accepted.headers["location"]).query)
        assert query["state"] == ["test-state"] and query["iss"] == [ISSUER]
        code = query["code"][0]
        assert code.startswith("yui_ac_")
        repeat = auth.post("/authorize", data={**values,
            "approval_secret": approval.json()["approval_secret"]},
            headers={"Origin": ISSUER}, follow_redirects=False)
        assert parse_qs(urlsplit(repeat.headers["location"]).query)["error"] == ["access_denied"]
        token_request = {"grant_type": "authorization_code", "code": code,
                         "client_id": CLIENT, "redirect_uri": REDIRECT,
                         "resource": RESOURCE, "code_verifier": VERIFIER}
        assert auth.post("/token", data={**token_request, "code_verifier": "z" * 64}).status_code == 400
        token = auth.post("/token", data=token_request)
        assert token.status_code == 200, token.text
        access = token.json()["access_token"]
        first_refresh = token.json()["refresh_token"]
        assert token.json()["scope"] == "yui:read yui:write"
        assert auth.post("/token", data=token_request).status_code == 400
        refresh_request = {"grant_type": "refresh_token", "refresh_token": first_refresh,
                           "client_id": CLIENT, "resource": RESOURCE}
        rotated = auth.post("/token", data=refresh_request)
        assert rotated.status_code == 200, rotated.text
        assert rotated.json()["refresh_token"] != first_refresh
        assert auth.post("/token", data=refresh_request).status_code == 400
        assert auth.post("/token", data={**refresh_request,
            "refresh_token": rotated.json()["refresh_token"],
            "resource": "https://evil.test/mcp"}).status_code == 400
    store = GrantStore(CompanionStore(get_settings().database_url))
    assert store.authorize(access, connection, character, "read_inbox")
    monkeypatch.setenv("YUI_COMPANION_MCP_TESTING_ENABLED", "true")
    monkeypatch.setenv("YUI_COMPANION_EVENTS_TESTING_ENABLED", "true")
    mcp_adapter = importlib.reload(importlib.import_module("tools.companion_mcp_http"))
    with TestClient(mcp_adapter.app, base_url="http://127.0.0.1:8767") as mcp:
        envelope = {"io.modelcontextprotocol/protocolVersion": "2026-07-28",
                    "io.modelcontextprotocol/clientCapabilities": {}}
        anonymous_list = mcp.post("/mcp", json={"jsonrpc": "2.0", "id": 9,
            "method": "tools/list", "params": {"_meta": envelope}}, headers={
                "Accept": "application/json, text/event-stream",
                "MCP-Protocol-Version": "2026-07-28"})
        assert anonymous_list.status_code == 200
        assert all("securitySchemes" in tool for tool in
                   anonymous_list.json()["result"]["tools"])
        public_headers = {"Accept": "application/json, text/event-stream",
                          "MCP-Protocol-Version": "2026-07-28",
                          "Host": "mcp.example.test", "Origin": "https://mcp.example.test"}
        public_list = mcp.post("/mcp", json={"jsonrpc": "2.0", "id": 91,
            "method": "tools/list", "params": {"_meta": envelope}}, headers=public_headers)
        assert public_list.status_code == 200, public_list.text
        hostile_host = mcp.post("/mcp", json={"jsonrpc": "2.0", "id": 92,
            "method": "tools/list", "params": {"_meta": envelope}},
            headers={**public_headers, "Host": "other.example.test"})
        assert hostile_host.status_code == 421
        link = mcp.post("/mcp", json={"jsonrpc": "2.0", "id": 10,
            "method": "tools/call", "params": {"_meta": envelope,
                "name": "yui_read_inbox", "arguments": {"connection_id": connection,
                    "character_id": character}}}, headers={
                "Accept": "application/json, text/event-stream",
                "MCP-Protocol-Version": "2026-07-28"})
        assert link.status_code == 200
        assert link.json()["result"]["isError"] is True
        assert "mcp/www_authenticate" in link.json()["result"]["_meta"]
        assert "SYNTHETIC_OAUTH_WORK" not in link.text
        discovered = mcp.post("/mcp", json={"jsonrpc": "2.0", "id": 3,
            "method": "server/discover", "params": {"_meta": envelope}}, headers={
                "Accept": "application/json, text/event-stream",
                "MCP-Protocol-Version": "2026-07-28", "mcp-method": "server/discover",
                "Authorization": "Bearer " + access})
        assert discovered.status_code == 200, discovered.text
        assert "events" in discovered.json()["result"]["capabilities"]
        listed_events = mcp.post("/mcp", json={"jsonrpc": "2.0", "id": 4,
            "method": "events/list", "params": {"_meta": envelope}}, headers={
                "Accept": "application/json, text/event-stream",
                "MCP-Protocol-Version": "2026-07-28", "mcp-method": "events/list",
                "Authorization": "Bearer " + access})
        assert "yui.work.requested" in listed_events.text
        metadata_path = "/.well-known/oauth-protected-resource/mcp"
        protected = mcp.get(metadata_path)
        assert protected.status_code == 200
        assert protected.json()["resource"] == RESOURCE
        unauthorized = mcp.post("/mcp", json={}, headers={"Accept": "application/json"})
        assert unauthorized.status_code == 401
        assert ('resource_metadata="' + ISSUER +
                '/.well-known/oauth-protected-resource"') in (
            unauthorized.headers["www-authenticate"])
        listed = mcp.post("/mcp", json={"jsonrpc": "2.0", "id": 2,
            "method": "tools/list", "params": {"_meta": {
                "io.modelcontextprotocol/protocolVersion": "2026-07-28",
                "io.modelcontextprotocol/clientCapabilities": {}}}}, headers={
                "Accept": "application/json, text/event-stream",
                "MCP-Protocol-Version": "2026-07-28", "mcp-method": "tools/list",
                "Authorization": "Bearer " + access})
        assert listed.status_code == 200, listed.text
        schemes = {tool["name"]: tool["securitySchemes"][0]["scopes"]
                   for tool in listed.json()["result"]["tools"]}
        assert schemes["yui_read_inbox"] == ["yui:read"]
        assert schemes["yui_post_update"] == ["yui:write"]
        assert "yui_read_context" not in schemes
        payload = {"jsonrpc": "2.0", "id": 1, "method": "tools/call", "params": {
            "_meta": {"io.modelcontextprotocol/protocolVersion": "2026-07-28",
                      "io.modelcontextprotocol/clientCapabilities": {}},
            "name": "yui_read_inbox", "arguments": {"connection_id": connection,
                                                 "character_id": character}}}
        headers = {"Accept": "application/json, text/event-stream",
                   "MCP-Protocol-Version": "2026-07-28", "mcp-method": "tools/call",
                   "mcp-name": "yui_read_inbox", "Authorization": "Bearer " + access}
        assert "SYNTHETIC_OAUTH_WORK" in mcp.post("/mcp", json=payload, headers=headers).text
        headers["Authorization"] = "Bearer " + grant["token"]
        static_denied = mcp.post("/mcp", json=payload, headers=headers)
        assert static_denied.status_code == 200
        assert static_denied.json()["result"]["isError"] is True
        assert "SYNTHETIC_OAUTH_WORK" not in static_denied.text
    owner.delete("/companion/v2/grants/" + grant["grant_id"])
    with TestClient(adapter.app, base_url=ISSUER) as auth:
        assert auth.post("/token", data={**refresh_request,
            "refresh_token": rotated.json()["refresh_token"]}).status_code == 400
    try:
        store.authenticate_transport(access)
        assert False, "revoked parent grant accepted"
    except Exception as error:
        assert getattr(error, "status_code", None) == 401


def test_chatgpt_cimd_must_publish_pinned_redirect_and_public_pkce_method(monkeypatch):
    class Response:
        def raise_for_status(self):
            pass
        def json(self):
            return {"client_id": CLIENT, "redirect_uris": [REDIRECT],
                    "token_endpoint_auth_methods_supported": ["none", "private_key_jwt"],
                    "response_types": ["code"]}
    monkeypatch.setattr("app.core.companion_oauth.httpx.get", lambda *args, **kwargs: Response())
    verify_chatgpt_client_metadata()
    class Wrong(Response):
        def json(self):
            return {**super().json(), "redirect_uris": ["https://evil.test/callback"]}
    monkeypatch.setattr("app.core.companion_oauth.httpx.get", lambda *args, **kwargs: Wrong())
    try:
        verify_chatgpt_client_metadata()
        assert False, "invalid redirect metadata accepted"
    except ValueError:
        pass
