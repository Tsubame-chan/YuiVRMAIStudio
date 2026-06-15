from app.core.config import Settings
from app.core.provider_status import build_provider_status
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
    assert payload["providers"]["lmstudio"]["status"] == "not_implemented"


def test_provider_status_reports_missing_cloud_keys_without_cloud_probe() -> None:
    response = build_provider_status(
        Settings(openai_api_key="", gemini_api_key=""),
        database_ok=True,
        voicevox_status={"status": "not_checked"},
    )

    assert response.backend.status == "ok"
    assert response.database.status == "ok"
    assert response.providers["openai"].status == "missing_key"
    assert response.providers["gemini"].status == "missing_key"
    assert response.providers["voicevox"].status == "not_checked"


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
