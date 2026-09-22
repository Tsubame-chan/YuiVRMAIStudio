#!/usr/bin/env python3
"""Single-request local inference worker. JSON stdin/stdout; no HTTP server or credentials.

The parent owns the process and kills it on cancellation/timeout. Keep imports lazy so
VOICEVOX does not load the language model. Runtime dependencies are pinned by packaging.
"""
import base64
import contextlib
import ctypes as C
import io
import json
import os
from pathlib import Path
import sys
import tempfile
import time
import wave


def output_token_budget(capability, payload):
    if capability == "Transcription":
        return 512
    return 1536 if str(payload.get("Mode", "")).lower() == "work" else 256


def infer(request):
    import litert_lm as lm
    payload = json.loads(request.get("payload_json") or "{}")
    capability = request.get("capability")
    if payload.get("action") in ("warm", "release"):
        return {"Success": True}
    model = Path(request["model_path"])
    if not model.is_file():
        raise ValueError("Local model is missing. Complete the in-app data download first.")
    cache = Path(request["cache_directory"])
    cache.mkdir(parents=True, exist_ok=True)
    audio_path = None
    try:
        if capability == "Transcription":
            data = base64.b64decode(payload.get("AudioBytes", ""), validate=True)
            with wave.open(io.BytesIO(data)) as wav:
                if not 0 < wav.getnframes() / wav.getframerate() <= 120:
                    raise ValueError("Audio must be between 0 and 120 seconds.")
            with tempfile.NamedTemporaryFile(suffix=".wav", dir=cache, delete=False) as audio:
                audio.write(data)
                audio_path = audio.name
            prompt = {"role": "user", "content": [
                {"type": "audio", "path": audio_path},
                {"type": "text", "text": "聞こえた日本語の発話だけを正確に文字起こししてください。説明や返答を加えないでください。無音なら空文字にしてください。"},
            ]}
        elif capability == "Chat":
            text = payload.get("Prompt") or payload.get("Message") or ""
            if not text.strip():
                raise ValueError("Chat message is empty.")
            prompt = (request.get("system_instruction") or "") + "\n\n" + text
        else:
            raise ValueError("Unsupported desktop inference capability: " + str(capability))
        result = None
        for backend in (lm.Backend.GPU(), lm.Backend.CPU()):
            try:
                engine = lm.Engine(str(model), backend=backend, max_num_tokens=4096,
                    cache_dir=str(cache), audio_backend=lm.Backend.CPU() if audio_path else None,
                    enable_speculative_decoding=False)
                engine.__enter__()
            except Exception:
                if isinstance(backend, lm.Backend.CPU):
                    raise
                continue  # Fallback only on engine initialization, never repeat a completed generation.
            try:
                with engine.create_conversation(thinking_config=lm.ThinkingConfig(enable_thinking=False),
                        sampler_config=lm.SamplerConfig(temperature=0.0 if audio_path else 0.65, top_k=30, top_p=0.85)) as conversation:
                    result = conversation.send_message(prompt, max_output_tokens=output_token_budget(capability, payload))
            finally:
                engine.__exit__(None, None, None)
            break
        text = "".join(c.get("text", "") for c in (result or {}).get("content", []) if c.get("type") == "text").strip()
        if not text and not audio_path:
            raise ValueError("Local model returned an empty response.")
        return {"Success": True, "Text": text, "Face": "Neutral", "Animation": "idle_normal", "ShouldTts": not bool(audio_path)}
    finally:
        if audio_path:
            Path(audio_path).unlink(missing_ok=True)


def synthesize(request):
    class LoadOptions(C.Structure):
        _fields_ = [("filename", C.c_char_p)]
    class InitOptions(C.Structure):
        _fields_ = [("acceleration_mode", C.c_int), ("cpu_num_threads", C.c_uint16)]
    class SynthesisOptions(C.Structure):
        _fields_ = [("enable_interrogative_upspeak", C.c_bool)]
    root = Path(request["native_library_directory"])
    dll_dir = os.add_dll_directory(str(root)) if os.name == "nt" else None
    lib = C.CDLL(str(root / ("voicevox_core.dll" if os.name == "nt" else "libvoicevox_core.dylib")))
    ptr = C.c_void_p
    def bind(name, args, result=C.c_int):
        f = getattr(lib, "voicevox_" + name); f.argtypes = args; f.restype = result
        return f
    error = bind("error_result_to_message", [C.c_int], C.c_char_p)
    def check(code):
        if code:
            raise RuntimeError(error(code).decode("utf-8"))
    ort, jtalk, synth, model, query, wav = (ptr() for _ in range(6))
    try:
        onnx = next(root.glob("*voicevox_onnxruntime*.dll" if os.name == "nt" else "*voicevox_onnxruntime*.dylib"))
        check(bind("onnxruntime_load_once", [LoadOptions, C.POINTER(ptr)])(LoadOptions(str(onnx).encode()), C.byref(ort)))
        check(bind("open_jtalk_rc_new", [C.c_char_p, C.POINTER(ptr)])(request["open_jtalk_dict_path"].encode(), C.byref(jtalk)))
        check(bind("synthesizer_new", [ptr, ptr, InitOptions, C.POINTER(ptr)])(ort, jtalk, InitOptions(1, 0), C.byref(synth)))
        check(bind("voice_model_file_open", [C.c_char_p, C.POINTER(ptr)])(request["model_path"].encode(), C.byref(model)))
        check(bind("synthesizer_load_voice_model", [ptr, ptr])(synth, model))
        style = int(request.get("style_id", 14))
        check(bind("synthesizer_create_audio_query", [ptr, C.c_char_p, C.c_uint32, C.POINTER(ptr)])(synth, request["text"].encode(), style, C.byref(query)))
        value = json.loads(C.string_at(query).decode())
        for key, source, default, low, high in [
            ("speedScale", "speed_scale", 1., .5, 2.), ("pitchScale", "pitch_scale", 0., -.15, .15),
            ("intonationScale", "intonation_scale", 1., 0., 2.), ("volumeScale", "volume_scale", 1., 0., 2.),
            ("prePhonemeLength", "pre_phoneme_length", .1, 0., 1.5), ("postPhonemeLength", "post_phoneme_length", .1, 0., 1.5)]:
            value[key] = max(low, min(high, float(request.get(source, default))))
        length = C.c_size_t()
        check(bind("synthesizer_synthesis", [ptr, C.c_char_p, C.c_uint32, SynthesisOptions, C.POINTER(C.c_size_t), C.POINTER(ptr)])(
            synth, json.dumps(value).encode(), style, SynthesisOptions(True), C.byref(length), C.byref(wav)))
        data = C.string_at(wav, length.value)
        with wave.open(io.BytesIO(data)) as audio:
            sample_rate = audio.getframerate(); duration = round(audio.getnframes() * 1000 / sample_rate)
        return {"ok": True, "audio_base64": base64.b64encode(data).decode(), "sample_rate": sample_rate, "duration_ms": duration}
    finally:
        for name, value in [("wav_free", wav), ("json_free", query), ("voice_model_file_delete", model), ("synthesizer_delete", synth), ("open_jtalk_rc_delete", jtalk)]:
            if value:
                bind(name, [ptr], None)(value)
        if dll_dir:
            dll_dir.close()


def main():
    if hasattr(sys.stdin, "reconfigure"):
        sys.stdin.reconfigure(encoding="utf-8"); sys.stdout.reconfigure(encoding="utf-8")
    started = time.monotonic()
    try:
        request = json.load(sys.stdin)
        with contextlib.redirect_stdout(sys.stderr):
            if request.get("capability") == "SpeechSynthesis":
                result = synthesize(request)
            else:
                payload = infer(request)
                payload["LatencyMs"] = round((time.monotonic() - started) * 1000)
                result = {"ok": True, "model_id": request.get("runtime_model_ref"), "payload_json": json.dumps(payload, ensure_ascii=False)}
    except Exception as exc:
        result = {"ok": False, "error_code": "desktop_inference_failed", "error_message": str(exc)}
    print(json.dumps(result, ensure_ascii=False))


if __name__ == "__main__":
    main()
