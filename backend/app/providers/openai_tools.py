from typing import Any

from app.core.config import Settings


WEB_SEARCH_TRIGGERS = (
    "検索",
    "調べ",
    "今日",
    "明日",
    "現在",
    "最新",
    "直近",
    "今月",
    "来月",
    "ニュース",
    "天気",
    "雨",
    "気温",
    "台風",
    "地図",
    "場所",
    "近く",
    "行き方",
    "営業時間",
    "価格",
    "株価",
    "為替",
    "発売日",
    "スケジュール",
    "祭り",
    "お祭り",
    "花火",
    "イベント",
    "開催",
    "weather",
    "forecast",
    "news",
    "latest",
    "current",
    "upcoming",
    "event",
    "festival",
    "near me",
    "map",
    "directions",
    "price",
)


def build_web_search_tools(settings: Settings, text: str) -> list[dict[str, Any]]:
    if not should_offer_web_search(settings, text):
        return []

    location: dict[str, str] = {"type": "approximate"}
    if settings.openai_web_search_country:
        location["country"] = settings.openai_web_search_country
    if settings.openai_web_search_city:
        location["city"] = settings.openai_web_search_city
    if settings.openai_web_search_region:
        location["region"] = settings.openai_web_search_region
    if settings.openai_web_search_timezone:
        location["timezone"] = settings.openai_web_search_timezone

    tool: dict[str, Any] = {
        "type": "web_search",
        "search_context_size": settings.openai_web_search_context_size,
    }
    if len(location) > 1:
        tool["user_location"] = location
    return [tool]


def should_offer_web_search(settings: Settings, text: str) -> bool:
    if not settings.openai_web_search_enabled:
        return False

    mode = settings.openai_web_search_mode
    if mode == "off":
        return False
    if mode == "always":
        return True

    normalized_text = text.lower()
    return any(trigger in normalized_text for trigger in WEB_SEARCH_TRIGGERS)
