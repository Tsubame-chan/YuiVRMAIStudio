# Build variants

Public desktop releases target macOS and Windows. The currently published app is v0.2.0-beta.5. Mobile targets remain development builds pending device acceptance and store preparation. See [runtime support](RUNTIME_SUPPORT.md) for the actual OS-specific LLM, STT and TTS paths.

Build public candidates from a fresh sanitized source tree. Set `YUI_PROFILE_PUBLIC` and use the platform build entry point. The default avatar must be redistributable; imported user avatars must never become bundled release assets. Merely selecting a runtime profile does not remove assets from scenes or Resources.

Public builds use the generated `YuiBuildProfile` and public build tools. Keep platform settings (permissions, native dependencies, signing) separate from profile defaults and persisted user preferences. Do not copy a development build directory into a release.

Required checks are documented in [Public Player asset validation](PUBLIC_PLAYER_ASSET_VALIDATION.md): source audit, packed-asset guard, independent Player asset inspection, and native-device acceptance. A cross-build does not establish Windows, iOS or Android runtime compatibility.

No personal assets, local network defaults, API keys or private operational documents belong in a public repository or distribution. Source changes after beta.5 do not change the already published beta.5 binary.
