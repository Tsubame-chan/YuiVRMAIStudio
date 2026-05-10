import asyncio
import base64
from io import BytesIO
import json
from uuid import uuid4
import wave

try:
    import audioop
except ImportError:  # pragma: no cover - removed in newer Python versions
    audioop = None

from app.core.config import Settings
from app.db.repositories import ChatRepository, MemoryRepository
from app.models.chat import ChatResponse
from app.models.memory import MemorySaveRequest
from app.models.realtime import (
    RealtimeAudioResponse,
    RealtimeMode,
    RealtimeProbeResponse,
    RealtimeStatusResponse,
)

try:
    from fastapi import WebSocket
except ImportError:  # pragma: no cover
    WebSocket = None


class RealtimeProviderError(RuntimeError):
    pass


class RealtimeProvider:
    def __init__(
        self,
        settings: Settings,
        chat_repository: ChatRepository | None = None,
        memory_repository: MemoryRepository | None = None,
    ):
        self.settings = settings
        self.chat_repository = chat_repository
        self.memory_repository = memory_repository

    def status(self) -> RealtimeStatusResponse:
        return RealtimeStatusResponse(
            configured=bool(self.settings.openai_api_key),
            voice_model=self.settings.openai_realtime_model,
            translation_model=self.settings.openai_realtime_translate_model,
            transcription_model=self.settings.openai_realtime_transcribe_model,
            voice=self.settings.openai_realtime_voice,
            modes=["stable", "voice", "voice_text", "translate"],
            warning=(
                "Realtime modes are experimental and can consume tokens/audio quickly. "
                "Enable them only while actively testing."
            ),
        )

    async def probe(self, mode: RealtimeMode, connect: bool) -> RealtimeProbeResponse:
        model = self._model_for(mode)
        endpoint = f"wss://api.openai.com/v1/realtime?model={model}"
        if not self.settings.openai_api_key:
            return RealtimeProbeResponse(
                ok=False,
                mode=mode,
                model=model,
                endpoint=endpoint,
                message="OpenAI API key is not configured.",
            )

        if not connect:
            return RealtimeProbeResponse(
                ok=True,
                mode=mode,
                model=model,
                endpoint=endpoint,
                message="Realtime configuration is present. Probe skipped network connection.",
            )

        try:
            first_event_type = await self._connect_once(endpoint)
        except Exception as exc:  # pragma: no cover - depends on network and API availability
            return RealtimeProbeResponse(
                ok=False,
                mode=mode,
                model=model,
                endpoint=endpoint,
                message=f"Realtime connection failed: {exc}",
            )

        return RealtimeProbeResponse(
            ok=True,
            mode=mode,
            model=model,
            endpoint=endpoint,
            connected=True,
            first_event_type=first_event_type,
            message="Realtime connection opened and closed successfully.",
        )

    async def respond_to_wav(
        self,
        wav_bytes: bytes,
        mode: RealtimeMode,
        instructions: str = "",
    ) -> RealtimeAudioResponse:
        if not self.settings.openai_api_key:
            raise RealtimeProviderError("OpenAI API key is not configured.")
        if mode == "voice_text":
            mode = "voice"
        if mode == "transcribe":
            mode = "voice"

        model = self._model_for(mode)
        endpoint = f"wss://api.openai.com/v1/realtime?model={model}"
        pcm16 = self._wav_to_pcm16_mono_24k(wav_bytes)
        if not pcm16:
            raise RealtimeProviderError("Audio payload is empty or unsupported.")

        websocket = await self._connect(endpoint)
        text_parts: list[str] = []
        audio_parts: list[str] = []
        events: list[str] = []
        system_instructions = instructions.strip() or (
            "あなたは日本語で自然に会話するVRMアバターです。"
            "短く、会話として返してください。"
            "可能な範囲で、明るく若い女性らしい高めの声に寄せてください。"
        )
        if mode == "translate":
            system_instructions = instructions.strip() or (
                "You are a realtime interpreter. Translate every Japanese utterance into natural English only. "
                "Do not answer questions, acknowledge setup requests, or add commentary. "
                "Speak clearly with the brightest, most youthful feminine voice available."
            )

        try:
            await websocket.send(json.dumps({
                "type": "session.update",
                "session": {
                    "modalities": ["text", "audio"],
                    "instructions": system_instructions,
                    "input_audio_format": "pcm16",
                    "output_audio_format": "pcm16",
                    "voice": self.settings.openai_realtime_voice,
                    "turn_detection": None,
                },
            }))

            chunk_size = 12_000
            for start in range(0, len(pcm16), chunk_size):
                chunk = pcm16[start:start + chunk_size]
                await websocket.send(json.dumps({
                    "type": "input_audio_buffer.append",
                    "audio": base64.b64encode(chunk).decode("ascii"),
                }))
            await websocket.send(json.dumps({"type": "input_audio_buffer.commit"}))
            await websocket.send(json.dumps({
                "type": "response.create",
                "response": {"modalities": ["text", "audio"]},
            }))

            while True:
                raw = await asyncio.wait_for(websocket.recv(), timeout=30)
                event = json.loads(raw)
                event_type = event.get("type", "")
                if event_type:
                    events.append(event_type)
                if event_type in {
                    "response.text.delta",
                    "response.output_text.delta",
                    "response.audio_transcript.delta",
                }:
                    text_parts.append(event.get("delta", ""))
                elif event_type == "response.audio.delta":
                    audio_parts.append(event.get("delta", ""))
                elif event_type in {"response.done", "error"}:
                    if event_type == "error":
                        raise RealtimeProviderError(json.dumps(event.get("error", event), ensure_ascii=False))
                    break
        finally:
            await websocket.close()

        return RealtimeAudioResponse(
            text="".join(text_parts).strip(),
            audio_base64="".join(audio_parts),
            events=events[-40:],
        )

    async def relay_unity_stream(self, unity_socket: WebSocket) -> None:
        if not self.settings.openai_api_key:
            await unity_socket.send_json({
                "type": "error",
                "message": "OpenAI API key is not configured.",
            })
            return

        openai_socket = None
        mode: RealtimeMode = "voice"
        user_id = self.settings.default_user_id
        last_user_text = ""
        response_text_parts: list[str] = []
        pending_response_text = ""
        response_active = False

        async def unity_to_openai() -> None:
            nonlocal openai_socket, mode, user_id, response_active
            while True:
                message = await unity_socket.receive_json()
                message_type = message.get("type")
                if message_type == "start":
                    requested_mode = message.get("mode")
                    if requested_mode == "translate":
                        mode = "translate"
                    elif requested_mode in {"voice_text", "voicevox"}:
                        mode = "voice_text"
                    else:
                        mode = "voice"
                    user_id = (message.get("user_id") or user_id or self.settings.default_user_id).strip()
                    endpoint = f"wss://api.openai.com/v1/realtime?model={self._model_for(mode)}"
                    openai_socket = await self._connect(endpoint)
                    instructions = message.get("instructions") or self._default_stream_instructions(mode)
                    instructions = self._with_realtime_context(instructions, user_id, mode)
                    response_modalities = ["text"] if mode == "voice_text" else ["text", "audio"]
                    session = {
                        "modalities": response_modalities,
                        "instructions": instructions,
                        "input_audio_format": "pcm16",
                        "input_audio_transcription": {
                            "model": self.settings.openai_transcribe_model,
                        },
                        "turn_detection": self._turn_detection_for(mode),
                    }
                    if mode != "voice_text":
                        session["output_audio_format"] = "pcm16"
                        session["voice"] = self.settings.openai_realtime_voice
                    await openai_socket.send(json.dumps({
                        "type": "session.update",
                        "session": session,
                    }))
                    await unity_socket.send_json({
                        "type": "ready",
                        "mode": mode,
                        "voice": self.settings.openai_realtime_voice if mode != "voice_text" else "voicevox",
                        "turn_detection": session["turn_detection"],
                    })
                elif message_type == "audio":
                    if openai_socket is not None:
                        await openai_socket.send(json.dumps({
                            "type": "input_audio_buffer.append",
                            "audio": message.get("audio", ""),
                        }))
                elif message_type == "stop":
                    if openai_socket is not None:
                        response_modalities = ["text"] if mode == "voice_text" else ["text", "audio"]
                        await openai_socket.send(json.dumps({"type": "input_audio_buffer.commit"}))
                        await openai_socket.send(json.dumps({
                            "type": "response.create",
                            "response": {"modalities": response_modalities},
                        }))
                        response_active = True
                elif message_type == "close":
                    break

        async def openai_to_unity() -> None:
            nonlocal last_user_text, response_text_parts, pending_response_text, response_active
            while openai_socket is None:
                await asyncio.sleep(0.01)

            while True:
                raw = await openai_socket.recv()
                event = json.loads(raw)
                event_type = event.get("type", "")
                if event_type in {
                    "session.created",
                    "session.updated",
                    "input_audio_buffer.speech_started",
                    "input_audio_buffer.speech_stopped",
                    "response.created",
                }:
                    if event_type == "input_audio_buffer.speech_started":
                        last_user_text = ""
                        if mode == "voice_text" and response_active and openai_socket is not None:
                            await openai_socket.send(json.dumps({"type": "response.cancel"}))
                            response_active = False
                            response_text_parts = []
                            pending_response_text = ""
                            await unity_socket.send_json({"type": "event", "event": "response.cancelled"})
                    if event_type == "response.created":
                        response_text_parts = []
                        pending_response_text = ""
                        response_active = True
                    await unity_socket.send_json({"type": "event", "event": event_type})
                elif event_type in {
                    "response.text.delta",
                    "response.output_text.delta",
                    "response.audio_transcript.delta",
                }:
                    delta = event.get("delta", "")
                    response_text_parts.append(delta)
                    await unity_socket.send_json({"type": "text_delta", "delta": delta})
                elif event_type == "conversation.item.input_audio_transcription.completed":
                    transcript = (event.get("transcript") or "").strip()
                    if transcript:
                        last_user_text = transcript
                        if pending_response_text:
                            self._save_realtime_turn(user_id, last_user_text, pending_response_text, mode)
                            pending_response_text = ""
                    await unity_socket.send_json({"type": "event", "event": event_type})
                elif event_type == "response.audio.delta":
                    if mode != "voice_text":
                        await unity_socket.send_json({"type": "audio_delta", "audio": event.get("delta", "")})
                elif event_type == "response.done":
                    response_active = False
                    response = event.get("response")
                    if isinstance(response, dict) and response.get("status") in {"cancelled", "canceled"}:
                        response_text_parts = []
                        pending_response_text = ""
                        await unity_socket.send_json({"type": "event", "event": "response.cancelled"})
                        continue
                    response_text = "".join(response_text_parts).strip() or self._extract_response_text(event)
                    if last_user_text:
                        self._save_realtime_turn(user_id, last_user_text, response_text, mode)
                    else:
                        pending_response_text = response_text
                    await unity_socket.send_json({"type": "done"})
                elif event_type == "error":
                    error = event.get("error", event)
                    if isinstance(error, dict) and error.get("code") == "input_audio_buffer_commit_empty":
                        continue
                    await unity_socket.send_json({
                        "type": "error",
                        "message": json.dumps(error, ensure_ascii=False),
                    })

        try:
            tasks = [
                asyncio.create_task(unity_to_openai()),
                asyncio.create_task(openai_to_unity()),
            ]
            done, pending = await asyncio.wait(tasks, return_when=asyncio.FIRST_COMPLETED)
            for task in pending:
                task.cancel()
            for task in done:
                task.result()
        finally:
            if openai_socket is not None:
                await openai_socket.close()

    @staticmethod
    def _turn_detection_for(mode: RealtimeMode) -> dict:
        if mode == "voice_text":
            return {
                "type": "server_vad",
                "threshold": 0.6,
                "prefix_padding_ms": 500,
                "silence_duration_ms": 1400,
                "create_response": True,
                "interrupt_response": True,
            }
        if mode == "translate":
            return {
                "type": "server_vad",
                "threshold": 0.5,
                "prefix_padding_ms": 300,
                "silence_duration_ms": 650,
                "create_response": True,
                "interrupt_response": True,
            }
        return {
            "type": "server_vad",
            "threshold": 0.5,
            "prefix_padding_ms": 300,
            "silence_duration_ms": 500,
            "create_response": True,
            "interrupt_response": True,
        }

    @staticmethod
    def _default_stream_instructions(mode: RealtimeMode) -> str:
        if mode == "translate":
            return (
                "You are a realtime interpreter. Translate every Japanese utterance into natural English only. "
                "Do not answer questions, acknowledge setup requests, or add commentary. "
                "Speak clearly with the brightest, most youthful feminine voice available."
            )
        if mode == "voice_text":
            return (
                "あなたは日本語で自然に会話するVRMアバターです。"
                "音声はUnity側のVOICEVOXで読み上げます。必ずテキストだけを返してください。"
                "返答は短めに、音声会話として聞き取りやすくしてください。"
                "Web検索、天気、最新情報、外部アプリ操作はこのモードではできません。"
                "求められた場合は、調べているふりをせず、このモードでは取得できないことを短く伝えてください。"
            )
        return (
            "あなたは日本語で自然に会話するVRMアバターです。"
            "短く、会話として返してください。"
            "可能な範囲で、明るく若い女性らしい高めの声に寄せてください。"
            "Web検索、天気、最新情報、外部アプリ操作はこのモードではできません。"
            "求められた場合は、調べているふりをせず、このモードでは取得できないことを短く伝えてください。"
        )

    def _model_for(self, mode: RealtimeMode) -> str:
        if mode == "voice_text":
            return self.settings.openai_realtime_model
        if mode == "translate":
            # The dedicated translation guide uses a separate surface. For this
            # Unity WebSocket spike, keep the transport on gpt-realtime and drive
            # translation through session instructions so audio output still works.
            return self.settings.openai_realtime_model
        if mode == "transcribe":
            return self.settings.openai_realtime_transcribe_model
        return self.settings.openai_realtime_model

    @staticmethod
    def _extract_response_text(event: dict) -> str:
        response = event.get("response")
        if not isinstance(response, dict):
            return ""

        parts: list[str] = []

        def collect(value: object) -> None:
            if isinstance(value, dict):
                for key in ("text", "transcript"):
                    text = value.get(key)
                    if isinstance(text, str) and text.strip():
                        parts.append(text)
                for nested in value.values():
                    collect(nested)
            elif isinstance(value, list):
                for nested in value:
                    collect(nested)

        collect(response.get("output", response))
        return "".join(parts).strip()

    def _with_realtime_context(self, instructions: str, user_id: str, mode: RealtimeMode) -> str:
        if mode == "translate":
            return instructions

        context_parts: list[str] = []
        if self.memory_repository is not None:
            try:
                memories = self.memory_repository.list_recent(user_id, limit=5)
                if memories:
                    context_parts.append(
                        "保存済みメモリ:\n" + "\n".join(f"- {item.content}" for item in memories)
                    )
            except Exception:
                pass

        if self.chat_repository is not None:
            try:
                messages = self.chat_repository.list_recent_messages(user_id, limit=8)
                if messages:
                    lines = []
                    for message in messages:
                        role = "User" if message.get("role") == "user" else "Assistant"
                        content = (message.get("content") or "").strip()
                        if content:
                            lines.append(f"{role}: {content}")
                    if lines:
                        context_parts.append("直近の会話:\n" + "\n".join(lines))
            except Exception:
                pass

        if not context_parts:
            return instructions

        return (
            instructions.rstrip()
            + "\n\n以下は会話継続のための参考情報です。ユーザーに読み上げず、自然な返答にだけ反映してください。\n"
            + "\n\n".join(context_parts)
        )

    def _save_realtime_turn(
        self,
        user_id: str,
        user_text: str,
        response_text: str,
        mode: RealtimeMode,
    ) -> None:
        if mode == "translate" or self.chat_repository is None:
            return
        if not user_text or not response_text:
            return

        request_id = f"realtime-{uuid4().hex}"
        try:
            self.chat_repository.save_chat_turn(
                request_id=request_id,
                user_id=user_id or self.settings.default_user_id,
                user_message=user_text,
                response=ChatResponse(text=response_text),
                provider="openai-realtime",
                model=self._model_for(mode),
                usage_metadata={"mode": mode},
            )
        except Exception:
            return

        if self.memory_repository is None:
            return
        if not any(keyword in user_text for keyword in ("覚えて", "記憶して", "忘れないで", "remember")):
            return
        try:
            self.memory_repository.save(MemorySaveRequest(
                user_id=user_id or self.settings.default_user_id,
                content=user_text,
                importance=4,
                tags=["realtime", "user-requested"],
            ))
        except Exception:
            return

    async def _connect_once(self, endpoint: str) -> str | None:
        websocket = await self._connect(endpoint)
        try:
            try:
                raw = await asyncio.wait_for(websocket.recv(), timeout=5)
            except asyncio.TimeoutError:
                return None
        finally:
            await websocket.close()

        try:
            event = json.loads(raw)
        except (TypeError, json.JSONDecodeError):
            return None
        return event.get("type")

    async def _connect(self, endpoint: str):
        try:
            import websockets
        except ImportError as exc:  # pragma: no cover
            raise RealtimeProviderError(
                "The 'websockets' package is required for realtime probes."
            ) from exc

        headers = {
            "Authorization": f"Bearer {self.settings.openai_api_key}",
            "OpenAI-Beta": "realtime=v1",
        }

        try:
            websocket = await websockets.connect(
                endpoint,
                additional_headers=headers,
                open_timeout=10,
            )
        except TypeError:
            websocket = await websockets.connect(
                endpoint,
                extra_headers=headers,
                open_timeout=10,
            )
        return websocket

    @staticmethod
    def _wav_to_pcm16_mono_24k(wav_bytes: bytes) -> bytes:
        with wave.open(BytesIO(wav_bytes), "rb") as reader:
            channels = reader.getnchannels()
            sample_width = reader.getsampwidth()
            sample_rate = reader.getframerate()
            frames = reader.readframes(reader.getnframes())

        if sample_width != 2:
            if audioop is None:
                raise RealtimeProviderError("Only 16-bit WAV is supported without audioop.")
            frames = audioop.lin2lin(frames, sample_width, 2)
            sample_width = 2
        if channels > 1:
            if audioop is None:
                raise RealtimeProviderError("Mono conversion requires audioop.")
            frames = audioop.tomono(frames, sample_width, 0.5, 0.5)
            channels = 1
        if sample_rate != 24000:
            if audioop is None:
                raise RealtimeProviderError("Resampling requires audioop.")
            frames, _ = audioop.ratecv(frames, sample_width, channels, sample_rate, 24000, None)
        return frames
