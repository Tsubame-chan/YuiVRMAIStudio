from app.core.config import Settings
from app.models.chat import ChatRequest
from app.models.common import RequestContext
from app.providers.openai_chat import OpenAIChatProvider


def make_provider() -> OpenAIChatProvider:
    provider = OpenAIChatProvider.__new__(OpenAIChatProvider)
    provider.settings = Settings(openai_api_key="test-key")
    return provider


def test_parse_fallback_clean_json() -> None:
    parsed = make_provider()._parse_fallback(
        """
        {
          "text": "こんにちは。",
          "face": "Joy",
          "animation": "wave_small",
          "voice_style": "normal",
          "should_use_vision": false,
          "memory_action": "none",
          "should_tts": true
        }
        """
    )

    assert parsed is not None
    assert parsed.text == "こんにちは。"
    assert parsed.face == "Joy"
    assert parsed.animation == "wave_small"


def test_parse_fallback_json_code_fence() -> None:
    parsed = make_provider()._parse_fallback(
        """
        ```json
        {
          "text": "任せて。",
          "face": "Fun",
          "animation": "nod_small",
          "voice_style": "normal",
          "should_use_vision": false,
          "memory_action": "none",
          "should_tts": true
        }
        ```
        """
    )

    assert parsed is not None
    assert parsed.text == "任せて。"
    assert parsed.face == "Fun"
    assert parsed.animation == "nod_small"


def test_parse_fallback_bracket_tags() -> None:
    parsed = make_provider()._parse_fallback("[face: Joy] こんにちは [anim=nod_small]")

    assert parsed is not None
    assert parsed.text == "こんにちは"
    assert parsed.face == "Joy"
    assert parsed.animation == "nod_small"


def test_parse_fallback_garbage_stays_allowed() -> None:
    provider = make_provider()
    parsed = provider._parse_fallback("not json but still speech")

    assert parsed is not None
    assert parsed.text == "not json but still speech"
    assert parsed.face in provider.settings.available_faces
    assert parsed.animation in provider.settings.available_animations


def test_foreground_app_context_text() -> None:
    provider = make_provider()
    request = ChatRequest(
        request_id="test",
        message="今なにしてる？",
        context=RequestContext(
            extra={
                "foreground_app": {
                    "category": "Browser",
                    "display_name": "Chrome",
                    "process_name": "chrome",
                    "window_title": "ignored for privacy",
                }
            }
        ),
    )

    context = provider._foreground_app_context_text(request)

    assert context == "category=Browser, app=Chrome, process=chrome"
