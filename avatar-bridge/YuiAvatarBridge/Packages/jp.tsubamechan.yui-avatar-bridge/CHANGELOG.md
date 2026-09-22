# Changelog

## 0.2.0 (local candidate; not published)
- Standard export is a portable VRM 1 file; no OS target selection or platform build modules.
- Pinned UniVRM dependencies are provided in the VPM release candidate.
- Descriptor-first mouth/blink conversion, conservative known-name fallback, current mesh customization, basic materials and spring motion.
- Process Modular Avatar on a copy through NDMF, verify its success, and refuse unprocessed unsupported pipelines.
- Keep legacy ZIP loading/export API for compatibility. Windows VCC and mobile device acceptance remain pending.

## 0.1.1
- Fix missing built-in AssetBundle module dependencies and validate host payloads before ZIP finalization.
- Add installation and mobile transfer instructions.
- Keep mobile targets opt-in; desktop targets remain selected when installed.
- Native Windows/mobile round-trip, expressions and runtime physics remain experimental.
