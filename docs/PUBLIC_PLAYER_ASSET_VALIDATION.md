# Public Player asset validation

Updated: 2026-09-09

A public runtime profile does not remove private scene references or Resources assets. Source-tree filtering and text audits alone do not prove a built Player is safe to distribute.

Build public candidates from a **fresh sanitized tree generated from canonical sources** with `scripts/prepare_public_repository.py`. Do not regenerate an existing working public checkout. Do not build release Players directly from a workspace containing private assets.

`YuiPublicBuildPrivacyGuard` runs for `YUI_PROFILE_PUBLIC`. Before a build it checks enabled-scene and Resources dependencies against approved asset roots. After a build it checks actual packed asset paths and writes `yui-public-asset-audit.json` next to the Player. Any unapproved path fails the build. Keep this evidence with the exact candidate. Adding a permitted source root requires explicit review; approved roots alone do not certify the contents of every file or its redistribution terms.

Release gates:

1. Generate a new sanitized tree from canonical sources and pass the distribution source audit before Unity creates caches.
2. Build the intended platform and require a successful packed-asset audit.
3. Independently inspect the exact Player's serialized object inventory, including inactive scene objects. A hidden avatar still ships if serialized.
4. Archive that exact Player and audit evidence; record its SHA-256.
5. Run native-platform installation and avatar-import checks before release.

On 2026-09-09, historical macOS release Players were found to contain private avatar assets despite passing source-level checks. Treat those artifacts as unsuitable for distribution. Source audit success must never be used as evidence that an independently built release binary is clean.
