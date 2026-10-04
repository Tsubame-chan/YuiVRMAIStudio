"""Loopback-only HTTP entry point for the synthetic MCP 2.0 probe.

This is deliberately separate from the real Yui Backend and exposes no user
data. The private Tunnel client can reach it on this host without a public
listener. It is not an authenticated production MCP endpoint.
"""
from __future__ import annotations

import os
import json

import uvicorn

from tools.dots_probe_mcp import server
from tools.dots_probe_events import SyntheticEvents, register


events = SyntheticEvents()
register(server, events)


class EventDiscoveryCompatibility:
    """Add the draft Events capability missing from MCP Python SDK 2.3.0.

    Custom methods are handled by the SDK. Its spec-result sieve removes an
    unknown `events` capability, so only the discover response is amended.
    """

    def __init__(self, inner):
        self.inner = inner

    async def __call__(self, scope, receive, send):
        if scope["type"] != "http" or scope["method"] != "POST" or scope["path"] != "/mcp":
            await self.inner(scope, receive, send)
            return
        chunks = []
        while True:
            message = await receive()
            if message["type"] != "http.request":
                return
            chunks.append(message.get("body", b""))
            if not message.get("more_body", False):
                break
        request_body = b"".join(chunks)
        try:
            discover = json.loads(request_body).get("method") == "server/discover"
        except (ValueError, AttributeError):
            discover = False
        if not discover:
            async def replay():
                nonlocal request_body
                if request_body is not None:
                    body, request_body = request_body, None
                    return {"type": "http.request", "body": body, "more_body": False}
                return await receive()
            await self.inner(scope, replay, send)
            return
        frames = []

        async def capture(frame):
            frames.append(frame)

        async def replay():
            nonlocal request_body
            if request_body is not None:
                body, request_body = request_body, None
                return {"type": "http.request", "body": body, "more_body": False}
            return await receive()

        await self.inner(scope, replay, capture)
        raw = b"".join(frame.get("body", b"") for frame in frames if frame["type"] == "http.response.body")
        try:
            payload = json.loads(raw)
            payload["result"]["capabilities"]["events"] = {}
            updated = json.dumps(payload, separators=(",", ":")).encode()
        except (ValueError, KeyError, TypeError):
            for frame in frames:
                await send(frame)
            return
        for frame in frames:
            if frame["type"] == "http.response.start":
                frame = dict(frame)
                frame["headers"] = [(key, value) for key, value in frame.get("headers", [])
                                    if key.lower() != b"content-length"] + [(b"content-length", str(len(updated)).encode())]
            elif frame["type"] == "http.response.body":
                frame = {"type": "http.response.body", "body": updated, "more_body": False}
            await send(frame)
            if frame["type"] == "http.response.body":
                break


app = EventDiscoveryCompatibility(server.streamable_http_app(stateless_http=True, host="127.0.0.1"))


if __name__ == "__main__":
    port = int(os.environ.get("YUI_DOTS_PROBE_PORT", "8765"))
    if not 1 <= port <= 65535:
        raise ValueError("Invalid probe port")
    uvicorn.run(app, host="127.0.0.1", port=port, log_level="warning")
