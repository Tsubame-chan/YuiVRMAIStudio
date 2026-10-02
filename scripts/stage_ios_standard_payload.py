#!/usr/bin/env python3
"""Stage reviewed E2B and standard VOICEVOX data into a fresh sanitized iOS source.

Run prepare_public_repository.py and its audit first. This does not create a
distributable build: packed-asset verification is still required after export.
"""
import argparse
import hashlib
import json
from pathlib import Path
import shutil


def digest(path):
    h = hashlib.sha256()
    with path.open('rb') as stream:
        for block in iter(lambda: stream.read(8 * 1024 * 1024), b''):
            h.update(block)
    return h.hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--destination-root', type=Path, required=True)
    args = parser.parse_args()
    source = Path(__file__).resolve().parents[1]
    destination = args.destination_root.resolve(strict=True)
    if destination == source or (destination / '.git').exists() or not destination.is_relative_to(source / 'builds'):
        raise SystemExit('Use a fresh sanitized source under builds, never canonical or public Git.')
    if not (destination / 'unity/Assets/App/Editor/YuiPublicBuildPrivacyGuard.cs').is_file():
        raise SystemExit('Generate and audit sanitized public source first.')
    relative = Path('unity/Assets/StreamingAssets/YuiLocalAI')
    model = relative / 'Models/gemma-4-E2B-it.litertlm'
    if (source / model).stat().st_size != 2588147712 or digest(source / model) != '181938105e0eefd105961417e8da75903eacda102c4fce9ce90f50b97139a63c':
        raise SystemExit('Standard E2B does not match the reviewed artifact.')
    model_dir = destination / relative / 'Models'
    if model_dir.exists() and any(model_dir.iterdir()):
        raise SystemExit('Refusing to modify an existing model payload; use fresh source.')
    voices = ['meimei_himari_1.vvm', 'metan_zundamon_0.vvm', 'kyushu_sora_2.vvm', 'sayo_15.vvm']
    files = [model] + [relative / 'Voicevox/Models' / voice for voice in voices]
    for folder in ['Voicevox/Licenses', 'Voicevox/open_jtalk_dic_utf_8-1.11']:
        files += [p.relative_to(source) for p in (source / relative / folder).rglob('*') if p.is_file() and not p.name.endswith('.meta')]
    evidence = []
    for rel in files:
        origin, target = source / rel, destination / rel
        if origin.is_symlink() or not origin.is_file():
            raise SystemExit(f'Missing regular source asset: {rel}')
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(origin, target)
        value = digest(origin)
        if digest(target) != value:
            raise SystemExit(f'Copy verification failed: {rel}')
        evidence.append({'path': str(rel), 'bytes': target.stat().st_size, 'sha256': value})
    output = destination / 'ios-standard-payload.json'
    output.write_text(json.dumps({'default_model': 'E2B', 'optional_apple_pack': 'yui-gemma-e4b-v1', 'initial_download': False, 'files': evidence}, indent=2) + '\n')
    print(f'Verified {len(evidence)} bundled files, {sum(p["bytes"] for p in evidence)} bytes; E4B remains external.')


if __name__ == '__main__':
    main()
