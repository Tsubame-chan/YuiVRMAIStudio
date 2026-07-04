#!/usr/bin/env python3
"""Audit local AI assets that are easy to confuse with disposable caches."""

from __future__ import annotations

import argparse
import json
from dataclasses import dataclass
from pathlib import Path


@dataclass(frozen=True)
class Finding:
    severity: str
    path: str
    message: str


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--project-root", type=Path, default=Path("."))
    parser.add_argument(
        "--fail-on-desktop-missing",
        action="store_true",
        help="Treat the desktop-preferred Gemma E4B asset as a blocker.",
    )
    return parser.parse_args()


def load_manifest(root: Path) -> dict:
    path = root / "unity/Assets/StreamingAssets/YuiLocalAI/local_ai_model_packs.json"
    if not path.exists():
        return {}
    return json.loads(path.read_text(encoding="utf-8"))


def find_pack(manifest: dict, pack_id: str) -> dict | None:
    for pack in manifest.get("packs", []):
        if pack.get("id") == pack_id:
            return pack
    return None


def supports(pack: dict | None, *platforms: str) -> bool:
    values = set(pack.get("platforms", []) if pack else [])
    return all(platform in values for platform in platforms)


def collect_findings(root: Path, fail_on_desktop_missing: bool) -> list[Finding]:
    findings: list[Finding] = []

    def require(relative: str, message: str) -> None:
        if not (root / relative).exists():
            findings.append(Finding("BLOCKER", relative, message))

    def prefer(relative: str, message: str) -> None:
        if not (root / relative).exists():
            findings.append(
                Finding(
                    "BLOCKER" if fail_on_desktop_missing else "WARN",
                    relative,
                    message,
                )
            )

    require(
        "unity/Assets/StreamingAssets/YuiLocalAI/Models/gemma-4-E2B-it.litertlm",
        "mobile/default desktop fallback Gemma E2B model is missing",
    )
    prefer(
        "unity/Assets/StreamingAssets/YuiLocalAI/Models/gemma-4-E4B-it.litertlm",
        "desktop-preferred Gemma E4B model is not installed; Mac/Windows will fall back to E2B",
    )
    require(
        "unity/Assets/StreamingAssets/YuiLocalAI/Aivis/Models/female_voice_1.aivmx",
        "embedded mobile Aivis default voice is missing",
    )
    require(
        "unity/Assets/StreamingAssets/YuiLocalAI/Aivis/Runtime/JapaneseBert/model_fp16.onnx",
        "embedded mobile Aivis Japanese BERT runtime asset is missing",
    )
    require(
        "unity/Assets/StreamingAssets/YuiLocalAI/Voicevox/Models/meimei_himari_1.vvm",
        "embedded VOICEVOX fallback voice model is missing",
    )
    require(
        "tools/tts/aivis-engine/extracted/macOS-arm64/run",
        "desktop Aivis audition engine is missing",
    )

    for voice in ["female_voice_1", "female_voice_2", "male_voice_1"]:
        require(
            f"tools/tts/aivis-models/selected/{voice}.aivmx",
            f"selected Aivis source voice {voice} is missing",
        )

    manifest = load_manifest(root)
    e4b = find_pack(manifest, "core_text")
    e2b = find_pack(manifest, "core_text_e2b")
    vision = find_pack(manifest, "vision_gemma4_e2b")
    if not e4b or not e4b.get("enabled_by_default") or not supports(e4b, "macos", "windows"):
        findings.append(Finding("BLOCKER", "unity/Assets/StreamingAssets/YuiLocalAI/local_ai_model_packs.json", "Gemma E4B must be the enabled desktop pack for macOS/Windows"))
    if not e2b or not e2b.get("enabled_by_default") or not supports(e2b, "ios", "android", "macos", "windows"):
        findings.append(Finding("BLOCKER", "unity/Assets/StreamingAssets/YuiLocalAI/local_ai_model_packs.json", "Gemma E2B must remain enabled for mobile and desktop fallback"))
    if not vision or not vision.get("enabled_by_default") or not supports(vision, "ios", "android"):
        findings.append(Finding("BLOCKER", "unity/Assets/StreamingAssets/YuiLocalAI/local_ai_model_packs.json", "Gemma E2B vision pack must remain enabled for mobile"))

    return findings


def main() -> int:
    args = parse_args()
    root = args.project_root.resolve()
    findings = collect_findings(root, args.fail_on_desktop_missing)

    print("Yui local AI asset audit")
    print(f"Project: {root}")
    print()
    for finding in findings:
        print(f"{finding.severity}: {finding.path} - {finding.message}")

    blockers = [finding for finding in findings if finding.severity == "BLOCKER"]
    print()
    print(f"Summary: {len(blockers)} blocker(s), {len(findings)} total finding(s).")
    return 1 if blockers else 0


if __name__ == "__main__":
    raise SystemExit(main())
