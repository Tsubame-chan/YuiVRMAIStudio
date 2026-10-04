import base64
import json
import socket
from pathlib import Path

import httpx
import pytest

pytest.importorskip("mcp")
pytest.importorskip("standardwebhooks")
from tools.dots_probe_events import EventParams, SyntheticEvents


def test_synthetic_subscription_verification_delivery_and_restart(tmp_path, monkeypatch):
    def public_address(host, port, **kwargs):
        assert host == "callback.openai.com"
        return [(socket.AF_INET, socket.SOCK_STREAM, 0, "", ("8.8.8.8", 443))]

    monkeypatch.setattr(socket, "getaddrinfo", public_address)
    seen = []

    def respond(request):
        assert request.url.host == "callback.openai.com"
        assert request.headers["webhook-signature"].startswith("v1,")
        assert request.headers["x-mcp-subscription-id"].startswith("sub_")
        body = json.loads(request.content)
        seen.append(body)
        return httpx.Response(200, json={"challenge": body["challenge"]} if body.get("type") == "verification" else {})

    client = httpx.Client(transport=httpx.MockTransport(respond))
    path = tmp_path / "events.sqlite3"
    events = SyntheticEvents(path, client)
    secret = "whsec_" + base64.b64encode(b"s" * 32).decode()
    params = EventParams.model_validate({"name": "yui.probe.changed", "arguments": {"probe_id": "probe-synthetic"},
                                         "delivery": {"mode": "webhook", "url": "https://callback.openai.com/hooks/123",
                                                      "secret": secret}})
    first = events.subscribe(params)
    assert first["id"] == events.subscribe(params)["id"]
    assert events.emit("probe-deadbeef") == 1
    assert seen[-1]["data"] == {"probe_id": "probe-synthetic", "marker": "probe-deadbeef"}
    assert SyntheticEvents(path, client).emit("probe-cafebabe") == 1
    with pytest.raises(ValueError):
        events.emit("probe-private")
    assert events.unsubscribe(params) == {}
    assert events.emit("probe-deadbeef") == 0
    assert path.stat().st_mode & 0o077 == 0


def test_synthetic_events_reject_non_openai_callback(tmp_path):
    events = SyntheticEvents(tmp_path / "events.sqlite3", httpx.Client(transport=httpx.MockTransport(lambda r: None)))
    params = EventParams.model_validate({"name": "yui.probe.changed", "arguments": {"probe_id": "probe-synthetic"},
                                         "delivery": {"mode": "webhook", "url": "https://127.0.0.1/steal",
                                                      "secret": "whsec_" + base64.b64encode(b"x" * 32).decode()}})
    with pytest.raises(ValueError):
        events.subscribe(params)
