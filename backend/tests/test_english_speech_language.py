from types import SimpleNamespace
from app.core.config import Settings
from app.models.chat import ChatRequest
from app.providers.openai_chat import OpenAIChatProvider
from app.providers.openai_stt import OpenAISTTProvider
from app.core.shared_conversation import SharedChat


def test_chat_language_preserves_japanese_default_and_english_instruction():
    provider = OpenAIChatProvider.__new__(OpenAIChatProvider)
    provider.settings = Settings(openai_api_key="test-key")
    assert "Reply in natural Japanese" in provider._instructions(ChatRequest(request_id="ja", message="hello"))
    instruction = provider._instructions(ChatRequest(request_id="en", message="hello", language_code="en"))
    assert "Reply in natural English" in instruction
    assert "Reply in natural Japanese" not in instruction
    assert SharedChat(request_id="test", session_id="test", message="hello", language_code="en").language_code == "en"


def test_transcription_passes_language_without_network():
    calls = []
    def create(**kwargs):
        calls.append(kwargs)
        return SimpleNamespace(text="Hello!")
    provider = OpenAISTTProvider.__new__(OpenAISTTProvider)
    provider.settings = Settings(openai_api_key="test-key")
    provider.client = SimpleNamespace(audio=SimpleNamespace(transcriptions=SimpleNamespace(create=create)))
    assert provider._transcribe_sync(b"test", "test.wav", "en") == "Hello!"
    assert calls[-1]["language"] == "en"
    provider._transcribe_sync(b"test", "test.wav")
    assert calls[-1]["language"] == "ja"
