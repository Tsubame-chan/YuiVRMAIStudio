# Yui Avatar Bridge Architecture

Date: 2026-07-14
Status: exporter/ZIP and macOS runtime import validated on 2026-09-09; source-project roundtrip and per-device expression/outfit/physics acceptance remain open.

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

### Observable Webcam Motion Capture beta direction

The public beta page instructs users to import `WMCTool.unitypackage` into a
VCC project, select an avatar, and invoke `WMC Tool > Export Avatar for Webcam
Motion Capture`. The public package contains one proprietary Editor DLL. Without
decompiling it, observable assembly metadata shows use of Unity
`PrefabUtility.SaveAsPrefabAsset`, `BuildPipeline.BuildAssetBundles`, Windows and
macOS build targets, `VRCAvatarDescriptor`, five vowel mappings, eyelid mappings,
`VRCPhysBone`, `VRCPhysBoneCollider`, and a JSON `AvatarMetadataConfig` with
physics-node and collider-node records.

The defensible behavioral conclusion is that WMC exports a sanitized Unity
Prefab into platform AssetBundles and separately records the VRC-only metadata
its player needs. It does not rely on mandatory VRM conversion. Yui follows the
same high-level public interoperability pattern, but uses an independently
written implementation and adds explicit diagnostics, source non-mutation,
portable ZIP packaging, payload hashes, unsafe-entry rejection, expression clip
discovery, editable mappings, and published schema/version boundaries.

### Black-box run on 2026-07-14

The public tool was installed normally into a disposable Unity 2022.3.62f3
project containing VRChat SDK Avatars 3.9.0. The official VRChat Avatar Dynamics
Robot sample was selected through the tool's public UI. The UI required a
GameObject with both `VRCAvatarDescriptor` and `Animator`, exposed macOS and
Windows radio buttons, and saved one platform at a time. The macOS selection
created a 688 KB file with the proprietary `.wmcmac` extension. Its public file
header identified it as a UnityFS AssetBundle built by Unity 2022.3.62f3.

No DLL or generated payload was decompiled. The same Unity Editor refused to
load that generated file back through the ordinary `AssetBundle.LoadFromFile`
API, so no undocumented internal metadata layout is assumed. Only the visible
workflow and the generated file's outer format inform Yui's design.

The independently implemented Yui Bridge exported the same official sample
with these diagnostics: humanoid valid, 1 SkinnedMeshRenderer, 2 materials, 23
BlendShapes, 5/5 vowels, 6 expression clips, and 5 PhysBone chains. A single
standard ZIP successfully contained both Windows and macOS payloads. After an
observed staging-duplication defect was fixed, the ZIP contained exactly four
files (`FORMAT.txt`, `manifest.json`, and two payload bundles), and both payload
SHA-256 values matched the manifest.

### Runtime-load gate found on 2026-07-14

The untouched Kikyo validation exported a 95 MB standard ZIP with Windows,
macOS, Android, and iOS payloads. Its manifest recorded 15 renderers, 6 resolved
materials/shaders, 359 BlendShapes, 5/5 vowels, 55 referenced clips, and 25
PhysBone chains. All four payload sizes and SHA-256 hashes matched.

However, both the Robot and Kikyo macOS bundles were rejected by
`AssetBundle.LoadFromFile` in the Yui Unity 2022.3.62f3 project with an
incompatible-runtime error, even though the bundle header also reports
2022.3.62f3. Therefore payload generation and ZIP integrity are proven, but
runtime loading is not. Schema 1 must not be declared release-ready until this
same-version load failure is fixed and a built Yui Player completes a round
trip. Unity documents that AssetBundles are platform-specific and do not offer
forward compatibility; the release pipeline must additionally pin the exporter
and Yui Player Unity versions.

## Architecture decision

Use a separate VPM Editor package named `Yui Avatar Bridge`.

```text
User's VCC avatar project
        |
        | Yui Avatar Bridge (Editor only; knows VRC SDK)
        v
platform-native avatar package ZIP, with portable .vrm as an optional fallback
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

The first user validation path is Windows VCC because that is where the user's
working avatar project lives. The package is also an ordinary UPM-compatible
Editor package, so a macOS Unity 2022.3 project can install it from disk and use
the same exporter. A public VPM repository and `Add to VCC` link remain release
work; the current development test uses `Add package from disk...`.

## Export format phases

### Phase A: native avatar package ZIP MVP

The primary path preserves more of the user's Unity avatar than a mandatory VRM
conversion. The exporter clones and sanitizes the selected avatar, then builds
platform-specific non-code AssetBundles for Windows, macOS, Android, and iOS. A ZIP-compatible
standard `.zip` container stores those bundles together with hashes, diagnostics,
viseme mappings, expression-clip candidates, and PhysBone conversion data.

This is not the unmodified VCC prefab. Unity players cannot load project Prefabs
or scripts directly, AssetBundles cannot distribute scripts, and bundles are
platform-specific. VRC SDK components are therefore interpreted in the Editor
and removed from the runtime prefab. Yui loads only known data and implements
the corresponding runtime behavior itself.

Advantages:

- preserves meshes, textures, material/shader assets, bones, and AnimationClips;
- retains source mappings for VRC visemes, expressions, and PhysBones;
- supports higher-fidelity platform-native output than VRM alone;
- leaves the user's source scene and prefab unchanged.

Tradeoffs:

- a separate bundle is required for each target OS;
- Unity and shader compatibility must be validated against each Yui app build;
- VRC scripts, contacts, constraints, and PhysBone execution cannot run unchanged;
- the corresponding Unity Build Support module is required for every selected payload.

### Phase B: portable VRM fallback

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

### Implemented schema 1 container

```text
avatar-name.zip
  manifest.json
  FORMAT.txt
  payloads/
    avatar_windows.bundle
    avatar_macos.bundle
    avatar_android.bundle
    avatar_ios.bundle
```

`manifest.json` contains:

- format/schema version;
- exporter and compatible Yui versions;
- avatar display name and a stable ID without an absolute source path;
- source Unity version and prefab address;
- per-platform payload filename, byte size, and SHA-256;
- mapped five-vowel visemes;
- expression/AnimationClip candidates with source paths and facial/gesture/wardrobe categories;
- material and source-shader resolution diagnostics;
- PhysBone roots, affected-bone counts, colliders, and conversion parameters;
- user license acknowledgement;
- hashes for payload files.

The portable VRM payload remains an optional future addition for recovery or
opening the avatar in other VRM software. It is not required for the native
Windows/macOS/Android/iOS path.

## Export pipeline

1. Require a selected GameObject with `VRCAvatarDescriptor` and humanoid Animator.
2. Clone into a temporary export scene/folder; never mutate the user's source avatar.
3. Capture VRC visemes, expression AnimationClips, PhysBones, and colliders into versioned mappings before removing VRC components.
4. Remove every component except Transform, Animator, renderers, MeshFilter, and LODGroup; clear the Animator Controller from the runtime clone.
5. Build compressed AssetBundles for the selected Windows, macOS, Android, and/or iOS targets.
6. Write the versioned manifest and payload hashes into a standard ZIP.
7. Present an explicit completion path.
8. Delete temporary assets in a `finally` path, including after cancellation or exceptions.

Documented VRC/NDMF preprocessing, Modular Avatar resolution, shader fallback,
thumbnail generation, and automated Yui round-trip display validation are
future reliability work. They must not be implied by the schema 1 prototype.

## Export window

The Editor window should be task-oriented, not a field dump.

1. Avatar: selected object, thumbnail, humanoid status.
2. Compatibility: green/warning/error rows for bones, face, materials, PhysBones, size.
3. Output: avatar name, destination, and target-platform payload toggles.
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

The main action should accept `.vrm` and a standard avatar package `.zip` whose
`manifest.json` declares `format: unity-avatar-package`. A secondary
`VRChatから使う` action opens a three-step guide and the `Add to VCC` link.
Drag/drop should use the same importer. No Yui-only filename extension is required.

Imported private avatars stay in the user data directory and are never copied into public repositories, release assets, telemetry, or cloud storage.

## Security and licensing

- Never read from or upload to VRChat services on the user's behalf.
- Never modify the source avatar/project during export.
- Reject scripts, DLLs, executables, absolute paths, and path traversal entries in avatar package ZIPs.
- Treat all avatar files and textures as private local data.
- Show the avatar's known license metadata when available; do not imply that technical export grants legal permission.
- Require explicit confirmation that the user has rights for conversion and use outside VRChat.
- Use a clean-room implementation based on public APIs and documented behavior.
- If MPL-2.0 code from VRMConverterForVRChat is reused, isolate and publish modified MPL-covered files accordingly. Prefer UniVRM APIs and independently written orchestration for the MIT Yui codebase.

## Implementation plan

### Milestone 1: proof of export

- [x] Create a separate VPM/UPM package scaffold.
- [x] Select and validate the official VRChat Robot sample.
- [x] Export sanitized Windows and macOS native payloads inside a standard `.zip`.
- [x] Add validated matching-payload import to the Yui runtime.
- [ ] Load the package in current Windows and macOS Yui application builds.
- [ ] Compare bones, face, lip sync, materials, and physics against the source with a user-owned avatar.
- [x] Keep optional VRM 1.0 export as a portability fallback rather than a fidelity requirement.

### Milestone 2: reliable beta

- Add PhysBone/expression/material diagnostics.
- Test common lilToon and Poiyomi source materials through explicit fallbacks.
- Add round-trip validation and cleanup tests.
- Test five legally redistributable avatars of increasing complexity.
- Publish through a VPM community repository with `Add to VCC`.

### Milestone 3: first-class Yui flow

- Add avatar package ZIP import and the avatar library.
- Add `Test in Yui` handoff and clear import errors.
- Add user documentation with screenshots and a short video.
- Measure VCC install-to-successful-Yui-import completion.

## Acceptance criteria

- The exporter never changes the source prefab or scene.
- A supported avatar is exported and loaded in Yui without manual Blender work.
- Unsupported features are reported before export with visible consequences.
- Exported files contain no machine-local absolute paths or unrelated project assets.
- A failed/cancelled export leaves no temporary assets.
- The same standard avatar package ZIP opens in current Windows and macOS Yui builds.
- Public and private avatar assets never cross repository boundaries.

## Validation update — 2026-09-09

The macOS AssetBundle load blocker was traced to missing built-in module dependencies in the Bridge project. A build could produce a ZIP with valid SHA-256 while logging that AssetBundle support was disabled. The Bridge package now declares assetbundle, animation and jsonserialize modules, checks the required module before export, uses StrictMode and validates a host-platform staged bundle before publishing the ZIP.

A minimal primitive producer/consumer isolated the failure. After the fix, the SDK Robot sample and a privately owned unmodified avatar each loaded in an actual Unity 2022.3.62f3 macOS Yui Player: rendering, supported shaders, humanoid and five writable mapped vowel shapes passed. Initial native-avatar facing now uses the same 180-degree yaw as VRM; existing saved user transforms remain authoritative. Editor tests are not a substitute for this Player gate.

Windows cross-compilation succeeded; native Windows execution remains unverified. Audio-driven lipsync, expressions, clothing toggles, physics and arbitrary avatar compatibility remain separate gates. A customized baked avatar was identified and rendered locally, but that does not establish a VCC source → Bridge ZIP → Player round trip for its modified animation/clothing setup.

ZIP loading also rejects traversal segments, normalized duplicate paths, oversized manifests, invalid declared payload sizes and malformed SHA-256 values. Editor platform detection now uses the host platform even after cross-compiling for Windows.

See `PUBLIC_PLAYER_ASSET_VALIDATION.md` before creating any distributable Player.
