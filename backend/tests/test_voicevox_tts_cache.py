import os
import time
from pathlib import Path
from types import SimpleNamespace

from app.providers.voicevox_tts import VoiceVoxProvider


def _provider_for(audio_dir: Path, **settings) -> VoiceVoxProvider:
    provider = VoiceVoxProvider.__new__(VoiceVoxProvider)
    provider.audio_dir = audio_dir
    provider.settings = SimpleNamespace(**settings)
    return provider


def _write_wav(path: Path, size: int, age_seconds: int) -> None:
    path.write_bytes(b"0" * size)
    timestamp = time.time() - age_seconds
    os.utime(path, (timestamp, timestamp))


def test_voicevox_cache_limit_removes_oldest_files(tmp_path: Path) -> None:
    _write_wav(tmp_path / "old.wav", 10, 30)
    _write_wav(tmp_path / "middle.wav", 10, 20)
    _write_wav(tmp_path / "new.wav", 10, 10)
    provider = _provider_for(
        tmp_path,
        tts_audio_cache_max_files=2,
        tts_audio_cache_max_mb=0,
        tts_audio_cache_max_age_hours=0,
    )

    provider._enforce_cache_limit()

    assert sorted(path.name for path in tmp_path.glob("*.wav")) == ["middle.wav", "new.wav"]


def test_voicevox_cache_limit_removes_oldest_until_total_size_is_under_limit(tmp_path: Path) -> None:
    _write_wav(tmp_path / "old.wav", 700 * 1024, 30)
    _write_wav(tmp_path / "middle.wav", 700 * 1024, 20)
    _write_wav(tmp_path / "new.wav", 700 * 1024, 10)
    provider = _provider_for(
        tmp_path,
        tts_audio_cache_max_files=0,
        tts_audio_cache_max_mb=1,
        tts_audio_cache_max_age_hours=0,
    )

    provider._enforce_cache_limit()

    remaining = sorted(path.name for path in tmp_path.glob("*.wav"))
    total_bytes = sum(path.stat().st_size for path in tmp_path.glob("*.wav"))
    assert remaining == ["new.wav"]
    assert total_bytes <= 1024 * 1024


def test_voicevox_cache_limit_removes_files_older_than_retention_hours(tmp_path: Path) -> None:
    _write_wav(tmp_path / "stale.wav", 10, 3 * 60 * 60)
    _write_wav(tmp_path / "fresh.wav", 10, 30 * 60)
    provider = _provider_for(
        tmp_path,
        tts_audio_cache_max_files=0,
        tts_audio_cache_max_mb=0,
        tts_audio_cache_max_age_hours=1,
    )

    provider._enforce_cache_limit()

    assert sorted(path.name for path in tmp_path.glob("*.wav")) == ["fresh.wav"]
