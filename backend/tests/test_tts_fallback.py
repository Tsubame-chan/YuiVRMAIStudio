import pytest

from app.api.routes import tts
from app.core.config import Settings
from app.models.tts import TTSRequest, TTSResponse
from app.providers.voicevox_tts import TTSProviderError


class FakeUsageRepository:
    def __init__(self) -> None:
        self.logged: list[dict[str, object]] = []

    def log(self, **kwargs) -> None:
        self.logged.append(kwargs)


@pytest.mark.anyio
async def test_tts_falls_back_to_voicevox_when_default_http_tts_fails(monkeypatch) -> None:
    calls: list[str | None] = []

    class FailingHttpProvider:
        name = "http"

        async def synthesize(self, request: TTSRequest) -> TTSResponse:
            raise TTSProviderError("offline")

    class WorkingVoicevoxProvider:
        name = "voicevox"

        async def synthesize(self, request: TTSRequest) -> TTSResponse:
            return TTSResponse(audio_url="/audio/fallback.wav", format="wav")

    def fake_tts(self, provider=None):
        calls.append(provider)
        if provider in {None, "http"}:
            return FailingHttpProvider()
        if provider == "voicevox":
            return WorkingVoicevoxProvider()
        raise AssertionError(provider)

    monkeypatch.setattr("app.api.routes.ProviderRouter.tts", fake_tts)
    usage_repository = FakeUsageRepository()

    response = await tts(
        TTSRequest(text="こんにちは"),
        settings=Settings(tts_provider="http", tts_fallback_provider="voicevox"),
        usage_repository=usage_repository,
    )

    assert response.audio_url == "/audio/fallback.wav"
    assert calls == [None, "voicevox"]
    assert usage_repository.logged[0]["provider"] == "voicevox"


@pytest.mark.anyio
async def test_tts_does_not_fallback_when_provider_is_explicit(monkeypatch) -> None:
    class FailingHttpProvider:
        name = "http"

        async def synthesize(self, request: TTSRequest) -> TTSResponse:
            raise TTSProviderError("offline")

    monkeypatch.setattr("app.api.routes.ProviderRouter.tts", lambda self, provider=None: FailingHttpProvider())

    with pytest.raises(Exception):
        await tts(
            TTSRequest(text="こんにちは", provider="http"),
            settings=Settings(tts_provider="voicevox", tts_fallback_provider="voicevox"),
            usage_repository=FakeUsageRepository(),
        )
