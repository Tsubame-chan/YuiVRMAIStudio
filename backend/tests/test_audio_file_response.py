from pathlib import Path

from app.api import routes


def test_audio_file_response_allows_common_tts_audio_formats(tmp_path: Path, monkeypatch) -> None:
    monkeypatch.setattr(routes, "_AUDIO_DIR", tmp_path)
    (tmp_path / "sample.wav").write_bytes(b"wav")
    (tmp_path / "sample.mp3").write_bytes(b"mp3")
    (tmp_path / "sample.ogg").write_bytes(b"ogg")

    assert routes._audio_file_response("sample.wav").media_type == "audio/wav"
    assert routes._audio_file_response("sample.mp3").media_type == "audio/mpeg"
    assert routes._audio_file_response("sample.ogg").media_type == "audio/ogg"
