from fastapi.testclient import TestClient

from app.core.config import Settings
from app.main import app


def test_config_reports_available_provider_options() -> None:
    app.dependency_overrides.clear()
    from app.api.routes import get_settings

    app.dependency_overrides[get_settings] = lambda: Settings(
        openai_api_key="test-key",
        tts_provider="voicevox",
        http_tts_base_url="https://tts.example.test",
    )
    try:
        payload = TestClient(app).get("/config").json()
    finally:
        app.dependency_overrides.clear()

    assert payload["chat_providers"] == ["openai", "lmstudio"]
    assert payload["vision_providers"] == ["openai"]
    assert payload["stt_providers"] == ["openai"]
    assert payload["tts_providers"] == ["voicevox", "http"]
    assert payload["tts_recommendation"]["default"] == "voicevox"
    assert payload["tts_recommendation"]["macos_apple_silicon"] == "irodori_mlx"
    assert payload["tts_recommendation"]["windows_nvidia"] == "irodori_server"
    assert payload["tts_recommendation"]["windows_cpu"] == "voicevox"
    assert payload["tts_recommendation"]["irodori_server_label"] == "Irodori-TTS-Server for Windows NVIDIA"


def test_config_does_not_advertise_gemini_vision() -> None:
    app.dependency_overrides.clear()
    from app.api.routes import get_settings

    app.dependency_overrides[get_settings] = lambda: Settings(
        openai_api_key="test-key",
        gemini_api_key="test-gemini-key",
    )
    try:
        payload = TestClient(app).get("/config").json()
    finally:
        app.dependency_overrides.clear()

    assert payload["vision_providers"] == ["openai"]


def test_config_reports_xai_chat_provider_when_configured() -> None:
    app.dependency_overrides.clear()
    from app.api.routes import get_settings

    app.dependency_overrides[get_settings] = lambda: Settings(
        openai_api_key="test-key",
        xai_api_key="test-xai-key",
    )
    try:
        payload = TestClient(app).get("/config").json()
    finally:
        app.dependency_overrides.clear()

    assert payload["chat_providers"] == ["openai", "lmstudio", "xai"]
