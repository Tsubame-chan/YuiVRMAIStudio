"""Validated, revisioned settings overrides; never edit the owner's .env."""
from __future__ import annotations

import hashlib
import json
import os
import tempfile
import threading
from pathlib import Path
from urllib.parse import urlsplit

from app.core.capabilities import PROVIDERS, provider_options

LOCK = threading.RLock()
SECRET_FIELDS = {p.credential for p in PROVIDERS if p.credential}
READ_ONLY = {"app_version", "database_url", "available_faces", "available_animations"}
LABELS = {
    "character_name": "既定のキャラクター名", "default_user_id": "既定の利用者ID",
    "chat_provider": "会話の提供元", "vision_provider": "画像理解の提供元", "stt_provider": "音声認識の提供元",
    "tts_provider": "読み上げの提供元", "tts_fallback_provider": "読み上げ失敗時の代替",
    "openai_max_output_tokens": "会話の出力上限", "openai_work_max_output_tokens": "Workの出力上限",
    "openai_vision_max_output_tokens": "画像説明の出力上限", "openai_vision_detail": "画像の読み取り精度",
    "openai_realtime_translation_language": "翻訳先の言語", "openai_realtime_voice": "Realtimeの声",
    "openai_web_search_enabled": "Web検索を使う", "openai_web_search_mode": "検索するタイミング",
    "openai_web_search_context_size": "検索情報の量", "openai_web_search_country": "国コード",
    "openai_web_search_city": "都市", "openai_web_search_region": "地域", "openai_web_search_timezone": "タイムゾーン",
    "http_tts_endpoint": "音声生成のパス", "http_tts_health_endpoint": "接続確認のパス",
    "http_tts_provider_id": "サービス名", "http_tts_payload_format": "API形式", "http_tts_voice": "声のID",
    "http_tts_model": "音声モデル", "http_tts_gender": "声の性別指定", "http_tts_instruct": "声の演技指示",
    "http_tts_lang_code": "言語コード", "http_tts_format": "音声形式", "http_tts_audio_processor": "音声の後処理",
    "http_tts_soundstretch_path": "SoundStretchの実行ファイル",
    "http_tts_irodori_num_steps": "生成ステップ数", "http_tts_irodori_seed": "生成シード",
    "http_tts_irodori_cfg_scale_text": "テキストへの忠実度", "http_tts_irodori_cfg_scale_caption": "説明への忠実度",
    "http_tts_irodori_cfg_scale_speaker": "話者への忠実度", "http_tts_irodori_chunking_enabled": "外部Irodoriサーバーの分割生成",
    "http_tts_irodori_chunk_min_chars": "分割する最小文字数", "http_tts_irodori_first_sentence_chunk_min_chars": "最初の分割の最小文字数",
    "daily_chat_limit": "会話回数の目安 / 日", "daily_vision_limit": "画像回数の目安 / 日",
    "daily_stt_minutes_limit": "音声認識の目安 / 日（分）", "daily_tts_limit": "読み上げ回数の目安 / 日",
    "tts_audio_cache_max_files": "音声キャッシュの最大ファイル数", "tts_audio_cache_max_mb": "音声キャッシュの最大容量（MB）",
    "tts_audio_cache_max_age_hours": "音声キャッシュの保持時間",
    "open_meteo_geocoding_base_url": "地名検索API", "open_meteo_forecast_base_url": "天気API",
}
OPTIONS = {
    **{f"{c}_provider": provider_options(c) for c in ("chat", "tts", "stt", "vision")},
    "tts_fallback_provider": ["", *provider_options("tts")],
    "openai_web_search_mode": ["off", "auto", "always"], "openai_web_search_context_size": ["low", "medium", "high"],
    "openai_vision_detail": ["auto", "low", "high"],
    "http_tts_payload_format": ["generic", "openai_speech", "irodori_openai_speech"],
    "http_tts_audio_processor": ["auto", "none", "soundstretch"],
}


def settings_path() -> Path:
    from app.core.config import ROOT_DIR
    return Path(os.environ.get("YUI_BACKEND_SETTINGS_PATH", ROOT_DIR / "backend/data/admin-settings.json"))


def read_overrides() -> tuple[dict, str]:
    path = settings_path()
    if path.is_symlink():
        raise ValueError("管理設定ファイルにシンボリックリンクは使えません。")
    if not path.exists():
        return {}, "initial"
    raw = path.read_bytes()
    if len(raw) > 65536:
        raise ValueError("管理設定ファイルが大きすぎます。")
    obj = json.loads(raw)
    if not isinstance(obj, dict) or obj.get("schema") != 1 or not isinstance(obj.get("values"), dict):
        raise ValueError("管理設定ファイルの形式が不正です。元ファイルを保持して復旧してください。")
    return obj["values"], hashlib.sha256(raw).hexdigest()


def group_for(name: str) -> str:
    if name.startswith("openai_web_search_"): return "search"
    if name.startswith("open_meteo_"): return "weather"
    for p in PROVIDERS:
        if name.startswith(p.prefix): return p.id
    if name.startswith("daily_"): return "limits"
    if name.startswith("tts_audio_"): return "cache"
    return "general"


def field_catalog() -> list[dict]:
    from app.core.config import Settings
    out = []
    for name, field in Settings.model_fields.items():
        if name in READ_ONLY: continue
        label = LABELS.get(name)
        if not label:
            suffix = next((name[len(p.prefix):] for p in PROVIDERS if name.startswith(p.prefix)), name)
            label = {"api_key": "APIキー", "base_url": "接続先URL", "chat_model": "会話モデル", "vision_model": "画像モデル", "transcribe_model": "音声認識モデル", "realtime_model": "音声会話モデル", "realtime_translate_model": "音声翻訳モデル", "realtime_transcribe_model": "リアルタイム文字起こしモデル"}.get(suffix, name)
        out.append({"name": name, "label": label, "group": group_for(name), "secret": name in SECRET_FIELDS,
                    "type": "boolean" if field.annotation is bool else "number" if field.annotation in (int, float) else "text",
                    "integer": field.annotation is int, "options": OPTIONS.get(name),
                    "default": "" if name in SECRET_FIELDS else field.default,
                    "advanced": not (name in SECRET_FIELDS or name.endswith(("base_url", "chat_model", "vision_model", "transcribe_model", "realtime_model")) or name in {"http_tts_endpoint", "http_tts_payload_format", "http_tts_model", "http_tts_voice"})})
    return out


def validated(values: dict) -> dict:
    from app.core.config import Settings
    from pydantic import ValidationError
    allowed = {f["name"] for f in field_catalog()}
    unknown = set(values) - allowed
    if unknown: raise ValueError("変更できない設定: " + ", ".join(sorted(unknown)))
    for name, value in values.items():
        annotation = Settings.model_fields[name].annotation
        if annotation is str and not isinstance(value, str):
            raise ValueError(f"{name}: 文字列を指定してください。")
        if annotation is bool and not isinstance(value, bool):
            raise ValueError(f"{name}: 真偽値を指定してください。")
        if annotation in (int, float) and (isinstance(value, bool) or not isinstance(value, (int, float))):
            raise ValueError(f"{name}: 数値を指定してください。")
        if name in OPTIONS and value not in OPTIONS[name]: raise ValueError(f"{name}: 対応する選択肢を指定してください。")
        if isinstance(value, str) and len(value) > 8000: raise ValueError(f"{name}: 入力が長すぎます。")
        if name.endswith("base_url") and value:
            u = urlsplit(value)
            if u.scheme not in {"http", "https"} or not u.hostname or u.username or u.password or u.query or u.fragment:
                raise ValueError(f"{name}: 認証情報やクエリを含まないhttp/https URLを指定してください。")
        if name in {"http_tts_endpoint", "http_tts_health_endpoint"} and value and (not value.startswith("/") or value.startswith("//") or "?" in value):
            raise ValueError(f"{name}: /から始まるパスを指定してください。")
        field = Settings.model_fields[name]
        if field.annotation in (int, float):
            try: number = float(value)
            except (TypeError, ValueError): raise ValueError(f"{name}: 数値を指定してください。") from None
            if not 0 <= number <= 1000000: raise ValueError(f"{name}: 0〜1000000で指定してください。")
            if name.endswith("max_output_tokens") and number < 1: raise ValueError(f"{name}: 1以上で指定してください。")
    try: obj = Settings(**values)
    except ValidationError as e:
        raise ValueError("設定値の形式が不正です: " + ", ".join(str(x["loc"][0]) for x in e.errors(include_input=False))) from None
    return {k: getattr(obj, k) for k in values}


def public_settings(settings) -> dict:
    _, revision = read_overrides()
    return {"revision": revision, "values": {f["name"]: getattr(settings, f["name"]) for f in field_catalog() if not f["secret"]},
            "secrets": {k: bool(getattr(settings, k)) for k in SECRET_FIELDS}, "fields": field_catalog()}


def save_settings(changes: dict, revision: str) -> dict:
    from app.core.config import get_settings
    with LOCK:
        values, current = read_overrides()
        if revision != current: raise FileExistsError("別の画面で設定が変更されました。再読込してから保存してください。")
        # Empty password fields mean 'keep'. Explicit null clears that override.
        for name, value in changes.items():
            if name in SECRET_FIELDS and value == "": continue
            values[name] = "" if name in SECRET_FIELDS and value is None else value
        values = validated(values)
        path = settings_path()
        path.parent.mkdir(parents=True, exist_ok=True)
        raw = json.dumps({"schema": 1, "values": values}, ensure_ascii=False, indent=2).encode()
        fd, temp = tempfile.mkstemp(prefix=".admin-settings-", dir=path.parent)
        try:
            with os.fdopen(fd, "wb") as f:
                os.chmod(temp, 0o600)
                f.write(raw); f.flush(); os.fsync(f.fileno())
            os.replace(temp, path)
        finally:
            if os.path.exists(temp): os.unlink(temp)
        get_settings.cache_clear()
        return public_settings(get_settings())
