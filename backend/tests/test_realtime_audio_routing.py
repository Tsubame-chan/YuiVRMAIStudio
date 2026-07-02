from app.api.routes import _normalize_realtime_audio_mode


def test_realtime_audio_mode_keeps_openai_voice_default() -> None:
    assert _normalize_realtime_audio_mode("voice") == "voice"
    assert _normalize_realtime_audio_mode("") == "voice"


def test_realtime_audio_mode_routes_tts_conversation_through_voice_text() -> None:
    assert _normalize_realtime_audio_mode("voice_text") == "voice_text"
    assert _normalize_realtime_audio_mode("voicevox") == "voice_text"
    assert _normalize_realtime_audio_mode("aivis") == "voice_text"


def test_realtime_audio_mode_keeps_translation_separate() -> None:
    assert _normalize_realtime_audio_mode("translate") == "translate"
