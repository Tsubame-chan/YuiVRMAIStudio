"""Local management surface. Existing Unity APIs retain their request contracts."""
from __future__ import annotations

import secrets
import json
import os
import sqlite3
from pathlib import Path
from urllib.parse import urlsplit

import httpx
from fastapi import APIRouter, Depends, HTTPException, Request, Response, WebSocketException
from fastapi.responses import FileResponse, RedirectResponse
from pydantic import BaseModel, Field, ConfigDict
from starlette.requests import HTTPConnection

from app.core.config import Settings, get_settings
from app.core.runtime_events import events_since, record, diagnose
from app.core.capabilities import PROVIDER_BY_ID, provider_catalog, CAPABILITIES
from app.core.admin_settings import public_settings, save_settings
from app.db.sqlite import sqlite_path_from_url, check_database
from app.api.routes import router as runtime_router, providers_status

from app.core.voice_library import LibraryUpdate

STATIC = Path(__file__).resolve().parents[1] / "static/admin"
LOOPBACK = {"127.0.0.1", "localhost", "::1"}
COOKIE = "yui_admin_session"


def reject(connection: HTTPConnection, message: str):
    if connection.scope["type"] == "websocket": raise WebSocketException(code=1008, reason=message)
    raise HTTPException(403, message)


def local_only(connection: HTTPConnection):
    if not connection.client or connection.client.host not in LOOPBACK or connection.url.hostname not in LOOPBACK:
        reject(connection, "管理画面はBackendが動くPCのlocalhostから開いてください。")
    origin = connection.headers.get("origin")
    expected = ("https" if connection.url.scheme in {"https", "wss"} else "http") + "://" + connection.headers.get("host", "")
    if origin and origin != expected:
        reject(connection, "別のサイトから管理画面を操作することはできません。")


def require_admin(connection: HTTPConnection):
    local_only(connection)
    token = getattr(connection.app.state, "admin_session", "")
    if not token or not secrets.compare_digest(connection.cookies.get(COOKIE, ""), token):
        reject(connection, "管理画面を再読み込みしてください。")
    if connection.scope["type"] == "websocket":
        if not connection.headers.get("origin"): reject(connection, "Originが必要です。")
    elif connection.scope["method"] not in {"GET", "HEAD"} and connection.headers.get("x-yui-admin") != "1":
        reject(connection, "管理画面から操作してください。")


public = APIRouter(dependencies=[Depends(local_only)])
router = APIRouter(prefix="/admin/api", dependencies=[Depends(require_admin)])


@public.get("/admin", include_in_schema=False)
def admin_redirect(): return RedirectResponse("/admin/")


@public.get("/admin/", include_in_schema=False)
def admin_page(): return FileResponse(STATIC / "index.html")


@public.get("/admin/assets/{name}", include_in_schema=False)
def admin_asset(name: str):
    if name not in {"console.css", "console.js", "realtime.js", "diagnostics.js", "voices.js", "tts-guides.js", "device-sync.js", "shared-character.js", "work.js", "NotoSansJP-Regular.otf", "font-license.txt", "icon.png"}:
        raise HTTPException(404)
    return FileResponse(STATIC / name)


@public.post("/admin/session", include_in_schema=False)
def session(request: Request, response: Response):
    if request.headers.get("x-yui-admin") != "1": raise HTTPException(403, "管理画面から開いてください。")
    if not getattr(request.app.state, "admin_session", None): request.app.state.admin_session = secrets.token_urlsafe(48)
    response.set_cookie(COOKIE, request.app.state.admin_session, httponly=True, samesite="strict", secure=request.url.scheme == "https", path="/admin", max_age=43200)
    return {"ok": True}


@router.get("/overview")
def overview(request: Request, settings: Settings = Depends(get_settings)):
    try:
        hosts = json.loads(os.environ.get("YUI_BACKEND_LISTEN_HOSTS", "null"))
    except (ValueError, TypeError):
        hosts = None
    if not isinstance(hosts, list):
        hosts = [request.scope.get("server", ("127.0.0.1", 8000))[0]]
    remote_hosts = [host for host in hosts if host not in LOOPBACK]
    port = request.url.port or 8000
    remote_urls = [f"http://{'['+host+']' if ':' in host else host}:{port}" for host in remote_hosts if host not in {"0.0.0.0", "::"}]
    return {"backend": "ok" if check_database(settings.database_url) else "degraded", "version": settings.app_version,
            "schema": "2026-05-10", "console_version": "1", "url": str(request.base_url).rstrip("/"),
            "management_access": "localhost", "network": {"remote_enabled": bool(remote_hosts), "remote_urls": remote_urls},
            "capabilities": CAPABILITIES, "providers": provider_catalog(),
            "settings": public_settings(settings), "extensions": [
                {"name": "資料を取り込むRAG・embedding索引", "state": "未実装", "detail": "現在の記憶検索はキーワード検索です。資料取込やembedding基盤は別途実装します。"},
                {"name": "キャラクターと会話の端末同期", "state": "端末登録後に利用", "detail": "対応アプリで共有先と差分を確認します。接続と設定 → 端末と同期で登録してください。"},
                {"name": "前面アプリの自動認識", "state": "試作・無効", "detail": "通常のBackend会話へ自動で取り込む機能は提供していません。"},
            ]}


class SettingsUpdate(BaseModel):
    model_config = ConfigDict(extra="forbid")
    revision: str = Field(max_length=128)
    changes: dict = Field(max_length=100)


@router.patch("/settings")
def update_settings(body: SettingsUpdate):
    try: return save_settings(body.changes, body.revision)
    except FileExistsError as e:
        record("issue", operation="settings", code="settings_conflict")
        raise HTTPException(409, str(e)) from None
    except (ValueError, TypeError):
        record("issue", operation="settings", code="invalid_settings")
        raise HTTPException(422, "設定値を保存できません。選択肢・URL・数値の範囲を確認してください。") from None
    except OSError as e:
        diagnose(e, operation="settings")
        raise HTTPException(503, "設定ファイルを保存できません。現在の設定は保持しました。") from None


@router.get("/events")
def activity_events(after: int = 0):
    return events_since(max(0, after))


@router.get("/status")
async def status(settings: Settings = Depends(get_settings)):
    return await providers_status(settings)


@router.post("/probe/{provider_id}")
async def probe(provider_id: str, settings: Settings = Depends(get_settings)):
    p = PROVIDER_BY_ID.get(provider_id)
    if p is None: raise HTTPException(404)
    record("provider_selected", operation="probe", provider=p.id)
    if p.credential and provider_id != "http" and not getattr(settings, p.credential):
        record("issue", operation="probe", provider=p.id, code="missing_key")
        return {"status": "missing_key", "detail": "APIキーを設定してください。", "voices": [], "models": []}
    if provider_id in {"openai", "xai", "gemini"}:
        record("configured_only", operation="probe", provider=p.id)
        return {"status": "configured", "detail": "キーは設定済みです。認証と応答は「試す」で確認できます（API利用料金が発生する場合があります）。", "voices": [], "models": []}
    base = getattr(settings, p.endpoint, "")
    endpoint = "/speakers" if provider_id in {"voicevox", "aivis"} else "/models" if provider_id in {"lmstudio", "litert_lm"} else settings.http_tts_health_endpoint
    if not base:
        record("issue", operation="probe", provider=p.id, code="missing_endpoint")
        return {"status": "not_configured", "detail": "接続先URLを設定してください。", "voices": [], "models": []}
    if not endpoint:
        record("probe_skipped", operation="probe", provider=p.id)
        return {"status": "configured", "detail": "接続確認パスが未設定です。試聴で実際の生成を確認してください。", "voices": [], "models": []}
    try:
        headers = {"Authorization": f"Bearer {settings.http_tts_api_key}"} if provider_id == "http" and settings.http_tts_api_key else {}
        async with httpx.AsyncClient(timeout=4, follow_redirects=False) as c:
            r = await c.get(base.rstrip("/") + "/" + endpoint.lstrip("/"), headers=headers)
            r.raise_for_status()
            voices, models = [], []
            if provider_id in {"voicevox", "aivis"}:
                payload = r.json()
                if not isinstance(payload, list): raise ValueError()
                for speaker in payload[:200]:
                    for style in speaker.get("styles", [])[:100]:
                        voices.append({"id": style["id"], "label": f"{speaker['name']} / {style['name']}"})
            elif provider_id in {"lmstudio", "litert_lm"}:
                models = [m["id"] for m in r.json().get("data", [])[:200]]
            record("connection_ok", operation="probe", provider=p.id)
            return {"status": "ok", "detail": "接続確認済み。生成の可否は試聴・会話で確認できます。", "voices": voices, "models": models}
    except (httpx.HTTPError, ValueError, KeyError, TypeError) as exc:
        diagnose(exc, operation="probe", provider=p.id)
        return {"status": "offline", "detail": "接続先または応答形式を確認できません。エンジンの起動とURLを確認してください。", "voices": [], "models": []}


@router.get("/voice-library")
def voice_library(settings: Settings = Depends(get_settings)):
    from app.core.voice_library import public_library
    return public_library(settings)


@router.put("/voice-library")
def update_voice_library(body: "LibraryUpdate", settings: Settings = Depends(get_settings)):
    from app.core.voice_library import save
    try: return save(body, settings)
    except FileExistsError as exc: raise HTTPException(409, str(exc)) from None
    except ValueError: raise HTTPException(422, "音声設定を保存できません。項目・範囲・参照先を確認してください。") from None
    except OSError: raise HTTPException(503, "音声設定を保存できません。現在の設定は保持しました。") from None


@router.post("/voice-endpoints/{endpoint_id}/probe")
async def probe_voice_endpoint(endpoint_id: str, settings: Settings = Depends(get_settings)):
    from app.core.voice_library import load, endpoints, endpoint_settings, capabilities
    data, _ = load(settings)
    endpoint = endpoints(data, settings).get(endpoint_id)
    if endpoint is None: raise HTTPException(404)
    resolved=endpoint_settings(endpoint, settings)
    if capabilities(endpoint,settings)['voices']!='design':
        return await probe(endpoint['provider_type'], resolved)
    # Irodori VoiceDesign has models and voice descriptions, not a speaker list.
    if not resolved.http_tts_base_url:
        return {'status':'not_configured','detail':'Irodoriの接続先URLを設定してください。','voices':[],'models':[]}
    prefix=resolved.http_tts_endpoint.rsplit('/audio/speech',1)[0]
    model_path=prefix+'/models' if '/audio/speech' in resolved.http_tts_endpoint else '/v1/models'
    headers={'Authorization':f'Bearer {resolved.http_tts_api_key}'} if resolved.http_tts_api_key else {}
    try:
        async with httpx.AsyncClient(timeout=4,follow_redirects=False) as client:
            response=await client.get(resolved.http_tts_base_url.rstrip('/')+model_path,headers=headers)
            response.raise_for_status()
            models=[m['id'] for m in response.json()['data'][:200] if isinstance(m.get('id'),str)]
            voices=[]
            voice_detail=''
            if resolved.http_tts_payload_format=='irodori_openai_speech':
                try:
                    voice_response=await client.get(resolved.http_tts_base_url.rstrip('/')+prefix+'/audio/voices',headers=headers)
                    voice_response.raise_for_status()
                    voices=[{'id':v['id'],'label':'声の説明で生成' if v['id']=='none' else v['id']} for v in voice_response.json()['data'][:200] if isinstance(v.get('id'),str)]
                except (httpx.HTTPError,ValueError,KeyError,TypeError):
                    voice_detail=' 登録済みの声一覧には接続できません。接続設定のvoiceを使うか、サーバーで登録した名前を指定できます。'
        if not models:
            return {'status':'empty','detail':'接続できましたがモデルがありません。Irodori側でモデルを読み込んでください。','voices':[],'models':[]}
        record('connection_ok',operation='probe',provider='http')
        return {'status':'ok','detail':'モデル一覧を取得しました。生成の可否は試聴で確認できます。'+voice_detail,'voices':voices,'models':models}
    except (httpx.HTTPError,ValueError,KeyError,TypeError) as exc:
        diagnose(exc,operation='probe',provider='http')
        return {'status':'offline','detail':'Irodoriのモデル一覧を取得できません。エンジンの起動と接続先を確認してください。設定済みモデルは選べます。','voices':[],'models':[]}


@router.get("/identities")
def identities(settings: Settings = Depends(get_settings)):
    with sqlite3.connect(sqlite_path_from_url(settings.database_url)) as c:
        c.row_factory = sqlite3.Row
        rows = c.execute("""SELECT user_id, character_id, session_id FROM conversations
            UNION SELECT user_id, character_id, NULL FROM memories
            ORDER BY user_id, character_id, session_id LIMIT 1000""").fetchall()
    return {"items": [dict(r) for r in rows], "limit": 1000}


class ClearScope(BaseModel):
    model_config = ConfigDict(extra="forbid")
    user_id: str = Field(min_length=1, max_length=128)
    character_id: str | None = Field(default=None, max_length=128)
    session_id: str | None = Field(default=None, max_length=128)
    include_memories: bool = False
    confirmation: str


@router.post("/clear-scope")
def clear_scope(body: ClearScope, settings: Settings = Depends(get_settings)):
    if body.confirmation != body.user_id: raise HTTPException(422, "確認用の利用者IDが一致しません。")
    with sqlite3.connect(sqlite_path_from_url(settings.database_url)) as c:
        args = (body.user_id, body.character_id, body.session_id)
        counts = {}
        for table in ("conversations", "chat_responses"):
            counts[table] = c.execute(f"DELETE FROM {table} WHERE user_id=? AND character_id IS ? AND session_id IS ?", args).rowcount
        if body.include_memories:
            counts["memories"] = c.execute("DELETE FROM memories WHERE user_id=? AND character_id IS ?", args[:2]).rowcount
    return {"deleted": counts}


# Same handlers, validation and provider execution as the Unity API, with the
# browser-only access policy applied to every route including WebSocket.
router.include_router(runtime_router, prefix="/run")

class VoicePreview(BaseModel):
    model_config = ConfigDict(extra='forbid')
    endpoint_id: str = Field(max_length=64)
    parameters: dict = Field(default_factory=dict, max_length=12)
    voice: str = Field(default='', max_length=200)
    model: str = Field(default='', max_length=200)
    text: str = Field(min_length=1, max_length=2000)


@router.post('/voice-preview')
async def preview_voice(body: VoicePreview, settings: Settings = Depends(get_settings)):
    from app.core.voice_library import load, resolve_profile
    from app.models.tts import TTSRequest
    from app.providers.router import ProviderRouter
    from app.providers.voicevox_tts import TTSProviderError
    from app.api.routes import _audio_file_response
    data, _ = load(settings)
    profile = {'id':'preview','name':'試聴','endpoint_id':body.endpoint_id,'parameters':body.parameters,
               'voice':body.voice,'model':body.model,'fallback_profile_id':None}
    data = {**data, 'profiles':[profile]}
    try:
        request, resolved_settings, _, _ = resolve_profile('preview', TTSRequest(text=body.text), settings, data)
    except ValueError: raise HTTPException(422, '接続先と対応する音声調整を確認してください。') from None
    try:
        provider = ProviderRouter(resolved_settings).tts(request.provider)
        record('generating', operation='tts', provider=provider.name)
        response = await provider.synthesize(request)
        return _audio_file_response(response.audio_url.rsplit('/',1)[-1])
    except TTSProviderError as exc:
        diagnose(exc, operation='tts', provider=request.provider)
        raise HTTPException(502, '音声を生成できませんでした。接続先・声の選択・実行状況を確認してください。') from None
