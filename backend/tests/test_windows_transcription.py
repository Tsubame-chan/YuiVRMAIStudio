"""Windows STT regression checks; no model download or native dependencies."""
import array
import base64
import importlib.util
import io
import json
from pathlib import Path
import sys
import subprocess
from types import SimpleNamespace
from unittest.mock import patch
import wave

import pytest


spec = importlib.util.spec_from_file_location(
    "windows_stt_worker", Path(__file__).resolve().parents[2] / "scripts/yui_desktop_inference.py")
worker = importlib.util.module_from_spec(spec)
spec.loader.exec_module(worker)


def wav_bytes(rate=48000, seconds=3, channels=1, amplitude=0):
    output = io.BytesIO()
    samples = array.array("h", [amplitude, -amplitude]) * (rate * seconds * channels // 2)
    if sys.byteorder != "little":
        samples.byteswap()
    with wave.open(output, "wb") as wav:
        wav.setnchannels(channels)
        wav.setsampwidth(2)
        wav.setframerate(rate)
        wav.writeframes(samples.tobytes())
    return output.getvalue()


def test_cpu_is_selected_only_for_windows_transcription():
    lm = SimpleNamespace(Backend=SimpleNamespace(CPU=lambda: "cpu", GPU=lambda: "gpu"))
    assert worker.inference_backends(lm, "Transcription", "win32") == ("cpu",)
    assert worker.inference_backends(lm, "Chat", "win32") == ("gpu", "cpu")
    assert worker.inference_backends(lm, "Transcription", "darwin") == ("gpu", "cpu")


@pytest.mark.parametrize("rate,channels", [(16000, 1), (48000, 1), (48000, 2)])
def test_duration_is_measured_from_actual_wav_frames(rate, channels):
    duration, actual_rate, actual_channels, rms, peak = worker.transcription_audio_info(
        wav_bytes(rate, channels=channels, amplitude=32767))
    assert duration == 3
    assert (actual_rate, actual_channels) == (rate, channels)
    assert .999 < rms <= 1 and .999 < peak <= 1


def test_truncated_audio_is_rejected():
    with pytest.raises(ValueError, match="truncated"):
        worker.transcription_audio_info(wav_bytes()[:-2])


def test_three_second_silence_never_reaches_model(tmp_path):
    request = dict(capability="Transcription", model_path=str(tmp_path / "model"),
                   cache_directory=str(tmp_path / "cache"),
                   payload_json=json.dumps(dict(AudioBytes=base64.b64encode(wav_bytes()).decode())))
    (tmp_path / "model").touch()
    # Deliberately no Engine API: silence must return without loading inference.
    with patch.dict(sys.modules, litert_lm=SimpleNamespace()), patch.object(worker.sys, "platform", "win32"):
        result = worker.infer(request)
    assert result["Success"] and result["Text"] == "" and not result["ShouldTts"]


def test_short_utterance_rejects_reported_341_character_hallucination():
    assert worker.validate_transcript("こんにちは、お元気ですか", 3) == "こんにちは、お元気ですか"
    with pytest.raises(ValueError, match="too long"):
        worker.validate_transcript("でたらめ" * 86, 3)
    # Long recordings can legitimately contain more than 341 characters.
    assert worker.validate_transcript("文章" * 200, 60) == "文章" * 200


def test_inference_failure_does_not_retry_a_different_backend(tmp_path):
    calls = []
    class CPU:
        pass
    class GPU:
        pass
    class Engine:
        def __init__(self, *args, **kwargs):
            calls.append(type(kwargs["backend"]))
        def __enter__(self):
            raise RuntimeError("initialization failed")
    lm = SimpleNamespace(Engine=Engine, Backend=SimpleNamespace(CPU=CPU, GPU=GPU))
    (tmp_path / "model").touch()
    request = dict(capability="Transcription", model_path=str(tmp_path / "model"),
                   cache_directory=str(tmp_path / "cache"),
                   payload_json=json.dumps(dict(AudioBytes=base64.b64encode(wav_bytes(amplitude=3000)).decode())))
    with patch.dict(sys.modules, litert_lm=lm), patch.object(worker.sys, "platform", "win32"):
        with pytest.raises(RuntimeError, match="initialization failed"):
            worker.infer(request)
    assert calls == [CPU]


@pytest.mark.skipif(sys.platform != "win32", reason="Windows native STT regression")
def test_native_short_japanese_audio_when_requested():
    import os
    audio = os.environ.get("YUI_NATIVE_STT_WAV")
    model = os.environ.get("YUI_NATIVE_STT_MODEL")
    python = os.environ.get("YUI_NATIVE_STT_PYTHON")
    if not all((audio, model, python)):
        pytest.skip("Set YUI_NATIVE_STT_WAV, YUI_NATIVE_STT_MODEL and YUI_NATIVE_STT_PYTHON")
    import tempfile
    with tempfile.TemporaryDirectory() as cache:
        request = dict(capability="Transcription", model_path=model, cache_directory=cache,
                       payload_json=json.dumps(dict(AudioBytes=base64.b64encode(Path(audio).read_bytes()).decode(), LanguageCode="ja")))
        run = subprocess.run([python, str(Path(worker.__file__))], input=json.dumps(request),
                             capture_output=True, text=True, encoding="utf-8", errors="replace", timeout=120,
                             env=dict(os.environ, PYTHONUTF8="1", PYTHONIOENCODING="utf-8"))
        result = json.loads(run.stdout)
        assert result["ok"], result
        transcript = json.loads(result["payload_json"])["Text"]
        assert os.environ.get("YUI_NATIVE_STT_EXPECTED", "お元気ですか") in transcript
