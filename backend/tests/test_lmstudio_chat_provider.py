import json

import httpx
import pytest

from app.core.config import Settings
from app.models.chat import ChatRequest
from app.providers.lmstudio_chat import LMStudioChatProvider
from app.providers.router import ProviderRouter


def test_provider_router_returns_lmstudio_chat_provider() -> None:
    provider = ProviderRouter(Settings(chat_provider="lmstudio")).chat()

    assert isinstance(provider, LMStudioChatProvider)


@pytest.mark.anyio
async def test_lmstudio_chat_posts_openai_compatible_payload() -> None:
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
                                    "text": "ローカルで返答しています。",
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

    provider = LMStudioChatProvider(
        Settings(
            chat_provider="lmstudio",
            lmstudio_base_url="https://lmstudio.example.test/v1",
            lmstudio_chat_model="local-yui",
        ),
        transport=httpx.MockTransport(handler),
    )

    response = await provider.generate(
        ChatRequest(request_id="test", message="こんにちは"),
        history=[{"role": "assistant", "content": "前の返答です。"}],
    )

    assert response.text == "ローカルで返答しています。"
    assert response.face == "Joy"
    assert response.animation == "nod_small"
    assert captured_request is not None
    assert captured_request.url == "https://lmstudio.example.test/v1/chat/completions"
    payload = json.loads(captured_request.content)
    assert payload["model"] == "local-yui"
    assert payload["messages"][0]["role"] == "system"
    assert payload["messages"][1] == {"role": "assistant", "content": "前の返答です。"}
    assert payload["messages"][2]["role"] == "user"
    assert payload["messages"][2]["content"] == "こんにちは"


@pytest.mark.anyio
async def test_lmstudio_chat_falls_back_from_plain_text() -> None:
    provider = LMStudioChatProvider(
        Settings(chat_provider="lmstudio", lmstudio_base_url="https://lmstudio.example.test/v1"),
        transport=httpx.MockTransport(
            lambda _: httpx.Response(
                200,
                json={"choices": [{"message": {"content": "[face: Joy] もちろん [anim=nod_small]"}}]},
            )
        ),
    )

    response = await provider.generate(ChatRequest(request_id="test", message="お願い"))

    assert response.text == "もちろん"
    assert response.face == "Joy"
    assert response.animation == "nod_small"
