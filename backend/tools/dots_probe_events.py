"""Synthetic-only MCP Events compatibility seam for the HTTP tunnel probe.

The installed MCP SDK does not yet advertise/serve the draft Events methods.
This module adds them only to the isolated probe; it never opens Yui data.
"""
from __future__ import annotations

import base64
import hashlib
import ipaddress
import json
import os
import re
import secrets
import socket
import sqlite3
from datetime import datetime, timedelta, timezone
from pathlib import Path
from urllib.parse import urlsplit

import httpx
from mcp.types import RequestParams
from pydantic import BaseModel, ConfigDict, Field
from standardwebhooks import Webhook


EVENT = "yui.probe.changed"
PROBE = "probe-synthetic"
DEFAULT_DB = Path(__file__).resolve().parents[2] / "_local_tools" / "yui-probe-events.sqlite3"


class Delivery(BaseModel):
    model_config = ConfigDict(extra="forbid")
    mode: str
    url: str = Field(max_length=2048)
    secret: str | None = None


class EventParams(RequestParams):
    name: str
    arguments: dict[str, str]
    delivery: Delivery
    cursor: str | None = None
    ttl_ms: int | None = Field(default=None, alias="ttlMs", ge=60000)


def _callback_url(value: str) -> str:
    parsed = urlsplit(value)
    host = (parsed.hostname or "").lower()
    if parsed.scheme != "https" or parsed.username or parsed.password or parsed.port not in (None, 443):
        raise ValueError("Invalid callback URL")
    if not (host.endswith(".openai.com") or host.endswith(".chatgpt.com")):
        raise ValueError("Synthetic probe accepts only OpenAI callback hosts")
    if not parsed.path or parsed.fragment:
        raise ValueError("Invalid callback URL")
    for result in socket.getaddrinfo(host, 443, type=socket.SOCK_STREAM):
        if not ipaddress.ip_address(result[4][0]).is_global:
            raise ValueError("Callback address is not public")
    return value


def _secret(value: str | None) -> str:
    if not value or not value.startswith("whsec_"):
        raise ValueError("Invalid signing secret")
    raw = base64.b64decode(value[6:], validate=True)
    if not 24 <= len(raw) <= 64:
        raise ValueError("Invalid signing secret length")
    return value


def _json(value: object) -> str:
    return json.dumps(value, ensure_ascii=False, sort_keys=True, separators=(",", ":"))


class SyntheticEvents:
    def __init__(self, path: Path | None = None, client: httpx.Client | None = None):
        self.path = path or Path(os.environ.get("YUI_DOTS_PROBE_EVENTS_DB", DEFAULT_DB))
        self.path.parent.mkdir(parents=True, exist_ok=True)
        self.client = client or httpx.Client(timeout=10, follow_redirects=False, trust_env=False)
        with self._connect() as db:
            db.execute("""CREATE TABLE IF NOT EXISTS subscriptions (
                id TEXT PRIMARY KEY, url TEXT NOT NULL, secret TEXT NOT NULL,
                expires_at TEXT NOT NULL, verified INTEGER NOT NULL)""")

    def _connect(self):
        connection = sqlite3.connect(self.path)
        os.chmod(self.path, 0o600)
        return connection

    def list(self):
        return {"events": [{"name": EVENT,
            "description": "A synthetic Yui connectivity marker changed; no user data is available.",
            "delivery": ["webhook"],
            "inputSchema": {"type": "object", "properties": {"probe_id": {"type": "string", "enum": [PROBE]}},
                            "required": ["probe_id"], "additionalProperties": False},
            "payloadSchema": {"type": "object", "properties": {
                "probe_id": {"type": "string"}, "marker": {"type": "string"}},
                "required": ["probe_id", "marker"], "additionalProperties": False}}]}

    @staticmethod
    def _validate(params: EventParams, require_secret: bool):
        if params.name != EVENT or params.arguments != {"probe_id": PROBE} or params.delivery.mode != "webhook":
            raise ValueError("Unsupported synthetic event or filter")
        url = _callback_url(params.delivery.url)
        secret = _secret(params.delivery.secret) if require_secret else None
        return url, secret

    @staticmethod
    def _id(url: str) -> str:
        return "sub_" + hashlib.sha256(f"synthetic\0{EVENT}\0{PROBE}\0{url}".encode()).hexdigest()[:32]

    def _post(self, url: str, secret: str, subscription_id: str, body: dict, message_id: str):
        serialized = _json(body)
        now = datetime.now(timezone.utc)
        signature = Webhook(secret).sign(message_id, now, serialized)
        response = self.client.post(url, content=serialized.encode(), headers={
            "Content-Type": "application/json", "webhook-id": message_id,
            "webhook-timestamp": str(int(now.timestamp())), "webhook-signature": signature,
            "X-MCP-Subscription-Id": subscription_id})
        if response.status_code // 100 != 2:
            raise ValueError("Callback rejected synthetic event")
        return response

    def subscribe(self, params: EventParams):
        url, secret = self._validate(params, True)
        sub_id = self._id(url)
        challenge = secrets.token_urlsafe(24)
        response = self._post(url, secret, sub_id, {"type": "verification", "challenge": challenge},
                              "msg_verification_" + secrets.token_hex(16))
        try:
            echoed = response.json()["challenge"]
        except (ValueError, KeyError, TypeError) as error:
            raise ValueError("Callback challenge was not echoed") from error
        if not secrets.compare_digest(str(echoed), challenge):
            raise ValueError("Callback challenge mismatch")
        lifetime = min(params.ttl_ms or 24 * 60 * 60 * 1000, 24 * 60 * 60 * 1000)
        expires = datetime.now(timezone.utc) + timedelta(milliseconds=lifetime)
        with self._connect() as db:
            db.execute("INSERT INTO subscriptions VALUES(?,?,?,?,1) ON CONFLICT(id) DO UPDATE SET "
                       "secret=excluded.secret,expires_at=excluded.expires_at,verified=1",
                       (sub_id, url, secret, expires.isoformat()))
        return {"id": sub_id, "refreshBefore": expires.isoformat().replace("+00:00", "Z"),
                "cursor": None, "truncated": False}

    def unsubscribe(self, params: EventParams):
        url, _ = self._validate(params, False)
        with self._connect() as db:
            db.execute("DELETE FROM subscriptions WHERE id=?", (self._id(url),))
        return {}

    def emit(self, marker: str):
        if not re.fullmatch(r"probe-[0-9a-f]{8}", marker):
            raise ValueError("Only synthetic markers may be emitted")
        event_id = "evt_" + secrets.token_hex(16)
        body = {"eventId": event_id, "name": EVENT,
                "timestamp": datetime.now(timezone.utc).isoformat().replace("+00:00", "Z"),
                "data": {"probe_id": PROBE, "marker": marker}, "cursor": None}
        with self._connect() as db:
            subscriptions = db.execute("SELECT * FROM subscriptions WHERE verified=1 AND expires_at>?",
                                       (datetime.now(timezone.utc).isoformat(),)).fetchall()
        delivered = 0
        for sub_id, url, secret, _, _ in subscriptions:
            _callback_url(url)
            self._post(url, secret, sub_id, body, event_id)
            delivered += 1
        return delivered


def register(server, events: SyntheticEvents):
    async def listing(ctx, params):
        return events.list()

    async def subscribing(ctx, params):
        return events.subscribe(params)

    async def unsubscribing(ctx, params):
        return events.unsubscribe(params)

    lowlevel = server._lowlevel_server
    lowlevel.add_request_handler("events/list", RequestParams, listing)
    lowlevel.add_request_handler("events/subscribe", EventParams, subscribing)
    lowlevel.add_request_handler("events/unsubscribe", EventParams, unsubscribing)
