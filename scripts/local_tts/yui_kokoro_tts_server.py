#!/usr/bin/env python3
from __future__ import annotations

import argparse
import asyncio
import io
import json
import os
import wave
from pathlib import Path
from typing import Any

import numpy as np
from fastapi import FastAPI, HTTPException
from fastapi.responses import Response
from pydantic import BaseModel


class SpeechRequest(BaseModel):
    model: str | None = None
    input: str | None = None
    text: str | None = None
    voice: str | None = None
    language: str | None = None
    lang: str | None = None
    lang_code: str | None = None
    response_format: str | None = None
    format: str | None = None
    speed: float | None = None
    speed_scale: float | None = None


class ServerState:
    kokoro: Any | None = None
    default_voice: str = "af_heart"
    sample_rate: int = 24000


state = ServerState()
app = FastAPI(title="Yui Kokoro ONNX TTS")


@app.get("/health")
def health() -> dict[str, Any]:
    return {
        "ok": True,
        "provider": "kokoro-onnx",
        "voice": state.default_voice,
        "supported_languages": ["en-us", "en-gb"],
        "japanese_supported": False,
    }


@app.get("/v1/models")
def models() -> dict[str, Any]:
    return {
        "object": "list",
        "data": [
            {
                "id": "kokoro-82m-onnx",
                "object": "model",
                "owned_by": "local",
            }
        ],
    }


@app.post("/tts")
async def generic_tts(request: SpeechRequest) -> Response:
    return await synthesize(request)


@app.post("/v1/audio/speech")
async def openai_speech(request: SpeechRequest) -> Response:
    return await synthesize(request)


async def synthesize(request: SpeechRequest) -> Response:
    text = (request.input or request.text or "").strip()
    if not text:
        raise HTTPException(status_code=400, detail="input/text is required")

    language = normalize_language(request.language or request.lang or request.lang_code)
    if contains_japanese(text) or language.startswith("ja"):
        raise HTTPException(
            status_code=422,
            detail=(
                "This Kokoro ONNX v1.0 audition server uses the kokoro-onnx "
                "English phonemizer path only. Japanese Yui voice auditions "
                "must use Irodori/Aivis or another Japanese-capable TTS."
            ),
        )

    audio_format = (request.response_format or request.format or "wav").lower()
    if audio_format not in {"wav", "pcm"}:
        raise HTTPException(status_code=400, detail="Only wav/pcm is supported by this local Kokoro server")

    voice = (request.voice or state.default_voice).strip() or state.default_voice
    speed = request.speed if request.speed is not None else request.speed_scale
    speed = float(speed if speed is not None else 1.0)
    speed = max(0.5, min(1.8, speed))

    audio, sample_rate = await asyncio.to_thread(generate_audio, text, voice, speed, language)
    wav_bytes = to_wav_bytes(audio, sample_rate)
    return Response(content=wav_bytes, media_type="audio/wav")


def generate_audio(text: str, voice: str, speed: float, language: str) -> tuple[np.ndarray, int]:
    if state.kokoro is None:
        raise RuntimeError("Kokoro model is not loaded")
    audio, sample_rate = state.kokoro.create(text, voice=voice, speed=speed, lang=language)
    audio = np.asarray(audio, dtype=np.float32)
    return audio, int(sample_rate or state.sample_rate)


def normalize_language(language: str | None) -> str:
    value = (language or "en-us").strip().lower().replace("_", "-")
    if value in {"en", "en-us", "us"}:
        return "en-us"
    if value in {"en-gb", "gb", "uk"}:
        return "en-gb"
    return value


def contains_japanese(text: str) -> bool:
    return any(
        "\u3040" <= char <= "\u30ff"
        or "\u3400" <= char <= "\u4dbf"
        or "\u4e00" <= char <= "\u9fff"
        for char in text
    )


def to_wav_bytes(audio: np.ndarray, sample_rate: int) -> bytes:
    audio = np.nan_to_num(audio, nan=0.0, posinf=0.0, neginf=0.0)
    audio = np.clip(audio, -1.0, 1.0)
    pcm = (audio * 32767.0).astype(np.int16)
    buffer = io.BytesIO()
    with wave.open(buffer, "wb") as writer:
        writer.setnchannels(1)
        writer.setsampwidth(2)
        writer.setframerate(sample_rate)
        writer.writeframes(pcm.tobytes())
    return buffer.getvalue()


def resolve_default_path(env_name: str, default: Path) -> Path:
    value = os.environ.get(env_name)
    return Path(value).expanduser() if value else default


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--host", default=os.environ.get("KOKORO_HOST", "127.0.0.1"))
    parser.add_argument("--port", type=int, default=int(os.environ.get("KOKORO_PORT", "41081")))
    parser.add_argument("--model", type=Path, default=resolve_default_path("KOKORO_MODEL", Path.home() / ".cache/yui-vrm-ai-studio/kokoro/kokoro-v1.0.onnx"))
    parser.add_argument("--voices", type=Path, default=resolve_default_path("KOKORO_VOICES", Path.home() / ".cache/yui-vrm-ai-studio/kokoro/voices-v1.0.bin"))
    parser.add_argument("--voice", default=os.environ.get("KOKORO_VOICE", "af_heart"))
    args = parser.parse_args()

    from kokoro_onnx import Kokoro
    import uvicorn

    if not args.model.exists():
        raise FileNotFoundError(f"Kokoro model not found: {args.model}")
    if not args.voices.exists():
        raise FileNotFoundError(f"Kokoro voices not found: {args.voices}")

    state.default_voice = args.voice
    state.kokoro = Kokoro(str(args.model), str(args.voices))
    uvicorn.run(app, host=args.host, port=args.port, log_level="info")


if __name__ == "__main__":
    main()
