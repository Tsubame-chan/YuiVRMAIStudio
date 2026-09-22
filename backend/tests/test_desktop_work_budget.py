import importlib.util
from pathlib import Path


def test_work_expands_local_output_without_slowing_talk_or_changing_stt():
    path = Path(__file__).resolve().parents[2] / "scripts/yui_desktop_inference.py"
    spec = importlib.util.spec_from_file_location("yui_worker", path)
    worker = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(worker)
    assert worker.output_token_budget("Chat", {"Mode": "standard"}) == 256
    assert worker.output_token_budget("Chat", {"Mode": "work"}) == 1536
    assert worker.output_token_budget("Transcription", {"Mode": "work"}) == 512
