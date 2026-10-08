#!/usr/bin/env python3
"""Archive an assembled, pinned optional Windows NVIDIA Irodori V4.1 package."""
import argparse
import hashlib
import json
from pathlib import Path
import zipfile


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--staging-dir', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    root = args.staging_dir.resolve()
    model = root / 'model/int8-weight-only/model.safetensors'
    with model.open('rb') as stream:
        assert hashlib.file_digest(stream, 'sha256').hexdigest() == '1fb6394b52c11329fd19d6c6f388a1e224721752a32aad906cae8653f5b25dcc'
    for path in ['codec/weights.pth', 'runtime/uv.exe', 'server/uv.lock', 'Install_Irodori_V4.bat', 'Start_Irodori_V4.bat']:
        assert (root / path).is_file(), path
    aliases = json.loads((root / 'voices/voices.json').read_text())
    assert list(aliases) == ['bright_natural', 'gentle_friend', 'calm_natural']
    rows = []
    for path in sorted(root.rglob('*')):
        relative = path.relative_to(root)
        if '.cache' in relative.parts or '__pycache__' in relative.parts or path.name == 'files.json':
            continue
        if path.is_symlink():
            raise ValueError(f'Symlink is not permitted: {relative}')
        if not path.is_file():
            continue
        if path.name == '.env' or '.venv' in relative.parts or '.git' in relative.parts:
            raise ValueError(f'Private/runtime state is not permitted: {relative}')
        with path.open('rb') as stream:
            digest = hashlib.file_digest(stream, 'sha256').hexdigest()
        rows.append({'path': relative.as_posix(), 'bytes': path.stat().st_size, 'sha256': digest})
    metadata = {
        'schema': 1,
        'model': 'Aratako/Irodori-TTS-v4.1-Small-Quantized/int8-weight-only',
        'model_revision': 'ef04e6c3ba56138ae23e86a2eabc004f76990e37',
        'codec_revision': '47376ee24834d7a05a48ebabfe3cde29b3c5e214',
        'server_commit': '61012c760f22f7b4a6c21c5c5f8f9e148120b6f9',
        'upstream_runtime_commit': '89f9d8fbd4d51ea019867ee1197725ede1df13c5',
        'uv_version': '0.12.23',
        'native_windows_verified': False,
        'files': rows,
    }
    (root / 'files.json').write_text(json.dumps(metadata, indent=2) + '\n')
    args.output.parent.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(args.output, 'w', zipfile.ZIP_DEFLATED, compresslevel=6) as archive:
        for row in rows + [{'path': 'files.json'}]:
            archive.write(root / row['path'], 'YuiIrodoriV4/' + row['path'])
    with args.output.open('rb') as stream:
        digest = hashlib.file_digest(stream, 'sha256').hexdigest()
    args.output.with_suffix(args.output.suffix + '.sha256').write_text(f'{digest}  {args.output.name}\n')
    print(json.dumps({'files': len(rows), 'bytes': args.output.stat().st_size, 'sha256': digest}))


if __name__ == '__main__':
    main()
