"""Opt-in loopback MCP adapter for an owner-issued Companion grant.

Transport credentials are HTTP Bearer headers and never tool arguments. This
is a private trial adapter; public distribution still requires OAuth and
device/connection acceptance. Disabled unless both testing flags are set.
"""
from __future__ import annotations

import contextvars
import json
import logging
import os
from urllib.parse import urlsplit
from uuid import UUID

import uvicorn
from fastapi import HTTPException
from mcp.server import MCPServer
from mcp.server.transport_security import TransportSecuritySettings
from mcp.types import ToolAnnotations
from pydantic import BaseModel

from app.core.companion_activity import ActivityReport
from app.core.companion_context import ContextRequest
from app.core.companion_grants import ExternalActivityService
from app.core.companion_v2 import CompanionStore
from tools.companion_events import CompanionEvents, register as register_events
from app.core.config import get_settings


if os.environ.get("YUI_COMPANION_MCP_TESTING_ENABLED") != "true" or not get_settings().companion_v2_testing_enabled:
    raise RuntimeError("Companion MCP adapter requires explicit testing flags")

service = ExternalActivityService(CompanionStore(get_settings().database_url))
logger = logging.getLogger(__name__)
request_token = contextvars.ContextVar("companion_mcp_bearer", default=None)


server = MCPServer("Yui Companion private trial", instructions=(
    "Read only the granted channel/character. Reports are external statements, "
    "not verified user speech or proof of completed external actions."))


class InboxResult(BaseModel):
    items: list[dict[str, object]]


class AcknowledgeResult(BaseModel):
    state: str


class UpdateResult(BaseModel):
    applied: bool
    state: str
    completion_basis: str | None
    revision: int


class ContextResult(BaseModel):
    conversation_id: str
    purpose: str
    realm: str
    head_seq: int
    privacy_epoch: int
    items: list[dict[str, object]]
    truncated: bool


def _id(value: str) -> str:
    try:
        if UUID(value).hex != value:
            raise ValueError
    except (TypeError, ValueError):
        raise ValueError("Expected lowercase 32-character ID") from None
    return value


def _token() -> str:
    token = request_token.get()
    if token is None:
        raise HTTPException(401, {"code": "missing_companion_grant"})
    return token


@server.tool(annotations=ToolAnnotations(readOnlyHint=True, openWorldHint=False), structured_output=True)
async def yui_read_inbox(connection_id: str, character_id: str, limit: int = 20) -> InboxResult:
    """Read pending requests for one granted connection and character."""
    if not 1 <= limit <= 20:
        raise ValueError("limit must be 1 to 20")
    items = service.read_inbox(_token(), _id(connection_id), _id(character_id), limit)
    return InboxResult(items=items)


@server.tool(annotations=ToolAnnotations(readOnlyHint=True, openWorldHint=False), structured_output=True)
async def yui_read_context(connection_id: str, character_id: str, conversation_id: str,
                           purpose: str, query: str, realm: str = "real",
                           budget_chars: int = 4000) -> ContextResult:
    """Read a bounded, source-checked context for a granted conversation."""
    request = ContextRequest(conversation_id=_id(conversation_id), purpose=purpose,
                             query=query, realm=realm, budget_chars=budget_chars)
    return ContextResult.model_validate(service.read_context(
        _token(), _id(connection_id), _id(character_id), request))


@server.tool(annotations=ToolAnnotations(readOnlyHint=False, openWorldHint=False), structured_output=True)
async def yui_acknowledge(connection_id: str, character_id: str, notification_id: str) -> AcknowledgeResult:
    """Acknowledge receipt of a request; this does not mark work complete."""
    return AcknowledgeResult.model_validate(
        service.acknowledge(_token(), _id(connection_id), _id(character_id), _id(notification_id)))


@server.tool(annotations=ToolAnnotations(readOnlyHint=False, openWorldHint=False), structured_output=True)
async def yui_post_update(connection_id: str, character_id: str, activity_id: str,
                          op_id: str, request_revision: int, expected_revision: int,
                          kind: str, text: str, spoken_text: str | None = None) -> UpdateResult:
    """Save an external progress, question, result, or cancellation report."""
    report = ActivityReport(op_id=_id(op_id), request_revision=request_revision,
                            expected_revision=expected_revision, kind=kind, text=text,
                            spoken_text=spoken_text)
    return UpdateResult.model_validate(service.post_update(
        _token(), _id(connection_id), _id(character_id), _id(activity_id), report))


if os.environ.get("YUI_COMPANION_EVENTS_TESTING_ENABLED") == "true":
    events = CompanionEvents(service.activities.companion, lambda connection, character:
                             service.grants.authorize(_token(), _id(connection),
                                                      _id(character), "read_inbox"))
    register_events(server, events)


class BearerGate:
    def __init__(self, inner):
        self.inner = inner
        resource = os.environ.get("YUI_COMPANION_OAUTH_RESOURCE", "").rstrip("/")
        issuer = os.environ.get("YUI_COMPANION_OAUTH_ISSUER", "").rstrip("/")
        resource_path = urlsplit(resource).path if resource else ""
        resource_origin = (resource[:-(len(resource_path))] if resource_path else resource)
        metadata_url = os.environ.get("YUI_COMPANION_OAUTH_METADATA_URL", "") or (
            resource_origin + "/.well-known/oauth-protected-resource" + resource_path
            if resource else "")
        if bool(resource) != bool(issuer):
            raise ValueError("OAuth resource and issuer must be configured together")
        for value in (resource, issuer, metadata_url):
            if value:
                parsed = urlsplit(value)
                if (not value.isascii() or parsed.scheme != "https" or not parsed.hostname or parsed.username or
                        parsed.password or parsed.query or parsed.fragment):
                    raise ValueError("OAuth endpoints require canonical HTTPS URLs")
        self.resource = resource
        self.issuer = issuer
        self.metadata_url = metadata_url
        self.metadata_path = ("/.well-known/oauth-protected-resource" + resource_path
                              if resource else "/.well-known/oauth-protected-resource")

    async def _respond(self, send, status: int, body: dict, headers=()):
        payload = json.dumps(body, separators=(",", ":")).encode("utf-8")
        await send({"type": "http.response.start", "status": status,
                    "headers": [(b"content-type", b"application/json"),
                                (b"content-length", str(len(payload)).encode())] + list(headers)})
        await send({"type": "http.response.body", "body": payload})

    async def _auth_result(self, receive, send, challenge: str):
        chunks = []
        total = 0
        while True:
            message = await receive()
            if message["type"] != "http.request":
                return False
            chunk = message.get("body", b"")
            total += len(chunk)
            if total > 65536:
                return False
            chunks.append(chunk)
            if not message.get("more_body", False):
                break
        try:
            request = json.loads(b"".join(chunks))
            if request.get("method") != "tools/call" or not isinstance(request.get("id"), (str, int)):
                return False
        except (ValueError, AttributeError):
            return False
        await self._respond(send, 200, {"jsonrpc": "2.0", "id": request["id"],
            "result": {"content": [{"type": "text", "text": "Yui connection is required."}],
                       "_meta": {"mcp/www_authenticate": [challenge]}, "isError": True}})
        return True

    async def __call__(self, scope, receive, send):
        if scope["type"] != "http":
            await self.inner(scope, receive, send)
            return
        if (scope["method"] == "GET" and scope["path"] == self.metadata_path):
            if not self.resource:
                await self._respond(send, 404, {"error": "not_configured"})
            else:
                await self._respond(send, 200, {"resource": self.resource,
                                               "authorization_servers": [self.issuer],
                                               "scopes_supported": ["yui:read", "yui:write"]})
            return
        method = next((value for key, value in scope.get("headers", [])
                       if key.lower() == b"mcp-method"), b"")
        if not method and scope["method"] == "POST" and scope["path"] == "/mcp":
            chunks = []
            size = 0
            while True:
                frame = await receive()
                if frame["type"] != "http.request":
                    return
                chunk = frame.get("body", b"")
                size += len(chunk)
                if size > 1048576:
                    await self._respond(send, 413, {"error": "request_too_large"})
                    return
                chunks.append(chunk)
                if not frame.get("more_body", False):
                    break
            request_body = b"".join(chunks)
            try:
                inferred = json.loads(request_body).get("method", "")
                method = inferred.encode("ascii") if isinstance(inferred, str) else b""
            except (ValueError, AttributeError, UnicodeEncodeError):
                method = b""
            if method:
                scope = {**scope, "headers": list(scope.get("headers", [])) +
                         [(b"mcp-method", method)]}
            original_receive = receive
            async def replay():
                nonlocal request_body
                if request_body is not None:
                    body, request_body = request_body, None
                    return {"type": "http.request", "body": body, "more_body": False}
                return await original_receive()
            receive = replay
        values = [value for key, value in scope.get("headers", []) if key.lower() == b"authorization"]
        token = None
        if len(values) == 1 and values[0].startswith(b"Bearer "):
            try:
                token = values[0][7:].decode("ascii")
                if self.resource and not token.startswith("yui_at_"):
                    raise HTTPException(401, {"code": "oauth_required"})
                service.grants.authenticate_transport(token)
            except (UnicodeDecodeError, HTTPException):
                token = None
        if token is None:
            challenge = b"Bearer"
            if self.resource:
                challenge = ('Bearer resource_metadata="' + self.metadata_url +
                    '", error="insufficient_scope", error_description="Connect Yui to continue"').encode("ascii")
                if method == b"tools/call" and await self._auth_result(
                        receive, send, challenge.decode("ascii")):
                    return
                if method not in {b"tools/list", b"server/discover", b"events/list"}:
                    await self._respond(send, 401, {"error": "unauthorized"},
                                        [(b"www-authenticate", challenge)])
                    return
            else:
                await self._respond(send, 401, {"error": "unauthorized"},
                                    [(b"www-authenticate", challenge)])
                return
        reset = request_token.set(token)
        try:
            rewrite = self.resource and method in {b"tools/list", b"server/discover"}
            if not rewrite:
                await self.inner(scope, receive, send)
            else:
                start = None
                body = bytearray()
                async def send_list(message):
                    nonlocal start
                    if message["type"] == "http.response.start":
                        start = message
                    elif message["type"] == "http.response.body":
                        body.extend(message.get("body", b""))
                        if not message.get("more_body", False):
                            payload = bytes(body)
                            if start["status"] == 200 and b"application/json" in dict(
                                    start.get("headers", [])).get(b"content-type", b""):
                                document = json.loads(payload)
                                if method == b"tools/list":
                                    operations = (service.grants.allowed_operations(token) if token else
                                        {"read_inbox", "read_context", "acknowledge", "post_update"})
                                    mapping = {"yui_read_inbox": "read_inbox",
                                               "yui_read_context": "read_context",
                                               "yui_acknowledge": "acknowledge",
                                               "yui_post_update": "post_update"}
                                    tools = document.get("result", {}).get("tools", [])
                                    document["result"]["tools"] = [tool for tool in tools
                                        if mapping.get(tool.get("name")) in operations]
                                    for tool in document["result"]["tools"]:
                                        tool["securitySchemes"] = [{"type": "oauth2", "scopes": [
                                            "yui:read" if tool.get("name") in
                                            {"yui_read_inbox", "yui_read_context"} else "yui:write"]}]
                                elif os.environ.get("YUI_COMPANION_EVENTS_TESTING_ENABLED") == "true":
                                    document.get("result", {}).get("capabilities", {})["events"] = {}
                                payload = json.dumps(document, separators=(",", ":"), ensure_ascii=False).encode()
                            headers = [(key, value) for key, value in start.get("headers", [])
                                       if key.lower() != b"content-length"]
                            headers.append((b"content-length", str(len(payload)).encode()))
                            await send({**start, "headers": headers})
                            await send({"type": "http.response.body", "body": payload})
                await self.inner(scope, receive, send_list)
        finally:
            request_token.reset(reset)


_resource = os.environ.get("YUI_COMPANION_OAUTH_RESOURCE", "").rstrip("/")
_public_host = urlsplit(_resource).netloc if _resource else ""
_public_origin = (urlsplit(_resource).scheme + "://" + _public_host) if _public_host else ""
_security = TransportSecuritySettings(
    enable_dns_rebinding_protection=True,
    allowed_hosts=["127.0.0.1:*", "localhost:*", "[::1]:*"] +
                  ([_public_host] if _public_host else []),
    allowed_origins=["http://127.0.0.1:*", "http://localhost:*", "http://[::1]:*"] +
                    ([_public_origin] if _public_origin else []),
)
app = BearerGate(server.streamable_http_app(
    stateless_http=True, host="127.0.0.1", transport_security=_security))


if __name__ == "__main__":
    port = int(os.environ.get("YUI_COMPANION_MCP_PORT", "8767"))
    if not 1 <= port <= 65535:
        raise ValueError("Invalid MCP port")
    uvicorn.run(app, host="127.0.0.1", port=port, log_level="warning")
