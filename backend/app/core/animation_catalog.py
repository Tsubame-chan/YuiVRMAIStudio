import json
from functools import lru_cache
from pathlib import Path


CATALOG_PATH = Path(__file__).with_name("yui_animation_catalog.json")


@lru_cache
def load_animation_catalog() -> dict[str, tuple[str, ...]]:
    with CATALOG_PATH.open("r", encoding="utf-8") as handle:
        raw = json.load(handle)
    return {
        "faces": tuple(str(item) for item in raw.get("faces", ()) if str(item).strip()),
        "animations": tuple(
            str(item) for item in raw.get("animations", ()) if str(item).strip()
        ),
    }


def available_faces() -> tuple[str, ...]:
    return load_animation_catalog()["faces"]


def available_animations() -> tuple[str, ...]:
    return load_animation_catalog()["animations"]
