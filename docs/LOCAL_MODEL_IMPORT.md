# Additional on-device chat models

Standard Gemma 4 E2B/E4B remain the supported model choices, subject to their documented device requirements. Additional models are optional experiments chosen by the user. A short reply must repeat a random verification code to check loading, generation and input handling; it does not guarantee quality, speed, stability, or suitability for another device.

In Settings, open **On-device AI · Choose or add model**, open the **Add** tab, select a file, and import it. The app checks that the model can repeat an input verification code locally before registering the file. Choose **Use this model** explicitly after import. No PC, Backend, or model server is required on supported builds.

- Single-file GGUF v3 instruction/chat models run through the platform's bundled llama.cpp runtime. Apple build integration uses Metal. The published Windows app does not bundle the GGUF runtime and accepts compatible LiteRT-LM only. A Windows x64 CPU/Vulkan/CUDA preparation path exists in source; native runtime acceptance remains outstanding. Android GGUF is future work. Split GGUF, separate vision projectors, and unsupported chat templates are not handled.
- Compatible `.litertlm` chat models use the existing LiteRT-LM runtime. File format alone does not establish model compatibility.
- Hugging Face is a distribution source. Download an appropriate GGUF or LiteRT-LM file and choose it through Files. Bare `.safetensors` files are not directly executable by this feature.

Model weights are imported after installation and are not added to the app's distributed bundle. The original file remains in its location. Import uses a streaming copy and SHA-256 checksum, requires spare storage, and can be cancelled. iCloud files may first need to download. Large models may generate slowly, stop, or cause the app to close. Mobile builds suggest starting with a small quantized model; the UI does not display device specifications, benchmark scores, or a universal parameter-size recommendation.

The importer currently accepts files up to **32 GiB** and reserves **512 MiB** of spare storage. This is a file/import limit, not a device-memory recommendation. Keep room for the app's model copy as well as the original Files-provider download; LiteRT-LM may also need an additional copy for its runtime cache.

Changing the chat model keeps the character, memory, voice configuration, and standard speech recognition. Select the standard model to return to it. To remove an imported model, first select another model, then open its details and remove the app's copy. Standard files and the original selected file are kept.

The [published desktop apps](DOWNLOADS.md) include the feature with the platform limits above. The released iOS app does not yet include model import; the iOS implementation is a development-build feature. See [device compatibility](RUNTIME_SUPPORT.md) for publication status. Device acceptance is recorded separately.

The popup has **Models**, **Add**, and **Settings** tabs. Model rows show selection separately from actions; only the list scrolls. Closing returns to the existing settings draft. Model settings retain unsaved edits across tabs, separately for each model and Talk/Work; closing the model panel discards unsaved edits. On desktop, Cmd/Ctrl+Shift+M opens the panel, Cmd/Ctrl+1–3 changes tabs, Cmd/Ctrl+O opens the picker, and Cmd/Ctrl+S invokes the primary action.


The bundled LiteRT-LM adapters currently use **0.17.0**. Some newer `.litertlm` artifacts require a newer engine even when they load successfully. For example, the [Qwen3-4B mixed-int4 artifact](https://huggingface.co/litert-community/Qwen3-4B) requires at least 0.18.0: the same file ignored the input under the Mac 0.17.0 SDK and followed it under an isolated 0.18.0 SDK test. The import check rejects that input mismatch; the app does not upgrade its engine automatically.

The current GGUF adapter uses the embedded Jinja chat template, disables thinking when the template supports that option, and filters reasoning from models that prefill it. Additional GGUF models support text chat; separate vision/audio projectors and tool calling are not implemented. LiteRT-LM thinking is an import option for compatible models. These settings, quantization and runtime version affect comparisons: a format name alone cannot guarantee identical answers.

Development checks on an iPhone 16 Pro confirmed import and chat for Gemma 4 E2B Q4_K_M GGUF (3.46 GB) and Qwen3-4B Q4_K_M GGUF (2.50 GB), English Kokoro playback state, and switching back to the standard E2B LiteRT-LM model. Mac checks also exercised the same GGUF files through the native runtime. This is evidence for those particular files and builds, not a compatibility promise for every file from the same model family or another OS.
