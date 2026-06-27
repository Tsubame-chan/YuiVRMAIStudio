import hashlib
import io
import logging
import shutil
import subprocess
import struct
import tempfile
import time
import wave
from pathlib import Path
import audioop

import httpx

from app.core.config import ROOT_DIR, Settings
from app.models.tts import TTSRequest, TTSResponse
from app.providers.interfaces import TTSProvider
from app.providers.voicevox_tts import TTSProviderError


logger = logging.getLogger(__name__)
IRODORI_VOICE_REFERENCE_TEXT = "こんにちは、声の基準を作ります。"


class HttpTTSProvider(TTSProvider):
    name = "http"

    def __init__(
        self,
        settings: Settings,
        *,
        audio_dir: Path | None = None,
        transport: httpx.AsyncBaseTransport | None = None,
    ):
        self.settings = settings
        self.audio_dir = audio_dir or Path(__file__).resolve().parents[2] / "data" / "audio"
        self.audio_dir.mkdir(parents=True, exist_ok=True)
        if not self.settings.http_tts_base_url:
            raise TTSProviderError("HTTP_TTS_BASE_URL is required when TTS_PROVIDER=http.")
        self._client = httpx.AsyncClient(
            base_url=self.settings.http_tts_base_url,
            timeout=60.0,
            transport=transport,
        )

    async def synthesize(self, request: TTSRequest) -> TTSResponse:
        started_at = time.perf_counter()
        audio_format = self._audio_format()
        audio_id = self._cache_key(request, self.settings)
        filename = f"{audio_id}.{audio_format}"
        output_path = self.audio_dir / filename
        if output_path.exists():
            logger.warning(
                "HTTP TTS cache hit provider=%s chars=%s speed=%.3f pitch=%.3f file=%s elapsed_ms=%d",
                self.settings.http_tts_provider_id,
                len(request.text),
                request.speed_scale or 1.0,
                request.pitch_scale or 0.0,
                filename,
                int((time.perf_counter() - started_at) * 1000),
            )
            return TTSResponse(audio_url=f"/audio/{filename}", format=audio_format, duration_ms=None)

        headers = {}
        if self.settings.http_tts_api_key:
            headers["Authorization"] = f"Bearer {self.settings.http_tts_api_key}"

        payload = self._payload(request, audio_format)
        if self._should_use_voice_reference(request):
            payload["ref_audio"] = str(await self._ensure_voice_reference(request))
            payload["ref_text"] = IRODORI_VOICE_REFERENCE_TEXT

        try:
            remote_started_at = time.perf_counter()
            response = await self._client.post(self._endpoint(request), json=payload, headers=headers)
            response.raise_for_status()
        except httpx.HTTPError as exc:
            raise TTSProviderError(str(exc)) from exc
        remote_ms = int((time.perf_counter() - remote_started_at) * 1000)

        tmp_path = output_path.with_suffix(f".{audio_format}.partial")
        try:
            post_started_at = time.perf_counter()
            tmp_path.write_bytes(
                self._postprocess_audio(
                    response.content,
                    request,
                    audio_format,
                    protect_quality=self._should_use_voice_reference(request),
                    audio_processor=self.settings.http_tts_audio_processor,
                    soundstretch_path=self.settings.http_tts_soundstretch_path,
                )
            )
            postprocess_ms = int((time.perf_counter() - post_started_at) * 1000)
            tmp_path.replace(output_path)
        except OSError as exc:
            tmp_path.unlink(missing_ok=True)
            raise TTSProviderError(f"Failed to persist HTTP TTS audio: {exc}") from exc

        logger.warning(
            "HTTP TTS synth provider=%s chars=%s speed=%.3f pitch=%.3f remote_ms=%d postprocess_ms=%d total_ms=%d file=%s",
            self.settings.http_tts_provider_id,
            len(request.text),
            request.speed_scale or 1.0,
            request.pitch_scale or 0.0,
            remote_ms,
            postprocess_ms,
            int((time.perf_counter() - started_at) * 1000),
            filename,
        )
        self._enforce_cache_limit(audio_format)
        return TTSResponse(audio_url=f"/audio/{filename}", format=audio_format, duration_ms=None)

    def _endpoint(self, request: TTSRequest | None = None) -> str:
        endpoint = self.settings.http_tts_endpoint.strip() or "/tts"
        return endpoint if endpoint.startswith("/") else f"/{endpoint}"

    def _audio_format(self) -> str:
        audio_format = (self.settings.http_tts_format or "wav").strip().lower()
        audio_format = audio_format or "wav"
        if audio_format not in {"wav", "mp3", "ogg"}:
            raise TTSProviderError("HTTP_TTS_FORMAT must be one of: wav, mp3, ogg.")
        return audio_format

    def _payload(self, request: TTSRequest, audio_format: str) -> dict[str, object]:
        if self.settings.http_tts_payload_format in {"openai_speech", "irodori_openai_speech"}:
            request_instruct = request.voice_instruct
            request_gender = request.voice_gender
            request_lang_code = request.voice_lang_code
            payload: dict[str, object] = {
                "model": self.settings.http_tts_model,
                "input": request.text,
                "response_format": audio_format,
            }
            if self.settings.http_tts_voice:
                payload["voice"] = self.settings.http_tts_voice
            if request.speed_scale is not None:
                payload["speed"] = 1.0 if self._is_irodori_openai_speech() else request.speed_scale
            if request.pitch_scale is not None and self.settings.http_tts_payload_format != "irodori_openai_speech":
                payload["pitch"] = 1.0 if self._is_irodori_openai_speech() else round(1.0 + request.pitch_scale, 4)
            gender = request_gender if request_gender is not None else (
                None if request_instruct is not None else self.settings.http_tts_gender
            )
            instruct = request_instruct if request_instruct is not None else self.settings.http_tts_instruct
            lang_code = request_lang_code if request_lang_code is not None else self.settings.http_tts_lang_code
            if gender:
                payload["gender"] = gender
            if instruct:
                payload["instruct"] = instruct
            if lang_code:
                payload["lang_code"] = lang_code
            if self.settings.http_tts_payload_format == "irodori_openai_speech":
                irodori_options: dict[str, object] = {
                    "chunking_enabled": self.settings.http_tts_irodori_chunking_enabled,
                }
                if instruct:
                    payload["caption"] = instruct
                    irodori_options["caption"] = instruct
                if self.settings.http_tts_irodori_num_steps > 0:
                    irodori_options["num_steps"] = self.settings.http_tts_irodori_num_steps
                if self.settings.http_tts_irodori_seed > 0:
                    irodori_options["seed"] = self.settings.http_tts_irodori_seed
                if self.settings.http_tts_irodori_cfg_scale_text > 0:
                    irodori_options["cfg_scale_text"] = self.settings.http_tts_irodori_cfg_scale_text
                if self.settings.http_tts_irodori_cfg_scale_caption > 0:
                    irodori_options["cfg_scale_caption"] = self.settings.http_tts_irodori_cfg_scale_caption
                if self.settings.http_tts_irodori_cfg_scale_speaker > 0:
                    irodori_options["cfg_scale_speaker"] = self.settings.http_tts_irodori_cfg_scale_speaker
                if self.settings.http_tts_irodori_chunk_min_chars > 0:
                    irodori_options["chunk_min_chars"] = self.settings.http_tts_irodori_chunk_min_chars
                if self.settings.http_tts_irodori_first_sentence_chunk_min_chars > 0:
                    irodori_options["first_sentence_chunk_min_chars"] = (
                        self.settings.http_tts_irodori_first_sentence_chunk_min_chars
                    )
                payload["irodori"] = irodori_options
            return payload

        return {
            "provider": self.settings.http_tts_provider_id,
            "text": request.text,
            "voice": self.settings.http_tts_voice,
            "model": self.settings.http_tts_model,
            "format": audio_format,
            "speaker_id": request.speaker_id,
            "speed_scale": request.speed_scale,
            "pitch_scale": request.pitch_scale,
            "intonation_scale": request.intonation_scale,
            "volume_scale": request.volume_scale,
        }

    def _is_irodori_openai_speech(self) -> bool:
        if self.settings.http_tts_payload_format not in {"openai_speech", "irodori_openai_speech"}:
            return False

        provider_key = " ".join(
            (
                self.settings.http_tts_provider_id,
                self.settings.http_tts_model,
            )
        ).lower()
        return "irodori" in provider_key

    def _should_use_voice_reference(self, request: TTSRequest) -> bool:
        if not self._is_irodori_openai_speech():
            return False

        return bool(
            request.voice_instruct
            or request.voice_gender
            or self.settings.http_tts_instruct
            or self.settings.http_tts_gender
        )

    async def _ensure_voice_reference(self, request: TTSRequest) -> Path:
        reference_path = self._voice_reference_path(request)
        if reference_path.exists():
            return reference_path

        payload = self._payload(request, "wav")
        payload["input"] = IRODORI_VOICE_REFERENCE_TEXT
        payload["response_format"] = "wav"
        payload["speed"] = 1.0
        if self.settings.http_tts_payload_format != "irodori_openai_speech":
            payload["pitch"] = 1.0
        payload.pop("ref_audio", None)

        headers = {}
        if self.settings.http_tts_api_key:
            headers["Authorization"] = f"Bearer {self.settings.http_tts_api_key}"

        try:
            response = await self._client.post(self._endpoint(request), json=payload, headers=headers)
            response.raise_for_status()
        except httpx.HTTPError as exc:
            raise TTSProviderError(str(exc)) from exc

        tmp_path = reference_path.with_suffix(".wav.partial")
        try:
            tmp_path.write_bytes(response.content)
            tmp_path.replace(reference_path)
        except OSError as exc:
            tmp_path.unlink(missing_ok=True)
            raise TTSProviderError(f"Failed to persist HTTP TTS voice reference: {exc}") from exc

        return reference_path

    def _voice_reference_path(self, request: TTSRequest) -> Path:
        payload = "|".join(
            (
                "irodori_voice_reference_v1",
                self.settings.http_tts_base_url,
                self.settings.http_tts_endpoint,
                self.settings.http_tts_voice,
                self.settings.http_tts_model,
                self.settings.http_tts_gender,
                self.settings.http_tts_instruct,
                self.settings.http_tts_lang_code,
                str(request.voice_gender),
                str(request.voice_instruct),
                str(request.voice_lang_code),
            )
        )
        audio_id = hashlib.sha1(payload.encode("utf-8")).hexdigest()
        return self.audio_dir / f"http_voice_ref_{audio_id}.wav"

    @staticmethod
    def _postprocess_audio(
        content: bytes,
        request: TTSRequest,
        audio_format: str,
        *,
        protect_quality: bool = False,
        audio_processor: str = "auto",
        soundstretch_path: str = "",
    ) -> bytes:
        if audio_format != "wav":
            return content

        speed = request.speed_scale if request.speed_scale is not None else 1.0
        pitch = request.pitch_scale if request.pitch_scale is not None else 0.0
        if abs(speed - 1.0) < 0.001 and abs(pitch) < 0.001:
            if not protect_quality:
                return content
            return HttpTTSProvider._normalize_wav_peak(
                HttpTTSProvider._trim_wav_silence(content)
            )

        if protect_quality:
            processed = HttpTTSProvider._postprocess_audio_with_soundstretch(
                content,
                speed,
                pitch,
                audio_processor=audio_processor,
                soundstretch_path=soundstretch_path,
            )
            processed = HttpTTSProvider._trim_wav_silence(processed)
            return HttpTTSProvider._normalize_wav_peak(processed)

        factor = max(0.4, min(2.0, speed * max(0.4, 1.0 + pitch)))
        if abs(factor - 1.0) < 0.001:
            return content

        try:
            with wave.open(io.BytesIO(content), "rb") as reader:
                channels = reader.getnchannels()
                sample_width = reader.getsampwidth()
                frame_rate = reader.getframerate()
                frames = reader.readframes(reader.getnframes())
                params = reader.getparams()

            converted, _ = audioop.ratecv(
                frames,
                sample_width,
                channels,
                max(1, int(frame_rate * factor)),
                frame_rate,
                None,
            )
            output = io.BytesIO()
            with wave.open(output, "wb") as writer:
                writer.setnchannels(channels)
                writer.setsampwidth(sample_width)
                writer.setframerate(frame_rate)
                writer.setcomptype(params.comptype, params.compname)
                writer.writeframes(converted)
            return output.getvalue()
        except (audioop.error, EOFError, OSError, wave.Error) as exc:
            logger.warning("HTTP TTS audio postprocess skipped: %s", exc)
            return content

    @staticmethod
    def _postprocess_audio_with_soundstretch(
        content: bytes,
        speed_scale: float,
        pitch_scale: float,
        *,
        audio_processor: str,
        soundstretch_path: str,
    ) -> bytes:
        processor = (audio_processor or "auto").strip().lower()
        if processor == "none":
            return content
        if processor not in {"auto", "soundstretch"}:
            return content

        executable = HttpTTSProvider._resolve_soundstretch_path(soundstretch_path)
        if not executable:
            return content

        tempo_percent = (max(0.5, min(2.0, speed_scale)) - 1.0) * 100.0
        pitch_semitones = max(-0.5, min(0.5, pitch_scale)) * 12.0
        args = []
        if abs(tempo_percent) >= 0.05:
            args.append(f"-tempo={tempo_percent:.3f}")
        if abs(pitch_semitones) >= 0.10:
            args.append(f"-pitch={pitch_semitones:.3f}")
        if not args:
            return content

        try:
            with tempfile.TemporaryDirectory(prefix="yui-http-tts-") as work_dir:
                input_path = Path(work_dir) / "input.wav"
                output_path = Path(work_dir) / "output.wav"
                input_path.write_bytes(content)
                subprocess.run(
                    [executable, str(input_path), str(output_path), *args],
                    check=True,
                    stdout=subprocess.DEVNULL,
                    stderr=subprocess.PIPE,
                    timeout=20,
                )
                if output_path.exists():
                    logger.warning(
                        "HTTP TTS SoundStretch postprocess applied: speed=%.3f pitch=%.3f tool=%s args=%s",
                        speed_scale,
                        pitch_scale,
                        executable,
                        " ".join(args),
                    )
                    return output_path.read_bytes()
        except (OSError, subprocess.SubprocessError) as exc:
            logger.warning("HTTP TTS SoundStretch postprocess skipped: %s", exc)

        return content

    @staticmethod
    def _trim_wav_silence(content: bytes, *, keep_ms: int = 120, min_trim_ms: int = 240) -> bytes:
        try:
            with wave.open(io.BytesIO(content), "rb") as reader:
                channels = reader.getnchannels()
                sample_width = reader.getsampwidth()
                frame_rate = reader.getframerate()
                frame_count = reader.getnframes()
                frames = reader.readframes(frame_count)
                params = reader.getparams()

            if sample_width != 2 or channels < 1 or frame_count <= 0:
                return content

            sample_count = len(frames) // sample_width
            samples = struct.unpack("<" + "h" * sample_count, frames)
            frame_amplitudes = []
            for index in range(0, sample_count, channels):
                frame = samples[index:index + channels]
                frame_amplitudes.append(max(abs(value) for value in frame))

            if not frame_amplitudes:
                return content

            peak = max(frame_amplitudes)
            threshold = max(80, int(peak * 0.015))
            first = 0
            while first < len(frame_amplitudes) and frame_amplitudes[first] <= threshold:
                first += 1
            last = len(frame_amplitudes) - 1
            while last > first and frame_amplitudes[last] <= threshold:
                last -= 1
            if first == 0 and last == len(frame_amplitudes) - 1:
                return content
            if first >= last:
                return content

            keep_frames = max(0, int(frame_rate * keep_ms / 1000))
            min_trim_frames = max(1, int(frame_rate * min_trim_ms / 1000))
            leading_silence = first
            trailing_silence = len(frame_amplitudes) - 1 - last
            if leading_silence <= min_trim_frames and trailing_silence <= min_trim_frames:
                return content

            start_frame = max(0, first - keep_frames)
            end_frame = min(len(frame_amplitudes), last + 1 + keep_frames)
            start_byte = start_frame * channels * sample_width
            end_byte = end_frame * channels * sample_width

            output = io.BytesIO()
            with wave.open(output, "wb") as writer:
                writer.setnchannels(channels)
                writer.setsampwidth(sample_width)
                writer.setframerate(frame_rate)
                writer.setcomptype(params.comptype, params.compname)
                writer.writeframes(frames[start_byte:end_byte])
            return output.getvalue()
        except (EOFError, OSError, struct.error, wave.Error) as exc:
            logger.warning("HTTP TTS silence trim skipped: %s", exc)
            return content

    @staticmethod
    def _normalize_wav_peak(content: bytes, *, target_peak_db: float = -1.5) -> bytes:
        try:
            with wave.open(io.BytesIO(content), "rb") as reader:
                channels = reader.getnchannels()
                sample_width = reader.getsampwidth()
                frame_rate = reader.getframerate()
                frames = reader.readframes(reader.getnframes())
                params = reader.getparams()

            if sample_width != 2 or not frames:
                return content

            peak = audioop.max(frames, sample_width)
            max_value = (2 ** (8 * sample_width - 1)) - 1
            target_peak = max_value * (10 ** (target_peak_db / 20.0))
            if peak <= 0 or peak <= target_peak:
                return content

            scaled = audioop.mul(frames, sample_width, target_peak / peak)
            output = io.BytesIO()
            with wave.open(output, "wb") as writer:
                writer.setnchannels(channels)
                writer.setsampwidth(sample_width)
                writer.setframerate(frame_rate)
                writer.setcomptype(params.comptype, params.compname)
                writer.writeframes(scaled)
            return output.getvalue()
        except (audioop.error, EOFError, OSError, wave.Error) as exc:
            logger.warning("HTTP TTS peak normalize skipped: %s", exc)
            return content

    @staticmethod
    def _resolve_soundstretch_path(
        configured_path: str,
        *,
        bundled_root: Path | None = None,
    ) -> str:
        explicit_path = (configured_path or "").strip()
        if explicit_path:
            explicit = Path(explicit_path).expanduser()
            if explicit.is_file():
                return str(explicit)
            return ""

        root = bundled_root or ROOT_DIR
        candidates = (
            root / "tools" / "tts" / "soundtouch" / "bin" / "soundstretch",
            root / "tools" / "tts" / "soundtouch" / "bin" / "soundstretch.exe",
            root / "tools" / "tts" / "bin" / "soundstretch",
            root / "tools" / "tts" / "bin" / "soundstretch.exe",
        )
        for candidate in candidates:
            if candidate.is_file():
                return str(candidate)

        on_path = shutil.which("soundstretch")
        if on_path:
            return on_path

        return ""

    def _enforce_cache_limit(self, audio_format: str) -> None:
        """Keep generated HTTP TTS cache within the configured local retention policy."""
        suffix = f".{audio_format.lower()}"
        try:
            entries = [
                path
                for path in self.audio_dir.iterdir()
                if path.name.startswith("http_") and path.suffix.lower() == suffix
                and not path.name.startswith("http_voice_ref_")
            ]
        except OSError as exc:
            logger.warning("HTTP TTS cache scan failed: %s", exc)
            return

        if not entries:
            return

        max_files = max(0, int(getattr(self.settings, "tts_audio_cache_max_files", 300) or 0))
        max_mb = max(0, int(getattr(self.settings, "tts_audio_cache_max_mb", 256) or 0))
        max_age_hours = max(0, int(getattr(self.settings, "tts_audio_cache_max_age_hours", 24) or 0))
        max_bytes = max_mb * 1024 * 1024
        cutoff = time.time() - (max_age_hours * 60 * 60) if max_age_hours > 0 else None

        def stat_mtime(path: Path) -> float:
            try:
                return path.stat().st_mtime
            except OSError:
                return 0.0

        entries.sort(key=stat_mtime)
        stale_paths: set[Path] = set()
        if cutoff is not None:
            stale_paths.update(path for path in entries if stat_mtime(path) < cutoff)

        remaining = [path for path in entries if path not in stale_paths]
        if max_files > 0 and len(remaining) > max_files:
            overflow = len(remaining) - max_files
            stale_paths.update(remaining[:overflow])
            remaining = remaining[overflow:]

        if max_bytes > 0:
            total_bytes = 0
            sizes: dict[Path, int] = {}
            for path in remaining:
                try:
                    size = path.stat().st_size
                except OSError:
                    size = 0
                sizes[path] = size
                total_bytes += size

            for path in remaining:
                if total_bytes <= max_bytes:
                    break
                stale_paths.add(path)
                total_bytes -= sizes[path]

        for stale in sorted(stale_paths, key=stat_mtime):
            try:
                stale.unlink()
            except OSError as exc:
                logger.warning("HTTP TTS cache cleanup skipped %s: %s", stale.name, exc)

    @staticmethod
    def _cache_key(request: TTSRequest, settings: Settings) -> str:
        payload = "|".join(
            (
                settings.http_tts_provider_id,
                settings.http_tts_payload_format,
                "postprocess_soundstretch_v6_ref_text_trim_peak_voice_ref_v2",
                request.text,
                settings.http_tts_base_url,
                settings.http_tts_endpoint,
                settings.http_tts_voice,
                settings.http_tts_model,
                settings.http_tts_gender,
                settings.http_tts_instruct,
                settings.http_tts_lang_code,
                settings.http_tts_audio_processor,
                settings.http_tts_soundstretch_path,
                str(settings.http_tts_irodori_num_steps),
                str(settings.http_tts_irodori_seed),
                str(settings.http_tts_irodori_cfg_scale_text),
                str(settings.http_tts_irodori_cfg_scale_caption),
                str(settings.http_tts_irodori_cfg_scale_speaker),
                str(settings.http_tts_irodori_chunking_enabled),
                str(settings.http_tts_irodori_chunk_min_chars),
                str(settings.http_tts_irodori_first_sentence_chunk_min_chars),
                str(request.voice_gender),
                str(request.voice_instruct),
                str(request.voice_lang_code),
                settings.http_tts_format,
                str(request.speaker_id),
                str(request.speed_scale),
                str(request.pitch_scale),
                str(request.intonation_scale),
                str(request.volume_scale),
            )
        )
        return "http_" + hashlib.sha1(payload.encode("utf-8")).hexdigest()
