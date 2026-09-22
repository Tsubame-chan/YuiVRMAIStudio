from app.core.config import Settings
from app.providers.openai_tools import build_web_search_tools


def test_web_search_tools_auto_skips_general_chat() -> None:
    settings = Settings(openai_api_key="test-key")

    assert build_web_search_tools(settings, "こんにちは") == []


def test_web_search_tools_auto_enables_current_weather_lookup() -> None:
    settings = Settings(openai_api_key="test-key")

    assert build_web_search_tools(settings, "今日の東京の天気は？") == [
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


def test_web_search_tools_auto_enables_near_term_festival_lookup() -> None:
    settings = Settings(openai_api_key="test-key")

    tools = build_web_search_tools(settings, "東京都で直近1ヶ月ぐらいでお祭り何かありますか？")

    assert tools
    assert tools[0]["type"] == "web_search"


def test_web_search_tools_always_mode_provides_model_choice() -> None:
    settings = Settings(openai_api_key="test-key", openai_web_search_mode="always")

    tools = build_web_search_tools(settings, "夕飯の献立を考えて")

    assert tools
    assert tools[0]["type"] == "web_search"


def test_web_search_tools_off_mode_wins_over_enabled_flag() -> None:
    settings = Settings(
        openai_api_key="test-key",
        openai_web_search_enabled=True,
        openai_web_search_mode="off",
    )

    assert build_web_search_tools(settings, "今日のニュースを調べて") == []


def test_search_citations_support_sdk_objects_and_do_not_accept_local_files() -> None:
    from types import SimpleNamespace as Item
    from app.providers.openai_tools import append_response_citations
    citation = Item(type="url_citation", title="Official\nsource", url="https://example.com/news")
    response = Item(output=[Item(content=[Item(annotations=[citation, citation,
        Item(type="url_citation", url="file:///private/data")])])])
    text = append_response_citations("Result", response)
    assert text.count("https://example.com/news") == 1
    assert "Official source" in text
    assert "file:" not in text


def test_search_annotations_are_displayed_without_being_read_aloud() -> None:
    from types import SimpleNamespace as Item
    from app.providers.openai_chat import OpenAIChatProvider
    from app.models.chat import ChatRequest, OpenAIChatOutput
    provider = OpenAIChatProvider.__new__(OpenAIChatProvider)
    provider.settings = Settings(openai_api_key="test-key")
    output = OpenAIChatOutput(text="調べた結果です。", spoken_text="", face="Neutral", animation="idle_normal", voice_style="normal", should_use_vision=False, memory_action="none", should_tts=True)
    def parse(**kwargs):
        assert kwargs["store"] is False
        assert kwargs["tools"][0]["type"] == "web_search"
        return Item(output_parsed=output, output=[Item(content=[Item(annotations=[{"type":"url_citation","title":"Source","url":"https://example.com"}])])])
    provider.client = Item(responses=Item(parse=parse))
    request = ChatRequest(request_id="citations", message="Search the web", mode="work")
    result = provider._normalize_response(provider._generate_structured(request, []), request)
    assert "https://example.com" in result.text
    assert result.spoken_text == "調べた結果です。"
