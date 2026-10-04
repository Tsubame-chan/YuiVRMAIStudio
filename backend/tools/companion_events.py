"""Authenticated, opt-in MCP Events for pending Companion Work notifications.

Events contain IDs only. The granted client must read the current inbox and
acknowledge it separately; callback acceptance never completes an Activity.
"""
from __future__ import annotations

import base64
import hashlib
import ipaddress
import json
import secrets
import socket
import time
from datetime import datetime, timezone
from urllib.parse import urlsplit

import httpx
from mcp.types import RequestParams
from pydantic import BaseModel, ConfigDict, Field
from standardwebhooks import Webhook

from app.core.companion_v2 import CompanionStore


EVENT = "yui.work.requested"


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
        raise ValueError("Only OpenAI callback hosts are supported")
    if not parsed.path or parsed.fragment:
        raise ValueError("Invalid callback URL")
    for result in socket.getaddrinfo(host, 443, type=socket.SOCK_STREAM):
        if not ipaddress.ip_address(result[4][0]).is_global:
            raise ValueError("Callback address is not public")
    return value


def _secret(value: str | None) -> str:
    if not value or not value.startswith("whsec_"):
        raise ValueError("Invalid callback secret")
    raw = base64.b64decode(value[6:], validate=True)
    if not 24 <= len(raw) <= 64:
        raise ValueError("Invalid callback secret length")
    return value


class CompanionEvents:
    def __init__(self, companion: CompanionStore, authorize, client: httpx.Client | None = None):
        self.companion = companion
        self.authorize = authorize
        self.client = client or httpx.Client(timeout=10, follow_redirects=False, trust_env=False)
        with companion.connect() as db:
            db.execute("""CREATE TABLE IF NOT EXISTS companion_v2_event_subscriptions (
                id TEXT PRIMARY KEY, grant_id TEXT NOT NULL, connection_id TEXT NOT NULL,
                character_id TEXT NOT NULL, url TEXT NOT NULL, secret TEXT NOT NULL,
                expires_at REAL NOT NULL,
                FOREIGN KEY(grant_id) REFERENCES companion_v2_grants(id) ON DELETE CASCADE)""")
            db.execute("""CREATE TABLE IF NOT EXISTS companion_v2_event_deliveries (
                subscription_id TEXT NOT NULL, notification_id TEXT NOT NULL,
                delivered_at REAL NOT NULL,
                PRIMARY KEY(subscription_id,notification_id))""")
            db.execute("CREATE INDEX IF NOT EXISTS companion_v2_event_deliveries_time "
                       "ON companion_v2_event_deliveries(delivered_at)")

    def list(self):
        return {"events": [{"name": EVENT,
            "description": "A new Work request is available in the granted Yui inbox.",
            "delivery": ["webhook"],
            "inputSchema": {"type": "object", "properties": {
                "connection_id": {"type": "string"}, "character_id": {"type": "string"}},
                "required": ["connection_id", "character_id"], "additionalProperties": False},
            "payloadSchema": {"type": "object", "properties": {
                "connection_id": {"type": "string"}, "character_id": {"type": "string"},
                "activity_id": {"type": "string"}, "notification_id": {"type": "string"},
                "kind": {"type": "string"}},
                "required": ["connection_id", "character_id", "activity_id", "notification_id", "kind"],
                "additionalProperties": False}}]}

    def _scope(self, params: EventParams):
        args = params.arguments
        if (params.name != EVENT or set(args) != {"connection_id", "character_id"} or
                params.delivery.mode != "webhook"):
            raise ValueError("Unsupported event filter")
        connection, character = args["connection_id"], args["character_id"]
        principal = self.authorize(connection, character)
        return principal.removeprefix("grant:"), connection, character

    @staticmethod
    def _id(grant_id: str, connection: str, character: str, url: str) -> str:
        return "sub_" + hashlib.sha256(
            f"{grant_id}\0{EVENT}\0{connection}\0{character}\0{url}".encode()).hexdigest()[:32]

    def _post(self, url: str, secret: str, sub_id: str, body: dict, message_id: str):
        serialized = json.dumps(body, ensure_ascii=False, sort_keys=True, separators=(",", ":"))
        now = datetime.now(timezone.utc)
        signature = Webhook(secret).sign(message_id, now, serialized)
        response = self.client.post(url, content=serialized.encode(), headers={
            "Content-Type": "application/json", "webhook-id": message_id,
            "webhook-timestamp": str(int(now.timestamp())), "webhook-signature": signature,
            "X-MCP-Subscription-Id": sub_id})
        if response.status_code // 100 != 2:
            raise ValueError("Event callback rejected")
        return response

    def subscribe(self, params: EventParams):
        grant_id, connection, character = self._scope(params)
        url = _callback_url(params.delivery.url)
        secret = _secret(params.delivery.secret)
        sub_id = self._id(grant_id, connection, character, url)
        challenge = secrets.token_urlsafe(24)
        response = self._post(url, secret, sub_id, {"type": "verification", "challenge": challenge},
                              "msg_verification_" + secrets.token_hex(16))
        try:
            echoed = response.json()["challenge"]
        except (ValueError, KeyError, TypeError) as error:
            raise ValueError("Callback verification failed") from error
        if not secrets.compare_digest(str(echoed), challenge):
            raise ValueError("Callback verification mismatch")
        expires = min(time.time() + min((params.ttl_ms or 86400000) / 1000, 86400),
                      self._grant_expiry(grant_id))
        with self.companion.connect() as db:
            db.execute("INSERT INTO companion_v2_event_subscriptions VALUES(?,?,?,?,?,?,?) "
                       "ON CONFLICT(id) DO UPDATE SET secret=excluded.secret,expires_at=excluded.expires_at",
                       (sub_id, grant_id, connection, character, url, secret, expires))
        return {"id": sub_id, "refreshBefore": datetime.fromtimestamp(
            expires, timezone.utc).isoformat().replace("+00:00", "Z"),
            "cursor": None, "truncated": False}

    def _grant_expiry(self, grant_id: str) -> float:
        with self.companion.connect() as db:
            row = db.execute("SELECT expires_at FROM companion_v2_grants WHERE id=? AND revoked_at IS NULL",
                             (grant_id,)).fetchone()
            if row is None:
                raise ValueError("Grant was revoked")
            return row["expires_at"]

    def unsubscribe(self, params: EventParams):
        grant_id, connection, character = self._scope(params)
        url = _callback_url(params.delivery.url)
        with self.companion.connect() as db:
            db.execute("DELETE FROM companion_v2_event_subscriptions WHERE id=? AND grant_id=?",
                       (self._id(grant_id, connection, character, url), grant_id))
        return {}

    def deliver_once(self, limit: int = 20) -> int:
        now = time.time()
        with self.companion.connect() as db:
            expired = [row[0] for row in db.execute(
                "SELECT id FROM companion_v2_event_subscriptions WHERE expires_at<=?", (now,))]
            if expired:
                db.executemany("DELETE FROM companion_v2_event_deliveries WHERE subscription_id=?",
                               ((item,) for item in expired))
                db.executemany("DELETE FROM companion_v2_event_subscriptions WHERE id=?",
                               ((item,) for item in expired))
            db.execute("DELETE FROM companion_v2_event_deliveries WHERE delivered_at<?",
                       (now - 30 * 86400,))
            rows = db.execute("""SELECT s.id,s.url,s.secret,s.connection_id,s.character_id,
                o.id AS notification_id,o.activity_id,o.kind FROM companion_v2_event_subscriptions s
                JOIN companion_v2_grants g ON g.id=s.grant_id
                JOIN companion_v2_activity_outbox o ON o.connection_id=s.connection_id
                JOIN companion_v2_activities a ON a.id=o.activity_id AND a.character_id=s.character_id
                WHERE s.expires_at>? AND g.revoked_at IS NULL AND g.expires_at>?
                  AND o.state='pending' AND o.kind IN ('request','answer')
                  AND o.request_revision=a.request_revision
                  AND NOT EXISTS (SELECT 1 FROM companion_v2_event_deliveries d
                    WHERE d.subscription_id=s.id AND d.notification_id=o.id)
                ORDER BY o.created_at,o.id LIMIT ?""", (now, now, limit)).fetchall()
        delivered = 0
        for row in rows:
            try:
                _callback_url(row["url"])
                event_id = "evt_" + hashlib.sha256(
                    (row["id"] + ":" + row["notification_id"]).encode()).hexdigest()[:32]
                body = {"eventId": event_id, "name": EVENT,
                        "timestamp": datetime.now(timezone.utc).isoformat().replace("+00:00", "Z"),
                        "data": {"connection_id": row["connection_id"],
                                 "character_id": row["character_id"],
                                 "activity_id": row["activity_id"],
                                 "notification_id": row["notification_id"],
                                 "kind": row["kind"]}, "cursor": None}
                self._post(row["url"], row["secret"], row["id"], body, event_id)
                with self.companion.connect() as db:
                    db.execute("INSERT OR IGNORE INTO companion_v2_event_deliveries VALUES(?,?,?)",
                               (row["id"], row["notification_id"], time.time()))
                delivered += 1
            except (ValueError, OSError, httpx.HTTPError):
                continue
        return delivered


def register(server, events: CompanionEvents):
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
