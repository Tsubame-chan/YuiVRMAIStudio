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
    assert payload["tts_providers"] == ["voicevox", "aivis", "http"]
    assert payload["tts_recommendation"]["default"] == "voicevox"
    assert payload["tts_recommendation"]["mobile_default"] == "voicevox_core"
    assert payload["tts_recommendation"]["desktop_quality"] == "aivis"
    assert payload["tts_recommendation"]["macos_apple_silicon"] == "irodori_mlx"
    assert payload["tts_recommendation"]["windows_nvidia"] == "irodori_server"
    assert payload["tts_recommendation"]["windows_cpu"] == "voicevox"
    assert payload["tts_recommendation"]["aivis_label"] == "AivisSpeech HD"
    assert payload["tts_recommendation"]["irodori_server_label"] == "Irodori-TTS-Server for Windows NVIDIA"
    assert payload["tts_default_voice"]["voicevox"] == 14
    assert payload["tts_default_voice"]["aivis"] == 1431611904
    assert payload["tts_voice_options"]["aivis"] == [
        {
            "provider": "aivis",
            "id": 1431611904,
            "label": "女性ボイス①",
            "gender": "female",
            "style": "normal",
            "release_review": "ok",
        },
        {
            "provider": "aivis",
            "id": 604166016,
            "label": "女性ボイス②",
            "gender": "female",
            "style": "normal",
            "release_review": "ok",
        },
        {
            "provider": "aivis",
            "id": 1310138976,
            "label": "男性ボイス①",
            "gender": "male",
            "style": "normal",
            "release_review": "ok",
        },
    ]


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
