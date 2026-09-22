"""Build a local VPM release candidate with its pinned VRM conversion dependencies.

No downloading, publishing, project migration or deletion is performed. VCC can
resolve the complete Yui -> VRM -> glTF chain from the generated listing. VRChat's
SDK remains an official external dependency and is never repackaged here.
"""
import argparse
import hashlib
import json
from pathlib import Path
import zipfile

UNIVRM_VERSION = "0.127.2"
REPO_URL = "https://raw.githubusercontent.com/Tsubame-chan/YuiVRMAIStudio/main/vpm/index.json"


def read_manifest(package):
    return json.loads((package / "package.json").read_text(encoding="utf-8"))


def write_package(package, manifest, output, additions=None):
    members = {}
    for path in sorted(package.rglob("*")):
        relative = path.relative_to(package)
        if path.is_symlink():
            raise ValueError(f"Package contains a symlink: {relative}")
        if not path.is_file() or any(part.startswith(".") or part in {"Samples~", "Tests", "Tests~", "Library", "Temp"} for part in relative.parts):
            continue
        if path.name.startswith("YuiBridgeGateCli"):
            continue
        members[relative.as_posix()] = path.read_bytes()
    members.update(additions or {})
    manifest = dict(manifest)
    manifest.pop("samples", None)  # Samples are deliberately not shipped in the conversion tool.
    members["package.json"] = (json.dumps(manifest, indent=2) + "\n").encode()
    archive = output / f"{manifest['name']}-{manifest['version']}.zip"
    with zipfile.ZipFile(archive, "w", zipfile.ZIP_DEFLATED) as stream:
        for name, content in sorted(members.items()):
            info = zipfile.ZipInfo(name, (2026, 9, 21, 0, 0, 0))
            info.external_attr = 0o100644 << 16
            info.compress_type = zipfile.ZIP_DEFLATED
            stream.writestr(info, content)
    digest = hashlib.sha256(archive.read_bytes()).hexdigest()
    archive.with_suffix(".zip.sha256").write_text(f"{digest}  {archive.name}\n")
    return dict(manifest, zipSHA256=digest)


def build_release(bridge, gltf, vrm, license_file, output, previous=None):
    manifest = read_manifest(bridge)
    dependencies = [(gltf, "com.vrmc.gltf"), (vrm, "com.vrmc.vrm")]
    # Validate everything before writing a seemingly complete release candidate.
    for path, name in dependencies:
        value = read_manifest(path)
        if value.get("name") != name or value.get("version") != UNIVRM_VERSION:
            raise ValueError(f"Expected {name} {UNIVRM_VERSION}: {path}")
    license_text = license_file.read_bytes()
    if b"VRM Consortium" not in license_text or b"Permission is hereby granted" not in license_text:
        raise ValueError("The pinned upstream UniVRM license is required.")
    if manifest.get("vpmDependencies", {}).get("com.vrmc.vrm") != UNIVRM_VERSION:
        raise ValueError("Bridge dependency must match the tested UniVRM version.")
    output.mkdir(parents=True, exist_ok=True)
    base = manifest["url"].rsplit("/", 1)[0]
    listing = json.loads(previous.read_text()) if previous and previous.is_file() else dict(
        name="Yui Avatar Bridge", id="jp.tsubamechan.yui.vpm", url=REPO_URL, author="Tsubame-chan", packages={})
    packages = listing.setdefault("packages", {})
    for path, name in dependencies:
        value = read_manifest(path)
        value["url"] = f"{base}/{name}-{UNIVRM_VERSION}.zip"
        value["license"] = "MIT"
        if name == "com.vrmc.vrm":
            value["vpmDependencies"] = {"com.vrmc.gltf": UNIVRM_VERSION}
        else:
            value.setdefault("dependencies", {})["com.unity.modules.imageconversion"] = "1.0.0"
        result = write_package(path, value, output, {"UNIVRM-LICENSE.txt": license_text,
            "YUI-PACKAGING-NOTICE.txt": b"Source: https://github.com/vrm-c/UniVRM/tree/v0.127.2\nYui packaging adds VPM dependency metadata and omits optional samples/tests. Runtime source is unchanged.\n"})
        packages.setdefault(name, {"versions": {}})["versions"][UNIVRM_VERSION] = result
    result = write_package(bridge, manifest, output)
    packages.setdefault(result["name"], {"versions": {}})["versions"][result["version"]] = result
    (output / "index.json").write_text(json.dumps(listing, indent=2) + "\n")
    return listing


def cached_package(root, name):
    for base in [root / "avatar-bridge/YuiAvatarBridge/Library/PackageCache", root / "unity/Library/PackageCache"]:
        for candidate in sorted(base.glob(name + "@*")):
            if read_manifest(candidate).get("version") == UNIVRM_VERSION:
                return candidate
    raise ValueError(f"Provide --{name.rsplit('.', 1)[-1]} from UniVRM {UNIVRM_VERSION}; no matching local package cache.")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--gltf", type=Path)
    parser.add_argument("--vrm", type=Path)
    args = parser.parse_args()
    root = Path(__file__).resolve().parent.parent
    bridge = root / "avatar-bridge/YuiAvatarBridge/Packages/jp.tsubamechan.yui-avatar-bridge"
    if not bridge.is_dir():
        bridge = root / "Packages/jp.tsubamechan.yui-avatar-bridge"
    previous = root / "avatar-bridge/vpm/index.json"
    if not previous.is_file(): previous = root / "vpm/index.json"
    listing = build_release(bridge, args.gltf or cached_package(root, "com.vrmc.gltf"), args.vrm or cached_package(root, "com.vrmc.vrm"),
        root / "avatar-bridge/third-party/UniVRM-LICENSE.txt", args.output, previous)
    print(f"Local candidate: {args.output / 'index.json'} ({len(listing['packages'])} packages). Not published.")


if __name__ == "__main__":
    main()
