"""Release metadata shared by runtime settings and build/package tools."""
import json
import re
from pathlib import Path

METADATA_PATH = Path(__file__).with_name("release_versions.json")


def read_release_versions(path: Path = METADATA_PATH) -> dict[str, str]:
    values = json.loads(path.read_text(encoding="utf-8"))
    patterns = {
        "desktop_tag": r"v[0-9]+\.[0-9]+\.[0-9]+(?:-[0-9A-Za-z.-]+)?",
        "ios_version": r"[0-9]+\.[0-9]+\.[0-9]+",
        "ios_build": r"[1-9][0-9]*",
    }
    for key, pattern in patterns.items():
        if not isinstance(values.get(key), str) or not re.fullmatch(pattern, values[key]):
            raise ValueError(f"Invalid release metadata: {key} in {path}")
    return values


def desktop_version() -> str:
    return read_release_versions()["desktop_tag"].removeprefix("v")
