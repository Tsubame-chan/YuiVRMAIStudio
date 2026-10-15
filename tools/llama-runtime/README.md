# Platform runtime integration

Shared C# model management, prompt/history handling, cancellation, and a C ABI adapter call the platform's native llama.cpp build. The adapter source currently lives at `unity/Assets/Plugins/iOS/YuiLlamaBridge.cpp` for Unity's iOS compilation; it is portable C++17 and is also compiled into desktop plugins. Its directory name does not restrict it to iOS.

- iOS/iPadOS: official b11617 XCFramework, device/simulator slice chosen by Xcode, Metal enabled; linked/embedded by `YuiLlamaBuild`.
- macOS: universal bundle embedding the official macOS framework, prepared with `scripts/prepare_llama_runtime_macos.py`; Metal enabled.
- Windows x64: `scripts/prepare_llama_runtime_windows.ps1` builds the same adapter and pinned llama.cpp commit with MSVC/CMake. CPU is the baseline. `-Backend Vulkan` or `-Backend CUDA` requires the corresponding SDK/toolchain. Unity loads `YuiLlama.dll` and its native dependency DLLs from its Windows plugin directory. The ABI availability check refuses to advertise GGUF when the engine/dependencies are missing. Windows build and runtime acceptance must occur on Windows; Apple acceptance does not prove it.
- Android: future native build/JNI and device acceptance. Do not advertise GGUF support until that implementation is supplied. The existing model/file management is shared, while OS document access and inference packaging remain separate.

Model format support, accelerator selection, and verified execution are separate properties. GGUF is a file format, not a promise that any model runs on every OS. Experimental models remain opt-in. No additional engine should load its model merely because its library is bundled.

Windows acceptance must also check the MSVC runtime, optional CUDA redistributable dependencies, Vulkan driver support, and the exact packaged DLL set on a clean machine. The preparation script does not establish those runtime prerequisites or device acceptance. Build optional accelerator variants in separate clean build directories; do not mix unverified dependency DLLs into a distribution.

Apple archive SHA and source tag are recorded in `runtime.json`. Windows source is pinned to the official b11617 commit `14310190cd0a0c831fe949ac8cbedd8abdc90695`. Keep MIT notices in distributed builds. Generated native files require source/distribution audits before release.

Chat formatting uses the same-tag llama.cpp Jinja engine under `unity/Assets/Plugins/iOS/YuiLlamaJinja`, rather than the limited `llama_chat_apply_template` heuristic for serialized Jinja templates. `SOURCE.json` records upstream hashes and local cancellation/budget hooks. The adapter passes the actual BOS/EOS and system/user/assistant roles, avoids adding BOS twice, and requests non-thinking chat where the model template supports that switch. Models that force a reasoning prefill are normalized before display/speech. GGUF tool calling and vision projectors are not implemented. Matching model names does not imply matching quantization or thinking settings between LiteRT-LM and GGUF.

## Build prerequisites on Apple hosts

Before building an iOS or macOS Player from a Git checkout, run `python3 scripts/prepare_llama_runtime_macos.py` on a Mac with Xcode command line tools. It downloads the official b11617 XCFramework, verifies the pinned archive SHA-256, preserves framework symlinks, and builds the universal adapter. The generated `tools/llama-runtime/apple`, `unpacked`, and SDK archive are Git-ignored build inputs; the official debug-symbol archive exceeds GitHub's individual-file limit. The runtime manifest, source, licenses and preparation script are tracked. No model weights are downloaded by this step.
