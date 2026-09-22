from app.core import tts_inventory
from app.core.config import Settings
from app.core.provider_status import build_provider_status


def test_aivis_stopped_installed_engine_is_selectable_not_ready(monkeypatch):
    monkeypatch.setattr("app.core.provider_status.aivis_installed", lambda _: True)
    result = build_provider_status(Settings(_env_file=None), database_ok=True,
        voicevox_status={"status": "offline"}, aivis_status={"status": "offline"})
    assert result.providers["aivis"].selectable
    assert result.providers["aivis"].status == "offline"


def test_aivis_default_url_does_not_mean_installed(monkeypatch):
    monkeypatch.setattr("app.core.provider_status.aivis_installed", lambda _: False)
    result = build_provider_status(Settings(_env_file=None), database_ok=True,
        voicevox_status={"status": "offline"}, aivis_status={"status": "offline"})
    assert not result.providers["aivis"].selectable


def test_explicit_engine_path_is_detected_only_for_local_endpoint(tmp_path, monkeypatch):
    engine = tmp_path / "run"
    engine.write_text("#!/bin/sh\n")
    engine.chmod(0o700)
    monkeypatch.setenv("AIVIS_ENGINE_EXE", str(engine))
    assert tts_inventory.aivis_installed("http://127.0.0.1:10101")
    assert not tts_inventory.aivis_installed("http://192.0.2.1:10101")


def test_configured_irodori_offline_is_selectable_not_ready():
    settings = Settings(_env_file=None, http_tts_base_url="http://127.0.0.1:41080",
        http_tts_provider_id="irodori", http_tts_payload_format="openai_speech")
    result = build_provider_status(settings, database_ok=True,
        voicevox_status={"status": "offline"}, http_tts_status={"status": "offline"})
    assert result.providers["http_tts"].selectable
    assert result.providers["http_tts"].status == "offline"
    assert result.providers["http_tts"].engine == "irodori_mlx"
