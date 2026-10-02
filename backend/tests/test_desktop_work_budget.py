import importlib.util
from pathlib import Path


def test_work_expands_local_output_without_slowing_talk_or_changing_stt():
    path = Path(__file__).resolve().parents[2] / "scripts/yui_desktop_inference.py"
    spec = importlib.util.spec_from_file_location("yui_worker", path)
    worker = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(worker)
    assert worker.output_token_budget("Chat", {"Mode": "standard"}) == 1280
    assert worker.output_token_budget("Chat", {"Mode": "work"}) == 2304
    assert worker.output_token_budget("Transcription", {"Mode": "work"}) == 512


def test_worker_honors_selected_model_budgets_and_clamps_invalid_limits():
    path = Path(__file__).resolve().parents[2] / "scripts/yui_desktop_inference.py"
    spec = importlib.util.spec_from_file_location("yui_worker", path)
    worker = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(worker)
    assert worker.output_token_budget("Chat", {"Mode": "standard"}, {"max_output_tokens": 512}) == 512
    assert worker.output_token_budget("Chat", {"Mode": "work"}, {"max_output_tokens": 1536}) == 1536
    assert worker.output_token_budget("Chat", {"Mode": "work"}, {"max_output_tokens": 2304}) == 2304
    assert worker.output_token_budget("Chat", {}, {"max_output_tokens": -1}) == 256
    assert worker.output_token_budget("Chat", {}, {"max_output_tokens": 99999}) == 4096
    assert worker.output_token_budget("Transcription", {}, {"max_output_tokens": 99999}) == 512
