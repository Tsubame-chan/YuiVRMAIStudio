"""Personal-trial OAuth facade. Publish only behind owner-controlled HTTPS.

This process never serves the Yui database directly. It exchanges a one-time
approval issued by a paired device for a PKCE-bound authorization code.
"""
from __future__ import annotations

import html
import os
from urllib.parse import parse_qs, urlencode

import uvicorn
from fastapi import FastAPI, HTTPException, Request
from fastapi.responses import HTMLResponse, JSONResponse, RedirectResponse

from app.core.companion_oauth import (CompanionOAuthStore, CLIENT_ID, REDIRECT_URI,
                                      verify_chatgpt_client_metadata)
from app.core.companion_v2 import CompanionStore
from app.core.config import get_settings


if (os.environ.get("YUI_COMPANION_OAUTH_TESTING_ENABLED") != "true" or
        not get_settings().companion_v2_testing_enabled):
    raise RuntimeError("Companion OAuth requires explicit testing flags")

oauth = CompanionOAuthStore(
    CompanionStore(get_settings().database_url),
    os.environ["YUI_COMPANION_OAUTH_ISSUER"],
    os.environ["YUI_COMPANION_OAUTH_RESOURCE"])
verify_chatgpt_client_metadata()
app = FastAPI(docs_url=None, redoc_url=None, openapi_url=None)
NO_STORE = {"Cache-Control": "no-store", "Pragma": "no-cache",
            "Referrer-Policy": "no-referrer", "X-Content-Type-Options": "nosniff"}
FORM_HEADERS = {**NO_STORE, "Referrer-Policy": "same-origin", "Content-Security-Policy":
                "default-src 'none'; style-src 'unsafe-inline'; form-action 'self' https://chatgpt.com; base-uri 'none'"}
FIELDS = ("client_id", "redirect_uri", "resource", "scope", "response_type",
          "code_challenge", "code_challenge_method", "state")


def _redirect(**params):
    response = RedirectResponse(REDIRECT_URI + "?" + urlencode(params), status_code=302)
    response.headers.update(NO_STORE)
    return response


def _flow(values: dict[str, str]) -> str:
    if len(values.get("state", "")) > 512 or not values.get("state"):
        raise HTTPException(400, {"code": "invalid_state"})
    return oauth.validate_request(
        values.get("client_id", ""), values.get("redirect_uri", ""),
        values.get("resource", ""), values.get("scope", ""),
        values.get("response_type", ""), values.get("code_challenge", ""),
        values.get("code_challenge_method", ""))


def _form(values: dict[str, str], scopes: str) -> HTMLResponse:
    requested = "閲覧" if scopes == "yui:read" else "書き込み" if scopes == "yui:write" else "閲覧・書き込み"
    hidden = "".join(f'<input type="hidden" name="{name}" value="{html.escape(values[name], quote=True)}">'
                     for name in FIELDS)
    body = f"""<!doctype html><html lang="ja"><meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>Yui 接続の確認</title><style>
body{{margin:0;min-height:100vh;display:grid;place-items:center;background:#17191e;color:#f5f2fa;
font-family:-apple-system,BlinkMacSystemFont,'Noto Sans JP',sans-serif}}
main{{box-sizing:border-box;width:min(100%,480px);padding:32px;background:#24232b;border:1px solid #4d4759;
border-radius:20px}}h1{{font-size:23px;margin:0 0 18px}}p{{line-height:1.7;color:#dbd4e8}}
label{{display:block;margin:24px 0 8px}}input[type=text]{{box-sizing:border-box;width:100%;min-height:48px;
border-radius:10px;border:2px solid #a99aca;background:#17191e;color:#fff;padding:10px;font-size:16px}}
button{{width:100%;min-height:48px;margin-top:22px;border:0;border-radius:10px;background:#b9a6e8;
color:#18151f;font-weight:700;font-size:16px;cursor:pointer}}small{{display:block;margin-top:18px;color:#b6aec6;line-height:1.6}}
</style><main><h1>Yui と ChatGPT を接続</h1>
<p>ChatGPT に、このYui接続で許可した範囲の<b>{requested}</b>を許可します。接続先は本人のYui母艦です。</p>
<form method="post" action="/authorize">{hidden}
<label for="approval">Yuiアプリで発行した一回限りの確認コード</label>
<input id="approval" type="password" name="approval_secret" autocomplete="off" required>
<button type="submit">この範囲で接続する</button></form>
<small>確認コードは約10分で失効します。接続はYui側でいつでも取り消せます。</small></main></html>"""
    return HTMLResponse(body, headers=FORM_HEADERS)


async def _urlencoded(request: Request) -> dict[str, str]:
    if request.headers.get("content-type", "").split(";")[0] != "application/x-www-form-urlencoded":
        raise HTTPException(415, {"code": "form_required"})
    body = await request.body()
    if len(body) > 4096:
        raise HTTPException(413, {"code": "form_too_large"})
    try:
        pairs = parse_qs(body.decode("ascii"), keep_blank_values=True, strict_parsing=True,
                         max_num_fields=16)
    except (UnicodeDecodeError, ValueError):
        raise HTTPException(400, {"code": "invalid_form"}) from None
    if any(len(values) != 1 for values in pairs.values()):
        raise HTTPException(400, {"code": "duplicate_form_field"})
    return {key: values[0] for key, values in pairs.items()}


@app.get("/.well-known/oauth-authorization-server")
def metadata():
    return JSONResponse(oauth.metadata(), headers=NO_STORE)


@app.get("/.well-known/oauth-protected-resource")
def protected_resource_metadata():
    # The public issuer can host this discovery document when the private
    # Tunnel forwards only MCP requests. The 401 challenge points here.
    return JSONResponse({"resource": oauth.resource,
                         "authorization_servers": [oauth.issuer],
                         "scopes_supported": sorted(("yui:read", "yui:write"))},
                        headers=NO_STORE)


@app.get("/authorize")
def authorize_page(request: Request):
    if len(str(request.url.query)) > 4096:
        raise HTTPException(413, {"code": "query_too_large"})
    if any(len(request.query_params.getlist(name)) != 1 for name in request.query_params):
        raise HTTPException(400, {"code": "duplicate_query_field"})
    values = dict(request.query_params)
    if values.get("client_id") != CLIENT_ID or values.get("redirect_uri") != REDIRECT_URI:
        raise HTTPException(400, {"code": "invalid_client"})
    try:
        scopes = _flow(values)
    except HTTPException:
        if not values.get("state") or len(values["state"]) > 512:
            raise HTTPException(400, {"code": "invalid_state"}) from None
        return _redirect(error="invalid_request", state=values.get("state", ""), iss=oauth.issuer)
    return _form(values, scopes)


@app.post("/authorize")
async def authorize_submit(request: Request):
    if request.headers.get("origin") != oauth.issuer:
        raise HTTPException(403, {"code": "invalid_origin"})
    values = await _urlencoded(request)
    if values.get("client_id") != CLIENT_ID or values.get("redirect_uri") != REDIRECT_URI:
        raise HTTPException(400, {"code": "invalid_client"})
    try:
        _flow(values)
        code = oauth.authorize(values.get("approval_secret", ""),
                               values["client_id"], values["redirect_uri"], values["resource"],
                               values["scope"], values["response_type"],
                               values["code_challenge"], values["code_challenge_method"])
    except HTTPException as exc:
        error = "access_denied" if exc.status_code == 403 else "invalid_request"
        return _redirect(error=error, state=values.get("state", ""), iss=oauth.issuer)
    return _redirect(code=code, state=values["state"], iss=oauth.issuer)


@app.post("/token")
async def token(request: Request):
    values = await _urlencoded(request)
    if values.get("grant_type") == "refresh_token":
        if set(values) != {"grant_type", "refresh_token", "client_id", "resource"}:
            raise HTTPException(400, {"code": "invalid_token_request"})
        result = oauth.refresh(values["refresh_token"], values["client_id"],
                               values["resource"], values["grant_type"])
    else:
        if set(values) != {"grant_type", "code", "client_id", "redirect_uri", "code_verifier", "resource"}:
            raise HTTPException(400, {"code": "invalid_token_request"})
        result = oauth.exchange(values["code"], values["client_id"], values["redirect_uri"],
                                values["resource"], values["code_verifier"], values["grant_type"])
    return JSONResponse(result, headers=NO_STORE)


if __name__ == "__main__":
    port = int(os.environ.get("YUI_COMPANION_OAUTH_PORT", "8768"))
    if not 1 <= port <= 65535:
        raise ValueError("Invalid OAuth port")
    uvicorn.run(app, host="127.0.0.1", port=port, log_level="warning")
