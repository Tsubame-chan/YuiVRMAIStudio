# Build variants

Public desktop releases target macOS and Windows. See [runtime support](RUNTIME_SUPPORT.md) for current distribution and the actual OS-specific LLM, STT and TTS paths; source versions and published binaries may differ. Internal build/review status belongs in the [project handoff](handoffs/active/20261003/PROJECT_HANDOFF.md).

Build public candidates from a fresh sanitized source tree. Set `YUI_PROFILE_PUBLIC` and use the platform build entry point. The default avatar must be redistributable; imported user avatars must never become bundled release assets. Merely selecting a runtime profile does not remove assets from scenes or Resources.

Public builds use the generated `YuiBuildProfile` and public build tools. Keep platform settings (permissions, native dependencies, signing) separate from profile defaults and persisted user preferences. Do not copy a development build directory into a release.

Required checks are documented in [Public Player asset validation](PUBLIC_PLAYER_ASSET_VALIDATION.md): source audit, packed-asset guard, independent Player asset inspection, and native-device acceptance. A cross-build does not establish Windows, iOS or Android runtime compatibility.

No personal assets, machine-specific network defaults, API keys or private operational documents belong in a public repository or distribution. Changing source does not update an already published binary.

Apple source builds require the pinned native GGUF SDK preparation described in [platform runtime integration](../tools/llama-runtime/README.md#build-prerequisites-on-apple-hosts). The generated SDK is not stored in Git.
