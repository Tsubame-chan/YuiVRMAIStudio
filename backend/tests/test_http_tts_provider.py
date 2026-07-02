import json
import wave
import audioop
from pathlib import Path

import httpx
import pytest
from fastapi.testclient import TestClient

from app.core.config import Settings
from app.main import app
from app.models.tts import TTSRequest
from app.providers.http_tts import HttpTTSProvider
from app.providers.voicevox_tts import TTSProviderError
from app.providers.voicevox_tts import AivisSpeechProvider
from app.providers.router import ProviderRouter


def test_provider_router_returns_http_tts_provider() -> None:
    provider = ProviderRouter(
        Settings(tts_provider="http", http_tts_base_url="https://tts.example.test")
    ).tts()

    assert isinstance(provider, HttpTTSProvider)


def test_provider_router_allows_tts_request_provider_override() -> None:
    provider = ProviderRouter(
        Settings(tts_provider="voicevox", http_tts_base_url="https://tts.example.test")
    ).tts("http")

    assert isinstance(provider, HttpTTSProvider)


def test_provider_router_returns_aivis_tts_provider() -> None:
    provider = ProviderRouter(Settings(tts_provider="aivis")).tts()

    assert isinstance(provider, AivisSpeechProvider)


def test_tts_request_accepts_provider_override() -> None:
    request = TTSRequest(text="hello", provider="HTTP")

    assert request.provider == "http"


def test_config_reports_configured_tts_provider_options() -> None:
    app.dependency_overrides.clear()
    from app.api.routes import get_settings

    app.dependency_overrides[get_settings] = lambda: Settings(
        tts_provider="voicevox",
        http_tts_base_url="https://tts.example.test",
    )
    try:
        payload = TestClient(app).get("/config").json()
    finally:
        app.dependency_overrides.clear()

    assert payload["tts_providers"] == ["voicevox", "aivis", "http"]


@pytest.mark.anyio
async def test_http_tts_posts_json_and_persists_audio(tmp_path: Path) -> None:
    captured_request: httpx.Request | None = None

    def handler(request: httpx.Request) -> httpx.Response:
        nonlocal captured_request
        captured_request = request
        return httpx.Response(200, content=b"RIFF-yui-audio", headers={"content-type": "audio/wav"})

    settings = Settings(
        tts_provider="http",
        http_tts_base_url="https://tts.example.test",
        http_tts_endpoint="/v3/tts",
        http_tts_api_key="test-key",
        http_tts_provider_id="irodori",
        http_tts_payload_format="generic",
        http_tts_voice="yui",
        http_tts_model="v3",
        http_tts_gender="",
        http_tts_instruct="",
        http_tts_lang_code="",
        http_tts_format="wav",
        http_tts_audio_processor="none",
    )
    provider = HttpTTSProvider(
        settings,
        audio_dir=tmp_path,
        transport=httpx.MockTransport(handler),
    )

    response = await provider.synthesize(TTSRequest(text="こんにちは", speed_scale=1.1))

    assert response.audio_url.startswith("/audio/http_")
    assert response.format == "wav"
    files = list(tmp_path.glob("http_*.wav"))
    assert len(files) == 1
    assert files[0].read_bytes() == b"RIFF-yui-audio"
    assert captured_request is not None
    assert captured_request.url == "https://tts.example.test/v3/tts"
    assert captured_request.headers["authorization"] == "Bearer test-key"
    assert captured_request.headers["content-type"] == "application/json"
    assert json.loads(captured_request.content) == {
        "provider": "irodori",
        "text": "こんにちは",
        "voice": "yui",
        "model": "v3",
        "format": "wav",
        "speaker_id": 14,
        "speed_scale": 1.1,
        "pitch_scale": 0.0,
        "intonation_scale": 1.0,
        "volume_scale": 1.0,
    }


@pytest.mark.anyio
async def test_http_tts_can_post_openai_speech_payload_for_irodori(tmp_path: Path) -> None:
    captured_payloads: list[dict[str, object]] = []

    def handler(request: httpx.Request) -> httpx.Response:
        captured_payloads.append(json.loads(request.content))
        return httpx.Response(200, content=b"RIFF-irodori-audio", headers={"content-type": "audio/wav"})

    provider = HttpTTSProvider(
        Settings(
            tts_provider="http",
            http_tts_base_url="http://127.0.0.1:41080",
            http_tts_endpoint="/v1/audio/speech",
            http_tts_provider_id="irodori",
            http_tts_payload_format="openai_speech",
            http_tts_voice="yui",
            http_tts_model="mlx-community/Irodori-TTS-600M-v3-VoiceDesign-8bit",
            http_tts_gender="female",
            http_tts_instruct="若い女性の、明るく可愛いアニメ調の声で話してください。",
            http_tts_lang_code="ja",
            http_tts_format="wav",
            http_tts_audio_processor="none",
        ),
        audio_dir=tmp_path,
        transport=httpx.MockTransport(handler),
    )

    response = await provider.synthesize(
        TTSRequest(text="こんにちは", speed_scale=1.15, pitch_scale=0.05)
    )

    assert response.format == "wav"
    assert (tmp_path / captured_payloads[1]["ref_audio"]).exists()
    assert len(captured_payloads) == 2
    assert captured_payloads[0] == {
        "model": "mlx-community/Irodori-TTS-600M-v3-VoiceDesign-8bit",
        "input": "こんにちは、声の基準を作ります。",
        "voice": "yui",
        "response_format": "wav",
        "speed": 1.0,
        "pitch": 1.0,
        "gender": "female",
        "instruct": "若い女性の、明るく可愛いアニメ調の声で話してください。",
        "lang_code": "ja",
    }
    assert captured_payloads[1] == {
        "model": "mlx-community/Irodori-TTS-600M-v3-VoiceDesign-8bit",
        "input": "こんにちは",
        "voice": "yui",
        "response_format": "wav",
        "speed": 1.0,
        "pitch": 1.0,
        "gender": "female",
        "instruct": "若い女性の、明るく可愛いアニメ調の声で話してください。",
        "lang_code": "ja",
        "ref_audio": captured_payloads[1]["ref_audio"],
        "ref_text": "こんにちは、声の基準を作ります。",
    }


@pytest.mark.anyio
async def test_http_tts_openai_speech_request_instruct_overrides_backend_defaults(tmp_path: Path) -> None:
    captured_payloads: list[dict[str, object]] = []

    def handler(request: httpx.Request) -> httpx.Response:
        captured_payloads.append(json.loads(request.content))
        return httpx.Response(200, content=b"RIFF-irodori-audio", headers={"content-type": "audio/wav"})

    provider = HttpTTSProvider(
        Settings(
            tts_provider="http",
            http_tts_base_url="http://127.0.0.1:41080",
            http_tts_endpoint="/v1/audio/speech",
            http_tts_provider_id="irodori",
            http_tts_payload_format="openai_speech",
            http_tts_model="mlx-community/Irodori-TTS-600M-v3-VoiceDesign-8bit",
            http_tts_gender="female",
            http_tts_instruct="若い女性の声で話してください。",
            http_tts_lang_code="ja",
            http_tts_format="wav",
            http_tts_audio_processor="none",
        ),
        audio_dir=tmp_path,
        transport=httpx.MockTransport(handler),
    )

    await provider.synthesize(
        TTSRequest(
            text="テストです",
            speed_scale=1.25,
            pitch_scale=-0.08,
            voice_gender="male",
            voice_instruct="低く落ち着いた男性アバターの声で話してください。",
            voice_lang_code="ja",
        )
    )

    assert len(captured_payloads) == 2
    captured_payload = captured_payloads[1]
    assert captured_payload["speed"] == 1.0
    assert captured_payload["pitch"] == 1.0
    assert captured_payload["gender"] == "male"
    assert captured_payload["instruct"] == "低く落ち着いた男性アバターの声で話してください。"
    assert captured_payload["ref_audio"]


@pytest.mark.anyio
async def test_http_tts_can_post_irodori_openai_server_payload(tmp_path: Path) -> None:
    captured_payloads: list[dict[str, object]] = []

    def handler(request: httpx.Request) -> httpx.Response:
        captured_payloads.append(json.loads(request.content))
        return httpx.Response(200, content=b"RIFF-irodori-audio", headers={"content-type": "audio/wav"})

    provider = HttpTTSProvider(
        Settings(
            tts_provider="http",
            http_tts_base_url="http://127.0.0.1:8088",
            http_tts_endpoint="/v1/audio/speech",
            http_tts_provider_id="irodori-server",
            http_tts_payload_format="irodori_openai_speech",
            http_tts_voice="none",
            http_tts_model="irodori-tts",
            http_tts_gender="female",
            http_tts_instruct="若い女性の、明るく聞き取りやすい声で話してください。",
            http_tts_lang_code="ja",
            http_tts_format="wav",
            http_tts_audio_processor="none",
            http_tts_irodori_num_steps=16,
            http_tts_irodori_seed=1234,
            http_tts_irodori_cfg_scale_caption=4.0,
            http_tts_irodori_chunking_enabled=False,
            http_tts_irodori_chunk_min_chars=120,
        ),
        audio_dir=tmp_path,
        transport=httpx.MockTransport(handler),
    )

    await provider.synthesize(TTSRequest(text="こんにちは", speed_scale=1.0, pitch_scale=-0.1))

    assert len(captured_payloads) == 2
    assert captured_payloads[0] == {
        "model": "irodori-tts",
        "input": "こんにちは、声の基準を作ります。",
        "response_format": "wav",
        "voice": "none",
        "speed": 1.0,
        "gender": "female",
        "instruct": "若い女性の、明るく聞き取りやすい声で話してください。",
        "lang_code": "ja",
        "caption": "若い女性の、明るく聞き取りやすい声で話してください。",
        "irodori": {
            "chunking_enabled": False,
            "caption": "若い女性の、明るく聞き取りやすい声で話してください。",
            "num_steps": 16,
            "seed": 1234,
            "cfg_scale_caption": 4.0,
            "chunk_min_chars": 120,
        },
    }
    assert captured_payloads[1]["irodori"]["chunking_enabled"] is False
    assert captured_payloads[1]["irodori"]["num_steps"] == 16
    assert captured_payloads[1]["irodori"]["seed"] == 1234
    assert captured_payloads[1]["ref_audio"]
    assert "pitch" not in captured_payloads[1]


def test_http_tts_rejects_unsupported_audio_format(tmp_path: Path) -> None:
    provider = HttpTTSProvider(
        Settings(
            tts_provider="http",
            http_tts_base_url="https://tts.example.test",
            http_tts_format="../wav",
        ),
        audio_dir=tmp_path,
        transport=httpx.MockTransport(lambda _: httpx.Response(200, content=b"")),
    )

    try:
        provider._audio_format()
    except TTSProviderError as exc:
        assert "HTTP_TTS_FORMAT" in str(exc)
    else:
        raise AssertionError("Expected unsupported HTTP_TTS_FORMAT to be rejected")


def test_http_tts_wav_postprocess_changes_audio_when_speed_or_pitch_is_requested() -> None:
    source = tmp_path = Path("/tmp/yui-http-tts-postprocess-source.wav")
    with wave.open(str(source), "wb") as writer:
        writer.setnchannels(1)
        writer.setsampwidth(2)
        writer.setframerate(24000)
        writer.writeframes(b"\x00\x00" * 24000)

    content = source.read_bytes()
    processed = HttpTTSProvider._postprocess_audio(
        content,
        TTSRequest(text="hello", speed_scale=1.0, pitch_scale=-0.5),
        "wav",
    )

    assert processed != content
    with wave.open(str(source), "rb") as original:
        original_frames = original.getnframes()
    processed_path = tmp_path.with_name("yui-http-tts-postprocess-processed.wav")
    processed_path.write_bytes(processed)
    with wave.open(str(processed_path), "rb") as changed:
        assert changed.getframerate() == 24000
        assert changed.getnframes() > original_frames


def test_http_tts_trim_wav_silence_limits_leading_and_trailing_pauses(tmp_path: Path) -> None:
    source = tmp_path / "source.wav"
    with wave.open(str(source), "wb") as writer:
        writer.setnchannels(1)
        writer.setsampwidth(2)
        writer.setframerate(1000)
        writer.writeframes(b"\x00\x00" * 1000)
        writer.writeframes(b"\x00\x20" * 1000)
        writer.writeframes(b"\x00\x00" * 1000)

    trimmed = HttpTTSProvider._trim_wav_silence(source.read_bytes(), keep_ms=100)
    trimmed_path = tmp_path / "trimmed.wav"
    trimmed_path.write_bytes(trimmed)

    with wave.open(str(trimmed_path), "rb") as changed:
        assert changed.getnframes() == 1200


def test_http_tts_trim_wav_silence_leaves_audio_without_edge_silence_unchanged(tmp_path: Path) -> None:
    source = tmp_path / "source.wav"
    with wave.open(str(source), "wb") as writer:
        writer.setnchannels(1)
        writer.setsampwidth(2)
        writer.setframerate(1000)
        writer.writeframes(b"\x00\x20" * 1000)

    content = source.read_bytes()
    assert HttpTTSProvider._trim_wav_silence(content, keep_ms=100) == content


def test_http_tts_normalize_wav_peak_reduces_near_clipping(tmp_path: Path) -> None:
    source = tmp_path / "source.wav"
    with wave.open(str(source), "wb") as writer:
        writer.setnchannels(1)
        writer.setsampwidth(2)
        writer.setframerate(1000)
        writer.writeframes((32000).to_bytes(2, "little", signed=True) * 100)

    normalized = HttpTTSProvider._normalize_wav_peak(source.read_bytes(), target_peak_db=-3.0)
    normalized_path = tmp_path / "normalized.wav"
    normalized_path.write_bytes(normalized)

    with wave.open(str(normalized_path), "rb") as reader:
        samples = reader.readframes(reader.getnframes())

    assert audioop.max(samples, 2) < 32000


def test_http_tts_irodori_neutral_postprocess_trims_and_normalizes(tmp_path: Path) -> None:
    source = tmp_path / "source.wav"
    with wave.open(str(source), "wb") as writer:
        writer.setnchannels(1)
        writer.setsampwidth(2)
        writer.setframerate(1000)
        writer.writeframes(b"\x00\x00" * 1000)
        writer.writeframes((32000).to_bytes(2, "little", signed=True) * 1000)
        writer.writeframes(b"\x00\x00" * 1000)

    processed = HttpTTSProvider._postprocess_audio(
        source.read_bytes(),
        TTSRequest(text="こんにちは", speed_scale=1.0, pitch_scale=0.0),
        "wav",
        protect_quality=True,
        audio_processor="none",
    )
    processed_path = tmp_path / "processed.wav"
    processed_path.write_bytes(processed)

    with wave.open(str(processed_path), "rb") as reader:
        frames = reader.getnframes()
        samples = reader.readframes(frames)

    assert frames == 1240
    assert audioop.max(samples, 2) < 32000


def test_http_tts_irodori_postprocess_uses_soundstretch_when_configured(tmp_path: Path) -> None:
    source = tmp_path / "source.wav"
    with wave.open(str(source), "wb") as writer:
        writer.setnchannels(1)
        writer.setsampwidth(2)
        writer.setframerate(24000)
        writer.writeframes(b"\x00\x00" * 24000)

    tool_log = tmp_path / "soundstretch-args.json"
    tool = tmp_path / "soundstretch"
    tool.write_text(
        "#!/usr/bin/env python3\n"
        "import json, shutil, sys\n"
        f"open({str(tool_log)!r}, 'w').write(json.dumps(sys.argv[1:]))\n"
        "shutil.copyfile(sys.argv[1], sys.argv[2])\n",
        encoding="utf-8",
    )
    tool.chmod(tool.stat().st_mode | 0o111)

    content = source.read_bytes()
    processed = HttpTTSProvider._postprocess_audio(
        content,
        TTSRequest(text="hello", speed_scale=1.15, pitch_scale=-0.5),
        "wav",
        protect_quality=True,
        audio_processor="soundstretch",
        soundstretch_path=str(tool),
    )

    assert processed == content
    args = json.loads(tool_log.read_text(encoding="utf-8"))
    assert args[2:] == ["-tempo=15.000", "-pitch=-6.000"]


def test_http_tts_irodori_pitch_and_speed_are_not_added_to_voice_design_instruction(tmp_path: Path) -> None:
    provider = HttpTTSProvider(
        Settings(
            tts_provider="http",
            http_tts_base_url="http://127.0.0.1:41080",
            http_tts_endpoint="/v1/audio/speech",
            http_tts_provider_id="irodori",
            http_tts_payload_format="openai_speech",
            http_tts_model="mlx-community/Irodori-TTS-600M-v3-VoiceDesign-8bit",
            http_tts_gender="female",
            http_tts_instruct="若い女性の声で話してください。",
            http_tts_lang_code="ja",
            http_tts_format="wav",
        ),
        audio_dir=tmp_path,
        transport=httpx.MockTransport(lambda _: httpx.Response(200, content=b"RIFF")),
    )

    payload = provider._payload(
        TTSRequest(text="hello", speed_scale=0.75, pitch_scale=-0.4),
        "wav",
    )

    assert payload["instruct"] == "若い女性の声で話してください。"


def test_http_tts_soundstretch_postprocess_is_skipped_when_tool_is_unavailable(tmp_path: Path) -> None:
    source = tmp_path / "source.wav"
    with wave.open(str(source), "wb") as writer:
        writer.setnchannels(1)
        writer.setsampwidth(2)
        writer.setframerate(24000)
        writer.writeframes(b"\x00\x00" * 24000)

    content = source.read_bytes()
    processed = HttpTTSProvider._postprocess_audio(
        content,
        TTSRequest(text="hello", speed_scale=0.8, pitch_scale=-0.5),
        "wav",
        protect_quality=True,
        audio_processor="soundstretch",
        soundstretch_path=str(tmp_path / "missing-soundstretch"),
    )

    assert processed == content


def test_http_tts_resolves_bundled_soundstretch_from_repo_tools(tmp_path: Path) -> None:
    tool = tmp_path / "tools" / "tts" / "soundtouch" / "bin" / "soundstretch"
    tool.parent.mkdir(parents=True)
    tool.write_text("#!/usr/bin/env sh\n", encoding="utf-8")
    tool.chmod(tool.stat().st_mode | 0o111)

    assert HttpTTSProvider._resolve_soundstretch_path("", bundled_root=tmp_path) == str(tool)


def test_http_tts_cache_key_separates_audio_processor_settings() -> None:
    request = TTSRequest(text="same text", speed_scale=1.0, pitch_scale=-0.3)
    base = Settings(
        tts_provider="http",
        http_tts_base_url="http://127.0.0.1:41080",
        http_tts_endpoint="/v1/audio/speech",
        http_tts_provider_id="irodori",
        http_tts_payload_format="openai_speech",
        http_tts_model="mlx-community/Irodori-TTS-600M-v3-VoiceDesign-8bit",
        http_tts_format="wav",
        http_tts_audio_processor="none",
    )
    processed = Settings(
        tts_provider="http",
        http_tts_base_url="http://127.0.0.1:41080",
        http_tts_endpoint="/v1/audio/speech",
        http_tts_provider_id="irodori",
        http_tts_payload_format="openai_speech",
        http_tts_model="mlx-community/Irodori-TTS-600M-v3-VoiceDesign-8bit",
        http_tts_format="wav",
        http_tts_audio_processor="soundstretch",
        http_tts_soundstretch_path="/opt/homebrew/bin/soundstretch",
    )

    assert HttpTTSProvider._cache_key(request, base) != HttpTTSProvider._cache_key(request, processed)


def test_http_tts_cache_key_separates_irodori_server_settings() -> None:
    request = TTSRequest(text="same text", speed_scale=1.0, pitch_scale=0.0)
    base = Settings(
        tts_provider="http",
        http_tts_base_url="http://127.0.0.1:8088",
        http_tts_endpoint="/v1/audio/speech",
        http_tts_provider_id="irodori-server",
        http_tts_payload_format="irodori_openai_speech",
        http_tts_model="irodori-tts",
        http_tts_format="wav",
        http_tts_irodori_num_steps=16,
    )
    slower = Settings(
        tts_provider="http",
        http_tts_base_url="http://127.0.0.1:8088",
        http_tts_endpoint="/v1/audio/speech",
        http_tts_provider_id="irodori-server",
        http_tts_payload_format="irodori_openai_speech",
        http_tts_model="irodori-tts",
        http_tts_format="wav",
        http_tts_irodori_num_steps=24,
    )

    assert HttpTTSProvider._cache_key(request, base) != HttpTTSProvider._cache_key(request, slower)


@pytest.mark.anyio
async def test_http_tts_enforces_cache_limit_without_touching_voice_references(tmp_path: Path) -> None:
    counter = 0

    def handler(request: httpx.Request) -> httpx.Response:
        nonlocal counter
        counter += 1
        return httpx.Response(
            200,
            content=f"RIFF-http-audio-{counter}".encode("utf-8"),
            headers={"content-type": "audio/wav"},
        )

    provider = HttpTTSProvider(
        Settings(
            tts_provider="http",
            http_tts_base_url="https://tts.example.test",
            http_tts_payload_format="generic",
            http_tts_format="wav",
            http_tts_audio_processor="none",
            tts_audio_cache_max_files=2,
            tts_audio_cache_max_mb=0,
            tts_audio_cache_max_age_hours=0,
        ),
        audio_dir=tmp_path,
        transport=httpx.MockTransport(handler),
    )

    voice_reference = tmp_path / "http_voice_ref_keep.wav"
    voice_reference.write_bytes(b"voice reference")

    for index in range(3):
        await provider.synthesize(TTSRequest(text=f"cache item {index}"))

    cached = sorted(path.name for path in tmp_path.glob("http_*.wav"))
    assert len(cached) == 3
    assert "http_voice_ref_keep.wav" in cached
    assert len([name for name in cached if not name.startswith("http_voice_ref_")]) == 2
