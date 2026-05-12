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

# Cap the on-disk cache so long-running sessions don't fill the data directory.
# Each chat reply is split into multiple chunks and produces multiple .wav files,
# so this needs to be generous; ~600 files is a few hours of conversation worst case.
_AUDIO_CACHE_LIMIT = 600


class VoiceVoxProvider(TTSProvider):
    name = "voicevox"

    def __init__(self, settings: Settings):
        self.settings = settings
        self.audio_dir = Path(__file__).resolve().parents[2] / "data" / "audio"
        self.audio_dir.mkdir(parents=True, exist_ok=True)
        self._client = httpx.AsyncClient(base_url=self.settings.voicevox_base_url, timeout=30.0)

    async def synthesize(self, request: TTSRequest) -> TTSResponse:
        audio_id = self._cache_key(request)
        filename = f"{audio_id}.wav"
        output_path = self.audio_dir / filename
        if output_path.exists():
            logger.info("VOICEVOX cache hit chars=%s speaker=%s", len(request.text), request.speaker_id)
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
                "VOICEVOX synthesis chars=%s speaker=%s endpoint=%s audio_query_ms=%s synthesis_ms=%s bytes=%s",
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
            raise TTSProviderError(f"Failed to persist VOICEVOX audio: {exc}") from exc

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
            if response.status_code != 404:
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
        """Keep at most _AUDIO_CACHE_LIMIT .wav files in the audio cache."""
        try:
            entries = [p for p in self.audio_dir.iterdir() if p.suffix.lower() == ".wav"]
        except OSError as exc:
            logger.warning("VOICEVOX cache scan failed: %s", exc)
            return

        if len(entries) <= _AUDIO_CACHE_LIMIT:
            return

        # Drop oldest first; mtime is good enough for a local cache.
        entries.sort(key=lambda p: p.stat().st_mtime)
        for stale in entries[: len(entries) - _AUDIO_CACHE_LIMIT]:
            try:
                stale.unlink()
            except OSError as exc:
                logger.warning("VOICEVOX cache cleanup skipped %s: %s", stale.name, exc)

    @staticmethod
    def _cache_key(request: TTSRequest) -> str:
        payload = "|".join(
            (
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
        return "vv_" + hashlib.sha1(payload.encode("utf-8")).hexdigest()
