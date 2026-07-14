# Yui Avatar Bridge Architecture

Date: 2026-07-14
Status: approved direction; implementation pending

## Goal

Let a user who owns a VRChat avatar Unity project export a Yui-compatible copy from that existing VCC project with a few explicit steps.

This does **not** download avatars from a VRChat account, VRChat CDN, or another user's world. It only processes assets already present in a Unity project controlled by the user.

## Evidence

The [Webcam Motion Capture beta page](https://webcammotioncapture.info/ja/beta.php) documents the exact user flow we need to match: install `WMCTool.unitypackage` into a VRChat Creator Companion project, select `WMC Tool > Export Avatar for Webcam Motion Capture`, and save the selected avatar in a loadable form.

Inspection of the publicly downloadable package showed an Editor-only assembly. Observable type and string metadata indicate that it reads the selected `VRCAvatarDescriptor`, skinned meshes, viseme/blink mappings, VRC PhysBones/colliders, creates a temporary sanitized prefab, writes metadata, and builds platform-targeted Unity assets. This is a behavioral inference, not source code availability. Yui must not copy, redistribute, or derive code from that proprietary assembly.

Open implementations prove the conversion path is feasible:

- [VRMConverterForVRChat](https://github.com/esperecyan/VRMConverterForVRChat) converts a VRChat avatar to VRM, including expressions, humanoid normalization, materials, and VRM spring-bone mapping. It is MPL-2.0, so code reuse must preserve MPL obligations.
- [UniVRM](https://github.com/vrm-c/UniVRM) is MIT-licensed and provides the VRM 0.x/1.0 import/export foundation already used by Yui.
- [un-avatar](https://github.com/usagi/un-avatar) demonstrates the architectural principle of keeping Unity/VRC interpretation in the exporter and the runtime format independent of VRC SDK.
- [Avalab Toolkit](https://github.com/avalabai/toolkit) is an open example of sanitizing an avatar, generating temporary export assets and thumbnails, and producing a runtime package.

## Architecture decision

Use a separate VPM Editor package named `Yui Avatar Bridge`.

```text
User's VCC avatar project
        |
        | Yui Avatar Bridge (Editor only; knows VRC SDK)
        v
portable .vrm initially, then .yuiavatar
        |
        | normal file import
        v
Yui VRM AI Studio (runtime; does not depend on VRC SDK)
```

The Yui application must not add VRC SDK as a runtime dependency. This keeps Windows/macOS/mobile builds smaller, avoids SDK version coupling, and maintains the current public/private asset boundary.

## Distribution

VCC supports community repositories and an `Add to VCC` link. VPM packages are Unity Package Manager-compatible and declare VRChat dependencies in `vpmDependencies`.

Recommended package identity:

```json
{
  "name": "jp.tsubamechan.yui-avatar-bridge",
  "displayName": "Yui Avatar Bridge",
  "version": "0.1.0",
  "unity": "2022.3",
  "vpmDependencies": {
    "com.vrchat.avatars": "3.x"
  }
}
```

Use the official VPM package/listing templates and release automation described in [Creating a Package Listing](https://vcc.docs.vrchat.com/guides/create-listing/). Keep Editor code in an `Editor` assembly and use asmdefs as required by [Converting Assets to a VPM Package](https://vcc.docs.vrchat.com/guides/convert-unitypackage/).

VCC GUI officially supports Windows 10/11. macOS has partial CLI functionality, so the first supported exporter target should be Windows VCC. A UPM/manual package path can serve advanced macOS Unity users later.

## Export format phases

### Phase A: VRM MVP

Export a `.vrm` file that the existing `YuiRuntimeVrmImporter` can load today.

Advantages:

- one cross-platform file;
- no Yui runtime change required for the first prototype;
- compatible with existing Windows/macOS/iOS/Android UniVRM paths;
- easy to inspect and test independently.

Tradeoffs:

- some VRChat shaders, components, contacts, constraints, and expression behavior cannot map exactly;
- PhysBone conversion and material fallback require validation;
- avatar creator license may prohibit conversion or use outside VRChat.

### Phase B: `.yuiavatar` container

Add a versioned ZIP-compatible container while retaining a portable VRM payload.

```text
avatar-name.yuiavatar
  manifest.json
  avatar.vrm
  thumbnail.png
  diagnostics.json
  mappings/
    expressions.json
    physics.json
```

`manifest.json` minimum fields:

- format/schema version;
- exporter and compatible Yui versions;
- avatar display name and stable local ID;
- source project/avatar identifiers that do not expose absolute local paths;
- VRM version;
- texture and file sizes;
- mapped/unmapped feature summary;
- user license acknowledgement timestamp;
- hashes for payload files.

Yui then imports the container into an avatar library, stores a thumbnail, and presents diagnostics before activation.

### Phase C: Native fallback only if needed

A platform-specific Unity AssetBundle can preserve features VRM cannot represent, but it couples the export to Unity version, render pipeline, shaders, platform, and app build. It should be an optional fallback, not the canonical format.

## Export pipeline

1. Require a selected GameObject with `VRCAvatarDescriptor` and humanoid Animator.
2. Clone into a temporary export scene/folder; never mutate the user's source avatar.
3. Invoke supported VRC/NDMF preprocess callbacks only through documented APIs and record what changed.
4. Remove Editor-only, networked, executable, missing-script, and unsupported components from the clone.
5. Resolve Modular Avatar/wardrobe output where legally and technically possible.
6. Map humanoid bones, head/eyes, visemes, blink, expressions, first-person/look-at data, PhysBones, and colliders.
7. Convert supported materials to VRM MToon/PBR; report every fallback.
8. Validate texture dimensions, total size, blendshapes, bounds, and required bones.
9. Generate a neutral preview thumbnail.
10. Export to a temporary file, re-import with UniVRM, and fail if round-trip validation does not pass.
11. Present `Export complete`, `Open folder`, and `Test in Yui` actions.
12. Delete temporary assets in a `finally` path, including after cancellation or exceptions.

## Export window

The Editor window should be task-oriented, not a field dump.

1. Avatar: selected object, thumbnail, humanoid status.
2. Compatibility: green/warning/error rows for bones, face, materials, PhysBones, size.
3. Output: avatar name, destination, VRM 1.0 default.
4. Rights: checkbox confirming the user owns or is permitted to convert and use the avatar outside VRChat.
5. Primary action: `Export for Yui`.

Warnings must name the visible consequence, for example: “Hair PhysBone cannot be mapped and will not move in Yui,” rather than only naming a component type.

## Yui application changes

### Avatar library

Replace the current slot-oriented import with a visual library:

- thumbnail and display name;
- source type (`Bundled`, `VRM`, `Yui Avatar Bridge`);
- active/importing/broken/update-available state;
- import, rename, replace source, reveal file, and delete;
- diagnostics and original license note.

### Import entry

The main action should accept `.vrm` and `.yuiavatar`. A secondary `VRChatから使う` action opens a three-step guide and the `Add to VCC` link. Drag/drop should use the same importer.

Imported private avatars stay in the user data directory and are never copied into public repositories, release assets, telemetry, or cloud storage.

## Security and licensing

- Never read from or upload to VRChat services on the user's behalf.
- Never modify the source avatar/project during export.
- Reject scripts, DLLs, executables, absolute paths, and path traversal entries in `.yuiavatar`.
- Treat all avatar files and textures as private local data.
- Show the avatar's known license metadata when available; do not imply that technical export grants legal permission.
- Require explicit confirmation that the user has rights for conversion and use outside VRChat.
- Use a clean-room implementation based on public APIs and documented behavior.
- If MPL-2.0 code from VRMConverterForVRChat is reused, isolate and publish modified MPL-covered files accordingly. Prefer UniVRM APIs and independently written orchestration for the MIT Yui codebase.

## Implementation plan

### Milestone 1: proof of export

- Create a separate VPM package repository/scaffold.
- Select and validate one simple VRC avatar.
- Export VRM 1.0 through UniVRM.
- Import it into the current Yui runtime on Windows and macOS.
- Compare bones, face, lip sync, materials, and physics against the source.

### Milestone 2: reliable beta

- Add PhysBone/expression/material diagnostics.
- Test common lilToon and Poiyomi source materials through explicit fallbacks.
- Add round-trip validation and cleanup tests.
- Test five legally redistributable avatars of increasing complexity.
- Publish through a VPM community repository with `Add to VCC`.

### Milestone 3: first-class Yui flow

- Add `.yuiavatar` and avatar library.
- Add `Test in Yui` handoff and clear import errors.
- Add user documentation with screenshots and a short video.
- Measure VCC install-to-successful-Yui-import completion.

## Acceptance criteria

- The exporter never changes the source prefab or scene.
- A supported avatar is exported and loaded in Yui without manual Blender work.
- Unsupported features are reported before export with visible consequences.
- Exported files contain no machine-local absolute paths or unrelated project assets.
- A failed/cancelled export leaves no temporary assets.
- The same portable avatar opens in current Windows and macOS Yui builds.
- Public and private avatar assets never cross repository boundaries.
