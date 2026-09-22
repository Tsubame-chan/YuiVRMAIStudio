from fastapi.testclient import TestClient

from app.core.config import Settings, get_settings
from app.core.provider_status import build_provider_status
from app.main import app


def test_backend_stt_missing_key_is_configuration_failure():
    app.dependency_overrides[get_settings] = lambda: Settings(openai_api_key="")
    try:
        response = TestClient(app).post(
            "/stt", files={"audio": ("synthetic.wav", b"synthetic", "audio/wav")},
            headers={"Authorization": "Bearer app-only-key"},
        )
    finally:
        app.dependency_overrides.clear()
    assert response.status_code == 503
    assert "Backend OpenAI key" in response.json()["detail"]
    assert "app-only-key" not in response.text


def test_backend_reads_its_own_env_and_environment_has_precedence(tmp_path, monkeypatch):
    env_file = tmp_path / ".env"
    env_file.write_text("OPENAI_API_KEY=backend-file-key\n")
    monkeypatch.delenv("OPENAI_API_KEY", raising=False)
    assert Settings(_env_file=env_file).openai_api_key == "backend-file-key"
    monkeypatch.setenv("OPENAI_API_KEY", "backend-process-key")
    assert Settings(_env_file=env_file).openai_api_key == "backend-process-key"


def test_optional_voice_engine_is_not_ready_from_a_url_alone():
    result = build_provider_status(
        Settings(openai_api_key="", aivis_base_url="http://127.0.0.1:10101"),
        database_ok=True, voicevox_status={"status": "offline"},
        aivis_status={"status": "offline", "detail": "not running"},
    )
    assert result.providers["aivis"].status == "offline"
    assert result.chat_provider == "openai"
