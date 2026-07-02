#!/usr/bin/env python3
"""Prune generated Android Aivis assets to a compact one-voice package.

Android APK packaging is still constrained by Zip32 placement limits. The
source StreamingAssets pack can keep multiple Aivis voices for iOS/desktop, but
the generated Android Gradle project should keep one voice until asset packs or
an install-time model downloader exists.
"""

from __future__ import annotations

import argparse
import json
from pathlib import Path


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "--aivis-root",
        type=Path,
        required=True,
        help="Generated Gradle assets/YuiLocalAI/Aivis directory.",
    )
    parser.add_argument(
        "--voice-key",
        default="female_voice_1",
        help="Single Aivis voice key to keep in the Android APK.",
    )
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    root = args.aivis_root
    catalog_path = root / "aivis_voices.json"
    if not catalog_path.is_file():
        raise FileNotFoundError(catalog_path)

    catalog = json.loads(catalog_path.read_text(encoding="utf-8"))
    voices = catalog.get("voices") or []
    kept = [voice for voice in voices if voice.get("key") == args.voice_key]
    if len(kept) != 1:
        raise RuntimeError(f"Aivis voice key was not found exactly once: {args.voice_key}")

    catalog["voices"] = kept
    catalog["default_voice_id"] = int(kept[0]["id"])
    catalog_path.write_text(json.dumps(catalog, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")

    keep_names = {
        f"{args.voice_key}.aivmx",
        f"{args.voice_key}.hyper_parameters.json",
        f"{args.voice_key}.manifest.json",
        f"{args.voice_key}.style_vectors.npy",
    }
    removed = 0
    for directory in (root / "Models", root / "Metadata"):
        if not directory.is_dir():
            continue
        for path in directory.iterdir():
            if path.is_file() and path.name not in keep_names:
                path.unlink()
                removed += 1

    print(f"Kept Android Aivis voice: {args.voice_key}; removed {removed} generated files.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
