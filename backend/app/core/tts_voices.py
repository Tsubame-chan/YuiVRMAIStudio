from __future__ import annotations

from dataclasses import asdict, dataclass


@dataclass(frozen=True)
class TTSVoiceOption:
    provider: str
    id: int
    label: str
    gender: str
    style: str
    release_review: str = "ok"

    def to_config(self) -> dict[str, str | int]:
        return asdict(self)


VOICEVOX_VOICE_OPTIONS: tuple[TTSVoiceOption, ...] = (
    TTSVoiceOption(
        provider="voicevox",
        id=14,
        label="冥鳴ひまり / ノーマル",
        gender="female",
        style="normal",
    ),
)

AIVIS_VOICE_OPTIONS: tuple[TTSVoiceOption, ...] = (
    TTSVoiceOption(
        provider="aivis",
        id=1431611904,
        label="女性ボイス①",
        gender="female",
        style="normal",
    ),
    TTSVoiceOption(
        provider="aivis",
        id=604166016,
        label="女性ボイス②",
        gender="female",
        style="normal",
    ),
    TTSVoiceOption(
        provider="aivis",
        id=1920374593,
        label="女性ボイス③",
        gender="female",
        style="negative",
        release_review="custom_license",
    ),
    TTSVoiceOption(
        provider="aivis",
        id=1310138976,
        label="男性ボイス①",
        gender="male",
        style="normal",
    ),
)

TTS_VOICE_OPTIONS: dict[str, tuple[TTSVoiceOption, ...]] = {
    "voicevox": VOICEVOX_VOICE_OPTIONS,
    "aivis": AIVIS_VOICE_OPTIONS,
}

TTS_DEFAULT_VOICE: dict[str, int] = {
    "voicevox": 14,
    "aivis": 1431611904,
}


def voice_options_for_config(providers: list[str]) -> dict[str, list[dict[str, str | int]]]:
    return {
        provider: [voice.to_config() for voice in TTS_VOICE_OPTIONS.get(provider, ())]
        for provider in providers
        if provider in TTS_VOICE_OPTIONS
    }


def default_voices_for_config(providers: list[str]) -> dict[str, int]:
    return {
        provider: voice_id
        for provider, voice_id in TTS_DEFAULT_VOICE.items()
        if provider in providers
    }
