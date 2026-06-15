# Alpha Release Checklist

Use this checklist for `v0.1.0-alpha.1` before publishing the public GitHub page or installer package.

## Build Artifacts

- Public app exists at `builds/YuiVRMAIStudio_PublicAlpha_v0.1.0-alpha.1/Yui VRM AI Studio.exe`.
- `YuiFilePickerHelper.exe` is beside the app exe.
- No `*_BurstDebugInformation_DoNotShip` folders are present in the public build output.
- The old private stable build `YuiPhysicalAICore_LocalAlpha` was not overwritten.

## Public Repository Safety

- `.env` is not included.
- `backend/data/*.db` is not included.
- `logs/` is not included.
- Private Yui/Kikyo avatar assets and scene objects are not included.
- Handoff notes, investigation prompts, local machine paths, and private workflow notes are not included.
- README and setup docs describe the release as a current Windows alpha and BYOK.

## Custom VRM Support

- The README says local `.vrm` import is supported for VRM 1.0 and VRM 0.x.
- The README says VRChat SDK avatars, Unity scenes, prefabs, `.unitypackage` files, and uploaded-only VRChat avatars are not directly supported in v0.1.
- The setup guide tells VRChat users to export or convert a separate VRM copy first.
- Finger pose polish is deferred to v0.2+; do not keep tuning direct finger-bone rotation for v0.1.

## Final Commands

From the public repository root:

```powershell
.\scripts\audit_distribution_release.ps1 -ProjectRoot .
```

Expected result:

```text
Distribution release audit passed
```

If runtime Unity code changes are made after this checklist, rebuild the relevant personal and public Windows alpha builds and rerun the public audit.

## Manual Smoke Test

1. Run `scripts\setup_backend_byok.ps1` in a fresh copy if possible.
2. Set `OPENAI_API_KEY` in `.env`.
3. Start `Start_Yui_Backend_And_VOICEVOX.bat`.
4. Launch `Yui VRM AI Studio.exe`.
5. Confirm UnityChan appears by default.
6. Send one text chat message.
7. Import a VRM 1.0 or VRM 0.x file through Settings -> Custom VRM.
8. Confirm the avatar loads, faces forward, stays grounded, and can lip-sync.
9. Stop services with Enter in the launcher window or `Stop_Yui_Backend_And_VOICEVOX.bat`.
