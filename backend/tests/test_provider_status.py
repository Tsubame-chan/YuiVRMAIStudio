from app.core.config import Settings
import pytest

from app.core.provider_status import build_provider_status, probe_http_tts
from app.main import app
from fastapi.testclient import TestClient


def test_provider_status_endpoint_returns_user_facing_status(monkeypatch) -> None:
    async def fake_probe_voicevox(settings):
        return {"status": "offline", "detail": "not running"}

    monkeypatch.setattr("app.api.routes.probe_voicevox", fake_probe_voicevox)

    response = TestClient(app).get("/providers/status")

    assert response.status_code == 200
    payload = response.json()
    assert payload["backend"]["status"] in {"ok", "degraded"}
    assert payload["providers"]["openai"]["status"] in {"configured", "missing_key"}
    assert payload["providers"]["voicevox"]["status"] == "offline"
    assert payload["providers"]["lmstudio"]["status"] == "configured"


def test_provider_status_reports_missing_cloud_keys_without_cloud_probe() -> None:
    response = build_provider_status(
        Settings(openai_api_key="", gemini_api_key="", http_tts_base_url=""),
        database_ok=True,
        voicevox_status={"status": "not_checked"},
    )

    assert response.backend.status == "ok"
    assert response.database.status == "ok"
    assert response.providers["openai"].status == "missing_key"
    assert response.providers["gemini"].status == "missing_key"
    assert response.providers["voicevox"].status == "not_checked"
    assert response.providers["http_tts"].status == "not_configured"


def test_provider_status_marks_configured_openai_without_calling_paid_api() -> None:
    response = build_provider_status(
        Settings(openai_api_key="sk-test", gemini_api_key="gemini-test"),
        database_ok=False,
        voicevox_status={"status": "ok", "version": "1.2.3", "speakers": 30},
    )

    assert response.backend.status == "degraded"
    assert response.database.status == "error"
    assert response.providers["openai"].status == "configured"
    assert response.providers["openai"].chat_model == "gpt-5.4-mini"
    assert response.providers["gemini"].status == "configured"
    assert response.providers["voicevox"].status == "ok"
    assert response.providers["voicevox"].version == "1.2.3"
    assert response.providers["voicevox"].speakers == 30


def test_provider_status_marks_http_tts_configured_without_network_probe() -> None:
    response = build_provider_status(
        Settings(
            tts_provider="http",
            http_tts_base_url="https://tts.example.test",
            http_tts_provider_id="irodori",
            http_tts_payload_format="openai_speech",
        ),
        database_ok=True,
        voicevox_status={"status": "disabled"},
        http_tts_status={"status": "configured", "detail": "not probed"},
    )

    assert response.providers["http_tts"].status == "configured"
    assert response.providers["http_tts"].base_url == "https://tts.example.test"
    assert response.providers["http_tts"].detail == "provider=irodori; not probed"
    assert response.providers["http_tts"].engine == "irodori_mlx"
    assert response.providers["http_tts"].recommendation == "macOS Apple Silicon candidate; keep VOICEVOX as default."


def test_provider_status_labels_irodori_server_for_windows_gpu_candidate() -> None:
    response = build_provider_status(
        Settings(
            tts_provider="http",
            http_tts_base_url="http://127.0.0.1:8088",
            http_tts_provider_id="irodori-server",
            http_tts_payload_format="irodori_openai_speech",
            http_tts_irodori_num_steps=16,
            http_tts_irodori_chunking_enabled=False,
        ),
        database_ok=True,
        voicevox_status={"status": "disabled"},
        http_tts_status={"status": "configured", "detail": "not probed"},
    )

    item = response.providers["http_tts"]
    assert item.engine == "irodori_server"
    assert item.recommendation == "Windows NVIDIA candidate; verify model quality before exposing broadly."
    assert "num_steps=16" in item.detail
    assert "chunking=false" in item.detail


def test_provider_status_reports_lmstudio_configured() -> None:
    response = build_provider_status(
        Settings(
            lmstudio_base_url="http://127.0.0.1:1234/v1",
            lmstudio_chat_model="local-yui",
        ),
        database_ok=True,
        voicevox_status={"status": "disabled"},
    )

    assert response.providers["lmstudio"].status == "configured"
    assert response.providers["lmstudio"].base_url == "http://127.0.0.1:1234/v1"
    assert response.providers["lmstudio"].chat_model == "local-yui"


def test_provider_status_reports_xai_key_status() -> None:
    response = build_provider_status(
        Settings(xai_api_key="xai-test", xai_chat_model="grok-4.3"),
        database_ok=True,
        voicevox_status={"status": "disabled"},
    )

    assert response.providers["xai"].status == "configured"
    assert response.providers["xai"].requires_api_key is True
    assert response.providers["xai"].chat_model == "grok-4.3"


@pytest.mark.anyio
async def test_probe_http_tts_skips_network_when_health_endpoint_is_not_configured() -> None:
    status = await probe_http_tts(
        Settings(
            tts_provider="http",
            http_tts_base_url="https://tts.example.test",
            http_tts_health_endpoint="",
        )
    )

    assert status == {"status": "configured", "detail": "not probed"}
