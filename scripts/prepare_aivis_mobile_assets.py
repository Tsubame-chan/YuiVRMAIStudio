#!/usr/bin/env python3
"""Prepare embedded AivisSpeech mobile assets from selected .aivmx files.

This script intentionally prepares assets only. It does not claim that native
mobile synthesis is ready; the app exposes Aivis only after the native runtime
reports runtime_ready=true.
"""

from __future__ import annotations

import argparse
import base64
import io
import json
import shutil
from pathlib import Path

import numpy as np
import onnx


VOICES = (
    ("female_voice_1", "女性ボイス①", 1431611904, 0),
    ("female_voice_2", "女性ボイス②", 604166016, 0),
    ("female_voice_3", "女性ボイス③", 1920374593, 1),
    ("male_voice_1", "男性ボイス①", 1310138976, 0),
)
DEFAULT_MOBILE_VOICES = (
    ("female_voice_1", "女性ボイス①", 1431611904, 0),
)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "--source",
        type=Path,
        default=Path("tools/tts/aivis-models/selected"),
        help="Directory containing selected .aivmx files.",
    )
    parser.add_argument(
        "--output",
        type=Path,
        default=Path("unity/Assets/StreamingAssets/YuiLocalAI/Aivis"),
        help="StreamingAssets Aivis output directory.",
    )
    parser.add_argument(
        "--prepare-runtime-layout",
        action="store_true",
        help="Also create the Aivis Runtime directory layout with non-ready manifests.",
    )
    parser.add_argument(
        "--include-all-voices",
        action="store_true",
        help="Embed all selected voices. The default mobile package embeds only the default Yui voice.",
    )
    return parser.parse_args()


def write_runtime_layout(output: Path) -> None:
    runtime_dir = output / "Runtime"
    manifests = {
        runtime_dir / "ONNXRuntime" / "manifest.json": {
            "component": "onnxruntime",
            "status": "missing",
            "note": "Add official ONNX Runtime binaries and mark this manifest ready only after validation.",
        },
        runtime_dir / "StyleBertVits2" / "manifest.json": {
            "component": "style_bert_vits2_runtime",
            "status": "missing",
            "note": "Add the native Style-Bert-VITS2/AIVMX execution runtime before enabling Aivis offline.",
        },
        runtime_dir / "JapaneseTextFrontend" / "manifest.json": {
            "component": "japanese_text_frontend",
            "status": "missing",
            "note": "Add native Japanese text normalization, G2P, accent, and dictionary assets.",
        },
        runtime_dir / "JapaneseBert" / "manifest.json": {
            "component": "japanese_bert",
            "status": "missing",
            "note": "Add model_fp16.onnx and tokenizer.json for Japanese BERT feature extraction.",
        },
    }
    for path, payload in manifests.items():
        path.parent.mkdir(parents=True, exist_ok=True)
        if not path.exists():
            path.write_text(json.dumps(payload, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def metadata_props(model_path: Path) -> dict[str, str]:
    model = onnx.load(str(model_path), load_external_data=False)
    return {prop.key: prop.value for prop in model.metadata_props}


def main() -> int:
    args = parse_args()
    models_dir = args.output / "Models"
    metadata_dir = args.output / "Metadata"
    models_dir.mkdir(parents=True, exist_ok=True)
    metadata_dir.mkdir(parents=True, exist_ok=True)

    catalog: list[dict[str, object]] = []
    voices = VOICES if args.include_all_voices else DEFAULT_MOBILE_VOICES
    for stem, label, voice_id, default_style_id in voices:
        source_model = args.source / f"{stem}.aivmx"
        if not source_model.exists():
            raise FileNotFoundError(source_model)

        target_model = models_dir / source_model.name
        shutil.copy2(source_model, target_model)

        props = metadata_props(target_model)
        hyper_parameters = json.loads(props["aivm_hyper_parameters"])
        manifest = json.loads(props["aivm_manifest"])
        style_vectors = np.load(io.BytesIO(base64.b64decode(props["aivm_style_vectors"])))

        (metadata_dir / f"{stem}.hyper_parameters.json").write_text(
            json.dumps(hyper_parameters, ensure_ascii=False, indent=2),
            encoding="utf-8",
        )
        (metadata_dir / f"{stem}.manifest.json").write_text(
            json.dumps(manifest, ensure_ascii=False, indent=2),
            encoding="utf-8",
        )
        np.save(metadata_dir / f"{stem}.style_vectors.npy", style_vectors)

        data = hyper_parameters.get("data", {})
        spk2id = data.get("spk2id") or {"speaker": 0}
        speaker_name = next(iter(spk2id.keys()))
        style2id = data.get("style2id") or {str(i): i for i in range(int(style_vectors.shape[0]))}
        if default_style_id not in {int(value) for value in style2id.values()}:
            default_style_id = 0
        default_style_name = next(
            (name for name, style_id in style2id.items() if int(style_id) == default_style_id),
            next(iter(style2id.keys())),
        )

        catalog.append(
            {
                "id": voice_id,
                "key": stem,
                "display_name": label,
                "model_path": f"Models/{stem}.aivmx",
                "hyper_parameters_path": f"Metadata/{stem}.hyper_parameters.json",
                "manifest_path": f"Metadata/{stem}.manifest.json",
                "style_vectors_path": f"Metadata/{stem}.style_vectors.npy",
                "speaker_id": int(spk2id[speaker_name]),
                "voicevox_speaker_id": 14,
                "speaker_name": speaker_name,
                "default_style_id": int(default_style_id),
                "default_style_name": default_style_name,
                "style_count": int(style_vectors.shape[0]),
                "sampling_rate": int(data.get("sampling_rate", 44100)),
                "hop_length": int(data.get("hop_length", 512)),
                "version": hyper_parameters.get("version"),
                "runtime": "aivis-style-bert-vits2-onnx-jp-extra",
                "platforms": ["ios", "android", "macos", "windows"],
            }
        )

    (args.output / "aivis_voices.json").write_text(
        json.dumps(
            {
                "schema_version": "2026-06-30",
                "default_voice_id": 1431611904,
                "voices": catalog,
            },
            ensure_ascii=False,
            indent=2,
        ),
        encoding="utf-8",
    )
    if args.prepare_runtime_layout:
        write_runtime_layout(args.output)
    print(f"Prepared {len(catalog)} Aivis voices in {args.output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
