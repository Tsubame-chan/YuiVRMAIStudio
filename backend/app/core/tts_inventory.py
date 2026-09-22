"""Distinguish an installed, stopped engine from an unused default URL."""
import os
from pathlib import Path
from urllib.parse import urlparse

from app.core.config import ROOT_DIR


def aivis_installed(base_url: str) -> bool:
    if urlparse(base_url).hostname not in {"127.0.0.1", "localhost", "::1"}:
        return False
    candidates = [
        ROOT_DIR.parent / "tools/tts/aivis-engine/extracted/macOS-arm64/run",
        Path("/Applications/AivisSpeech.app/Contents/Resources/engine/run"),
        Path.home() / "Applications/AivisSpeech.app/Contents/Resources/engine/run",
    ]
    if os.environ.get("AIVIS_ENGINE_EXE"):
        candidates.insert(0, Path(os.environ["AIVIS_ENGINE_EXE"]))
    return any(path.is_file() and os.access(path, os.X_OK) for path in candidates)
