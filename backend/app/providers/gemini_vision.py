import base64
import json
from datetime import datetime, timezone
from uuid import uuid4

import httpx

from app.core.config import Settings
from app.models.vision import VisionResponse, VisionStructured
from app.providers.interfaces import VisionProvider


class VisionProviderError(RuntimeError):
    pass


class GeminiVisionProvider(VisionProvider):
    name = "gemini"

    def __init__(self, settings: Settings):
        if not settings.gemini_api_key:
            raise VisionProviderError("GEMINI_API_KEY is not configured.")
        if not settings.gemini_vision_model:
            raise VisionProviderError("GEMINI_VISION_MODEL is not configured.")
        self.settings = settings

    async def analyze_image(
        self,
        *,
        image_bytes: bytes,
        prompt_type: str,
        mime_type: str = "image/jpeg",
    ) -> VisionResponse:
        prompt = self._prompt(prompt_type)
        payload = {
            "contents": [
                {
                    "parts": [
                        {
                            "inline_data": {
                                "mime_type": mime_type,
                                "data": base64.b64encode(image_bytes).decode("ascii"),
                            }
                        },
                        {"text": prompt},
                    ]
                }
            ],
            "generationConfig": {
                "response_mime_type": "application/json",
            },
        }
        url = (
            "https://generativelanguage.googleapis.com/v1beta/models/"
            f"{self.settings.gemini_vision_model}:generateContent"
        )

        try:
            async with httpx.AsyncClient(timeout=45.0) as client:
                response = await client.post(
                    url,
                    headers={
                        "x-goog-api-key": self.settings.gemini_api_key,
                        "Content-Type": "application/json",
                    },
                    json=payload,
                )
                response.raise_for_status()
        except httpx.HTTPError as exc:
            raise VisionProviderError(str(exc)) from exc

        text = self._extract_text(response.json())
        summary, structured = self._parse_response(text)
        return VisionResponse(
            vision_result_id="vision_" + uuid4().hex,
            summary=summary,
            structured=structured,
            created_at=datetime.now(timezone.utc).isoformat(),
        )

    def _prompt(self, prompt_type: str) -> str:
        if prompt_type == "screen":
            subject = "the user's current desktop or application screenshot"
        elif prompt_type == "camera":
            subject = "the camera image"
        else:
            subject = "the image"

        return (
            f"Analyze {subject} for a local embodied AI assistant named Yui. "
            "Describe only what is directly visible. Do not infer brand names, product names, "
            "places, people, text, labels, or exact quantities unless they are clearly readable "
            "or visually unmistakable. If unsure, say so instead of guessing. "
            "Reply only as JSON with this shape: "
            '{"summary":"short Japanese summary for conversation context",'
            '"objects":[{"type":"string","brand_or_type":null,'
            '"estimated_total_volume_ml":null,"estimated_remaining_ratio":null,'
            '"estimated_consumed_ml":null,"confidence":"low|medium|high"}],'
            '"extra":{}}. '
            "If text is too small, blurred, partially hidden, or unreadable, say that it cannot be read. "
            "Use conservative confidence."
        )

    def _extract_text(self, payload: dict) -> str:
        candidates = payload.get("candidates") or []
        parts = (
            candidates[0]
            .get("content", {})
            .get("parts", [])
            if candidates
            else []
        )
        texts = [part.get("text", "") for part in parts if part.get("text")]
        return "\n".join(texts).strip()

    def _parse_response(self, text: str) -> tuple[str, VisionStructured]:
        if not text:
            return "画像を解析できませんでした。", VisionStructured()

        text = self._strip_json_fence(text)
        try:
            data = json.loads(text)
            if isinstance(data, str):
                data = json.loads(self._strip_json_fence(data))
            if not isinstance(data, dict):
                raise ValueError("Gemini response JSON is not an object.")
            summary = str(data.get("summary") or "画像を解析しました。")
            structured = VisionStructured(
                objects=data.get("objects") or [],
                extra=data.get("extra") or {},
            )
            return summary, structured
        except (TypeError, ValueError, json.JSONDecodeError):
            return text, VisionStructured(extra={"raw": text})

    def _strip_json_fence(self, text: str) -> str:
        stripped = text.strip()
        if stripped.startswith("```json"):
            stripped = stripped[7:]
        elif stripped.startswith("```"):
            stripped = stripped[3:]
        if stripped.endswith("```"):
            stripped = stripped[:-3]
        return stripped.strip()
