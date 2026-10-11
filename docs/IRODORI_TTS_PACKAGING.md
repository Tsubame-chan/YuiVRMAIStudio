# Irodori TTS Optional Backend Data

VOICEVOX remains the standard voice. The current optional Backend models are **Irodori V4.1 Small MLX 8bit on Apple Silicon** and **V4.1 Small INT8 on Windows NVIDIA**. Mobile CoreML data is a separate runtime format.

The next-update source adds [background acquisition in the Backend Console](BACKEND_ASSET_DOWNLOADS.md). Published Desktop v0.2.5-beta.2 uses the [manual Windows package](IRODORI_TTS_WINDOWS_NVIDIA.md). For an existing external TTS server, see [Backend TTS setup](BACKEND_TTS_GUIDE.md).

## Acquisition and ownership

The built-in catalog pins model revisions, sizes and SHA-256 values. Downloaded files are verified before installation; archives are checked for unsafe paths. Model and dependency installation run outside the request handler. Download, verification, dependency installation and actual voice preparation are separate phases.

Mac uses a separate Python 3.12 environment with pinned binary dependencies, including mlx-audio 0.5.8. Earlier 0.4.4/0.4.7 wheels cannot load the bundled V4.1 model tensors used by this catalog. The tokenizer and codec are bundled; model inference runs without further Hugging Face downloads. The derived 40-step config keeps the downloaded files immutable.

Windows uses the authenticated v0.2.5-beta.2 NVIDIA package, Python 3.11 and its frozen CUDA dependencies. The Console installer does not rewrite `.env`. Managed servers bind only to loopback, on 41091 (Mac) or 41092 (Windows), and the Backend stops only processes it owns. Existing external servers and saved endpoints are independent.

Three synthetic reference voices generated with the PC V4.1 FP16 model are retained: bright natural, gentle friend and calm natural. Existing saved voices, credentials and selected providers are preserved. The default remains VOICEVOX until the user chooses an added voice.

## Distribution and verification

User-installed models, virtual environments, logs and caches under `data/addons` must stay out of public source and Backend bundles. New platform adapters should reuse the acquisition manager while retaining their own capability checks, install steps and real inference readiness checks.

The Irodori model and the packaged Windows codec declare MIT; their upstream README conditions and each dependency's own notices apply. Use the catalog's source/license links for the exact model. Optional audio postprocessors keep their separate license requirements.

The [June benchmark](IRODORI_TTS_BENCHMARK_20260626.md) is historical V3 evidence, not current installation guidance. It must not be used to restore obsolete model IDs, reduced-step settings or disabled sentence splitting.
