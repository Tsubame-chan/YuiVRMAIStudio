import logging
import hashlib
import time
from pathlib import Path

import httpx

from app.core.config import Settings
from app.models.tts import TTSRequest, TTSResponse
from app.providers.interfaces import TTSProvider


class TTSProviderError(RuntimeError):
    pass


logger = logging.getLogger(__name__)


class VoiceVoxProvider(TTSProvider):
    name = "voicevox"

    def __init__(
        self,
        settings: Settings,
        *,
        base_url: str | None = None,
        provider_name: str = "voicevox",
        cache_prefix: str = "vv",
    ):
        self.settings = settings
        self.name = provider_name
        self.cache_prefix = cache_prefix
        self.audio_dir = Path(__file__).resolve().parents[2] / "data" / "audio"
        self.audio_dir.mkdir(parents=True, exist_ok=True)
        self._client = httpx.AsyncClient(base_url=base_url or self.settings.voicevox_base_url, timeout=30.0)

    async def synthesize(self, request: TTSRequest) -> TTSResponse:
        audio_id = self._cache_key(request)
        filename = f"{audio_id}.wav"
        output_path = self.audio_dir / filename
        if output_path.exists():
            logger.info("%s cache hit chars=%s speaker=%s", self.name.upper(), len(request.text), request.speaker_id)
            return TTSResponse(
                audio_url=f"/audio/{filename}",
                format="wav",
                duration_ms=None,
            )

        try:
            started_at = time.perf_counter()
            query_response = await self._client.post(
                "/audio_query",
                params={"text": request.text, "speaker": request.speaker_id},
            )
            query_response.raise_for_status()
            audio_query_ms = int((time.perf_counter() - started_at) * 1000)
            audio_query = query_response.json()
            if request.speed_scale is not None:
                audio_query["speedScale"] = request.speed_scale
            if request.pitch_scale is not None:
                audio_query["pitchScale"] = request.pitch_scale
            if request.intonation_scale is not None:
                audio_query["intonationScale"] = request.intonation_scale
            if request.volume_scale is not None:
                audio_query["volumeScale"] = request.volume_scale
            if request.pre_phoneme_length is not None:
                audio_query["prePhonemeLength"] = request.pre_phoneme_length
            if request.post_phoneme_length is not None:
                audio_query["postPhonemeLength"] = request.post_phoneme_length

            synthesis_started_at = time.perf_counter()
            synthesis_response, synthesis_endpoint = await self._post_synthesis(request.speaker_id, audio_query)
            synthesis_response.raise_for_status()
            synthesis_ms = int((time.perf_counter() - synthesis_started_at) * 1000)
            logger.info(
                "%s synthesis chars=%s speaker=%s endpoint=%s audio_query_ms=%s synthesis_ms=%s bytes=%s",
                self.name.upper(),
                len(request.text),
                request.speaker_id,
                synthesis_endpoint,
                audio_query_ms,
                synthesis_ms,
                len(synthesis_response.content),
            )
        except httpx.HTTPError as exc:
            raise TTSProviderError(str(exc)) from exc

        # Write to a tmp file first so a partial write never leaves a corrupt cache hit.
        tmp_path = output_path.with_suffix(".wav.partial")
        try:
            tmp_path.write_bytes(synthesis_response.content)
            tmp_path.replace(output_path)
        except OSError as exc:
            tmp_path.unlink(missing_ok=True)
            raise TTSProviderError(f"Failed to persist {self.name} audio: {exc}") from exc

        self._enforce_cache_limit()
        return TTSResponse(
            audio_url=f"/audio/{filename}",
            format="wav",
            duration_ms=None,
        )

    async def _post_synthesis(self, speaker_id: int, audio_query: dict) -> tuple[httpx.Response, str]:
        """Use cancellable synthesis when the local VOICEVOX Engine supports it."""
        try:
            response = await self._client.post(
                "/cancellable_synthesis",
                params={"speaker": speaker_id},
                json=audio_query,
            )
            if response.status_code not in {404, 405, 501}:
                return response, "/cancellable_synthesis"
        except httpx.ConnectError as exc:
            logger.warning("VOICEVOX cancellable_synthesis failed, falling back: %s", exc)

        response = await self._client.post(
            "/synthesis",
            params={"speaker": speaker_id},
            json=audio_query,
        )
        return response, "/synthesis"

    def _enforce_cache_limit(self) -> None:
        """Keep generated WAV cache within the configured local retention policy."""
        try:
            entries = [p for p in self.audio_dir.iterdir() if p.suffix.lower() == ".wav"]
        except OSError as exc:
            logger.warning("VOICEVOX cache scan failed: %s", exc)
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
                logger.warning("VOICEVOX cache cleanup skipped %s: %s", stale.name, exc)

    @staticmethod
    def _cache_key(request: TTSRequest) -> str:
        payload = "|".join(
            (
                getattr(request, "provider", None) or "",
                request.text,
                str(request.speaker_id),
                str(request.speed_scale),
                str(request.pitch_scale),
                str(request.intonation_scale),
                str(request.volume_scale),
                str(request.pre_phoneme_length),
                str(request.post_phoneme_length),
            )
        )
        prefix = getattr(request, "_cache_prefix", None) or "vv"
        return prefix + "_" + hashlib.sha1(payload.encode("utf-8")).hexdigest()


class AivisSpeechProvider(VoiceVoxProvider):
    name = "aivis"

    def __init__(self, settings: Settings):
        super().__init__(
            settings,
            base_url=settings.aivis_base_url,
            provider_name="aivis",
            cache_prefix="aivis",
        )

    @staticmethod
    def _cache_key(request: TTSRequest) -> str:
        payload = "|".join(
            (
                "aivis",
                request.text,
                str(request.speaker_id),
                str(request.speed_scale),
                str(request.pitch_scale),
                str(request.intonation_scale),
                str(request.volume_scale),
                str(request.pre_phoneme_length),
                str(request.post_phoneme_length),
            )
        )
        return "aivis_" + hashlib.sha1(payload.encode("utf-8")).hexdigest()
