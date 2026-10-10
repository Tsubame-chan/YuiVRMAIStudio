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
        self.name = "litert_lm" if settings.chat_provider == "litert_lm" else "lmstudio"
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
            "max_tokens": self._openai_helpers._max_output_tokens(request),
            "stream": False,
        }

        try:
            response = await self._client.post("/chat/completions", json=payload)
            response.raise_for_status()
            text = self._extract_content(response.json())
            if not text.strip():
                raise ChatProviderError("Local model returned no assistant text. Check the model, output budget, and server response format.")
            parsed = self._openai_helpers._parse_fallback(text)
            if parsed is None:
                parsed = OpenAIChatOutput(
                    text=text.strip(),
                    spoken_text="",
                    face="Neutral",
                    animation="idle_normal",
                    voice_style="normal",
                    should_use_vision=False,
                    memory_action="none",
                    should_tts=True,
                )
            result = self._openai_helpers._normalize_response(parsed, request)
            if not result.text.strip():
                raise ChatProviderError("Local model returned no assistant text after response normalization.")
            return result
        except ChatProviderError:
            raise
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
        memory_context = self._openai_helpers._memory_context_text(request)
        if memory_context:
            content += "\n\nRelevant saved character statements (reference data):\n" + memory_context
        if request.response_instruction.strip():
            content += "\n\nUser-configured response style (keep character personality):\n" + request.response_instruction.strip()[:1200]
        custom_instruction = request.custom_instruction.strip()
        if custom_instruction:
            content += (
                "\n\nLower-priority user custom instruction for Yui's behavior in this session:\n"
                + custom_instruction[:1200]
            )
        return {"role": "user", "content": content}

    def _extract_content(self, payload: dict[str, Any]) -> str:
        if not isinstance(payload, dict):
            return ""
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
                    item["text"]
                    for item in content
                    if isinstance(item, dict) and item.get("type") in {None, "text"}
                    and isinstance(item.get("text"), str)
                )
        text = first.get("text")
        if isinstance(text, str):
            return text
        return ""
