#!/usr/bin/env python3
"""Prepare the optional, data-only English voice ZIP and manifest entry.

Run with a Python environment containing numpy and onnx. The runtime never needs
Python. Inputs are separately licensed Kokoro v1.0 and Misaki data; no eSpeak data
or executable is packed. Publication is a separate step.
"""
from __future__ import annotations
import argparse
import hashlib
import json
from pathlib import Path
import shutil
import tempfile
import zipfile


def digest(path):
    with path.open("rb") as stream:
        sha = hashlib.sha256()
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            sha.update(block)
        return sha.hexdigest()


def used_domains(graph):
    import onnx
    result = {node.domain for node in graph.node}
    for node in graph.node:
        for attribute in node.attribute:
            if attribute.type == onnx.AttributeProto.GRAPH:
                result.update(used_domains(attribute.g))
            elif attribute.type == onnx.AttributeProto.GRAPHS:
                for nested in attribute.graphs:
                    result.update(used_domains(nested))
    return result


SELECTED_VOICES = ("af_bella", "af_nova", "af_nicole", "af_heart", "am_puck", "am_michael")


def prepare(model, voices, sources, output, asset_url=""):
    import numpy as np
    import onnx
    output.mkdir(parents=True, exist_ok=True)
    archive = output / "YuiVRMAIStudio_KokoroEnglish_v2.zip"
    if archive.exists():
        raise FileExistsError(f"Refusing to replace an existing pack: {archive}")
    with tempfile.TemporaryDirectory(prefix="yui-kokoro-pack-") as temp:
        root = Path(temp)
        graph = onnx.load(str(model))
        # The official export lists unused ai.onnx.ml opset 5. ORT 1.17 rejects
        # this declaration, although the actual graph uses standard opset 20.
        # Remove ONLY unused domain declarations, including nested graph checks;
        # no quantized weights/operators/opset semantics are changed.
        domains = used_domains(graph.graph)
        retained = [item for item in graph.opset_import if item.domain in domains]
        del graph.opset_import[:]
        graph.opset_import.extend(retained)
        onnx.checker.check_model(graph)
        onnx.save(graph, str(root / "kokoro-v1.0.int8.onnx"))
        with np.load(voices, allow_pickle=False) as bank:
            for name in SELECTED_VOICES:
                voice = bank[name]
                if voice.shape not in ((510, 1, 256), (510, 256)) or not np.isfinite(voice).all():
                    raise ValueError(f"Unexpected voice shape or values: {name}")
                voice.astype("<f4").tofile(root / (name + ".bin"))
        for source, target in (("us_gold.json", "us_gold.json"), ("us_silver.json", "us_silver.json"),
                               ("kokoro-config.json", "config.json"), ("misaki-LICENSE", "Misaki-LICENSE.txt"),
                               ("onnx-conversion-LICENSE", "ONNX-conversion-LICENSE.txt"),
                               ("kokoro-model-card.md", "MODEL_CARD.md")):
            shutil.copyfile(sources / source, root / target)
        shutil.copyfile(sources / "misaki-LICENSE", root / "Kokoro-Apache-2.0.txt")
        notice = """Kokoro v1.0 / Bella, Nova, Nicole, Heart, Puck and Michael voices: hexgrad, Apache-2.0.
Original: https://huggingface.co/hexgrad/Kokoro-82M
ONNX conversion/quantization: https://github.com/thewh1teagle/kokoro-onnx
Misaki US English pronunciation data: hexgrad, Apache-2.0.
Original: https://github.com/hexgrad/misaki
Yui modifications: unused ONNX domain declarations removed; six voice tensors
converted from the voice bank to raw float32. No model weight changes.
Kokoro model card training attribution: Koniwa tnc, CC BY 3.0,
https://github.com/koniwa/koniwa (tnc), https://creativecommons.org/licenses/by/3.0/;
SIWIS, CC BY 4.0, https://creativecommons.org/licenses/by/4.0/,
https://datashare.ed.ac.uk/handle/10283/2353.
This pack contains data only. No eSpeak NG, phonemizer, Python or executable.
"""
        (root / "NOTICE.txt").write_text(notice, encoding="utf-8")
        files = sorted(p.name for p in root.iterdir())
        provenance = {"schema_version": 1, "model": "Kokoro-v1.0-int8", "voices": list(SELECTED_VOICES), "default_voice": "af_bella",
                      "upstream_model_sha256": digest(model), "upstream_voice_bank_sha256": digest(voices),
                      "files": {name: {"sha256": digest(root / name), "size_bytes": (root / name).stat().st_size}
                                for name in files}}
        (root / "pack.json").write_text(json.dumps(provenance, indent=2) + "\n")
        files.append("pack.json")
        with zipfile.ZipFile(archive, "w", zipfile.ZIP_DEFLATED, compresslevel=6) as zipped:
            for name in files:
                info = zipfile.ZipInfo(name, (2026, 10, 5, 0, 0, 0))
                info.compress_type = zipfile.ZIP_DEFLATED
                info.external_attr = 0o100644 << 16
                zipped.writestr(info, (root / name).read_bytes())
        unpacked = sum((root / name).stat().st_size for name in files)
    entry = {"id": "kokoro-english-v1", "display_name": "Kokoro English · 4 female / 2 male voices",
             "kind": "kokoro_english_voice", "platforms": ["ios", "android", "macos", "windows"],
             "required_for": ["english_speech"], "optional": True, "version": "2",
             "filename": archive.name, "url": asset_url, "sha256": digest(archive),
             "size_bytes": archive.stat().st_size, "install_root": "YuiLocalAI/Kokoro", "installed_paths": files}
    (output / "kokoro-manifest-entry.json").write_text(json.dumps(entry, ensure_ascii=False, indent=2) + "\n")
    (output / "size-report.json").write_text(json.dumps({"archive_bytes": archive.stat().st_size,
        "installed_bytes": unpacked, "initial_bundle_model_bytes": 0,
        "asset_url_configured": bool(asset_url)}, indent=2) + "\n")
    return entry


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--model", type=Path, required=True)
    parser.add_argument("--voices", type=Path, required=True)
    parser.add_argument("--sources", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--asset-url", default="", help="Published HTTPS ZIP URL; omit for an unpublished candidate.")
    args = parser.parse_args()
    print(json.dumps(prepare(args.model, args.voices, args.sources, args.output, args.asset_url), indent=2))


if __name__ == "__main__":
    main()
