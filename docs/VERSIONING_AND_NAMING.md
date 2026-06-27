# Versioning And Naming

## Build Identity

Every build artifact must encode these fields:

- product: `YuiVRMAIStudio`
- profile: `Personal` or `Public`
- platform: `iOS`, `Android`, `macOS`, `Windows`
- architecture when useful: `arm64`, `x64`, `universal`
- channel/configuration: `Debug`, `Alpha`, `Beta`, `Release`
- semantic version: for example `v0.1.0-alpha.1`
- build number: monotonic per platform/profile when signing or store tooling needs it

Recommended artifact directory:

```text
artifacts/<profile>/<platform>/<version>/<configuration>/
```

Recommended artifact name:

```text
YuiVRMAIStudio_<Profile>_<Platform>_<Configuration>_<Version>[_Build<Number>]
```

Examples:

```text
YuiVRMAIStudio_Personal_iOS_Debug_v0.1.0-alpha.1_Build12
YuiVRMAIStudio_Public_Windows_Alpha_v0.1.0-alpha.1
YuiVRMAIStudio_Public_macOS_Alpha_v0.1.0-alpha.1
```

## Semantic Versioning

Use SemVer for user-facing app versions:

- `MAJOR`: incompatible project or data changes.
- `MINOR`: new user-facing features or platform support.
- `PATCH`: bug fixes and small behavior fixes.
- prerelease: `alpha.N`, `beta.N`, `rc.N`.

Examples:

- `0.1.0-alpha.1`: first BYOK alpha line.
- `0.1.0-alpha.2`: same feature line, follow-up fixes.
- `0.2.0-alpha.1`: new platform/profile behavior or larger feature set.

## Build Numbers

Use a monotonically increasing integer for Apple/Android build metadata. The build number is not a replacement for SemVer.

Recommended policy:

- Increment build number for every installed device build.
- Keep separate counters per bundle/package ID if store tooling requires it.
- Record the mapping in a dated status doc until automated.

## Bundle And Package IDs

Profiles must not share bundle/package IDs.

Examples:

- Personal iOS: `jp.tsubamechan.yuivrm.personal`
- Public iOS future: `jp.tsubamechan.yuivrm`
- Public Windows: app name may be public-safe, with no private defaults.

## Script Naming

Build scripts should be explicit:

```text
<verb>_<platform>_<profile>_<purpose>_<host>.<ext>
```

Examples:

- `export_ios_personal_unity_macos.sh`
- `build_ios_personal_xcode_macos.sh`
- `build_windows_public_alpha.ps1`
- `prepare_public_repository.py`

Avoid generic names for profile-sensitive behavior.

## Profile Defaults

Profile defaults belong in build/profile configuration, not scattered through gameplay code.

Personal may default to the owner's avatar and local backend preferences. Public must default to public-safe avatars, public-safe docs, and BYOK/local setup. Runtime user choices may override defaults through persistent preferences.
