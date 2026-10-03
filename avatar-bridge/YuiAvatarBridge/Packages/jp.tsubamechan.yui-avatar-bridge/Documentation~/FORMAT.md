# Unity Avatar Package ZIP schema 1

The package is an ordinary ZIP. It intentionally has no application-specific
filename extension.

```text
AvatarName_AvatarPackage.zip
  manifest.json
  FORMAT.txt
  payloads/
    avatar_windows.bundle
    avatar_macos.bundle
    avatar_android.bundle
    avatar_ios.bundle
```

`manifest.json` declares `format: unity-avatar-package` and
`schemaVersion: 1`. Each payload has a platform, relative filename, byte size,
and SHA-256. Only payloads selected at export time are present. The prefab
address is `avatar/prefab`. AnimationClip candidates use the addresses recorded
under `diagnostics.expressionClips`; each candidate also records its source path
and category (`facial_expression`, `facial_option`, `gesture`, `locomotion`,
`wardrobe`, or `animation`). Material diagnostics record the source shader and
whether Unity could resolve it at export time.

Keep the exported ZIP intact when transferring it to the app.
Consumers must reject absolute paths, path traversal, scripts,
executables, and payload hash mismatches.

AssetBundles are standard Unity non-code assets, but remain OS- and Unity-version
sensitive. Consumers must select the matching Windows, macOS, Android, or iOS
payload and a compatible Unity version. Compatibility must not be assumed in
either direction across Unity versions. Re-export the original avatar as VRM
when the app cannot load a legacy bundle.
