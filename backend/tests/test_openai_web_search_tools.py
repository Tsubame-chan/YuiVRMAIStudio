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
