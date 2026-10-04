"""Opt-in, single-owner OAuth authorization-code state for the Companion trial.

The public HTTP facade must authenticate the owner with a one-time approval
secret issued through the paired-device route. Codes and approval secrets are
stored as SHA-256 digests; no bearer secret is placed in a browser URL.
"""
from __future__ import annotations

import base64
import hashlib
import hmac
import re
import secrets
import time
from urllib.parse import urlsplit

import httpx
from fastapi import HTTPException

from app.core.companion_grants import GrantStore
from app.core.companion_v2 import CompanionStore


CLIENT_ID = "https://chatgpt.com/oauth/client.json"
REDIRECT_URI = "https://chatgpt.com/connector_platform_oauth_redirect"
CHALLENGE = re.compile(r"^[A-Za-z0-9_-]{43}$")
VERIFIER = re.compile(r"^[A-Za-z0-9._~-]{43,128}$")
SCOPE = {"yui:read", "yui:write"}


def verify_chatgpt_client_metadata() -> None:
    """CIMD is pinned to ChatGPT's official HTTPS document, never a user URL."""
    response = httpx.get(CLIENT_ID, timeout=8, follow_redirects=False)
    response.raise_for_status()
    document = response.json()
    if (document.get("client_id") != CLIENT_ID or
            REDIRECT_URI not in document.get("redirect_uris", []) or
            "none" not in document.get("token_endpoint_auth_methods_supported", []) or
            "code" not in document.get("response_types", [])):
        raise ValueError("ChatGPT CIMD metadata does not match the pinned client policy")


def canonical_https(value: str) -> str:
    parsed = urlsplit(value)
    if (not value or not value.isascii() or parsed.scheme != "https" or
            not parsed.hostname or parsed.username or parsed.password or
            parsed.query or parsed.fragment or value.endswith("/")):
        raise ValueError("OAuth URLs require canonical HTTPS without a trailing slash")
    return value


class CompanionOAuthStore:
    def __init__(self, companion: CompanionStore, issuer: str, resource: str):
        self.companion = companion
        self.issuer = canonical_https(issuer)
        self.resource = canonical_https(resource)
        self.grants = GrantStore(companion)
        if self.grants.oauth_resource != self.resource:
            raise ValueError("OAuth resource does not match the protected MCP resource")
        with companion.connect() as db:
            db.execute("""CREATE TABLE IF NOT EXISTS companion_v2_oauth_approvals (
                secret_digest TEXT PRIMARY KEY, grant_id TEXT NOT NULL,
                expires_at REAL NOT NULL, used_at REAL,
                FOREIGN KEY(grant_id) REFERENCES companion_v2_grants(id) ON DELETE CASCADE)""")
            db.execute("""CREATE TABLE IF NOT EXISTS companion_v2_oauth_refresh (
                token_digest TEXT PRIMARY KEY, grant_id TEXT NOT NULL,
                resource TEXT NOT NULL, scope TEXT NOT NULL,
                expires_at REAL NOT NULL, used_at REAL,
                FOREIGN KEY(grant_id) REFERENCES companion_v2_grants(id) ON DELETE CASCADE)""")
            db.execute("""CREATE TABLE IF NOT EXISTS companion_v2_oauth_codes (
                code_digest TEXT PRIMARY KEY, grant_id TEXT NOT NULL,
                client_id TEXT NOT NULL, redirect_uri TEXT NOT NULL,
                resource TEXT NOT NULL, scope TEXT NOT NULL, challenge TEXT NOT NULL,
                expires_at REAL NOT NULL, used_at REAL,
                FOREIGN KEY(grant_id) REFERENCES companion_v2_grants(id) ON DELETE CASCADE)""")

    @staticmethod
    def digest(value: str) -> str:
        return hashlib.sha256(value.encode("ascii")).hexdigest()

    def metadata(self) -> dict:
        return {"issuer": self.issuer,
                "authorization_response_iss_parameter_supported": True,
                "authorization_endpoint": self.issuer + "/authorize",
                "token_endpoint": self.issuer + "/token",
                "client_id_metadata_document_supported": True,
                "token_endpoint_auth_methods_supported": ["none"],
                "response_types_supported": ["code"],
                "grant_types_supported": ["authorization_code", "refresh_token"],
                "code_challenge_methods_supported": ["S256"],
                "scopes_supported": sorted(SCOPE)}

    def issue_owner_approval(self, owner: str, grant_id: str) -> dict:
        secret = "yui_oa_" + secrets.token_urlsafe(32)
        now = time.time()
        with self.companion.connect() as db:
            db.execute("BEGIN IMMEDIATE")
            grant = db.execute("""SELECT owner_principal,expires_at FROM companion_v2_grants
                WHERE id=? AND revoked_at IS NULL AND expires_at>?""", (grant_id, now)).fetchone()
            if grant is None or grant["owner_principal"] != owner:
                raise HTTPException(404, {"code": "grant_unavailable"})
            expiry = min(now + 600, grant["expires_at"])
            db.execute("INSERT INTO companion_v2_oauth_approvals VALUES(?,?,?,NULL)",
                       (self.digest(secret), grant_id, expiry))
        return {"approval_secret": secret, "expires_at": expiry}

    def validate_request(self, client_id: str, redirect_uri: str, resource: str,
                         scope: str, response_type: str, challenge: str,
                         challenge_method: str) -> str:
        scopes = scope.split()
        if (client_id != CLIENT_ID or redirect_uri != REDIRECT_URI or
                resource != self.resource or response_type != "code" or
                challenge_method != "S256" or not CHALLENGE.fullmatch(challenge or "") or
                not scopes or len(scopes) != len(set(scopes)) or not set(scopes) <= SCOPE):
            raise HTTPException(400, {"code": "invalid_authorization_request"})
        return " ".join(sorted(scopes))

    def authorize(self, approval_secret: str, client_id: str, redirect_uri: str,
                  resource: str, scope: str, response_type: str,
                  challenge: str, challenge_method: str) -> str:
        scopes = self.validate_request(client_id, redirect_uri, resource, scope,
                                       response_type, challenge, challenge_method)
        if (not approval_secret.startswith("yui_oa_") or len(approval_secret) > 128 or
                not approval_secret.isascii()):
            raise HTTPException(403, {"code": "invalid_owner_approval"})
        code = "yui_ac_" + secrets.token_urlsafe(32)
        now = time.time()
        with self.companion.connect() as db:
            db.execute("BEGIN IMMEDIATE")
            approval = db.execute("""SELECT a.grant_id FROM companion_v2_oauth_approvals a
                JOIN companion_v2_grants g ON g.id=a.grant_id
                WHERE a.secret_digest=? AND a.used_at IS NULL AND a.expires_at>?
                  AND g.revoked_at IS NULL AND g.expires_at>?""",
                (self.digest(approval_secret), now, now)).fetchone()
            if approval is None:
                raise HTTPException(403, {"code": "invalid_owner_approval"})
            for wanted in scopes.split():
                operations = ("read_inbox", "read_context") if wanted == "yui:read" else (
                    "acknowledge", "post_update", "request_input")
                placeholders = ",".join("?" for _ in operations)
                if not db.execute("SELECT 1 FROM companion_v2_grant_operations "
                                  f"WHERE grant_id=? AND operation IN ({placeholders})",
                                  (approval["grant_id"], *operations)).fetchone():
                    raise HTTPException(403, {"code": "companion_grant_scope_denied"})
            db.execute("UPDATE companion_v2_oauth_approvals SET used_at=? WHERE secret_digest=?",
                       (now, self.digest(approval_secret)))
            db.execute("INSERT INTO companion_v2_oauth_codes VALUES(?,?,?,?,?,?,?,?,NULL)",
                       (self.digest(code), approval["grant_id"], client_id, redirect_uri,
                        resource, scopes, challenge, now + 120))
        return code

    def exchange(self, code: str, client_id: str, redirect_uri: str, resource: str,
                 verifier: str, grant_type: str) -> dict:
        if (grant_type != "authorization_code" or client_id != CLIENT_ID or
                redirect_uri != REDIRECT_URI or resource != self.resource or
                not code.startswith("yui_ac_") or len(code) > 128 or not code.isascii() or
                not VERIFIER.fullmatch(verifier or "")):
            raise HTTPException(400, {"code": "invalid_grant"})
        challenge = base64.urlsafe_b64encode(hashlib.sha256(verifier.encode("ascii")).digest()).rstrip(b"=").decode()
        now = time.time()
        with self.companion.connect() as db:
            db.execute("BEGIN IMMEDIATE")
            row = db.execute("""SELECT c.grant_id,c.scope,c.challenge FROM companion_v2_oauth_codes c
                JOIN companion_v2_grants g ON g.id=c.grant_id
                WHERE c.code_digest=? AND c.used_at IS NULL AND c.expires_at>?
                  AND c.client_id=? AND c.redirect_uri=? AND c.resource=?
                  AND g.revoked_at IS NULL AND g.expires_at>?""",
                (self.digest(code), now, client_id, redirect_uri, resource, now)).fetchone()
            if row is None or not hmac.compare_digest(row["challenge"], challenge):
                raise HTTPException(400, {"code": "invalid_grant"})
            db.execute("UPDATE companion_v2_oauth_codes SET used_at=? WHERE code_digest=?",
                       (now, self.digest(code)))
        # Parent revocation is checked again while minting. A consumed code
        # remains consumed if a later database failure prevents token issuance.
        return self._issue_with_refresh(row["grant_id"], resource, row["scope"])

    def _issue_with_refresh(self, grant_id: str, resource: str, scope: str) -> dict:
        access = self.grants.issue_oauth_access(grant_id, resource, scope)
        refresh = "yui_rt_" + secrets.token_urlsafe(32)
        now = time.time()
        with self.companion.connect() as db:
            db.execute("BEGIN IMMEDIATE")
            parent = db.execute("""SELECT expires_at FROM companion_v2_grants
                WHERE id=? AND revoked_at IS NULL AND expires_at>?""",
                (grant_id, now)).fetchone()
            if parent is None:
                raise HTTPException(403, {"code": "companion_grant_denied"})
            db.execute("INSERT INTO companion_v2_oauth_refresh VALUES(?,?,?,?,?,NULL)",
                       (self.digest(refresh), grant_id, resource, scope, parent["expires_at"]))
        return {**access, "refresh_token": refresh}

    def refresh(self, refresh_token: str, client_id: str, resource: str,
                grant_type: str) -> dict:
        if (grant_type != "refresh_token" or client_id != CLIENT_ID or
                resource != self.resource or not refresh_token.startswith("yui_rt_") or
                len(refresh_token) > 128 or not refresh_token.isascii()):
            raise HTTPException(400, {"code": "invalid_grant"})
        now = time.time()
        with self.companion.connect() as db:
            db.execute("BEGIN IMMEDIATE")
            row = db.execute("""SELECT r.grant_id,r.scope FROM companion_v2_oauth_refresh r
                JOIN companion_v2_grants g ON g.id=r.grant_id
                WHERE r.token_digest=? AND r.used_at IS NULL AND r.expires_at>?
                  AND r.resource=? AND g.revoked_at IS NULL AND g.expires_at>?""",
                (self.digest(refresh_token), now, resource, now)).fetchone()
            if row is None:
                raise HTTPException(400, {"code": "invalid_grant"})
            db.execute("UPDATE companion_v2_oauth_refresh SET used_at=? WHERE token_digest=?",
                       (now, self.digest(refresh_token)))
        return self._issue_with_refresh(row["grant_id"], resource, row["scope"])
