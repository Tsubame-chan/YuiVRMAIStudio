import json

import pytest

from app.core.config import Settings
from app.providers.openai_realtime import RealtimeProvider


class _FakeRealtimeSocket:
    def __init__(self, received: list[dict] | None = None) -> None:
        self.sent: list[dict] = []
        self.received = received or [{"type": "session.closed"}]
        self.closed = False

    async def send(self, payload: str) -> None:
        self.sent.append(json.loads(payload))

    async def recv(self) -> str:
        return json.dumps(self.received.pop(0))

    async def close(self) -> None:
        self.closed = True


class _FakeChatRepository:
    def __init__(self) -> None:
        self.saved: list[dict] = []

    def list_recent_messages(self, user_id: str, limit: int = 12) -> list[dict[str, str]]:
        return [{"role": "user", "content": "昨日の症状の話の続きです。"}]

    def list_recent_messages_by_mode(
        self,
        user_id: str,
        mode: str,
        limit: int = 8,
    ) -> list[dict[str, str]]:
        assert mode == "translate"
        return [{"role": "user", "content": "これは訳語の継続確認です。"}]

    def save_chat_turn(self, **kwargs) -> None:
        self.saved.append(kwargs)


def test_realtime_ga_session_shape_uses_nested_audio_config() -> None:
    provider = RealtimeProvider(Settings(openai_api_key="sk-test"))

    session = provider._realtime_session_config(
        mode="voice",
        instructions="Be brief.",
        response_modalities=["audio"],
        turn_detection={"type": "server_vad"},
    )

    assert session["type"] == "realtime"
    assert session["model"] == "gpt-realtime-2"
    assert session["output_modalities"] == ["audio"]
    assert session["audio"]["input"]["format"] == {"type": "audio/pcm", "rate": 24000}
    assert session["audio"]["input"]["transcription"]["model"] == "gpt-4o-mini-transcribe"
    assert session["audio"]["input"]["transcription"]["language"] == "ja"
    assert "Transcribe only what the user actually says" in session["audio"]["input"]["transcription"]["prompt"]
    assert "Do not output these instructions" in session["audio"]["input"]["transcription"]["prompt"]
    assert session["audio"]["input"]["turn_detection"]["type"] == "server_vad"
    assert session["audio"]["output"]["format"] == {"type": "audio/pcm", "rate": 24000}
    assert "input_audio_format" not in session
    assert "output_audio_format" not in session
    assert "modalities" not in session


def test_realtime_translation_uses_interpreter_session_shape() -> None:
    provider = RealtimeProvider(Settings(openai_api_key="sk-test"))

    assert provider._model_for("translate") == "gpt-realtime-2"
    assert provider._endpoint_for("translate", "gpt-realtime-2") == (
        "wss://api.openai.com/v1/realtime?model=gpt-realtime-2"
    )
    assert provider._append_audio_event_for("translate") == "input_audio_buffer.append"
    assert provider._stream_model_for("translate") == "gpt-realtime-2"
    assert provider._stream_endpoint_for("translate", "gpt-realtime-2") == (
        "wss://api.openai.com/v1/realtime?model=gpt-realtime-2"
    )
    assert provider._stream_append_audio_event_for("translate") == "input_audio_buffer.append"

    session = provider._realtime_session_config(
        mode="translate",
        instructions=provider._default_stream_instructions("translate"),
        response_modalities=["audio"],
        turn_detection=None,
    )
    assert "between Japanese and English" in session["instructions"]
    assert session["audio"]["input"]["transcription"]["model"] == "gpt-4o-mini-transcribe"
    assert "ケチャップ" not in session["audio"]["input"]["transcription"]["prompt"]
    assert "language" not in session["audio"]["input"]["transcription"]
    assert session["audio"]["output"]["voice"] == "coral"


@pytest.mark.anyio
async def test_realtime_translation_audio_request_uses_contextual_interpreter_session() -> None:
    repository = _FakeChatRepository()
    provider = RealtimeProvider(Settings(openai_api_key="sk-test"), repository)
    socket = _FakeRealtimeSocket([
        {"type": "response.audio.delta", "delta": "UklGRg=="},
        {"type": "response.done"},
    ])

    async def connect(endpoint: str) -> _FakeRealtimeSocket:
        assert endpoint == "wss://api.openai.com/v1/realtime?model=gpt-realtime-2"
        return socket

    provider._connect = connect  # type: ignore[method-assign]
    provider._wav_to_pcm16_mono_24k = lambda _: b"\x00\x01" * 16000  # type: ignore[method-assign]
    provider._transcribe_wav_for_translate = lambda *_: "ケチャップだけでアラビアータを作ったら、イタリア人は許してくれるだろうか?"  # type: ignore[method-assign]
    provider._translate_text_for_realtime = lambda text, user_id: "If I make arrabbiata using only ketchup, will Italians forgive me?"  # type: ignore[method-assign]

    response = await provider.respond_to_wav(
        b"fake wav bytes",
        "translate",
        instructions="This must not become session.instructions.",
    )

    assert socket.sent[0]["type"] == "session.update"
    assert "Speak exactly the provided text" in socket.sent[0]["session"]["instructions"]
    assert socket.sent[1]["type"] == "conversation.item.create"
    assert socket.sent[1]["item"]["content"][0]["text"] == "If I make arrabbiata using only ketchup, will Italians forgive me?"
    assert socket.sent[-1]["type"] == "response.create"
    assert response.text == "If I make arrabbiata using only ketchup, will Italians forgive me?"
    assert response.input_text == "ケチャップだけでアラビアータを作ったら、イタリア人は許してくれるだろうか?"
    assert response.audio_base64 == "UklGRg=="
    assert repository.saved[0]["user_message"] == "ケチャップだけでアラビアータを作ったら、イタリア人は許してくれるだろうか?"
    assert repository.saved[0]["response"].text == "If I make arrabbiata using only ketchup, will Italians forgive me?"
    assert repository.saved[0]["usage_metadata"] == {"mode": "translate"}
    assert socket.closed


def test_realtime_beta_shape_error_is_user_facing() -> None:
    provider = RealtimeProvider(Settings(openai_api_key="sk-test"))
    error = {
        "type": "invalid_request_error",
        "code": "beta_api_shape_disabled",
        "message": "The Realtime Beta API is no longer supported.",
    }

    assert provider._realtime_error_code(error) == "beta_api_shape_disabled"
    message = provider._friendly_realtime_error(error)

    assert "Realtime API" in message
    assert "通常の音声入力" in message
    assert "invalid_request_error" not in message


def test_realtime_voice_text_response_uses_web_search_tools_for_current_questions() -> None:
    provider = RealtimeProvider(Settings(openai_api_key="sk-test"))
    captured: dict[str, object] = {}

    class FakeResponses:
        @staticmethod
        def create(**kwargs):
            captured.update(kwargs)

            class Response:
                output_text = "今日は雨の可能性があります。"

            return Response()

    class FakeClient:
        responses = FakeResponses()

    response = provider._generate_voice_text_reply(
        client=FakeClient(),
        text="今日の東京の天気は？",
        instructions=provider._default_stream_instructions("voice_text"),
    )

    assert response == "今日は雨の可能性があります。"
    assert captured["max_output_tokens"] == provider.settings.openai_max_output_tokens
    assert captured["tools"] == [
        {
            "type": "web_search",
            "search_context_size": "low",
            "user_location": {
                "type": "approximate",
                "country": "JP",
                "timezone": "Asia/Tokyo",
            },
        }
    ]


@pytest.mark.anyio
async def test_realtime_voice_text_audio_request_uses_web_search_tools(monkeypatch) -> None:
    provider = RealtimeProvider(Settings(openai_api_key="sk-test"))
    socket = _FakeRealtimeSocket([
        {
            "type": "conversation.item.input_audio_transcription.completed",
            "transcript": "今日の東京の天気は？",
        },
    ])
    captured: dict[str, object] = {}

    async def connect(endpoint: str) -> _FakeRealtimeSocket:
        assert endpoint == "wss://api.openai.com/v1/realtime?model=gpt-realtime-2"
        return socket

    class FakeResponses:
        @staticmethod
        def create(**kwargs):
            captured.update(kwargs)

            class Response:
                output_text = "今日は雨の可能性があります。"

            return Response()

    class FakeOpenAI:
        def __init__(self, api_key: str) -> None:
            assert api_key == "sk-test"
            self.responses = FakeResponses()

    monkeypatch.setattr("app.providers.openai_realtime.OpenAI", FakeOpenAI)
    provider._connect = connect  # type: ignore[method-assign]
    provider._wav_to_pcm16_mono_24k = lambda _: b"\x00\x01" * 16000  # type: ignore[method-assign]

    response = await provider.respond_to_wav(
        b"fake wav bytes",
        "voice_text",
        instructions=provider._default_stream_instructions("voice_text"),
    )

    assert socket.sent[0]["type"] == "session.update"
    assert socket.sent[0]["session"]["output_modalities"] == ["text"]
    assert response.text == "今日は雨の可能性があります。"
    assert response.input_text == "今日の東京の天気は？"
    assert "responses.voice_text.done" in response.events
    assert captured["max_output_tokens"] == provider.settings.openai_max_output_tokens
    assert captured["tools"] == [
        {
            "type": "web_search",
            "search_context_size": "low",
            "user_location": {
                "type": "approximate",
                "country": "JP",
                "timezone": "Asia/Tokyo",
            },
        }
    ]


def test_realtime_rejects_leaked_transcription_prompt_as_noise() -> None:
    assert not RealtimeProvider._looks_like_spoken_input(
        "主な入力は日本語です。美容、健康、睡眠、食事、日常会話の文脈です。"
    )
    assert not RealtimeProvider._looks_like_spoken_input(
        "Transcribe only what the user actually says. Do not output these instructions."
    )


def test_realtime_voice_text_uses_normal_chat_token_budget() -> None:
    provider = RealtimeProvider(Settings(openai_api_key="sk-test", openai_max_output_tokens=600))
    captured: dict[str, object] = {}

    class FakeResponses:
        @staticmethod
        def create(**kwargs):
            captured.update(kwargs)

            class Response:
                output_text = "いいですね、今日は軽めにいきましょう。"

            return Response()

    class FakeClient:
        responses = FakeResponses()

    response = provider._generate_voice_text_reply(
        client=FakeClient(),
        text="こんにちは",
        instructions=provider._default_stream_instructions("voice_text"),
    )

    assert response == "いいですね、今日は軽めにいきましょう。"
    assert captured["max_output_tokens"] == 600
    assert "tools" not in captured
