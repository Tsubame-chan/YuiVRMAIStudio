#!/usr/bin/env python3
"""Create an Apple-hosted model archive locally; never uploads or submits it."""
import argparse, hashlib, json, os, subprocess
from pathlib import Path

MODELS = {
    'e4b': ('gemma-4-E4B-it.litertlm', 3659530240, '0b2a8980ce155fd97673d8e820b4d29d9c7d99b8fa6806f425d969b145bd52e0'),
}

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--model', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--evaluate-only', action='store_true')
    args = parser.parse_args()
    source = args.model.resolve(strict=True)
    filename, size, digest = MODELS['e4b']
    if source.name != filename or source.stat().st_size != size:
        raise SystemExit('The model name or size does not match the reviewed official E4B artifact.')
    with source.open('rb') as f:
        actual = hashlib.file_digest(f, 'sha256').hexdigest()
    if actual != digest: raise SystemExit('The model checksum differs from the reviewed artifact.')
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=True)
    archive = output / 'yui-gemma-e4b-v1.aar'
    if archive.exists(): raise SystemExit('Refusing to overwrite an existing asset archive.')
    manifest = {
        'assetPackID': 'yui-gemma-e4b-v1',
        'downloadPolicy': {'onDemand': {}},
        'fileSelectors': [{'fileSource': filename, 'fileDestination': 'YuiLocalAI/Models/' + filename}],
        'platforms': ['iOS'], 'sourceRoot': os.path.relpath(source.parent, output),
    }
    manifest_path = output / 'manifest.json'
    manifest_path.write_text(json.dumps(manifest, indent=2) + '\n')
    (output / 'source-evidence.json').write_text(json.dumps(dict(filename=filename, size_bytes=size, sha256=actual,
        source='https://huggingface.co/litert-community/gemma-4-E4B-it-litert-lm/tree/2eee7ac325f20eb8c9ac1d0e972f7c84663062da'), indent=2)+'\n')
    # Keep paths relative as required by Apple's manifest format.
    subprocess.run(['xcrun','ba-package','evaluate',manifest_path.name],cwd=output,check=True)
    if not args.evaluate_only:
        # The packager resolves a relative output against sourceRoot, not cwd.
        subprocess.run(['xcrun','ba-package','package',manifest_path.name,'--output-path',str(archive)],cwd=output,check=True)
        if not archive.is_file(): raise SystemExit('Apple did not create the expected archive.')
        with archive.open('rb') as f:
            archive_hash = hashlib.file_digest(f, 'sha256').hexdigest()
        (output / 'archive-evidence.json').write_text(json.dumps(dict(
            filename=archive.name, size_bytes=archive.stat().st_size, sha256=archive_hash,
            model_sha256=digest, asset_pack_id=manifest['assetPackID'], download_policy='onDemand'), indent=2)+'\n')
    print(manifest_path if args.evaluate_only else archive)

if __name__ == '__main__': main()
