#!/usr/bin/env python3
from __future__ import annotations

import argparse
import audioop
import base64
import json
import math
import os
import platform
import shutil
import subprocess
import statistics
import time
import urllib.error
import urllib.parse
import urllib.request
import wave
from dataclasses import asdict, dataclass
from datetime import datetime
from pathlib import Path
from typing import Any


ROOT = Path(__file__).resolve().parents[1]
DEFAULT_OUTPUT_ROOT = ROOT / "benchmarks" / "tts"

DEFAULT_TEXT_PRESETS = {
    "short_a": "こんにちは。",
    "short_b": "今日は天気がいい。だから散歩に行こう。",
    "medium_c": (
        "ほどほどに元気ならよかった。開発は頭も使うし、結構しんどいよね。"
        "無理しすぎないでね。少し休憩しながら進めよっか。"
    ),
    "long_d": (
        "おすすめは、通常会話では安定した音声を使い、高品質な声が必要な場面だけIrodoriを使う構成です。"
        "短いセリフや感情表現には向いていますが、長い説明をすべて読み上げる用途では待ち時間が目立ちます。"
        "そのため、画面には全文を表示し、音声では要点だけを自然に話す設計が現実的です。"
    ),
}

DEFAULT_IRODORI_INSTRUCT = "若い女性の、明るく聞き取りやすいアニメ調の声で話してください。"
DEFAULT_IRODORI_REF_TEXT = "こんにちは、声の基準を作ります。"


@dataclass
class AudioMetrics:
    duration_ms: int | None
    sample_rate: int | None
    channels: int | None
    bits_per_sample: int | None
    rms_dbfs: float | None
    peak_dbfs: float | None
    leading_silence_ms: int | None
    trailing_silence_ms: int | None
    clipping_count: int | None
    bytes: int


@dataclass
class BenchResult:
    run_id: str
    engine: str
    text_id: str
    chars: int
    iteration: int
    elapsed_ms: int
    rtf: float | None
    cache_hit: bool | None
    phase: str
    output_path: str
    request: dict[str, Any]
    response: dict[str, Any]
    audio: AudioMetrics
    error: str | None = None


def now_run_id() -> str:
    return datetime.now().strftime("%Y%m%d-%H%M%S")


def collect_environment() -> dict[str, Any]:
    env: dict[str, Any] = {
        "os": platform.system(),
        "os_release": platform.release(),
        "os_version": platform.version(),
        "machine": platform.machine(),
        "processor": platform.processor(),
        "python": platform.python_version(),
    }
    env["docker"] = command_output(["docker", "--version"])
    env["nvidia_smi"] = command_output(
        ["nvidia-smi", "--query-gpu=name,memory.total,driver_version", "--format=csv,noheader"],
        timeout=5,
    )
    env["cuda_available_hint"] = bool(env["nvidia_smi"])
    return env


def command_output(command: list[str], *, timeout: float = 3.0) -> str:
    if not shutil.which(command[0]):
        return ""
    try:
        completed = subprocess.run(
            command,
            check=False,
            stdout=subprocess.PIPE,
            stderr=subprocess.STDOUT,
            text=True,
            timeout=timeout,
        )
    except (OSError, subprocess.SubprocessError):
        return ""
    return completed.stdout.strip()


def post_json(url: str, payload: dict[str, Any], timeout: float) -> tuple[bytes, str, int, dict[str, str]]:
    data = json.dumps(payload, ensure_ascii=False).encode("utf-8")
    request = urllib.request.Request(
        url,
        data=data,
        headers={"Content-Type": "application/json"},
        method="POST",
    )
    with urllib.request.urlopen(request, timeout=timeout) as response:
        return (
            response.read(),
            response.headers.get("Content-Type", ""),
            response.status,
            {key: value for key, value in response.headers.items()},
        )


def get_bytes(url: str, timeout: float) -> bytes:
    with urllib.request.urlopen(url, timeout=timeout) as response:
        return response.read()


def save_response_audio(
    *,
    body: bytes,
    content_type: str,
    output_path: Path,
) -> dict[str, Any]:
    response_info: dict[str, Any] = {"content_type": content_type}
    media_type = content_type.split(";", 1)[0].strip().lower()
    if media_type in {"audio/wav", "audio/x-wav", "audio/mpeg", "audio/ogg"}:
        output_path.write_bytes(body)
        response_info["mode"] = "audio_body"
        return response_info

    try:
        payload = json.loads(body.decode("utf-8"))
    except (UnicodeDecodeError, json.JSONDecodeError):
        output_path.write_bytes(body)
        response_info["mode"] = "raw_body"
        return response_info

    response_info["json"] = payload
    audio_url = payload.get("audio_url")
    if isinstance(audio_url, str):
        response_info["mode"] = "json_audio_url"
        response_info["audio_url"] = audio_url
        return response_info

    audio_base64 = payload.get("audio_base64")
    if isinstance(audio_base64, str):
        output_path.write_bytes(base64.b64decode(audio_base64))
        response_info["mode"] = "json_audio_base64"
        return response_info

    output_path.write_bytes(body)
    response_info["mode"] = "json_no_audio"
    return response_info


def fetch_backend_audio(
    *,
    backend_base_url: str,
    audio_url: str,
    output_path: Path,
    timeout: float,
) -> None:
    if audio_url.startswith("http://") or audio_url.startswith("https://"):
        url = audio_url
    else:
        url = backend_base_url.rstrip("/") + "/" + audio_url.lstrip("/")
    output_path.write_bytes(get_bytes(url, timeout))


def wav_metrics(path: Path) -> AudioMetrics:
    content = path.read_bytes()
    try:
        with wave.open(str(path), "rb") as reader:
            channels = reader.getnchannels()
            sample_width = reader.getsampwidth()
            sample_rate = reader.getframerate()
            frames = reader.getnframes()
            data = reader.readframes(frames)
    except wave.Error:
        return AudioMetrics(
            duration_ms=None,
            sample_rate=None,
            channels=None,
            bits_per_sample=None,
            rms_dbfs=None,
        peak_dbfs=None,
        leading_silence_ms=None,
        trailing_silence_ms=None,
        clipping_count=None,
        bytes=len(content),
    )

    duration_ms = int(round(frames / sample_rate * 1000)) if sample_rate else None
    full_scale = (2 ** (8 * sample_width - 1)) - 1
    rms = audioop.rms(data, sample_width) if data else 0
    peak = audioop.max(data, sample_width) if data else 0
    rms_dbfs = 20 * math.log10(rms / full_scale) if rms else -999.0
    peak_dbfs = 20 * math.log10(peak / full_scale) if peak else -999.0
    leading = edge_silence_ms(data, sample_width, sample_rate, channels, from_start=True)
    trailing = edge_silence_ms(data, sample_width, sample_rate, channels, from_start=False)
    clipping_count = count_clipped_samples(data, sample_width)
    return AudioMetrics(
        duration_ms=duration_ms,
        sample_rate=sample_rate,
        channels=channels,
        bits_per_sample=sample_width * 8,
        rms_dbfs=round(rms_dbfs, 2),
        peak_dbfs=round(peak_dbfs, 2),
        leading_silence_ms=leading,
        trailing_silence_ms=trailing,
        clipping_count=clipping_count,
        bytes=len(content),
    )


def edge_silence_ms(
    data: bytes,
    sample_width: int,
    sample_rate: int,
    channels: int,
    *,
    from_start: bool,
    threshold_dbfs: float = -45.0,
    window_ms: int = 10,
) -> int:
    if not data or sample_rate <= 0 or channels <= 0:
        return 0
    full_scale = (2 ** (8 * sample_width - 1)) - 1
    threshold = int(full_scale * (10 ** (threshold_dbfs / 20)))
    bytes_per_window = max(1, int(sample_rate * window_ms / 1000)) * channels * sample_width
    windows = 0
    if from_start:
        iterator = range(0, len(data), bytes_per_window)
        for offset in iterator:
            if audioop.rms(data[offset : offset + bytes_per_window], sample_width) <= threshold:
                windows += 1
            else:
                break
    else:
        for end in range(len(data), 0, -bytes_per_window):
            start = max(0, end - bytes_per_window)
            if audioop.rms(data[start:end], sample_width) <= threshold:
                windows += 1
            else:
                break
    return windows * window_ms


def count_clipped_samples(data: bytes, sample_width: int) -> int:
    if not data or sample_width != 2:
        return 0
    high = (32767).to_bytes(2, "little", signed=True)
    low = (-32768).to_bytes(2, "little", signed=True)
    return sum(
        1
        for offset in range(0, len(data) - 1, 2)
        if data[offset : offset + 2] in {high, low}
    )


def write_mock_wav(path: Path, *, duration_ms: int = 600, sample_rate: int = 48000) -> None:
    frames = int(sample_rate * duration_ms / 1000)
    amplitude = 6000
    with wave.open(str(path), "wb") as writer:
        writer.setnchannels(1)
        writer.setsampwidth(2)
        writer.setframerate(sample_rate)
        payload = bytearray()
        for index in range(frames):
            sample = int(amplitude * math.sin(2 * math.pi * 440 * index / sample_rate))
            payload.extend(sample.to_bytes(2, "little", signed=True))
        writer.writeframes(bytes(payload))


def build_payload(engine: str, text: str, args: argparse.Namespace) -> tuple[str, dict[str, Any], str]:
    if engine == "backend-http":
        return (
            args.backend_base_url.rstrip("/") + "/tts",
            {
                "provider": "http",
                "text": text,
                "speed_scale": args.speed_scale,
                "pitch_scale": args.pitch_scale,
                "voice_gender": args.voice_gender,
                "voice_instruct": args.voice_instruct,
                "voice_lang_code": args.voice_lang_code,
            },
            "wav",
        )
    if engine == "backend-voicevox":
        return (
            args.backend_base_url.rstrip("/") + "/tts",
            {
                "provider": "voicevox",
                "text": text,
                "speaker_id": args.speaker_id,
                "speed_scale": args.speed_scale,
                "pitch_scale": args.pitch_scale,
                "intonation_scale": args.intonation_scale,
                "volume_scale": args.volume_scale,
                "pre_phoneme_length": args.pre_phoneme_length,
                "post_phoneme_length": args.post_phoneme_length,
            },
            "wav",
        )
    if engine == "mlx-direct":
        payload: dict[str, Any] = {
            "model": args.mlx_model,
            "input": text,
            "response_format": args.response_format,
            "speed": 1.0,
            "pitch": 1.0,
            "gender": args.voice_gender,
            "instruct": args.voice_instruct,
            "lang_code": args.voice_lang_code,
            "temperature": args.temperature,
            "top_p": args.top_p,
            "top_k": args.top_k,
            "repetition_penalty": args.repetition_penalty,
            "max_tokens": args.max_tokens,
        }
        if args.ref_audio:
            payload["ref_audio"] = args.ref_audio
            payload["ref_text"] = args.ref_text
        return (args.mlx_base_url.rstrip("/") + "/v1/audio/speech", payload, args.response_format)
    if engine == "irodori-server-direct":
        payload = {
            "model": args.irodori_server_model,
            "input": text,
            "voice": args.irodori_server_voice,
            "response_format": args.response_format,
            "speed": args.server_speed,
            "stream_format": args.stream_format,
            "irodori": {
                "num_steps": args.num_steps,
                "seed": args.seed,
                "cfg_scale_text": args.cfg_scale_text,
                "cfg_scale_caption": args.cfg_scale_caption,
                "cfg_scale_speaker": args.cfg_scale_speaker,
                "chunking_enabled": args.chunking_enabled,
                "chunk_min_chars": args.chunk_min_chars,
                "first_sentence_chunk_min_chars": args.first_sentence_chunk_min_chars,
            },
        }
        if args.server_ref_wav:
            payload["irodori"]["ref_wav"] = args.server_ref_wav
        if args.server_ref_latent:
            payload["irodori"]["ref_latent"] = args.server_ref_latent
        if args.server_ref_embed:
            payload["irodori"]["ref_embed"] = args.server_ref_embed
        if args.server_no_ref:
            payload["irodori"]["no_ref"] = True
        if args.server_caption:
            # Irodori-TTS itself supports caption/cfg_scale_caption. This is kept
            # in the benchmark payload so local server forks can expose it.
            payload["caption"] = args.server_caption
            payload["instruct"] = args.server_caption
            payload["irodori"]["caption"] = args.server_caption
        if args.stream_format != "sse":
            payload.pop("stream_format", None)
        payload["irodori"] = {k: v for k, v in payload["irodori"].items() if v is not None}
        return (args.irodori_server_base_url.rstrip("/") + "/v1/audio/speech", payload, args.response_format)
    if engine == "mock-direct":
        return ("mock://local", {"duration_ms": args.mock_duration_ms}, "wav")
    raise ValueError(f"Unsupported engine: {engine}")


def run_one(
    *,
    run_id: str,
    engine: str,
    text_id: str,
    text: str,
    iteration: int,
    output_dir: Path,
    args: argparse.Namespace,
) -> BenchResult:
    url, payload, audio_format = build_payload(engine, text, args)
    suffix = "wav" if audio_format in {"wav", "pcm"} else audio_format
    output_path = output_dir / f"{engine}_{text_id}_{iteration}.{suffix}"
    started = time.perf_counter()
    cache_hit = None
    response_info: dict[str, Any] = {}
    error = None
    try:
        if engine == "mock-direct":
            write_mock_wav(output_path, duration_ms=args.mock_duration_ms)
            response_info["status"] = 200
            response_info["mode"] = "mock_wav"
            response_info["content_type"] = "audio/wav"
        else:
            body, content_type, status, headers = post_json(url, payload, args.timeout)
            response_info["status"] = status
            response_info["headers"] = headers
            response_info.update(save_response_audio(body=body, content_type=content_type, output_path=output_path))
        if response_info.get("mode") == "json_audio_url":
            before_mtime = output_path.stat().st_mtime if output_path.exists() else None
            fetch_backend_audio(
                backend_base_url=args.backend_base_url,
                audio_url=str(response_info["audio_url"]),
                output_path=output_path,
                timeout=args.timeout,
            )
            cache_hit = before_mtime is not None and output_path.stat().st_mtime == before_mtime
    except Exception as exc:  # noqa: BLE001 - benchmark should record failures.
        error = repr(exc)
        output_path.write_bytes(b"")
    elapsed_ms = int(round((time.perf_counter() - started) * 1000))
    metrics = wav_metrics(output_path)
    rtf = None
    if metrics.duration_ms and metrics.duration_ms > 0:
        rtf = round(elapsed_ms / metrics.duration_ms, 3)
    return BenchResult(
        run_id=run_id,
        engine=engine,
        text_id=text_id,
        chars=len(text),
        iteration=iteration,
        elapsed_ms=elapsed_ms,
        rtf=rtf,
        cache_hit=cache_hit,
        phase=args.phase,
        output_path=str(output_path),
        request={"url": url, "payload": payload},
        response=response_info,
        audio=metrics,
        error=error,
    )


def write_summary(results: list[BenchResult], output_dir: Path) -> None:
    lines = ["# TTS Benchmark", ""]
    by_key: dict[tuple[str, str, str], list[BenchResult]] = {}
    for result in results:
        by_key.setdefault((result.engine, result.phase, result.text_id), []).append(result)

    lines.append("| engine | phase | text | n | chars | elapsed min | elapsed median | elapsed max | audio median | RTF median | errors |")
    lines.append("| --- | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |")
    for (engine, phase, text_id), group in sorted(by_key.items()):
        elapsed = [r.elapsed_ms for r in group if r.error is None]
        durations = [r.audio.duration_ms for r in group if r.error is None and r.audio.duration_ms]
        rtfs = [r.rtf for r in group if r.error is None and r.rtf is not None]
        chars = group[0].chars
        errors = sum(1 for r in group if r.error)
        lines.append(
            "| {engine} | {phase} | {text_id} | {n} | {chars} | {elapsed_min} | {elapsed_median} | {elapsed_max} | {duration} | {rtf} | {errors} |".format(
                engine=engine,
                phase=phase,
                text_id=text_id,
                n=len(group),
                chars=chars,
                elapsed_min=min(elapsed) if elapsed else "",
                elapsed_median=int(statistics.median(elapsed)) if elapsed else "",
                elapsed_max=max(elapsed) if elapsed else "",
                duration=int(statistics.median(durations)) if durations else "",
                rtf=round(statistics.median(rtfs), 3) if rtfs else "",
                errors=errors,
            )
        )
    lines.append("")
    lines.append("## Files")
    for result in results:
        lines.append(
            f"- `{Path(result.output_path).name}`: engine={result.engine}, text={result.text_id}, "
            f"elapsed={result.elapsed_ms}ms, duration={result.audio.duration_ms}ms, "
            f"rtf={result.rtf}, error={result.error}"
        )
    (output_dir / "summary.md").write_text("\n".join(lines) + "\n", encoding="utf-8")


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Benchmark Yui TTS backends.")
    parser.add_argument(
        "--engine",
        action="append",
        choices=["backend-http", "backend-voicevox", "mlx-direct", "irodori-server-direct", "mock-direct"],
        required=True,
        help="Engine to benchmark. Can be passed multiple times.",
    )
    parser.add_argument("--text", action="append", choices=sorted(DEFAULT_TEXT_PRESETS), help="Text preset.")
    parser.add_argument("--iterations", type=int, default=1)
    parser.add_argument("--phase", default="warm-cache-miss")
    parser.add_argument("--timeout", type=float, default=240.0)
    parser.add_argument("--output-root", type=Path, default=DEFAULT_OUTPUT_ROOT)
    parser.add_argument("--run-id", default=now_run_id())
    parser.add_argument("--backend-base-url", default="http://127.0.0.1:8000")
    parser.add_argument("--mlx-base-url", default="http://127.0.0.1:41080")
    parser.add_argument("--mlx-model", default="mlx-community/Irodori-TTS-600M-v3-VoiceDesign-8bit")
    parser.add_argument("--irodori-server-base-url", default="http://127.0.0.1:8088")
    parser.add_argument("--irodori-server-model", default="irodori-tts")
    parser.add_argument("--irodori-server-voice", default="none")
    parser.add_argument("--response-format", default="wav")
    parser.add_argument("--voice-gender", default="female")
    parser.add_argument("--voice-instruct", default=DEFAULT_IRODORI_INSTRUCT)
    parser.add_argument("--voice-lang-code", default="ja")
    parser.add_argument("--ref-audio", default=os.getenv("IRODORI_BENCH_REF_AUDIO", ""))
    parser.add_argument("--ref-text", default=DEFAULT_IRODORI_REF_TEXT)
    parser.add_argument("--temperature", type=float, default=0.7)
    parser.add_argument("--top-p", type=float, default=0.95)
    parser.add_argument("--top-k", type=int, default=40)
    parser.add_argument("--repetition-penalty", type=float, default=1.0)
    parser.add_argument("--max-tokens", type=int, default=1200)
    parser.add_argument("--speed-scale", type=float, default=1.0)
    parser.add_argument("--pitch-scale", type=float, default=0.0)
    parser.add_argument("--intonation-scale", type=float, default=1.0)
    parser.add_argument("--volume-scale", type=float, default=1.0)
    parser.add_argument("--pre-phoneme-length", type=float, default=0.1)
    parser.add_argument("--post-phoneme-length", type=float, default=0.1)
    parser.add_argument("--speaker-id", type=int, default=14)
    parser.add_argument("--server-speed", type=float, default=1.0)
    parser.add_argument("--num-steps", type=int)
    parser.add_argument("--seed", type=int)
    parser.add_argument("--cfg-scale-text", type=float)
    parser.add_argument("--cfg-scale-caption", type=float)
    parser.add_argument("--cfg-scale-speaker", type=float)
    parser.add_argument("--chunking-enabled", type=json_bool)
    parser.add_argument("--chunk-min-chars", type=int)
    parser.add_argument("--first-sentence-chunk-min-chars", type=int)
    parser.add_argument("--stream-format", default="")
    parser.add_argument("--server-ref-wav", default="")
    parser.add_argument("--server-ref-latent", default="")
    parser.add_argument("--server-ref-embed", default="")
    parser.add_argument("--server-no-ref", action="store_true")
    parser.add_argument("--server-caption", default="")
    parser.add_argument("--mock-duration-ms", type=int, default=600)
    return parser.parse_args()


def json_bool(value: str) -> bool:
    lowered = value.strip().lower()
    if lowered in {"1", "true", "yes", "on"}:
        return True
    if lowered in {"0", "false", "no", "off"}:
        return False
    raise argparse.ArgumentTypeError(f"Invalid boolean: {value}")


def main() -> int:
    args = parse_args()
    text_ids = args.text or list(DEFAULT_TEXT_PRESETS)
    output_dir = args.output_root / args.run_id
    output_dir.mkdir(parents=True, exist_ok=True)
    (output_dir / "texts.json").write_text(
        json.dumps({key: DEFAULT_TEXT_PRESETS[key] for key in text_ids}, ensure_ascii=False, indent=2),
        encoding="utf-8",
    )
    (output_dir / "environment.json").write_text(
        json.dumps(collect_environment(), ensure_ascii=False, indent=2),
        encoding="utf-8",
    )
    results: list[BenchResult] = []
    jsonl_path = output_dir / "results.jsonl"
    with jsonl_path.open("w", encoding="utf-8") as writer:
        for engine in args.engine:
            for text_id in text_ids:
                text = DEFAULT_TEXT_PRESETS[text_id]
                for iteration in range(1, args.iterations + 1):
                    result = run_one(
                        run_id=args.run_id,
                        engine=engine,
                        text_id=text_id,
                        text=text,
                        iteration=iteration,
                        output_dir=output_dir,
                        args=args,
                    )
                    results.append(result)
                    writer.write(json.dumps(asdict(result), ensure_ascii=False) + "\n")
                    writer.flush()
                    status = "ERROR" if result.error else "OK"
                    print(
                        f"{status} {engine} {text_id} iter={iteration} "
                        f"elapsed={result.elapsed_ms}ms duration={result.audio.duration_ms}ms rtf={result.rtf}"
                    )
    write_summary(results, output_dir)
    print(f"summary={output_dir / 'summary.md'}")
    return 0 if all(result.error is None for result in results) else 1


if __name__ == "__main__":
    raise SystemExit(main())
