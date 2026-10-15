#!/usr/bin/env python3
"""Prepare the pinned MIT llama.cpp Apple runtime and Unity macOS bundle."""
import hashlib
import json
import shutil
import subprocess
import urllib.request
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
TAG = 'b11617'
SHA = 'ee2d7d4c72a4e5c618ab794c36f1ad9ffd2a89f73f6cc91ab00539e87679a29b'
BASE = ROOT / 'tools/llama-runtime'
FRAMEWORK = BASE / 'apple/llama.xcframework'
URL = f'https://github.com/ggml-org/llama.cpp/releases/download/{TAG}/llama-{TAG}-xcframework.zip'

def main():
    if not FRAMEWORK.exists():
        archive = BASE / f'llama-{TAG}-xcframework.zip'
        BASE.mkdir(parents=True, exist_ok=True)
        if not archive.exists():
            urllib.request.urlretrieve(URL, archive)
        with archive.open('rb') as source:
            digest = hashlib.sha256()
            for chunk in iter(lambda: source.read(1024 * 1024), b''):
                digest.update(chunk)
            sha = digest.hexdigest()
        if sha != SHA:
            raise SystemExit('llama.cpp runtime checksum mismatch')
        with zipfile.ZipFile(archive) as package:
            for item in package.infolist():
                if '..' in Path(item.filename).parts or Path(item.filename).is_absolute():
                    raise SystemExit('Unsafe runtime archive path')
            # ditto preserves framework symlinks which ZipFile.extractall does not.
        subprocess.run(['/usr/bin/ditto', '-x', '-k', str(archive), str(BASE / 'unpacked')], check=True)
        FRAMEWORK.parent.mkdir(parents=True, exist_ok=True)
        shutil.copytree(BASE / 'unpacked/build-apple/llama.xcframework', FRAMEWORK, symlinks=True)
    manifest = BASE / 'runtime.json'
    manifest.write_text(json.dumps({'tag':TAG,'url':URL,'sha256':SHA,'license':'MIT'}, indent=2)+'\n')
    bundle = ROOT / 'unity/Assets/Plugins/macOS/YuiLlama.bundle'
    mac = FRAMEWORK / 'macos-arm64_x86_64/llama.framework'
    (bundle / 'Contents/MacOS').mkdir(parents=True, exist_ok=True)
    (bundle / 'Contents/Frameworks').mkdir(parents=True, exist_ok=True)
    embedded = bundle / 'Contents/Frameworks/llama.framework'
    if not embedded.exists():
        shutil.copytree(mac, embedded, symlinks=True)
    source = ROOT / 'unity/Assets/Plugins/iOS/YuiLlamaBridge.cpp'
    subprocess.run(['xcrun','clang++','-std=c++17','-dynamiclib','-arch','arm64','-arch','x86_64',
        '-mmacosx-version-min=14.0','-I',str(source.parent/'YuiLlamaJinja'),'-I',str(mac/'Headers'),'-F',str(mac.parent),'-framework','llama','-lobjc',
        '-Wl,-rpath,@loader_path/../Frameworks',str(source),*[str(p) for p in sorted((source.parent/'YuiLlamaJinja').rglob('*.cpp'))],'-o',str(bundle/'Contents/MacOS/YuiLlama')],check=True)
    (bundle / 'Contents/Info.plist').write_text('<?xml version="1.0"?><plist version="1.0"><dict><key>CFBundleExecutable</key><string>YuiLlama</string><key>CFBundleIdentifier</key><string>jp.tsubamechan.yui.llama</string><key>CFBundlePackageType</key><string>BNDL</string></dict></plist>')
    print(f'Prepared {TAG}: {bundle}')

if __name__ == '__main__':
    main()
