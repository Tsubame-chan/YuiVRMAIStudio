import asyncio
import base64
from io import BytesIO
import json
import re
from uuid import uuid4
import wave

from fastapi.concurrency import run_in_threadpool
from openai import OpenAI, OpenAIError

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
            translation_model=self.settings.openai_realtime_model,
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
        endpoint = self._endpoint_for(mode, model)
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
        if mode == "transcribe":
            mode = "voice"
        if mode == "translate":
            return await self._respond_to_translate_wav(wav_bytes)

        model = self._model_for(mode)
        endpoint = self._endpoint_for(mode, model)
        pcm16 = self._wav_to_pcm16_mono_24k(wav_bytes)
        if not pcm16:
            raise RealtimeProviderError("Audio payload is empty or unsupported.")

        websocket = await self._connect(endpoint)
        text_parts: list[str] = []
        audio_parts: list[str] = []
        events: list[str] = []
        input_transcript = ""
        system_instructions = instructions.strip() or (
            "あなたは日本語で自然に会話するVRMアバターです。"
            "短く、会話として返してください。"
            "『短くまとめると』『少し整理して』など、返答方針の前置きは言わず、答えから始めてください。"
            "可能な範囲で、明るく若い女性らしい高めの声に寄せてください。"
        )
        if mode == "translate":
            system_instructions = self._with_realtime_context(
                self._default_stream_instructions(mode),
                self.settings.default_user_id,
                mode,
            )
        elif mode == "voice_text":
            system_instructions = instructions.strip() or self._default_stream_instructions(mode)

        try:
            response_modalities = ["text"] if mode == "voice_text" else ["audio"]
            await websocket.send(json.dumps({
                "type": "session.update",
                "session": self._realtime_session_config(
                    mode=mode,
                    instructions=system_instructions,
                    response_modalities=response_modalities,
                    turn_detection=None,
                ),
            }))

            chunk_size = 12_000
            for start in range(0, len(pcm16), chunk_size):
                chunk = pcm16[start:start + chunk_size]
                await websocket.send(json.dumps({
                    "type": self._append_audio_event_for(mode),
                    "audio": base64.b64encode(chunk).decode("ascii"),
                }))
            await websocket.send(json.dumps({"type": "input_audio_buffer.commit"}))
            await websocket.send(json.dumps({
                "type": "response.create",
                "response": {"output_modalities": response_modalities},
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
                    "response.output_audio_transcript.delta",
                    "session.output_transcript.delta",
                }:
                    text_parts.append(event.get("delta", ""))
                elif event_type == "conversation.item.input_audio_transcription.completed":
                    input_transcript = (event.get("transcript") or "").strip()
                elif event_type in {"response.audio.delta", "response.output_audio.delta", "session.output_audio.delta"}:
                    audio_parts.append(event.get("delta", ""))
                elif event_type in {"response.done", "session.closed", "error"}:
                    if event_type == "error":
                        raise RealtimeProviderError(json.dumps(event.get("error", event), ensure_ascii=False))
                    break
        finally:
            await websocket.close()

        response_text = "".join(text_parts).strip()
        if not response_text and events and events[-1] == "response.done":
            response_text = self._extract_response_text(event)
        if input_transcript and response_text:
            self._save_realtime_turn(
                self.settings.default_user_id,
                input_transcript,
                response_text,
                mode,
            )
        return RealtimeAudioResponse(
            text=response_text,
            input_text=input_transcript,
            audio_base64="".join(audio_parts),
            events=events[-40:],
        )

    async def _respond_to_translate_wav(self, wav_bytes: bytes) -> RealtimeAudioResponse:
        input_transcript = await run_in_threadpool(
            self._transcribe_wav_for_translate,
            wav_bytes,
            "realtime_translate_phrase.wav",
        )
        input_transcript = input_transcript.strip()
        if not input_transcript:
            return RealtimeAudioResponse(events=["stt.empty"])

        translated_text = await run_in_threadpool(
            self._translate_text_for_realtime,
            input_transcript,
            self.settings.default_user_id,
        )
        translated_text = translated_text.strip()
        if not translated_text:
            return RealtimeAudioResponse(input_text=input_transcript, events=["translation.empty"])

        model = self._model_for("translate")
        endpoint = self._endpoint_for("translate", model)
        websocket = await self._connect(endpoint)
        audio_parts: list[str] = []
        events: list[str] = ["stt.completed", "translation.completed"]
        text_to_speech_event_names = {
            "session.created",
            "session.updated",
            "conversation.item.created",
            "conversation.item.added",
            "response.created",
            "response.output_audio.done",
            "response.done",
            "session.closed",
            "error",
        }

        try:
            await websocket.send(json.dumps({
                "type": "session.update",
                "session": self._realtime_session_config(
                    mode="translate",
                    instructions=(
                        "You are a text-to-speech engine. Speak exactly the provided text. "
                        "Do not translate, answer, explain, add greetings, or add filler words."
                    ),
                    response_modalities=["audio"],
                    turn_detection=None,
                ),
            }))
            await websocket.send(json.dumps({
                "type": "conversation.item.create",
                "item": {
                    "type": "message",
                    "role": "user",
                    "content": [{"type": "input_text", "text": translated_text}],
                },
            }))
            await websocket.send(json.dumps({
                "type": "response.create",
                "response": {"output_modalities": ["audio"]},
            }))

            while True:
                raw = await asyncio.wait_for(websocket.recv(), timeout=30)
                event = json.loads(raw)
                event_type = event.get("type", "")
                if event_type in text_to_speech_event_names:
                    events.append(event_type)
                if event_type in {"response.audio.delta", "response.output_audio.delta", "session.output_audio.delta"}:
                    audio_parts.append(event.get("delta", ""))
                elif event_type in {"response.done", "session.closed", "error"}:
                    if event_type == "error":
                        raise RealtimeProviderError(json.dumps(event.get("error", event), ensure_ascii=False))
                    break
        finally:
            await websocket.close()

        self._save_realtime_turn(
            self.settings.default_user_id,
            input_transcript,
            translated_text,
            "translate",
        )
        return RealtimeAudioResponse(
            text=translated_text,
            input_text=input_transcript,
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
                    endpoint = self._stream_endpoint_for(mode, self._stream_model_for(mode))
                    openai_socket = await self._connect(endpoint)
                    instructions = message.get("instructions") or self._default_stream_instructions(mode)
                    instructions = self._with_realtime_context(instructions, user_id, mode)
                    response_modalities = ["text"] if mode == "voice_text" else ["audio"]
                    session = (
                        self._realtime_session_config(
                            mode=mode,
                            instructions=instructions,
                            response_modalities=response_modalities,
                            turn_detection=self._turn_detection_for(mode),
                        )
                    )
                    await openai_socket.send(json.dumps({
                        "type": "session.update",
                        "session": session,
                    }))
                    await unity_socket.send_json({
                        "type": "ready",
                        "mode": mode,
                        "voice": self.settings.openai_realtime_voice if mode != "voice_text" else "voicevox",
                        "turn_detection": session.get("turn_detection") or session.get("audio", {}).get("input", {}).get("turn_detection"),
                    })
                elif message_type == "audio":
                    if openai_socket is not None:
                        await openai_socket.send(json.dumps({
                            "type": self._stream_append_audio_event_for(mode),
                            "audio": message.get("audio", ""),
                        }))
                elif message_type == "stop":
                    if openai_socket is not None:
                        response_modalities = ["text"] if mode == "voice_text" else ["audio"]
                        await openai_socket.send(json.dumps({"type": "input_audio_buffer.commit"}))
                        if mode != "voice_text":
                            await openai_socket.send(json.dumps({
                                "type": "response.create",
                                "response": {"output_modalities": response_modalities},
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
                    "input_audio_buffer.committed",
                    "conversation.item.added",
                    "conversation.item.done",
                    "response.output_item.added",
                    "response.output_item.done",
                    "response.content_part.added",
                    "response.content_part.done",
                    "response.output_text.done",
                    "response.output_audio.done",
                    "response.output_audio_transcript.done",
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
                    "response.output_audio_transcript.delta",
                    "session.output_transcript.delta",
                }:
                    delta = event.get("delta", "")
                    response_text_parts.append(delta)
                    await unity_socket.send_json({"type": "text_delta", "delta": delta})
                elif event_type == "conversation.item.input_audio_transcription.delta":
                    # Partial STT events are extremely noisy in Unity Console and are not
                    # needed for client state. Forward only the completed transcript below.
                    continue
                elif event_type in {"session.input_transcript.delta", "conversation.item.input_audio_transcription.completed"}:
                    if event_type == "conversation.item.input_audio_transcription.completed":
                        transcript = (event.get("transcript") or "").strip()
                        await unity_socket.send_json({
                            "type": "event",
                            "event": event_type,
                            "transcript": transcript,
                        })
                        if transcript and self._looks_like_spoken_input(transcript):
                            last_user_text = transcript
                            if pending_response_text:
                                self._save_realtime_turn(user_id, last_user_text, pending_response_text, mode)
                                pending_response_text = ""
                            if mode == "voice_text" and openai_socket is not None and not response_active:
                                await openai_socket.send(json.dumps({
                                    "type": "response.create",
                                    "response": {"output_modalities": ["text"]},
                                }))
                                response_active = True
                        elif mode == "voice_text":
                            await unity_socket.send_json({
                                "type": "event",
                                "event": "input_audio_buffer.no_speech",
                                "transcript": transcript,
                            })
                    elif event_type != "conversation.item.input_audio_transcription.completed":
                        await unity_socket.send_json({"type": "event", "event": event_type})
                elif event_type in {"response.audio.delta", "response.output_audio.delta", "session.output_audio.delta"}:
                    if mode != "voice_text":
                        await unity_socket.send_json({"type": "audio_delta", "audio": event.get("delta", "")})
                elif event_type == "session.output_transcript.done":
                    transcript = (event.get("transcript") or "").strip()
                    if transcript:
                        response_text_parts = [transcript]
                    await unity_socket.send_json({"type": "event", "event": event_type})
                elif event_type == "session.output_audio.done":
                    await unity_socket.send_json({"type": "done"})
                elif event_type in {"response.done", "session.closed"}:
                    response_active = False
                    if event_type == "response.done":
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
                    code = self._realtime_error_code(error)
                    await unity_socket.send_json({
                        "type": "error",
                        "code": code,
                        "message": self._friendly_realtime_error(error),
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
                "create_response": False,
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

    def _endpoint_for(self, mode: RealtimeMode, model: str) -> str:
        return f"wss://api.openai.com/v1/realtime?model={model}"

    @staticmethod
    def _stream_endpoint_for(mode: RealtimeMode, model: str) -> str:
        return f"wss://api.openai.com/v1/realtime?model={model}"

    def _realtime_session_config(
        self,
        mode: RealtimeMode,
        instructions: str,
        response_modalities: list[str],
        turn_detection: dict | None,
    ) -> dict:
        session = {
            "type": "realtime",
            "model": self._stream_model_for(mode),
            "output_modalities": response_modalities,
            "instructions": instructions,
            "audio": {
                "input": {
                    "format": self._pcm16_audio_format(),
                    "transcription": {
                        "model": self.settings.openai_transcribe_model,
                        "prompt": self._transcription_prompt_for(mode),
                    },
                    "turn_detection": turn_detection,
                },
            },
        }
        if mode != "translate":
            session["audio"]["input"]["transcription"]["language"] = "ja"
        if mode != "voice_text":
            session["audio"]["output"] = {
                "format": self._pcm16_audio_format(),
                "voice": self.settings.openai_realtime_voice,
            }
        return session

    @staticmethod
    def _pcm16_audio_format() -> dict:
        return {
            "type": "audio/pcm",
            "rate": 24000,
        }

    @staticmethod
    def _transcription_prompt_for(mode: RealtimeMode) -> str:
        if mode == "translate":
            return (
                "Input may be Japanese, English, or a mix of both. "
                "Preserve the spoken language accurately before translation. "
                "For Japanese, avoid over-normalizing natural daily expressions into unrelated English brand or business terms. "
                "For English, keep proper nouns and everyday phrasing intact."
            )

        return (
            "主な入力は日本語です。美容、健康、睡眠、食事、日常会話の文脈です。"
            "日本語の発音を英語の商品名へ過剰に置き換えず、"
            "睡眠不足、肌荒れ、保湿、スキンケアなどの自然な日本語として認識してください。"
        )

    @staticmethod
    def _append_audio_event_for(mode: RealtimeMode) -> str:
        return "input_audio_buffer.append"

    @staticmethod
    def _stream_append_audio_event_for(mode: RealtimeMode) -> str:
        return "input_audio_buffer.append"

    @staticmethod
    def _looks_like_spoken_input(transcript: str) -> bool:
        text = re.sub(r"\s+", "", transcript.strip())
        if len(text) < 2:
            return False

        lowered = text.lower()
        non_speech_markers = {
            "拍手",
            "拍手音",
            "通知音",
            "効果音",
            "物音",
            "音",
            "音楽",
            "bgm",
            "music",
            "clap",
            "clapping",
            "applause",
        }
        if lowered in non_speech_markers:
            return False

        speech_chars = re.findall(r"[A-Za-z0-9ぁ-んァ-ン一-龯]", text)
        return len(speech_chars) >= 2

    @staticmethod
    def _realtime_error_code(error: object) -> str | None:
        if isinstance(error, dict):
            code = error.get("code")
            return str(code) if code else None
        return None

    @staticmethod
    def _friendly_realtime_error(error: object) -> str:
        if isinstance(error, dict):
            code = error.get("code")
            if code == "beta_api_shape_disabled":
                return (
                    "Realtime APIの仕様が更新されたため、このRealtimeモードは現在利用できません。"
                    "通常の音声入力またはテキスト入力を使ってください。"
                )
            message = error.get("message")
            if message:
                return str(message)
            return json.dumps(error, ensure_ascii=False)
        if error:
            return str(error)
        return "Realtime error"

    @staticmethod
    def _default_stream_instructions(mode: RealtimeMode) -> str:
        if mode == "translate":
            return (
                "You are a realtime interpreter between Japanese and English. "
                "If the user speaks Japanese, translate it into natural English. "
                "If the user speaks English, translate it into natural Japanese. "
                "If the utterance mixes both languages, translate each meaningful part into the other language while preserving names, titles, and numbers. "
                "Do not omit limiting words or constraints such as only, just, without, using only, だけ, だけで, or しか. "
                "Preserve concrete nouns, quantities, and negation. "
                "Do not answer questions, acknowledge setup requests, or add commentary; output only the translation. "
                "Use recent translation history only to resolve pronouns, omitted subjects, terminology, and continuity. "
                "Preserve the user's meaning literally unless a natural phrasing is required."
            )
        if mode == "voice_text":
            return (
                "あなたは日本語で自然に会話するVRMアバターです。"
                "音声はUnity側のVOICEVOXで読み上げます。必ずテキストだけを返してください。"
                "返答は原則1〜2文、80字前後を目安にしてください。"
                "複雑な質問では必要な要点を短くまとめ、相づちや短い質問にはさらに短く返してください。"
                "『短くまとめると』『少し整理して』など、返答方針の前置きは言わず、答えから始めてください。"
                "Web検索、天気、最新情報、外部アプリ操作はこのモードではできません。"
                "求められた場合は、調べているふりをせず、このモードでは取得できないことを短く伝えてください。"
            )
        return (
            "あなたは日本語で自然に会話するVRMアバターです。"
            "短く、会話として返してください。"
            "『短くまとめると』『少し整理して』など、返答方針の前置きは言わず、答えから始めてください。"
            "可能な範囲で、明るく若い女性らしい高めの声に寄せてください。"
            "Web検索、天気、最新情報、外部アプリ操作はこのモードではできません。"
            "求められた場合は、調べているふりをせず、このモードでは取得できないことを短く伝えてください。"
        )

    def _model_for(self, mode: RealtimeMode) -> str:
        if mode == "voice_text":
            return self.settings.openai_realtime_model
        if mode == "translate":
            return self.settings.openai_realtime_model
        if mode == "transcribe":
            return self.settings.openai_realtime_transcribe_model
        return self.settings.openai_realtime_model

    def _stream_model_for(self, mode: RealtimeMode) -> str:
        if mode == "transcribe":
            return self.settings.openai_realtime_transcribe_model
        if mode == "translate":
            return self.settings.openai_realtime_model
        return self.settings.openai_realtime_model

    def _transcribe_wav_for_translate(self, wav_bytes: bytes, filename: str) -> str:
        audio_file = BytesIO(wav_bytes)
        audio_file.name = filename or "realtime_translate_phrase.wav"
        client = OpenAI(api_key=self.settings.openai_api_key)
        try:
            transcription = client.audio.transcriptions.create(
                model=self.settings.openai_transcribe_model,
                file=audio_file,
                response_format="json",
                prompt=(
                    "Transcribe the user's speech accurately. "
                    "The input may be Japanese, English, or mixed. "
                    "Preserve short particles, limiting words, negation, quantities, names, and rhetorical questions."
                ),
            )
        except OpenAIError as exc:
            raise RealtimeProviderError(str(exc)) from exc
        return getattr(transcription, "text", "") or ""

    def _translate_text_for_realtime(self, text: str, user_id: str) -> str:
        client = OpenAI(api_key=self.settings.openai_api_key)
        instructions = (
            "Translate between Japanese and English. "
            "If the input is primarily Japanese, output natural English. "
            "If the input is primarily English, output natural Japanese. "
            "Output only the translation. "
            "Preserve every meaning-bearing detail, including limiting words, conditions, negation, quantities, and proper nouns. "
            "Do not answer the user's question and do not add prefaces."
        )
        context = self._translation_context_text(user_id)
        user_input = text if not context else context + "\n\nCurrent input:\n" + text
        try:
            response = client.responses.create(
                model=self.settings.openai_chat_model,
                instructions=instructions,
                input=user_input,
                max_output_tokens=160,
            )
        except OpenAIError as exc:
            raise RealtimeProviderError(str(exc)) from exc
        return (getattr(response, "output_text", "") or "").strip()

    def _translation_context_text(self, user_id: str) -> str:
        if self.chat_repository is None:
            return ""
        try:
            messages = self.chat_repository.list_recent_messages_by_mode(user_id, "translate", limit=8)
        except Exception:
            return ""

        lines: list[str] = []
        for message in messages:
            role = "Source" if message.get("role") == "user" else "Translation"
            content = (message.get("content") or "").strip()
            if content:
                lines.append(f"{role}: {content}")
        if not lines:
            return ""
        return (
            "Recent translation history for resolving pronouns and terminology only. "
            "Do not continue the conversation or add any of this text unless it is required by the current input:\n"
            + "\n".join(lines)
        )

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
            + "\n\n以下は会話継続のための参考情報です。ユーザーに読み上げず、文脈理解にだけ反映してください。\n"
            + "\n\n".join(context_parts)
        )

    def _save_realtime_turn(
        self,
        user_id: str,
        user_text: str,
        response_text: str,
        mode: RealtimeMode,
    ) -> None:
        if self.chat_repository is None:
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
