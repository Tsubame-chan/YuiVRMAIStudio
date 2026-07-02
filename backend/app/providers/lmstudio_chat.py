import json
from typing import Any

import httpx

from app.core.config import Settings
from app.models.chat import ChatRequest, ChatResponse, OpenAIChatOutput
from app.providers.interfaces import ChatProvider
from app.providers.openai_chat import ChatProviderError, OpenAIChatProvider


class LMStudioChatProvider(ChatProvider):
    name = "lmstudio"

    def __init__(
        self,
        settings: Settings,
        *,
        transport: httpx.AsyncBaseTransport | None = None,
    ):
        self.settings = settings
        base_url = settings.litert_lm_base_url if settings.chat_provider == "litert_lm" else settings.lmstudio_base_url
        self._model = settings.litert_lm_chat_model if settings.chat_provider == "litert_lm" else settings.lmstudio_chat_model
        self._client = httpx.AsyncClient(
            base_url=base_url,
            timeout=90.0,
            transport=transport,
        )
        self._openai_helpers = OpenAIChatProvider.__new__(OpenAIChatProvider)
        self._openai_helpers.settings = settings

    async def generate(
        self,
        request: ChatRequest,
        history: list[dict[str, str]] | None = None,
    ) -> ChatResponse:
        payload = {
            "model": self._model,
            "messages": [
                {"role": "system", "content": self._openai_helpers._instructions(request)},
                *self._history_as_messages(history or []),
                self._current_user_message(request),
            ],
            "temperature": 0.7,
            "max_tokens": self.settings.openai_max_output_tokens,
            "stream": False,
        }

        try:
            response = await self._client.post("/chat/completions", json=payload)
            response.raise_for_status()
            text = self._extract_content(response.json())
            parsed = self._openai_helpers._parse_fallback(text)
            if parsed is None:
                parsed = OpenAIChatOutput(
                    text=text.strip() or "ローカルモデルから空の返答が返ってきました。",
                    face="Neutral",
                    animation="idle_normal",
                    voice_style="normal",
                    should_use_vision=False,
                    memory_action="none",
                    should_tts=True,
                )
            return self._openai_helpers._normalize_response(parsed)
        except httpx.HTTPError as exc:
            raise ChatProviderError(str(exc)) from exc
        except Exception as exc:
            raise ChatProviderError(str(exc)) from exc

    def _history_as_messages(self, history: list[dict[str, str]]) -> list[dict[str, str]]:
        return [
            {
                "role": item["role"] if item.get("role") in {"user", "assistant"} else "user",
                "content": item["content"],
            }
            for item in history
            if item.get("content")
        ]

    def _current_user_message(self, request: ChatRequest) -> dict[str, str]:
        content = request.message
        custom_instruction = request.custom_instruction.strip()
        if custom_instruction:
            content += (
                "\n\nLower-priority user custom instruction for Yui's behavior in this session:\n"
                + custom_instruction[:1200]
            )
        return {"role": "user", "content": content}

    def _extract_content(self, payload: dict[str, Any]) -> str:
        choices = payload.get("choices")
        if not isinstance(choices, list) or not choices:
            return ""
        first = choices[0]
        if not isinstance(first, dict):
            return ""
        message = first.get("message")
        if isinstance(message, dict):
            content = message.get("content")
            if isinstance(content, str):
                return content
            if isinstance(content, list):
                return "\n".join(
                    str(item.get("text"))
                    for item in content
                    if isinstance(item, dict) and item.get("text")
                )
        text = first.get("text")
        if isinstance(text, str):
            return text
        return json.dumps(payload, ensure_ascii=False)
