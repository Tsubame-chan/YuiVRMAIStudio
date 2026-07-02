#!/usr/bin/env python3
"""Validate the embedded Aivis runtime asset contract.

This script intentionally validates presence and shape only. It does not claim
that synthesis is working; native ONNX execution must still report readiness at
runtime before Unity exposes AivisSpeech HD (Offline).
"""

from __future__ import annotations

import argparse
import json
from dataclasses import dataclass
from pathlib import Path


@dataclass(frozen=True)
class Requirement:
    component: str
    relative_path: str
    minimum_bytes: int = 1
    ready_manifest: bool = False


REQUIREMENTS = (
    Requirement("onnxruntime", "Runtime/ONNXRuntime/manifest.json", ready_manifest=True),
    Requirement("style_bert_vits2_runtime", "Runtime/StyleBertVits2/manifest.json", ready_manifest=True),
    Requirement("japanese_bert_onnx", "Runtime/JapaneseBert/model_fp16.onnx", 1024 * 1024),
    Requirement("japanese_bert_tokenizer", "Runtime/JapaneseBert/tokenizer.json", 1024),
    Requirement("japanese_text_frontend", "Runtime/JapaneseTextFrontend/manifest.json", ready_manifest=True),
)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "--root",
        type=Path,
        default=Path("unity/Assets/StreamingAssets/YuiLocalAI/Aivis"),
        help="Aivis StreamingAssets root.",
    )
    parser.add_argument(
        "--json",
        action="store_true",
        help="Print a machine-readable status document.",
    )
    parser.add_argument(
        "--platform",
        default="",
        choices=["", "ios", "android", "macos", "windows"],
        help="Validate platform_ready manifests for a specific platform.",
    )
    return parser.parse_args()


def manifest_is_ready(path: Path, platform: str) -> bool:
    try:
        manifest = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError):
        return False
    if manifest.get("status") == "ready":
        return True
    ready_platforms = manifest.get("ready_platforms")
    return bool(platform and isinstance(ready_platforms, list) and platform in ready_platforms)


def missing_components(root: Path, platform: str) -> list[str]:
    missing: list[str] = []
    for requirement in REQUIREMENTS:
        path = root / requirement.relative_path
        if not path.is_file() or path.stat().st_size < requirement.minimum_bytes:
            missing.append(requirement.component)
            continue
        if requirement.ready_manifest:
            if not manifest_is_ready(path, platform):
                missing.append(requirement.component)
    return missing


def main() -> int:
    args = parse_args()
    root = args.root
    missing = missing_components(root, args.platform)
    status = {
        "ok": not missing,
        "runtime_ready": not missing,
        "root": str(root),
        "platform": args.platform,
        "missing_components": missing,
        "required_components": [requirement.component for requirement in REQUIREMENTS],
    }

    if args.json:
        print(json.dumps(status, ensure_ascii=False, indent=2))
    elif missing:
        print("Aivis runtime assets are incomplete:")
        for component in missing:
            print(f"  - {component}")
    else:
        print(f"Aivis runtime assets are present: {root}")

    return 0 if not missing else 1


if __name__ == "__main__":
    raise SystemExit(main())
