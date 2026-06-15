import base64
import json
import logging
import re
from datetime import datetime, timezone
from uuid import uuid4

import json5
from fastapi.concurrency import run_in_threadpool
from openai import OpenAI, OpenAIError

from app.core.config import Settings
from app.models.vision import OpenAIVisionOutput, VisionResponse, VisionStructured
from app.providers.interfaces import VisionProvider


class VisionProviderError(RuntimeError):
    pass


logger = logging.getLogger(__name__)


class OpenAIVisionProvider(VisionProvider):
    name = "openai"

    def __init__(self, settings: Settings):
        if not settings.openai_api_key:
            raise VisionProviderError("OPENAI_API_KEY is not configured.")
        self.settings = settings
        self.client = OpenAI(api_key=settings.openai_api_key)

    async def analyze_image(
        self,
        *,
        image_bytes: bytes,
        prompt_type: str,
        mime_type: str = "image/jpeg",
    ) -> VisionResponse:
        try:
            return await run_in_threadpool(
                self._analyze_image,
                image_bytes,
                prompt_type,
                mime_type,
            )
        except OpenAIError as exc:
            raise VisionProviderError(str(exc)) from exc
        except Exception as exc:
            raise VisionProviderError(str(exc)) from exc

    def _analyze_image(
        self,
        image_bytes: bytes,
        prompt_type: str,
        mime_type: str,
    ) -> VisionResponse:
        data_url = self._to_data_url(image_bytes, mime_type)
        model = self.settings.openai_vision_model or self.settings.openai_chat_model
        vision_input = self._vision_input(data_url, prompt_type)

        try:
            response = self.client.responses.parse(
                model=model,
                instructions=self._instructions(),
                input=vision_input,
                text_format=OpenAIVisionOutput,
                max_output_tokens=self.settings.openai_vision_max_output_tokens,
            )
        except OpenAIError:
            raise
        except Exception as exc:
            logger.warning(
                "OpenAI vision structured parse failed; retrying with text fallback. error=%s",
                exc,
            )
            response = self.client.responses.create(
                model=model,
                instructions=(
                    self._instructions()
                    + " If strict JSON formatting is difficult, return a concise Japanese summary."
                ),
                input=vision_input,
                max_output_tokens=self.settings.openai_vision_max_output_tokens,
            )

        summary, structured = self._parse_response(response)
        return VisionResponse(
            vision_result_id="vision_" + uuid4().hex,
            summary=summary,
            structured=structured,
            created_at=datetime.now(timezone.utc).isoformat(),
        )

    def _instructions(self) -> str:
        return (
            "You are the vision layer for Yui, a Japanese embodied AI assistant. "
            "Inspect the image carefully. "
            "Describe only what is directly visible in the image. "
            "Do not infer brand names, product names, places, people, text, labels, or exact quantities "
            "unless they are clearly readable or visually unmistakable. "
            "If something is unclear, say it is unclear instead of guessing. "
            "Be specific about visible objects, colors, shapes, positions, and uncertainty. "
            "Keep the structured output compact: include only the most important visible objects, "
            "and keep each visible detail short."
        )

    def _vision_input(self, data_url: str, prompt_type: str) -> list[dict[str, object]]:
        return [
            {
                "role": "user",
                "content": [
                    {
                        "type": "input_text",
                        "text": self._prompt(prompt_type),
                    },
                    {
                        "type": "input_image",
                        "image_url": data_url,
                        "detail": self.settings.openai_vision_detail,
                    },
                ],
            }
        ]

    def _prompt(self, prompt_type: str) -> str:
        if prompt_type == "screen":
            subject = "the user's current screen or application screenshot"
        elif prompt_type == "camera":
            subject = "the user's camera image"
        else:
            subject = "the image"

        return (
            f"Analyze {subject}. "
            "Return a short summary for the Unity chat log plus structured visible details. "
            "Use Japanese for summary and visible detail text unless the image contains important English text. "
            "When text is too small, blurred, partially hidden, or unreadable, explicitly say that it cannot be read. "
            "Prefer 'looks like' or 'may be' for low-confidence observations."
        )

    def _parse_response(self, response: object) -> tuple[str, VisionStructured]:
        parsed = getattr(response, "output_parsed", None)
        if parsed is not None:
            return self._from_output(parsed)

        text = getattr(response, "output_text", "") or ""
        stripped = self._strip_json_fence(text)
        candidates = [stripped, *self._json_object_candidates(stripped)]
        for candidate in candidates:
            for loader in (json.loads, json5.loads):
                try:
                    data = loader(candidate)
                    if isinstance(data, str):
                        data = loader(self._strip_json_fence(data))
                    if isinstance(data, dict):
                        return self._from_output(OpenAIVisionOutput.model_validate(data))
                except Exception:
                    pass

        return self._safe_fallback(stripped)

    def _from_output(self, output: OpenAIVisionOutput) -> tuple[str, VisionStructured]:
        summary = self._clean_summary(output.summary)
        structured = VisionStructured(
            objects=output.objects or [],
            extra=output.extra.model_dump() if output.extra else {},
        )
        if not summary:
            summary = self._summary_from_structured(structured)
        return summary, structured

    def _safe_fallback(self, raw_text: str) -> tuple[str, VisionStructured]:
        if not raw_text:
            return "画像を解析しました。", VisionStructured()

        compact = re.sub(r"\s+", " ", raw_text).strip()
        if compact.startswith("{") or '"objects"' in compact or '"summary"' in compact:
            return "画像を解析しました。詳細情報は内部コンテキストに保存しました。", VisionStructured(
                extra={"raw": raw_text}
            )

        return compact[:220], VisionStructured(extra={"raw": raw_text})

    def _summary_from_structured(self, structured: VisionStructured) -> str:
        if structured.objects:
            labels = []
            for item in structured.objects[:3]:
                parts = [item.color, item.shape, item.type]
                labels.append(" ".join(part for part in parts if part))
            if labels:
                return "画像内に " + "、".join(labels) + " が見えます。"

        scene = structured.extra.get("scene") if structured.extra else None
        if isinstance(scene, str) and scene.strip():
            return scene.strip()

        return "画像を解析しました。"

    def _clean_summary(self, summary: str) -> str:
        if not summary:
            return ""

        cleaned = re.sub(r"\s+", " ", summary).strip()
        if cleaned.startswith("{") or '"objects"' in cleaned:
            for candidate in self._json_object_candidates(cleaned):
                try:
                    data = json5.loads(candidate)
                    if isinstance(data, dict) and data.get("summary"):
                        return str(data["summary"]).strip()
                except Exception:
                    pass
            return ""

        return cleaned

    def _json_object_candidates(self, text: str) -> list[str]:
        candidates: list[str] = []
        start = text.find("{")
        end = text.rfind("}")
        if 0 <= start < end:
            candidates.append(text[start : end + 1])

        for match in re.finditer(r"\{.*?\}", text, flags=re.DOTALL):
            candidates.append(match.group(0))

        return candidates

    def _strip_json_fence(self, text: str) -> str:
        stripped = text.strip()
        if stripped.startswith("```json"):
            stripped = stripped[7:]
        elif stripped.startswith("```"):
            stripped = stripped[3:]
        if stripped.endswith("```"):
            stripped = stripped[:-3]
        return stripped.strip()

    def _to_data_url(self, image_bytes: bytes, mime_type: str) -> str:
        encoded = base64.b64encode(image_bytes).decode("ascii")
        return f"data:{mime_type};base64,{encoded}"
