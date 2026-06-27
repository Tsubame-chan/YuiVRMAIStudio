import json

import httpx
import pytest

from app.core.config import Settings
from app.models.chat import ChatRequest
from app.providers.router import ProviderRouter
from app.providers.xai_chat import XAIChatProvider


def test_provider_router_returns_xai_chat_provider() -> None:
    provider = ProviderRouter(Settings(chat_provider="xai", xai_api_key="test-key")).chat()

    assert isinstance(provider, XAIChatProvider)


@pytest.mark.anyio
async def test_xai_chat_posts_authorized_openai_compatible_payload() -> None:
    captured_request: httpx.Request | None = None

    def handler(request: httpx.Request) -> httpx.Response:
        nonlocal captured_request
        captured_request = request
        return httpx.Response(
            200,
            json={
                "choices": [
                    {
                        "message": {
                            "content": json.dumps(
                                {
                                    "text": "Grok側から返答しています。",
                                    "face": "Joy",
                                    "animation": "nod_small",
                                    "voice_style": "normal",
                                    "should_use_vision": False,
                                    "memory_action": "none",
                                    "should_tts": True,
                                },
                                ensure_ascii=False,
                            )
                        }
                    }
                ]
            },
        )

    provider = XAIChatProvider(
        Settings(
            chat_provider="xai",
            xai_api_key="test-key",
            xai_base_url="https://api.x.ai/v1",
            xai_chat_model="grok-4.3",
        ),
        transport=httpx.MockTransport(handler),
    )

    response = await provider.generate(ChatRequest(request_id="test", message="こんにちは"))

    assert response.text == "Grok側から返答しています。"
    assert response.face == "Joy"
    assert captured_request is not None
    assert captured_request.url == "https://api.x.ai/v1/chat/completions"
    assert captured_request.headers["authorization"] == "Bearer test-key"
    payload = json.loads(captured_request.content)
    assert payload["model"] == "grok-4.3"
    assert payload["messages"][0]["role"] == "system"
    assert payload["messages"][1] == {"role": "user", "content": "こんにちは"}
