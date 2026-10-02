# Use your own avatar

[日本語](AVATAR_IMPORT.md) · [Help](HELP.md) · [README](../README.en.md)

Yui imports **VRM (.vrm)** files. Import an existing VRM directly, or export your Unity/VRChat avatar to VRM first.

## Import a VRM

1. Open Settings → Character → Load avatar.
2. Select the `.vrm` and wait for loading to finish. VRM 0.x and 1.0 are supported.
3. Set the character's name, personality and voice.

The app copies the file into its own storage. If your purchase ZIP contains a VRM, extract it first. Purchase ZIPs, FBX and unitypackage files cannot be imported directly.

For **an outfit change**, choose My characters → Change appearance and select another VRM. The character keeps its name, personality, voice and memories. Importing a new character instead gives it a separate identity and memory.

## Export from Unity or a VRChat project

Open the project you use for the avatar and prepare its outfit, colors and accessories. For a new purchase, follow the author's SDK/shader/model setup. Back up the project before adding conversion tools. You do not need to upload the avatar to VRChat.

For avatars using lilToon/Modular Avatar, use [NDMF VRM Exporter](https://github.com/hkrn/ndmf-vrm-exporter). Follow the [official installation and export guide](https://github.com/hkrn/ndmf-vrm-exporter/blob/main/docs~/usage.md): install through VCC/ALCOM, add the export-description component to the avatar root, set authors/metadata, then use the NDMF Console's VRM 1.0 export platform to save your file.

Export the appearance configured in Unity, rather than a temporary in-game outfit selection. Consult the tool's [compatibility guide](https://github.com/hkrn/ndmf-vrm-exporter/blob/main/docs~/compatibility.md) for supported shader/package configurations. A VRM produced by another tool also works; it does not need to be recreated specifically for Yui.

## Move the file to your device

On Mac/Windows, choose the exported VRM from Settings. For iPhone, copy it into Files using AirDrop from Mac, or iCloud Drive from Windows. USB transfer is available through [Apple Devices file sharing](https://support.apple.com/guide/devices-windows/mchl4bd77d3a/windows). Let cloud downloads finish before selecting the file. A PC Backend connection is not required.

## If appearance or motion differs

VRM conversion can change shading, gloss, outlines and secondary motion. VRChat outfit menus, grabbing interactions and custom scripts do not carry over.

- Missing clothing/colors: check the original Unity appearance and exporter shader settings.
- Missing blink/lip sync: check expression and blend-shape setup before exporting.
- Slow loading: baked textures can increase file size. Reduce mesh/texture complexity before exporting.

Use avatars you have permission to use. Report app/tool versions, errors and reproduction steps through Issues; do not publicly attach purchased model files.

Older Yui Avatar Bridge ZIPs use a separate compatibility path and need a payload for your OS. For new imports, use VRM. See the [legacy ZIP guide](YUI_AVATAR_BRIDGE_USER_TEST_GUIDE.md).
