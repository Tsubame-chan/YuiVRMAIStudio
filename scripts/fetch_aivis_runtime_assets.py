#!/usr/bin/env python3
"""Create or populate the Aivis runtime asset layout.

The default mode is conservative and only writes manifests/directories. Pass
explicit URLs to download large assets; this keeps licenses, versions, and
network access visible to the person running the command.
"""

from __future__ import annotations

import argparse
import json
import shutil
import urllib.request
import zipfile
from datetime import datetime, timezone
from pathlib import Path


ONNXRUNTIME_VERSION = "1.23.0"
DEFAULT_ANDROID_ORT_AAR_URL = (
    "https://repo1.maven.org/maven2/com/microsoft/onnxruntime/"
    f"onnxruntime-android/{ONNXRUNTIME_VERSION}/onnxruntime-android-{ONNXRUNTIME_VERSION}.aar"
)
DEFAULT_ORT_C_API_HEADER_URL = (
    "https://raw.githubusercontent.com/microsoft/onnxruntime/"
    f"rel-{ONNXRUNTIME_VERSION}/include/onnxruntime/core/session/onnxruntime_c_api.h"
)
DEFAULT_ORT_CXX_API_HEADER_URL = (
    "https://raw.githubusercontent.com/microsoft/onnxruntime/"
    f"rel-{ONNXRUNTIME_VERSION}/include/onnxruntime/core/session/onnxruntime_cxx_api.h"
)
DEFAULT_ORT_CXX_INLINE_HEADER_URL = (
    "https://raw.githubusercontent.com/microsoft/onnxruntime/"
    f"rel-{ONNXRUNTIME_VERSION}/include/onnxruntime/core/session/onnxruntime_cxx_inline.h"
)
DEFAULT_ORT_EP_C_API_HEADER_URL = (
    "https://raw.githubusercontent.com/microsoft/onnxruntime/"
    f"rel-{ONNXRUNTIME_VERSION}/include/onnxruntime/core/session/onnxruntime_ep_c_api.h"
)
DEFAULT_ORT_FLOAT16_HEADER_URL = (
    "https://raw.githubusercontent.com/microsoft/onnxruntime/"
    f"rel-{ONNXRUNTIME_VERSION}/include/onnxruntime/core/session/onnxruntime_float16.h"
)
DEFAULT_BERT_MODEL_URL = (
    "https://huggingface.co/tsukumijima/"
    "deberta-v2-large-japanese-char-wwm-onnx/resolve/main/model_fp16.onnx"
)
DEFAULT_BERT_TOKENIZER_URL = (
    "https://huggingface.co/tsukumijima/"
    "deberta-v2-large-japanese-char-wwm-onnx/resolve/main/tokenizer.json"
)
DEFAULT_BERT_EXTRA_FILES = (
    "config.json",
    "special_tokens_map.json",
    "tokenizer_config.json",
    "vocab.txt",
)
DEFAULT_BERT_EXTRA_BASE_URL = (
    "https://huggingface.co/tsukumijima/"
    "deberta-v2-large-japanese-char-wwm-onnx/resolve/main/"
)
DEFAULT_AIVIS_ENGINE_URL = (
    "https://raw.githubusercontent.com/Aivis-Project/AivisSpeech-Engine/"
    "master/voicevox_engine/tts_pipeline/style_bert_vits2_tts_engine.py"
)
DEFAULT_STYLE_BERT_VITS2_REVISION = "171f796f37651346bb5435afd75f2f6f3b335bb1"
DEFAULT_NATIVE_STYLE_RUNTIME_SOURCE = Path("unity/Assets/Plugins/NativeAivis/YuiAivisStyleBertRuntime.cpp")
VOICEVOX_CORE_VERSION = "0.16.4"
DEFAULT_ANDROID_VOICEVOX_CORE_URL = (
    "https://github.com/VOICEVOX/voicevox_core/releases/download/"
    f"{VOICEVOX_CORE_VERSION}/voicevox_core-android-arm64-{VOICEVOX_CORE_VERSION}.zip"
)
ANDROID_VOICEVOX_CORE_DIR_NAME = f"voicevox_core-android-arm64-{VOICEVOX_CORE_VERSION}"
ANDROID_ONNXRUNTIME_PLUGIN_PATH = Path(
    f"unity/Assets/Plugins/Android/onnxruntime-android-{ONNXRUNTIME_VERSION}.aar"
)
ANDROID_ONNXRUNTIME_SO_PLUGIN_PATH = Path(
    "unity/Assets/Plugins/Android/ONNXRuntime/arm64-v8a/libonnxruntime.so"
)
ANDROID_ONNXRUNTIME_ARCHIVE_PATH = Path(
    f"tools/tts/aivis-engine/optional-runtime-archive/android-onnxruntime-{ONNXRUNTIME_VERSION}/"
    f"onnxruntime-android-{ONNXRUNTIME_VERSION}.aar"
)
ANDROID_VOICEVOX_PLUGIN_ROOT = Path("unity/Assets/Plugins/Android/Voicevox")
ANDROID_VOICEVOX_ARCHIVE_PATH = Path(
    f"downloads/aivis-runtime/{ANDROID_VOICEVOX_CORE_DIR_NAME}.zip"
)
AIVIS_IOS_ONNXRUNTIME_FRAMEWORK_PATH = Path("unity/Assets/Plugins/iOS/Aivis/onnxruntime.framework")


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "--root",
        type=Path,
        default=Path("unity/Assets/StreamingAssets/YuiLocalAI/Aivis"),
        help="Aivis StreamingAssets root.",
    )
    parser.add_argument("--bert-model-url", default="", help="URL for Japanese BERT model_fp16.onnx.")
    parser.add_argument("--bert-tokenizer-url", default="", help="URL for Japanese BERT tokenizer.json.")
    parser.add_argument("--use-default-bert-urls", action="store_true")
    parser.add_argument("--onnxruntime-manifest-source", type=Path, default=None)
    parser.add_argument("--text-frontend-manifest-source", type=Path, default=None)
    parser.add_argument("--install-onnxruntime-android", action="store_true")
    parser.add_argument("--android-onnxruntime-aar-url", default=DEFAULT_ANDROID_ORT_AAR_URL)
    parser.add_argument(
        "--android-onnxruntime-plugin-path",
        type=Path,
        default=ANDROID_ONNXRUNTIME_PLUGIN_PATH,
    )
    parser.add_argument(
        "--android-onnxruntime-so-plugin-path",
        type=Path,
        default=ANDROID_ONNXRUNTIME_SO_PLUGIN_PATH,
    )
    parser.add_argument(
        "--ios-onnxruntime-framework-path",
        type=Path,
        default=AIVIS_IOS_ONNXRUNTIME_FRAMEWORK_PATH,
    )
    parser.add_argument("--install-onnxruntime-c-header", action="store_true")
    parser.add_argument("--onnxruntime-c-header-url", default=DEFAULT_ORT_C_API_HEADER_URL)
    parser.add_argument("--onnxruntime-cxx-api-header-url", default=DEFAULT_ORT_CXX_API_HEADER_URL)
    parser.add_argument("--onnxruntime-cxx-inline-header-url", default=DEFAULT_ORT_CXX_INLINE_HEADER_URL)
    parser.add_argument("--onnxruntime-ep-c-api-header-url", default=DEFAULT_ORT_EP_C_API_HEADER_URL)
    parser.add_argument("--onnxruntime-float16-header-url", default=DEFAULT_ORT_FLOAT16_HEADER_URL)
    parser.add_argument("--install-bert-extras", action="store_true")
    parser.add_argument("--install-text-frontend-from-voicevox", action="store_true")
    parser.add_argument("--install-voicevox-core-android", action="store_true")
    parser.add_argument("--android-voicevox-core-url", default=DEFAULT_ANDROID_VOICEVOX_CORE_URL)
    parser.add_argument(
        "--android-voicevox-archive-path",
        type=Path,
        default=ANDROID_VOICEVOX_ARCHIVE_PATH,
    )
    parser.add_argument(
        "--android-voicevox-plugin-root",
        type=Path,
        default=ANDROID_VOICEVOX_PLUGIN_ROOT,
    )
    parser.add_argument(
        "--voicevox-open-jtalk-dict-path",
        type=Path,
        default=Path("unity/Assets/StreamingAssets/YuiLocalAI/Voicevox/open_jtalk_dic_utf_8-1.11"),
    )
    parser.add_argument("--install-style-runtime-reference", action="store_true")
    parser.add_argument("--aivis-engine-source-url", default=DEFAULT_AIVIS_ENGINE_URL)
    parser.add_argument(
        "--native-style-runtime-source",
        type=Path,
        default=DEFAULT_NATIVE_STYLE_RUNTIME_SOURCE,
    )
    parser.add_argument("--download", action="store_true", help="Download URL-backed assets.")
    return parser.parse_args()


def write_manifest(path: Path, payload: dict[str, object]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(payload, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def copy_if_present(source: Path | None, target: Path) -> bool:
    if source is None:
        return False
    if not source.is_file():
        raise FileNotFoundError(source)
    target.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(source, target)
    return True


def download_file(url: str, target: Path) -> None:
    target.parent.mkdir(parents=True, exist_ok=True)
    with urllib.request.urlopen(url) as response, target.open("wb") as output:
        shutil.copyfileobj(response, output)


def file_ready(path: Path, minimum_bytes: int = 1) -> bool:
    return path.is_file() and path.stat().st_size >= minimum_bytes


def directory_ready(path: Path) -> bool:
    return path.is_dir() and any(path.iterdir())


def validate_android_aar(path: Path) -> bool:
    if not path.is_file():
        return False
    try:
        with zipfile.ZipFile(path) as archive:
            names = set(archive.namelist())
        return "jni/arm64-v8a/libonnxruntime.so" in names
    except zipfile.BadZipFile:
        return False


def android_voicevox_core_ready(root: Path) -> bool:
    core_dir = root / ANDROID_VOICEVOX_CORE_DIR_NAME
    return (
        file_ready(core_dir / "include" / "voicevox_core.h", 1024)
        and file_ready(core_dir / "lib" / "libvoicevox_core.so", 1024)
    )


def install_android_onnxruntime_plugin(aar: Path, plugin_aar: Path, plugin_so: Path) -> bool:
    if not validate_android_aar(aar):
        return False

    plugin_aar.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(aar, plugin_aar)

    plugin_so.parent.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(aar) as archive:
        with archive.open("jni/arm64-v8a/libonnxruntime.so") as source, plugin_so.open("wb") as target:
            shutil.copyfileobj(source, target)

    return file_ready(plugin_aar, 1024) and file_ready(plugin_so, 1024)


def install_android_voicevox_core(zip_path: Path, plugin_root: Path) -> bool:
    if not zip_path.is_file():
        return False
    plugin_root.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(zip_path) as archive:
        archive.extractall(plugin_root)
    return android_voicevox_core_ready(plugin_root)


def ready_platform_manifest(component: str, ready_platforms: list[str], payload: dict[str, object]) -> dict[str, object]:
    if not ready_platforms:
        status = "missing"
    elif set(ready_platforms) >= {"ios", "android", "macos", "windows"}:
        status = "ready"
    else:
        status = "platform_ready"
    return {
        "component": component,
        "status": status,
        "ready_platforms": sorted(ready_platforms),
        **payload,
    }


def main() -> int:
    args = parse_args()
    root = args.root
    runtime = root / "Runtime"
    generated_at = datetime.now(timezone.utc).isoformat()

    onnx_dir = runtime / "ONNXRuntime"
    android_aar = ANDROID_ONNXRUNTIME_ARCHIVE_PATH
    ort_header = onnx_dir / "include" / "onnxruntime_c_api.h"
    ort_cxx_header = onnx_dir / "include" / "onnxruntime_cxx_api.h"
    ort_cxx_inline_header = onnx_dir / "include" / "onnxruntime_cxx_inline.h"
    ort_ep_header = onnx_dir / "include" / "onnxruntime_ep_c_api.h"
    ort_float16_header = onnx_dir / "include" / "onnxruntime_float16.h"
    if args.download and args.install_onnxruntime_android:
        download_file(args.android_onnxruntime_aar_url, android_aar)
    if args.download and args.install_onnxruntime_c_header:
        download_file(args.onnxruntime_c_header_url, ort_header)
        download_file(args.onnxruntime_cxx_api_header_url, ort_cxx_header)
        download_file(args.onnxruntime_cxx_inline_header_url, ort_cxx_inline_header)
        download_file(args.onnxruntime_ep_c_api_header_url, ort_ep_header)
        download_file(args.onnxruntime_float16_header_url, ort_float16_header)
    android_ort_plugin_ready = False
    if args.install_onnxruntime_android:
        android_ort_plugin_ready = install_android_onnxruntime_plugin(
            android_aar,
            args.android_onnxruntime_plugin_path,
            args.android_onnxruntime_so_plugin_path,
        )

    onnx_manifest = runtime / "ONNXRuntime" / "manifest.json"
    if not copy_if_present(args.onnxruntime_manifest_source, onnx_manifest):
        ready_platforms: list[str] = []
        ios_ort_ready = file_ready(args.ios_onnxruntime_framework_path / "onnxruntime", 1024) and file_ready(ort_header, 1024)
        if ios_ort_ready:
            ready_platforms.append("ios")
        if validate_android_aar(android_aar) and android_ort_plugin_ready and file_ready(ort_header, 1024):
            ready_platforms.append("android")

        write_manifest(
            onnx_manifest,
            ready_platform_manifest(
                "onnxruntime",
                ready_platforms,
                {
                    "version": ONNXRUNTIME_VERSION,
                    "ios": {
                        "source": f"Microsoft.ML.OnnxRuntime {ONNXRUNTIME_VERSION} NuGet ios-arm64 framework",
                        "path": str(args.ios_onnxruntime_framework_path),
                        "ready": ios_ort_ready,
                    },
                    "android": {
                        "source": args.android_onnxruntime_aar_url,
                        "archive_path": str(android_aar),
                        "plugin_path": str(args.android_onnxruntime_plugin_path),
                        "plugin_so_path": str(args.android_onnxruntime_so_plugin_path),
                        "ready": validate_android_aar(android_aar) and android_ort_plugin_ready,
                    },
                    "c_api_header": {
                        "source": args.onnxruntime_c_header_url,
                        "path": str(ort_header),
                        "ready": file_ready(ort_header, 1024),
                    },
                    "cxx_api_headers": {
                        "source": [
                            args.onnxruntime_cxx_api_header_url,
                            args.onnxruntime_cxx_inline_header_url,
                            args.onnxruntime_ep_c_api_header_url,
                            args.onnxruntime_float16_header_url,
                        ],
                        "paths": [
                            str(ort_cxx_header),
                            str(ort_cxx_inline_header),
                            str(ort_ep_header),
                            str(ort_float16_header),
                        ],
                        "ready": file_ready(ort_cxx_header, 1024)
                        and file_ready(ort_cxx_inline_header, 1024)
                        and file_ready(ort_ep_header, 1024)
                        and file_ready(ort_float16_header, 1024),
                    },
                    "generated_at": generated_at,
                },
            ),
        )

    style_reference = runtime / "StyleBertVits2" / "Reference" / "style_bert_vits2_tts_engine.py"
    if args.download and args.install_style_runtime_reference:
        download_file(args.aivis_engine_source_url, style_reference)
    native_style_runtime_ready = file_ready(args.native_style_runtime_source, 1024)
    style_ready_platforms = ["android", "ios"] if native_style_runtime_ready else []

    write_manifest(
        runtime / "StyleBertVits2" / "manifest.json",
        ready_platform_manifest(
            "style_bert_vits2_runtime",
            style_ready_platforms,
            {
            "note": "Native Style-Bert-VITS2 execution core is present when ready_platforms includes the target. Build/device audio verification is still required before release.",
            "native_requirements": {
                "aivis_onnx_session": native_style_runtime_ready,
                "japanese_bert_onnx_session": native_style_runtime_ready,
                "style_vectors_npy_loader": native_style_runtime_ready,
                "japanese_text_frontend": native_style_runtime_ready,
                "wav_output": native_style_runtime_ready,
            },
            "native_source": str(args.native_style_runtime_source),
            "reference_sources": {
                "aivis_engine": str(style_reference),
                "style_bert_vits2_revision": DEFAULT_STYLE_BERT_VITS2_REVISION,
            },
            "generated_at": generated_at,
            },
        ),
    )

    text_frontend_dir = runtime / "JapaneseTextFrontend"
    if args.install_text_frontend_from_voicevox:
        text_frontend_dir.mkdir(parents=True, exist_ok=True)
    android_voicevox_zip = args.android_voicevox_archive_path
    if args.download and args.install_voicevox_core_android:
        download_file(args.android_voicevox_core_url, android_voicevox_zip)
    android_text_frontend_ready = False
    if args.install_voicevox_core_android:
        android_text_frontend_ready = install_android_voicevox_core(
            android_voicevox_zip,
            args.android_voicevox_plugin_root,
        )

    text_frontend_manifest = runtime / "JapaneseTextFrontend" / "manifest.json"
    if not copy_if_present(args.text_frontend_manifest_source, text_frontend_manifest):
        ready_platforms = []
        dict_ready = directory_ready(args.voicevox_open_jtalk_dict_path)
        if dict_ready:
            ready_platforms.append("ios")
        if dict_ready and android_text_frontend_ready:
            ready_platforms.append("android")
        write_manifest(
            text_frontend_manifest,
            ready_platform_manifest(
                "japanese_text_frontend",
                ready_platforms,
                {
                    "source": "existing_voicevox_core_open_jtalk",
                    "open_jtalk_dict_path": str(args.voicevox_open_jtalk_dict_path),
                    "dictionary_ready": dict_ready,
                    "android_voicevox_core": {
                        "source": args.android_voicevox_core_url,
                        "archive_path": str(android_voicevox_zip),
                        "plugin_root": str(args.android_voicevox_plugin_root),
                        "ready": android_text_frontend_ready,
                    },
                    "note": "iOS and Android can reuse VOICEVOX Core/OpenJTalk analysis. macOS/Windows still need equivalent native frontend wiring or helper-process adapters.",
                    "generated_at": generated_at,
                },
            ),
        )

    bert_model_url = args.bert_model_url
    bert_tokenizer_url = args.bert_tokenizer_url
    if args.use_default_bert_urls:
        bert_model_url = bert_model_url or DEFAULT_BERT_MODEL_URL
        bert_tokenizer_url = bert_tokenizer_url or DEFAULT_BERT_TOKENIZER_URL

    if args.download:
        if bert_model_url:
            download_file(bert_model_url, runtime / "JapaneseBert" / "model_fp16.onnx")
        if bert_tokenizer_url:
            download_file(bert_tokenizer_url, runtime / "JapaneseBert" / "tokenizer.json")
        if args.install_bert_extras:
            for filename in DEFAULT_BERT_EXTRA_FILES:
                download_file(DEFAULT_BERT_EXTRA_BASE_URL + filename, runtime / "JapaneseBert" / filename)

    bert_model = runtime / "JapaneseBert" / "model_fp16.onnx"
    bert_tokenizer = runtime / "JapaneseBert" / "tokenizer.json"
    bert_ready = file_ready(bert_model, 1024 * 1024) and file_ready(bert_tokenizer, 1024)

    write_manifest(
        runtime / "JapaneseBert" / "manifest.json",
        {
            "component": "japanese_bert",
            "status": "ready" if bert_ready else "missing",
            "model_url": bert_model_url,
            "tokenizer_url": bert_tokenizer_url,
            "downloaded": bert_ready,
            "model_path": str(bert_model),
            "tokenizer_path": str(bert_tokenizer),
            "extra_files": list(DEFAULT_BERT_EXTRA_FILES),
            "revision": "5e5cc2b628d083d0a815c4d4a4c3fe84a414f8ed",
            "generated_at": generated_at,
        },
    )

    print(f"Prepared Aivis runtime layout under {runtime}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
